using NUnit.Framework;
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.Text;
using CollectSFDataDllTest.Utilities;
using CollectSFData.DataFile;
using System.IO;
using CollectSFData.Azure;
using CollectSFDataTest.Utilities;

namespace CollectSFDataDll.ConfigurationTests
{
    [TestFixture()]
    public class AzureClientCertificateTests : TestUtilities
    {
        [Test()]
        public void AzureClientCertificateBase64()
        {
            TestUtilities utils = DefaultUtilities();
            var config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                //config.AzureClientId = "test";
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateKeyVault()
        {
            Assert.Fail();
        }

        [Test()]
        public void AzureClientCertificateKeyVaultAppRegistration()
        {
            Assert.Fail();
        }

        [Test()]
        public void AzureClientCertificateKeyVaultSystemManagedIdentity()
        {
            Assert.Fail();
        }

        [Test()]
        public void AzureClientCertificateKeyVaultUserManagedIdentity()
        {
            Assert.Fail();
        }

        [Test()]
        public void AzureClientCertificateLocalFile()
        {
            Assert.Fail();
        }

        [Test()]
        public void AzureClientCertificateLocalStore()
        {
            Assert.Fail();
        }

        [Test()]
        public void AzureClientCertificateSubject()
        {
            Assert.Fail();
        }

        [Test()]
        public void AzureClientCertificateThumb()
        {
            Assert.Fail();
        }

        [Test()]
        public void ConfigurationOptionsTest()
        {
            //TestUtilities utils = DefaultUtilities();
            //var config = utils.ConfigurationOptions;
            //utils.ConfigurationOptions.Validate();
            //DeleteTokenCache();

            //ProcessOutput results = utils.ExecuteTest((config) =>
            //{
            //    config.AzureClientId = "test";
            //    return config.ValidateAad();
            //}, utils.Collector.Config);

            //Assert.IsTrue(results.ToString().Contains("client_id_must_be_guid"), results.ToString());
            //Assert.IsTrue(results.HasErrors(), results.ToString());
        }

        private static void DeleteTokenCache()
        {
            if (File.Exists(TokenCacheHelper.CacheFilePath))
            {
                File.Delete(TokenCacheHelper.CacheFilePath);
            }
        }

        private TestUtilities DefaultUtilities()
        {
            TestUtilities utils = new TestUtilities();
            ConfigurationOptions config = utils.Collector.Config;

            config.SasKey = TestUtilities.TestProperties.SasKey;
            config.CacheLocation = TestUtilities.TempDir;
            config.StartTimeStamp = DateTime.MinValue.ToString("o");
            config.EndTimeStamp = DateTime.Now.ToString("o");
            config.AzureClientId = TestUtilities.TestProperties.AzureClientId;
            config.AzureClientCertificate = TestUtilities.TestProperties.AzureClientCertificate;
            config.AzureClientSecret = TestUtilities.TestProperties.AzureClientSecret;
            config.AzureResourceGroup = TestUtilities.TestProperties.AzureResourceGroup;
            config.AzureResourceGroupLocation = TestUtilities.TestProperties.AzureResourceGroupLocation;
            config.AzureSubscriptionId = TestUtilities.TestProperties.AzureSubscriptionId;
            config.AzureTenantId = TestUtilities.TestProperties.AzureTenantId;
            config.List = true;

            // force auth
            //config.KustoCluster = TestUtilities.TestProperties.KustoCluster;
            //config.KustoTable = "test";

            config.GatherType = FileTypesEnum.trace.ToString();
            return utils;
        }
    }
}