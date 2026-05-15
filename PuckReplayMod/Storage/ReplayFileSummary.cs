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
}
