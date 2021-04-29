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
using System.Security.Cryptography.X509Certificates;

namespace CollectSFDataDll.ConfigurationTests
{
    [TestFixture()]
    public class AzureClientCertificateTests : TestUtilities
    {
        private X509Certificate2 _appCertificate = new X509Certificate2();
        private X509Certificate2 _clientCertificate = new X509Certificate2();

        [Test()]
        public void AzureClientCertificateBase64()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                config.AzureClientCertificate = GetCertBase64String();
                config.AzureKeyVault = "";
                config.AzureManagedIdentity = false;
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateKeyVaultAppRegistration()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                config.AzureManagedIdentity = false;
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateKeyVaultSystemManagedIdentity()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                config.AzureClientId = "";
                config.AzureManagedIdentity = true;
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                AzureResourceManager arm = new AzureResourceManager();

                Assert.IsTrue(arm.ClientIdentity.IsSystemManagedIdentity, "arm.IsSystemManagedIdentity not detected. test from azure vm with system managed identity enabled.");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateKeyVaultUserManagedIdentity()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                //config.AzureClientId = "";
                config.AzureManagedIdentity = true;
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                AzureResourceManager arm = new AzureResourceManager();

                Assert.IsTrue(arm.ClientIdentity.IsUserManagedIdentity, "arm.IsUserManagedIdentity not detected. test from azure vm with user managed identity enabled.");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateLocalFile()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                AzureResourceManager arm = new AzureResourceManager();
                string certFile = $"{TestUtilities.TempDir}\\{config.AzureClientSecret}.pfx";
                arm.ClientCertificate.SaveCertificateToFile(_appCertificate, certFile);

                //config.AzureClientId = "";
                config.AzureClientCertificate = certFile;
                config.AzureKeyVault = "";
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateLocalStoreSubject()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                //config.AzureClientId = "";
                config.AzureClientCertificate = _appCertificate.Subject;
                config.AzureKeyVault = "";
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateLocalStoreThumb()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                //config.AzureClientId = "";
                config.AzureClientCertificate = _appCertificate.Thumbprint;
                config.AzureKeyVault = "";
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateX509()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                //config.AzureClientId = "";
                config.AzureClientCertificate = "";
                config.ClientCertificate = _appCertificate;
                config.AzureKeyVault = "";
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test()]
        public void AzureClientCertificateX509withPassword()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.ConfigurationOptions;
            utils.ConfigurationOptions.Validate();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                //config.AzureClientId = "";
                config.AzureClientCertificate = "";
                config.ClientCertificate = _appCertificate;
                config.AzureKeyVault = "";
                Assert.IsTrue(config.IsClientIdConfigured(), "test configuration invalid");
                return config.ValidateAad();
            }, utils.Collector.Config);

            Assert.IsTrue(results.ExitBool);
            Assert.IsTrue(!results.HasErrors(), results.ToString());
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
            // verify test credentials work
            AzureResourceManager arm = new AzureResourceManager();
            _appCertificate = arm.ClientCertificate.ReadCertificate(TestUtilities.TestProperties.AzureClientCertificate);
            //_appCertificate = new X509Certificate2(Convert.FromBase64String(TestUtilities.TestProperties.AzureClientCertificate),
            //    TestUtilities.TestProperties.adminPassword,
            //    X509KeyStorageFlags.Exportable);
            Assert.IsNotNull(_appCertificate);

            _clientCertificate = arm.ClientCertificate.ReadCertificate(TestUtilities.TestProperties.testAzClientCertificate);
            //_clientCertificate = new X509Certificate2(Convert.FromBase64String(TestUtilities.TestProperties.testAzClientCertificate),
            //    TestUtilities.TestProperties.adminPassword,
            //    X509KeyStorageFlags.Exportable);
            Assert.IsNotNull(_clientCertificate);

            config.AzureResourceGroup = TestUtilities.TestProperties.AzureResourceGroup;
            config.AzureResourceGroupLocation = TestUtilities.TestProperties.AzureResourceGroupLocation;
            config.AzureSubscriptionId = TestUtilities.TestProperties.AzureSubscriptionId;
            config.AzureTenantId = TestUtilities.TestProperties.AzureTenantId;

            config.AzureClientSecret = TestUtilities.TestProperties.AzureClientSecret;
            config.AzureClientId = TestUtilities.TestProperties.testAzClientId;
            config.AzureClientCertificate = TestUtilities.TestProperties.testAzClientCertificate;

            arm.Authenticate(true);

            config.SasKey = TestUtilities.TestProperties.SasKey;
            config.CacheLocation = TestUtilities.TempDir;
            config.StartTimeStamp = DateTime.MinValue.ToString("o");
            config.EndTimeStamp = DateTime.Now.ToString("o");
            config.AzureClientId = TestUtilities.TestProperties.AzureClientId;
            config.AzureClientCertificate = TestUtilities.TestProperties.AzureClientCertificate;
            config.AzureClientSecret = TestUtilities.TestProperties.AzureClientSecret;
            config.AzureKeyVault = TestUtilities.TestProperties.AzureKeyVault;

            config.List = true;
            config.GatherType = FileTypesEnum.trace.ToString();
            return utils;
        }

        private string GetCertBase64String()
        {
            //byte[] bytes = _appCertificate.Export(X509ContentType.Pkcs12, TestUtilities.TestProperties.AzureClientSecret ?? string.Empty);
            byte[] bytes = _appCertificate.Export(X509ContentType.Pkcs12, string.Empty);
            string base64 = Convert.ToBase64String(bytes);
            return base64;
        }
    }
}