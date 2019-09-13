# script to test nuget package from azure devops build artifacts

param(
    $projectName = "Microsoft.ServiceFabric.CollectSFData",
    $nugetSource = "https://pkgs.dev.azure.com/ServiceFabricSupport/_packaging/CollectSFData/nuget/v3/index.json",
    $outputDirectory = "$($env:USERPROFILE)\downloads\$($ProjectName)"
)

set-location $PSScriptRoot
Invoke-WebRequest "https://raw.githubusercontent.com/jagilber/powershellScripts/master/download-nuget.ps1" -UseBasicParsing | Invoke-Expression

# nuget pack "..\$($projectName)\$($projectName).nuspec" -OutputDirectory $outputDirectory
# nuget sources Add -Name $projectName -Source $nugetSource
.\nuget.exe install $projectName -Source $nugetSource -outputdirectory $outputDirectory -verbosity detailed
# nuget sources Remove -Name $projectName -Source $nugetSource
tree /a /f $outputDirectory
$exePath = @((get-childitem -Path $outputDirectory -Filter collectsfdata.exe -Recurse).DirectoryName)
. "$($exePath[-1])\collectsfdata.exe"
write-host "install / output dir: $($outputDirectory)" -ForegroundColor Green



