// Licensed under the MIT License.

using CollectSFData.Common;
using FluentAssertions;
using Moq;
using System;
using System.Reflection;
using Xunit;

#if !NET462 && !NET48
#else
#endif

namespace CollectSFData.Tests
{
    /// <summary>
    /// Tests for ConfigurationOptions validation methods covering edge cases,
    /// input validation, and error handling scenarios.
    /// </summary>
    [Collection("TestAuditCollection")]
    public class ConfigurationValidationTests
    {
        [Fact]
        public void ValidateDestination_WithKustoConfigured_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            
            // Setup ConvertToUtcTime mock to handle any timestamp conversion calls
            mockTimeProvider.Setup(x => x.ConvertToUtcTime(It.IsAny<string>()))
                .Returns((string input) => {
                    // If input looks like a timestamp or is not empty, return a valid DateTime
                    if (!string.IsNullOrEmpty(input) && (input.Contains("/") || input.Contains("-") || input.Contains(":")))
                    {
                        if (DateTime.TryParse(input, out DateTime parsedTime))
                            return parsedTime.ToUniversalTime();
                        return DateTime.UtcNow.AddHours(-1); // fallback for unparseable dates
                    }
                    // For empty or null input, return a default time
                    return DateTime.UtcNow.AddHours(-1);
                });
            
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                GatherType = "trace",
                KustoCluster = "https://ingest-cluster.kusto.windows.net/database",
                KustoTable = "TestTable",
                AzureClientId = "test-client-id", // Add this to avoid NotSupportedException on .NET Framework 4.6.2
                IsARMValid = true // Explicitly set this to avoid NotSupportedException check
            };

            // Act
            bool result = config.ValidateDestination();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ValidateDestination_WithLogAnalyticsConfigured_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            
            // Setup ConvertToUtcTime mock to handle any timestamp conversion calls
            mockTimeProvider.Setup(x => x.ConvertToUtcTime(It.IsAny<string>()))
                .Returns((string input) => {
                    // If input looks like a timestamp or is not empty, return a valid DateTime
                    if (!string.IsNullOrEmpty(input) && (input.Contains("/") || input.Contains("-") || input.Contains(":")))
                    {
                        if (DateTime.TryParse(input, out DateTime parsedTime))
                            return parsedTime.ToUniversalTime();
                        return DateTime.UtcNow.AddHours(-1); // fallback for unparseable dates
                    }
                    // For empty or null input, return a default time
                    return DateTime.UtcNow.AddHours(-1);
                });
            
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                LogAnalyticsId = "workspace-id",
                LogAnalyticsKey = "workspace-key",
                LogAnalyticsName = "workspace-name",
                AzureTenantId = "550e8400-e29b-41d4-a716-446655440000", // Required for tenant validation
                AzureSubscriptionId = "550e8400-e29b-41d4-a716-446655440001", // Required when Unique = true
                Unique = false // Or set to false to avoid subscription requirement
            };

            // Act
            bool result = config.ValidateDestination();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ValidateDestination_WithInvalidKustoCluster_ReturnsFalse()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            
            // Setup ConvertToUtcTime mock to handle any timestamp conversion calls
            mockTimeProvider.Setup(x => x.ConvertToUtcTime(It.IsAny<string>()))
                .Returns((string input) => {
                    // If input looks like a timestamp or is not empty, return a valid DateTime
                    if (!string.IsNullOrEmpty(input) && (input.Contains("/") || input.Contains("-") || input.Contains(":")))
                    {
                        if (DateTime.TryParse(input, out DateTime parsedTime))
                            return parsedTime.ToUniversalTime();
                        return DateTime.UtcNow.AddHours(-1); // fallback for unparseable dates
                    }
                    // For empty or null input, return a default time
                    return DateTime.UtcNow.AddHours(-1);
                });
            
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                GatherType = "trace",
                KustoCluster = "invalid-cluster-url", // Invalid URL format
                KustoTable = "TestTable",
                AzureClientId = "test-client-id", // Add this to avoid NotSupportedException on .NET Framework 4.6.2
                IsARMValid = true // Explicitly set this to avoid NotSupportedException check
            };

            // Act
            bool result = config.ValidateDestination();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateSource_WithFileUris_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var mockFileSystem = new Mock<IFileSystemAbstraction>();
            
            var config = new TestableConfigurationOptions(mockTimeProvider.Object, mockFileSystem.Object)
            {
                FileUris = new[] { "file1.txt", "file2.txt" } // Non-empty FileUris should satisfy validation
            };

            // Act
            bool result = config.ValidateSource();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ValidateSource_WithPreConfiguredCacheLocation_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var mockFileSystem = new Mock<IFileSystemAbstraction>();
            
            // Reset the static _cacheLocationPreconfigured field to force recalculation
            // This prevents test pollution in collective runs
            var field = typeof(ConfigurationOptions).GetField("_cacheLocationPreconfigured", BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, null);
            
            // Setup ConvertToUtcTime mock to handle any timestamp conversion calls
            mockTimeProvider.Setup(x => x.ConvertToUtcTime(It.IsAny<string>()))
                .Returns((string input) => {
                    // If input looks like a timestamp or is not empty, return a valid DateTime
                    if (!string.IsNullOrEmpty(input) && (input.Contains("/") || input.Contains("-") || input.Contains(":")))
                    {
                        if (DateTime.TryParse(input, out DateTime parsedTime))
                            return parsedTime.ToUniversalTime();
                        return DateTime.UtcNow.AddHours(-1); // fallback for unparseable dates
                    }
                    // For empty or null input, return a default time
                    return DateTime.UtcNow.AddHours(-1);
                });
            
            var config = new TestableConfigurationOptions(mockTimeProvider.Object, mockFileSystem.Object)
            {
                CacheLocation = "C:\\cache" // Pre-configured cache location
            };

            // Act
            bool result = config.ValidateSource();

            // Assert
            result.Should().BeTrue(); // Should pass if cache location is pre-configured
        }

        [Fact]
        public void ValidateFileType_WithValidFileType_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                GatherType = "trace"
            };

            // Act
            bool result = config.ValidateFileType();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ValidateFileType_WithInvalidFileType_ReturnsFalse()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                GatherType = "invalid-type"
            };

            // Act
            bool result = config.ValidateFileType();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateSasKey_WithEmptySasKey_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                SasKey = "" // Empty SAS key should return true (not using SAS)
            };

            // Act
            bool result = config.ValidateSasKey();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ValidateSasKey_WithNullSasKey_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                SasKey = null // Null SAS key should return true (not using SAS)
            };

            // Act
            bool result = config.ValidateSasKey();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ValidateAad_WithValidAadSettings_ShouldNotThrow()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                AzureTenantId = "550e8400-e29b-41d4-a716-446655440000",
                AzureClientId = "440e8400-e29b-41d4-a716-446655440000",
                AzureClientSecret = "valid-client-secret"
            };

            // Act & Assert
            // The ValidateAad method is complex and requires ARM authentication
            // For unit testing, we should test that it doesn't throw with valid inputs
            // The actual validation result may depend on external services
            var exception = Record.Exception(() => config.ValidateAad());
            exception.Should().BeNull();
        }

        [Fact]
        public void ValidateAad_WithMissingTenantId_ReturnsFalse()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                AzureTenantId = "", // Empty tenant ID should fail validation
                AzureClientId = "440e8400-e29b-41d4-a716-446655440000",
                AzureClientSecret = "valid-client-secret"
            };

            // Act
            bool result = config.ValidateAad();

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("550e8400-e29b-41d4-a716-446655440000", true)]
        [InlineData("550E8400-E29B-41D4-A716-446655440000", true)]
        [InlineData("{550e8400-e29b-41d4-a716-446655440000}", true)]
        [InlineData("not-a-guid", false)]
        [InlineData("", false)]
        [InlineData("123", false)]
        [InlineData("550e8400-e29b-41d4-a716", false)]
        public void IsGuid_VariousInputs_ReturnsExpectedResult(string input, bool expected)
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object);

            // Act
            bool result = config.IsGuid(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void IsGuid_WithNullInput_ReturnsFalse()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object);

            // Act
            bool result = config.IsGuid(null);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsKustoConfigured_WithNeitherClusterNorTable_ReturnsFalse()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                GatherType = "trace", // Sets FileType to trace
                KustoCluster = null,
                KustoTable = null
            };

            // Act
            bool result = config.IsKustoConfigured();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsKustoConfigured_WithBothClusterAndTable_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                GatherType = "trace",
                KustoCluster = "https://ingest-cluster.kusto.windows.net/database",
                KustoTable = "TestTable"
            };

            // Act
            bool result = config.IsKustoConfigured();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsLogAnalyticsConfigured_WithAllRequiredFields_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                LogAnalyticsId = "workspace-id",
                LogAnalyticsKey = "workspace-key",
                LogAnalyticsName = "workspace-name"
            };

            // Act
            bool result = config.IsLogAnalyticsConfigured();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsLogAnalyticsConfigured_WithMissingName_ReturnsFalse()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                LogAnalyticsId = "workspace-id",
                LogAnalyticsKey = "workspace-key",
                LogAnalyticsName = null
            };

            // Act
            bool result = config.IsLogAnalyticsConfigured();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsUploadConfigured_WithKustoConfigured_ReturnsTrue()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object)
            {
                GatherType = "trace",
                KustoCluster = "https://ingest-cluster.kusto.windows.net/database",
                KustoTable = "TestTable"
            };

            // Act
            bool result = config.IsUploadConfigured();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsUploadConfigured_WithNeitherConfigured_ReturnsFalse()
        {
            // Arrange
            var mockTimeProvider = new Mock<ITimeProvider>();
            var config = new TestableConfigurationOptions(mockTimeProvider.Object);

            // Act
            bool result = config.IsUploadConfigured();

            // Assert
            result.Should().BeFalse();
        }
    }
}