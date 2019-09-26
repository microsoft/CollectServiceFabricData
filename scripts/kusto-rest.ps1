# script to query kusto with AAD client id and secret or user and password

using namespace Microsoft.IdentityModel.Clients.ActiveDirectory
[cmdletbinding()]
param(
    [string]$query = '.show tables',
    [string]$cluster,
    [string]$database,
    [string]$table,
    [string]$resultFile, # = ".\result.json",
    [bool]$viewResults,
    [string]$token,
    [int]$limit,
    [string]$script,
    [string]$clientSecret,
    [string]$clientId,
    [bool]$pipeLine,
    [string]$tenant = "common",
    [string]$wellknownClientId = "1950a258-227b-4e31-a9cf-717495945fc2", 
    [string]$redirectUri = "urn:ietf:wg:oauth:2.0:oob",
    #[string]$resourceUrl, # = "https://{{kusto cluster}}.kusto.windows.net"
    [bool]$force,
    [hashtable]$parameters = @{ } #@{'clusterName' = $resourceGroup; 'dnsName' = $resourceGroup;}
)

$ErrorActionPreference = "continue"
$global:kusto = $null

class KustoObj {
    [object]$adalDll = $null
    [object]$authenticationResult
    [string]$clientId = $clientID
    [string]$clientSecret = $clientSecret
    [string]$cluster = $cluster
    [string]$database = $database
    [bool]$force = $force
    [int]$limit = $limit
    [hashtable]$parameters = $parameters
    [bool]$pipeLine = $null
    [string]$query = $query
    [string]$redirectUri = $redirectUri
    [object]$result = $null
    [object]$resultObject = $null
    [object]$resultTable = $null
    [string]$resultFile = $resultFile
    [string]$script = $script
    [string]$table = $table
    [string]$tenant = $tenant
    [string]$token = $token
    [bool]$viewResults = $viewResults
    [string]$wellknownClientId = $wellknownClientId
    
    KustoObj() { }
    static KustoObj() { }

    [void] CheckAdal() {
        [object]$kusto = $this
        [string]$projectName = "Microsoft.IdentityModel.Clients.ActiveDirectory"
        [string]$outputDirectory = "$($env:USERPROFILE)\.nuget\packages"
        [string]$nugetSource = "https://api.nuget.org/v3/index.json"
        [string]$nugetDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"

        if (!($env:path -contains ";$pwd;$psscriptroot;$env:temp")) { 
            $env:path += ";$pwd;$psscriptroot;$env:temp" 
        } 

        if (!(test-path nuget)) {
            (new-object net.webclient).downloadFile($nugetDownloadUrl, "$pwd\nuget.exe")
        }

        [io.directory]::createDirectory($outputDirectory)
        [string]$localPackages = nuget list -Source $outputDirectory

        if (!$kusto.force -and !($localPackages -imatch $projectName)) {
            write-host ".\nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed"
            nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed
        }
        else {
            write-host "$projectName already installed" -ForegroundColor green
        }

        [string]$projectDirectory = "$outputDirectory\$projectName"
        tree /a /f $projectDirectory
        $kusto.adalDll = @(get-childitem -Path $projectDirectory -Recurse | where-object FullName -match "net45\\$projectName\.dll" | select-object FullName)[-1]

        write-host "install / output dir: $($projectDirectory)" -ForegroundColor Green
        $kusto.adalDll
        $kusto.adalDll.FullName
        #$kusto.adalDll = Add-Type -Path $adalDll.FullName
        $kusto.adalDll = [Reflection.Assembly]::LoadFile($kusto.adalDll.FullName) # Microsoft.IdentityModel.Clients.ActiveDirectory
        $kusto.adalDll
    }

    [KustoObj] CreateResultTable() {
        # not currently handling duplicate column names with case insensitive
        [object]$kusto = $this
        $kusto.resultTable = [collections.arraylist]@()
        $columns = @{ }

        if (!$kusto.resultObject.tables[0]) {
            write-warning "run query first"
            return $this.Pipe()
        }

        foreach ($column in ($kusto.resultObject.tables[0].columns)) {
            [void]$columns.Add($column.ColumnName, $null)
        }

        $resultModel = New-Object -TypeName PsObject -Property $columns

        foreach ($row in ($kusto.resultObject.tables[0].rows)) {
            $count = 0
            $kusto.result = $resultModel.PsObject.Copy()

            foreach ($column in ($kusto.resultObject.tables[0].columns)) {
                $kusto.result.($column.ColumnName) = $row[$count++]
            }

            [void]$kusto.resultTable.add($kusto.result)
        }

        return $this.Pipe()
    }

    [KustoObj] Pipe() {
        if ($this.pipeLine) {
            return $this
        }

        return $null
    }

    [KustoObj] Exec([string]$query) {
        $this.query = $query
        $this.Exec()
        $this.query = $null
        return $this.Pipe()
    }

    [KustoObj] Exec() {
        [object]$kusto = $this
        $startTime = get-date
        $kusto

        if (!$kusto.limit) {
            $kusto.limit = 10000
        }

        if (!$kusto.script -and !$kusto.query) {
            Write-Warning "-script and / or -query should be set. exiting"
            return $this.Pipe()
        }

        if (!$kusto.cluster -or !$kusto.database) {
            Write-Warning "-cluster and -database have to be set once. exiting"
            return $this.Pipe()
        }

        if ($kusto.query) {
            write-host "query:$($kusto.query.substring(0, [math]::min($kusto.query.length,60)))" -ForegroundColor Cyan
        }

        if ($kusto.script) {
            write-host "script:$($kusto.script)" -ForegroundColor Cyan
        }

        $kusto.resultObject = $this.Post($null)

        if ($kusto.viewResults) {
            $this.CreateResultTable()
            write-host ($kusto.resultTable | out-string)
        }

        if ($kusto.resultFile) {
            out-file -FilePath $kusto.resultFile -InputObject  ($kusto.resultObject | convertto-json -Depth 99)
        }

        $primaryResult = $kusto.resultObject | where-object TableKind -eq PrimaryResult
    
        if ($primaryResult) {
            write-host ($primaryResult.columns | out-string)
            write-host ($primaryResult.Rows | out-string)
        }

        write-host "results: $($kusto.resultObject.tables[0].rows.count) / $(((get-date) - $startTime).TotalSeconds) seconds to execute" -ForegroundColor DarkCyan
        return $this.Pipe()
    }

    [KustoObj] ExecScript([string]$script, [hashtable]$parameters) {
        $this.script = $script
        $this.parameters = $parameters
        $this.ExecScript()
        $this.script = $null
        return $this.Pipe()
    }

    [KustoObj] ExecScript([string]$script) {
        $this.script = $script
        $this.ExecScript()
        $this.script = $null
        return $this.Pipe()
    }

    [KustoObj] ExecScript() {
        [object]$kusto = $this
        if ($kusto.script.startswith('http')) {
            $destFile = "$pwd\$([io.path]::GetFileName($kusto.script))" -replace '\?.*', ''
        
            if (!(test-path $destFile)) {
                Write-host "downloading $($kusto.script)" -foregroundcolor green
                (new-object net.webclient).DownloadFile($kusto.script, $destFile)
            }
            else {
                Write-host "using cached script $($kusto.script)"
            }

            $kusto.script = $destFile
        }
    
        if ((test-path $kusto.script)) {
            $kusto.query = (Get-Content -raw -Path $kusto.script)#.trimend([environment]::newLine) + [environment]::newLine + $kusto.query
        }
        else {
            write-error "unknown script:$($kusto.script)"
            return $this.Pipe()
        }

        $this.Exec()
        return $this.Pipe()
    }

    [void] ExportCsv([string]$exportFile) {
        $this.CreateResultTable()
        $this.resultTable | export-csv -notypeinformation $exportFile
    }

    [void] ExportJson([string]$exportFile) {
        $this.CreateResultTable()
        $this.resultTable | convertto-json -depth 99 | out-file $exportFile
    }

    [void] Import() {
        if ($this.table) {
            $this.Import($this.table)
        }
        else {
            write-warning "set table name first"
            return
        }
    }

    [void] Import([string]$table) {
        if (!$this.resultObject.Tables) {
            write-warning 'no results to import'
            return
        }

        [object]$results = $this.resultObject.Tables[0]
        [string]$formattedHeaders = "("

        foreach ($column in ($results.Columns)) {
            $formattedHeaders += "['$($column.ColumnName)']:$($column.DataType.tolower()), "
        }
        
        $formattedHeaders = $formattedHeaders.trimend(', ')
        $formattedHeaders += ")"

        [text.StringBuilder]$csv = New-Object text.StringBuilder

        foreach ($row in ($results.rows)) {
            $csv.AppendLine($row -join ',')
        }

        $this.Exec(".drop table ['$table'] ifexists")
        $this.Exec(".create table ['$table'] $formattedHeaders") #(TableName:string, DatabaseName:string, Folder:string, Docstring:string)")
        $this.Exec(".ingest inline into table ['$table'] <| $($csv.tostring())")

        return
        $tempFile = "$pwd\import.csv"
        $this.ExportCsv($tempFile)
        $this.ImportCsv($tempFile, $table)
        remove-item $tempFile
    }

    [void] ImportCsv([string]$importFile, [string]$table) {
        [object]$kusto = $this
        $kusto.table = $table
        $this.ImportCsv($importFile)
    }

    [void] ImportCsv([string]$importFile) {
        if (!(test-path $importFile) -or !$this.table) {
            write-warning "verify importfile: $importFile and import table: $($this.table)"
            return
        }
        
        # not working
        #POST https://help.kusto.windows.net/v1/rest/ingest/Test/Logs?streamFormat=Csv HTTP/1.1
        #[string]$csv = Get-Content -Raw $importFile -encoding utf8
        #$this.Post($csv)

        $sr = new-object io.streamreader($importFile) # [io.file]::ReadAllLines($importFile)
        [string]$headers = $sr.ReadLine()
        [text.StringBuilder]$csv = New-Object text.StringBuilder

        while ($sr.peek() -ge 0) {
            $csv.AppendLine($sr.ReadLine())
        }

        $sr.close()
        [string]$formattedHeaders = "("

        foreach ($header in ($headers.Split(',').trim())) {
            $formattedHeaders += "['$($header.trim('`"'))']:string, "
        }
        
        $formattedHeaders = $formattedHeaders.trimend(', ')
        $formattedHeaders += ")"

        $this.Exec(".drop table ['$($this.table)'] ifexists")
        $this.Exec(".create table ['$($this.table)'] $formattedHeaders") #(TableName:string, DatabaseName:string, Folder:string, Docstring:string)")
        $this.Exec(".ingest inline into table ['$($this.table)'] <| $($csv.tostring())")
    }

    [void] Logon($resourceUrl) {
        [object]$kusto = $this
        [object]$authenticationContext = $Null
        [object]$promptBehavior = 0 # 0 auto 1 always
        [string]$ADauthorityURL = "https://login.microsoftonline.com/$($kusto.tenant)"

        if (!$resourceUrl) {
            write-warning "-resourceUrl required. example: https://{{kusto cluster}}.kusto.windows.net"
            exit
        }

        if (!$kusto.force -and $kusto.AuthenticationResult.expireson -gt (get-date)) {
            write-verbose "token valid: $($kusto.AuthenticationResult.expireson). use -force to force logon"
            return
        }

        $error.Clear()

        try {
            $authenticationContext = New-Object AuthenticationContext($ADAuthorityURL)    
        }
        catch {
            $this.CheckAdal()
            $authenticationContext = New-Object AuthenticationContext($ADAuthorityURL)
        }

        if ($kusto.clientid -and $kusto.clientSecret) {
            # client id / secret
            $kusto.authenticationResult = $authenticationContext.AcquireTokenAsync($resourceUrl, 
                (new-object ClientCredential($kusto.clientId, $kusto.clientSecret))).Result
        }
        else {
            # user / pass
            $error.Clear()
            $kusto.authenticationResult = $authenticationContext.AcquireTokenAsync($resourceUrl, 
                $kusto.wellknownClientId,
                (new-object Uri($kusto.redirectUri)),
                (new-object PlatformParameters($promptBehavior))).Result # auto
        
            if ($error) {
                # MFA
                $kusto.authenticationResult = $authenticationContext.AcquireTokenAsync($resourceUrl, 
                    $kusto.wellknownClientId,
                    (new-object Uri($kusto.redirectUri)),
                    (new-object PlatformParameters(++$promptBehavior))).Result # always
            }
        }

        write-verbose (convertto-json $kusto.authenticationResult -Depth 99)
        $kusto.token = $kusto.authenticationResult.AccessToken;
        $kusto.token
        $kusto.AuthenticationResult
        write-verbose "results saved in `$kusto.authenticationResult and `$kusto.token"
    }

    [object] Post([string]$body = "") {
        # authenticate to aad first to get token
        [object]$kusto = $this
        [string]$kustoHost = "$($kusto.cluster).kusto.windows.net"
        [string]$kustoResource = "https://$kustoHost"
        [string]$csl = "$($kusto.query)"# | limit $($kusto.limit)"
    
        if ($body -and ($kusto.table)) {
            $uri = "$kustoResource/v1/rest/ingest/$($kusto.database)/$($kusto.table)?streamFormat=Csv&mappingName=CsvMapping"
        }
        elseif ($kusto.query.startswith('.show') -or !$kusto.query.startswith('.')) {
            $uri = "$kustoResource/v1/rest/query"
            $csl = "$($kusto.query) | limit $($kusto.limit)"
        }
        else {
            $uri = "$kustoResource/v1/rest/mgmt"
        }

        $this.Logon($kustoResource)

        if (!$kusto.token) {
            write-error "unable to acquire token. exiting"
            return $error
        }

        $requestId = [guid]::NewGuid().ToString()
        write-verbose "request id: $requestId"

        $header = @{
            'accept'                 = 'application/json'
            'authorization'          = "Bearer $($kusto.token)"
            'content-type'           = 'application/json'
            'host'                   = $kustoHost
            'x-ms-app'               = 'kusto-rest.ps1' 
            'x-ms-user'              = $env:USERNAME
            'x-ms-client-request-id' = $requestId
        } 

        if ($body) {
            $header.Add("content-length", $body.Length)
        }
        else {
            $body = @{
                db         = $kusto.database
                csl        = $csl
                properties = @{
                    Options    = @{
                        queryconsistency = "strongconsistency"
                    }
                    Parameters = $kusto.parameters
                }
            } | ConvertTo-Json
        }

        write-verbose ($header | convertto-json)
        write-verbose $body

        $error.clear()
        $ret = Invoke-WebRequest -Method Post -Uri $uri -Headers $header -Body $body
        write-verbose $ret
    
        if ($error) {
            return $error
        }

        return ($ret.content | convertfrom-json)
    }

    [KustoObj] SetCluster([string]$cluster) {
        $this.cluster = $cluster
        return $this.Pipe()
    }

    [KustoObj] SetDatabase([string]$database) {
        $this.database = $database
        return $this.Pipe()
    }

    [KustoObj] SetPipe([bool]$enable) {
        $this.pipeLine = $enable
        return $this.Pipe()
    }

    [KustoObj] SetTable([string]$table) {
        $this.table = $table
        return $this.Pipe()
    }
}

$global:kusto = [KustoObj]::new()
$kusto.Exec()
write-host "use `$kusto object to set properties and run queries. example: `$kusto.Exec('.show operations')" -ForegroundColor Green
