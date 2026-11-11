using System;

namespace EnemyHealthBars
{
    /// <summary>
    /// The surface an optional integration plugin uses to put team marks on the bars. The dependency
    /// points one way: an integration references this assembly, and this assembly names no type of
    /// any other mod. Without an integration everything here is inert and no bar shows a mark.
    ///
    /// A team mark says how many other players have marked an enemy (0 to 3). The integration owns
    /// that number; the bars only show it. All members are main thread only.
    /// </summary>
    public static class HealthBarIntegration
    {
        /// <summary>The most marks a bar can show.</summary>
        public const int MaxMarks = 3;

        private static Func<object, int> _markCount;
        private static bool _marksChanged;

        /// <summary>
        /// Sets the read for "how many other players have their mark on this unit" (a game Unit in, a
        /// count out). Pass null to clear it, which takes every mark off the bars. The read is called for
        /// the enemies that have a bar, when a bar appears, after <see cref="NotifyMarksChanged"/> and
        /// twice a second. A read that throws is dropped for good, with one log line, and the bars go on
        /// without marks.
        /// </summary>
        public static void SetMarkSource(Func<object, int> markCount)
        {
            _markCount = markCount;
            _marksChanged = true;
        }

        /// <summary>The marks changed somewhere: the bars read them again on their next update.</summary>
        public static void NotifyMarksChanged()
        {
            _marksChanged = true;
        }

        /// <summary>
        /// True while this mod shows the marks of <paramref name="unit"/> (a game Unit) on screen: its bar
        /// drew a mark in this frame or the last and is not fading. Cheap enough to call every frame for
        /// every marked unit. False for anything else, including after the mod shut down.
        /// </summary>
        public static bool IsShowingMarks(object unit)
        {
            return HealthBarManager.IsShowingMarks(unit);
        }

        internal static bool HasMarkSource => _markCount != null;

        /// <summary>Returns whether the marks changed since the last call, and clears that.</summary>
        internal static bool TakeMarksChanged()
        {
            bool changed = _marksChanged;
            _marksChanged = false;
            return changed;
        }

        internal static int ReadMarks(object unit)
        {
            Func<object, int> read = _markCount;
            if (read == null) return 0;

            try
            {
                int count = read(unit);
                return count < 0 ? 0 : count > MaxMarks ? MaxMarks : count;
            }
            catch (Exception e)
            {
                // A broken integration costs the marks, never the bars: the update loop that would
                // otherwise catch this hides every bar.
                if (ReferenceEquals(_markCount, read))
                {
                    _markCount = null;
                    _marksChanged = true;
                    Plugin.Log.LogError("Team marks are off for this session: reading them failed. " + e);
                }

                return 0;
            }
        }
    }
}
