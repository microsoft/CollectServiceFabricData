// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using CollectSFData;
using NUnit.Framework;

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

    [TestFixture]
    public class SasUriTests : TestUtilities
    {
        private string SasUriDataFile = $"{TestFilesDir}\\SasUriTests.json";
        private List<SasUri> SasUriList;

        public SasUriTests()
        {
            Assert.IsTrue(File.Exists(SasUriDataFile));
            SasUriList = JsonConvert.DeserializeObject<SasUris>(File.ReadAllText(SasUriDataFile)).SasUri.ToList();
        }

        [Test]
        public void SasUriFailTest()
        {
            foreach (SasUri sasUri in SasUriList.Where(x => x.ShouldSucceed.Equals(false)))
            {
                WriteConsole($"checking uri {sasUri.Uri}", sasUri);
                SasEndpoints endpoints = new SasEndpoints(sasUri.Uri);
                WriteConsole($"checking uri result {sasUri.Uri}", endpoints);

                Assert.AreEqual(sasUri.ShouldSucceed, endpoints.IsValid());
            }
        }

        [Test]
        public void SasUriSucceedTest()
        {
            foreach (SasUri sasUri in SasUriList.Where(x => x.ShouldSucceed.Equals(true)))
            {
                WriteConsole($"checking uri {sasUri.Uri}", sasUri);
                SasEndpoints endpoints = new SasEndpoints(sasUri.Uri);
                WriteConsole($"checking uri result {sasUri.Uri}", endpoints);

                Assert.AreEqual(sasUri.ShouldSucceed, endpoints.IsValid());
            }
        }
    }
}