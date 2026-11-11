using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace EnemyHealthBars
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ryuka.sulfur.enemy_health_bars";
        public const string PluginName = "Enemy Health Bars";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<BarDisplayMode> Mode;
        internal static ConfigEntry<BarStyleId> Style;
        internal static ConfigEntry<float> MaxDistance;
        internal static ConfigEntry<float> BarScale;
        internal static ConfigEntry<bool> ShowName;
        internal static ConfigEntry<bool> DebugLog;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Mode = Config.Bind("Display", "DisplayMode", BarDisplayMode.Damaged,
                "Damaged shows a bar for a few seconds after an enemy loses health. Nearby shows a bar on every enemy in range that you can see.");
            Style = Config.Bind("Display", "BarStyle", BarStyleId.Sulfur,
                "Look of the bars. Sulfur matches the HUD health bar and follows its color. Rounds is a row of ten rings like the ammo gauge. Ichor is a bar of dripping ink. Bone has a skull at its left end. Classic is a plain bar.");
            MaxDistance = Config.Bind("Display", "MaxDistance", 30f,
                new ConfigDescription("How far away a bar can still show, in meters.",
                    new AcceptableValueRange<float>(5f, 100f)));
            BarScale = Config.Bind("Display", "BarScale", 1f,
                new ConfigDescription("Size of the bars and names.",
                    new AcceptableValueRange<float>(0.5f, 2f)));
            ShowName = Config.Bind("Display", "ShowName", true,
                "Show the enemy's name above its bar.");
            DebugLog = Config.Bind("Debug", "DebugLog", false,
                "Write bar events to the log. Doesn't change what you see.");

            HealthBarManager.EnsureExists();

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(BossBarTracker));
            }
            catch (Exception e)
            {
                Log.LogError("Could not follow the boss health bar; a boss that is not flagged as one may show two bars: " + e);
            }

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            BossBarTracker.Reset();
            HealthBarManager.Shutdown();
            BarStyle.Unload();
            SkullArt.Unload();
        }
    }

    public enum BarDisplayMode
    {
        Damaged = 1,
        Nearby = 2
    }
}
