# CollectServiceFabricData DLL Configuration and Usage

## Outline

[Overview](#overview)  
[Design](#design)  
[Supported Configurations](#supported-configurations)  
[Adding NuGet package to project](#adding-nuget-package-to-project)  
[Implementing Collector](#implementing-collector)  
[Reuse](#reuse)  
[Logging](#logging)  
[Troubleshooting](#troubleshooting)  
[Current Issues](#current-issues)  

## Overview

CollectSFData can be used as an exe or as a dll from Microsoft signed nuget package [Microsoft.ServiceFabric.CollectSFData](https://www.nuget.org/packages/Microsoft.ServiceFabric.CollectSFData/). To use as an exe, see [configuration](./configuration.md).

## Design

CollectSFData is a high performance multi-threaded binary with a custom task scheduler. The 'Instance' state class is a singleton and therefore Collector class should be reusable by calling Collect() multiple times, but only one instance of Collector should be used concurrently.  

## Supported Configurations

The below configurations are currently supported.

### .Net Framework

#### Windows

.Net Framework 4.7.2+

### .Net Core

#### Windows

.Net Core 3.1+
.Net 5.0+

#### Windows Container

Supports GatherType 'counter' performance counter logs with beta option 'UseTx' == true.

.Net Core 3.1+
.Net 5.0+

#### Linux

Does not support GatherType 'counter' performance counter logs.

.Net Core 3.1+
.Net 5.0+

## Adding NuGet package to project

From command line, to add Microsoft.ServiceFabric.CollectSFData nuget package, navigate to download on nuget.org and use one of the provided commands [Microsoft.ServiceFabric.CollectSFData](https://www.nuget.org/packages/Microsoft.ServiceFabric.CollectSFData/).  

In Visual Studio, use 'NuGet Package Manager' to install package.

## Kusto Setup

### Creating Kusto Cluster

### Headless Execution with Client Credentials

Use these steps to optionally configure CollectSFData to run headless with client credentials and secret. 

#### Configuration of Azure Active Directory App Registration

#### Configuration of Secret

## Implementing Collector

After CollectSFData nuget package has been added to project, use the following information to implement. The main classes are 'Collector' for execution and 'ConfigurationOptions' for configuration.

### Setting Configuration

Minimum configuration has to be set before calling Collector.Collect(). The main configuration is the type of data to collect with configuration option 'GatherType'. Configuration can be set by command line arguments, configuration file, or by using ConfigurationOptions class before calling Collector.Collect(). See [configuration](./configuration.md).

### Calling Collector.Collect()

Once configuration options have been set, call Collector.Collect(). Collect can optionally be passed list of Uris that need to be ingested. See example below on how to use.

### Example

```c#
private static int Main(string[] args)
{
        Collector collector = new Collector(args, true);
        ConfigurationOptions config = collector.Instance.Config;

        Log.MessageLogged += Log_MessageLogged;

        config.GatherType = FileTypesEnum.counter.ToString();
        config.UseMemoryStream = true;
        config.KustoCluster = "https://ingest-sfcluster.kusto.windows.net/sfdatabase";
        config.KustoTable = "sfclusterlogs";
        config.KustoRecreateTable = true;
        config.LogDebug = 5;
        config.LogFile = "c:\\temp\\csfd.3.log";
        //config.Validate();

        return collector.Collect();

        /*
        return collector.Collect(new List<string> 
        {
                "C:\\temp\\test\\counter\\DataCollector01.blg"
        });
        */
}
```

## Logging

Externally there is logging both to console output and optionally to a log file. When using as a DLL, subscribing to event 'Log_MessageLogged' will provide the same information in 'LogMessage' object format. See examples above and below.

### Example


```c#
private static void Log_MessageLogged(object sender, LogMessage args)
{
    Console.WriteLine(args.Message);
}

public class LogMessage : EventArgs
{
    public ConsoleColor? BackgroundColor {get; set;}
    public ConsoleColor? ForegroundColor {get; set;}
    public bool IsError {get; set;}
    public bool LogFileOnly {get; set;}
    public string Message {get; set;}
    public string TimeStamp {get; set;}
}
```

## Troubleshooting

Use log file and 'LogDebug' values (4-5) to troubleshoot execution of library. In logging, check for messages starting with 'warning', 'error', or 'exception'.  

When starting execution from Collect(), current configuration is first validated and is a common failure point especially with values 'GatherType' and 'Saskey'. Traces starting with 'Validate*' delineate validation of current configuration.  

## Current Issues

### CSV log file compliance for GatherType trace

Certain events in the Service Fabric detailed diagnostic logs gathered when 'GatherType' is set to 'trace' are not CSV compliant and can fail ingestion into Kusto. Current mitigation until these traces are properly formatted is to either set 'UseKustoBlobAsSource' == false which is remarkably slow and more resource intensive. Another option is to do two collections with Collect() as shown in the following example assuming there will be a small number of failures during first collect. This is how CollectSFData currently executes when executing as an exe. See [Program.cs](..\src\CollectSFData\Program.cs).  

```c#
private static int Main(string[] args)
{
    Collector collector = new Collector(args, true);
    ConfigurationOptions config = collector.Instance.Config;

    int retval = collector.Collect();

    // mitigation for dtr files not being csv compliant
    // causing kusto ingest to fail
    if (collector.Instance.Kusto.IngestFileObjectsFailed.Count() > 0
        && config.IsKustoConfigured()
        && config.KustoUseBlobAsSource == true
        && config.FileType == DataFile.FileTypesEnum.trace)
    {
    KustoConnection kusto = collector.Instance.Kusto;
    Log.Warning("failed ingests due to csv compliance. restarting.");

    // change config to download files to parse and fix csv fields
    config.KustoUseBlobAsSource = false;
    config.KustoRecreateTable = false;
    retval = collector.Collect(kusto.IngestFileObjectsFailed.Select(x => x.FileUri).ToList());
    }

    return retval;
}
```

## Reference 

### NuGet Package Layout

The current layout of nuget package looks similar to below. Both .net framework and .net core versions are provided in both exe and dll format. The exe's are located in the 'tools' directory and include all the required dependent dll's for direct use from command line.

```text
\MICROSOFT.SERVICEFABRIC.COLLECTSFDATA.2.8.2102.1080317.NUPKG
|   Microsoft.ServiceFabric.CollectSFData.nuspec
|   [Content_Types].xml
|
+---images
|       FabricSupport.png
|
+---lib
|   +---net472
|   |       CollectSFDataDll.dll
|   |       Tx.Core.dll
|   |       Tx.Windows.dll
|   |
|   \---netcoreapp3.1
|           CollectSFDataDll.dll
|           Tx.Core.dll
|           Tx.Windows.dll
|
+---package
|   \---services
|       \---metadata
|           \---core-properties
|                   de55b4d8f8f74dbe9582305e2fc0996e.psmdcp
|
+---tools
|   +---net472
|   |       CollectSFData.exe
|   |       CollectSFData.exe.config
|   |       collectsfdata.options.json
|   |       CollectSFDataDll.dll
|   |       Kusto.Cloud.Platform.Azure.dll
|   |       Kusto.Cloud.Platform.dll
|   |       Kusto.Data.dll
|   |       Kusto.Ingest.dll
|   |       Microsoft.Azure.KeyVault.Core.dll
|   |       Microsoft.Azure.KeyVault.dll
|   |       Microsoft.Azure.KeyVault.WebKey.dll
|   |       Microsoft.Azure.Storage.Blob.dll
|   |       Microsoft.Azure.Storage.Common.dll
|   |       Microsoft.Azure.Storage.Queue.dll
|   |       Microsoft.Extensions.CommandLineUtils.dll
|   |       Microsoft.Identity.Client.dll
|   |       Microsoft.Identity.Client.Extensions.Msal.dll
|   |       Microsoft.IdentityModel.Clients.ActiveDirectory.dll
|   |       Microsoft.IO.RecyclableMemoryStream.dll
|   |       Microsoft.Rest.ClientRuntime.Azure.dll
|   |       Microsoft.Rest.ClientRuntime.dll
|   |       Microsoft.WindowsAzure.Storage.dll
|   |       Newtonsoft.Json.dll
|   |       System.CodeDom.dll
|   |       System.Collections.Immutable.dll
|   |       System.Data.SqlClient.dll
|   |       System.Reactive.dll
|   |       System.Reactive.Linq.dll
|   |       System.Security.Cryptography.Cng.dll
|   |       System.Security.Cryptography.ProtectedData.dll
|   |       System.Security.Principal.Windows.dll
|   |       Tx.Core.dll
|   |       Tx.Windows.dll
|   |
|   \---netcoreapp3.1
|           CollectSFData.dll
|           CollectSFData.dll.config
|           CollectSFData.exe
|           collectsfdata.options.json
|           CollectSFData.runtimeconfig.json
|           CollectSFDataDll.dll
|           Kusto.Cloud.Platform.Azure.dll
|           Kusto.Cloud.Platform.dll
|           Kusto.Data.dll
|           Kusto.Ingest.dll
|           Microsoft.Azure.KeyVault.Core.dll
|           Microsoft.Azure.KeyVault.dll
|           Microsoft.Azure.KeyVault.WebKey.dll
|           Microsoft.Azure.Storage.Blob.dll
|           Microsoft.Azure.Storage.Common.dll
|           Microsoft.Azure.Storage.Queue.dll
|           Microsoft.Extensions.CommandLineUtils.dll
|           Microsoft.Identity.Client.dll
|           Microsoft.Identity.Client.Extensions.Msal.dll
|           Microsoft.IdentityModel.Clients.ActiveDirectory.dll
|           Microsoft.IO.RecyclableMemoryStream.dll
|           Microsoft.Rest.ClientRuntime.Azure.dll
|           Microsoft.Rest.ClientRuntime.dll
|           Microsoft.WindowsAzure.Storage.dll
|           Newtonsoft.Json.dll
|           System.CodeDom.dll
|           System.Data.SqlClient.dll
|           System.Reactive.dll
|           System.Reactive.Linq.dll
|           System.Security.Cryptography.ProtectedData.dll
|           Tx.Core.dll
|           Tx.Windows.dll
|
\---_rels
        .rels
```
