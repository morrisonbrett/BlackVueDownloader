using System;

namespace BlackVueDownloader.PCL
{
    public struct BlackVueDownloaderCopyStats
    {
        public int Copied { get; set; }
        public int Ignored { get; set; }
        public TimeSpan TotalTime { get; set; }
        public int Errored { get; set; }
        public int TmpDeleted { get; set; }

        public void Clear()
        {
            Copied = 0;
            Ignored = 0;
            TotalTime = TimeSpan.MinValue;
            Errored = 0;
            TmpDeleted = 0;
        }
    }
}