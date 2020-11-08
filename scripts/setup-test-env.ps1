<#
.SYNOPSIS
setup variables for running tests
#>
[cmdletbinding()]
param(
    [switch]$clean,
    [switch]$reset,
    [string]$configurationFile = "$env:LOCALAPPDATA\collectsfdata\collectSfDataTestProperties.json", # "$psscriptroot\..\bin\temp\collectSfDataTestProperties.json",
    [string]$tempDir = "$psscriptroot\..\bin\temp"
)

$PSModuleAutoLoadingPreference = 2
$ErrorActionPreference = "continue"
$error.clear()

class TestSettings {
    TestSettings() {}
      
    # for file download and gather tests
    [string]$testAzStorageAccount = "collectsfdatatests"
    [string]$testAzClientId = ""
    [string]$testAzClientSecret = ""

    # for azure cluster deployments
    [string]$adminUserName = $null
    [string]$adminPassword = $null
      
    # existing collectsfdata variables
    [string]$AzureClientId = $null
    [string]$AzureClientSecret = $null
    [string]$AzureResourceGroup = $null
    [string]$AzureResourceGroupLocation = $null
    [string]$AzureSubscriptionId = $null
    [string]$AzureTenantId = $null
    [string]$KustoCluster = $null
    [string]$SasKey = $null
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
        
        if ($this.CheckAzureConfig()) {
            $this.CheckKustoConfig()
        }
        
    }

    [bool] CheckAzureConfig() {
        $settings = $this.testSettings
        if (!$settings.AzureClientId -or !$settings.AzureClientSecret -or !$settings.AzureResourceGroup -or !$settings.AzureResourceGroupLocation) {
            Write-Warning "azure settings not configured. storage tests may fail"
            return $false
        }

        <#
        write-host "checking azure config" -ForegroundColor Cyan
        $credential = new-object -typename System.Management.Automation.PSCredential `
            -argumentlist @(
            $settings.AzureClientId, 
            ($settings.AzureClientSecret | convertto-securestring -Force -AsPlainText)
        )
        #>

        if (!(get-module -ListAvailable -Name az.accounts)) {
            install-module Az.Accounts #-UseWindowsPowerShell
            import-module Az.Accounts #-UseWindowsPowerShell
        }

        if (!(get-module -ListAvailable -Name az.storage)) {
            install-module Az.Storage #-UseWindowsPowerShell
            import-module Az.Storage #-UseWindowsPowerShell
        }

        if (!(get-module -ListAvailable -Name Az.Resources)) {
            install-module Az.Resources #-UseWindowsPowerShell
            import-module Az.Resources #-UseWindowsPowerShell
        }

        # bug Could not load type 'System.Security.Cryptography.SHA256Cng' from assembly 'System.Core, Version=4.0.0.0,
        # https://github.com/PowerShell/PowerShell/issues/10473
        # Cng is not in .net core but the az modules havent been updated
        # possible cause is credential
        # need cert to use appid
        #connect-AzAccount -TenantId $settings.AzureTenantId -Credential $credential -ServicePrincipal
        # https://docs.microsoft.com/en-us/powershell/module/az.accounts/connect-azaccount?view=azps-4.7.0#example-7--connect-using-certificates
        # .\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdatatestcert -logonType cert
        write-host "connect-AzAccount -TenantId $($settings.AzureTenantId) `
            -ApplicationId $($settings.testAzClientId) `
            -ServicePrincipal `
            -CertificateThumbprint $($settings.testAzClientSecret)
        "
        connect-AzAccount -TenantId $settings.AzureTenantId `
            -ApplicationId $settings.testAzClientId `
            -ServicePrincipal `
            -CertificateThumbprint $settings.testAzClientSecret
        
        get-azcontext | fl *

        write-host "checking resource group $($settings.AzureResourceGroup)"
        if (!(get-azresourcegroup $settings.AzureResourceGroup -Location $settings.AzureResourceGroupLocation)) {
            if (!(new-azresourcegroup $settings.AzureResourceGroup -Location $settings.AzureResourceGroupLocation)) {
                throw new-object System.UnauthorizedAccessException($error | out-string)
            }
        }

        $settings.testAzStorageAccount = "$($settings.testAzStorageAccount)$([math]::Abs($settings.AzureClientId.GetHashCode()))".Substring(0, 24)
        write-host "setting unique storage account $($settings.testAzStorageAccount)"
        $this.SaveConfig()

        write-host "checking storage account $($settings.testAzStorageAccount)"
        $sa = get-azstorageaccount $settings.testAzStorageAccount -resourcegroupname $settings.AzureResourceGroup
        if (!($sa)) {
            if (!(new-azstorageaccount $settings.testAzStorageAccount -skuname 'Standard_LRS' -resourcegroupname $settings.AzureResourceGroup -Location $settings.AzureResourceGroupLocation)) {
                throw new-object System.UnauthorizedAccessException($error | out-string)
            }
            $sa = get-azstorageaccount $settings.testAzStorageAccount -resourcegroupname $settings.AzureResourceGroup
        }

        write-host "getting key $($settings.testAzStorageAccount)"
        $sk = Get-AzStorageAccountKey -ResourceGroupName $settings.AzureResourceGroup -Name $settings.testAzStorageAccount
        #$sc = new-azstoragecontext -storageaccountname $settings.testAzStorageAccount -storageaccountkey ([convert]::Tobase64String([text.encoding]::Unicode.GetBytes($sk.Value)))
        $sc = new-azstoragecontext -storageaccountname $settings.testAzStorageAccount -storageaccountkey ($sk[0].Value)
        $st = New-AzStorageAccountSASToken -ResourceType Container, Service, Object `
            -Permission 'racwdlup' `
            -Protocol HttpsOnly `
            -StartTime (get-date) `
            -ExpiryTime (get-date).AddHours(8) `
            -Context $sc `
            -Service Blob, Table, File, Queue

        $global:sasuri = "$($sa.Context.BlobEndPoint)$($st)"
        $global:storageAccount = $sa
        write-host "setting test token $global:sasUri"
        $settings.SasKey = $global:sasuri
        $this.SaveConfig()
        return $true
    }

    [bool] CheckKustoConfig() {
        $settings = $this.testSettings
        if (!$settings.AzureClientId -or !$settings.AzureClientSecret -or !$settings.AzureResourceGroup -or !$settings.AzureResourceGroupLocation) {
            Write-Warning "azure settings not configured. kusto tests may fail"
            return $false
        }

        if (!(get-module -ListAvailable -Name Az.Kusto)) {
            install-module Az.Kusto
            import-module Az.Kusto
        }
        
        $pattern = "https://(?<ingest>ingest-){0,1}(?<clusterName>.+?)\.(?<location>.+?)\.(?<domainName>.+?)(/|$)(?<databaseName>.+?){0,1}(/|$)(?<tableName>.+?){0,1}(/|$)"

        if ([regex]::IsMatch($settings.KustoCluster, $pattern)) {
            $results = [regex]::Matches($settings.KustoCluster, $pattern)
            $Global:results = $results
            $ingest = $results[0].Groups['ingest']
            $clusterName = $results[0].Groups['clusterName']
            $location = $results[0].Groups['location']
            $domainName = $results[0].Groups['domainName']
            $databaseName = $results[0].Groups['databaseName']
            $hostName = "$ingest$clusterName.$location.$domainName"

            write-host "hostName: $hostName"
            write-host "ingest: $ingest"
            write-host "clusterName: $clusterName"
            write-host "location: $location"
            write-host "domainName $domainName"
            write-host "databaseName $databaseName"

            write-host "Test-NetConnection -ComputerName $hostName -port 443"
            $pingResults = Test-NetConnection -ComputerName $hostName -port 443 -ErrorAction SilentlyContinue
            $pingResults | convertto-json

            if (!$pingResults.TcpTestSucceeded) {
                write-warning "unable to ping kusto ingest url"
            }
            else {
                $error.Clear()
                write-host "able to ping kusto ingest url"
                return $true
            }

            if ($location -ieq 'kusto') {
                write-host "not a user kusto cluster"
                return $true
            }

            write-host "checking test resource group for kusto cluster"
            $rgClusters = Get-AzKustoCluster -ResourceGroupName $settings.AzureResourceGroup
            $rgClusters
            
            write-host "checking subscriptions for kusto cluster"
            $subClusters = Get-AzKustoCluster -ResourceGroupName $settings.AzureResourceGroup
            $subClusters

            if (!$rgClusters -and !$subClusters) {
                write-warning 'no kusto clusters found. create new kusto cluster with new-azkustocluster command 
                    or provide valid kusto ingest url to test kusto functions'
                write-host "example command: New-AzKustoCluster -Name collectsfdatatest `
                    -ResourceGroupName $($settings.AzureResourceGroup) `
                    -location $($settings.AzureResourceGroupLocation) `
                    -SkuName 'Dev(No SLA)_Standard_D11_v2' `
                    -SkuTier basic `
                    -EnablePurge
                "
            }

            return $false
        }
        else {
            write-warning "unable to determine kusto settings"
            return $false
        }

        return $true
    }

    [void] CheckTempDir() {
        write-host "checking temp dir $($this.tempDir)"
        if ((test-path $this.tempDir) -and $this.clean) {
            remove-item $this.tempDir -recurse -force
            new-item -itemType directory -path $this.tempDir
        }
        elseif (!(test-path $this.tempDir)) {
            new-item -itemType directory -path $this.tempDir
        }
    }

    [void] CheckTemplate() {
        if (!(test-path $this.configurationFile) -or $this.reset) {
            $this.SaveConfig()
            write-host "edit file directly and save: $this.configurationFile" -foregroundcolor green
            write-host "create azure app id / spn for azure storage / gather tests. .\azure-az-create-aad-application-spn.ps1 can be used to create one progammatically."
            write-host ".\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdatatestcert -logonType cert"
            write-host ".\azure-az-create-aad-application-spn.ps1 -aadDisplayName collectsfdatatest -uri http://collectsfdatatest -logontype certthumb"
            . $this.configurationFile
        }
    }

    [TestSettings] ReadConfig() {
        return $this.ReadConfig($this.configurationFile)
    }

    [TestSettings] ReadConfig([string]$configurationFile) {
        $this.testSettings = get-content -raw $configurationFile | convertfrom-json
        write-host "current configuration:`r`n$($global:testProperties | out-string)" -foregroundcolor green
        write-host "current configuration saved in `$global:testProperties" -foregroundcolor green

        return $this.testSettings
    }

    [bool] SaveConfig() {
        return $this.SaveConfig($this.testSettings)
    }

    [bool] SaveConfig([TestSettings] $settings = $this.testSettings) {
       
        $configDir = [io.path]::GetDirectoryName($this.configurationFile)
        if (!(test-path $configDir)) {
            New-Item -ItemType Directory -Path $configDir
        }
        
        $error.Clear()
        out-file -inputobject ($settings | convertto-json) -filepath $this.configurationFile
        write-host "current configuration:`r`n$($settings | convertto-json -depth 5)" -foregroundcolor green
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
    write-host "current configuration:`r`n$($testenv | convertto-json -depth 5)" -foregroundcolor green
    write-host "use `$global:testEnv object" -ForegroundColor Green
}
