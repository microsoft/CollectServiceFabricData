﻿// ------------------------------------------------------------
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
        public bool TimedOut;
        public int TotalErrors;
        public int TotalFilesConverted;
        public int TotalFilesDownloaded;
        public int TotalFilesEnumerated;
        public int TotalFilesFormatted;
        public int TotalFilesMatched;
        public int TotalFilesSkipped;
        public int TotalRecords;
        private static readonly Instance _instance = new Instance();
        public FileManager FileMgr { get; set; }
        public FileObjectCollection FileObjects{get; set;}
        public bool IsWindows { get; } = Environment.OSVersion.Platform.Equals(PlatformID.Win32NT);
        public KustoConnection Kusto { get; set; }
        public LogAnalyticsConnection LogAnalytics { get; set; }
        public DateTime StartTime { get; set; }
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