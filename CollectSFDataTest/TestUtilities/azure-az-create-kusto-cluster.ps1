<#
create test kusto cluster

.\azure-az-create-kusto-cluster.ps1 -resourceGroupName -resourceGroupLocation -appRegistrationId
#>

param(
    $resourceGroupName = 'collectsfdataunittest',
    [Parameter(Mandatory = $true)]
    $resourceGroupLocation = '',
    $clusterName = 'collectsfdataunittest',
    $databaseName = 'collectsfdatadb',
    $skuName = 'Dev(No SLA)_Standard_D11_v2', #'Dev(No SLA)_Standard_E2a_V4', # Dev(No SLA)_Standard_D11_v2
    [ValidateSet('basic', 'standard')]
    $skuTier = 'basic',
    $databaseKind = 'readwrite',
    $appRegistrationId,
    [switch]$force
)

$PSModuleAutoLoadingPreference = 2
$ErrorActionPreference = "continue"
$error.clear()

if (!(get-module -ListAvailable az.kusto)) {
    install-module az.kusto
    import-module az.kusto
}

if (!(get-azresourcegroup)) {
    connect-azaccount
}

if (!(Get-AzResourceGroup -Name $resourceGroupName)) {
    write-warning "resource group does not exist. creating"
    new-azresourcegroup -Name $resourceGroupName -Location $resourceGroupLocation
}

$kustoClusters = @(Get-AzKustoCluster)

write-host "current clusters"
$kustoClusters

if (($kustoClusters.Name -imatch $clusterName)) {
    write-error "cluster exists $clustername"
    if(!$force) { return }
}

$availableSkus = Get-AzKustoClusterSku | ? location -ieq $resourceGroupLocation | ? tier -ieq $skuTier
write-host "available skus in $resourceGroupLocation"
$availableSkus | convertto-json

if(!($availableSkus | ? name -ieq $skuName)) {
    write-error "$skuName unavailable in $resourceGroupLocation"
    if(!$force) { return }
}

$cluster = New-AzKustoCluster -name $clusterName `
    -ResourceGroupName $resourceGroupName `
    -Location $resourceGroupLocation `
    -SkuName $skuName `
    -SkuTier $skuTier `
    -EnablePurge

$cluster | convertto-json 

$database = New-AzKustoDatabase -ClusterName $clusterName `
    -Name $databaseName `
    -ResourceGroupName $resourceGroupName `
    -Location $resourceGroupLocation `
    -Kind $databaseKind

$database | convertto-json

<#
COMPLEX PARAMETER PROPERTIES

To create the parameters described below, construct a hash table containing the appropriate properties. For information on hash tables, run Get-Help about_Hash_Tables.

INPUTOBJECT : Identity Parameter

[AttachedDatabaseConfigurationName <String>]: The name of the attached database configuration.
[ClusterName <String>]: The name of the Kusto cluster.
[DataConnectionName <String>]: The name of the data connection.
[DatabaseName <String>]: The name of the database in the Kusto cluster.
[Id <String>]: Resource identity path
[Location <String>]: Azure location.
[PrincipalAssignmentName <String>]: The name of the Kusto principalAssignment.
[ResourceGroupName <String>]: The name of the resource group containing the Kusto cluster.
[SubscriptionId <String>]: Gets subscription credentials which uniquely identify Microsoft Azure subscription. The subscription ID forms part of the URI for every service call.
VALUE <IDatabasePrincipal[]>: The list of Kusto database principals.

Name <String>: Database principal name.
Role <DatabasePrincipalRole>: Database principal role.
Type <DatabasePrincipalType>: Database principal type.
[AppId <String>]: Application id - relevant only for application principal type.
[Email <String>]: Database principal email if exists.
[Fqn <String>]: Database principal fully qualified name.
#>

$principal = Add-AzKustoDatabasePrincipal -ClusterName $clusterName `
    -DatabaseName $databaseName `
    -ResourceGroupName $resourceGroupName `
    -Value (@{Name = $appRegistrationId; Role = 'Admin'; Type = 'App'; AppId = $appRegistrationId })

$principal | convertto-json

<#
$assignment = New-AzKustoDatabasePrincipalAssignment -ClusterName $clusterName `
    -DatabaseName $databaseName `
    -ResourceGroupName $resourceGroupName `
    -PrincipalAssignmentName $appRegistrationId `
    -PrincipalId $appRegistrationId `
    -PrincipalType 'App' `
    -Role 'Admin'

$assignment | convertto-json
#>
$global:cluster = $cluster
write-host 'finished. object stored in `$global:cluster'
write-host "data ingestion uri: $($cluster.DataIngestionUri)"
