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
        public long DiscoveredMaxDateTicks { get; set; }
        public long DiscoveredMinDateTicks { get; set; }
        public FileManager FileMgr { get; set; }
        public FileObjectCollection FileObjects { get; set; }
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
        protected internal ConfigurationOptions Config { get; private set; } = new ConfigurationOptions();
        protected internal CustomTaskManager TaskManager { get; private set; }

        static Instance()
        {
        }

        public Instance(ConfigurationOptions configurationOptions = null)
        {
            Initialize(configurationOptions);
        }

        public void Close()
        {
            CustomTaskManager.Cancel();
        }

        public void Initialize(ConfigurationOptions configurationOptions = null)
        {
            TaskManager = new CustomTaskManager() { Instance = this };

            if (configurationOptions == null)
            {
                configurationOptions = new ConfigurationOptions();
            }

            Config = configurationOptions;
            Log.Config = Config;
            DiscoveredMaxDateTicks = DateTime.MinValue.Ticks;
            DiscoveredMinDateTicks = DateTime.MaxValue.Ticks;
            FileObjects = new FileObjectCollection();
            FileMgr = new FileManager(this);
            Kusto = new KustoConnection(this);
            LogAnalytics = new LogAnalyticsConnection(this);
            StartTime = DateTime.Now;
            TimedOut = false;
            TotalErrors = 0;
            TotalFilesConverted = 0;
            TotalFilesDownloaded = 0;
            TotalFilesEnumerated = 0;
            TotalFilesFormatted = 0;
            TotalFilesMatched = 0;
            TotalFilesSkipped = 0;
            TotalRecords = 0;
        }

        public void SetMaxDate(long maxDateTicks)
        {
            if (maxDateTicks > DiscoveredMaxDateTicks && maxDateTicks < DateTime.MaxValue.Ticks)
            {
                DiscoveredMaxDateTicks = maxDateTicks;
                Log.Debug($"set new discovered max time range ticks: {new DateTime(maxDateTicks).ToString("o")}");
            }
        }

        public void SetMinDate(long minDateTicks)
        {
            if (minDateTicks < DiscoveredMinDateTicks && minDateTicks > DateTime.MinValue.Ticks)
            {
                DiscoveredMinDateTicks = minDateTicks;
                Log.Debug($"set new discovered min time range ticks: {new DateTime(minDateTicks).ToString("o")}");
            }
        }

        public void SetMinMaxDate(long ticks)
        {
            SetMaxDate(ticks);
            SetMinDate(ticks);
        }

        public void SetMinMaxDate(long minDateTicks, long maxDateTicks)
        {
            SetMaxDate(minDateTicks);
            SetMinDate(maxDateTicks);
        }

    }
}