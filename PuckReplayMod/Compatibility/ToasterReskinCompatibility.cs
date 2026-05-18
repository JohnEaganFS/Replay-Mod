using System;
using System.Reflection;
using UnityEngine;

namespace PuckReplayMod
{
    internal sealed class ToasterReskinCompatibility
    {
        private const float RetryDurationSeconds = 12f;
        private const float RetryIntervalSeconds = 0.75f;

        private MethodInfo onPlayerSpawnedMethod;
        private MethodInfo onStickReadyMethod;
        private bool hasLookedForToaster;
        private bool availabilityLogged;
        private bool warnedInvokeFailure;
        private float retryUntilRealtime;
        private float nextRetryRealtime;
        private int lastReplayBodyCount = -1;
        private int lastReplayStickCount = -1;

        public void StartPlayback()
        {
            this.retryUntilRealtime = Time.realtimeSinceStartup + RetryDurationSeconds;
            this.nextRetryRealtime = 0f;
            this.lastReplayBodyCount = -1;
            this.lastReplayStickCount = -1;
            this.TryApplyToReplayPlayers(true);
        }

        public void Tick()
        {
            if (!this.EnsureToasterMethods())
            {
                return;
            }

            int bodyCount;
            int stickCount;
            this.CountReplayObjects(out bodyCount, out stickCount);
            bool replayRosterChanged = bodyCount != this.lastReplayBodyCount || stickCount != this.lastReplayStickCount;
            if (replayRosterChanged)
            {
                this.RequestApply();
            }

            if (Time.realtimeSinceStartup > this.retryUntilRealtime || Time.realtimeSinceStartup < this.nextRetryRealtime)
            {
                return;
            }

            this.nextRetryRealtime = Time.realtimeSinceStartup + RetryIntervalSeconds;
            this.TryApplyToReplayPlayers(false);
        }

        public void RequestApply()
        {
            this.retryUntilRealtime = Mathf.Max(this.retryUntilRealtime, Time.realtimeSinceStartup + RetryDurationSeconds);
            this.nextRetryRealtime = 0f;
        }

        public void StopPlayback()
        {
            this.retryUntilRealtime = 0f;
            this.nextRetryRealtime = 0f;
            this.lastReplayBodyCount = -1;
            this.lastReplayStickCount = -1;
        }

        private void TryApplyToReplayPlayers(bool force)
        {
            if (!this.EnsureToasterMethods())
            {
                return;
            }

            int bodyCount = 0;
            int stickCount = 0;
            PlayerManager playerManager = this.CountReplayObjects(out bodyCount, out stickCount);
            if (playerManager == null)
            {
                return;
            }

            this.lastReplayBodyCount = bodyCount;
            this.lastReplayStickCount = stickCount;

            int appliedPlayers = 0;
            int appliedSticks = 0;
            foreach (Player player in playerManager.GetReplayPlayers())
            {
                if (player == null)
                {
                    continue;
                }

                try
                {
                    if (player.PlayerBody != null && this.onPlayerSpawnedMethod != null)
                    {
                        this.onPlayerSpawnedMethod.Invoke(null, new object[] { player });
                        appliedPlayers++;
                    }

                    if (player.Stick != null && this.onStickReadyMethod != null)
                    {
                        this.onStickReadyMethod.Invoke(null, new object[] { player });
                        appliedSticks++;
                    }
                }
                catch (Exception exception)
                {
                    if (!this.warnedInvokeFailure)
                    {
                        this.warnedInvokeFailure = true;
                        ReplayModLog.Warning("Toaster Reskin compatibility apply failed: " + GetInnermostMessage(exception));
                    }
                }
            }

            if (!this.availabilityLogged && (appliedPlayers > 0 || appliedSticks > 0))
            {
                this.availabilityLogged = true;
                ReplayModLog.Info("Toaster Reskin compatibility bridge applied to replay players.");
            }
        }

        private bool EnsureToasterMethods()
        {
            if (this.hasLookedForToaster)
            {
                return this.onPlayerSpawnedMethod != null || this.onStickReadyMethod != null;
            }

            this.hasLookedForToaster = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("ToasterReskinLoader.api.AppearanceAPI", false);
                if (type == null)
                {
                    continue;
                }

                this.onPlayerSpawnedMethod = type.GetMethod("OnPlayerSpawned", BindingFlags.Public | BindingFlags.Static);
                this.onStickReadyMethod = type.GetMethod("OnStickReady", BindingFlags.Public | BindingFlags.Static);
                ReplayModLog.Info("Detected Toaster Reskin Loader; enabling replay appearance compatibility bridge.");
                return this.onPlayerSpawnedMethod != null || this.onStickReadyMethod != null;
            }

            return false;
        }

        private PlayerManager CountReplayObjects(out int bodyCount, out int stickCount)
        {
            bodyCount = 0;
            stickCount = 0;
            PlayerManager playerManager = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (playerManager == null)
            {
                return null;
            }

            foreach (Player player in playerManager.GetReplayPlayers())
            {
                if (player == null)
                {
                    continue;
                }

                if (player.PlayerBody != null)
                {
                    bodyCount++;
                }

                if (player.Stick != null)
                {
                    stickCount++;
                }
            }

            return playerManager;
        }

        private static string GetInnermostMessage(Exception exception)
        {
            while (exception != null && exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception != null ? exception.Message : "unknown error";
        }
    }
}
