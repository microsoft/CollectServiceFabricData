// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using CollectSFData.Common;
using CollectSFData.DataFile;
using CollectSFDataTest.Utilities;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;

//using System.Windows;
//using System.Windows.Forms;

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
            Assert.IsTrue(results.HasErrors(), results.ToString());
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

        [Test(Description = "Azure Msal auth as device test", TestOf = typeof(AzureResourceManager))]
        public void AzureMsalDeviceAuthTest()
        {
            ProcessOutput results = DefaultUtilities().ExecuteTest(() =>
            {
                AzureResourceManager arm = new AzureResourceManager();
                AzureResourceManager.MsalMessage += AzureResourceManager_MsalMessage;
                AzureResourceManager.MsalDeviceCode += AzureResourceManager_MsalDeviceCode;

                bool result = arm.CreatePublicClient(true, true);
                AzureResourceManager.MsalMessage -= AzureResourceManager_MsalMessage;
                AzureResourceManager.MsalDeviceCode -= AzureResourceManager_MsalDeviceCode;

                return result;
            });

            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test(Description = "Azure Msal auth as user test", TestOf = typeof(AzureResourceManager))]
        public void AzureMsalUserAuthTest()
        {
            TestUtilities utils = DefaultUtilities();
            DeleteTokenCache();

            ProcessOutput results = utils.ExecuteTest((config) =>
            {
                config.AzureClientId = null;
                config.AzureClientSecret = null;
                config.AzureResourceGroup = null;
                config.AzureResourceGroupLocation = null;
                config.AzureSubscriptionId = null;
                config.AzureTenantId = null;

                return config.ValidateAad();
            }, utils.Collector.Instance.Config);

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

        private void AzureResourceManager_MsalDeviceCode(Microsoft.Identity.Client.DeviceCodeResult arg)
        {
            string message = $"\r\n*************\r\ndevice code received:\r\n{arg.UserCode}\r\n*************\r\n";
            WriteConsole(message);

            // display devicelogin page with usercode appended for copy into prompt
            Process.Start(new ProcessStartInfo("cmd", $"/c start https://microsoft.com/devicelogin?{arg.UserCode}") { CreateNoWindow = true });
        }

        private void AzureResourceManager_MsalMessage(Microsoft.Identity.Client.LogLevel level, string message, bool containsPII)
        {
            WriteConsole(message);
        }

        private TestUtilities DefaultUtilities()
        {
            TestUtilities utils = new TestUtilities();
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