<#
    script to install kusto functions in .csl format using kusto-rest.ps1 
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$kustoCluster = '',
    [Parameter(Mandatory = $true)]
    [string]$kustoDatabase = '',
    [string]$location = '',
    [switch]$test,
    [switch]$force,
    [string]$kustoFunctionsDir = "$psscriptroot\..\KustoFunctions"
)

$ErrorActionPreference = 'continue'
$error.clear()

if (!$kusto) {
    . .\kusto-rest.ps1 -cluster $kustoCluster -database $kustoDatabase
}

$kustoScripts = [io.directory]::getFiles($kustoFunctionsDir, '*.csl', [io.searchoption]::AllDirectories)
$scriptErrors = [collections.arraylist]::new()

foreach ($script in $kustoScripts) {
    write-host "`$kusto.ExecScript(`"$script`")" -foregroundcolor cyan

    if (!$test) {
        try {
            $kusto.ExecScript("$script")
        }
        catch {
            [void]$scriptErrors.Add($script)
        }
    }

    if ($error -and !$force) {
        return
    }
    else {
        $error.Clear()
    }
}

if ($scriptErrors) {
    $scriptErrors | out-string
    Write-Warning "the above scripts need to be executed manually:"
}

write-host 'finished'