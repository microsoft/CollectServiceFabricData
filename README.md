# CollectSFData

## Overview

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


## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
