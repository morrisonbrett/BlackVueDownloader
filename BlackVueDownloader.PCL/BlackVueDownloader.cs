using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Flurl.Http;

namespace BlackVueDownloader.PCL
{
    public class BlackVueDownloader
    {
        private readonly IFileSystemHelper _fileSystemHelper;

        public BlackVueDownloader(IFileSystemHelper fileSystemHelper)
        {
            _fileSystemHelper = fileSystemHelper;
        }

        public static bool IsValidIp(string ip)
        {
            IPAddress address;
            return IPAddress.TryParse(ip, out address);
        }

        public List<string> GetListOfFilesFromResponse(string body)
        {
            var fileList = new List<string>();

            // Parse each element of the body by the separator, which fortunately is a 'space'
            var element = body.Split(' ');
            foreach (var e in element)
            {
                // Parse only what we need off of it by replacing what we don't with nothing.
                fileList.Add(e.Replace("n:/Record/", "").Replace(",s:1000000", ""));
            }

            return fileList;
        }

        public void DownloadFile(string ip, string filename, string filetype,
            ref BlackVueDownloaderCopyStats blackVueDownloaderCopyStats)
        {
            if (_fileSystemHelper.Exists($"Record/{filename}"))
            {
                blackVueDownloaderCopyStats.Ignored++;
            }
            else
            {
                try
                {
                    string url = $"http://{ip}/Record/{filename}";
                    var path = url.DownloadFileAsync("Record");
                    Console.WriteLine($"Downloading {filetype} file: {url}");
                    path.Wait();
                    blackVueDownloaderCopyStats.Copied++;
                }
                catch (FlurlHttpTimeoutException e)
                {
                    Console.WriteLine($"FlurlHttpTimeoutException: {e.Message}");
                    blackVueDownloaderCopyStats.Errored++;
                }
                catch (FlurlHttpException e)
                {
                    if (e.Call.Response != null)
                    {
                        Console.WriteLine($"Failed with response code: {e.Call.Response.StatusCode}");
                    }
                    Console.Write($"Failed before getting a response: {e.Message}");
                    blackVueDownloaderCopyStats.Errored++;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                    blackVueDownloaderCopyStats.Errored++;
                }
            }
        }

        public void ProcessList(string ip, List<string> list)
        {
            var blackVueDownloaderCopyStats = new BlackVueDownloaderCopyStats();
            ProcessList(ip, list, ref blackVueDownloaderCopyStats);
        }

        public void ProcessList(string ip, List<string> list,
            ref BlackVueDownloaderCopyStats blackVueDownloaderCopyStats)
        {
            var sw = new Stopwatch();
            sw.Start();

            // The list includes _NF and _NR files.
            // Loop through and download each, but also try and download .gps and .3gf files
            foreach (var s in list)
            {
                Console.WriteLine($"Processing File: {s}");

                DownloadFile(ip, s, "video", ref blackVueDownloaderCopyStats);

                // Line below because the list may includes _NF and _NR.  Only continue if it's an NF.
                // Otherwise it's trying to download files that are probably already downloaded
                if (!s.Contains("_NF.mp4")) continue;

                var gpsfile = s.Replace("_NF.mp4", "_N.gps");
                DownloadFile(ip, gpsfile, "gps", ref blackVueDownloaderCopyStats);

                var gffile = s.Replace("_NF.mp4", "_N.3gf");
                DownloadFile(ip, gffile, "3gf", ref blackVueDownloaderCopyStats);
            }

            sw.Stop();
            blackVueDownloaderCopyStats.TotalTime = sw.Elapsed;

            Console.WriteLine(
                $"Copied {blackVueDownloaderCopyStats.Copied}, Ignored {blackVueDownloaderCopyStats.Ignored}, Errored {blackVueDownloaderCopyStats.Errored} TotalTime {blackVueDownloaderCopyStats.TotalTime}");
        }

        public string QueryCameraForFileList(string ip)
        {
            try
            {
                string url = $"http://{ip}/blackvue_vod";

                var fileListBody = url.GetStringAsync();
                fileListBody.Wait();

                var content = fileListBody.Result;

                return content;
            }
            catch (FlurlHttpTimeoutException e)
            {
                throw new Exception(e.Message);
            }
            catch (FlurlHttpException e)
            {
                if (e.Call.Response != null)
                {
                    throw new Exception($"Failed with response code : {e.Call.Response.StatusCode}");
                }
                throw new Exception($"Failed before getting a response: {e.Message}");
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
    }
}