using NUnit.Framework;
using CollectSFData.Kusto;
using CollectSFData.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace CollectSFData.Kusto.Tests
{
    [TestFixture()]
    public class KustoConnectionTests
    {
        [Test()]
        public void GetNextIngestionQueueTest()
        {
            Collector collector = new Collector();
            ConfigurationOptions configurationOptions = new ConfigurationOptions();

            configurationOptions.KustoCluster = "https://ingest-sfcluster.kusto.windows.net/sfdatabase";
            configurationOptions.KustoTable = "sfcluster_test";
            KustoEndpoint kustoEndpoint = new KustoEndpoint(configurationOptions);

            kustoEndpoint.IngestionResources = new IngestionResourcesSnapshot()
            {
                FailureNotificationsQueue = "https://fake-failure.queue/fake-fail",
                IngestionQueues = new List<string>()
                {
                    "https://fake-ingestion.queue/fake-ingest1",
                    "https://fake-ingestion.queue/fake-ingest2",
                },
                SuccessNotificationsQueue = "https://fake-success.queue/fake-success",
                TempStorageContainers = new List<string>()
                {
                    "https://fake-temp.queue/fake-temp1",
                    "https://fake-temp.queue/fake-temp2",
                }
            };

            KustoConnection kustoConnection = new KustoConnection(collector.Instance);
            kustoConnection.Endpoint = kustoEndpoint;
            kustoConnection.PopulateQueueEnumerators();

            WaitCallback waitCallback = new WaitCallback(ThreadProc);
            bool result = ThreadPool.SetMinThreads(100, 100);

            for (int i = 0; i <= 1000; i++)
            {
                ThreadPool.QueueUserWorkItem(waitCallback, kustoConnection);
            }
        }

        // This thread procedure performs the task.
        private static void ThreadProc(Object stateInfo)
        {
            KustoConnection kustoConnection = stateInfo as KustoConnection;
            int i = 0;

            for (i = 0; i <= 1000; i++)
            {
                Tuple<string, string> results = kustoConnection.GetNextIngestionQueue();

                Assert.IsNotNull(results.Item1);
                Assert.IsNotNull(results.Item2);
            }

            // No state object was passed to QueueUserWorkItem, so stateInfo is null.
            Console.WriteLine($"Hello from the thread pool. count{i}");
        }
    }
}