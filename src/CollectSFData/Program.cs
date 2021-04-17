﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using CollectSFData.Kusto;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectSFData
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            if (!Environment.Is64BitOperatingSystem | Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("only supported on win32 x64");
            }

            Collector collector = new Collector(args, true);
            ConfigurationOptions config = new ConfigurationOptions();
            int retval = collector.Collect(config);

            // mitigation for dtr files not being csv compliant causing kusto ingest to fail
            if (config.IsKustoConfigured()
                && (collector.Instance.Kusto.IngestFileObjectsFailed.Any() | collector.Instance.Kusto.IngestFileObjectsPending.Any())
                && config.KustoUseBlobAsSource == true
                && config.FileType == DataFile.FileTypesEnum.trace)
            {
                Log.Warning("failed ingests due to csv compliance. restarting.");

                // change config to download files to parse and fix csv fields
                config.KustoUseBlobAsSource = false;
                config.KustoRecreateTable = false;

                retval = collector.Collect();
            }

            return retval;
        }
    }
}