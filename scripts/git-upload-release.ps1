# script to create git release and upload file to release
# https://developer.github.com/v3/repos/releases/#create-a-release

[cmdletbinding()]
param(
    $user = $null,
    $repo = $null,
    $token = $null,
    $contentType = "application/zip",
    $branch = $null,
    $releaseName = "",
    $releaseDescription = "$releaseName $(get-date)",
    $tagName = "$releaseName-$((get-date -Format o).replace(":","-").replace(".","-"))",
    $prerelease = $branch -ne "master",
    $file = $null
)

$githubUri = "https://api.github.com/repos"

function main()
{
    $VerbosePreference = $DebugPreference = "continue"
    write-host $PSBoundParameters -ForegroundColor Yellow

    if($user -and $repo)
    {
        $repoUri = "$githubUri/$user/$repo"
    }
    else
    {
        $repoUri = "$githubUri/$env:GITHUB_REPOSITORY"
    }

    if(!$branch)
    {
        $branch = ($env:GITHUB_REF).replace("refs/heads/","")
    }

    # create
    $createResults = invoke-rest -method post `
        -uri "$repoUri/releases" `
        -body (@{ 
            tag_name         = $tagName
            target_commitish = $branch
            name             = $releaseName
            body             = $releaseDescription
            draft            = $false
            prerelease       = $prerelease
        } | ConvertTo-Json)

    # upload
    if ($createResults.upload_url)
    {
        $uploadUrl = $createResults.upload_url.replace("{?name,label}","")
        invoke-rest -method post `
            -uri "$($uploadUrl)?name=$([io.path]::GetFileName($file))&label=$($releaseName)" `
            -inFile $file
    }

    $VerbosePreference = $DebugPreference = "silentlycontinue"
}

function invoke-rest($uri, $method, $body = "", $inFile = $null)
{
    write-host "invoking:$uri" -ForegroundColor Cyan

    $headers = @{
        'authorization' = "token $token" 
        'contentType'   = "application/json"
    }

    $params = @{ 
        ContentType = $contentType
        Headers     = $headers
        Method      = $method
        uri         = $uri
        timeoutsec  = 600
    }

    if ($method -imatch "post")
    {
        if ($inFile)
        {
            [void]$params.Add('inFile', $inFile)
        }
        elseif ($body) 
        {
            [void]$params.Add('body', $body)
        }
    }

    write-verbose ($params | out-string)
    $error.Clear()
    $response = Invoke-RestMethod @params -Verbose -Debug
    write-verbose "response: $response"
    write-host $error

    $error.Clear()
    $json = convertto-json -InputObject $response -ErrorAction SilentlyContinue 
    write-host $json -ForegroundColor Green 

    if ($error)
    {
        write-host ($response) -ForegroundColor DarkGreen
        $error.Clear()
    }

    $global:response = $response
    return $response
}

main