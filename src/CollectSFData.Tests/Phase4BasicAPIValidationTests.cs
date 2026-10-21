using System;
using System.Linq;
using System.Threading.Tasks;
using CollectSFData.Azure;
using CollectSFData.DataFile;
using CollectSFData.Common;
using FluentAssertions;
using Xunit;

namespace CollectSFData.Tests
{
    [Collection("TestAuditCollection")]
    public class Phase4BasicAPIValidationTests
    {
        [Fact]
        public void API_FileStatus_EnumValues_ShouldBeCorrect()
        {
            // Validate FileStatus enum values based on FileObjectStatus.cs
            var enumValues = Enum.GetValues(typeof(FileStatus)).Cast<FileStatus>().ToArray();
            
            enumValues.Should().Contain(FileStatus.unknown);
            enumValues.Should().Contain(FileStatus.enumerated);
            enumValues.Should().Contain(FileStatus.existing);
            enumValues.Should().Contain(FileStatus.queued);
            enumValues.Should().Contain(FileStatus.downloading);
            enumValues.Should().Contain(FileStatus.formatting);
            enumValues.Should().Contain(FileStatus.formatted);
            enumValues.Should().Contain(FileStatus.uploading);
            enumValues.Should().Contain(FileStatus.failed);
            enumValues.Should().Contain(FileStatus.succeeded);
            enumValues.Should().Contain(FileStatus.all);
        }

        [Fact]
        public void API_ConfigurationOptions_GatherType_ShouldAcceptString()
        {
            // Validate GatherType property accepts string values
            var config = new ConfigurationOptions
            {
                GatherType = "trace" // String value, not enum
            };
            
            config.GatherType.Should().Be("trace");
            
            // Test other valid values
            config.GatherType = "counter";
            config.GatherType.Should().Be("counter");
        }

        [Fact]
        public void API_ConfigurationOptions_SasEndpointInfo_ShouldBeSasEndpointsType()
        {
            // Validate SasEndpointInfo property is of type SasEndpoints
            var config = new ConfigurationOptions
            {
                SasEndpointInfo = new SasEndpoints()
            };
            
            config.SasEndpointInfo.Should().NotBeNull();
            config.SasEndpointInfo.Should().BeOfType<SasEndpoints>();
        }

        [Fact]
        public void API_FileObjectCollection_Count_ShouldAcceptFileStatus()
        {
            // Validate FileObjectCollection.Count(FileStatus) method exists
            var collection = new FileObjectCollection();
            
            // Add some test files with different statuses
            var file1 = new FileObject("test1.etl") { Status = FileStatus.enumerated };
            var file2 = new FileObject("test2.etl") { Status = FileStatus.formatted };
            var file3 = new FileObject("test3.etl") { Status = FileStatus.failed };
            
            collection.AddRange(new[] { file1, file2, file3 });
            
            // Test Count method with FileStatus parameter
            collection.Count(FileStatus.enumerated).Should().Be(1);
            collection.Count(FileStatus.formatted).Should().Be(1);
            collection.Count(FileStatus.failed).Should().Be(1);
            collection.Count(FileStatus.succeeded).Should().Be(0);
        }

        [Fact]
        public void API_FileObject_PropertiesAccess_ShouldWorkCorrectly()
        {
            // Test FileObject property access patterns
            var fileObject = new FileObject("test.etl");
            
            // Test readable properties
            fileObject.FileUri.Should().NotBeNullOrEmpty();
            fileObject.RelativeUri.Should().NotBeNullOrEmpty();
            
            // Test settable properties
            fileObject.Status = FileStatus.enumerated;
            fileObject.Status.Should().Be(FileStatus.enumerated);
            
            // FileExtensionType is read-only - do not attempt to set it
            // Instead, test that it can be read
            var extensionType = fileObject.FileExtensionType;
            extensionType.ToString().Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task API_BasicWorkflow_ShouldNotThrowErrors()
        {
            // Test basic API workflow without actual processing
            var config = new ConfigurationOptions
            {
                GatherType = "trace",
                SasEndpointInfo = new SasEndpoints()
            };
            
            var collection = new FileObjectCollection();
            
            // Create test files
            for (int i = 0; i < 5; i++)
            {
                var fileObject = new FileObject($"test-{i:D3}.etl")
                {
                    Status = FileStatus.enumerated
                };
                collection.Add(fileObject);
            }
            
            // Verify collection state
            collection.Count().Should().Be(5);
            collection.Count(FileStatus.enumerated).Should().Be(5);
            collection.Count(FileStatus.formatted).Should().Be(0);
            
            // Simulate status changes
            foreach (var file in collection)
            {
                file.Status = FileStatus.formatted;
            }
            
            collection.Count(FileStatus.enumerated).Should().Be(0);
            collection.Count(FileStatus.formatted).Should().Be(5);
            
            await Task.CompletedTask; // Async pattern validation
        }

        [Fact]
        public void API_ConfigurationValidation_ShouldHandleValidScenarios()
        {
            // Test configuration scenarios that should work
            var config1 = new ConfigurationOptions
            {
                GatherType = "trace",
                SasEndpointInfo = new SasEndpoints()
            };
            
            var config2 = new ConfigurationOptions
            {
                GatherType = "counter",
                SasEndpointInfo = new SasEndpoints()
            };
            
            // Both configurations should be valid
            config1.GatherType.Should().Be("trace");
            config2.GatherType.Should().Be("counter");
            
            config1.SasEndpointInfo.Should().NotBeNull();
            config2.SasEndpointInfo.Should().NotBeNull();
        }
    }
}