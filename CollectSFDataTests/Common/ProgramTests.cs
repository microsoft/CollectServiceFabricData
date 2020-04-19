using Microsoft.VisualStudio.TestTools.UnitTesting;
using CollectSFData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CollectSFDataTests;
using System.Text.RegularExpressions;

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
            //string[] args = new string[1] { TestUtilities.TestConfig.SaveConfiguration };
            Program program = new Program();

            //TestUtilities.StartConsoleRedirection();
            int result = program.Execute(TestUtilities.TestArgs);
            //ProcessOutput output = TestUtilities.StopConsoleRedirection();

            //Assert.IsTrue(Regex.IsMatch(output, "version", RegexOptions.IgnoreCase));
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