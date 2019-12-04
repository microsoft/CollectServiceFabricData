// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData
{
    public class KustoConnection : Instance
    {
        private const int _maxMessageCount = 32;
        private static readonly CustomTaskManager _kustoTasks = new CustomTaskManager(true);
        private readonly SynchronizedList<string> _messageList = new SynchronizedList<string>();
        private readonly TimeSpan _messageTimeToLive = new TimeSpan(0, 1, 0, 0);
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private int _failureCount;
        private SynchronizedList<string> _ingestedUris = new SynchronizedList<string>();
        private IEnumerator<string> _ingestionQueueEnumerator;
        private Task _monitorTask;
        private IEnumerator<string> _tempContainerEnumerator;
        private int _totalBlobIngestQueued;
        private int _totalBlobIngestResults;
        public KustoEndpointInfo Endpoint { get; private set; }

        public void AddFile(FileObject fileObject)
        {
            Log.Debug("enter");

            if (!CanInjest(fileObject.RelativeUri))
            {
                Log.Warning($"file already ingested. skipping: {fileObject.RelativeUri}");
                return;
            }

            if (Config.KustoUseBlobAsSource)
            {
                IngestSingleFile(fileObject);
            }
            else
            {
                IngestMultipleFiles(FileMgr.ProcessFile(fileObject));
            }
        }

        public bool Complete()
        {
            try
            {
                Log.Info("finished. cancelling", ConsoleColor.White);
                _tokenSource.Cancel();
                _monitorTask.Wait();
                _monitorTask.Dispose();

                Log.Info($"return: total kusto ingests:{_totalBlobIngestQueued} success:{_failureCount == 0} ");
                TotalErrors += _failureCount;
                return _failureCount == 0;
            }
            catch (Exception ex)
            {
                Log.Exception($"{ex}");
                return false;
            }
        }

        public bool Connect()
        {
            Endpoint = new KustoEndpointInfo();
            Endpoint.Authenticate();
            _tempContainerEnumerator = Endpoint.IngestionResources.TempStorageContainers.GetEnumerator();
            _ingestionQueueEnumerator = Endpoint.IngestionResources.IngestionQueues.GetEnumerator();

            if (Config.IsKustoPurgeRequested())
            {
                Purge();
                return false;
            }
            else if (Config.KustoRecreateTable)
            {
                PurgeMessages(Endpoint.TableName);

                if (!Endpoint.DropTable(Endpoint.TableName))
                {
                    return false;
                }
            }
            else if (Config.Unique && Endpoint.HasTable(Endpoint.TableName))
            {
                _ingestedUris.AddRange(Endpoint.Query($"['{Endpoint.TableName}']|distinct RelativeUri"));
            }

            // monitor for new files to be uploaded
            if (_monitorTask == null)
            {
                _monitorTask = Task.Run((Action)QueueMonitor, _tokenSource.Token);
            }

            return true;
        }

        public void IngestMultipleFiles(FileObjectCollection fileObjectCollection)
        {
            fileObjectCollection.ForEach(x => IngestSingleFile(x));
        }

        public void IngestSingleFile(FileObject fileObject)
        {
            string blobUriWithSas = null;
            string ingestionMapping = SetIngestionMapping(fileObject);

            if (!_tempContainerEnumerator.MoveNext())
            {
                _tempContainerEnumerator.Reset();
                _tempContainerEnumerator.MoveNext();
            }

            if (!_ingestionQueueEnumerator.MoveNext())
            {
                _ingestionQueueEnumerator.Reset();
                _ingestionQueueEnumerator.MoveNext();
            }

            if (Config.KustoUseBlobAsSource)
            {
                blobUriWithSas = $"{fileObject.FileUri}{Config.SasEndpointInfo.SasToken}";
            }
            else
            {
                string blobName = Path.GetFileName(fileObject.FileUri);
                blobUriWithSas = UploadFileToBlobContainer(fileObject, _tempContainerEnumerator.Current, fileObject.NodeName, blobName);
            }

            PostMessageToQueue(_ingestionQueueEnumerator.Current, PrepareIngestionMessage(blobUriWithSas, fileObject.Length, ingestionMapping));
        }

        public IEnumerable<CloudQueueMessage> PopTopMessagesFromQueue(string queueUriWithSas, int count = _maxMessageCount)
        {
            List<string> messages = Enumerable.Empty<string>().ToList();
            CloudQueue queue = new CloudQueue(new Uri(queueUriWithSas));
            IEnumerable<CloudQueueMessage> messagesFromQueue = queue.GetMessages(count);

            foreach (CloudQueueMessage m in messagesFromQueue)
            {
                messages.Add(m.AsString);
            }

            return messagesFromQueue;
        }

        public void PostMessageToQueue(string queueUriWithSas, KustoIngestionMessage message)
        {
            Log.Info($"post: {queueUriWithSas}", ConsoleColor.Magenta);
            _totalBlobIngestQueued++;

            CloudQueue queue = new CloudQueue(new Uri(queueUriWithSas));
            CloudQueueMessage queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(message));

            OperationContext context = new OperationContext()
            {
                ClientRequestID = message.Id,
            };

            queue.AddMessage(queueMessage, _messageTimeToLive, null, null, context);
            _messageList.Add(message.Id);
            Log.Info($"queue message id: {message.Id}");
        }

        public KustoIngestionMessage PrepareIngestionMessage(string blobUriWithSas, long blobSizeBytes, string ingestionMapping)
        {
            string id = Guid.NewGuid().ToString();

            KustoIngestionMessage message = new KustoIngestionMessage()
            {
                Id = id,
                BlobPath = blobUriWithSas,
                RawDataSize = Convert.ToInt32(blobSizeBytes),
                DatabaseName = Endpoint.DatabaseName,
                TableName = Endpoint.TableName,
                RetainBlobOnSuccess = !Config.KustoUseBlobAsSource,
                Format = FileExtensionTypesEnum.csv.ToString(),
                FlushImmediately = true,
                ReportLevel = 2,
                ReportMethod = 0,
                AdditionalProperties = new KustoAdditionalProperties()
                {
                    authorizationContext = Endpoint.IdentityToken,
                    compressed = Config.KustoCompressed,
                    csvMapping = ingestionMapping
                }
            };

            return message;
        }

        public void Purge()
        {
            if (Config.KustoPurge.ToLower() == "true" & Endpoint.HasTable(Config.KustoTable))
            {
                PurgeMessages(Endpoint.TableName);
                Endpoint.DropTable(Endpoint.TableName);
            }
            else if (Config.KustoPurge.ToLower().StartsWith("list"))
            {
                List<string> results = new List<string>();

                if (Config.KustoPurge.ToLower().Split(' ').Length > 1)
                {
                    results = Endpoint.Query($".show tables | where TableName contains {Config.KustoPurge.ToLower().Split(' ')[1]} | project TableName");
                }
                else
                {
                    results = Endpoint.Query(".show tables | project TableName");
                }

                Log.Info($"current table list:", results);
            }
            else if (Endpoint.HasTable(Config.KustoPurge))
            {
                PurgeMessages(Config.KustoPurge);
                Endpoint.DropTable(Config.KustoPurge);
            }
            else
            {
                Log.Error($"invalid purge option:{Config.KustoPurge}. should be 'true' or 'list' or table name to drop");
            }
        }

        public void RemoveMessageFromQueue(string queueUriWithSas, CloudQueueMessage message)
        {
            try
            {
                CloudQueue queue = new CloudQueue(new Uri(queueUriWithSas));
                queue.DeleteMessage(message);
                Log.Debug($"Removed message from queue:", message);
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
            }
        }

        public string UploadFileToBlobContainer(FileObject fileObject, string blobContainerUri, string containerName, string blobName)
        {
            Log.Info($"uploading: {fileObject.Stream.Get().Length} bytes to {fileObject.FileUri} to {blobContainerUri}", ConsoleColor.Magenta);
            Uri blobUri = new Uri(blobContainerUri);
            BlobRequestOptions blobRequestOptions = new BlobRequestOptions()
            {
                RetryPolicy = new IngestRetryPolicy(),
                ParallelOperationThreadCount = Config.Threads,
            };

            CloudBlobContainer blobContainer = new CloudBlobContainer(blobUri);
            CloudBlockBlob blockBlob = blobContainer.GetBlockBlobReference(blobName);

            if (Config.UseMemoryStream)
            {
                _kustoTasks.TaskAction(() => blockBlob.UploadFromStreamAsync(fileObject.Stream.Get(), null, blobRequestOptions, null).Wait()).Wait();
                fileObject.Stream.Close();
            }
            else
            {
                _kustoTasks.TaskAction(() => blockBlob.UploadFromFileAsync(fileObject.FileUri, null, blobRequestOptions, null).Wait()).Wait();
            }

            Log.Info($"uploaded: {fileObject.FileUri} to {blobContainerUri}", ConsoleColor.DarkMagenta);
            return $"{blockBlob.Uri.AbsoluteUri}{blobUri.Query}";
        }

        private bool CanInjest(string relativeUri)
        {
            if (!Config.Unique)
            {
                return true;
            }

            string cleanUri = Regex.Replace(relativeUri, $"\\.?\\d*?({ZipExtension}|{TableExtension})", "");
            return !_ingestedUris.Any(x => x.Contains(cleanUri));
        }

        private void PurgeMessages(string tableName)
        {
            Log.Info($"dropping ingestion messages for table:{tableName}");

            while (true)
            {
                IEnumerable<CloudQueueMessage> successes = PopTopMessagesFromQueue(Endpoint.IngestionResources.SuccessNotificationsQueue);

                foreach (CloudQueueMessage success in successes)
                {
                    KustoSuccessMessage message = JsonConvert.DeserializeObject<KustoSuccessMessage>(success.AsString);
                    Log.Debug("success:", message);

                    if (message.Table.Equals(tableName))
                    {
                        RemoveMessageFromQueue(Endpoint.IngestionResources.SuccessNotificationsQueue, success);
                    }
                }
                if (successes.Count() < _maxMessageCount)
                {
                    break;
                }
            }

            while (true)
            {
                IEnumerable<CloudQueueMessage> errors = PopTopMessagesFromQueue(Endpoint.IngestionResources.FailureNotificationsQueue);

                foreach (CloudQueueMessage error in errors)
                {
                    KustoSuccessMessage message = JsonConvert.DeserializeObject<KustoSuccessMessage>(error.AsString);
                    Log.Debug("error:", message);

                    if (message.Table.Equals(tableName))
                    {
                        RemoveMessageFromQueue(Endpoint.IngestionResources.FailureNotificationsQueue, error);
                    }
                }
                if (errors.Count() < _maxMessageCount)
                {
                    break;
                }
            }
        }

        private void QueueMonitor()
        {
            while (!_tokenSource.IsCancellationRequested | _messageList.Any())
            {
                Thread.Sleep(ThreadSleepMs100);

                // read success notifications
                while (true)
                {
                    IEnumerable<CloudQueueMessage> successes = PopTopMessagesFromQueue(Endpoint.IngestionResources.SuccessNotificationsQueue);

                    foreach (CloudQueueMessage success in successes)
                    {
                        KustoSuccessMessage message = JsonConvert.DeserializeObject<KustoSuccessMessage>(success.AsString);
                        Log.Debug("success:", message);

                        if (_messageList.Exists(x => x.Equals(message.IngestionSourceId)))
                        {
                            _messageList.Remove(message.IngestionSourceId);
                            RemoveMessageFromQueue(Endpoint.IngestionResources.SuccessNotificationsQueue, success);
                            _totalBlobIngestResults++;
                            Log.Info($"Ingestion completed total:({_totalBlobIngestResults}/{_totalBlobIngestQueued}): {JsonConvert.DeserializeObject(success.AsString)}", ConsoleColor.Green);
                        }
                        else if (message.SucceededOn + _messageTimeToLive < DateTime.Now)
                        {
                            Log.Warning($"cleaning stale message", message);
                            RemoveMessageFromQueue(Endpoint.IngestionResources.SuccessNotificationsQueue, success);
                        }
                    }
                    if (successes.Count() < _maxMessageCount)
                    {
                        break;
                    }
                }

                while (true)
                {
                    // read failure notifications
                    IEnumerable<CloudQueueMessage> errors = PopTopMessagesFromQueue(Endpoint.IngestionResources.FailureNotificationsQueue);

                    foreach (CloudQueueMessage error in errors)
                    {
                        KustoErrorMessage message = JsonConvert.DeserializeObject<KustoErrorMessage>(error.AsString);
                        Log.Debug("error:", message);

                        if (_messageList.Exists(x => x.Equals(message.IngestionSourceId)))
                        {
                            _messageList.Remove(message.IngestionSourceId);
                            RemoveMessageFromQueue(Endpoint.IngestionResources.FailureNotificationsQueue, error);
                            _totalBlobIngestResults++;
                            _failureCount++;
                            Log.Error($"Ingestion error total:({_failureCount}): {JsonConvert.DeserializeObject(error.AsString)}");
                        }
                        else if (message.FailedOn + _messageTimeToLive < DateTime.Now)
                        {
                            Log.Warning($"cleaning stale message", message);
                            RemoveMessageFromQueue(Endpoint.IngestionResources.FailureNotificationsQueue, error);
                        }
                    }
                    if (errors.Count() < _maxMessageCount)
                    {
                        break;
                    }
                }
            }

            Log.Info($"exiting {_messageList.Count()}", _messageList);
        }

        private string SetIngestionMapping(FileObject fileObject)
        {
            string ingestionJsonString = null;

            switch (Config.FileType)
            {
                case FileTypesEnum.counter:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = Config.ResourceUri,
                        SetConstants = Config.KustoUseBlobAsSource,
                    }.CounterSchema());

                    break;
                
                case FileTypesEnum.exception:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = Config.ResourceUri,
                        SetConstants = Config.KustoUseBlobAsSource,
                    }.ExceptionSchema());

                    break;

                case FileTypesEnum.setup:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = Config.ResourceUri,
                        SetConstants = Config.KustoUseBlobAsSource,
                    }.SetupSchema());

                    break;

                case FileTypesEnum.table:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = Config.ResourceUri
                    }.TableSchema());

                    break;

                case FileTypesEnum.trace:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = Config.ResourceUri,
                        SetConstants = Config.KustoUseBlobAsSource,
                    }.TraceSchema());

                    break;
            }

            Log.Debug($"Ingestion Mapping: {ingestionJsonString}");
            return ingestionJsonString;
        }
    }
}