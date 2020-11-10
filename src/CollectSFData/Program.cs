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

            // to subscribe to log messages
            // Log.MessageLogged += Log_MessageLogged;

            // to modify / validate config
            // collector.Instance.Config.Validate();

            return collector.Collect(args);
        }

        private static void Log_MessageLogged(object sender, LogMessage args)
        {
            throw new NotImplementedException();
        }
    }
}