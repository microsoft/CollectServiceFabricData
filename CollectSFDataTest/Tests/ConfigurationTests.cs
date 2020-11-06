// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.DataFile;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CollectSFDataTests
{
    [TestFixture]
    public class ConfigurationTests : TestUtilities
    {
        [Test]
        public void DefaultConfigurationTest()
        {
            TestUtilities utils = DefaultUtilities();
            string defaultOptionsFile = $"{TestUtilities.WorkingDir}\\collectsfdata.options.json";
            if (!File.Exists(defaultOptionsFile) & File.Exists(DefaultOptionsFile))
            {
                defaultOptionsFile = DefaultOptionsFile;
            }

            Assert.IsTrue(File.Exists(defaultOptionsFile));

            ProcessOutput results = utils.ExecuteCollectSfData(defaultOptionsFile);

            Assert.IsTrue(results.HasErrors(), results.ToString());

            // should not start execution
            Assert.NotZero(results.ExitCode);
        }

        [Test]
        public void DefaultSaveConfigurationTest()
        {
            TestUtilities utils = DefaultUtilities();
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
            File.Delete(utils.TempOptionsFile);

            ProcessOutput results = utils.ExecuteCollectSfData($"");

            Assert.IsTrue(results.HasErrors(), results.ToString());

            // should not start execution
            Assert.NotZero(results.ExitCode);
        }

        [Test]
        public void ThreadsNullConfigurationTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.SaveTempOptions();

            string config = File.ReadAllText(utils.TempOptionsFile);
            config = Regex.Replace(config, "\"Threads\".+", "\"Threads\": null,", RegexOptions.IgnoreCase);
            File.WriteAllText(utils.TempOptionsFile, config);

            ProcessOutput results = utils.ExecuteCollectSfData($"-config {utils.TempOptionsFile}");

            Assert.IsTrue(!results.HasErrors(), results.ToString());
            Assert.IsTrue(File.Exists(utils.TempOptionsFile));

            // should start execution
            Assert.Zero(results.ExitCode);
        }

        [Test]
        public void ThreadsOneConfigurationTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.Threads = 1;

            ProcessOutput results = utils.ExecuteTest();

            Assert.IsTrue(!results.HasErrors(), results.ToString());
            Assert.IsTrue(File.Exists(utils.TempOptionsFile));

            // should start execution
            Assert.Zero(results.ExitCode);
        }

        [Test]
        public void ThreadsZeroConfigurationTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.Threads = 0;
            ProcessOutput results = utils.ExecuteTest();

            Assert.IsTrue(!results.HasErrors(), results.ToString());
            Assert.IsTrue(File.Exists(utils.TempOptionsFile));

            // should start execution
            Assert.Zero(results.ExitCode);
        }

        private TestUtilities DefaultUtilities()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.any.ToString();
            utils.ConfigurationOptions.SasKey = TestUtilities.TestProperties.SasKey;
            utils.ConfigurationOptions.CacheLocation = TestUtilities.TempDir;
            utils.ConfigurationOptions.StartTimeStamp = DateTime.MinValue.ToString("o");
            utils.ConfigurationOptions.EndTimeStamp = DateTime.Now.ToString("o");
            return utils;
        }
    }
}