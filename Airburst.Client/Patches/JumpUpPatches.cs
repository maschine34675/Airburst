using Comfort.Common;
using EFT;
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
    internal class JumpUpImpactPatch : ModulePatch
    {
        private const float ArmingDistance = 14f;
        private const int ProcessedAmmoLimit = 64;
        private static readonly HashSet<string> ProcessedAmmoIds = new HashSet<string>();
        private static readonly Queue<string> ProcessedAmmoOrder = new Queue<string>();

        private static string _cachedIdConfig;
        private static readonly HashSet<string> TemplateIds = new HashSet<string>();

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ClientGameWorld), nameof(ClientGameWorld.ShotDelegate));
        }

        [PatchPostfix]
        static void Postfix(Shot shotResult)
        {
            if (!Plugin.Enabled.Value || shotResult.IsFlyingOutOfTime)
            {
                return;
            }

            if (shotResult.Ammo == null || !IsJumpUpTemplate(shotResult.Ammo.TemplateId.ToString()))
            {
                return;
            }

            ExplosiveAmmoComponent explosiveComponent = shotResult.Ammo.GetItemComponent<ExplosiveAmmoComponent>();
            if (explosiveComponent == null)
            {
                return;
            }
            if ((shotResult.HitPoint - shotResult.StartPosition).magnitude < ArmingDistance)
            {
                return;
            }

            if (!TryMarkProcessed(shotResult.Ammo.Id))
            {
                return;
            }

            Vector3 normal = shotResult.HitNormal.sqrMagnitude > 0.01f
                ? shotResult.HitNormal.normalized
                : Vector3.up;
            string profileId = shotResult.Player?.iPlayer?.ProfileId ?? shotResult.PlayerProfileID;
            float height = Plugin.JumpUpHeight.Value;
            Shot hop = null;
            if (shotResult.Ammo is Ammo ammo
                && Singleton<GameWorld>.Instantiated
                && Singleton<GameWorld>.Instance.SharedBallisticsCalculator is IBallisticsCalculator calculator)
            {
                float hopSpeed = Mathf.Max(5f, Mathf.Sqrt(2f * 9.81f * height) * 1.5f);
                float speedFactor = hopSpeed / Mathf.Max(1f, ammo.InitialSpeed);
                Vector3 origin = shotResult.HitPoint + normal * 0.08f;

                hop = calculator.Shoot(ammo, origin, normal, profileId, shotResult.Weapon, speedFactor, 0);
                if (hop != null)
                {
                    AirburstTracker.Tracked.Add(new AirburstTracker.TrackedShell
                    {
                        Shot = hop,
                        StartPosition = origin,
                        LastPosition = origin,
                        BurstDistance = height,
                        TargetHeight = float.NaN,
                        ProfileId = profileId,
                        AmmoId = shotResult.Ammo.Id,
                        HorizontalMetric = false,
                        Explosive = explosiveComponent,
                        Weapon = shotResult.Weapon,
                        DetonateIfCutShort = true,
                    });
                }
            }
            if (hop == null)
            {
                var entry = new AirburstTracker.TrackedShell
                {
                    ProfileId = profileId,
                    Explosive = explosiveComponent,
                    Weapon = shotResult.Weapon,
                };
                AirburstTracker.Detonate(entry, shotResult.HitPoint + normal * height, normal);
            }
        }

        private static bool IsJumpUpTemplate(string templateId)
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
