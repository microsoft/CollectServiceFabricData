# setup

[log analytics quickstart](../docs/logAnalyticsQuickStart.md)  

## CollectSFData Setup

CollectSFData is a console only utility that has no install.
Use the below steps to setup environment for use with CollectSFData.

1. ensure machine executing utility has comparable [requirements](../docs/requirements.md)
2. download latest release [releases](https://github.com/microsoft/CollectServiceFabricData/releases/tag/CollectSFData-latest)  
3. extract zip to working directory
4. from extracted directory, use command prompt / powershell to execute utility
5. [configuration](../docs/configuration.md) can be passed as command line arguments or in json files.
6. for help, type 'collectsfdata.exe /?'

## CollectSFData Setup with Kusto

If using Kusto, an existing online Kusto database with authentication is required.

**NOTE: It may be preferable to use an existing shared Kusto cluster or Log Analytics (OMS) instead of running a standalone Kusto cluster and database. A minimum sized Kusto cluster is expensive as they are large multi-node (at least 2) clusters. If stand alone cluster is the only option, turn off when not in use.**

- Existing online Kusto database. See [Create an Azure Data Explorer cluster](https://docs.microsoft.com/en-us/azure/data-explorer/create-cluster-database-portal) if creating a new cluster.
- Authentication to Kusto database. This can be interactive or non-interactive using an Azure application id / spn.

## Kusto Authentication

So as not to require users to repeatedly enter their credentials, Kusto caches AAD tokens.
The token cache is stored locally on the machine (%APPDATA%\Kusto\tokenCache.data)
and is bound to the logged-on user identity so it can't be decrypted by other users on that machine.
Resource id should be your Kusto cluster URL, e.g. https://mycluster.kusto.windows.net or https://mycluster.kustomfa.windows.net.

The term AAD Authority URL denotes the AAD endpoint to be contacted for authentication ans expands into one of the following:
- In order to access the common endpoint and to authenticate based on the principal's default AAD tenant: https://login.microsoftonline.com/common/oauth2/authorize
- In order to specify the AAD tenant for the authentication: https://login.microsoftonline.com/<%tenantId%>/oauth2/authorize

## CollectSFData Setup with Log Analytics

If using Log Analytics (OMS), an existing or new workspace is required.
From workspace, the workspace id guid and primary /secondary shared key from workspace -> advanced settings are required parameters.
[Log Analytics Pricing](https://azure.microsoft.com/en-us/pricing/details/monitor/). For quickstart setup of collectsfdata and log analytics, use [log analytics quickstart](../docs/logAnalyticsQuickStart.md).