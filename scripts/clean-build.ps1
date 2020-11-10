$error.clear()
$ErrorActionPreference = 'continue'
$projectDir = resolve-path "$psscriptroot\..\src"
$projectDirs = @("$projectDir", "$projectDir\CollectSFData", "$projectDir\CollectSFDataDll", "$projectDir\CollectSFDataTest")

write-host "project dir: $projectDir" -ForegroundColor Green

foreach ($dir in $projectDirs) {
    write-host "checking $dir" -ForegroundColor Green

    if ((test-path "$dir\bin")) {
        write-warning "removing $dir\bin"
        rd "$dir\bin" -re -fo
    }

    if ((test-path "$dir\obj")) {
        write-warning "removing $dir\obj"
        rd "$dir\obj" -re -fo
    }
}
