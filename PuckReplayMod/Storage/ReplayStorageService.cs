using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PuckReplayMod
{
    public class ReplayStorageService
    {
        public string RootDirectory { get; private set; }

        public string ReplaysDirectory { get; private set; }

        public string SummariesDirectory { get; private set; }

        public string TempDirectory { get; private set; }

        public void Initialize()
        {
            this.RootDirectory = Path.Combine(Application.persistentDataPath, "PuckReplayMod");
            this.ReplaysDirectory = Path.Combine(this.RootDirectory, "Replays");
            this.SummariesDirectory = Path.Combine(this.RootDirectory, "Summaries");
            this.TempDirectory = Path.Combine(this.RootDirectory, "Temp");

            Directory.CreateDirectory(this.RootDirectory);
            Directory.CreateDirectory(this.ReplaysDirectory);
            Directory.CreateDirectory(this.SummariesDirectory);
            Directory.CreateDirectory(this.TempDirectory);
        }

        public string SaveReplay(ReplaySessionData session)
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string finalPath = Path.Combine(this.ReplaysDirectory, "replay_" + stamp + ReplayModConstants.ReplayFileExtension);
            string tempPath = Path.Combine(this.TempDirectory, Path.GetFileName(finalPath) + ".tmp");

            JsonSerializerSettings jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            };

            string json = JsonConvert.SerializeObject(session, jsonSettings);
            File.WriteAllText(tempPath, json);

            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath);
            this.WriteReplaySummary(finalPath, session);
            ReplayModLog.Info("Saved replay: " + finalPath);
            return finalPath;
        }

        public void CleanupOldReplays(int storageLimitMb)
        {
            if (storageLimitMb <= 0 || !Directory.Exists(this.ReplaysDirectory))
            {
                return;
            }

            FileInfo[] files = new DirectoryInfo(this.ReplaysDirectory)
                .GetFiles("*" + ReplayModConstants.ReplayFileExtension)
                .OrderBy(file => file.CreationTimeUtc)
                .ToArray();

            long limitBytes = (long)storageLimitMb * 1024L * 1024L;
            long totalBytes = files.Sum(file => file.Length);
            foreach (FileInfo file in files)
            {
                if (totalBytes <= limitBytes)
                {
                    return;
                }

                try
                {
                    long length = file.Length;
                    this.DeleteReplayFile(file);
                    totalBytes -= length;
                    ReplayModLog.Info("Deleted old replay to enforce storage limit: " + file.FullName);
                }
                catch (Exception e)
                {
                    ReplayModLog.Warning("Failed to delete old replay " + file.FullName + ": " + e.Message);
                }
            }
        }

        public int CleanupShortReplays(int minimumLengthSeconds)
        {
            if (minimumLengthSeconds <= 0 || !Directory.Exists(this.ReplaysDirectory))
            {
                return 0;
            }

            int deletedCount = 0;
            FileInfo[] files = new DirectoryInfo(this.ReplaysDirectory)
                .GetFiles("*" + ReplayModConstants.ReplayFileExtension)
                .ToArray();

            foreach (FileInfo file in files)
            {
                try
                {
                    if (!this.IsReplayShorterThan(file.FullName, minimumLengthSeconds))
                    {
                        continue;
                    }

                    this.DeleteReplayFile(file);
                    deletedCount++;
                    ReplayModLog.Info("Deleted short replay: " + file.FullName);
                }
                catch (Exception e)
                {
                    ReplayModLog.Warning("Failed to check short replay cleanup for " + file.FullName + ": " + e.Message);
                }
            }

            return deletedCount;
        }

        private bool IsReplayShorterThan(string filePath, int minimumLengthSeconds)
        {
            JObject root = JObject.Parse(File.ReadAllText(filePath));
            JToken header = root["Header"];
            if (header == null)
            {
                return false;
            }

            int tickRate = header.Value<int?>("TickRate") ?? 0;
            int totalTicks = header.Value<int?>("TotalTicks") ?? 0;
            if (tickRate <= 0 || totalTicks <= 0)
            {
                return false;
            }

            float durationSeconds = totalTicks / (float)tickRate;
            return durationSeconds < minimumLengthSeconds;
        }

        private void WriteReplaySummary(string replayPath, ReplaySessionData session)
        {
            try
            {
                FileInfo file = new FileInfo(replayPath);
                ReplayHeaderDto header = session != null ? session.Header : null;
                ReplaySummaryCache cache = new ReplaySummaryCache
                {
                    FileName = file.Name,
                    SizeBytes = file.Length,
                    LastWriteUtcTicks = file.LastWriteTimeUtc.Ticks,
                    ServerName = header != null ? header.ServerName : string.Empty,
                    RecordedBy = header != null ? header.RecordedBy : string.Empty,
                    TickRate = header != null ? header.TickRate : 0,
                    TotalTicks = header != null ? header.TotalTicks : 0
                };

                File.WriteAllText(ReplayFileReader.GetSummaryPath(replayPath, this.SummariesDirectory), JsonConvert.SerializeObject(cache, Formatting.None));
            }
            catch (Exception e)
            {
                ReplayModLog.Warning("Failed to write replay summary cache for " + replayPath + ": " + e.Message);
            }
        }

        private void DeleteReplayFile(FileInfo file)
        {
            file.Delete();
            this.DeleteReplaySummary(file.FullName);
        }

        private void DeleteReplaySummary(string replayPath)
        {
            string summaryPath = ReplayFileReader.GetSummaryPath(replayPath, this.SummariesDirectory);
            if (File.Exists(summaryPath))
            {
                File.Delete(summaryPath);
            }
        }
    }
}
