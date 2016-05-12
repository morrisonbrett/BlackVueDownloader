using System;
using System.IO;
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
                Console.WriteLine("Usage: BlackVueDownloader.exe ipaddress [destinationdirectory]");
                return;
            }

            var ip = args[0];
            if (!PCL.BlackVueDownloader.IsValidIp(ip)) 
            {
                Console.WriteLine($"Invalid IP Address: {ip}");
                return;
            }

            var directory = Directory.GetCurrentDirectory();
            if (args.Length == 2)
            {
                directory = args[1];
            }

            var blackVueDownloader = new PCL.BlackVueDownloader();

            blackVueDownloader.Run(ip, directory);
        }
    }
}
