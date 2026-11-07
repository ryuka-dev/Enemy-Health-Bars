using System;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Units;
using PerfectRandom.Sulfur.Gameplay;
using UnityEngine;

namespace EnemyHealthBars
{
    /// <summary>
    /// Decides which units may carry a bar. Everything here only reads game state.
    /// </summary>
    internal static class EnemyRules
    {
        // Ghosts vanish by fading their material. No flag or status says so, and the only record of
        // it is this private pair, so it is read directly rather than guessed from the unit type.
        private static readonly AccessTools.FieldRef<Npc, float> CurrentOpacity = ResolveOpacity("unitOpacity");
        private static readonly AccessTools.FieldRef<Npc, float> TargetOpacity = ResolveOpacity("unitTargetOpacity");

        private const float HiddenBelowOpacity = 0.5f;

        private static AccessTools.FieldRef<Npc, float> ResolveOpacity(string field)
        {
            try
            {
                return AccessTools.FieldRefAccess<Npc, float>(field);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Npc." + field + " was not found; ghosts will never show a bar: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Identity checks that do not change while the unit lives. Also the guard against a unit that
        /// has not finished setting up: these members read data the game assigns while spawning.
        /// </summary>
        public static bool CanEverShow(Npc npc)
        {
            return npc.unitSO != null
                   && npc.Stats != null
                   && !npc.IsProtectedNpc
                   && !npc.IsCivilian
                   && !npc.IsBoss;
        }

        /// <summary>
        /// Everything that can change from one moment to the next. Runs every frame for a unit that
        /// is showing a bar, so an enemy that hides again loses it at once.
        /// </summary>
        public static bool CanShowNow(TrackedEnemy enemy, Unit player)
        {
            Npc npc = enemy.Npc;

            // A co-op client plays a mirrored death without the unit ever leaving the alive state,
            // so the state alone would leave an empty bar over the corpse.
            if (!npc.gameObject.activeInHierarchy || !npc.IsAlive || npc.GetCurrentHealth() <= 0f) return false;

            if (!npc.IsHostileTo(player)) return false;

            // A charm applied through a mirrored status does not carry the faction override, so the
            // faction test alone still reads such a unit as hostile.
            if (npc.IsCharmed || npc.forcedCharmingOwner != null || npc.isFoulAlly) return false;

            return !IsHidden(enemy)
                   && npc.unitFrame == null
                   && !BossBarTracker.Shows(npc);
        }

        private static bool IsHidden(TrackedEnemy enemy)
        {
            Npc npc = enemy.Npc;

            // Targetable is the game's own switch for "enemies cannot see this right now": burrowed
            // diggers and invulnerable worm sections set it without touching any hidden state.
            if (npc.isInvisible || npc.HiddenMode || !npc.Targetable) return true;
            if (enemy.Sniper != null && enemy.Sniper.IsHidden) return true;

            // Pike riders stay alive and in the enemy list with only their model switched off.
            if (npc.mainRenderer != null && !npc.mainRenderer.gameObject.activeInHierarchy) return true;

            if (CurrentOpacity == null || TargetOpacity == null) return enemy.IsGhost;

            return CurrentOpacity(npc) < HiddenBelowOpacity || TargetOpacity(npc) < HiddenBelowOpacity;
        }
    }
}
