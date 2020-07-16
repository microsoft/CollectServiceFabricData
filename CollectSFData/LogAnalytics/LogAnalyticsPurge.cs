// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData.LogAnalytics
{
    public class LogAnalyticsPurge
    {
        public Filters[] filters { get; set; }

        public string table { get; set; }

        public class Filters
        {
            public string @operator { get; set; }

            public string column { get; set; }

            public string value { get; set; }
        }
    }
}