<#
    script to install kusto functions in .csl format using kusto-rest.ps1 
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$kustoCluster = '',
    [Parameter(Mandatory=$true)]
    [string]$kustoDatabase = '',
    [switch]$test,
    [switch]$force,
    [string]$kustoFunctionsDir = "$psscriptroot\..\KustoFunctions"
)

$erroractionpreference = 'continue'
$error.clear()
. .\kusto-rest.ps1 -cluster $kustoCluster -database $kustoDatabase

$kustoScripts = [io.directory]::getFiles($kustoFunctionsDir, '*.csl',[io.searchoption]::AllDirectories)

foreach($script in $kustoScripts) {
    write-host "`$kusto.ExecScript(`"$script`")" -foregroundcolor cyan

    if(!$test) {
        $kusto.ExecScript("$script")
    }

    if($error -and !$force) {
        return
    }
}

write-host 'finished'