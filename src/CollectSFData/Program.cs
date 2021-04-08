// ------------------------------------------------------------
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
            ConfigurationOptions config = collector.Config;

            int retval = collector.Collect();

            // mitigation for dtr files not being csv compliant causing kusto ingest to fail
            if ((collector.Instance.Kusto.IngestFileObjectsFailed.Count() > 0
                | collector.Instance.Kusto.IngestFileObjectsPending.Count() > 0)
                && config.IsKustoConfigured()
                && config.KustoUseBlobAsSource == true
                && config.FileType == DataFile.FileTypesEnum.trace)
            {
                KustoConnection kusto = collector.Instance.Kusto;
                Log.Warning("failed ingests due to csv compliance. restarting.");

                // change config to download files to parse and fix csv fields
                config.KustoUseBlobAsSource = false;
                config.KustoRecreateTable = false;

                List<string> ingestList = kusto.IngestFileObjectsFailed.Select(x => x.FileUri).ToList();
                ingestList.AddRange(kusto.IngestFileObjectsPending.Select(x => x.FileUri));
                config.FileUris = ingestList.ToArray();

                retval = collector.Collect();
            }

            return retval;
        }
    }
}