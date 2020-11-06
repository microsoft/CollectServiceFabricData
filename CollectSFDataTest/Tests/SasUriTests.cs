// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using CollectSFData.Azure;
using System;

namespace CollectSFDataTests
{
    [TestFixture]
    public class SasUriTests : TestUtilities
    {
        [Test(Description = "SAS ASC / Jarvis connection string test", TestOf = typeof(SasEndpoints))]
        public void SasAscTest()
        {
            string sasUri = "https://sflogsxxxxxxxx.blob.core.windows.net/fabriclogs-f33707ca-75bd-43c7-aafb-f589f66bed6c" +
                "?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2025-04-16T00:45:16.1725245Z&sig=5YDOE%2B1VSEGc5Te%2F%2B7w%2FK%2FXgZGmTL5%2FX9LNiCFKWWGU%3D," +
                "https://sflogsxxxxxxxx.table.core.windows.net/fabriclogf33707ca75bd43c7aafbf589f66bed6cApplications" +
                "?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2020-04-16T00:45:16.1725245Z&sig=5YDOE%2B1VSEGc5Te%2F%2B7w%2FK%2FXgZGmTL5%2FX9LNiCFKWWGU%3D," +
                "https://sflogsxxxxxxxx.table.core.windows.net/fabriclogf33707ca75bd43c7aafbf589f66bed6cDeploymentIds" +
                "?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2020-04-16T00:45:16.1725245Z&sig=5YDOE%2B1VSEGc5Te%2F%2B7w%2FK%2FXgZGmTL5%2FX9LNiCFKWWGU%3D," +
                "https://sflogsxxxxxxxx.table.core.windows.net/fabriclogf33707ca75bd43c7aafbf589f66bed6cGlobalTime" +
                "?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2020-04-16T00:45:16.1725245Z&sig=5YDOE%2B1VSEGc5Te%2F%2B7w%2FK%2FXgZGmTL5%2FX9LNiCFKWWGU%3D," +
                "https://sflogsxxxxxxxx.table.core.windows.net/fabriclogf33707ca75bd43c7aafbf589f66bed6cIncidents" +
                "?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2020-04-16T00:45:16.1725245Z&sig=5YDOE%2B1VSEGc5Te%2F%2B7w%2FK%2FXgZGmTL5%2FX9LNiCFKWWGU%3D," +
                "https://sflogsxxxxxxxx.table.core.windows.net/fabriclogf33707ca75bd43c7aafbf589f66bed6cNodes" +
                "?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2020-04-16T00:45:16.1725245Z&sig=5YDOE%2B1VSEGc5Te%2F%2B7w%2FK%2FXgZGmTL5%2FX9LNiCFKWWGU%3D," +
                "https://sflogsxxxxxxxx.table.core.windows.net/fabriclogf33707ca75bd43c7aafbf589f66bed6cPartitions" +
                "?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2020-04-16T00:45:16.1725245Z&sig=5YDOE%2B1VSEGc5Te%2F%2B7w%2FK%2FXgZGmTL5%2FX9LNiCFKWWGU%3D," +
                "https://sflogsxxxxxxxx.table.core.windows.net/fabriclogf33707ca75bd43c7aafbf589f66bed6cServices" +
                "?sv=2015-04-05&ss=bt&srt=sco&sp=rl&se=2020-04-16T00:45:16.1725245Z&sig=5YDOE%2B1VSEGc5Te%2F%2B7w%2FK%2FXgZGmTL5%2FX9LNiCFKWWGU%3D";

            WriteConsole($"checking uri {sasUri}");
            StartConsoleRedirection();

            SasEndpoints endpoints = new SasEndpoints(sasUri);
            WriteConsole($"ProcessOutput", StopConsoleRedirection());
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(true, endpoints.IsValid());
        }

        [Test(Description = "SAS expired sas fail test", TestOf = typeof(SasEndpoints))]
        public void SasExpiredFailTest()
        {
            string sasUri = "https://sflogsxxxxxxxx.blob.core.windows.net/" +
                "?sv=2019-10-10&ss=bfqt&srt=sco&sp=rwdlacup&se=2020-04-22T12:10:19Z&st=2020-04-22T12:05:19Z&spr=https&sig=oie5MRDFM9VLlCMye%2F%2FPFDkO4ZOdPsYT5auRlpve7sE%3D";

            WriteConsole($"checking uri {sasUri}");
            StartConsoleRedirection();

            SasEndpoints endpoints = new SasEndpoints(sasUri);
            WriteConsole($"ProcessOutput", StopConsoleRedirection());
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(false, endpoints.IsValid());
        }

        [Test(Description = "SAS invalid uri fail test", TestOf = typeof(SasEndpoints))]
        public void SasInvalidUriFailTest()
        {
            string sasUri = "sflogsxxxxxxxx.table.core.windows.net/" +
                "?sv=2019-02-02&ss=bfqt&srt=sco&sp=rwdlacup&se=2020-04-21T02:40:09Z&st=2020-04-21T02:28:09Z&spr=https&sig=0W03jW29bxVoYk0T480sbd0NsfEyOAw%2B%2F6mBHGl5fVY%3D";

            StartConsoleRedirection();
            SasEndpoints endpoints = null;

            WriteConsole($"checking uri for UriFormatException {sasUri}");
            Assert.Throws(typeof(UriFormatException), () => endpoints = new SasEndpoints(sasUri));

            WriteConsole($"ProcessOutput", StopConsoleRedirection());
        }

        [Test(Description = "SAS no access sas fail test", TestOf = typeof(SasEndpoints))]
        public void SasNoAccessFailTest()
        {
            string sasUri = "https://sflogsxxxxxxxx.file.core.windows.net/" +
                "?sv=2019-10-10&ss=bfqt&srt=s&sp=acup&se=2025-04-22T12:10:19Z&st=2020-04-22T12:05:19Z&spr=https&sig=AWXU0d737F%2BicH1Ht9vutSOdWmS3K6JEzCihLT0DjZc%3D";

            WriteConsole($"checking uri {sasUri}");
            StartConsoleRedirection();

            SasEndpoints endpoints = new SasEndpoints(sasUri);
            WriteConsole($"ProcessOutput", StopConsoleRedirection());
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(false, endpoints.IsValid());
        }

        [Test(Description = "SAS no signature sas fail test", TestOf = typeof(SasEndpoints))]
        public void SasNoSignatureFailTest()
        {
            string sasUri = "https://sflogsxxxxxxxx.table.core.windows.net/" +
                "?sv=2019-02-02&ss=bfqt&srt=sco&sp=rwdlacup&se=2025-04-21T10:28:09Z&st=2020-04-21T02:28:09Z&spr=https&sig=";

            WriteConsole($"checking uri {sasUri}");
            StartConsoleRedirection();

            SasEndpoints endpoints = new SasEndpoints(sasUri);
            WriteConsole($"ProcessOutput", StopConsoleRedirection());
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(false, endpoints.IsValid());
        }

        [Test(Description = "SAS portal connection string test", TestOf = typeof(SasEndpoints))]
        public void SasPortalConnectionStringTest()
        {
            string sasUri = "https://sflogsxxxxxxxx.blob.core.windows.net/;" +
                "QueueEndpoint=https://sflogsxxxxxxxx.queue.core.windows.net/;" +
                "FileEndpoint=https://sflogsxxxxxxxx.file.core.windows.net/;" +
                "TableEndpoint=https://sflogsxxxxxxxx.table.core.windows.net/;" +
                "SharedAccessSignature=sv=2019-02-02&ss=bfqt&srt=sco&sp=rwdlacup&se=2025-04-21T10:28:09Z&st=2020-04-21T02:28:09Z&spr=https&sig=quPGr6xzAvhli6KbLn%2BMm%2Fy2x0EsFw%2FipFvBiyiSLEU%3D";

            WriteConsole($"checking uri {sasUri}");
            StartConsoleRedirection();

            SasEndpoints endpoints = new SasEndpoints(sasUri);
            WriteConsole($"ProcessOutput", StopConsoleRedirection());
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(true, endpoints.IsValid());
        }

        [Test(Description = "SAS portal full access test (default)", TestOf = typeof(SasEndpoints))]
        public void SasPortalFullAccessTest()
        {
            string sasUri = "https://sflogsxxxxxxxx.blob.core.windows.net/" +
                "?sv=2019-02-02&ss=bfqt&srt=sco&sp=rwdlacup&se=2025-04-21T10:28:09Z&st=2020-04-21T02:28:09Z&spr=https&sig=quPGr6xzAvhli6KbLn%2BMm%2Fy2x0EsFw%2FipFvBiyiSLEU%3D";

            WriteConsole($"checking uri {sasUri}");
            StartConsoleRedirection();

            SasEndpoints endpoints = new SasEndpoints(sasUri);
            WriteConsole($"ProcessOutput", StopConsoleRedirection());
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(true, endpoints.IsValid());
        }

        [Test(Description = "SAS service sas test", TestOf = typeof(SasEndpoints))]
        public void SasServiceSasTest()
        {
            // service sas example has 'sr='
            string sasUri = "https://sflogsxxxxxxxx.blob.core.windows.net/" +
                "?sv=2015-02-21&st=2015-07-01T08%3a49%3a37.0000000Z&se=2025-07-02T08%3a49%3a37.0000000Z&sr=b&sp=rd&si=YWJjZGVmZw%3d%3d&sig=%2bSzBm0wi8xECuGkKw97wnkSZ%2f62sxU%2b6Hq6a7qojIVE%3d";

            WriteConsole($"checking uri {sasUri}");
            StartConsoleRedirection();

            SasEndpoints endpoints = new SasEndpoints(sasUri);
            WriteConsole($"ProcessOutput", StopConsoleRedirection());
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(true, endpoints.IsValid());
        }

        [Test(Description = "SAS storage account only fail test", TestOf = typeof(SasEndpoints))]
        public void SasStorageAccountOnlyFailTest()
        {
            string sasUri = "https://sflogsxxxxxxxx.table.core.windows.net/";
            StartConsoleRedirection();
            SasEndpoints endpoints = null;

            WriteConsole($"checking uri {sasUri}", sasUri);
            endpoints = new SasEndpoints(sasUri);
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(false, endpoints.IsValid());

            WriteConsole($"ProcessOutput", StopConsoleRedirection());
        }

        [Test(Description = "SAS write only sas fail test", TestOf = typeof(SasEndpoints))]
        public void SasWriteOnlyFailTest()
        {
            string sasUri = "https://sflogsxxxxxxxx.blob.core.windows.net/" +
                "?sv=2019-10-10&ss=bqt&srt=sco&sp=wacup&se=2025-04-22T12:10:19Z&st=2020-04-22T12:05:19Z&spr=https&sig=HF7%2Fb5lV0M1pLCHExVFtQy5zdcMDvSh5WomJNIhzUOA%3D";

            WriteConsole($"checking uri {sasUri}");
            StartConsoleRedirection();

            SasEndpoints endpoints = new SasEndpoints(sasUri);
            WriteConsole($"ProcessOutput", StopConsoleRedirection());
            WriteConsole($"checking uri result {sasUri}", endpoints);

            Assert.AreEqual(false, endpoints.IsValid());
        }
    }
}