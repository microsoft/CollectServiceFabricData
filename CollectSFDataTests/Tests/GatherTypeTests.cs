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
        public void GatherTypeAllTests()
        {
            foreach (string type in Enum.GetValues(typeof(FileTypesEnum)))
            {
                TestUtilities utils = new TestUtilities();
                ProcessOutput results = utils.ExecuteCollectSfData($"-type {type}");
                Assert.IsFalse(results.HasErrors());
            }
        }

        [TestMethod()]
        public void GatherTypeAnyTests()
        {
            TestUtilities utils = new TestUtilities();
            utils.TempOptions.GatherType = FileTypesEnum.any.ToString();

            ProcessOutput results = utils.ExecuteTest(utils.TempOptions);
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeCounterTests()
        {
            TestUtilities utils = new TestUtilities();
            ProcessOutput results = utils.ExecuteCollectSfData("-type counter");
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeExceptionTests()
        {
            TestUtilities utils = new TestUtilities();
            ProcessOutput results = utils.ExecuteCollectSfData("-type exception");
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeSetupTests()
        {
            TestUtilities utils = new TestUtilities();
            ProcessOutput results = utils.ExecuteCollectSfData("-type setup");
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeTableTests()
        {
            TestUtilities utils = new TestUtilities();
            ProcessOutput results = utils.ExecuteCollectSfData("-type table");
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeTraceTests()
        {
            TestUtilities utils = new TestUtilities();
            utils.TempOptions.GatherType = FileTypesEnum.trace.ToString();
            //testOptions.UseMemoryStream = false;

            ProcessOutput results = utils.ExecuteTest(utils.TempOptions);
            Assert.IsFalse(results.HasErrors());
        }

        [TestMethod()]
        public void GatherTypeUnknownTests()
        {
            TestUtilities utils = new TestUtilities();
            ProcessOutput results = utils.ExecuteCollectSfData("-type unknown");
            Assert.IsTrue(results.HasErrors());
        }
    }
}