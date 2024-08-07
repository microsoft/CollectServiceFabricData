<#
    script to import kusto functions in .csl format using kusto-rest.ps1 
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$kustoCluster = '',
    [Parameter(Mandatory = $true)]
    [string]$kustoDatabase = '',
    [switch]$test,
    [switch]$force,
    [string]$kustoDir = "$psscriptroot\..\kusto\functions",
    [string]$clientId,
    [string]$clientSecret,
    [string]$tenantId
)

$ErrorActionPreference = 'continue'
$kustoScripts = [io.directory]::getFiles($kustoDir, '*.csl', [io.searchoption]::AllDirectories)
$scriptErrors = [collections.arraylist]::new()
$scriptSuccess = [collections.arraylist]::new()

function main() {
    $error.clear()

    $kustoParams = @{
        cluster  = $kustoCluster
        database = $kustoDatabase
    }

    if ($clientId) { $kustoParams.clientId = $clientId }
    if ($clientSecret) { $kustoParams.clientSecret = $clientSecret }
    if ($tenantId) { $kustoParams.tenantId = $tenantId }

    if (!$kusto -or $force) {
        . .\kusto-rest.ps1 @kustoParams
    }

    foreach ($script in $kustoScripts) {
        exec-script $script
    }
    
    if ($scriptErrors) {
        $kustoScripts.Clear()
        $kustoScripts = $scriptErrors.ToArray()
        $scriptErrors.Clear()
        write-warning "rerunning failed scripts for dependencies"
        
        foreach ($script in $kustoScripts) {
            exec-script $script
        }
    }


    if ($scriptSuccess) {
        $scriptSuccess | out-string
        Write-host "the above scripts executed successfully:" -ForegroundColor Green
    }
    
    if ($scriptErrors) {
        $scriptErrors | out-string
        Write-Warning "the above scripts need to be executed manually:"
    }
    
    write-host 'finished'
}

function exec-script($script) {
    write-host "`$kusto.ExecScript(`"$script`")" -foregroundcolor cyan

    if (!$test) {
        try {
            $kusto.ExecScript("$script")
            [void]$scriptSuccess.Add($script)
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


main

