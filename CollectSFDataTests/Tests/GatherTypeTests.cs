// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Moq;
using NUnit.Framework;
using CollectSFData.DataFile;

namespace CollectSFDataTests
{
    [TestFixture]
    public class GatherTypeTests
    {
        [Test]
        public void GatherTypeAnyTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.any.ToString();
            utils.ConfigurationOptions.SasKey = TestUtilities.TestProperties.SasKey;
            utils.ConfigurationOptions.CacheLocation = TestUtilities.TempDir;
            //test
            //ProcessOutput result = utils.ExecuteMoqTest();
            //end test


            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [Test]
        public void GatherTypeBadTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = "wtw";

            ProcessOutput results = utils.ExecuteTest();


            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"));
            // should not start execution
            Assert.NotZero(results.ExitCode);
        }

        [Test]
        public void GatherTypeCounterTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.counter.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
            Assert.IsTrue(results.StandardOutput.Contains("total execution time in minutes"));
        }

        [Test]
        public void GatherTypeExceptionTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.exception.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [Test]
        public void GatherTypeNullTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = null;

            ProcessOutput results = utils.ExecuteTest();


            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"));
            // should not start execution
            Assert.NotZero(results.ExitCode);
        }

        [Test]
        public void GatherTypeSetupTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.setup.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [Test]
        public void GatherTypeTableTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.table.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [Test]
        public void GatherTypeTraceTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.trace.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [Test]
        public void GatherTypeUnknownTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.unknown.ToString();

            ProcessOutput results = utils.ExecuteTest();

            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"));
            // should not start execution
            Assert.NotZero(results.ExitCode);
        }
    }
}