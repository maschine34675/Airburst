using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Airburst.Patches;
using UnityEngine;

namespace Airburst
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.maschine.Airburst";
        public const string PluginName = "maschine-Airburst";
        public const string PluginVersion = "1.0.0";

        public const string DefaultAirburstShellTemplateId = "67d4f0c8a1b2e30123457041";
        public const string DefaultJumpUpTemplateId = "67d4f0c8a1b2e30123457043";

        public static ManualLogSource LogSource;
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<string> AirburstShellTemplateIds;
        public static ConfigEntry<float> AirburstDefaultDistance;
        public static ConfigEntry<bool> UseScopeRangefinderDistance;
        public static ConfigEntry<KeyboardShortcut> AirburstLockHotkey;
        public static ConfigEntry<float> AirburstBurstOffset;
        public static ConfigEntry<string> JumpUpTemplateIds;
        public static ConfigEntry<float> JumpUpHeight;

        private void Awake()
        {
            LogSource = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                Tagged("Enabled", 100, "Enable mid-flight detonation for the airburst round."));
            AirburstShellTemplateIds = Config.Bind("General", "AirburstShellTemplateIds", DefaultAirburstShellTemplateId,
                Tagged("Airburst Round Template IDs", 50, "Comma-separated template IDs treated as airburst rounds. Ships with this mod's XM1166 HEAB; add explosive rounds from other mods to give them the same fire control (e.g. WTT-Armory's 25x59mm XM1019: 6938bc0b6e96bcf17932873e)."));
            AirburstDefaultDistance = Config.Bind("General", "AirburstDefaultDistance", 100f,
                Tagged("Default Burst Distance (m)", 70, "Fallback detonation distance when no sight zeroing is available (no sight mounted).", new AcceptableValueRange<float>(25f, 400f)));
            UseScopeRangefinderDistance = Config.Bind("General", "UseScopeRangefinderDistance", true,
                Tagged("Use ScopeRangefinder Distance", 60, "When ScopeRangefinder is installed, burst at its live measured distance (meter-exact, if fresher than 3 s) instead of the sight zeroing steps."));
            AirburstLockHotkey = Config.Bind("General", "AirburstLockHotkey", new KeyboardShortcut(KeyCode.J),
                Tagged("Range Lock Hotkey", 90, "Aim down sights at the cover and press to lock that range for airburst rounds; press from the hip to clear it. Defaults to the same key as ScopeRangefinder's zeroing hotkey, so a single press can do both (SR zeroes only if its own Auto Zero is enabled)."));
            AirburstBurstOffset = Config.Bind("General", "AirburstBurstOffset", 1f,
                Tagged("Burst Offset (m)", 80, "Meters added to a measured range so the shell bursts past the cover instead of level with it (real fire control does the same).", new AcceptableValueRange<float>(0f, 10f)));

            JumpUpTemplateIds = Config.Bind("JumpUp", "JumpUpTemplateIds", DefaultJumpUpTemplateId,
                Tagged("Jump-Up Round Template IDs", 90, "Comma-separated template IDs treated as bounding (jump-up) grenades: on impact they detonate offset from the surface along its normal."));
            JumpUpHeight = Config.Bind("JumpUp", "JumpUpHeight", 1.5f,
                Tagged("Jump Height (m)", 100, "Distance the HE charge jumps away from the impact surface before detonating.", new AcceptableValueRange<float>(0.5f, 3f)));

            new AirburstShotCreatedPatch().Enable();
            new AirburstShotUpdatePatch().Enable();
            new AirburstWorldDisposePatch().Enable();
            new JumpUpImpactPatch().Enable();
            LogSource.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void Update()
        {
            AirburstRangeLock.Poll();
        }

        private static ConfigDescription Tagged(string displayName, int order, string description)
        {
            return Tagged(displayName, order, description, null);
        }

        private static ConfigDescription Tagged(string displayName, int order, string description,
            AcceptableValueBase acceptableValues)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new ConfigurationManagerAttributes { DispName = displayName, Order = order });
        }
    }
}
