// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using CollectSFData.DataFile;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
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
    public class BlobManager : Constants
    {
        private readonly CustomTaskManager _blobChildTasks = new CustomTaskManager(true) { CreationOptions = TaskCreationOptions.AttachedToParent };
        private readonly CustomTaskManager _blobTasks = new CustomTaskManager(true);
        private CloudStorageAccount _account;
        private CloudBlobClient _blobClient;
        private object _dateTimeMaxLock = new object();
        private object _dateTimeMinLock = new object();
        private string _fileFilterPattern = @"(?:.+_){6}(\d{20})_";
        private Instance _instance = Instance.Singleton();
        private ConfigurationOptions Config => _instance.Config;
        public List<CloudBlobContainer> ContainerList { get; set; } = new List<CloudBlobContainer>();

        public Action<FileObject> IngestCallback { get; set; }

        public bool ReturnSourceFileLink { get; set; }

        public bool Connect()
        {
            if (!Config.SasEndpointInfo.IsPopulated())
            {
                Log.Warning("no blob or token info. exiting:", Config.SasEndpointInfo);
                return false;
            }

            try
            {
                _account = CloudStorageAccount.Parse(Config.SasEndpointInfo.ConnectionString);
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
        private void AddContainerToList(CloudBlobContainer container)
        {
            if (!ContainerList.Any(x => x.Name.Equals(container.Name)))
            {
                Log.Info($"adding container to list:{container.Name}", ConsoleColor.Green);
                ContainerList.Add(container);
            }
        }

        public void DownloadContainers(string containerPrefix = "")
        {
            EnumerateContainers(containerPrefix);

            foreach (CloudBlobContainer container in ContainerList)
            {
                Log.Info($"ContainerName: {container.Name}, NodeFilter: {Config.NodeFilter}");
                DownloadContainer(container);
            }

            Log.Info("waiting for download tasks");
            _blobTasks.Wait();
            _blobChildTasks.Wait();
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

        public void DownloadFiles(string[] uris)
        {
            List<IListBlobItem> blobItems = new List<IListBlobItem>();

            foreach (string uri in uris)
            {
                try
                {
                    blobItems.Add(_blobClient.GetBlobReferenceFromServer(new Uri(uri)));
                }
                catch (Exception e)
                {
                    Log.Exception($"{e}");
                }
            }

            QueueBlobSegmentDownload(blobItems);
        }

        private IEnumerable<BlobResultSegment> EnumerateContainerBlobs(CloudBlobContainer cloudBlobContainer)
        {
            Log.Info($"enter {cloudBlobContainer.Uri}");
            BlobResultSegment resultSegment = default(BlobResultSegment);
            BlobContinuationToken blobToken = null;

            while (true)
            {
                resultSegment = _blobTasks.TaskFunction((blobresultsegment) =>
                cloudBlobContainer.ListBlobsSegmentedAsync(
                    null,
                    false,
                    BlobListingDetails.None,
                    MaxResults,
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
            string containerFilter = Config.ContainerFilter ?? string.Empty;

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

            if (Config.SasEndpointInfo.AbsolutePath.Length > 1)
            {
                Log.Info("absolute path sas");
                CloudBlobContainer container = new CloudBlobContainer(new Uri(_account.BlobEndpoint + Config.SasEndpointInfo.AbsolutePath + "?" + _account.Credentials.SASToken));

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

            while (true)
            {
                resultSegment = _blobChildTasks.TaskFunction((blobresultsegment) =>
                cloudBlobDirectory.ListBlobsSegmentedAsync(
                    false,
                    BlobListingDetails.None,
                    MaxResults,
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
                    ParallelOperationThreadCount = Config.Threads
                };

                if (sourceLength > MaxStreamTransmitBytes)
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

                IngestCallback?.Invoke(fileObject);
                Interlocked.Increment(ref _instance.TotalFilesDownloaded);
            }
            else
            {
                Log.Warning($"destination file exists. skipping download:\r\n file: {fileObject}");
                Interlocked.Increment(ref _instance.TotalFilesSkipped);
            }
        }

        private void QueueBlobSegmentDownload(IEnumerable<IListBlobItem> blobResults)
        {
            int parentId = Thread.CurrentThread.ManagedThreadId;
            Log.Debug($"enter. current id:{parentId}. results count: {blobResults.Count()}");
            long segmentMinDateTicks = Interlocked.Read(ref DiscoveredMinDateTicks);
            long segmentMaxDateTicks = Interlocked.Read(ref DiscoveredMaxDateTicks);

            foreach (IListBlobItem blob in blobResults)
            {
                ICloudBlob blobRef = null;
                Log.ToFile($"parent id:{parentId} current Id:{Thread.CurrentThread.ManagedThreadId}");

                if (blob is CloudBlobDirectory)
                {
                    if (!string.IsNullOrEmpty(Config.NodeFilter) && !Regex.IsMatch(blob.Uri.ToString(), Config.NodeFilter, RegexOptions.IgnoreCase))
                    {
                        Log.Debug($"blob:{blob.Uri} does not match nodeFilter pattern:{Config.NodeFilter}, skipping...");
                        continue;
                    }

                    DownloadBlobsFromDirectory(blob as CloudBlobDirectory);
                    Log.Debug("blob is directory.");
                    continue;
                }

                Interlocked.Increment(ref _instance.TotalFilesEnumerated);

                if (Regex.IsMatch(blob.Uri.ToString(), _fileFilterPattern, RegexOptions.IgnoreCase))
                {
                    long ticks = Convert.ToInt64(Regex.Match(blob.Uri.ToString(), _fileFilterPattern, RegexOptions.IgnoreCase).Groups[1].Value);

                    if (ticks < Config.StartTimeUtc.Ticks | ticks > Config.EndTimeUtc.Ticks)
                    {
                        Interlocked.Increment(ref _instance.TotalFilesSkipped);
                        Log.ToFile($"exclude:bloburi file ticks {new DateTime(ticks).ToString("o")} outside of time range:{blob.Uri}");

                        SetMinMaxDate(ref segmentMinDateTicks, ref segmentMaxDateTicks, ticks);
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
                    Interlocked.Increment(ref _instance.TotalErrors);
                    Log.Exception($"getting ref for {blob.Uri}, skipping. {se.Message}");
                    continue;
                }

                if (blobRef.Properties.LastModified.HasValue)
                {
                    DateTimeOffset lastModified = blobRef.Properties.LastModified.Value;
                    SetMinMaxDate(ref segmentMinDateTicks, ref segmentMaxDateTicks, lastModified.Ticks);

                    if (!string.IsNullOrEmpty(Config.UriFilter) && !Regex.IsMatch(blob.Uri.ToString(), Config.UriFilter, RegexOptions.IgnoreCase))
                    {
                        Interlocked.Increment(ref _instance.TotalFilesSkipped);
                        Log.Debug($"blob:{blob.Uri} does not match uriFilter pattern:{Config.UriFilter}, skipping...");
                        continue;
                    }

                    if (Config.FileType != FileTypesEnum.any
                        && !FileTypes.MapFileTypeUri(blob.Uri.AbsolutePath).Equals(Config.FileType))
                    {
                        Interlocked.Increment(ref _instance.TotalFilesSkipped);
                        Log.Debug($"skipping uri with incorrect file type: {FileTypes.MapFileTypeUri(blob.Uri.AbsolutePath)}");
                        continue;
                    }

                    if (lastModified >= Config.StartTimeUtc && lastModified <= Config.EndTimeUtc)
                    {
                        Interlocked.Increment(ref _instance.TotalFilesMatched);

                        if (Config.List)
                        {
                            Log.Info($"listing file with timestamp: {lastModified}\r\n file: {blob.Uri.AbsolutePath}");
                            continue;
                        }

                        if (ReturnSourceFileLink)
                        {
                            IngestCallback?.Invoke(new FileObject(blob.Uri.AbsolutePath, Config.SasEndpointInfo.BlobEndpoint)
                            {
                                LastModified = lastModified
                            });
                            continue;
                        }

                        FileObject fileObject = new FileObject(blob.Uri.AbsolutePath, Config.CacheLocation)
                        {
                            LastModified = lastModified
                        };

                        Log.Info($"queueing blob with timestamp: {lastModified}\r\n file: {blob.Uri.AbsolutePath}");
                        InvokeCallback(blob, fileObject, (int)blobRef.Properties.Length);
                    }
                    else
                    {
                        Interlocked.Increment(ref _instance.TotalFilesSkipped);
                        Log.Debug($"exclude:bloburi {lastModified.ToString("o")} outside of time range:{blob.Uri}");

                        SetMinMaxDate(ref segmentMinDateTicks, ref segmentMaxDateTicks, lastModified.Ticks);
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

        private void SetMinMaxDate(ref long segmentMinDateTicks, ref long segmentMaxDateTicks, long ticks)
        {
            if (ticks > DateTime.MinValue.Ticks && ticks < DateTime.MaxValue.Ticks)
            {
                if (ticks < segmentMinDateTicks)
                {
                    Log.Debug($"set new discovered min time range ticks: {new DateTime(ticks).ToString("o")}");
                    lock (_dateTimeMinLock)
                    {
                        segmentMinDateTicks = DiscoveredMinDateTicks = Math.Min(DiscoveredMinDateTicks, ticks);
                    }
                }

                if (ticks > segmentMaxDateTicks)
                {
                    Log.Debug($"set new discovered max time range ticks: {new DateTime(ticks).ToString("o")}");
                    lock (_dateTimeMaxLock)
                    {
                        segmentMaxDateTicks = DiscoveredMaxDateTicks = Math.Max(DiscoveredMaxDateTicks, ticks);
                    }
                }
            }
        }
    }
}