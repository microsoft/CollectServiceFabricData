﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData
{
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
        public static int Main(string[] args)
        {
            return new Program().Execute(args);
        }

        public int Execute(string[] args)
        {
            StartTime = DateTime.Now;

            try
            {
                if (!Config.PopulateConfig(args))
                {
                    Config.SaveConfigFile();
                    return 1;
                }

                Log.Info($"version: {Version}");
                ParallelConfig = new ParallelOptions { MaxDegreeOfParallelism = Config.Threads };
                ServicePointManager.DefaultConnectionLimit = Config.Threads * MaxThreadMultiplier;
                ThreadPool.SetMinThreads(Config.Threads * MinThreadMultiplier, Config.Threads * MinThreadMultiplier);
                ThreadPool.SetMaxThreads(Config.Threads * MaxThreadMultiplier, Config.Threads * MaxThreadMultiplier);

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

                Log.Info($"{TotalFilesEnumerated} files enumerated.");
                Log.Info($"{TotalFilesMatched} files matched.");
                Log.Info($"{TotalFilesDownloaded} files downloaded.");
                Log.Info($"{TotalFilesSkipped} files skipped.");
                Log.Info($"{TotalFilesFormatted} files formatted.");
                Log.Info($"{TotalErrors} errors.");
                Log.Info($"{TotalRecords} records.");
                Log.Info($"total execution time in minutes: { (DateTime.Now - StartTime).TotalMinutes.ToString("F2") }");

                return 0;
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

        public void QueueForIngest(FileObject fileObject)
        {
            Log.Debug("enter");

            if (Config.IsKustoConfigured())
            {
                TaskManager.QueueTaskAction(() => Kusto.AddFile(fileObject));
            }

            if (Config.IsLogAnalyticsConfigured())
            {
                TaskManager.QueueTaskAction(() => LogAnalytics.AddFile(fileObject));
            }
        }

        private string DetermineClusterId()
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

            Log.Info($"cluster id:{clusterId}");
            return clusterId;
        }

        private void DownloadAzureData()
        {
            string containerPrefix = null;
            string tablePrefix = null;
            string clusterId = DetermineClusterId();

            if (!Config.FileType.Equals(FileTypesEnum.any))
            {
                containerPrefix = FileTypes.MapFileTypeUriPrefix(Config.FileType);
                tablePrefix = containerPrefix + clusterId.Replace("-", "");
            }

            if (!string.IsNullOrEmpty(clusterId))
            {
                // 's-' in prefix may not always be correct
                containerPrefix += "s-" + clusterId;
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
                    ReturnSourceFileLink = Config.IsKustoConfigured() & Config.KustoUseBlobAsSource
                };

                if (blobMgr.Connect())
                {
                    blobMgr.DownloadContainers(containerPrefix);
                }
            }
        }

        private void FinalizeKusto()
        {
            if (Config.IsKustoConfigured() && !Kusto.Complete())
            {
                Log.Warning($"there may have been errors during kusto import. {Config.CacheLocation} has *not* been deleted.");
            }

            Log.Min($"https://dataexplorer.azure.com/clusters/{Kusto.Endpoint.ClusterName}/databases/{Kusto.Endpoint.DatabaseName}");
        }

        private bool InitializeKusto()
        {
            if (Config.IsKustoConfigured() | Config.IsKustoPurgeRequested())
            {
                Kusto = new KustoConnection();
                return Kusto.Connect();
            }

            return true;
        }

        private bool InitializeLogAnalytics()
        {
            if (Config.IsLogAnalyticsConfigured() | Config.LogAnalyticsCreate | Config.IsLogAnalyticsPurgeRequested())
            {
                LogAnalytics = new LogAnalyticsConnection();
                return LogAnalytics.Connect();
            }

            return true;
        }

        private void UploadCacheData()
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
    }
}