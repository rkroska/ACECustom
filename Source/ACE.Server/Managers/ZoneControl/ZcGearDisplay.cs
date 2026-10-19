using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// CHARACTER PANEL DISPLAY of the T11+ gear lines (owner 2026-10-19). The server adds the All Attributes line, the
    /// skill lines, Fortify Vitals and Max Health Pct OUTSIDE the enchantment system (ZoneModifiers), so the combat math has
    /// them but the client - which works out attributes, skills and max vitals itself, from base + the enchantments it
    /// knows - never did, and the panel read low.
    ///
    /// The fix tells the client about them as DISPLAY-ONLY enchantments: sent to the owning client and never added to the
    /// server's enchantment registry, so they cannot be dispelled, saved, snapshotted or fed back into any server number.
    /// The worst a wrong one can do is show a wrong number.
    ///
    /// Client behaviour this relies on (decompiled acclient 11.4186 + 11.6096, Mag-nus/acclient-AI-RE):
    ///   - the attribute / skill / max-vital math uses only the enchantment's own stat type, key and value - no spell
    ///     table lookup - so a spell id the client does not know still applies (EnchantAttribute / EnchantAttribute2nd /
    ///     EnchantSkill);
    ///   - an unknown spell id is skipped, not dereferenced, by the spell-count totals and the effects list (no icon);
    ///   - the enchantment id is (spell, layer): re-sending the same pair REPLACES the entry, so we always send the TOTAL;
    ///   - stacking is by the enchantment's own category: each entry gets a category no real spell uses, so it never
    ///     competes with a buff;
    ///   - duration -1 (an item enchantment) survives the client's purges (death, dispel-all).
    /// </summary>
    public static class ZcGearDisplay
    {
        /// <summary>The display spell id. Above the last retail spell (6340), unused by any world spell, spellbook or emote
        /// (checked 2026-10-19; 8185 IS referenced by world content, avoid it). Startup refuses to send if a spell row or a
        /// dat spell ever takes this id (<see cref="CheckSpellIdFree"/>).</summary>
        public const ushort SpellId = 7878;

        /// <summary>Category base for the display entries: category = base + layer. Real categories end at 733 and 0x8000
        /// is the server's cooldown category, so 0x7800-0x7FFF is free and every entry stays below 0x8000.</summary>
        public const ushort CategoryBase = 0x7800;

        // layers: one per stat, so each entry is replaced in place
        public const ushort AttributeLayerBase = 0;     // + PropertyAttribute 1..6
        public const ushort VitalLayerBase = 100;       // + PropertyAttribute2nd MaxHealth 1 / MaxStamina 3 / MaxMana 5
        public const ushort SkillLayerBase = 200;       // + Skill

        // stat-mod flags copied from retail spells of the same shape
        public const EnchantmentTypeFlags AttributeAdditive = EnchantmentTypeFlags.Additive | EnchantmentTypeFlags.SingleStat | EnchantmentTypeFlags.Attribute;           // 0x9001, Legendary Strength
        public const EnchantmentTypeFlags SkillAdditive = EnchantmentTypeFlags.Additive | EnchantmentTypeFlags.SingleStat | EnchantmentTypeFlags.Skill;                   // 0x9010, Legendary War Magic Aptitude
        public const EnchantmentTypeFlags VitalMultiplier = EnchantmentTypeFlags.Multiplicative | EnchantmentTypeFlags.SingleStat | EnchantmentTypeFlags.SecondAtt;      // 0x5002, the max-vital curses (x0.5)

        public static readonly PropertyAttribute[] Attributes =
        {
            PropertyAttribute.Strength, PropertyAttribute.Endurance, PropertyAttribute.Coordination,
            PropertyAttribute.Quickness, PropertyAttribute.Focus, PropertyAttribute.Self,
        };

        public static readonly PropertyAttribute2nd[] Vitals =
        {
            PropertyAttribute2nd.MaxHealth, PropertyAttribute2nd.MaxStamina, PropertyAttribute2nd.MaxMana,
        };

        /// <summary>One display entry: what one (SpellId, Layer) enchantment carries.</summary>
        public readonly record struct Entry(ushort Layer, EnchantmentTypeFlags StatModType, uint StatModKey, float StatModValue)
        {
            public ushort Category => (ushort)(CategoryBase + Layer);
        }

        /// <summary>The value at which an entry changes nothing, so it is not sent (0 to add, 1 to multiply).</summary>
        public static float NeutralValue(EnchantmentTypeFlags type) => (type & EnchantmentTypeFlags.Multiplicative) != 0 ? 1.0f : 0.0f;

        /// <summary>
        /// Every entry the client should hold, from the SAME reads the server's own math uses:
        ///   bonus(propId) = Creature.GetZoneModifierBonus (summed across worn pieces, the line cap and the zone lock applied),
        ///   max(propId)   = Creature.GetZoneModifierMax   (the slot specials' max-wins, the zone lock applied),
        ///   skillPropIds  = the 50300-block props present on worn gear (no catalog line rolls them today; legacy pieces can).
        /// Neutral entries are left out. The mirrored formulas:
        ///   attribute: CreatureAttribute.GetCurrent adds the bonus after enchantments  -> an additive attribute entry;
        ///   skill:     CreatureSkill.GetAugBonus_Current adds it after vitae (positives only) -> an additive skill entry;
        ///   max vital: CreatureVital.GetMaxValue multiplies by 1 + (Fortify + Pct-for-health) / 100 -> a multiplier entry.
        /// </summary>
        public static List<Entry> Compute(Func<int, int> bonus, Func<int, int> max, IEnumerable<int> skillPropIds)
        {
            var entries = new List<Entry>();

            foreach (var attr in Attributes)
            {
                var value = bonus(ZoneModifiers.AttrBonusBase + (int)attr);
                if (value != 0)
                    entries.Add(new Entry((ushort)(AttributeLayerBase + (int)attr), AttributeAdditive, (uint)attr, value));
            }

            var fortify = Math.Max(0, max(ZoneModifiers.FortifyVitalsPct));
            var pctHealth = Math.Max(0, bonus(ZoneModifiers.PctMaxHealthPct));
            foreach (var vital in Vitals)
            {
                var pts = fortify + (vital == PropertyAttribute2nd.MaxHealth ? pctHealth : 0);
                if (pts > 0)
                    entries.Add(new Entry((ushort)(VitalLayerBase + (int)vital), VitalMultiplier, (uint)vital, (float)(1.0 + pts / 100.0)));
            }

            if (skillPropIds != null)
            {
                foreach (var propId in skillPropIds)
                {
                    var skill = propId - ZoneModifiers.SkillBonusBase;
                    if (skill <= 0 || propId > ZoneModifiers.PropMax)
                        continue;
                    var value = bonus(propId);
                    if (value > 0)      // GetAugBonus_Current only ever adds a positive skill line
                        entries.Add(new Entry((ushort)(SkillLayerBase + skill), SkillAdditive, (uint)skill, value));
                }
            }

            return entries;
        }

        /// <summary>The display enchantment for one entry, as the client receives it: item-style (duration -1, start 0).</summary>
        public static Network.Structure.Enchantment ToEnchantment(Entry entry, uint ownerGuid)
            => Network.Structure.Enchantment.ForDisplay(SpellId, entry.Layer, entry.Category, ownerGuid, entry.StatModType, entry.StatModKey, entry.StatModValue);

        private static bool? spellIdFree;

        /// <summary>True when no world spell row and no dat spell uses <see cref="SpellId"/>. Read once; if the id is ever taken
        /// the feature stays off (logged) rather than paint another spell's icon and name onto the gear bonus.</summary>
        public static bool CheckSpellIdFree()
        {
            if (spellIdFree.HasValue)
                return spellIdFree.Value;

            var inDat = DatLoader.DatManager.PortalDat?.SpellTable?.Spells?.ContainsKey(SpellId) == true;
            var inDb = Database.DatabaseManager.World?.GetCachedSpell(SpellId) != null;
            spellIdFree = !inDat && !inDb;
            if (!spellIdFree.Value)
                log4net.LogManager.GetLogger(typeof(ZcGearDisplay)).Error($"[ZC PANEL] spell id {SpellId} is in use (dat={inDat}, world db={inDb}) - the gear-line panel display is OFF until it moves to a free id.");
            return spellIdFree.Value;
        }
    }
}
