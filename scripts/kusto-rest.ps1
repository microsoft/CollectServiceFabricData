# script to query kusto with AAD client id and secret or user and password

using namespace Microsoft.IdentityModel.Clients.ActiveDirectory
[cmdletbinding()]
param(
    [string]$query = '.show tables',
    [string]$cluster,
    [string]$database,
    [string]$resultFile = ".\result.json",
    [bool]$viewResults,
    [bool]$gridViewResults,
    [string]$token,
    [int]$limit,
    [string]$script,
    [string]$clientSecret,
    [string]$clientId,
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
    [bool]$gridViewResults = $gridViewResults
    [int]$limit = $limit
    [hashtable]$parameters = $parameters
    [string]$query = $query
    [string]$redirectUri = $redirectUri
    [object]$result = $null
    [object]$resultObject = $null
    [object]$resultTable = $null
    [string]$resultFile = $resultFile
    [string]$script = $script
    [string]$tenant = $tenant
    [string]$token = $token
    [bool]$viewResults = $viewResults
    [string]$wellknownClientId = $wellknownClientId
        

    [void] Exec([string]$query) {
        $this.query = $query
        $this.Exec()
    }

    [void] Exec() {
        [object]$kusto = $this
        $startTime = get-date
        $kusto

        if (!$kusto.limit) {
            $kusto.limit = 10000
        }

        if (!$kusto.script -and !$kusto.query) {
            Write-Warning "-script and / or -query should be set. exiting"
            return
        }

        if (!$kusto.cluster -or !$kusto.database) {
            Write-Warning "-cluster and -database have to be set once. exiting"
            return
        }

        if ($kusto.script) {
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
                $kusto.query = (Get-Content -raw -Path $kusto.script).trimend([environment]::newLine) + [environment]::newLine + $kusto.query
            }
            else {
                write-error "unknown script:$($kusto.script)"
                return
            }
        }

        # authenticate to aad first to get token
        $kustoHost = "$($kusto.cluster).kusto.windows.net"
        $kustoResource = "https://$kustoHost"
    
        if ($kusto.query.startswith('.show') -or !$kusto.query.startswith('.')) {
            $uri = "$kustoResource/v1/rest/query"
        }
        else {
            $uri = "$kustoResource/v1/rest/mgmt"
        }

        $this.Logon($kustoResource)

        if (!$kusto.token) {
            write-error "unable to acquire token. exiting"
            return
        }

        $requestId = [guid]::NewGuid().ToString()
        write-host "request id: $requestId"

        $header = @{
            "Accept"                 = "application/json"
            "Authorization"          = "Bearer $($kusto.token)"
            "Content-Type"           = "application/json"
            "Host"                   = $kustoHost
            "x-ms-app"               = "kusto-rest" #[io.path]::GetFileName($MyInvocation.ScriptName)
            "x-ms-user"              = $env:USERNAME
            "x-ms-client-request-id" = $requestId
        } 

        write-verbose $header | convertto-json

        $body = @{
            db         = $kusto.database
            csl        = "$($kusto.query) | limit $($kusto.limit)"
            properties = @{
                Options    = @{
                    queryconsistency = "strongconsistency"
                }
                Parameters = $kusto.parameters
            }
        } | ConvertTo-Json

        write-verbose $body

        $error.clear()
        $ret = Invoke-WebRequest -Method Post -Uri $uri -Headers $header -Body $body
        write-verbose $ret
    
        if ($error) {
            return
        }

        $kusto.resultObject = $ret.content | convertfrom-json
        $kusto.result = $kusto.resultObject | convertto-json -Depth 99

        if ($kusto.viewResults) {
            $kusto.resultTable = [collections.arraylist]@()
            $columns = @{ }
    
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

            write-host ($kusto.resultTable | out-string)
            #write-output $kusto.resultTable

            if ($kusto.gridViewResults) {
                $kusto.resultTable | select-object * | out-gridview
            }

            write-host "results count: $($kusto.resultTable.Count)"
        }

        out-file -FilePath $kusto.resultFile -InputObject $kusto.result

        $primaryResult = $kusto.resultObject | where-object TableKind -eq PrimaryResult
    
        if ($primaryResult) {
            write-host ($primaryResult.columns | out-string)
            write-host ($primaryResult.Rows | out-string)
        }

        write-host "use `$kusto object to set properties and run queries. example: `$kusto.Exec('.show operations')" -ForegroundColor Green
        write-host "$(((get-date) - $startTime).TotalSeconds) seconds to execute"
    }

    [void] Logon($resourceUrl) {
        [object]$kusto = $this
        [object]$authenticationContext = $Null
        [object]$promptBehavior = $null
        [string]$ADauthorityURL = "https://login.microsoftonline.com/$($kusto.tenant)"

        if (!$resourceUrl) {
            write-warning "-resourceUrl required. example: https://{{kusto cluster}}.kusto.windows.net"
            exit
        }

        if (!$kusto.force -and $kusto.AuthenticationResult.expireson -gt (get-date)) {
            write-host "token valid: $($kusto.AuthenticationResult.expireson). use -force to force logon" -ForegroundColor cyan
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
                (new-object PlatformParameters(0))).Result # auto
        
            if ($error) {
                # MFA
                $kusto.authenticationResult = $authenticationContext.AcquireTokenAsync($resourceUrl, 
                    $kusto.wellknownClientId,
                    (new-object Uri($kusto.redirectUri)),
                    (new-object PlatformParameters(1))).Result # always
            }
        }

        write-verbose (convertto-json $kusto.authenticationResult -Depth 99)
        $kusto.token = $kusto.authenticationResult.AccessToken;
        $kusto.token
        $kusto.AuthenticationResult
        write-host "results saved in `$kustoAuthenticationResult `$global:tenantid and `$global:token" -ForegroundColor green
    }

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
            write-host ".\nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed -prerelease"
            nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed #-prerelease
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

}

$global:kusto = [KustoObj]::new()
$kusto.Exec()