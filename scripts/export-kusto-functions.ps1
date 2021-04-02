<#
    script to export kusto functions in .csl format using kusto-rest.ps1 
#>
[cmdletbinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$kustoCluster = '',
    [Parameter(Mandatory = $true)]
    [string]$kustoDatabase = '',
    [string[]]$exclusions = @('sfrplog'),
    [switch]$test,
    [switch]$force,
    [string]$kustoDir = "$psscriptroot\..\kusto\functions"
)

$ErrorActionPreference = 'continue'
$scriptErrors = [collections.arraylist]::new()
$scriptSuccess = [collections.arraylist]::new()

function main() {
    $error.clear()

    if (!$kusto -or $force) {
        . .\kusto-rest.ps1 -cluster $kustoCluster -database $kustoDatabase
    }
    
    $kusto.Exec('.show functions')

    foreach ($function in $kusto.ResultTable) {
        export-function $function
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

function export-function($function)
{
    write-host "exporting $($function.Name)"
    $functionScript = ".create-or-alter function with (docstring = `"$($function.DocString)`", folder = `"$($function.Folder)`")`r`n    $($function.Name)$($function.Parameters) $($function.Body)"
    Write-Verbose $functionScript
    $fileName = "$kustoDir\$($function.Folder)\$($function.Name).csl"
    $fileDirectory = [io.path]::GetDirectoryName($fileName)

    foreach($exclusion in $exclusions)
    {
        if($fileName.Contains($exclusion))
        {
            write-warning "skipping exclusion $($function.Name)"
            return
        }
    }

    if(!(test-path $fileDirectory)){
        mkdir $fileDirectory
    }

    if((test-path $fileName) -and !$force){
        $currentFunction = get-content -raw $fileName

        write-verbose "comparing export and current functions"
        if([string]::Compare([regex]::replace($functionScript,"\s",""),[regex]::replace($currentFunction,"\s","")) -eq 0){
            write-host "no change to function $functionScript. skipping" -ForegroundColor Cyan
            return
        }
    }

    if (!$test) {
        try {
            out-file -InputObject $functionScript -FilePath $fileName
            [void]$scriptSuccess.Add($function.Name)
        }
        catch {
            [void]$scriptErrors.Add($function.Name)
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

