// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace CollectSFData.Common
{
    public class Total
    {
        public int Converted;
        public int Downloading;
        public int Downloaded;
        public int Enumerated;
        public int Formatting;
        public int Existing;
        public int Errors;
        public int Failed;
        public int Formatted;
        public int Matched;
        public int Queued;
        public int Records;
        public int Skipped;
        public int Succeeded;
        public int Unknown;
        public int Uploading;

        public override bool Equals(object obj)
        {
            return obj is Total total &&
                   Converted == total.Converted &&
                   Downloading == total.Downloading &&
                   Downloaded == total.Downloaded &&
                   Enumerated == total.Enumerated &&
                   Formatting == total.Formatting &&
                   Existing == total.Existing &&
                   Errors == total.Errors &&
                   Failed == total.Failed &&
                   Formatted == total.Formatted &&
                   Matched == total.Matched &&
                   Queued == total.Queued &&
                   Records == total.Records &&
                   Skipped == total.Skipped &&
                   Succeeded == total.Succeeded &&
                   Unknown == total.Unknown &&
                   Uploading == total.Uploading;
        }

        public override int GetHashCode()
        {
            Dictionary<string,int> hash = new Dictionary<string,int>();
            hash.Add("Converted",Converted);
            hash.Add("Downloading",Downloading);
            hash.Add("Downloaded",Downloaded);
            hash.Add("Enumerated",Enumerated);
            hash.Add("Formatting",Formatting);
            hash.Add("Existing",Existing);
            hash.Add("Errors",Errors);
            hash.Add("Failed",Failed);
            hash.Add("Formatted",Formatted);
            hash.Add("Matched",Matched);
            hash.Add("Queued",Queued);
            hash.Add("Records",Records);
            hash.Add("Skipped",Skipped);
            hash.Add("Succeeded",Succeeded);
            hash.Add("Unknown",Unknown);
            hash.Add("Uploading",Uploading);
            return hash.GetHashCode();
        }
    }
}