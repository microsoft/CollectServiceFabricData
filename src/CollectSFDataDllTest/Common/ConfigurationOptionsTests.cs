using CollectSFData.Common;
using NUnit.Framework;
using System;
using System.IO;

namespace CollectSFData.Common.Tests
{
    [TestFixture()]
    public class ConfigurationOptionsTests
    {
        [Test()]
        public void CheckReleaseVersionTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void CheckReleaseVersionTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void CloneTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void CloneTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ConfigurationOptionsTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ConfigurationOptionsTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ConfigurationOptionsTest2()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ConfigurationOptionsTest3()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ConvertToUtcTimeStringTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ConvertToUtcTimeStringTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ConvertToUtcTimeTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ConvertToUtcTimeTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void DisplayStatusTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void DisplayStatusTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void DownloadEtwManifestsTest()
        {
            ConfigurationOptions configurationOptions = new ConfigurationOptions();
            configurationOptions.EtwManifestsCache = $"{Path.GetTempPath()}/manifests";
            if (Directory.Exists(configurationOptions.EtwManifestsCache))
            {
                Directory.Delete(configurationOptions.EtwManifestsCache, true);
            }

            configurationOptions.DownloadEtwManifests();

            Assert.IsTrue(Directory.Exists(configurationOptions.EtwManifestsCache));
            Assert.IsTrue(Directory.GetFiles(configurationOptions.EtwManifestsCache).Length > 1);
            Directory.Delete(configurationOptions.EtwManifestsCache, true);
        }

        [Test()]
        public void GetDefaultConfigTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void GetDefaultConfigTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void HasValueTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsCacheLocationPreConfiguredTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsCacheLocationPreConfiguredTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsClientIdConfiguredTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsClientIdConfiguredTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsGuidIfPopulatedTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsGuidIfPopulatedTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsKustoConfiguredTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsKustoConfiguredTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsKustoPurgeRequestedTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsKustoPurgeRequestedTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsLogAnalyticsConfiguredTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsLogAnalyticsConfiguredTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsLogAnalyticsPurgeRequestedTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void IsLogAnalyticsPurgeRequestedTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void MergeConfigTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void MergeConfigTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void MergeConfigTest2()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void MergeConfigTest3()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void MergeConfigTest4()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void MergeConfigTest5()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void MergeConfigTest6()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void MergeConfigTest7()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void PropertyCloneTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void PropertyCloneTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void SaveConfigFileTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void SaveConfigFileTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void SetDefaultConfigTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void SetDefaultConfigTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateAadTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateAadTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateDestinationTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateDestinationTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateFileTypeTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateFileTypeTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateSasKeyTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateSasKeyTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateSourceTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateSourceTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateTest1()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateTimeTest()
        {
            throw new NotImplementedException();
        }

        [Test()]
        public void ValidateTimeTest1()
        {
            throw new NotImplementedException();
        }
    }
}