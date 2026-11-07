using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Units;
using PerfectRandom.Sulfur.Gameplay;
using UnityEngine;

namespace EnemyHealthBars
{
    /// <summary>
    /// What this mod remembers about one enemy. It is a cache of what the game reports and owns
    /// nothing: the game remains the only authority on the unit.
    /// </summary>
    internal sealed class TrackedEnemy
    {
        public readonly Npc Npc;
        public readonly int Id;
        public readonly HellShrewSnipa Sniper;
        public readonly bool IsGhost;
        public readonly CapsuleCollider Collider;

        public int SeenInScan;
        public float LastHealth;
        public float Distance;
        public float LastHurtTime = float.NegativeInfinity;

        public bool InSight;
        public float NextSightCheck;

        public float Alpha;
        public float Trail;
        public string Name;
        public HealthBarView View;

        /// <summary>How far above the head the bar floats.</summary>
        public const float GapAboveHead = 0.2f;

        public TrackedEnemy(Npc npc)
        {
            Npc = npc;
            Id = npc.GetInstanceID();
            Sniper = npc.GetComponent<HellShrewSnipa>();
            IsGhost = npc.GetComponent<GhostHelper>() != null;
            Collider = npc.GetComponent<CapsuleCollider>();
            LastHealth = npc.normalizedHealth;
            Trail = Mathf.Clamp01(LastHealth);
        }

        /// <summary>
        /// Where the bar sits. The authored overhead point comes first. The collider is only a
        /// fallback and only while enabled: a disabled collider reports empty bounds at the origin,
        /// and co-op switches colliders off on some mirrored units.
        /// </summary>
        public Vector3 Anchor()
        {
            const float heightWithoutReference = 2f;

            if (Npc.overhead != null) return Npc.overhead.position + Vector3.up * GapAboveHead;

            if (Collider != null && Collider.enabled)
            {
                Bounds bounds = Collider.bounds;
                return bounds.center + Vector3.up * (bounds.extents.y + GapAboveHead);
            }

            return Npc.transform.position + Vector3.up * heightWithoutReference;
        }
    }
}
