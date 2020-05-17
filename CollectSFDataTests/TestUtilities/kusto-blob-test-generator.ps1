<#
    script to create specified number of test sf 6.x sflog .dtr files in specified number of directories in specified depth
    useful for testing enumeration and continuation token
    storage format is storage account -> root container ($root) -> container(n) -> directory(n)+ -> blob(n)+
#>

param(
    [Parameter(Mandatory = $true)]
    $blobSasUri = "",
    [switch]$recreateBaseContainer,
    $numberOfBlobs = 10,
    $nodeNames = @('_nt0_0', '_nt0_1', '_nt0_2'),
    $baseContainerName = "fabriclogs-", #"sflogsblobtest",
    $testClusterGuid = "00000000-0000-0000-0000-000000000000", #[guid]::newguid().tostring(),
    $testFileTime = (get-date).ToFileTime(),
    $numberOfContainers = 6
)

$PSModuleAutoLoadingPreference = 2
$ErrorActionPreference = "continue"
Clear-Host
$error.Clear()
$totalBlobs = 0
$startTime = get-date
import-module azure.storage
#$ErrorActionPreference = "stop"

function main() {
    $baseContainerName = $baseContainerName + $testClusterGuid
    
    write-host "creating $numberOfBlobs blobs"
    $storageCredentials = new-object microsoft.windowsazure.storage.auth.storageCredentials($BlobSasUri)
    #[microsoft.windowsazure.storage.blob.cloudBlobContainer]
    $rootContainer = new-object microsoft.windowsazure.storage.blob.CloudBlobContainer(new-object Uri($BlobSasUri), $storageCredentials);
    # Get the ServiceClient from the RootContainer ref
    $baseContainer = $rootContainer.ServiceClient.GetContainerReference($baseContainerName)

    if ($recreateBaseContainer -and $baseContainer.DeleteIfExistsAsync().Wait()) {
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
            $blobName = "$nodeName/$typeTrace/00000000aaaabbbbccccdddddddddddd_$($typeTrace.ToLower())_traces_7.0.470.9590_$($testFileTime)_106_00$((get-date).ticks)_000000000$i.dtr"
        
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
        #2020-5-15 13:36:02.073,Informational,12488,5076,PLBM.PLBPrepareEnd,"PLB Prepare end. Total ms: 0"
        $timestamp = (get-date).ToUniversalTime().ToString('yyyy-M-d HH:mm:ss.fff')  # 2020-5-15 13:36:01.861 #(get-date).ToString('o')
        $level = get-random @('Informational', 'Warning', 'Error')
        $TID = get-random -Minimum 0 -Maximum 10000
        $PID1 = get-random -Minimum 0 -Maximum 10000
        $type = get-random @('Transport.Message', 'LeaseAgent.Heartbeat', 'Transport.SendBuf')
        $text = "`"test messsage $i`""
        $dataString = "$timestamp,$level,$tid,$pid1,$type,$text"#,$nodeName,$fileType,$relativeUri"
        [void]$testData.Add(
            $dataString
        )
    }

    return ($testData | out-string)
}

main
write-host "finished: total time:$((get-date) - $startTime)"
