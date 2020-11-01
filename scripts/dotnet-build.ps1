
<#
.SYNOPSIS    script to build different frameworks with dotnet

#>
param(
    [ValidateSet('net472', 'netcoreapp2.2', 'netcoreapp3.1', 'net5')]
    $targetFramework = 'net5',
    [ValidateSet('debug', 'release')]
    $configuration = 'release',
    [ValidateSet('win-x64', 'ubuntu.18.04-x64')]
    $runtimeIdentifier = 'win-x64',
    [switch]$publish,
    [switch]$clean
)

$ErrorActionPreference = 'continue'

$error.Clear()
$global:tempFiles = [collections.arraylist]::new()
$csproj = "$psscriptroot\..\CollectSFData\CollectSFData.csproj"
$dllcsproj = "$psscriptroot\..\CollectSFDataDll\CollectSFDataDll.csproj"
$frameworksPattern = "\<TargetFrameworks\>(.+?)\</TargetFrameworks\>"
$ignoreCase = [text.regularExpressions.regexOptions]::IgnoreCase

function main() {

    if ($clean) {
        . "$psscriptroot\clean-build.ps1"
    }

    $csproj = create-tempProject -projectFile $csproj
    $dllcsproj = create-tempProject -projectFile $dllcsproj
    
    write-host "dotnet restore $csproj" -ForegroundColor Green
    dotnet restore $csproj

    write-host "dotnet build $csproj -c $configuration" -ForegroundColor Green
    dotnet build $csproj -c $configuration

    if ($publish) {
        write-host "dotnet publish $csproj -f $targetFramework -r $runtimeIdentifier -c $configuration --self-contained $true -p:PublishSingleFile=true -p:PublishedTrimmed=true" -ForegroundColor Green
        dotnet publish $csproj -f $targetFramework -r $runtimeIdentifier -c $configuration --self-contained $true -p:PublishSingleFile=true -p:PublishedTrimmed=true
    }

    if ($global:tempFiles) {
        foreach ($file in $global:tempFiles) {
            write-warning "removing temp file $file"
            remove-item $file -Force
        }
    }
    return
}

function create-tempProject($projectFile) {
    $projContent = Get-Content -raw $projectFile

    if (!([regex]::IsMatch($projContent, $targetFramework, $ignoreCase))) {
        $currentFrameworks = [regex]::Match($projContent, $frameworksPattern, $ignoreCase).Groups[1].Value
        write-host "current frameworks: $currentFrameworks" -ForegroundColor Green
        write-host "copying and adding target framework to csproj $targetFramework" -ForegroundColor Green
        $projContent = [regex]::Replace($projContent, $currentFrameworks, "$currentFrameworks;$targetFramework", $ignoreCase)
        $tempProject = $projectFile.Replace(".csproj", ".$targetFramework.csproj")
        write-host "saving to $tempProject" -ForegroundColor Green
        $projContent | out-file $tempProject
        [void]$global:tempFiles.add($tempProject)
        return $tempProject
    }

    return $projectFile
}

main