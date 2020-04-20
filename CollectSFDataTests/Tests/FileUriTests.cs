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
        public FileExtensionTypesEnum ExtensionType;
        public FileTypesEnum FileType;
        public string Uri;
    }

    public class FileUris //: List<FileUri>
    {
        public FileUri[] FileUri;
    }

    [TestClass()]
    public class FileUriTests
    {
        private string FileUriDataFile = $"{TestUtilities.TestFilesDir}\\FileUriTests.json";
        private List<FileUri> FileUris;

        public FileUriTests()
        {
            Assert.IsTrue(File.Exists(FileUriDataFile));
            FileUris = JsonConvert.DeserializeObject<List<FileUri>>(File.ReadAllText(FileUriDataFile));
        }

        [TestMethod()]
        public void FileTypeAnyTest()
        {
            foreach (var fileUri in FileUris.Where(x => x.FileType == FileTypesEnum.any))
            {
                Log.Info($"checking file", ConsoleColor.Black, ConsoleColor.White, fileUri);
                Assert.AreEqual(FileTypesEnum.any, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeCounterTest()
        {
            foreach (var fileUri in FileUris.Where(x => x.FileType == FileTypesEnum.counter))
            {
                Log.Info($"checking file", ConsoleColor.Black, ConsoleColor.White, fileUri);
                Assert.AreEqual(FileTypesEnum.counter, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeExceptionTest()
        {
            foreach (var fileUri in FileUris.Where(x => x.FileType == FileTypesEnum.exception))
            {
                Log.Info($"checking file", ConsoleColor.Black, ConsoleColor.White, fileUri);
                Assert.AreEqual(FileTypesEnum.exception, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeSetupTest()
        {
            foreach (var fileUri in FileUris.Where(x => x.FileType == FileTypesEnum.setup))
            {
                Log.Info($"checking file", ConsoleColor.Black, ConsoleColor.White, fileUri);
                Assert.AreEqual(FileTypesEnum.setup, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeTableTest()
        {
            foreach (var fileUri in FileUris.Where(x => x.FileType == FileTypesEnum.table))
            {
                Log.Info($"checking file", ConsoleColor.Black, ConsoleColor.White, fileUri);
                Assert.AreEqual(FileTypesEnum.table, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [TestMethod()]
        public void FileTypeTraceTest()
        {
            foreach (var fileUri in FileUris.Where(x => x.FileType == FileTypesEnum.trace))
            {
                Log.Info($"checking file", ConsoleColor.Black, ConsoleColor.White, fileUri);
                Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }
    }
}