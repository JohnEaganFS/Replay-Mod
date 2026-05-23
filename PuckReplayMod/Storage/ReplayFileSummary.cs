using System;
using System.Collections.Generic;

namespace PuckReplayMod
{
    public class ReplayFileSummary
    {
        public string FilePath;
        public string FileName;
        public long SizeBytes;
        public long FileCreatedUtcTicks;
        public DateTime LastWriteUtc;
        public string ServerName;
        public string DisplayName;
        public string RecordedBy;
        public string ReplayMagic;
        public int ReplayFormatVersion;
        public string ReplayContainerFormat;
        public int ReplayContainerVersion;
        public string ModVersion;
        public string GameVersion;
        public long StartedUtcTicks;
        public long EndedUtcTicks;
        public int TickRate;
        public int TotalTicks;
        public int EventCount;
        public bool HasScoreboard;
        public bool HasChat;
        public bool HasMarkers;
        public bool HasGoals;
        public int GoalCount;
        public int MarkerCount;
        public List<ReplayTimelineEntrySummary> TimelineEvents = new List<ReplayTimelineEntrySummary>();
        public List<ReplayGameSegmentSummary> GameSegments = new List<ReplayGameSegmentSummary>();
        public bool IsFavorite;
        public bool IsImported;
        public long ImportedUtcTicks;
        public bool IsMetadataComplete;
        public int SummaryCacheVersion;
        public long SummaryGeneratedUtcTicks;
        public string SummaryGeneratedByModVersion;

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
        public int CacheVersion = ReplayModConstants.ReplaySummaryCacheVersion;
        public string FileName;
        public long SizeBytes;
        public long FileCreatedUtcTicks;
        public long LastWriteUtcTicks;
        public string ServerName;
        public string DisplayName;
        public string RecordedBy;
        public string ReplayMagic;
        public int ReplayFormatVersion;
        public string ReplayContainerFormat;
        public int ReplayContainerVersion;
        public string ModVersion;
        public string GameVersion;
        public long StartedUtcTicks;
        public long EndedUtcTicks;
        public int TickRate;
        public int TotalTicks;
        public int EventCount;
        public bool HasScoreboard;
        public bool HasChat;
        public bool HasMarkers;
        public bool HasGoals;
        public int GoalCount;
        public int MarkerCount;
        public List<ReplayTimelineEntrySummary> TimelineEvents = new List<ReplayTimelineEntrySummary>();
        public List<ReplayGameSegmentSummary> GameSegments = new List<ReplayGameSegmentSummary>();
        public bool IsFavorite;
        public bool IsImported;
        public long ImportedUtcTicks;
        public long SummaryGeneratedUtcTicks;
        public string SummaryGeneratedByModVersion;
    }

    public class ReplayTimelineEntrySummary
    {
        public int Tick;
        public string Type;
        public string Team;
        public string Label;
        public string Tooltip;
    }

    public class ReplayGameSegmentSummary
    {
        public int Index;
        public int StartTick;
        public int EndTick;
        public string Label;
        public int BlueScore;
        public int RedScore;
    }
}
