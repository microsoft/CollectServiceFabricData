// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using CollectSFData.DataFile;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using System.ComponentModel;

namespace CollectSFData.Azure
{
    public class BlobManager
    {
        private readonly CustomTaskManager _blobChildTasks = new CustomTaskManager() { CreationOptions = TaskCreationOptions.AttachedToParent };
        private readonly CustomTaskManager _blobTasks = new CustomTaskManager();
        private BlobServiceClient _blobServiceClient;
        //private BlobContainerClient _blobContainerClient;
        private ConfigurationOptions _config;
        private string _fileFilterPattern = @"(?:.+_){6}(\d{20})_";
        private Instance _instance;
        private BlobClientOptions _blobClientOptions;
        private string _pathDelimiter = "/";

        public List<BlobContainerClient> ContainerList { get; set; } = new List<BlobContainerClient>();

        public Action<FileObject> IngestCallback { get; set; }

        public bool ReturnSourceFileLink { get; set; }

        public BlobManager(Instance instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _config = _instance.Config;
            _blobClientOptions = new BlobClientOptions()
            {
                Retry =
                    {
                        Mode = RetryMode.Exponential,
                        MaxRetries = Constants.RetryCount,
                        Delay = TimeSpan.FromSeconds(Constants.RetryDelay),
                        MaxDelay = TimeSpan.FromSeconds(Constants.RetryMaxDelay)
                    }
            };
        }

        public bool Connect()
        {
            if (!_config.SasEndpointInfo.IsPopulated())
            {
                Log.Warning("no blob or token info. exiting:", _config.SasEndpointInfo);
                return false;
            }

            try
            {
                //CloudStorageAccount.UseV1MD5 = false; //DevSkim: ignore DS126858. required for jarvis
                //_blobServiceClient = CloudStorageAccount.Parse(_config.SasEndpointInfo.ConnectionString);

                _blobServiceClient = new BlobServiceClient(_config.SasEndpointInfo.ConnectionString, _blobClientOptions);
                //CloudBlobClient storageClient = _blobServiceClient.CreateCloudBlobClient();
                //_blobContainerClient = storageClient.GetRootContainerReference().ServiceClient;

                //_blobContainerClient = _blobServiceClient.GetBlobContainerClient(_config.SasEndpointInfo.AbsolutePath);

                // no communication with storage account until here:
                EnumerateContainers(null, true);
                return true;
            }
            catch (Exception e)
            {
                _config.CheckPublicIp();
                Log.Exception($"{e}");
                return false;
            }
        }

        public BlobClient CreateBlobClient(Uri blobUri)
        {
            return new BlobClient(blobUri, _blobClientOptions);
        }

        public void DownloadContainers(string containerPrefix = "")
        {
            foreach (BlobContainerClient container in EnumerateContainers(containerPrefix))
            {
                Log.Info($"ContainerName: {container.Name}, NodeFilter: {_config.NodeFilter}");
                DownloadBlobsFromContainer(container);
            }

            Log.Info("waiting for download tasks");
            _blobTasks.Wait();
            _blobChildTasks.Wait();
        }

        public void UploadFile(FileObject fileObject, Uri uri)
        {
            BlobClient blobClient = new BlobClient(uri, _blobClientOptions);
            blobClient.UploadAsync(fileObject.Stream.Get(), _blobTasks.CancellationToken).Wait();

        }

        public void DownloadFiles(string[] uris)
        {
            List<Uri> blobItems = new List<Uri>();

            foreach (string uri in uris)
            {
                try
                {
                    if (FileTypes.MapFileUriType(uri) != FileUriTypesEnum.azureStorageUri)
                    {
                        Log.Warning($"not blob storage path. skipping:{uri}");
                        continue;
                    }
                    else
                    {
                        //blobItems.Add(_blobContainerClient.GetBlobReferenceFromServer(new Uri(uri)));
                        blobItems.Add(new Uri(uri));
                    }
                }
                catch (Exception e)
                {
                    Log.Exception($"{e}");
                }
            }

            QueueBlobSegmentDownload(blobItems);
            uris = blobItems.Select(x => x.ToString()).ToArray();

            Log.Info("waiting for download tasks");
            _blobTasks.Wait();
            _blobChildTasks.Wait();
        }

        private void AddContainerToList(string containerName)
        {
            if (!ContainerList.Any(x => x.Name.Equals(containerName)))
            {
                BlobContainerClient container = _blobServiceClient.GetBlobContainerClient(containerName);
                if (container == null)
                {
                    Log.Error($"container is null: {containerName}");
                    return;
                }

                Log.Info($"adding container to list:{container.Name}", ConsoleColor.Green);
                ContainerList.Add(container);
            }
        }

        private void DownloadBlobsFromContainer(BlobContainerClient containerClient)
        {
            Log.Info($"enumerating:{containerClient.Name}", ConsoleColor.Black, ConsoleColor.Cyan);
            _blobTasks.TaskAction(() => QueueBlobSegmentDownload(EnumerateContainerBlobPages(containerClient.Uri)));

            //foreach (IEnumerable<Uri> blobItem in EnumerateContainerBlobs(containerClient.Uri))
            //{
            //    _blobTasks.TaskAction(() => QueueBlobSegmentDownload(blobItem));
            //}
        }

        private void DownloadBlobsFromDirectory(Uri containerDirectory)
        {
            Log.Info($"enumerating:{containerDirectory}", ConsoleColor.Cyan);
            _blobTasks.TaskAction(() => QueueBlobSegmentDownload(EnumerateContainerBlobPages(containerDirectory)));

            //foreach (BlobItem segment in EnumerateDirectoryBlobs(directory))
            //{
            //    _blobChildTasks.TaskAction(() => QueueBlobSegmentDownload(segment.Results));
            //}
        }

        //private void DownloadContainer(BlobContainerClient container)
        //{
        //    Log.Info($"enter:{container.Name}");

        //    DownloadBlobsFromContainer(container);
        //}

        private IEnumerable<Uri> EnumerateContainerBlobPages(Uri containerUri)
        {
            Log.Info($"enter containerUri: {containerUri}");
            string continuationToken = null;
            bool moreResultsAvailable = true;
            string blobPrefix = string.Empty;

            BlobUriBuilder blobUriBuilder = new BlobUriBuilder(containerUri);
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(_pathDelimiter + blobUriBuilder.BlobContainerName);
            Page<BlobHierarchyItem> blobHierarchyItems = default(Page<BlobHierarchyItem>);

            while (!_blobTasks.CancellationToken.IsCancellationRequested && moreResultsAvailable)
            {
                blobHierarchyItems = containerClient.GetBlobsByHierarchy(
                    BlobTraits.All,
                    BlobStates.All,
                    _pathDelimiter,
                    blobPrefix,
                    _blobTasks.CancellationToken)
                .AsPages(continuationToken, Constants.MaxResults)
                .FirstOrDefault(); // since setting max results, only one page should be returned

                continuationToken = blobHierarchyItems.ContinuationToken;
                moreResultsAvailable = blobHierarchyItems.Values.Any() && continuationToken != null;

                yield return _blobTasks.TaskFunction((BlobHierarchyItem) => EnumerateContainerBlobs(blobHierarchyItems)).Result as Uri;
            }
        }

        private List<Uri> EnumerateContainerBlobs(Page<BlobHierarchyItem> blobHierarchyItems)
        {
            Log.Info($"enter. items count: {blobHierarchyItems.Values.Count()}");
            List<Uri> blobItems = new List<Uri>();
            var etag = blobHierarchyItems.GetRawResponse().Headers.ETag;
            BlobUriBuilder builder = new BlobUriBuilder(blobHierarchyItems.GetRawResponse().Request.Uri);

            foreach (BlobHierarchyItem blobHierarchyItem in blobHierarchyItems.Values)
            {
                var blobName = blobHierarchyItem.Prefix;

                if (blobHierarchyItem.IsBlob)
                {
                    blobName = blobHierarchyItem.Blob.Name;
                    Log.Info($"blob Name:{blobName}");

                    blobItems.Add(new Uri(blobHierarchyItem.Prefix));
                    // containerClient.GetBlobs(BlobTraits.All, BlobStates.All, blobPrefix, _blobTasks.CancellationToken).AsEnumerable<BlobItem>();
                    //var blobProperties = blobClient.GetProperties();
                    //var blobDownloadInfo = blobClient.DownloadTo(outputFile);
                }
                else
                {
                    Log.Info($"directory Name:{blobName}");

                    //blobItems.AddRange(EnumerateContainerBlobs(new Uri(containerUri.AbsolutePath + _pathDelimiter + blobName)));
                    DownloadBlobsFromDirectory(new Uri(containerUri.AbsolutePath + _pathDelimiter + blobName));
                    //blobItems.AddRange(_blobTasks.TaskFunction((List<BlobClient>) => EnumerateContainerBlobs()).Result);

                }

            }

            //    BlobItem resultSegment = default(BlobItem);
            //string blobToken = null;

            //while (!_blobTasks.CancellationToken.IsCancellationRequested)
            //{
            //    resultSegment = _blobTasks.TaskFunction((BlobItem) =>

            //    blobContainerClient.ListBlobsSegmentedAsync(
            //        null,
            //        false,
            //        BlobListingDetails.None,
            //        Constants.MaxResults,
            //        blobToken,
            //        null,
            //        null).Result as BlobItem).Result as BlobItem;

            //    blobToken = resultSegment.ContinuationToken;
            //    yield return resultSegment;

            //    if (blobToken == null)
            //    {
            //        break;
            //    }
            //}

            Log.Info($"exit {containerClient.Uri}");
            return blobItems;
        }

        private List<BlobContainerClient> EnumerateContainers(string containerPrefix = "", bool testConnectivity = false)
        {
            var blobContainers = default(Pageable<BlobContainerItem>);
            string containerFilter = string.Empty;

            if (!string.IsNullOrEmpty(_config.ContainerFilter))
            {
                containerPrefix = null;
                containerFilter = _config.ContainerFilter;
            }

            try
            {
                Log.Info("account sas");
                Log.Info($"containerPrefix:{containerPrefix} containerFilter:{containerFilter}");
                blobContainers = _blobServiceClient.GetBlobContainers(BlobContainerTraits.Metadata,
                    BlobContainerStates.None,
                    containerPrefix,
                    _blobChildTasks.CancellationToken);

                if (testConnectivity)
                {
                    return null;
                }

                if (blobContainers.Any())
                {
                    IEnumerable<BlobContainerItem> containers = blobContainers.Where(x => Regex.IsMatch(x.Name, containerFilter, RegexOptions.IgnoreCase));
                    Log.Info("container list results:", containers.Select(x => x.Name));

                    if (containers.Count() < 1)
                    {
                        Log.Warning($"no containers detected. use 'containerFilter' argument to specify");
                    }

                    if (containers.Count() > 1)
                    {
                        Log.Warning($"multiple containers detected. use 'containerFilter' argument to enumerate only one");
                    }

                    foreach (BlobContainerItem container in containers)
                    {
                        AddContainerToList(container.Name);
                    }
                }
                else if (!string.IsNullOrEmpty(containerPrefix))
                {
                    Log.Warning("retrying without containerPrefix");
                    containerPrefix = null;
                }
                else
                {
                    Log.Warning("no results");
                }

                return null;
            }
            catch (Exception e)
            {
                Log.Debug($"{e}");
                Log.Warning($"unable to connect to containerPrefix: {containerPrefix} containerFilter: {containerFilter} error: {e.HResult}");
            }

            if (_config.SasEndpointInfo.AbsolutePath.Length > 1)
            {
                Log.Info("absolute path sas");
                //BlobContainerClient container = new BlobContainerClient(new Uri(_blobServiceClient.BlobEndpoint + _config.SasEndpointInfo.AbsolutePath + "?" + _blobServiceClient.Credentials.SASToken));
                BlobContainerClient blobContainerClient = _blobServiceClient.GetBlobContainerClient(_config.SasEndpointInfo.AbsolutePath);

                // force connection / error
                var containerProperties = blobContainerClient.GetProperties(new BlobRequestConditions(), _blobTasks.CancellationToken);
                Log.Debug("containerProperties:", containerProperties.Value.Metadata);

                if (blobContainerClient.GetBlobs(BlobTraits.None, BlobStates.None, null, _blobTasks.CancellationToken).Any())
                {
                    if (testConnectivity)
                    {
                        return null;
                    }

                    Log.Info($"connected with absolute path to container:{blobContainerClient.Name}", ConsoleColor.Green);
                    AddContainerToList(blobContainerClient.Name);
                }
                else
                {
                    string errMessage = $"there are no blobs in container:{blobContainerClient.Name}";
                    Log.Error(errMessage);
                    throw new Exception(errMessage);
                }
            }
            else
            {
                string errMessage = "unable to enumerate containers with or without absolute path";
                Log.Error(errMessage);
                throw new Exception(errMessage);
            }

            return ContainerList;
        }

        //private IEnumerable<BlobItem> EnumerateDirectoryBlobs(CloudBlobDirectory cloudBlobDirectory)
        //{
        //    Log.Info($"enter {cloudBlobDirectory.Uri}");
        //    BlobItem resultSegment = default(BlobItem);
        //    BlobContinuationToken blobToken = null;

        //    while (!_blobChildTasks.CancellationToken.IsCancellationRequested)
        //    {
        //        resultSegment = _blobChildTasks.TaskFunction((BlobItem) =>
        //        cloudBlobDirectory.ListBlobsSegmentedAsync(
        //            false,
        //            BlobListingDetails.None,
        //            Constants.MaxResults,
        //            blobToken,
        //            null,
        //            null).Result as BlobItem).Result as BlobItem;

        //        blobToken = resultSegment.ContinuationToken;
        //        yield return resultSegment;

        //        if (blobToken == null)
        //        {
        //            break;
        //        }
        //    }

        //    Log.Info($"exit {cloudBlobDirectory.Uri}");
        //}

        private void InvokeCallback(BlobClient blob, FileObject fileObject, int sourceLength)
        {
            if (!fileObject.Exists)
            {
                //BlobRequestOptions blobRequestOptions = new BlobRequestOptions()
                //{
                //    RetryPolicy = new IngestRetryPolicy(),
                //    ParallelOperationThreadCount = _config.Threads
                //};

                BlobDownloadToOptions _blobDownloadToOptions = new BlobDownloadToOptions()
                {
                    Conditions = new BlobRequestConditions()
                    {
                        IfModifiedSince = fileObject.LastModified
                    }
                    //},
                    //TransferOptions = new StorageTransferOptions()
                    //{
                    //    MaximumConcurrency = _config.Threads
                    //}
                };

                StorageTransferOptions _storageTransferOptions = new StorageTransferOptions()
                {
                    MaximumConcurrency = _config.Threads
                };

                BlobRequestConditions _blobRequestConditions = new BlobRequestConditions()
                {
                    IfModifiedSince = fileObject.LastModified
                };

                if (sourceLength > Constants.MaxStreamTransmitBytes)
                {
                    fileObject.DownloadAction = () =>
                    {
                        FileManager.CreateDirectory(fileObject.FileUri);
                        //((CloudBlob)blob).DownloadToFileAsync(fileObject.FileUri, FileMode.Create, null, blobRequestOptions, null).Wait();
                        blob.DownloadToAsync(fileObject.FileUri, _blobRequestConditions, _storageTransferOptions, _blobTasks.CancellationToken).Wait();
                    };
                }
                else
                {
                    fileObject.DownloadAction = () =>
                    {
                        //((CloudBlob)blob).DownloadToStreamAsync(fileObject.Stream.Get(), null, blobRequestOptions, null).Wait();
                        blob.DownloadToAsync(fileObject.Stream.Get(), _blobRequestConditions, _storageTransferOptions, _blobTasks.CancellationToken).Wait();
                    };
                }

                _instance.TotalFilesDownloaded++;
            }
            else
            {
                Log.Warning($"destination file exists. skipping download:\r\n file: {fileObject}");
                _instance.TotalFilesSkipped++;
            }

            IngestCallback?.Invoke(fileObject);
        }

        private void QueueBlobSegmentDownload(IEnumerable<Uri> blobUris)
        {
            int parentId = Thread.CurrentThread.ManagedThreadId;
            Log.Debug($"enter. current id:{parentId}. results count: {blobUris.Count()}");
            long segmentMinDateTicks = _instance.DiscoveredMinDateTicks;
            long segmentMaxDateTicks = _instance.DiscoveredMaxDateTicks;
            bool regexUriTicksMatch = false;

            foreach (Uri blobUri in blobUris)
            {
                BlobClient blobClient = null;
                Response<BlobProperties> blobProperties = null;
                Log.Debug($"parent id:{parentId} current Id:{Thread.CurrentThread.ManagedThreadId}");

                //if (blob is CloudBlobDirectory)
                //{
                //    if (!string.IsNullOrEmpty(_config.NodeFilter) && !Regex.IsMatch(blob.Uri.ToString(), _config.NodeFilter, RegexOptions.IgnoreCase))
                //    {
                //        Log.Debug($"blob:{blob.Uri} does not match nodeFilter pattern:{_config.NodeFilter}, skipping...");
                //        continue;
                //    }

                //    DownloadBlobsFromDirectory(blob as CloudBlobDirectory);
                //    Log.Debug("blob is directory.");
                //    continue;
                //}

                _instance.TotalFilesEnumerated++;

                if (Regex.IsMatch(blobUri.ToString(), _fileFilterPattern, RegexOptions.IgnoreCase))
                {
                    long ticks = Convert.ToInt64(Regex.Match(blobUri.ToString(), _fileFilterPattern, RegexOptions.IgnoreCase).Groups[1].Value);

                    if (ticks < _config.StartTimeUtc.Ticks | ticks > _config.EndTimeUtc.Ticks)
                    {
                        _instance.TotalFilesSkipped++;
                        Log.Debug($"exclude:bloburi file ticks {new DateTime(ticks).ToString("o")} outside of time range:{blobUri}");

                        _instance.SetMinMaxDate(ticks);
                        continue;
                    }
                    else
                    {
                        regexUriTicksMatch = true;
                    }
                }
                else
                {
                    Log.Debug($"regex not matched: {blobUri.ToString()} pattern: {_fileFilterPattern}");
                }

                try
                {
                    Log.Debug($"file Blob: {blobUri}");
                    // blobRef = blob.Container.ServiceClient.GetBlobReferenceFromServerAsync(blob.Uri).Result;
                    blobClient = new BlobClient(blobUri, _blobClientOptions);
                    blobProperties = blobClient.GetProperties(new BlobRequestConditions { }, _blobTasks.CancellationToken);
                }
                catch (Exception se)
                {
                    _instance.TotalErrors++;
                    Log.Exception($"getting ref for {blobUri}, skipping. {se.Message}");
                    continue;
                }

                DateTimeOffset lastModified = blobProperties.Value.LastModified;
                _instance.SetMinMaxDate(lastModified.Ticks);

                if (!string.IsNullOrEmpty(_config.UriFilter) && !Regex.IsMatch(blobUri.ToString(), _config.UriFilter, RegexOptions.IgnoreCase))
                {
                    _instance.TotalFilesSkipped++;
                    Log.Debug($"blob:{blobUri} does not match uriFilter pattern:{_config.UriFilter}, skipping...");
                    continue;
                }

                if (_config.FileType != FileTypesEnum.any
                    && !FileTypes.MapFileTypeUri(blobUri.AbsolutePath).Equals(_config.FileType))
                {
                    _instance.TotalFilesSkipped++;
                    Log.Debug($"skipping uri with incorrect file type: {FileTypes.MapFileTypeUri(blobUri.AbsolutePath)}");
                    continue;
                }

                if (regexUriTicksMatch || (lastModified >= _config.StartTimeUtc && lastModified <= _config.EndTimeUtc))
                {
                    _instance.TotalFilesMatched++;

                    if (_config.List)
                    {
                        Log.Info($"listing file with timestamp: {lastModified}\r\n file: {blobUri.AbsolutePath}");
                        continue;
                    }

                    FileObject fileObject = new FileObject(blobUri.AbsolutePath, _config.CacheLocation)
                    {
                        LastModified = lastModified,
                        Status = FileStatus.enumerated,
                        SourceFileUri = blobUri.AbsoluteUri
                    };

                    if (_instance.FileObjects.FindByUriFirstOrDefault(fileObject.RelativeUri).Status == FileStatus.existing)
                    {
                        Log.Info($"{fileObject} already exists. skipping", ConsoleColor.DarkYellow);
                        continue;
                    }

                    _instance.FileObjects.Add(fileObject);

                    if (ReturnSourceFileLink && fileObject.IsSourceFileLinkCompliant())
                    {
                        fileObject.BaseUri = _config.SasEndpointInfo.BlobEndpoint;
                        fileObject.FileUri = blobUri.AbsolutePath;
                        IngestCallback?.Invoke(fileObject);
                        continue;
                    }
                    else if (ReturnSourceFileLink && !fileObject.IsSourceFileLinkCompliant())
                    {
                        Log.Warning("updating configuration to not use blob as source as incompatible files / configuration detected");

                        ReturnSourceFileLink = false;
                        _config.KustoUseBlobAsSource = false;
                    }

                    Log.Info($"queueing blob with timestamp: {lastModified}\r\n file: {blobUri.AbsolutePath}");
                    InvokeCallback(blobClient, fileObject, (int)blobProperties.Value.ContentLength);
                }
                else
                {
                    _instance.TotalFilesSkipped++;
                    Log.Debug($"exclude:bloburi {lastModified.ToString("o")} outside of time range:{blobUri}");

                    _instance.SetMinMaxDate(lastModified.Ticks);
                    continue;
                }
            }
        }
    }
}