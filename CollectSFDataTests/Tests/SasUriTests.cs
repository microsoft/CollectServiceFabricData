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
using System;

namespace CollectSFDataTests
{
    public class SasUri
    {
        public string Description;
        public string Exception;
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
                StartConsoleRedirection();
                SasEndpoints endpoints = null;

                if (!string.IsNullOrEmpty(sasUri.Exception))
                {
                    WriteConsole($"checking uri for exception {sasUri.Exception} {sasUri.Uri}", sasUri);
                    Type exceptionType = Type.GetType(sasUri.Exception, true, true);

                    Assert.Throws(exceptionType, () => endpoints = new SasEndpoints(sasUri.Uri));
                }
                else
                {
                    WriteConsole($"checking uri {sasUri.Uri}", sasUri);
                    endpoints = new SasEndpoints(sasUri.Uri);
                    WriteConsole($"checking uri result {sasUri.Uri}", endpoints);

                    if (sasUri.ShouldSucceed != endpoints.IsValid())
                    {
                        WriteConsole($"test fail bug: {sasUri.Uri} {Context?.Test.Name}", Context);
                    }

                    Assert.AreEqual(sasUri.ShouldSucceed, endpoints.IsValid());
                }

                WriteConsole($"ProcessOutput", StopConsoleRedirection());
            }
        }

        [Test]
        public void SasUriSucceedTest()
        {
            foreach (SasUri sasUri in SasUriList.Where(x => x.ShouldSucceed.Equals(true)))
            {
                WriteConsole($"checking uri {sasUri.Uri}", sasUri);
                StartConsoleRedirection();
                SasEndpoints endpoints = new SasEndpoints(sasUri.Uri);
                WriteConsole($"ProcessOutput", StopConsoleRedirection());

                WriteConsole($"checking uri result {sasUri.Uri}", endpoints);

                if (sasUri.ShouldSucceed != endpoints.IsValid())
                {
                    WriteConsole($"test fail bug: {sasUri.Uri} {Context?.Test.Name}", Context);
                }

                Assert.AreEqual(sasUri.ShouldSucceed, endpoints.IsValid());
            }
        }
    }
}