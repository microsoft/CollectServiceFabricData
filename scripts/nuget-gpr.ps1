# script to test nuget package from github package registry gpr
# https://help.github.com/en/articles/configuring-nuget-for-use-with-github-package-registry

param(
    $projectName = "Microsoft.ServiceFabric.CollectSFData",
    $nugetSource = "https://nuget.pkg.github.com/microsoft/index.json",
    $outputDirectory = "$([io.path]::GetDirectoryName($MyInvocation.MyCommand.Path))\..\CollectSFData\bin\X64\Release\$($ProjectName)"
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



