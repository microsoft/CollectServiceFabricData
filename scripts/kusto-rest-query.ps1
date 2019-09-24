# script to query kusto with AAD client id and secret
# > ..\..\jagilber-pr\graph\draft-graph-test.ps1 -tenant 72f988bf-86f1-41af-91ab-2d7cd011db47 -resourceUrl "https://sflogs.kusto.windows.net"
[cmdletbinding()]
param(
    $query = $global:query,
    $kustoCluster = $global:kustoCluster,
    $kustoDb = $global:kustoDb,
    $resultFile = ".\result.json",
    $viewResults = $global:viewResults,
    $gridViewResults = $global:gridViewResults,
    $token = $global:token,
    $kustoLogonScript = "$PSScriptRoot\kusto-rest-logon.ps1",
    $limit = $global:limit,
    $script = $global:script,
    [hashtable]$parameters = @{} #@{'clusterName' = $resourceGroup; 'dnsName' = $resourceGroup;}
)

$ErrorActionPreference = "continue"

function main() 
{
    $startTime = get-date
    $global:kustoCluster = $kustoCluster
    $global:kustoDb = $kustoDb
    $global:viewResults = $viewResults
    $global:gridViewResults = $gridViewResults
    $global:token = $token
    $global:limit = $limit
    $global:script = $script
    $global:query = $query

    write-host "kustoCluster = $kustoCluster"
    write-host "kustoDb = $kustoDb"
    write-host "viewResults = $viewResults"
    write-host "gridViewResults = $gridViewResults"
    #write-host "token = $token"
    write-host "limit = $limit"
    write-host "script = $script"
    write-host "query = $query"

    if(!$global:limit)
    {
        $global:limit = 10000
    }

    if(!$script -and !$query)
    {
        Write-Warning "-script and / or -query should be set. exiting"
        return
    }

    if(!$kustoCluster -or !$kustoDb)
    {
        Write-Warning "-kustoCluster and -kustoDb have to be set once. exiting"
        return
    }

    if($script)
    {
        if ($script.startswith('http'))
        {
            $destFile = "$pwd\$([io.path]::GetFileName($script))" -replace '\?.*', ''
            
            if(!(test-path $destFile))
            {
                Write-host "downloading $script" -foregroundcolor green
                (new-object net.webclient).DownloadFile($script, $destFile)
            }
            else 
            {
                Write-host "using cached script $script"
            }

            $script = $destFile
        }
        
        if((test-path $script))
        {
            $query = (Get-Content -raw -Path $script).trimend([environment]::newLine) + [environment]::newLine + $query
        }
        else 
        {
            write-error "unknown script:$script"
            return
        }
    }

    # authenticate to aad first to get token
    $kustoHost = "$kustoCluster.kusto.windows.net"
    $kustoResource = "https://$kustoHost"
    
    if($query.startswith('.show') -or !$query.startswith('.'))
    {
        $uri = "$kustoResource/v1/rest/query"
    }
    else 
    {
        $uri = "$kustoResource/v1/rest/mgmt"
    }

    . $kustoLogonScript -resourceUrl $kustoResource

    if (!$global:token)
    {
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
            }
            Parameters = $parameters
        }
    } | ConvertTo-Json

    write-verbose $body

    $error.clear()
    $ret = Invoke-WebRequest -Method Post -Uri $uri -Headers $header -Body $body -Verbose -Debug
    write-verbose $ret
    
    if($error)
    {
        return
    }

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

        if($gridViewResults)
        {
            $global:resultTable | select-object * | out-gridview
        }

        write-host "results count: $($global:resultTable.Count)"
    }

    out-file -FilePath $resultFile -InputObject $global:result

    $primaryResult = $global:resultObject| where-object TableKind -eq PrimaryResult
    
    if($primaryResult)
    {
        $primaryResult.columns
        $primaryResult.Rows
    }

    set-alias kq $MyInvocation.ScriptName -Scope global

    write-host "output stored in `$global:resultObject and `$global:resultTable" -foregroundcolor green
    write-host "output json stored in $resultFile and `$global:result" -foregroundcolor green
    write-host "use alias 'kq' to run queries. example kq '.show tables'" -ForegroundColor Green
    write-host "$(((get-date) - $startTime).TotalSeconds) seconds to execute"
}

main