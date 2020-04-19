using Microsoft.VisualStudio.TestTools.UnitTesting;
using CollectSFData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CollectSFDataTests;

namespace CollectSFData.Tests
{
    [TestClass()]
    public class GatherTypeTests
    {
        [TestMethod()]
        public void GatherTypeAnyTests()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.any.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeCounterTests()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.counter.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeExceptionTests()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.exception.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeSetupTests()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.setup.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeTableTests()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.table.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeTraceTests()
        {
            TestUtilities utils = new TestUtilities();
            utils.ConfigurationOptions.GatherType = FileTypesEnum.trace.ToString();

            ProcessOutput results = utils.ExecuteTest();
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeUnknownTests()
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