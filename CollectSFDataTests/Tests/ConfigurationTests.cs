// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.DataFile;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;

namespace CollectSFDataTests
{
    [TestFixture]
    public class ConfigurationTests : TestUtilities
    {
        [Test]
        public void DefaultConfigurationTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.any.ToString();
            //ProcessOutput results = utils.ExecuteTest();
            File.Delete(utils.TempOptionsFile);
            ProcessOutput results = utils.ExecuteCollectSfData($"-save collectsfdata.options.json");

            Assert.IsTrue(results.HasErrors(), results.ToString());
            Assert.IsTrue(File.Exists(utils.TempOptionsFile));

            results = utils.ExecuteCollectSfData($"");

            Assert.IsTrue(results.HasErrors(), results.ToString());
            // should not start execution
            Assert.NotZero(results.ExitCode);
        }

        [Test]
        public void NoConfigurationTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.any.ToString();
            //ProcessOutput results = utils.ExecuteTest();
            File.Delete(utils.TempOptionsFile);
            ProcessOutput results = utils.ExecuteCollectSfData($"");

            Assert.IsTrue(results.HasErrors(), results.ToString());
            Assert.IsTrue(File.Exists(utils.TempOptionsFile));
            // should not start execution
            Assert.NotZero(results.ExitCode);
        }

        private TestUtilities DefaultUtilities()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.SasKey = TestUtilities.TestProperties.SasKey;
            utils.ConfigurationOptions.CacheLocation = TestUtilities.TempDir;
            utils.ConfigurationOptions.StartTimeStamp = DateTime.MinValue.ToString("o");
            utils.ConfigurationOptions.EndTimeStamp = DateTime.Now.ToString("o");
            return utils;
        }
    }
}