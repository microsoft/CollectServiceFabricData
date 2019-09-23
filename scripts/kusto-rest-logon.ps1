# azure MFA rest logon
# requires Microsoft.IdentityModel.Clients.ActiveDirectory

using namespace Microsoft.IdentityModel.Clients.ActiveDirectory

param(
    $clientSecret,
    $clientId,
    $tenant = "common",
    $wellknownClientId = "1950a258-227b-4e31-a9cf-717495945fc2", 
    $redirectUri = "urn:ietf:wg:oauth:2.0:oob",
    [ValidateNotNullorEmpty()]
    $resourceUrl, # = "https://{{kusto cluster}}.kusto.windows.net"
    [switch]$force
)

Function GetAuthToken
{
    if(!$force -and $global:kustoAuthenticationResult.expireson -gt (get-date))
    {
        write-host "token still valid. use -force to force logon" -ForegroundColor cyan
        write-host "$global:kustoAuthenticationResult"
        exit
    }

    $error.Clear()
    $env:path += ";$pwd;$psscriptroot"
    $ADauthorityURL = "https://login.microsoftonline.com/$tenant"
    $authenticationContext = New-Object AuthenticationContext($ADAuthorityURL)

    if($error)
    {
        $error.Clear()

        if(!(test-path nuget))
        {
            write-warning "installing nuget"
            (new-object net.webclient).downloadFile($nugetDownloadUrl, "$pwd\nuget.exe")
        }

        write-warning "installing Microsoft.IdentityModel.Clients.ActiveDirectory"
        nuget install Microsoft.IdentityModel.Clients.ActiveDirectory -outputdirectory "$($env:USERPROFILE)\.nuget\packages", 
        $authenticationContext = New-Object AuthenticationContext($ADAuthorityURL)

        if($error)
        {
            write-error "unable to find / load Microsoft.IdentityModel.Clients.ActiveDirectory"
            exit
        }
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

    $VerbosePreference = $DebugPreference = $ErrorActionPreference = "silentlycontinue"
    return $authenticationResult
}

$global:kustoAuthenticationResult = GetAuthToken $tenant
$global:kustoAuthenticationResult
write-host "results saved in `$global:kustoAuthenticationResult `$global:tenantid and `$global:token" -ForegroundColor green

