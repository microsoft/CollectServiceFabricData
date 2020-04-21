<#
.SYNOPSIS
setup variables for running tests
#>
[cmdletbinding()]
param(
    [switch]$clean,
    [switch]$reset,
    [string]$configurationFile = "..\..\temp\collectSfDataTestProperties.json",
    [string]$configurationFileTemplate = "$psscriptroot\collectSfDataTestProperties.json",
    [string]$tempDir = "..\..\temp"
)

$PSModuleAutoLoadingPreference = 2
$ErrorActionPreference = "continue"
$error.clear()

function main(){

if((test-path $tempDir) -and $clean){
    remove-item $tempDir -recurse -force
    new-item -itemType directory -name $tempDir
}

if(!(test-path $configurationFile) -or $reset){
    if(!(test-path $configurationFileTemplate)){
        write-error "unable to find template file $configurationFileTemplate"
        return 1
    }

    copy-item $configurationFileTemplate $configurationFile -force
    write-host "edit file directly and save: $configurationFile" -foregroundcolor green
    . $configurationFile
}

$testProperties = get-content -raw $configurationFile
write-host "current configuration:`r`n$($testProperties | out-string)" -foregroundcolor green



}

main