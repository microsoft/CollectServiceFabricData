using System;

namespace CollectSFData
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            if (!Environment.Is64BitOperatingSystem | Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("only supported on win32 x64");
            }

            Collector collector = new Collector();

            // to modify config
            //collector.Instance.Config.Validate();

            return collector.Collect(args);
        }
    }
}