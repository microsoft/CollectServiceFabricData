<#
.SYNOPSIS
setup variables for running tests
#>
[cmdletbinding()]
param(
    [switch]$clean,
    [switch]$reset,
    [string]$configurationFile = "$psscriptroot\..\bin\temp\collectSfDataTestProperties.json",
    [string]$tempDir = "$psscriptroot\..\bin\temp"
)

$PSModuleAutoLoadingPreference = 2
$ErrorActionPreference = "continue"
$error.clear()

class TestSettings {
    TestSettings() {}
        $testAadUser = $null
        $testAadPassword = $null
        $testAadKeyVault = $null
        $testAadCertificateThumbprint = $null
        $testAadSourceVaultValue = $null
        $aadCertificateUrlValue = $null
      
        # for azure cluster deployments
        $adminUserName = $null
        $adminPassword = $null
      
        # existing collectsfdata variables
        $AzureClientId = $null
        $AzureClientSecret = $null
        $AzureResourceGroup = $null
        $AzureResourceGroupLocation = $null
        $AzureSubscriptionId = $null
        $AzureTenantId = $null
        $KustoCluster = $null
        $SasKey = $null
}

class TestEnv {
    [TestSettings]$testSettings = [TestSettings]::new()
    [switch]$clean = $clean
    [switch]$reset = $reset
    [string]$configurationFile = $configurationFile
    [string]$tempDir = $tempDir
    
    TestEnv() {
        $this.CheckTempDir()
        $this.CheckTemplate()
        $this.ReadConfig($this.configurationFile)
    }

    [void] CheckTempDir(){
        if ((test-path $this.tempDir) -and $this.clean) {
            remove-item $this.tempDir -recurse -force
            new-item -itemType directory -name $this.tempDir
        }
    }

    [void] CheckTemplate(){
        if (!(test-path $this.configurationFile) -or $this.reset) {
            $this.SaveConfig()
            write-host "edit file directly and save: $this.configurationFile" -foregroundcolor green
            start . $this.configurationFile
        }
    }

    [TestSettings] ReadConfig(){
        return $this.ReadConfig($this.configurationFile)
    }

    [TestSettings] ReadConfig([string]$configurationFile){
        $this.testSettings = get-content -raw $configurationFile | convertfrom-json
        write-host "current configuration:`r`n$($global:testProperties | out-string)" -foregroundcolor green
        write-host "current configuration saved in `$global:testProperties" -foregroundcolor green
        
        return $this.testSettings
    }

    [bool] SaveConfig() {
        return $this.SaveConfig($this.testSettings)
    }

    [bool] SaveConfig([TestSettings] $settings = $this.testSettings) {
        $error.Clear()
        out-file -inputobject ($settings | convertto-json) -filepath $this.configurationFile
        write-host "current configuration:`r`n$($settings | out-string)" -foregroundcolor green
        write-host "current configuration saved in $($this.configurationFile)" -foregroundcolor green
    
        return !$error
    }
}

$error.Clear()
$global:testEnv = [TestEnv]::new()
write-host ($PSBoundParameters | out-string)

if ($error) {
    write-warning ($error | out-string)
}
else {
    write-host ($testEnv | Get-Member | out-string)
    write-host "use `$global:testEnv object" -ForegroundColor Green
}
