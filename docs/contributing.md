# Contribute to CollectServiceFabricData

## Submitting issues

To report issue with collectsfdata, create a new issue in github repo [new issue](https://github.com/microsoft/CollectServiceFabricData/issues/new/choose)

## Building

### CollectSFData / CollectSFDataDll

Visual Studio 2019 with .net 4.7.2 / .netcoreapp3.1 / net5  
or  
Visual Studio Code with .net 4.7.2 / .netcoreapp3.1 / net5  

### **launch.json**

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "PowerShell Launch Current File",
            "type": "PowerShell",
            "request": "launch",
            "script": "${file}",
            "cwd": "${file}"
        },
        {
            "name": ".NET Core Launch (console)",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/src/bin/Debug/netcoreapp3.1/CollectSFData.dll",
            "args": [],
            "cwd": "${workspaceFolder}/src/bin/Debug/netcoreapp3.1",
            "console": "internalConsole",
            "stopAtEntry": false
        },
        {
            "name": ".NET Core Attach",
            "type": "coreclr",
            "request": "attach",
            "processId": "${command:pickProcess}"
        }
    ]
}
```

### commandline

```powershell
.\dotnet-build.ps1 [[-targetFramework] <string[]>] [[-configuration] <Object>] [[-runtimeIdentifier] <Object>] [[-projectDir] <string>] [[-nugetFallbackFolder] <string>] [-publish] [-clean]
```

#### **example**

```powershell
PS C:\github\jagilber\CollectServiceFabricData\scripts> .\dotnet-build.ps1
current frameworks: netcoreapp3.1;net472
copying and adding target framework to csproj net5
saving to C:\github\jagilber\CollectServiceFabricData\src\CollectSFData\CollectSFData.temp.csproj
current frameworks: netcoreapp3.1;net472
copying and adding target framework to csproj net5
saving to C:\github\jagilber\CollectServiceFabricData\src\CollectSFDataDll\CollectSFDataDll.temp.csproj
dotnet restore C:\github\jagilber\CollectServiceFabricData\src\CollectSFData\CollectSFData.temp.csproj
  Determining projects to restore...
  Restored C:\github\jagilber\CollectServiceFabricData\src\CollectSFData\CollectSFData.temp.csproj (in 560 ms).
  Restored C:\github\jagilber\CollectServiceFabricData\src\CollectSFDataDll\CollectSFDataDll.csproj (in 563 ms).
dotnet build C:\github\jagilber\CollectServiceFabricData\src\CollectSFData\CollectSFData.temp.csproj -c debug
Microsoft (R) Build Engine version 16.8.0-preview-20451-02+51a1071f8 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  Restored C:\github\jagilber\CollectServiceFabricData\src\CollectSFData\CollectSFData.temp.csproj (in 537 ms).
  Restored C:\github\jagilber\CollectServiceFabricData\src\CollectSFDataDll\CollectSFDataDll.csproj (in 538 ms).
  CollectSFDataDll -> C:\github\jagilber\CollectServiceFabricData\src\bin\debug\netcoreapp3.1\CollectSFDataDll.dll
  CollectSFData.temp -> C:\github\jagilber\CollectServiceFabricData\src\bin\debug\net5\CollectSFData.dll
  Output written to C:\github\jagilber\CollectServiceFabricData\src\bin\debug\net5\
  Successfully created package 'C:\github\jagilber\CollectServiceFabricData\src\bin\debug\Microsoft.ServiceFabric.CollectSFData.2.7.2012.4151456.nupkg'.

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.81
nuget add C:\github\jagilber\CollectServiceFabricData\src\bin\debug\Microsoft.ServiceFabric.CollectSFData.2.7.2012.4151456.nupkg -source C:\Users\jagilber\.dotnet\NuGetFallbackFolder
Installing Microsoft.ServiceFabric.CollectSFData 2.7.2012.4151456.
Successfully added package 'C:\github\jagilber\CollectServiceFabricData\src\bin\debug\Microsoft.ServiceFabric.CollectSFData.2.7.2012.4151456.nupkg' to feed 'C:\Users\jagilber\.dotnet\NuGetFallbackFolder'.
dotnet build C:\github\jagilber\CollectServiceFabricData\src\CollectSFData\CollectSFData.temp.csproj -c release
Microsoft (R) Build Engine version 16.8.0-preview-20451-02+51a1071f8 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  Restored C:\github\jagilber\CollectServiceFabricData\src\CollectSFData\CollectSFData.temp.csproj (in 546 ms).
  Restored C:\github\jagilber\CollectServiceFabricData\src\CollectSFDataDll\CollectSFDataDll.csproj (in 547 ms).
  CollectSFDataDll -> C:\github\jagilber\CollectServiceFabricData\src\bin\release\netcoreapp3.1\CollectSFDataDll.dll
  CollectSFData.temp -> C:\github\jagilber\CollectServiceFabricData\src\bin\release\net5\CollectSFData.dll
  Output written to C:\github\jagilber\CollectServiceFabricData\src\bin\release\net5\
  Successfully created package 'C:\github\jagilber\CollectServiceFabricData\src\bin\release\Microsoft.ServiceFabric.CollectSFData.2.7.2012.4151459.nupkg'.

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.73
nuget add C:\github\jagilber\CollectServiceFabricData\src\bin\release\Microsoft.ServiceFabric.CollectSFData.2.7.2012.4151459.nupkg -source C:\Users\jagilber\.dotnet\NuGetFallbackFolder
Installing Microsoft.ServiceFabric.CollectSFData 2.7.2012.4151459.
Successfully added package 'C:\github\jagilber\CollectServiceFabricData\src\bin\release\Microsoft.ServiceFabric.CollectSFData.2.7.2012.4151459.nupkg' to feed 'C:\Users\jagilber\.dotnet\NuGetFallbackFolder'.
removing temp file C:\github\jagilber\CollectServiceFabricData\src\CollectSFData\CollectSFData.temp.csproj
removing temp file C:\github\jagilber\CollectServiceFabricData\src\CollectSFDataDll\CollectSFDataDll.temp.csproj
```

### CollectSFDataTest

Visual Studio Code with .netcoreapp3.1 / net5 and powershell 7.0+
or
Visual Studio 2019 with .netcoreapp3.1 / net5 and powershell 7.0+

## Setup

there is currently a bug with powershell core and azure authentication cmdlets using Cng cryptography.
.\scripts\setup-test-env.ps1 will not authenticate to azure properly using spn credentials until the connect-azaccount module is updated. until then, running of script outside of visual studio to set configuration settings in test configuration file may be required.

### App registration (user) setup

azure application registration ids are required for certain tests.  
msal confidentialclient does not require replyurls.  
**NOTE: msal confidentialclient does require that api permissions be set on app registration in portal after creation**

#### **powershell test script app registration id setup**

the following script will create an app registration using certificate logon for confidentialclient msal .net core authentication

- .\scripts\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdatatestclient -uri http://collectsfdatatestclient -logontype cert

#### **collectsfdata azureclientid and azureclientsecret setup**

the following script will create an app registration using certificate thumbprint logon for confidentialclient msal .net authentication

- .\scripts\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdata -uri http://collectsfdata -logontype certthumb

### Environment Setup

- .\scripts\setup-test-env.ps1

.\scripts\setup-test-env.ps1 creates the following configuration file: $env:LocalAppData\collectsfdata\collectSfDataTestProperties.json

using output from .\scripts\azure-az-create-aad-application-spn.ps1, enter:
- AzureClientId
- AzureClientSecret
- AzureTenantId

```json
{
  "testAzClientId": "",
  "testAzClientSecret":"", // thumbprint for .net core test project
  "testAzStorageAccount": "collectsfdatatests",
  "adminUserName": null,
  "adminPassword": null,
  "AzureClientId": "{{azure client id}}",
  "AzureClientSecret": "{{azure client secret}}",
  "AzureResourceGroup": "collectsfdataunittest",
  "AzureResourceGroupLocation": "{{azure resource group location}}",
  "AzureSubscriptionId": "{{azure subscription id}}",
  "AzureTenantId": "{{azure tenant id}}",
  "KustoCluster": "{{ kusto cluster ingest url }}",
  "SasKey": "{{sas key}}"
}
```

## Kusto Setup

to create a test kusto cluster for testing, the below script can be used. Pass resource group location and application registration id (azureclientid) to script.  
script will create dev kusto cluster and database. Script will add app registration id as a database admin.  
output will display 'kustocluster' kusto ingest url with database.

.\scripts\azure-az-create-kusto-cluster.ps1 -resourceGroupLocation {{ location }} -appRegistrationId {{ app registration id for 'collectsfdata' }}

example output:

```text
finished. object stored in `$global:cluster
data ingestion uri: https://ingest-collectsfdataunittest.eastus.kusto.windows.net/collectsfdatadb
```

## Testing

todo

## Creating pull requests

Fork repo and create pull request as normal across forks.
Labeling will be applied by git actions.
If executable changes, git actions will build.

## Creating Releases

To create a release, merge into master, and add 'Pre-release' or 'Release' label before merge.
Release will build signed exe and nuget package, and publish release to repo.