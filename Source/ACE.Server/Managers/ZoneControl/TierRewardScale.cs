using System;

using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// TIER DIP (owner 2026-10-24, locked): a T11+ kill is worth less to a player hunting BELOW their own tier, by a flat step
    /// per tier - with the 0.15 default that is 100 / 85 / 70 / 55 / 40 pct ... down to the floor. At or above their own tier
    /// a kill always counts in full, and so does any kill or player below T11 (Valley of Death, retail content and players
    /// without a tier are never touched).
    ///
    /// "Own tier" is AUG-BASED: the highest tier whose monsters the player can hit (TierHitGate.PlayerTier) narrowed to the
    /// highest tier that has an enabled zone (ZoneRare.OwnTier) - the same own tier the rare odds use.
    ///
    /// The credit scales Prestige Coins per kill and kill-task credit (and, behind zc_tier_dip_xp_enabled, kill XP and
    /// luminance). Rares keep their own ZoneRare.BelowTierScale. The rules here are pure functions so they can be tested
    /// without a world; the live wrappers read ServerConfig on every call.
    /// </summary>
    public static class TierRewardScale
    {
        /// <summary>The lowest tier the dip applies to.</summary>
        public const int MinTier = TierHitGate.MinGatedVariation;

        /// <summary>
        /// Share of a kill's reward a player keeps: max(floor, 1 - step x tiersBelow). 1.0 at or above own tier, and 1.0 when
        /// either side is below T11. A step of 0 or less, or a NaN, never cuts; the result is always in [0, 1].
        /// </summary>
        public static double Credit(int ownTier, int killTier, double step, double floor)
        {
            if (ownTier < MinTier || killTier < MinTier || killTier >= ownTier)
                return 1.0;

            if (double.IsNaN(step) || step <= 0)
                return 1.0;

            var f = double.IsNaN(floor) ? 0.0 : Math.Clamp(floor, 0.0, 1.0);
            var below = ownTier - killTier;
            return Math.Clamp(Math.Max(f, 1.0 - step * below), 0.0, 1.0);
        }

        /// <summary>The live credit: the settings as they are right now. 1.0 whenever zc_tier_dip_enabled is off.</summary>
        public static double Credit(int ownTier, int killTier)
        {
            if (!ServerConfig.zc_tier_dip_enabled.Value)
                return 1.0;

            return Credit(ownTier, killTier, ServerConfig.zc_tier_dip_step.Value, ServerConfig.zc_tier_dip_floor.Value);
        }

        /// <summary>This player's own tier for the dip: the aug-based tier narrowed to the enabled zone tiers. 0 = none.</summary>
        public static int OwnTier(Player player)
        {
            if (player == null)
                return 0;

            return ZoneRare.OwnTier(TierHitGate.PlayerTier(player), ZoneControlManager.EnabledZoneTiers);
        }

        /// <summary>The live credit for this player on a kill at <paramref name="killTier"/>.</summary>
        public static double CreditFor(Player player, int killTier) => Credit(OwnTier(player), killTier);

        /// <summary>
        /// Kill XP / luminance through the tier dip - the per-recipient multiplier slot (owner 2026-10-24: built, OFF by default).
        /// Unchanged unless zc_tier_dip_xp_enabled is on and <paramref name="killTier"/> is a T11+ tier; call it on
        /// <paramref name="player"/>'s own thread (it reads their augmentation counters).
        /// </summary>
        public static long ScaleXp(long amount, Player player, int killTier)
        {
            if (!ServerConfig.zc_tier_dip_xp_enabled.Value || amount <= 0 || player == null || killTier < MinTier || killTier > ZoneRare.MaxTier)
                return amount;

            return (long)Math.Round(amount * CreditFor(player, killTier));
        }

        /// <summary>
        /// The tier a kill counts at: the creature's effective variation when Zone Control governs it and that is T11 or
        /// higher, else 0. 0 means "not a T11+ zone kill" - every tier reward leaves such kills exactly as they were.
        /// </summary>
        public static int KillTier(Creature creature)
        {
            if (creature == null || creature is Player)
                return 0;

            if (ZoneControlManager.ResolveForCreature(creature) == null)
                return 0;

            var tier = ZoneControlManager.GetEffectiveVariation(creature);
            return tier >= MinTier && tier <= ZoneRare.MaxTier ? tier : 0;
        }

        // ---- Prestige Coins per kill ------------------------------------------------------------------------------------

        /// <summary>
        /// Coins one kill at this tier is worth before the dip: <paramref name="baseValue"/> at T11, x <paramref name="tierStep"/>
        /// for each tier above it. 0 below T11, past T25, and for a base of 0 or less (off). A step of 0 or less counts as 1.
        /// </summary>
        public static double ZoneValue(int killTier, double baseValue, double tierStep)
        {
            if (killTier < MinTier || killTier > ZoneRare.MaxTier)
                return 0.0;
            if (double.IsNaN(baseValue) || baseValue <= 0)
                return 0.0;

            var step = double.IsNaN(tierStep) || tierStep <= 0 ? 1.0 : tierStep;
            var value = baseValue * Math.Pow(step, killTier - MinTier);
            return double.IsFinite(value) ? value : 0.0;
        }

        /// <summary>The live zone value (zc_pcoin_kill_base / zc_pcoin_kill_tier_step).</summary>
        public static double ZoneValue(int killTier)
            => ZoneValue(killTier, ServerConfig.zc_pcoin_kill_base.Value, ServerConfig.zc_pcoin_kill_tier_step.Value);

        /// <summary>
        /// Adds <paramref name="earned"/> coins to a carried fraction: the whole coins to bank, and the fraction left over
        /// (always in [0, 1)). A bad carried value (NaN, negative, 1 or more from a hand edit) is treated as 0 rather than
        /// minting coins from it.
        /// </summary>
        public static (long Whole, double Fraction) AddCoins(double carried, double earned)
        {
            var c = double.IsFinite(carried) && carried >= 0 && carried < 1 ? carried : 0.0;
            var e = double.IsFinite(earned) && earned > 0 ? earned : 0.0;

            var total = c + e;
            var whole = Math.Floor(total);
            var frac = total - whole;
            if (!(frac >= 0))
                frac = 0.0;
            else if (frac >= 1 - Epsilon)
            {
                // a hair under the next whole coin (20 x 0.05 in floating point) is that coin
                whole += 1;
                frac = 0.0;
            }
            return ((long)whole, frac);
        }

        // ---- kill-task credit (T11+ Zone Control kills only) ------------------------------------------------------------

        /// <summary>
        /// One recipient's kill-task credit for one kill, before it is added to their progress. A tagger (did damage, pet
        /// damage counts for the owner) gets a full kill; a non-damaging sharer gets the fellowship share for
        /// <paramref name="groupSize"/> (everyone in range, taggers included) - except on a one-solve task (MaxSolves 1),
        /// where every sharer gets full credit. Everyone's credit is then x their own tier credit.
        /// </summary>
        public static double KillTaskCredit(bool tagger, int groupSize, bool singleSolve, double tierCredit, Func<int, double> sharePercent)
        {
            var share = tagger || singleSolve ? 1.0 : sharePercent(Math.Max(1, groupSize));
            if (double.IsNaN(share)) share = 0.0;
            var t = double.IsNaN(tierCredit) ? 0.0 : Math.Clamp(tierCredit, 0.0, 1.0);
            return Math.Clamp(share, 0.0, 1.0) * t;
        }

        /// <summary>
        /// Decimal kill-task progress: <paramref name="count"/> whole kills + <paramref name="fraction"/> carried, plus
        /// <paramref name="credit"/>, capped at <paramref name="maxSolves"/> (overflow dropped; a cap of 0 or less = no cap).
        /// Returns the new whole count and fraction; at the cap the fraction is always 0.
        /// </summary>
        public static (int Count, double Fraction) AddKillTaskProgress(int count, double fraction, double credit, int maxSolves)
        {
            var f = double.IsFinite(fraction) && fraction > 0 && fraction < 1 ? fraction : 0.0;
            var c = double.IsFinite(credit) && credit > 0 ? credit : 0.0;

            var total = Math.Max(0, count) + f + c;
            if (maxSolves > 0 && total >= maxSolves - Epsilon)
                return (Math.Max(count, maxSolves), 0.0);

            // a hair under the next whole kill (0.85 + 0.15 in floating point) counts as that kill
            var whole = Math.Floor(total + Epsilon);
            var frac = total - whole;
            if (frac < Epsilon) frac = 0.0;
            return ((int)whole, frac);
        }

        /// <summary>Rounding slack for decimal kill-task progress: far below anything a share table or tier step produces.</summary>
        internal const double Epsilon = 1e-6;

        /// <summary>Progress as shown to players: whole numbers stay whole, decimals keep up to two places ("4.5", "4.25").</summary>
        public static string FormatProgress(double value)
        {
            if (!double.IsFinite(value)) value = 0;
            var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            return rounded.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
