# Contribute to CollectServiceFabricData

## Submitting issues

To report issue with collectsfdata, create a new issue in github repo [new issue](https://github.com/microsoft/CollectServiceFabricData/issues/new/choose)

## Building

Visual Studio 2019 with .net 4.7.2 for CollectSFData
Visual Studio 2019 with .netcoreapp3.1 and powershell 7.0 for CollectSFDataTest

## Setup

there is currently a bug with powershell core and azure authentication cmdlets using Cng cryptography.
.\setup-test-env.ps1 will not authenticate to azure properly using spn credentials until the connect-azaccount module is updated. until then, running of script outside of visual studio to set configuration settings in test configuration file may be required.

### App registration (user) setup

azure application registration ids are required for certain tests.  
msal confidentialclient does not require replyurls.  
**NOTE: msal confidentialclient does require that api permissions be set on app registration in portal after creation**

#### **powershell test script app registration id setup**

the following script will create an app registration using certificate logon for confidentialclient msal .net core authentication

- .\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdatatestclient -uri http://collectsfdatatestclient -logontype cert

#### **collectsfdata azureclientid and azureclientsecret setup**

the following script will create an app registration using certificate thumbprint logon for confidentialclient msal .net authentication

- .\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdata -uri http://collectsfdata -logontype certthumb

### Environment Setup

- .\setup-test-env.ps1

.\setup-test-env.ps1 creates the following configuration file: $env:LocalAppData\collectsfdata\collectSfDataTestProperties.json

using output from .\azure-az-create-aad-application-spn.ps1, enter:
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

.\azure-az-create-kusto-cluster.ps1 -resourceGroupLocation {{ location }} -appRegistrationId {{ app registration id for 'collectsfdata' }}

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