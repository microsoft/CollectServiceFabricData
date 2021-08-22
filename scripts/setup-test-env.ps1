<#
.SYNOPSIS
setup variables for running tests
#>
using namespace System.Security.Cryptography.X509Certificates;
[cmdletbinding()]
param(
    [switch]$clean,
    [switch]$reset,
    [string]$configurationFile = "$env:LOCALAPPDATA\collectsfdata\collectSfDataDllTestProperties.json", # "$psscriptroot\..\bin\temp\collectSfDataDllTestProperties.json",
    [string]$tempDir = "$psscriptroot\..\src\bin\temp"
)

$PSModuleAutoLoadingPreference = 2
$ErrorActionPreference = "continue"
$error.clear()

class TestSettings {
    TestSettings() {}
      
    # for file download and gather tests
    [string]$testAzStorageAccount = "collectsfdatatests"
    [string]$testAzClientId = ""
    [string]$testAzClientCertificate = ""
    [string]$testAzClientName = 'collectsfdatadlltestclient';
    [string]$testCertificateNoPasswordBase64 = "";
    [string]$testCertificateWithPasswordBase64 = "";
    [string]$testCertificatePassword = "";
    

    # for azure cluster deployments
    [string]$testAdminUserName = "testadmin"
    [string]$testAdminPassword = ""
      
    [string]$AzureClientName = "collectsfdatadlltestapp"
    # existing collectsfdata variables
    [string]$AzureClientId = $null
    [string]$AzureClientCertificate = $null
    [string]$AzureClientSecret = $null
    [string]$AzureKeyVault = $null
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
    [string]$certFile = $null

    TestEnv() {
        $this.CheckTempDir()
        $this.CheckTemplate()
        $this.ReadConfig($this.configurationFile)
        
        if ($this.CheckAzureConfig()) {
            $this.CheckKustoConfig()
        }
        
    }

    [bool] AddAppRegistrationCertificate([X509Certificate2]$certificate, [string]$applicationName) {
        $error.Clear()
        $settings = $this.testSettings
        [bool]$retval = $true;
        $appRegistration = $this.GetAzAdApplication($applicationName)
        $keyid = $appRegistration.ObjectId
        write-host -foregroundcolor white "New-AzADAppCredential -ObjectId $keyid `
            -CustomKeyIdentifier $certificate.Thumbprint `
            -Type AsymmetricX509Cert `
            -Usage Verify `
            -Value $($certificate.GetRawCertData()) `
            -StartDate $($certificate.GetEffectiveDateString()) `
            -EndDate $($certificate.GetExpirationDateString())
        "

        $keyCredential = New-AzADAppCredential -ObjectId $keyid `
            -CustomKeyIdentifier $certificate.Thumbprint `
            -Type AsymmetricX509Cert `
            -Usage Verify `
            -Value $certificate.GetRawCertData() `
            -StartDate $certificate.GetEffectiveDateString() `
            -EndDate $certificate.GetExpirationDateString()

        

        return $retval
    }

    [bool] CheckAzureAppRegistration() {
        write-host -ForegroundColor Cyan "CheckAzureAppRegistration()"
        $error.Clear()
        $settings = $this.testSettings
        $appRegistrationName = $settings.AzureClientName
        $appRegistration = $this.GetAzAdApplication($appRegistrationName)

        write-host -foregroundcolor white "appregistration:$($appRegistration)"
        if ($appRegistration) {
            return $true
        }
        [bool]$retval = $this.CreateAppRegistration($appRegistrationName)
        $retval = $retval -band $this.AddAppRegistrationCertificate($this.LoadCertificate($settings.testCertificateNoPasswordBase64, ""), $appRegistrationName) 
        $retval = $retval -band $this.AddAppRegistrationCertificate($this.LoadCertificate($settings.testCertificateWithPasswordBase64, $settings.testCertificatePassword), $appRegistrationName) 
        if ($error) { retval = $false }
        write-host -ForegroundColor DarkCyan "CheckAzureAppRegistration(return:$retval)"
        return $retval
    }

    [bool] CheckTestAppRegistration() {
        write-host -ForegroundColor Cyan "CheckTestAppRegistration()"
        $error.Clear()
        $settings = $this.testSettings
        $appRegistrationName = $settings.testAzClientName
        $appRegistration = $this.GetAzAdApplication($appRegistrationName)

        write-host -foregroundcolor white "appregistration:$($appRegistration)"
        if ($appRegistration) {
            return $true
        }
        [bool]$retval = $this.CreateAppRegistration($appRegistrationName)
        $retval = $retval -band $this.AddAppRegistrationCertificate($this.LoadCertificate($settings.testAzClientCertificate, ""), $appRegistrationName) 
        if ($error) { retval = $false }
        write-host -ForegroundColor DarkCyan "CheckTestAppRegistration(return:$retval)"
        return $retval
    }

    [bool] CheckAppRegistrations() {
        write-host -ForegroundColor Cyan "CheckAppRegistrations()"
        $error.Clear()

        [bool]$retval = $this.CheckTestAppRegistration()
        $retval = $retval -band $this.CheckAzureAppRegistration()
        if ($error) { retval = $false }
        write-host -ForegroundColor DarkCyan "CheckAppRegistrations(return:$retval)"
        return $retval

    }

    [bool] CheckAppRegistrationCertificate([X509Certificate2]$certificate, [string]$appRegistrationName) {
        write-host -ForegroundColor Cyan "CheckAppRegistrationCertificate([X509Certificate2]$certificate, [string]$appRegistrationName)"
        $error.Clear()
        $settings = $this.testSettings
        [bool]$retval = $true;
        $appRegistration = $this.GetAzAdApplication($appRegistrationName)
        write-host $appRegistration

        if (!$appRegistration -or $error) { retval = $false }
        write-host -ForegroundColor DarkCyan "CheckAppRegistrationCertificate(return:$retval)"
        return $retval
    }

    [bool] CheckAppRegistrationCertificates() {
        write-host -ForegroundColor Cyan "CheckAppRegistrationCertificates()"
        $error.Clear()
        $settings = $this.testSettings
        [bool]$retval = $true;
        [X509Certificate2]$cert = $null

        write-host -foregroundcolor white "CheckAppRegistrationCertificates():checking testCertificateNoPasswordBase64"
        if ($settings.testCertificateNoPasswordBase64) {
            $cert = $this.LoadCertificate($settings.AzureClientCertificate, "")
            $retval = $retval -band ($this.CheckAppRegistrationCertificate($cert, $settings.AzureClientName))
        }

        write-host -foregroundcolor white "CheckAppRegistrationCertificates():checking testCertificateWithPasswordBase64"
        if ($settings.testCertificateWithPasswordBase64) {
            $cert = $this.LoadCertificate($settings.AzureClientCertificate, $settings.testCertificatePassword)
            $retval = $retval -band ($this.CheckAppRegistrationCertificate($cert, $settings.AzureClientName))
        }

        if ($settings.AzureClientCertificate) {
            write-host -foregroundcolor white "CheckAppRegistrationCertificates():loading collectsfdata cert $($settings.AzureClientCertificate)"
            #$retval = $retval -band ($this.CheckKeyVaultCertificate($settings.AzureClientCertificate, $this.testAzClientName,""))
            $retval = $retval -band ($this.CheckAppRegistrationCertificate($cert, $settings.AzureClientName))
        }

        if ($error) { 
            $retval = $false
        }
        else {
            $this.SaveConfig()
        }

        write-host -ForegroundColor DarkCyan "CheckAppRegistrationCertificates(return:$retval)"
        return $retval
    }

    [bool] CheckAzureConfig() {
        write-host -ForegroundColor Cyan "CheckAzureConfig()"
        $settings = $this.testSettings
        #if (!$settings.AzureClientId -or !$settings.AzureClientCertificate -or !$settings.AzureResourceGroup -or !$settings.AzureResourceGroupLocation) {
        if (!$settings.testAzClientId -or !$settings.testAzClientCertificate -or !$settings.AzureResourceGroup -or !$settings.AzureResourceGroupLocation) {
            Write-Warning "azure settings not configured. storage tests may fail"
            return $false
        }

        <#
        write-host -foregroundcolor white "checking azure config" -ForegroundColor Cyan
        $credential = new-object -typename System.Management.Automation.PSCredential `
            -argumentlist @(
            $settings.AzureClientId, 
            ($settings.AzureClientCertificate | convertto-securestring -Force -AsPlainText)
        )
        #>

        if ((get-module -ListAvailable -Name azurerm)) {
            write-host -foregroundColor Red  "azurerm modules are installed and are not compatible with this script. remove azurerm modules and install az modules. and restart."
            return $false
        }

        if (!(get-module -ListAvailable -Name az.accounts)) {
            install-module Az.Accounts -Force -scope CurrentUser #-UseWindowsPowerShell
        }
        import-module Az.Accounts #-UseWindowsPowerShell

        if (!(get-module -ListAvailable -Name az.storage)) {
            install-module Az.Storage -Force -scope CurrentUser #-UseWindowsPowerShell
        }
        import-module Az.Storage #-UseWindowsPowerShell

        if (!(get-module -ListAvailable -Name Az.Resources)) {
            install-module Az.Resources -Force -scope CurrentUser #-UseWindowsPowerShell
        }
        import-module Az.Resources #-UseWindowsPowerShell

        if (!(get-module -ListAvailable -Name az.keyvault)) {
            install-module Az.KeyVault -Force -scope CurrentUser #-UseWindowsPowerShell
        }
        import-module Az.KeyVault #-UseWindowsPowerShell

        if ($settings.testAzClientId -and $settings.testAzClientCertificate) {

            $error.Clear()
            write-host -foregroundcolor white "loading test client cert $($settings.testAzClientCertificate)"
            $cert = $this.LoadCertificate($settings.testAzClientCertificate, $settings.testCertificatePassword)
            if ($error) { return $false }

            write-host -foregroundcolor white "connect-AzAccount -TenantId $($settings.AzureTenantId) `
                -ApplicationId $($settings.testAzClientId) `
                -ServicePrincipal `
                -CertificateThumbprint $($cert.thumbprint)
            "
            connect-AzAccount -TenantId $settings.AzureTenantId `
                -ApplicationId $settings.testAzClientId `
                -ServicePrincipal `
                -CertificateThumbprint $cert.thumbprint
        }
        else {
            # first time setup needs interactive logon
            write-host -foregroundcolor white "connect-AzAccount -TenantId $($settings.AzureTenantId) " #`
            connect-AzAccount -TenantId $settings.AzureTenantId #`
        }

        if (!(get-azcontext)) { return $false }

        get-azcontext | Format-List *

        if (!$this.CheckResourceGroup()) { return $false }
        if (!$this.CheckStorageAccount()) { return $false }
        
        if ($this.CheckKeyVault()) {
            $this.CheckKeyVaultCertificates()
        }

        if ($this.CheckAppRegistrations()) {
            $this.CheckAppRegistrationCertificates()
            #$this.CheckAppRegistrationSecrets() # not implemented
        }

        return $true
    }

    [bool] CheckKeyVault() {
        write-host -ForegroundColor Cyan "CheckKeyVault()"
        $error.Clear()
        $settings = $this.testSettings
        $keyvaultname = $null

        if ($settings.AzureKeyVault) {
            $keyvaultname = $settings.AzureKeyVault
            if ($this.GetAzureKeyVault()) {
                return $true
            }
        }
        else {
            $keyvaultname = "$($settings.AzureResourceGroup)$([math]::Abs($settings.AzureResourceGroup.GetHashCode()))".Substring(0, 24)
        }

        write-host -foregroundcolor white "keyvault name:$keyvaultname"
        $retval = New-AzKeyVault -Name $keyvaultname -ResourceGroupName $settings.AzureResourceGroup -Location $settings.AzureResourceGroupLocation -EnabledForDeployment -EnabledForTemplateDeployment -EnableRbacAuthorization
        write-host -foregroundcolor white "new keyvault result:$retval"
        $settings.AzureKeyVault = (Get-AzKeyVault -resourcegroupname $settings.AzureResourceGroup -vaultname $keyvaultname).VaultUri
        write-host -foregroundcolor white "setting key vault $($settings.AzureKeyVault)"
        $this.SaveConfig()

        if (!$settings.AzureKeyVault -or $error) { retval = $false }
        write-host -ForegroundColor DarkCyan "CheckKeyVault(return:$retval)"
        return $retval
    }

    [bool] CheckKeyVaultCertificate([string]$base64String, [string]$secretName, [string]$password) {
        write-host -ForegroundColor Cyan "CheckKeyVaultCertificate([string]$base64String, [string]$secretName, [string]$password)"
        $error.Clear()
        $settings = $this.testSettings
        $keyVault = $this.GetAzureKeyVault()
        $keyVaultCertificate = $null
        if (!$keyVault) { return $false }
        
        $keyvaultname = [uri]::new($settings.AzureKeyVault).Host.Split('.')[0]
        write-host -foregroundcolor white "checking certificate $($base64String)"

        if (($keyVaultCertificate = Get-AzKeyVaultCertificate -VaultName $keyvaultname -Name $secretName) -ne $null) {
            write-host "keyvaultcertificate:$keyVaultCertificate"
            if (!$base64String) {
                $base64String = $this.GetAzureKeyVaultCertificateBase64String($secretName, $password)
                return $true
            }
            elseif ($base64String -eq $this.GetAzureKeyVaultCertificateBase64String($secretName, $password)) {
                return $true
            }
        }

        if ($error) {
            write-host -foregroundColor Red  "error getting cert from keyvault $($error | out-string)"
            return $false
        }

        $cert = $this.LoadCertificate($base64String, $password)
        if (!$cert -or $error) { return $false }
        
        if (!(Get-AzKeyVaultCertificate -VaultName $keyvaultname -Name $secretName)) {
            write-host -foregroundcolor white "import-azkeyvaultcertificate -vaultname $keyvaultname -name $($cert.thumbprint) -filepath $($this.certFile) -password $password"
            import-azkeyvaultcertificate -vaultname $keyvaultname -name ($cert.thumbprint) -filepath $this.certFile -Password $this.CreateSecureString($password)
        }
        
        return $true
    }

    [bool] CheckKeyVaultCertificates() {
        write-host -ForegroundColor Cyan "CheckKeyVaultCertificates()"
        $error.Clear()
        $settings = $this.testSettings
        [bool]$retval = $true;

        write-host -foregroundcolor white "checking testCertificateNoPasswordBase64"
        if (!($this.CheckKeyVaultCertificate($settings.testCertificateNoPasswordBase64, 'testCertificateNoPassword', $null))) {
            $settings.testCertificateNoPasswordBase64 = $this.CreateKeyVaultCertificate('testCertificateNoPassword', $null)
        }
        $retval = $retval -band ($this.CheckKeyVaultCertificate($settings.testCertificateNoPasswordBase64, 'testCertificateNoPassword', ""))

        write-host -foregroundcolor white "checking testCertificateWithPasswordBase64"
        if (!($this.CheckKeyVaultCertificate($settings.testCertificateWithPasswordBase64, 'testCertificateWithPassword', $settings.testCertificatePassword))) {
            $settings.testCertificateWithPasswordBase64 = $this.CreateKeyVaultCertificate('testCertificateWithPassword', $settings.testCertificatePassword)
        }
        $retval = $retval -band ($this.CheckKeyVaultCertificate($settings.testCertificateWithPasswordBase64, 'testCertificateWithPassword', $settings.testCertificatePassword))

        if ($settings.AzureClientCertificate) {
            write-host -foregroundcolor white "loading collectsfdata cert $($settings.AzureClientCertificate)"
            $retval = $retval -band ($this.CheckKeyVaultCertificate($settings.AzureClientCertificate, $settings.AzureClientName, $settings.AzureClientSecret))
            #$cert = $this.LoadCertificate([convert]::FromBase64String($settings.AzureClientCertificate))
        }

        if ($error) { 
            $retval = $false
        }
        else {
            $this.SaveConfig()
        }
        write-host -ForegroundColor DarkCyan "CheckKeyVaultCertificates(return:$retval)"
        return $retval
    }

    [bool] CheckKustoConfig() {
        $settings = $this.testSettings
        if (!$settings.AzureClientId -or !$settings.AzureClientCertificate -or !$settings.AzureResourceGroup -or !$settings.AzureResourceGroupLocation) {
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

            write-host -foregroundcolor white "hostName: $hostName"
            write-host -foregroundcolor white "ingest: $ingest"
            write-host -foregroundcolor white "clusterName: $clusterName"
            write-host -foregroundcolor white "location: $location"
            write-host -foregroundcolor white "domainName $domainName"
            write-host -foregroundcolor white "databaseName $databaseName"

            write-host -foregroundcolor white "Test-NetConnection -ComputerName $hostName -port 443"
            $pingResults = Test-NetConnection -ComputerName $hostName -port 443 -ErrorAction SilentlyContinue
            $pingResults | convertto-json

            if (!$pingResults.TcpTestSucceeded) {
                write-warning "unable to ping kusto ingest url"
            }
            else {
                $error.Clear()
                write-host -foregroundcolor white "able to ping kusto ingest url"
                return $true
            }

            if ($location -ieq 'kusto') {
                write-host -foregroundcolor white "not a user kusto cluster"
                return $true
            }

            write-host -foregroundcolor white "checking test resource group for kusto cluster"
            $rgClusters = Get-AzKustoCluster -ResourceGroupName $settings.AzureResourceGroup
            $rgClusters
            
            write-host -foregroundcolor white "checking subscriptions for kusto cluster"
            $subClusters = Get-AzKustoCluster -ResourceGroupName $settings.AzureResourceGroup
            $subClusters

            if (!$rgClusters -and !$subClusters) {
                write-warning 'no kusto clusters found. create new kusto cluster with new-azkustocluster command 
                    or provide valid kusto ingest url to test kusto functions'
                write-host -foregroundcolor white "example command: New-AzKustoCluster -Name collectsfdatatest `
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

    [bool] CheckResourceGroup() {
        $settings = $this.testSettings
        write-host -foregroundcolor white "checking resource group $($settings.AzureResourceGroup)"
        if (!(get-azresourcegroup $settings.AzureResourceGroup -Location $settings.AzureResourceGroupLocation)) {
            if (!(new-azresourcegroup $settings.AzureResourceGroup -Location $settings.AzureResourceGroupLocation)) {
                throw new-object System.UnauthorizedAccessException($error | out-string)
                return $false;
            }
        }
        return $true;
    }

    [bool] CheckStorageAccount() {
        $settings = $this.testSettings
        if (!$settings.testAzStorageAccount) {
            $settings.testAzStorageAccount = "$($settings.testAzStorageAccount)$([math]::Abs($settings.AzureResourceGroup.GetHashCode()))".Substring(0, 24)
        }
        write-host -foregroundcolor white "setting unique storage account $($settings.testAzStorageAccount)"
        $this.SaveConfig()

        write-host -foregroundcolor white "checking storage account $($settings.testAzStorageAccount)"
        $sa = get-azstorageaccount $settings.testAzStorageAccount -resourcegroupname $settings.AzureResourceGroup
        if (!($sa)) {
            if (!(new-azstorageaccount $settings.testAzStorageAccount -skuname 'Standard_LRS' -resourcegroupname $settings.AzureResourceGroup -Location $settings.AzureResourceGroupLocation)) {
                throw new-object System.UnauthorizedAccessException($error | out-string)
                return $false
            }
            $sa = get-azstorageaccount $settings.testAzStorageAccount -resourcegroupname $settings.AzureResourceGroup
        }

        write-host -foregroundcolor white "getting key $($settings.testAzStorageAccount)"
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
        write-host -foregroundcolor white "setting test token $global:sasUri"
        $settings.SasKey = $global:sasuri
        $this.SaveConfig()
        return $true
    }

    [void] CheckTempDir() {
        write-host -ForegroundColor Cyan "CheckTempDir($($this.tempDir))"
        if ((test-path $this.tempDir) -and $this.clean) {
            remove-item $this.tempDir -recurse -force
            new-item -itemType directory -path $this.tempDir
        }
        elseif (!(test-path $this.tempDir)) {
            new-item -itemType directory -path $this.tempDir
        }
    }

    [void] CheckTemplate() {
        write-host -ForegroundColor Cyan "CheckTemplate()"
        if (!(test-path $this.configurationFile) -or $this.reset) {
            $this.SaveConfig()
            write-host -foregroundcolor white "edit file directly and save: $this.configurationFile" 
            write-host -foregroundcolor white "create azure app id / spn for azure storage / gather tests. .\azure-az-create-aad-application-spn.ps1 can be used to create one progammatically."
            write-host -foregroundcolor white ".\azure-az-create-aad-application-spn.ps1 -aadDisplayName $($this.testAzClientName) -logonType cert"
            write-host -foregroundcolor white ".\azure-az-create-aad-application-spn.ps1 -aadDisplayName $($this.testAzClientName) -uri http://$($this.testAzClientName) -logontype cert"
            . $this.configurationFile
        }
    }

    [bool] CreateAppRegistration([string]$displayName) {
        write-host -ForegroundColor Cyan "CreateAppRegistration([string]$displayName)"
        $error.Clear()
        $settings = $this.testSettings
        #$appRegistration = New-AzADApplication -DisplayName $displayName -CertValue ([convert]::tobase64string($certificate.GetRawCertData()))
        $appRegistration = New-AzADApplication -DisplayName $displayName -IdentifierUris @("http://$displayName") #-CertValue ([convert]::tobase64string($certificate.GetRawCertData()))
        if ($error) {
            write-host -foregroundColor Red  "error:createappregistration $($error |out-string)"
            return $false
        }
        write-host -foregroundcolor white "appregistration:$($appRegistration)"
        if ($appRegistration) {
            return $true
        }
        return $null
    }

    [string] CreateKeyVaultCertificate([string]$secretName, [string]$password) {
        write-host -ForegroundColor Cyan "CreateKeyVaultCertificate([string]$secretName, [string]$password)"
        $keyVault = $this.GetAzureKeyVault()
        $error.Clear()

        write-host "new-azKeyVaultCertificatePolicy -SecretContentType `"application/x-pkcs12`" `
            -SubjectName `"CN=$secretName`" `
            -IssuerName 'self' `
            -ValidityInMonths 6 `
            -ReuseKeyOnRenewal
        "
        $policy = new-azKeyVaultCertificatePolicy -SecretContentType "application/x-pkcs12" `
            -SubjectName "CN=$secretName" `
            -IssuerName 'self' `
            -ValidityInMonths 6 `
            -ReuseKeyOnRenewal
        
        write-host -ForegroundColor Green "Add-AzKeyVaultCertificate -VaultName $($keyVault.VaultName) -Name $secretName -CertificatePolicy $policy"
        Add-AzKeyVaultCertificate -VaultName $keyVault.VaultName -Name $secretName -CertificatePolicy $policy

        if ($error) {
            write-host -foregroundColor Red  "error adding cert to keyvault $($error | out-string)"
            return $null
        }

        return $this.GetAzureKeyVaultCertificateBase64String($secretName, $password)
    }

    [securestring] CreateSecureString([string]$inputString) {
        write-host -ForegroundColor Cyan "[securestring] CreateSecureString([string]$inputString)"
        [securestring]$returnString = [securestring]::new()
        foreach ($element in $inputString.ToCharArray()) {
            $returnString.AppendChar($element)
        }
        return $returnString
    }

    [object] GetAzAdApplication([string]$displayName) {
        $settings = $this.testSettings
        # if ($settings.AzureClientId) {
        #     return Get-AzADApplication -ApplicationId $settings.AzureClientId
        # }
        # elseif ($settings.AzureClientSecret) {
        #     return Get-AzADApplication -DisplayName $settings.AzureClientSecret
        # }
        # return $null
        write-host -foregroundcolor white "Get-AzADApplication -DisplayName $displayName"
        [object]$retval = Get-AzADApplication -DisplayName $displayName
        write-host -foregroundcolor white "return: $retval"
        return $retval
    }

    [object] GetAzureKeyVault() {
        write-host -ForegroundColor Cyan "GetAzureKeyVault()"
        $error.Clear()
        $settings = $this.testSettings
        [string]$vaultUri = $settings.AzureKeyVault
        $settings = $this.testsettings
        $keyvaultname = [uri]::new($vaultUri).Host.Split('.')[0]
        write-host -foregroundcolor white "checking keyvault:name:$keyvaultname"
        return (Get-AzKeyVault -ResourceGroupName $settings.AzureResourceGroup -VaultName $keyvaultname)
    }

    [string] GetAzureKeyVaultCertificateBase64String([string]$secretName, [string]$password) {
        write-host -ForegroundColor Cyan "[string] GetAzureKeyVaultCertificateBase64String([string]$secretName, [string]$password)"
        $keyvault = $this.GetAzureKeyVault()
        $cert = $null
        while ($true) {
            $cert = Get-AzKeyVaultCertificate -VaultName $keyVault.VaultName -name $secretName
            if ($cert) {
                break
            }
            start-sleep -seconds 1
        }

        #$secret = Get-AzKeyVaultSecret -VaultName $keyVault.VaultName -Name $cert.Name -AsPlainText
        # if ($password) {
        #    $secretByte = [Convert]::FromBase64String($secret)
        #    $x509Cert = [security.cryptography.x509Certificates.x509Certificate2]::new($secretByte, $password, "Exportable,PersistKeySet")
        $type = [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx
        [byte[]]$pfxFileByte = $cert.Certificate.Export($type, $password)#,"Exportable,PersistKeySet")
        # $secret = $pfxFileByte
        # }

        return [convert]::ToBase64String($pfxFileByte)
    }

    [X509Certificate2] LoadCertificate([string]$base64String, [string]$password) {
        write-host -ForegroundColor Cyan "[X509Certificate2] LoadCertificate([string]$base64String, [string]$password)"
        $error.Clear()
        $settings = $this.testSettings
        $cert = $null

        if (!$base64String) {
            Write-Warning "LoadCertificate:empty base64string. returning"
            return $null
        }

        write-host -foregroundcolor white "loading collectsfdata cert $($base64String.Substring(0,10)) with pw: $password"
        [X509Certificate2]$cert = $null
        [byte[]] $bytes = $null
        $securePassword = $null

        if ($password) {
            write-host -foregroundcolor white "converting password"
            $securePassword = ConvertTo-SecureString -String $password -Force -AsPlainText
        }

        $store = [Security.Cryptography.X509Certificates.X509Store]::new([Security.Cryptography.X509Certificates.StoreName]::My, [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite -bor [Security.Cryptography.X509Certificates.OpenFlags]::OpenExistingOnly)
        
        try {
            $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new([convert]::FromBase64String($base64String), $securePassword, [X509KeyStorageFlags]::UserKeySet -bor [X509KeyStorageFlags]::PersistKeySet -bor [X509KeyStorageFlags]::Exportable)        
        }
        catch {
            write-host -foregroundColor Red  "exception: $($_)"
        }      
        
        if (!($store.Certificates.thumbprint.contains($cert.Thumbprint))) {
            write-host -foregroundcolor white "adding cert $($cert.Thumbprint) to store"
            $store.Add($cert)
        }
        else {
            write-host -foregroundcolor white "$($cert.Thumbprint) already exists in store"
        }

        $store.Close()
        $bytes = $cert.Export([X509ContentType]::pkcs12, $password)

        if ($bytes) {
            $this.certFile = "$($this.tempdir)\$($cert.thumbprint).pfx"
            write-host -foregroundcolor white "saving cert to temp dir $($this.certFile)"
            [io.File]::WriteAllBytes($this.certFile, $bytes)
        }

        return $cert
    }

    [TestSettings] ReadConfig() {
        write-host -ForegroundColor Cyan "ReadConfig()"
        return $this.ReadConfig($this.configurationFile)
    }

    [TestSettings] ReadConfig([string]$configurationFile) {
        write-host -ForegroundColor Cyan "[TestSettings] ReadConfig([string]$configurationFile)"
        $this.testSettings = get-content -raw $configurationFile | convertfrom-json
        write-host -foregroundcolor white "current configuration:`r`n$($global:testProperties | out-string)"
        write-host -foregroundcolor white "current configuration saved in `$global:testProperties"

        return $this.testSettings
    }

    [bool] SaveConfig() {
        write-host -ForegroundColor Cyan "SaveConfig()"
        return $this.SaveConfig($this.testSettings)
    }

    [bool] SaveConfig([TestSettings] $settings = $this.testSettings) {
        write-host -ForegroundColor Cyan "[bool] SaveConfig([TestSettings] $settings = `$this.testSettings)"
        $configDir = [io.path]::GetDirectoryName($this.configurationFile)
        if (!(test-path $configDir)) {
            New-Item -ItemType Directory -Path $configDir
        }
        
        $error.Clear()
        out-file -inputobject ($settings | convertto-json) -filepath $this.configurationFile
        write-host -foregroundcolor white "current configuration:`r`n$($settings | convertto-json -depth 5)" 
        write-host -foregroundcolor white "current configuration saved in $($this.configurationFile)" 

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
    write-host -foregroundcolor white "current configuration:`r`n$($testenv | convertto-json -depth 5)" 
    write-host -foregroundcolor white "use `$global:testEnv object" 
}
