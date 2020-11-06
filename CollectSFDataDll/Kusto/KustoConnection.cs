// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using CollectSFData.DataFile;
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

namespace CollectSFData.Kusto
{
    public class KustoConnection : Constants
    {
        private Instance _instance = Instance.Singleton();
        private ConfigurationOptions Config => _instance.Config;
        private const int _maxMessageCount = 32;
        private readonly CustomTaskManager _kustoTasks = new CustomTaskManager(true);
        private readonly SynchronizedList<string> _messageList = new SynchronizedList<string>();
        private readonly TimeSpan _messageTimeToLive = new TimeSpan(0, 1, 0, 0);
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private SynchronizedList<string> _failIngestedUris = new SynchronizedList<string>();
        private int _failureCount;
        private DateTime _failureQueryTime;
        private string _ingestCursor = "''";
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

            if (!CanIngest(fileObject.RelativeUri))
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
                IngestMultipleFiles(_instance.FileMgr.ProcessFile(fileObject));
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
                IngestResourceIdKustoTableMapping();

                if (_failureCount > 0)
                {
                    _instance.TotalErrors += _failureCount;
                    Log.Error($"Ingestion error total:({_failureCount})");
                }

                Log.Info($"return: total kusto ingests:{_totalBlobIngestQueued} success:{_failureCount == 0} ");
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
            _failureQueryTime = _instance.StartTime.ToUniversalTime();
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

            PostMessageToQueue(_ingestionQueueEnumerator.Current, PrepareIngestionMessage(blobUriWithSas, fileObject.Length, ingestionMapping), fileObject);
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

        public void PostMessageToQueue(string queueUriWithSas, KustoIngestionMessage message, FileObject fileObject)
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

            if (Config.KustoUseIngestMessage)
            {
                _messageList.Add(message.Id);
            }
            else
            {
                _messageList.Add(fileObject.RelativeUri);
            }

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
                ReportLevel = Config.KustoUseIngestMessage ? 2 : 1, //(int)IngestionReportLevel.FailuresAndSuccesses, // 2 FailuresAndSuccesses, 0 failures, 1 none
                ReportMethod = Convert.ToInt32(!Config.KustoUseIngestMessage), //(int)IngestionReportMethod.Table, // 0 queue, 1 table, 2 both
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

        private bool CanIngest(string relativeUri)
        {
            if (!Config.Unique)
            {
                return true;
            }

            string cleanUri = Regex.Replace(relativeUri, $"\\.?\\d*?({ZipExtension}|{TableExtension})", "");
            return !_ingestedUris.Any(x => x.Contains(cleanUri));
        }

        private void IngestResourceIdKustoTableMapping()
        {
            if (_ingestedUris.Any() && Config.FileType == FileTypesEnum.trace)
            {
                // Fetch resource ID from ingested traces
                var results = Endpoint.Query($"['{Endpoint.TableName}']" +
                    $" | where Type == 'InfrastructureService.RestClientHelper'" +
                    $" | take 1");

                if (results.Any())
                {
                    Regex pattern = new Regex(@"resourceId\W+?(/[A-Za-z0-9./-]+)");
                    Match match = pattern.Match(results.FirstOrDefault());
                    Config.ResourceUri = match.Groups[1].Value;
                    Log.Info($"ResourceID: {Config.ResourceUri}");
                }
            }

            if (!string.IsNullOrWhiteSpace(Config.ResourceUri))
            {
                var metaDatatableName = "TableMetaData";
                var metaDatetableSchema = "TimeStamp:datetime, startTime:datetime, endTime:datetime, resourceId:string, tableName:string, logType:string";

                if (Endpoint.CreateTable(metaDatatableName, metaDatetableSchema))
                {
                    Endpoint.IngestInline(metaDatatableName, string.Format("{0},{1},{2},{3},{4},{5}", DateTime.UtcNow, Config.StartTimeUtc.UtcDateTime, Config.EndTimeUtc.UtcDateTime, Config.ResourceUri, Config.KustoTable, Config.FileType));
                }
            }
        }

        private void IngestStatusQuery()
        {
            List<string> successUris = new List<string>();

            if (!Endpoint.HasTable(Endpoint.TableName))
            {
                return;
            }

            _ingestCursor = _ingestedUris.Count() < 1 ? "''" : _ingestCursor;
            successUris.AddRange(Endpoint.Query($"['{Endpoint.TableName}']" +
                $"| where cursor_after({_ingestCursor})" +
                $"| where ingestion_time() > todatetime('{_instance.StartTime.ToUniversalTime().ToString("o")}')" +
                $"| distinct RelativeUri"));

            _ingestCursor = Endpoint.Cursor;

            Endpoint.Query($".show ingestion failures" +
                $"| where Table == '{Endpoint.TableName}'" +
                $"| where FailedOn >= todatetime('{_failureQueryTime}')" +
                $"| order by FailedOn asc");

            KustoRestRecords failedRecords = Endpoint.PrimaryResultTable.Records();

            foreach (KustoRestRecord record in failedRecords)
            {
                string uri = record["IngestionSourcePath"].ToString();
                Log.Debug($"checking failed ingested relativeuri: {uri}");

                if (!_failIngestedUris.Contains(uri))
                {
                    Log.Error($"adding failedUri to _failIngestedUris[{_failIngestedUris.Count()}]: {uri}", record);
                    _failIngestedUris.Add(uri);
                    _failureCount++;
                    _failureQueryTime = DateTime.Now.ToUniversalTime().AddMinutes(-1);
                }
            }

            foreach (string uri in _failIngestedUris)
            {
                if (_messageList.Any(x => Regex.IsMatch(x, Regex.Escape(new Uri(uri).AbsolutePath.TrimStart('/')), RegexOptions.IgnoreCase)))
                {
                    _messageList.RemoveAll(x => Regex.IsMatch(x, Regex.Escape(new Uri(uri).AbsolutePath.TrimStart('/')), RegexOptions.IgnoreCase));
                    Log.Error($"removing failed ingested relativeuri from _messageList[{_messageList.Count()}]: {uri}");
                }
            }

            Log.Debug($"files ingested:{successUris.Count}");

            foreach (string uri in successUris)
            {
                Log.Debug($"checking ingested relativeuri: {uri}");

                if (!_ingestedUris.Contains(uri))
                {
                    Log.Info($"adding relativeuri to _ingestedUris[{_ingestedUris.Count()}]: {uri}", ConsoleColor.Green);
                    _ingestedUris.Add(uri);
                }
            }

            foreach (string uri in _ingestedUris)
            {
                if (_messageList.Any(x => Regex.IsMatch(x, Regex.Escape(uri), RegexOptions.IgnoreCase)))
                {
                    _messageList.RemoveAll(x => Regex.IsMatch(x, Regex.Escape(uri), RegexOptions.IgnoreCase));
                    Log.Info($"removing ingested relativeuri from _messageList[{_messageList.Count()}]: {uri}", ConsoleColor.Green);
                }
            }

            Log.Info($"current count ingested: {_ingestedUris.Count()} ingesting: {_messageList.Count()} failed: {_failureCount} total: {_ingestedUris.Count() + _messageList.Count() + _failureCount}", ConsoleColor.Green);
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

        private void QueueMessageMonitor()
        {
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

        private void QueueMonitor()
        {
            while (!_tokenSource.IsCancellationRequested | _messageList.Any())
            {
                Thread.Sleep(ThreadSleepMs100);
                QueueMessageMonitor();

                if (!Config.KustoUseIngestMessage)
                {
                    Thread.Sleep(ThreadSleepMs10000);
                    IngestStatusQuery();
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