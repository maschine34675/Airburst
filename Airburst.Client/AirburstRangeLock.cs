using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.CameraControl;
using EFT.Communications;
using EFT.InventoryLogic;
using UnityEngine;

namespace Airburst
{
    internal static class AirburstRangeLock
    {
        private static readonly int RaycastMask =
            LayersMaskController.HighPolyWithTerrainMask
            | LayersMaskController.TransparentLayerMask
            | LayersMaskController.HitColliderMask;

        private const float MaxMeasureDistance = 500f;
        private const float RayStartOffset = 0.1f;
        internal const float MinimumBurstDistance = 20f;

        private static float _lockedDistance;
        private static float _lockedTargetHeight = float.NaN;
        private static string _lockedWeaponId;
        internal static bool TryGetLock(string weaponId, out float distance, out float targetHeight)
        {
            distance = _lockedDistance;
            targetHeight = _lockedTargetHeight;
            return _lockedDistance > 0f && weaponId != null && weaponId == _lockedWeaponId;
        }

        internal static void Clear()
        {
            _lockedDistance = 0f;
            _lockedTargetHeight = float.NaN;
            _lockedWeaponId = null;
        }

        internal static void Poll()
        {
            if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            {
                return;
            }
            if (GUIUtility.keyboardControl != 0)
            {
                return;
            }

            if (!Singleton<GameWorld>.Instantiated)
            {
                return;
            }

            Player player = Singleton<GameWorld>.Instance.MainPlayer;
            if (player == null || !IsHotkeyDown(Plugin.AirburstLockHotkey.Value))
            {
                return;
            }
            if (!(player.HandsController is Player.FirearmController firearmController) || !firearmController.IsAiming)
            {
                if (_lockedDistance > 0f)
                {
                    Clear();
                    Plugin.LogSource.LogInfo("Airburst range lock cleared.");
                    Notify("Airburst range lock cleared", ENotificationIconType.Default, null);
                }
                return;
            }

            if (!TryMeasureBurstSolution(player, false, out float distance, out float targetHeight, out float rawDistance))
            {
                Plugin.LogSource.LogInfo("Airburst range lock unchanged: nothing measured.");
                Notify("Airburst: no target measured", ENotificationIconType.Alert, null);
                return;
            }

            if (distance < MinimumBurstDistance)
            {
                Plugin.LogSource.LogWarning(
                    $"Airburst range lock refused: {distance:F1} m is inside the {MinimumBurstDistance:F0} m safety minimum.");
                Notify($"Airburst lock refused: {distance:F0} m (minimum {MinimumBurstDistance:F0} m)", ENotificationIconType.Alert, Color.red);
                return;
            }

            _lockedDistance = distance;
            _lockedTargetHeight = targetHeight;
            _lockedWeaponId = firearmController.Item?.Id;
            Plugin.LogSource.LogInfo(
                $"Airburst range locked: {distance:F1} m ground range (measured {rawDistance:F1} m + {Plugin.AirburstBurstOffset.Value:F1} m offset).");

            string toast = $"Airburst locked: {distance:F0} m";
            if (TryRecommendZeroing(player, firearmController, distance, out int recommended, out int current))
            {
                toast += recommended == current
                    ? $" (zeroing {current} fits)"
                    : $" - set zeroing to {recommended}";
            }
            Notify(toast, ENotificationIconType.Default, null);
        }
        private static bool TryRecommendZeroing(Player player, Player.FirearmController firearmController, float distance, out int recommended, out int current)
        {
            recommended = 0;
            current = 0;

            int[] steps = null;
            if (firearmController.IsInLauncherMode() && firearmController.UnderbarrelWeapon != null)
            {
                steps = firearmController.UnderbarrelWeapon.SightingRange;
                current = firearmController.UnderbarrelWeapon.RangeValue;
            }
            else
            {
                SightComponent sight = player.ProceduralWeaponAnimation?.CurrentAimingMod;
                if (sight != null)
                {
                    steps = sight.GetScopeCalibrationDistances(sight.SelectedScope);
                    current = sight.GetCurrentOpticCalibrationDistance();
                }
            }

            if (steps == null || steps.Length == 0)
            {
                return false;
            }
            recommended = steps[0];
            float bestDelta = float.MaxValue;
            for (int i = 0; i < steps.Length; i++)
            {
                float delta = Mathf.Abs(steps[i] - distance);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    recommended = steps[i];
                }
            }

            return recommended > 0;
        }
        private static void Notify(string message, ENotificationIconType icon, Color? color)
        {
            try
            {
                NotificationManager.DisplayMessageNotification(message, ENotificationDurationType.Default, icon, color);
            }
            catch
            {
            }
        }
        internal static bool TryMeasureBurstSolution(Player player, bool rangefinderOnly, out float distance, out float targetHeight, out float rawDistance)
        {
            distance = 0f;
            targetHeight = float.NaN;
            rawDistance = 0f;

            if (!TryMeasurePoint(player, rangefinderOnly, out Vector3 point, out Vector3 origin))
            {
                return false;
            }

            Vector3 ground = point - origin;
            rawDistance = ground.magnitude;
            ground.y = 0f;

            distance = ground.magnitude + Plugin.AirburstBurstOffset.Value;
            targetHeight = point.y;
            return distance > 0f;
        }
        private static bool TryMeasurePoint(Player player, bool rangefinderOnly, out Vector3 point, out Vector3 origin)
        {
            point = Vector3.zero;
            origin = Vector3.zero;

            if (!CameraManager.Exist)
            {
                return false;
            }

            Camera camera = CameraManager.Instance.Camera;
            if (camera == null)
            {
                return false;
            }

            origin = camera.transform.position;
            Vector3 direction = camera.transform.forward;

            if (Plugin.UseScopeRangefinderDistance.Value
                && ScopeRangefinderBridge.TryGetFreshDistance(out float measured))
            {
                point = origin + direction * measured;
                return true;
            }
            if (rangefinderOnly || !TryRaycastSkippingSelfHits(player, origin, direction, out RaycastHit hit))
            {
                return false;
            }

            point = hit.point;
            return true;
        }
        private static bool TryRaycastSkippingSelfHits(Player player, Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            hit = default;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                MaxMeasureDistance,
                RaycastMask,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            ProceduralWeaponAnimation weaponAnimation = player.ProceduralWeaponAnimation;
            Transform weaponRoot = weaponAnimation?.HandsContainer?.WeaponRoot;
            Transform weaponTransform = weaponAnimation?.HandsContainer?.Weapon?.transform;
            Transform cameraContainer = weaponAnimation?.CameraContainer?.transform;
            Transform playerBody = player.PlayerBody != null ? player.PlayerBody.transform : null;
            Transform playerMesh = player.PlayerBody != null ? player.PlayerBody.MeshTransform : null;
            Transform playerRoot = player.Transform?.Original;

            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit candidate = hits[i];
                if (candidate.distance < RayStartOffset || candidate.distance >= best)
                {
                    continue;
                }

                Transform hitTransform = candidate.collider != null ? candidate.collider.transform : null;
                if (hitTransform == null
                    || IsInHierarchy(hitTransform, weaponRoot)
                    || IsInHierarchy(hitTransform, weaponTransform)
                    || IsInHierarchy(hitTransform, cameraContainer)
                    || IsInHierarchy(hitTransform, playerBody)
                    || IsInHierarchy(hitTransform, playerMesh)
                    || IsInHierarchy(hitTransform, playerRoot))
                {
                    continue;
                }

                best = candidate.distance;
                hit = candidate;
                found = true;
            }

            return found;
        }

        private static bool IsInHierarchy(Transform candidate, Transform root)
        {
            return root != null && candidate.IsChildOf(root);
        }
        private static bool IsHotkeyDown(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !Input.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
