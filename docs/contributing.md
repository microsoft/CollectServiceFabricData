# [DRAFT] To contribute to CollectServiceFabricData

## Submitting issues

## Building

Visual Studio 2019 with .net 4.6.2 for CollectSFData
Visual Studio 2019 with .netcoreapp3.1 and powershell 7.0 for CollectSFDataTest

## Testing

To setup environment run:
- .\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdatatestclient -uri http://collectsfdatatestclient -logontype cert
- .\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdata -uri http://collectsfdata -logontype certthumb
- .\setup-test-env.ps1

there is currently a bug with powershell core and azure authentication cmdlets using Cng cryptography.
.\setup-test-env.ps1 will not authenticate to azure properly using spn creds until the connect-azaccount module is updated. until then, running of script outside of visual studio to set configuration settings in test configuration file may be required.

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
  "KustoCluster": null,
  "SasKey": "{{sas key}}"
}
```

## Creating pull requests

Fork repo and create pull request as normal across forks.
Labeling will be applied by git actions.
If executable changes, git actions will build.

## Creating Releases

To create a release, merge into master, and add 'Pre-release' or 'Release' label before merge.
Release will build signed exe and nuget package, and publish release to repo.