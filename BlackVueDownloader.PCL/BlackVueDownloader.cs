using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using ByteSizeLib;
using Flurl.Http;
using NLog;

namespace BlackVueDownloader.PCL
{
    public static class BlackVueDownloaderExtensions
    {
        public const string FileSeparator = "\r\n";

        // Extension method to parse file list response into string array
        public static string[] ParseBody(this string s)
        {
            return s.Replace($"v:1.00{FileSeparator}", "").Replace($"v:2.00{FileSeparator}", "").Replace(FileSeparator, " ").Split(' ');
        }
    }

    public class BlackVueDownloader
    {
        private readonly IFileSystemHelper _fileSystemHelper;
        public BlackVueDownloaderCopyStats BlackVueDownloaderCopyStats;
        Logger logger = LogManager.GetCurrentClassLogger();

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
        public BlackVueDownloader() : this(new FileSystemHelper()) { }

        /// <summary>
        /// Main control flow
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="directory"></param>
        /// <param name="timeout"></param>
        public void Run(string ip, string directory, int timeout)
        {
            var body = QueryCameraForFileList(ip, timeout);
            var list = GetListOfFilesFromResponse(body);

            var tempdir = Path.Combine(directory, "_tmp");
            var targetdir = Path.Combine(directory, "Record");

            CreateDirectories(tempdir, targetdir);

            ProcessList(ip, list, tempdir, targetdir, timeout);
        }

        public void CreateDirectories(string tempdir, string targetdir)
        {
            if (!_fileSystemHelper.DirectoryExists(tempdir))
                _fileSystemHelper.CreateDirectory(tempdir);

            if (!_fileSystemHelper.DirectoryExists(targetdir))
                _fileSystemHelper.CreateDirectory(targetdir);
        }

        public static bool IsValidIp(string ip)
        {
            return IPAddress.TryParse(ip, out _);
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
        /// <param name="filename"></param>
        /// <param name="filetype"></param>
        /// <param name="tempdir"></param>
        /// <param name="targetdir"></param>
        /// <param name="timeout"></param>
        public void DownloadFile(string ip, string filename, string filetype, string tempdir, string targetdir, int timeout)
        {
            string filepath;
            string tempFilepath;

            try
            {
                filepath = Path.Combine(targetdir, filename);
            }
            catch (Exception e)
            {
                logger.Error($"Path Combine exception for filepath, filename {filename}, Exception Message: {e.Message}");
                BlackVueDownloaderCopyStats.Errored++;
                return;
            }

            try
            {
                tempFilepath = Path.Combine(tempdir, filename);
            }
            catch (Exception e)
            {
                logger.Error($"Path Combine exception for temp_filepath, filename {filename}, Exception Message: {e.Message}");
                BlackVueDownloaderCopyStats.Errored++;
                return;
            }

            if (_fileSystemHelper.Exists(filepath))
            {
                logger.Info($"File exists {filepath}, ignoring");
                BlackVueDownloaderCopyStats.Ignored++;
            }
            else
            {
                try
                {
                    var url = $"http://{ip}/Record/{filename}";

                    var tempfile = Path.Combine(tempdir, filename);
                    var targetfile = Path.Combine(targetdir, filename);

                    // If it already exists in the _tmp directory, delete it.
                    if (_fileSystemHelper.Exists(tempFilepath))
                    {
                        logger.Info($"File exists in tmp {tempFilepath}, deleting");
                        BlackVueDownloaderCopyStats.TmpDeleted++;
                        _fileSystemHelper.Delete(tempFilepath);
                    }

                    // Download to the temp directory, that way, if the file is partially downloaded,
                    // it won't leave a partial file in the target directory
                    logger.Info($"Downloading {filetype} file: {url}");
                    Stopwatch st = Stopwatch.StartNew();
                    if (timeout > 0)
                        url.WithTimeout(timeout).DownloadFileAsync(tempdir).Wait();
                    else
                        url.DownloadFileAsync(tempdir).Wait();
                    st.Stop();
                    BlackVueDownloaderCopyStats.DownloadingTime = BlackVueDownloaderCopyStats.DownloadingTime.Add(st.Elapsed);

                    FileInfo fi = new FileInfo(tempfile);

                    BlackVueDownloaderCopyStats.TotalDownloaded += fi.Length;

                    // File downloaded. Move from temp to target.
                    _fileSystemHelper.Move(tempfile, targetfile);

                    logger.Info($"Downloaded {filetype} file: {url}");
                    BlackVueDownloaderCopyStats.Copied++;
                }
                catch (FlurlHttpTimeoutException e)
                {
                    logger.Error($"FlurlHttpTimeoutException: {e.Message}");
                    BlackVueDownloaderCopyStats.Errored++;
                }
                catch (FlurlHttpException e)
                {
                    if (e.Call.Response != null)
                    {
                        logger.Error($"Failed with response code: {e.Call.Response.StatusCode}");
                    }
                    Console.Write($"Failed before getting a response: {e.Message}");
                    BlackVueDownloaderCopyStats.Errored++;
                }
                catch (Exception e)
                {
                    logger.Error($"Exception: {e.Message}");
                    BlackVueDownloaderCopyStats.Errored++;
                }
            }
        }

        /// <summary>
        /// For the list, loop through and process it
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="list"></param>
        /// <param name="tempdir"></param>
        /// <param name="targetdir"></param>
        /// <param name="timeout"></param>
        public void ProcessList(string ip, IList<string> list, string tempdir, string targetdir, int timeout)
        {
            var sw = new Stopwatch();
            sw.Start();

            // The list includes _NF and _NR files.
            // Loop through and download each, but also try and download .gps and .3gf files
            foreach (var s in list)
            {
                logger.Info($"Processing File: {s}");

                DownloadFile(ip, s, "video", tempdir, targetdir, timeout);

                // Line below because the list may include _NF and _NR named files.  Only continue if it's an NF.
                // Otherwise it's trying to download files that are probably already downloaded
                if (!s.Contains("_NF.mp4")) continue;

                // Make filenames for accompanying gps file
                var gpsfile = s.Replace("_NF.mp4", "_N.gps");
                DownloadFile(ip, gpsfile, "gps", tempdir, targetdir, timeout);

                // Make filenames for accompanying gff file
                var gffile = s.Replace("_NF.mp4", "_N.3gf");
                DownloadFile(ip, gffile, "3gf", tempdir, targetdir, timeout);
            }

            sw.Stop();
            BlackVueDownloaderCopyStats.TotalTime = sw.Elapsed;

            logger.Info(
                $"Copied {BlackVueDownloaderCopyStats.Copied}, Ignored {BlackVueDownloaderCopyStats.Ignored}, Errored {BlackVueDownloaderCopyStats.Errored}, TmpDeleted {BlackVueDownloaderCopyStats.TmpDeleted}, TotalTime {BlackVueDownloaderCopyStats.TotalTime}");

            logger.Info(
                $"Downloaded {ByteSize.FromBytes(BlackVueDownloaderCopyStats.TotalDownloaded).ToString()} in {BlackVueDownloaderCopyStats.DownloadingTime}");
        }

        /// <summary>
        /// Get a raw string response from the camera
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="timeout"></param>
        /// <returns>Raw string list of files</returns>
        public string QueryCameraForFileList(string ip, int timeout)
        {
            try
            {
                var url = $"http://{ip}/blackvue_vod.cgi";

                System.Threading.Tasks.Task<string> fileListBody;
                if (timeout > 0)
                    fileListBody = url.WithTimeout(timeout).GetStringAsync();
                else
                    fileListBody = url.GetStringAsync();
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