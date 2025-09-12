using System;
using CollectSFData.Common;
using CollectSFData.DataFile;
using FluentAssertions;
using Xunit;

namespace CollectSFData.Tests
{
    public class ConfigurationOptionsTests
    {
        [Theory]
        [InlineData("2025-09-11T10:15:30+00:00")]
        [InlineData("2025-09-11T10:15:30Z")]
        [InlineData("09/11/2025 10:15:30 -00:00")]
        public void ConvertToUtcTime_ValidFormats_ReturnsExpected(string input)
        {
            var opts = new ConfigurationOptions();
            var utc = opts.ConvertToUtcTime(input);
            utc.Should().NotBe(DateTime.MinValue);
            utc.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void ConvertToUtcTime_InvalidFormat_ReturnsMinValue()
        {
            var opts = new ConfigurationOptions();
            var utc = opts.ConvertToUtcTime("not-a-date");
            utc.Should().Be(DateTime.MinValue);
        }

        [Theory]
        [InlineData("trace", "trace", true)]
        [InlineData("TRaCe", "trace", true)]
        [InlineData("unknownvalue", "unknown", false)]
        public void GatherType_Assignment_NormalizesAndSetsFileType(string input, string expectedFileType, bool valid)
        {
            var opts = new ConfigurationOptions();
            opts.GatherType = input; // side effect sets FileType via property override
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
            var opts = new ConfigurationOptions();
            // configure minimal Kusto so IsKustoConfigured true
            opts.GatherType = "trace"; // sets FileType
            opts.KustoCluster = "https://ingest-test.kusto.windows.net/db";
            opts.KustoTable = input;
            // trigger validation path that prefixes (ValidateDestination -> CleanTableName(withGatherType:true))
            opts.ValidateDestination();
            opts.KustoTable.Should().Be(expected);
        }
    }
}
