// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace CollectSFData.LogAnalytics
{
    public class LogAnalyticsWorkspaceModel
    {
        public string location;
        public Properties properties = new Properties();
        public Dictionary<string, string> tags = new Dictionary<string, string>();

        public class Properties
        {
            public string portalUrl;
            public int retentionInDays;
            public Sku sku = new Sku();
            public string source;

            public class Sku
            {
                public string name;
            }
        }
    }
}