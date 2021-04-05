// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using CollectSFData.Kusto;
using System;
using System.Linq;

namespace CollectSFData
{
    internal class Program
    {
        // to subscribe to log messages
        // Log.MessageLogged += Log_MessageLogged;
        private static void Log_MessageLogged(object sender, LogMessage args)
        {
            throw new NotImplementedException();
        }

        private static int Main(string[] args)
        {
            if (!Environment.Is64BitOperatingSystem | Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("only supported on win32 x64");
            }

            // default constructor
            Collector collector = new Collector(args, true);

            // use config to modify / validate config
            // config.Validate();
            ConfigurationOptions config = collector.Instance.Config;

            // collect data
            int retval = collector.Collect();

            // mitigation for dtr files not being csv compliant causing kusto ingest to fail
            if (collector.Instance.Kusto.IngestFileObjectsFailed.Count() > 0
                && config.IsKustoConfigured()
                && config.KustoUseBlobAsSource == true
                && config.FileType == DataFile.FileTypesEnum.trace)
            {
                KustoConnection kusto = collector.Instance.Kusto;
                Log.Warning("failed ingests due to csv compliance. restarting.");

                // change config to download files to parse and fix csv fields
                config.KustoUseBlobAsSource = false;
                config.KustoRecreateTable = false;
                config.FileUris = kusto.IngestFileObjectsFailed.Select(x => x.FileUri).ToArray();
                retval = collector.Collect();
            }

            return retval;
        }
    }
}