using System;
using System.Reflection;
using BlackVueDownloader.PCL;

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

            var blackVueDownloader = new PCL.BlackVueDownloader(new FileSystemHelper());

            var ip = args[0];
            string body;
            try
            {
                body = blackVueDownloader.QueryCameraForFileList(ip);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                return;
            }

            var list = blackVueDownloader.GetListOfFilesFromResponse(body);

            blackVueDownloader.ProcessList(ip, list);
        }
    }
}