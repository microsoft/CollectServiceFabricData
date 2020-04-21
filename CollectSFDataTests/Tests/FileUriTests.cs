using CollectSFData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CollectSFDataTests
{
    public class FileUri
    {
        public FileDataTypesEnum DataType;
        public string Description;
        public FileTypesEnum FileType;
        public string Uri;
    }

    public class FileUris
    {
        public FileUri[] FileUri;
    }

    [TestClass()]
    public class FileUriTests
    {
        private string FileUriDataFile = $"{TestUtilities.TestFilesDir}\\FileUriTests.json";
        private List<FileUri> FileUriList;

        public FileUriTests()
        {
            Assert.IsTrue(File.Exists(FileUriDataFile));
            FileUriList = JsonConvert.DeserializeObject<FileUris>(File.ReadAllText(FileUriDataFile)).FileUri.ToList();
        }

        [TestMethod()]
        public void FileTypeAnyTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.any))
            {
                TestUtilities.WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.any, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeCounterTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.counter))
            {
                TestUtilities.WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.counter, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeExceptionTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.exception))
            {
                TestUtilities.WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.exception, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeSetupTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.setup))
            {
                TestUtilities.WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.setup, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeTableTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.table))
            {
                TestUtilities.WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.table, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeTraceTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.trace))
            {
                TestUtilities.WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }
    }
}