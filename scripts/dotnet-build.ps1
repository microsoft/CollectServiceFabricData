<#
.SYNOPSIS    script to build different frameworks with dotnet

#>
param(
    [ValidateSet('net48', 'net6.0', 'net8.0', 'net462')]
    [string[]]$targetFrameworks = @('net48', 'net6.0', 'net8.0', 'net462'),
    [ValidateSet('all', 'debug', 'release')]
    $configuration = 'all',
    [switch]$publish,
    [string]$projectDir = (resolve-path "$psscriptroot/../src").Path,
    [string]$nugetPackageName = 'Microsoft.ServiceFabric.CollectSFData',
    [string]$nugetFallbackFolder = "$($env:userprofile)/.nuget/packages" , #"$($env:userprofile)/.dotnet/NuGetFallbackFolder", # "$($env:userprofile)/.nuget/packages"
    [switch]$clean,
    [switch]$replace
)

$ErrorActionPreference = 'continue'
$runtimeIdentifier = 'win-x64',
$error.Clear()
$global:tempFiles = [collections.arraylist]::new()
$csproj = "$projectDir/CollectSFData/CollectSFData.csproj"
$dllcsproj = "$projectDir/CollectSFDataDll/CollectSFDataDll.csproj"
$frameworksPattern = "\<TargetFrameworks\>(.+?)\</TargetFrameworks\>"
$ignoreCase = [text.regularExpressions.regexOptions]::IgnoreCase
$nuspecFile = "$projectDir/CollectSFData/CollectSFData.nuspec"
$xmlns = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"

$globalNugetFiles = @{
    '../FabricSupport.png' = 'images/'
}

$commonNugetFiles = @{
    '../bin/$configuration$/$targetFramework/*.exe'                     = 'tools/$targetFramework'
    '../bin/$configuration$/$targetFramework/*.dll'                     = 'tools/$targetFramework'
    #'../bin/$configuration$/$targetFramework/*.pdb*'                     = 'tools/$targetFramework'
    '../bin/$configuration$/$targetFramework/manifests/*.man'           = 'tools/$targetFramework/manifests'
    '../../configurationFiles/collectsfdata.options.json'               = 'tools/$targetFramework'
    '../bin/$configuration$/$targetFramework/*.config'                  = 'tools/$targetFramework'
    '../bin/$configuration$/$targetFramework/EtlReader.dll'             = 'lib/$targetFramework'
    '../bin/$configuration$/$targetFramework/System.Fabric.Strings.dll' = 'lib/$targetFramework'
    #'../bin/$configuration$/$targetFramework/manifests/*.man*'          = 'lib/$targetFramework/manifests'
    '../bin/$configuration$/$targetFramework/CollectSFDataDll.dll'      = 'lib/$targetFramework'
    '../bin/$configuration$/$targetFramework/Sf.Tx.Core.dll'            = 'lib/$targetFramework'
    '../bin/$configuration$/$targetFramework/Sf.Tx.Windows.dll'         = 'lib/$targetFramework'
    '../bin/$configuration$/$targetFramework/*.pdb'                     = 'lib/$targetFramework'
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
        rename-nugetConfig
        
        $error.Clear()
        write-host "dotnet restore $csproj" -ForegroundColor Green
        dotnet restore $csproj
        if($error) {
            write-warning "dotnet restore $csproj failed"
            write-host "utility uses central configuration store that requires authentication."
            write-host "artifacts authentication package information: https://github.com/microsoft/artifacts-credprovider#azure-artifacts-credential-provider"
            return
        }

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
                    write-error "original file:$file missing"
                }
                else {
                    $tempFile = $file.replace(".oem", "")
                    if((test-path $tempFile)) {
                        write-host "removing temp file $tempFile" -ForegroundColor Cyan
                        write-host "remove-item $tempFile -Force"
                        remove-item $tempFile -Force
                    }

                    write-host "renaming original file $file" -ForegroundColor Cyan
                    write-host "rename-Item $file $tempFile -Force"
                    rename-Item $file $tempFile -Force
                }
            }
        }
    }
}

function build-configuration($configuration) {
    write-host "dotnet list $csproj package  --include-transitive"
    dotnet list $csproj package  --include-transitive

    write-host "dotnet build $csproj -c $configuration" -ForegroundColor Magenta
    dotnet build $csproj -c $configuration

    if ($publish) {
        write-host "dotnet publish $csproj -f $targetFrameworks -r $runtimeIdentifier -c $configuration --self-contained $true -p:PublishSingleFile=true -p:PublishedTrimmed=true" -ForegroundColor Magenta
        dotnet publish $csproj -f $targetFrameworks -r $runtimeIdentifier -c $configuration --self-contained $true -p:PublishSingleFile=true -p:PublishedTrimmed=true
    }

    $nugetFile = "$projectDir/bin/$configuration/*.nupkg"
    $nugetFile = (resolve-path $nugetFile)[-1]
    $nugetFunctions = "$pwd/nuget-functions.ps1"

    if ((test-path $nugetFile)) {
        if (!(test-path $nugetFunctions)) {
            [net.servicePointManager]::Expect100Continue = $true; [net.servicePointManager]::SecurityProtocol = [net.SecurityProtocolType]::Tls12;
            invoke-webRequest "https://aka.ms/nuget-functions.ps1" -outFile $nugetFunctions;
        }

        . $nugetFunctions
        #write-host "nuget add $nugetFile -source $nugetFallbackFolder" -ForegroundColor Green
        #nuget add $nugetFile -source $nugetFallbackFolder
        $nuget.AddPackage($nugetPackageName, $nugetFile, $nugetFallbackFolder)
    }
}

function rename-nugetConfig() {
    # rename nuget.config to nuget.config.oem to prevent nuget from using it for internal packages
    $nugetConfig = (resolve-path "$projectDir/../nuget.config").Path
    if (!(test-path $nugetConfig)) {
        write-warning "$nugetConfig does not exist"
        return
    }
    $tempConfig = $nugetConfig.Replace(".oem", "").Replace(".config", ".config.oem")
    write-host "renaming $nugetConfig to $tempConfig" -ForegroundColor Yellow
    move-item $nugetConfig $tempConfig -Force
    write-host "adding temp file $tempConfig to list" -ForegroundColor Yellow
    [void]$global:tempFiles.add($tempConfig)

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

    foreach ($globalNugetFile in $globalNugetFiles.GetEnumerator()) {
        $srcPath = $globalNugetFile.Key.Replace("`$targetFramework", $targetFramework)
        $targetPath = $globalNugetFile.Value.Replace("`$targetFramework", $targetFramework)

        $element = $nuspecXml.CreateElement("file", $xmlns)
        $src = $nuspecXml.CreateAttribute("src")
        $src.Value = $srcPath
        $element.Attributes.Append($src)

        $target = $nuspecXml.CreateAttribute("target")
        $target.Value = $targetPath
        $element.Attributes.Append($target)

        $filesElement.AppendChild($element)
    }

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
