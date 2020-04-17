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
    public class ProgramTests
    {
        static ProgramTests()
        {
            // todo
            //TestUtilities.BuildWindowsCluster();
        }

        [TestMethod()]
        public void DetermineClusterIdTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void DownloadAzureDataTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void ExecuteTest()
        {
            string[] args = new string[1] { $"{TestUtilities.TestConfigurationsDir}\\collectsfdata.options.json" };
            Program program = new Program();
            int result = program.Execute(args);
            Assert.AreEqual(0, result);
        }

        [TestMethod()]
        public void FinalizeKustoTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void InitializeKustoTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void InitializeLogAnalyticsTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void MainTest()
        {
            Program program = new Program();
            Assert.IsNotNull(program);
        }

        [TestMethod()]
        public void QueueForIngestTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void UploadCacheDataTest()
        {
            Assert.Fail();
        }
    }
}