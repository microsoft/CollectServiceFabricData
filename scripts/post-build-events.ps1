<#
post build event script called from CollectSFDataDll.csproj
#>
param(
    $projectDir,
    $outdir
)

$ErrorActionPreference = 'continue'
$PSModuleAutoLoadingPreference = 2
$currentDir = (get-location).path
$scriptDir = $PSScriptRoot

function main() {
    write-host "starting post-build-event.ps1"
    write-host "projectDir:$projectDir"
    write-host "outDir:$outDir"
    write-host "current dir:$currentDir"
    write-host "scriptDir:$scriptDir"

    $manifestPath = "$projectDir\..\..\manifests"
    $defaultOptionsPath = "$projectDir\..\..\configurationFiles"
    $defaultOptionsFile = "$defaultOptionsPath\collectsfdata.options.json"

    $manifestIndex = "$manifestPath\index.json"
    [object]$root = @{}
    $root.manifests = (Get-ChildItem -Filter "*.man" -path $manifestpath).Name
    $manifestJson = $root | convertto-json 
    $currentManifestJson = Get-Content -raw $manifestIndex
    $manifestOutDir = "$outDir\manifests"

    if ([string]::Compare($manifestJson, $currentManifestJson) -ne 0) {
        write-host "updating $manifestIndex" -ForegroundColor Magenta
        $manifestJson | out-file -path $manifestIndex
    }

    if (!(test-path $manifestOutDir)) {
        mkdir $manifestOutDir
    }

    write-host "copying manifests $manifestPath to $outputDir"
    foreach ($manifest in $manifests) {
        Copy-Item $manifest.FullName "$outDir\manifests"
    }

    write-host "copying default options file $defaultOptionsFile"
    Copy-Item $defaultOptionsFile $outDir
}

main