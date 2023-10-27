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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Azure.Core;

namespace CollectSFData.Azure
{
    public class BlobManager
    {
        private readonly CustomTaskManager _blobTasks = new CustomTaskManager();
        private BlobClientOptions _blobClientOptions;
        private BlobServiceClient _blobServiceClient;
        private ConfigurationOptions _config;
        private string _fileFilterPattern = @"(?:.+_){6}(\d{20})_";
        private Instance _instance;
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
                _blobServiceClient = new BlobServiceClient(_config.SasEndpointInfo.ConnectionString, _blobClientOptions);
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

        public BlobClient CreateBlobClient(string blobUri)
        {
            try
            {
                Log.Debug($"enter: {blobUri}");
                Uri uri = new Uri(blobUri);
                return CreateBlobClient(uri);
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                return null;
            }
        }

        public BlobClient CreateBlobClient(BlobHierarchyItem blobHierarchyItem)
        {
            try
            {
                Log.Debug($"enter: {blobHierarchyItem.Blob.Name}");
                return CreateBlobClient(blobHierarchyItem.Blob.Name);
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                return null;
            }
        }

        public BlobClient CreateBlobClient(Uri blobUri, string prefix = "")
        {
            Log.Debug($"enter: {blobUri}");
            if (!string.IsNullOrEmpty(prefix))
            {
                blobUri = new Uri($"{blobUri.Scheme}://{blobUri.Host}{blobUri.AbsolutePath + _pathDelimiter + prefix}{blobUri.Query}");
            }
            Log.Debug($"exit: {blobUri}");
            return new BlobClient(blobUri, _blobClientOptions);
        }

        public BlobContainerClient CreateBlobContainerClient(Uri blobContainerUri)
        {
            Log.Debug($"enter: {blobContainerUri}");
            return new BlobContainerClient(blobContainerUri, _blobClientOptions);
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
        }

        public void DownloadFiles(string[] uris)
        {
            List<BlobClient> blobItems = new List<BlobClient>();

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
                        blobItems.Add(CreateBlobClient(uri));
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
        }

        public List<BlobClient> EnumerateContainerBlobPages(BlobContainerClient containerClient, string prefix = "")
        {
            Log.Info($"enter containerUri: {containerClient.Name}");
            string continuationToken = "";
            bool moreResultsAvailable = true;
            List<BlobClient> blobItems = new List<BlobClient>();
            Page<BlobHierarchyItem> blobHierarchyItems = default;

            while (!_blobTasks.CancellationToken.IsCancellationRequested && moreResultsAvailable)
            {
                blobHierarchyItems = GetBlobsByHierarchy(containerClient, prefix, continuationToken);
                continuationToken = blobHierarchyItems.ContinuationToken;
                moreResultsAvailable = blobHierarchyItems.Values.Any() && !string.IsNullOrEmpty(continuationToken);

                foreach (BlobHierarchyItem item in blobHierarchyItems.Values)
                {
                    if (item.IsBlob)
                    {
                        blobItems.Add(CreateBlobClient(containerClient.Uri, item.Blob.Name));
                    }
                    else
                    {
                        //blobItems.AddRange(EnumerateContainerBlobPages(containerClient, item.Prefix));
                        //_blobTasks.QueueTaskAction(() => QueueBlobSegmentDownload(EnumerateContainerBlobPages(containerClient, item.Prefix)));
                        //blobItems.AddRange(DownloadBlobsFromDirectory(containerClient, item.Prefix));
                        DownloadBlobsFromDirectory(containerClient, item.Prefix);
                    }
                }
            }
            return blobItems;
        }

        public void UploadFile(FileObject fileObject, Uri uri)
        {
            BlobClient blobClient = new BlobClient(uri, _blobClientOptions);
            blobClient.UploadAsync(fileObject.Stream.Get(), _blobTasks.CancellationToken).Wait();
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
            _blobTasks.QueueTaskAction(() => QueueBlobSegmentDownload(EnumerateContainerBlobPages(containerClient)));
            //QueueBlobSegmentDownload(EnumerateContainerBlobPages(containerClient));
        }

        private void DownloadBlobsFromDirectory(BlobContainerClient containerDirectory, string prefix = "")
        {
            Log.Info($"enumerating:{containerDirectory}", ConsoleColor.Cyan);
            _blobTasks.QueueTaskAction(() => QueueBlobSegmentDownload(EnumerateContainerBlobPages(containerDirectory, prefix)));
            //QueueBlobSegmentDownload(EnumerateContainerBlobPages(containerDirectory, prefix));
        }

        private List<BlobContainerClient> EnumerateContainers(string containerPrefix = "", bool testConnectivity = false)
        {
            Pageable<BlobContainerItem> blobContainers = default;
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
                    _blobTasks.CancellationToken);

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

                return ContainerList;
            }
            catch (Exception e)
            {
                Log.Debug($"{e}");
                Log.Warning($"unable to connect to containerPrefix: {containerPrefix} containerFilter: {containerFilter} error: {e.HResult}");
            }

            if (_config.SasEndpointInfo.AbsolutePath.Length > 1)
            {
                Log.Info("absolute path sas");
                BlobContainerClient blobContainerClient = _blobServiceClient.GetBlobContainerClient(_config.SasEndpointInfo.AbsolutePath);

                // force connection / error
                Response<BlobContainerProperties> containerProperties = blobContainerClient.GetProperties(new BlobRequestConditions(), _blobTasks.CancellationToken);
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

        private Page<BlobHierarchyItem> GetBlobsByHierarchy(BlobContainerClient containerClient, string prefix = "", string continuationToken = "")
        {
            Log.Debug($"enter containerInfo: {containerClient.Uri} prefix:{prefix}");
            Page<BlobHierarchyItem> blobItems = containerClient.GetBlobsByHierarchy(
                BlobTraits.None,
                BlobStates.None,
                _pathDelimiter,
                prefix,
                _blobTasks.CancellationToken)
            .AsPages(continuationToken, Constants.MaxEnumerationResults)
            .FirstOrDefault(); // since setting max results, only one page should be returned

            Log.Debug($"exit containerInfo: {containerClient.Uri} prefix:{prefix} count:{blobItems.Values.Count()}");
            return blobItems;
        }

        private void InvokeCallback(BlobClient blob, FileObject fileObject, int sourceLength)
        {
            if (!fileObject.Exists)
            {
                BlobDownloadToOptions _blobDownloadToOptions = new BlobDownloadToOptions()
                {
                    Conditions = new BlobRequestConditions()
                    {
                        IfModifiedSince = fileObject.LastModified
                    }
                };

                StorageTransferOptions _storageTransferOptions = new StorageTransferOptions()
                {
                    MaximumConcurrency = _config.Threads
                };

                BlobRequestConditions _blobRequestConditions = new BlobRequestConditions()
                {
                    //IfModifiedSince = fileObject.LastModified
                };

                if (sourceLength > Constants.MaxStreamTransmitBytes)
                {
                    fileObject.DownloadAction = () =>
                    {
                        FileManager.CreateDirectory(fileObject.FileUri);
                        blob.DownloadToAsync(fileObject.FileUri, _blobRequestConditions, _storageTransferOptions, _blobTasks.CancellationToken).Wait();
                    };
                }
                else
                {
                    fileObject.DownloadAction = () =>
                    {
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

        private void QueueBlobSegmentDownload(IEnumerable<BlobClient> blobClients)
        {
            int parentId = Thread.CurrentThread.ManagedThreadId;
            Log.Debug($"enter. current id:{parentId}. results count: {blobClients.Count()}");
            long segmentMinDateTicks = _instance.DiscoveredMinDateTicks;
            long segmentMaxDateTicks = _instance.DiscoveredMaxDateTicks;
            bool regexUriTicksMatch = false;

            foreach (BlobClient blobClient in blobClients)
            {
                Response<BlobProperties> blobProperties = null;
                Log.Debug($"parent id:{parentId} current Id:{Thread.CurrentThread.ManagedThreadId}");

                _instance.TotalFilesEnumerated++;

                if (Regex.IsMatch(blobClient.Uri.ToString(), _fileFilterPattern, RegexOptions.IgnoreCase))
                {
                    long ticks = Convert.ToInt64(Regex.Match(blobClient.Uri.ToString(), _fileFilterPattern, RegexOptions.IgnoreCase).Groups[1].Value);

                    if (ticks < _config.StartTimeUtc.Ticks | ticks > _config.EndTimeUtc.Ticks)
                    {
                        _instance.TotalFilesSkipped++;
                        Log.Debug($"exclude:bloburi file ticks {new DateTime(ticks).ToString("o")} outside of time range:{blobClient.Uri}");

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
                    Log.Debug($"regex not matched: {blobClient.Uri} pattern: {_fileFilterPattern}");
                }

                try
                {
                    Log.Debug($"file Blob: {blobClient}");
                    blobProperties = blobClient.GetProperties(new BlobRequestConditions { }, _blobTasks.CancellationToken);
                }
                catch (Exception se)
                {
                    _instance.TotalErrors++;
                    Log.Exception($"getting ref for {blobClient}, skipping. {se.Message}");
                    continue;
                }

                DateTimeOffset lastModified = blobProperties.Value.LastModified;
                _instance.SetMinMaxDate(lastModified.Ticks);

                if (!string.IsNullOrEmpty(_config.UriFilter) && !Regex.IsMatch(blobClient.ToString(), _config.UriFilter, RegexOptions.IgnoreCase))
                {
                    _instance.TotalFilesSkipped++;
                    Log.Debug($"blob:{blobClient} does not match uriFilter pattern:{_config.UriFilter}, skipping...");
                    continue;
                }

                if (_config.FileType != FileTypesEnum.any
                    && !FileTypes.MapFileTypeUri(blobClient.Uri.AbsolutePath).Equals(_config.FileType))
                {
                    _instance.TotalFilesSkipped++;
                    Log.Debug($"skipping uri with incorrect file type: {FileTypes.MapFileTypeUri(blobClient.Uri.AbsolutePath)}");
                    continue;
                }

                if (regexUriTicksMatch || (lastModified >= _config.StartTimeUtc && lastModified <= _config.EndTimeUtc))
                {
                    _instance.TotalFilesMatched++;

                    if (_config.List)
                    {
                        Log.Info($"listing file with timestamp: {lastModified}\r\n file: {blobClient.Uri.AbsolutePath}");
                        continue;
                    }

                    FileObject fileObject = new FileObject(blobClient.Uri.AbsolutePath, _config.CacheLocation)
                    {
                        LastModified = lastModified,
                        Status = FileStatus.enumerated,
                        SourceFileUri = blobClient.Uri.AbsoluteUri
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
                        fileObject.FileUri = blobClient.Uri.AbsolutePath;
                        IngestCallback?.Invoke(fileObject);
                        continue;
                    }
                    else if (ReturnSourceFileLink && !fileObject.IsSourceFileLinkCompliant())
                    {
                        Log.Warning("updating configuration to not use blob as source as incompatible files / configuration detected");

                        ReturnSourceFileLink = false;
                        _config.KustoUseBlobAsSource = false;
                    }

                    Log.Info($"queueing blob with timestamp: {lastModified}\r\n file: {blobClient.Uri.AbsolutePath}");
                    InvokeCallback(blobClient, fileObject, (int)blobProperties.Value.ContentLength);
                }
                else
                {
                    _instance.TotalFilesSkipped++;
                    Log.Debug($"exclude:bloburi {lastModified.ToString("o")} outside of time range:{blobClient}");

                    _instance.SetMinMaxDate(lastModified.Ticks);
                    continue;
                }
            }
        }
    }
}