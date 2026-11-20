using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Attributes and vitals. Checked against ZoneSpawnScaler.ApplyToSpawn (the only writer), Creature_Combat.GetAttributeMod,
    // CreatureSkill.Base, AttributeFormula, Monster_Awareness.CalculateCreaturePowerScore and Creature_Death (rare eligibility).
    public static partial class ZoneStatHelp
    {
        private const string AttributeShape =
            "An absolute value. It replaces the monster's own base attribute (monsters have no trained ranks, so this IS their base). "
            + "Rounded to a whole number, minimum 1. Player buffs and debuffs on the monster still apply on top.";

        private const string AttributeUnset = "The monster keeps the attribute its weenie ships with.";

        private const string SkillsNote =
            "Like every attribute it also feeds the monster's skills through the retail skill formulas, unless the zone sets that skill "
            + "directly (attack_skill, melee_defense, missile_defense, magic_defense, magic_skill replace the result outright).";

        static partial void AddAttributes(List<ZoneStatHelpEntry> e)
        {
            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.Strength,
                Summary = "The monster's Strength.",
                Details = "Strength scales the damage of the monster's melee attacks: unarmed, Light, Heavy and Two-Handed weapons "
                    + "(bows and Finesse weapons use Coordination instead). Each point above 55 adds a little damage. "
                    + SkillsNote,
                Shape = AttributeShape,
                WhenUnset = AttributeUnset,
                Raise = "Harder melee hits, and higher Strength-based skills where the zone does not set the skill itself.",
                Lower = "Weaker melee hits. Below 55 it gives no damage bonus at all.",
                Range = "Any whole number from 1. Zone Control monsters usually sit near their tier's Default.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "If the zone sets attack_damage, Strength still multiplies that damage, so raising both stacks.",
                Related = new[] { ZoneStat.AttackDamage, ZoneStat.AttackSkill, ZoneStat.Coordination },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.Endurance,
                Summary = "The monster's Endurance.",
                Details = "Endurance feeds the monster's maximum Health (half of Endurance) and maximum Stamina (all of it) through the "
                    + "retail vital formulas. If max_health or max_stamina is set, that value wins: it is applied after the "
                    + "attributes, as the final maximum. " + SkillsNote,
                Shape = AttributeShape,
                WhenUnset = AttributeUnset,
                Raise = "More Health and Stamina, but only where max_health / max_stamina are not set.",
                Lower = "Less Health and Stamina where those maxima are not set.",
                Range = "Any whole number from 1.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "To change how much Health monsters have, use max_health: it lands on the exact number. Endurance only "
                    + "moves Health on monsters whose zone leaves max_health unset.",
                Related = new[] { ZoneStat.MaxHealth, ZoneStat.MaxStamina },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.Coordination,
                Summary = "The monster's Coordination.",
                Details = "Coordination scales the damage of the monster's bow and Finesse weapon attacks (each point above 55 adds a "
                    + "little; bows scale differently from melee). " + SkillsNote,
                Shape = AttributeShape,
                WhenUnset = AttributeUnset,
                Raise = "Harder bow and Finesse hits, and higher Coordination-based skills (for example the retail melee and missile "
                    + "defense formulas) where the zone does not set them.",
                Lower = "Weaker bow and Finesse hits.",
                Range = "Any whole number from 1.",
                Timing = ZoneStatTiming.Spawn,
                Related = new[] { ZoneStat.Strength, ZoneStat.MeleeDefense, ZoneStat.MissileDefense },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.Quickness,
                Summary = "The monster's Quickness.",
                Details = "Quickness has no direct damage or vital effect. It matters through the retail skill formulas (the melee and "
                    + "missile defense formulas use it). " + SkillsNote,
                Shape = AttributeShape,
                WhenUnset = AttributeUnset,
                Raise = "Higher Quickness-based skills, mainly defense, where the zone does not set them.",
                Lower = "Lower Quickness-based skills where the zone does not set them.",
                Range = "Any whole number from 1.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "On a zone that sets melee_defense and missile_defense, Quickness changes nothing you will notice.",
                Related = new[] { ZoneStat.MeleeDefense, ZoneStat.MissileDefense },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.Focus,
                Summary = "The monster's Focus.",
                Details = "Focus has no direct damage or vital effect. It matters through the retail skill formulas (the magic skill "
                    + "and magic defense formulas use it). " + SkillsNote,
                Shape = AttributeShape,
                WhenUnset = AttributeUnset,
                Raise = "Higher magic and magic defense skills where the zone does not set them.",
                Lower = "Lower magic and magic defense skills where the zone does not set them.",
                Range = "Any whole number from 1.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "On a zone that sets magic_skill and magic_defense, Focus changes nothing you will notice.",
                Related = new[] { ZoneStat.MagicSkill, ZoneStat.MagicDefense, ZoneStat.Self },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.Self,
                Summary = "The monster's Self (Willpower).",
                Details = "Self feeds the monster's maximum Mana through the retail vital formula; if max_mana is set, that value wins. "
                    + "It also feeds the magic skill and magic defense formulas. " + SkillsNote,
                Shape = AttributeShape,
                WhenUnset = AttributeUnset,
                Raise = "More Mana where max_mana is not set, and higher magic skills where the zone does not set them.",
                Lower = "Less Mana and lower magic skills, under the same conditions.",
                Range = "Any whole number from 1.",
                Timing = ZoneStatTiming.Spawn,
                Related = new[] { ZoneStat.MaxMana, ZoneStat.MagicSkill, ZoneStat.MagicDefense, ZoneStat.Focus },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MaxHealth,
                Summary = "The monster's maximum Health, as an exact number.",
                Details = "Applied at spawn after the attributes, so it is the final maximum whatever Endurance gives. The monster "
                    + "spawns full.",
                Shape = "An absolute value: the maximum Health the monster ends up with. Rounded to a whole number, minimum 1.",
                WhenUnset = "Health comes from the weenie and the Endurance formula.",
                Raise = "Monsters take longer to kill. Kill speed (kills per hour) falls roughly in proportion.",
                Lower = "Monsters die faster.",
                Range = "Any whole number from 1. Change it in steps and watch kills per hour: it is the most direct difficulty knob.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "Each rank row can carry its own value, so Leaders and Bosses can have more Health than Regulars.",
                Related = new[] { ZoneStat.Endurance, ZoneStat.DamageResistRating, ZoneStat.ArmorLevel },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MaxStamina,
                Summary = "The monster's maximum Stamina, as an exact number.",
                Details = "Applied at spawn after the attributes, so it is the final maximum whatever Endurance gives. The monster "
                    + "spawns full.",
                Shape = "An absolute value: the maximum Stamina. Rounded to a whole number, minimum 1.",
                WhenUnset = "Stamina comes from the weenie and the Endurance formula.",
                Raise = "Rarely matters. Monsters do not spend Stamina on their own attacks; it only drops from Stamina damage and "
                    + "drain effects. More Stamina makes it harder to drain a monster to 0.",
                Lower = "A monster at 0 Stamina is exhausted: its melee and missile defense drop to 0. Little Stamina makes that easier "
                    + "to reach.",
                Range = "Any whole number from 1. Leave it unset unless you want exhaustion to come into play.",
                Timing = ZoneStatTiming.Spawn,
                Related = new[] { ZoneStat.Endurance },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MaxMana,
                Summary = "The monster's maximum Mana, as an exact number.",
                Details = "Applied at spawn after the attributes, so it is the final maximum whatever Self gives. The monster spawns full.",
                Shape = "An absolute value: the maximum Mana. Rounded to a whole number, minimum 1.",
                WhenUnset = "Mana comes from the weenie and the Self formula.",
                Raise = "A casting monster can keep casting for longer.",
                Lower = "A casting monster can run dry. Non-casters do not care.",
                Range = "Any whole number from 1.",
                Timing = ZoneStatTiming.Spawn,
                Related = new[] { ZoneStat.Self, ZoneStat.SpellDamage },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MonsterLevel,
                Summary = "The level the monster shows and counts as.",
                Details = "Stamped on the monster at spawn, replacing its weenie's level. Appraisal shows it. It decides whether the "
                    + "monster can drop a retail rare: at level 100 or more it always can; below 100 only when it is higher level "
                    + "than the player who killed it. Other creatures also use it as its score when picking a target.",
                Shape = "An absolute value: the monster's level. Rounded to a whole number, minimum 1.",
                WhenUnset = "The monster keeps the level its weenie ships with, so a retail monster dropped into a T11 zone shows its "
                    + "retail level.",
                Raise = "Shows a higher level. Crossing 100 makes every kill eligible for a retail rare.",
                Lower = "Below 100, players of a higher level stop getting retail rare rolls from it.",
                Range = "Whole numbers from 1. Keep it at 100 or more on endgame zones unless you mean to cut retail rares.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "It does not change the monster's combat stats; those come from the other zone stats.",
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MonsterCreatureType,
                Summary = "A fallback species for monsters that have none.",
                Details = "Applied at spawn ONLY to a monster whose weenie has no creature type (or Invalid). A monster with a real "
                    + "species keeps it - a Drudge stays a Drudge. Without a species no Slayer weapon can roll in that monster's loot, "
                    + "and player Slayer weapons get no bonus against it.",
                Shape = "A creature type number from the game's CreatureType list (for example 3 = Drudge). An unknown number or 0 "
                    + "is ignored.",
                WhenUnset = "A monster with no species stays without one, and its loot can never roll a Slayer weapon.",
                Raise = "Not a scale: each number is a different species.",
                Lower = "Not a scale: each number is a different species.",
                Range = "A valid CreatureType number.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "The species also decides which Slayer weapons drop from these monsters, and which Slayer weapons hurt them more.",
            });
        }
    }
}
