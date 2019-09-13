// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Text;

namespace CollectSFData
{
    public class CommandLineArguments : Constants
    {
        public CommandLineArguments()
        {
            CmdLineApp = new CommandLineApplication(throwOnUnexpectedArg: true)
            {
                Name = AppDomain.CurrentDomain.FriendlyName,
                Description = "collect service fabric logs for analysis",
                ShowInHelpText = true
            };
        }

        public CommandOption AzureClientId { get; set; }

        public CommandOption AzureClientSecret { get; set; }

        public CommandOption AzureResourceGroup { get; set; }

        public CommandOption AzureResourceGroupLocation { get; set; }

        public CommandOption AzureSubscriptionId { get; set; }

        public CommandOption AzureTenantId { get; set; }

        public CommandOption CacheLocation { get; set; }

        public CommandLineApplication CmdLineApp { get; private set; }

        public CommandOption ConfigurationFile { get; set; }

        public CommandOption ContainerFilter { get; set; }

        public CommandOption DeleteCache { get; set; }

        public CommandOption EndTimeStamp { get; set; }

        public CommandOption Examples { get; set; }

        public CommandOption GatherType { get; set; }

        public CommandOption KustoCluster { get; set; }

        public CommandOption KustoCompressed { get; set; }

        public CommandOption KustoPurge { get; set; }

        public CommandOption KustoRecreateTable { get; set; }

        public CommandOption KustoTable { get; set; }

        public CommandOption KustoUseBlobAsSource { get; set; }

        public CommandOption List { get; set; }

        public CommandOption LogAnalyticsCreate { get; set; }

        public CommandOption LogAnalyticsId { get; set; }

        public CommandOption LogAnalyticsKey { get; set; }

        public CommandOption LogAnalyticsName { get; set; }

        public CommandOption LogAnalyticsPurge { get; set; }

        public CommandOption LogAnalyticsRecreate { get; set; }

        public CommandOption LogAnalyticsWorkspaceName { get; set; }

        public CommandOption LogAnalyticsWorkspaceSku { get; set; }

        public CommandOption LogDebug { get; set; }

        public CommandOption LogFile { get; set; }

        public CommandOption NodeFilter { get; set; }

        public CommandOption ResourceUri { get; set; }

        public CommandOption SasKey { get; set; }

        public CommandOption SaveConfiguration { get; set; }

        public CommandOption StartTimeStamp { get; set; }

        public CommandOption Threads { get; set; }

        public CommandOption Unique { get; set; }

        public CommandOption UriFilter { get; set; }

        public CommandOption UseMemoryStream { get; set; }

        public void DisplayExamples()
        {
            StringBuilder sb = new StringBuilder();
            DateTimeOffset exampleDate = DateTimeOffset.Now;
            Log.Min("Example Usage #1 to download performance counter .blg files", ConsoleColor.Yellow, ConsoleColor.Black);
            sb.AppendLine("");
            sb.AppendLine("CollectSFData.exe"
                      + $" -from \"{exampleDate.ToString(DefaultDatePattern)}\""
                      + $" -to \"{exampleDate.AddHours(2).ToString(DefaultDatePattern)}\""
                      + " -cache \"C:\\Cases\\123245\\perfcounters\""
                      + " -s \"https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine("");
            sb.AppendLine($"\t    Gathering: counter");
            sb.AppendLine($"\t   Start Time: {exampleDate.ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t          UTC: {exampleDate.ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t     End Time: {exampleDate.AddHours(2).ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t          UTC: {exampleDate.AddHours(2).ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine("\t   AccountName: sflgaccountname");
            sb.AppendLine("\t       SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine($"\t      Threads: 8");
            sb.AppendLine($"\tCacheLocation: C:\\Cases\\123245\\perfcounters");
            sb.AppendLine($"\t       Filter: ");
            sb.AppendLine("");
            sb.AppendLine("\t ContainerName: fabriccounters-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231");
            sb.AppendLine("");
            sb.AppendLine("");
            Log.Min(sb.ToString());
            sb.Clear();

            Log.Min("Example Usage #2 to download service fabric trace dtr.zip (.csv) files", ConsoleColor.Yellow, ConsoleColor.Black);
            sb.AppendLine("");
            sb.AppendLine("CollectSFData.exe"
                      + $" -from \"{exampleDate.ToString(DefaultDatePattern)}\""
                      + $" -to \"{exampleDate.AddHours(2).ToString(DefaultDatePattern)}\""
                      + " --cacheLocation \"C:\\Cases\\123245\\traceLogs\""
                      + " --gatherType trace"
                      + " --sasKey \"https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine("");
            sb.AppendLine($"\t    Gathering: trace");
            sb.AppendLine($"\t   Start Time: {exampleDate.ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t          UTC: {exampleDate.ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t     End Time: {exampleDate.AddHours(2).ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t          UTC: {exampleDate.AddHours(2).ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine("\t   AccountName: sflgaccountname");
            sb.AppendLine("\t       SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine($"\t      Threads: 8");
            sb.AppendLine($"\tCacheLocation: C:\\Cases\\123245\\traceLogs");
            sb.AppendLine($"\t       Filter: ");
            sb.AppendLine("");
            sb.AppendLine("\t ContainerName: fabriclogs-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231");
            sb.AppendLine("");
            sb.AppendLine("");
            Log.Min(sb.ToString());
            sb.Clear();

            Log.Min("Example Usage #3 using optional --nodeFilter switch to service fabric trace dtr.zip (.csv) files" +
                "\r\n\t--nodeFilter is regex / string based.", ConsoleColor.Yellow, ConsoleColor.Black);
            sb.AppendLine("");
            sb.AppendLine("CollectSFData.exe"
                      + " --nodeFilter \"_node_0\""
                      + " --cacheLocation \"C:\\Cases\\123245\\traceLogs\""
                      + " --gatherType trace"
                      + " -csv --sasKey \"https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine("");
            sb.AppendLine($"\t    Gathering: trace");
            sb.AppendLine($"\t  Start Time: {exampleDate.ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t          UTC: {exampleDate.ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t    End Time: {exampleDate.AddHours(2).ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t          UTC: {exampleDate.AddHours(2).ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine("\t   AccountName: sflgaccountname");
            sb.AppendLine("\t       SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine($"\t      Threads: 8");
            sb.AppendLine($"\tCacheLocation: C:\\Cases\\123245\\traceLogs");
            sb.AppendLine($"\t       Filter: _node_0");
            sb.AppendLine("");
            sb.AppendLine("\t ContainerName: fabriclogs-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231");
            sb.AppendLine("");
            sb.AppendLine("");
            Log.Min(sb.ToString());
            sb.Clear();

            Log.Min("Example Usage #4 download service fabric trace files, unzip, and format .csv output files", ConsoleColor.Yellow, ConsoleColor.Black);
            sb.AppendLine("");
            sb.AppendLine("CollectSFData.exe"
                      + $" -from \"{exampleDate.ToString(DefaultDatePattern)}\""
                      + $" -to \"{exampleDate.AddHours(2).ToString(DefaultDatePattern)}\""
                      + " --cacheLocation \"C:\\Cases\\123245\\traceLogs\""
                      + " --gatherType trace"
                      + " --sasKey \"https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine("");
            sb.AppendLine($"\t     Gathering: trace");
            sb.AppendLine($"\t    Start Time: {exampleDate.ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t           UTC: {exampleDate.ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t      End Time: {exampleDate.AddHours(2).ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t           UTC: {exampleDate.AddHours(2).ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine("\t    AccountName: sflgaccountname");
            sb.AppendLine("\t        SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine($"\t       Threads: 8");
            sb.AppendLine($"\t CacheLocation: C:\\Cases\\123245\\traceLogs");
            sb.AppendLine($"\t        Filter: ");
            sb.AppendLine("");
            sb.AppendLine("\t  ContainerName: fabriclogs-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231");
            sb.AppendLine("");
            sb.AppendLine("");
            Log.Min(sb.ToString());
            sb.Clear();

            Log.Min("Example Usage #5 download performance counter .blg files and convert to csv files", ConsoleColor.Yellow, ConsoleColor.Black);
            sb.AppendLine("");
            sb.AppendLine("CollectSFData.exe"
                      + $" -from \"{exampleDate.ToString(DefaultDatePattern)}\""
                      + $" -to \"{exampleDate.AddHours(2).ToString(DefaultDatePattern)}\""
                      + " --cacheLocation \"C:\\Cases\\123245\\perfcounters\""
                      + " --gatherType counter"
                      + " --csv --sasKey \"https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine("");
            sb.AppendLine($"\t     Gathering: counter");
            sb.AppendLine($"\t    Start Time: {exampleDate.ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t           UTC: {exampleDate.ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t      End Time: {exampleDate.AddHours(2).ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t           UTC: {exampleDate.AddHours(2).ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine("\t    AccountName: sflgaccountname");
            sb.AppendLine("\t        SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine($"\t       Threads: 8");
            sb.AppendLine($"\tCacheLocation: C:\\Cases\\123245\\perfcounters");
            sb.AppendLine($"\t       Filter: ");
            sb.AppendLine("");
            sb.AppendLine("\t ContainerName: fabriccounters-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231");
            sb.AppendLine("");
            sb.AppendLine("");

            Log.Min("Example Usage #6 unzip and format service fabric trace files already on disk. for example from standalonelogcollector upload to dtm.", ConsoleColor.Yellow, ConsoleColor.Black);
            sb.AppendLine("");
            sb.AppendLine("CollectSFData.exe"
                      + " --cacheLocation \"C:\\Cases\\123245\\traceLogs\""
                      + " --gatherType trace");
            sb.AppendLine("");
            sb.AppendLine($"\t  Gathering: trace");
            sb.AppendLine($"\t Start Time: {exampleDate.ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t        UTC: {exampleDate.ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t   End Time: {exampleDate.AddHours(2).ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t        UTC: {exampleDate.AddHours(2).ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine("\tAccountName: ");
            sb.AppendLine("\t    SAS key: ");
            sb.AppendLine("\tParse 2 Csv: False");
            sb.AppendLine($"\t    Threads: 8");
            sb.AppendLine($"\t      CacheLocation: C:\\Cases\\123245\\traceLogs");
            sb.AppendLine($"\t      Filter: ");
            sb.AppendLine("");
            sb.AppendLine("\tContainerName: ");
            sb.AppendLine("");
            sb.AppendLine("");
            Log.Min(sb.ToString());
            sb.Clear();

            Log.Min("Example Usage #7 kusto: download service fabric trace files, unzip, format for kusto, queue for kusto ingest.", ConsoleColor.Yellow, ConsoleColor.Black);
            sb.AppendLine("");
            sb.AppendLine("CollectSFData.exe"
                      + $" -from \"{exampleDate.ToString(DefaultDatePattern)}\""
                      + $" -to \"{exampleDate.AddHours(2).ToString(DefaultDatePattern)}\""
                      + " -cache \"C:\\Cases\\123245\\traceLogs\""
                      + " -j 100"
                      + " -u"
                      + " -type trace"
                      + " -s \"https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\""
                      + " -kc \"https://ingest-kustoclusterinstance.eastus.kusto.windows.net/kustoDB\""
                      + " -krt"
                      + " -kt \"kustoTable-trace\"");
            sb.AppendLine("");
            sb.AppendLine($"\t     Gathering: trace");
            sb.AppendLine($"\t    Start Time: {exampleDate.ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t           UTC: {exampleDate.ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t      End Time: {exampleDate.AddHours(2).ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t           UTC: {exampleDate.AddHours(2).ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine("\t    AccountName: sflgaccountname");
            sb.AppendLine("\t        SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine($"\t       Threads: 8");
            sb.AppendLine($"\tCacheLocation: C:\\Cases\\123245\\traceLogs");
            sb.AppendLine($"\t       Filter: ");
            sb.AppendLine("");
            sb.AppendLine("\t ContainerName: fabriclogs-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231");
            sb.AppendLine("");
            sb.AppendLine("");
            Log.Min(sb.ToString());
            sb.Clear();

            Log.Min("Example Usage #8 kusto: download performance counter .blg files, convert to .csv files, format for kusto, queue for kusto ingest.",
                ConsoleColor.Yellow, ConsoleColor.Black);
            sb.AppendLine("");
            sb.AppendLine("CollectSFData.exe"
                      + $" -from \"{exampleDate.ToString(DefaultDatePattern)}\""
                      + $" -to \"{exampleDate.AddHours(2).ToString(DefaultDatePattern)}\""
                      + " -cache \"C:\\Cases\\123245\\traceLogs\""
                      + " -c"
                      + " -type counter"
                      + " -s \"https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\""
                      + " -kc \"https://ingest-kustoclusterinstance.eastus.kusto.windows.net/kustoDB\""
                      + " -krt"
                      + " -kt \"kustoTable-perf\"");
            sb.AppendLine("");
            sb.AppendLine($"\t     Gathering: counter");
            sb.AppendLine($"\t    Start Time: {exampleDate.ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t           UTC: {exampleDate.ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t      End Time: {exampleDate.AddHours(2).ToString(DefaultDatePattern)}");
            sb.AppendLine($"\t           UTC: {exampleDate.AddHours(2).ToUniversalTime().ToString(DefaultDatePattern)}");
            sb.AppendLine("\t    AccountName: sflgaccountname");
            sb.AppendLine("\t        SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D\"");
            sb.AppendLine($"\t       Threads: 8");
            sb.AppendLine($"\t CacheLocation: C:\\Cases\\123245\\traceLogs");
            sb.AppendLine($"\t        Filter: ");
            sb.AppendLine("");
            sb.AppendLine("\t  ContainerName: fabriccounters-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231");
            sb.AppendLine("");
            sb.AppendLine("");
            Log.Min(sb.ToString());
            sb.Clear();
        }

        public void InitFromCmdLine()
        {
            string newLine = "\r\n\t\t\t\t\t";
            string exampleDateFormat = "MM/dd/yyyy HH:mm:ss zzz";
            CmdLineApp.VersionOption("-v|--version", () => Version);
            CmdLineApp.HelpOption("-?|--?");
            CmdLineApp.ExtendedHelpText = $"\r\nargument names on command line *are* case sensitive." +
                $"\r\nbool argument values on command line should either be {TrueStringPattern} or {FalseStringPattern}." +
                $"\r\n{CodeRepository}";

            AzureClientId = CmdLineApp.Option("-client|--azureClientId",
                    $"[string] azure application id / client id for use with authentication" +
                    $"{newLine} for non interactive to kusto. default is to use integrated AAD auth token" +
                    $"{newLine} and leave this blank.",
                    CommandOptionType.SingleValue);

            AzureClientSecret = CmdLineApp.Option("-secret|--azureClientSecret",
                    $"[string] azure application id / client id secret for use with authentication" +
                    $"{newLine} for non interactive to kusto. default is to use integrated AAD auth token" +
                    $"{newLine} and leave this blank.",
                    CommandOptionType.SingleValue);

            AzureResourceGroup = CmdLineApp.Option("-rg|--azureResourceGroup",
                    "[string] azure resource group name / used for log analytics actions.",
                    CommandOptionType.SingleValue);

            AzureResourceGroupLocation = CmdLineApp.Option("-loc|--azureResourceGroupLocation",
                    "[string] azure resource group location / used for log analytics actions.",
                    CommandOptionType.SingleValue);

            AzureSubscriptionId = CmdLineApp.Option("-sub|--azureSubscriptionId",
                    "[string] azure subscription id / used for log analytics actions.",
                    CommandOptionType.SingleValue);

            AzureTenantId = CmdLineApp.Option("-tenant|--azureTenantId",
                    "[string] azure tenant id for use with kusto AAD authentication",
                    CommandOptionType.SingleValue);

            CacheLocation = CmdLineApp.Option("-cache|--cacheLocation",
                     "[string] Write files to this output location. e.g. \"C:\\Perfcounters\\Output\" ",
                     CommandOptionType.SingleValue);

            ConfigurationFile = CmdLineApp.Option("-config|--configurationFile",
                    $"[string] json file containing configuration options." +
                    $"{newLine} type collectsfdata.exe -save default.json to create a default file." +
                    $"{newLine} if {DefaultOptionsFile} exists, it will be used for configuration.",
                    CommandOptionType.SingleValue);

            ContainerFilter = CmdLineApp.Option("-cf|--containerFilter",
                    "[string] string / regex to filter container names",
                    CommandOptionType.SingleValue);

            DeleteCache = CmdLineApp.Option("-dc|--deleteCache",
                    "[bool] delete downloaded blobs from local disk at end of execution. ",
                    CommandOptionType.SingleValue);

            EndTimeStamp = CmdLineApp.Option("-to|--stop",
                    $"[DateTime] end time range to collect data to. default is now." +
                    $"{newLine} example: \"{DateTime.Now.ToString(exampleDateFormat)}\"",
                    CommandOptionType.SingleValue);

            Examples = CmdLineApp.Option("-ex|--examples",
                    "[bool] show example commands",
                    CommandOptionType.SingleValue);

            GatherType = CmdLineApp.Option("-type|--gatherType",
                    $"[string] Gather data type:" +
                    $"{newLine}counter" +
                    $"{newLine}trace" +
                    $"{newLine}exception" +
                    $"{newLine}table" +
                    $"{newLine}setup" +
                    $"{newLine}any",
                    CommandOptionType.SingleValue);

            KustoCompressed = CmdLineApp.Option("-kz|--kustoCompressed",
                    "[bool] compress upload to kusto ingest.",
                    CommandOptionType.SingleValue);

            KustoCluster = CmdLineApp.Option("-kc|--kustoCluster",
                    $"[string] ingest url for kusto." +
                    $"{newLine} ex: https://ingest-{{clusterName}}.{{location}}.kusto.windows.net/{{databaseName}}",
                    CommandOptionType.SingleValue);

            KustoPurge = CmdLineApp.Option("-kp|--KustoPurge",
                    $"[string] 'true' to purge 'KustoTable' table from Kusto" +
                    $"{newLine} or 'list' to list tables from Kusto." +
                    $"{newLine} or {{tableName}} to drop from Kusto.",
                    CommandOptionType.SingleValue);

            KustoRecreateTable = CmdLineApp.Option("-krt|--kustoRecreateTable",
                    $"[bool] drop and recreate kusto table." +
                    $"{newLine} default is to append. All data in table will be deleted!",
                    CommandOptionType.SingleValue);

            KustoTable = CmdLineApp.Option("-kt|--kustoTable",
                    "[string] name of kusto table to create / use.",
                    CommandOptionType.SingleValue);

            KustoUseBlobAsSource = CmdLineApp.Option("-kbs|--kustoUseBlobAsSource",
                    $"[bool] for blob -> kusto direct ingest." +
                    $"{newLine} requires .dtr (.csv) files to be csv compliant." +
                    $"{newLine} service fabric 6.5+ dtr files are compliant.",
                    CommandOptionType.SingleValue);

            List = CmdLineApp.Option("-l|--list",
                    "[bool] list files instead of downloading",
                    CommandOptionType.SingleValue);

            LogAnalyticsCreate = CmdLineApp.Option("-lac|--logAnalyticsCreate",
                    $"[bool] create new log analytics workspace." +
                    $"{newLine} requires LogAnalyticsWorkspaceName, AzureResourceGroup," +
                    $"{newLine} AzureResourceGroupLocation, and AzureSubscriptionId",
                    CommandOptionType.SingleValue);

            LogAnalyticsKey = CmdLineApp.Option("-lak|--logAnalyticsKey",
                    "[string] Log Analytics shared key",
                    CommandOptionType.SingleValue);

            LogAnalyticsId = CmdLineApp.Option("-laid|--logAnalyticsId",
                    "[string] Log Analytics workspace ID",
                    CommandOptionType.SingleValue);

            LogAnalyticsName = CmdLineApp.Option("-lan|--logAnalyticsName",
                    "[string] Log Analytics name to use for import",
                    CommandOptionType.SingleValue);

            LogAnalyticsPurge = CmdLineApp.Option("-lap|--logAnalyticsPurge",
                    $"[string] 'true' to purge 'LogAnalyticsName' data from Log Analytics" +
                    $"{newLine} or %purge operation id% of active purge.",
                    CommandOptionType.SingleValue);

            LogAnalyticsRecreate = CmdLineApp.Option("-lar|--logAnalyticsRecreate",
                    $"[bool] recreate workspace based on existing workspace resource information." +
                    $"{newLine} requires LogAnalyticsName, LogAnalyticsId, LogAnalyticsKey," +
                    $"{newLine} and AzureSubscriptionId. All data in workspace will be deleted!",
                    CommandOptionType.SingleValue);

            LogAnalyticsWorkspaceName = CmdLineApp.Option("-lawn|--logAnalyticsWorkspaceName",
                     $"[string] Log Analytics Workspace Name to use when creating" +
                     $"{newLine} new workspace with LogAnalyticsCreate",
                    CommandOptionType.SingleValue);

            LogAnalyticsWorkspaceSku = CmdLineApp.Option("-laws|--logAnalyticsWorkspaceSku",
                     $"[string] Log Analytics Workspace Sku to use when creating new" +
                     $"{newLine} workspace with LogAnalyticsCreate. default is PerGB2018",
                    CommandOptionType.SingleValue);

            LogDebug = CmdLineApp.Option("-debug|--logDebug",
                    "[bool] output debug statements to console",
                    CommandOptionType.SingleValue);

            LogFile = CmdLineApp.Option("-log|--logFile",
                    "[string] file name and path to save console output",
                    CommandOptionType.SingleValue);

            NodeFilter = CmdLineApp.Option("-nf|--nodeFilter",
                    $"[string] string / regex Filter on node name or any string in blob url" +
                    $"{newLine} (case-insensitive comparison)",
                    CommandOptionType.SingleValue);

            ResourceUri = CmdLineApp.Option("-ruri|--resourceUri",
                    $"[string] resource uri / resource id used by microsoft internal support for tracking.",
                    CommandOptionType.SingleValue);

            SasKey = CmdLineApp.Option("-s|--sasKey",
                    $"[string] source blob SAS key required to access service fabric ***REMOVED***" +
                    $"{newLine} blob storage.",
                    CommandOptionType.SingleValue);

            SaveConfiguration = CmdLineApp.Option("-save|--saveConfiguration",
                    "[string] file name and path to save current configuration",
                    CommandOptionType.SingleValue);

            StartTimeStamp = CmdLineApp.Option("-from|--start",
                    $"[DateTime] start time range to collect data from." +
                    $"{newLine} default is {DefaultStartTimeHours} hours." +
                    $"{newLine} example: \"{DateTime.Now.AddHours(DefaultStartTimeHours).ToString(exampleDateFormat)}\"",
                    CommandOptionType.SingleValue);

            Threads = CmdLineApp.Option("-t|--threads",
                    "[int] override default number of threads equal to processor count.",
                    CommandOptionType.SingleValue);

            Unique = CmdLineApp.Option("-u|--unique",
                    "[bool] default true to query for fileuri before ingestion to prevent duplicates",
                    CommandOptionType.SingleValue);

            UriFilter = CmdLineApp.Option("-uf|--uriFilter",
                    "[string] string / regex filter for storage account blob uri.",
                    CommandOptionType.SingleValue);

            UseMemoryStream = CmdLineApp.Option("-stream|--useMemoryStream",
                    "[bool] default true to use memory stream instead of disk during format.",
                    CommandOptionType.SingleValue);
        }
    }
}