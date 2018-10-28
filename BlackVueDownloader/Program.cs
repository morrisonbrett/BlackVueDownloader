using System;
using System.IO;
using System.Reflection;
using NLog;

namespace BlackVueDownloader
{
    internal class Program
    {
        private static void Main(string[] args)
        {
	        Logger logger = LogManager.GetCurrentClassLogger();
            var timeout = 0;

            var version = Assembly.GetEntryAssembly().GetName().Version.ToString();

            logger.Info($"BlackVue Downloader Version {version}");

            if (args.Length < 1)
            {
                logger.Warn("Usage: BlackVueDownloader.exe ipaddress [destinationdirectory] [timeoutinminutes (default is no timeout)]");
                return;
            }

            var ip = args[0];
            if (!PCL.BlackVueDownloader.IsValidIp(ip)) 
            {
                logger.Error($"Invalid IP Address: {ip}");
                return;
            }

            var directory = Directory.GetCurrentDirectory();
            if (args.Length == 2)
            {
                directory = args[1];
            }

            if (args.Length == 3)
            {
                timeout = int.Parse(args[2]) * 60;
            }

            try
            {
                var blackVueDownloader = new PCL.BlackVueDownloader();
                blackVueDownloader.Run(ip, directory, timeout);
            }
            catch(Exception e)
            {
                logger.Error($"General exception {e.Message}");
            }
        }
    }
}
