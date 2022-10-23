# configuration

To configure CollectSFData, either command line or json or both can be used.
If exists, default configuration file 'collectsfdata.options.json' will always be read first.
Any additional configuration files specified on command line with -config will be loaded next.
Finally any additional arguments passed on command line will be loaded last.

## Command line options

For help with command line options, type 'collectsfdata.exe -?'.  
**NOTE:** command line options **are** case sensitive.

```text
C:\>CollectSFData.exe /?
Usage: CollectSFData [options]

Options:
  -?|--?                             Show help information
  -client|--azureClientId            [string] azure application id / client id for use with authentication
                                         for non interactive to kusto. default is to use integrated AAD auth token
                                         and leave this blank.
  -cert|--azureClientCertificate     [string] azure application id / client id certificate for use with authentication
                                         for non interactive to kusto. default is to use integrated AAD auth token
                                         and leave this blank.
  -secret|--azureClientSecret        [string] azure application id / client id secret for use with authentication
                                         for non interactive to kusto. default is to use integrated AAD auth token
                                         and leave this blank.
  -vault|--AzureKeyVault             [string] azure base key vault fqdn for use with authentication
                                         for non interactive to kusto. default is to use integrated AAD auth token
                                         and leave this blank.
                                         example: https://clusterkeyvault.vault.azure.net/
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
                                         example: "06/01/2021 09:01:23 -04:00"
  -mc|--etwManifestsCache            [string] local folder path to use for manifest cache .man files.
  -ex|--examples                     [bool] show example commands
  -uris|--fileUris                   [string[]] optional comma separated string array list of files to ingest.
                                         overrides default collection from diagnosticsStore
                                         example: D:\temp\lease_trace1.dtr.zip,D:\temp\lease_trace2.dtr.zip
  -type|--gatherType                 [string] Gather data type:
                                        counter
                                        trace
                                        exception
                                        sfextlog
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
  -kim|--kustoUseIngestMessage       [bool] for kusto ingestion message tracking.
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
  -debug|--logDebug                  [int] 0-disabled, 1-exception, 2-error, 3-warning, 4-info, 5-debug.
                                         use logdebug levels for troubleshooting utility
  -log|--logFile                     [string] file name and path to save console output
  -nf|--nodeFilter                   [string] string / regex Filter on node name or any string in blob url
                                         (case-insensitive comparison)
  -timeout|--noProgressTimeoutMin    [int] no progress timer in minutes. set to 0 to disable timeout.
  -ruri|--resourceUri                [string] resource uri / resource id used by microsoft internal support for tracking.
  -s|--sasKey                        [string] source blob SAS key required to access service fabric sflogs
                                         blob storage.
  -save|--saveConfiguration          [string] file name and path to save current configuration
                                         specify file name 'collectsfdata.options.json' to create default configuration file.
  -from|--start                      [DateTime] start time range to collect data from.
                                         default is -2 hours.
                                         example: "06/01/2021 07:01:23 -04:00"
  -t|--threads                       [int] override default number of threads equal to processor count.
  -u|--unique                        [bool] default true to query for fileuri before ingestion to prevent duplicates
  -uf|--uriFilter                    [string] string / regex filter for storage account blob uri.
  -stream|--useMemoryStream          [bool] default true to use memory stream instead of disk during format.
  -v|--version                       [switch] check local and online version

argument names on command line *are* case sensitive.
bool argument values on command line should either be (true|1|on) or (false|0|off|null).
https://github.com/microsoft/CollectServiceFabricData
```

## JSON config file options

Instead of or in addition to using command line arguments, default and specified json configuration files can be used. Arguments in the json configuration files are not case sensitive but execution of utility will fail if an unknown argument is specified. For additional json configuration files see [configurationFiles](../configurationFiles).

### Default JSON configuration file

To use a default configuration file without having to specify on the command line, create a file named **'collectsfdata.options.json'** in the working directory using example file or json below. A clean configuration can be generated with command *collectsfdata.exe -save collectsfdata.options.json*.

### config file argument definitions

#### collectsfdata general arguments

- **CacheLocation** - required. string. path to blob download location. this path depending on configuration may need to have many GB free and should be premium / fast ssd disk for best performance. **NOTE:** this path should be as short as possible as downloaded file names lengths are close to MAX_PATH.
- **ContainerFilter** - optional. string / regex. default null. if populated, pattern will be used to filter which containers are enumerated for blob download.
- **DeleteCache** - bool. default false. if true, blobs downloaded from storage account into 'cacheLocation' will be deleted at end after successful formatting and ingestion.
- **EndTimeStamp** - datetime string. default is now. example format: "10/31/2018 22:00:00 +00:00".
- **EtwManifestsCache** - required. string. default is ./manifests. local path where manifest (.man) files are located for parsing .etl files to .csv.
- **FileUris** - optional, string[]. default null. if populated, FileUris will be used for the source file ingestion and will bypass default behavior of enumerating cluster 'diagnosticsStore'.
- **GatherType** - required. string. options: counter, exception, table, trace, any
  - **counter** - 'counter' will enumerate service fabric performance counter (.blg) blobs from 'fabriccounters*' container.
  - **exception** - 'exception' will enumerate service fabric fabric crash dumps (.dmp) blobs from 'fabriccrashdumps*' container.
  - **setup** - 'setup' will enumerate service fabric fabric deployer setup (.trace) blobs from 'fabriclogs*' container.
  - **sfextlog** - 'sfextlog' will enumerate managed service fabric node extension logs (.log) blobs from 'vmextlog*' container.
  - **table** - 'table' will enumerate service fabric events from blob tables 'fabriclogs*'
  - **trace** - 'trace' will enumerate service fabric diagnostic logs (.dtr) zip blobs from 'fabriclogs*'
  - **any** - 'any' without other filters will enumerate all containers for blobs matching criteria.
- **List** - bool. default false. if true, lists the blobs meeting all criteria for download but does not download the file.
- **LogDebug** - int. default 4. if > 0, logs additional 'debug' output to console for troubleshooting. 0-disabled, 1-exception, 2-error, 3-warning, 4-info, 5-debug.
- **LogFile** - optional. string. default null. if populated with file and path, will log all console output to specified file. file is recreated every execution if exists.
- **NodeFilter** -  optional. string / regex. if populated uses client side searching for blobs after enumeration before download.
- **NoProgressTimeoutMin** - optional. int. default 10. if no progress has been made during given timeout, utility will exit. set to 0 to disable.
- **ResourceUri** - optional. string. used internally for resource tracking.
- **SasKey** - required unless using existing data in 'CacheLocation' from prior execution or 'FileUris'. string. string type options: account sas uri, service sas uri, or sas connection string. see [shared access signatures](https://docs.microsoft.com/en-us/rest/api/storageservices/delegating-access-with-a-shared-access-signature).
- **StartTimeStamp** - datetime string. default is -2 hours. example format: "10/31/2018 20:00:00 +00:00".
- **Threads** - int. default is number of cpu. if specified, is the number of concurrent threads to use for download and ingest to Kusto overriding number of cpus.
- **UriFilter** - optional. string. if populated has to be blob uri prefix and uses fast server side searching for blobs.
- **UseMemoryStream** - optional. bool. default true. if enabled, uses MemoryStream instead of disk for all operations except parsing of counter files. this option improves performance by not having to rely on disk.
- **Unique** - optional. bool. default true. if enabled, option ensures duplicate records are not ingested into same table.

#### collectsfdata azure arguments (optional)
#### collectsfdata kusto arguments

- **KustoCluster** - required. uri. kusto cluster ingest url found in properties in azure portal. example: https://ingest-{{cluster}}[.{{location}}].kusto.windows.net/{{database}}
- **KustoCompressed** - optional. bool. default true. if enabled, will compress (zip) files before sending to kusto saving network bandwidth.
- **KustoPurge** - optional. bool. default false. if enabled, will attempt to remove data from Kusto database.
- **KustoRecreateTable** - bool. default false. if true, will drop (recreate) table before ingesting new data regardless if table is currently populated.
- **KustoTable** - required. string. name of kusto table to create and or use.
- **KustoUseBlobAsSource** - bool. default true. if true will ingest service diagnostic logs directly from azure storage account instead of downloading and formatting. this option is remarkably faster and is preferred option when collecting data from azure clusters. **NOTE:** this option will *not* work if there is a firewall in between Kusto ingest queue servers and storage account.
- **KustoUseIngestMessage** - bool. default true. if true, will use kusto fail and success queue messaging for data ingestion (service bus additional overhead). if false, use kusto 'ingestion failures' table and data table 'RelativeUri' field for confirmation. better performance when set to false for larger ingests example 'KustoUseBlobAsSource'.

#### collectsfdata log analytics arguments

- **LogAnalyticsId** - required. guid. log analytics workspace id guid.
- **LogAnalyticsKey** - required. base64 key. primary / secondary key located in workspace advanced settings.
- **LogAnalyticsName** - string. name / tag for custom log ingest. requires first character to be alpha.

## Authorization

  **NOTE: Connecting to Azure resources requires an Azure Active Directory (AAD) Application registration. 
  see [aad-configuration](./aad-configuration.md).**

- **AzureClientId** - required. guid. AAD app registration object id.
- **AzureClientCertificate** - optional. string. required if not using AzureClientSecret. used for non-interactive authorization. can be in the following forms:
  - thumbprint if using local store 'D60F1AA6632B4C2A385879C227387359535B77DE'
  - path to file name if using local file system 'cluster.pfx'
  - subject name if using local store 'sfcluster.com'
  - secret name if using azure key vault 'star-sfcluster-com'
  - base64 string if adding cert directly into configuration 'MIIV3AIBAzCCF...Jsc='  
    You can use the following PowerShell command to obtain a Base64-encoded representation of a certificate:

    ```powershell
    [convert]::ToBase64String([io.file]::ReadAllBytes("C:\path\to\certificate.pfx"))
    ```

- **AzureClientSecret** - optional. string. required if not using AzureClientCertificate. string. Can also be used to pass certificate password.
- **AzureKeyVault** - optional. url. can be used to store AzureClientCertificate if being used.
  - 'https://{{key vault name}}.vault.azure.net/'
- **AzureResourceGroup** - optional. string. required if using Log Analytics and creating a workspace. string. if populated, value is used for creation of Log Analytics workspace.
- **AzureResourceGroupLocation** - optional. string to identify current Azure region for MSAL authentication string. Default value 'TryAutoDetect' will be set if not populated. required if using Log Analytics and creating a workspace. string. if populated for Log Analytics, value is used for location of resource group for creation of Log Analytics workspace.
- **AzureSubscriptionId** - optional. guid. required if tenant contains multiple subscriptions and using AzureClientId.
- **AzureTenantId** - optional. guid. used in confidential and public client authentication if *not* using 'common'.

### **Using with Client Credentials for non-interactive execution**

#### **Configuration of Client Certificate**

CollectSFData can use a certificate for authorization to Azure.  
The certificate can be stored in the following locations:  
- local file  
    "AzureClientCertificate": "c:\\temp\\collectsfdata.pfx",  
- local cert store  
    "AzureClientCertificate": "*.sfcluster.com",  
- base64 string  
    "AzureClientCertificate": "MIIXDAIBAzCCFsgGCSqGSIb3D...",  
- keyvault  
    "AzureClientSecret": "cluster-key-vault-secret",  
    "AzureKeyVault": "https://clusterkeyvault.vault.azure.net/",  

### Example Authorization configurations  

Use these examples to configure CollectSFData to run non-interactively with client credentials and client certificate.  

example variables:  
app registration id: f4289be6-a77a-4554-b5d7-13a5d0ef66c7  
app registration secret name: app-registration-secret  
certificate base64 partial string: MIIXDAIBAzCCFsgGCSqGSIb3D...  
key vault url: https://clusterkeyvault.vault.azure.net/  
key vault secret name: cluster-key-vault-secret  
subscription id: 3ddc104f-35a1-4e5a-8122-b18c15a486bf  
tenant id: b4be3bd5-1e7f-4c0c-a9b4-97d1d1bb0290  
user managed identity: 3080722d-0cf6-4552-8e45-c5ccbc3d091f  

#### **app registration and client certificate**

```json
{
  "AzureClientId": "f4289be6-a77a-4554-b5d7-13a5d0ef66c7",
  "AzureClientCertificate": "MIIXDAIBAzCCFsgGCSqGSIb3D...",
  "AzureClientSecret": null,
  "AzureKeyVault": null,
  "AzureSubscriptionId": "3ddc104f-35a1-4e5a-8122-b18c15a486bf",
  "AzureTenantId": "b4be3bd5-1e7f-4c0c-a9b4-97d1d1bb0290",
}
```

#### **app registration and client certificate and password**

```json
{
  "AzureClientId": "f4289be6-a77a-4554-b5d7-13a5d0ef66c7",
  "AzureClientCertificate": "MIIXDAIBAzCCFsgGCSqGSIb3D...",
  "AzureClientSecret": "plaintextpassword",
  "AzureKeyVault": null,
  "AzureSubscriptionId": "3ddc104f-35a1-4e5a-8122-b18c15a486bf",
  "AzureTenantId": "b4be3bd5-1e7f-4c0c-a9b4-97d1d1bb0290",
}
```

#### **app registration and client secret**

```json
{
  "AzureClientId": "f4289be6-a77a-4554-b5d7-13a5d0ef66c7",
  "AzureClientCertificate": null,
  "AzureClientSecret": "app-registration-secret",
  "AzureKeyVault": null,
  "AzureSubscriptionId": "3ddc104f-35a1-4e5a-8122-b18c15a486bf",
  "AzureTenantId": "b4be3bd5-1e7f-4c0c-a9b4-97d1d1bb0290",
}
```

#### **app registration with user managed identity to key vault**

```json
{
  "AzureClientId": "f4289be6-a77a-4554-b5d7-13a5d0ef66c7",
  "AzureClientCertificate": null,
  "AzureClientSecret": "cluster-key-vault-secret",
  "AzureKeyVault": "https://clusterkeyvault.vault.azure.net/",
  "AzureSubscriptionId": null,
  "AzureTenantId": null,
}
```

#### **app registration with system managed identity to key vault**

```json
{
  "AzureClientId": "f4289be6-a77a-4554-b5d7-13a5d0ef66c7",
  "AzureClientCertificate": null,
  "AzureClientSecret": "cluster-key-vault-secret",
  "AzureKeyVault": "https://clusterkeyvault.vault.azure.net/",
  "AzureSubscriptionId": null,
  "AzureTenantId": null,
}
```

#### **user managed identity**

```json
{
  "AzureClientId": "3080722d-0cf6-4552-8e45-c5ccbc3d091f",
  "AzureClientCertificate": null,
  "AzureClientSecret": null,
  "AzureKeyVault": null,
  "AzureSubscriptionId": null,
  "AzureTenantId": null,
}
```

### Example JSON configurations

#### **clean configuration without Kusto**

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "[counter|exception|setup|sfextlog|trace|table|any]",
  "LogDebug": 4,
  "CacheLocation": "<%fast drive path with 100 GB free%>",
  "SasKey": "[account sas uri|service sas uri|sas uri connection string]",
  "StartTimeStamp": null,
  "EndTimeStamp": null,
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": ""
}
```

#### **clean configuration with Kusto**

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "[counter|exception|setup|sfextlog|trace|table|any]",
  "LogDebug": 4,
  "CacheLocation": "<%fast drive path with 100 GB free%>",
  "SasKey": "[account sas uri|service sas uri|sas uri connection string]",
  "StartTimeStamp": null,
  "EndTimeStamp": null,
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "",
  "KustoCluster": "https://<kusto ingest url>.<location>.kusto.windows.net/<kusto database>",
  "KustoRecreateTable": false,
  "KustoTable": "<%kusto table name%>"
}
```

#### **clean configuration with Log Analytics**

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "[counter|exception|setup|sfextlog|trace|table|any]",
  "LogDebug": 4,
  "CacheLocation": "<%fast drive path with 100 GB free%>",
  "SasKey": "[account sas uri|service sas uri|sas uri connection string]",
  "StartTimeStamp": null,
  "EndTimeStamp": null,
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "",
  "LogAnalyticsId" : "<% oms workspace id %>",
  "LogAnalyticsKey" : "<% oms primary / secondary key %>",
  "LogAnalyticsName" : "<% oms tag / name for ingest %>"
}
```

#### **configuration for downloading service fabric diagnostic trace logs**

for download only:

- Kusto* settings cannot not be configured.  
- LogAnalytics* settings cannot be configured.  
- CacheLocation has to be configured.  

NOTE: for standalone clusters a central diagnostic store must be configured

```json
{
  "CacheLocation": "g:\\cases",
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "trace",
  "LogDebug": 4,
  "SasKey": "https://sflogsxxxxxxxxxxxxx.blob.core.windows.net/...",
  "StartTimeStamp": "10/31/2018 20:00:00 +00:00",
  "EndTimeStamp": "10/31/2018 22:30:00 +00:00",
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "",
}
```

#### **configuration for uploading downloaded service fabric diagnostic trace logs from above**

for upload only:

- Kusto* or LogAnalytics* has to be configured.
- CacheLocation has to be configured.
- Cachelocation has to be populated from [collectsfdata download](#example-configuration-for-downloading-service-fabric-diagnostic-trace-logs) with correct structure.
- SasKey cannot not be configured.

NOTE: for standalone clusters a central diagnostic store must be configured

```json
{
  "CacheLocation": "g:\\cases",
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "trace",
  "KustoCluster": "https://<kusto ingest url>.<location>.kusto.windows.net/<kusto database>",
  "KustoRecreateTable": false,
  "KustoTable": "<%kusto table name%>",
  "LogDebug": 4,
  "SasKey": null,
  "StartTimeStamp": "10/31/2018 20:00:00 +00:00",
  "EndTimeStamp": "10/31/2018 22:30:00 +00:00",
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "",
}
```

#### **configuration for downloading service fabric diagnostic trace logs and uploading to kusto**

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "trace",
  "List": false,
  "LogDebug": 4,
  "CacheLocation": "g:\\cases",
  "SasKey": "https://sflogsxxxxxxxxxxxxx.blob.core.windows.net/...",
  "StartTimeStamp": "10/31/2018 20:00:00 +00:00",
  "EndTimeStamp": "10/31/2018 22:30:00 +00:00",
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "nt0",
  "KustoCluster": "https://ingest-kustodb.eastus.kusto.windows.net/serviceFabricDB",
  "KustoRecreateTable": true,
  "KustoTable": "_00000000000001"
}
```

#### **configuration for ingesting adhoc / custom service fabric diagnostic trace logs into kusto**

```json
{
  "ContainerFilter": "",
  "DeleteCache": true,
  "GatherType": "trace",
  "LogDebug": 4,
  "FileUris":[
    "https://sflogsxxxxxxxxxxxxx.blob.core.windows.net/fabriclogs-8de7b13a-4137-454a-9ad5-a356fa0c3159/_nt0_2/Fabric/bc4316ec4b0814dcc367388a46d9903e_fabric_traces_7.2.457.9590_132610909242411002_3_00637522249901331579_0000000000.dtr.zip",
    "c:/temp/trace.dtr.zip"
   ],
  "SasKey": "https://sflogsxxxxxxxxxxxxx.blob.core.windows.net/...",
  "Threads": 8,
  "UriFilter": "",
  "NodeFilter": "nt0",
  "KustoCluster": "https://ingest-kustodb.eastus.kusto.windows.net/serviceFabricDB",
  "KustoRecreateTable": true,
  "KustoTable": "_00000000000001"
}
```

#### **default configuration generated from collectsfdata -save collectsfdata.options.json**

```json
{
  "$schema": "https://raw.githubusercontent.com/microsoft/CollectServiceFabricData/master/configurationFiles/collectsfdata.schema.json",
  "AzureClientId": null,
  "AzureClientCertificate": null,
  "AzureResourceGroup": null,
  "AzureResourceGroupLocation": null,
  "AzureSubscriptionId": null,
  "AzureTenantId": null,
  "CacheLocation": "C:/Users/user/AppData/Local/Temp",
  "ContainerFilter": null,
  "DeleteCache": false,
  "EndTimeStamp": "10/25/2019 08:05 -04:00",
  "GatherType": "unknown",
  "KustoCluster": null,
  "KustoCompressed": false,
  "KustoPurge": null,
  "KustoRecreateTable": false,
  "KustoTable": null,
  "KustoUseBlobAsSource": false,
  "List": false,
  "LogAnalyticsCreate": false,
  "LogAnalyticsId": null,
  "LogAnalyticsKey": null,
  "LogAnalyticsName": null,
  "LogAnalyticsPurge": null,
  "LogAnalyticsRecreate": false,
  "LogAnalyticsWorkspaceName": null,
  "LogAnalyticsWorkspaceSku": "PerGB2018",
  "LogDebug": 4,
  "LogFile": null,
  "NodeFilter": null,
  "ResourceUri": null,
  "SasKey": "",
  "Schema": null,
  "StartTimeStamp": "10/25/2019 06:05 -04:00",
  "Threads": 8,
  "Unique": true,
  "UriFilter": null,
  "UseMemoryStream": true
}
```
