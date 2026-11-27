using System.Collections.Generic;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Defense stats. Checked against Creature_Combat.GetEffectiveDefenseSkill, Creature_Magic.GetEffectiveMagicDefense,
    // Creature_Rating (damage resist, crit resist and crit damage resist ratings), ZoneSpawnScaler.ApplyRatingProp, Creature_BodyPart
    // (armor), Creature_Properties (resist and armor-vs-type multipliers), Creature_SpellArmor, ZoneControlManager.MonsterDamageTakenMultFor,
    // Creature_CurseCut, Creature_CharmImmunity and Creature_ShrapnelAgonyImmunity. All of them are read per hit, cast or check except the
    // two crit resist ratings, which are stamped when the monster spawns.
    public static partial class ZoneStatHelp
    {
        private const string HitGateNote =
            "At the higher tiers a separate hit gate can also stop an under-augmented player's attacks outright, whatever this number is.";

        static partial void AddDefense(List<ZoneStatHelpEntry> e)
        {
            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MeleeDefense,
                Summary = "The monster's melee defense skill: how well it avoids melee hits.",
                Details = "When a player swings at the monster, the game compares the player's attack skill with the monster's effective "
                    + "melee defense to decide whether the swing lands. If the zone sets this stat, that number is the defense, and the "
                    + "monster's own skill, weapon, burden and imbues are skipped.",
                Shape = "An absolute value that replaces the monster's defense. Rounded to a whole number. A negative value is treated as 0. "
                    + "A monster that is out of stamina (exhausted) counts as 0 whatever this is set to.",
                WhenUnset = "The monster's own melee defense skill is used, with its normal modifiers from weapon, burden and imbues.",
                Raise = "Players miss the monster more often.",
                Lower = "Players land more of their melee swings.",
                Range = "Any number from 0. Anything below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = HitGateNote + " Missile attacks use missile_defense and spells use magic_defense, not this one.",
                Related = new[] { ZoneStat.MissileDefense, ZoneStat.MagicDefense, ZoneStat.AttackSkill },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MissileDefense,
                Summary = "The monster's missile defense skill: how well it avoids arrows and thrown weapons.",
                Details = "Works like melee_defense, but is used when the attack coming in is a missile attack. The game compares the "
                    + "player's attack skill with this number to decide whether the shot lands.",
                Shape = "An absolute value that replaces the monster's defense. Rounded to a whole number. A negative value is treated as 0. "
                    + "A monster that is out of stamina (exhausted) counts as 0 whatever this is set to.",
                WhenUnset = "The monster's own missile defense skill is used, with its normal modifiers from weapon, burden and imbues.",
                Raise = "Players miss the monster more often with missile weapons.",
                Lower = "Players land more of their missile shots.",
                Range = "Any number from 0. Anything below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = HitGateNote + " Melee attacks use melee_defense, not this one.",
                Related = new[] { ZoneStat.MeleeDefense, ZoneStat.MagicDefense },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MagicDefense,
                Summary = "The monster's magic defense skill: how well it resists spells.",
                Details = "When a player casts a spell at the monster, the game compares the caster's skill with the monster's magic defense "
                    + "to decide whether the spell is resisted. If the zone sets this stat, that number is the monster's magic defense, "
                    + "and its own skill, weapon bonus and imbues are skipped. The Pandemic charm's spread also uses it when that charm's resist option is on.",
                Shape = "An absolute value that replaces the monster's defense. Rounded to a whole number. A negative value is treated as 0.",
                WhenUnset = "The monster's own magic defense skill is used, with its weapon modifier and imbues.",
                Raise = "Player spells are resisted more often.",
                Lower = "Fewer player spells are resisted.",
                Range = "Any number from 0. Anything below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = HitGateNote + " Unlike the melee and missile stats, being exhausted does not zero this one. "
                    + "This only decides whether a spell lands: how much damage it does once it lands is set by spell_armor and the resist stats.",
                Related = new[] { ZoneStat.MeleeDefense, ZoneStat.MissileDefense, ZoneStat.SpellArmor, ZoneStat.MagicSkill },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DamageResistRating,
                Summary = "The monster's Damage Resist Rating.",
                Details = "Every hit and spell that damages the monster is scaled by 100 / (100 + rating): a rating of 100 halves the "
                    + "damage. This stat replaces the monster's own base rating before that is worked out. Ratings and enchantments on the "
                    + "monster are still added on top of it.",
                Shape = "A rating, as a whole number. It replaces the monster's own base Damage Resist Rating. Rounded to a whole number.",
                WhenUnset = "The monster uses the Damage Resist Rating its weenie ships with (0 if it has none).",
                Raise = "The monster takes less damage from every hit and spell.",
                Lower = "The monster takes more damage. A negative rating makes it take more than normal.",
                Range = "Any whole number. Zero means no change. A negative rating makes the monster take extra damage; how steeply depends on a server setting (allow_negative_rating_curve).",
                Timing = ZoneStatTiming.Live,
                Caution = "Crits also run through crit_damage_resist_rating on top of this. On direct hits, a player's void damage-over-time "
                    + "spells also lower the monster's effective rating, unless the server's endgame debuff rules apply to that monster.",
                Related = new[] { ZoneStat.CritDamageResistRating, ZoneStat.ArmorLevel, ZoneStat.MonsterDamageTakenMult, ZoneStat.SpellArmor },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArmorLevel,
                Summary = "The monster's base armor level, for every body part.",
                Details = "When a melee or missile hit lands, the monster's armor for that damage type is its base armor level times the "
                    + "armor_vs_<type> multiplier, plus any armor enchantments and armor worn by the monster. That total is turned into a "
                    + "damage factor of 66.67 / (66.67 + armor): an armor of 66.67 halves the damage. This stat replaces the base armor level "
                    + "of every body part at once.",
                Shape = "An absolute armor level that replaces the monster's base armor. It beats any per-body-part armor set in the zone, "
                    + "which beats the weenie's own. Armor rend from the attacker lowers the total when it is above 0.",
                WhenUnset = "A body part armor set in the zone is used if there is one, otherwise each body part's own armor from the weenie.",
                Raise = "Melee and missile hits do less damage.",
                Lower = "Melee and missile hits do more damage. Below 0 the monster takes extra damage (the factor goes above x1).",
                Range = "Any number. 0 means no armor (x1 damage). A negative level adds damage instead of cutting it.",
                Timing = ZoneStatTiming.Live,
                Caution = "Spells skip armor, so this does not slow spell damage: use spell_armor for that. Setting this stat also hides any "
                    + "weak spot armor set on individual body parts in the zone.",
                Related = new[] { ZoneStat.SpellArmor, ZoneStat.ArmorVsSlash, ZoneStat.DamageResistRating, ZoneStat.MonsterDamageTakenMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.SpellArmor,
                Summary = "Armor that player spells have to get through. Spells ignore normal armor, so this is their armor.",
                Details = "A player's spell damage on the monster is multiplied by 66.67 / (66.67 + level), the same curve armor uses: a level "
                    + "of 66.67 halves it. It covers hand-cast spells, Cast on Strike procs, ring spells and the damage-over-time ticks of void spells. "
                    + "It never makes a spell miss: landing is magic_defense. A Spell Armor Rend card on the player's wand ignores a share of this level.",
                Shape = "An armor level, any positive number. 0 or below applies nothing.",
                WhenUnset = "Spells deal their full damage (x1).",
                Raise = "Player spells do less damage.",
                Lower = "Player spells do more damage. At 0 the stat does nothing.",
                Range = "Any number above 0. 0 or below is the same as unset, so it cannot be used to boost spell damage.",
                Timing = ZoneStatTiming.Live,
                Caution = "Does nothing while Zone Control itself is off (zonecontrol_enabled). Only spells cast by players count: "
                    + "pets, other monsters and spells on players never use it. Imperil and Armor Rend do not touch it.",
                Related = new[] { ZoneStat.ArmorLevel, ZoneStat.MagicDefense, ZoneStat.DotArmor, ZoneStat.DamageResistRating },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ImmuneShrapnelAgony,
                Summary = "Makes the monster take no damage from Rocky Shrapnel and Ring of Unspeakable Agony.",
                Details = "When a player's Rocky Shrapnel or Ring of Unspeakable Agony would damage the monster, the game checks this stat. "
                    + "At 1 the damage from those two spells is 0, and the caster gets a message that the monster is immune. "
                    + "Every other spell and attack is unaffected.",
                Shape = "An on/off switch. 0.5 or more means immune, anything below means not immune. Use 1 to turn it on.",
                WhenUnset = "The monster takes damage from the two spells like any other spell.",
                Raise = "At 1 the monster is immune to the two spells.",
                Lower = "At 0 the monster takes their damage normally.",
                Range = "0 or 1. Anything from 0.5 up counts as on.",
                Timing = ZoneStatTiming.Live,
                Caution = "Does nothing while Zone Control itself is off (zonecontrol_enabled). A per-monster flag on the monster itself also makes it immune, "
                    + "so clearing this stat will not remove immunity a monster already carries from its own flag.",
                Related = new[] { ZoneStat.ImmuneCurseCut, ZoneStat.ImmunePandemic, ZoneStat.ImmuneChain },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CritResistRating,
                Summary = "The monster's Crit Resist Rating: how often it shrugs off player crits.",
                Details = "A player's chance to crit the monster (melee, missile and spells) is multiplied by 100 / (100 + rating): a rating "
                    + "of 100 halves the chance. The number is written onto the monster when it spawns, replacing the rating its weenie ships with. "
                    + "Enchantments on the monster still add to it.",
                Shape = "A rating, as a whole number. It replaces the monster's own base rating. Rounded to a whole number.",
                WhenUnset = "The monster keeps the Crit Resist Rating its weenie ships with (0 if it has none).",
                Raise = "Player crits against the monster become rarer.",
                Lower = "Player crits become more common. A negative rating raises the chance above normal.",
                Range = "Any whole number. 0 means no change.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "Only changes how often a crit happens. How much a crit hurts the monster is crit_damage_resist_rating.",
                Related = new[] { ZoneStat.CritDamageResistRating, ZoneStat.CritRating },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CritDamageResistRating,
                Summary = "The monster's Crit Damage Resist Rating: how much of a player crit it takes off.",
                Details = "When a player crits the monster, this rating is worked into the damage the crit does: a 100 / (100 + rating) factor "
                    + "that applies to crit hits only, on top of the monster's normal Damage Resist Rating. The number is written onto the "
                    + "monster when it spawns, replacing the rating its weenie ships with. Enchantments on the monster still add to it.",
                Shape = "A rating, as a whole number. It replaces the monster's own base rating. Rounded to a whole number.",
                WhenUnset = "The monster keeps the Crit Damage Resist Rating its weenie ships with (0 if it has none).",
                Raise = "Player crits against the monster hit softer.",
                Lower = "Player crits hit harder. A negative rating makes crits stronger than normal.",
                Range = "Any whole number. 0 means no change.",
                Timing = ZoneStatTiming.Spawn,
                Caution = "For monsters at the endgame variations, when crit compression is on (zc_crit_compress), a player's crit is pulled toward "
                    + "a target size set in server settings, so the final crit does not move in a straight line with this number. "
                    + "Compression only acts when this rating is above 0.",
                Related = new[] { ZoneStat.CritResistRating, ZoneStat.DamageResistRating, ZoneStat.CritDamageRating },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "resist_slash..electric",
                Pattern = new Regex("^resist_(slash|pierce|bludgeon|fire|cold|acid|electric)$"),
                Summary = "How much damage of that one type the monster takes, as a multiplier.",
                Details = "Each of these covers one damage type: slash, pierce, bludgeon, fire, cold, acid or electric. When a hit or spell "
                    + "of that type damages the monster, its damage is multiplied by this number as part of the resist step. The stat replaces "
                    + "the monster's own resist for that type. Melee and missile hits, war spells, ring spells and Harm and Drain all use it.",
                Shape = "A multiplier. 1 is neutral, below 1 means the monster resists that type, above 1 means it is weak to it. "
                    + "The final resist step is never allowed to go below 0.",
                WhenUnset = "The monster uses the resist multiplier for that type that its weenie ships with (1 if it has none).",
                Raise = "The monster takes more damage of that type.",
                Lower = "The monster takes less damage of that type. At 0 it takes none of it.",
                Range = "Any number from 0. 1 means no change. Values above 1 make the monster vulnerable.",
                Timing = ZoneStatTiming.Live,
                Caution = "Several other things stack in the same step (player protections, vulns, rend, the monster's own weakness setting), "
                    + "so this is one factor among them. Damage-over-time ticks from players do not read this stat.",
                Related = new[] { ZoneStat.ResistNether, ZoneStat.ArmorVsSlash, ZoneStat.DamageResistRating, ZoneStat.MonsterDamageTakenMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ResistNether,
                Summary = "How much nether (void) damage the monster takes, as a multiplier.",
                Details = "When a nether hit or void spell damages the monster, its damage is multiplied by this number in the resist step. "
                    + "The stat replaces the monster's own nether resist. The monster's Nether Resist Rating, if it has one, is multiplied on top.",
                Shape = "A multiplier. 1 is neutral, below 1 means the monster resists nether, above 1 means it is weak to it. "
                    + "The final resist step is never allowed to go below 0.",
                WhenUnset = "The monster uses the nether resist its weenie ships with (1 if it has none).",
                Raise = "The monster takes more nether damage.",
                Lower = "The monster takes less nether damage. At 0 it takes none.",
                Range = "Any number from 0. 1 means no change. Values above 1 make the monster vulnerable.",
                Timing = ZoneStatTiming.Live,
                Caution = "Direct nether hits and void spells use it, but the ticks of a player's void damage-over-time spells do not read this stat. "
                    + "Several other things stack in the same step (player protections, vulns, rend, the monster's own weakness setting).",
                Related = new[] { ZoneStat.ResistSlash, ZoneStat.ArmorVsNether, ZoneStat.VoidDamageTakenMult, ZoneStat.DotArmor },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "armor_vs_slash..nether",
                Pattern = new Regex("^armor_vs_(slash|pierce|bludgeon|fire|cold|acid|electric|nether)$"),
                Summary = "How much of the monster's base armor counts against that one damage type, as a multiplier.",
                Details = "Each of these covers one damage type: slash, pierce, bludgeon, fire, cold, acid, electric or nether. When a melee or "
                    + "missile hit of that type lands, the monster's armor for it is its base armor level times this number. The stat replaces "
                    + "the monster's own multiplier for that type. The result then goes through the armor curve (see armor_level).",
                Shape = "A multiplier on base armor. 1 is neutral, 2 doubles the armor against that type, 0 removes the base armor against it.",
                WhenUnset = "The monster uses the armor multiplier for that type that its weenie ships with (1 if it has none).",
                Raise = "That damage type is cut harder by armor.",
                Lower = "That damage type is cut less by armor.",
                Range = "Any number. 1 means no change.",
                Timing = ZoneStatTiming.Live,
                Caution = "It only scales the monster's base armor. Armor enchantments and worn armor are added after it, unscaled. "
                    + "Spells skip armor, so it does not change spell damage.",
                Related = new[] { ZoneStat.ArmorLevel, ZoneStat.ResistSlash, ZoneStat.SpellArmor },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MonsterDamageTakenMult,
                Summary = "A multiplier on everything the monster takes, from any attacker.",
                Details = "Multiplies the whole damage of every hit the monster takes: melee, missile, spells, ring spells, Harm, Drain Health and "
                    + "damage-over-time ticks. It is applied last, after armor, resists and ratings. It is the defense twin of monster_damage_mult, "
                    + "and is the simple way to make fights last longer without changing the monster's health.",
                Shape = "A multiplier. 0.8 means the monster takes 20 percent less, so fights last 1.25 times as long. "
                    + "Only values from 0.1 to 10 are used; a value outside that range is ignored.",
                WhenUnset = "No change (x1).",
                Raise = "The monster takes more damage.",
                Lower = "The monster takes less damage and lasts longer.",
                Range = "0.1 to 10. A value outside that range is ignored and the stat has no effect.",
                Timing = ZoneStatTiming.Live,
                Caution = "Does nothing while Zone Control itself is off (zonecontrol_enabled). 0 does not make the monster unkillable: it is "
                    + "ignored, as is anything under 0.1. The melee, missile, war and void versions multiply on top of this one. "
                    + "A percent-of-health bonus some player gear adds is only ever scaled down by it, never up. "
                    + "A separate server-wide setting for damage taken by endgame monsters (zc_mob_dmg_taken_enabled) also stacks with it.",
                Related = new[] { ZoneStat.MeleeDamageTakenMult, ZoneStat.MissileDamageTakenMult, ZoneStat.WarDamageTakenMult, ZoneStat.VoidDamageTakenMult, ZoneStat.MonsterDamageMult, ZoneStat.DamageResistRating },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "melee/missile/war/void_damage_taken_mult",
                Pattern = new Regex("^(melee|missile|war|void)_damage_taken_mult$"),
                Summary = "A multiplier on the damage the monster takes from one kind of player attack.",
                Details = "There is one for each kind: melee weapon hits, missile weapon shots, war spells and void spells (void damage-over-time "
                    + "ticks count as void). It multiplies the damage on top of monster_damage_taken_mult, so the four kinds can be brought "
                    + "to the same kill time. A Cast on Strike proc counts as the weapon that fired it: a melee or missile weapon's proc is melee "
                    + "or missile, and a wand's proc is its spell's school. Only a player's attacks count, so pets and monsters hitting it only use "
                    + "the overall stat.",
                Shape = "A multiplier. 0.8 means the monster takes 20 percent less from that kind of attack. "
                    + "Only values from 0.1 to 10 are used; a value outside that range is ignored.",
                WhenUnset = "Only monster_damage_taken_mult applies to that kind of attack (x1 if that is also unset).",
                Raise = "The monster takes more damage from that kind of attack.",
                Lower = "The monster takes less damage from that kind of attack.",
                Range = "0.1 to 10. A value outside that range is ignored and the stat has no effect.",
                Timing = ZoneStatTiming.Live,
                Caution = "Does nothing while Zone Control itself is off (zonecontrol_enabled). A void spell's damage-over-time tick also takes "
                    + "monster_damage_taken_mult as well as this, in the same way a direct hit does.",
                Related = new[] { ZoneStat.MonsterDamageTakenMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CurseDamageCut,
                Summary = "How much damage the monster loses while a player's Destructive Curse is on it.",
                Details = "While a player's Destructive Curse (any level) is on the monster, everything it deals to players is multiplied by "
                    + "1 minus this number: swings with their True Damage, spells, Harm, Drain and its damage-over-time ticks. It is on or off, "
                    + "so two Curses do not stack and the Curse level does not matter.",
                Shape = "A fraction of the monster's damage that is cut. 0.25 cuts a quarter. Held between 0 and 0.9. A value that is not a real number is ignored.",
                WhenUnset = "The cut is 0.25.",
                Raise = "Players take less from a cursed monster.",
                Lower = "The Curse protects players less. At 0 there is no cut.",
                Range = "0 to 0.9. Anything outside is held to that range.",
                Timing = ZoneStatTiming.Live,
                Caution = "Does nothing while Zone Control itself (zonecontrol_enabled) or the server setting zc_curse_damage_cut is off, "
                    + "and only for monsters that fall under the endgame debuff rules (zc_vuln_enabled, zc_combat_rules_enabled and the minimum variation in zc_vuln_min_variation). "
                    + "A monster marked immune_curse_cut takes no cut. Unset is not the same as 0: unset still cuts.",
                Related = new[] { ZoneStat.ImmuneCurseCut, ZoneStat.MonsterDamageMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ImmuneCurseCut,
                Summary = "Makes Destructive Curse stop cutting this monster's damage.",
                Details = "When this is 1, a player's Destructive Curse does not reduce what the monster deals to players, whatever curse_damage_cut "
                    + "says. When a player casts the Curse on such a monster, they get a message that it is immune to the weakening.",
                Shape = "An on/off switch. 0.5 or more means immune, anything below means not immune. Use 1 to turn it on.",
                WhenUnset = "The Curse cuts the monster's damage as curse_damage_cut says.",
                Raise = "At 1 the Curse no longer weakens the monster.",
                Lower = "At 0 the Curse weakens the monster as normal.",
                Range = "0 or 1. Anything from 0.5 up counts as on.",
                Timing = ZoneStatTiming.Live,
                Caution = "Does nothing while Zone Control itself is off (zonecontrol_enabled). A per-monster flag on the monster itself also makes it immune, "
                    + "so clearing this stat will not remove immunity a monster already carries from its own flag.",
                Related = new[] { ZoneStat.CurseDamageCut, ZoneStat.ImmuneShrapnelAgony },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ImmunePandemic,
                Summary = "Makes the Pandemic charm skip this monster when it spreads damage-over-time spells.",
                Details = "When this is 1, the Pandemic charm never spreads a damage-over-time spell onto the monster: it is left out when "
                    + "the charm looks for monsters to spread to, and a copy that is about to land on it is dropped. A spell cast straight at "
                    + "the monster works as always.",
                Shape = "An on/off switch. 0.5 or more means immune, anything below means not immune. Use 1 to turn it on.",
                WhenUnset = "The monster can receive spread damage-over-time spells.",
                Raise = "At 1 the charm will not spread onto the monster.",
                Lower = "At 0 the charm can spread onto the monster.",
                Range = "0 or 1. Anything from 0.5 up counts as on.",
                Timing = ZoneStatTiming.Live,
                Caution = "A per-monster flag on the monster itself also makes it immune, and that part works even when Zone Control is off. "
                    + "This stat does nothing while Zone Control (zonecontrol_enabled) is off.",
                Related = new[] { ZoneStat.ImmuneChain, ZoneStat.ImmuneCurseCut },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ImmuneChain,
                Summary = "Makes the Chain charm skip this monster when it jumps between targets.",
                Details = "When this is 1, the Chain charm never jumps onto the monster: it is left out when the charm picks the next target, "
                    + "and if a jumping spell still strikes it on the way it takes no damage and starts no further jumps. A spell cast "
                    + "straight at the monster works as always.",
                Shape = "An on/off switch. 0.5 or more means immune, anything below means not immune. Use 1 to turn it on.",
                WhenUnset = "The monster can be jumped to by the Chain charm.",
                Raise = "At 1 the charm will not chain onto the monster.",
                Lower = "At 0 the charm can chain onto the monster.",
                Range = "0 or 1. Anything from 0.5 up counts as on.",
                Timing = ZoneStatTiming.Live,
                Caution = "A per-monster flag on the monster itself also makes it immune, and that part works even when Zone Control is off. "
                    + "This stat does nothing while Zone Control (zonecontrol_enabled) is off.",
                Related = new[] { ZoneStat.ImmunePandemic, ZoneStat.ImmuneCurseCut },
            });
        }
    }
}
