using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace PuckReplayMod
{
    public class ReplayStorageService
    {
        public string RootDirectory { get; private set; }

        public string ReplaysDirectory { get; private set; }

        public string TempDirectory { get; private set; }

        public void Initialize()
        {
            this.RootDirectory = Path.Combine(Application.persistentDataPath, "PuckReplayMod");
            this.ReplaysDirectory = Path.Combine(this.RootDirectory, "Replays");
            this.TempDirectory = Path.Combine(this.RootDirectory, "Temp");

            Directory.CreateDirectory(this.RootDirectory);
            Directory.CreateDirectory(this.ReplaysDirectory);
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
                    file.Delete();
                    totalBytes -= length;
                    ReplayModLog.Info("Deleted old replay to enforce storage limit: " + file.FullName);
                }
                catch (Exception e)
                {
                    ReplayModLog.Warning("Failed to delete old replay " + file.FullName + ": " + e.Message);
                }
            }
        }
    }
}
