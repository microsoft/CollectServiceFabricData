# script to query kusto with AAD client id and secret

param(
    [parameter(Mandatory = $true)]
    $kustoCluster = "",
    [parameter(Mandatory = $true)]
    $kustoDb = "",
    $query = "search * | count",
    $resultFile = ".\result.json",
    [switch]$viewResults,
    [ValidateSet("rm", "az")]
    [string]$armModuleType = "az"
)
Write-Warning "not currently working!!!"
$ErrorActionPreference = "continue"
$DebugPreference = $VerbosePreference = "continue"

function main() {


    Invoke-WebRequest "https://raw.githubusercontent.com/jagilber/powershellScripts/master/download-nuget.ps1" -UseBasicParsing | Invoke-Expression

    # authenticate to aad first to get token
    $kustoHost = "$kustoCluster.kusto.windows.net"
    $kustoResource = "https://$kustoHost"
    $restLogon = "azure-$armModuleType-rest-logon.ps1"
    $restLogonSource = "https://raw.githubusercontent.com/jagilber/powershellScripts/master/$restLogon"
    $uri = "$kustoResource/v2/rest/query"

    if (!(test-path ".\$restLogon")) {
        write-host "downloading $restLogonSource"
        (New-Object System.Net.WebClient).DownloadFile($restLogonSource, "$PSScriptRoot\$restLogon")
    }

    Invoke-Expression ".\$restLogon -logonType graph -resource $kustoResource"

    if (!$global:token.access_token) {
        write-error "unable to acquire token. exiting"
        return
    }

    $requestId = [guid]::NewGuid().ToString()
    write-host "request id: $requestId"

    $header = @{
        "Accept"                 = "application/json"
        "Authorization"          = "Bearer $($global:token.access_token)"
        "Content-Type"           = "application/json"
        "Host"                   = $kustoHost
        "x-ms-app"               = [io.path]::GetFileName($MyInvocation.ScriptName)
        "x-ms-user"              = $env:USERNAME
        "x-ms-client-request-id" = $requestId
    } 

    $header | convertto-json

    $body = @{
        db         = $kustoDb
        csl        = $query
        properties = @{
            Options = @{
                queryconsistency = "strongconsistency"
                #Parameters       = @{ }
            }
        }
    } | ConvertTo-Json

    $body

    $ret = Invoke-WebRequest -Method Post -Uri $uri -Headers $header -Body $body -Verbose -Debug
    $ret
    $resultObject = $ret.content | convertfrom-json
    $global:result = $resultObject | convertto-json -Depth 99

    if ($viewResults) {
        $results = [collections.arraylist]@()
        $columns = @{ }
    
        foreach ($column in ($resultObject.tables[0].columns))
        {
            [void]$columns.Add($column.name, $null)
        }

        $resultModel = New-Object -TypeName PsObject -Property $columns

        foreach ($row in ($resultObject.tables[0].rows)) 
        {
            $count = 0
            $result = $resultModel.PsObject.Copy()

            foreach ($column in ($resultObject.tables[0].columns)) {
                $result.($column.name) = $row[$count++]
            }

            [void]$results.add($result)
        }

        $results | format-table -AutoSize
        write-host "results count: $($results.Count)"
    }

    out-file -FilePath $resultFile -InputObject $global:result
    write-host "output stored in $resultFile and `$global:result"
    $DebugPreference = $VerbosePreference = "silentlycontinue"
}

main