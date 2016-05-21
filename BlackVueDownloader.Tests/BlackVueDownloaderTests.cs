using System;
using System.IO;
using BlackVueDownloader.PCL;
using Flurl.Http.Testing;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace BlackVueDownloader.Tests
{
    public class BlackVueDownloaderTests
    {
        // ReSharper disable once NotAccessedField.Local
        private readonly ITestOutputHelper _output;

        public BlackVueDownloaderTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string GenerateRecords(int numRecords)
        {
            string ret = null;
            var date = DateTime.Now.ToString("yyyyMMdd");

            for (var i = 0; i < numRecords; i++)
            {
                ret += $"n:/Record/{date}_{i}_NF.mp4,s:1000000\r\n";
                ret += $"n:/Record/{date}_{i}_NR.mp4,s:1000000";
                if (i + 1 < numRecords)
                    ret += "\r\n";
            }

            return ret;
        }

        [Theory]
        [InlineData("192.168.1.1")]
        [InlineData("192.168.1")]
        public void IsValidIpTheory(string ip)
        {
            Assert.True(PCL.BlackVueDownloader.IsValidIp(ip));
        }

        [Theory]
        [InlineData("19-2")]
        [InlineData("not-a good ip address!")]
        public void IsInValidIpTheory(string ip)
        {
            Assert.False(PCL.BlackVueDownloader.IsValidIp(ip));
        }

        [Theory]
        [InlineData("n:/Record/20160404_12345_NR.mp4,s:1000000\r\nn:/Record/20160404_12345_NF.mp4,s:1000000",
            "20160404_12345_NR.mp4", 2)]
        [InlineData(
            "n:/Record/20160404_12345_NR.mp4,s:1000000\r\nn:/Record/20160404_12345_NF.mp4,s:1000000\r\nn:/Record/20160404_12346_NR.mp4,s:1000000\r\nn:/Record/20160404_12346_NF.mp4,s:1000000",
            "20160404_12345_NR.mp4", 4)]
        public void GetListOfFilesFromResponseTest(string body, string firstval, int numelements)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            var list = blackVueDownloader.GetListOfFilesFromResponse(body);
            Assert.Equal(numelements, list.Count);
            Assert.Equal(firstval, list[0]);
        }

        [Theory]
        [InlineData("192.168.1.1")]
        public void QueryCameraForFileListTest(string ip)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWith(200, "this is the body");

                var body = blackVueDownloader.QueryCameraForFileList(ip);

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");

                Assert.True(!string.IsNullOrEmpty(body));
            }
        }

        [Theory]
        [InlineData("192.168.1.1")]
        public void EmptyResponseTest(string ip)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWith(200, "");

                var body = blackVueDownloader.QueryCameraForFileList(ip);

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");

                Assert.True(string.IsNullOrEmpty(body));
            }
        }

        [Theory]
        [InlineData("192.168.1.1")]
        public void InvalidResponseTest(string ip)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWith(500, "Simulated Error");

                try
                {
                    blackVueDownloader.QueryCameraForFileList(ip);
                }
                catch (Exception e)
                {
                    Assert.StartsWith("One or more errors occurred.", e.Message);
                }

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");
            }
        }

        [Theory]
        [InlineData("192.168.1.99")]
        public void CantFindCameraTest(string ip)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.SimulateTimeout();

                try
                {
                    blackVueDownloader.QueryCameraForFileList(ip);
                }
                catch (Exception e)
                {
                    Assert.StartsWith("One or more errors occurred.", e.Message);
                }

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");
            }
        }

        [Theory]
        [InlineData("192.168.1.99", 10)]
        public void CameraRespondValidTest(string ip, int numRecords)
        {
            var blackVueDownloader = new PCL.BlackVueDownloader();

            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWith(200, GenerateRecords(numRecords));

                var body = blackVueDownloader.QueryCameraForFileList(ip);

                httpTest.ShouldHaveCalled($"http://{ip}/blackvue_vod.cgi");

                Assert.True(!string.IsNullOrEmpty(body));

                if (body != null) Assert.Equal(numRecords*2, body.Replace("\r\n", " ").Split(' ').Length);
            }
        }

        [Theory]
        [InlineData("192.168.1.99", 10)]
        [InlineData("192.168.1.99", 1)]
        public void GetListOfFilesAndProcessTest(string ip, int numRecords)
        {
            var filesystem = new Mock<IFileSystemHelper>();

            var blackVueDownloader = new PCL.BlackVueDownloader(filesystem.Object);
            var blackVueDownloaderNoMock = new PCL.BlackVueDownloader();

            var httpTest = new HttpTest();

            var list = blackVueDownloader.GetListOfFilesFromResponse(GenerateRecords(numRecords));

            Assert.Equal(numRecords*2, list.Count);

            // Success test
            for (var i = 0; i < numRecords*4; i++)
            {
                httpTest.RespondWith(200, "OK");
            }
            blackVueDownloader.BlackVueDownloaderCopyStats.Clear();
            blackVueDownloader.ProcessList(ip, Directory.GetCurrentDirectory(), list);
            Assert.Equal(numRecords*4, blackVueDownloader.BlackVueDownloaderCopyStats.Copied);

            // Ignored from above test
            // What happens with the above tests, is that it writes actual files to
            // BlackVueDownloader.Tests\bin\Debug\Record directory,
            // so there should be numrecords * 4 files there
            // And if we loop through again, they should all exist, and therefore be "ignored"
            // We need to do this with an unmocked version of the file system helper
            blackVueDownloaderNoMock.BlackVueDownloaderCopyStats.Clear();
            blackVueDownloaderNoMock.ProcessList(ip, Directory.GetCurrentDirectory(), list);
            Assert.Equal(numRecords*4, blackVueDownloaderNoMock.BlackVueDownloaderCopyStats.Ignored);

            // Fail test
            for (var i = 0; i < numRecords*4; i++)
            {
                httpTest.RespondWith(500, "FAILURE");
            }
            blackVueDownloader.BlackVueDownloaderCopyStats.Clear();
            blackVueDownloader.ProcessList(ip, Directory.GetCurrentDirectory(), list);
            Assert.Equal(numRecords*4, blackVueDownloader.BlackVueDownloaderCopyStats.Errored);

            // Timeout Fail test
            for (var i = 0; i < numRecords*4; i++)
            {
                httpTest.SimulateTimeout();
            }
            blackVueDownloader.BlackVueDownloaderCopyStats.Clear();
            blackVueDownloader.ProcessList(ip, Directory.GetCurrentDirectory(), list);
            Assert.Equal(numRecords*4, blackVueDownloader.BlackVueDownloaderCopyStats.Errored);
        }

        [Theory]
        [InlineData("192.168.1.99")]
        public void DownloadFileIgnoreTest(string ip)
        {
            var filesystem = new Mock<IFileSystemHelper>();

            var blackVueDownloader = new PCL.BlackVueDownloader(filesystem.Object);

            filesystem.Setup(x => x.Exists(Path.Combine("Record", "ignorefile.mp4"))).Returns(true);
            blackVueDownloader.DownloadFile(ip, Directory.GetCurrentDirectory(), "ignorefile.mp4", "video");

            Assert.Equal(1, blackVueDownloader.BlackVueDownloaderCopyStats.Ignored);
        }
    }
}