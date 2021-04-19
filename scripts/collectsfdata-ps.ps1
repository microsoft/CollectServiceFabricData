param(
    [string]$module = (resolve-path "$psscriptroot/../src/bin/debug/netcoreapp3.1/collectsfdatadll.dll"),
    [hashtable]$collectsfdataArgs = @{}
)

write-host "checking $module"
if(!(test-path $module)){
    write-error "$module does not exist"
    return
}

import-module $module

[CollectSFData.Collector]$global:csfd = [CollectSFData.Collector]::new($true)
[CollectSFData.Common.ConfigurationOptions]$global:config = [CollectSFData.Common.ConfigurationOptions]::new($collectsfdataArgs)
$global:csfd

write-host use "`$global:csfd"
$csfd.Collect($config)