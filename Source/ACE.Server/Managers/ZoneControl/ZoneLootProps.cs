using System;
using System.Collections.Generic;

using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// LOOT PROPERTIES (owner 2026-10-26): the tier 11+ numbers the examine panel already prints as text, sent as plain
    /// int / float properties so a VTank loot rule (LongValKeyGE / DoubleValKeyGE on any key) can read them instead of
    /// matching description text. Added to the appraisal RESPONSE COPY only - nothing here is ever stored on the item, so
    /// every piece already in the world gets them, and they can never disagree with the panel because they come from the
    /// same resolve the panel's lines do.
    ///
    /// Values are UNLOCKED (what the item rolled, not what a zone lock lets it give where the examiner stands) and in the
    /// units the panel prints (Crushing Blow 1.85, not the stored 0.85; Armor Rend / Shield Cleave / Spell Armor Rend clamped
    /// to 0-1 as the panel clamps them). Two exceptions to "as printed": Pct HP Damage (key 44) is sent in TENTHS of a percent
    /// as stored (the panel prints value / 10), and the Cast on Strike damage cards (-17 / -18) send the stored damage, which
    /// the panel never prints (it shows the spell name and proc chance).
    ///
    /// Id block 58000-58999 (free in every property type; 53000 = Dressing Room, 56000 = Deeds):
    ///   58000 + catalog key   gear line VALUE (int)       58500 + catalog key   gear line GRADE 0-1000 (int)
    ///   58100 - weapon key    weapon card VALUE (float)   58600 - weapon key    weapon card GRADE 0-1000 (int)
    ///   58900 Gear Grade 0-1000 | 58901 Properties used | 58902 Properties limit | 58903 All Creatures slayer (1)
    ///   58999 format version - present on every tier 11+ item, so a loot profile can tell an old server from a new one.
    /// Catalog keys are 19-54 (legacy core resists c1..c4 report as their Always Rolled twins 50-53); weapon keys are
    /// -11..-30. Both are stable (saved records), so these ids are too. NEVER renumber: loot profiles in the wild use them.
    /// </summary>
    public static class ZoneLootProps
    {
        public const int FormatVersion = 1;

        public const int GearValueBase = 58000;
        public const int WeaponValueBase = 58100;
        public const int GearGradeBase = 58500;
        public const int WeaponGradeBase = 58600;
        public const int GearGradeId = 58900;
        public const int PropertiesUsedId = 58901;
        public const int PropertiesLimitId = 58902;
        public const int SlayerAllCreaturesId = 58903;
        public const int VersionId = 58999;

        public const int BlockMin = 58000;
        public const int BlockMax = 58999;

        /// <summary>The catalog key a record key reports under: itself, or for a legacy core resist (-1..-4) its Always Rolled
        /// twin (50..53). Null for anything else (weapon keys have their own ids).</summary>
        public static int? GearKey(int recordKey)
        {
            if (ZoneStatResolver.IsCoreKey(recordKey))
                return 49 - recordKey;          // -1 Damage Resist -> 50 ... -4 Nether Resist -> 53
            return recordKey > 0 && recordKey < 100 ? recordKey : null;
        }

        public static int GearLineValueId(int catalogKey) => GearValueBase + catalogKey;
        public static int GearLineGradeId(int catalogKey) => GearGradeBase + catalogKey;
        public static int WeaponValueId(int weaponKey) => WeaponValueBase - weaponKey;
        public static int WeaponGradeId(int weaponKey) => WeaponGradeBase - weaponKey;

        /// <summary>
        /// Add the loot properties for <paramref name="wo"/> to the response dictionaries. <paramref name="resolved"/> is the
        /// appraisal's own <see cref="ZoneStatResolver.Compute"/> result (null = no record); <paramref name="properties"/> is
        /// <see cref="GearEssences.PropertiesCount"/> (passed in so the tier-limit lookup stays out of the pure part).
        /// <paramref name="legacyText"/> is true for a piece whose block comes from baked "Zone Cantrip:" text only (no record):
        /// the panel shows no Properties count for it, because there is nothing to count.
        /// <paramref name="ifEquipped"/> is the appraisal's <see cref="ZoneStatResolver.FloatsIfEquipped(WorldObject, ZoneStatResolver.Resolved)"/>
        /// result, passed in so it is worked out once per appraisal; null = work it out here.
        /// </summary>
        public static void Collect(WorldObject wo, ZoneStatResolver.Resolved resolved, (int Count, int Cap)? properties, bool legacyText,
            IDictionary<PropertyInt, int> ints, IDictionary<PropertyFloat, double> floats, IReadOnlyDictionary<PropertyFloat, double> ifEquipped = null)
        {
            if (wo == null || wo is Creature || ints == null || floats == null)
                return;
            if (resolved == null && ZoneStatResolver.TierOf(wo) < LootGenerationFactory.ZoneLootSetMinTier)
                return;

            ints[(PropertyInt)VersionId] = FormatVersion;
            var weapon = GearEssences.IsWeapon(wo);

            // gear lines, exactly as the panel lists them (record order; specials, Armor Level and the Always Rolled resists
            // included), each with the tinker on its own prop added - the panel shows that as "(+n tinkered)" on the line
            if (resolved != null)
            {
                var tinker = ZoneStatResolver.ReadTinkerBonus(wo);
                foreach (var line in resolved.Lines)
                {
                    var key = GearKey(line.Record.Key);
                    if (key == null)
                        continue;
                    var tinkered = 0;
                    if (tinker.Count > 0 && line.Def?.Ints != null)
                        foreach (var (propId, _) in line.Def.Ints)
                            if (tinker.TryGetValue(propId, out var add)) { tinkered = add; break; }
                    ints[(PropertyInt)GearLineValueId(key.Value)] = line.Value + tinkered;
                    // an on/off special (Battle Mending, Cheat Death) has no grade - the panel's own test for "no magnitude"
                    if (!(line.Def != null && line.Def.SlotSpecial && string.IsNullOrEmpty(line.Def.ValFmt)))
                        ints[(PropertyInt)GearLineGradeId(key.Value)] = line.Record.Grade;
                }
            }

            // Reinforced: earned and frozen, never in the record - the rank stamped on the piece (1-3), no grade
            var rank = wo.GetProperty((PropertyInt)ZoneModifiers.ReinforcedRank) ?? 0;
            if (rank > 0)
                ints[(PropertyInt)GearLineValueId(ZoneModifiers.ReinforcedKey)] = rank;

            // weapon cards: the value the item would have once equipped (bandit hilt included, as the panel shows it; owner
            // 2026-10-29: an unworn weapon's stored number lags a band retune until equip) in display space, and the recorded
            // grade. Only cards in the record - a retail Biting Strike with no card is not a tier 11+ roll.
            var current = ifEquipped ?? ZoneStatResolver.FloatsIfEquipped(wo, resolved);
            foreach (var rec in ZoneStatResolver.Read(wo))
            {
                if (!ZoneStatResolver.IsWeaponKey(rec.Key) || !ZoneStatResolver.TryGetWeapon(rec.Key, out var ws))
                    continue;
                var stored = current.TryGetValue(ws.Prop, out var now) ? now : wo.GetProperty(ws.Prop);
                if (stored == null)
                    continue;
                var value = ws.DisplayIsMultiplier ? stored.Value + 1.0 : stored.Value;
                if (ClampedToFraction(ws))
                    value = Math.Clamp(value, 0.0, 1.0);
                floats[(PropertyFloat)WeaponValueId(rec.Key)] = value;
                ints[(PropertyInt)WeaponGradeId(rec.Key)] = rec.Grade;
            }

            // Gear Grade: armour / jewelry / clothing / cloak whose record resolved to at least one line - the panel draws
            // no block (so no Gear Grade) for a record whose keys this build does not know
            if (!weapon && resolved != null && resolved.Lines.Count > 0)
            {
                var g = ZoneStatResolver.GearGrade(wo);
                if (g != null)
                    ints[(PropertyInt)GearGradeId] = g.Value.Grade;
            }

            // "Properties: X of Y" - weapons always, gear unless it is a text-only legacy piece (the panel's rule)
            if (properties != null && (weapon || !legacyText))
            {
                ints[(PropertyInt)PropertiesUsedId] = properties.Value.Count;
                if (properties.Value.Cap != int.MaxValue)
                    ints[(PropertyInt)PropertiesLimitId] = properties.Value.Cap;
            }

            if (wo.GetProperty(PropertyBool.SlayerAllCreatures) == true)
                ints[(PropertyInt)SlayerAllCreaturesId] = 1;
        }

        /// <summary>The cards the panel prints as a clamped 0-100 pct (BuildWeapon: Armor Cleaving, Shield Cleaving, Spell Armor Rend).</summary>
        private static bool ClampedToFraction(ZoneStatResolver.WeaponSpecial ws)
            => ws == ZoneStatResolver.SpecArmorRend || ws == ZoneStatResolver.SpecShieldCleave || ws == ZoneStatResolver.SpecSpellArmorRend;
    }
}
