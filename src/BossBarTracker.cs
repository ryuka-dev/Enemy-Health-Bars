using System.Collections.Generic;
using HarmonyLib;
using PerfectRandom.Sulfur.Core.Units;
using PerfectRandom.Sulfur.Core.UI;

namespace EnemyHealthBars
{
    /// <summary>
    /// Follows which units the game's own boss bar is showing. The boss flag on a unit is not enough:
    /// the bar can be attached to a unit that does not carry it, such as one section of a segmented
    /// boss, and that unit must not get a second bar from this mod.
    /// </summary>
    [HarmonyPatch(typeof(BossHealth))]
    internal static class BossBarTracker
    {
        private static readonly Dictionary<BossHealth, Unit> Attached = new Dictionary<BossHealth, Unit>();

        public static bool Shows(Unit unit)
        {
            foreach (KeyValuePair<BossHealth, Unit> entry in Attached)
            {
                if (entry.Value == unit) return true;
            }

            return false;
        }

        public static void Reset()
        {
            Attached.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BossHealth.Attach))]
        private static void AfterAttach(BossHealth __instance, Unit unit)
        {
            Attached[__instance] = unit;
        }

        // Called with no unit by level teardown, which means "whatever you are tracking".
        [HarmonyPostfix]
        [HarmonyPatch(nameof(BossHealth.Detach))]
        private static void AfterDetach(BossHealth __instance, Unit unit)
        {
            Unit tracked;
            if (!Attached.TryGetValue(__instance, out tracked)) return;
            if (unit == null || unit == tracked) Attached.Remove(__instance);
        }
    }
}
