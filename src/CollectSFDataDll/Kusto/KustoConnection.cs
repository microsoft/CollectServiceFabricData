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
    public class KustoConnection
    {
        private const int _maxMessageCount = 32;
        private readonly CustomTaskManager _kustoTasks;
        private readonly TimeSpan _messageTimeToLive = new TimeSpan(0, 1, 0, 0);
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private bool _appendingToExistingTableUnique;
        private ConfigurationOptions _config;
        private object _enumeratorLock = new object();
        private DateTime _failureQueryTime;
        private string _ingestCursor = "''";
        private IEnumerator<string> _ingestionQueueEnumerator;
        private Instance _instance;
        private Task _monitorTask;
        private IEnumerator<string> _tempContainerEnumerator;
        public KustoEndpoint Endpoint { get; set; }

        public KustoConnection(Instance instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _config = _instance.Config;
            _kustoTasks = new CustomTaskManager();
        }

        public void AddFile(FileObject fileObject)
        {
            Log.Debug("enter");

            if (!CanIngest(fileObject.RelativeUri))
            {
                Log.Warning($"file already ingested. skipping: {fileObject.RelativeUri}");
                return;
            }

            if (_config.KustoUseBlobAsSource && fileObject.IsSourceFileLinkCompliant())
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

                if (_appendingToExistingTableUnique
                    && _config.FileType == FileTypesEnum.table
                    && _instance.FileObjects.Any(FileStatus.succeeded))
                {
                    // only way for records from table storage to be unique since there is not a file reference
                    Log.Info("removing duplicate records", ConsoleColor.White);
                    IEnumerable<KustoCsvSchema> schema = new KustoIngestionMappings(new FileObject()).TableSchema();
                    schema = schema.Where(x => x.Name != "RelativeUri");
                    string names = string.Join(",", schema.Select(x => x.Name).ToList());

                    string command = $".set-or-replace {_config.KustoTable} <| {_config.KustoTable} | summarize min(RelativeUri) by {names}";
                    Log.Info(command);

                    Endpoint.Command(command);
                    Log.Info("removed duplicate records", ConsoleColor.White);
                }

                int ingestFailureCount = _instance.FileObjects.Count(FileStatus.failed);
                _instance.TotalErrors += ingestFailureCount;

                Log.Info($"return: total kusto ingests:{_instance.FileObjects.Count(FileStatus.uploading | FileStatus.succeeded)} success:{ingestFailureCount == 0} ");
                return ingestFailureCount == 0;
            }
            catch (Exception ex)
            {
                Log.Exception($"{ex}");
                return false;
            }
        }

        public bool Connect()
        {
            Endpoint = new KustoEndpoint(_config);
            Endpoint.Authenticate();
            _failureQueryTime = _instance.StartTime.ToUniversalTime();

            if (!PopulateQueueEnumerators())
            {
                return false;
            }

            if (_config.IsKustoPurgeRequested())
            {
                Purge();
                return false;
            }
            else if (_config.KustoRecreateTable)
            {
                PurgeMessages(Endpoint.TableName);

                if (!Endpoint.DropTable(Endpoint.TableName))
                {
                    return false;
                }
            }
            else if (_config.Unique && Endpoint.HasTable(Endpoint.TableName))
            {
                _appendingToExistingTableUnique = true;
                List<string> existingUploads = Endpoint.Query($"['{Endpoint.TableName}']|distinct RelativeUri");
                foreach (string existingUpload in existingUploads)
                {
                    _instance.FileObjects.Add(new FileObject(existingUpload) { Status = FileStatus.existing });
                }
            }

            // monitor for new files to be uploaded
            if (_monitorTask == null)
            {
                _monitorTask = Task.Run((Action)QueueMonitor, _tokenSource.Token);
            }

            return true;
        }

        public Tuple<string, string> GetNextIngestionQueue()
        {
            string tempContainer = null;
            string ingestionQueue = null;

            lock (_enumeratorLock)
            {
                if (!_tempContainerEnumerator.MoveNext())
                {
                    _tempContainerEnumerator.Reset();
                    _tempContainerEnumerator.MoveNext();
                }

                tempContainer = _tempContainerEnumerator.Current;

                if (!_ingestionQueueEnumerator.MoveNext())
                {
                    _ingestionQueueEnumerator.Reset();
                    _ingestionQueueEnumerator.MoveNext();
                }

                ingestionQueue = _ingestionQueueEnumerator.Current;
            }

            Log.Debug($"returning:tempContainer.Current:{tempContainer} ingestionQueue.Current:{ingestionQueue}");
            return new Tuple<string, string>(ingestionQueue, tempContainer);
        }

        public bool PopulateQueueEnumerators()
        {
            _tempContainerEnumerator = Endpoint.IngestionResources.TempStorageContainers.GetEnumerator();
            _ingestionQueueEnumerator = Endpoint.IngestionResources.IngestionQueues.GetEnumerator();

            if (!_ingestionQueueEnumerator.MoveNext())
            {
                Log.Error($"problem with ingestion queues", Endpoint.IngestionResources);
                return false;
            }

            if (!_tempContainerEnumerator.MoveNext())
            {
                Log.Error($"problem with temp container ", Endpoint.IngestionResources);
                return false;
            }
            return true;
        }

        private bool CanIngest(string relativeUri)
        {
            if (!_config.Unique)
            {
                return true;
            }

            string cleanUri = Regex.Replace(relativeUri, $"\\.?\\d*?({Constants.ZipExtension}|{Constants.TableExtension})", "");
            FileObject fileObject = _instance.FileObjects.FindByUriFirstOrDefault(cleanUri);
            return fileObject.Status != FileStatus.existing;
        }

        private void IngestMultipleFiles(FileObjectCollection fileObjectCollection)
        {
            fileObjectCollection.ForEach(x => IngestSingleFile(x));
        }

        private void IngestResourceIdKustoTableMapping()
        {
            string resourceUri = _config.ResourceUri;

            if (string.IsNullOrEmpty(resourceUri)
                && _instance.FileObjects.Any(FileStatus.succeeded)
                && _config.FileType == FileTypesEnum.trace)
            {
                // Fetch resource ID from ingested traces
                List<string> results = Endpoint.Query($"['{Endpoint.TableName}']" +
                    $" | where Type == 'InfrastructureService.RestClientHelper'" +
                    $" | take 1");

                if (results.Any())
                {
                    Regex pattern = new Regex(@"resourceId\W+?(/[A-Za-z0-9./-]+)");
                    Match match = pattern.Match(results.FirstOrDefault());
                    resourceUri = match.Groups[1].Value;
                    Log.Info($"ResourceID: {resourceUri}");
                }
            }

            if (!string.IsNullOrWhiteSpace(resourceUri))
            {
                string metaDatatableName = "TableMetaData";
                string metaDatetableSchema = "TimeStamp:datetime, startTime:datetime, endTime:datetime, resourceId:string, tableName:string, logType:string";

                if (Endpoint.CreateTable(metaDatatableName, metaDatetableSchema))
                {
                    Endpoint.IngestInline(metaDatatableName, string.Format("{0},{1},{2},{3},{4},{5}", DateTime.UtcNow, _config.StartTimeUtc.UtcDateTime, _config.EndTimeUtc.UtcDateTime, resourceUri, _config.KustoTable, _config.FileType));
                }
            }
        }

        private void IngestSingleFile(FileObject fileObject)
        {
            string blobUriWithSas = null;
            string ingestionMapping = SetIngestionMapping(fileObject);
            Tuple<string, string> nextQueues = GetNextIngestionQueue();
            string ingestionQueue = nextQueues.Item1;
            string tempContainer = nextQueues.Item2;

            if (_config.KustoUseBlobAsSource && fileObject.IsSourceFileLinkCompliant())
            {
                blobUriWithSas = $"{fileObject.FileUri}{_config.SasEndpointInfo.SasToken}";
            }
            else
            {
                string blobName = Path.GetFileName(fileObject.FileUri);
                blobUriWithSas = UploadFileToBlobContainer(fileObject, tempContainer, fileObject.NodeName, blobName);
            }

            PostMessageToQueue(ingestionQueue, PrepareIngestionMessage(blobUriWithSas, fileObject.Length, ingestionMapping), fileObject);
        }

        private void IngestStatusFailQuery()
        {
            Endpoint.Query($".show ingestion failures" +
                $"| where Table == '{Endpoint.TableName}'" +
                $"| where FailedOn >= todatetime('{_failureQueryTime}')" +
                $"| order by FailedOn asc");

            KustoRestRecords failedRecords = Endpoint.PrimaryResultTable.Records();

            foreach (KustoRestRecord record in failedRecords)
            {
                string uriFile = record["IngestionSourcePath"].ToString();
                Log.Debug($"checking failed ingested for failed relativeuri: {uriFile}");
                FileObject fileObject = _instance.FileObjects.FindByUriFirstOrDefault(uriFile);

                fileObject.Status = FileStatus.failed;

                if (fileObject.IsPopulated)
                {
                    Log.Error($"file upload to kusto failed: [{_instance.FileObjects.Count(FileStatus.failed)}]: {uriFile}", record);
                }
                else
                {
                    Log.Error($"file upload to kusto failed:adding fileUri fileObject [{_instance.FileObjects.Count(FileStatus.failed)}]: {uriFile}", record);
                    _instance.FileObjects.Add(fileObject);
                }

                _failureQueryTime = DateTime.Now.ToUniversalTime().AddMinutes(-1);
            }
        }

        private void IngestStatusSuccessQuery()
        {
            List<string> successUris = new List<string>();
            successUris.AddRange(Endpoint.Query($"['{Endpoint.TableName}']" +
                $"| where cursor_after('{_ingestCursor}')" +
                $"| where ingestion_time() > todatetime('{_instance.StartTime.ToUniversalTime().ToString("o")}')" +
                $"| distinct RelativeUri"));

            _ingestCursor = !_instance.FileObjects.Any(FileStatus.succeeded) ? "" : Endpoint.Cursor;
            Log.Debug($"files ingested:{successUris.Count}");

            foreach (string uriFile in successUris)
            {
                Log.Debug($"checking ingested uri for success relativeuri: {uriFile}");
                FileObject fileObject = _instance.FileObjects.FindByUriFirstOrDefault(uriFile);
                fileObject.Status = FileStatus.succeeded;

                if (fileObject.IsPopulated)
                {
                    Log.Info($"file upload to kusto succeeded:[{_instance.FileObjects.Count(FileStatus.succeeded)}]: {uriFile}", ConsoleColor.Green);
                }
                else
                {
                    Log.Info($"file upload to kusto succeeded:adding relativeuri fileObject[{_instance.FileObjects.Count(FileStatus.succeeded)}]: {uriFile}", ConsoleColor.Green);
                    _instance.FileObjects.Add(fileObject);
                }
            }

            Log.Debug($"files ingested:{successUris.Count}");
        }

        private IEnumerable<CloudQueueMessage> PopTopMessagesFromQueue(string queueUriWithSas, int count = _maxMessageCount)
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

        private void PostMessageToQueue(string queueUriWithSas, KustoIngestionMessage message, FileObject fileObject)
        {
            Log.Info($"post: {queueUriWithSas ?? "(null ingest uri)"}", ConsoleColor.Magenta);
            CloudQueue queue = new CloudQueue(new Uri(queueUriWithSas));
            CloudQueueMessage queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(message));
            OperationContext context = new OperationContext() { ClientRequestID = message.Id };

            queue.AddMessage(queueMessage, _messageTimeToLive, null, null, context);
            fileObject.Status = FileStatus.uploading;
            fileObject.MessageId = message.Id;
            Log.Info($"fileobject uploading FileUri:{fileObject.FileUri} RelativeUri: {fileObject.RelativeUri} message id: {message.Id}", ConsoleColor.Cyan);
        }

        private KustoIngestionMessage PrepareIngestionMessage(string blobUriWithSas, long blobSizeBytes, string ingestionMapping)
        {
            string id = Guid.NewGuid().ToString();

            KustoIngestionMessage message = new KustoIngestionMessage()
            {
                Id = id,
                BlobPath = blobUriWithSas,
                RawDataSize = Convert.ToInt32(blobSizeBytes),
                DatabaseName = Endpoint.DatabaseName,
                TableName = Endpoint.TableName,
                RetainBlobOnSuccess = true,
                Format = FileExtensionTypesEnum.csv.ToString(),
                FlushImmediately = true,
                ReportLevel = _config.KustoUseIngestMessage ? 2 : 1, //(int)IngestionReportLevel.FailuresAndSuccesses, // 2 FailuresAndSuccesses, 0 failures, 1 none
                ReportMethod = Convert.ToInt32(!_config.KustoUseIngestMessage), //(int)IngestionReportMethod.Table, // 0 queue, 1 table, 2 both
                AdditionalProperties = new KustoAdditionalProperties()
                {
                    authorizationContext = Endpoint.IdentityToken,
                    compressed = _config.KustoCompressed,
                    csvMapping = ingestionMapping
                }
            };

            Log.Debug($"ingestion message:", message);
            return message;
        }

        private void Purge()
        {
            if (_config.KustoPurge.ToLower() == "true" & Endpoint.HasTable(_config.KustoTable))
            {
                PurgeMessages(Endpoint.TableName);
                Endpoint.DropTable(Endpoint.TableName);
            }
            else if (_config.KustoPurge.ToLower().StartsWith("list"))
            {
                List<string> results = new List<string>();

                if (_config.KustoPurge.ToLower().Split(' ').Length > 1)
                {
                    results = Endpoint.Query($".show tables | where TableName contains {_config.KustoPurge.ToLower().Split(' ')[1]} | project TableName");
                }
                else
                {
                    results = Endpoint.Query(".show tables | project TableName");
                }

                Log.Info($"current table list:", results);
            }
            else if (Endpoint.HasTable(_config.KustoPurge))
            {
                PurgeMessages(_config.KustoPurge);
                Endpoint.DropTable(_config.KustoPurge);
            }
            else
            {
                Log.Error($"invalid purge option:{_config.KustoPurge}. should be 'true' or 'list' or table name to drop");
            }
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
                    FileObject fileObject = _instance.FileObjects.FindByMessageId(message.IngestionSourceId);

                    if (fileObject.IsPopulated)
                    {
                        fileObject.Status = FileStatus.succeeded;
                        RemoveMessageFromQueue(Endpoint.IngestionResources.SuccessNotificationsQueue, success);
                        Log.Info($"Ingestion completed total:({_instance.FileObjects.Count()}/{_instance.FileObjects.Count(FileStatus.uploading)}): {JsonConvert.DeserializeObject(success.AsString)}", ConsoleColor.Green);
                    }
                    else if (message.SucceededOn + _messageTimeToLive < DateTime.Now)
                    {
                        // remove sas key
                        message.IngestionSourcePath = Regex.Replace(message.IngestionSourcePath, @"\?(.*)", "");
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
                    FileObject fileObject = _instance.FileObjects.FindByMessageId(message.IngestionSourceId);

                    if (fileObject.IsPopulated)
                    {
                        fileObject.Status = FileStatus.failed;
                        RemoveMessageFromQueue(Endpoint.IngestionResources.FailureNotificationsQueue, error);
                        Log.Error($"Ingestion error total:({_instance.FileObjects.Count(FileStatus.failed)}): {JsonConvert.DeserializeObject(error.AsString)}");
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
            while ((!_tokenSource.IsCancellationRequested | _instance.FileObjects.Count(FileStatus.uploading) > 0) & !_kustoTasks.CancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(Constants.ThreadSleepMs100);
                QueueMessageMonitor();

                if (!_config.KustoUseIngestMessage)
                {
                    Thread.Sleep(Constants.ThreadSleepMs10000);

                    if (!Endpoint.HasTable(Endpoint.TableName))
                    {
                        continue;
                    }

                    IngestStatusSuccessQuery();
                    IngestStatusFailQuery();
                    Log.Info(_instance.FileObjects.StatusString(), ConsoleColor.Green);
                }
            }

            Log.Info($"exiting {_instance.FileObjects.Count(FileStatus.uploading)}", _instance.FileObjects.FindAll(FileStatus.uploading));
        }

        private void RemoveMessageFromQueue(string queueUriWithSas, CloudQueueMessage message)
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

        private string SetIngestionMapping(FileObject fileObject)
        {
            string ingestionJsonString = null;

            switch (_config.FileType)
            {
                case FileTypesEnum.counter:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = _config.ResourceUri,
                        SetConstants = _config.KustoUseBlobAsSource,
                    }.CounterSchema());

                    break;

                case FileTypesEnum.exception:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = _config.ResourceUri,
                        SetConstants = _config.KustoUseBlobAsSource,
                    }.ExceptionSchema());

                    break;

                case FileTypesEnum.setup:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = _config.ResourceUri,
                        SetConstants = _config.KustoUseBlobAsSource,
                    }.SetupSchema());

                    break;

                case FileTypesEnum.table:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = _config.ResourceUri
                    }.TableSchema());

                    break;

                case FileTypesEnum.trace:
                    ingestionJsonString = JsonConvert.SerializeObject(new KustoIngestionMappings(fileObject)
                    {
                        ResourceUri = _config.ResourceUri,
                        SetConstants = _config.KustoUseBlobAsSource,
                    }.TraceSchema());

                    break;
            }

            Log.Debug($"Ingestion Mapping: {ingestionJsonString}");
            return ingestionJsonString;
        }

        private string UploadFileToBlobContainer(FileObject fileObject, string blobContainerUri, string containerName, string blobName)
        {
            Log.Info($"uploading: {fileObject.Stream.Get().Length} bytes to {fileObject.FileUri} to {blobContainerUri}", ConsoleColor.Magenta);
            Uri blobUri = new Uri(blobContainerUri);
            BlobRequestOptions blobRequestOptions = new BlobRequestOptions()
            {
                RetryPolicy = new IngestRetryPolicy(),
                ParallelOperationThreadCount = _config.Threads,
            };

            CloudBlobContainer blobContainer = new CloudBlobContainer(blobUri);
            CloudBlockBlob blockBlob = blobContainer.GetBlockBlobReference(blobName);

            if (!_kustoTasks.CancellationToken.IsCancellationRequested)
            {
                if (_config.UseMemoryStream)
                {
                    _kustoTasks.TaskAction(() => blockBlob.UploadFromStreamAsync(fileObject.Stream.Get(), null, blobRequestOptions, null).Wait()).Wait();
                    fileObject.Stream.Dispose();
                }
                else
                {
                    _kustoTasks.TaskAction(() => blockBlob.UploadFromFileAsync(fileObject.FileUri, null, blobRequestOptions, null).Wait()).Wait();
                }

                Log.Info($"uploaded: {fileObject.FileUri} to {blobContainerUri}", ConsoleColor.DarkMagenta);
                return $"{blockBlob.Uri.AbsoluteUri}{blobUri.Query}";
            }
            else
            {
                return $"task cancelled:{blockBlob.Uri.AbsoluteUri}{blobUri.Query}";
            }
        }
    }
}