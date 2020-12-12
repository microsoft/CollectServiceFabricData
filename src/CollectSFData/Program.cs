// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Common;
using System;

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

            Collector collector = new Collector(true);
            ConfigurationOptions config = collector.Instance.Config;

            // to modify / validate config
            // config.Validate();

            int retval = collector.Collect(args);
            // to subscribe to log messages
            // Log.MessageLogged += Log_MessageLogged;
            
            // mitigation for dtr files not being csv compliant causing kusto ingest to fail
            if(collector.Instance.Kusto.IngestFileObjectsFailed.Count() > 0 
                && config.IsKustoConfigured()
                && config.KustoUseBlobAsSource == true
                && config.FileType == DataFile.FileTypesEnum.trace)
            {
                config.KustoUseBlobAsSource = false;
                config.KustoRecreateTable = false;
                //collector.Instance.Kusto.FailIngestedUris.ForEach( x => collector.QueueForIngest(new DataFile.FileObject(x)));
                retval = collector.Collect(args);
            }
            
            return retval;
        }

        private static void Log_MessageLogged(object sender, LogMessage args)
        {
            throw new NotImplementedException();
        }
    }
}