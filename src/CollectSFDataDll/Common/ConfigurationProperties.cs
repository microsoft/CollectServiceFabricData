// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using System;

namespace CollectSFData.Common
{
    public class ConfigurationProperties : Constants
    {
        public string AzureClientId { get; set; }

        public string AzureClientCertificate { get; set; }

        public string AzureClientSecret { get; set; }

        public string AzureResourceGroup { get; set; }

        public string AzureResourceGroupLocation { get; set; }

        public string AzureSubscriptionId { get; set; }

        public string AzureTenantId { get; set; }

        public string CacheLocation { get; set; }

        public string ConfigurationFile { get; set; }

        public string ContainerFilter { get; set; }

        public bool DeleteCache { get; set; }

        public string EndTimeStamp { get; set; }
        
        public DateTimeOffset EndTimeUtc { get; set; }

        public bool Examples { get; private set; }

        public string[] FileUris {get; set;} = new string[0];

        public string GatherType { get; set; }
        
        public string KustoCluster { get; set; }

        public bool KustoCompressed { get; set; } = true;

        public string KustoPurge { get; set; }

        public bool KustoRecreateTable { get; set; }

        public string KustoTable { get; set; }

        public bool KustoUseBlobAsSource { get; set; } = true;

        public bool KustoUseIngestMessage { get; set; }

        public bool List { get; set; }

        public bool LogAnalyticsCreate { get; set; }

        public string LogAnalyticsId { get; set; }

        public string LogAnalyticsKey { get; set; }

        public string LogAnalyticsName { get; set; }

        public string LogAnalyticsPurge { get; set; }

        public bool LogAnalyticsRecreate { get; set; }

        public string LogAnalyticsWorkspaceName { get; set; }

        public string LogAnalyticsWorkspaceSku { get; set; } = "PerGB2018";

        public int LogDebug { get; set; } = 4;

        public string LogFile { get; set; }

        public string NodeFilter { get; set; }

        public int NoProgressTimeoutMin { get; set; } = 10;

        public string ResourceUri { get; set; }

        public SasEndpoints SasEndpointInfo { get; set; } = new SasEndpoints();

        public string SasKey { get; set; } = string.Empty;

        public string SaveConfiguration { get; set; }

        public string Schema { get; set; }

        public string StartTimeStamp { get; set; }

        public DateTimeOffset StartTimeUtc { get; set; }

        public int Threads { get; set; }

        public bool Unique { get; set; } = true;

        public string UriFilter { get; set; }

        public bool UseMemoryStream { get; set; } = true;

        public bool UseTx { get; set; }

        public bool VersionOption { get; set; }

        public ConfigurationProperties()
        {
        }
    }
}