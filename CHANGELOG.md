# Change log

## 06/01/2021

- adding support for formatting service fabric etw (.etl) files to csv for kusto ingest  
- using sf binary etlreader.dll for formatting etl  
- using sf manifests (.man) for formatting etl in /manifests directory  
- if /manifests directory is not found, repo will be checked
- update schema with new EtwManifestsCache value defaults to ./manifests
- reduce attempts to check for managedIdentity
- fix 'ContainerFilter' not always being applied correctly if other containers match container prefix.
- merge test projects.
- add additional tests.
- move debug logging to file only.
- update nuspec to reduce file size and add manifests.
- only check time variables if not empty.
- add pdbs to nuget package.
- add postbuild events ps1 to solution.
- improve logsummary output indicating items of interest for overall result.

    ```text
    1:LogSummary:22 files enumerated.
    1:LogSummary:22 files matched.
    1:LogSummary:22 files downloaded.
    1:LogSummary:22 files formatted.
    1:LogSummary:0 files skipped.
    1:LogSummary:4643415 parsed events.
    1:LogSummary:timed out: False.
    1:LogSummary:FileObjects:status:unknown:0 enumerated:0 existing:0 queued:0 downloading:0 formatting:0 uploading:0 failed:0 succeeded:22 all:22
    1:LogSummary:discovered time range: 2021-05-11T21:53:04.3278378 - 2021-06-01T12:38:10.0000000
    1:LogSummary:1 errors.
    1:LogSummary:0 files failed to be processed.
    1:LogSummary:total execution time in minutes: 1.14
    ```

## 4/18/2021

- fix potential scenario where blob could be deleted from storage by hardcoding RetainBlobOnSuccess = true
- fix issue where uriString may be null for temporary container or ingestionqueue resources for kusto upload
- fix nuget warnings during build for net5.0
- split properties from ConfigurationOptions.cs into ConfigurationProperties.cs for class reuse
- allow new ConfigurationOptions instances to be used.
- add ConfigurationOptions argument to Collector
- ConfigurationOptions
- add DefaultConfiguration to ConfigurationOptions populated with default 'collectsfdata.options.json' and any commandline arguments if passed to constructor
- update [dllConfiguration](./docs/dllConfiguration.md)

## 04/08/2021

- add AzureClientCertificate property for use with AzureTenantId and AzureClientId for confidentialClient authentication with certificate in LocalMachine or CurrentUser My
- fix intermittent table name truncation after gathertype bad: trace_agilber_test good: trace_jagilber_test
- modify NoProgressTimeout from throw exception to tasks Cancel() allowing collector.Collect() to return 1 to caller
- modify EndTimeStamp / StartTimeStamp to ignore empty strings and better timeformat error handling, logging
- modify Log.Last to always log regardless of LogDebug value
- sync Sf.Tx with microsoft.Tx final changes for DateTimeKind.Unspecified. waiting for next microsoft.Tx release to remove Sf.Tx
- update collectsfdata.schema.json with additional examples
- fix creation of log name when directory is not specified. will create in working directory
- expose ConfigurationOptions on collector instead of instance

## 04/06/2021

- add FileUris string array optional parameter (-uris|--fileUris) to pass file uri strings for upload. this will override default file collection from service fabric diagnosticsStore

    ```json
    "FileUris":[
        "C:\\temp\\f45f24746c42cc2a6dd69da9e7797e2c_fabric_traces_7.2.457.9590_132610909762170249_865_00637532483045019565_0000000000.dtr.zip",
        "C:\\temp\\f45f24746c42cc2a6dd69da9e7797e2c_fabric_traces_7.2.457.9590_132610909762170249_865_00637532485406085273_2147483647.dtr.zip"
    ],
    ```

- allow download and upload options in same execution

    ```c#
    if (Config.SasEndpointInfo.IsPopulated())
    {
        DownloadAzureData();
    }
    
    if (Config.IsCacheLocationPreConfigured() | Config.FileUris.Length > 0)
    {
        UploadCacheData();
    }
    ```

- add Clone() to ConfigurationOptions to copy configuration for multiple configs when using as dll.
- ConfigurationOptions can now be optionally passed as argument to Collect(config).

    ```c#
    // default constructor
    Collector collector = new Collector(args, true);
    // int retval = collector.Collect();

    // use Clone() to create shallow copy for multiple configurations
    ConfigurationOptions config = collector.Config.Clone();
    config.LogDebug = 6;
    int retval = collector.Collect(config);
    ```

## 03/09/2021  https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.9.2103.10923

- 2.9
- add strong name signing for binaries for jarvis integration

## 03/02/2021 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.8.2102.1282117

- downgrade newtonsoft and kusto for .net462 jarvis asc integration

## 02/27/2021 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.8.2102.1282117

- adding net462 targetframework
- fix for new kusto error when using blob as source Ingestion properties contains invalid CsvMapping ingestion mapping. Mapping: '' Invalidity reason: CsvMapping An item with the same key has already been added. Ordinal: '0' appears '1' times,

## 02/22/2021

- fix fields not matching in gathertype 'setup'. FormatTraceFile incorrectly using DtrTraceRecord instead of T. tested setup and trace gathertype

## 02/08/2021 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.8.2102.1081337

- 2.8
- fix gathertype table propertyvalue add quotes and replace " and ,
- fix table duplicate cleanup RelativeUri
- move table cleanup after Wait
- add export kusto function script
- exported current kusto functions  

## 01/30/2021 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.8.2101.1302111

- 2.8
- add System.Reactive dependency for Tx
- add uris argument to UpdloadData()
- modify PopulateConfig() to use GatherType to determine if configuration populated
- fix CheckLogFile to not check file if open
- fix table name prepend to prevent duplicate
- fix file name with multiple splits from concatenating counters in name
- testing microsoft Tx module to replace relog.exe (UseTx=true) non-default
- fix message compare for ingest causing no progress timeouts
- fix db table cursor kql syntax
- fix CSV compliance issue with Linux cluster tables  

## 12/27/2020 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.7.2012.31150827

- add version check in LogSummary random 10% of time if logging to console  
- fix aggregate collection modified exception in pending ingest list  

## 12/21/2020 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.7.2012.21142504

- modify collectsfdata.schema.json split title into title, description  
- add TraceKnownIssues.csl additional known issues  
- add trivial logging that logs only to logfile if logdebug > 5  
- fix ingest messages not being processed correctly resulting in noprogresstimeout  
- fix issue with database cursor not always being set correctly
- fix gathertype value validation failure
- add codemaid config
- format with codemaid
- adding collect() reuse logic for dll use
-- move no progress timer to start / stop in collect()  
-- move Kusto and Log Analytics to 'instance' class  
-- change methods to private that are not needed publicly  
- adding collect() retry mitigation for csv compliance issue  
-- modify kusto ingest lists for public access for retry  
-- default KustoUseBlobAsSource to true  
- add additional vscode launch configurations  

## 12/4/2020  https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.7.2012.04200700

- modify repo layout structure and place source in /src  
- add .devcontainer with net5 custom install for codespaces  
- add kusto functions  
- fix table ingest only ingesting last chunk enumerated from table  
- fix unique table ingest when kustoingestmessage false using .set-or-replace  
- fix table filter now using containerfilter  
- migrate from binaryformatter deprecated in net5 to newtonsoft json serializer  
- fix unique when kustoingestmessage false and file is over max ingest size  
- update loganalytics api version  
- add tests for msal and kusto  
- update README  
- update configuration json examples  
- add building md  
- add CHANGELOG  

## 11/10/2020 https://github.com/microsoft/CollectServiceFabricData/tree/v2.7.2011.10013644  

- add labeler workflow back after github cross fork pr fix  
- migrate from nuget to dotnet build for workflow  
- add vscode launch.json config  
- add support for dll and exe. converted project to dll and created new exe project referencing dll project.  
- clean app.config  
- migrate to csproj nuget package from packages.config  
- clean csproj
- modify nuspec layout to support dll and exe  
- migrate from .net462 to .net472 for security and stability  
- add support for .netcoreapp3.1  
- modify static inherited 'instance' class to singleton  
- migrate from deprecated ADAL to MSAL. tested client, user, and device on windows and ubuntu  
- move token cache to userprofile  
- add object, counter, instance fields for gather type counter  
- fix temp directory saveconfig in json output  
- fix custom task scheduler thread race causing duplicate managers
- migrate from logdebug bool to logdebug int levels 0-5 to turn down logging  
- update test project to .netcore to remove security vulnerabilities  
- add kusto functions  
- add build scripts in /scripts dir  

## 7/30/2020 https://github.com/microsoft/CollectServiceFabricData/tree/v2.6.7516.26434  

- fix nuget package layout

## 7/23/2020 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.6.7509.24815  

- remove broken labeler workflow  
- add nuget package to workflow  
- set source code namespaces  
- remove static members from httpclient  
- add tests  
- add kusto functions  

## 4/22/2020 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.6.7417.39096

- add collectsfdata.options.json to release
- add discovered blob time range to output
- add sas validation
- add noprogresstimeout min to prevent indefinite hang  
- add online version check  
- add maxstreamtransmitbytes  
- add last message list  
- add determineclusterid fallback  
- add kustoUseIngestMessage  
- add admin and query clients to kusto endpoint
- add test classes  
- add kusto functions  
- default kustoCompressed to true  
- update config schema  

## 12/4/2019 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.6.7277.26289

- modify git actions for v1 and update tag
- modify blob callback to include length
- modify displaystatus summary
- add config validation functions
- add gathertype exception
- add / modify kusto functions

## 11/21/2019 init 2.6 public release https://github.com/microsoft/CollectServiceFabricData/tree/CollectServiceFabricData-c75b19fef60739aebb3707fef556c768bc1432c5

- .net462 console utility  
