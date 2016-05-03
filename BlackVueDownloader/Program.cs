using System;
using System.Reflection;

namespace BlackVueDownloader
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Console.WriteLine($"BlackVue Downloader Version {version}");

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: BlackVueDownloader.exe ipaddress");
                return;
            }

            var blackVueDownloader = new PCL.BlackVueDownloader();

            blackVueDownloader.Run(args[0]);
        }
    }
}
