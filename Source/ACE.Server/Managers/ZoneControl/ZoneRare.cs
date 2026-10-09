using System;
using System.Collections.Generic;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>Which rare a kill produced. Ascendant outranks Pristine: one kill never yields both.</summary>
    public enum ZoneRareTier
    {
        None = 0,
        /// <summary>Max rolls for the tier it dropped in. Never grows; normal wield requirements.</summary>
        Pristine = 1,
        /// <summary>Max rolls for a tier zc_rare_ascendant_tier_bonus ABOVE the tier it dropped in (never past 25). Static: it never
        /// changes afterwards and does not depend on who holds it. Wield requirements of the tier it DROPPED in.</summary>
        Ascendant = 2,
    }

    /// <summary>
    /// T11+ RARES (owner 2026-10-08): a T11+ kill can upgrade ONE item of its own loot to a rare. This file holds
    /// the rules as pure functions - odds, the below-tier penalty, the tier an Ascendant item is made at and the tier
    /// whose wield requirements it carries - so they can be tested without a world. The roll itself and the item work
    /// are wired in from loot generation.
    ///
    /// Two tiers. PRISTINE: every roll at the maximum for the tier it dropped in. ASCENDANT (owner 2026-10-09, replacing
    /// the earlier "follows its holder" idea): every roll at the maximum for a HIGHER tier - the drop tier plus
    /// zc_rare_ascendant_tier_bonus, never past 25 - fixed at the moment it drops, wielded with the DROP tier's requirements.
    ///
    /// The odds live in ServerConfig (zc_rare_pristine_odds / zc_rare_ascendant_odds, 0 = off) and are read live.
    /// </summary>
    public static partial class ZoneRare
    {
        /// <summary>The lowest tier a rare exists at.</summary>
        public const int MinTier = TierHitGate.MinGatedVariation;

        /// <summary>The top of the ladder - an Ascendant item is never made above it.</summary>
        public const int MaxTier = 25;

        /// <summary>
        /// The tier a player is expected to farm: the highest tier they qualify for (TierHitGate.PlayerTier) that
        /// actually has an enabled zone. Without the zone test a player past the last populated tier would have
        /// nowhere to get full odds. 0 = no enabled tier at or below theirs.
        /// </summary>
        internal static int OwnTier(int playerTier, IEnumerable<int> enabledZoneTiers)
        {
            if (enabledZoneTiers == null)
                return 0;

            var best = 0;
            foreach (var tier in enabledZoneTiers)
            {
                if (tier >= MinTier && tier <= MaxTier && tier <= playerTier && tier > best)
                    best = tier;
            }
            return best;
        }

        /// <summary>
        /// Share of the rare odds a kill keeps, by how far below their own tier the killer is farming. Own tier = 1.
        /// One and two tiers below are the two live settings; three or more never rolls.
        ///
        /// A killer with no tier of their own (below T11), and a kill ABOVE the killer's own tier, never roll. The hit
        /// gate normally makes both impossible, but it can be switched off (zc_tier_hit_gate_enabled) and a tier row
        /// can be removed - neither may turn into full rare odds on monsters the player has not earned.
        /// </summary>
        internal static double BelowTierScale(int ownTier, int killTier, double oneBelow, double twoBelow)
        {
            if (ownTier < MinTier || killTier < MinTier || killTier > ownTier)
                return 0.0;

            var below = ownTier - killTier;
            if (below == 0) return 1.0;
            if (below == 1) return Clamp01(oneBelow);
            if (below == 2) return Clamp01(twoBelow);
            return 0.0;
        }

        /// <summary>Chance per kill for a tier whose setting is "1 in N", after the below-tier share. 0 or less = off.</summary>
        internal static double ChancePerKill(long oneIn, double scale)
        {
            if (oneIn <= 0 || !(scale > 0))
                return 0.0;
            return Math.Min(1.0, scale) / oneIn;   // a share can lower the odds, never raise them
        }

        /// <summary>
        /// One kill, two independent draws in [0, 1). Ascendant is tested first, so the rarer tier is never
        /// shadowed by the commoner one and a kill yields at most one rare.
        /// </summary>
        internal static ZoneRareTier Pick(double drawAscendant, double drawPristine, double chanceAscendant, double chancePristine)
        {
            if (drawAscendant < chanceAscendant) return ZoneRareTier.Ascendant;
            if (drawPristine < chancePristine) return ZoneRareTier.Pristine;
            return ZoneRareTier.None;
        }

        /// <summary>
        /// The ITEM tier of an Ascendant drop from a kill at this tier: kill tier + bonus (zc_rare_ascendant_tier_bonus),
        /// never past the top of the ladder - a T22 kill with bonus 3 gives a T25 item, and so does a T25 kill. This is
        /// the tier the item is generated and resolved at, for good; nothing about its holder enters into it.
        /// A negative bonus never lowers it (bonus 0 = the kill's own tier), and a kill tier outside 11-25 is clamped.
        /// </summary>
        internal static int AscendantTier(int killTier, long bonus)
        {
            var from = Math.Clamp(killTier, MinTier, MaxTier);
            var add = (int)Math.Clamp(bonus, 0, MaxTier - MinTier);
            return Math.Min(MaxTier, from + add);
        }

        /// <summary>
        /// The tier whose WIELD REQUIREMENTS an item carries. Every ordinary item, and a Pristine one: its own tier.
        /// An Ascendant item stores the tier it dropped in (PropertyInt.ZcRareGateTier) and that tier wins - a T14 item
        /// found in T11 is wielded with the T11 requirements. The stored tier can only LOWER the gate: a value above the
        /// item's own tier, or outside 11-25, is ignored, so a bad property can never raise or remove a requirement.
        ///
        /// Every wield-gate stamp passes through this (LootGenerationFactory.StampTierGates), which is what stops the
        /// re-stamps on equip / appraise / re-resolve from putting the item tier's gates back.
        /// </summary>
        internal static int GateTier(int? storedGateTier, int itemTier)
        {
            if (storedGateTier == null)
                return itemTier;
            var gate = storedGateTier.Value;
            if (gate < MinTier || gate > MaxTier || gate >= itemTier)
                return itemTier;
            return gate;
        }

        private static double Clamp01(double v) => double.IsNaN(v) ? 0.0 : Math.Clamp(v, 0.0, 1.0);
    }
}
