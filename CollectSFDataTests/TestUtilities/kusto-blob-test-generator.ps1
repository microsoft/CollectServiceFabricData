<#
    script to create specified number of test sf 6.x sflog .dtr files in specified number of directories in specified depth
    useful for testing enumeration and continuation token
    storage format is storage account -> root container ($root) -> container(n) -> directory(n)+ -> blob(n)+
    max return from cloudblob is 5000. any count over that will return continuationtoken when enumerating
    total blob count == ($numberOfBlobs * $numberOfDirectories * ($directoryDepth + 1))
    181220
#>

param(
    [Parameter(Mandatory = $true)]
    $BlobSasUri = "",
    $baseContainerName = "fabriclogs-", #"sflogsblobtest",
    $numberOfContainers = 6,
    $numberOfDirectories = 3,
    $numberOfBlobs = 10,
    $nodeNames = @('_nt0_0', '_nt0_1', '_nt0_2'),
    $testClusterGuid = "4a17f977-93a7-4f4b-b924-4daa96fe13c5", #[guid]::newguid().tostring(),
    [switch]$recreateBaseContainer
)

$PSModuleAutoLoadingPreference = 2
$ErrorActionPreference = "continue"
Clear-Host
$error.Clear()
$totalBlobs = 0
$startTime = get-date
import-module azure.storage
$ErrorActionPreference = "stop"

function main() {
    $baseContainerName = $baseContainerName + $testClusterGuid
    
    write-host "creating $($numberOfBlobs * $numberOfDirectories) blobs"
    $storageCredentials = new-object microsoft.windowsazure.storage.auth.storageCredentials($BlobSasUri)
    #[microsoft.windowsazure.storage.blob.cloudBlobContainer]
    $rootContainer = new-object microsoft.windowsazure.storage.blob.CloudBlobContainer(new-object Uri($BlobSasUri), $storageCredentials);
    # Get the ServiceClient from the RootContainer ref
    $baseContainer = $rootContainer.ServiceClient.GetContainerReference($baseContainerName)

    if ($recreateBaseContainer -and $baseContainer.DeleteIfExists()) {
        write-host "deleting existing container $($baseContainerName)"
    }
    
    $error.Clear()

    while ($true) {
        $result = $baseContainer.CreateIfNotExistsAsync().Wait()
        write-host "create container result $($result | convertto-json -depth 5)"

        if ($error) {
            if ($error -imatch "409") {
                write-host "." -NoNewline
            }
            else {
                write-host $error
            }

            $error.Clear()
            Start-Sleep -Seconds 1
        }
        else {
            write-host
            write-host "recreated container $($baseContainerName)"
            break
        }
    }

    # root
    for ($containerCount = 0; $containerCount -le $numberOfContainers; $containerCount++) {
        $containerName = "testcontainer$($containerCount)"
        $container = $baseContainer.ServiceClient.GetContainerReference($containerName)
        $result = $container.CreateIfNotExistsAsync().Wait()
        write-host "creating container: $($containerName) result: $($result | convertto-json -depth 5)"
    }

    foreach ($nodeName in $nodeNames) {
        for ($i = 0; $i -lt $numberOfBlobs; $i++) {
            $count++
            #$testFileName = "f45f24746c42cc2a6dd69da9e7797e2c_fabric_traces_7.0.470.9590_132335945633288356_106_00637251513810151760_0000000000.dtr"
            $typeTrace = $(get-random @('Fabric', 'Lease'))
            $blobName = "$nodeName/$typeTrace/f45f24746c42cc2a6dd69da9e7797e2c_$($typeTrace.ToLower())_traces_7.0.470.9590_$((get-date).ticks)_106_00637251513810151760_000000000$i.dtr"
        
            write-host "creating blob $($totalBlobs): $($blobName)"
            $blobRef = $baseContainer.GetBlockBlobReference($blobName)
            write-host "creating blob $($totalBlobs): $($blobName)"
            $blobRef = $baseContainer.GetBlockBlobReference($blobName)
            $result = $blobRef.UploadTextAsync((generate-testData), $null, $null, $null, $null).Result;
            write-host "upload file result: $($result | convertto-json -depth 5)"
        }
    }

    return
}

function generate-testData() {
    $testData = [collections.arraylist]::new()

    for ($i = 0; $i -lt 100; $i++) {
        $timestamp = (get-date).ToString('o')
        $level = get-random @('Informational', 'Warning', 'Error')
        $TID = get-random -Minimum 0 -Maximum 10000
        $PID1 = get-random -Minimum 0 -Maximum 10000
        $type = get-random @('Transport.Message', 'LeaseAgent.Heartbeat', 'Transport.SendBuf')
        $text = "test messsage"
        #$nodeName = get-random @('_nt0_0','_nt0_1','_nt0_2')
        #$fileType = get-random @('fabric','lease')
        #$relativeUri = "$baseContaineraName$testClusterGuid/$($data.NodeName)/$($data.FileType)/eeb6ae3dc3ff88122d45c1b426b30a9a_fabric_traces_7.0.470.9590_132314840561756254_1444_$((get-date).ticks)_0000000000.dtr"
        $dataString = "$timestamp,$level,$tid,$pid1,$type,$text"#,$nodeName,$fileType,$relativeUri"
        [void]$testData.Add(
            $dataString
        )
    }

    return ($testData | out-string)
}

main
write-host "finished: total time:$((get-date) - $startTime)"
