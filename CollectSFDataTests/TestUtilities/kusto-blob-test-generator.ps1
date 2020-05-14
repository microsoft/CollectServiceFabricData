<#
    script to create specified number of test sf 6.x sflog .dtr files in specified number of directories in specified depth
    useful for testing enumeration and continuation token
    storage format is storage account -> root container ($root) -> container(n) -> directory(n)+ -> blob(n)+
    max return from cloudblob is 5000. any count over that will return continuationtoken when enumerating
    total blob count == ($numberOfBlobs * $numberOfDirectories * ($directoryDepth + 1))
    181220
#>

param(
    [Parameter(Mandatory = $false)]
    $BlobUri = "",
    $baseContainerName = "kustoblobtest",
    $directoryDepth = 3,
    $numberOfContainers = 6,
    $numberOfDirectories = 3,
    $numberOfBlobs = 100,
    [switch]$recreateBaseContainer
)

Clear-Host
$error.Clear()
$ErrorActionPreference = "silentlycontinue"
$totalBlobs = 0
$startTime = get-date
import-module azure.storage

function main()
{
    write-host "creating $($numberOfBlobs * $numberOfDirectories * ($directoryDepth + 1)) blobs"
    #[microsoft.windowsazure.storage.blob.cloudBlobContainer]
    $rootContainer = new-object microsoft.windowsazure.storage.blob.CloudBlobContainer(new-object Uri($BlobUri));
    # Get the ServiceClient from the RootContainer ref
    $baseContainer = $rootContainer.ServiceClient.GetContainerReference($baseContainerName)

    if ($recreateBaseContainer -and $baseContainer.DeleteIfExists())
    {
        write-host "deleting existing container $($baseContainerName)"
    }
    
    $error.Clear()

    while ($true)
    {
        [void]$baseContainer.CreateIfNotExists() 

        if ($error)
        {
            if ($error -imatch "409")
            {
                write-host "." -NoNewline
            }
            else
            {
                write-host $error
            }

            $error.Clear()
            Start-Sleep -Seconds 1
        }
        else
        {
            write-host
            write-host "recreated container $($baseContainerName)"
            break
        }
    }

    # root
    for ($containerCount = 0; $containerCount -le $numberOfContainers; $containerCount++)
    {
        $containerName = "testcontainer$($containerCount)"
        $container = $baseContainer.ServiceClient.GetContainerReference($containerName)
        [void]$container.CreateIfNotExists()
        write-host "creating container: $($containerName)"
    }
       
    for ($directoryDepthCount = 1; $directoryDepthCount -le $directoryDepth; $directoryDepthCount++)
    {
        if ($directoryDepthCount -eq 1)
        {
            $directoryDepthName = ""
        }
        else
        {
            $directoryDepthName += "depth$($directoryDepthCount)/"
        }

        for ($directoryCount = 0; $directoryCount -le $numberOfDirectories; $directoryCount++)
        {
            if ($directoryCount -eq 0)
            {
                $directoryName = ""
            }
            else
            {
                $directoryName = "$($directoryDepthName)testdirectory$($directoryCount)/"
            }

            for ($blobCount = 0; $blobCount -le $numberOfBlobs; $blobCount++)
            {
                $blobName = "$($directoryName)testblob$($totalBlobs.ToString().PadLeft(7,"0")).dtr"
                $totalBlobs++
                write-host "creating blob $($totalBlobs): $($blobName)"
                $blobRef = $baseContainer.GetBlockBlobReference($blobName)
                $blobRef.UploadText("testtest", $null, $null, $null, $null);
            }    
        }
    }
}

main
write-host "finished: total time:$((get-date) - $startTime)"
