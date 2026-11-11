using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;

namespace EnemyHealthBars.SulfurTogether
{
    /// <summary>
    /// Connects Enemy Health Bars to SULFUR Together's team marks.
    ///
    /// The dependency on SULFUR Together is soft, so that a player without it gets no load error, only one info
    /// line: the soft dependency still makes BepInEx load SULFUR Together first when it is there, and Awake checks
    /// that it is before anything touches its assembly. A matching GUID does not guarantee the API either, because
    /// an older SULFUR Together has the GUID but not the team marks. Every first contact with it happens inside a
    /// guarded non-inlined call, and a missing API leaves one log line instead of a type-load failure.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, Plugin.PluginVersion)]
    [BepInDependency(Plugin.PluginGuid)]
    [BepInDependency(SulfurTogetherGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SulfurTogetherAdapterPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ryuka.sulfur.enemy_health_bars_sulfur_together";
        public const string PluginName = "Enemy Health Bars - SULFUR Together Adapter";

        private const string SulfurTogetherGuid = "com.ryuka.sulfur.together";
        private const string NoMarks = "the bars show no team marks, and SULFUR Together keeps its own icons.";

        // IDisposable on purpose: no field in this assembly may name a SULFUR Together type.
        private IDisposable _bridge;

        private void Awake()
        {
            if (!Chainloader.PluginInfos.ContainsKey(SulfurTogetherGuid))
            {
                Logger.LogInfo("SULFUR Together is not installed; team marks are off.");
                return;
            }

            int apiVersion;
            try
            {
                apiVersion = ReadApiVersion();
            }
            catch (Exception e) when (IsMissingApi(e))
            {
                Logger.LogInfo("The installed SULFUR Together has no combat API; " + NoMarks +
                               " (" + e.GetType().Name + ": " + e.Message + ")");
                return;
            }

            if (apiVersion < TeamMarkBridge.TeamMarksApiVersion)
            {
                Logger.LogInfo("The installed SULFUR Together has combat API " + apiVersion + ", without team marks; " + NoMarks);
                return;
            }

            try
            {
                _bridge = Attach();
                Logger.LogInfo("Showing SULFUR Together's team marks on the bars (combat API " + apiVersion + ").");
            }
            catch (Exception e) when (IsMissingApi(e))
            {
                // The version says the team marks are there, but not in the form this build was made against.
                Logger.LogWarning("The installed SULFUR Together reports combat API " + apiVersion +
                                  ", but its team marks are not what this version expects; " + NoMarks +
                                  " (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        private void OnDestroy()
        {
            if (_bridge == null) return;

            _bridge.Dispose();
            _bridge = null;
        }

        private static bool IsMissingApi(Exception e)
        {
            return e is TypeLoadException || e is MissingMethodException || e is MissingFieldException ||
                   e is TypeInitializationException || e is BadImageFormatException;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadApiVersion()
        {
            return TeamMarkBridge.ReadApiVersion();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDisposable Attach()
        {
            return TeamMarkBridge.Create();
        }
    }
}
