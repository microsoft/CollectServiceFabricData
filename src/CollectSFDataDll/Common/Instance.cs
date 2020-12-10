// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.DataFile;
using CollectSFData.Kusto;
using CollectSFData.LogAnalytics;
using System;

namespace CollectSFData.Common
{
    public class Instance : Constants
    {
        private static readonly Instance _instance = new Instance();
        private static object _instanceLock = new object();

        static Instance()
        {
            // set instances in static ctor
            if (_instance.Config == null)
            {
                _instance.Config = new ConfigurationOptions();
                _instance.FileMgr = new FileManager();
                _instance.Kusto = new KustoConnection();
                _instance.LogAnalytics = new LogAnalyticsConnection();
            }
        }

        public static Instance Singleton()
        {
            return _instance;
        }

        private Instance()
        {
        }

        public ConfigurationOptions Config;
        public FileManager FileMgr;
        public bool IsWindows = Environment.OSVersion.Platform.Equals(PlatformID.Win32NT);
        public KustoConnection Kusto;
        public LogAnalyticsConnection LogAnalytics;
        public DateTime StartTime = DateTime.Now;
        public int TotalErrors = 0;
        public int TotalFilesConverted = 0;
        public int TotalFilesDownloaded = 0;
        public int TotalFilesEnumerated = 0;
        public int TotalFilesFormatted = 0;
        public int TotalFilesMatched = 0;
        public int TotalFilesSkipped = 0;
        public int TotalRecords = 0;
    }
}