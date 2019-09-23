# script to query kusto with AAD client id and secret
# > ..\..\jagilber-pr\graph\draft-graph-test.ps1 -tenant 72f988bf-86f1-41af-91ab-2d7cd011db47 -resourceUrl "https://sflogs.kusto.windows.net"
[cmdletbinding()]
param(
    $query = "search * | count",
    [ValidateNotNullorEmpty()]
    $kustoCluster = $global:kustoCluster,
    [ValidateNotNullorEmpty()]
    $kustoDb = $global:kustoDb,
    $resultFile = ".\result.json",
    [switch]$viewResults = $global:viewResults,
    [ValidateSet("rm", "az")]
    [string]$armModuleType = "az",
    $nugetDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe",
    $token = $global:token,
    $kustoLogonScript = "$PSScriptRoot\kusto-rest-logon.ps1",
    $limit = $global:limit
)

$ErrorActionPreference = "continue"
#$DebugPreference = $VerbosePreference = "continue"
$global:kustoCluster = $kustoCluster
$global:kustoDb = $kustoDb
$global:viewResults = $viewResults
$global:token = $token
$global:limit = $limit

function main() 
{
    if(!$global:limit)
    {
        $global:limit = 10000
    }

    write-warning "current limit set to $global:limit"

    # authenticate to aad first to get token
    $kustoHost = "$kustoCluster.kusto.windows.net"
    $kustoResource = "https://$kustoHost"
    $uri = "$kustoResource/v2/rest/query"
    . $kustoLogonScript -resourceUrl $kustoResource

    if (!$global:token) {#.access_token) {
        write-error "unable to acquire token. exiting"
        return
    }

    $requestId = [guid]::NewGuid().ToString()
    write-host "request id: $requestId"

    $header = @{
        "Accept"                 = "application/json"
        "Authorization"          = "Bearer $($global:token)"
        "Content-Type"           = "application/json"
        "Host"                   = $kustoHost
        "x-ms-app"               = [io.path]::GetFileName($MyInvocation.ScriptName)
        "x-ms-user"              = $env:USERNAME
        "x-ms-client-request-id" = $requestId
    } 

    write-verbose $header | convertto-json

    $body = @{
        db         = $kustoDb
        csl        = "$query | limit $global:limit"
        properties = @{
            Options = @{
                queryconsistency = "strongconsistency"
                #Parameters       = @{ }
            }
        }
    } | ConvertTo-Json

    write-verbose $body

    $ret = Invoke-WebRequest -Method Post -Uri $uri -Headers $header -Body $body -Verbose -Debug
    write-verbose $ret
    $global:resultObject = $ret.content | convertfrom-json
    $global:result = $global:resultObject | convertto-json -Depth 99

    if ($global:viewResults) {
        $global:resultTable = [collections.arraylist]@()
        $columns = @{ }
    
        foreach ($column in ($global:resultObject.tables[0].columns))
        {
            [void]$columns.Add($column.ColumnName, $null)
        }

        $resultModel = New-Object -TypeName PsObject -Property $columns

        foreach ($row in ($global:resultObject.tables[0].rows)) 
        {
            $count = 0
            $result = $resultModel.PsObject.Copy()

            foreach ($column in ($global:resultObject.tables[0].columns)) {
                $result.($column.ColumnName) = $row[$count++]
            }

            [void]$global:resultTable.add($result)
        }

        $global:resultTable | format-table -AutoSize
        write-host "results count: $($global:resultTable.Count)"
    }

    write-host "output stored in `$global:resultObject and `$global:resultTable" -foregroundcolor green
    out-file -FilePath $resultFile -InputObject $global:result

    $primaryResult = $global:resultObject| where-object TableKind -eq PrimaryResult
    
    if($primaryResult)
    {
        $primaryResult.columns
        $primaryResult.Rows
    }

    write-host "output json stored in $resultFile and `$global:result" -foregroundcolor green
    #$DebugPreference = $VerbosePreference = "silentlycontinue"
}

main