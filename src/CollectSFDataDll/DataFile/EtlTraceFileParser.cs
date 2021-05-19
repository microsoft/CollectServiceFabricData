// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Tools.EtlReader;

namespace CollectSFData.DataFile
{
    public class EtlTraceFileParser<T> where T : ITraceRecord, new()
    {
        private readonly Action<T> _traceDispatcher;
        private ConfigurationOptions _config;
        public static ManifestCache ManifestCache { get; set; }
        public int EventsLost { get; private set; }
        public TraceSessionMetadata TraceSessionMetaData { get; private set; }

        public EtlTraceFileParser(Action<T> traceDispatcher, ConfigurationOptions config, ManifestCache cache = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (cache != null)
            {
                ManifestCache = cache;
            }

            if (ManifestCache == null)
            {
                ManifestCache = LoadManifests(_config.EtwManifestCache, _config.CacheLocation);
            }

            _traceDispatcher = traceDispatcher;
        }

        public ManifestCache LoadManifests(string manifestPath, string cacheLocation, string versionString = null)
        {
            Version version = null;
            if (!Version.TryParse(versionString, out version))
            {
                Log.Debug("unknown version:{versionString}");
                version = new Version();
            }
            return LoadManifests(manifestPath, cacheLocation, version);
        }

        public ManifestCache LoadManifests(string manifestPath, string cacheLocation, Version version)
        {
            string versionString = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            if (ManifestCache == null)
            {
                ManifestCache = new ManifestCache(cacheLocation);
            }

            ManifestLoader manifest = new ManifestLoader();

            if (Directory.Exists(manifestPath) && Directory.Exists(cacheLocation))
            {
                List<string> manifestFiles = Directory.GetFiles(manifestPath, $"*{Constants.ManifestExtension}").ToList();
                Log.Info("manifest files:", manifestFiles);

                if (version != new Version())
                {
                    manifestFiles = manifestFiles.Where(x => Regex.IsMatch(x, Regex.Escape(versionString))).ToList();
                }
                else
                {
                    Log.Info("getting latest version");
                    string versionPattern = @"_(\d+?\.\d+?\.\d+?\.\d+?)(?:_|\.)";
                    Version maxVersion = new Version();
                    List<string> versionedManifestFiles = manifestFiles.Where(x => Regex.IsMatch(x, versionPattern)).ToList();
                    List<string> unVersionedManifestFiles = manifestFiles.Where(x => !Regex.IsMatch(x, versionPattern)).ToList();

                    foreach (string file in versionedManifestFiles)
                    {
                        Version fileVersion = new Version(Regex.Match(file, versionPattern).Groups[1].Value);
                        if (fileVersion > maxVersion)
                        {
                            Log.Info($"setting maxVersion:{maxVersion} -> {fileVersion}");
                            maxVersion = fileVersion;
                        }
                    }

                    versionedManifestFiles = manifestFiles.Where(x => Regex.IsMatch(x, $@"_{maxVersion.Major}\.{maxVersion.Minor}\.{maxVersion.Build}\.{maxVersion.Revision}(?:_|\.)")).ToList();
                    unVersionedManifestFiles.AddRange(versionedManifestFiles);
                    manifestFiles = unVersionedManifestFiles;
                }

                Log.Info("filtered manifest files:", manifestFiles);

                foreach (string manifestFile in manifestFiles)
                {
                    ManifestDefinitionDescription description = manifest.LoadManifest(manifestFile);
                    List<ProviderDefinitionDescription> manifestProviderList = description.Providers.ToList();

                    if (!ManifestCache.ProviderDefinitions.Keys.Any(x => manifestProviderList.Any(y => y.Guid == x)))
                    {
                        ManifestCache.LoadManifest(manifestFile);
                    }
                    else
                    {
                        Log.Warning($"manifest already loaded:{manifestFile}");
                    }
                }
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