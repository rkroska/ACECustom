using System;
using System.Collections.Generic;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Result of attempting to apply an ability charm bonus.
    /// Used by all ability charm types (e.g. Heavy Swing, Focused Casting).
    /// </summary>
    public struct AbilityCharmResult
    {
        public bool Applied;            // true = bonus was applied this hit
        public uint StaminaSpent;
        public uint ManaSpent;
        public float DamageMultiplier;  // 1.0 = no bonus applied
    }

    public partial class Player
    {
        // ── Generic charm scaling helpers ─────────────────────────────────────────
        // Used by all tiered ability charms to scale damage multiplier and cost.

        /// <summary>
        /// Returns a damage multiplier scaled by the player's active charm tier.
        /// Base (level 1): baseMultiplier unchanged.
        /// Each additional level: +0.5x bonus (e.g. 2.0 → 2.5 → 3.0).
        /// </summary>
        public float GetCharmDamageMultiplier(float baseMultiplier)
        {
            var level = ActiveCharmLevel ?? 1;
            if (level <= 1) return baseMultiplier;
            return baseMultiplier + (level - 1) * 0.5f;
        }

        /// <summary>
        /// Returns a vital cost percentage scaled by the player's active charm tier.
        /// Base (level 1): baseCost unchanged.
        /// Each additional level: –2% (floored at 2%).
        /// </summary>
        public float GetCharmCostPct(float baseCost)
        {
            var level = ActiveCharmLevel ?? 1;
            if (level <= 1) return baseCost;
            return Math.Max(0.02f, baseCost - (level - 1) * 0.02f);
        }

        /// <summary>
        /// On login: ensures no ability is active without a corresponding activated charm in inventory.
        /// Silently clears orphaned abilities (e.g., charm lost or removed while offline).
        /// </summary>
        public void ValidateAbilityCharms()
        {
            var activeCharmAbilities = new HashSet<int>();
            foreach (var item in GetAllPossessions())
            {
                if (item.IsAbilityCharm && item.IsCharmActivated && item.CharmGrantsAbility.HasValue)
                    activeCharmAbilities.Add(item.CharmGrantsAbility.Value);
            }

            foreach (var id in CharmAbilityRegistry.RegisteredIds)
            {
                if (CharmAbilityRegistry.IsActive(this, id) && !activeCharmAbilities.Contains(id))
                    CharmAbilityRegistry.Apply(this, id, false);
            }
        }
    }
}
