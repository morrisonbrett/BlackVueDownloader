using System;

namespace BlackVueDownloader.PCL
{
    public struct BlackVueDownloaderCopyStats
    {
        public int Copied { get; internal set; }
        public int Ignored { get; internal set; }
        public TimeSpan TotalTime { get; internal set; }
        public int Errored { get; internal set; }

        public void Clear()
        {
            Copied = 0;
            Ignored = 0;
            TotalTime = TimeSpan.MinValue;
            Errored = 0;
        }
    }
}