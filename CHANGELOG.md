# Change log

## 12/4/2020  

- modify repo layout structure and place source in /src  
- add kusto functions  
- fix table ingest only ingesting last chunk enumerated from table  
- fix unique table ingest when kustoingestmessage false using .set-or-replace  
- fix table filter now using containerfilter  
- migrate from binaryformatter deprecated in net5 to newtonsoft json serializer  
- fix unique when kustoingestmessage false and file is over max ingest size  
- updated loganalytics api  
- add tests for msal and kusto  
- update README  
- update configuration json examples  
- add building md  

## 11/10/2020 https://github.com/microsoft/CollectServiceFabricData/tree/v2.7.2011.10013644  

- add labeler workflow back after github fix  
- migrated from nuget to dotnet build for workflow  
- add vscode launch.json config  
- add support for dll and exe. converted project to dll and made new exe project referencing dll project.  
- clean app.config  
- migrate to csproj nuget package from packages.config   
- clean csproj
- modify nuspec layout to support dll and exe  
- migrate from .net462 to .net472 for security and stability  
- add support for .netcoreapp3.1  
- modify static inherited instance class to singleton reference  
- migrate from ADAL to MSAL as ADAL is now deprecated  
- move token cache to userprofile  
- add object, counter, instance fields for gather type counter  
- fix temp directory saveconfig in json output  
- fix custom task scheduler thread race causing duplicate managers
- migrate from logdebug bool to logdebug int levels 0-5 to turn down logging  
- update test project to .netcore to remove security vulnerabilities  
- add kusto functions  
- add build scripts in /scripts dir  

## 7/30/2020 https://github.com/microsoft/CollectServiceFabricData/tree/v2.6.7516.26434  

- fix nuget package layout

## 7/23/2020 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.6.7509.24815  

- remove broken labeler workflow  
- add nuget package to workflow  
- set source code namespaces  
- remove static members from httpclient  
- add tests  
- add kusto functions  

## 4/22/2020 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.6.7417.39096

- add collectsfdata.options.json to release
- add discovered blob time range to output
- add sas validation
- add noprogresstimeout min to prevent indefinite hang  
- add online version check  
- add maxstreamtransmitbytes  
- add last message list  
- add determineclusterid fallback 
- add kustoUseIngestMessage  
- add admin and query clients to kusto endpoint
- add test classes  
- add kusto functions  
- default kustoCompressed to true  
- update config schema  


## 12/4/2019 https://github.com/microsoft/CollectServiceFabricData/releases/tag/v2.6.7277.26289

- modify git actions for v1 and update tag
- modify blob callback to include length
- modify displaystatus summary
- add config validation functions
- add gathertype exception
- add / modify kusto functions

## 11/21/2019 init 2.6 public release  

- .net462 console utility  