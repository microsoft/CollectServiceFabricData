﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.DataFile;
using CollectSFData.Kusto;
using CollectSFDataTest.Utilities;
using NUnit.Framework;
using System;
using CollectSFDataDllTest.Utilities;

namespace CollectSFData.ConfigurationTests
{
    [TestFixture]
    public class KustoTests : TestUtilities
    {
        [Test(Description = "Kusto cluster valid url no location test", TestOf = typeof(KustoEndpoint))]
        public void KustoClusterValidUrlNoLocationTest()
        {
            TestUtilities utils = DefaultUtilities();

            utils.ConfigurationOptions.KustoCluster = "https://ingest-testcluster.kusto.windows.net/testdatabase";
            utils.ConfigurationOptions.KustoTable = "test";

            ProcessOutput results = utils.ExecuteTest();

            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test(Description = "Kusto cluster valid url test", TestOf = typeof(KustoEndpoint))]
        public void KustoClusterValidUrlTest()
        {
            TestUtilities utils = DefaultUtilities();

            utils.ConfigurationOptions.KustoCluster = "https://ingest-testcluster.eastus.kusto.windows.net/testdatabase";
            utils.ConfigurationOptions.KustoTable = "test";

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
            utils.ConfigurationOptions.AzureClientCertificate = TestUtilities.TestProperties.AzureClientCertificate;
            utils.ConfigurationOptions.AzureResourceGroup = TestUtilities.TestProperties.AzureResourceGroup;
            utils.ConfigurationOptions.AzureResourceGroupLocation = TestUtilities.TestProperties.AzureResourceGroupLocation;
            utils.ConfigurationOptions.AzureSubscriptionId = TestUtilities.TestProperties.AzureSubscriptionId;
            utils.ConfigurationOptions.AzureTenantId = TestUtilities.TestProperties.AzureTenantId;

            utils.ConfigurationOptions.GatherType = FileTypesEnum.trace.ToString();
            return utils;
        }
    }
}