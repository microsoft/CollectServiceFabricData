// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData
{
    using CollectSFData.Azure;
    using CollectSFData.Common;
    using CollectSFData.DataFile;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class Collector
    {
        private int _noProgressCounter = 0;
        private Timer _noProgressTimer;
        private ParallelOptions _parallelConfig;
        private Total _progressTotal = new Total();

        public ConfigurationOptions Config { get => Instance.Config; }

        public Instance Instance { get; } = new Instance();

        public Collector(bool isConsole = false)
        {
            Log.IsConsole = isConsole;
        }

        public void Close()
        {
            Instance.Close();
            _noProgressTimer?.Dispose();
            Log.Close();
        }

        public int Collect()
        {
            return Collect(Config);
        }

        public int Collect(ConfigurationOptions configurationOptions)
        {
            try
            {
                if (!Initialize(configurationOptions) || !InitializeKusto() || !InitializeLogAnalytics())
                {
                    return 1;
                }

                if (Config.SasEndpointInfo.IsPopulated())
                {
                    DownloadAzureData();
                    CustomTaskManager.WaitAll();
                }

                if (Config.IsCacheLocationPreConfigured() | Config.FileUris.Any())
                {
                    UploadCacheData();
                    CustomTaskManager.WaitAll();
                }

                FinalizeKusto();

                if (Config.DeleteCache && Config.IsCacheLocationPreConfigured() && Directory.Exists(Config.CacheLocation))
                {
                    Log.Info($"Deleting outputlocation: {Config.CacheLocation}");

                    try
                    {
                        Directory.Delete($"{Config.CacheLocation}", true);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception($"{ex}");
                    }
                }

                Config.SaveConfigFile();
                Instance.TotalErrors += Instance.FileObjects.Pending() + Log.LogErrors;
                LogSummary();

                return Instance.TotalErrors;
            }
            catch (Exception ex)
            {
                Log.Exception($"{ex}");
                return 1;
            }
            finally
            {
                Close();
            }
        }

        public string DetermineClusterId()
        {
            string clusterId = string.Empty;

            if (!string.IsNullOrEmpty(Config.SasEndpointInfo.AbsolutePath))
            {
                //fabriclogs-e2fd6f05-921f-4e81-92d5-f70a648be762
                string pattern = ".+-([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})";

                if (Regex.IsMatch(Config.SasEndpointInfo.AbsolutePath, pattern))
                {
                    clusterId = Regex.Match(Config.SasEndpointInfo.AbsolutePath, pattern).Groups[1].Value;
                }
            }

            if (string.IsNullOrEmpty(clusterId))
            {
                TableManager tableMgr = new TableManager(Instance);

                if (tableMgr.Connect())
                {
                    clusterId = tableMgr.QueryTablesForClusterId();
                }
            }

            if (!string.IsNullOrEmpty(clusterId))
            {
                Log.Info($"cluster id:{clusterId}");
            }
            else
            {
                Log.Warning("unable to determine cluster id");
            }

            return clusterId;
        }

        public bool Initialize(ConfigurationOptions configurationOptions)
        {
            _noProgressCounter = 0;
            _noProgressTimer = new Timer(NoProgressCallback, null, 0, 60 * 1000);
            Instance.Initialize(configurationOptions);

            Log.Last($"version: {Config.Version}");

            if ((Config.NeedsValidation && !Config.Validate()) | !Config.IsValid)
            {
                return false;
            }

            _parallelConfig = new ParallelOptions { MaxDegreeOfParallelism = Config.Threads };

            ServicePointManager.DefaultConnectionLimit = Config.Threads * Constants.MaxThreadMultiplier;
            ServicePointManager.Expect100Continue = true;
#if NET462
#pragma warning disable CA5398
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12; //DevSkim: ignore DS440000,DS144436,DS440020
#pragma warning restore CA5398
#endif
            ThreadPool.SetMinThreads(Config.Threads * Constants.MinThreadMultiplier, Config.Threads * Constants.MinThreadMultiplier);
            ThreadPool.SetMaxThreads(Config.Threads * Constants.MaxThreadMultiplier, Config.Threads * Constants.MaxThreadMultiplier);

            return true;
        }

        private void DownloadAzureData()
        {
            string containerPrefix = null;
            string tablePrefix = null;
            string clusterId = DetermineClusterId();

            if (!Config.FileType.Equals(FileTypesEnum.any) && !Config.FileType.Equals(FileTypesEnum.table))
            {
                containerPrefix = FileTypes.MapFileTypeRelativeUriPrefix(Config.FileType);

                if (!string.IsNullOrEmpty(clusterId))
                {
                    containerPrefix += "-" + clusterId;
                }

                tablePrefix = containerPrefix + clusterId?.Replace("-", "");
            }

            if (Config.FileType == FileTypesEnum.table)
            {
                TableManager tableMgr = new TableManager(Instance)
                {
                    IngestCallback = (exportedFile) => { QueueForIngest(exportedFile); }
                };

                if (tableMgr.Connect())
                {
                    tableMgr.DownloadTables(tablePrefix);
                }
            }
            else
            {
                BlobManager blobMgr = new BlobManager(Instance)
                {
                    IngestCallback = (sourceFileUri) => { QueueForIngest(sourceFileUri); },
                    ReturnSourceFileLink = (Config.IsKustoConfigured() & Config.KustoUseBlobAsSource) | Config.FileType == FileTypesEnum.exception
                };

                if (blobMgr.Connect())
                {
                    string[] azureFiles = Config.FileUris.Where(x => FileTypes.MapFileUriType(x) == FileUriTypesEnum.azureStorageUri).ToArray();

                    if (azureFiles.Any())
                    {
                        blobMgr.DownloadFiles(azureFiles);
                        List<string> fileUris = Config.FileUris.ToList();
                        fileUris.RemoveAll(x => azureFiles.Contains(x));
                        Config.FileUris = fileUris.ToArray();
                    }
                    else
                    {
                        blobMgr.DownloadContainers(containerPrefix);
                    }
                }
            }
        }

        private void FinalizeKusto()
        {
            if (Config.IsKustoConfigured() && !Instance.Kusto.Complete())
            {
                Log.Warning($"there may have been errors during kusto import. {Config.CacheLocation} has *not* been deleted.");
            }
            else if (Config.IsKustoConfigured())
            {
                Log.Last($"{Constants.DataExplorer}/clusters/{Instance.Kusto.Endpoint.ClusterName}/databases/{Instance.Kusto.Endpoint.DatabaseName}", ConsoleColor.Cyan);
            }

            if (Instance.FileObjects.Any(FileStatus.failed | FileStatus.uploading))
            {
                Log.Warning($"adding failed uris to FileUris. use save option to keep list of failed uris.");
                List<string> ingestList = new List<string>();
                ingestList.AddRange(Instance.FileObjects.FindAll(FileStatus.failed | FileStatus.uploading).Select(x => x.FileUri));
                Config.FileUris = ingestList.ToArray();
            }
        }

        private bool InitializeKusto()
        {
            if (Config.IsKustoConfigured() | Config.IsKustoPurgeRequested())
            {
                return Instance.Kusto.Connect();
            }

            return true;
        }

        private bool InitializeLogAnalytics()
        {
            if (Config.IsLogAnalyticsConfigured() | Config.LogAnalyticsCreate | Config.IsLogAnalyticsPurgeRequested())
            {
                return Instance.LogAnalytics.Connect();
            }

            return true;
        }

        private void LogSummary()
        {
            Config.DisplayStatus();
            Log.Last($"{Instance.TotalFilesEnumerated} files enumerated.");
            Log.Last($"{Instance.TotalFilesMatched} files matched.");
            Log.Last($"{Instance.TotalFilesDownloaded} files downloaded.");
            Log.Last($"{Instance.TotalFilesFormatted} files formatted.");
            Log.Last($"{Instance.TotalFilesSkipped} files skipped.");
            Log.Last($"{Instance.TotalRecords} parsed events.");
            Log.Last($"timed out: {Instance.TimedOut}.");
            Log.Last($"{Instance.FileObjects.StatusString()}", ConsoleColor.Cyan);

            if (Config.List)
            {
                Log.Last($"file list:", jsonSerializer: Instance.FileObjects.Select(x => x.FileUri));
            }

            if (Instance.TotalFilesEnumerated > 0)
            {
                if (Config.FileType != FileTypesEnum.table)
                {
                    DateTime discoveredMinDateTime = new DateTime(Instance.DiscoveredMinDateTicks);
                    DateTime discoveredMaxDateTime = new DateTime(Instance.DiscoveredMaxDateTicks);

                    Log.Last($"discovered time range: {discoveredMinDateTime.ToString("o")} - {discoveredMaxDateTime.ToString("o")}", ConsoleColor.Green);

                    if (discoveredMinDateTime.Ticks > Config.EndTimeUtc.Ticks | discoveredMaxDateTime.Ticks < Config.StartTimeUtc.Ticks)
                    {
                        Log.Last($"error: configured time range not within discovered time range. configured time range: {Config.StartTimeUtc} - {Config.EndTimeUtc}", ConsoleColor.Red);
                    }
                }

                if (Instance.TotalFilesMatched + Instance.TotalRecords == 0
                    && (!string.IsNullOrEmpty(Config.UriFilter) | !string.IsNullOrEmpty(Config.ContainerFilter) | !string.IsNullOrEmpty(Config.NodeFilter)))
                {
                    Log.Last("0 records found and filters are configured. verify filters and / or try time range are correct.", ConsoleColor.Yellow);
                }
                else if (Instance.TotalFilesMatched + Instance.TotalRecords == 0)
                {
                    Log.Last("0 records found. verify time range is correct.", ConsoleColor.Yellow);
                }
            }
            else
            {
                Log.Last("0 files enumerated.", ConsoleColor.Red);
            }

            // do version check
            Config.CheckReleaseVersion();

            Log.Last($"{Instance.TotalErrors} errors.", Instance.TotalErrors > 0 ? ConsoleColor.Yellow : ConsoleColor.Green);
            Log.Last($"{Instance.FileObjects.Pending()} files failed to be processed.", Instance.FileObjects.Pending() > 0 ? ConsoleColor.Red : ConsoleColor.Green);
            Log.Last($"total execution time in minutes: {(DateTime.Now - Instance.StartTime).TotalMinutes.ToString("F2")}");
        }

        private void NoProgressCallback(object state)
        {
            Log.Highlight($"checking progress {_noProgressCounter} of {Config.NoProgressTimeoutMin}.");

            if (Config.NoProgressTimeoutMin < 1 | Instance.TaskManager.CancellationToken.IsCancellationRequested)
            {
                _noProgressTimer?.Dispose();
                return;
            }

            Total total = Instance.Totals();
            Log.Highlight($"totals:", total);

            if (total.Equals(_progressTotal))
            {
                if (_noProgressCounter >= Config.NoProgressTimeoutMin)
                {
                    if (Config.IsKustoConfigured())
                    {
                        Log.Warning($"kusto ingesting:", Instance.FileObjects.FindAll(FileStatus.uploading));
                        Log.Warning($"kusto failed:", Instance.FileObjects.FindAll(FileStatus.failed));
                    }

                    LogSummary();
                    string message = $"no progress timeout reached {Config.NoProgressTimeoutMin}. exiting application.";
                    Log.Error(message);

                    Instance.TimedOut = true;
                    CustomTaskManager.Cancel();
                    _noProgressTimer.Dispose();
                }

                ++_noProgressCounter;
            }
            else
            {
                _noProgressCounter = 0;
                _progressTotal = total;
            }
        }

        private void QueueForIngest(FileObject fileObject)
        {
            Log.Debug("enter");
            fileObject.Status = FileStatus.queued;

            if (Config.IsUploadConfigured())
            {
                if (Config.IsKustoConfigured())
                {
                    Instance.TaskManager.QueueTaskAction(() => Instance.Kusto.AddFile(fileObject));
                }

                if (Config.IsLogAnalyticsConfigured())
                {
                    Instance.TaskManager.QueueTaskAction(() => Instance.LogAnalytics.AddFile(fileObject));
                }
            }
            else
            {
                Instance.TaskManager.QueueTaskAction(() => Instance.FileMgr.ProcessFile(fileObject));
            }

            Log.Debug("exit");
        }

        private List<string> FilterFilesByTimeStamp(List<string> files)
        {
            List<string> filteredFiles = new List<string>();

            foreach (string file in files)
            {
                DateTime lastWriteTime = File.GetLastWriteTime(file);
                lastWriteTime = DateTime.SpecifyKind(lastWriteTime, DateTimeKind.Utc);
                DateTimeOffset lastWriteTimeOffset = lastWriteTime;

                if (lastWriteTimeOffset > Config.StartTimeUtc && lastWriteTimeOffset < Config.EndTimeUtc)
                {
                    filteredFiles.Add(file);
                }
            }
            return filteredFiles;

        }

        private void UploadCacheData()
        {
            Log.Info("enter");
            List<string> files = new List<string>();

            if (!Config.IsUploadConfigured())
            {
                // update status to succeeded if not configured for upload
                Instance.FileObjects.Where(x => x.Status == FileStatus.formatting).ToList().ForEach(x => x.Status = FileStatus.succeeded);
                Log.Info("config options not set for upload. returning");
                return;
            }

            string[] localFiles = Config.FileUris.Where(x => FileTypes.MapFileUriType(x) == FileUriTypesEnum.fileUri).ToArray();

            if (localFiles.Any())
            {
                List<string> fileUris = Config.FileUris.ToList();

                foreach (string file in localFiles)
                {
                    if (File.Exists(file))
                    {
                        Log.Info($"adding file to list: {file}");
                        files.Add(file);
                        fileUris.Remove(file);
                    }
                    else
                    {
                        Log.Warning($"file does not exist: {file}");
                    }
                }

                if (files.Any())
                {
                    Config.FileUris = fileUris.ToArray();
                }
                else
                {
                    string logString = $"configuration set to upload cache files from 'fileUris' count:{Config.FileUris.Length} but no files found";

                    if (Config.SasEndpointInfo.IsPopulated())
                    {
                        Log.Warning(logString);
                    }
                    else
                    {
                        Log.Error(logString);
                    }
                }
            }
            else if (Config.IsCacheLocationPreConfigured())
            {
                switch (Config.FileType)
                {
                    case FileTypesEnum.counter:
                        files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.PerfCtrExtension);

                        if (files.Count < 1)
                        {
                            files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.PerfCsvExtension);
                        }

                        break;

                    case FileTypesEnum.setup:
                        files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.SetupExtension);

                        break;

                    case FileTypesEnum.table:
                        files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.TableExtension);

                        break;

                    case FileTypesEnum.trace:
                        files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.DtrExtension + Constants.ZipExtension);

                        if (files.Count < 1)
                        {
                            files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.DtrExtension);
                        }

                        if (files.Count < 1)
                        {
                            files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.EtlExtension + Constants.ZipExtension);
                        }

                        if (files.Count < 1)
                        {
                            files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.EtlExtension);
                        }

                        break;

                    case FileTypesEnum.sfextlog:
                        files = Instance.FileMgr.GetFilesByExtension(Config.CacheLocation, Constants.LogExtension);

                        break;

                    default:
                        Log.Warning($"configured filetype:{Config.FileType} not valid for cache upload. returning.");
                        return;
                }

                if (files.Count < 1)
                {
                    Log.Error($"configuration set to upload cache files from 'cachelocation' {Config.CacheLocation} but no files found");
                }
            }

            files = FilterFilesByTimeStamp(files);

            // what is this value representing? should i do this before file filtering or after
            Instance.TotalFilesEnumerated += files.Count;

            foreach (string file in files)
            {
                FileObject fileObject = new FileObject(file, Config.CacheLocation) { Status = FileStatus.enumerated };

                // only queue if file not already in FileObjects list
                if (Instance.FileObjects.Add(fileObject))
                {
                    Log.Info($"adding file: {fileObject.FileUri}", ConsoleColor.Green);

                    if (!Config.List)
                    {
                        QueueForIngest(fileObject);
                    }
                }
                else
                {
                    Log.Debug($"file {fileObject.FileUri} already in FileObjects. not queueing for ingest.");
                }
            }
        }
    }
}