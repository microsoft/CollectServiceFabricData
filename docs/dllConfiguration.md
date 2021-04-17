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

Once configuration options have been set, call Collector.Collect(). 
Collect can optionally be passed ConfigurationsOptions for queueing multiple configurations to collect.
Use Clone() to create a shallow copy of existing configuration.

See examples below on how to use:

### Example

```c#
private static int Main(string[] args)
{
        Collector collector = new Collector(args, true);
        ConfigurationOptions config = collector.Config;

        config.GatherType = FileTypesEnum.counter.ToString();
        config.UseMemoryStream = true;
        config.KustoCluster = "https://ingest-sfcluster.kusto.windows.net/sfdatabase";
        config.KustoTable = "sfclusterlogs";
        config.KustoRecreateTable = true;
        config.LogDebug = 5;
        config.LogFile = "c:\\temp\\csfd.3.log";
        //config.Validate();

        return collector.Collect();
}
```

### Example

```c#
private static int Main(string[] args)
{
        Collector collector = new Collector(args, true);
        ConfigurationOptions config = collector.Config.Clone();

        config.GatherType = FileTypesEnum.counter.ToString();
        config.UseMemoryStream = true;
        config.KustoCluster = "https://ingest-sfcluster.kusto.windows.net/sfdatabase";
        config.KustoTable = "sfclusterlogs";
        config.KustoRecreateTable = true;
        config.LogDebug = 5;
        config.LogFile = "c:\\temp\\csfd.3.log";
        //config.Validate();

        return collector.Collect(config);

}
```


## Logging

Externally there is logging both to console output and optionally to a log file. When using as a DLL, subscribing to event 'Log_MessageLogged' will provide the same information in 'LogMessage' object format. See examples above and below.

### Example


LogMessage Callback
```c#
Log.MessageLogged += Log_MessageLogged;

private static void Log_MessageLogged(object sender, LogMessage args)
{
    Console.WriteLine(args.Message);
}
```

LogMessage Class
```c#
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

    // mitigation for dtr files not being csv compliant causing kusto ingest to fail
    if ((collector.Instance.Kusto.IngestFileObjectsFailed.Count() > 0
        | collector.Instance.Kusto.IngestFileObjectsPending.Count() > 0)
        && config.IsKustoConfigured()
        && config.KustoUseBlobAsSource == true
        && config.FileType == DataFile.FileTypesEnum.trace)
    {
        KustoConnection kusto = collector.Instance.Kusto;
        Log.Warning("failed ingests due to csv compliance. restarting.");

        // change config to download files to parse and fix csv fields
        config.KustoUseBlobAsSource = false;
        config.KustoRecreateTable = false;

        List<string> ingestList = kusto.IngestFileObjectsFailed.Select(x => x.FileUri).ToList();
        ingestList.AddRange(kusto.IngestFileObjectsPending.Select(x => x.FileUri));
        config.FileUris = ingestList.ToArray();

        retval = collector.Collect();
    }

    return retval;
}
```

## Reference 

### NuGet Package Layout

The current layout of nuget package looks similar to below. 
Both .net framework and .net core versions are provided in both exe and dll format. 
The exe's are located in the 'tools' directory and include all the required dependent dll's for direct use from command line.
Binaries are signed.

```text
C:.
|   Microsoft.ServiceFabric.CollectSFData.nuspec
|   [Content_Types].xml
|
+---images
|       FabricSupport.png
|
+---lib
|   +---net462
|   |       CollectSFDataDll.dll
|   |       Sf.Tx.Core.dll
|   |       Sf.Tx.Windows.dll
|   |
|   +---net472
|   |       CollectSFDataDll.dll
|   |       Sf.Tx.Core.dll
|   |       Sf.Tx.Windows.dll
|   |
|   \---netcoreapp3.1
|           CollectSFDataDll.dll
|           Sf.Tx.Core.dll
|           Sf.Tx.Windows.dll
|
+---package
|   \---services
|       \---metadata
|           \---core-properties
|                   59d18c1edf7540bd9f273bd2344bed15.psmdcp
|
+---tools
|   +---net462
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
|   |       Microsoft.Win32.Primitives.dll
|   |       Microsoft.WindowsAzure.Storage.dll
|   |       netstandard.dll
|   |       Newtonsoft.Json.dll
|   |       Sf.Tx.Core.dll
|   |       Sf.Tx.Windows.dll
|   |       System.AppContext.dll
|   |       System.CodeDom.dll
|   |       System.Collections.Concurrent.dll
|   |       System.Collections.dll
|   |       System.Collections.Immutable.dll
|   |       System.Collections.NonGeneric.dll
|   |       System.Collections.Specialized.dll
|   |       System.ComponentModel.dll
|   |       System.ComponentModel.EventBasedAsync.dll
|   |       System.ComponentModel.Primitives.dll
|   |       System.ComponentModel.TypeConverter.dll
|   |       System.Console.dll
|   |       System.Data.Common.dll
|   |       System.Data.SqlClient.dll
|   |       System.Diagnostics.Contracts.dll
|   |       System.Diagnostics.Debug.dll
|   |       System.Diagnostics.FileVersionInfo.dll
|   |       System.Diagnostics.Process.dll
|   |       System.Diagnostics.StackTrace.dll
|   |       System.Diagnostics.TextWriterTraceListener.dll
|   |       System.Diagnostics.Tools.dll
|   |       System.Diagnostics.TraceSource.dll
|   |       System.Diagnostics.Tracing.dll
|   |       System.Drawing.Primitives.dll
|   |       System.Dynamic.Runtime.dll
|   |       System.Globalization.Calendars.dll
|   |       System.Globalization.dll
|   |       System.Globalization.Extensions.dll
|   |       System.IO.Compression.dll
|   |       System.IO.Compression.ZipFile.dll
|   |       System.IO.dll
|   |       System.IO.FileSystem.dll
|   |       System.IO.FileSystem.DriveInfo.dll
|   |       System.IO.FileSystem.Primitives.dll
|   |       System.IO.FileSystem.Watcher.dll
|   |       System.IO.IsolatedStorage.dll
|   |       System.IO.MemoryMappedFiles.dll
|   |       System.IO.Pipes.dll
|   |       System.IO.UnmanagedMemoryStream.dll
|   |       System.Linq.dll
|   |       System.Linq.Expressions.dll
|   |       System.Linq.Parallel.dll
|   |       System.Linq.Queryable.dll
|   |       System.Net.Http.dll
|   |       System.Net.NameResolution.dll
|   |       System.Net.NetworkInformation.dll
|   |       System.Net.Ping.dll
|   |       System.Net.Primitives.dll
|   |       System.Net.Requests.dll
|   |       System.Net.Security.dll
|   |       System.Net.Sockets.dll
|   |       System.Net.WebHeaderCollection.dll
|   |       System.Net.WebSockets.Client.dll
|   |       System.Net.WebSockets.dll
|   |       System.ObjectModel.dll
|   |       System.Reactive.dll
|   |       System.Reactive.Linq.dll
|   |       System.Reflection.dll
|   |       System.Reflection.Extensions.dll
|   |       System.Reflection.Primitives.dll
|   |       System.Resources.Reader.dll
|   |       System.Resources.ResourceManager.dll
|   |       System.Resources.Writer.dll
|   |       System.Runtime.CompilerServices.VisualC.dll
|   |       System.Runtime.dll
|   |       System.Runtime.Extensions.dll
|   |       System.Runtime.Handles.dll
|   |       System.Runtime.InteropServices.dll
|   |       System.Runtime.InteropServices.RuntimeInformation.dll
|   |       System.Runtime.Numerics.dll
|   |       System.Runtime.Serialization.Formatters.dll
|   |       System.Runtime.Serialization.Json.dll
|   |       System.Runtime.Serialization.Primitives.dll
|   |       System.Runtime.Serialization.Xml.dll
|   |       System.Security.Claims.dll
|   |       System.Security.Cryptography.Algorithms.dll
|   |       System.Security.Cryptography.Cng.dll
|   |       System.Security.Cryptography.Csp.dll
|   |       System.Security.Cryptography.Encoding.dll
|   |       System.Security.Cryptography.Primitives.dll
|   |       System.Security.Cryptography.ProtectedData.dll
|   |       System.Security.Cryptography.X509Certificates.dll
|   |       System.Security.Principal.dll
|   |       System.Security.Principal.Windows.dll
|   |       System.Security.SecureString.dll
|   |       System.Text.Encoding.dll
|   |       System.Text.Encoding.Extensions.dll
|   |       System.Text.RegularExpressions.dll
|   |       System.Threading.dll
|   |       System.Threading.Overlapped.dll
|   |       System.Threading.Tasks.dll
|   |       System.Threading.Tasks.Parallel.dll
|   |       System.Threading.Thread.dll
|   |       System.Threading.ThreadPool.dll
|   |       System.Threading.Timer.dll
|   |       System.ValueTuple.dll
|   |       System.Xml.ReaderWriter.dll
|   |       System.Xml.XDocument.dll
|   |       System.Xml.XmlDocument.dll
|   |       System.Xml.XmlSerializer.dll
|   |       System.Xml.XPath.dll
|   |       System.Xml.XPath.XDocument.dll
|   |
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
|   |       Sf.Tx.Core.dll
|   |       Sf.Tx.Windows.dll
|   |       System.CodeDom.dll
|   |       System.Collections.Immutable.dll
|   |       System.Data.SqlClient.dll
|   |       System.Reactive.dll
|   |       System.Reactive.Linq.dll
|   |       System.Security.Cryptography.Cng.dll
|   |       System.Security.Cryptography.ProtectedData.dll
|   |       System.Security.Principal.Windows.dll
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
|           Sf.Tx.Core.dll
|           Sf.Tx.Windows.dll
|           System.CodeDom.dll
|           System.Data.SqlClient.dll
|           System.Reactive.dll
|           System.Reactive.Linq.dll
|           System.Security.Cryptography.ProtectedData.dll
|
\---_rels
        .rels
```
