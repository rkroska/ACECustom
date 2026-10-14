using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// The levelled charms (owner 2026-10-13: "Tiers or Levels of these charms will go from 1 to 100"): Split Arrow, Chain,
    /// Pandemic, Improved Cleave. One item per charm; its CharmLevel is 1-100.
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

            public CharmSettingDef(string key, CharmSettingKind kind, bool levels, double min, double max, double def, double def100, string help)
            {
                Key = key; Kind = kind; Levels = levels; Min = min; Max = max; Default = def; Default100 = def100; Help = help;
            }

            public static CharmSettingDef Switch(string key, bool def, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Bool, false, 0, 1, def ? 1 : 0, double.NaN, help);
            public static CharmSettingDef Count(string key, int min, int max, int def, int def100, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Int, true, min, max, def, def100, help);
            public static CharmSettingDef FlatCount(string key, int min, int max, int def, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Int, false, min, max, def, double.NaN, help);
            public static CharmSettingDef Number(string key, double min, double max, double def, double def100, string help) =>
                new CharmSettingDef(key, CharmSettingKind.Float, true, min, max, def, def100, help);
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

            public LeveledCharmBlock(string name, string displayName, string summary, Func<LeveledCharmBlock, int, string> playerText, params CharmSettingDef[] defs)
            {
                Name = name; DisplayName = displayName; Summary = summary; this.playerText = playerText;
                var all = new List<CharmSettingDef> { CharmSettingDef.Switch("enabled", true, "The whole charm on / off for everybody.") };
                all.AddRange(defs);
                Defs = all.ToArray();
                at1 = new double[Defs.Length];
                at100 = new double[Defs.Length];
                for (var i = 0; i < Defs.Length; i++) index[Defs[i].Key] = i;
                Reset();
            }

            // ── reads (combat) ────────────────────────────────────────────────

            public bool Enabled => On("enabled");

            public bool On(string key) => index.TryGetValue(key, out var i) && at1[i] >= 0.5;

            /// <summary>The value of a setting at a charm level (1-100): a straight line from the level-1 value to the
            /// level-100 value, inside the setting's limits. A level below 1 counts as 1.</summary>
            public double At(string key, int level)
            {
                if (!index.TryGetValue(key, out var i)) return 0;
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

            // ── ICharmBlock ───────────────────────────────────────────────────

            public void Reset()
            {
                for (var i = 0; i < Defs.Length; i++)
                {
                    at1[i] = Defs[i].Default;
                    at100[i] = Defs[i].Default100;
                }
            }

            public string TrySet(string key, string value)
            {
                key = (key ?? "").ToLowerInvariant();
                if (key is "on" or "off" or "true" or "false") { value = key; key = "enabled"; }

                var hundred = key.EndsWith(Suffix100, StringComparison.Ordinal);
                var baseKey = hundred ? key.Substring(0, key.Length - Suffix100.Length) : key;
                if (!index.TryGetValue(baseKey, out var i)) return null;
                var d = Defs[i];
                if (hundred && !d.Levels) return $"{Name}.{baseKey} has no level-100 value - it is the same at every level.";

                value = (value ?? "").Trim();
                if (hundred && (value == "" || value == "-" || value.Equals("clear", StringComparison.OrdinalIgnoreCase)))
                {
                    at100[i] = double.NaN;
                    return $"{Name}.{key} cleared - {baseKey} is {Show(d, at1[i])} at every level";
                }

                if (!Parse(d, value, out var v, out var why)) return why;
                if (hundred) at100[i] = v; else at1[i] = v;
                return $"{Name}.{key} = {Show(d, v)}" + (d.Levels ? $"  (level 1: {Show(d, at1[i])}, level 100: {(double.IsNaN(at100[i]) ? "same" : Show(d, at100[i]))})" : "");
            }

            public void ApplyRaw(string key, string value)
            {
                key = (key ?? "").ToLowerInvariant();
                var hundred = key.EndsWith(Suffix100, StringComparison.Ordinal);
                var baseKey = hundred ? key.Substring(0, key.Length - Suffix100.Length) : key;
                if (!index.TryGetValue(baseKey, out var i)) return;
                var d = Defs[i];
                if (hundred && !d.Levels) return;
                value = (value ?? "").Trim();
                if (hundred && value == "") { at100[i] = double.NaN; return; }
                if (!Parse(d, value, out var v, out _)) return;
                if (hundred) at100[i] = v; else at1[i] = v;
            }

            public string GetRaw(string key)
            {
                key = (key ?? "").ToLowerInvariant();
                var hundred = key.EndsWith(Suffix100, StringComparison.Ordinal);
                var baseKey = hundred ? key.Substring(0, key.Length - Suffix100.Length) : key;
                if (!index.TryGetValue(baseKey, out var i)) return null;
                var d = Defs[i];
                if (hundred) return d.Levels && !double.IsNaN(at100[i]) ? Raw(d, at100[i]) : "";
                return Raw(d, at1[i]);
            }

            public IEnumerable<(string key, string value)> GetAllRaw()
            {
                for (var i = 0; i < Defs.Length; i++)
                {
                    yield return (Defs[i].Key, Raw(Defs[i], at1[i]));
                    if (Defs[i].Levels) yield return (Defs[i].Key + Suffix100, double.IsNaN(at100[i]) ? "" : Raw(Defs[i], at100[i]));
                }
            }

            public string Help()
            {
                var sb = new StringBuilder();
                sb.Append($"[{DisplayName}] {Summary}\n");
                sb.Append("Adjustable settings (a number has a level-1 value 'key' and a level-100 value 'key_100'; levels between are a straight line):\n");
                foreach (var d in Defs)
                {
                    var kind = d.Kind == CharmSettingKind.Bool ? "on / off" : d.Kind == CharmSettingKind.Int ? $"whole number {F0(d.Min)} to {F0(d.Max)}" : $"number {F0(d.Min)} to {F0(d.Max)}";
                    sb.Append($"  • {d.Key}{(d.Levels ? " / " + d.Key + Suffix100 : "")}  ({kind}) — {d.Help}\n");
                }
                sb.Append("\n[Examples]\n");
                sb.Append($"  • /charm {Name} off\n");
                foreach (var d in Defs)
                    if (d.Levels)
                    {
                        sb.Append($"  • /charm {Name} {d.Key} {Raw(d, d.Default)}\n");
                        sb.Append($"  • /charm {Name} {d.Key}{Suffix100} {Raw(d, double.IsNaN(d.Default100) ? d.Default : d.Default100)}   (or 'clear' = same at every level)\n");
                        break;
                    }
                sb.Append($"  • /charm level {Name} 50   — set the level of the {DisplayName} charm you carry");
                return sb.ToString();
            }

            public string Dump()
            {
                var sb = new StringBuilder();
                sb.Append($"[{DisplayName}] Current Settings\n");
                for (var i = 0; i < Defs.Length; i++)
                {
                    var d = Defs[i];
                    if (!d.Levels) sb.Append($"  • {d.Key}: {Show(d, at1[i])}\n");
                    else sb.Append($"  • {d.Key}: level 1 = {Show(d, at1[i])}, level 100 = {(double.IsNaN(at100[i]) ? "same" : Show(d, at100[i]))}, level 50 = {ShowAt(d, 50)}\n");
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

            // ── the plugin's wire line ────────────────────────────────────────

            /// <summary>One line for the client plugin: name | label | key=kind~v1~v100~min~max~levels ... (v100 empty =
            /// unset). Help text travels separately (HelpLines) because it never changes.</summary>
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
                      .Append(F0(d.Min)).Append('~').Append(F0(d.Max)).Append('~').Append(d.Levels ? '1' : '0');
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

            // ── helpers ───────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────────
        //  The four levelled charms and their settings
        // ─────────────────────────────────────────────────────────────────────

        private const string T11OnlyHelp = "Needs a T11+ weapon of the right kind in hand, where its power is live. Off = any weapon, anywhere.";

        public static LeveledCharmBlock SplitArrow { get; } = new LeveledCharmBlock("splitarrow", "Split Arrow",
            "A missile launcher fires extra arrows at monsters near the main target. The higher of the charm's and the launcher's own split numbers is used, never the sum.",
            (b, lv) => $"Your {Gear(b, "missile launchers")} fire {Things(b.CountAt("arrows", lv), "extra arrow")} at monsters within {M(b.At("range", lv))} of your target, each for {Pct(b.At("damage", lv))} of a normal hit. A launcher with split arrows of its own uses the higher numbers.",
            CharmSettingDef.Count("arrows", 0, 10, 1, 5, "Extra arrows a shot (one per nearby monster)."),
            CharmSettingDef.Number("range", 0, 50, 8, 8, "Metres around the MAIN TARGET in which the extra arrows find monsters."),
            CharmSettingDef.Number("damage", 0, 1, 0.5, 0.5, "Damage of an extra arrow as a fraction of a normal hit (0.5 = half)."),
            CharmSettingDef.Number("angle", 0, 180, 60, 60, "How far to either side of the line to the main target, in degrees seen from the archer, a monster may stand (60 = the split arrows everyone has; 180 = all around)."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp));

        public static LeveledCharmBlock Chain { get; } = new LeveledCharmBlock("chain", "Chain",
            "A hand-cast war arc, bolt or streak that hits a monster jumps on to the nearest other monster, then the next. One monster after another, each jump from the last monster hit.",
            (b, lv) => $"Your war {Shapes(b)} jump on to {Things(b.CountAt("jumps", lv), "more monster")}, each within {M(b.At("range", lv))} of the one just hit. The first jump deals {Pct(b.At("damage", lv))} of a normal hit"
                       + (Math.Abs(b.At("falloff", lv) - 1) < 0.0005 ? "." : $", each further jump {Pct(b.At("falloff", lv))} of the one before.")
                       + (b.On("t11only") ? " Needs a T11+ wand in hand." : ""),
            CharmSettingDef.Count("jumps", 0, 20, 1, 4, "Jumps after the first hit."),
            CharmSettingDef.Number("range", 0, 50, 8, 8, "Metres a jump can cover, from the monster just hit."),
            CharmSettingDef.Number("damage", 0, 2, 0.5, 0.5, "Damage of the first jump as a fraction of a normal hit."),
            CharmSettingDef.Number("falloff", 0, 2, 0.8, 0.8, "Each further jump deals this fraction of the jump before it (1 = no change, 0.8 = a fifth less each jump)."),
            CharmSettingDef.Number("chance", 0, 1, 1, 1, "Chance a spell that hits starts a chain (1 = always)."),
            CharmSettingDef.Switch("revisit", false, "A chain may come back to a monster it has already hit - never the same monster twice in a row. Off = every monster once."),
            CharmSettingDef.Switch("los", true, "A jump needs a clear line from the monster just hit to the next one."),
            CharmSettingDef.Switch("arcs", true, "Arc spells chain."),
            CharmSettingDef.Switch("bolts", true, "Bolt spells chain."),
            CharmSettingDef.Switch("streaks", true, "Streak spells chain."),
            CharmSettingDef.Switch("onresist", false, "A chain goes on from a monster that resisted the hit."),
            CharmSettingDef.Switch("procs", false, "A chained hit can fire the weapon's cast-on-strike spells."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp));

        public static LeveledCharmBlock Pandemic { get; } = new LeveledCharmBlock("pandemic", "Pandemic",
            "Each time a void damage-over-time spell ticks on a monster (not the tick it lands with), it spreads to nearby monsters that do not carry it. A copy lasts only as long as the DoT it came from has left, and copies spread too.",
            (b, lv) => $"Each tick of your void damage-over-time spells "
                       + (b.At("chance", lv) >= 0.9995 ? "spreads" : $"has a {Pct(b.At("chance", lv))} chance to spread")
                       + $" the spell to {Things(b.CountAt("targets", lv), "monster")} within {M(b.At("range", lv))} that do not carry it. A spread spell lasts as long as the one it came from has left, and spreads again."
                       + (b.On("t11only") ? " Needs a T11+ wand in hand at the cast." : ""),
            CharmSettingDef.Number("range", 0, 50, 5, 10, "Metres around the ticking monster in which the DoT can spread."),
            CharmSettingDef.Number("chance", 0, 1, 1, 1, "Chance a tick spreads (1 = every tick)."),
            CharmSettingDef.Count("targets", 0, 20, 1, 3, "New monsters one tick of one DoT can infect."),
            CharmSettingDef.Number("damage", 0, 2, 1, 1, "Strength of a spread copy as a fraction of a cast DoT (1 = the same)."),
            CharmSettingDef.FlatCount("generations", 0, 100, 0, "How many steps a DoT can travel from the monster it was cast on (1 = only the cast one spreads, 0 = no limit)."),
            CharmSettingDef.Count("maxinfected", 0, 500, 30, 30, "Most monsters that can carry one caster's spread copies at the same time (0 = no limit)."),
            CharmSettingDef.Switch("corrosion", true, "Corrosion spreads."),
            CharmSettingDef.Switch("corruption", true, "Corruption spreads."),
            CharmSettingDef.Switch("curse", true, "Destructive Curse spreads (its damage cut travels with it)."),
            CharmSettingDef.Switch("los", true, "A spread needs a clear line between the two monsters."),
            CharmSettingDef.Switch("resist", false, "A monster can resist a spread with its magic defense (no message)."),
            CharmSettingDef.Switch("landing", false, "A spread copy deals one tick the moment it lands."),
            CharmSettingDef.Switch("aggro", true, "A monster that catches the DoT comes for the caster. Off = it stays where it is and dies there."),
            CharmSettingDef.Switch("message", true, "Tell the caster when a DoT spreads (at most one line every 5 seconds)."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp));

        public static LeveledCharmBlock ImprovedCleave { get; } = new LeveledCharmBlock("improvedcleave", "Improved Cleave",
            "Melee cleave reaches further and all around. A weapon without cleave gets the charm's extra targets; a weapon that cleaves uses the higher of its own and the charm's, never the sum.",
            (b, lv) => $"Your {Gear(b, "melee weapons")} cleave {Things(b.CountAt("targets", lv), "extra monster")} within {M(b.At("range", lv))}, "
                       + (b.At("angle", lv) >= 359.5 ? "all around you" : $"in a {b.At("angle", lv):0} degree arc in front of you")
                       + ". A weapon that already cleaves more monsters keeps its own number.",
            CharmSettingDef.Count("targets", 0, 10, 2, 4, "Extra monsters a swing cleaves (besides the main target)."),
            CharmSettingDef.Number("range", 0, 20, 3, 5, "Metres from the attacker, edge to edge, in which a monster is cleaved (2 = the cleave everyone has)."),
            CharmSettingDef.Number("angle", 0, 360, 360, 360, "Arc of the cleave in degrees, centred on where the attacker faces (180 = the front half, 360 = all around)."),
            CharmSettingDef.Switch("los", true, "A cleaved monster must be in sight (no cleaving through walls)."),
            CharmSettingDef.Switch("t11only", true, T11OnlyHelp));

        /// <summary>The levelled charms by their /charm name. The older charms keep their own blocks and switch tables.</summary>
        public static readonly IReadOnlyDictionary<string, LeveledCharmBlock> LeveledBlocks = new Dictionary<string, LeveledCharmBlock>(StringComparer.Ordinal)
        {
            { SplitArrow.Name, SplitArrow },
            { Chain.Name, Chain },
            { Pandemic.Name, Pandemic },
            { ImprovedCleave.Name, ImprovedCleave },
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
