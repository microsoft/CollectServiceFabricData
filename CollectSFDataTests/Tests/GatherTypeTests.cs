// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.DataFile;
using NUnit.Framework;
using System;
using System.Diagnostics;

namespace CollectSFDataTests
{
    [TestFixture]
    public class GatherTypeTests : TestUtilities
    {
        [Test]
        public void GatherTypeAnyTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.any.ToString();
            ProcessOutput results = utils.ExecuteTest();

            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test]
        public void GatherTypeBadTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = "wtw";
            ProcessOutput results = utils.ExecuteTest();

            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"), results.ToString());
            // should not start execution
            Assert.NotZero(results.ExitCode);
        }

        [Test]
        public void GatherTypeCounterTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.counter.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors(), results.ToString());
            Assert.IsTrue(results.StandardOutput.Contains("total execution time in minutes"));
        }

        [Test]
        public void GatherTypeExceptionTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.exception.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test]
        public void GatherTypeNullTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = null;

            ProcessOutput results = utils.ExecuteTest();

            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"), results.ToString());
            // should not start execution
            Assert.NotZero(results.ExitCode);
        }

        [Test]
        public void GatherTypeSetupTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.setup.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test]
        public void GatherTypeTableTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.table.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test]
        public void GatherTypeTraceTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.trace.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors(), results.ToString());
        }

        [Test]
        public void GatherTypeUnknownTest()
        {
            TestUtilities utils = DefaultUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.unknown.ToString();

            ProcessOutput results = utils.ExecuteTest();

            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"), results.ToString());
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