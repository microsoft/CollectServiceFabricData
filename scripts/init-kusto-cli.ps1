<#
script to setup microsoft.azure.kusto.tools nuget package for kusto interactive console in code, powershell, cmd, ...
#>
param(
    $scriptDir = "$([io.path]::GetDirectoryName($MyInvocation.MyCommand.Path))\..\docs\KustoQueries",
    $kustoEngineUrl = "https://sflogs.kusto.windows.net/incidentlogs",
    $kustoToolsPackage = "microsoft.azure.kusto.tools",
    $kustoConnectionString = "$kustoEngineUrl;Fed=True",
    $location = "$($env:USERPROFILE)\.nuget\packages", #global-packages", # local
    $nugetInstallScript = "https://raw.githubusercontent.com/jagilber/powershellScripts/master/download-nuget.ps1",
    $nugetIndex = "https://api.nuget.org/v3/index.json",
    $transcriptFile = "$($env:temp)\kusto.cli.csv",
    [switch]$noTranscript
)

$ErrorActionPreference = "continue"
function main()
{
    $kustoToolsDir = "$env:USERPROFILE\.nuget\packages\$kustoToolsPackage\"
    $currentDir = Get-Location
    Set-Location $scriptDir

    if (!(test-path $kustoToolsDir))
    {
        $error.clear()
        (nuget) | out-null

        if ($error)
        {
            $error.Clear()
            invoke-webrequest $nugetInstallScript -UseBasicParsing | invoke-expression
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
