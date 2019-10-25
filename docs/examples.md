# Examples

Some basic examples on how to use arguments and configuration files.  
For additional examples, type 'collectsfdata.exe -ex'

## Kusto

### example command to ingest into kusto with minimal arguments

```text
collectsfdata.exe -type trace -s "<% sasKey %>" -kc "https://<% kusto ingest name %>.<% location %>.kusto.windows.net/<% kusto database %>" -kt "<% kusto table name %>"
collectsfdata.exe -type trace -s "https://sflogsxxxxxxxxxxxxx.blob.core.windows.net/?sv=2017-11-09&ss=bfqt&srt=sco&sp=rwdlacup&se=2018-12-05T23:51:08Z&st=2018-11-05T15:51:08Z&spr=https&sig=VYT1J9Ene1NktyCgsu1gEH%2FN%2BNH9zRhJO05auUPQkSA%3D" -kc https://ingest-kustodb.eastus.kusto.windows.net/serviceFabricDB -kt "fabric_traces"
```

## Log Analytics

### example command to ingest into log analytics with minimal arguments

```text
collectsfdata.exe -type trace -s "<% sasKey %>" -laid  -lak -lan
collectsfdata.exe -type trace -s "<% sasKey %>" -laid  -lak -lan
```

## Local

### example command line to download traces with minimal arguments

```text
collectsfdata.exe -type trace -cache c:\temp\sflogs -s "<% sasKey %>"
```

### example command line to download traces with full arguments

```text
collectsfdata.exe -type trace -cache c:\temp\sflogs -s "<% sasKey %>" -from "01/12/2019 09:40:08 -05:00" -to "01/12/2019 13:40:00 -05:00"
```

### example command line with existing default configuration file 'collectsfdata.options.json' populated

```text
collectsfdata.exe
```

### example command line with existing default configuration file 'collectsfdata.options.json' and existing custom configuration file.

```text
collectsfdata.exe -config collectsfdata.counters.json
```

### example command line with existing custom configuration file and command line argument.

```text
collectsfdata.exe -config collectsfdata.counters.json -s "<% sasKey %>"
```

### example command lines with node filter

```text
collectsfdata.exe -type trace -s "<% sasKey %>" --nodeFilter "_nt0_0"
collectsfdata.exe -type trace -s "<% sasKey %>" --nodeFilter "_nt0_[1-3]"
collectsfdata.exe -type trace -s "<% sasKey %>" --nodeFilter "_nt0_0|_nt0_1|_nt0_2"
```

### example command lines with uri filter  

This can be used for filtering 'trace' file types as well as any other pattern in the storage file uri. There are currently two 'trace' types, 'fabric' and 'lease'.
example uri: fabriclogs-b353af95-27a2-450e-9393-39953778b47f/_nt0_0/Fabric/f1369b98e045ced5eeac5e0d75a49f9b_fabric_traces_6.5.658.9590_132134919985415767_65_00637047454049360029_2147483647.dtr

```text
collectsfdata.exe -type trace -s "<% sasKey %>" --uriFilter "lease_"
collectsfdata.exe -type trace -s "<% sasKey %>" --uriFilter "fabric_"
```

```text
Example Usage #1 to download performance counter .blg files

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" -cache  "C:\Cases\123245\perfcounters" -s "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D"

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

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" -cache  "C:\Cases\123245\traceLogs" -type trace -s "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D" -kc "https://ingest-kustoclusterinstance.eastus.kusto.windows.net/kustoDB" -krt -kt "kustoTable-trace"

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

CollectSFData.exe -from "1/12/19 11:41:35 -05:00" -to "1/12/19 13:41:35 -05:00" -cache  "C:\Cases\123245\traceLogs" -type counter -s "https://sflgaccountname.blob.core.windows.net/fabriclogs-6b...E%3D" -kc "https://ingest-kustoclusterinstance.eastus.kusto.windows.net/kustoDB" -krt -kt "kustoTable-perf"

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
