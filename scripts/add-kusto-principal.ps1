<#
.SYNOPSIS
    script to export kusto functions in .csl format using kusto-rest.ps1 
.EXAMPLE
// No need to specify AAD tenant for UPN, as Kusto performs the resolution by itself
.add database Test users ('aaduser=imikeoein@fabrikam.com') 'Test user (AAD)'

// AAD SG on 'fabrikam.com' tenant
.add database Test users ('aadgroup=SGDisplayName;fabrikam.com') 'Test group @fabrikam.com (AAD)'

// AAD App on 'fabrikam.com' tenant - by tenant name
.add database Test users ('aadapp=4c7e82bd-6adb-46c3-b413-fdd44834c69b;fabrikam.com') 'Test app @fabrikam.com (AAD)'
#>
[cmdletbinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$kustoCluster = "",
    [Parameter(Mandatory = $true)]
    [string]$kustoDatabase = "",
    [string]$principalUpn = "",
    [ValidateSet('aaduser','aadgroup','aadapp')]
    [string]$principalType = "aaduser",
    [string]$principalDescription = "$principalType (AAD)",
    [ValidateSet('admins','ingestors','monitors','unrestrictedviewers','users','viewers')]
    [string]$principalRole = 'users',
    [switch]$whatIf,
    [switch]$force
)

$ErrorActionPreference = 'continue'

function main() {
    $error.clear()

    if (!$kusto -or $force) {
        . .\kusto-rest.ps1 -cluster $kustoCluster -database $kustoDatabase
    }
    
    $kusto.Exec(".show database $kustoDatabase principals")
    
    # if not aaduser, $principal should not have @ but should have ;
    if($principalType -ne 'aaduser'){
        $principalUpn = $principalUpn.Replace("@",";")
        write-warning "modifying upn: $principalUpn"
    }

    if($kusto.ResultTable.PrincipalDisplayName -imatch $principalUpn -and !$force){
        Write-Warning "$principalUpn already exists"
    }
    else{
        write-host "`$kusto.Exec(`".add database $kustoDatabase $principalRole ('$principalType=$principalUpn') '$principalDescription'`")"
        $error.clear()
        if(!$whatIf){
            $kusto.Exec(".add database $kustoDatabase $principalRole ('$principalType=$principalUpn') '$principalDescription'")
        }
    }
    
    write-host 'finished'
}

main

