using CollectSFData.Common;

namespace CollectSFData
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            Collector collector = new Collector();
            ConfigurationOptions options = new ConfigurationOptions();
            options.DefaultConfig();
            return collector.Collect(args, options);
        }
    }
}