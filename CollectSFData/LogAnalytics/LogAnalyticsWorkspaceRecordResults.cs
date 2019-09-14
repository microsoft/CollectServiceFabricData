// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace CollectSFData
{
    public class LogAnalyticsWorkspaceRecordResult
    {
        public string id;
        public string location;
        public string name;
        public WorkspaceProperties properties = new WorkspaceProperties();
        public string type;

        public class WorkspaceProperties
        {
            public string customerId;
            public Features features = new Features();
            public string portalUrl;
            public string provisioningState;
            public int retentionInDays;
            public Sku sku = new Sku();
            public string source;
            public WorkspaceCapping workspaceCapping = new WorkspaceCapping();

            public class Features
            {
                public bool enableLogAccessUsingOnlyResourcePermissions;
                public int legacy;
                public int searchVersion;
            }

            public class Sku
            {
                public string name;
            }

            public class WorkspaceCapping
            {
                public decimal dailyQuotaGb;
                public string dataIngestionStatus;
                public string quotaNextResetTime;
            }
        }
    }

    public class LogAnalyticsWorkspaceRecordResults
    {
        public LogAnalyticsWorkspaceRecordResult[] value;
    }
}