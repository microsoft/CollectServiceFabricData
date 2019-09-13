# examples

[project root](https://dev.azure.com/ServiceFabricSupport/Tools)  
[overview](../docs/overview.md)  
[log analytics example queries](../docs/logAnalyticsExampleQueries.md)  

```text
Example Usage #1 to download performance counter .blg files

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" -c "C:\Cases\123245\perfcounters" -s "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"

          Gathering: counter
         Start Time: 1/12/19 11:41:35 -05:00
                UTC: 1/12/19 16:41:35 +00:00
           End Time: 1/12/19 13:41:35 -05:00
                UTC: 1/12/19 18:41:35 +00:00
        AccountName: sflgaccountname
            SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"
        Parse 2 CSV: False
            Threads: 8
              Join: 0
              CacheLocation: C:\Cases\123245\perfcounters
              Filter:

        ContainerName: fabriccounters-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231



Example Usage #2 to download service fabric trace dtr.zip (.csv) files

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" --cacheLocation "C:\Cases\123245\traceLogs" --gatherType trace --sasKey "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"

          Gathering: trace
         Start Time: 1/12/19 11:41:35 -05:00
                UTC: 1/12/19 16:41:35 +00:00
           End Time: 1/12/19 13:41:35 -05:00
                UTC: 1/12/19 18:41:35 +00:00
        AccountName: sflgaccountname
            SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"
        Parse 2 CSV: False
            Threads: 8
              Join: 0
              CacheLocation: C:\Cases\123245\traceLogs
              Filter:

        ContainerName: fabriclogs-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231



Example Usage #3 using optional --nodeFilter switch to service fabric trace dtr.zip (.csv) files
        --nodeFilter is regex / string based.

CollectSFData.exe --nodeFilter "_node_0" --cacheLocation "C:\Cases\123245\traceLogs" --gatherType trace -csv --sasKey "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"

          Gathering: trace
         Start Time: 1/12/19 11:41:35 -05:00
                UTC: 1/12/19 16:41:35 +00:00
           End Time: 1/12/19 13:41:35 -05:00
                UTC: 1/12/19 18:41:35 +00:00
        AccountName: sflgaccountname
            SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"
        Parse 2 CSV: False
            Threads: 8
              Join: 0
              CacheLocation: C:\Cases\123245\traceLogs
              Filter: _node_0

        ContainerName: fabriclogs-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231



Example Usage #4 download service fabric trace files, unzip, and join into 100 MB .csv output files

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" --cacheLocation "C:\Cases\123245\traceLogs" --join 100 --gatherType trace --sasKey "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"

          Gathering: trace
         Start Time: 1/12/19 11:41:35 -05:00
                UTC: 1/12/19 16:41:35 +00:00
           End Time: 1/12/19 13:41:35 -05:00
                UTC: 1/12/19 18:41:35 +00:00
        AccountName: sflgaccountname
            SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"
        Parse 2 CSV: False
            Threads: 8
              Join: 100
              CacheLocation: C:\Cases\123245\traceLogs
              Filter:

        ContainerName: fabriclogs-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231



Example Usage #5 download performance counter .blg files and convert to csv files

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" --cacheLocation "C:\Cases\123245\perfcounters" --gatherType counter --csv --sasKey "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"

          Gathering: counter
         Start Time: 1/12/19 11:41:35 -05:00
                UTC: 1/12/19 16:41:35 +00:00
           End Time: 1/12/19 13:41:35 -05:00
                UTC: 1/12/19 18:41:35 +00:00
        AccountName: sflgaccountname
            SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"
        Parse 2 CSV: True
            Threads: 8
              Join: 0
              CacheLocation: C:\Cases\123245\perfcounters
              Filter:

        ContainerName: fabriccounters-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231


Example Usage #6 unzip and join service fabric trace files already on disk. for example from standalonelogcollector upload to dtm.

CollectSFData.exe --cacheLocation "C:\Cases\123245\traceLogs" --join 100 --gatherType trace

          Gathering: trace
         Start Time: 1/12/19 11:41:35 -05:00
                UTC: 1/12/19 16:41:35 +00:00
           End Time: 1/12/19 13:41:35 -05:00
                UTC: 1/12/19 18:41:35 +00:00
        AccountName:
            SAS key:
        Parse 2 CSV: False
            Threads: 8
              Join: 100
              CacheLocation: C:\Cases\123245\traceLogs
              Filter:

        ContainerName:



Example Usage #7 kusto: download service fabric trace files, unzip, join into 100 MB .csv output files, format for kusto, queue for kusto ingest.

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" -c "C:\Cases\123245\traceLogs" -j 100 -type trace -s "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D" -kc "https://ingest-kustoclusterinstance.eastus.kusto.windows.net/kustoDB" -krt -kt "kustoTable-trace"

          Gathering: trace
         Start Time: 1/12/19 11:41:35 -05:00
                UTC: 1/12/19 16:41:35 +00:00
           End Time: 1/12/19 13:41:35 -05:00
                UTC: 1/12/19 18:41:35 +00:00
        AccountName: sflgaccountname
            SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"
        Parse 2 CSV: False
            Threads: 8
              Join: 100
              CacheLocation: C:\Cases\123245\traceLogs
              Filter:

        ContainerName: fabriclogs-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231



Example Usage #8 kusto: download performance counter .blg files, convert to .csv files, format for kusto, queue for kusto ingest.

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" -c "C:\Cases\123245\traceLogs" -c -type counter -s "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D" -kc "https://ingest-kustoclusterinstance.eastus.kusto.windows.net/kustoDB" -krt -kt "kustoTable-perf"

          Gathering: counter
         Start Time: 1/12/19 11:41:35 -05:00
                UTC: 1/12/19 16:41:35 +00:00
           End Time: 1/12/19 13:41:35 -05:00
                UTC: 1/12/19 18:41:35 +00:00
        AccountName: sflgaccountname
            SAS key: https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"
        Parse 2 CSV: True
            Threads: 8
              Join: 0
              CacheLocation: C:\Cases\123245\traceLogs
              Filter:

        ContainerName: fabriccounters-6BCCE7C6-16C1-4CEA-A5EF-BE989F7C0231

```