using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using System.Collections.Generic;
using UnityEngine;

namespace Airburst.Patches
{
    internal static class JumpUpHandler
    {
        private const float ArmingDistance = 14f;
        private const int ProcessedAmmoLimit = 64;
        private static readonly HashSet<string> ProcessedAmmoIds = new HashSet<string>();
        private static readonly Queue<string> ProcessedAmmoOrder = new Queue<string>();
        internal static bool CreatingHop { get; private set; }

        private static string _cachedIdConfig;
        private static readonly HashSet<string> TemplateIds = new HashSet<string>();

        internal static bool IsJumpUpTemplate(string templateId)
        {
            string raw = Plugin.JumpUpTemplateIds.Value;
            if (!ReferenceEquals(raw, _cachedIdConfig))
            {
                TemplateIds.Clear();
                if (!string.IsNullOrEmpty(raw))
                {
                    foreach (string part in raw.Split(','))
                    {
                        string trimmed = part.Trim();
                        if (trimmed.Length > 0)
                        {
                            TemplateIds.Add(trimmed);
                        }
                    }
                }
                _cachedIdConfig = raw;
            }

            return templateId != null && TemplateIds.Contains(templateId);
        }
        internal static void OnImpact(AirburstTracker.TrackedShell entry, Shot shot)
        {
            if (entry.Explosive == null)
            {
                return;
            }
            if ((shot.HitPoint - entry.StartPosition).magnitude < ArmingDistance)
            {
                return;
            }

            if (!TryMarkProcessed(entry.AmmoId))
            {
                return;
            }

            Vector3 normal = shot.HitNormal.sqrMagnitude > 0.01f
                ? shot.HitNormal.normalized
                : Vector3.up;
            float height = Plugin.JumpUpHeight.Value;
            Shot hop = null;
            if (shot.Ammo is Ammo ammo
                && Singleton<GameWorld>.Instantiated
                && Singleton<GameWorld>.Instance.SharedBallisticsCalculator is IBallisticsCalculator calculator)
            {
                float hopSpeed = Mathf.Max(5f, Mathf.Sqrt(2f * 9.81f * height) * 1.5f);
                float speedFactor = hopSpeed / Mathf.Max(1f, ammo.InitialSpeed);
                Vector3 origin = shot.HitPoint + normal * 0.08f;

                CreatingHop = true;
                try
                {
                    hop = calculator.Shoot(ammo, origin, normal, entry.ProfileId, entry.Weapon, speedFactor, 0);
                }
                finally
                {
                    CreatingHop = false;
                }
                if (hop != null)
                {
                    AirburstTracker.Tracked.Add(new AirburstTracker.TrackedShell
                    {
                        Shot = hop,
                        StartPosition = origin,
                        LastPosition = origin,
                        BurstDistance = height,
                        TargetHeight = float.NaN,
                        ProfileId = entry.ProfileId,
                        AmmoId = entry.AmmoId,
                        HorizontalMetric = false,
                        Explosive = entry.Explosive,
                        Weapon = entry.Weapon,
                        DetonateIfCutShort = true,
                    });
                }
            }
            if (hop == null)
            {
                AirburstTracker.Detonate(entry, shot.HitPoint + normal * height, normal);
            }
        }

        private static bool TryMarkProcessed(string ammoId)
        {
            if (!ProcessedAmmoIds.Add(ammoId))
            {
                return false;
            }

            ProcessedAmmoOrder.Enqueue(ammoId);
            while (ProcessedAmmoOrder.Count > ProcessedAmmoLimit)
            {
                ProcessedAmmoIds.Remove(ProcessedAmmoOrder.Dequeue());
            }
            return true;
        }
    }
}
