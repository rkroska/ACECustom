using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Blacksmithing math (design settled with the owner 2026-09-27/28). Two actions:
    ///
    /// FORGE - two loot weapons of the same forge group (ForgeGroups: same loot tables) become one. The MAIN weapon (placed first)
    /// always keeps its hone levels, misfortune count, tinkers, imbue and dye; the FEEDER only lends stats.
    /// Every inherited value is an independent roll that takes the better parent's value with
    /// <see cref="ForgeConfig.HigherParentChance"/> (0.55), ties counting the main weapon as better. A value
    /// only one parent has counts as the better one, so it survives 55% of the time. forge_roll_mode can instead
    /// blend numeric values between the two weapons (see <see cref="RollMode"/>). Requirements and tier
    /// are never rolled: the result takes the stricter of each. The element is a plain 50/50 between the two (neither
    /// is better). A small spark chance adds a free hone level.
    ///
    /// HONE - spend luminance on one weapon for +1 level on a line the player picks. Failure costs only the
    /// luminance and adds misfortune (a bonus to the next try, reset on success). Cost and chance are keyed
    /// to the weapon's TOTAL hone level, capped at <see cref="ForgeConfig.HoneMaxLevels"/>.
    ///
    /// Everything here is side-effect free and deterministic for a given draw source, so the live forge,
    /// the unit tests and the website simulator all run the same rules. Draw order is part of the contract:
    ///   forge  1. one draw per line, in <see cref="InheritOrder"/> (always consumed, even when both parents
    ///             lack the line, so replays line up)
    ///          2. quality
    ///          3. one per Zone Control grade key, ascending, over the union of both parents' keys
    ///          4. one per spell family, ascending, over the union
    ///          5. one per trait package key, ascending, over the union
    ///          6. element: 50/50, always consumed (even when both elements match)
    ///          7. spark roll; when it hits and the weapon is under the hone cap, one more draw picks the line
    ///   hone   1. the success roll
    /// </summary>
    public static class ForgeMath
    {
        /// <summary>Stat lines. The numeric order is the inheritance draw order and the spark pick order.</summary>
        public enum ForgeLine
        {
            MaxDamage = 1,
            Variance = 2,
            Speed = 3,
            AttackMod = 4,
            MeleeDefense = 5,
            MissileDefense = 6,
            MagicDefense = 7,
            /// <summary>Elemental damage mod on casters, damage mod on missile launchers.</summary>
            DamageMod = 8,
            Spellcraft = 9,
            MaxMana = 10,

            // Armour and shields (2026-10-04). Stored in hone records and logs like the rest: never renumber.
            ArmorLevel = 20,
            ProtSlash = 21,
            ProtPierce = 22,
            ProtBludgeon = 23,
            ProtCold = 24,
            ProtFire = 25,
            ProtAcid = 26,
            ProtElectric = 27,
            ProtNether = 28,
            RatingDamage = 30,
            RatingDamageResist = 31,
            RatingCrit = 32,
            RatingCritResist = 33,
            RatingCritDamage = 34,
            RatingCritDamageResist = 35,
            RatingHealingBoost = 36,
            RatingNetherResist = 37,
            RatingLifeResist = 38,
            RatingMaxHealth = 39,
        }

        /// <summary>The stat lines of a weapon, in the order the forge rolls them. Fixed: a logged forge replays by draw order.</summary>
        /// <summary>ImbuedEffectType bits of the eight elemental rends (slash, pierce, bludgeon, acid, cold, electric, fire, nether).</summary>
        public const int RendMask = 0x0008 | 0x0010 | 0x0020 | 0x0040 | 0x0080 | 0x0100 | 0x0200 | 0x4000;

        /// <summary>True when two weapons may be forged as far as rends go: at most one carries a rend, or both carry the same.</summary>
        public static bool RendsCompatible(int mainRend, int feederRend) => mainRend == 0 || feederRend == 0 || mainRend == feederRend;

        public static readonly ForgeLine[] InheritOrder =
        {
            ForgeLine.MaxDamage, ForgeLine.Variance, ForgeLine.Speed, ForgeLine.AttackMod, ForgeLine.MeleeDefense,
            ForgeLine.MissileDefense, ForgeLine.MagicDefense, ForgeLine.DamageMod, ForgeLine.Spellcraft, ForgeLine.MaxMana,
        };

        /// <summary>The stat lines of a piece of armour or a shield, in the order the forge rolls them.</summary>
        public static readonly ForgeLine[] ArmorOrder =
        {
            ForgeLine.ArmorLevel, ForgeLine.ProtSlash, ForgeLine.ProtPierce, ForgeLine.ProtBludgeon, ForgeLine.ProtCold,
            ForgeLine.ProtFire, ForgeLine.ProtAcid, ForgeLine.ProtElectric, ForgeLine.ProtNether,
            ForgeLine.RatingDamage, ForgeLine.RatingDamageResist, ForgeLine.RatingCrit, ForgeLine.RatingCritResist,
            ForgeLine.RatingCritDamage, ForgeLine.RatingCritDamageResist, ForgeLine.RatingHealingBoost,
            ForgeLine.RatingNetherResist, ForgeLine.RatingLifeResist, ForgeLine.RatingMaxHealth,
            ForgeLine.Spellcraft, ForgeLine.MaxMana,
        };

        public static ForgeLine[] OrderFor(bool isArmor) => isArmor ? ArmorOrder : InheritOrder;

        /// <summary>Lines honing (and the forge spark) can raise. Spellcraft and mana are inherit-only.</summary>
        public static readonly ForgeLine[] HonableLines =
        {
            ForgeLine.MaxDamage, ForgeLine.Variance, ForgeLine.Speed, ForgeLine.AttackMod,
            ForgeLine.MeleeDefense, ForgeLine.MissileDefense, ForgeLine.MagicDefense, ForgeLine.DamageMod,
        };

        public static bool IsHonable(ForgeLine line) => Array.IndexOf(HonableLines, line) >= 0;

        /// <summary>Lines the game stores as integers; a blended roll is rounded to a whole number.</summary>
        public static bool IsWholeNumber(ForgeLine line)
            => line == ForgeLine.MaxDamage || line == ForgeLine.Speed || line == ForgeLine.Spellcraft || line == ForgeLine.MaxMana
               || line == ForgeLine.ArmorLevel || (line >= ForgeLine.RatingDamage && line <= ForgeLine.RatingMaxHealth);

        /// <summary>Variance and weapon time are better when lower; honing reduces them.</summary>
        public static bool LowerIsBetter(ForgeLine line) => line == ForgeLine.Variance || line == ForgeLine.Speed;

        /// <summary>Client-safe (ASCII) display name.</summary>
        public static string LineName(ForgeLine line) => line switch
        {
            ForgeLine.MaxDamage => "Damage",
            ForgeLine.Variance => "Variance",
            ForgeLine.Speed => "Speed",
            ForgeLine.AttackMod => "Attack",
            ForgeLine.MeleeDefense => "Melee Defense",
            ForgeLine.MissileDefense => "Missile Defense",
            ForgeLine.MagicDefense => "Magic Defense",
            ForgeLine.DamageMod => "Damage Modifier",
            ForgeLine.Spellcraft => "Spellcraft",
            ForgeLine.MaxMana => "Mana",
            ForgeLine.ArmorLevel => "Armor Level",
            ForgeLine.ProtSlash => "Slashing Protection",
            ForgeLine.ProtPierce => "Piercing Protection",
            ForgeLine.ProtBludgeon => "Bludgeoning Protection",
            ForgeLine.ProtCold => "Cold Protection",
            ForgeLine.ProtFire => "Fire Protection",
            ForgeLine.ProtAcid => "Acid Protection",
            ForgeLine.ProtElectric => "Lightning Protection",
            ForgeLine.ProtNether => "Nether Protection",
            ForgeLine.RatingDamage => "Damage Rating",
            ForgeLine.RatingDamageResist => "Damage Resist Rating",
            ForgeLine.RatingCrit => "Crit Rating",
            ForgeLine.RatingCritResist => "Crit Resist Rating",
            ForgeLine.RatingCritDamage => "Crit Damage Rating",
            ForgeLine.RatingCritDamageResist => "Crit Damage Resist Rating",
            ForgeLine.RatingHealingBoost => "Healing Boost Rating",
            ForgeLine.RatingNetherResist => "Nether Resist Rating",
            ForgeLine.RatingLifeResist => "Life Resist Rating",
            ForgeLine.RatingMaxHealth => "Vitality",
            _ => "None",
        };

        /// <summary>One spell on a weapon. Family groups the levels of one effect (Blood Drinker V and VIII).</summary>
        public sealed class SpellEntry
        {
            public uint Family;
            public uint SpellId;
            public int Level;
        }

        /// <summary>
        /// A property group that travels whole from one parent: slayer, rending, resistance cleaving,
        /// equipment set. Score decides which parent's is better; Value is opaque to the math.
        /// </summary>
        public sealed class TraitPackage
        {
            public int Key;
            public double Score;
            public string Value;
        }

        /// <summary>The forge-relevant state of one weapon, as read from its biota (tinkers already stripped).</summary>
        public sealed class ForgeWeapon
        {
            public uint Wcid;
            /// <summary>Armour or a shield, not a weapon: it rolls <see cref="ArmorOrder"/> instead of <see cref="InheritOrder"/>.</summary>
            public bool IsArmor;
            /// <summary>ForgeGroups group; only weapons of one group may be forged together.</summary>
            public string Group;
            /// <summary>The weenie's element (DamageType), rolled 50/50 between the two weapons.</summary>
            public int Element;
            public int Tier;
            /// <summary>WeaponAugScaleQuality 0-1000; null below T11. A missing value counts as 0.</summary>
            public int? Quality;
            /// <summary>Base (untinkered, unhoned) value of each line the weapon has.</summary>
            public Dictionary<ForgeLine, double> Lines = new();
            /// <summary>Zone Control record: key -> grade 0-1000, every key the item carries.</summary>
            public SortedDictionary<int, int> ZcGrades = new();
            /// <summary>
            /// Tier 11+ PROPERTIES the forge may keep, drop or take from the other item: the record keys that use one of the
            /// item's property slots (what the appraisal's "Properties: X of Y" counts) and are free to move. A subset of
            /// <see cref="ZcGrades"/>'s keys. Every other record key (the built-in resists, slot specials, a weapon's rend)
            /// stays with the main item.
            /// </summary>
            public SortedSet<int> PoolKeys = new();
            /// <summary>Property slots used by things the forge never moves (Reinforced, a weapon's rend, Cast on Strike, a locked property...).</summary>
            public int FixedSlots;
            /// <summary>Record keys whose grade the forge never changes on the main item: the property a Bag of Locking locked.</summary>
            public SortedSet<int> FrozenKeys = new();
            /// <summary>The tier's limit on properties for an item of this tier; int.MaxValue = no limit.</summary>
            public int PropertyCap = int.MaxValue;
            /// <summary>How many properties the item has, as its appraisal counts them.</summary>
            public int PropertyCount => PoolKeys.Count + FixedSlots;
            /// <summary>Stat lines the forge leaves exactly as the main item has them (tier 11+ armour: its armour level,
            /// protections and ratings belong to its properties, not to a roll).</summary>
            public HashSet<ForgeLine> LockedLines = new();
            /// <summary>Spells keyed by family.</summary>
            public SortedDictionary<uint, SpellEntry> Spells = new();
            public SortedDictionary<int, TraitPackage> Packages = new();
            /// <summary>Wield requirement slot -> difficulty. The result takes the higher of each.</summary>
            public SortedDictionary<int, int> WieldDifficulty = new();

            // Owned by the main weapon: never rolled, never merged.
            public Dictionary<ForgeLine, int> HoneLevels = new();
            public int HoneMisfortune;
            public int TinkerCount;
            public string TinkerLog;
            public int ImbuedEffect;
            /// <summary>
            /// The elemental rends this weapon carries (ImbuedEffectType bits within <see cref="RendMask"/>, over all its
            /// imbue slots); 0 = none. A rend is an imbue, so the result has the MAIN weapon's. Two rules follow from it
            /// (owner, 2026-10-07): two weapons that both carry a rend must carry the same one, and a main weapon with a
            /// rend keeps its own element, so the rend always matches the weapon it ends up on.
            /// </summary>
            public int Rend;
            public int? DyePalette;

            public int ForgeCount;

            public int HoneTotal => HoneLevels.Values.Sum();

            public int HoneLevel(ForgeLine line) => HoneLevels.TryGetValue(line, out var l) ? l : 0;

            public ForgeWeapon Clone()
            {
                return new ForgeWeapon
                {
                    Wcid = Wcid,
                    IsArmor = IsArmor,
                    Group = Group,
                    Element = Element,
                    Tier = Tier,
                    Quality = Quality,
                    Lines = new Dictionary<ForgeLine, double>(Lines),
                    ZcGrades = new SortedDictionary<int, int>(ZcGrades),
                    PoolKeys = new SortedSet<int>(PoolKeys),
                    FixedSlots = FixedSlots,
                    FrozenKeys = new SortedSet<int>(FrozenKeys),
                    PropertyCap = PropertyCap,
                    LockedLines = new HashSet<ForgeLine>(LockedLines),
                    Spells = new SortedDictionary<uint, SpellEntry>(Spells.ToDictionary(kv => kv.Key, kv => new SpellEntry { Family = kv.Value.Family, SpellId = kv.Value.SpellId, Level = kv.Value.Level })),
                    Packages = new SortedDictionary<int, TraitPackage>(Packages.ToDictionary(kv => kv.Key, kv => new TraitPackage { Key = kv.Value.Key, Score = kv.Value.Score, Value = kv.Value.Value })),
                    WieldDifficulty = new SortedDictionary<int, int>(WieldDifficulty),
                    HoneLevels = new Dictionary<ForgeLine, int>(HoneLevels),
                    HoneMisfortune = HoneMisfortune,
                    TinkerCount = TinkerCount,
                    TinkerLog = TinkerLog,
                    ImbuedEffect = ImbuedEffect,
                    Rend = Rend,
                    DyePalette = DyePalette,
                    ForgeCount = ForgeCount,
                };
            }
        }

        /// <summary>Every tunable, field for field from ServerConfig (forge_*). All placeholders until balanced.</summary>
        public sealed class ForgeConfig
        {
            public double HigherParentChance = 0.55;
            /// <summary>How a numeric value both weapons have is rolled. Spells, traits and the element always pick one.</summary>
            public RollMode RollMode = RollMode.Pick;
            public long ForgeFeePyreals;
            public double SparkChance;

            public long HoneBaseCost;
            public double HoneCostGrowth;
            public double HoneBaseChance, HoneChanceStep, HoneMinChance, HoneMisfortuneStep;
            public int HoneMaxLevels;
            /// <summary>Fraction of the line's BASE value added (or, for lower-is-better lines, removed) per level.</summary>
            public Dictionary<ForgeLine, double> HoneStep = new();

            public double UnbindFraction;
            public long UnbindMinFee;

            public double StepFor(ForgeLine line) => HoneStep.TryGetValue(line, out var s) ? s : 0.0;

            /// <summary>
            /// The live per-level hone step of one line, straight from its server setting; 0 for a line that cannot be
            /// honed. The single mapping from a line to its setting: <see cref="FromServerConfig"/> is built from it,
            /// and combat reads it directly so a hit does not build a whole config.
            /// </summary>
            public static double ServerStepFor(ForgeLine line) => line switch
            {
                ForgeLine.MaxDamage => ServerConfig.forge_hone_step_damage.Value,
                ForgeLine.Variance => ServerConfig.forge_hone_step_variance.Value,
                ForgeLine.Speed => ServerConfig.forge_hone_step_speed.Value,
                ForgeLine.AttackMod => ServerConfig.forge_hone_step_attack.Value,
                ForgeLine.MeleeDefense or ForgeLine.MissileDefense or ForgeLine.MagicDefense => ServerConfig.forge_hone_step_defense.Value,
                ForgeLine.DamageMod => ServerConfig.forge_hone_step_damage_mod.Value,
                _ => 0.0,
            };

            public static ForgeConfig FromServerConfig() => new()
            {
                HigherParentChance = ServerConfig.forge_higher_parent_chance.Value,
                RollMode = ParseRollMode(ServerConfig.forge_roll_mode.Value),
                ForgeFeePyreals = ServerConfig.forge_fee_pyreals.Value,
                SparkChance = ServerConfig.forge_spark_chance.Value,
                HoneBaseCost = ServerConfig.forge_hone_base_cost.Value,
                HoneCostGrowth = ServerConfig.forge_hone_cost_growth.Value,
                HoneBaseChance = ServerConfig.forge_hone_base_chance.Value,
                HoneChanceStep = ServerConfig.forge_hone_chance_step.Value,
                HoneMinChance = ServerConfig.forge_hone_min_chance.Value,
                HoneMisfortuneStep = ServerConfig.forge_hone_misfortune_step.Value,
                HoneMaxLevels = (int)ServerConfig.forge_hone_max_levels.Value,
                HoneStep = HonableLines.ToDictionary(l => l, ServerStepFor),
                UnbindFraction = ServerConfig.forge_unbind_fraction.Value,
                UnbindMinFee = ServerConfig.forge_unbind_min_fee.Value,
            };
        }

        /// <summary>
        /// How a numeric value is rolled when both weapons have it (ServerConfig forge_roll_mode):
        ///   Pick       - one of the two values; the better one with HigherParentChance (the original rule).
        ///   Between    - uniformly anywhere between the two. Averages the midpoint, so a better main weapon always
        ///                gives up some of its edge.
        ///   BestOfTwo  - between the two, as the better of two uniform rolls: averages two thirds of the way to the
        ///                better value. Computed from ONE draw as sqrt(roll), which has that exact distribution.
        /// Every mode consumes exactly one draw per value, so the draw order never changes.
        /// </summary>
        public enum RollMode { Pick, Between, BestOfTwo }

        /// <summary>Reads forge_roll_mode: "between", "best-of-two" (or "bestoftwo"), anything else = pick.</summary>
        public static RollMode ParseRollMode(string text)
        {
            var t = (text ?? "").Trim().Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
            return t switch { "between" => RollMode.Between, "bestoftwo" => RollMode.BestOfTwo, _ => RollMode.Pick };
        }

        /// <summary>
        /// Rolls one numeric value both weapons have. Returns the value and, for Pick, whether it is the main weapon's
        /// (for a blended value: whether it lies nearer the main weapon's).
        /// </summary>
        public static double RollNumeric(double mainValue, double feederValue, bool lowerIsBetter, ForgeConfig config, double roll, out bool fromMain)
        {
            var mainBetter = lowerIsBetter ? mainValue <= feederValue : mainValue >= feederValue;
            if (config.RollMode == RollMode.Pick)
            {
                fromMain = roll < config.HigherParentChance ? mainBetter : !mainBetter;
                return fromMain ? mainValue : feederValue;
            }
            var better = mainBetter ? mainValue : feederValue;
            var worse = mainBetter ? feederValue : mainValue;
            var t = config.RollMode == RollMode.BestOfTwo ? Math.Sqrt(roll) : roll;   // 0 = the worse value, 1 = the better
            var value = worse + t * (better - worse);
            fromMain = Math.Abs(value - mainValue) <= Math.Abs(value - feederValue);
            return value;
        }

        public enum PickKind { Line, Quality, Grade, Spell, Package, Element }

        /// <summary>One inheritance decision, kept for the preview, the appraisal and support logs.</summary>
        public sealed class ForgePick
        {
            public PickKind Kind;
            public int Key;
            public string MainValue, FeederValue;
            public bool MainIsBetter;
            public bool FromMain;
            /// <summary>True when the value was blended between the two rather than taken from one (RollMode Between / BestOfTwo).</summary>
            public bool Blended;
            public double Roll;
        }

        public sealed class ForgeOutcome
        {
            public ForgeWeapon Result;
            public List<ForgePick> Picks = new();
            public bool Spark;
            public ForgeLine SparkLine;
            public double SparkRoll;
            /// <summary>Every draw consumed, in order. Replaying them reproduces this outcome exactly.</summary>
            public List<double> RngDraws = new();
            /// <summary>Tier 11+ properties of the result that only the second item had (record keys).</summary>
            public List<int> PropertiesGained = new();
            /// <summary>Tier 11+ properties the main item had that the result does not (record keys).</summary>
            public List<int> PropertiesLost = new();
        }

        /// <summary>
        /// The 55/45 rule. Returns true when the main weapon's value is taken. A value only one side has is
        /// the better one; ties count the main weapon as better. <paramref name="roll"/> is uniform in [0, 1).
        /// </summary>
        public static bool PicksMain(bool mainHas, bool feederHas, double mainValue, double feederValue, bool lowerIsBetter, double higherChance, double roll)
        {
            bool mainBetter;
            if (mainHas != feederHas)
                mainBetter = mainHas;
            else
                mainBetter = lowerIsBetter ? mainValue <= feederValue : mainValue >= feederValue;

            return roll < higherChance ? mainBetter : !mainBetter;
        }

        /// <summary>Chance the result takes the main weapon's value (for the forge preview).</summary>
        public static double ChanceFromMain(bool mainIsBetter, double higherChance) => mainIsBetter ? higherChance : 1.0 - higherChance;

        /// <summary>
        /// Forges <paramref name="main"/> with <paramref name="feeder"/>. Throws when the two are not of one forge
        /// group: the caller must refuse that before charging anything.
        /// </summary>
        public static ForgeOutcome Forge(ForgeWeapon main, ForgeWeapon feeder, ForgeConfig config, Func<double> nextDouble)
        {
            if (main == null) throw new ArgumentNullException(nameof(main));
            if (feeder == null) throw new ArgumentNullException(nameof(feeder));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (nextDouble == null) throw new ArgumentNullException(nameof(nextDouble));
            if (string.IsNullOrEmpty(main.Group) || main.Group != feeder.Group)
                throw new ArgumentException($"Forge inputs must share a forge group ('{main.Group}' vs '{feeder.Group}').");

            var o = new ForgeOutcome();
            double Draw()
            {
                var v = nextDouble();
                o.RngDraws.Add(v);
                return v;
            }

            var p = config.HigherParentChance;

            // Start from the main weapon so everything it owns (hones, tinkers, imbue, dye) carries over
            // untouched, then overwrite every rolled value.
            var r = main.Clone();
            r.Lines.Clear();
            r.ZcGrades.Clear();
            r.PoolKeys.Clear();
            var lockedLines = new HashSet<ForgeLine>(main.LockedLines);
            lockedLines.UnionWith(feeder.LockedLines);
            r.LockedLines = lockedLines;
            r.Spells.Clear();
            r.Packages.Clear();

            // 1. Stat lines.
            foreach (var line in OrderFor(main.IsArmor))
            {
                var roll = Draw();
                var mainHas = main.Lines.TryGetValue(line, out var mv);
                var feederHas = feeder.Lines.TryGetValue(line, out var fv);
                if (!mainHas && !feederHas)
                    continue;
                if (lockedLines.Contains(line))
                {
                    // not the forge's to roll: the main item's value stands (the draw above is still spent)
                    if (mainHas)
                        r.Lines[line] = mv;
                    continue;
                }

                var lower = LowerIsBetter(line);
                bool fromMain;
                if (mainHas && feederHas)
                {
                    var value = RollNumeric(mv, fv, lower, config, roll, out fromMain);
                    r.Lines[line] = IsWholeNumber(line) ? Math.Round(value, MidpointRounding.AwayFromZero) : value;
                }
                else
                {
                    // only one weapon has the line: nothing to blend, so it is kept or lost by the pick rule
                    fromMain = PicksMain(mainHas, feederHas, mv, fv, lower, p, roll);
                    if (fromMain ? mainHas : feederHas)
                        r.Lines[line] = fromMain ? mv : fv;
                }

                o.Picks.Add(new ForgePick
                {
                    Kind = PickKind.Line, Key = (int)line, Roll = roll, FromMain = fromMain,
                    Blended = mainHas && feederHas && config.RollMode != RollMode.Pick,
                    MainIsBetter = mainHas != feederHas ? mainHas : (lower ? mv <= fv : mv >= fv),
                    MainValue = mainHas ? mv.ToString("0.####") : "none", FeederValue = feederHas ? fv.ToString("0.####") : "none",
                });
            }

            // 2. Quality: missing counts as 0 (a T9 parent carries none), so it is always a 0-1000 contest
            // once either parent has one.
            {
                var roll = Draw();
                if (main.Quality.HasValue || feeder.Quality.HasValue)
                {
                    var mq = main.Quality ?? 0;
                    var fq = feeder.Quality ?? 0;
                    r.Quality = (int)Math.Round(RollNumeric(mq, fq, false, config, roll, out var fromMain), MidpointRounding.AwayFromZero);
                    o.Picks.Add(new ForgePick { Kind = PickKind.Quality, Roll = roll, FromMain = fromMain, Blended = config.RollMode != RollMode.Pick, MainIsBetter = mq >= fq, MainValue = mq.ToString(), FeederValue = fq.ToString() });
                }
                else
                    r.Quality = null;
            }

            // 3. Tier 11+ properties (owner, 2026-10-07): the forge is its own gamble, separate from the salvage bags.
            //    Every property either item has goes into one pool; the result gets a random NUMBER of them, anywhere
            //    from the smaller of the two items' counts to the larger (never past the tier's limit), picked at random
            //    from the pool. A property both had rolls its grade between the two; one only one had keeps its grade.
            //    Record keys that are not movable properties stay with the main item, graded between the two when
            //    the second item carries the same key.
            {
                var mainFixed = main.ZcGrades.Keys.Where(k => !main.PoolKeys.Contains(k)).OrderBy(k => k).ToList();
                foreach (var key in mainFixed)
                {
                    var mg = main.ZcGrades[key];
                    if (!main.FrozenKeys.Contains(key) && feeder.ZcGrades.TryGetValue(key, out var fg) && !feeder.PoolKeys.Contains(key))
                    {
                        var roll = Draw();
                        r.ZcGrades[key] = (int)Math.Round(RollNumeric(mg, fg, false, config, roll, out var fromMain), MidpointRounding.AwayFromZero);
                        o.Picks.Add(new ForgePick { Kind = PickKind.Grade, Key = key, Roll = roll, FromMain = fromMain, Blended = config.RollMode != RollMode.Pick, MainIsBetter = mg >= fg, MainValue = mg.ToString(), FeederValue = fg.ToString() });
                    }
                    else
                        r.ZcGrades[key] = mg;
                }

                // a key the main item holds as fixed is never also drawn from the pool
                var pool = main.PoolKeys.Union(feeder.PoolKeys).Where(k => !mainFixed.Contains(k)).OrderBy(k => k).ToList();
                r.FixedSlots = main.FixedSlots;
                r.PropertyCap = (feeder.Tier > main.Tier ? feeder : main).PropertyCap;
                if (pool.Count > 0)
                {
                    var lo = Math.Min(main.PropertyCount, feeder.PropertyCount);
                    var hi = Math.Max(main.PropertyCount, feeder.PropertyCount);
                    var count = lo + PickIndex(Draw(), hi - lo + 1);
                    count = Math.Min(count, r.PropertyCap);
                    var free = Math.Max(0, Math.Min(count - main.FixedSlots, pool.Count));

                    var remaining = new List<int>(pool);
                    var chosen = new List<int>();
                    for (var i = 0; i < free; i++)
                    {
                        var index = PickIndex(Draw(), remaining.Count);
                        chosen.Add(remaining[index]);
                        remaining.RemoveAt(index);
                    }

                    foreach (var key in chosen.OrderBy(k => k))
                    {
                        var mainHas = main.PoolKeys.Contains(key) && main.ZcGrades.TryGetValue(key, out _);
                        var feederHas = feeder.PoolKeys.Contains(key) && feeder.ZcGrades.TryGetValue(key, out _);
                        var mg = mainHas ? main.ZcGrades[key] : 0;
                        var fg = feederHas ? feeder.ZcGrades[key] : 0;
                        if (mainHas && feederHas)
                        {
                            var roll = Draw();
                            r.ZcGrades[key] = (int)Math.Round(RollNumeric(mg, fg, false, config, roll, out var fromMain), MidpointRounding.AwayFromZero);
                            o.Picks.Add(new ForgePick { Kind = PickKind.Grade, Key = key, Roll = roll, FromMain = fromMain, Blended = config.RollMode != RollMode.Pick, MainIsBetter = mg >= fg, MainValue = mg.ToString(), FeederValue = fg.ToString() });
                        }
                        else
                            r.ZcGrades[key] = mainHas ? mg : fg;
                        r.PoolKeys.Add(key);
                        if (!mainHas)
                            o.PropertiesGained.Add(key);
                    }
                    o.PropertiesLost.AddRange(main.PoolKeys.Where(k => !r.PoolKeys.Contains(k) && !mainFixed.Contains(k)));
                }
            }

            // 4. Spells, per family: higher level is better; a family the chosen parent lacks is lost.
            foreach (var family in main.Spells.Keys.Union(feeder.Spells.Keys).OrderBy(k => k))
            {
                var roll = Draw();
                var mainHas = main.Spells.TryGetValue(family, out var ms);
                var feederHas = feeder.Spells.TryGetValue(family, out var fs);
                var fromMain = PicksMain(mainHas, feederHas, ms?.Level ?? 0, fs?.Level ?? 0, false, p, roll);
                var chosen = fromMain ? ms : fs;
                if (chosen != null)
                    r.Spells[family] = new SpellEntry { Family = chosen.Family, SpellId = chosen.SpellId, Level = chosen.Level };
                o.Picks.Add(new ForgePick
                {
                    Kind = PickKind.Spell, Key = (int)family, Roll = roll, FromMain = fromMain,
                    MainIsBetter = mainHas != feederHas ? mainHas : ms.Level >= fs.Level,
                    MainValue = ms != null ? ms.SpellId.ToString() : "none", FeederValue = fs != null ? fs.SpellId.ToString() : "none",
                });
            }

            // 5. Trait packages (slayer, rending, cleaving, set): whole from one parent, compared on Score.
            foreach (var key in main.Packages.Keys.Union(feeder.Packages.Keys).OrderBy(k => k))
            {
                var roll = Draw();
                var mainHas = main.Packages.TryGetValue(key, out var mp);
                var feederHas = feeder.Packages.TryGetValue(key, out var fp);
                var fromMain = PicksMain(mainHas, feederHas, mp?.Score ?? 0, fp?.Score ?? 0, false, p, roll);
                var chosen = fromMain ? mp : fp;
                if (chosen != null)
                    r.Packages[key] = new TraitPackage { Key = chosen.Key, Score = chosen.Score, Value = chosen.Value };
                o.Picks.Add(new ForgePick
                {
                    Kind = PickKind.Package, Key = key, Roll = roll, FromMain = fromMain,
                    MainIsBetter = mainHas != feederHas ? mainHas : mp.Score >= fp.Score,
                    MainValue = mp?.Value ?? "none", FeederValue = fp?.Value ?? "none",
                });
            }

            // Never rolled: the stricter of each requirement, and the higher tier.
            r.Tier = Math.Max(main.Tier, feeder.Tier);
            r.WieldDifficulty = new SortedDictionary<int, int>(main.WieldDifficulty);
            foreach (var kv in feeder.WieldDifficulty)
                r.WieldDifficulty[kv.Key] = r.WieldDifficulty.TryGetValue(kv.Key, out var cur) ? Math.Max(cur, kv.Value) : kv.Value;

            r.ForgeCount = Math.Max(main.ForgeCount, feeder.ForgeCount) + 1;

            // 6. Element: a plain 50/50 - neither element is better. The caller turns a changed element into the
            // main model's version in that element (ForgeGroups.FindElementVariant).
            {
                var roll = Draw();
                // A main weapon with a rend keeps its element: the rend stays on the result, and a fire bow with an acid
                // rend is what a free roll would make of it. The draw is still taken, so the sequence does not shift.
                var fromMain = main.Rend != 0 || roll < 0.5;
                r.Element = fromMain ? main.Element : feeder.Element;
                o.Picks.Add(new ForgePick { Kind = PickKind.Element, Roll = roll, FromMain = fromMain, MainIsBetter = true, MainValue = main.Element.ToString(), FeederValue = feeder.Element.ToString() });
            }

            // 7. Spark: a free hone level on a random line the weapon has. The roll is always consumed.
            o.SparkRoll = Draw();
            if (o.SparkRoll < config.SparkChance && r.HoneTotal < config.HoneMaxLevels)
            {
                var eligible = HonableLines.Where(l => r.Lines.ContainsKey(l)).ToList();
                if (eligible.Count > 0)
                {
                    var line = eligible[PickIndex(Draw(), eligible.Count)];
                    r.HoneLevels[line] = r.HoneLevel(line) + 1;
                    o.Spark = true;
                    o.SparkLine = line;
                }
            }

            o.Result = r;
            return o;
        }

        /// <summary>Uniform pick of one of <paramref name="count"/> entries from a [0, 1) draw.</summary>
        public static int PickIndex(double roll, int count) => Math.Max(0, Math.Min(count - 1, (int)Math.Floor(roll * count)));

        // ---------------------------------------------------------------- honing

        public enum HoneRefusal { None, NotHonable, LineMissing, Capped }

        public sealed class HoneOutcome
        {
            public HoneRefusal Refused;
            public ForgeLine Line;
            public long Cost;
            public double Chance;
            public double Roll;
            public bool Success;
            /// <summary>The weapon after the attempt (a copy; the input is untouched). Null when refused.</summary>
            public ForgeWeapon Result;
        }

        /// <summary>Luminance for one attempt at the weapon's current total hone level: base x growth^total.</summary>
        public static long HoneCost(int totalLevel, ForgeConfig config)
        {
            var cost = config.HoneBaseCost * Math.Pow(config.HoneCostGrowth, Math.Max(0, totalLevel));
            return cost >= long.MaxValue ? long.MaxValue : (long)Math.Round(cost, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Success chance: max(floor, base - step x total), plus misfortune x misfortuneStep, plus the flux
        /// bonus, clamped to [0, 1]. The floor applies before the bonuses so they always help.
        /// </summary>
        public static double HoneChance(int totalLevel, int misfortune, double fluxBonus, ForgeConfig config)
        {
            var baseChance = Math.Max(config.HoneMinChance, config.HoneBaseChance - config.HoneChanceStep * totalLevel);
            return Math.Clamp(baseChance + Math.Max(0, misfortune) * config.HoneMisfortuneStep + Math.Max(0.0, fluxBonus), 0.0, 1.0);
        }

        /// <summary>One hone attempt on <paramref name="line"/>. Refusals consume no draw and cost nothing.</summary>
        public static HoneOutcome Hone(ForgeWeapon weapon, ForgeLine line, double fluxBonus, ForgeConfig config, Func<double> nextDouble)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (nextDouble == null) throw new ArgumentNullException(nameof(nextDouble));

            var o = new HoneOutcome { Line = line };
            if (!IsHonable(line)) { o.Refused = HoneRefusal.NotHonable; return o; }
            if (!weapon.Lines.ContainsKey(line)) { o.Refused = HoneRefusal.LineMissing; return o; }
            var total = weapon.HoneTotal;
            if (total >= config.HoneMaxLevels) { o.Refused = HoneRefusal.Capped; return o; }

            o.Cost = HoneCost(total, config);
            o.Chance = HoneChance(total, weapon.HoneMisfortune, fluxBonus, config);
            o.Roll = nextDouble();
            o.Success = o.Roll < o.Chance;

            var r = weapon.Clone();
            if (o.Success)
            {
                r.HoneLevels[line] = r.HoneLevel(line) + 1;
                r.HoneMisfortune = 0;
            }
            else
                r.HoneMisfortune += 1;

            o.Result = r;
            return o;
        }

        /// <summary>
        /// The line's value with its hone levels applied: base x (1 + step x levels), or for a lower-is-better
        /// line base x (1 - step x levels), never below 0. Levels are measured against the BASE roll, so five
        /// levels at 5% are +25%, not compounding.
        /// </summary>
        public static double HonedValue(ForgeLine line, double baseValue, int levels, ForgeConfig config)
        {
            var delta = config.StepFor(line) * Math.Max(0, levels);
            return LowerIsBetter(line) ? Math.Max(0.0, baseValue * (1.0 - delta)) : baseValue * (1.0 + delta);
        }

        /// <summary>
        /// Expected attempts to gain one level from <paramref name="totalLevel"/> with no flux, counting
        /// misfortune: sum over n of P(first n tries all fail). Exact, and finite because misfortune
        /// eventually drives the chance to 1 (a zero misfortune step with a zero chance returns infinity).
        /// </summary>
        public static double ExpectedAttempts(int totalLevel, ForgeConfig config)
        {
            double expected = 0, allFailed = 1;
            for (var k = 0; k < 10000; k++)
            {
                expected += allFailed;
                var chance = HoneChance(totalLevel, k, 0, config);
                allFailed *= 1.0 - chance;
                if (allFailed <= 0)
                    return expected;
            }
            return allFailed > 1e-9 ? double.PositiveInfinity : expected;
        }

        /// <summary>Expected pyreals to hone a fresh weapon up to <paramref name="level"/> total levels.</summary>
        public static double ExpectedCostToReach(int level, ForgeConfig config)
        {
            double total = 0;
            for (var l = 0; l < Math.Min(level, config.HoneMaxLevels); l++)
                total += HoneCost(l, config) * ExpectedAttempts(l, config);
            return total;
        }

        /// <summary>Unbind fee: a fraction of the expected pyreals sunk into the weapon's hone levels, never below the floor.</summary>
        public static long UnbindFee(ForgeWeapon weapon, ForgeConfig config)
        {
            var fee = config.UnbindFraction * ExpectedCostToReach(weapon.HoneTotal, config);
            var rounded = fee >= long.MaxValue ? long.MaxValue : (long)Math.Round(fee, MidpointRounding.AwayFromZero);
            return Math.Max(config.UnbindMinFee, rounded);
        }
    }
}
