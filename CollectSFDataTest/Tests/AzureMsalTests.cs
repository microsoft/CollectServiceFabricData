// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using CollectSFData.DataFile;
using NUnit.Framework;
using System;

namespace CollectSFDataTests
{
    [TestFixture]
    public class AzureMsalTests : TestUtilities
    {
        [Test(Description = "Azure Msal Client test", TestOf = typeof(SasEndpoints))]
        public void AzureMsalClientTest()
        {
            TestUtilities utils = DefaultUtilities();

            ProcessOutput results = utils.ExecuteTest();

            Assert.IsFalse(results.HasErrors(), results.ToString());
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
            utils.ConfigurationOptions.GatherType = FileTypesEnum.any.ToString();
            return utils;
        }
    }
}