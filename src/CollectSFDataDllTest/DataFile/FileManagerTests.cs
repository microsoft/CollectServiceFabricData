using NUnit.Framework;
using CollectSFData.DataFile;
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            char defaultSeparator = Convert.ToChar("/");
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
            Instance instance = new Instance();
            FileManager fileManager = new FileManager(instance);

            Assert.IsTrue(File.Exists(tempFile));
            fileManager.DeleteFile(tempFile);

            FileObject fileObject = new FileObject(tempFile);
            List<DtrTraceRecord> records = new List<DtrTraceRecord>();
            string traceRecord = $"{DateTime.Now},Informational,456,123,test.type,\"test text\",_nt0_0,fabric,/relative.uri";
            DtrTraceRecord dtrTraceRecord = new DtrTraceRecord(traceRecord, fileObject);

            records.Add(dtrTraceRecord);
            fileObject.Stream.Write(records);

            fileManager.SaveToCache(fileObject);

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
        }

        [Test()]
        public void TxEtlTest()
        {
            throw new NotImplementedException();
        }
    }
}