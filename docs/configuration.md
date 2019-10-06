# configuration

To configure CollectSFData, either command line or json or both can be used.
If exists, default configuration file 'collectsfdata.options.json' will always be read first.
Any additional configuration files specified on command line with -config will be loaded next.
Finally any additional arguments passed on command line will be loaded last.

## Command line options

For help with command line options, type 'collectsfdata.exe -?'.
**NOTE:** command line options **are** case sensitive.

```text
G:\github\Tools\CollectSFData\CollectSFData\bin\x64\Debug>CollectSFData.exe /?
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
  -s|--sasKey                        [string] source blob SAS key required to access service fabric sflogs
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

## JSON config file options

Instead of or in addition to using command line arguments, default and specified json configuration files can be used. Arguments in the json configuration files are not case sensitive but execution of utility will fail if an unknown argument is specified. For additional json configuration files see [configurationFiles](../configurationFiles).

### Default JSON configuration file

To use a default configuration file without having to specify on the command line, create a file named **'collectsfdata.options.json'** in the working directory using example file or json below.

### config file argument definitions

#### collectsfdata general arguments

- **ContainerFilter** - optional. string / regex. default null. if populated, pattern will be used to filter which containers are enumerated for blob download.
- **DeleteCache** - bool. default false. if true, blobs downloaded from storage account into 'cacheLocation' will be deleted at end after successful formatting and ingestion.
- **GatherType** - required. string. options: any, counter, exception, table, trace
  - **any** - 'any' without other filters will enumerate all containers for blobs matching criteria.
  - **counter** - 'counter' will enumerate service fabric performance counter (.blg) blobs from 'fabriccounters*' container.
  - **exception** - 'exception' will enumerate service fabric fabric crash dumps (.dmp) blobs from 'fabriccrashdumps*' container.
  - **table** - 'table' will enumerate service fabric events from blob tables 'fabriclogs*'
  - **trace** - 'trace' will enumerate service fabric diagnostic logs (.dtr) zip blobs from 'fabriclogs*'
- **List** - bool. default false. if true, lists the blobs meeting all criteria for download but does not download the file.
- **LogDebug** - bool. default false. if true, logs additional 'debug' output to console for troubleshooting.
- **LogFile** - optional. string. default null. if populated with file and path, will log all console output to specified file. file is recreated every execution if exists.
- **CacheLocation** - required. string. path to blob download location. this path depending on configuration may need to have many GB free and should be premium / fast ssd disk for best performance. **NOTE:** this path should be as short as possible as downloaded file names lengths are close to MAX_PATH.
- **SasKey** - required unless using existing data in 'outputlocation' cache from prior execution, then leave empty. string. string type options: account sas uri, service sas uri, or sas connection string. see [shared access signatures](https://docs.microsoft.com/en-us/rest/api/storageservices/delegating-access-with-a-shared-access-signature).
- **StartTimeStamp** - datetime string. default is -2 hours. example format: "10/31/2018 20:00:00 +00:00".
- **EndTimeStamp** - datetime string. default is now. example format: "10/31/2018 22:00:00 +00:00".
- **Threads** - int. default is number of cpu. if specified, is the number of concurrent threads to use for download and ingest to Kusto overriding number of cpus.
- **UriFilter** - optional. string. if populated has to be blob uri prefix and uses fast server side searching for blobs.
- **NodeFilter** -  optional. string / regex. if populated uses client side searching for blobs after enumeration before download.

#### collectsfdata kusto arguments

- **KustoCluster** - required. uri.
- **KustoRecreateTable** - bool. default false. if true, will drop (recreate) table before ingesting new data regardless if table is currently populated.
- **KustoTable** - required. string. name of kusto table to create and or use.
- **KustoUseBlobAsSource** - **not currently used**. bool. default false. if true will ingest service diagnostic logs directly from storage account instead of downloading and formatting. to use this option, service fabric .dtr files have to be CSV compliant.

#### collectsfdata log analytics arguments
- **LogAnalyticsId** - required. guid. log analytics workspace id guid.
- **LogAnalyticsKey** - required. base64 key. primary / secondary key located in workspace advanced settings.
- **LogAnalyticsName** - string. name / tag for custom log ingest. requires first character to be alpha.

### Example JSON configuration files

example clean configuration without Kusto

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "[any|counter|exception|trace|table]",
  "List": false,
  "LogDebug": false,
  "LogFile": null,
  "CacheLocation": "<%fast drive path with 100 GB free%>",
  "SasKey": "[account sas uri|service sas uri|sas uri connection string]",
  "StartTimeStamp": null,
  "EndTimeStamp": null,
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": ""
}
```

example clean configuration with Kusto

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "[any|counter|exception|trace|table]",
  "List": false,
  "LogDebug": false,
  "LogFile": null,
  "CacheLocation": "<%fast drive path with 100 GB free%>",
  "SasKey": "[account sas uri|service sas uri|sas uri connection string]",
  "StartTimeStamp": null,
  "EndTimeStamp": null,
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "",
  "KustoCluster": "https://<kusto ingest url>.<location>.kusto.windows.net/<kusto database>",
  //"AzureTenantId" : "",
  //"AzureClientId": "",
  //"AzureClientSecret":"<64bit encoded client secret>",
  "KustoRecreateTable": false,
  "KustoTable": "<%kusto table name%>"
}
```

example clean configuration with Log Analytics

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "[any|counter|exception|trace|table]",
  "List": false,
  "LogDebug": false,
  "LogFile": null,
  "CacheLocation": "<%fast drive path with 100 GB free%>",
  "SasKey": "[account sas uri|service sas uri|sas uri connection string]",
  "StartTimeStamp": null,
  "EndTimeStamp": null,
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "",
  //"AzureTenantId" : "",
  //"AzureClientId": "",
  //"AzureClientSecret":"<64bit encoded client secret>",
  "LogAnalyticsId" : "<% oms workspace id %>",
  "LogAnalyticsKey" : "<% oms primary / secondary key %>",
  "LogAnalyticsName" : "<% oms tag / name for ingest %>"
}
```

example configuration for downloading service fabric diagnostic trace logs

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "trace",
  "List": false,
  "LogDebug": false,
  "LogFile": null,
  "CacheLocation": "g:\\cases",
  "SasKey": "https://sflogsxxxxxxxxxxxxx.blob.core.windows.net/?sv=2017-11-09&ss=bfqt&srt=sco&sp=rwdlacup&se=2018-12-05T23:51:08Z&st=2018-11-05T15:51:08Z&spr=https&sig=VYT1J9Ene1NktyCgsu1gEH%2FN%2BNH9zRhJO05auUPQkSA%3D",
  "StartTimeStamp": "10/31/2018 20:00:00 +00:00",
  "EndTimeStamp": "10/31/2018 22:30:00 +00:00",
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "",
}
```

example configuration for downloading service fabric diagnostic trace logs and uploading to kusto.

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "trace",
  "List": false,
  "LogDebug": false,
  "LogFile": null,
  "CacheLocation": "g:\\cases",
  "SasKey": "https://sflogsxxxxxxxxxxxxx.blob.core.windows.net/?sv=2017-11-09&ss=bfqt&srt=sco&sp=rwdlacup&se=2018-12-05T23:51:08Z&st=2018-11-05T15:51:08Z&spr=https&sig=VYT1J9Ene1NktyCgsu1gEH%2FN%2BNH9zRhJO05auUPQkSA%3D",
  "StartTimeStamp": "10/31/2018 20:00:00 +00:00",
  "EndTimeStamp": "10/31/2018 22:30:00 +00:00",
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "fabric_traces",
  "KustoCluster": "https://ingest-kustodb.eastus.kusto.windows.net/serviceFabricDB",
  "KustoRecreateTable": true,
  "KustoTable": "_00000000000001"
}
```