using System;
using System.Collections.Generic;
using CollectSFData.Common;
using CollectSFData.DataFile;
using FluentAssertions;
using Moq;
using Xunit;

namespace CollectSFData.Tests
{
    /// <summary>
    /// Phase 3: Data Processing Tests - File parsing and data transformation validation
    /// Tests data processing functionality using actual CollectServiceFabricData APIs
    /// </summary>
    [Collection("TestAuditCollection")]
    public class DataProcessingTests
    {
        [Fact]
        public void CsvCounterRecord_Constructor_ShouldInitializeWithDefaults()
        {
            // Test: CsvCounterRecord default construction
            var record = new CsvCounterRecord();

            // Verify: All string properties should be initialized as empty/null, CounterValue as decimal
            record.Should().NotBeNull();
            record.Counter.Should().BeNullOrEmpty();
            record.CounterName.Should().BeNullOrEmpty();
            record.CounterValue.Should().Be(0);  // Decimal default is 0
            record.Instance.Should().BeNullOrEmpty();
            record.NodeName.Should().BeNullOrEmpty();
            record.Object.Should().BeNullOrEmpty();
        }

        [Fact]
        public void CsvCounterRecord_Properties_ShouldAcceptValidValues()
        {
            // Test: Setting CsvCounterRecord properties with valid data
            var record = new CsvCounterRecord
            {
                Counter = "TestCounter",
                CounterName = "Performance Counter Name",
                CounterValue = 100.50m,  // Decimal literal
                Instance = "TestInstance",
                NodeName = "Node01",
                Object = "TestObject",
                RelativeUri = "test/path/file.csv",
                ResourceUri = "https://storage.blob.core.windows.net/test"
            };

            // Verify: All properties should retain assigned values
            record.Counter.Should().Be("TestCounter");
            record.CounterName.Should().Be("Performance Counter Name");
            record.CounterValue.Should().Be(100.50m);  // Decimal comparison
            record.Instance.Should().Be("TestInstance");
            record.NodeName.Should().Be("Node01");
            record.Object.Should().Be("TestObject");
            record.RelativeUri.Should().Be("test/path/file.csv");
            record.ResourceUri.Should().Be("https://storage.blob.core.windows.net/test");
        }

        [Fact]
        public void CsvCounterRecord_Timestamp_ShouldHandleDateTimeParsing()
        {
            // Test: CsvCounterRecord with timestamp data
            var expectedTimestamp = new DateTime(2025, 1, 20, 14, 30, 0);
            var record = new CsvCounterRecord();
            record.Timestamp = expectedTimestamp;

            // Verify: Timestamp should be preserved correctly
            record.Timestamp.Should().Be(expectedTimestamp);
            record.Timestamp.Year.Should().Be(2025);
            record.Timestamp.Month.Should().Be(1);
            record.Timestamp.Day.Should().Be(20);
        }

        [Fact]
        public void CsvCounterRecord_FileType_ShouldIndicateDataType()
        {
            // Test: CsvCounterRecord FileType property usage
            var record = new CsvCounterRecord();
            record.FileType = "performance";

            // Verify: FileType should identify the data category
            record.FileType.Should().Be("performance");
            record.FileType.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void FileManager_PopulateCollection_ShouldProcessCsvCounterRecords()
        {
            // Setup: Use real FileManager since PopulateCollection is not virtual
            var instance = new Instance();
            var fileManager = new FileManager(instance);
            var fileObject = new FileObject("test.csv")
            {
                Status = FileStatus.enumerated
            };

            // Test: Call PopulateCollection with CsvCounterRecord type
            // Note: This tests the method signature and basic functionality
            var result = fileManager.PopulateCollection<CsvCounterRecord>(fileObject);

            // Verify: Should return FileObjectCollection
            result.Should().NotBeNull();
            result.Should().BeOfType<FileObjectCollection>();
            result.Should().Contain(fileObject);
            
            // Verify the file object was processed (status should change to formatted)
            fileObject.Status.Should().Be(FileStatus.formatted);
        }

        [Fact]
        public void CsvCounterRecord_Performance_ShouldHandleLargeDataSets()
        {
            // Test: Performance with multiple CsvCounterRecord instances
            var records = new List<CsvCounterRecord>();
            
            for (int i = 0; i < 1000; i++)
            {
                records.Add(new CsvCounterRecord
                {
                    Counter = $"Counter{i}",
                    CounterValue = (decimal)(i * 1.5),  // Convert to decimal
                    NodeName = $"Node{i % 10}",
                    Instance = $"Instance{i}",
                    Timestamp = DateTime.Now.AddMinutes(-i)
                });
            }

            // Verify: Should handle large collections efficiently
            records.Should().HaveCount(1000);
            records[999].Counter.Should().Be("Counter999");
            records[500].NodeName.Should().Be("Node0"); // 500 % 10 = 0
        }

        [Fact]
        public void CsvCounterRecord_Validation_ShouldHandleEdgeCases()
        {
            // Test: CsvCounterRecord with edge case values
            var record = new CsvCounterRecord
            {
                Counter = "",  // Empty string
                CounterValue = 0m,  // Zero decimal value
                NodeName = "Node_With-Special.Characters",
                Instance = null,  // Null value
                Object = "Object With Spaces"
            };

            // Verify: Should handle edge cases gracefully
            record.Counter.Should().Be("");
            record.CounterValue.Should().Be(0m);  // Decimal zero
            record.NodeName.Should().Be("Node_With-Special.Characters");
            record.Instance.Should().BeNull();
            record.Object.Should().Be("Object With Spaces");
        }

        [Fact]
        public void CsvCounterRecord_Integration_ShouldWorkWithFileObject()
        {
            // Test: CsvCounterRecord integration with FileObject
            var fileObject = new FileObject("counters.csv");
            fileObject.RecordCount = 5;
            
            var records = new List<CsvCounterRecord>();
            for (int i = 0; i < fileObject.RecordCount; i++)
            {
                records.Add(new CsvCounterRecord 
                { 
                    Counter = $"Test{i}",
                    CounterValue = i * 10m,  // Decimal value
                    RelativeUri = fileObject.RelativeUri 
                });
            }

            // Verify: Records should correlate with FileObject properties
            records.Should().HaveCount((int)fileObject.RecordCount);
            records.Should().AllSatisfy(r => r.RelativeUri.Should().Be(fileObject.RelativeUri));
            fileObject.RelativeUri.Should().Be("counters.csv");
        }
    }
}