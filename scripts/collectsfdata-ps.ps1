param(
    [string]$module = (resolve-path "$psscriptroot/../src/bin/debug/net6.0/collectsfdatadll.dll"),
    [string]$collectsfdataOptionsFile = (resolve-path "$psscriptroot/../src/bin/debug/net6.0/collectsfdata.options.json")
)

write-host "checking $module"
if(!(test-path $module)){
    write-error "$module does not exist"
    return
}

import-module $module
write-host "$collectsfdataOptionsFile"
$validate = $true; #$false; # $true
$loadDefaultConfig = $true; #$true; #$false
$logToConsole = $true

write-host "[CollectSFData.Collector]`$global:csfd = [CollectSFData.Collector]::new($logToConsole)"
[CollectSFData.Collector]$global:csfd = [CollectSFData.Collector]::new($logToConsole)

write-host "[CollectSFData.Common.ConfigurationOptions]`$global:config = [CollectSFData.Common.ConfigurationOptions]::new(@($collectsfdataOptionsFile), $validate, $loadDefaultConfig)"
[CollectSFData.Common.ConfigurationOptions]$global:config = [CollectSFData.Common.ConfigurationOptions]::new(@($collectsfdataOptionsFile), $validate, $loadDefaultConfig)
$global:csfd

write-host "`$csfd.Collect($config)"
$csfd.Collect($config)
write-host "use `$global:csfd"