using System;

namespace PuckReplayMod
{
    public class ReplayFileSummary
    {
        public string FilePath;
        public string FileName;
        public long SizeBytes;
        public DateTime LastWriteUtc;
        public string ServerName;
        public string RecordedBy;
        public int TickRate;
        public int TotalTicks;
        public bool IsMetadataComplete;

        public float DurationSeconds
        {
            get
            {
                if (this.TickRate <= 0)
                {
                    return 0f;
                }

                return this.TotalTicks / (float)this.TickRate;
            }
        }
    }

    public class ReplaySummaryCache
    {
        public int CacheVersion = 1;
        public string FileName;
        public long SizeBytes;
        public long LastWriteUtcTicks;
        public string ServerName;
        public string RecordedBy;
        public int TickRate;
        public int TotalTicks;
    }
}
