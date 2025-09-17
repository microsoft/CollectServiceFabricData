using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CollectSFData.Common;
using CollectSFData.DataFile;
using FluentAssertions;
using Xunit;

namespace CollectSFData.Tests
{
    // Synthetic record type to control serialized size (CSV uses ToString()).
    internal class SyntheticCsvRecord : IRecord
    {
        public string Payload { get; set; } = string.Empty;
        public string RelativeUri { get; set; } = string.Empty;
        public string ResourceUri { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public IRecord Populate(FileObject fileObject, string record, string resourceUri = "")
        {
            Payload = record;
            RelativeUri = fileObject.RelativeUri;
            ResourceUri = resourceUri ?? fileObject.FileUri;
            return this;
        }
        public override string ToString() => Payload + Environment.NewLine; // newline like other records
    }

    // Simple JSON-like record; SerializeJson uses fileObject.Stream.Read<T>() so we only need ToString plus RelativeUri setter.
    internal class SyntheticJsonRecord : IRecord
    {
        public string Payload { get; set; } = string.Empty;
        public string RelativeUri { get; set; } = string.Empty;
        public string ResourceUri { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public IRecord Populate(FileObject fileObject, string record, string resourceUri = "")
        {
            Payload = record;
            RelativeUri = fileObject.RelativeUri;
            ResourceUri = resourceUri ?? fileObject.FileUri;
            return this;
        }
        public override string ToString() => $"{{\"p\":\"{Payload}\"}}" + Environment.NewLine;
    }

    [Collection("TestAuditCollection")]
    public class SerializationTests
    {
        private static FileObject BuildCsvFile(int recordCount, int recordPayloadBytes, Instance instance)
        {
            var fileObject = new FileObject("synthetic.csv")
            {
                FileDataType = FileDataTypesEnum.counter, // triggers Csv path when Kusto configured
                Status = FileStatus.enumerated
            };

            var records = new List<SyntheticCsvRecord>(recordCount);
            string payload = new string('A', recordPayloadBytes);
            for (int i = 0; i < recordCount; i++)
            {
                records.Add(new SyntheticCsvRecord { Payload = payload });
            }
            fileObject.Stream.Write(records);
            instance.FileObjects.Add(fileObject);
            return fileObject;
        }

        private static FileObject BuildJsonFile(int recordCount, int recordPayloadBytes, Instance instance)
        {
            var fileObject = new FileObject("synthetic.json")
            {
                FileDataType = FileDataTypesEnum.counter, // not Kusto -> JSON if Log Analytics configured
                Status = FileStatus.enumerated
            };

            var records = new List<SyntheticJsonRecord>(recordCount);
            string payload = new string('B', recordPayloadBytes);
            for (int i = 0; i < recordCount; i++)
            {
                records.Add(new SyntheticJsonRecord { Payload = payload });
            }
            fileObject.Stream.Write(records);
            instance.FileObjects.Add(fileObject);
            return fileObject;
        }

        private Instance NewInstance(bool kustoConfigured = false, bool logAnalyticsConfigured = false)
        {
            var config = new ConfigurationOptions(validate: false, loadDefaultConfig: false);
            if (kustoConfigured)
            {
                config.KustoCluster = "https://ingest-test.eastus.kusto.windows.net/db";
                config.KustoTable = "table";
            }
            if (logAnalyticsConfigured)
            {
                config.LogAnalyticsId = "id";
                config.LogAnalyticsKey = "key";
                config.LogAnalyticsName = "name";
            }
            // force validation to set internal flags used by IsKustoConfigured / IsLogAnalyticsConfigured
            config.Validate();
            var instance = new Instance(config);
            return instance;
        }

        [Fact]
        public void SerializeCsv_Splits_When_Exceeding_MaxCsvTransmitBytes()
        {
            var instance = NewInstance(kustoConfigured: true); // ensures CSV serialization path
            var fileManager = new FileManager(instance);

            // Choose payload so that adding one more record forces a split.
            // Each record serialized length = recordPayloadBytes + newline.
            int targetBytes = Constants.MaxCsvTransmitBytes;
            int recordPayload = 1024; // 1 KB per record (approx)
            int recordsNeeded = (targetBytes / (recordPayload + Environment.NewLine.Length)) + 2; // ensure overflow

            var fileObject = BuildCsvFile(recordsNeeded, recordPayload, instance);

            var collection = fileManager.PopulateCollection<SyntheticCsvRecord>(fileObject);

            collection.Count().Should().BeGreaterThan(1, "expected multiple CSV segments when size exceeds MaxCsvTransmitBytes");
            // Ensure each resulting FileObject stream length <= threshold
            foreach (var segment in collection)
            {
                segment.Stream.Length.Should().BeLessOrEqualTo(Constants.MaxCsvTransmitBytes);
            }
            // Filenames after first should contain a numeric suffix before .csv
            var csvNames = collection.Select(c => c.FileUri).ToList();
            csvNames.Count.Should().BeGreaterThan(1);
            csvNames.Skip(1).Any(name => System.IO.Path.GetFileNameWithoutExtension(name).Split('.').Length > 1)
                .Should().BeTrue("subsequent CSV segments should have a numeric suffix before extension");
        }

        [Fact]
        public void SerializeCsv_DoesNotSplit_When_Below_MaxCsvTransmitBytes()
        {
            var instance = NewInstance(kustoConfigured: true);
            var fileManager = new FileManager(instance);

            // Choose total bytes comfortably below threshold: 1MB worth of data
            int recordPayload = 2048; // 2KB each
            int recordCount = 400; // ~800KB + newlines
            var fileObject = BuildCsvFile(recordCount, recordPayload, instance);

            var collection = fileManager.PopulateCollection<SyntheticCsvRecord>(fileObject);
            collection.Count().Should().Be(1, "CSV should not split when total size below MaxCsvTransmitBytes");
            collection.Single().Stream.Length.Should().BeLessThan(Constants.MaxCsvTransmitBytes);
        }

        [Fact]
        public void SerializeJson_Splits_When_Exceeding_MaxJsonTransmitBytes()
        {
            var instance = NewInstance(logAnalyticsConfigured: true); // ensures JSON serialization path
            var fileManager = new FileManager(instance);

            // Build a JSON file larger than MaxJsonTransmitBytes to trigger splitting logic.
            // Rough size per record ~ payload + constant JSON overhead (~10 chars + newline). Use conservative overhead.
            int targetBytes = Constants.MaxJsonTransmitBytes + 1024; // exceed by small margin
            int recordPayload = 512; // bytes
            int perRecord = recordPayload + 12; // approximate overhead
            int recordCount = (targetBytes / perRecord) + 5;

            var original = BuildJsonFile(recordCount, recordPayload, instance);
            long originalLength = original.Length;
            originalLength.Should().BeGreaterThan(Constants.MaxJsonTransmitBytes, "precondition: original file must exceed MaxJsonTransmitBytes");

            var collection = fileManager.PopulateCollection<SyntheticJsonRecord>(original);

            collection.Count().Should().BeGreaterThan(1, "expected multiple JSON segments when size exceeds MaxJsonTransmitBytes");
            // All but maybe final segment should be below or around WarningJsonTransmitBytes
            foreach (var segment in collection)
            {
                segment.Stream.Length.Should().BeLessOrEqualTo(Constants.MaxJsonTransmitBytes);
            }
            collection.Select(c => c.FileUri).Any(u => u.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
            var jsonNames = collection.Select(c => c.FileUri).ToList();
            jsonNames.Skip(1).Any(name => System.IO.Path.GetFileNameWithoutExtension(name).Split('.').Length > 1)
                .Should().BeTrue("JSON split segments should have numeric suffixes before extension");
        }

        [Fact]
        public void SerializeJson_DoesNotSplit_When_Below_MaxJsonTransmitBytes()
        {
            var instance = NewInstance(logAnalyticsConfigured: true);
            var fileManager = new FileManager(instance);

            // Keep well below threshold: small payload & low record count ensures no split
            int recordPayload = 4;
            int recordCount = 20;
            var original = BuildJsonFile(recordCount, recordPayload, instance);

            original.Length.Should().BeLessThan(Constants.MaxJsonTransmitBytes, "original JSON file should remain below threshold for no-split test");
            var collection = fileManager.PopulateCollection<SyntheticJsonRecord>(original);
            collection.Count().Should().Be(1, "JSON should not split when below threshold");
        }

        [Fact]
        public void SerializeJson_Segments_Below_Warning_Threshold_Before_Roll()
        {
            var instance = NewInstance(logAnalyticsConfigured: true);
            var fileManager = new FileManager(instance);

            // Force multiple segments: overshoot by several MB
            int recordPayload = 1024; // 1KB
            int perRecord = recordPayload + 12;
            int recordCount = ((Constants.MaxJsonTransmitBytes * 2) / perRecord) + 500; // ensure >2 segments
            var original = BuildJsonFile(recordCount, recordPayload, instance);
            original.Length.Should().BeGreaterThan(Constants.MaxJsonTransmitBytes);

            var collection = fileManager.PopulateCollection<SyntheticJsonRecord>(original);
            var segments = collection.ToList();
            segments.Count.Should().BeGreaterThan(1);

            // Ensure every segment is below absolute max limit
            foreach (var segment in segments)
            {
                segment.Length.Should().BeLessThan(Constants.MaxJsonTransmitBytes + 1);
            }

            // At least one non-final segment should meet or exceed warning threshold implying rotation near watermark
            segments.Take(segments.Count - 1)
                    .Any(s => s.Length >= Constants.WarningJsonTransmitBytes)
                    .Should().BeTrue("a non-final JSON segment should reach warning threshold to trigger rotation logic");
        }
    }
}
