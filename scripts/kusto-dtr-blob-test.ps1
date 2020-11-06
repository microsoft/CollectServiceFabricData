<#
    script to take sf 6.x sflog .dtr files without quoted message and format for testing kusto .zip blob -> kusto ingest blob
    181115
#>

param(
    $cacheLocation = $env:temp, #"$(get-location)\csfd",
    [Parameter(Mandatory = $false)]
    $sourceBlobUri = "",
    [Parameter(Mandatory = $false)]
    $destBlobUri = "",
    $uriPattern = "_fabric_",
    $destContainerName = "kustoformatteddtrzips",
    [switch]$downloadOnly,
    [switch]$downloadAndFormatOnly,
    $maxFiles = 10
)

$error.Clear()
$ErrorActionPreference = "continue"

$managedCode = @'
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public static class KustoConnection
{
    static string _newEventPattern = "^[0-9]{2,4}-[0-9]{1,2}-[0-9]{1,2} [0-9]{1,2}:[0-9]{1,2}:[0-9]{1,2}";
    static int _totalLogFilesAdded = 0;

	public class ThreadProcObject
	{
		public string databaseName;
		public string kcsbDM;
		public string logType;
		public string nodeName;
		public string sourceFileUri;
		public List<string> sourceFileUris;
		public string tableName;
	}

    public static bool AddLogFile(string sourceFileUri, string nodeName = null, string logType = null)
	{
		_totalLogFilesAdded++;
		ThreadProcObject tso = new ThreadProcObject()
		{
			sourceFileUri = sourceFileUri,
			nodeName = nodeName,
			logType = logType
		};

		Task.Run(() => { FormatServiceFabricLogFile(tso); });
		return true;
	}

	public static string ParseServiceFabricRecord(string record, string nodeName, string logType)
	{
		// format for kusto
		// by default the message field is not quoted, contains commas, contains quotes
		// kusto conforms to csv standards. service fabric dtr.zip (csv file) does not

		string[] newLine = record.Split(new string[] { "," }, 6, StringSplitOptions.None);
		string additionalCommas = string.Empty;

		if (newLine.Length < 6)
		{
			additionalCommas = new string(',', 6 - newLine.Length);
		}

		newLine[newLine.Length - 1] = newLine[newLine.Length - 1].Replace("\"", "'").TrimEnd('\r', '\n');
        //newLine[newLine.Length - 1] = string.Format("{0}\"{1}\",{2},{3}",additionalCommas, newLine[newLine.Length - 1], nodeName, logType);
        newLine[newLine.Length - 1] = string.Format("{0}\"{1}\"",additionalCommas, newLine[newLine.Length - 1]);
		return string.Join(",", newLine);
	}

    public static void FormatServiceFabricLogFile(string file, string nodename, string logtype)
    {
        FormatServiceFabricLogFile(new ThreadProcObject()
        {
            sourceFileUri = file,
            nodeName = nodename,
            logType = logtype
        });
    }

    public static void FormatServiceFabricLogFile(object state)
	{

		ThreadProcObject tso = (state as ThreadProcObject);
		string file = tso.sourceFileUri;
		string nodeName = tso.nodeName;
		string logType = tso.logType;

		Console.WriteLine(string.Format("{0}:FormatServiceFabricLog:{1}",Thread.CurrentThread.ManagedThreadId,file));
		string[] sourceLines = File.ReadAllLines(file);
		File.Delete(file);
		string[] destLines = new string[sourceLines.Length];
		string record = string.Empty;
		int destCounter = 0;
		int retry = 0;

		try
		{
			record = string.Empty;

			for (int i = 0; i < sourceLines.Length; i++)
			{
				//string tempLine = readerStream.ReadLine();
				string tempLine = sourceLines[i];

				if (Regex.IsMatch(tempLine, _newEventPattern))
				{
					// new record
					// write old record
					if (record.Length > 0)
					{
						destLines[destCounter++] = ParseServiceFabricRecord(record, nodeName, logType);
					}

					record = string.Empty;
				}

				record += tempLine;
			}

			// last record
			if (record.Length > 0)
			{
				destLines[destCounter] = ParseServiceFabricRecord(record, nodeName, logType);
			}

			File.WriteAllLines(file, destLines.Take(destCounter + 1));
			//_ingestQueue.Enqueue(file);
			Console.WriteLine(string.Format("{0}:FormatServiceFabricLog:finished format:{1}",Thread.CurrentThread.ManagedThreadId, file));
			return;
		}
		catch (Exception e)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(string.Format("{0}:formatlog exception:  {1}:{2}:{3}",Thread.CurrentThread.ManagedThreadId,retry,file,e.ToString()));
			Console.ResetColor();
		}
	}
}
'@
Add-Type $managedCode

function main()
{
    if ($cacheLocation)
    {
        new-item -path $cacheLocation -itemtype directory -erroraction silentlycontinue
    }

    if ($PSCloudShellUtilityModuleInfo)
    {
        throw (new-object NotImplementedException)
        #import-module az.storage
    }
    else
    {
        import-module azure.storage
    }

    #[microsoft.windowsazure.storage.blob.cloudBlobContainer]
    $sourceCloudBlobContainer = new-object microsoft.windowsazure.storage.blob.CloudBlobContainer(new-object Uri($sourceBlobUri));
    # Get the ServiceClient from the RootContainer ref
    $sourceRootContainer = $sourceCloudBlobContainer.ServiceClient.GetRootContainerReference().ServiceClient;
    #[microsoft.windowsAzure.storage.blob.blobContinuationToken]
    $token = new-object microsoft.windowsAzure.storage.blob.blobContinuationToken

    #[microsoft.windowsazure.storage.blob.cloudBlobContainer]
    $destCloudBlobContainer = new-object microsoft.windowsazure.storage.blob.CloudBlobContainer(new-object Uri($destBlobUri));
    # Get the ServiceClient from the RootContainer ref
    $destRootContainer = $destCloudBlobContainer.ServiceClient.GetContainerReference($destContainerName)
    $destRootContainer.CreateIfNotExists()
    $count = 0

    while ($token)
    {
        # Get top level container list
        foreach ($container in $sourceRootContainer.ListContainersSegmented($null).Results)
        {
            write-host "checking container $($container.gettype()) $($container.name)" -ForegroundColor Cyan
            #[Microsoft.WindowsAzure.Storage.Blob.BlobResultSegment]
            # use flat listing
            foreach ($blobSegment in $container.ListBlobsSegmented($prefix, $true, [Microsoft.WindowsAzure.Storage.Blob.BlobListingDetails]::None, 5000, $token, $null, $null).results)
            {
                $count++
                if($count -gt $maxFiles)
                {
                    write-host "max file count reached."
                    return
                }

                $token = $container.ContinuationToken
                write-host "checking blob directory $($blobSegment.gettype()) $($blobSegment.uri)" -ForegroundColor Cyan

                $filePath = $cacheLocation + $blobSegment.Uri.AbsolutePath
                write-host "processing $filePath"

                if (![regex]::IsMatch($filePath, $uriPattern))
                {
                    continue
                }

                if (!(test-path $filePath))
                {
                    try
                    {
                        $null = new-item -path ([io.path]::getDirectoryName($filePath)) -itemtype directory -erroraction silentlycontinue
                        #[microsoft.windowsazure.storage.blob.cloudBlockBlob]
                        $blobSegment.downloadToFile($filePath, [io.filemode]::CreateNew)
                        Expand-Archive -Path $filePath -DestinationPath ([io.path]::GetDirectoryName($filePath)) -ErrorAction Continue | Out-Null

                        if(!$downloadOnly)
                        {
                            [kustoconnection]::FormatServiceFabricLogFile(($filePath.trimend(".zip")), "testnode", "testlog")
                            #[kustoconnection]::AddLogFile(($filePath.trimend(".zip")), $nodename, $logType)
                         
                            if(!$downloadAndFormatOnly)
                            {
                                remove-item -Force -Path $filePath
                                compress-archive -Path ($filePath.trimend(".zip")) -DestinationPath $filePath -ErrorAction Continue | Out-Null
                                #[microsoft.windowsazure.storage.blob.cloudBlockBlob]
                                $destCloudBlockBlob = $destRootContainer.GetBlockBlobReference($blobSegment.uri.absolutePath.trimstart("/"))
                                $destCloudBlockBlob.UploadFromFile($filePath, $null, $null, $null);
                            }
                        }        
                    }
                    catch
                    {
                        Write-Warning "exception $($error | out-string)"
                        $error.Clear()
                    }
                }
                else
                {
                    Write-Warning "file exists: $filePath"
                }
            }
        }
    }
}

main
