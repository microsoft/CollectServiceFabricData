using NUnit.Framework;
using CollectSFData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CollectSFData.Common;
using CollectSFData.Azure;
using CollectSFData.DataFile;

namespace CollectSFData.Tests
{
    [TestFixture()]
    public class CollectorTests
    {
        private List<string> messages = new List<string>();

        [Test()]
        public void CloseTest()
        {
            Collector collector = new Collector();
            int result = collector.Collect();
            collector.Close();
            Assert.IsFalse(CustomTaskManager.IsRunning);
        }

        [Test()]
        public void CollectorTest()
        {
            Collector collector = new Collector();
            Assert.NotNull(collector.Instance);
            Assert.NotNull(collector.Config);
        }

        [Test()]
        public void CollectTest()
        {
            Collector collector = new Collector();
            int result = collector.Collect();
            Assert.NotZero(result);
        }

        [Test()]
        public void CollectTest1()
        {
            Collector collector = new Collector();
            ConfigurationOptions configurationOptions = new ConfigurationOptions();
            configurationOptions.VersionOption = true;
            configurationOptions.GatherType = FileTypesEnum.trace.ToString();
            Log.MessageLogged += Log_MessageLogged;
            configurationOptions.Validate();

            int result = collector.Collect(configurationOptions);
            string results = string.Join<string>(Environment.NewLine, messages.ToArray());
            collector.Close();
            Assert.IsTrue(results.Contains("CheckReleaseVersion:"));
            messages.Clear();
        }

        [Test()]
        public void DetermineClusterIdTest()
        {
            string clusterId = "12345678-1234-1234-1234-123456789012";
            string sasKey = $"https://fake-sas-key.blob.core.windows.net/fabriclogs-{clusterId}?sv=2019-07-07&sig=UD9WzTUk6o...ogM%2FEY5JkmWu4%3D&spr=https&st=2021-05-19T01%3A48%3A00Z&se=2021-05-19T09%3A49%3A00Z&srt=sco&ss=bfqt&sp=racupwdl";
            SasEndpoints sasEndpoints = new SasEndpoints(sasKey);
            Collector collector = new Collector();
            collector.Initialize(new ConfigurationOptions() { SasEndpointInfo = sasEndpoints });
            string result = collector.DetermineClusterId();
            Assert.AreEqual(clusterId, result);
        }

        [Test()]
        public void InitializeTest()
        {
            Collector collector = new Collector();
            collector.Initialize(new ConfigurationOptions());
            Assert.IsTrue(CustomTaskManager.IsRunning);
            CloseTest();
        }

        private void Log_MessageLogged(object sender, LogMessage args)
        {
            messages.Add(args.Message);
        }
    }
}