# script to test nuget package from nuget.org

param(
    $projectName = "Microsoft.ServiceFabric.Support.CollectSFData",
    $nugetSource = "https://api.nuget.org/v3/index.json",
    $outputDirectory = "$($env:USERPROFILE)\downloads\$($ProjectName)",
    $apiKey
)

$currentLocation = get-location
set-location $PSScriptRoot

if(![io.file]::exists("$pwd\nuget.exe"))
{
    (new-object net.webclient).downloadFile("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe", "$PSScriptRoot\nuget.exe")
}

# .\nuget.exe sources Add -Name $projectName -Source $nugetSource
# .\nuget.exe setapikey $apiKey -Source "GPR" -Verbosity detailed
# .\nuget.exe pack "..\$($projectName)\$($projectName).nuspec" -OutputDirectory $outputDirectory -apikey $apiKey

[io.directory]::createDirectory($outputDirectory)
write-host ".\nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed -prerelease"
.\nuget.exe install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed -prerelease

# .\nuget.exe sources Remove -Name $projectName -Source $nugetSource
tree /a /f $outputDirectory
$exePath = @((get-childitem -Path $outputDirectory -Filter "collectsfdata.exe" -Recurse | ? FullName -match "bin.+release").FullName)[-1]
. $exePath
set-location $currentLocation
write-host "install / output dir: $($outputDirectory)" -ForegroundColor Green



