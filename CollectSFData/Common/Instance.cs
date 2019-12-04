// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Text;
using System.Threading.Tasks;

namespace CollectSFData
{
    public class Instance : Constants
    {
        public static SynchronizedList<string> DisplayMessages = new SynchronizedList<string>();
        public static ConfigurationOptions Config = new ConfigurationOptions();
        public static FileManager FileMgr = new FileManager();
        public static KustoConnection Kusto = null;
        public static LogAnalyticsConnection LogAnalytics = null;
        public static ParallelOptions ParallelConfig;
        public static DateTime StartTime;
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