using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    /// <summary>
    /// QB Deeds: 15 cumulative tiers, tier N at N x 1,000 QB. THE one table - Player_Deeds applies the buff spells from it, the special-effect
    /// hooks key off its tier numbers, and /deeds list is printed from it, so the three can never disagree.
    ///
    /// Spell id choices (Legendary Summoning Prowess, no Prodigal Summoning, Master Voidlock's, Sigil of Vigor XV = 5313, no Icy Shield, Sigil of Fury X = 5324,
    /// Timaru's Shelter = 2429, Rare Damage Reduction V = 5192, 7 Masterwork resistances). Every buff keeps its REAL power
    /// level, so a stronger cantrip on gear still wins its category and Deeds never makes a player weaker.
    /// </summary>
    public static class DeedsTiers
    {
        public const int QbPerTier = 1000;
        public const int MaxTier = 15;

        // -- the special-effect tiers (the hooks test DeedsTier against these) --
        public const int SurgeTier       = 6;    // the three self surges, permanent, the player's life augs baked in; their Aetheria procs stop
        public const int BaneFloorTier   = 9;    // Legendary banes + Legendary Impenetrability as a floor in the armor math
        public const int RebuffTier      = 10;   // the /rebuff command and the Deedkeeper's Writ (/deeds item)
        public const int BreathTier      = 12;   // Breath of Renewal, ServerConfig.deeds_breath_chance on each attack
        public const int DamageTier      = 13;   // x DamageMultiplier, every attack style
        public const int CritTier        = 14;   // + the BASE crit chance again (base 10 pct -> 20 pct, Biting Strike 25 -> 35)
        public const int LootLuckTier    = 15;   // every loot quality / grade roll of a kill rolled twice, the better kept

        public const float DamageMultiplier = 1.10f;
        public const uint BreathOfRenewalSpellId = 4100;

        /// <summary>The Deedkeeper's Writ weenie (Database/Updates/World/2026-10-24-00-Deedkeepers-Writ.sql): using it runs /rebuff.</summary>
        public const uint DeedkeepersWritWcid = 777700311;

        /// <summary>T6: Surge of Destruction / Protection / Regeneration. Kept always on with the player's augs baked in, and an
        /// Aetheria proc of the same surge does not fire while the player has them.</summary>
        public static readonly IReadOnlySet<uint> SurgeSpellIds = new HashSet<uint> { 5204, 5206, 5208 };

        /// <summary>The T9 armor floor: Legendary Impenetrability (armor level) and the Legendary bane for each damage type.</summary>
        public const uint LegendaryImpenetrabilitySpellId = 6095;

        public static readonly IReadOnlyDictionary<DamageType, uint> LegendaryBaneSpellIds = new Dictionary<DamageType, uint>
        {
            { DamageType.Acid,     6088 },
            { DamageType.Bludgeon, 6090 },
            { DamageType.Fire,     6092 },
            { DamageType.Cold,     6093 },
            { DamageType.Pierce,   6096 },
            { DamageType.Slash,    6097 },
            { DamageType.Electric, 6099 },
        };

        public sealed class Tier
        {
            public int Number { get; init; }
            public long Qb => (long)Number * QbPerTier;

            /// <summary>Buff spells Deeds keeps on the player (Duration -1, the player as caster).</summary>
            public IReadOnlyList<uint> Spells { get; init; } = Array.Empty<uint>();

            /// <summary>Spells named in the appraisal that are NOT cast on the player (the T9 banes, the T12 proc).</summary>
            public IReadOnlyList<uint> ListedSpells { get; init; } = Array.Empty<uint>();

            /// <summary>Plain appraisal lines for the effects that are not spells. ASCII only.</summary>
            public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
        }

        public static readonly IReadOnlyList<Tier> Tiers = new List<Tier>
        {
            new Tier { Number = 1, Spells = new uint[]
            {
                5137, 4024, 3811,                                   // Augmented Understanding III, Asheron's Lesser Benediction, Blackmoor's Favor
                6107, 6104, 6103, 6106, 6105, 6101,                 // Legendary Strength / Endurance / Coordination / Quickness / Focus / Willpower
                6046, 6048, 6049, 6050, 6053, 6056, 6060, 6067, 6069, 6070,   // Legendary Creature .. Sneak Attack
                6125,                                               // Legendary Summoning Prowess
                6047, 6072, 6043, 6044, 6073,                       // Legendary Finesse / Heavy / Light / Missile / Two Handed
                6063, 6055, 6054,                                   // Legendary Magic Resistance / Invulnerability / Impregnability
                6074, 6075,                                         // Legendary Void / War
                6077, 6076, 6078,                                   // Legendary Health / Stamina / Mana Gain
            } },
            new Tier { Number = 2, Spells = new uint[]
            {
                6102,                                               // Legendary Armor
                6081, 6084, 6085, 6080, 6082, 6083, 6079,           // Legendary Bludgeoning / Piercing / Slashing / Acid / Flame / Frost / Storm Ward
            } },
            new Tier { Number = 3, Spells = new uint[]
            {
                5911, 3717, 3698, 5905, 5907, 5909,                 // Prodigal Dirty Fighting / Life / Deception / Recklessness / Shield / Sneak Attack
                3723, 3712, 3711,                                   // Prodigal Magic Resistance / Invulnerability / Impregnability
                5436, 3743, 3707,                                   // Prodigal Void / War / Healing
            } },
            new Tier { Number = 4, Spells = new uint[]
            {
                3005, 3007, 3006,                                   // Dispersion, Uncanny Dodge, Foresight
                4853, 4861, 4865,                                   // Master Negator's / Guardian's / Wayfarer's
                5434, 6245, 4895, 6250,                             // Master Voidlock's, Paragon's Void V, Master Warlock's, Paragon's War V
            } },
            new Tier { Number = 5, Spells = new uint[]
            {
                // health
                3204, 2006, 4755, 3034, 3571, 5451, 5146, 5122, 5966, 6170, 6340, 2446, 5314,
                // stamina
                2980, 2010, 3208, 3570, 4759, 5152, 6172, 4906, 4072, 5313,
                // mana
                2016, 2014, 4757, 3569, 5149, 5127, 6171, 4068,
            } },
            new Tier { Number = 6, Spells = new uint[]
            {
                5204, 5206, 5208,                                   // Surge of Destruction / Protection / Regeneration, always on
            },
                Lines = new[] { "(always on, with your life augs - your Aetheria no longer procs these three)" } },
            new Tier { Number = 7, Spells = new uint[]
            {
                3738, 3700, 3694, 3730, 3705, 3733,                 // Prodigal Strength .. Willpower
                3731, 3732, 3725,                                   // Prodigal Regeneration / Rejuvenation / Mana Renewal
            } },
            new Tier { Number = 8, Spells = new uint[]
            {
                4749, 4737, 5324, 6270,                             // Master Brute's, Master Hero's, Sigil of Fury X (Endurance), Paragon's Endurance V
                4280, 4733, 4745,                                   // Deck of Hands Favor, Master Duelist's, Master Rover's
                2347, 2348, 4281, 4741,                             // Concentration, Brilliance, Deck of Eyes Favor, Master Sage's
                2638, 4753, 4904,                                   // Heart of Oak, Master Adherent's, Society Master's Blessing
                5140, 5233, 5187, 5978, 6331, 6305,                 // damage rating
                5143, 5248, 5450, 5192, 6333, 6310,                 // damage reduction rating
                4015, 2429,                                         // Ruschk Skin, Timaru's Shelter (armor) - Icy Shield dropped
                6337, 5263,                                         // Gauntlet Healing Boost II, Sigil of Growth X
            } },
            new Tier { Number = 9,
                ListedSpells = new uint[] { 6088, 6090, 6092, 6093, 6096, 6097, 6099, 6095 },
                Lines = new[] { "(on every armor piece you wear, as a floor - never cast on the item)" } },
            new Tier { Number = 10, Lines = new[] { "/rebuff - cast all your known level 8 buffs in one go", "/deeds item - a Deedkeeper's Writ that does the same when used" } },
            new Tier { Number = 11, Spells = new uint[]
            {
                3684,                                               // Prodigal Armor
                3690, 3729, 3687, 3680, 3702, 3692, 3719,           // Prodigal protections
                4767, 4783, 4787, 4779, 4763, 4771, 4775,           // Masterwork resistances
            } },
            // the chance line is added live by Player.BuildDeedsList (ServerConfig.deeds_breath_chance)
            new Tier { Number = 12,
                ListedSpells = new uint[] { BreathOfRenewalSpellId } },
            new Tier { Number = 13, Lines = new[] { "+10 percent damage with every attack: melee, missile, war and void" } },
            new Tier { Number = 14, Lines = new[] { "+100 percent of your BASE critical chance (10 percent becomes 20)" } },
            new Tier { Number = 15, Lines = new[] { "Loot luck: the quality of every item you loot is rolled twice, the better kept" } },
        };

        /// <summary>The tier a QB reaches: 0 below 1,000, capped at MaxTier.</summary>
        public static int TierFor(long qb) => (int)Math.Clamp(qb / QbPerTier, 0, MaxTier);

        /// <summary>Every buff spell of every tier, with the tier that grants it (a spell listed twice: its LOWEST tier).</summary>
        public static readonly IReadOnlyDictionary<uint, int> SpellTier =
            Tiers.SelectMany(t => t.Spells.Select(s => (Spell: s, t.Number)))
                .GroupBy(x => x.Spell)
                .ToDictionary(g => g.Key, g => g.Min(x => x.Number));

        /// <summary>The buff spells Deeds keeps on the player at a tier (cumulative).</summary>
        public static IEnumerable<uint> SpellsUpTo(int tier) => SpellTier.Where(kv => kv.Value <= tier).Select(kv => kv.Key);

        private static readonly Dictionary<uint, string> spellNames = new();

        /// <summary>A spell's name for the appraisal (the spell table's own name, ASCII as stored), cached.</summary>
        public static string SpellName(uint spellId)
        {
            lock (spellNames)
            {
                if (spellNames.TryGetValue(spellId, out var name))
                    return name;

                var spell = new Spell(spellId);   // with the DB row: NotFound is true for a DAT-only load (10-22: the list showed "Spell 6107")
                // the DB row's name (10-22: the DAT copy put non-ASCII into the appraisal); printable ASCII only either way
                var raw = spell.NotFound ? $"Spell {spellId}" : (spell._spell?.Name ?? spell.Name);
                name = new string(raw.Where(c => c >= ' ' && c <= '~').ToArray()).Trim();
                spellNames[spellId] = name;
                return name;
            }
        }
    }

    /// <summary>
    /// QB Deeds T15 loot luck: while a kill's treasure is generated for a killer who has it, every quality roll
    /// (QualityChance.Roll / RollInterval - the LootQualityMod path) and every T11+ grade roll (ZoneStatResolver.RollGrade)
    /// is rolled twice and the better kept. Thread-static and scoped, exactly like ZoneStatResolver.ScopeDropFloor, so a
    /// roll outside a lucky kill's GenerateTreasure (crafting, essences, other kills) is never touched.
    /// </summary>
    public static class DeedsLootLuck
    {
        [ThreadStatic] private static bool _active;

        public static bool Active => _active;

        public static LuckScope Scope(bool lucky) => new LuckScope(lucky);

        public readonly struct LuckScope : IDisposable
        {
            private readonly bool _prev;
            public LuckScope(bool lucky) { _prev = _active; _active = lucky; }
            public void Dispose() => _active = _prev;
        }

        /// <summary>One roll, or the better of two while luck is active. Callers pass a STATIC lambda and its argument, so the
        /// common (no luck) path allocates nothing.</summary>
        public static int Better<TArg>(TArg arg, Func<TArg, int> roll) => _active ? Math.Max(roll(arg), roll(arg)) : roll(arg);

        /// <summary>One roll, or the better of two while luck is active (see the int overload).</summary>
        public static float Better<TArg>(TArg arg, Func<TArg, float> roll) => _active ? Math.Max(roll(arg), roll(arg)) : roll(arg);
    }
}
