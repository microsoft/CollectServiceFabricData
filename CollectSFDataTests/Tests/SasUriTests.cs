using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System;
using CollectSFData;
using System.Net;

namespace CollectSFDataTests
{
    public class SasUri
    {
        public string Description;
        public bool ShouldSucceed;
        public string Uri;
    }

    public class SasUris
    {
        public SasUri[] SasUri;
    }

    [TestClass()]
    public class SasUriTests
    {
        private string SasUriDataFile = $"{TestUtilities.TestFilesDir}\\SasUriTests.json";
        private List<SasUri> SasUriList;

        public SasUriTests()
        {
            Assert.IsTrue(File.Exists(SasUriDataFile));
            SasUriList = JsonConvert.DeserializeObject<SasUris>(File.ReadAllText(SasUriDataFile)).SasUri.ToList();
        }

        [TestMethod()]
        public void SasUriFailTest()
        {
            foreach (SasUri sasUri in SasUriList.Where(x => x.ShouldSucceed.Equals(false)))
            {
                TestUtilities.WriteConsole($"checking uri {sasUri.Uri}", sasUri);
                SasEndpoints endpoints = new SasEndpoints(sasUri.Uri);
                TestUtilities.WriteConsole($"checking uri result {sasUri.Uri}", endpoints);

                Assert.AreEqual(sasUri.ShouldSucceed, endpoints.IsPopulated());
            }
        }

        [TestMethod()]
        public void SasUriSucceedTest()
        {
            foreach (SasUri sasUri in SasUriList.Where(x => x.ShouldSucceed.Equals(true)))
            {
                TestUtilities.WriteConsole($"checking uri {sasUri.Uri}", sasUri);
                SasEndpoints endpoints = new SasEndpoints(sasUri.Uri);
                TestUtilities.WriteConsole($"checking uri result {sasUri.Uri}", endpoints);

                Assert.AreEqual(sasUri.ShouldSucceed, endpoints.IsPopulated());
            }
        }
    }
}