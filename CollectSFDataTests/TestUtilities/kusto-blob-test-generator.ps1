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
    $baseContainerName = "sflogsblobtest",
    $directoryDepth = 3,
    $numberOfContainers = 6,
    $numberOfDirectories = 3,
    $numberOfBlobs = 100,
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
    write-host "creating $($numberOfBlobs * $numberOfDirectories * ($directoryDepth + 1)) blobs"
    #[microsoft.windowsazure.storage.blob.cloudBlobContainer]
    $rootContainer = new-object microsoft.windowsazure.storage.blob.CloudBlobContainer(new-object Uri($BlobSasUri));
    # Get the ServiceClient from the RootContainer ref
    $baseContainer = $rootContainer.ServiceClient.GetContainerReference($baseContainerName)

    if ($recreateBaseContainer -and $baseContainer.DeleteIfExists()) {
        write-host "deleting existing container $($baseContainerName)"
    }
    
    $error.Clear()

    while ($true) {
        $result = $baseContainer.CreateIfNotExistsAsync().Result
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
        $result = $container.CreateIfNotExistsAsync().Result
        write-host "creating container: $($containerName) result: $($result | convertto-json -depth 5)"
    }

    for ($directoryDepthCount = 1; $directoryDepthCount -le $directoryDepth; $directoryDepthCount++) {
        if ($directoryDepthCount -eq 1) {
            $directoryDepthName = ""
        }
        else {
            $directoryDepthName += "depth$($directoryDepthCount)/"
        }

        for ($directoryCount = 0; $directoryCount -le $numberOfDirectories; $directoryCount++) {
            if ($directoryCount -eq 0) {
                $directoryName = ""
            }
            else {
                $directoryName = "$($directoryDepthName)testdirectory$($directoryCount)/"
            }

            for ($blobCount = 0; $blobCount -le $numberOfBlobs; $blobCount++) {
                $blobName = "$($directoryName)testblob$($totalBlobs.ToString().PadLeft(7,"0")).dtr"
                $totalBlobs++
                write-host "creating blob $($totalBlobs): $($blobName)"
                $blobRef = $baseContainer.GetBlockBlobReference($blobName)
                $result = $blobRef.UploadTextAsync("testtest", $null, $null, $null, $null).Result;
                write-host "upload file result: $($result | convertto-json -depth 5)"
            }
        }
    }
}

main
write-host "finished: total time:$((get-date) - $startTime)"
