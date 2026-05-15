using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PuckReplayMod
{
    public class ReplayFileReader
    {
        public List<ReplayFileSummary> GetRecentReplays(string replayDirectory, string summaryDirectory, int maxCount)
        {
            List<ReplayFileSummary> summaries = new List<ReplayFileSummary>();
            if (!Directory.Exists(replayDirectory))
            {
                return summaries;
            }

            FileInfo[] files = new DirectoryInfo(replayDirectory)
                .GetFiles("*" + ReplayModConstants.ReplayFileExtension)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(Math.Max(0, maxCount))
                .ToArray();

            foreach (FileInfo file in files)
            {
                try
                {
                    summaries.Add(this.ReadSummaryFast(file, summaryDirectory));
                }
                catch (Exception exception)
                {
                    ReplayModLog.Warning("Failed to read replay summary " + file.FullName + ": " + exception.Message);
                }
            }

            return summaries;
        }

        public bool IndexNextMissingSummary(string replayDirectory, string summaryDirectory, int maxCount)
        {
            if (!Directory.Exists(replayDirectory))
            {
                return false;
            }

            FileInfo[] files = new DirectoryInfo(replayDirectory)
                .GetFiles("*" + ReplayModConstants.ReplayFileExtension)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(Math.Max(0, maxCount))
                .ToArray();

            foreach (FileInfo file in files)
            {
                if (this.IsSummaryCacheFresh(file, summaryDirectory))
                {
                    continue;
                }

                try
                {
                    this.ReadSummary(file.FullName, summaryDirectory);
                    return true;
                }
                catch (Exception exception)
                {
                    ReplayModLog.Warning("Failed to index replay summary " + file.FullName + ": " + exception.Message);
                }
            }

            return false;
        }

        public ReplayFileSummary ReadSummary(string filePath, string summaryDirectory)
        {
            JObject root = JObject.Parse(File.ReadAllText(filePath));
            ReplayHeaderDto header = this.ReadHeader(root);
            FileInfo file = new FileInfo(filePath);
            ReplayFileSummary summary = new ReplayFileSummary
            {
                FilePath = filePath,
                FileName = file.Name,
                SizeBytes = file.Length,
                LastWriteUtc = file.LastWriteTimeUtc,
                ServerName = header.ServerName,
                RecordedBy = header.RecordedBy,
                TickRate = header.TickRate,
                TotalTicks = header.TotalTicks,
                IsMetadataComplete = true
            };

            this.WriteSummaryCache(summary, summaryDirectory);
            return summary;
        }

        public void WriteSummaryCache(ReplayFileSummary summary, string summaryDirectory)
        {
            if (summary == null || string.IsNullOrEmpty(summary.FilePath))
            {
                return;
            }

            ReplaySummaryCache cache = new ReplaySummaryCache
            {
                FileName = summary.FileName,
                SizeBytes = summary.SizeBytes,
                LastWriteUtcTicks = summary.LastWriteUtc.Ticks,
                ServerName = summary.ServerName,
                RecordedBy = summary.RecordedBy,
                TickRate = summary.TickRate,
                TotalTicks = summary.TotalTicks
            };

            string summaryPath = GetSummaryPath(summary.FilePath, summaryDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath));
            File.WriteAllText(summaryPath, JsonConvert.SerializeObject(cache, Formatting.None));
        }

        public ReplaySessionData Load(string filePath)
        {
            JObject root = JObject.Parse(File.ReadAllText(filePath));
            ReplaySessionData session = new ReplaySessionData
            {
                Header = this.ReadHeader(root),
                Events = new List<ReplayEventDto>()
            };

            JArray events = root["Events"] as JArray;
            if (events == null)
            {
                throw new InvalidDataException("Replay is missing an Events array.");
            }

            foreach (JToken eventToken in events)
            {
                string type = eventToken.Value<string>("Type");
                JToken payloadToken = eventToken["Payload"];
                session.Events.Add(new ReplayEventDto
                {
                    Tick = eventToken.Value<int?>("Tick") ?? 0,
                    Type = type,
                    Payload = this.ReadPayload(type, payloadToken)
                });
            }

            return session;
        }

        public static string GetSummaryPath(string replayFilePath, string summaryDirectory)
        {
            return Path.Combine(summaryDirectory, Path.GetFileName(replayFilePath) + ReplayModConstants.ReplaySummaryFileSuffix);
        }

        private ReplayFileSummary ReadSummaryFast(FileInfo file, string summaryDirectory)
        {
            ReplayFileSummary cachedSummary = this.TryReadSummaryCache(file, summaryDirectory);
            if (cachedSummary != null)
            {
                return cachedSummary;
            }

            return new ReplayFileSummary
            {
                FilePath = file.FullName,
                FileName = file.Name,
                SizeBytes = file.Length,
                LastWriteUtc = file.LastWriteTimeUtc,
                ServerName = Path.GetFileNameWithoutExtension(file.Name),
                RecordedBy = string.Empty,
                TickRate = 0,
                TotalTicks = 0,
                IsMetadataComplete = false
            };
        }

        private ReplayFileSummary TryReadSummaryCache(FileInfo file, string summaryDirectory)
        {
            string summaryPath = GetSummaryPath(file.FullName, summaryDirectory);
            if (!File.Exists(summaryPath))
            {
                return null;
            }

            ReplaySummaryCache cache = JsonConvert.DeserializeObject<ReplaySummaryCache>(File.ReadAllText(summaryPath));
            if (cache == null || cache.SizeBytes != file.Length || cache.LastWriteUtcTicks != file.LastWriteTimeUtc.Ticks)
            {
                return null;
            }

            return new ReplayFileSummary
            {
                FilePath = file.FullName,
                FileName = file.Name,
                SizeBytes = file.Length,
                LastWriteUtc = file.LastWriteTimeUtc,
                ServerName = cache.ServerName,
                RecordedBy = cache.RecordedBy,
                TickRate = cache.TickRate,
                TotalTicks = cache.TotalTicks,
                IsMetadataComplete = true
            };
        }

        private bool IsSummaryCacheFresh(FileInfo file, string summaryDirectory)
        {
            return this.TryReadSummaryCache(file, summaryDirectory) != null;
        }

        private ReplayHeaderDto ReadHeader(JObject root)
        {
            JToken headerToken = root["Header"];
            if (headerToken == null)
            {
                throw new InvalidDataException("Replay is missing a Header object.");
            }

            ReplayHeaderDto header = headerToken.ToObject<ReplayHeaderDto>();
            if (header == null)
            {
                throw new InvalidDataException("Replay header could not be read.");
            }

            if (header.Magic != ReplayModConstants.ReplayMagic)
            {
                throw new InvalidDataException("Replay magic does not match " + ReplayModConstants.ReplayMagic + ".");
            }

            if (header.FormatVersion > 2)
            {
                throw new InvalidDataException("Replay format version " + header.FormatVersion + " is newer than this mod supports.");
            }

            if (header.TickRate <= 0)
            {
                throw new InvalidDataException("Replay tick rate is invalid.");
            }

            return header;
        }

        private object ReadPayload(string type, JToken payloadToken)
        {
            if (payloadToken == null || payloadToken.Type == JTokenType.Null)
            {
                return null;
            }

            switch (type)
            {
                case "InitialSnapshot":
                    return payloadToken.ToObject<InitialSnapshotPayload>();
                case "TransformFrame":
                    return payloadToken.ToObject<TransformFramePayload>();
                case "PlayerSpawned":
                case "PlayerDespawned":
                    return payloadToken.ToObject<PlayerLifecyclePayload>();
                case "PlayerBodySpawned":
                case "PlayerBodyDespawned":
                    return payloadToken.ToObject<BodyLifecyclePayload>();
                case "PuckSpawned":
                case "PuckDespawned":
                    return payloadToken.ToObject<PuckLifecyclePayload>();
                case "PlayerState":
                    return payloadToken.ToObject<PlayerSnapshotPayload>();
                case "GameState":
                    return payloadToken.ToObject<GameStatePayload>();
                case "ScoreboardSnapshot":
                    return payloadToken.ToObject<ScoreboardSnapshotPayload>();
                case "ChatMessage":
                    return payloadToken.ToObject<ChatMessagePayload>();
                case "Marker":
                    return payloadToken.ToObject<MarkerPayload>();
                default:
                    return payloadToken;
            }
        }
    }
}
