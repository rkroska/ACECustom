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

        // ---- Prestige Coin kill cap (falloff 2026-11-01; steps per coin hour 2026-11-17) ----------------------------------

        /// <summary>Length of a coin hour in seconds: the window a character's coin kills are counted in. It starts at their
        /// first coin kill and the next one after it ends starts a new one.</summary>
        public const double CoinHourSeconds = 3600.0;

        /// <summary>The most reduced steps a schedule is built with; past them the last step's share runs on to the stop (or to
        /// the end of the hour when there is no stop). At the shipped 50 pct reduction the 100th step pays 0.5^100 - nothing.</summary>
        internal const int MaxFalloffSteps = 100;

        /// <summary>The smallest share a step notice is sent for (1 pct): past it the steps keep falling without telling the
        /// player each time, so a fast hunter is not told "0.781 pct", "0.391 pct" ... every interval. A hard stop is always told.</summary>
        internal const double MinNoticeShare = 0.01;

        /// <summary>
        /// The kill cap for one kill, already held to its ranges (<see cref="CleanFalloff"/>). Of the kills a character makes in
        /// one coin hour, the first <see cref="Start"/> pay in full; then every <see cref="Interval"/> kills the share is cut by
        /// <see cref="Reduction"/> (a step keeps 1 - Reduction of the step before), and when <see cref="Max"/> is set nothing is
        /// paid past it. The defaults (zc_kill_cap_start 375 / zc_kill_cap_reduction 0.5 / zc_kill_cap_interval 75 /
        /// zc_kill_cap_max 0) halve the pay every 75 kills past 375 with no stop: kills 1-375 of the hour pay in full, 376-450
        /// half, 451-525 a quarter, 526-600 an eighth and so on, so an hour can never pay more than 450 kills' worth (375 + 75).
        ///
        /// Start 0 turns the cap off. Interval 0 makes every kill past Start pay 1 - Reduction up to the stop. Max 0 means no stop
        /// (the steps run on, see MaxFalloffSteps); a Max at or under Start stops all pay at Max. <see cref="Scale"/> stretches
        /// every limit for one zone (FalloffFor); the hour's count is kept in server kills, so a kill there uses 1 / Scale of one.
        /// </summary>
        public readonly record struct FalloffDials(double Start, double Reduction, double Interval, double Max, double Scale = 1.0)
        {
            /// <summary>True when kills under these dials are counted and can pay less than full.</summary>
            public bool On => Start > 0 && Scale > 0 && double.IsFinite(Scale);

            /// <summary>The share each step keeps of the step before: 1 - Reduction.</summary>
            public double Keep => 1.0 - Reduction;
        }

        /// <summary>
        /// Holds the kill cap numbers to their ranges - server settings and zone stats alike, since neither is checked when it is
        /// set: start, interval and max 0..1,000,000 kills, reduction 0..1. A value that is not a number takes the setting's
        /// declared default (PropertyManager).
        /// </summary>
        public static FalloffDials CleanFalloff(double start, double reduction, double interval, double max) => new(
            Clean(start, 0.0, MaxFalloffLimit, ServerConfig.zc_kill_cap_start.Default),
            Clean(reduction, 0.0, 1.0, ServerConfig.zc_kill_cap_reduction.Default),
            Clean(interval, 0.0, MaxFalloffLimit, ServerConfig.zc_kill_cap_interval.Default),
            Clean(max, 0.0, MaxFalloffLimit, ServerConfig.zc_kill_cap_max.Default));

        /// <summary>The highest start, interval or max can be set, in kills.</summary>
        private const double MaxFalloffLimit = 1_000_000.0;

        /// <summary>One dial held to [<paramref name="min"/>, <paramref name="max"/>]; NaN or infinity takes <paramref name="fallback"/>.</summary>
        private static double Clean(double value, double min, double max, double fallback)
            => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

        /// <summary>The server settings alone (zc_kill_cap_*), cleaned - what every kill uses until a zone authors a stat.</summary>
        public static FalloffDials ServerFalloff() => CleanFalloff(
            ServerConfig.zc_kill_cap_start.Value,
            ServerConfig.zc_kill_cap_reduction.Value,
            ServerConfig.zc_kill_cap_interval.Value,
            ServerConfig.zc_kill_cap_max.Value);

        /// <summary>
        /// The kill cap for a kill governed by <paramref name="zoneDefault"/> (the zone's Default row - its own stats over the tier
        /// Default; ZoneControlManager.ResolveZoneDefaultRowForCreature). Null row = the server settings.
        ///
        /// The Zone Control stat pcoin_falloff_kills_per_hour is the zone's start: it stretches the interval and the max with it (a
        /// zone at 500 against the server's 375 has 1.333 x each: full to 500, steps of 100); 0 turns the cap off there.
        /// pcoin_falloff_mult is the share each step keeps there (1 - its reduction: 0.5 = halve). A stat the row does not author,
        /// or authors as something that is not a number, keeps the server value. With the server's start at 0 (cap off except
        /// where a zone turns it on) the zone's start is read against the setting's declared default (375).
        /// </summary>
        public static FalloffDials FalloffFor(EvaluatedProfile zoneDefault) => FalloffFor(zoneDefault, ServerFalloff());

        /// <summary>The same as FalloffFor(zoneDefault), against given server dials (already cleaned) - for tests.</summary>
        internal static FalloffDials FalloffFor(EvaluatedProfile zoneDefault, FalloffDials server)
        {
            if (zoneDefault == null)
                return server;

            var reduction = TryPick(zoneDefault, ZoneStat.PcoinFalloffMult, out var keep)
                ? 1.0 - Math.Clamp(keep, 0.0, 1.0)
                : server.Reduction;

            var zoneStart = TryPick(zoneDefault, ZoneStat.PcoinFalloffKillsPerHour, out var start)
                ? Math.Clamp(start, 0.0, MaxFalloffLimit)
                : server.Start;

            if (!(zoneStart > 0))
                return server with { Start = 0.0, Reduction = reduction };   // off here

            // the server limits the zone's are stretched from: the server's own, or the declared default while it is off
            var baseStart = server.Start > 0 ? server.Start : ServerConfig.zc_kill_cap_start.Default;
            return server with { Start = baseStart, Reduction = reduction, Scale = zoneStart / baseStart };
        }

        /// <summary>A zone's authored value for <paramref name="stat"/>: false when the row does not author it or authors
        /// something that is not a finite number.</summary>
        private static bool TryPick(EvaluatedProfile profile, string stat, out double value)
        {
            value = 0.0;
            if (!profile.Has(stat))
                return false;

            value = profile.Get(stat, 0.0);
            return double.IsFinite(value);
        }

        /// <summary>
        /// The bands of <paramref name="dials"/> in order, in server kills (before Scale): each is the kill count it runs up to and
        /// the share of its coins a kill in it pays. The first is (Start, 1), then one band per interval at Keep, Keep^2, ...
        /// Bands that pay the same are merged. The last always runs to +infinity: at 0 past the max, or at the last step's share
        /// when there is no max. Off = one full-pay band.
        /// </summary>
        public static List<(double UpTo, double Rate)> FalloffBands(FalloffDials dials)
        {
            var inf = double.PositiveInfinity;
            var raw = new List<(double UpTo, double Rate)>(8);
            if (!dials.On)
            {
                raw.Add((inf, 1.0));
                return raw;
            }

            var max = dials.Max > 0 ? dials.Max : inf;
            var at = Math.Min(dials.Start, max);
            raw.Add((at, 1.0));

            if (at < max)
            {
                if (!(dials.Interval > 0))
                {
                    raw.Add((max, dials.Keep));   // one reduced band up to the stop
                    at = max;
                }
                else
                {
                    var rate = 1.0;
                    for (var step = 1; step <= MaxFalloffSteps && at < max; step++)
                    {
                        rate *= dials.Keep;
                        at = step == MaxFalloffSteps ? max : Math.Min(at + dials.Interval, max);
                        raw.Add((at, rate));
                    }
                }
            }

            if (!double.IsPositiveInfinity(at))
                raw.Add((inf, 0.0));   // past the max: nothing

            // merge neighbours that pay the same (a reduction of 0 or 1, or a step that ends right at the max)
            var bands = new List<(double UpTo, double Rate)>(raw.Count);
            foreach (var band in raw)
            {
                if (bands.Count > 0 && bands[bands.Count - 1].Rate == band.Rate)
                    bands[bands.Count - 1] = band;
                else
                    bands.Add(band);
            }
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
        /// The hour's count in server kills from the value SAVED on the character (PropertyFloat.PrestigeCoinHourShare), which is
        /// the share of the full-rate start used: count / Start, 1 = the whole start. Saving the share rather than kills keeps what
        /// each kill already used when zc_kill_cap_start changes mid-hour - a zone with its own start of 500 still reaches its first
        /// cut step after 500 of its kills (CodeRabbit #607). Off dials, or a bad saved value, read 0.
        /// </summary>
        public static double FalloffCountFromSaved(double saved, FalloffDials dials)
            => dials.Start > 0 && double.IsFinite(saved) && saved > 0 ? saved * dials.Start : 0.0;

        /// <summary>The value to save for an hour's count in server kills: the share of the full-rate start used (see
        /// FalloffCountFromSaved).</summary>
        public static double FalloffCountToSaved(double count, FalloffDials dials)
            => dials.Start > 0 && double.IsFinite(count) && count > 0 ? count / dials.Start : 0.0;

        /// <summary>
        /// The share of its coins the next kill pays (pure, read only - for /tierdip), with <paramref name="count"/> server kills
        /// already in the hour and the kill worth <paramref name="credit"/> kills (its tier-dip credit; 0 or less previews a
        /// full-value kill). A kill that straddles a step pays a blend: at 374.5 a full-value kill pays 75 pct (CodeRabbit #607).
        /// </summary>
        public static double FalloffNextShare(double count, double credit, FalloffDials dials)
        {
            var probe = double.IsFinite(credit) && credit > 0 ? credit : 1.0;
            return FalloffKill(count, probe, dials).Paid / probe;
        }

        /// <summary>
        /// One kill through the kill cap (pure). <paramref name="count"/> is the server kills already counted in this coin hour;
        /// the kill is worth <paramref name="credit"/> kills (its tier-dip credit) and uses credit / Scale of the hour. Each part of
        /// it pays the share of the band it lands in, so with the defaults the 375th kill of the hour pays in full, the 376th half,
        /// the 451st a quarter, the 526th an eighth, and so on. Returns the credit to pay coins on, the count to save and whether
        /// any part paid less than full. Off: full pay and the count unchanged.
        /// </summary>
        public static (double Paid, double Count, bool Reduced) FalloffKill(double count, double credit, FalloffDials dials)
            => FalloffKill(count, credit, dials, FalloffBands(dials));

        /// <summary>The same as FalloffKill(count, credit, dials) with the bands already built (FalloffBands(dials)), so one kill
        /// builds them once.</summary>
        internal static (double Paid, double Count, bool Reduced) FalloffKill(double count, double credit, FalloffDials dials,
            List<(double UpTo, double Rate)> bands)
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

            foreach (var (upTo, rate) in bands)
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
            => FalloffBandAt(count, FalloffBands(dials));

        /// <summary>The same as FalloffBandAt(count, dials) with the bands already built.</summary>
        internal static (double Rate, double From, double UpTo) FalloffBandAt(double count, List<(double UpTo, double Rate)> bands)
        {
            var at = double.IsFinite(count) && count > 0 ? count : 0.0;
            var from = 0.0;
            foreach (var (upTo, rate) in bands)
            {
                if (at < upTo)
                    return (rate, from, upTo);
                from = upTo;
            }
            return (0.0, from, double.PositiveInfinity);   // not reached: the last band runs to +infinity
        }

        /// <summary>Whether a kill that moved the player from a band paying <paramref name="before"/> into one paying
        /// <paramref name="after"/> earns a chat notice: a lower share of at least MinNoticeShare, or the hard stop.</summary>
        public static bool FalloffNotice(double before, double after)
            => after <= 0.0 ? before > 0.0 : after < before - Epsilon && after >= MinNoticeShare - Epsilon;

        /// <summary>The most reduced bands DescribeFalloff lists one by one; a longer schedule is told as its rule instead.</summary>
        private const int MaxDescribedBands = 6;

        /// <summary>
        /// The schedule in a player's words, with this zone's limits (x Scale). The shipped one is told as its rule: "full for
        /// the first 375 kills, then 50 pct less every 75 kills (50, 25, 12.5 pct ...)"; a short one band by band: "full for the
        /// first 375 kills, then 50 pct up to 450, 25 pct up to 525 and none past 525". Plain ASCII (it goes to the client).
        /// </summary>
        public static string DescribeFalloff(FalloffDials dials)
        {
            var bands = FalloffBands(dials);
            if (bands.Count == 1)
                return bands[0].Rate > 0 ? "full for every kill" : "none";

            var first = $"full for the first {Kills(bands[0].UpTo * dials.Scale)} kills";

            if (bands.Count - 1 > MaxDescribedBands)
            {
                var k = dials.Keep;
                var rule = $"{first}, then {Pct(dials.Reduction)} pct less every {Kills(dials.Interval * dials.Scale)} kills ({Pct(k)}, {Pct(k * k)}, {Pct(k * k * k)} pct ...)";
                return dials.Max > 0 ? $"{rule}, and none past {Kills(dials.Max * dials.Scale)}" : rule;
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
