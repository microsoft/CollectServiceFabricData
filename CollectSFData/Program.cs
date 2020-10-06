using CollectSFData.Common;


namespace CollectSFData
{
    class Program
    {
        static int Main(string[] args)
        {
            Collector collector = new Collector();
            ConfigurationOptions sfConfig = new ConfigurationOptions();           
            sfConfig.DefaultConfig();

            return collector.Collect(args, sfConfig);
        }
    }
}
