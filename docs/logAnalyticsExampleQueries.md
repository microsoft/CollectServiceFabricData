# Example Log Analytics queries

[project root](https://dev.azure.com/ServiceFabricSupport/Tools)  
[overview](../docs/overview.md)  
[log analytics quickstart](../docs/logAnalyticsQuickStart.md)  
[log analytic queries in csl query format](../docs/LogAnalyticsQueries/logAnalyticsExampleQueries.md.csl)  

For examples below, 'search *' can be replaced with the LogAnalyticsName parameter specified when ingesting data or use.
This parameter gets stored in field 'Type' and will have '_CL' suffix appended.
Default fields for service fabric diagnostic .dtr logs are:
- Timestamp = Timestamp_t
- TID = TID_d
- PID = PID_d
- Level = Level
- Type = Type_s
- Text = Text
- NodeName = NodeName_s
- FileType = FileType_s
- Type = Type (stores tag / name specified during ingest)

For additional information see [Log Analytics query language](https://docs.microsoft.com/en-us/azure/azure-monitor/log-query/query-language)

### display events by time range

```
let startTime = datetime('2019-01-22T14:30');
let endTime = datetime('2019-01-22T14:40');
search *
| order by Timestamp_t asc
| where Timestamp_t between (startTime .. endTime)
| project Timestamp_t , TID_d , PID_d , Level , Type_s , Text , NodeName_s , FileType_s , Type
```

### display events that are not informational

```
let exclusion1 = "chaos";
search *
| order by Timestamp_t asc
| where Level !contains "info" and Level !contains "unknown"
| where Type_s !contains exclusion1 and Text !contains exclusion1
| project Timestamp_t , TID_d , PID_d , Level , Type_s , Text , NodeName_s , FileType_s , Type
| limit 1000 
```

#### sample result

```
Timestamp_t [UTC] 2019-01-06T18:22:35.588Z
TID_d 2,852
PID_d 4
Level Warning
Type_s .ASYNC_REQUEST
Text "An Async request was failed. (Activity Id:0) (Status:=c0000034) (AsyncContext:=ffff9d011b294960) (D1:=2) (D2:=ffff9d011b294d00)(Function:RequestMarshallerKernel::OpenLogContainerCompletion) (Line:780) (File:x:\bt\960840\repo\src\prod\src\ktllogger\sys\ktlshim\ktllogmarshalkernel.cpp)"
NodeName_s _nt0_0
FileType_s lease
Type laTest10_CL
```

### display 'fabric_e_*' events

```
let filterPattern = "FABRIC_E_";
let excludePattern = "FABRIC_E_CONNECTION_CLOSED"; // normal tcp connection close
let excludePattern2 = "FABRIC_E_INVALID_SUBJECT_NAME"; // noise bug to be fixed in 6.5
search *
| order by Timestamp_t asc
| where Type_s matches regex filterPattern or Text matches regex filterPattern
| where Type_s !contains excludePattern and Text !contains excludePattern
| where Type_s !contains excludePattern2 and Text !contains excludePattern2
| project Timestamp_t , TID_d , PID_d , Level , Type_s , Text , NodeName_s , FileType_s, Type
| limit 1000
```

### display DNS response times in new column

```
let filterPattern = @'Dns.+Duration \((?P<ms> \d{2,5}) ms \)';
search *
| where Type_s matches regex filterPattern or Text matches regex filterPattern 
| extend durationMs = extract(filterPattern, 1, Text, typeof(long))
| project Timestamp_t , TID_d , PID_d , NodeName_s , Level , Type_s , Text , FileType_s, durationMs
| where durationMs > 30
| order by Timestamp_t asc
//| order by durationMs desc
| limit 1000
```

### regex example with match extraction / extend case insensitive

```
let filterPattern = @'dns';
let regexPattern = strcat('((?i)',filterPattern,')'); // re2 case insensitive https://github.com/google/re2/wiki/Syntax
search *
//| where Type_s matches regex filterPattern or Text matches regex filterPattern 
| where Type_s matches regex regexPattern or Text matches regex regexPattern 
| extend durationMs = extract(filterPattern, 1, Text, typeof(long))
| project Timestamp_t , TID_d , PID_d , NodeName_s , Level , Type_s , Text , FileType_s, durationMs
| where durationMs > 30
| order by Timestamp_t asc
//| order by durationMs desc
| limit 1000
```

### regex example with match case insensitive

```
let ingest = 'trace_9769_0128_1_CL';
let filterPattern = @'lease';
let regexPattern = strcat('((?i)',filterPattern,')'); // re2 case insensitive 
search ingest
| where Type_s matches regex regexPattern or Text matches regex regexPattern 
//| where Type_s contains filterPattern or Text contains filterPattern 
//| where Type_s matches regex filterPattern or Text matches regex filterPattern 
| project Timestamp_t , TID_d , PID_d , NodeName_s , Level , Type_s , Text , FileType_s
| order by Timestamp_t asc
| limit 1000
```

### performance counter example

```
// perf counter table query
let startTime = datetime('2019-02-14T17:00');
let endTime = datetime('2019-02-14T18:00');
counter_2153_0214_6_CL
| where Timestamp_t between (startTime .. endTime)
//| where CounterName_s contains "Avg. Disk Queue Length" and CounterName_s contains "c:"
| where CounterName_s contains "% Idle Time" and CounterName_s contains "c:"
//| where CounterName_s contains "Avg. Disk Queue Length" and CounterName_s contains "d:"
//| where CounterName_s contains "% Idle Time" and CounterName_s contains "c:"
//| where CounterName_s contains "TCP" and CounterName_s contains "reset"
//| where CounterName_s contains "TCP" and CounterName_s contains "fail"
//| where CounterName_s contains "TCP" and CounterName_s contains "segments received"
//| where CounterName_s contains "TCP" and CounterName_s contains "segments sent"
//| where CounterName_s contains "TCP" and CounterName_s contains "connections active"
//| where CounterName_s contains "TCP" and CounterName_s contains "connections passive"
//| where CounterName_s contains "Processor(_Total)"
//| where CounterName_s contains "Paging File"
//| where CounterName_s contains "Available Mbytes"
//| where CounterName_s contains "Process(Fabric)\\% Processor Time"
//| where CounterName_s contains "Process(FabricDCA)\\% Processor Time"
//| where NodeName_s == "_t1eu_5"
//| where Timestamp between(todatetime("12/08/2018 23:00:00.000")..todatetime("12/09/2018 00:30:00.000"))
//| extend disk = extract(@"PhysicalDisk\(\d (?P<disk>.+:)\)\\Avg. Disk Queue Length", 1, CounterName_s)
//| summarize percentiles(CounterValue_d,5,50,95) by xtime=bin(Timestamp,2m), NodeName_s //CounterName_s, disk
//| where CounterValue_d > 75
| summarize avg(CounterValue_d) by xtime=bin(Timestamp_t,1m), NodeName_s //CounterName_s, NodeName_s, disk
| render timechart;
```

### blob table example

```
table_2153_0214_6_CL
| where PartitionKey_s contains "cm" //todatetime('2019-02-01T22:37:59.425')
//| where Timestamp_t between(todatetime('2019-02-04T04:57') .. todatetime('2019-02-04T05:01'))
//| where * contains "cm"
//| where PropertyValue_s contains "W9492DevG_3"
| order by Timestamp_t asc
| project Timestamp_t, TypeTimeStamp_s, ETag_s, PartitionKey_s, RowKey_s, PropertyName_s, PropertyValue_s, Table_s
```

### blob table join example

```
// queries sf table for cluster manager 'cm' entries
let t = ['table_2153_0214_6_CL'];
let e = t| where PropertyValue_s contains "cm"; //"nodeadded";
e | join (t) on RowKey_s, $left.RowKey_s == $right.RowKey_s
//e | join (t |where PropertyName contains "nodeName") on RowKey,$left.RowKey == $right.RowKey
| project Timestamp_t, PartitionKey_s, PropertyValue_s, PropertyName_s1, PropertyValue_s1
| order by Timestamp_t asc, PropertyName_s1 asc
```

### blob table example

```
// searches log analytics _table property value for string
// joins matching node name record based on RowKey
let t = ['table_2153_0214_6_CL'];
//let events = t;
let e = t|where PropertyValue_s contains "The Data Collection Agent (DCA) does not have enough disk space to operate. Diagnostics information will be left uncollected if this continues to happen.";
e | join (t |where PropertyName_s contains "nodeName") on RowKey_s, $left.RowKey_s == $right.RowKey_s
|project Timestamp_t, PropertyValue_s, PropertyValue_s1
| order by Timestamp_t asc
| summarize count() by xtime=bin(Timestamp_t,1m), PropertyValue_s1
| render timechart
```