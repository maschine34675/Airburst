using Airburst.Networking;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using JsonType;
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
        private static string _cachedCaliberConfig;
        private static readonly HashSet<string> _calibers = new HashSet<string>();

        internal static bool IsAirburstCaliber(string caliber)
        {
            if (string.IsNullOrEmpty(caliber))
            {
                return false;
            }

            string raw = Plugin.AirburstShellTemplateIds.Value;
            if (!ReferenceEquals(raw, _cachedCaliberConfig) && Singleton<ItemFactory>.Instantiated)
            {
                _calibers.Clear();
                ItemTemplates templates = Singleton<ItemFactory>.Instance.ItemTemplates;
                if (!string.IsNullOrEmpty(raw) && templates != null)
                {
                    foreach (string part in raw.Split(','))
                    {
                        string id = part.Trim();
                        if (id.Length > 0 && templates.TryGetValue(id, out ItemTemplate template)
                            && template is AmmoTemplate ammoTemplate && !string.IsNullOrEmpty(ammoTemplate.Caliber))
                        {
                            _calibers.Add(NormalizeCaliber(ammoTemplate.Caliber));
                        }
                    }
                }
                _cachedCaliberConfig = raw;
            }

            return _calibers.Contains(NormalizeCaliber(caliber));
        }
        private static string NormalizeCaliber(string caliber)
        {
            return caliber.StartsWith("Caliber") ? caliber.Substring("Caliber".Length) : caliber;
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
            public bool JumpUp;
            public float CreatedTime;
            public bool Owned = true;
            public bool NetworkSolution;
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
            AirburstNetwork.ClearRaidState();
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

            string templateId = ammo.TemplateId.ToString();
            if (JumpUpHandler.IsJumpUpTemplate(templateId))
            {
                if (JumpUpHandler.CreatingHop)
                {
                    return;
                }

                AirburstTracker.Tracked.Add(new AirburstTracker.TrackedShell
                {
                    Shot = __result,
                    StartPosition = __result.StartPosition,
                    LastPosition = __result.StartPosition,
                    ProfileId = player,
                    AmmoId = ammo.Id,
                    Explosive = ammo.GetItemComponent<ExplosiveAmmoComponent>(),
                    Weapon = weapon,
                    TargetHeight = float.NaN,
                    JumpUp = true,
                });
                return;
            }

            if (!AirburstTracker.IsAirburstTemplate(templateId))
            {
                return;
            }
            float burstDistance;
            float targetHeight;
            string distanceSource;
            bool owned = false;
            bool networkSolution = AirburstNetwork.TryConsumeSolution(player, __result.StartPosition, out burstDistance, out targetHeight);
            if (networkSolution)
            {
                distanceSource = "peer fire control";
            }
            else
            {
                burstDistance = ResolveBurstDistance(player, weapon, out distanceSource, out targetHeight, out owned);
                if (owned)
                {
                    AirburstNetwork.Publish(player, __result.StartPosition, burstDistance, targetHeight);
                }
            }

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
                CreatedTime = Time.time,
                Owned = owned,
                NetworkSolution = networkSolution,
            });
            Plugin.LogSource.LogDebug($"Airburst shell tracked: burst at {burstDistance:F1} m ({distanceSource}).");
        }
        private static float ResolveBurstDistance(string profileId, Item weapon, out string source, out float targetHeight, out bool owned)
        {
            targetHeight = float.NaN;

            Player localPlayer = Singleton<GameWorld>.Instantiated
                ? Singleton<GameWorld>.Instance.MainPlayer
                : null;
            bool localShooter = localPlayer != null && localPlayer.ProfileId == profileId;

            Player shooter = localShooter
                ? localPlayer
                : Singleton<GameWorld>.Instantiated
                    ? Singleton<GameWorld>.Instance.GetAlivePlayerByProfileID(profileId)
                    : null;
            owned = AirburstNetwork.IsOwner(localShooter, shooter != null && shooter.IsAI);
            float locked = 0f;
            float lockedHeight = float.NaN;
            if (localShooter
                && (AirburstRangeLock.TryGetLock(weapon?.Id, out locked, out lockedHeight)
                    || AirburstRangeLock.TryGetLock(localPlayer.HandsController?.Item?.Id, out locked, out lockedHeight)))
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

                if (entry.JumpUp)
                {
                    if (shot == null || shot.Ammo == null || shot.Ammo.Id != entry.AmmoId)
                    {
                        tracked.RemoveAt(i);
                        continue;
                    }
                    if (shot.HasAchievedTarget)
                    {
                        tracked.RemoveAt(i);
                        JumpUpHandler.OnImpact(entry, shot);
                        continue;
                    }
                    if (shot.IsShotFinished)
                    {
                        tracked.RemoveAt(i);
                    }
                    continue;
                }
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
                    Plugin.LogSource.LogDebug(
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
