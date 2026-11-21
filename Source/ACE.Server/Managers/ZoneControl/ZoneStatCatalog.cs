using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>Display group and label for each stat, for the portal page. A new stat lands in a group by its prefix (or in Other) with no change here.</summary>
    public static class ZoneStatCatalog
    {
        public const string OtherGroup = "Other";

        // Page order. First match wins, so the narrow prefixes sit above the broad ones.
        private static readonly (string Group, string[] Prefixes)[] Rules =
        {
            ("Attributes and vitals", new[] { "strength", "endurance", "coordination", "quickness", "focus", "self", "max_", "monster_level", "monster_creature_type" }),
            ("Attack", new[] { "attack_", "magic_skill", "spell_damage", "spell_variance", "true_damage", "damage_rating", "crit_rating", "crit_damage_rating", "monster_damage_mult", "percent_hp_base" }),
            ("Defense", new[] { "melee_defense", "missile_defense", "magic_defense", "damage_resist_rating", "crit_resist_rating", "crit_damage_resist_rating", "armor_level", "spell_armor", "resist_", "armor_vs_", "monster_damage_taken_mult", "melee_damage_taken", "missile_damage_taken", "war_damage_taken", "void_damage_taken", "curse_damage_cut", "immune_" }),
            ("Debuffs and DoTs", new[] { "vuln_", "imperil_", "debuff_", "dot_" }),
            ("Relief and aug curves", new[] { "relief_", "aug_" }),
            ("Archetypes", new[] { "archetype_", "archer_", "caster_" }),
            ("Kill rewards", new[] { "xp_kill", "lum_award", "bonus_currency", "charm_level_cap", "pcoin_" }),
            ("Weapon cards", new[] { "weapon_proc_", "weapon_imbue_", "weapon_slayer_", "weapon_cleave_", "weapon_split_", "weapon_bite_", "weapon_crush_", "weapon_armor_rend_", "weapon_shield_cleave_", "weapon_rend_power_", "weapon_spell_armor_rend_", "slayer_pool_share" }),
            ("Armor and jewelry lines", new[] { "modifier_chance_", "weapon_modifier_", "armor_modifier_", "armor_base_", "armor_prot_", "gear_cap_", "jewelry_", "item_spell_" }),
            ("Specials and cloaks", new[] { "special_odds", "battlemend_", "pcthp_", "cheatdeath_", "lifeonhit_", "cloak_" }),
            ("Loot", new[] { "loot_", "drop_", "grade_", "weapon_attuned", "weapon_bonded", "weapon_unenchantable", "armor_attuned", "armor_bonded", "armor_unenchantable" }),
            ("Salvage bags", new[] { "bag_" }),
            ("Item bounties", new[] { "bounty_" }),
        };

        private static readonly Regex ModifierChance = new Regex(@"^modifier_chance_(\d+)$", RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> Groups { get; } = Rules.Select(r => r.Group).Append(OtherGroup).ToList();

        public static string GroupOf(string stat)
        {
            foreach (var (group, prefixes) in Rules)
                if (prefixes.Any(p => stat.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    return group;
            return OtherGroup;
        }

        public static string LabelOf(string stat)
        {
            var anchored = stat.EndsWith("_t25", StringComparison.OrdinalIgnoreCase);
            var key = anchored ? stat.Substring(0, stat.Length - 4) : stat;

            string label;
            var m = ModifierChance.Match(key);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var line) && ZoneModifiers.TryGet(line, out var def))
                label = def.Name + " chance";
            else
            {
                label = key.Replace('_', ' ');
                label = char.ToUpperInvariant(label[0]) + label.Substring(1);
            }
            return anchored ? label + " (T25)" : label;
        }
    }
}
