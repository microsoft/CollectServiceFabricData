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
        protected internal ConfigurationOptions Config;
        public FileManager FileMgr;
        public bool IsWindows = Environment.OSVersion.Platform.Equals(PlatformID.Win32NT);
        public KustoConnection Kusto;
        public LogAnalyticsConnection LogAnalytics;
        public DateTime StartTime;
        public int TotalErrors;
        public int TotalFilesConverted;
        public int TotalFilesDownloaded;
        public int TotalFilesEnumerated;
        public int TotalFilesFormatted;
        public int TotalFilesMatched;
        public int TotalFilesSkipped;
        public int TotalRecords;
        private static readonly Instance _instance = new Instance();
        private static object _instanceLock = new object();

        static Instance()
        {
            // set instances in static ctor
            if (_instance.Config == null)
            {
                _instance.Config = new ConfigurationOptions();
                Initialize();
            }
        }

        private Instance()
        {
        }

        public static void Initialize()
        {
            _instance.FileMgr = new FileManager();
            _instance.Kusto = new KustoConnection();
            _instance.LogAnalytics = new LogAnalyticsConnection();
            _instance.StartTime = DateTime.Now;
            _instance.TotalErrors = 0;
            _instance.TotalFilesConverted = 0;
            _instance.TotalFilesDownloaded = 0;
            _instance.TotalFilesEnumerated = 0;
            _instance.TotalFilesFormatted = 0;
            _instance.TotalFilesMatched = 0;
            _instance.TotalFilesSkipped = 0;
            _instance.TotalRecords = 0;
        }

        public static Instance Singleton()
        {
            return _instance;
        }
    }
}