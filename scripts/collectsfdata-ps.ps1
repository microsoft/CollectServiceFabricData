<#
requires .net sdk 6
requires pwsh
#>
param(
    [string]$module = (resolve-path "$psscriptroot/../src/bin/debug/net8.0/collectsfdatadll.dll"),
    [string]$collectsfdataOptionsFile = (resolve-path "$psscriptroot/../src/bin/debug/net8.0/collectsfdata.options.json")
)

write-host "checking $module"
if(!(test-path $module)){
    write-error "$module does not exist"
    return
}

import-module $module
write-host "$collectsfdataOptionsFile"
$validate = $true; #$true; #$false; # $true
$loadDefaultConfig = $false; #$true; #$true; #$false
$logToConsole = $true

write-host "[CollectSFData.Collector]`$global:csfd = [CollectSFData.Collector]::new($logToConsole)"
[CollectSFData.Collector]$global:csfd = [CollectSFData.Collector]::new($logToConsole)

write-host "[CollectSFData.Common.ConfigurationOptions]`$global:config = [CollectSFData.Common.ConfigurationOptions]::new(@($collectsfdataOptionsFile), $validate, $loadDefaultConfig)"
[CollectSFData.Common.ConfigurationOptions]$global:config = [CollectSFData.Common.ConfigurationOptions]::new(@($collectsfdataOptionsFile), $validate, $loadDefaultConfig)
$global:csfd

write-host "execute:`$csfd.Collect($config) when ready to collect"
#$csfd.Collect($config)
write-host "use `$global:csfd"