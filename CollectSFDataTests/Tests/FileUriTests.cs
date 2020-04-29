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
    [TestFixture]
    public class FileUriTests : TestUtilities
    {
        [Test(Description = "any .etl file local test", TestOf = typeof(FileTypes))]
        public void FileAnyEtlLocalTest()
        {
            string fileUri = "c:\\temp\\sf-ps1\\_nt0_2\\test.etl";
            WriteConsole($"checking uri result {fileUri}");
            Assert.AreEqual(FileTypesEnum.any, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.unknown, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.unknown, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "any .etl file test", TestOf = typeof(FileTypes))]
        public void FileAnyEtlTest()
        {
            string fileUri = "sf-ps1/_nt0_2/test.etl";
            WriteConsole($"checking uri result {fileUri}");
            Assert.AreEqual(FileTypesEnum.any, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.unknown, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.unknown, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "counter blg file full uri", TestOf = typeof(FileTypes))]
        public void FileCounterBlgFullUriTest()
        {
            string fileUri = "https://sflogsbmnjfzoagi7jc2.blob.core.windows.net/fabriclogs-c9113ef8-4fea-420e-aa37-3be4401dce67/_nt0_0/Fabric/f45f24746c42cc2a6dd69da9e7797e2c_fabric_traces_6.3.187.9494_131861030069574505_1_00636772262289400052_2147483647.dtr.zip";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.counter, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.counter, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.blg, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "counter .blg file partial uri", TestOf = typeof(FileTypes))]
        public void FileCounterLocalTest()
        {
            string fileUri = "G:\\temp\\perftest5node\\fabriccounters-85be08a2-361a-4089-8a72-1268ce0ee591\\_nt0_0\\fabric_counters_636785815845605349_000001.blg";
            WriteConsole($"checking uri result {fileUri}");
            Assert.AreEqual(FileTypesEnum.counter, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.counter, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.blg, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "counter .blg file partial uri", TestOf = typeof(FileTypes))]
        public void FileCounterPartialUriTest()
        {
            string fileUri = "/0358/Collected/fabricperf-62821bc2-e537-4084-a505-615184da27ad/ECS-S-FABN-B04/fabric_counters_636830189613638393_000017.blg";
            WriteConsole($"checking uri result {fileUri}");
            Assert.AreEqual(FileTypesEnum.counter, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.counter, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.blg, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "exception .dmp local file", TestOf = typeof(FileTypes))]
        public void FileExceptionLocalTest()
        {
            string fileUri = "F:\\cases\\archive\\118091819033049\\09.05.10.00-09.05.12.30\\fabriccrashdumps-6fc5a92a-0c94-4e51-8589-db9691903011\\_vm-t4axcpod01-ocp-est_0\\FabricDCA.exe.4376.dmp";
            WriteConsole($"checking uri result {fileUri}");
            Assert.AreEqual(FileTypesEnum.exception, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabriccrashdumps, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.dmp, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "fabric csv local file", TestOf = typeof(FileTypes))]
        public void FileFabricCsvTraceLocalTest()
        {
            string fileUri = "G:\\temp\\dtrtest5nodenew\\_nt0_0.Fabric.181126.02-29-40_181126.03-50-49.004.csv";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.csv, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "fabric local dtr file test with backslashes", TestOf = typeof(FileTypes))]
        public void FileFabricDtrTraceLocalBSTest()
        {
            string fileUri = "c:\\temp\\fabriclogs-82ee81a7-9d95-4003-b81b-883fdf945eaf\\_nt0_2\\Fabric\\bc4316ec4b0814dcc367388a46d9903e_fabric_traces_7.0.470.9590_132301541323585400_1570_00637227551217830354_0000000000.dtr";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.dtr, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "fabric local dtr file test with forward slashes", TestOf = typeof(FileTypes))]
        public void FileFabricDtrTraceLocalFSTest()
        {
            string fileUri = "c:/temp/fabriclogs-82ee81a7-9d95-4003-b81b-883fdf945eaf/_nt0_2/Fabric/bc4316ec4b0814dcc367388a46d9903e_fabric_traces_7.0.470.9590_132301541323585400_1570_00637227551217830354_0000000000.dtr";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.dtr, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "fabric dtr file", TestOf = typeof(FileTypes))]
        public void FileFabricDtrTraceTest()
        {
            string fileUri = "fabriclogs-82ee81a7-9d95-4003-b81b-883fdf945eaf/_nt0_2/Fabric/bc4316ec4b0814dcc367388a46d9903e_fabric_traces_7.0.470.9590_132301541323585400_1570_00637227551217830354_0000000000.dtr";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.dtr, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "fabric zip file alternate path", TestOf = typeof(FileTypes))]
        public void FileFabricZipTraceAlternateTest()
        {
            string fileUri = "/5e07ccefb6d346508082dd9fa9c5ed71/FEFabric/FEFabric_IN_0/Fabric/f26ff4bedf68bd8a96ec0e36e5d4b5_fabric_traces_6.2.301.9494_131850706152861697_128_00636764220556393989_0000000000.dtr.zip";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.zip, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "fabric zip file full uri", TestOf = typeof(FileTypes))]
        public void FileFabricZipTraceFullUriTest()
        {
            string fileUri = "https://sflogsbmnjfzoagi7jc2.blob.core.windows.net/fabriclogs-c9113ef8-4fea-420e-aa37-3be4401dce67/_nt0_0/Fabric/f45f24746c42cc2a6dd69da9e7797e2c_fabric_traces_6.3.187.9494_131861030069574505_1_00636772262289400052_2147483647.dtr.zip";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.zip, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "fabric zip file partial uri", TestOf = typeof(FileTypes))]
        public void FileFabricZipTracePartialUriTest()
        {
            string fileUri = "/_nt0_0/Fabric/f45f24746c42cc2a6dd69da9e7797e2c_fabric_traces_6.3.187.9494_131861030069574505_1_00636772262289400052_2147483647.dtr.zip";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.zip, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "fabric zip file", TestOf = typeof(FileTypes))]
        public void FileFabricZipTraceTest()
        {
            string fileUri = "fabriclogs-82ee81a7-9d95-4003-b81b-883fdf945eaf/_nt0_2/Fabric/bc4316ec4b0814dcc367388a46d9903e_fabric_traces_7.0.470.9590_132301541323585400_1570_00637227551217830354_0000000000.dtr.zip";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.zip, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "lease csv local file", TestOf = typeof(FileTypes))]
        public void FileLeaseCsvTraceLocalTest()
        {
            string fileUri = "G:\\temp\\dtrtest5nodenew\\_nt0_0.Lease.181126.01-35-16_181126.03-50-49.003.csv";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.lease, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.csv, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "lease dtr local file", TestOf = typeof(FileTypes))]
        public void FileLeaseDtrTraceLocalTest()
        {
            string fileUri = "G:\\temp\\dtrtest5nodenew\\fabriclogs-9ad814a1-0c6e-41d9-8db5-a21bc64fcb2b\\_nt0_0\\Lease\\f45f24746c42cc2a6dd69da9e7797e2c_lease_traces_6.3.187.9494_131876365524017503_1_00636787597571191225_2147483647.dtr";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.lease, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.dtr, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "lease dtr file", TestOf = typeof(FileTypes))]
        public void FileLeaseDtrTraceTest()
        {
            string fileUri = "fabriclogs-82ee81a7-9d95-4003-b81b-883fdf945eaf/_nt0_2/Fabric/bc4316ec4b0814dcc367388a46d9903e_lease_traces_7.0.470.9590_132301541323585400_1570_00637227551217830354_0000000000.dtr";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.lease, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.dtr, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "lease zip file", TestOf = typeof(FileTypes))]
        public void FileLeaseZipTraceTest()
        {
            string fileUri = "fabriclogs-82ee81a7-9d95-4003-b81b-883fdf945eaf/_nt0_2/Fabric/bc4316ec4b0814dcc367388a46d9903e_lease_traces_7.0.470.9590_132301541323585400_1570_00637227551217830354_0000000000.dtr.zip";
            WriteConsole($"checking uri result {fileUri}");

            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.lease, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.zip, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "setup fabric deployer trace local file", TestOf = typeof(FileTypes))]
        public void FileSetupLocalTest()
        {
            string fileUri = "c:/temp/001/fabriclogs-2b692f00-1604-4e00-8537-04cb88243c1e/_nt0_3/Bootstrap/a0350f0b15d5495ac172a2b2b8b0ff4f_FabricDeployer-636817856041415553.trace";
            WriteConsole($"checking uri result {fileUri}");
            Assert.AreEqual(FileTypesEnum.setup, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabricdeployer, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.trace, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "", TestOf = typeof(FileTypes))]
        public void FileTableTest()
        {
            string fileUri = "";
            WriteConsole($"checking uri result {fileUri}");
            Assert.AreEqual(FileTypesEnum.table, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.table, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.csv, FileTypes.MapKnownFileExtension(fileUri));
        }

        [Test(Description = "", TestOf = typeof(FileTypes))]
        public void FileTraceTest()
        {
            string fileUri = "";
            WriteConsole($"checking uri result {fileUri}");
            Assert.AreEqual(FileTypesEnum.trace, FileTypes.MapFileTypeUri(fileUri));
            Assert.AreEqual(FileDataTypesEnum.fabric, FileTypes.MapFileDataTypeUri(fileUri));
            Assert.AreEqual(FileExtensionTypesEnum.zip, FileTypes.MapKnownFileExtension(fileUri));
        }
    }
}