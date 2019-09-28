# script to test nuget package from nuget.org

param(
    $projectName = "Microsoft.ServiceFabric.Support.CollectSFData",
    $nugetSource = "https://api.nuget.org/v3/index.json",
    $nugetDownloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe",
    $outputDirectory = "$($env:USERPROFILE)\downloads\$($ProjectName)",
    $apiKey
)

if(!($env:path.contains(";$pwd;$psscriptroot"))) { $env:path += ";$pwd;$psscriptroot" } 

if(!(test-path nuget))
{
    (new-object net.webclient).downloadFile($nugetDownloadUrl, "$pwd\nuget.exe")
}

# nuget sources Add -Name $projectName -Source $nugetSource
# nuget setapikey $apiKey -Source "GPR" -Verbosity detailed
# nuget pack "..\$($projectName)\$($projectName).nuspec" -OutputDirectory $outputDirectory -apikey $apiKey

[io.directory]::createDirectory($outputDirectory)
write-host ".\nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed -prerelease"
nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed -prerelease

# nuget sources Remove -Name $projectName -Source $nugetSource
tree /a /f $outputDirectory
$exePath = @((get-childitem -Path $outputDirectory -Filter "collectsfdata.exe" -Recurse | ? FullName -match "bin.+release").FullName)[-1]
. $exePath
write-host "install / output dir: $($outputDirectory)" -ForegroundColor Green



