// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData.Common
{
    using CollectSFData.Azure;
    using CollectSFData.DataFile;
    using CollectSFData.Kusto;
    using CollectSFData.LogAnalytics;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class Program : Instance
    {
        private KustoConnection _kusto = null;
        private LogAnalyticsConnection _logAnalytics = null;
        private int _noProgressCounter = 0;
        private Timer _noProgressTimer;
        private ParallelOptions _parallelConfig;
        private Tuple<int, int, int, int, int, int, int> _progressTuple = new Tuple<int, int, int, int, int, int, int>(0, 0, 0, 0, 0, 0, 0);
        private CustomTaskManager _taskManager = new CustomTaskManager(true);

        public static int Main(string[] args)
        {
            return new Program().Execute(args);
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
                TableManager tableMgr = new TableManager();

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

        public void DownloadAzureData()
        {
            string containerPrefix = null;
            string tablePrefix = null;
            string clusterId = DetermineClusterId();

            if (!Config.FileType.Equals(FileTypesEnum.any) && !Config.FileType.Equals(FileTypesEnum.table))
            {
                containerPrefix = FileTypes.MapFileTypeUriPrefix(Config.FileType);

                if (!string.IsNullOrEmpty(clusterId))
                {
                    // 's-' in prefix may not always be correct
                    containerPrefix += "s-" + clusterId;
                }

                tablePrefix = containerPrefix + clusterId?.Replace("-", "");
            }

            if (Config.FileType == FileTypesEnum.table)
            {
                TableManager tableMgr = new TableManager()
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
                BlobManager blobMgr = new BlobManager()
                {
                    IngestCallback = (sourceFileUri) => { QueueForIngest(sourceFileUri); },
                    ReturnSourceFileLink = (Config.IsKustoConfigured() & Config.KustoUseBlobAsSource) | Config.FileType == FileTypesEnum.exception
                };

                if (blobMgr.Connect())
                {
                    blobMgr.DownloadContainers(containerPrefix);
                }
            }
        }

        public int Execute(string[] args)
        {
            try
            {
                if (!Config.PopulateConfig(args))
                {
                    Config.SaveConfigFile();
                    return 1;
                }

                Log.Info($"version: {Version}");
                _parallelConfig = new ParallelOptions { MaxDegreeOfParallelism = Config.Threads };
                ServicePointManager.DefaultConnectionLimit = Config.Threads * MaxThreadMultiplier;
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                ThreadPool.SetMinThreads(Config.Threads * MinThreadMultiplier, Config.Threads * MinThreadMultiplier);
                ThreadPool.SetMaxThreads(Config.Threads * MaxThreadMultiplier, Config.Threads * MaxThreadMultiplier);

                if (Config.NoProgressTimeoutMin > 0)
                {
                    _noProgressTimer = new Timer(NoProgressCallback, null, 0, 60 * 1000);
                }

                if (!InitializeKusto() | !InitializeLogAnalytics())
                {
                    return 1;
                }

                if (Config.SasEndpointInfo.IsPopulated())
                {
                    DownloadAzureData();
                }
                else if (Config.IsCacheLocationPreConfigured())
                {
                    UploadCacheData();
                }

                CustomTaskManager.WaitAll();
                FinalizeKusto();
                CustomTaskManager.Close();

                if (Config.DeleteCache & Config.IsCacheLocationPreConfigured())
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

                Config.DisplayStatus();
                Config.SaveConfigFile();
                TotalErrors += Log.LogErrors;

                Log.Last($"{TotalFilesEnumerated} files enumerated.");
                Log.Last($"{TotalFilesMatched} files matched.");
                Log.Last($"{TotalFilesDownloaded} files downloaded.");
                Log.Last($"{TotalFilesSkipped} files skipped.");
                Log.Last($"{TotalFilesFormatted} files formatted.");
                Log.Last($"{TotalErrors} errors.");
                Log.Last($"{TotalRecords} records.");

                if (TotalFilesEnumerated > 0)
                {
                    if (Config.FileType != FileTypesEnum.table)
                    {
                        DateTime discoveredMinDateTime = new DateTime(DiscoveredMinDateTicks);
                        DateTime discoveredMaxDateTime = new DateTime(DiscoveredMaxDateTicks);

                        Log.Last($"discovered time range: {discoveredMinDateTime.ToString("o")} - {discoveredMaxDateTime.ToString("o")}", ConsoleColor.Green);

                        if (discoveredMinDateTime.Ticks > Config.EndTimeUtc.Ticks | discoveredMaxDateTime.Ticks < Config.StartTimeUtc.Ticks)
                        {
                            Log.Last($"error: configured time range not within discovered time range. configured time range: {Config.StartTimeUtc} - {Config.EndTimeUtc}", ConsoleColor.Red);
                        }
                    }

                    if (TotalFilesMatched + TotalRecords == 0 && (!string.IsNullOrEmpty(Config.UriFilter) | !string.IsNullOrEmpty(Config.ContainerFilter) | !string.IsNullOrEmpty(Config.NodeFilter)))
                    {
                        Log.Last("0 records found and filters are configured. verify filters and / or try time range are correct.", ConsoleColor.Yellow);
                    }
                    else if (TotalFilesMatched + TotalRecords == 0)
                    {
                        Log.Last("0 records found. verify time range is correct.", ConsoleColor.Yellow);
                    }
                }
                else
                {
                    Log.Last("0 files enumerated.", ConsoleColor.Red);
                }

                Log.Last($"total execution time in minutes: { (DateTime.Now - StartTime).TotalMinutes.ToString("F2") }");
                return TotalErrors;
            }
            catch (Exception ex)
            {
                Log.Exception($"{ex}");
                return 1;
            }
            finally
            {
                Log.Close();
            }
        }

        public void FinalizeKusto()
        {
            if (Config.IsKustoConfigured() && !_kusto.Complete())
            {
                Log.Warning($"there may have been errors during kusto import. {Config.CacheLocation} has *not* been deleted.");
            }
            else if (Config.IsKustoConfigured())
            {
                Log.Last($"{DataExplorer}/clusters/{_kusto.Endpoint.ClusterName}/databases/{_kusto.Endpoint.DatabaseName}", ConsoleColor.Cyan);
            }
        }

        public bool InitializeKusto()
        {
            if (Config.IsKustoConfigured() | Config.IsKustoPurgeRequested())
            {
                _kusto = new KustoConnection();
                return _kusto.Connect();
            }

            return true;
        }

        public bool InitializeLogAnalytics()
        {
            if (Config.IsLogAnalyticsConfigured() | Config.LogAnalyticsCreate | Config.IsLogAnalyticsPurgeRequested())
            {
                _logAnalytics = new LogAnalyticsConnection();
                return _logAnalytics.Connect();
            }

            return true;
        }

        public void QueueForIngest(FileObject fileObject)
        {
            Log.Debug("enter");

            if (Config.IsKustoConfigured() | Config.IsLogAnalyticsConfigured())
            {
                if (Config.IsKustoConfigured())
                {
                    _taskManager.QueueTaskAction(() => _kusto.AddFile(fileObject));
                }

                if (Config.IsLogAnalyticsConfigured())
                {
                    _taskManager.QueueTaskAction(() => _logAnalytics.AddFile(fileObject));
                }
            }
            else
            {
                _taskManager.QueueTaskAction(() => FileMgr.ProcessFile(fileObject));
            }
        }

        public void UploadCacheData()
        {
            Log.Info("enter");
            List<string> files = new List<string>();

            switch (Config.FileType)
            {
                case FileTypesEnum.counter:
                    files = Directory.GetFiles(Config.CacheLocation, $"*{PerfCtrExtension}", SearchOption.AllDirectories).ToList();

                    if (files.Count < 1)
                    {
                        files = Directory.GetFiles(Config.CacheLocation, $"*{PerfCsvExtension}", SearchOption.AllDirectories).ToList();
                    }

                    break;

                case FileTypesEnum.setup:
                    files = Directory.GetFiles(Config.CacheLocation, $"*{SetupExtension}", SearchOption.AllDirectories).ToList();

                    break;

                case FileTypesEnum.table:
                    files = Directory.GetFiles(Config.CacheLocation, $"*{TableExtension}", SearchOption.AllDirectories).ToList();

                    break;

                case FileTypesEnum.trace:
                    files = Directory.GetFiles(Config.CacheLocation, $"*{TraceFileExtension}{ZipExtension}", SearchOption.AllDirectories).ToList();

                    if (files.Count < 1)
                    {
                        files = Directory.GetFiles(Config.CacheLocation, $"*{TraceFileExtension}", SearchOption.AllDirectories).ToList();
                    }

                    break;

                default:
                    Log.Warning($"invalid filetype for cache upload. returning {Config.FileType}");
                    return;
            }

            if (files.Count < 1)
            {
                Log.Error($"configuration set to upload cache files from 'cachelocation' {Config.CacheLocation} but no files found");
            }

            foreach (string file in files)
            {
                FileObject fileObject = new FileObject(file, Config.CacheLocation);
                Log.Info($"adding file: {fileObject.FileUri}", ConsoleColor.Green);

                if (!Config.List)
                {
                    QueueForIngest(fileObject);
                }
            }
        }

        private void NoProgressCallback(object state)
        {
            Log.Highlight($"checking progress {_noProgressCounter} of {Config.NoProgressTimeoutMin}.");

            Tuple<int, int, int, int, int, int, int> tuple = new Tuple<int, int, int, int, int, int, int>(
                TotalErrors,
                TotalFilesDownloaded,
                TotalFilesEnumerated,
                TotalFilesFormatted,
                TotalFilesMatched,
                TotalFilesSkipped,
                TotalRecords);

            if (tuple.Equals(_progressTuple))
            {
                if (_noProgressCounter >= Config.NoProgressTimeoutMin)
                {
                    Log.Info("progress tuple:", tuple);
                    string message = $"no progress timeout reached {Config.NoProgressTimeoutMin}. exiting application.";
                    Log.Error(message);
                    Log.Close();
                    throw new TimeoutException(message);
                }
                ++_noProgressCounter;
            }
            else
            {
                _noProgressCounter = 0;
                _progressTuple = tuple;
            }
        }
    }
}