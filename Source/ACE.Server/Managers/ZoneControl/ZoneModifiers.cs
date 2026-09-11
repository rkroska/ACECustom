using System;
using System.Collections.Generic;
using ACE.Common;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Zone Control unique loot cantrips — a PROP-BASED system, deliberately OUTSIDE the spell/enchantment
    /// machinery: no dat entries, no SpellId, no SpellCategory stacking rules. Each cantrip stamps int props
    /// (custom block 50200-50399 in the shard's private prop space, plus the retail Gear* rating props) onto
    /// the dropped item; read hooks sum them across EQUIPPED items, so the bonuses always stack on top of
    /// every spell, gem and aug — and across multiple cantripped pieces worn at once.
    /// Consumers: CreatureAttribute.GetCurrent, CreatureSkill.GetAugBonus_Current, CreatureVital.GearBonus,
    /// Creature_Vitals.VitalHeartBeat (regen augMod), EnchantmentManager/AddEnchantmentResult (spell duration).
    /// </summary>
    public static class ZoneModifiers
    {
        // ── custom PropertyInt ids (ZC cantrip block) ────────────────────────
        public const int PropMin = 50200;
        public const int PropMax = 50399;

        public const int AttrBonusBase = 50200;       // + (int)PropertyAttribute (1..6) => 50201..50206
        public const int SpellDurationLevels = 50212; // each level = +20 pct spell duration (aug formula)
        public const int FortifyVitalsPct = 50226;    // additive percentage POINTS across pieces - all three vitals (key 41)
        public const int BattleMendChancePct = 50227; // per-PIECE proc chance in % (key 42)
        public const int BattleMendHealAmount = 50228;// per-PIECE heal amount on proc (key 42) - LEGACY, no longer stamped
        // Armor v2 slot specials (2026-08-21): MAX-wins across worn pieces (Creature.GetZoneModifierMax), never summed
        public const int PctHpDamagePct = 50229;      // Gauntlets (key 44): pct of target max HP per hit, in TENTHS of a pct (45 = 4.5 pct)
        public const int CheatDeathFlag = 50230;      // Boots (key 45): stamp 1 - lethal hit -> 1 HP + immunity window
        public const int RegenSpecialMult = 50231;    // Bracers (key 46): FLAT pct of max vital added per natural regen tick (was a multiplier until 2026-08-23)
        // 2026-08-22 additions (owner cantrip walkthrough)
        public const int PctMaxHealthPct = 50232;     // key 47 Pct Max Health: percentage POINTS of max HP, SUMMED across worn pieces
        public const int LifeOnHitPct = 50233;        // key 48 Life on Hit: pct of the wielder's MAX HP healed per landed hit, SUMMED, worn cap lifeonhit_cap (25)
        public const int ReinforcedRank = 50234;      // key 49 Reinforced: the protection rank stamped on the piece (1 Superior / 2 Excellent / 3 Unparalleled) - display/bookkeeping only
        public const int SkillBonusBase = 50300;      // + (int)Skill => 50300+.. (flat skill, post-vitae)

        /// <summary>
        /// Per-line slot rule (owner 2026-08-22, "a way to specify live in game which slots the cantrip
        /// can drop in"). Bit flags; 0 = Any. The catalog's ArmorOnly / JewelryOnly flags are the
        /// DEFAULT (DefaultSlotMask); a zone / Default-layer override (ZoneVariantProfile.CustomModifierSlots,
        /// `cantrip <scope> slots <key> ...`) replaces it per key. The mutator and the premade forge both ask
        /// EffectiveSlotMask, so what drops and what /asforge mints agree.
        /// </summary>
        [Flags]
        public enum SlotMask
        {
            Any = 0,
            Armor = 1,
            Shield = 2,
            Jewelry = 4,
            Clothing = 8,
            Cloak = 16,
        }

        public static SlotMask DefaultSlotMask(Def def)
            => def == null ? SlotMask.Any : def.ArmorOnly ? SlotMask.Armor | SlotMask.Shield : def.JewelryOnly ? SlotMask.Jewelry : SlotMask.Any;

        /// <summary>The mask the roll should use: the profile's per-key override when authored, else the catalog default.</summary>
        public static SlotMask EffectiveSlotMask(Def def, IReadOnlyDictionary<int, int> overrides)
            => def != null && overrides != null && overrides.TryGetValue(def.Key, out var m) ? (SlotMask)m : DefaultSlotMask(def);

        /// <summary>What kind of piece this is, for the slot rule. Armor = has an armor level and is not a shield;
        /// Shield = IsShield; Jewelry = ItemType Jewelry; Clothing = ItemType Clothing without an AL (undies);
        /// Cloak = ItemType Clothing covering the cloak slot. Weapons return 0 (they never roll lines here anyway).</summary>
        public static SlotMask PieceMask(WorldObject wo)
        {
            if (wo == null) return SlotMask.Any;
            if (wo.IsShield) return SlotMask.Shield;
            if (wo.ArmorLevel.HasValue && wo.ArmorLevel.Value > 0 && wo.ItemType != ItemType.Jewelry) return SlotMask.Armor;
            if (wo.ItemType == ItemType.Jewelry) return SlotMask.Jewelry;
            if (wo.ItemType == ItemType.Clothing)
                return (wo.ValidLocations ?? 0).HasFlag(EquipMask.Cloak) ? SlotMask.Cloak : SlotMask.Clothing;
            return SlotMask.Any;
        }

        public static bool SlotAllowed(SlotMask rule, SlotMask piece) => rule == SlotMask.Any || (rule & piece) != 0;

        /// <summary>"armor,shield" / "jewelry" / "any" / "clear" -> mask; null on a bad token. -1 = clear.</summary>
        public static bool TryParseSlotSpec(string spec, out int mask)
        {
            mask = 0;
            if (string.IsNullOrWhiteSpace(spec)) return false;
            if (spec.Trim().Equals("clear", StringComparison.OrdinalIgnoreCase)) { mask = -1; return true; }
            if (int.TryParse(spec.Trim(), out var raw) && raw >= 0 && raw <= 31) { mask = raw; return true; }
            foreach (var tok in spec.Split(new[] { ',', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (tok.Trim().ToLowerInvariant())
                {
                    case "any": break;
                    case "armor": mask |= (int)SlotMask.Armor; break;
                    case "shield": mask |= (int)SlotMask.Shield; break;
                    case "jewelry": case "jewellery": mask |= (int)SlotMask.Jewelry; break;
                    case "clothing": case "undies": mask |= (int)SlotMask.Clothing; break;
                    case "cloak": mask |= (int)SlotMask.Cloak; break;
                    default: return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Where a SLOT SPECIAL lands (owner 2026-08-22: "drop down here, just with single slot"). One id per
        /// drop slot of the T11+ set. The catalog's SpecialSlot (CoverageMask) is the default; a zone / Default
        /// override in CustomModifierSlots (same dict as the line slot rules - for a special the value is a
        /// SpecialSlotId, not a mask) replaces it. Creature_Death matches and spawns by this id.
        /// </summary>
        public enum SpecialSlotId
        {
            Helm = 1, Chest, Shoulders, Bracers, Gauntlets, Girth, Tassets, Greaves, Boots,
            Shield = 10, Neck = 11, Trinket = 12, Ring = 13, Bracelet = 14, Cloak = 15,
        }

        public static SpecialSlotId DefaultSpecialSlot(Def def) => def?.SpecialSlot switch
        {
            CoverageMask.Head => SpecialSlotId.Helm,
            CoverageMask.OuterwearChest => SpecialSlotId.Chest,
            CoverageMask.OuterwearUpperArms => SpecialSlotId.Shoulders,
            CoverageMask.OuterwearLowerArms => SpecialSlotId.Bracers,
            CoverageMask.Hands => SpecialSlotId.Gauntlets,
            CoverageMask.OuterwearAbdomen => SpecialSlotId.Girth,
            CoverageMask.OuterwearUpperLegs => SpecialSlotId.Tassets,
            CoverageMask.OuterwearLowerLegs => SpecialSlotId.Greaves,
            CoverageMask.Feet => SpecialSlotId.Boots,
            _ => SpecialSlotId.Chest,
        };

        public static SpecialSlotId EffectiveSpecialSlot(Def def, IReadOnlyDictionary<int, int> overrides)
            => def != null && overrides != null && overrides.TryGetValue(def.Key, out var v) && Enum.IsDefined(typeof(SpecialSlotId), v)
                ? (SpecialSlotId)v : DefaultSpecialSlot(def);

        public static bool TryParseSpecialSlot(string spec, out int id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(spec)) return false;
            var t = spec.Trim();
            if (t.Equals("clear", StringComparison.OrdinalIgnoreCase)) { id = -1; return true; }
            if (int.TryParse(t, out var raw) && Enum.IsDefined(typeof(SpecialSlotId), raw)) { id = raw; return true; }
            switch (t.ToLowerInvariant())
            {
                case "helm": case "head": id = (int)SpecialSlotId.Helm; return true;
                case "chest": id = (int)SpecialSlotId.Chest; return true;
                case "shoulders": case "shoulder": case "pauldrons": id = (int)SpecialSlotId.Shoulders; return true;
                case "bracers": case "bracer": id = (int)SpecialSlotId.Bracers; return true;
                case "gauntlets": case "gloves": case "glove": case "hands": id = (int)SpecialSlotId.Gauntlets; return true;
                case "girth": id = (int)SpecialSlotId.Girth; return true;
                case "tassets": case "upperleg": id = (int)SpecialSlotId.Tassets; return true;
                case "greaves": case "lowerleg": id = (int)SpecialSlotId.Greaves; return true;
                case "boots": case "boot": case "feet": id = (int)SpecialSlotId.Boots; return true;
                case "shield": id = (int)SpecialSlotId.Shield; return true;
                case "neck": case "amulet": case "necklace": id = (int)SpecialSlotId.Neck; return true;
                case "trinket": id = (int)SpecialSlotId.Trinket; return true;
                case "ring": id = (int)SpecialSlotId.Ring; return true;
                case "bracelet": case "wrist": id = (int)SpecialSlotId.Bracelet; return true;
                case "cloak": id = (int)SpecialSlotId.Cloak; return true;
            }
            return false;
        }

        public static string SpecialSlotName(int id) => Enum.IsDefined(typeof(SpecialSlotId), id) ? ((SpecialSlotId)id).ToString() : "slot " + id;

        private static CoverageMask SpecialCoverage(SpecialSlotId id) => id switch
        {
            SpecialSlotId.Helm => CoverageMask.Head,
            SpecialSlotId.Chest => CoverageMask.OuterwearChest,
            SpecialSlotId.Shoulders => CoverageMask.OuterwearUpperArms,
            SpecialSlotId.Bracers => CoverageMask.OuterwearLowerArms,
            SpecialSlotId.Gauntlets => CoverageMask.Hands,
            SpecialSlotId.Girth => CoverageMask.OuterwearAbdomen,
            SpecialSlotId.Tassets => CoverageMask.OuterwearUpperLegs,
            SpecialSlotId.Greaves => CoverageMask.OuterwearLowerLegs,
            SpecialSlotId.Boots => CoverageMask.Feet,
            _ => 0,
        };

        /// <summary>Does this dropped piece sit in the special's slot?</summary>
        public static bool SpecialPieceMatches(WorldObject wo, SpecialSlotId id)
        {
            if (wo == null) return false;
            switch (id)
            {
                case SpecialSlotId.Shield: return wo.IsShield;
                case SpecialSlotId.Cloak: return wo.ItemType == ItemType.Clothing && (wo.ValidLocations ?? 0).HasFlag(EquipMask.Cloak);
                case SpecialSlotId.Neck: return wo.ItemType == ItemType.Jewelry && (wo.ValidLocations ?? 0).HasFlag(EquipMask.NeckWear);
                case SpecialSlotId.Trinket: return wo.ItemType == ItemType.Jewelry && (wo.ValidLocations ?? 0).HasFlag(EquipMask.TrinketOne);
                case SpecialSlotId.Ring: return wo.ItemType == ItemType.Jewelry && ((wo.ValidLocations ?? 0) & (EquipMask.FingerWearLeft | EquipMask.FingerWearRight)) != 0;
                case SpecialSlotId.Bracelet: return wo.ItemType == ItemType.Jewelry && ((wo.ValidLocations ?? 0) & (EquipMask.WristWearLeft | EquipMask.WristWearRight)) != 0;
                default:
                    var cov = SpecialCoverage(id);
                    return cov != 0 && !wo.IsShield && wo.ClothingPriority.HasValue && (wo.ClothingPriority.Value & cov) != 0 && (wo.ArmorLevel ?? 0) > 0;
            }
        }

        public static string SlotMaskName(int mask)
        {
            if (mask <= 0) return "Any";
            var parts = new List<string>();
            if ((mask & 1) != 0) parts.Add("Armor");
            if ((mask & 2) != 0) parts.Add("Shield");
            if ((mask & 4) != 0) parts.Add("Jewelry");
            if ((mask & 8) != 0) parts.Add("Clothing");
            if ((mask & 16) != 0) parts.Add("Cloak");
            return string.Join("+", parts);
        }

        public class Def
        {
            public int Key;
            public string Name;                            // codename ("Empyrean Might")
            public string Effect;                          // legacy fixed-value text, kept for old callers/help
            public string ValFmt;                          // rolled-value format: "+{0}", "x{0}", "+{0} lvl", "+{0}% vitals", "heal {0}"
            public int Min, Max;                           // roll band, INCLUSIVE both bounds
            public bool ArmorOnly;                         // keys 25 / 49 - needs an ArmorLevel piece
            public bool JewelryOnly;                       // key 48 Life on Hit - rolls only on jewelry (no AL, ItemType Jewelry)
            public bool TierScaled;                        // the 1250-class lines + Armor Level: catalog band x (1 + (t-11)/14) when no Default is authored (owner 2026-08-23)
            public int Min25, Max25;                       // optional explicit T25 anchor: band lerps linearly T11 (Min/Max) -> T25 (Min25/Max25); 0 = unused
            public bool SetsProtection;                    // key 49 Reinforced - the rolled value is a RANK that SETS every ArmorModVs* on the piece
            public (int PropId, int Value)[] Ints;         // int props stamped on the item; PropId also = banded stamp target
            public ModifierClass Class;                     // Armor v2 pick weight class (Trash/Mid/Chase); None = never in the line pool
            public bool SlotSpecial;                       // Armor v2 slot special: rolls once per KILL outside the line count, MAX-wins when worn
            public CoverageMask SpecialSlot;               // the armor slot a SlotSpecial stamps onto (Head/OuterwearChest/Hands/Feet/OuterwearLowerArms)
        }

        /// <summary>Armor v2 random-pool weight class (Cantrip_Band_Ladder v2 section 2). The per-class
        /// weights are zone stats cantrip_weight_trash/mid/chase.</summary>
        public enum ModifierClass
        {
            None = 0,   // retired / specials - never drawn as a line
            Trash,      // 32 Spell Duration, 25 Armor Level
            Mid,        // 28 Damage Rating, 29 Crit Damage Rating, 19 Max Health, 31 Healing Boost, 49 Reinforced
            Chase,      // 33 Crit Chance, 43 All Attributes, 47 Pct Max Health, 48 Life on Hit
        }

        private static (int, int)[] P(int propId, int v) => new[] { (propId, v) };

        /// <summary>The unique zone cantrip catalog. Keys are stable — they live in saved zone pools.
        /// Bands (Min/Max/ValFmt) mirror the plugin ModifierCatalog, the band authority.</summary>
        public static readonly SortedDictionary<int, Def> Catalog = new()
        {
            // vitals
            { 19, new Def { Key = 19, TierScaled = true, Class = ModifierClass.Mid, Name = "Max Health", Effect = "+14-69 Max Health", ValFmt = "+{0}", Min = 14, Max = 69, Ints = P((int)PropertyInt.GearMaxHealth, 300) } },
            // bulwark (Aegis is the ONLY line that modifies the item, not the player - armor/shield pieces only)
            { 25, new Def { Key = 25, TierScaled = true, Class = ModifierClass.Trash, ArmorOnly = true, Name = "Armor Level", Effect = "+300 Armor Level on this piece", ValFmt = "+{0}", Min = 50, Max = 250 } },
            // slaughter
            { 28, new Def { Key = 28, TierScaled = true, Class = ModifierClass.Mid, Name = "Damage Rating", Effect = "+14-69 Damage Rating", ValFmt = "+{0}", Min = 14, Max = 69, Ints = P((int)PropertyInt.GearDamage, 25) } },
            { 29, new Def { Key = 29, TierScaled = true, Class = ModifierClass.Mid, Name = "Crit Damage Rating", Effect = "+14-69 Critical Damage Rating", ValFmt = "+{0}", Min = 14, Max = 69, Ints = P((int)PropertyInt.GearCritDamage, 40) } },
            // whimsy
            { 31, new Def { Key = 31, TierScaled = true, Class = ModifierClass.Mid, Name = "Healing Boost", Effect = "+14-69 Healing Boost Rating", ValFmt = "+{0}", Min = 14, Max = 69, Ints = P((int)PropertyInt.GearHealingBoost, 40) } },
            { 32, new Def { Key = 32, Class = ModifierClass.Trash, Name = "Spell Duration", Effect = "+100 pct spell duration", ValFmt = "+{0} lvl", Min = 1, Max = 3, Ints = P(SpellDurationLevels, 5) } },
            // ratings (banded-only lines - legacy fixed Value is 0, they never ship via the legacy overload)
            { 33, new Def { Key = 33, Class = ModifierClass.Chase, Name = "Crit Chance", Effect = "+1-3 Crit Rating", ValFmt = "+{0}", Min = 1, Max = 3, Ints = P((int)PropertyInt.GearCrit, 0) } },
            // 37-40 RETIRED 2026-08-22 (owner): four class-split copies of what Damage Rating (28) already does
            // for every class. They forced a melee/caster premade split and made 3 of 4 dead for any given
            // player. Keys kept (saved pools, old items still read), props 50222-50225 still summed on equip.
            // ── Armor v2 SLOT SPECIALS (2026-08-21) ─────────────────────────────────────────────
            // Roll ONCE per KILL outside the line count (Creature_Death), stamp the dropped piece of
            // their slot, MAX-wins across worn pieces. Never drawn as a pool line (SlotSpecial).
            // Helm - fortify vitals, percentage POINTS, highest single value wins, all three pools
            { 41, new Def { Key = 41, SlotSpecial = true, SpecialSlot = CoverageMask.Head, Name = "Fortify Vitals", Effect = "+5-25 pct max vitals (Helm special)", ValFmt = "+{0}% vitals", Min = 5, Max = 25, Ints = P(FortifyVitalsPct, 0) } },
            // Chest - Battle Mending death save: after damage, HP > 0 and < 25 pct -> heal to max, 60 s CD; no magnitude.
            // The old proc PAIR is gone: only 50227 is stamped (= 1, presence flag); the combat side reads it MAX-wins.
            { 42, new Def { Key = 42, SlotSpecial = true, SpecialSlot = CoverageMask.OuterwearChest, Name = "Battle Mending", Effect = "death save: below 25 pct HP heal to full, 60 s cooldown (Chest special)", ValFmt = "", Min = 1, Max = 1, Ints = P(BattleMendChancePct, 0) } },
            // Gauntlets - flat pct of target max HP per hit, no crit/mults; value in TENTHS of a pct (40-60 = 4-6 pct at T11); 2 s CD; no-kill rule
            { 44, new Def { Key = 44, SlotSpecial = true, SpecialSlot = CoverageMask.Hands, Name = "Pct HP Damage", Effect = "4-6 pct of target max HP per hit (Gauntlets special)", ValFmt = "{0} ({1:0.#} pct of max HP per hit)", Min = 40, Max = 60, Ints = P(PctHpDamagePct, 0) } },
            // Boots - Cheat Death: lethal hit -> 1 HP + 5 s immunity, 10 min CD per character; no magnitude
            { 45, new Def { Key = 45, SlotSpecial = true, SpecialSlot = CoverageMask.Feet, Name = "Cheat Death", Effect = "lethal hit leaves 1 HP + 5 s immunity, 10 min cooldown (Boots special)", ValFmt = "", Min = 1, Max = 1, Ints = P(CheatDeathFlag, 0) } },
            // Bracers - natural regen multiplier (x3 default), Prodigal stays suppressed
            { 46, new Def { Key = 46, SlotSpecial = true, SpecialSlot = CoverageMask.OuterwearLowerArms, Name = "Regeneration", Effect = "+1-2 pct (T11) to +3-5 pct (T25) of max vitals on every natural regen tick (Bracers special; FLAT, never multiplies the buff stack - owner 2026-08-23)", ValFmt = "+{0}% per tick", Min = 1, Max = 2, Min25 = 3, Max25 = 5, Ints = P(RegenSpecialMult, 0) } },
            // ── 2026-08-22 pool additions (owner cantrip walkthrough; design in Cantrip_Pool_Decisions_2026-08-22.md) ──
            // Pct Max Health - chase, pinned 1-3 like Crit Chance, SUMMED across pieces (18 x 3 = 54 pct at BiS),
            // stacks on top of the Helm special Fortify Vitals (max-wins). Read in CreatureVital (MaxHealth only).
            { 47, new Def { Key = 47, Class = ModifierClass.Chase, Name = "Max Health Pct", Effect = "+1-3 pct max health", ValFmt = "+{0}%", Min = 1, Max = 3, Ints = P(PctMaxHealthPct, 0) } },
            // Life on Hit - chase, JEWELRY ONLY (6 slots), 1-3 pct of the wielder's max HP per landed hit, summed,
            // worn cap lifeonhit_cap (25 pct), lifeonhit_cooldown (3 s). Read in Player.ZcTryLifeOnHit from both hit paths.
            { 48, new Def { Key = 48, Class = ModifierClass.Chase, JewelryOnly = true, Name = "Life on Hit", Effect = "heal 1-3 pct max HP per hit", ValFmt = "{0}% HP per hit", Min = 1, Max = 3, Ints = P(LifeOnHitPct, 0) } },
            // Reinforced - mid, ARMOR ONLY, the value is a protection RANK: +1 Superior 1.40 / +2 Excellent 1.60 /
            // +3 Unparalleled 1.80. Stamp SETS every ArmorModVs* on the piece (after EqualizeT11ArmorResists), so it
            // is item data, not an enchantment - hollow mobs cannot strip it. Tier-weighted toward +3 via TierThirds.
            { 49, new Def { Key = 49, Class = ModifierClass.Mid, ArmorOnly = true, SetsProtection = true, Name = "Reinforced", Effect = "+1-3 protection rank (Superior / Excellent / Unparalleled)", ValFmt = "+{0}", Min = 1, Max = 3, Ints = P(ReinforcedRank, 0) } },
            // ── Armor v2 pool additions ─────────────────────────────────────────────────────────
            // All Attributes - six attrs behind ONE line (keys 1-6 retired); anchor 2500 at T25 = the Dmg/HP per-piece table. AVERAGE class since 2026-08-23 (owner)
            { 43, new Def { Key = 43, TierScaled = true, Class = ModifierClass.Mid, Name = "All Attributes", Effect = "+14-69 to ALL six attributes", ValFmt = "+{0}", Min = 14, Max = 69,
                Ints = new[] { (AttrBonusBase + (int)PropertyAttribute.Strength, 0), (AttrBonusBase + (int)PropertyAttribute.Endurance, 0), (AttrBonusBase + (int)PropertyAttribute.Coordination, 0),
                               (AttrBonusBase + (int)PropertyAttribute.Quickness, 0), (AttrBonusBase + (int)PropertyAttribute.Focus, 0), (AttrBonusBase + (int)PropertyAttribute.Self, 0) } } },
        };

        public static bool TryGet(int key, out Def def) => Catalog.TryGetValue(key, out def);

        /// <summary>Every catalog def, for the per-line roll sweep (owner 2026-08-29: each LINE rolls
        /// its own anchored chance; the pool / count / weight machinery is retired).</summary>
        public static IEnumerable<Def> AllDefs => Catalog.Values;

        /// <summary>The per-line chance stat for a catalog key ("modifier_chance_43"); its "_t25"
        /// twin is the T25 anchor. Registered explicitly in ZoneStat.All - a NEW catalog line needs
        /// its pair added there too, or the wire will not carry it.</summary>
        public static string LineChanceStat(int key) => "modifier_chance_" + key;

        /// <summary>Tier scale of the anchored linear ladder: 1.0 at T11, 2.0 at T25 (same f as the core anchors).</summary>
        public static double TierScale(int tier) => 1.0 + (Math.Clamp(tier, 11, 25) - 11) / 14.0;

        // LinesFallback REMOVED 2026-08-29 with the line-count ladder (one-row-per-Modifier design):
        // every line rolls its own anchored modifier_chance_<key> now, so there is no per-piece
        // count to fall back to.

        /// <summary>
        /// The HARDCODED band at a tier - what every fallback uses when no Default / zone band is authored
        /// (owner 2026-08-23: the catalog stays the reset target, but it must be tier-aware or T12+ rolls
        /// T11 numbers). TierScaled lines (the 1250-class ones + Armor Level) scale min and max by
        /// <see cref="TierScale"/>; pinned lines (1-3) and specials return the catalog band unchanged.
        /// </summary>
        public static (int Min, int Max) CatalogBandAt(Def def, int tier)
        {
            if (def == null) return (0, 0);
            var (min, max) = def.Min <= def.Max ? (def.Min, def.Max) : (def.Max, def.Min);
            if (def.Max25 > 0 && tier > 11)
            {
                // explicit T11 -> T25 anchors (owner 2026-08-23: Regeneration 1-2 at T11 -> 3-5 at T25)
                var t = (Math.Clamp(tier, 11, 25) - 11) / 14.0;
                return ((int)Math.Round(min + (def.Min25 - min) * t), (int)Math.Round(max + (def.Max25 - max) * t));
            }
            if (!def.TierScaled || tier <= 11) return (min, max);
            var f = TierScale(tier);
            return ((int)Math.Round(min * f), (int)Math.Round(max * f));
        }

        // ---- WEAPON card bands (2026-08-25) -----------------------------------------------------
        //
        // WHY THIS IS A SEPARATE TABLE AND NOT SIX MORE Catalog ROWS
        //
        // The six continuous weapon specials (Biting Strike, Crushing Blow, Armor Rend, Shield
        // Cleaving, Slayer, Rend Power) are DOUBLES whose second decimal matters: 0.58 crit chance,
        // 1.85x crush, 2.10 slayer. Def.Min/Max are ints, and CatalogBandAt returns (int, int).
        // Widening those would drag ValueFor/GradeFor, StampGraded, Stamp, the "[min-max]" LongDesc
        // text (which ZoneControlCommands.TryParseZcLine PARSES BACK for legacy-item migration),
        // ModifierBand (which is BOTH the [[ZC]]/[[ZCD]] wire shape AND the on-disk store JSON), and
        // the plugin's int.Parse of the cantrips= tag - a synchronised server+plugin deploy for six
        // values that never travel the wire in the first place. So: a separate table, double bounds,
        // keyed by the STAT NAME PAIR that already exists.
        //
        // Keying by stat name rather than by a new integer Catalog key is deliberate. Catalog keys
        // are load-bearing forever (see the comment at Catalog and at the retired 37-40 block): they
        // live in saved zone pools (ZoneVariantProfile.CustomModifiers) and on already-dropped items.
        // Minting six of them for weapons would leak weapon keys into the armour line pool and into
        // the "cantrip <scope> band <key>" command surface, where they mean nothing. This table
        // mints none, changes no existing type, and needs ZERO wire or store work.
        //
        // WHAT THIS TABLE IS: the FALLBACK a weapon card rolls from when the zone authors NEITHER
        // weapon_<card>_min NOR weapon_<card>_max - exactly the role CatalogBandAt plays for armour
        // lines. An authored min/max still wins outright and behaves exactly as it did before this
        // table existed, including the "one box = exact value, not a band" rule in RollRange.
        //
        // UNITS: every number below is in WIRE space, i.e. the units the weapon_*_min/max stats
        // already carry and the units the roll site writes onto the item. The plugin's Weapons-tab
        // boxes are NOT all in wire space, so each row states its own conversion. Getting this wrong
        // is silent: a 100x error in Biting Strike just clamps to 1.0 and every weapon crits always.
        //
        // KEEP IN STEP WITH THE PLUGIN. The same six anchors are mirrored in the ZoneControl plugin
        // at ZoneControlHud.cs "WpnBands" (BOX space there, wire space here). The plugin table is
        // what the owner reads while authoring; this one is what actually rolls. If one moves and
        // the other does not, the tooltip lies about what drops. There is no wire tag that syncs
        // them - that is the price of option (C), and it is cheaper than the alternative.
        //
        // Lo/Hi on each row are NOT design numbers - they are copied verbatim from that card's
        // existing RollRange(..., lo, hi) arguments in ZoneLootMutator.TrySpecialRolls, so the
        // banded path can never produce a value the authored path could not. If a clamp moves there,
        // move it here in the same edit.
        /// <summary>
        /// A T11 -> T25 magnitude ladder for ONE continuous weapon special, in wire units.
        /// Deliberately NOT <see cref="Def"/>: doubles, no catalog key, never in the armour line pool.
        /// </summary>
        public sealed class WeaponBand
        {
            public string Name;              // card name as the plugin's Weapons tab shows it
            public string MinStat, MaxStat;  // the authored zone stats that OVERRIDE this ladder entirely
            public double Min, Max;          // T11 anchor, WIRE units
            public double Min25, Max25;      // T25 anchor, WIRE units
            public double Lo, Hi;            // hard clamp - mirrors the card's RollRange(lo, hi) at its roll site
        }

        // Biting Strike -> CriticalFrequency. Plugin box is PERCENT 0..100; wire is a 0..1 fraction.
        // Conversion: wire = box / 100. Box 58-65 -> 0.58-0.65 at T11; box 78-88 -> 0.78-0.88 at T25.
        public static readonly WeaponBand WeaponBite = new WeaponBand
        {
            Name = "Biting Strike", MinStat = ZoneScaling.ZoneStat.WeaponBiteMin, MaxStat = ZoneScaling.ZoneStat.WeaponBiteMax,
            // T11 floor RE-ANCHORED 2026-08-25 (owner): exactly 0.33, which is what every
            // general crit-chance craft SetValues - Bag of Abyssal-Touched Gems (527870098/100/113),
            // Salvaged Yellow Garnet (527870006/010), Admin Only Yellow Garnet. So a T11 drop at its
            // floor exactly TIES the best craftable weapon, and every roll above it beats one.
            // The old 0.58 was chosen to clear the Critical Strike imbue's flat 0.50 - that imbue is
            // suppressed for players now, so it is no longer the thing to beat.
            // DECIDED 2026-08-29 (owner, power-level session): the whole weapon ladder is budgeted
            // at +20% player power per tier (BiS-over-craft ~13x at T25, split evenly across the
            // three multiplying cards). Bite's share is shaped so crits stay an EVENT: 0.60 at the
            // T25 chase, deliberately NOT the old imbue-era 0.88 near-every-hit zone. T11 max 0.40
            // gives an S-grade roll a visible edge over the 0.33 craft tie.
            Min = 0.33, Max = 0.40, Min25 = 0.50, Max25 = 0.60, Lo = 0.0, Hi = 1.0,
        };

        // Armor Rend -> prop 9057, the fraction of the target's armour ignored. Plugin box is
        // PERCENT 0..100; wire is a 0..1 fraction. Conversion: wire = box / 100.
        // Box 62-68 -> 0.62-0.68 at T11; box 74-80 -> 0.74-0.80 at T25.
        public static readonly WeaponBand WeaponArmorRend = new WeaponBand
        {
            Name = "Armor Rend", MinStat = ZoneScaling.ZoneStat.WeaponArmorRendMin, MaxStat = ZoneScaling.ZoneStat.WeaponArmorRendMax,
            Min = 0.62, Max = 0.68, Min25 = 0.74, Max25 = 0.80, Lo = 0.0, Hi = 1.0,
        };

        // Rend Power -> prop 9056, a vuln FRACTION (the engine computes rendingMod = 1 + this, so
        // wire 1.60 means the target takes 2.60x from that element - NOT 1.60x; that off-by-one bit
        // once already, 2026-08-24). Plugin box is PERCENT 0..1000, i.e. box 160 = wire 1.60.
        // Conversion: wire = box / 100. Box 150-213 -> 1.50-2.13 at T11; box 275-400 -> 2.75-4.00 at T25.
        //
        // RE-ANCHORED 2026-08-26 (owner set all four numbers plus the clamp). The old 1.60/1.85 ->
        // 2.20/2.70 was imbue-era: 1.60 was picked to CLEAR the vanilla rend's 2.5x cap by a margin.
        // The doctrine the other cards were re-anchored to on 08-25 is TIE THE BEST CRAFT, and here
        // the best craft IS that ceiling - every elemental salvage SetValues 1.50 stored = 2.50x, on
        // 280 weapon targets per element. So the floor moves DOWN to 1.50 to tie it exactly, and the
        // spread above it is what carries the card.
        // Hi LOWERED 10.0 -> 5.0 in the same edit. Rend Power substitutes into vulnMod AFTER the v11
        // vulnerability compression (Creature_Properties.cs:130-146 then :168-169), so it is the one
        // channel that BYPASSES v11_vuln_cap entirely - at 10.0 the engine computes rendingMod = 11.0,
        // an 11x multiplier on the whole damage line before prots. 5.0 is still a 6x ceiling.
        public static readonly WeaponBand WeaponRendPower = new WeaponBand
        {
            Name = "Rend Power", MinStat = ZoneScaling.ZoneStat.WeaponRendPowerMin, MaxStat = ZoneScaling.ZoneStat.WeaponRendPowerMax,
            // T25 RAISED 2026-08-29 (owner, power-level session, +20%/tier budget): T25 3.75-5.00.
            // In damage terms: wire 5.00 -> rendingMod 6.0 = 2.4x the craft's 2.5x contribution,
            // sitting exactly AT the Hi clamp - rend's even third of the ~13x BiS-over-craft budget.
            // Rides the Rending merge: every card-stamped rend rolls this band at the imbue chance.
            Min = 1.50, Max = 2.13, Min25 = 3.75, Max25 = 5.00, Lo = 1.5, Hi = 5.0,
        };

        // Slayer -> SlayerDamageBonus, a RAW damage multiplier vs the killed creature type. The
        // plugin box is raw too (weapon_slayer_* is NOT in the plugin's PercentStats set), so there
        // is NO conversion: box 1.80 = wire 1.80. Box 1.80-2.10 at T11; box 2.40-3.00 at T25.
        // Cast on Strike damage (added 2026-08-27, owner). B = the spell base the proc rolls, and it
        // REPLACES the rolled base plus the flat War/Void aug term rather than multiplying it.
        //
        // WHY 440: collapsing everything a proc shares with a melee swing, landed proc damage is B x K
        // with K ~= 3.20 against a T11 minion at the reference build, and measured melee lands ~1,413
        // per hit there - so B ~= 440 is EXACTLY ONE MELEE HIT. That is the yardstick the owner chose
        // over "tie the best craft" (~150), because 0.34 of a swing is not something a player notices.
        // The ratio is rend-proof: a proc and a swing pass through the SAME weaponResistanceMod term
        // with the same weapon, so a matched rend cancels out of it and 440 holds whatever the vuln
        // cap becomes (see the vuln/rend MAX ruling, Creature_Properties.cs:167).
        //
        // RE-RULED 2026-08-30 (owner): base B is a token "one damage to two damage" - flat 1-2 at
        // EVERY tier, no T25 growth. The proc's real damage comes from the WIELDER's War/Void augs,
        // which the engine adds flat on top of B, so tier scaling rides the aug ladder, not this
        // band. This supersedes the 440-660 provisional and the "440 + X pct" form entirely.
        public static readonly WeaponBand WeaponProcArcDamage = new WeaponBand
        {
            Name = "Cast on Strike (Arc)", MinStat = ZoneScaling.ZoneStat.WeaponProcArcDmgMin, MaxStat = ZoneScaling.ZoneStat.WeaponProcArcDmgMax,
            Min = 1, Max = 2, Min25 = 1, Max25 = 2, Lo = 1, Hi = 100000,
        };

        /// <summary>The ring's own band. Seeded identical to the arc's so the two start level and the
        /// owner can tune them apart from a known baseline - NOT because they are expected to stay
        /// equal. A ring is 9 projectiles centred on the wielder; an arc is one at the target.</summary>
        public static readonly WeaponBand WeaponProcRingDamage = new WeaponBand
        {
            Name = "Cast on Strike (Ring)", MinStat = ZoneScaling.ZoneStat.WeaponProcRingDmgMin, MaxStat = ZoneScaling.ZoneStat.WeaponProcRingDmgMax,
            Min = 1, Max = 2, Min25 = 1, Max25 = 2, Lo = 1, Hi = 100000,
        };

        public static readonly WeaponBand WeaponSlayer = new WeaponBand
        {
            Name = "Slayer", MinStat = ZoneScaling.ZoneStat.WeaponSlayerMin, MaxStat = ZoneScaling.ZoneStat.WeaponSlayerMax,
            // T11 floor RE-ANCHORED 2026-08-25 (owner): exactly 1.75, the highest value a player can
            // APPLY to a weapon. Fifteen separate stones and gems all SetValue exactly 1.75 and each
            // reaches 508-535 real weapon WCIDs, so it is not a rarity - it is the baseline any drop has
            // to at least match. Retail Isparian weapons reach 4.0 and the Weeping Axe 3.4, but those are
            // fixed weapons you WIELD rather than something you apply to a drop, so they are not the bar.
            // NOTE Lo below is 1.5, which sits BENEATH the free craftable value - it was never a
            // meaningful floor. The engine's own no-match fallback is 1.0, not 1.5.
            // DECIDED 2026-08-29 (owner, +20%/tier budget): T11 max 2.10 = +20% over the craft tie
            // for an S-grade; T25 3.00-4.10 carries slayer's even third of the ~13x BiS budget.
            Min = 1.75, Max = 2.10, Min25 = 3.00, Max25 = 4.10, Lo = 1.5, Hi = 10.0,
        };

        // Shield Cleaving -> IgnoreShield, the fraction of the defender's shield AL ignored. Plugin
        // box is PERCENT 0..100; wire is a 0..1 fraction. Conversion: wire = box / 100.
        // Box 60-75 -> 0.60-0.75 at T11; box 90-100 -> 0.90-1.00 at T25 (1.00 = shield fully ignored).
        public static readonly WeaponBand WeaponShieldCleave = new WeaponBand
        {
            Name = "Shield Cleaving", MinStat = ZoneScaling.ZoneStat.WeaponShieldCleaveMin, MaxStat = ZoneScaling.ZoneStat.WeaponShieldCleaveMax,
            // Floor re-ruled 2026-08-30 (owner: "fifty at T11 and a hundred percent at T25") -
            // 0.60 was the tie-the-vanilla-imbue floor; 50 now sits UNDER the best craftable
            // armor-rend-style value on purpose. T25 max 1.00 = total shield bypass, unchanged.
            Min = 0.50, Max = 0.75, Min25 = 0.90, Max25 = 1.00, Lo = 0.0, Hi = 1.0,
        };

        // Crushing Blow -> PropertyFloat.CriticalMultiplier. The band is in DISPLAY space, i.e. the
        // final crit damage multiplier the card advertises (7.50 = 7.5x), which is also what the
        // plugin box holds - weapon_crush_* is NOT a PercentStat, so there is NO conversion here.
        // The "- 1.0" that turns a display multiplier into the stored CriticalMultiplier does NOT live
        // in this table and must never be baked into it, or the two spaces get mixed and every
        // comparison is off by exactly one. Since 2026-08-25 it lives in exactly one method,
        // ZoneStatResolver.EngineValue, which both the drop-time write site and the equip-time
        // re-resolve call - the card's value is produced from a recorded grade now, so a second
        // subtraction anywhere would walk a 7.5x weapon down by 1.0 on every single login.
        public static readonly WeaponBand WeaponCrush = new WeaponBand
        {
            Name = "Crushing Blow", MinStat = ZoneScaling.ZoneStat.WeaponCrushMin, MaxStat = ZoneScaling.ZoneStat.WeaponCrushMax,
            // T11 floor RE-ANCHORED 2026-08-25 (owner): exactly the best general craft. These numbers
            // are in DISPLAY space (EngineValue subtracts the 1.0 at the single write site), and the
            // craft SetValues the STORED property, so the two spaces have to be lined up carefully:
            //   Bag of Abyssal-Touched Gems / Salvaged Turquoise store 2.25 -> 3.25x in combat
            //   Salvaged Turquoise (527870007)          stores 2.45 -> 3.45x in combat
            // 3.25 display here == the bag exactly. NOTE 3.45 is the true general ceiling, so a
            // floor-rolled T11 card ties the bag but is still beaten by that one turquoise recipe.
            // (Crimson Night Gem Setting reaches 3.0 stored / 4.0x, but it is retail quest content
            // that applies ONLY to a Silifi of Crimson Stars - not a general competitor.)
            // The old 7.50 cleared the Crippling Blow imbue's 7.0x; that imbue is suppressed for
            // players now.
            // DECIDED 2026-08-29 (owner, +20%/tier budget): T11 3.25-4.00 (S-grade beats the bag),
            // T25 5.50-6.80 - paired with Bite's 0.50-0.60 the crit pair carries its even third of
            // the ~13x BiS budget without the 9.5-10x every-hit-crits meta. Under the unified crit
            // model (same night) this band IS the crit ratio's bound on all three schools - the old
            // player_crit_damage_cap clamp was deleted with it.
            Min = 3.25, Max = 4.00, Min25 = 5.50, Max25 = 6.80, Lo = 2.0, Hi = 10.0,
        };

        /// <summary>Every weapon band, for help text / catalog printouts. Order is display order.</summary>
        public static readonly WeaponBand[] WeaponBands =
        {
            WeaponBite, WeaponArmorRend, WeaponRendPower, WeaponSlayer, WeaponShieldCleave, WeaponCrush,
            WeaponProcArcDamage, WeaponProcRingDamage,
        };

        /// <summary>
        /// The weapon band at a tier: linear on (tier - 11) / 14, clamped to [11, 25] - the SAME
        /// anchoring shape <see cref="CatalogBandAt"/> uses for a Min25/Max25 armour row, so a weapon
        /// band and a cantrip band read the same way and a reader only has to learn the rule once.
        /// The only difference is that nothing is rounded: these are doubles all the way through.
        ///
        /// The tier passed in MUST be the loot tier (the lootTier PARAMETER of TrySpecialRolls), not a
        /// property read off the weapon. At roll time the weapon carries NO tier property at all -
        /// ZcTier is never stamped on weapons, and WeaponAugScaleTier is stamped in Creature_Death
        /// AFTER MutateLootItem has already run. Asking the item (ZoneStatResolver.TierOf or
        /// ZoneCraftGate.TierOf) therefore returns 0 at THIS instant and every band would collapse to
        /// its T11 rung with no error anywhere. Both of those helpers are fine LATER in the same sweep -
        /// the drop-path self-check and the equip-time re-resolve both use them - just not here.
        /// </summary>
        public static (double Min, double Max) WeaponBandAt(WeaponBand b, int tier)
        {
            if (b == null) return (0.0, 0.0);
            var (min, max) = b.Min <= b.Max ? (b.Min, b.Max) : (b.Max, b.Min);
            var (min25, max25) = b.Min25 <= b.Max25 ? (b.Min25, b.Max25) : (b.Max25, b.Min25);
            var t = (Math.Clamp(tier, 11, 25) - 11) / 14.0;   // 0 at T11 and below .. 1 at T25 and above
            var lo = min + (min25 - min) * t;
            var hi = max + (max25 - max) * t;
            if (lo > hi) (lo, hi) = (hi, lo);                 // insurance against a hand-edited crossing ladder
            return (Math.Clamp(lo, b.Lo, b.Hi), Math.Clamp(hi, b.Lo, b.Hi));
        }

        /// <summary>
        /// Tier-weighted roll position (owner 2026-08-22, Option A): the carrot for pushing tiers is
        /// "better gear AND better-rolled gear". Every band splits into thirds (low / mid / high); the
        /// third is picked by these weights, then the value rolls uniform inside it. T11 = 33/33/34
        /// (today's uniform roll - T11 is deliberately UNCHANGED for ship), T25 = 10/30/60, linear
        /// between, clamped outside. Three-outcome lines (Reinforced +1/+2/+3, Life on Hit 1/2/3 pct)
        /// use the thirds directly. One curve for everything; Tempered's per-tier odds will ride it too.
        /// </summary>
        public static (int Lo, int Mid, int Hi) TierThirds(int tier)
        {
            var f = Math.Clamp((tier - 11) / 14.0, 0.0, 1.0);
            var lo = (int)Math.Round(33 + (10 - 33) * f);
            var mid = (int)Math.Round(33 + (30 - 33) * f);
            return (lo, mid, Math.Max(0, 100 - lo - mid));
        }

        /// <summary>Reinforced rank -> the ArmorModVs* value it sets (client label thresholds: Superior 1.40,
        /// Excellent 1.60, Unparalleled 1.80).</summary>
        public static double ReinforcedMod(int rank) => rank >= 3 ? 1.80 : rank == 2 ? 1.60 : 1.40;

        private static readonly PropertyFloat[] ReinforcedMods =
        {
            PropertyFloat.ArmorModVsSlash, PropertyFloat.ArmorModVsPierce, PropertyFloat.ArmorModVsBludgeon, PropertyFloat.ArmorModVsFire,
            PropertyFloat.ArmorModVsCold, PropertyFloat.ArmorModVsAcid, PropertyFloat.ArmorModVsElectric, PropertyFloat.ArmorModVsNether,
        };

        /// <summary>The slot specials (Armor v2) — the per-kill special roll picks one of these at random.</summary>
        public static List<Def> SlotSpecials()
        {
            var list = new List<Def>();
            foreach (var def in Catalog.Values)
                if (def.SlotSpecial)
                    list.Add(def);
            return list;
        }

        private static void AddInt(WorldObject wo, int propId, int value)
        {
            var cur = wo.GetProperty((PropertyInt)propId) ?? 0;
            wo.SetProperty((PropertyInt)propId, cur + value);
        }

        /// <summary>
        /// GRADED stamp (live stat resolution, 2026-08-22): records (key, grade) in the piece's ZcModifiers
        /// record, SETS the line's props to the value the grade resolves to inside the EFFECTIVE band
        /// (what a later ladder apply will recompute against the live band), and writes the same drop line
        /// text as <see cref="Stamp"/>. SET, not additive - one line per key per piece, the record is the
        /// truth and the props are its cache. Reinforced (SetsProtection) is NOT graded - it is earned and
        /// frozen (owner ruling) - so callers route key 49 through the plain Stamp. Returns the value.
        /// Callers must also <see cref="ZoneStatResolver.StampIdentity"/> the piece once (tier + version).
        /// </summary>
        public static int StampGraded(WorldObject wo, Def def, int grade, (int Min, int Max) band)
        {
            if (wo == null || def == null)
                return 0;
            if (def.SetsProtection)
            {
                var rank = ZoneStatResolver.ValueFor(band.Min, band.Max, grade);
                Stamp(wo, def, rank, (band.Min, band.Max));
                return rank;
            }

            var value = ZoneStatResolver.ValueFor(band.Min, band.Max, grade);
            ZoneStatResolver.AddLine(wo, def.Key, grade);

            if (def.ArmorOnly && def.Ints == null)
            {
                // key 25 Armor Level: the piece's AL = tier base + the line; SET through the base so a
                // re-roll of the same key never stacks
                var tier = ZoneStatResolver.TierOf(wo);
                if (wo.ArmorLevel.HasValue)
                    wo.ArmorLevel = (tier > 0 ? ZoneStatResolver.BaseArmorLevel(tier) : wo.ArmorLevel.Value) + value;
            }
            else if (def.Ints != null)
            {
                foreach (var (propId, _) in def.Ints)
                    wo.SetProperty((PropertyInt)propId, value);
            }

            string line;
            if (def.SlotSpecial)
                line = string.IsNullOrEmpty(def.ValFmt) ? $"Zone Cantrip: {def.Name}" : $"Zone Cantrip: {def.Name} {string.Format(def.ValFmt, value, value / 10.0)}";
            else
                line = $"Zone Cantrip: {def.Name} {string.Format(def.ValFmt ?? "+{0}", value, value / 10.0)} [{band.Min}-{band.Max}]";
            wo.LongDesc = string.IsNullOrEmpty(wo.LongDesc) ? line : wo.LongDesc + "\n\n" + line;
            return value;
        }

        // The pre-band fixed-value Stamp(wo, def) overload was DELETED 2026-08-21: it had zero
        // callers left, and for the banded-only keys 33-42 (Ints Value = 0 / no Ints at all) it
        // would stamp nothing while still marking the item. Roll a value and use the overload below.

        /// <summary>Banded stamp: writes the ROLLED value additively into each Ints PropId (the legacy fixed
        /// Value is ignored), handles the ArmorOnly item-AL mechanism, and marks the item with the
        /// plugin-format drop line ("Name +73 [25-100]"). The optional band is the
        /// EFFECTIVE band the value was rolled from (zone override) — omitted, the catalog band is printed.</summary>
        public static void Stamp(WorldObject wo, Def def, int value, (int Min, int Max)? band = null)
        {
            if (wo == null || def == null)
                return;

            if (def.SlotSpecial)
            {
                // Armor v2 slot special: the value is written to every Ints prop (magnitude-less keys
                // stamp 1 = "present"); the combat side reads it MAX-wins across worn pieces. The drop
                // line MUST start with "Zone Cantrip:" or FinalizeT11LongDesc deletes it; magnitude-less
                // keys (42/45) print bare, 41/44/46 print the rolled value.
                if (def.Ints != null)
                    foreach (var (propId, _) in def.Ints)
                        AddInt(wo, propId, value);

                var specialLine = string.IsNullOrEmpty(def.ValFmt)
                    ? $"Zone Cantrip: {def.Name}"
                    : $"Zone Cantrip: {def.Name} {string.Format(def.ValFmt, value, value / 10.0)}";
                wo.LongDesc = string.IsNullOrEmpty(wo.LongDesc) ? specialLine : wo.LongDesc + "\n\n" + specialLine;
                return;
            }

            if (def.SetsProtection)
            {
                // key 49 Reinforced: the value is a RANK. SET every elemental armor mod on the piece to the
                // rank's value (this runs after ApplyT11GearStats equalized them at ~1.30). Base mods are
                // item data, so hollow mobs (IgnoreMagicArmor) cannot strip this the way they strip Banes.
                var mod = ReinforcedMod(value);
                if (wo.ArmorLevel.HasValue)
                    foreach (var prop in ReinforcedMods)
                        wo.SetProperty(prop, mod);
                if (def.Ints != null)
                    foreach (var (propId, _) in def.Ints)
                        wo.SetProperty((PropertyInt)propId, value);     // bookkeeping, not summed-on-equip semantics
            }
            else if (def.ArmorOnly)
            {
                // key 25: modifies the item itself, no Ints — the rolled value, NOT the legacy 300
                if (wo.ArmorLevel.HasValue)
                    wo.ArmorLevel = wo.ArmorLevel.Value + value;
            }
            else if (def.Ints != null)
            {
                foreach (var (propId, _) in def.Ints)
                    AddInt(wo, propId, value);
            }

            var (min, max) = band ?? (def.Min, def.Max);
            var rolled = string.Format(def.ValFmt ?? "+{0}", value, value / 10.0);
            var line = $"Zone Cantrip: {def.Name} {rolled} [{min}-{max}]";
            wo.LongDesc = string.IsNullOrEmpty(wo.LongDesc) ? line : wo.LongDesc + "\n\n" + line;
        }
    }
}
