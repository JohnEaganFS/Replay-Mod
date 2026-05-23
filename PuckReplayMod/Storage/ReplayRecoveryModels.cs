using System.Collections.Generic;

namespace PuckReplayMod
{
    public class ReplayRecoveryManifest
    {
        public string TempFileName = string.Empty;
        public long CreatedUtcTicks;
        public int ContainerVersion;
        public ReplayHeaderDto Header = new ReplayHeaderDto();
        public int CompletedChunkCount;
        public int CompletedEventCount;
        public int LastTick;
        public long LastCompletedChunkUtcTicks;
    }

    public class ReplayRecoveryCandidate
    {
        public string TempPath = string.Empty;
        public string ManifestPath = string.Empty;
        public long CreatedUtcTicks;
        public long LastWriteUtcTicks;
        public int ChunkCount;
        public int EventCount;
        public int FirstTick;
        public int LastTick;
        public int TickRate;
        public string ServerName = string.Empty;
        public string RecordedBy = string.Empty;
        public long SizeBytes;

        public float DurationSeconds
        {
            get { return this.TickRate > 0 ? this.LastTick / (float)this.TickRate : 0f; }
        }
    }

    public class ReplayRecoveryResult
    {
        public int FoundCount;
        public int RecoveredCount;
        public int DiscardedCount;
        public int FailedCount;
        public readonly List<string> RecoveredFiles = new List<string>();
        public readonly List<string> Errors = new List<string>();
    }
}
