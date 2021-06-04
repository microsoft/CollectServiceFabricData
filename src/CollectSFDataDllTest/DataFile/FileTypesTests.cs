using NUnit.Framework;
using CollectSFData.Common;
using CollectSFData.DataFile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectSFData.DataFile.Tests
{
    [TestFixture()]
    public class FileTypesTests
    {
        [Test()]
        public void MapFileDataTypeExtensionTest()
        {
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension("c:\\") == FileDataTypesEnum.unknown);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension("") == FileDataTypesEnum.unknown);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension(null) == FileDataTypesEnum.unknown);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension("noextension") == FileDataTypesEnum.unknown);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension("./test.csv") == FileDataTypesEnum.unknown);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension("./test.etl") == FileDataTypesEnum.unknown);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension("./test.json") == FileDataTypesEnum.unknown);

            ExtensionTests(Constants.CsvExtension, FileDataTypesEnum.unknown);
            ExtensionTests(Constants.DumpExtension, FileDataTypesEnum.fabriccrashdumps);
            ExtensionTests(Constants.EtlExtension, FileDataTypesEnum.unknown);
            ExtensionTests(Constants.JsonExtension, FileDataTypesEnum.unknown);
            ExtensionTests(Constants.PerfCsvExtension, FileDataTypesEnum.counter);
            ExtensionTests(Constants.PerfCtrExtension, FileDataTypesEnum.counter);
            ExtensionTests(Constants.SetupExtension, FileDataTypesEnum.fabricsetup);
            ExtensionTests(Constants.TableExtension, FileDataTypesEnum.table);
            ExtensionTests(Constants.DtrExtension, FileDataTypesEnum.fabric);
            ExtensionTests(Constants.DtrZipExtension, FileDataTypesEnum.fabric);
            ExtensionTests(Constants.ZipExtension, FileDataTypesEnum.unknown);
        }

        [Test()]
        public void MapFileTypeRelativeUriPrefixTest()
        {
            Assert.IsTrue(FileTypes.MapFileTypeRelativeUriPrefix(FileTypesEnum.any) == FileTypesKnownUrisPrefix.any);
            Assert.IsTrue(FileTypes.MapFileTypeRelativeUriPrefix(FileTypesEnum.counter) == FileTypesKnownUrisPrefix.fabriccounter);
            Assert.IsTrue(FileTypes.MapFileTypeRelativeUriPrefix(FileTypesEnum.exception) == FileTypesKnownUrisPrefix.fabriccrashdump);
            Assert.IsTrue(FileTypes.MapFileTypeRelativeUriPrefix(FileTypesEnum.setup) == FileTypesKnownUrisPrefix.fabriclog);
            Assert.IsTrue(FileTypes.MapFileTypeRelativeUriPrefix(FileTypesEnum.table) == FileTypesKnownUrisPrefix.fabriclog);
            Assert.IsTrue(FileTypes.MapFileTypeRelativeUriPrefix(FileTypesEnum.trace) == FileTypesKnownUrisPrefix.fabriclog);
            Assert.IsTrue(FileTypes.MapFileTypeRelativeUriPrefix(FileTypesEnum.unknown) == FileTypesKnownUrisPrefix.unknown);
        }

        [Test()]
        public void MapKnownFileExtensionTest()
        {
            var result = FileTypes.MapKnownFileExtension("./test.zip");
            Assert.IsTrue(result == FileExtensionTypesEnum.zip, result.ToString());

            result = FileTypes.MapKnownFileExtension("./test.dtr.zip");
            Assert.IsTrue(result.HasFlag(FileExtensionTypesEnum.zip), result.ToString());

            result = FileTypes.MapKnownFileExtension("./test.etl.zip");
            Assert.IsTrue(result == FileExtensionTypesEnum.zip, result.ToString());
        }

        private static void ExtensionTests(string extension, FileDataTypesEnum dataType)
        {
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"./test{extension}") == dataType);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"/test{extension}") == dataType);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"test{extension}") == dataType);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"c:/test{extension}") == dataType);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"c:\\test{extension}") == dataType);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"c:\\temp/test{extension}") == dataType);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"./test/perf/csv/test{extension}") == dataType);

            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"./test/perf/csv/test{extension}.test") == FileDataTypesEnum.unknown);
            Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"{extension}.test") == FileDataTypesEnum.unknown);
            //Assert.IsTrue(FileTypes.MapFileDataTypeExtension($"{extension}") == FileDataTypesEnum.unknown);
        }
    }
}