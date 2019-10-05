<#
script to setup microsoft.azure.kusto.tools nuget package for kusto interactive console in code, powershell, cmd, ...
#>
param(
    $scriptDir = "$PSScriptRoot\..\docs\KustoQueries",
    $kustoEngineUrl = "https://{{kusto cluster}}.{{ location }}.kusto.windows.net/{{ kusto database }}",
    $kustoToolsPackage = "microsoft.azure.kusto.tools",
    $kustoConnectionString = "$kustoEngineUrl;Fed=True",
    $location = "$($env:USERPROFILE)\.nuget\packages", #global-packages", # local
    $nugetIndex = "https://api.nuget.org/v3/index.json",
    $nugetDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe",
    $transcriptFile = "$($env:temp)\kusto.cli.csv",
    [switch]$noTranscript
)

$ErrorActionPreference = "continue"
if(!($env:path.contains(";$pwd;$psscriptroot"))) { $env:path += ";$pwd;$psscriptroot" } 

function main()
{
    $kustoToolsDir = "$env:USERPROFILE\.nuget\packages\$kustoToolsPackage\"
    $currentDir = Get-Location
    Set-Location $scriptDir

    if (!(test-path $kustoToolsDir))
    {

        if(!(test-path nuget))
        {
            (new-object net.webclient).downloadFile($nugetDownloadUrl, "$pwd\nuget.exe")
        }

        nuget install $kustoToolsPackage -Source $nugetIndex -OutputDirectory $location
    }

    $kustoExe = $kustoToolsDir + @(get-childitem -recurse -path $kustoToolsDir -Name kusto.cli.exe)[-1]
    
    if($noTranscript)
    {
        $global:kustoCli = "$kustoExe `"$kustoConnectionString`""
    }
    else 
    {
        $global:kustoCli = "$kustoExe `"$kustoConnectionString`" -transcript:$transcriptFile"    
    }

    if (!(test-path $kustoExe))
    {
        Write-Warning "unable to find kusto client tool $kustoExe. exiting"
        return
    }

    invoke-expression "$($kustoExe) /?"
    invoke-expression "$($kustoExe) -execute:?"

    write-host "kusto.cli.exe help: $($kustoExe) /?" -ForegroundColor Green
    write-host "kustoCli syntax help: ?" -ForegroundColor Green
    write-host "kustoCli syntax exit: q" -ForegroundColor Green
    write-host "to restart cli: $($global:kustoCli)" -ForegroundColor Green
    
    if(!$noTranscript)
    {
        code $transcriptFile
    }

    invoke-expression $global:kustoCli

    set-location $currentDir
}

main
