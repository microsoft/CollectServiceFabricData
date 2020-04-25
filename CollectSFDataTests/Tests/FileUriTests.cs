// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData;
using Newtonsoft.Json;
using NUnit.Framework;
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

    [TestFixture]
    public class FileUriTests : TestUtilities
    {
        private string FileUriDataFile = $"{TestUtilities.TestFilesDir}\\FileUriTests.json";
        private List<FileUri> FileUriList;

        public FileUriTests()
        {
            Assert.IsTrue(File.Exists(FileUriDataFile));
            FileUriList = JsonConvert.DeserializeObject<FileUris>(File.ReadAllText(FileUriDataFile)).FileUri.ToList();
        }

        [Test]
        public void FileTypeAnyTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.any))
            {
                WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.any, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [Test]
        public void FileTypeCounterTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.counter))
            {
                WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.counter, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [Test]
        public void FileTypeExceptionTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.exception))
            {
                WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.exception, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [Test]
        public void FileTypeSetupTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.setup))
            {
                WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.setup, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [Test]
        public void FileTypeTableTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.table))
            {
                WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.table, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }

        [Test]
        public void FileTypeTraceTest()
        {
            foreach (FileUri fileUri in FileUriList.Where(x => x.FileType == FileTypesEnum.trace))
            {
                WriteConsole($"checking uri result {fileUri.Uri}", fileUri);
                Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri.Uri));
            }
        }
    }
}