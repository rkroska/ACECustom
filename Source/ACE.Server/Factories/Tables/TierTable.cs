using System;
using System.Collections.Generic;

namespace ACE.Server.Factories.Tables
{
    /// <summary>
    /// Tier-indexed table access with clamp-to-last-authored-tier semantics.
    ///
    /// Loot tables are authored per tier (index = tier - 1). Content tiers above the last
    /// authored entry (e.g. tier 11+ zones while tables are only authored through tier 10)
    /// clamp to the highest authored entry instead of throwing IndexOutOfRange, so a
    /// treasure_death profile may use ANY tier >= 1 and behaves as the best authored data.
    /// When a table later gains a real entry for a higher tier, that entry takes effect
    /// automatically — each table grows independently, no global max-tier constant.
    /// </summary>
    public static class TierTable
    {
        /// <summary>
        /// Returns the entry for a 1-based tier, clamped to [1, table.Count].
        /// </summary>
        public static T Entry<T>(IReadOnlyList<T> table, int tier)
        {
            return table[Math.Clamp(tier, 1, table.Count) - 1];
        }
    }
}
