using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CollectSFData;
using CollectSFData.Common;

namespace CollectSFData
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            Collector collector = new Collector();
            ConfigurationOptions options = new ConfigurationOptions();
            //options.DefaultConfig();
            return collector.Collect(args);
        }
    }
}