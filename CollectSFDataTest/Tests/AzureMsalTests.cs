// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using CollectSFData.DataFile;
using System;
using System.IO;

namespace CollectSFDataTests
{
    [TestFixture]
    public class AzureMsalTests : TestUtilities
    {
        [Test(Description = "Azure Msal Client test bad client id", TestOf = typeof(AzureResourceManager))]
        public void AzureMsalClientBadIdTest()
        {
            TestUtilities utils = DefaultUtilities();
            DeleteTokenCache();

            utils.ConfigurationOptions.AzureClientId = "test";

            ProcessOutput results = utils.ExecuteTest();

            Assert.IsTrue(results.HasErrors(), results.ToString());
        }

        [Test(Description = "Azure Msal Client test", TestOf = typeof(AzureResourceManager))]
        public void AzureMsalClientTest()
        {
            TestUtilities utils = DefaultUtilities();

            ProcessOutput results = utils.ExecuteTest();

            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test(Description = "Azure Msal auth as user test", TestOf = typeof(AzureResourceManager))]
        public void AzureMsalUserAuthTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.AzureClientId = null;
            utils.ConfigurationOptions.AzureClientSecret = null;
            utils.ConfigurationOptions.AzureResourceGroup = null;
            utils.ConfigurationOptions.AzureResourceGroupLocation = null;
            utils.ConfigurationOptions.AzureSubscriptionId = null;
            utils.ConfigurationOptions.AzureTenantId = null;

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
            utils.ConfigurationOptions.SasKey = TestUtilities.TestProperties.SasKey;
            utils.ConfigurationOptions.CacheLocation = TestUtilities.TempDir;
            utils.ConfigurationOptions.StartTimeStamp = DateTime.MinValue.ToString("o");
            utils.ConfigurationOptions.EndTimeStamp = DateTime.Now.ToString("o");
            utils.ConfigurationOptions.AzureClientId = TestUtilities.TestProperties.AzureClientId;
            utils.ConfigurationOptions.AzureClientSecret = TestUtilities.TestProperties.AzureClientSecret;
            utils.ConfigurationOptions.AzureResourceGroup = TestUtilities.TestProperties.AzureResourceGroup;
            utils.ConfigurationOptions.AzureResourceGroupLocation = TestUtilities.TestProperties.AzureResourceGroupLocation;
            utils.ConfigurationOptions.AzureSubscriptionId = TestUtilities.TestProperties.AzureSubscriptionId;
            utils.ConfigurationOptions.AzureTenantId = TestUtilities.TestProperties.AzureTenantId;
            utils.ConfigurationOptions.List = true;

            // force auth
            //utils.ConfigurationOptions.KustoCluster = TestUtilities.TestProperties.KustoCluster;
            //utils.ConfigurationOptions.KustoTable = "test";

            utils.ConfigurationOptions.GatherType = FileTypesEnum.trace.ToString();
            return utils;
        }
    }
}