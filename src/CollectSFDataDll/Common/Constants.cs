﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace CollectSFData.Common
{
    public static class Constants
    {
        public const string ArmApiVersion = "api-version=2018-05-01";
        public const string AzureKeyVaultSuffix = "vault.azure.net";
        public const string AzureStorageSuffix = "core.windows.net";
        public const string CodeLatestRelease = "https://api.github.com/repos/microsoft/CollectServiceFabricData/releases/latest";
        public const string CodeRepository = "https://github.com/microsoft/CollectServiceFabricData";
        public const string CsvExtension = ".csv";
        public const string CustomDatabasePersistencePathPattern = "^@'[a-zA-Z]:.+',@'[a-zA-Z]:.+'$";
        public const string DataExplorer = "https://dataexplorer.azure.com";
        public const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
        public const string DefaultDatePattern = "MM/dd/yyyy HH:mm zzz";
        public const string DefaultOptionsFile = "collectsfdata.options.json";
        public const int DefaultStartTimeHours = -2;
        public const string DtrExtension = ".dtr";
        public const string DtrZipExtension = ".dtr.zip";
        public const string DumpExtension = ".dmp";
        public const string EtlExtension = ".etl";
        public const string EtwDefaultManifestsCache = "./manifests";
        public const string EtwManifestExtension = ".man";
        public const string EtwManifestsUrl = "https://raw.githubusercontent.com/microsoft/CollectServiceFabricData/master/manifests";
        public const string FalseStringPattern = @"(false|0|off|null)";
        public const string InstanceMetaDataRestUri = "http://169.254.169.254/metadata/instance?api-version=2017-08-01"; //DevSkim: ignore DS137138 as is used on internal az network for instance metadata
        public const string JsonExtension = ".json";
        public const string KustoUrlPattern = "https://(?<ingest>ingest-){1}(?<clusterName>.+?)\\.(?<location>.+?){0,1}\\.(?<domainName>.+?)/(?<databaseName>.+?){1}(/|$)";
        public const string LocalWebServerPattern = @"^http://localhost:(?<port>\d){1,5}/(?<databaseName>.+?)$";
        public const string LogExtension = ".log";
        public const string ManagementAzureCom = "https://management.azure.com";
        public const int MaxCsvTransmitBytes = 1024 * 1024 * 100;
        public const int MaxEnumerationResults = 500;
        public const int MaxJsonTransmitBytes = 1024 * 1024 * 25;
        public const int MaxResults = 5000;
        public const int MaxStreamTransmitBytes = 1024 * 1024 * 1024;
        public const int MaxStringLength = 256;
        public const int MaxThreadMultiplier = 20;
        public const int MinThreadMultiplier = 10;
        public const string PerfCsvExtension = ".perf.csv";
        public const string PerfCtrExtension = ".blg";
        public const string PublicIpUrl = "https://ipinfo.io/json";
        public const int RetryCount = 10;
        public const int RetryDelay = 3;
        public const int RetryMaxDelay = 30;
        public const string SchemaFile = "https://raw.githubusercontent.com/microsoft/CollectServiceFabricData/master/configurationFiles/collectsfdata.schema.json";
        public const string SetupExtension = ".trace";
        public const string StartOfDefaultDatabasePersistencePath = "c:\\kustodata\\dbs\\";
        public const string TableExtension = ".table.csv";
        public const int TableMaxResults = 50000;
        public const string TableNamePattern = "fabriclog(?<guidString>[A-Fa-f0-9]{32}).+$";
        public const int ThreadSleepMs10 = 10;
        public const int ThreadSleepMs100 = 100;
        public const int ThreadSleepMs1000 = 1000;
        public const int ThreadSleepMs10000 = 10000;
        public const int ThreadSleepMsWarning = 5000;
        public const string TrueStringPattern = @"(true|1|on)";
        public const int WarningJsonTransmitBytes = (int)(MaxJsonTransmitBytes * .95);
        public const int WarningTimeSpanHours = 4;
        public const double WarningTimeSpanMinHours = .5F;
        public const string ZipExtension = ".zip";
        public static readonly string ApplicationName = AppDomain.CurrentDomain.FriendlyName;
        public static readonly string AppDataFolder = $"{Environment.GetEnvironmentVariable("LOCALAPPDATA")}\\{ApplicationName}";
        public static readonly string EtwManifestsUrlIndex = $"{EtwManifestsUrl}/index.json";
        public static readonly DateTime UnixEpochMinValue = new DateTime(1601,1,1);
    }
}