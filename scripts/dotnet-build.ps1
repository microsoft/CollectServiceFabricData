<#
.SYNOPSIS    script to build different frameworks with dotnet

#>
param(
    [ValidateSet('net472', 'netcoreapp2.2', 'netcoreapp3.1', 'net5.0', 'net462')]
    [string[]]$targetFrameworks = @('net472', 'netcoreapp3.1', 'net5.0', 'net462'),
    [ValidateSet('all', 'debug', 'release')]
    $configuration = 'all',
    [ValidateSet('win-x64', 'ubuntu.18.04-x64')]
    $runtimeIdentifier = 'win-x64',
    [switch]$publish,
    [string]$projectDir = (resolve-path "$psscriptroot/../src"),
    [string]$nugetFallbackFolder = "$($env:userprofile)/.dotnet/NuGetFallbackFolder",
    [switch]$clean,
    [switch]$replace
)

$ErrorActionPreference = 'continue'

$error.Clear()
$global:tempFiles = [collections.arraylist]::new()
$csproj = "$projectDir/CollectSFData/CollectSFData.csproj"
$dllcsproj = "$projectDir/CollectSFDataDll/CollectSFDataDll.csproj"
$frameworksPattern = "\<TargetFrameworks\>(.+?)\</TargetFrameworks\>"
$ignoreCase = [text.regularExpressions.regexOptions]::IgnoreCase
$nuspecFile = "$projectDir/CollectSFData/CollectSFData.nuspec"
$xmlns = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"

$commonNugetFiles = @{
    '../bin/$configuration$/$targetFramework/*.exe'                = 'tools/$targetFramework'
    '../bin/$configuration$/$targetFramework/*.dll'                = 'tools/$targetFramework'
    '../bin/$configuration$/$targetFramework/CollectSFDataDll.dll' = 'lib/$targetFramework'
    '../bin/$configuration$/$targetFramework/Sf.Tx.Core.dll'       = 'lib/$targetFramework'
    '../bin/$configuration$/$targetFramework/Sf.Tx.Windows.dll'    = 'lib/$targetFramework'
    '../../configurationFiles/collectsfdata.options.json'          = 'tools/$targetFramework'
    '../bin/$configuration$/$targetFramework/*.config'             = 'tools/$targetFramework'
    '../FabricSupport.png'                                         = 'images/'
}

$netCoreNugetFiles = @{
    '../bin/$configuration$/$targetFramework/*.runtimeconfig.json' = 'tools/$targetFramework'
}

function main() {

    if ($clean) {
        . "$psscriptroot/clean-build.ps1"
    }

    try {
        $csproj = create-tempProject -projectFile $csproj
        $dllcsproj = create-tempProject -projectFile $dllcsproj
        $nuspecFile = create-nuspec $targetFrameworks
        
        write-host "dotnet restore $csproj" -ForegroundColor Green
        dotnet restore $csproj

        if ($configuration -ieq 'all') {
            build-configuration 'debug'
            build-configuration 'release'
        }
        else {
            build-configuration $configuration
        }
        return
    }
    catch {
        write-error "exception:$($_ | Format-List * | out-string)"
    }
    finally {
        if ($global:tempFiles) {
            foreach ($file in $global:tempFiles) {
                if (!(test-path $file)) {
                    write-error "original csproj missing"
                }
                else {
                    $tempFile = $file.replace(".oem", "")
                    write-host "removing temp file $tempFile" -ForegroundColor Cyan
                    remove-item $tempFile -Force

                    write-host "renaming original file $file" -ForegroundColor Cyan
                    rename-Item $file $tempFile -Force
                }
            }
        }
    }
}

function build-configuration($configuration) {
    write-host "dotnet list $csproj package"
    dotnet list $csproj package

    write-host "dotnet build $csproj -c $configuration" -ForegroundColor Magenta
    dotnet build $csproj -c $configuration

    if ($publish) {
        write-host "dotnet publish $csproj -f $targetFrameworks -r $runtimeIdentifier -c $configuration --self-contained $true -p:PublishSingleFile=true -p:PublishedTrimmed=true" -ForegroundColor Magenta
        dotnet publish $csproj -f $targetFrameworks -r $runtimeIdentifier -c $configuration --self-contained $true -p:PublishSingleFile=true -p:PublishedTrimmed=true
    }

    $nugetFile = "$projectDir/bin/$configuration/*.nupkg"
    $nugetFile = (resolve-path $nugetFile)[-1]
    
    if ((test-path $nugetFile)) {
        write-host "nuget add $nugetFile -source $nugetFallbackFolder" -ForegroundColor Green
        nuget add $nugetFile -source $nugetFallbackFolder
    }
}

function create-nuspec($targetFrameworks) {
    if (!(test-path $nuspecFile)) {
        write-error "nuspec $nuspecFile does not exist"
        return
    }
    $tempNuspec = $nuspecFile.Replace(".oem", "").Replace(".nuspec", ".nuspec.oem")
    write-host "adding temp file $tempNuspec to list" -ForegroundColor Yellow
    [void]$global:tempFiles.add($tempNuspec)
    
    $nuspecXml = [xml]::new()
    $nuspecXml.Load($nuspecFile)
    $nuspecXml.Save($tempNuspec)

    $nuspecXml.package.files.RemoveAll()
    $filesElement = $nuspecxml.package.GetElementsByTagName("files")

    foreach ($targetFramework in $targetFrameworks) {
        foreach ($commonNugetFile in $commonNugetFiles.GetEnumerator()) {
            $srcPath = $commonNugetFile.Key.Replace("`$targetFramework", $targetFramework)
            $targetPath = $commonNugetFile.Value.Replace("`$targetFramework", $targetFramework)

            $element = $nuspecXml.CreateElement("file", $xmlns)
            $src = $nuspecXml.CreateAttribute("src")
            $src.Value = $srcPath
            $element.Attributes.Append($src)

            $target = $nuspecXml.CreateAttribute("target")
            $target.Value = $targetPath
            $element.Attributes.Append($target)

            $filesElement.AppendChild($element)
        }
        
        if ($targetFramework -imatch "netcore") {
            foreach ($netCoreNugetFile in $netCoreNugetFiles.GetEnumerator()) {
                $srcPath = $netCoreNugetFile.Key.Replace("`$targetFramework", $targetFramework)
                $targetPath = $netCoreNugetFile.Value.Replace("`$targetFramework", $targetFramework)
    
                $element = $nuspecXml.CreateElement("file")
                $src = $nuspecXml.CreateAttribute("src")
                $src.Value = $srcPath
                $element.Attributes.Append($src)
    
                $target = $nuspecXml.CreateAttribute("target")
                $target.Value = $targetPath
                $element.Attributes.Append($target)
    
                $filesElement.AppendChild($element)
            }
        }
    }

    $nuspecXml.Save($nuspecFile)
    return $tempNuspec
}

function create-tempProject($projectFile) {
    $projContent = Get-Content -raw $projectFile
    $targetFrameworkString = @($targetFrameworks) -join ";"
    $tempProject = $projectFile.Replace(".oem", "").Replace(".csproj", ".csproj.oem")
    
    write-host "saving to $tempProject" -ForegroundColor Green
    $projContent.trim() | out-file $tempProject -Force

    if (!([regex]::IsMatch($projContent, ">$targetFrameworkString<", $ignoreCase))) {
        $currentFrameworks = [regex]::Match($projContent, $frameworksPattern, $ignoreCase).Groups[1].Value
        write-host "current frameworks: $currentFrameworks" -ForegroundColor Green
        
        if ($replace) {
            write-host "replacing target framework to csproj: $targetFrameworkString" -ForegroundColor Green
            $projContent = [regex]::Replace($projContent, $currentFrameworks, $targetFrameworkString, $ignoreCase)
        }
        else {
            write-host "adding target framework to csproj: $targetFrameworkString" -ForegroundColor Green
            $projContent = [regex]::Replace($projContent, $currentFrameworks, "$currentFrameworks;$targetFrameworkString", $ignoreCase)
        }
        
        write-host "new frameworks: $projContent" -ForegroundColor Green
        write-host "saving to $projectFile" -ForegroundColor Green
        $projContent.trim() | out-file $projectFile -Force
        write-host "adding temp file $tempProject to list" -ForegroundColor Yellow
        [void]$global:tempFiles.add($tempProject)
        
        return $projectFile
    }

    return $projectFile
}

main
