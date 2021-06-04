using NUnit.Framework;
using CollectSFData.DataFile;
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CollectSFDataDllTest.Utilities;

namespace CollectSFData.DataFile.Tests
{
    [TestFixture()]
    public class FileManagerTests
    {
        [Test()]
        public void DeleteFileTestExistingFile()
        {
            string tempFile = Path.GetTempFileName();
            Assert.IsTrue(File.Exists(tempFile));
            FileManager fileManager = new FileManager(new Instance());
            fileManager.DeleteFile(tempFile);
            Assert.IsFalse(File.Exists(tempFile));
        }

        [Test()]
        public void DeleteFileTestNonExistingFile()
        {
            string tempFile = Path.GetTempFileName();
            Assert.IsTrue(File.Exists(tempFile));
            File.Delete(tempFile);
            FileManager fileManager = new FileManager(new Instance());
            fileManager.DeleteFile(tempFile);
            Assert.IsFalse(File.Exists(tempFile));
        }

        [Test()]
        public void ExtractPerfRelogCsvDataTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FileManagerTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FileManagerTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FormatCounterFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FormatDtrFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FormatEtlFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FormatExceptionFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FormatSetupFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FormatTableFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void FormatTraceFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void NormalizePathTest()
        {
            string path = "c:";
            char defaultSeparator = '/';
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsFalse(FileManager.NormalizePath(path).Contains(defaultSeparator));

            path = "";
            Assert.IsEmpty(FileManager.NormalizePath(path));

            path = "docs.microsoft.com";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsFalse(FileManager.NormalizePath(path).Contains(defaultSeparator));

            path = "c:\\";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Contains(defaultSeparator));

            path = "c:/";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Contains(defaultSeparator));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 1);

            path = "c:\\temp/";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 2);

            path = "c:\\temp/test";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 2);

            path = "c:/temp/test";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 2);

            path = "c:/temp/test/test.txt";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 3);

            path = "c:/temp/test\\test.txt";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 3);

            path = "http://microsoft.com";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 2);

            path = "test.txt";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 0);

            path = "\\test.txt";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 1);

            path = "\\test\\test.txt";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 2);

            path = "\\test\test.txt";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 1);

            path = "/";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 1);

            path = "\\";
            Assert.IsNotEmpty(FileManager.NormalizePath(path));
            Assert.IsTrue(FileManager.NormalizePath(path).Count(x => x.Equals(defaultSeparator)) == 1);
        }

        [Test()]
        public void PopulateCollectionTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ProcessFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ProcessFileTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ReadEtlTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ReadTraceRecordsTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void RelogBlgTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void SaveToCacheTest()
        {
            string tempFile = Path.GetTempFileName();
            FileManager fileManager = new FileManager(new Instance());

            Assert.IsTrue(File.Exists(tempFile));
            fileManager.DeleteFile(tempFile);

            FileObject fileObject = new FileObject(tempFile);
            List<DtrTraceRecord> records = new List<DtrTraceRecord>()
            {
                new DtrTraceRecord($"{DateTime.Now},Informational,456,123,test.type,\"test text\",nt0,fabric,/relative.uri", fileObject),
                new DtrTraceRecord($"{DateTime.Now},Warning,789,123,test2.type,\"test text 2\",nt1,fabric,/relative.uri", fileObject),
                new DtrTraceRecord($"{DateTime.Now},Error,123,456,test3.type,\"test text 3\",nt2,fabric,/relative.uri", fileObject)
            };

            fileObject.Stream.Write(records);

            fileManager.SaveToCache(fileObject, true);
            Assert.IsTrue(File.Exists(tempFile));

            fileManager.DeleteFile(tempFile);
            Assert.IsFalse(File.Exists(tempFile));
        }

        [Test()]
        public void SerializeCsvTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void SerializeJsonTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void TxBlgTest()
        {
            throw new NotImplementedException();
            string tempPath = TestUtilities.TempDir;
            string outputFile = $"{tempPath}/txetltest.json";
            string[] inputFiles = Directory.GetFiles("", $"{TestUtilities.TestDataFilesDir}/fabric_counters*.blg", SearchOption.AllDirectories);
            string inputFile = inputFiles[0];
            string logFile = $"{tempPath}\\etl.log";
            Assert.IsTrue(File.Exists(inputFile));

            ConfigurationOptions configurationOptions = new ConfigurationOptions
            {
                LogDebug = 5,
                LogFile = logFile,
                StartTimeStamp = DateTime.FromFileTimeUtc(0).ToString("O"),
                EndTimeStamp = DateTime.Now.ToString("O"),
                GatherType = FileTypesEnum.counter.ToString(),
                CacheLocation = tempPath,
                FileUris = new string[]
                {
                    inputFile
                }
            };

            Instance instance = new Instance(configurationOptions);
            FileManager fileManager = new FileManager(instance);
            FileObject fileObject = new FileObject(inputFile);

            File.Delete(outputFile);
            fileManager.TxBlg(fileObject, outputFile);
            Assert.IsTrue(File.Exists(outputFile), $"check log file {logFile}");
            File.Delete(outputFile);
        }

        [Test()]
        public void TxEtlTest()
        {
            string manifestPath = $"{TestUtilities.SolutionDir}/manifests";
            string tempPath = TestUtilities.TempDir;
            string outputFile = $"{tempPath}/txetltest.json";
            string[] inputFiles = Directory.GetFiles("", $"{TestUtilities.TestDataFilesDir}/fabric_traces_*.etl", SearchOption.AllDirectories);
            string inputFile = inputFiles[0];
            string logFile = $"{tempPath}\\etl.log";
            Assert.IsTrue(Directory.Exists(manifestPath));
            Assert.IsTrue(File.Exists(inputFile));

            ConfigurationOptions configurationOptions = new ConfigurationOptions
            {
                LogDebug = 5,
                LogFile = logFile,
                StartTimeStamp = DateTime.FromFileTimeUtc(0).ToString("O"),
                EndTimeStamp = DateTime.Now.ToString("O"),
                GatherType = FileTypesEnum.trace.ToString(),
                CacheLocation = tempPath,
                EtwManifestsCache = manifestPath,
                FileUris = new string[]
                {
                    inputFile
                }
            };

            Instance instance = new Instance(configurationOptions);
            FileManager fileManager = new FileManager(instance);
            FileObject fileObject = new FileObject(inputFile);

            File.Delete(outputFile);
            fileManager.ReadEtl(fileObject);
            Assert.IsTrue(File.Exists(outputFile), $"check log file {logFile}");
            File.Delete(outputFile);
        }
    }
}