# overview

[requirements](../docs/requirements.md)  
[releases](https://github.com/microsoft/CollectServiceFabricData/releases/tag/CollectSFData-latest)  
[setup](../docs/setup.md)  
[configuration](../docs/configuration.md)  
[examples](../docs/examples.md)  

CollectSFData is a .net command-line utility to assist with the download of Azure Service Fabric diagnostic data from the configured Azure storage account.
Optionally, CollectSFData can be configured to ingest downloaded data into a configured [Azure Data Explorer](https://docs.microsoft.com/en-us/azure/data-explorer/) (Kusto) database or Log Analytics (OMS) for analysis.
See [requirements](../docs/requirements.md), [setup](../docs/setup.md) and [configuration](../docs/configuration.md) for additional information.

Service Fabric diagnostic data that can be enumerated / downloaded from configured storage account:

- service fabric detailed .dtr logs in .zip. (fabriclogs-*)
- service fabric counter .blg files. (fabriccounters-*)
- service fabric fabric exceptions .dmp files. (fabriccrashdump-*)
- service fabric events stored in Azure blob tables.
