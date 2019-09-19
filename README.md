![](./CollectSFData/FabricSupport.ico)

# CollectSFData

[current download](https://github.com/microsoft/CollectServiceFabricData/releases/latest)  
[requirements](#requirements)  
[setup](#setup)  
[configuration](#configuration)  
[examples](#examples)  
[contributing](#contributing)  

## Overview

CollectSFData is a .net command-line utility to assist with the download of Azure Service Fabric diagnostic data from the configured Azure storage account.
Optionally, CollectSFData can be configured to ingest downloaded data into a configured [Azure Data Explorer](https://docs.microsoft.com/en-us/azure/data-explorer/) (Kusto) database or Log Analytics (OMS) for analysis.
See [requirements](../docs/requirements.md), [setup](../docs/setup.md) and [configuration](../docs/configuration.md) for additional information.

Service Fabric diagnostic data that can be enumerated / downloaded from configured storage account:

- service fabric detailed .dtr logs in .zip. (fabriclogs-*)
- service fabric counter .blg files. (fabriccounters-*)
- service fabric fabric exceptions .dmp files. (fabriccrashdump-*)
- service fabric events stored in Azure blob tables.
- service fabric setup .trace files.

## Requirements

Service Fabric diagnostic data can be large depending on utilization and size of cluster. CollectSFData is a multi-threaded utility designed to process data as quickly as possible.
Depending on configuration, resource usage of cpu, memory, network, disk, and disk space can be high. Below are general resource guidelines for execution of utility.

- Windows 10 / Windows 2016+ image x64
- .net 4.6.2+
- 16+ GB RAM
- 4+ cpu
- 100 GB free drive space preferably on ssd for fabriclogs if caching locally to disk (UseMemoryStream = false)

Currently fabriclogs, fabriccounters, setup, and table (event) data all have to be formatted after download before ingestion. This requires additional cpu, memory, and disk resources. The following is needed:

### Kusto option requirements

Configuration for data ingestion into Kusto has these requirements:

- Existing online Kusto database.
- Authentication to Kusto database. Authentication can be interactive or non interactive.

### Log Analytics option requirements

Configuration for date ingestion into Log Analytics as a custom log has these requirements:

- Existing Log Analytics workspace and workspace ID guid.
- Log Analytics primary / secondary base64 key for authentication.

## Setup

CollectSFData is a console only utility that has no install.
Use the below steps to setup environment for use with CollectSFData.

1. ensure machine executing utility has comparable [requirements](#requirements)
2. download latest build [collectsfdata.zip](/releases/tag/CollectSFData-latest)  
3. extract zip to working directory
4. from extracted directory, use command prompt / powershell to execute utility
5. [configuration](#configuration) can be passed as command line arguments or in json files.
6. for help, type 'collectsfdata.exe /?'

### CollectSFData Setup with Kusto

If using Kusto, an existing online Kusto database with authentication is required.

- Existing online Kusto database. See [Create an Azure Data Explorer cluster](https://docs.microsoft.com/en-us/azure/data-explorer/create-cluster-database-portal) if creating a new cluster.
- Authentication to Kusto database. This can be interactive or non-interactive using an Azure application id / spn.
- Resource id should be your Kusto cluster URL, e.g. https://mycluster.kusto.windows.net or https://mycluster.kustomfa.windows.net.

### CollectSFData Setup with Log Analytics

If using Log Analytics (OMS), an existing or new workspace is required.  

- Existing Log Analytics workspace  
- Primary / Secondary shared key from workspace -> advanced settings  
- [Log Analytics Pricing](https://azure.microsoft.com/en-us/pricing/details/monitor/).

## Configuration

[Additional configurations](/docs/configuration.md)  

To configure CollectSFData, either command line or json or both can be used.
If exists, default configuration file 'collectsfdata.options.json' will always be read first.
Any additional configuration files specified on command line with -config will be loaded next.
Finally any additional arguments passed on command line will be loaded last.

### Command line options

For help with command line options, type 'collectsfdata.exe -?'.
**NOTE:** command line options **are** case sensitive.

```text
C:\>CollectSFData.exe /?
2.5.7197.14241

Usage: CollectSFData.exe [options]

Options:
  -v|--version                       Show version information
  -?|--?                             Show help information
  -client|--azureClientId            [string] azure application id / client id for use with authentication
                                         for non interactive to kusto. default is to use integrated AAD auth token
                                         and leave this blank.
  -secret|--azureClientSecret        [string] azure application id / client id secret for use with authentication
                                         for non interactive to kusto. default is to use integrated AAD auth token
                                         and leave this blank.
  -rg|--azureResourceGroup           [string] azure resource group name / used for log analytics actions.
  -loc|--azureResourceGroupLocation  [string] azure resource group location / used for log analytics actions.
  -sub|--azureSubscriptionId         [string] azure subscription id / used for log analytics actions.
  -tenant|--azureTenantId            [string] azure tenant id for use with kusto AAD authentication
  -cache|--cacheLocation             [string] Write files to this output location. e.g. "C:\Perfcounters\Output"
  -config|--configurationFile        [string] json file containing configuration options.
                                         type collectsfdata.exe -save default.json to create a default file.
                                         if collectsfdata.options.json exists, it will be used for configuration.
  -cf|--containerFilter              [string] string / regex to filter container names
  -dc|--deleteCache                  [bool] delete downloaded blobs from local disk at end of execution.
  -to|--stop                         [DateTime] end time range to collect data to. default is now.
                                         example: "09/15/2019 08:04:06 -04:00"
  -ex|--examples                     [bool] show example commands
  -type|--gatherType                 [string] Gather data type:
                                        counter
                                        trace
                                        exception
                                        table
                                        setup
                                        any
  -kz|--kustoCompressed              [bool] compress upload to kusto ingest.
  -kc|--kustoCluster                 [string] ingest url for kusto.
                                         ex: https://ingest-{clusterName}.{location}.kusto.windows.net/{databaseName}
  -kp|--KustoPurge                   [string] 'true' to purge 'KustoTable' table from Kusto
                                         or 'list' to list tables from Kusto.
                                         or {tableName} to drop from Kusto.
  -krt|--kustoRecreateTable          [bool] drop and recreate kusto table.
                                         default is to append. All data in table will be deleted!
  -kt|--kustoTable                   [string] name of kusto table to create / use.
  -kbs|--kustoUseBlobAsSource        [bool] for blob -> kusto direct ingest.
                                         requires .dtr (.csv) files to be csv compliant.
                                         service fabric 6.5+ dtr files are compliant.
  -l|--list                          [bool] list files instead of downloading
  -lac|--logAnalyticsCreate          [bool] create new log analytics workspace.
                                         requires LogAnalyticsWorkspaceName, AzureResourceGroup,
                                         AzureResourceGroupLocation, and AzureSubscriptionId
  -lak|--logAnalyticsKey             [string] Log Analytics shared key
  -laid|--logAnalyticsId             [string] Log Analytics workspace ID
  -lan|--logAnalyticsName            [string] Log Analytics name to use for import
  -lap|--logAnalyticsPurge           [string] 'true' to purge 'LogAnalyticsName' data from Log Analytics
                                         or %purge operation id% of active purge.
  -lar|--logAnalyticsRecreate        [bool] recreate workspace based on existing workspace resource information.
                                         requires LogAnalyticsName, LogAnalyticsId, LogAnalyticsKey,
                                         and AzureSubscriptionId. All data in workspace will be deleted!
  -lawn|--logAnalyticsWorkspaceName  [string] Log Analytics Workspace Name to use when creating
                                         new workspace with LogAnalyticsCreate
  -laws|--logAnalyticsWorkspaceSku   [string] Log Analytics Workspace Sku to use when creating new
                                         workspace with LogAnalyticsCreate. default is PerGB2018
  -debug|--logDebug                  [bool] output debug statements to console
  -log|--logFile                     [string] file name and path to save console output
  -nf|--nodeFilter                   [string] string / regex Filter on node name or any string in blob url
                                         (case-insensitive comparison)
  -ruri|--resourceUri                [string] resource uri / resource id used by microsoft internal support for tracking.
  -s|--sasKey                        [string] source blob SAS key required to access service fabric ***REMOVED***
                                         blob storage.
  -save|--saveConfiguration          [string] file name and path to save current configuration
  -from|--start                      [DateTime] start time range to collect data from.
                                         default is -2 hours.
                                         example: "09/15/2019 06:04:06 -04:00"
  -t|--threads                       [int] override default number of threads equal to processor count.
  -u|--unique                        [bool] default true to query for fileuri before ingestion to prevent duplicates
  -uf|--uriFilter                    [string] string / regex filter for storage account blob uri.
  -stream|--useMemoryStream          [bool] default true to use memory stream instead of disk during format.

argument names on command line *are* case sensitive.
bool argument values on command line should either be (true|1|on) or (false|0|off|null).
https://github.com/microsoft/CollectServiceFabricData
```

### Configuration File options

Instead of or in addition to using command line arguments, default and specified json configuration files can be used. Arguments in the json configuration files are not case sensitive.  
For additional json configuration files see [configurationFiles](../configurationFiles).

### Default JSON configuration file

To use a default configuration file without having to specify on the command line, create a file named **'collectsfdata.options.json'** in the working directory using example file or json below.

## Examples

[Additional examples](/docs/examples.md)  

Some basic examples on how to use arguments and configuration files. For additional examples, type 'collectsfdata.exe -ex'

### Example Kusto with minimal arguments

```text
collectsfdata.exe -type trace -s "<% sasKey %>" -kc "https://<% kusto ingest name %>.<% location %>.kusto.windows.net/<% kusto database %>" -kt "<% kusto table name %>"
collectsfdata.exe -type trace -s "https://***REMOVED***xxxxxxxxxxxxx.blob.core.windows.net/?sv=2017-11-09&ss=bfqt&srt=sco&sp=rwdlacup&se=2018-12-05T23:51:08Z&st=2018-11-05T15:51:08Z&spr=https&sig=VYT1J9Ene1NktyCgsu1gEH%2FN%2BNH9zRhJO05auUPQkSA%3D" -kc https://ingest-kustodb.eastus.kusto.windows.net/serviceFabricDB -kt "fabric_traces"
```

### Example Log Analytics with minimal arguments

```text
collectsfdata.exe -type trace -s "<% sasKey %>" -kc "https://<% kusto ingest name %>.<% location %>.kusto.windows.net/<% kusto database %>" -kt "<% kusto table name %>"
collectsfdata.exe -type trace -s "https://***REMOVED***xxxxxxxxxxxxx.blob.core.windows.net/?sv=2017-11-09&ss=bfqt&srt=sco&sp=rwdlacup&se=2018-12-05T23:51:08Z&st=2018-11-05T15:51:08Z&spr=https&sig=VYT1J9Ene1NktyCgsu1gEH%2FN%2BNH9zRhJO05auUPQkSA%3D" -kc https://ingest-kustodb.eastus.kusto.windows.net/serviceFabricDB -kt "fabric_traces"
```

### Example JSON configuration file options

example clean configuration with Kusto

```json
{
  "GatherType": "[counter|setup|trace|table]", // choose one
  "SasKey": "[account sas uri|service sas uri|sas uri connection string]",
  "StartTimeStamp": null,
  "EndTimeStamp": null,
  "KustoCluster": "https://<kusto ingest url>.<location>.kusto.windows.net/<kusto database>",
  "KustoRecreateTable": false,
  "KustoTable": "<%kusto table name%>"
}
```

example clean configuration with Log Analytics

```json
{
  "GatherType": "[counter|setup|trace|table]", // choose one
  "SasKey": "[account sas uri|service sas uri|sas uri connection string]",
  "StartTimeStamp": null,
  "EndTimeStamp": null,
  "LogAnalyticsId" : "<% oms workspace id %>",
  "LogAnalyticsKey" : "<% oms primary / secondary key %>",
  "LogAnalyticsName" : "<% oms tag / name for ingest %>"
}
```

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
