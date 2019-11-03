// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData
{
    public class BlobManager : Instance
    {
        private static readonly CustomTaskManager _blobChildTasks = new CustomTaskManager(true) { CreationOptions = TaskCreationOptions.AttachedToParent };
        private static readonly CustomTaskManager _blobTasks = new CustomTaskManager(true);
        private CloudStorageAccount _account;
        private CloudBlobClient _blobClient;

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
                _blobTasks.TaskAction(() => QueueBlobSegmentDownload(segment));
            }
        }

        private void DownloadBlobsFromDirectory(CloudBlobDirectory directory)
        {
            Log.Info($"enumerating:{directory.Uri}", ConsoleColor.Cyan);

            foreach (BlobResultSegment segment in EnumerateDirectoryBlobs(directory))
            {
                _blobChildTasks.TaskAction(() => QueueBlobSegmentDownload(segment));
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

        private void QueueBlobSegmentDownload(BlobResultSegment blobResultSegment)
        {
            int parentId = Thread.CurrentThread.ManagedThreadId;
            Log.Debug($"enter. current id:{parentId}. results count: {blobResultSegment.Results.Count()}");

            foreach (var blob in blobResultSegment.Results)
            {
                ICloudBlob blobRef = null;
                Log.Debug($"parent id:{parentId} current Id:{Thread.CurrentThread.ManagedThreadId}");

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

                Interlocked.Increment(ref TotalFilesEnumerated);

                if (!string.IsNullOrEmpty(Config.UriFilter) && !Regex.IsMatch(blob.Uri.ToString(), Config.UriFilter, RegexOptions.IgnoreCase))
                {
                    Interlocked.Increment(ref TotalFilesSkipped);
                    Log.Debug($"blob:{blob.Uri} does not match uriFilter pattern:{Config.UriFilter}, skipping...");
                    continue;
                }

                if (Regex.IsMatch(blob.Uri.ToString(), FileFilterPattern, RegexOptions.IgnoreCase))
                {
                    long ticks = Convert.ToInt64(Regex.Match(blob.Uri.ToString(), FileFilterPattern, RegexOptions.IgnoreCase).Groups[1].Value);

                    if (ticks < Config.StartTimeUtc.Ticks | ticks > Config.EndTimeUtc.Ticks)
                    {
                        Interlocked.Increment(ref TotalFilesSkipped);
                        Log.Debug($"exclude:bloburi ticks outside of time range:{blob.Uri}");
                        continue;
                    }
                }

                try
                {
                    Log.Debug($"file Blob: {blob.Uri}");
                    blobRef = blob.Container.ServiceClient.GetBlobReferenceFromServerAsync(blob.Uri).Result;
                }
                catch (StorageException se)
                {
                    Interlocked.Increment(ref TotalErrors);
                    Log.Exception($"getting ref for {blob.Uri}, skipping. {se.Message}");
                    continue;
                }

                if (blobRef.Properties.LastModified.HasValue)
                {
                    DateTimeOffset lastModified = blobRef.Properties.LastModified.Value;
                    if (Config.FileType != FileTypesEnum.any && !FileTypes.MapFileTypeUri(blob.Uri.AbsolutePath).Equals(Config.FileType))
                    {
                        Interlocked.Increment(ref TotalFilesSkipped);
                        Log.Debug($"skipping uri with incorrect file type: {FileTypes.MapFileTypeUri(blob.Uri.AbsolutePath)}");
                        continue;
                    }

                    if (lastModified >= Config.StartTimeUtc && lastModified <= Config.EndTimeUtc)
                    {
                        Interlocked.Increment(ref TotalFilesMatched);

                        if (Config.List)
                        {
                            Log.Info($"listing file with timestamp: {lastModified}\r\n file: {blob.Uri.AbsolutePath}");
                            continue;
                        }

                        if (ReturnSourceFileLink)
                        {
                            IngestCallback?.Invoke(new FileObject(blob.Uri.AbsolutePath, Config.SasEndpointInfo.BlobEndpoint) { Length = blobRef.Properties.Length });
                            continue;
                        }

                        FileObject fileObject = new FileObject(blob.Uri.AbsolutePath, Config.CacheLocation);
                        Log.Info($"queueing blob with timestamp: {lastModified}\r\n file: {blob.Uri.AbsolutePath}");

                        if (!fileObject.Exists)
                        {
                            fileObject.DownloadAction = () =>
                            {
                                ((CloudBlockBlob)blob).DownloadToStreamAsync(fileObject.Stream.Get(), null,
                                new BlobRequestOptions()
                                {
                                    RetryPolicy = new IngestRetryPolicy(),
                                    ParallelOperationThreadCount = Config.Threads
                                }, null).Wait();
                            };

                            IngestCallback?.Invoke(fileObject);
                            Interlocked.Increment(ref TotalFilesDownloaded);
                        }
                        else
                        {
                            Log.Warning($"destination file exists. skipping download:\r\n file: {fileObject}");
                            IngestCallback?.Invoke(fileObject);
                        }
                    }
                }
                else
                {
                    Log.Error("unable to read blob modified date", blobRef);
                    TotalErrors++;
                }
            }
        }
    }
}