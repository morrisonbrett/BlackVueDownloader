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
        public BlackVueDownloaderCopyStats BlackVueDownloaderCopyStats;

        /// <summary>
        /// Instance Downloader with Moq friendly constructor
        /// </summary>
        /// <param name="fileSystemHelper"></param>
        public BlackVueDownloader(IFileSystemHelper fileSystemHelper)
        {
            _fileSystemHelper = fileSystemHelper;
            BlackVueDownloaderCopyStats = new BlackVueDownloaderCopyStats();
        }

        /// <summary>
        /// Instance Downloader with base constructor
        /// </summary>
        public BlackVueDownloader() : this (new FileSystemHelper()) {}

        /// <summary>
        /// Main control flow
        /// </summary>
        /// <param name="ip"></param>
        public void Run(string ip)
        {
            var body = QueryCameraForFileList(ip);
            var list = GetListOfFilesFromResponse(body);

            ProcessList(ip, list);
        }

        public static bool IsValidIp(string ip)
        {
            IPAddress address;
            return IPAddress.TryParse(ip, out address);
        }

        /// <summary>
        /// Connect to the camera and get a list of files
        /// </summary>
        /// <param name="body"></param>
        /// <returns>Normalized list of files</returns>
        public IList<string> GetListOfFilesFromResponse(string body)
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

        /// <summary>
        /// For given camera ip, filename, and filetype, download the file and return a status
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="filename"></param>
        /// <param name="filetype"></param>
        public void DownloadFile(string ip, string filename, string filetype)
        {
            if (_fileSystemHelper.Exists($"Record/{filename}"))
            {
                BlackVueDownloaderCopyStats.Ignored++;
            }
            else
            {
                try
                {
                    var url = $"http://{ip}/Record/{filename}";
                    var path = url.DownloadFileAsync("Record");
                    Console.WriteLine($"Downloading {filetype} file: {url}");
                    path.Wait();
                    BlackVueDownloaderCopyStats.Copied++;
                }
                catch (FlurlHttpTimeoutException e)
                {
                    Console.WriteLine($"FlurlHttpTimeoutException: {e.Message}");
                    BlackVueDownloaderCopyStats.Errored++;
                }
                catch (FlurlHttpException e)
                {
                    if (e.Call.Response != null)
                    {
                        Console.WriteLine($"Failed with response code: {e.Call.Response.StatusCode}");
                    }
                    Console.Write($"Failed before getting a response: {e.Message}");
                    BlackVueDownloaderCopyStats.Errored++;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                    BlackVueDownloaderCopyStats.Errored++;
                }
            }
        }

        /// <summary>
        /// For the list, loop through and process it
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="list"></param>
        public void ProcessList(string ip, IList<string> list)
        {
            var sw = new Stopwatch();
            sw.Start();

            // The list includes _NF and _NR files.
            // Loop through and download each, but also try and download .gps and .3gf files
            foreach (var s in list)
            {
                Console.WriteLine($"Processing File: {s}");

                DownloadFile(ip, s, "video");

                // Line below because the list may includes _NF and _NR.  Only continue if it's an NF.
                // Otherwise it's trying to download files that are probably already downloaded
                if (!s.Contains("_NF.mp4")) continue;

                var gpsfile = s.Replace("_NF.mp4", "_N.gps");
                DownloadFile(ip, gpsfile, "gps");

                var gffile = s.Replace("_NF.mp4", "_N.3gf");
                DownloadFile(ip, gffile, "3gf");
            }

            sw.Stop();
            BlackVueDownloaderCopyStats.TotalTime = sw.Elapsed;

            Console.WriteLine(
                $"Copied {BlackVueDownloaderCopyStats.Copied}, Ignored {BlackVueDownloaderCopyStats.Ignored}, Errored {BlackVueDownloaderCopyStats.Errored} TotalTime {BlackVueDownloaderCopyStats.TotalTime}");
        }

        /// <summary>
        /// Get a raw string response from the camera
        /// </summary>
        /// <param name="ip"></param>
        /// <returns>Raw string list of files</returns>
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