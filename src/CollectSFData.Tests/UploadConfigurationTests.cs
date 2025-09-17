using CollectSFData.Common;
using FluentAssertions;
using Xunit;

namespace CollectSFData.Tests
{
    [Collection("TestAuditCollection")]
    public class UploadConfigurationTests
    {
        [Fact]
        public void IsUploadConfigured_LogAnalyticsOnly_ReturnsTrue()
        {
            var config = new TestableConfigurationOptions
            {
                LogAnalyticsId = "workspace-id",
                LogAnalyticsKey = "workspace-key",
                LogAnalyticsName = "workspace-name"
            };

            config.IsLogAnalyticsConfigured().Should().BeTrue();
            config.IsUploadConfigured().Should().BeTrue();
        }
    }
}
