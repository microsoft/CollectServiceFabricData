// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData;
using CollectSFData.Azure;
using CollectSFData.Common;
using CollectSFData.DataFile;
using CollectSFDataTest.Utilities;
using Markdig.Extensions.Yaml;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CollectSFDataTest
{
    [TestFixture]
    public class AzureMsalTests : TestUtilities
    {
        [Test(Description = "Azure Msal Client test bad client id", TestOf = typeof(AzureResourceManager))]
        public void AzureMsalClientBadIdTest()
        {
            TestUtilities utils = DefaultUtilities();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
             {
                 config.AzureClientId = "test";
                 return config.ValidateAad();
             }, utils.Collector.Instance.Config);

            Assert.IsTrue(results.ToString().Contains("client_id_must_be_guid"), results.ToString());
            Assert.IsTrue(!results.HasErrors(), results.ToString());
        }

        [Test(Description = "Azure Msal Client test", TestOf = typeof(AzureResourceManager))]
        public void AzureMsalClientTest()
        {
            TestUtilities utils = DefaultUtilities();
            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                return config.ValidateAad();
            }, utils.Collector.Instance.Config);

            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test(Description = "Azure Msal auth as user test", TestOf = typeof(AzureResourceManager))]
        public void AzureMsalUserAuthTest()
        {
            TestUtilities utils = DefaultUtilities();
            ConfigurationOptions config = utils.Collector.Instance.Config;

            config.AzureClientId = null;
            config.AzureClientSecret = null;
            config.AzureResourceGroup = null;
            config.AzureResourceGroupLocation = null;
            config.AzureSubscriptionId = null;
            config.AzureTenantId = null;

            ProcessOutput results = utils.ExecuteTest();

            /* known cng error in .net core that test is running as and azure-az modules
             * fix is to use cert thumb as secret but cert may have to be real / from ca
             */
            Assert.IsFalse(results.HasErrors(), results.ToString());
            /*
             *  "StandardError": "13:Validate:exception: validate:exception:System.AggregateException: One or more errors occurred. (Could not load type 'System.Security.Cryptography.SHA256Cng' from assembly 'System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'.)\r\n ---> System.TypeLoadException: Could not load type 'System.Security.Cryptography.SHA256Cng' from assembly 'System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'.\r\n   at Microsoft.Identity.Client.Platforms.net45.NetDesktopCryptographyManager.CreateSha256HashBytes(
            */
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
            //utils.LogMessageQueueEnabled = true;
            ConfigurationOptions config = utils.Collector.Instance.Config;

            config.SasKey = TestUtilities.TestProperties.SasKey;
            config.CacheLocation = TestUtilities.TempDir;
            config.StartTimeStamp = DateTime.MinValue.ToString("o");
            config.EndTimeStamp = DateTime.Now.ToString("o");
            config.AzureClientId = TestUtilities.TestProperties.AzureClientId;
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