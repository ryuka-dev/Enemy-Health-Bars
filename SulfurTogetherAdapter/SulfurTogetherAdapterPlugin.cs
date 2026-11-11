using System;
using System.Runtime.CompilerServices;
using BepInEx;

namespace EnemyHealthBars.SulfurTogether
{
    /// <summary>
    /// Connects Enemy Health Bars to SULFUR Together's team marks. Both dependencies are hard: with either
    /// plugin absent, BepInEx skips this one and the bars play on their own.
    ///
    /// A matching GUID does not guarantee the API, because an older SULFUR Together has the GUID but not
    /// the team marks. The first contact with it therefore happens inside <see cref="Attach"/>, a guarded
    /// non-inlined call, and a missing API leaves one log line instead of a type-load failure.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, Plugin.PluginVersion)]
    [BepInDependency(Plugin.PluginGuid)]
    [BepInDependency(SulfurTogetherGuid)]
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
            try
            {
                int apiVersion;
                _bridge = Attach(out apiVersion);
                Logger.LogInfo(_bridge != null
                    ? "Showing SULFUR Together's team marks on the bars (combat API " + apiVersion + ")."
                    : "The installed SULFUR Together has combat API " + apiVersion + ", without team marks; " + NoMarks);
            }
            catch (Exception e) when (
                e is TypeLoadException || e is MissingMethodException || e is MissingFieldException ||
                e is TypeInitializationException || e is BadImageFormatException)
            {
                Logger.LogInfo("The installed SULFUR Together has no combat API; " + NoMarks +
                               " (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        private void OnDestroy()
        {
            if (_bridge == null) return;

            _bridge.Dispose();
            _bridge = null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDisposable Attach(out int apiVersion)
        {
            return TeamMarkBridge.TryCreate(out apiVersion);
        }
    }
}
