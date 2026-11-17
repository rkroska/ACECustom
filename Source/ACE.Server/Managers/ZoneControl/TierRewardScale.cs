using System;
using System.Collections.Generic;

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

        // ---- Prestige Coin falloff (2026-11-01; steps per coin hour 2026-11-17) ---------------------------------------

        /// <summary>Length of a coin hour in seconds: the window a character's coin kills are counted in. It starts at their
        /// first coin kill and the next one after it ends starts a new one.</summary>
        public const double CoinHourSeconds = 3600.0;

        /// <summary>The most reduced steps a schedule is built with; past them the last step's share runs on to the cap (or to
        /// the end of the hour when there is no cap). At the shipped half per step, the 100th step pays 0.5^100 - nothing.</summary>
        internal const int MaxFalloffSteps = 100;

        /// <summary>
        /// The falloff schedule for one kill, already held to its ranges (<see cref="CleanFalloff"/>). Of the kills a character
        /// makes in one coin hour, the first <see cref="KillsPerHour"/> pay in full; then every <see cref="StepKills"/> kills the
        /// share falls to <see cref="Mult"/> of the step before (first step Mult, then Mult^2, ...), and nothing is paid past
        /// <see cref="CapKillsPerHour"/>. The defaults (375 / 0.5 / 75 / 750) halve the pay every 75 kills past 375: kills 1-375
        /// of the hour pay in full, 376-450 half, 451-525 a quarter, 526-600 an eighth, 601-675 a sixteenth, 676-750 a
        /// thirty-second and 751 on nothing, so an hour of 750 kills or more pays 447.65625 kills' worth.
        ///
        /// KillsPerHour 0 turns the falloff off. StepKills 0 makes every kill past KillsPerHour pay Mult up to the cap.
        /// CapKillsPerHour 0 means no stop (the steps run on, see MaxFalloffSteps); a cap at or under KillsPerHour stops all pay
        /// at the cap. <see cref="Scale"/> stretches every limit for one zone (FalloffFor); the hour's count is kept in server
        /// kills, so a kill there uses 1 / Scale of one.
        /// </summary>
        public readonly record struct FalloffDials(double KillsPerHour, double Mult, double StepKills, double CapKillsPerHour,
            double Scale = 1.0)
        {
            /// <summary>True when kills under these dials are counted and can pay less than full.</summary>
            public bool On => KillsPerHour > 0 && Scale > 0 && double.IsFinite(Scale);
        }

        /// <summary>
        /// Holds the falloff numbers to their ranges - server settings and zone stats alike, since neither is checked when it is
        /// set: every limit and the step 0..1,000,000 kills, the share 0..1. A value that is not a number takes the setting's
        /// declared default (PropertyManager).
        /// </summary>
        public static FalloffDials CleanFalloff(double killsPerHour, double mult, double stepKills, double capKillsPerHour) => new(
            Clean(killsPerHour, 0.0, MaxFalloffLimit, ServerConfig.zc_pcoin_falloff_kills_per_hour.Default),
            Clean(mult, 0.0, 1.0, ServerConfig.zc_pcoin_falloff_mult.Default),
            Clean(stepKills, 0.0, MaxFalloffLimit, ServerConfig.zc_pcoin_falloff_step_kills.Default),
            Clean(capKillsPerHour, 0.0, MaxFalloffLimit, ServerConfig.zc_pcoin_falloff_cap_kills_per_hour.Default));

        /// <summary>The highest any falloff limit or step can be set, in kills.</summary>
        private const double MaxFalloffLimit = 1_000_000.0;

        /// <summary>One dial held to [<paramref name="min"/>, <paramref name="max"/>]; NaN or infinity takes <paramref name="fallback"/>.</summary>
        private static double Clean(double value, double min, double max, double fallback)
            => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

        /// <summary>The server settings alone (zc_pcoin_falloff_*), cleaned - what every kill uses until a zone authors a stat.</summary>
        public static FalloffDials ServerFalloff() => CleanFalloff(
            ServerConfig.zc_pcoin_falloff_kills_per_hour.Value,
            ServerConfig.zc_pcoin_falloff_mult.Value,
            ServerConfig.zc_pcoin_falloff_step_kills.Value,
            ServerConfig.zc_pcoin_falloff_cap_kills_per_hour.Value);

        /// <summary>
        /// The falloff for a kill governed by <paramref name="zoneDefault"/> (the zone's Default row - its own stats over the tier
        /// Default; ZoneControlManager.ResolveZoneDefaultRowForCreature). Null row = the server settings.
        ///
        /// pcoin_falloff_kills_per_hour moves the full-rate limit for the zone and stretches the step and the cap with it (a zone at
        /// 500 against the server's 375 has 1.333 x each: full to 500, steps of 100, cap 1000); 0 turns the falloff off there.
        /// pcoin_falloff_mult replaces zc_pcoin_falloff_mult, the share each step keeps. A stat the row does not author, or authors
        /// as something that is not a number, keeps the server value. With the server's full-rate limit at 0 (falloff off except
        /// where a zone turns it on) the zone's limit is read against the setting's declared default (375).
        /// </summary>
        public static FalloffDials FalloffFor(EvaluatedProfile zoneDefault) => FalloffFor(zoneDefault, ServerFalloff());

        /// <summary>The same as FalloffFor(zoneDefault), against given server dials (already cleaned) - for tests.</summary>
        internal static FalloffDials FalloffFor(EvaluatedProfile zoneDefault, FalloffDials server)
        {
            if (zoneDefault == null)
                return server;

            var zonePace = Math.Clamp(Pick(zoneDefault, ZoneStat.PcoinFalloffKillsPerHour, server.KillsPerHour), 0.0, MaxFalloffLimit);
            var zoneMult = Math.Clamp(Pick(zoneDefault, ZoneStat.PcoinFalloffMult, server.Mult), 0.0, 1.0);

            if (!(zonePace > 0))
                return server with { KillsPerHour = 0.0, Mult = zoneMult };   // off here

            // the server limits the zone's are stretched from: the server's own, or the declared default while it is off
            var basePace = server.KillsPerHour > 0 ? server.KillsPerHour : ServerConfig.zc_pcoin_falloff_kills_per_hour.Default;
            return server with { KillsPerHour = basePace, Mult = zoneMult, Scale = zonePace / basePace };
        }

        /// <summary>A zone's authored value for <paramref name="stat"/>, or <paramref name="server"/> when the row does not author
        /// it or authors something that is not a finite number.</summary>
        private static double Pick(EvaluatedProfile profile, string stat, double server)
        {
            if (!profile.Has(stat))
                return server;

            var value = profile.Get(stat, server);
            return double.IsFinite(value) ? value : server;
        }

        /// <summary>
        /// The bands of <paramref name="dials"/> in order, in server kills (before Scale): each is the kill count it runs up to and
        /// the share of its coins a kill in it pays. The first is (KillsPerHour, 1), then one band per step at Mult, Mult^2, ...
        /// The last always runs to +infinity: at 0 past the cap, or at the last step's share when there is no cap. Off = one
        /// full-pay band.
        /// </summary>
        public static List<(double UpTo, double Rate)> FalloffBands(FalloffDials dials)
        {
            var inf = double.PositiveInfinity;
            var bands = new List<(double UpTo, double Rate)>(8);
            if (!dials.On)
            {
                bands.Add((inf, 1.0));
                return bands;
            }

            var cap = dials.CapKillsPerHour > 0 ? dials.CapKillsPerHour : inf;
            var at = Math.Min(dials.KillsPerHour, cap);
            bands.Add((at, 1.0));

            if (at < cap)
            {
                if (!(dials.StepKills > 0))
                {
                    bands.Add((cap, dials.Mult));   // one reduced band up to the cap
                    at = cap;
                }
                else
                {
                    var rate = 1.0;
                    for (var step = 1; step <= MaxFalloffSteps && at < cap; step++)
                    {
                        rate *= dials.Mult;
                        at = step == MaxFalloffSteps ? cap : Math.Min(at + dials.StepKills, cap);
                        bands.Add((at, rate));
                    }
                }
            }

            if (!double.IsPositiveInfinity(at))
                bands.Add((inf, 0.0));   // past the cap: nothing
            return bands;
        }

        /// <summary>
        /// The coin hour a kill at <paramref name="now"/> falls in (pure), from the character's saved count and start: those, or a
        /// fresh hour (count 0, starting now, <c>Fresh</c> true) when there is no saved hour, it has run CoinHourSeconds, or the
        /// saved start is not a number. A start in the future (a clock that went back) is moved to now and keeps its count.
        /// </summary>
        public static (double Count, double Start, bool Fresh) FalloffHour(double? count, double? start, double now)
        {
            if (!start.HasValue || !double.IsFinite(start.Value) || now - start.Value >= CoinHourSeconds)
                return (0.0, now, true);

            var c = count.HasValue && double.IsFinite(count.Value) && count.Value > 0 ? count.Value : 0.0;
            return (c, Math.Min(start.Value, now), false);
        }

        /// <summary>
        /// One kill through the falloff (pure). <paramref name="count"/> is the server kills already counted in this coin hour;
        /// the kill is worth <paramref name="credit"/> kills (its tier-dip credit) and uses credit / Scale of the hour. Each part of
        /// it pays the share of the band it lands in, so with the defaults the 375th kill of the hour pays in full, the 376th half,
        /// the 451st a quarter, the 526th an eighth, the 601st a sixteenth, the 676th a thirty-second and the 751st nothing.
        /// Returns the credit to pay coins on, the count to save and whether any part paid less than full. Off: full pay and the
        /// count unchanged.
        /// </summary>
        public static (double Paid, double Count, bool Reduced) FalloffKill(double count, double credit, FalloffDials dials)
        {
            var c = double.IsFinite(credit) && credit > 0 ? credit : 0.0;
            var at = double.IsFinite(count) && count > 0 ? count : 0.0;
            if (!dials.On)
                return (c, at, false);

            var cost = c / dials.Scale;
            var left = cost;
            var pos = at;
            var paid = 0.0;
            var reduced = false;

            foreach (var (upTo, rate) in FalloffBands(dials))
            {
                if (left <= 0)
                    break;
                if (pos >= upTo)
                    continue;

                var take = Math.Min(left, upTo - pos);
                paid += take * rate;
                if (rate < 1.0 && take > Epsilon)
                    reduced = true;
                pos += take;
                left -= take;
            }

            return (paid * dials.Scale, at + cost, reduced);
        }

        /// <summary>The band the next kill starts in with <paramref name="count"/> server kills already in the hour: the share it
        /// pays and the server kill counts the band runs from and up to.</summary>
        public static (double Rate, double From, double UpTo) FalloffBandAt(double count, FalloffDials dials)
        {
            var at = double.IsFinite(count) && count > 0 ? count : 0.0;
            var from = 0.0;
            foreach (var (upTo, rate) in FalloffBands(dials))
            {
                if (at < upTo)
                    return (rate, from, upTo);
                from = upTo;
            }
            return (0.0, from, double.PositiveInfinity);   // not reached: the last band runs to +infinity
        }

        /// <summary>The most reduced bands DescribeFalloff lists one by one; a longer schedule is told as its rule instead.</summary>
        private const int MaxDescribedBands = 6;

        /// <summary>
        /// The schedule in a player's words, with this zone's limits (x Scale): "full for the first 375 kills, then 50 pct up to
        /// 450, 25 pct up to 525, 12.5 pct up to 600, 6.25 pct up to 675, 3.125 pct up to 750 and none past 750". A schedule with
        /// more steps than MaxDescribedBands is told as its rule ("then 50 pct, and 50 pct of that again every 75 kills"). Plain
        /// ASCII (it goes to the client).
        /// </summary>
        public static string DescribeFalloff(FalloffDials dials)
        {
            var bands = FalloffBands(dials);
            if (bands.Count == 1)
                return "full for every kill";

            var first = $"full for the first {Kills(bands[0].UpTo * dials.Scale)} kills";

            if (bands.Count - 1 > MaxDescribedBands)
            {
                var rule = $"{first}, then {Pct(dials.Mult)} pct, and {Pct(dials.Mult)} pct of that again every {Kills(dials.StepKills * dials.Scale)} kills";
                return dials.CapKillsPerHour > 0 ? $"{rule}, and none past {Kills(dials.CapKillsPerHour * dials.Scale)}" : rule;
            }

            var rest = new List<string>();
            for (var i = 1; i < bands.Count; i++)
            {
                var (upTo, rate) = bands[i];
                var from = bands[i - 1].UpTo * dials.Scale;
                if (double.IsPositiveInfinity(upTo))
                    rest.Add(rate > 0 ? $"{Pct(rate)} pct past {Kills(from)}" : $"none past {Kills(from)}");
                else
                    rest.Add($"{Pct(rate)} pct up to {Kills(upTo * dials.Scale)}");
            }

            var tail = rest.Count == 1 ? rest[0] : string.Join(", ", rest.GetRange(0, rest.Count - 1)) + " and " + rest[^1];
            return first + ", then " + tail;
        }

        /// <summary>A kill count for a player: whole numbers bare, else one decimal ("733.3").</summary>
        internal static string Kills(double kills) => kills.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A share as a percent for a player, to three decimals so halved shares stay exact ("50", "12.5", "3.125").</summary>
        internal static string Pct(double share) => (share * 100).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

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
