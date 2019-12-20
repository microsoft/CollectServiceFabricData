# CollectSFData Quickstart for Kusto Cluster

## Outline  

[Overview](#use-these-steps-to-setup-and-ingest-service-fabric-diagnostic-data-into-kusto-database)  
[Generating Shared Access Signature (Sas Uri / Saskey)](#generating-shared-access-signature-(sas-uri-/-saskey))  

- [Azure Portal](#azure-portal)  

[CollectSFData GatherTypes](#collectsfdata-gathertypes)  

- [counter](#gathertype-counter)  
- [exception](#gathertype-exception)  
- [setup](#gathertype-setup)  
- [table](#gathertype-table)  
- [trace](#gathertype-trace)  

[Download](#download)  
[Execute](#execute)  
[collectsfdata.options.json](#collectsfdata.options.json)  
[Cleanup](#cleanup)  
[Kusto Commands](#kusto-commands)  
[Troubleshooting](#Troubleshooting)  
[Reference](#Reference)  

## Use these steps to setup and ingest service fabric diagnostic data into kusto database  

1. download and extract latest release of [collectsfdata](https://github.com/microsoft/CollectServiceFabricData/releases/latest)
1. open cmd/powershell prompt and navigate to extracted directory
1. save [configuration](#collectsfdata.options.json) to extracted directory or generate new config with .\collectsfdata.exe -save collectsfdata.options.json or pass arguments on command line
1. modify saskey, starttimestamp, endtimestamp, kustocluster, kustotable, and gathertype  
    * service fabric kusto cluster ingest url: `https://ingest-{{cluster}}.{{location}}.kusto.windows.net/{{database}}`
    * 30 minutes is always added to EndTimeUtc due to the way sf uploads traces to storage account.  
1. execute utility.
1. analyze data `https://dataexplorer.azure.com/clusters/{{cluster}}/databases/{{database}}`

## Generating Shared Access Signature (Sas Uri / Saskey)

CollectSFData uses a sas uri similar to Traceviewer to connect to the 'sflogs' storage account used by the service fabric cluster to store data. This can be generated from the Azure portal.

### Azure portal

From Azure portal https://portal.azure.com navigate to service fabric cluster resource. There will typically be two storage accounts created for service fabric. One is for windows azure diagnostics (wad) and the other for service fabric diagnostic logs and events. The service fabric storage account is usually prefixed with 'sflogs'. If not, the correct account can be verified by determining which storage account has the three containers: 

- fabriccounters-*
- fabriccrashdumps-*
- fabriclogs-*

- ![](media/azure.portal.1.png)

- ![](media/azure.portal.2.png)

Once correct storage account is identified, select 'Shared access signature' and then 'Generate SAS and connection string'. Copy 'Blob service SAS URL' or 'Connection Sting'. This is the value that will be used for CollectSFData argument 'SasKey'.

- ![](media/azure.portal.3.png)

- ![](media/azure.portal.4.png)

## CollectSFData GatherTypes  

All gather types ingest into Kusto or Log Analytics as a single table per gather type. The gather type is also prepended to the table name during ingestion. This is to allow different gather types to be collected without having to change configuration for table name and for table naming constraints.  

Example: configured table name 'jagilber_0000000000000001' would be prepended with 'counter_' for gather type counter 'counter_jagilber_0000000000000001'. 

### GatherType counter  

#### data type: sf node performance monitor .blg files.  

#### time range: can typically gather 6 - 24+ hours without issue.  

#### start here:  

- use [perfmon kusto query](https://github.com/microsoft/CollectServiceFabricData/blob/master/docs/KustoQueries/sfcounters-general.csl) from [azure data explorer](https://dataexplorer.azure.com) to graph results  
    * change value in query from '['counter_serviceFabricLogs']' to '['counter_<%user%>_<%case%>']' for table name to be graphed.
- to view results, uncomment one counter line in query and execute.
    * // | where CounterName contains "Avg. Disk Queue Length" and CounterName contains "c:"
    * | where CounterName contains "Avg. Disk Queue Length" and CounterName contains "c:"
  
#### counter record

```text
{
"Timestamp": 2019-10-28T11:52:32.418Z,
"CounterName": \\nt0000002\Service Fabric TStore\Checkpoint File Write Bytes/sec,
"CounterValue": 0,
"NodeName": _nt0_2,
"FileType": counter,
"RelativeUri": fabriccounters-96b543e6-6444-462d-a4a6-6dd524a059b7/_nt0_2/fabric_counters_637068450074374168_000377.blg.perf.csv,
}
```

### GatherType exception  

- sf fabric*.exe dumps.  
- adds 'exception_' prefix to table name.
- creates table with records containing url, pid, and process name for dumps from storage account.  
- console output lists download url with sas.  
- can run for any time range.  

#### example exception record

```text
{
"Timestamp": 2019-11-06T01:01:36Z,
"PID": 2732,
"Type": FabricDCA,
"Text": https://xxx.blob.core.windows.net/fabriccrashdumps-75c7a5b0-649b-4b8a-bbf5-39a28ed3ae93/_nt0_1/FabricDCA.exe.2732.dmp,
"NodeName": _nt0_1,
"FileType": fabriccrashdumps,
"RelativeUri": fabriccrashdumps-75c7a5b0-649b-4b8a-bbf5-39a28ed3ae93/_nt0_1/FabricDCA.exe.2732.dmp,
}
```

#### example console output

```powershell
11/15/19 12:55:53 +00:00 https://xxx.blob.core.windows.net/fabriccrashdumps-75c7a5b0-649b-4b8a-bbf5-39a28ed3ae93/_nt0_1/Fabric.exe.6224.dmp?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2019-12-04T21:16:27.0651279Z&sig=8vn550BtkQCBS2DXRQ%2B%2FmcBBxa%2FjeB7TNr05xO1EZig%3D
11/15/19 12:57:36 +00:00 https://xxx.blob.core.windows.net/fabriccrashdumps-75c7a5b0-649b-4b8a-bbf5-39a28ed3ae93/_nt0_1/Fabric.exe.13044.dmp?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2019-12-04T21:16:27.0651279Z&sig=8vn550BtkQCBS2DXRQ%2B%2FmcBBxa%2FjeB7TNr05xO1EZig%3D
https://dataexplorer.azure.com
1:Execute:4024 files enumerated.
1:Execute:15 files matched.
1:Execute:0 files downloaded.
1:Execute:0 files skipped.
1:Execute:15 files formatted.
1:Execute:0 errors.
1:Execute:15 records.
1:Execute:total execution time in minutes: 0.41
```

### GatherType setup  

- sf install .trace files.  
- adds 'setup_' prefix to table name. 
- can run for any time range as data is small but typically only needed for time of issue. 

#### setup record

```text
{
"Timestamp": 2019-11-06T01:06:55.85Z,
"Level": Info,
"PID": 2996,
"Type": ImageBuilder.FabricDeployer,
"Text": Network interface ip address: fe80::b841:872:f6fd:2205%7.,
"NodeName": _nt0_0,
"FileType": fabricdeployer,
"RelativeUri": fabriclogs-52290932-903d-4492-883d-5ae186f0c313/_nt0_0/Bootstrap/f45f24746c42cc2a6dd69da9e7797e2c_FabricDeployer-637085992157096945.trace,
}
```

### GatherType table  

#### data type: sf cluster and node storage account table events. same as available in sfx explorer events.  

#### time range: can typically gather 2 - 7+ days without issue.  

#### start here:  

- best viewed with kusto function 'TableView('table_%user%_%case%')'.
- Example: 
    * TableView('table_jagilber_000000000000001') | facet by TaskName
    * TableView('table_jagilber_000000000000001') | where TaskName contains "hosting"

#### table record with TableView where TaskName is 'Hosting':  

```json
{
"Timestamp": 2019-10-28T04:00:27.0397761Z,
"PartitionKey": Hosting.ProcessExitedOperational,
"RowKey": 02518300514278364702_f7887fc4eaa9252af2883c45b819a018_2_5724_9320,
"RelativeUri": fabriclog96b543e66444462da4a66dd524a059b7ApplicationsOps,
"ExeName": start.bat,
"ExitCode": 0,
"HostId": 554aae28-6b00-4303-8071-f5fac02ae72a,
"TaskName": Hosting,
"CodePackageName": Code,
"EventType": ProcessExitedOperational,
"EventVersion": 2,
"UnexpectedTermination": True,
"dca_version": -2147483648,
"EntryPointType": 1,
"IsExclusive": True,
"ServicePackageName": TaskRunnerPkg,
"ProcessId": 15144,
"ServiceName": fabric:/PeriodicScriptService/TaskRunner,
"ServicePackageActivationId": a2442698-3076-459d-8a9a-f9a57d2520cf,
"StartTime": 2019-10-28T07:56:17.552874Z,
"applicationName": fabric:/PeriodicScriptService,
"category": StateTransition,
"eventInstanceId": 7f32ac63-4eea-424b-ae9a-5413d8024893,
"eventName": ApplicationProcessExited,
}
```

#### table record without TableView:  

```json
{
"Timestamp": 2019-10-27T20:17:40.2919037Z,
"EventTimeStamp": 2019-10-28T00:16:18.658833Z,
"ETag": W/"datetime'2019-10-28T00%3A17%3A40.2919037Z'",
"PartitionKey": RE.PrimaryReplicationQueueWarningEvent,
"RowKey": 02518300790213411661_d35d5c46ea9c7a12a6c6b2629604e55f_5_16428_23244,
"PropertyName": EventType,
"PropertyValue": PrimaryReplicationQueueWarningEvent,
"RelativeUri": fabriclogdf933b6d7f704376a07ef1862f877fa3ReplicasOps
}
```

### GatherType trace  

#### data type: sf node sflog .etl/.dtr/.zip files. same data as traceviewer  

#### time range: can typically gather 2 - 4+ hours without issue.  

#### best practice:  

- use 'KustoCompressed' to reduce network traffic
- use 'UriFilter' and set to 'fabric_' for fabric only traces
- use 'UriFilter' and set to 'lease_' for lease only traces
- use regex / string based 'NodeFilter' and add name of node(s) to gather from if all nodes are not needed.  
examples:
    * NodeFilter: "\_nt_0" to collect data for only node 0
    * NodeFilter: "\_nt_0|_nt_1" to collect data for only nodes 0 and 1
    * NodeFilter: "\_nt_0|_nt_1|_nt_2" to collect data for only nodes 0 - 2
    * NodeFilter: "\_nt_[0-1]" to collect data for only nodes 0 - 1
    * NodeFilter: "\_nt_[135]" to collect data for only nodes 1, 3, and 5
    * NodeFilter: "\_nt_" to collect data for only nodes for nodetype 'nt'

#### start here:
- use kusto functions. functions are viewable in tree view from azure data explorer or by typing '.show functions'  
from [azure data explorer](https://dataexplorer.azure.com) console, type the name of the function and pass name of table in single quotes.

- Example: TraceSummary('trace_jagilber_000000000000001')  

  ![kusto-function-tree-view.png](./media/kusto-function-tree-view.png)  

- use [kusto queries](https://github.com/microsoft/CollectServiceFabricData/blob/master/docs/KustoQueries/kusto-example-queries.csl) from [azure data explorer](https://dataexplorer.azure.com) to view results  
    * change value in queries from '%kusto table name%' to 'trace_<%user%>_<%case%>' for trace table name to be viewed.

#### trace record:  

```text
{
"Timestamp": 2019-10-22T14:35:27.761Z,
"Level": Informational,
"TID": 1112,
"PID": 1272,
"Type": Lease.ApiEnd,
"Text": 'Lease api end GET_REMOTE_LEASE_EXPIRATION_TIME',
"NodeName": _nt0_0,
"FileType": lease,
"RelativeUri": fabriclogs-33cf5c42-a3ba-403c-9717-6aaadaa45a55/_nt0_0/Lease/c3daf3c2018074d5bd8d8b8fa7cbd7bc_lease_traces_6.5.676.9590_132161469049099725_8_00637073520242075217_2147483647.dtr
}

```

## download

```powershell
$apiResults = convertfrom-json (iwr "https://api.github.com/repos/microsoft/CollectServiceFabricData/releases/latest" -UseBasicParsing)
(new-object net.webclient).downloadFile($apiResults.assets.browser_download_url,"$pwd\collectsfdata.zip")
expand-archive .\collectsfdata.zip .\collectsfdata
cd .\collectsfdata
```

## execute

### fabric and lease trace for all nodes command line

```powershell
.\collectsfdata.exe `
    --start "11/01/2019 08:00 +00:00" `
    --stop "11/01/2019 10:30 +00:00" `
    --gatherType trace `
    --kustoCluster "https://ingest-{{cluster}}.{{location}}.kusto.windows.net/{{database}}" `
    --kustoTable "jagilber_0000000000000001" `
    --sasKey "https://XXX.blob.core.windows.net/containerName?sasToken"
```

### fabric trace for node command line

```powershell
.\collectsfdata.exe `
    --start "11/01/2019 08:00 +00:00" `
    --stop "11/01/2019 10:30 +00:00" `
    --gatherType trace `
    --nodeFilter "_nt0_0" `
    --uriFilter "fabric_" `
    --kustoCluster "https://ingest-{{cluster}}.{{location}}.kusto.windows.net/{{database}}" `
    --kustoTable "jagilber_0000000000000001" `
    --sasKey "https://XXX.blob.core.windows.net/containerName?sasToken"
```

### fabric trace for nodes command line

```powershell
.\collectsfdata.exe `
    --start "11/01/2019 08:00 +00:00" `
    --stop "11/01/2019 10:30 +00:00" `
    --gatherType trace `
    --nodeFilter "_nt0_0|_nt0_1" `
    --uriFilter "fabric_" `
    --kustoCluster "https://ingest-{{cluster}}.{{location}}.kusto.windows.net/{{database}}" `
    --kustoTable "jagilber_0000000000000001" `
    --sasKey "https://XXX.blob.core.windows.net/containerName?sasToken"
```

### command line generate default configuration file

Creating a default configuration file named 'collectsfdata.options.json' in working directory eliminates having to specify a configuration file or configuration options on the command line.

```powershell
.\collectsfdata.exe -save .\collectsfdata.options.json --kustoCluster "https://ingest-{{cluster}}.{{location}}.kusto.windows.net/{{database}}"
.\collectsfdata.options.json
write-host "modify saskey, starttimestamp, endtimestamp, kustotable, and gathertype"
pause
.\collectsfdata.exe

```

### command line generate custom configuration file

Creating a custom configuration json file in any directory allows multiple configurations that can be used from command line.

```powershell
param([Parameter(Mandatory=$true)]$configurationFile)
.\collectsfdata.exe -save $configurationFile --kustoCluster "https://ingest-{{cluster}}.{{location}}.kusto.windows.net/{{database}}"
. $configurationFile
write-host "modify saskey, starttimestamp, endtimestamp, kustotable, and gathertype"
pause
.\collectsfdata.exe -config $configurationFile

```

### collectsfdata.options.json

```json
{
    "$schema": "https://raw.githubusercontent.com/microsoft/CollectServiceFabricData/master/configurationFiles/collectsfdata.schema.json",
    "CacheLocation": null,
    "ContainerFilter": null,
    "DeleteCache": false,
    "EndTimeStamp": null, // enter end time or leave blank for now
    "GatherType": "trace", // trace , counter, setup, exception, or table
    "KustoCluster": "https://ingest-{{cluster}}.{{location}}.kusto.windows.net/{{database}}", // sf pg kusto db ingest url
    "KustoCompressed": true,
    "KustoRecreateTable": false, // true to drop and create table before ingest
    "KustoTable": "<%username%>_<%case number%>", // enter username and case number for table name
    "KustoUseBlobAsSource": false, // if sf is > 6.5 and gathertype 'trace' this can be set to true for faster processing (some traces still not working in 6.5)
    "LogDebug": false,
    "LogFile": null,
    "NodeFilter": null, // regex / string match for one or more nodes ex: '_nt0_[0-4]' or '_nt0_0|_nt0_1|_nt0_2|_nt0_3|_nt0_4'
    "ResourceUri": null, // fill out for sf pg (future)
    "SasKey": null, // enter customer sas uri for blob 
    "StartTimeStamp": null, // enter start time or leave blank for -2 hours
    "Threads": 8,
    "Unique": true, // true to prevent duplicate records on multiple ingests
    "UriFilter": null, // set to 'fabric_' for only fabric traces. set to 'lease_' for only lease traces. default is all
    "UseMemoryStream": true
}
```

## cleanup

### collectsfdata.exe commands to delete / drop table when no longer needed

1. to view all tables: collectsfdata.exe -kp list
1. to drop table: collectsfdata.exe -kp <%table name%>  
     ex: collectsfdata.exe -kp trace_jagilber_0000000000000001
1. to verify table has been deleted / dropped (not necessary): collectsfdata.exe -kp list

### kusto commands to delete / drop table when no longer needed

1. open url: https://dataexplorer.azure.com
1. to view all tables: .show tables
1. to view all tables with name match: .show tables | where TableName contains "<%filter%>"  
     ex: .show tables | where TableName contains "jagilber"
1. to drop table: .drop table <%table name%>  
     ex: .drop table trace_jagilber_0000000000000001  
     note: on success, output will display all tables remaining
1. to verify table has been deleted / dropped (not necessary): .show table <%table name%>

## kusto commands

- to show all tables: .show tables  
- query to display previous queries: .show queries  
- drop table: .drop table <%table name%>
- ingestion failures: .show ingestion failures
- table ingestion time: <%table name%> | top 1 by Timestamp asc | project ingestion_time()

## troubleshooting  

1. E_WRONG_NUBER_OF_FIELDS. this can be caused by setting 'UseKustoBlobAsSource' to true. some events are still not csv compliant. to resolve, set  'UseKustoBlobAsSource' to false.  

```text
    5:QueueMonitor:error: Ingestion error total:(46): {
    "OperationId": "4d026446-2de4-4c29-b74e-37d021dfc33c",
    "Table": "trace_jagilber_test",
    "FailedOn": "2019-11-04T00:57:13.6847693Z",
    "IngestionSourceId": "5d608b78-2235-4885-911e-b16a84388a85",
    "Details": "Stream_WrongNumberOfFields: Stream with id 'bc4316ec4b0814dcc367388a46d9903e_fabric_traces_6.5.676.9590_132172633687587976_4_00637083925242089496_0000000000.dtr' has a malformed Csv format, failing per ValidationOptions policy with errorCode='0x80DA0008 E_WRONG_NUMBER_OF_FIELDS'\r\nAdditional information: HRESULT=0x80da0008\nRecord=8709\n(E_WRONG_NUMBER_OF_FIELDS)\nValidator=struct Kusto::Csv::CsvFormatValidatingParserTraits\nFragment=\nKusto::Csv::Parser<>.PrepareFields: CSV has an inconsistent number of fields per line:  -- Offending record: 8710 (start position in stream: 2663151), fieldsCount: 8, currentRecordFieldCount: 8, record: 2019-11-3 15:38:39.501,Informational,2720,2368,Transport.State@1e2920b0b60-Endpoint:10.0.0.6:20002,NodeName:_nt0_2,NodeInstanceId:132172634438947603,\"owner = 'FileTransfer', listenAddress = '10.0.0.6:20002', instance = 0, securitySettings = { provider=SSL protection=EncryptAndSign certType = '' store='LocalMachine/My' findValue='FindByThumbprint:fe76761442db1e245f1148be64af1e0f87effcb8' remoteCertThumbprints='fe76761442db1e245f1148be64af1e0f87effcb8' certChainFlags=40000000 isClientRoleInEffect=false claimBasedClientAuthEnabled=false }\"\r\n[end record]",
    "ErrorCode": "Stream_WrongNumberOfFields",
    "FailureStatus": "Permanent",
    "RootActivityId": "0e7d9608-64ca-460d-ad49-b5da22cfcbd0",
    "OriginatesFromUpdatePolicy": false,
    "ShouldRetry": false,
    "IngestionSourcePath": "https://xxx.blob.core.windows.net/fabriclogs-b2ca4ad0-2d44-44db-99a1-e93e56d35841/_nt0_2/Fabric/bc4316ec4b0814dcc367388a46d9903e_fabric_traces_6.5.676.9590_132172633687587976_4_00637083925242089496_0000000000.dtr.zip"
    }

```

## reference

### vscode extensions

Useful extensions for vscode in general.

* code-settings-sync - syncs vscode settings and extensions between machines using git gist.
* azurerm-vscode-tools - azure ARM from microsoft adds functionality for ARM templates.
* eamodio.gitlens - defacto standard for git functionality in vscode.
* wengerk.highlight-bad-chars - highlights invisible (bad) unicode characters.
* rsbondi.highlight-words - highlights matching text in uniquely colored frames.
* vscode-open - context action to launch default file type association.
* rainbow-csv - colors .csv file columns in different colors.

Some useful Kusto extensions for vscode that assist with syntax and coloring.

* kuskus-extensions-pack
* kuskus-kusto-color-themes
* kuskus-kusto-language-server
* kuskus-kusto-syntax-highlighting
* kusto-syntax-highlighting

### kusto reference

Kusto / Azure Data Explorer / Kusto Query Language (KQL) documentation is available online.
[Azure Data Explorer](https://docs.microsoft.com/en-us/azure/kusto/)
