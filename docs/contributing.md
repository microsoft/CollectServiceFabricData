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
help .\dotnet-build.ps1

dotnet-build.ps1 [[-targetFrameworks] <string[]>] [[-configuration] <Object>] [[-runtimeIdentifier] <Object>] [[-projectDir] <string>] [[-nugetFallbackFolder] <string>] [-publish] [-clean] [-replace]
```

#### **example**

to build all configured .net versions debug and release:

```powershell
.\dotnet-build.ps1
```

to build a specific configuration:

```powershell
.\dotnet-build.ps1 -targetFrameworks net462 -configuration debug -runtimeIdentifier win-x64
```

## CollectSFDataTest

Visual Studio Code with .netcoreapp3.1 / net5 and powershell 7.0+
or
Visual Studio 2019 with .netcoreapp3.1 / net5 and powershell 7.0+

### App registration (user) setup

azure application registration ids are required for certain tests.  
msal confidentialclient does not require replyurls.  
**NOTE: msal confidentialclient does require that api permissions be set on app registration in portal after creation**

#### **powershell test script app registration id setup**

the following script will create an app registration using certificate logon for confidentialclient msal .net core authentication.
This client is used for creating resources in azure for testing collectsfdata.

```powershell
.\scripts\azure-az-create-aad-application-spn.ps1 `
  -aadDisplayName collectsfdatatestclient `
  -uri http://collectsfdatatestclient `
  -logontype cert `
  -password {{cert password}}

...
application id: 59c41f0c-fb6c-43e7-a070-480e2af83838
tenant id: 1a4b5850-4150-4da6-9d0e-4cfcc078292b
application identifier Uri: http://collectsfdatatestclient
cert and key base64: MIIDHjCCAgagAwIBAgIQFgKT81w9vapAjxN...
thumbprint: C124CE6208B0547CB576019104FDDF97B01A37A8
pfx path: C:\Users\user\AppData\Local\Temp\collectsfdatatestclient.pfx
clientid / applicationid saved in $global:applicationId
clientsecret / base64 thumb saved in $global:clientSecret

```

#### **powershell collectsfdata app registration id setup**

the following script will create an app registration using certificate logon for confidentialclient msal collectsfdata testing.
this client is used for testing azure client authentication in collectsfdata.

```powershell
.\scripts\azure-az-create-aad-application-spn.ps1 `
  -aadDisplayName collectsfdataapp `
  -uri http://collectsfdataapp `
  -logontype cert `
  -password {{cert password}}

...
application id: 14b3dd02-66ec-46b4-b7aa-b65abc9bbb4d
tenant id: 1a4b5850-4150-4da6-9d0e-4cfcc078292b
application identifier Uri: http://collectsfdata
cert and key base64: MIIDEjCCAfqgAwIBAgIQPnmXz4qmKIpHlu...
thumbprint: 8C1AD1A0DBA04F78F7EE86FBDBC6E9CF06DB79E3
pfx path: C:\Users\user\AppData\Local\Temp\collectsfdataapp.pfx
clientid / applicationid saved in $global:applicationId
clientsecret / base64 thumb saved in $global:clientSecret

```

### Environment Setup

- .\scripts\setup-test-env.ps1

```powershell
.\scripts\setup-test-env.ps1 creates the following configuration file: $env:LocalAppData\collectsfdata\collectSfDataTestProperties.json
```

using output from .\scripts\azure-az-create-aad-application-spn.ps1, enter:
- AzureClientId = 'application id'
- AzureClientCertificate = 'clientsecret'
- AzureTenantId = 'tenant id'

example using values from above:

```json
{
  "testAzClientId": "59c41f0c-fb6c-43e7-a070-480e2af83838",
  "testAzClientCertificate": "MIIDHjCCAgagAwIBAgIQFgKT81w9vapAjxN...",
  "testAzStorageAccount": "collectsfdatatests",
  "adminUserName": null,
  "adminPassword": null,
  "AzureClientId": "14b3dd02-66ec-46b4-b7aa-b65abc9bbb4d",
  "AzureClientCertificate": "MIIDEjCCAfqgAwIBAgIQPnmXz4qmKIpHlu...",
  "AzureClientSecret": "{{private key}}",
  "AzureResourceGroup": "collectsfdataunittest",
  "AzureResourceGroupLocation": "eastus",
  "AzureSubscriptionId": "a79c1c37-7eba-4378-8000-abd3a23e66d8",
  "AzureTenantId": "1a4b5850-4150-4da6-9d0e-4cfcc078292b",
  "KustoCluster": "https://ingest-sfcluster.eastus.kusto.windows.net/sfdatabase",
  "SasKey": "{{sas key}}"
}
```

clean example:

```json
{
  "testAzClientId": "{{test client app registration}}",
  "testAzClientCertificate": "{{test client base64 certificate}}",
  "testAzStorageAccount": "{{test storage account name}}",
  "adminUserName": null,
  "adminPassword": null,
  "AzureClientId": "{{collectsfdata app registration}}",
  "AzureClientCertificate": "{{collectsfdata client base64 certificate}}",
  "AzureClientSecret": "{{private key}}",
  "AzureResourceGroup": "{{azure resource group name}}",
  "AzureResourceGroupLocation": "{{azure resource group location}}",
  "AzureSubscriptionId": "{{azure subscription id}}",
  "AzureTenantId": "{{azure tenant id}}",
  "KustoCluster": "{{kusto ingest url and database}}",
  "SasKey": "{{sas key}}"
}
```

## Kusto Setup

to create a test kusto cluster for testing, the below script can be used. Pass resource group location and application registration id (azureclientid) to script.  
script will create dev kusto cluster and database. Script will add app registration id as a database admin.  
output will display 'kustocluster' kusto ingest url with database.

.\scripts\azure-az-create-kusto-cluster.ps1 -resourceGroupLocation {{location}} -appRegistrationId {{app registration id for 'collectsfdata'}}

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