using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace PuckReplayMod
{
    internal static class ReplayPlaybackRuntime
    {
        private static readonly MethodInfo ServerTickMethod = AccessTools.Method(typeof(ReplayPlayer), "Server_Tick");
        private static readonly FieldInfo TickAccumulatorField = AccessTools.Field(typeof(ReplayPlayer), "tickAccumulator");
        private const int MaxTicksPerFrame = 16;

        public static ReplayPlayer ControlledPlayer { get; private set; }

        public static bool IsPaused { get; private set; }

        public static float PlaybackSpeed { get; private set; } = 1f;

        public static int LastAppliedTick { get; private set; }

        public static void Attach(ReplayPlayer replayPlayer)
        {
            ControlledPlayer = replayPlayer;
            IsPaused = false;
            PlaybackSpeed = 1f;
            LastAppliedTick = replayPlayer != null ? replayPlayer.Tick : 0;
            SetTickAccumulator(0f);
        }

        public static void Detach(ReplayPlayer replayPlayer)
        {
            if (ControlledPlayer != replayPlayer)
            {
                return;
            }

            ControlledPlayer = null;
            IsPaused = false;
            PlaybackSpeed = 1f;
            LastAppliedTick = 0;
        }

        public static void SetPaused(bool paused)
        {
            IsPaused = paused;
            if (paused)
            {
                SetTickAccumulator(0f);
            }
        }

        public static void SetPlaybackSpeed(float speed)
        {
            PlaybackSpeed = Mathf.Clamp(speed, 0.1f, 5f);
        }

        public static bool TryUpdateControlledReplay(ReplayPlayer replayPlayer)
        {
            if (ControlledPlayer != replayPlayer)
            {
                return false;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || replayPlayer == null || !replayPlayer.IsReplaying)
            {
                return true;
            }

            if (IsPaused)
            {
                return true;
            }

            if (replayPlayer.EventMap == null || replayPlayer.EventMap.Count == 0 || ServerTickMethod == null)
            {
                return true;
            }

            int maxTick = GetMaxEventTick(replayPlayer);
            if (replayPlayer.Tick > maxTick)
            {
                PauseAtEnd(replayPlayer, maxTick);
                return true;
            }

            float accumulator = GetTickAccumulator() + (Time.deltaTime * replayPlayer.TickRate * PlaybackSpeed);
            int ticksToRun = Mathf.Min(Mathf.FloorToInt(accumulator), MaxTicksPerFrame);
            if (ticksToRun <= 0)
            {
                SetTickAccumulator(accumulator);
                return true;
            }

            accumulator -= ticksToRun;
            for (int i = 0; i < ticksToRun; i++)
            {
                if (!replayPlayer.IsReplaying)
                {
                    break;
                }

                try
                {
                    ServerTickMethod.Invoke(replayPlayer, new object[] { replayPlayer.Tick, false });
                    LastAppliedTick = replayPlayer.Tick;
                }
                catch (Exception exception)
                {
                    ReplayModLog.Warning("Controlled replay tick failed: " + exception.Message);
                    break;
                }

                if (replayPlayer.IsReplaying)
                {
                    replayPlayer.Tick++;
                    if (replayPlayer.Tick > maxTick)
                    {
                        PauseAtEnd(replayPlayer, maxTick);
                        return true;
                    }
                }
            }

            SetTickAccumulator(accumulator);
            return true;
        }

        public static bool ShouldApplyEventsInstantly(ReplayPlayer replayPlayer)
        {
            return ControlledPlayer == replayPlayer && IsPaused;
        }

        public static void SetVisibleTick(ReplayPlayer replayPlayer, int tick)
        {
            if (ControlledPlayer != replayPlayer || replayPlayer == null)
            {
                return;
            }

            int maxTick = replayPlayer.EventMap != null && replayPlayer.EventMap.Count > 0 ? GetMaxEventTick(replayPlayer) : int.MaxValue;
            LastAppliedTick = Math.Min(Math.Max(0, tick), maxTick);
            replayPlayer.Tick = LastAppliedTick + 1;
            SetTickAccumulator(0f);
        }

        public static void RunImmediateThrough(ReplayPlayer replayPlayer, int targetTick)
        {
            RunImmediateThrough(replayPlayer, targetTick, MaxTicksPerFrame);
        }

        public static void RunImmediateThrough(ReplayPlayer replayPlayer, int targetTick, int maxTicks)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || replayPlayer == null || !replayPlayer.IsReplaying)
            {
                return;
            }

            if (replayPlayer.EventMap == null || replayPlayer.EventMap.Count == 0 || ServerTickMethod == null)
            {
                return;
            }

            int maxTick = GetMaxEventTick(replayPlayer);
            targetTick = Mathf.Min(targetTick, maxTick);
            int guard = 0;
            int guardLimit = Math.Max(1, maxTicks);
            while (replayPlayer.IsReplaying && replayPlayer.Tick <= targetTick && guard < guardLimit)
            {
                guard++;
                try
                {
                    ServerTickMethod.Invoke(replayPlayer, new object[] { replayPlayer.Tick, false });
                    LastAppliedTick = replayPlayer.Tick;
                }
                catch (Exception exception)
                {
                    ReplayModLog.Warning("Immediate replay seek tick failed: " + exception.Message);
                    break;
                }

                if (replayPlayer.IsReplaying)
                {
                    replayPlayer.Tick++;
                }
            }

            if (replayPlayer.IsReplaying && replayPlayer.Tick > maxTick)
            {
                PauseAtEnd(replayPlayer, maxTick);
                return;
            }

            SetTickAccumulator(0f);
        }

        public static int GetVisibleTick(ReplayPlayer replayPlayer)
        {
            if (ControlledPlayer == replayPlayer)
            {
                return LastAppliedTick;
            }

            return replayPlayer != null ? replayPlayer.Tick : 0;
        }

        public static float GetInterpolationFraction(ReplayPlayer replayPlayer)
        {
            if (ControlledPlayer != replayPlayer || replayPlayer == null || IsPaused)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetTickAccumulator());
        }

        private static int GetMaxEventTick(ReplayPlayer replayPlayer)
        {
            if (replayPlayer == null || replayPlayer.EventMap == null || replayPlayer.EventMap.Count == 0)
            {
                return 0;
            }

            return replayPlayer.EventMap.Keys[replayPlayer.EventMap.Keys.Count - 1];
        }

        private static void PauseAtEnd(ReplayPlayer replayPlayer, int maxTick)
        {
            LastAppliedTick = Math.Max(0, maxTick);
            if (replayPlayer != null)
            {
                replayPlayer.Tick = LastAppliedTick + 1;
            }

            IsPaused = true;
            SetTickAccumulator(0f);
        }

        private static float GetTickAccumulator()
        {
            if (ControlledPlayer == null || TickAccumulatorField == null)
            {
                return 0f;
            }

            object value = TickAccumulatorField.GetValue(ControlledPlayer);
            return value is float ? (float)value : 0f;
        }

        private static void SetTickAccumulator(float value)
        {
            if (ControlledPlayer == null || TickAccumulatorField == null)
            {
                return;
            }

            TickAccumulatorField.SetValue(ControlledPlayer, value);
        }
    }
}
