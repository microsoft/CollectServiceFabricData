// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;
using System.Collections.Generic;

namespace CollectSFDataTest.Utilities
{
    public class ProcessOutput
    {
        public bool ExitBool { get; internal set; }

        public int ExitCode { get; set; } = 0;

        public List<string> LogMessages { get; internal set; }

        public string StandardError { get; set; } = "";

        public string StandardOutput { get; set; } = "";

        public bool HasErrors()
        {
            return !string.IsNullOrEmpty(StandardError) | (ExitCode != 0 & ExitBool == false);
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}