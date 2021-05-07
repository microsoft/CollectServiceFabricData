// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tools.EtlReader;

namespace CollectSFData.DataFile
{
    public class EtlTraceFileParser<T> where T : ITraceRecord, new()
    {
        private ConfigurationOptions Config => Instance.Singleton().Config;
        public static ManifestCache ManifestCache { get; set; }
        private readonly Action<T> _traceDispatcher;
        public int EventsLost { get; private set; }
        public TraceSessionMetadata TraceSessionMetaData { get; private set; }

        public EtlTraceFileParser(Action<T> traceDispatcher, ManifestCache cache = null)
        {
            if (cache != null)
            {
                ManifestCache = cache;
            }

            if (ManifestCache == null)
            {
                ManifestCache = LoadManifests(Config.EtwManifestCache, Config.CacheLocation);
            }

            _traceDispatcher = traceDispatcher;
        }

        public ManifestCache LoadManifests(string manifestPath, string cacheLocation)
        {
            if (Directory.Exists(manifestPath) && Directory.Exists(cacheLocation))
            {
                List<string> manifestFiles = Directory.GetFiles(manifestPath, $"*{Constants.ManifestExtension}").ToList();
                Log.Info("manifest files:", manifestFiles);
                manifestFiles.ForEach(x =>
                {
                    string fileKey = FileManager.NormalizePath(x);
                    if (!ManifestCache.Guids.ContainsKey(fileKey))
                    {
                        ManifestCache.LoadManifest(x);
                    }
                });
            }
            else
            {
                Log.Error($"manifest path does not exist:{manifestPath} or cachelocation does not exist:{cacheLocation}");
            }

            return ManifestCache;
        }
        
        public void ParseTraces(string fileName, DateTime startTime, DateTime endTime)
        {
            using (var reader = new TraceFileEventReader(fileName))
            {
                reader.EventRead += this.OnEventRead;
                reader.ReadEvents(startTime, endTime);
                EventsLost = (int)reader.EventsLost;
                TraceSessionMetaData = reader.ReadTraceSessionMetadata();
            }
        }

        private void OnEventRead(object sender, EventRecordEventArgs e)
        {
            string eventType = null;
            string eventText = null;
            int formatVersion = 0;
            ManifestCache.FormatEvent(e.Record, out eventType, out eventText, formatVersion);

            EventDefinition eventDefinition = ManifestCache.GetEventDefinition(e.Record);
            T traceEvent = new T()
            {
                Timestamp = DateTime.FromFileTimeUtc(e.Record.EventHeader.TimeStamp),
                Level = eventDefinition?.Level ?? e.Record.EventHeader.EventDescriptor.Level.ToString(),
                TID = (int)e.Record.EventHeader.ThreadId,
                PID = (int)e.Record.EventHeader.ProcessId,
                Type = $"{eventDefinition?.TaskName}.{eventDefinition?.EventName}", //eventType, // todo:not complete string
                Text = eventText?.Replace("\n", "\t"),
            };

            _traceDispatcher(traceEvent);
        }
    }
}