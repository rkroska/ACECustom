using System;

using ACE.Server.Managers.ZoneScaling;
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
    /// The credit scales Prestige Coins per kill, kill-task credit and bounty kills (and, behind zc_tier_dip_xp_enabled, kill XP and
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

        // ---- Prestige Coin falloff (2026-11-01) --------------------------------------------------------------------------

        /// <summary>
        /// The falloff numbers in force for one kill, already held to their ranges: <see cref="KillsPerHour"/> the full-rate pace
        /// (0 = no falloff), <see cref="Mult"/> the share of its coins a kill past the pace pays (0..1), <see cref="Reserve"/> how many
        /// kills ahead of the pace a character can get before the falloff starts (at least 1), counted at this pace.
        /// </summary>
        public readonly record struct FalloffDials(double KillsPerHour, double Mult, double Reserve)
        {
            /// <summary>The reserve as time: how many seconds of kills at this pace it holds (0 when the falloff is off).
            /// 30 kills at 375 an hour = 288 seconds. FalloffFor keeps this the same in every zone.</summary>
            public double WindowSeconds => KillsPerHour > 0 ? Reserve * 3600.0 / KillsPerHour : 0.0;
        }

        /// <summary>
        /// Holds the falloff numbers to their ranges - a server setting and a zone stat alike, since neither is checked when it is
        /// set: pace 0..1,000,000 kills an hour, share 0..1, reserve 1..100,000. A value that is not a number takes the setting's
        /// declared default (PropertyManager).
        /// </summary>
        public static FalloffDials CleanFalloff(double killsPerHour, double mult, double reserve) => new(
            Clean(killsPerHour, 0.0, 1_000_000.0, ServerConfig.zc_pcoin_falloff_kills_per_hour.Default),
            Clean(mult, 0.0, 1.0, ServerConfig.zc_pcoin_falloff_mult.Default),
            Clean(reserve, 1.0, 100_000.0, ServerConfig.zc_pcoin_falloff_reserve.Default));

        private static double Clean(double value, double min, double max, double fallback)
            => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

        /// <summary>The server settings alone (zc_pcoin_falloff_*), cleaned - what every kill uses until a zone authors a stat.</summary>
        public static FalloffDials ServerFalloff() => CleanFalloff(
            ServerConfig.zc_pcoin_falloff_kills_per_hour.Value,
            ServerConfig.zc_pcoin_falloff_mult.Value,
            ServerConfig.zc_pcoin_falloff_reserve.Value);

        /// <summary>
        /// The falloff for a kill governed by <paramref name="zoneDefault"/> (the zone's Default row - its own stats over the tier
        /// Default; ZoneControlManager.ResolveZoneDefaultRowForCreature). Per stat: pcoin_falloff_kills_per_hour /
        /// pcoin_falloff_mult when the row has it, else the server setting; an authored value that is not a number keeps the
        /// server value. Null row = the server settings.
        ///
        /// The reserve is a server setting only, and a zone with its own pace gets the same reserve in TIME, not in kills: 30 kills
        /// at the server's 375 an hour is 288 seconds, so a zone authored at 500 an hour holds 40 kills. That keeps the saved
        /// count (in seconds - FalloffOnDebt) meaning the same thing in every zone.
        /// </summary>
        public static FalloffDials FalloffFor(EvaluatedProfile zoneDefault)
        {
            var server = ServerFalloff();
            if (zoneDefault == null)
                return server;

            var zone = CleanFalloff(
                Pick(zoneDefault, ZoneStat.PcoinFalloffKillsPerHour, server.KillsPerHour),
                Pick(zoneDefault, ZoneStat.PcoinFalloffMult, server.Mult),
                server.Reserve);

            if (zone.KillsPerHour > 0 && server.KillsPerHour > 0 && zone.KillsPerHour != server.KillsPerHour)
            {
                var scaled = server.Reserve * zone.KillsPerHour / server.KillsPerHour;
                if (double.IsFinite(scaled))
                    zone = zone with { Reserve = Math.Max(1.0, scaled) };
            }

            return zone;
        }

        private static double Pick(EvaluatedProfile profile, string stat, double server)
        {
            if (!profile.Has(stat))
                return server;

            var value = profile.Get(stat, server);
            return double.IsFinite(value) ? value : server;
        }

        /// <summary>
        /// One kill through the Prestige Coin falloff (pure). <paramref name="recent"/> is the character's count of recent
        /// full-value kills as saved after their last coin kill, <paramref name="elapsedSeconds"/> the time since then. The count
        /// first drains at <paramref name="killsPerHour"/>; the part of this kill (worth <paramref name="credit"/> kills - its
        /// tier-dip credit) that still fits under <paramref name="reserve"/> pays in full, the rest pays <paramref name="mult"/>.
        /// So a steady pace at or under killsPerHour always pays in full, and past it every extra kill pays mult: 500 an hour
        /// against 375 pays 375 + 125 x 0.5 = 437.5 kills' worth.
        ///
        /// Returns the credit to pay coins on, the count to save (0 .. reserve) and whether this kill was cut. A pace of 0 or less
        /// (or not a number) is OFF: full pay and a count of 0, so nothing is kept. A negative or unknown elapsed time (a clock that
        /// went back) drains nothing; a saved count above a lowered reserve is held to it.
        /// </summary>
        public static (double Paid, double Recent, bool Reduced) Falloff(double recent, double elapsedSeconds, double credit,
            double killsPerHour, double mult, double reserve)
        {
            var c = double.IsFinite(credit) && credit > 0 ? credit : 0.0;
            if (!(killsPerHour > 0) || !double.IsFinite(killsPerHour))
                return (c, 0.0, false);

            var m = double.IsNaN(mult) ? 1.0 : Math.Clamp(mult, 0.0, 1.0);
            var cap = double.IsFinite(reserve) ? Math.Max(1.0, reserve) : 1.0;
            var r = double.IsFinite(recent) && recent > 0 ? Math.Min(recent, cap) : 0.0;
            var dt = double.IsFinite(elapsedSeconds) && elapsedSeconds > 0 ? elapsedSeconds : 0.0;

            r = Math.Max(0.0, r - dt * killsPerHour / 3600.0);

            var full = Math.Min(c, cap - r);
            var paid = full + (c - full) * m;
            var reduced = m < 1.0 && full < c - Epsilon;
            return (paid, Math.Min(cap, r + c), reduced);
        }

        /// <summary>
        /// One kill through the falloff on the count as it is SAVED on the character: <paramref name="debtSeconds"/> is how far
        /// ahead of the pace they are, in seconds, not kills. A kill at a pace of P adds 3600 / P seconds (x its credit), the count
        /// drains one second a second, and it holds at most <see cref="FalloffDials.WindowSeconds"/> (288 at the defaults, the same
        /// in every zone). At one pace this is exactly <see cref="Falloff"/>. Across zones it means a kill in a zone authored at a
        /// high pace adds little but never drains a count built up elsewhere faster than the clock.
        ///
        /// A kill where the falloff is OFF pays in full and returns the count unchanged (not drained, not emptied) - the caller
        /// leaves the saved values alone, so a kill in a no-falloff zone cannot reset the reserve.
        /// Returns the credit to pay coins on, the seconds to save and whether this kill was cut.
        /// </summary>
        public static (double Paid, double Debt, bool Reduced) FalloffOnDebt(double debtSeconds, double elapsedSeconds, double credit,
            FalloffDials dials)
        {
            var pace = dials.KillsPerHour;
            var debt = double.IsFinite(debtSeconds) && debtSeconds > 0 ? debtSeconds : 0.0;

            if (!(pace > 0) || !double.IsFinite(pace))
                return (double.IsFinite(credit) && credit > 0 ? credit : 0.0, debt, false);

            var (paid, recent, reduced) = Falloff(debt * pace / 3600.0, elapsedSeconds, credit, pace, dials.Mult, dials.Reserve);
            return (paid, recent * 3600.0 / pace, reduced);
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

        // ---- bounty credit (owner 2026-10-28: the same tier dip as kill tasks) -----------------------------------------

        /// <summary>
        /// Decimal bounty progress: <paramref name="kills"/> so far plus one kill's <paramref name="credit"/> toward a
        /// <paramref name="target"/>-kill bounty. Reaching the target completes it and starts the next count at 0 (overflow
        /// dropped, like kill tasks). A hair under the target counts as reaching it.
        /// </summary>
        public static (double Kills, bool Completed) AddBountyProgress(double kills, double credit, int target)
        {
            var k = double.IsFinite(kills) && kills > 0 ? kills : 0.0;
            var c = double.IsFinite(credit) && credit > 0 ? credit : 0.0;

            var total = k + c;
            if (total >= Math.Max(1, target) - Epsilon)
                return (0.0, true);

            return (total, false);
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
