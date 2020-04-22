// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using CollectSFDataTests;

namespace CollectSFData.Tests
{
    [TestClass()]
    public class GatherTypeTests
    {
        [TestMethod()]
        public void GatherTypeAnyTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.any.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeBadTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = "wtw";

            ProcessOutput results = utils.ExecuteTest();

            // all test outputs will have invalid type initially
            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"));
            // should not start execution
            Assert.IsFalse(results.StandardOutput.Contains("total execution time in minutes"));
        }

        [TestMethod()]
        public void GatherTypeCounterTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.counter.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeExceptionTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.exception.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeNullTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = null;

            ProcessOutput results = utils.ExecuteTest();

            // all test outputs will have invalid type initially
            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"));
            // should not start execution
            Assert.IsFalse(results.StandardOutput.Contains("total execution time in minutes"));
        }

        [TestMethod()]
        public void GatherTypeSetupTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.setup.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeTableTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.table.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeTraceTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.trace.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeUnknownTest()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.unknown.ToString();

            ProcessOutput results = utils.ExecuteTest();

            // all test outputs will have invalid type initially
            Assert.IsTrue(results.StandardOutput.Contains("ValidateFileType:warning: invalid -type"));
            // should not start execution
            Assert.IsFalse(results.StandardOutput.Contains("total execution time in minutes"));
        }
    }
}