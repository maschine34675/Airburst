using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using Systems.Effects;
using UnityEngine;

namespace Airburst.Patches
{
    internal static class AirburstTracker
    {
        private static string _cachedIdConfig;
        private static readonly HashSet<string> _templateIds = new HashSet<string>();
        internal static bool IsAirburstTemplate(string templateId)
        {
            string raw = Plugin.AirburstShellTemplateIds.Value;
            if (!ReferenceEquals(raw, _cachedIdConfig))
            {
                _templateIds.Clear();
                if (!string.IsNullOrEmpty(raw))
                {
                    foreach (string part in raw.Split(','))
                    {
                        string trimmed = part.Trim();
                        if (trimmed.Length > 0)
                        {
                            _templateIds.Add(trimmed);
                        }
                    }
                }
                _cachedIdConfig = raw;
            }

            return templateId != null && _templateIds.Contains(templateId);
        }

        internal sealed class TrackedShell
        {
            public Shot Shot;
            public Vector3 StartPosition;
            public Vector3 LastPosition;
            public float BurstDistance;
            public float TargetHeight;
            public string ProfileId;
            public string AmmoId;
            public bool HorizontalMetric = true;
            public ExplosiveAmmoComponent Explosive;
            public Item Weapon;
            public bool DetonateIfCutShort;
        }
        internal const float MaxHeightAboveTarget = 25f;

        internal static readonly List<TrackedShell> Tracked = new List<TrackedShell>();

        internal static void Detonate(TrackedShell entry, Vector3 position, Vector3 direction)
        {
            if (entry.Explosive == null)
            {
                return;
            }

            string explosionEffect = !string.IsNullOrEmpty(entry.Explosive.Template.ExplosionType)
                ? entry.Explosive.Template.ExplosionType
                : "smallgrenade_expl";
            if (Singleton<Effects>.Instantiated)
            {
                Singleton<Effects>.Instance.EmitGrenade(explosionEffect, position, direction);
            }

            Grenade.Explosion(
                null,
                entry.Explosive,
                position,
                entry.ProfileId,
                Singleton<GameWorld>.Instance.SharedBallisticsCalculator,
                entry.Weapon,
                direction * 0.08f);
        }
    }
    internal class AirburstWorldDisposePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.Dispose));
        }

        [PatchPrefix]
        static void Prefix()
        {
            AirburstTracker.Tracked.Clear();
            AirburstRangeLock.Clear();
        }
    }

    internal class AirburstShotCreatedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BallisticsCalculator), nameof(BallisticsCalculator.CreateShot));
        }

        [PatchPostfix]
        static void Postfix(Shot __result, Ammo ammo, string player, Item weapon)
        {
            if (!Plugin.Enabled.Value || __result == null || ammo == null)
            {
                return;
            }

            if (!AirburstTracker.IsAirburstTemplate(ammo.TemplateId.ToString()))
            {
                return;
            }

            float burstDistance = ResolveBurstDistance(player, weapon, out string distanceSource, out float targetHeight);
            AirburstTracker.Tracked.Add(new AirburstTracker.TrackedShell
            {
                Shot = __result,
                StartPosition = __result.StartPosition,
                LastPosition = __result.StartPosition,
                BurstDistance = burstDistance,
                TargetHeight = targetHeight,
                ProfileId = player,
                AmmoId = ammo.Id,
                Explosive = ammo.GetItemComponent<ExplosiveAmmoComponent>(),
                Weapon = weapon,
            });
            Plugin.LogSource.LogInfo($"Airburst shell tracked: burst at {burstDistance:F1} m ({distanceSource}).");
        }
        private static float ResolveBurstDistance(string profileId, Item weapon, out string source, out float targetHeight)
        {
            targetHeight = float.NaN;

            Player localPlayer = Singleton<GameWorld>.Instantiated
                ? Singleton<GameWorld>.Instance.MainPlayer
                : null;
            bool localShooter = localPlayer != null && localPlayer.ProfileId == profileId;

            if (localShooter && AirburstRangeLock.TryGetLock(weapon?.Id, out float locked, out float lockedHeight))
            {
                source = "range lock";
                targetHeight = lockedHeight;
                return locked;
            }

            if (localShooter && Plugin.UseScopeRangefinderDistance.Value
                && AirburstRangeLock.TryMeasureBurstSolution(localPlayer, true, out float measured, out float measuredHeight, out float raw)
                && measured >= AirburstRangeLock.MinimumBurstDistance)
            {
                source = "ScopeRangefinder";
                targetHeight = measuredHeight;
                return measured;
            }

            Player shooter = localShooter
                ? localPlayer
                : Singleton<GameWorld>.Instantiated
                    ? Singleton<GameWorld>.Instance.GetAlivePlayerByProfileID(profileId)
                    : null;

            if (shooter?.HandsController is Player.FirearmController firearmController)
            {
                if (firearmController.IsInLauncherMode() && firearmController.UnderbarrelWeapon != null)
                {
                    int rangeValue = firearmController.UnderbarrelWeapon.RangeValue;
                    if (rangeValue > 0)
                    {
                        source = "launcher zeroing";
                        return rangeValue;
                    }
                }

                SightComponent sight = shooter.ProceduralWeaponAnimation?.CurrentAimingMod;
                if (sight != null)
                {
                    int calibration = sight.GetCurrentOpticCalibrationDistance();
                    if (calibration > 0)
                    {
                        source = "sight zeroing";
                        return calibration;
                    }
                }
            }

            source = "config default";
            return Plugin.AirburstDefaultDistance.Value;
        }
    }

    internal class AirburstShotUpdatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BallisticsCalculator), nameof(BallisticsCalculator.UpdateShots));
        }

        [PatchPostfix]
        static void Postfix()
        {
            List<AirburstTracker.TrackedShell> tracked = AirburstTracker.Tracked;
            if (tracked.Count == 0)
            {
                return;
            }

            if (!Plugin.Enabled.Value)
            {
                tracked.Clear();
                return;
            }

            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                AirburstTracker.TrackedShell entry = tracked[i];
                Shot shot = entry.Shot;
                if (shot == null || shot.IsShotFinished || shot.Ammo == null || shot.Ammo.Id != entry.AmmoId)
                {
                    tracked.RemoveAt(i);
                    if (entry.DetonateIfCutShort)
                    {
                        AirburstTracker.Detonate(entry, entry.LastPosition, Vector3.up);
                    }
                    continue;
                }

                Vector3 current = shot.CurrentPosition;
                float traveled = Distance(entry, entry.StartPosition, current);
                if (traveled < entry.BurstDistance)
                {
                    entry.LastPosition = current;
                    continue;
                }

                tracked.RemoveAt(i);
                Vector3 burstPosition = InterpolateToThreshold(entry, current, traveled);
                if (!float.IsNaN(entry.TargetHeight)
                    && burstPosition.y - entry.TargetHeight > AirburstTracker.MaxHeightAboveTarget)
                {
                    Plugin.LogSource.LogInfo(
                        $"Airburst skipped: shell was {burstPosition.y - entry.TargetHeight:F0} m above the target, letting it impact instead.");
                    continue;
                }

                Vector3 flightDirection = current - entry.LastPosition;
                flightDirection = flightDirection.sqrMagnitude > 1e-6f
                    ? flightDirection.normalized
                    : (entry.HorizontalMetric ? Vector3.down : Vector3.up);
                shot.AmmoLifeTime = 0f;

                AirburstTracker.Detonate(entry, burstPosition, flightDirection);
            }
        }

        private static float Distance(AirburstTracker.TrackedShell entry, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (entry.HorizontalMetric)
            {
                delta.y = 0f;
            }
            return delta.magnitude;
        }

        private static Vector3 InterpolateToThreshold(AirburstTracker.TrackedShell entry, Vector3 current, float traveled)
        {
            float previous = Distance(entry, entry.StartPosition, entry.LastPosition);
            if (previous >= entry.BurstDistance || traveled <= previous)
            {
                return current;
            }

            float t = (entry.BurstDistance - previous) / (traveled - previous);
            return Vector3.Lerp(entry.LastPosition, current, Mathf.Clamp01(t));
        }
    }
}
