using System;
using CollectSFData.Common;
using CollectSFData.DataFile;
using FluentAssertions;
using Xunit;

namespace CollectSFData.Tests
{
    [Collection("TestAuditCollection")]
    public class FileManagerProcessTests
    {
        [Fact]
        public void PopulateCollection_NullFileObject_ThrowsArgumentNullException()
        {
            var instance = new Instance();
            var fileManager = new FileManager(instance);
            Action act = () => fileManager.PopulateCollection<CsvCounterRecord>(null);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("fileObject");
        }

        [Fact]
        public void ProcessFile_WithUnknownFileDataTypeAndAnyFileType_ReturnsEmptyCollection()
        {
            var instance = new Instance();
            var fileManager = new FileManager(instance);
            var fileObject = new FileObject("unknown.bin")
            {
                // Simulate FileType any and unknown data type
                Status = FileStatus.enumerated
            };
            // Force file data type to unknown; FileType defaults to any for unrecognized extension
            fileObject.FileDataType = FileDataTypesEnum.unknown;

            var result = fileManager.ProcessFile(fileObject);
            result.Should().NotBeNull();
            result.Count().Should().Be(0);
        }
    }
}
