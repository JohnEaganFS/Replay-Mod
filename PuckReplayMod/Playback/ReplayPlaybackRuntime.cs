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

            LastAppliedTick = Math.Max(0, tick);
            replayPlayer.Tick = LastAppliedTick + 1;
            SetTickAccumulator(0f);
        }

        public static void RunImmediateThrough(ReplayPlayer replayPlayer, int targetTick)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || replayPlayer == null || !replayPlayer.IsReplaying)
            {
                return;
            }

            if (replayPlayer.EventMap == null || replayPlayer.EventMap.Count == 0 || ServerTickMethod == null)
            {
                return;
            }

            int guard = 0;
            while (replayPlayer.IsReplaying && replayPlayer.Tick <= targetTick && guard < MaxTicksPerFrame)
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
