# script to query log analytics with AAD client id and secret
# (Invoke-AzureRmOperationalInsightsQuery -WorkspaceId %workspace id%  -query "trace_0209_0418_1_CL|where Level contains @'information'|limit 10").results | ft -AutoSize

#.\log-analytics-query.ps1 -workspaceId %workspace id% -query "trace_0209_0418_1_CL|where Level contains @'information'|limit 10" -viewResults
# https://dev.loganalytics.io/documentation/Authorization/AAD-Setup
# https://dev.loganalytics.io/
# https://dev.loganalytics.io/documentation/Authorization/OAuth2

param(
    [parameter(Mandatory=$true)]
    $workspaceId = "", #"DEMO_WORKSPACE"
    $query = "search * | count",
    $resultFile = ".\result.json",
    [switch]$viewResults,
    [ValidateSet("rm","az")]
    [string]$armModuleType = "az"
)

$ErrorActionPreference = "continue"
$DebugPreference = $VerbosePreference = "continue"
Write-Warning "not currently working!!!"
# authenticate to aad first to get token
$logAnalyticsResource = "https://api.loganalytics.io"
$restLogon = "azure-$armModuleType-rest-logon.ps1"
$restLogonSource = "https://raw.githubusercontent.com/jagilber/powershellScripts/master/$restLogon"
$uri = "https://api.loganalytics.io/v1/workspaces/$workspaceId/query"

if(!(test-path ".\$restLogon"))
{
    write-host "downloading $restLogonSource"
    (New-Object System.Net.WebClient).DownloadFile($restLogonSource, "$PSScriptRoot\$restLogon")
}

Invoke-Expression ".\$restLogon -logonType graph -resource $logAnalyticsResource"

if(!$global:token.access_token)
{
    write-error "unable to acquire token. exiting"
    return
}


$header = @{"Authorization" = "Bearer $($global:token.access_token)"; "Content-Type" = "application/json"}
$header | convertto-json
$body = @{"query"= $query} | convertto-json


$ret = Invoke-WebRequest -Method Post -Uri $uri -Headers $header -Body $body -Verbose -Debug
$ret
$resultObject = $ret.content | convertfrom-json
$global:result = $resultObject | convertto-json -Depth 99

if($viewResults)
{
    $results = [collections.arraylist]@()
    $columns = @{}
    
    foreach($column in ($resultObject.tables[0].columns))
    {
        [void]$columns.Add($column.name, $null)
    }

    $resultModel = New-Object -TypeName PsObject -Property $columns

    foreach($row in ($resultObject.tables[0].rows))
    {
        $count = 0
        $result = $resultModel.PsObject.Copy()

        foreach($column in ($resultObject.tables[0].columns))
        {
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