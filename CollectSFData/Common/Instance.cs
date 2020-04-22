// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using System.Threading;

namespace CollectSFData
{
    public class Instance : Constants
    {
        public static ConfigurationOptions Config = new ConfigurationOptions();
        public static long DiscoveredMaxDateTicks = DateTime.MinValue.Ticks;
        public static long DiscoveredMinDateTicks = DateTime.MaxValue.Ticks;
        public static FileManager FileMgr = new FileManager();
        public static KustoConnection Kusto = null;
        public static LogAnalyticsConnection LogAnalytics = null;
        public static Timer NoProgressTimer;
        public static ParallelOptions ParallelConfig;
        public static DateTime StartTime = DateTime.Now;
        public static CustomTaskManager TaskManager = new CustomTaskManager(true);
        public static int TotalErrors = 0;
        public static int TotalFilesConverted = 0;
        public static int TotalFilesDownloaded = 0;
        public static int TotalFilesEnumerated = 0;
        public static int TotalFilesFormatted = 0;
        public static int TotalFilesMatched = 0;
        public static int TotalFilesSkipped = 0;
        public static int TotalRecords = 0;
    }
}