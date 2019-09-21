# script to test nuget package from azure devops build artifacts

param(
    $projectName = "Microsoft.ServiceFabric.Support.CollectSFData",
    $nugetSource = "https://api.nuget.org/v3/index.json",
    $outputDirectory = "$($env:USERPROFILE)\downloads\$($ProjectName)"
)

$currentLocation = get-location
set-location $PSScriptRoot

if(![io.file]::exists("$PSScriptRoot\nuget.exe"))
{
    (new-object net.webclient).downloadFile("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe", "$pwd\nuget.exe")
}

# nuget pack "..\$($projectName)\$($projectName).nuspec" -OutputDirectory $outputDirectory
# nuget sources Add -Name $projectName -Source $nugetSource
[io.directory]::createDirectory($outputDirectory)
write-host ".\nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed -prerelease"
.\nuget install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed -prerelease

# nuget sources Remove -Name $projectName -Source $nugetSource
tree /a /f $outputDirectory
$exePath = @((get-childitem -Path $outputDirectory -Filter "collectsfdata.exe" -Recurse | ? FullName -match "bin.+release").FullName)[-1]
. $exePath
set-location $currentLocation
write-host "install / output dir: $($outputDirectory)" -ForegroundColor Green



