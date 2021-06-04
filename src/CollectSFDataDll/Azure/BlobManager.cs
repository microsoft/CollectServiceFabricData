// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using CollectSFData.DataFile;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData.Azure
{
    public class BlobManager
    {
        private readonly CustomTaskManager _blobChildTasks = new CustomTaskManager() { CreationOptions = TaskCreationOptions.AttachedToParent };
        private readonly CustomTaskManager _blobTasks = new CustomTaskManager();
        private CloudStorageAccount _account;
        private CloudBlobClient _blobClient;
        private ConfigurationOptions _config;
        private string _fileFilterPattern = @"(?:.+_){6}(\d{20})_";
        private Instance _instance;

        public List<CloudBlobContainer> ContainerList { get; set; } = new List<CloudBlobContainer>();

        public Action<FileObject> IngestCallback { get; set; }

        public bool ReturnSourceFileLink { get; set; }

        public BlobManager(Instance instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _config = _instance.Config;
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
                _account = CloudStorageAccount.Parse(_config.SasEndpointInfo.ConnectionString);
                CloudBlobClient storageClient = _account.CreateCloudBlobClient();
                _blobClient = storageClient.GetRootContainerReference().ServiceClient;

                // no communication with storage account until here:
                EnumerateContainers(null, true);
                return true;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}");
                return false;
            }
        }

        public void DownloadContainers(string containerPrefix = "")
        {
            EnumerateContainers(containerPrefix);

            foreach (CloudBlobContainer container in ContainerList)
            {
                Log.Info($"ContainerName: {container.Name}, NodeFilter: {_config.NodeFilter}");
                DownloadContainer(container);
            }

            Log.Info("waiting for download tasks");
            _blobTasks.Wait();
            _blobChildTasks.Wait();
        }

        public void DownloadFiles(string[] uris)
        {
            List<IListBlobItem> blobItems = new List<IListBlobItem>();

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
                        blobItems.Add(_blobClient.GetBlobReferenceFromServer(new Uri(uri)));
                    }
                }
                catch (Exception e)
                {
                    Log.Exception($"{e}");
                }
            }

            QueueBlobSegmentDownload(blobItems);
            uris = blobItems.Select(x => x.Uri.ToString()).ToArray();

            Log.Info("waiting for download tasks");
            _blobTasks.Wait();
            _blobChildTasks.Wait();
        }

        private void AddContainerToList(CloudBlobContainer container)
        {
            if (!ContainerList.Any(x => x.Name.Equals(container.Name)))
            {
                Log.Info($"adding container to list:{container.Name}", ConsoleColor.Green);
                ContainerList.Add(container);
            }
        }

        private void DownloadBlobsFromContainer(CloudBlobContainer container)
        {
            Log.Info($"enumerating:{container.Name}", ConsoleColor.Black, ConsoleColor.Cyan);

            foreach (BlobResultSegment segment in EnumerateContainerBlobs(container))
            {
                _blobTasks.TaskAction(() => QueueBlobSegmentDownload(segment.Results));
            }
        }

        private void DownloadBlobsFromDirectory(CloudBlobDirectory directory)
        {
            Log.Info($"enumerating:{directory.Uri}", ConsoleColor.Cyan);

            foreach (BlobResultSegment segment in EnumerateDirectoryBlobs(directory))
            {
                _blobChildTasks.TaskAction(() => QueueBlobSegmentDownload(segment.Results));
            }
        }

        private void DownloadContainer(CloudBlobContainer container)
        {
            Log.Info($"enter:{container.Name}");
            DownloadBlobsFromContainer(container);
        }

        private IEnumerable<BlobResultSegment> EnumerateContainerBlobs(CloudBlobContainer cloudBlobContainer)
        {
            Log.Info($"enter {cloudBlobContainer.Uri}");
            BlobResultSegment resultSegment = default(BlobResultSegment);
            BlobContinuationToken blobToken = null;

            while (!_blobTasks.CancellationToken.IsCancellationRequested)
            {
                resultSegment = _blobTasks.TaskFunction((blobresultsegment) =>
                cloudBlobContainer.ListBlobsSegmentedAsync(
                    null,
                    false,
                    BlobListingDetails.None,
                    Constants.MaxResults,
                    blobToken,
                    null,
                    null).Result as BlobResultSegment).Result as BlobResultSegment;

                blobToken = resultSegment.ContinuationToken;
                yield return resultSegment;

                if (blobToken == null)
                {
                    break;
                }
            }

            Log.Info($"exit {cloudBlobContainer.Uri}");
        }

        private void EnumerateContainers(string containerPrefix = "", bool testConnectivity = false)
        {
            BlobContinuationToken blobToken = new BlobContinuationToken();
            ContainerResultSegment containerSegment = null;
            string containerFilter = string.Empty;

            if (!string.IsNullOrEmpty(_config.ContainerFilter))
            {
                containerPrefix = null;
                containerFilter = _config.ContainerFilter;
            }

            try
            {
                Log.Info("account sas");

                while (blobToken != null)
                {
                    Log.Info($"containerPrefix:{containerPrefix} containerFilter:{containerFilter}");
                    containerSegment = _blobClient.ListContainersSegmentedAsync(containerPrefix, blobToken).Result;

                    if (testConnectivity)
                    {
                        return;
                    }

                    if (containerSegment.Results.Any())
                    {
                        IEnumerable<CloudBlobContainer> containers = containerSegment.Results.Where(x => Regex.IsMatch(x.Name, containerFilter, RegexOptions.IgnoreCase));
                        Log.Info("container list results:", containers.Select(x => x.Name));

                        if (containers.Count() < 1)
                        {
                            Log.Warning($"no containers detected. use 'containerFilter' argument to specify");
                        }

                        if (containers.Count() > 1)
                        {
                            Log.Warning($"multiple containers detected. use 'containerFilter' argument to enumerate only one");
                        }

                        foreach (CloudBlobContainer container in containers)
                        {
                            AddContainerToList(container);
                        }

                        blobToken = containerSegment.ContinuationToken;
                    }
                    else if (!string.IsNullOrEmpty(containerPrefix))
                    {
                        Log.Warning("retrying without containerPrefix");
                        containerPrefix = null;
                    }
                    else
                    {
                        Log.Warning("no results");
                        break;
                    }
                }

                return;
            }
            catch (Exception e)
            {
                Log.Debug($"{e}");
                Log.Warning($"unable to connect to containerPrefix: {containerPrefix} containerFilter: {containerFilter} error: {e.HResult}");
            }

            if (_config.SasEndpointInfo.AbsolutePath.Length > 1)
            {
                Log.Info("absolute path sas");
                CloudBlobContainer container = new CloudBlobContainer(new Uri(_account.BlobEndpoint + _config.SasEndpointInfo.AbsolutePath + "?" + _account.Credentials.SASToken));

                // force connection / error
                if (container.ListBlobsSegmented(null, true, new BlobListingDetails(), 1, null, null, null).Results.Count() == 1)
                {
                    if (testConnectivity)
                    {
                        return;
                    }

                    Log.Info($"connected with absolute path to container:{container.Name}", ConsoleColor.Green);
                    AddContainerToList(container);
                }
                else
                {
                    string errMessage = $"there are no blobs in container:{container.Name}";
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
        }

        private IEnumerable<BlobResultSegment> EnumerateDirectoryBlobs(CloudBlobDirectory cloudBlobDirectory)
        {
            Log.Info($"enter {cloudBlobDirectory.Uri}");
            BlobResultSegment resultSegment = default(BlobResultSegment);
            BlobContinuationToken blobToken = null;

            while (!_blobChildTasks.CancellationToken.IsCancellationRequested)
            {
                resultSegment = _blobChildTasks.TaskFunction((blobresultsegment) =>
                cloudBlobDirectory.ListBlobsSegmentedAsync(
                    false,
                    BlobListingDetails.None,
                    Constants.MaxResults,
                    blobToken,
                    null,
                    null).Result as BlobResultSegment).Result as BlobResultSegment;

                blobToken = resultSegment.ContinuationToken;
                yield return resultSegment;

                if (blobToken == null)
                {
                    break;
                }
            }

            Log.Info($"exit {cloudBlobDirectory.Uri}");
        }

        private void InvokeCallback(IListBlobItem blob, FileObject fileObject, int sourceLength)
        {
            if (!fileObject.Exists)
            {
                BlobRequestOptions blobRequestOptions = new BlobRequestOptions()
                {
                    RetryPolicy = new IngestRetryPolicy(),
                    ParallelOperationThreadCount = _config.Threads
                };

                if (sourceLength > Constants.MaxStreamTransmitBytes)
                {
                    fileObject.DownloadAction = () =>
                    {
                        if (!Directory.Exists(Path.GetDirectoryName(fileObject.FileUri)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fileObject.FileUri));
                        }

                        ((CloudBlockBlob)blob).DownloadToFileAsync(fileObject.FileUri, FileMode.Create, null, blobRequestOptions, null).Wait();
                    };
                }
                else
                {
                    fileObject.DownloadAction = () =>
                    {
                        ((CloudBlockBlob)blob).DownloadToStreamAsync(fileObject.Stream.Get(), null, blobRequestOptions, null).Wait();
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

        private void QueueBlobSegmentDownload(IEnumerable<IListBlobItem> blobResults)
        {
            int parentId = Thread.CurrentThread.ManagedThreadId;
            Log.Debug($"enter. current id:{parentId}. results count: {blobResults.Count()}");
            long segmentMinDateTicks = _instance.DiscoveredMinDateTicks;
            long segmentMaxDateTicks = _instance.DiscoveredMaxDateTicks;

            foreach (IListBlobItem blob in blobResults)
            {
                ICloudBlob blobRef = null;
                Log.Debug($"parent id:{parentId} current Id:{Thread.CurrentThread.ManagedThreadId}");

                if (blob is CloudBlobDirectory)
                {
                    if (!string.IsNullOrEmpty(_config.NodeFilter) && !Regex.IsMatch(blob.Uri.ToString(), _config.NodeFilter, RegexOptions.IgnoreCase))
                    {
                        Log.Debug($"blob:{blob.Uri} does not match nodeFilter pattern:{_config.NodeFilter}, skipping...");
                        continue;
                    }

                    DownloadBlobsFromDirectory(blob as CloudBlobDirectory);
                    Log.Debug("blob is directory.");
                    continue;
                }

                _instance.TotalFilesEnumerated++;

                if (Regex.IsMatch(blob.Uri.ToString(), _fileFilterPattern, RegexOptions.IgnoreCase))
                {
                    long ticks = Convert.ToInt64(Regex.Match(blob.Uri.ToString(), _fileFilterPattern, RegexOptions.IgnoreCase).Groups[1].Value);

                    if (ticks < _config.StartTimeUtc.Ticks | ticks > _config.EndTimeUtc.Ticks)
                    {
                        _instance.TotalFilesSkipped++;
                        Log.Debug($"exclude:bloburi file ticks {new DateTime(ticks).ToString("o")} outside of time range:{blob.Uri}");

                        _instance.SetMinMaxDate(ticks);
                        continue;
                    }
                }
                else
                {
                    Log.Debug($"regex not matched: {blob.Uri.ToString()} pattern: {_fileFilterPattern}");
                }

                try
                {
                    Log.Debug($"file Blob: {blob.Uri}");
                    blobRef = blob.Container.ServiceClient.GetBlobReferenceFromServerAsync(blob.Uri).Result;
                }
                catch (StorageException se)
                {
                    _instance.TotalErrors++;
                    Log.Exception($"getting ref for {blob.Uri}, skipping. {se.Message}");
                    continue;
                }

                if (blobRef.Properties.LastModified.HasValue)
                {
                    DateTimeOffset lastModified = blobRef.Properties.LastModified.Value;
                    _instance.SetMinMaxDate(lastModified.Ticks);

                    if (!string.IsNullOrEmpty(_config.UriFilter) && !Regex.IsMatch(blob.Uri.ToString(), _config.UriFilter, RegexOptions.IgnoreCase))
                    {
                        _instance.TotalFilesSkipped++;
                        Log.Debug($"blob:{blob.Uri} does not match uriFilter pattern:{_config.UriFilter}, skipping...");
                        continue;
                    }

                    if (_config.FileType != FileTypesEnum.any
                        && !FileTypes.MapFileTypeUri(blob.Uri.AbsolutePath).Equals(_config.FileType))
                    {
                        _instance.TotalFilesSkipped++;
                        Log.Debug($"skipping uri with incorrect file type: {FileTypes.MapFileTypeUri(blob.Uri.AbsolutePath)}");
                        continue;
                    }

                    if (lastModified >= _config.StartTimeUtc && lastModified <= _config.EndTimeUtc)
                    {
                        _instance.TotalFilesMatched++;

                        if (_config.List)
                        {
                            Log.Info($"listing file with timestamp: {lastModified}\r\n file: {blob.Uri.AbsolutePath}");
                            continue;
                        }

                        FileObject fileObject = new FileObject(blob.Uri.AbsolutePath, _config.CacheLocation)
                        {
                            LastModified = lastModified,
                            Status = FileStatus.enumerated
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
                            fileObject.FileUri = blob.Uri.AbsolutePath;
                            IngestCallback?.Invoke(fileObject);
                            continue;
                        }

                        Log.Info($"queueing blob with timestamp: {lastModified}\r\n file: {blob.Uri.AbsolutePath}");
                        InvokeCallback(blob, fileObject, (int)blobRef.Properties.Length);
                    }
                    else
                    {
                        _instance.TotalFilesSkipped++;
                        Log.Debug($"exclude:bloburi {lastModified.ToString("o")} outside of time range:{blob.Uri}");

                        _instance.SetMinMaxDate(lastModified.Ticks);
                        continue;
                    }
                }
                else
                {
                    Log.Error("unable to read blob modified date", blobRef);
                    _instance.TotalErrors++;
                }
            }
        }
    }
}