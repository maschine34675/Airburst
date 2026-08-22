using Airburst.Patches;
using BepInEx.Bootstrap;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Airburst.Networking
{
    internal static class AirburstNetwork
    {
        internal const string FikaGuid = "com.fika.core";
        private const float SolutionTtl = 2f;
        private const float StartMatchSqr = 1e-6f;

        private struct PendingSolution
        {
            public Vector3 Start;
            public float Distance;
            public float TargetHeight;
            public float Received;
        }

        private static readonly Dictionary<string, List<PendingSolution>> _pending =
            new Dictionary<string, List<PendingSolution>>();

        private static bool _fikaPresent;
        private static bool _sendFailureLogged;

        internal static bool FikaPresent => _fikaPresent;
        internal static bool Active => _fikaPresent && BridgeHasManager();

        internal static void Initialize()
        {
            if (!Chainloader.PluginInfos.TryGetValue(FikaGuid, out BepInEx.PluginInfo fika))
            {
                return;
            }
            Version fikaVersion = fika.Metadata?.Version;
            if (fikaVersion == null || fikaVersion.Major != 2 || fikaVersion.Minor < 4)
            {
                Plugin.LogSource.LogWarning($"Fika {fikaVersion} is outside the tested 2.4.x range; airburst co-op synchronisation stays disabled (local-only).");
                return;
            }

            try
            {
                BridgeSubscribe();
                _fikaPresent = true;
                Plugin.LogSource.LogInfo($"Fika {fikaVersion} detected: airburst solutions will be synchronised between peers.");
            }
            catch (Exception ex)
            {
                _fikaPresent = false;
                Plugin.LogSource.LogWarning($"Fika detected, but the network bridge failed to initialise; airburst rounds fall back to local-only behaviour. {ex}");
            }
        }
        internal static bool IsOwner(bool localShooter, bool shooterIsAI)
        {
            if (!Active || localShooter)
            {
                return true;
            }

            try
            {
                return shooterIsAI && BridgeIsServer();
            }
            catch (Exception ex)
            {
                Disable($"Fika role query failed; airburst co-op synchronisation disabled for this session. {ex}");
                return true;
            }
        }

        internal static void Publish(string profileId, Vector3 startPosition, float distance, float targetHeight)
        {
            if (!Active || string.IsNullOrEmpty(profileId))
            {
                return;
            }

            try
            {
                BridgeSend(profileId, distance, targetHeight, startPosition);
            }
            catch (Exception ex)
            {
                if (!_sendFailureLogged)
                {
                    _sendFailureLogged = true;
                    Plugin.LogSource.LogWarning($"Sending an airburst solution failed; peers will use their local fallback for this raid. {ex}");
                }
            }
        }
        private static void OnSolutionReceived(string profileId, float distance, float targetHeight, Vector3 startPosition)
        {
            if (string.IsNullOrEmpty(profileId) || distance <= 0f || float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return;
            }

            if (TryApplyToTrackedShot(profileId, startPosition, distance, targetHeight))
            {
                return;
            }

            if (!_pending.TryGetValue(profileId, out List<PendingSolution> list))
            {
                list = new List<PendingSolution>();
                _pending[profileId] = list;
            }

            Prune(list);
            list.Add(new PendingSolution
            {
                Start = startPosition,
                Distance = distance,
                TargetHeight = targetHeight,
                Received = Time.time,
            });
        }
        internal static bool TryConsumeSolution(string profileId, Vector3 startPosition, out float distance, out float targetHeight)
        {
            distance = 0f;
            targetHeight = float.NaN;
            if (!Active || string.IsNullOrEmpty(profileId) || !_pending.TryGetValue(profileId, out List<PendingSolution> list))
            {
                return false;
            }

            Prune(list);
            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i].Start - startPosition).sqrMagnitude <= StartMatchSqr)
                {
                    distance = list[i].Distance;
                    targetHeight = list[i].TargetHeight;
                    list.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
        private static bool TryApplyToTrackedShot(string profileId, Vector3 startPosition, float distance, float targetHeight)
        {
            List<AirburstTracker.TrackedShell> tracked = AirburstTracker.Tracked;
            for (int i = 0; i < tracked.Count; i++)
            {
                AirburstTracker.TrackedShell entry = tracked[i];
                if (entry.JumpUp || entry.DetonateIfCutShort || entry.Owned || entry.NetworkSolution
                    || entry.ProfileId != profileId
                    || (entry.StartPosition - startPosition).sqrMagnitude > StartMatchSqr)
                {
                    continue;
                }

                entry.BurstDistance = distance;
                entry.TargetHeight = targetHeight;
                entry.NetworkSolution = true;
                Plugin.LogSource.LogDebug($"Airburst shell re-targeted from peer solution: burst at {distance:F1} m.");
                return true;
            }

            return false;
        }

        private static void Prune(List<PendingSolution> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (Time.time - list[i].Received > SolutionTtl)
                {
                    list.RemoveAt(i);
                }
            }
        }

        internal static void ClearRaidState()
        {
            _pending.Clear();
            _sendFailureLogged = false;
        }

        private static void Disable(string reason)
        {
            _fikaPresent = false;
            _pending.Clear();
            Plugin.LogSource.LogWarning(reason);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BridgeSubscribe()
        {
            FikaBridge.Subscribe(
                OnSolutionReceived,
                ClearRaidState,
                message => Plugin.LogSource.LogInfo(message),
                message => Plugin.LogSource.LogWarning(message));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool BridgeHasManager() => FikaBridge.HasManager;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool BridgeIsServer() => FikaBridge.IsServer;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BridgeSend(string profileId, float distance, float targetHeight, Vector3 startPosition)
            => FikaBridge.Send(profileId, distance, targetHeight, startPosition);
    }
}
