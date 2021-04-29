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
    public class Instance
    {
        private static readonly Instance _instance = new Instance();
        public long DiscoveredMaxDateTicks { get; set; }
        public long DiscoveredMinDateTicks { get; set; }
        public FileManager FileMgr { get; set; }
        public FileObjectCollection FileObjects { get; set; }
        public bool IsWindows { get; } = Environment.OSVersion.Platform.Equals(PlatformID.Win32NT);
        public KustoConnection Kusto { get; set; }
        public LogAnalyticsConnection LogAnalytics { get; set; }
        public DateTime StartTime { get; set; }
        public bool TimedOut { get; set; }
        public int TotalErrors { get; set; }
        public int TotalFilesConverted { get; set; }
        public int TotalFilesDownloaded { get; set; }
        public int TotalFilesEnumerated { get; set; }
        public int TotalFilesFormatted { get; set; }
        public int TotalFilesMatched { get; set; }
        public int TotalFilesSkipped { get; set; }
        public int TotalRecords { get; set; }
        protected internal ConfigurationOptions Config { get; private set; }

        static Instance()
        {
            Initialize();
        }

        private Instance()
        {
        }

        public static void Initialize(ConfigurationOptions configurationOptions = null)
        {
            if (configurationOptions == null)
            {
                configurationOptions = new ConfigurationOptions();
            }

            _instance.Config = configurationOptions;
            _instance.DiscoveredMaxDateTicks = DateTime.MinValue.Ticks;
            _instance.DiscoveredMinDateTicks = DateTime.MaxValue.Ticks;
            _instance.FileObjects = new FileObjectCollection();
            _instance.FileMgr = new FileManager();
            _instance.Kusto = new KustoConnection();
            _instance.LogAnalytics = new LogAnalyticsConnection();
            _instance.StartTime = DateTime.Now;
            _instance.TimedOut = false;
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