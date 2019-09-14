# requirements

[project root](https://dev.azure.com/ServiceFabricSupport/Tools)  
[overview](../docs/overview.md)  

## Base requirements

Service Fabric diagnostic data can be large depending on utilization and size of cluster. CollectSFData is a multithreaded utilty designed to process data as quickly as possible.
Depending on configuration, resource usage of cpu, memory, network, disk, and disk space can be high. Below are general resource guidelines for execution of utility.

- Windows 10 image x64
- .net 4.5.2+
- 16+ GB RAM
- 4+ cpu
- 100 GB free drive space preferably on ssd for fabriclogs

## Kusto option requirements

Configuration for data ingestion into Kusto has these requirements. Currently fabriclogs, fabriccounters, and table (event) data all have to be formatted after download before ingestion into Kusto. This requires additional cpu, memory, and disk resources. The following is needed:

- Existing online Kusto database.
- Authentication to Kusto database. Authentication can be interactive or non interactive.

**NOTE: It may be preferable to use an existing shared Kusto cluster or Log Analytics instead of running a standalone Kusto cluster and database. A minimum sized Kusto cluster is expensive as they are large multi-node (at least 2) clusters. If stand alone cluster is the only option, turn off when not in use.**

## Log Analytics option requirements

Configuration for date ingestion into Log Analytics as a custom log has these requirements. Currently fabriclogs, fabriccounters, and table (event) data all have to be formatted after download before ingestion into Log Analytics. This requires additional cpu, memory, and disk resources. 
The following is needed:

- Existing Log Analytics workspace and workspace ID guid.
- Log Analytics primary / secondary base64 key for authentication.