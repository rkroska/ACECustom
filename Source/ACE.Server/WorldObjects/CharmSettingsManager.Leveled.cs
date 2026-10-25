using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// The levelled charms (owner 2026-10-13: "Tiers or Levels of these charms will go from 1 to 100"): Split Arrow, Chain,
    /// Pandemic, Improved Cleave, Armor Breaker, Shield Arc. One item per charm; its CharmLevel is 1-100.
    ///
    /// A charm's level comes from the Prestige Coins its owner has put into it (owner 2026-10-24, Player_CharmLevels.cs):
    /// each charm's "coinbase" (coins for level 1 to 2) and "coingrowth" (each level costs that much more than the one
    /// before) are its price curve, and changing either re-levels every player from the coins they have put in.
    ///
    /// Every NUMBER of such a charm has two live settings: its value at level 1 (key "jumps") and its value at level 100
    /// (key "jumps_100"), a straight line between them. An unset level-100 value means "the same at every level".
    /// On / off settings have one value. All of it is set with /charm, live at once, kept in charm_settings.
    /// </summary>
    public static partial class CharmSettingsManager
    {
        public enum CharmSettingKind { Bool, Int, Float }

        /// <summary>One setting of a levelled charm: its key, what it is, its limits, its defaults and its help line.</summary>
        public sealed class CharmSettingDef
        {
            public readonly string Key;
            public readonly CharmSettingKind Kind;
            /// <summary>True for a number that has a level-100 twin; false for a number that is the same at every level.</summary>
            public readonly bool Levels;
            public readonly double Min, Max;
            public readonly double Default;
            /// <summary>NaN = no level-100 default (the same at every level).</summary>
            public readonly double Default100;
            public readonly string Help;

            /// <summary>The default level schedule (2026-10-17): steps "25:1,50:1", every "5:0.5", fill target (NaN = none).
            /// Set with <see cref="Sched"/>; a /charm change overrides it like any other default.</summary>
            public string DefaultSteps { get; private set; } = "";
            public string DefaultEvery { get; private set; } = "";
            public double DefaultFill { get; private set; } = double.NaN;

            public CharmSettingDef(string key, CharmSettingKind kind, bool levels, double min, double max, double def, double def100, string help)
            {
                Key = key; Kind = kind; Levels = levels; Min = min; Max = max; Default = def; Default100 = def100; Help = help;
            }

            /// <summary>Gives a levelled number a default schedule (any part may be left out).</summary>
            public CharmSettingDef Sched(string steps = "", string every = "", double fill = double.NaN)
            {
                DefaultSteps = steps; DefaultEvery = every; DefaultFill = fill;
                return this;
            }

            public static CharmSettingDef Switch(string key, bool def, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Bool, false, 0, 1, def ? 1 : 0, double.NaN, help);
            public static CharmSettingDef Count(string key, int min, int max, int def, int def100, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Int, true, min, max, def, def100, help);
            public static CharmSettingDef FlatCount(string key, int min, int max, int def, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Int, false, min, max, def, double.NaN, help);
            public static CharmSettingDef Number(string key, double min, double max, double def, double def100, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Float, true, min, max, def, def100, help);
            /// <summary>A number that is the same at every level (no level-100 twin, no schedule).</summary>
            public static CharmSettingDef FlatNumber(string key, double min, double max, double def, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Float, false, min, max, def, double.NaN, help);
        }

        /// <summary>The settings of one levelled charm. Reads are lock-free (combat threads); writes come from /charm.</summary>
        public sealed class LeveledCharmBlock : ICharmBlock
        {
            public const int MaxLevel = 100;
            private const string Suffix100 = "_100";

            public readonly string Name;
            public readonly string DisplayName;
            public readonly string Summary;
            public readonly CharmSettingDef[] Defs;
            private readonly Func<LeveledCharmBlock, int, string> playerText;

            private readonly Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly double[] at1;
            private readonly double[] at100;   // NaN = unset: the same at every level

            // The level SCHEDULE (owner 2026-10-17: "Retune any charm, live ... If we want to add another +1 arrow at level
            // 10, we should be able to add that in"). Per number, all optional; any of them set = they replace the straight
            // line: key_steps "25:1,50:1" (+amount at a level), key_every "5:0.5" (+X on every Nth level), key_fill (spread
            // the rest up to this value over the FREE levels - levels 2-100 on which no number of this charm steps).
            private const string SuffixSteps = "_steps", SuffixEvery = "_every", SuffixFill = "_fill";
            private readonly string[] stepsText;   // "" = none
            private readonly string[] everyText;   // "" = none
            private readonly double[] fillTo;      // NaN = none
            /// <summary>Per number: its value at every level (index = level 1-100), or null = the straight line. Rebuilt
            /// whole on every change and swapped in, so combat threads read it without a lock.</summary>
            private volatile double[][] curves;

            public LeveledCharmBlock(string name, string displayName, string summary, Func<LeveledCharmBlock, int, string> playerText, params CharmSettingDef[] defs)
            {
                Name = name; DisplayName = displayName; Summary = summary; this.playerText = playerText;
                var all = new List<CharmSettingDef> { CharmSettingDef.Switch("enabled", true, "The whole charm on / off for everybody.") };
                all.AddRange(defs);
                Defs = all.ToArray();
                at1 = new double[Defs.Length];
                at100 = new double[Defs.Length];
                stepsText = new string[Defs.Length];
                everyText = new string[Defs.Length];
                fillTo = new double[Defs.Length];
                for (var i = 0; i < Defs.Length; i++) index[Defs[i].Key] = i;
                Reset();
            }

            // -- reads (combat) ------------------------------------------------

            public bool Enabled => On("enabled");

            public bool On(string key) => index.TryGetValue(key, out var i) && at1[i] >= 0.5;

            /// <summary>The value of a setting at a charm level (1-100): a straight line from the level-1 value to the
            /// level-100 value, inside the setting's limits. A level below 1 counts as 1.</summary>
            public double At(string key, int level)
            {
                if (!index.TryGetValue(key, out var i)) return 0;
                var curve = curves?[i];
                if (curve != null) return curve[Math.Clamp(level, 1, MaxLevel)];
                var a = at1[i];
                var b = at100[i];
                var d = Defs[i];
                if (!d.Levels || double.IsNaN(b)) return a;
                var t = (Math.Clamp(level, 1, MaxLevel) - 1) / (double)(MaxLevel - 1);
                return Math.Clamp(a + (b - a) * t, d.Min, d.Max);
            }

            /// <summary>A count at a charm level, rounded to the nearest whole number (halves away from zero).</summary>
            public int CountAt(string key, int level) => (int)Math.Round(At(key, level), MidpointRounding.AwayFromZero);

            public float NumberAt(string key, int level) => (float)At(key, level);

            // -- the coin price curve (owner 2026-10-24) -----------------------
            // cost of level L -> L+1 = coinbase x coingrowth^(L-1). The level is worked out from the coins a player has put
            // in, so these are read on every level check and a change re-levels everyone (CharmCommands refreshes the
            // online players; the others at their next login).

            /// <summary>The setting keys of the price curve - a change to either re-levels every player.</summary>
            public static bool IsCurveKey(string key) => key is "coinbase" or "coingrowth";

            /// <summary>Coins for level 1 to 2.</summary>
            public double CoinBase => Math.Max(1, At("coinbase", 1));

            /// <summary>How much more each level costs than the one before (1 = every level the same).</summary>
            public double CoinGrowth => Math.Max(1, At("coingrowth", 1));

            /// <summary>Whole Prestige Coins needed in all to reach a level (0 for level 1): the sum of the level prices,
            /// coinbase x (coingrowth^(L-1) - 1) / (coingrowth - 1), rounded up.</summary>
            public long CoinsToReach(int level)
            {
                if (level <= 1) return 0;
                var l = Math.Min(level, MaxLevel) - 1;
                var b = CoinBase;
                var g = CoinGrowth;
                var total = g - 1 < 1e-9 ? b * l : b * (Math.Pow(g, l) - 1) / (g - 1);
                return (long)Math.Ceiling(Math.Min(total, 9e15) - 1e-6);
            }

            /// <summary>Whole coins from a level to the next one; 0 at level 100.</summary>
            public long CoinsForLevel(int level) => level < 1 || level >= MaxLevel ? 0 : CoinsToReach(level + 1) - CoinsToReach(level);

            /// <summary>Coins that reach level 100 - a charm never holds more.</summary>
            public long MaxCoins => CoinsToReach(MaxLevel);

            /// <summary>The whole level (1-100) a number of coins put in reaches.</summary>
            public int LevelFromCoins(long coins)
            {
                if (coins <= 0) return 1;
                var b = CoinBase;
                var g = CoinGrowth;
                var estimate = g - 1 < 1e-9 ? 1 + coins / b : 1 + Math.Log(1 + coins * (g - 1) / b) / Math.Log(g);
                var level = (int)Math.Clamp(double.IsFinite(estimate) ? Math.Floor(estimate) : MaxLevel, 1, MaxLevel);
                // the rounding of each threshold decides, never the estimate
                while (level < MaxLevel && CoinsToReach(level + 1) <= coins) level++;
                while (level > 1 && CoinsToReach(level) > coins) level--;
                return level;
            }

            /// <summary>The level with the share of the way to the next one, e.g. 41.95 = level 41, 95 pct of the coins to 42.</summary>
            public double ExactLevelFromCoins(long coins)
            {
                var level = LevelFromCoins(coins);
                if (level >= MaxLevel) return MaxLevel;
                var low = CoinsToReach(level);
                var span = CoinsToReach(level + 1) - low;
                return span > 0 ? level + Math.Clamp((coins - low) / (double)span, 0, 0.9999) : level;
            }

            // -- ICharmBlock ---------------------------------------------------

            public void Reset()
            {
                for (var i = 0; i < Defs.Length; i++)
                {
                    at1[i] = Defs[i].Default;
                    at100[i] = Defs[i].Default100;
                    stepsText[i] = Defs[i].Levels ? Defs[i].DefaultSteps : "";
                    everyText[i] = Defs[i].Levels ? Defs[i].DefaultEvery : "";
                    fillTo[i] = Defs[i].Levels ? Defs[i].DefaultFill : double.NaN;
                }
                Rebuild();
            }

            /// <summary>Splits "chain_steps" into ("chain", "_steps") for the three schedule suffixes; null when it is none.</summary>
            private static string SchedulePart(string key, out string baseKey)
            {
                foreach (var suffix in new[] { SuffixSteps, SuffixEvery, SuffixFill })
                    if (key.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        baseKey = key.Substring(0, key.Length - suffix.Length);
                        return suffix;
                    }
                baseKey = key;
                return null;
            }

            /// <summary>Sets one schedule part (value already trimmed); returns the reply, or why it was refused.</summary>
            private string SetSchedule(int i, string suffix, string value, bool quiet)
            {
                var d = Defs[i];
                if (!d.Levels) return $"{Name}.{d.Key} is the same at every level - it has no schedule.";
                var clear = value == "" || value == "-" || value.Equals("clear", StringComparison.OrdinalIgnoreCase);
                value = value.Replace(" ", "");
                switch (suffix)
                {
                    case SuffixSteps:
                    {
                        var parsed = new List<(int level, double amount)>();
                        if (!clear && (!ParseSteps(value, parsed) || value.Length > 96)) return quiet ? "" : $"{Name}.{d.Key}{suffix}: use Level:Amount pairs with a comma between, levels 2 to 100 - e.g. 25:1,50:1,75:1,100:1 (or clear).";
                        // an amount no bigger than the number's own span (review 2026-10-20 round 2: huge opposite amounts summed to NaN)
                        if (!clear && parsed.Exists(p => Math.Abs(p.amount) > d.Max - d.Min)) return quiet ? "" : $"{Name}.{d.Key}{suffix}: each amount must be between -{F0(d.Max - d.Min)} and {F0(d.Max - d.Min)}.";
                        stepsText[i] = clear ? "" : value;
                        break;
                    }
                    case SuffixEvery:
                        if (!clear && !ParseEvery(value, out _, out _)) return quiet ? "" : $"{Name}.{d.Key}{suffix}: use N:Amount, N 1 to 100 - e.g. 5:0.5 = +0.5 on every 5th level (or clear).";
                        if (!clear && ParseEvery(value, out _, out var perLevel) && Math.Abs(perLevel) > d.Max - d.Min) return quiet ? "" : $"{Name}.{d.Key}{suffix}: the amount must be between -{F0(d.Max - d.Min)} and {F0(d.Max - d.Min)}.";
                        everyText[i] = clear ? "" : value;
                        break;
                    default:
                        if (clear) { fillTo[i] = double.NaN; break; }
                        if (!Parse(d, value, out var v, out var why)) return quiet ? "" : why;
                        fillTo[i] = v;
                        break;
                }
                Rebuild();
                return $"{Name}.{d.Key}{suffix} = {(clear ? "cleared" : value)}  (level 1: {ShowAt(d, 1)}, 25: {ShowAt(d, 25)}, 50: {ShowAt(d, 50)}, 75: {ShowAt(d, 75)}, 100: {ShowAt(d, 100)})";
            }

            public string TrySet(string key, string value)
            {
                key = (key ?? "").ToLowerInvariant();
                if (key is "on" or "off" or "true" or "false") { value = key; key = "enabled"; }

                var part = SchedulePart(key, out var schedBase);
                if (part != null)
                {
                    if (!index.TryGetValue(schedBase, out var si)) return null;
                    return SetSchedule(si, part, (value ?? "").Trim(), false);
                }

                var hundred = key.EndsWith(Suffix100, StringComparison.Ordinal);
                var baseKey = hundred ? key.Substring(0, key.Length - Suffix100.Length) : key;
                if (!index.TryGetValue(baseKey, out var i)) return null;
                var d = Defs[i];
                if (hundred && !d.Levels) return $"{Name}.{baseKey} has no level-100 value - it is the same at every level.";
                // a schedule replaces the straight line, so a level-100 value would be saved and do nothing (review 2026-10-20)
                if (hundred && curves?[i] != null)
                    return $"{Name}.{baseKey} follows a level schedule, so {key} would change nothing. Change {baseKey}_steps / {baseKey}_every / {baseKey}_fill, or clear them first (e.g. {baseKey}_steps clear).";

                value = (value ?? "").Trim();
                if (hundred && (value == "" || value == "-" || value.Equals("clear", StringComparison.OrdinalIgnoreCase)))
                {
                    at100[i] = double.NaN;
                    return $"{Name}.{key} cleared - {baseKey} is {Show(d, at1[i])} at every level";
                }

                if (!Parse(d, value, out var v, out var why)) return why;
                if (hundred) at100[i] = v; else at1[i] = v;
                Rebuild();
                if (curves?[i] != null)
                    return $"{Name}.{key} = {Show(d, v)}  (schedule: level 1: {ShowAt(d, 1)}, 25: {ShowAt(d, 25)}, 50: {ShowAt(d, 50)}, 75: {ShowAt(d, 75)}, 100: {ShowAt(d, 100)})";
                return $"{Name}.{key} = {Show(d, v)}" + (d.Levels ? $"  (level 1: {Show(d, at1[i])}, level 100: {(double.IsNaN(at100[i]) ? "same" : Show(d, at100[i]))})" : "");
            }

            public void ApplyRaw(string key, string value)
            {
                key = (key ?? "").ToLowerInvariant();
                var part = SchedulePart(key, out var schedBase);
                if (part != null)
                {
                    // a saved schedule the checks now refuse keeps the default one - say so (round-2 fix-delta review)
                    if (index.TryGetValue(schedBase, out var si) && SetSchedule(si, part, (value ?? "").Trim(), true) == "")
                        log.Warn($"CharmSettingsManager: saved {Name}.{key} = '{value}' is not a valid schedule any more - the default schedule is used. Set it again with /charm.");
                    return;
                }
                var hundred = key.EndsWith(Suffix100, StringComparison.Ordinal);
                var baseKey = hundred ? key.Substring(0, key.Length - Suffix100.Length) : key;
                if (!index.TryGetValue(baseKey, out var i)) return;
                var d = Defs[i];
                if (hundred && !d.Levels) return;
                value = (value ?? "").Trim();
                if (hundred && value == "") { at100[i] = double.NaN; Rebuild(); return; }
                if (!Parse(d, value, out var v, out _)) return;
                if (hundred) at100[i] = v; else at1[i] = v;
                Rebuild();
            }

            public string GetRaw(string key)
            {
                key = (key ?? "").ToLowerInvariant();
                var part = SchedulePart(key, out var schedBase);
                if (part != null)
                {
                    if (!index.TryGetValue(schedBase, out var si) || !Defs[si].Levels) return null;
                    return part == SuffixSteps ? stepsText[si] : part == SuffixEvery ? everyText[si] : double.IsNaN(fillTo[si]) ? "" : Raw(Defs[si], fillTo[si]);
                }
                var hundred = key.EndsWith(Suffix100, StringComparison.Ordinal);
                var baseKey = hundred ? key.Substring(0, key.Length - Suffix100.Length) : key;
                if (!index.TryGetValue(baseKey, out var i)) return null;
                var d = Defs[i];
                if (hundred) return d.Levels && !double.IsNaN(at100[i]) ? Raw(d, at100[i]) : "";
                return Raw(d, at1[i]);
            }

            /// <summary>The saved rows' format (review 2026-10-20): 2 = the level schedules (2026-10-17). Rows saved without it
            /// predate the schedules, and an old level-1 value under a new default schedule gives numbers neither version
            /// had, so CharmSettingsManager.LoadAll drops them and saves the charm again from its defaults.</summary>
            public const string VersionKey = "settings_version";
            public const int SettingsVersion = 2;

            public IEnumerable<(string key, string value)> GetAllRaw()
            {
                yield return (VersionKey, SettingsVersion.ToString(CultureInfo.InvariantCulture));
                for (var i = 0; i < Defs.Length; i++)
                {
                    yield return (Defs[i].Key, Raw(Defs[i], at1[i]));
                    if (!Defs[i].Levels) continue;
                    yield return (Defs[i].Key + Suffix100, double.IsNaN(at100[i]) ? "" : Raw(Defs[i], at100[i]));
                    yield return (Defs[i].Key + SuffixSteps, stepsText[i]);
                    yield return (Defs[i].Key + SuffixEvery, everyText[i]);
                    yield return (Defs[i].Key + SuffixFill, double.IsNaN(fillTo[i]) ? "" : Raw(Defs[i], fillTo[i]));
                }
            }

            // -- the schedule --------------------------------------------------

            /// <summary>"25:1,50:1" -> (25, 1), (50, 1). False = not that shape (levels 2-100, finite amounts).</summary>
            private static bool ParseSteps(string text, List<(int level, double amount)> into)
            {
                into?.Clear();
                if (string.IsNullOrEmpty(text)) return true;
                foreach (var piece in text.Split(','))
                {
                    var c = piece.IndexOf(':');
                    if (c <= 0 || !int.TryParse(piece.AsSpan(0, c), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv) || lv < 2 || lv > MaxLevel) return false;
                    if (!double.TryParse(piece.AsSpan(c + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var amt) || double.IsNaN(amt) || double.IsInfinity(amt)) return false;
                    into?.Add((lv, amt));
                }
                return true;
            }

            /// <summary>"5:0.5" -> +0.5 on every 5th level. False = not that shape (N 1-100, a finite amount).</summary>
            private static bool ParseEvery(string text, out int every, out double amount)
            {
                every = 0; amount = 0;
                if (string.IsNullOrEmpty(text)) return true;
                var c = text.IndexOf(':');
                return c > 0 && int.TryParse(text.AsSpan(0, c), NumberStyles.Integer, CultureInfo.InvariantCulture, out every) && every >= 1 && every <= MaxLevel
                    && double.TryParse(text.AsSpan(c + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out amount) && !double.IsNaN(amount) && !double.IsInfinity(amount);
            }

            /// <summary>Works out every scheduled number at every level and swaps the tables in. The ZC plugin's Level Curve
            /// (ZoneControlHud.Charms.cs, CharmCurve) does the same sums - keep the two alike.</summary>
            private void Rebuild()
            {
                // levels 2-100 on which some number of this charm steps; the others are FREE (a fill spreads over them)
                var stepOn = new bool[MaxLevel + 1];
                var steps = new List<(int level, double amount)>();
                for (var i = 0; i < Defs.Length; i++)
                {
                    if (!Defs[i].Levels) continue;
                    if (ParseSteps(stepsText[i], steps))
                        foreach (var (lv, amt) in steps) if (amt != 0) stepOn[lv] = true;
                    if (ParseEvery(everyText[i], out var n, out var x) && n > 0 && x != 0)
                        for (var lv = 2; lv <= MaxLevel; lv++) if (lv % n == 0) stepOn[lv] = true;
                }
                var free = 0;
                for (var lv = 2; lv <= MaxLevel; lv++) if (!stepOn[lv]) free++;

                var built = new double[Defs.Length][];
                for (var i = 0; i < Defs.Length; i++)
                {
                    var d = Defs[i];
                    if (!d.Levels || (stepsText[i].Length == 0 && everyText[i].Length == 0 && double.IsNaN(fillTo[i]))) continue;
                    ParseSteps(stepsText[i], steps);
                    ParseEvery(everyText[i], out var every, out var per);
                    var raw = new double[MaxLevel + 1];
                    for (var lv = 1; lv <= MaxLevel; lv++)
                    {
                        var v = at1[i];
                        foreach (var (sl, amt) in steps) if (sl <= lv) v += amt;
                        if (every > 0) v += per * (lv / every - (every == 1 ? 1 : 0));   // every Nth level in 2..lv
                        raw[lv] = v;
                    }
                    if (!double.IsNaN(fillTo[i]) && free > 0)
                    {
                        var share = (fillTo[i] - raw[MaxLevel]) / free;
                        var got = 0;
                        for (var lv = 2; lv <= MaxLevel; lv++)
                        {
                            if (!stepOn[lv]) got++;
                            raw[lv] += share * got;
                        }
                    }
                    // Math.Clamp passes NaN through: a value that is not a number falls back to the default (round 2)
                    for (var lv = 1; lv <= MaxLevel; lv++) raw[lv] = Math.Clamp(double.IsFinite(raw[lv]) ? raw[lv] : d.Default, d.Min, d.Max);
                    raw[0] = raw[1];
                    built[i] = raw;
                }
                curves = built;
            }

            public string Help()
            {
                var sb = new StringBuilder();
                sb.Append($"[{DisplayName}] {Summary}\n");
                sb.Append("Adjustable settings (a number has a level-1 value 'key' and a level-100 value 'key_100'; levels between are a straight line):\n");
                sb.Append("Or a schedule per number, which replaces the line: 'key_steps 25:1,50:1' (+amount at a level), 'key_every 5:0.5' (+amount on every Nth level), 'key_fill 1' (spread up to this over the levels nothing else steps on). 'clear' removes a part.\n");
                foreach (var d in Defs)
                {
                    var kind = d.Kind == CharmSettingKind.Bool ? "on / off" : d.Kind == CharmSettingKind.Int ? $"whole number {F0(d.Min)} to {F0(d.Max)}" : $"number {F0(d.Min)} to {F0(d.Max)}";
                    sb.Append($"  - {d.Key}{(d.Levels ? " / " + d.Key + Suffix100 : "")}  ({kind}) - {d.Help}\n");
                }
                sb.Append("\n[Examples]\n");
                sb.Append($"  - /charm {Name} off\n");
                foreach (var d in Defs)
                    if (d.Levels)
                    {
                        sb.Append($"  - /charm {Name} {d.Key} {Raw(d, d.Default)}\n");
                        sb.Append($"  - /charm {Name} {d.Key}{Suffix100} {Raw(d, double.IsNaN(d.Default100) ? d.Default : d.Default100)}   (or 'clear' = same at every level)\n");
                        break;
                    }
                sb.Append($"  - /charm level {Name} 50   - set the level of the {DisplayName} charm you carry\n");
                sb.Append($"  - /charm curve {Name}      - the coin price of every level");
                return sb.ToString();
            }

            public string Dump()
            {
                var sb = new StringBuilder();
                sb.Append($"[{DisplayName}] Current Settings\n");
                for (var i = 0; i < Defs.Length; i++)
                {
                    var d = Defs[i];
                    if (!d.Levels) sb.Append($"  - {d.Key}: {Show(d, at1[i])}\n");
                    else if (curves?[i] != null)
                        sb.Append($"  - {d.Key}: level 1 = {Show(d, at1[i])}, schedule"
                            + (stepsText[i].Length > 0 ? $" steps {stepsText[i]}" : "") + (everyText[i].Length > 0 ? $" every {everyText[i]}" : "")
                            + (!double.IsNaN(fillTo[i]) ? $" fill to {Show(d, fillTo[i])}" : "")
                            + $" -> 25 = {ShowAt(d, 25)}, 50 = {ShowAt(d, 50)}, 75 = {ShowAt(d, 75)}, 100 = {ShowAt(d, 100)}\n");
                    else sb.Append($"  - {d.Key}: level 1 = {Show(d, at1[i])}, level 100 = {(double.IsNaN(at100[i]) ? "same" : Show(d, at100[i]))}, level 50 = {ShowAt(d, 50)}\n");
                }
                return sb.ToString();
            }

            /// <summary>What the charm does for its owner at one level, in plain words for the item's panel and the
            /// switch-on line. Built from the live settings, so it never disagrees with them.</summary>
            public string PlayerText(int level) =>
                Enabled ? playerText(this, Math.Clamp(level, 1, MaxLevel)) : "This charm is switched off for everyone at the moment.";

            /// <summary>What the charm does at one level, every levelled number worked out.</summary>
            public string DescribeAt(int level)
            {
                var sb = new StringBuilder();
                sb.Append($"[{DisplayName}] at level {Math.Clamp(level, 1, MaxLevel)}:");
                foreach (var d in Defs)
                    if (d.Levels) sb.Append($"  {d.Key} {ShowAt(d, level)}");
                return sb.ToString();
            }

            // -- the plugin's wire line ----------------------------------------

            /// <summary>One line for the client plugin: name | label | key=kind~v1~v100~min~max~levels~steps~every~fill ...
            /// (v100 / fill empty = unset; steps / every appended 2026-10-17, an older plugin ignores them). Help text travels
            /// separately (HelpLines) because it never changes.</summary>
            public string WireLine()
            {
                var sb = new StringBuilder();
                sb.Append("[[ZCCH]]").Append(Name).Append('|').Append(DisplayName).Append("|1");
                for (var i = 0; i < Defs.Length; i++)
                {
                    var d = Defs[i];
                    var kind = d.Kind == CharmSettingKind.Bool ? "b" : d.Kind == CharmSettingKind.Int ? "i" : "f";
                    sb.Append('|').Append(d.Key).Append('=').Append(kind).Append('~').Append(Raw(d, at1[i])).Append('~')
                      .Append(d.Levels && !double.IsNaN(at100[i]) ? Raw(d, at100[i]) : "").Append('~')
                      .Append(F0(d.Min)).Append('~').Append(F0(d.Max)).Append('~').Append(d.Levels ? '1' : '0')
                      .Append('~').Append(d.Levels ? stepsText[i] : "").Append('~').Append(d.Levels ? everyText[i] : "")
                      .Append('~').Append(d.Levels && !double.IsNaN(fillTo[i]) ? Raw(d, fillTo[i]) : "");
                }
                return sb.ToString();
            }

            /// <summary>The help lines for the plugin, packed several settings to a line, each line under 600 characters.</summary>
            public IEnumerable<string> HelpLines()
            {
                var head = "[[ZCCHH]]" + Name + "|" + Clean(Summary);
                var sb = new StringBuilder(head);
                foreach (var d in Defs)
                {
                    var piece = "|" + d.Key + "~" + Clean(d.Help);
                    if (sb.Length + piece.Length > 560 && sb.Length > head.Length)
                    {
                        yield return sb.ToString();
                        sb = new StringBuilder("[[ZCCHH]]" + Name + "|");
                    }
                    sb.Append(piece);
                }
                yield return sb.ToString();
            }

            // -- helpers -------------------------------------------------------

            private string ShowAt(CharmSettingDef d, int level) =>
                d.Kind == CharmSettingKind.Int ? CountAt(d.Key, level).ToString(CultureInfo.InvariantCulture) : Show(d, At(d.Key, level));

            private static bool Parse(CharmSettingDef d, string value, out double v, out string why)
            {
                v = 0; why = null;
                switch (d.Kind)
                {
                    case CharmSettingKind.Bool:
                        if (!ParseBool(value, out var b)) { why = $"Invalid on / off value: '{value}'. Use on or off."; return false; }
                        v = b ? 1 : 0; return true;
                    case CharmSettingKind.Int:
                        if (!ParseInt(value, out var n)) { why = $"Invalid whole number: '{value}'."; return false; }
                        if (n < d.Min || n > d.Max) { why = $"{d.Key} must be between {F0(d.Min)} and {F0(d.Max)}."; return false; }
                        v = n; return true;
                    default:
                        if (!ParseFloat(value, out var f) || float.IsNaN(f) || float.IsInfinity(f)) { why = $"Invalid number: '{value}'."; return false; }
                        if (f < d.Min || f > d.Max) { why = $"{d.Key} must be between {F0(d.Min)} and {F0(d.Max)}."; return false; }
                        v = f; return true;
                }
            }

            private static string Raw(CharmSettingDef d, double v) => d.Kind switch
            {
                CharmSettingKind.Bool => v >= 0.5 ? "true" : "false",
                CharmSettingKind.Int => ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture),
                _ => v.ToString("0.####", CultureInfo.InvariantCulture),
            };

            private static string Show(CharmSettingDef d, double v) => d.Kind == CharmSettingKind.Bool ? (v >= 0.5 ? "on" : "off") : Raw(d, v);

            private static string F0(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

            private static string Clean(string s) => (s ?? "").Replace('|', '/').Replace('~', '-').Replace('\n', ' ');
        }

        // ---------------------------------------------------------------------
        //  The four levelled charms and their settings
        // ---------------------------------------------------------------------

        // Owner 2026-10-24: the price curve of a levelled charm. ~6 months to level 100 at 20 hours a day, each tier's levels
        // ~1.3x slower than the last (income +20 pct a tier, ~15 levels a tier, 1.03^15 / 1.2). coinbase assumes the T11
        // placeholder income of ~15 coins an hour - retune both together.
        private static CharmSettingDef CoinBase() => CharmSettingDef.FlatNumber("coinbase", 1, 1000000, 230,
            "Prestige Coins to go from level 1 to level 2; each later level costs coingrowth times the one before. A change re-levels every player from the coins they have put in.");
        private static CharmSettingDef CoinGrowth() => CharmSettingDef.FlatNumber("coingrowth", 1, 1.2, 1.03,
            "How much more each level costs than the one before (1.03 = 3 pct more, 1 = every level the same). A change re-levels every player from the coins they have put in.");

        private const string T11OnlyHelp = "Needs a T11+ weapon of the right kind in hand, where its power is live. Off = any weapon, anywhere.";

        public static LeveledCharmBlock SplitArrow { get; } = new LeveledCharmBlock("splitarrow", "Split Arrow",
            "A missile launcher fires extra arrows at monsters near the main target. The higher of the charm's and the launcher's own split numbers is used, never the sum.",
            (b, lv) => $"Your {Gear(b, "missile launchers")} fire {Things(b.CountAt("arrows", lv), "extra arrow")} at monsters within {M(b.At("range", lv))} of your target, each for {Pct(b.At("damage", lv))} of a normal hit. A launcher with split arrows of its own uses the higher numbers.",
            CharmSettingDef.Count("arrows", 0, 10, 2, 6, "Extra arrows a shot (one per nearby monster).").Sched(steps: "25:1,50:1,75:1,100:1"),
            CharmSettingDef.Number("range", 0, 50, 10, 20, "Metres around the MAIN TARGET in which the extra arrows find monsters.").Sched(every: "5:0.5"),
            // owner 2026-10-21 (Schneebly): missile is strongest at range - up close it must not beat a two-hander, so the
            // extra arrows start at a quarter of a hit and reach half at level 100
            CharmSettingDef.Number("damage", 0, 1, 0.25, 0.5, "Damage of an extra arrow as a fraction of a normal hit (0.5 = half)."),
            // owner 2026-10-21: the cone never widens - 60 degrees each side of the target (120 in all) at every level
            CharmSettingDef.Number("angle", 0, 180, 60, double.NaN, "How far to either side of the line to the main target, in degrees seen from the archer, a monster may stand (60 = the split arrows everyone has, a 120 degree cone; 180 = all around)."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp),
            CoinBase(), CoinGrowth());

        public static LeveledCharmBlock Chain { get; } = new LeveledCharmBlock("chain", "Chain",
            "A hand-cast war arc, bolt or streak that hits a monster jumps on to the nearest other monster, then the next. One monster after another, each jump from the last monster hit.",
            (b, lv) => $"Your war {Shapes(b)} jump on to {Things(b.CountAt("jumps", lv), "more monster")}, each within {M(b.At("range", lv))} of the one just hit. The first jump deals {Pct(b.At("damage", lv))} of a normal hit"
                       + (Math.Abs(b.At("falloff", lv) - 1) < 0.0005 ? "." : $", each further jump {Pct(b.At("falloff", lv))} of the one before" + (b.At("floor", lv) > 0.0005 ? $", never below {Pct(b.At("floor", lv))}." : "."))
                       + (b.At("revisitchance", lv) > 0.0005 && !b.On("revisit") ? $" When no new monster is in reach, it has a {Pct(b.At("revisitchance", lv))} chance to jump back to one it already hit." : "")
                       + (b.On("t11only") ? " Needs a T11+ wand in hand." : ""),
            CharmSettingDef.Count("jumps", 0, 20, 2, 6, "Jumps after the first hit.").Sched(steps: "25:1,50:1,75:1,100:1"),
            CharmSettingDef.Number("range", 0, 50, 10, 20, "Metres a jump can cover, from the monster just hit.").Sched(every: "5:0.5"),
            CharmSettingDef.Number("damage", 0, 2, 0.5, 1, "Damage of the first jump as a fraction of a normal hit.").Sched(fill: 1),
            CharmSettingDef.Number("falloff", 0, 2, 1, 1, "Each further jump deals this fraction of the jump before it (1 = no change, 0.8 = a fifth less each jump)."),
            CharmSettingDef.Number("floor", 0, 2, 0, 0, "The lowest a jump can fall to through the falloff, as a share of a normal hit (0.125 = 12.5 pct). 0 = no floor. Only matters with falloff below 1."),
            CharmSettingDef.Number("chance", 0, 1, 1, 1, "Chance a spell that hits starts a chain (1 = always)."),
            CharmSettingDef.Number("revisitchance", 0, 1, 0, 0.2, "Chance a chain that would end (no fresh monster in reach) jumps BACK to a monster it already hit - never the one it just left (0.05 = 5 pct).").Sched(steps: "25:0.05,50:0.05,75:0.05,100:0.05"),
            CharmSettingDef.Switch("revisit", false, "A chain may come back to a monster it has already hit - never the same monster twice in a row. Off = every monster once."),
            CharmSettingDef.Switch("los", true, "A jump needs a clear line from the monster just hit to the next one."),
            CharmSettingDef.Switch("arcs", true, "Arc spells chain."),
            CharmSettingDef.Switch("bolts", true, "Bolt spells chain."),
            CharmSettingDef.Switch("streaks", true, "Streak spells chain."),
            CharmSettingDef.Switch("onresist", false, "A chain goes on from a monster that resisted the hit."),
            CharmSettingDef.Switch("procs", false, "A chained hit can fire the weapon's cast-on-strike spells."),
            CharmSettingDef.Switch("exclusive", true, "While Chain is on, the Fork and Split Cast charms do nothing for war spells (Chain replaces them, never stacks). Off = all three stack."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp),
            CoinBase(), CoinGrowth());

        public static LeveledCharmBlock Pandemic { get; } = new LeveledCharmBlock("pandemic", "Pandemic",
            "Each time a void damage-over-time spell ticks on a monster (not the tick it lands with), it spreads to nearby monsters that do not carry it. A copy lasts only as long as the DoT it came from has left; with generations 1 (the default) copies do not spread again.",
            (b, lv) => $"Each tick of your void damage-over-time spells "
                       + (b.At("chance", lv) >= 0.9995 ? "spreads" : $"has a {Pct(b.At("chance", lv))} chance to spread")
                       + $" the spell to {Things(b.CountAt("targets", lv), "monster")} within {M(b.At("range", lv))} that do not carry it, {Pct(b.At("damage", lv))} as strong. A spread spell lasts as long as the one it came from has left"
                       + (b.CountAt("generations", lv) == 1 ? " and does not spread again." : ", and spreads again.")
                       + (b.At("falloff", lv) < 0.9995 ? $" Each further spread is {Pct(b.At("falloff", lv))} as strong as the one before" + (b.At("floor", lv) > 0.0005 ? $", never below {Pct(b.At("floor", lv))} of a cast." : ".") : "")
                       + (b.At("critchance", lv) > 0.0005 ? $" Your void DoTs have {Pct(b.At("critchance", lv))} more chance to crit." : "")
                       + (b.On("leash") ? " It only spreads between monsters on your radar" + (b.On("leashclear") ? ", and ends on a monster that leaves your radar." : ".") : "")
                       + (b.On("t11only") ? " Needs a T11+ wand in hand at the cast." : ""),
            CharmSettingDef.Number("range", 0, 50, 10, 20, "Metres around the ticking monster in which the DoT can spread.").Sched(every: "5:0.5"),
            CharmSettingDef.Number("chance", 0, 1, 1, 1, "Chance a tick spreads (1 = every tick)."),
            CharmSettingDef.Count("targets", 0, 20, 2, 6, "New monsters one tick of one DoT can infect.").Sched(steps: "25:1,50:1,75:1,100:1"),
            // owner 2026-10-21: a copy is worth a free cast that lasts as long as the cast DoT has left (hours with duration
            // augs), so copies start at a quarter of a cast and reach half at level 100
            CharmSettingDef.Number("damage", 0, 2, 0.25, 0.5, "Strength of a spread copy as a fraction of a cast DoT (1 = the same)."),
            CharmSettingDef.Number("falloff", 0, 1, 1, 1, "Each generation of copies after the first deals this share of the generation before (1 = all the same, 0.5 = half each hop)."),
            CharmSettingDef.Number("floor", 0, 2, 0, 0, "The lowest a copy can fall to through the falloff, as a share of a cast DoT (0.125 = 12.5 pct). 0 = no floor. Only matters with falloff below 1."),
            // owner 2026-10-21: only the CAST DoT spreads - copies never spread again (no chain reaction through a pack)
            CharmSettingDef.FlatCount("generations", 0, 100, 1, "How many steps a DoT can travel from the monster it was cast on (1 = only the cast one spreads, 0 = no limit)."),
            CharmSettingDef.Number("critchance", 0, 1, 0, 0.2, "Extra crit chance on every tick of your void DoTs (cast and spread) while the charm is on, added to your own (0.05 = +5 pct).").Sched(steps: "25:0.05,50:0.05,75:0.05,100:0.05"),
            // owner 2026-10-20: the total matches the new monsters a tick - 2 at level 1, 6 at level 100 (as Chain / Split / Cleave)
            CharmSettingDef.Count("maxinfected", 0, 500, 2, 6, "Most monsters that can carry one caster's spread copies at the same time (0 = no limit).").Sched(steps: "25:1,50:1,75:1,100:1"),
            CharmSettingDef.Switch("corrosion", true, "Corrosion spreads."),
            CharmSettingDef.Switch("corruption", true, "Corruption spreads."),
            CharmSettingDef.Switch("curse", true, "Destructive Curse spreads (its damage cut travels with it)."),
            CharmSettingDef.Switch("los", true, "A spread needs a clear line between the two monsters."),
            CharmSettingDef.Switch("leash", false, "Both monsters must be on the caster's radar (25 m indoors, 75 m outdoors, and in the caster's view). Off = it spreads wherever the caster is."),
            CharmSettingDef.Switch("leashclear", true, "With leash on: a monster off the caster's radar loses the caster's DoT at its next tick (no more damage). Off = the DoT keeps ticking there, it only stops spreading."),
            CharmSettingDef.Switch("resist", false, "A monster can resist a spread with its magic defense (no message)."),
            CharmSettingDef.Switch("landing", true, "A spread copy deals one tick the moment it lands, like a hand cast (owner 2026-10-17: on by default)."),
            CharmSettingDef.Switch("spreadonland", true, "The CAST DoT's first (landing) tick spreads too. A spread copy spreads from its next tick (owner 2026-10-17)."),
            CharmSettingDef.Switch("aggro", true, "A monster that catches the DoT comes for the caster. Off = it stays where it is and dies there."),
            CharmSettingDef.Switch("message", true, "Tell the caster when a DoT spreads (at most one line every 5 seconds)."),
            CharmSettingDef.Switch("exclusive", true, "While Pandemic is on, the Fork and Split Cast charms do nothing for void DoT spells (Pandemic replaces them, never stacks). Off = all three stack."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp),
            CoinBase(), CoinGrowth());

        public static LeveledCharmBlock ImprovedCleave { get; } = new LeveledCharmBlock("improvedcleave", "Improved Cleave",
            "Two-handed cleave swings wider and hits more monsters: the charm's extra targets are added to the weapon's own cleave (held to maxtargets), and the arc opens up to all around. Two-handed weapons only (twohandedonly).",
            (b, lv) => $"Your {Gear(b, b.On("twohandedonly") ? "two-handed weapons" : "melee weapons")} cleave {Things(b.CountAt("targets", lv), "more monster")} on top of their own cleave"
                       + (b.CountAt("maxtargets", lv) > 0 ? $" (at most {b.CountAt("maxtargets", lv)} in all)" : "")
                       + $", within {M(b.At("range", lv))}, "
                       + (b.At("angle", lv) >= 359.5 ? "all around you." : $"in a {b.At("angle", lv):0} degree arc in front of you."),
            // owner 2026-10-21 (Schneebly): the two-hander is the pack clearer - added targets, a wider arc, full damage,
            // and melee reach (2 m -> 5 m), not the 10-15 m that made cleave hit half the room
            CharmSettingDef.Count("targets", 0, 10, 1, 5, "Extra monsters a swing cleaves ON TOP of the weapon's own cleave.").Sched(steps: "25:1,50:1,75:1,100:1"),
            CharmSettingDef.FlatCount("maxtargets", 0, 20, 10, "Most extra monsters a swing can cleave in all, the weapon's own included (0 = no cap; 10 = a T25 Cleave card's best)."),
            CharmSettingDef.Number("range", 0, 20, 2, 5, "Metres from the attacker, edge to edge, in which a monster is cleaved (2 = the cleave everyone has)."),
            CharmSettingDef.Number("angle", 0, 360, 200, 360, "Arc of the cleave in degrees, centred on where the attacker faces (180 = the front half, the cleave everybody has; 360 = all around)."),
            // owner 2026-10-14: "Add a 50% damage setting to cleave, use it as a tuning tool" - like Chain's jumps and Split Arrow's arrows
            // (2026-10-21: full damage - a two-hander's own cleave always hits for the whole hit, so the charm's do too)
            CharmSettingDef.Number("damage", 0, 2, 1, double.NaN, "Damage of a cleaved hit the charm adds, as a fraction of a normal hit (0.5 = half). The main target and the weapon's own cleave always take the full hit."),
            CharmSettingDef.Switch("twohandedonly", true, "Only two-handed weapons get the charm. Off = every T11+ melee weapon (dual wield and sword and shield too)."),
            CharmSettingDef.Switch("los", true, "A cleaved monster must be in sight (no cleaving through walls)."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp),
            CoinBase(), CoinGrowth());

        // owner 2026-10-21 (Schneebly): dual wield is the BOSS style - more single-target damage, no pack damage. A share of
        // the armor LEFT after the weapon's own rend, so it is worth about the same on every weapon and tier and can never
        // reach 100 pct armor ignored (where the armor curve jumps to x17.5): ~x1.2 at level 1, ~x2.2-2.5 at level 100.
        // Must sit ABOVE LeveledBlocks: static initialisers run in the order they are written.
        public static LeveledCharmBlock ArmorBreaker { get; } = new LeveledCharmBlock("armorbreaker", "Armor Breaker",
            "Dual wield only: every hit ignores a share of the armor a monster has left after the weapon's own Armor Rend. While it is on, the dual wielder's swings do not cleave (nocleave).",
            (b, lv) => $"While dual wielding {Gear(b, "melee weapons")}, every hit ignores {Pct(b.At("share", lv))} of the armor a monster has left after your weapon's own Armor Rend."
                       + (b.On("nocleave") ? " Your swings do not cleave while it is on." : ""),
            CharmSettingDef.Number("share", 0, 0.95, 0.20, 0.70, "Share of the armor LEFT after the weapon's Armor Rend that a hit ignores (0.2 = a fifth). Effective rend = rend + share x (1 - rend)."),
            CharmSettingDef.Switch("nocleave", true, "A dual wielder with the charm on does not cleave at all (the weapons' own cleave, Cleave cards and Improved Cleave). Off = cleave works as normal."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp),
            CoinBase(), CoinGrowth());

        // owner 2026-10-21 (Schneebly): sword and shield is the SURVIVOR - the shield already guards the front half; the
        // charm turns it to cover the sides and, at level 100, every direction. Melee and missile hits only: a shield's
        // spell absorb keeps its own 180 degree rule.
        public static LeveledCharmBlock ShieldArc { get; } = new LeveledCharmBlock("shieldarc", "Shield Arc",
            "Your shield guards a wider arc against monster melee and missile attacks: from the front half (180 degrees, everybody's shield) up to all around you. Your melee hits on monsters also deal more damage while a shield is in hand.",
            (b, lv) => $"Your {Gear(b, "shield")} guards you against monster attacks "
                       + (b.At("angle", lv) >= 359.5 ? "from every direction" : $"in a {b.At("angle", lv):0} degree arc in front of you")
                       + (b.At("damage", lv) > 0 ? $", and your melee hits on monsters deal {Pct(b.At("damage", lv))} more damage." : "."),
            CharmSettingDef.Number("angle", 0, 360, 200, 360, "Arc in degrees, centred on where you face, in which the shield works against a monster's melee or missile hit (180 = everybody's shield; 360 = all around)."),
            // owner 2026-10-22: one-hander and shield cleared a T15 pack in ~135 s against ~33 s for a two-hander - the
            // shield style's own damage bonus, so it can be tuned without moving every melee style's zone dial
            CharmSettingDef.Number("damage", 0, 5, 0.35, 1.35, "Extra damage of a melee hit on a monster while a shield is in hand, as a fraction (0.5 = +50 pct). 0 = none."),
            CharmSettingDef.Switch("t11only", true, "Needs a T11+ shield in hand. Off = any shield."),
            CoinBase(), CoinGrowth());

        /// <summary>The levelled charms by their /charm name. The older charms keep their own blocks and switch tables.</summary>
        public static readonly IReadOnlyDictionary<string, LeveledCharmBlock> LeveledBlocks = new Dictionary<string, LeveledCharmBlock>(StringComparer.Ordinal)
        {
            { SplitArrow.Name, SplitArrow },
            { Chain.Name, Chain },
            { Pandemic.Name, Pandemic },
            { ImprovedCleave.Name, ImprovedCleave },
            { ArmorBreaker.Name, ArmorBreaker },
            { ShieldArc.Name, ShieldArc },
        };

        // words for the player texts (plain ASCII)
        private static string Gear(LeveledCharmBlock b, string what) => b.On("t11only") ? "T11+ " + what : what;
        private static string Things(int n, string one) => n == 1 ? "1 " + one : n.ToString(CultureInfo.InvariantCulture) + " " + one + "s";
        private static string M(double metres) => metres.ToString("0.#", CultureInfo.InvariantCulture) + " m";
        private static string Pct(double fraction) => Math.Round(fraction * 100).ToString(CultureInfo.InvariantCulture) + "%";
        private static string Shapes(LeveledCharmBlock b)
        {
            var names = new List<string>();
            if (b.On("arcs")) names.Add("arcs");
            if (b.On("bolts")) names.Add("bolts");
            if (b.On("streaks")) names.Add("streaks");
            if (names.Count == 0) return "spells";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }

        /// <summary>The player text of the levelled charm that grants this ability at this level; null when the ability
        /// is not a levelled charm's.</summary>
        public static string PlayerTextFor(int abilityId, int level)
        {
            foreach (var block in LeveledBlocks.Values)
                if (LeveledAbilityId(block.Name) == abilityId) return block.PlayerText(level);
            return null;
        }

        /// <summary>The levelled charm block that grants this ability; null when the ability is not a levelled charm's.</summary>
        public static LeveledCharmBlock LeveledBlockFor(int abilityId)
        {
            foreach (var block in LeveledBlocks.Values)
                if (LeveledAbilityId(block.Name) == abilityId) return block;
            return null;
        }

        private static LeveledCharmBlock Leveled(string charmName) =>
            charmName != null && LeveledBlocks.TryGetValue(charmName, out var block) ? block : null;

        public static string DescribeAtLevel(string charmName, int level) => Leveled(charmName)?.DescribeAt(level) ?? "";

        /// <summary>The ability id a levelled charm's /charm name stands for, 0 when the name is not one.</summary>
        public static int LeveledAbilityId(string charmName) => charmName switch
        {
            "splitarrow" => CharmAbilityRegistry.SplitArrowAbilityId,
            "chain" => CharmAbilityRegistry.ChainAbilityId,
            "pandemic" => CharmAbilityRegistry.PandemicAbilityId,
            "improvedcleave" => CharmAbilityRegistry.ImprovedCleaveAbilityId,
            "armorbreaker" => CharmAbilityRegistry.ArmorBreakerAbilityId,
            "shieldarc" => CharmAbilityRegistry.ShieldArcAbilityId,
            _ => 0,
        };

        /// <summary>Every charm's settings as plugin wire lines: the levelled ones with their limits, the older ones as
        /// plain key = value text.</summary>
        public static IEnumerable<string> WireLines()
        {
            foreach (var block in LeveledBlocks.Values) yield return block.WireLine();
            foreach (var (name, label, block) in OlderBlocks())
            {
                var sb = new StringBuilder();
                sb.Append("[[ZCCH]]").Append(name).Append('|').Append(label).Append("|0");
                foreach (var (key, value) in block.GetAllRaw())
                    sb.Append('|').Append(key).Append("=t~").Append(value).Append("~~~~0");
                yield return sb.ToString();
            }
        }

        /// <summary>One charm's wire line (after a change), null when the name is unknown.</summary>
        public static string WireLine(string charmName)
        {
            var leveled = Leveled(charmName);
            if (leveled != null) return leveled.WireLine();
            foreach (var (name, label, block) in OlderBlocks())
            {
                if (name != charmName) continue;
                var sb = new StringBuilder();
                sb.Append("[[ZCCH]]").Append(name).Append('|').Append(label).Append("|0");
                foreach (var (key, value) in block.GetAllRaw())
                    sb.Append('|').Append(key).Append("=t~").Append(value).Append("~~~~0");
                return sb.ToString();
            }
            return null;
        }

        private static IEnumerable<(string name, string label, ICharmBlock block)> OlderBlocks()
        {
            yield return ("manabarrier", "Mana Barrier", ManaBarrier);
            yield return ("explosivearrow", "Explosive Arrow", ExplosiveArrow);
            yield return ("shrapnel", "Rocky Shrapnel", Shrapnel);
            yield return ("agony", "Ring of Agony", Agony);
            yield return ("splitcast", "Split Cast", SplitCast);
            yield return ("omnistrike", "Omni Strike", Omnistrike);
            yield return ("fork", "Fork", Fork);
            yield return ("autorebuff", "Auto-Rebuff", AutoRebuff);
            yield return ("infinitecasting", "Infinite Casting", InfiniteCasting);
            yield return ("asheronsfavor", "Asheron's Favor", AsheronsFavor);
            yield return ("artisans", "Artisan's", Artisans);
            yield return ("essencerefill", "Summon Essence Refill", EssenceRefill);
            yield return ("universalsummoning", "Universal Summoning", UniversalSummoning);
        }
    }
}
