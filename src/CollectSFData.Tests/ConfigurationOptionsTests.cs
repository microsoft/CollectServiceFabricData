using System;
using CollectSFData.Common;
using CollectSFData.DataFile;
using FluentAssertions;
using Moq;
using Xunit;

namespace CollectSFData.Tests
{
    [Collection("TestAuditCollection")]
    public class ConfigurationOptionsTests
    {
        [Theory]
        [InlineData("2025-09-11T10:15:30+00:00")]
        [InlineData("2025-09-11T10:15:30Z")]
        [InlineData("09/11/2025 10:15:30 -00:00")]
        public void ConvertToUtcTime_ValidFormats_ReturnsExpected(string input)
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var expectedTime = new DateTime(2025, 9, 11, 10, 15, 30, DateTimeKind.Utc);
            mockTimeProvider.Setup(x => x.ConvertToUtcTime(input)).Returns(expectedTime);
            
            var opts = new TestableConfigurationOptions(mockTimeProvider.Object);

            // Act
            var utc = opts.ConvertToUtcTime(input);

            // Assert
            utc.Should().Be(expectedTime);
            utc.Kind.Should().Be(DateTimeKind.Utc);
            mockTimeProvider.Verify(x => x.ConvertToUtcTime(input), Times.Once);
        }

        [Fact]
        public void ConvertToUtcTime_InvalidFormat_ReturnsMinValue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var invalidInput = "not-a-date";
            mockTimeProvider.Setup(x => x.ConvertToUtcTime(invalidInput)).Returns(DateTime.MinValue);
            
            var opts = new TestableConfigurationOptions(mockTimeProvider.Object);

            // Act
            var utc = opts.ConvertToUtcTime(invalidInput);

            // Assert
            utc.Should().Be(DateTime.MinValue);
            mockTimeProvider.Verify(x => x.ConvertToUtcTime(invalidInput), Times.Once);
        }

        [Theory]
        [InlineData("trace", "trace", true)]
        [InlineData("TRaCe", "trace", true)]
        [InlineData("unknownvalue", "unknown", false)]
        public void GatherType_Assignment_NormalizesAndSetsFileType(string input, string expectedFileType, bool valid)
        {
            // Arrange
            var opts = new TestableConfigurationOptions();

            // Act
            opts.GatherType = input; // side effect sets FileType via property override

            // Assert
            opts.FileType.ToString().Should().Be(expectedFileType);
            if (!valid)
            {
                opts.FileType.Should().Be(FileTypesEnum.unknown);
            }
        }

        [Theory]
        [InlineData("trace_table", "trace_table")] // already prefixed once
        [InlineData("TrAcE_table", "trace_table")] // case-insensitive normalization
        [InlineData("table", "trace_table")]       // missing prefix gets added when saving destination scenario (simulate behavior)
        public void CleanTableName_Behavior_ViaDestinationValidation(string input, string expected)
        {
            // Arrange
            var opts = new TestableConfigurationOptions();
            // configure minimal Kusto so IsKustoConfigured true
            opts.GatherType = "trace"; // sets FileType
            opts.KustoCluster = "https://ingest-test.kusto.windows.net/db";
            opts.KustoTable = input;

            // Act
            // trigger validation path that prefixes (ValidateDestination -> CleanTableName(withGatherType:true))
            opts.ValidateDestination();

            // Assert
            opts.KustoTable.Should().Be(expected);
        }

        [Fact]
        public void CurrentTime_UsesMockedTimeProvider()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var fixedTime = new DateTime(2025, 9, 11, 14, 30, 0);
            mockTimeProvider.Setup(x => x.Now).Returns(fixedTime);
            
            var opts = new TestableConfigurationOptions(mockTimeProvider.Object);

            // Act
            var currentTime = opts.CurrentTime;

            // Assert
            currentTime.Should().Be(fixedTime);
            mockTimeProvider.Verify(x => x.Now, Times.Once);
        }

        [Fact]
        public void CurrentUtcTime_UsesMockedTimeProvider()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var fixedUtcTime = new DateTime(2025, 9, 11, 14, 30, 0, DateTimeKind.Utc);
            mockTimeProvider.Setup(x => x.UtcNow).Returns(fixedUtcTime);
            
            var opts = new TestableConfigurationOptions(mockTimeProvider.Object);

            // Act
            var currentUtcTime = opts.CurrentUtcTime;

            // Assert
            currentUtcTime.Should().Be(fixedUtcTime);
            mockTimeProvider.Verify(x => x.UtcNow, Times.Once);
        }
    }
}
