using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Flurl.Http;

namespace BlackVueDownloader.PCL
{
    public static class BlackVueDownloaderExtensions
    {
        public const string FILE_SEPARATOR = "\r\n";

        // Extension method to parse file list response into string array
        public static string [] ParseBody(this string s)
        {
            return s.Replace($"v:1.00{FILE_SEPARATOR}", "").Replace(FILE_SEPARATOR, " ").Split(' ');
        }
    }

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
        /// <param name="directory"></param>
        public void Run(string ip, string directory)
        {
            var body = QueryCameraForFileList(ip);
            var list = GetListOfFilesFromResponse(body);

            ProcessList(ip, directory, list);
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
            // Strip the header. Parse each element of the body, strip the non-filename part, and return a list.
            return body.ParseBody().Select(e => e.Replace("n:/Record/", "").Replace(",s:1000000", "")).ToList();
        }

        /// <summary>
        /// For given camera ip, filename, and filetype, download the file and return a status
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="directory"></param>
        /// <param name="filename"></param>
        /// <param name="filetype"></param>
        public void DownloadFile(string ip, string directory, string filename, string filetype)
        {
            string filepath = "";

            try
            {
                filepath = Path.Combine("Record", filename);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Path Combine exception for directory {directory}, filename {filename}, Exception Message: {e.Message}");
                BlackVueDownloaderCopyStats.Errored++;
                return;
            }

            if (_fileSystemHelper.Exists(filepath))
            {
                Console.WriteLine($"File exists {filepath}, ignoring");
                BlackVueDownloaderCopyStats.Ignored++;
            }
            else
            {
                try
                {
                    var url = $"http://{ip}/Record/{filename}";
                    Console.WriteLine($"Downloading {filetype} file: {url}");
                    var path = url.DownloadFileAsync(Path.Combine(directory, "Record"));
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
        /// <param name="directory"></param>
        /// <param name="list"></param>
        public void ProcessList(string ip, string directory, IList<string> list)
        {
            var sw = new Stopwatch();
            sw.Start();

            // The list includes _NF and _NR files.
            // Loop through and download each, but also try and download .gps and .3gf files
            foreach (var s in list)
            {
                Console.WriteLine($"Processing File: {s}");

                DownloadFile(ip, directory, s, "video");

                // Line below because the list may includes _NF and _NR.  Only continue if it's an NF.
                // Otherwise it's trying to download files that are probably already downloaded
                if (!s.Contains("_NF.mp4")) continue;

                var gpsfile = s.Replace("_NF.mp4", "_N.gps");
                DownloadFile(ip, directory, gpsfile, "gps");

                var gffile = s.Replace("_NF.mp4", "_N.3gf");
                DownloadFile(ip, directory, gffile, "3gf");
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
                var url = $"http://{ip}/blackvue_vod.cgi";

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