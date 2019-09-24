# azure MFA rest logon
# requires Microsoft.IdentityModel.Clients.ActiveDirectory

using namespace Microsoft.IdentityModel.Clients.ActiveDirectory

param(
    $clientSecret,
    $clientId,
    $tenant = "common",
    $wellknownClientId = "1950a258-227b-4e31-a9cf-717495945fc2", 
    $redirectUri = "urn:ietf:wg:oauth:2.0:oob",
    $resourceUrl, # = "https://{{kusto cluster}}.kusto.windows.net"
    [switch]$force
)

$ErrorActionPreference = "continue"

function main()
{
    if(!$resourceUrl)
    {
        write-warning "-resourceUrl required. example: https://{{kusto cluster}}.kusto.windows.net"
        exit
    }

    if(!$force -and $global:kustoAuthenticationResult.expireson -gt (get-date))
    {
        write-host "token valid: $($global:kustoAuthenticationResult.expireson). use -force to force logon" -ForegroundColor cyan
        #write-host "$global:kustoAuthenticationResult"
        exit
    }

    $error.Clear()
    if(!($env:path -contains ";$pwd;$psscriptroot")) { $env:path += ";$pwd;$psscriptroot" } 
    $ADauthorityURL = "https://login.microsoftonline.com/$tenant"

    try 
    {
        $authenticationContext = New-Object AuthenticationContext($ADAuthorityURL)    
    }
    catch 
    {
        check-adal
        $authenticationContext = New-Object AuthenticationContext($ADAuthorityURL)    
    }

    if ($clientid -and $clientSecret)
    {
        # client id / secret
        $authenticationResult = $authenticationContext.AcquireTokenAsync($resourceUrl, 
            (new-object ClientCredential($clientId, $clientSecret))).Result
    }
    else
    {
        # user / pass
        $error.Clear()
        $authenticationResult = $authenticationContext.AcquireTokenAsync($resourceUrl, 
            $wellknownClientId,
            (new-object Uri($redirectUri)),
            (new-object PlatformParameters([PromptBehavior]::Auto))).Result
        
        if($error)
        {
            # MFA
            $authenticationResult = $authenticationContext.AcquireTokenAsync($resourceUrl, 
            $wellknownClientId,
            (new-object Uri($redirectUri)),
            (new-object PlatformParameters([PromptBehavior]::Always))).Result
        }
    }

    write-verbose (convertto-json $authenticationResult -Depth 99)
    $global:token = $authenticationResult.AccessToken;
    $global:token

    $global:kustoAuthenticationResult = $authenticationResult
    $global:kustoAuthenticationResult
    write-host "results saved in `$global:kustoAuthenticationResult `$global:tenantid and `$global:token" -ForegroundColor green
}

function check-adal()
{

    param(
        $projectName = "Microsoft.IdentityModel.Clients.ActiveDirectory",
        $outputDirectory = "$($env:USERPROFILE)\.nuget\packages", #\$($ProjectName)",
        $nugetSource = "https://api.nuget.org/v3/index.json",
        $nugetDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe",
        [switch]$force
    )

    if(!($env:path -contains ";$pwd;$psscriptroot;$env:temp")) { $env:path += ";$pwd;$psscriptroot;$env:temp" } 

    if(!(test-path nuget))
    {
        (new-object net.webclient).downloadFile($nugetDownloadUrl, "$pwd\nuget.exe")
    }

    [io.directory]::createDirectory($outputDirectory)
    $localPackages = nuget list -Source $outputDirectory

    if(!$force -and !($localPackages -imatch $projectName))
    {
        write-host ".\nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed -prerelease"
        nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed #-prerelease
    }
    else 
    {
        write-host "$projectName already installed" -ForegroundColor green
    }

    $projectDirectory = "$outputDirectory\$projectName"
    tree /a /f $projectDirectory
    $adalDll = @(get-childitem -Path $projectDirectory -Recurse | where-object FullName -match "net45\\$projectName\.dll" | select-object FullName)[-1]

    write-host "install / output dir: $($projectDirectory)" -ForegroundColor Green
    $adalDll
    $adalDll.FullName
    #$global:adal = Add-Type -Path $adalDll.FullName
    $global:adalDll = [Reflection.Assembly]::LoadFile($adalDll.FullName) # Microsoft.IdentityModel.Clients.ActiveDirectory

    $global:adalDll
}

main