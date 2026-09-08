using System.Collections.Generic;

using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Prestige-agnostic variation identity math. This is the single source of truth for
    /// "what layer is an object on" and "should two objects see/collide with each other",
    /// independent of Prestige, Zone Control, rifts, or any specific feature.
    ///
    /// Layer model:
    ///   - null / 0  == the retail "base" world (treated as one bucket).
    ///   - any other value == an explicit layer; two objects share visibility only when their
    ///     normalized layer values are equal.
    ///
    /// PrestigeManager still owns the prestige-specific meaning of certain variation values
    /// (tiers, allowlists, mirroring) via <see cref="PrestigeManager.IsPrestigeVariation"/>, but
    /// that ownership never gates generic visibility/collision — those go through here.
    ///
    /// Also owns the ENDGAME CONTENT floor (<see cref="EndgameMinVariation"/> /
    /// <see cref="GetEffectiveEndgameVariation"/>): "is this object on v11+ content" is a layer
    /// question, not a prestige or Zone Control one, and both of those systems previously kept
    /// their own copy of it (2026-07-30 decouple — the copies had already drifted once).
    /// </summary>
    public static class VariationManager
    {
        /// <summary>
        /// Lowest variation that counts as v11+ ENDGAME CONTENT (Zone Control's Tide layers 11-25).
        ///
        /// Intentionally unrelated to BOTH of the constants it used to be conflated with:
        ///   - <see cref="PrestigeManager.PRESTIGE_VAR_OFFSET"/> — prestige TIERING. When that moved
        ///     10 -> 1000 the endgame floor rode along and silently reported every ForceEndgameSystems
        ///     test dummy as variation 1001 (2026-07-30 review).
        ///   - <c>ZoneControlManager.MinBoundedVariation</c> — the minimum variation at which a zone may
        ///     be BOUNDED. Retuning the player-boundary rule must not move a combat gate.
        ///
        /// Every v11_*_min_variation config knob defaults to this value.
        /// </summary>
        public const int EndgameMinVariation = 11;

        /// <summary>
        /// Effective variation for the v11+ endgame systems (combat gates and Zone Control resolution):
        /// normally the object's real Location.Variation.
        ///
        /// TEST HOOK: a weenie carrying <see cref="PropertyBool.ForceEndgameSystems"/> reports an endgame
        /// variation even when spawned at a non-endgame one (0), so the endgame stack can be exercised in a
        /// normal landblock where /createinst /removeinst /reload-landblock work.
        /// <see cref="PropertyInt.EndgameForcedVariation"/> picks the simulated variation; unset — or any
        /// value below <see cref="EndgameMinVariation"/> — resolves to v11, i.e. a dummy that behaves
        /// exactly like a real Tide mob. No effect on real mobs (no flag set), and rift creatures never
        /// carry it (RiftScaling strips it at spawn).
        /// </summary>
        public static int GetEffectiveEndgameVariation(WorldObject wo)
        {
            var real = wo?.Location?.Variation ?? 0;
            if (real >= EndgameMinVariation)
                return real;

            if (wo?.GetProperty(PropertyBool.ForceEndgameSystems) == true)
            {
                var forced = wo.GetProperty(PropertyInt.EndgameForcedVariation) ?? 0;
                return forced >= EndgameMinVariation ? forced : EndgameMinVariation;
            }

            return real;
        }

        /// <summary>True for an explicit (non-base) layer. null and 0 are base.</summary>
        public static bool IsExplicitLayer(int? variation) => variation.HasValue && variation.Value != 0;

        /// <summary>Collapse the base bucket: null and 0 both become null; every other value is preserved.</summary>
        public static int? NormalizeBase(int? variation)
        {
            if (!variation.HasValue || variation.Value == 0)
                return null;
            return variation;
        }

        /// <summary>
        /// True when two variation values should share client <c>CreateObject</c> networking and
        /// physics collision. Retail treats null and 0 as one "base" bucket; every explicit layer
        /// matches only the same layer.
        /// </summary>
        public static bool SameVariationForVisibility(int? a, int? b)
        {
            return NormalizeBase(a) == NormalizeBase(b);
        }

        /// <summary>
        /// The layer an object should be judged on for visibility/collision: prefer
        /// <see cref="WorldObject.Location"/>.Variation, then the physics position variation.
        /// Non-spatial objects (inventory/escrow) inherit from the nearest spatial owner
        /// (wielder/container chain, then the online owning player).
        /// </summary>
        public static int? GetEffectiveVariationForVisibility(WorldObject wo)
        {
            if (wo == null)
                return null;

            var direct = wo.Location?.Variation ?? wo.PhysicsObj?.Position?.Variation;
            if (direct.HasValue)
                return direct;

            // Walk wielder / container chain (nested packs, etc.): wielder before container.
            var visited = new HashSet<uint> { wo.Guid.Full };
            for (var curr = wo; ; )
            {
                WorldObject next = null;
                if (curr.Wielder != null)
                    next = curr.Wielder;
                else if (curr.Container != null)
                    next = curr.Container;
                if (next == null)
                    break;
                if (!visited.Add(next.Guid.Full))
                    break;
                curr = next;
                var v = curr.Location?.Variation ?? curr.PhysicsObj?.Position?.Variation;
                if (v.HasValue)
                    return v;
            }

            // Fallback for typical inventory items: inherit from the online player owner.
            if (wo.OwnerId.HasValue)
            {
                var ownerPlayer = PlayerManager.GetOnlinePlayer(wo.OwnerId.Value);
                var viaOwnerPlayer = ownerPlayer?.Location?.Variation ?? ownerPlayer?.PhysicsObj?.Position?.Variation;
                if (viaOwnerPlayer.HasValue)
                    return viaOwnerPlayer;
            }

            return null;
        }
    }
}
