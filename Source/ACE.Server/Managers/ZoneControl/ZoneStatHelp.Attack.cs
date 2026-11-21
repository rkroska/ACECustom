using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Attack stats. Checked against Creature_Combat.GetEffectiveAttackSkill, WorldObject_Magic (spell skill), Creature_Rating.GetDamageRating,
    // Monster_Melee.GetBaseDamage, Monster_Combat.GetDamageType, WorldObject_Weapon (crit chance / crit damage), SpellProjectile (spell damage
    // and the player-hit steps), Creature_Archetype.ZcArchetypeSpellDamage, Creature_PercentHpDamage (percent HP floor, True Damage) and
    // ZoneControlManager.MonsterDamageMultForKind. All of them read the profile per hit or cast, so none is stamped at spawn.
    public static partial class ZoneStatHelp
    {
        private const string TrueDamageNote =
            "True Damage is added after armor, resists and the other defense steps, on health-damage hits only (not Stamina or Mana "
            + "hits), and only against players. Only the player's life augs reduce it.";

        static partial void AddAttack(List<ZoneStatHelpEntry> e)
        {
            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.AttackSkill,
                Summary = "The attack skill the monster uses for melee and missile attacks.",
                Details = "When a monster attacks, the game asks for its effective attack skill. If the zone sets this stat, that number "
                    + "is used as the skill, and the monster's own skill and weapon offense modifier are skipped. Spell casting uses "
                    + "magic_skill instead.",
                Shape = "An absolute value that replaces the monster's skill. Rounded to a whole number. A negative value is treated as 0.",
                WhenUnset = "The monster uses its own current skill for the weapon it is using, times its weapon's offense modifier.",
                Raise = "The monster lands more of its attacks against player defense.",
                Lower = "The monster misses more often.",
                Range = "Any number from 0. Anything below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = "A separate server setting can also raise attack skill to a minimum floor against players at high tiers, independent of this stat. "
                    + "Setting this stat does not change the monster's attributes or its other skills.",
                Related = new[] { ZoneStat.MagicSkill, ZoneStat.Strength, ZoneStat.Coordination },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MagicSkill,
                Summary = "The magic skill the monster casts its spells with.",
                Details = "When a monster casts a spell, the game uses this number as its skill in that spell's school instead of the "
                    + "monster's own skill. It is the spell equivalent of attack_skill.",
                Shape = "An absolute value that replaces the monster's skill. Rounded to a whole number. A negative value is treated as 0.",
                WhenUnset = "The monster casts with its own current skill in the spell's school.",
                Raise = "The monster's spells are resisted less often.",
                Lower = "Players resist more of the monster's spells.",
                Range = "Any number from 0. Anything below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = "The same number is used for every school the monster casts. Setting spell_damage without this stat leaves the monster at its own skill.",
                Related = new[] { ZoneStat.AttackSkill, ZoneStat.SpellDamage },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DamageRating,
                Summary = "The monster's Damage Rating.",
                Details = "Every melee, missile and spell hit by the monster is scaled by a rating factor of (100 + rating) / 100 "
                    + "(a rating of 20 gives x1.2), set against the defender's own damage resist rating. This stat replaces the "
                    + "monster's own rating before that is worked out.",
                Shape = "A rating, as a whole number. It replaces the monster's own base Damage Rating. Rounded to a whole number. "
                    + "A negative rating works as a penalty (below x1).",
                WhenUnset = "The monster uses the Damage Rating its weenie ships with.",
                Raise = "Every hit and spell from the monster is multiplied higher.",
                Lower = "Hits and spells are scaled down. Below 0 it becomes a penalty.",
                Range = "Any whole number. Zero means no change from x1.",
                Timing = ZoneStatTiming.Live,
                Caution = "Ratings and enchantments on the monster still add on top of this value.",
                Related = new[] { ZoneStat.MonsterDamageMult, ZoneStat.AttackDamage, ZoneStat.SpellDamage },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.PercentHpBase,
                Summary = "A minimum hit on players, as a fraction of the player's max health.",
                Details = "Each monster hit or harmful health spell on a player is checked against this floor: the fraction times the "
                    + "player's max health, reduced by the player's life augs and Damage Resist relief, and raised by a crit. "
                    + "Whichever is larger, the normal damage or the floor, is what lands. Stamina and Mana hits are skipped. "
                    + "A small random spread is applied to the floor on every hit.",
                Shape = "A fraction of the player's max health: 0.05 means a floor of 5 percent of max health before reductions. "
                    + "A value of 0 or below turns the floor off.",
                WhenUnset = "At high tiers the floor comes from the server's percent-HP settings if the rules for that tier are switched on, "
                    + "and the base there is 0 unless the server sets it. Outside that, no floor applies.",
                Raise = "A larger minimum chunk of health on every hit, whatever the player's armor.",
                Lower = "The floor shrinks and normal damage matters more. At 0 or below the floor is off.",
                Range = "Any number from 0. Set it as a fraction, not a percent.",
                Timing = ZoneStatTiming.Live,
                Caution = "Does nothing while the percent-HP server switch (zc_pcthp_enabled) or Zone Control itself is off. "
                    + "Setting this stat on a zone is also what lets a monster use the floor in a zone below the endgame tiers. "
                    + "A weenie-level override on the monster beats this stat. The floor is not reduced by armor, so small values bite hard on players with small health pools.",
                Related = new[] { ZoneStat.TrueDamage, ZoneStat.CritDamageRating },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.TrueDamage,
                Summary = "Fixed extra damage added to every landed monster hit on a player.",
                Details = "After a melee or missile hit lands and all defenses are done, this amount is added on top. "
                    + TrueDamageNote + " A crit multiplies it by the crit multiplier (crit_damage_rating if set, otherwise a server default), "
                    + "with the player's Crit Damage Resist shrinking only the bonus part. Then true_damage_variance spreads it.",
                Shape = "An absolute amount of health damage per hit. 0 or below adds nothing.",
                WhenUnset = "No True Damage is added.",
                Raise = "Every landed hit hurts more, and armor does not help against the extra amount.",
                Lower = "Less unavoidable damage per hit.",
                Range = "Any number from 0.",
                Timing = ZoneStatTiming.Live,
                Caution = "Spells use true_damage_spell if it is set, otherwise this same stat. The monster's damage multiplier (monster_damage_mult and the monster's own) multiplies it as well.",
                Related = new[] { ZoneStat.TrueDamageVariance, ZoneStat.TrueDamageSpell, ZoneStat.MonsterDamageMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.TrueDamageVariance,
                Summary = "How much True Damage varies from hit to hit.",
                Details = "Each hit's True Damage is multiplied by a random factor between 1 minus this value and 1 plus this value. "
                    + "Spells use true_damage_spell_variance first if it is set.",
                Shape = "A fraction. 0.2 means each hit lands between 80 and 120 percent of the amount. Held between 0 and 1.",
                WhenUnset = "The server's own True Damage variance setting is used.",
                Raise = "A wider spread between the weakest and strongest hits.",
                Lower = "More even hits. At 0 every hit is identical.",
                Range = "0 to 1. Anything outside is held to that range.",
                Timing = ZoneStatTiming.Live,
                Related = new[] { ZoneStat.TrueDamage, ZoneStat.TrueDamageSpellVariance },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.TrueDamageSpell,
                Summary = "True Damage for the monster's spells.",
                Details = "The fixed amount added to a monster's harmful spell on a player. " + TrueDamageNote
                    + " Crits and variance work as they do for true_damage.",
                Shape = "An absolute amount of health damage per cast. 0 or below adds nothing.",
                WhenUnset = "Spells use true_damage instead.",
                Raise = "Each spell lands harder, unreduced by the player's resists.",
                Lower = "Less unavoidable damage per spell. Note that 0 means spells deal no True Damage at all, even if true_damage is set.",
                Range = "Any number from 0.",
                Timing = ZoneStatTiming.Live,
                Caution = "Once this is set it fully replaces true_damage for spells, so a value of 0 switches spell True Damage off while melee keeps true_damage.",
                Related = new[] { ZoneStat.TrueDamage, ZoneStat.TrueDamageSpellVariance, ZoneStat.SpellDamage },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.TrueDamageSpellVariance,
                Summary = "How much spell True Damage varies from cast to cast.",
                Details = "Works like true_damage_variance, but only for the monster's spells.",
                Shape = "A fraction. 0.3 means each cast lands between 70 and 130 percent of the amount. Held between 0 and 1.",
                WhenUnset = "Spells use true_damage_variance, and if that is also unset, the server's own True Damage variance setting.",
                Raise = "A wider spread between casts.",
                Lower = "More even casts. At 0 every cast is identical.",
                Range = "0 to 1. Anything outside is held to that range.",
                Timing = ZoneStatTiming.Live,
                Related = new[] { ZoneStat.TrueDamageVariance, ZoneStat.TrueDamageSpell },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CritRating,
                Summary = "The monster's chance to land a critical hit, in percent.",
                Details = "The number replaces the monster's crit chance for melee, missile and spell attacks. It is read as a percent, "
                    + "so 25 means 25 percent. The player's crit resist still lowers the final chance.",
                Shape = "A chance in percent. It replaces the chance outright: the monster's own rating and weapon bonuses are not added to it.",
                WhenUnset = "The monster uses its normal crit chance from its weapon and rating.",
                Raise = "More crits, so more damage on average.",
                Lower = "Fewer crits. 0 means the monster never crits on its own roll.",
                Range = "Any number from 0. 100 means every hit is a crit before the player's crit resist.",
                Timing = ZoneStatTiming.Live,
                Caution = "How hard a crit hits is set separately by crit_damage_rating.",
                Related = new[] { ZoneStat.CritDamageRating },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CritDamageRating,
                Summary = "How hard the monster's crits hit: the final crit multiplier.",
                Details = "The number is the multiplier a crit deals: 4 means a crit hits for 4 times. It replaces the weapon's own crit "
                    + "multiplier for the monster's melee and missile attacks. It is also the crit multiplier for the percent HP floor "
                    + "and for True Damage, where the player's Crit Damage Resist shrinks only the bonus part above x1.",
                Shape = "A multiplier. 1 means a crit hits no harder than a normal hit. Values below 1 are held at 1 for melee and missile hits.",
                WhenUnset = "The weapon's own crit multiplier applies, and the floor and True Damage use the server's default crit multiplier.",
                Raise = "Crits hit harder, and so do the floor and True Damage on crits.",
                Lower = "Crits hit closer to a normal hit.",
                Range = "Any number from 1. Lower values are held at 1 for normal hits.",
                Timing = ZoneStatTiming.Live,
                Caution = "It replaces the multiplier outright, so a value of 2 makes the crit weaker than the weapon's own crit would have been if that was higher.",
                Related = new[] { ZoneStat.CritRating, ZoneStat.TrueDamage, ZoneStat.PercentHpBase },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.AttackDamage,
                Summary = "The monster's base damage per melee or missile swing.",
                Details = "Replaces the monster's own damage number for every melee and missile attack, whether it comes from a weapon, a "
                    + "missile or a body part. It beats a body part damage set in the zone, and that beats the weenie's own number. "
                    + "The monster's Strength or Coordination still multiplies the result afterwards, and armor, resists and ratings still apply.",
                Shape = "An absolute maximum damage per swing, rounded to a whole number. Held between 0 and the largest whole number the game can store.",
                WhenUnset = "The zone's body part damage is used if set, otherwise the monster's weapon or body part damage.",
                Raise = "Harder base hits before defenses.",
                Lower = "Weaker base hits. A negative value is held at 0 so it never heals the player.",
                Range = "0 up to the largest whole number the game can store. Anything outside is held to that range.",
                Timing = ZoneStatTiming.Live,
                Caution = "Strength or Coordination still multiplies this, so changing those stacks with this stat.",
                Related = new[] { ZoneStat.AttackVariance, ZoneStat.AttackDamageType, ZoneStat.Strength, ZoneStat.Coordination },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.AttackVariance,
                Summary = "How much a melee or missile swing varies below its maximum.",
                Details = "A swing rolls between the maximum damage times (1 minus this value) and the maximum. At 0 every swing is the "
                    + "maximum, at 0.5 swings land between half and the full maximum.",
                Shape = "A fraction, held between 0 and 1.",
                WhenUnset = "The zone's body part variance is used if set, otherwise the monster's own variance.",
                Raise = "A wider spread, with a lower average hit.",
                Lower = "More even swings. At 0 every swing is the maximum.",
                Range = "0 to 1. Anything outside is held to that range.",
                Timing = ZoneStatTiming.Live,
                Related = new[] { ZoneStat.AttackDamage },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.AttackDamageType,
                Summary = "The damage type of the monster's melee and missile attacks.",
                Details = "The number is a set of damage type flags. Each swing picks one of the flagged types at random. "
                    + "It beats a body part damage type set in the zone, and that beats the monster's own override, weapon or body part type.",
                Shape = "A whole number made of damage type flags added together. The server rounds it down to a whole number.",
                WhenUnset = "A zone body part type is used if set, then the monster's own override, then its weapon or body part type.",
                Raise = "Not a size: it picks which resist the player's defenses use.",
                Lower = "Not a size: it picks which resist the player's defenses use.",
                Range = "Any whole number of valid damage type flags. 0 is the same as unset.",
                Timing = ZoneStatTiming.Live,
                Caution = "A value of 0 is ignored, so you cannot use it to force physical damage. A value with no valid flag falls back to physical.",
                Related = new[] { ZoneStat.AttackDamage },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.SpellDamage,
                Summary = "The base damage of the monster's life projectile and Harm / Drain style spells.",
                Details = "Replaces the base damage of the monster's damaging spells, before the player's resists, absorbs and the rating "
                    + "chain act on it. The monster's own skill, the spell's rolled damage and life-magic aug bonuses are all replaced "
                    + "by this number. For magic archetype monsters it also sets the base damage of their Harm and Drain.",
                Shape = "An absolute base damage per cast, rounded to a whole number for bolts. A negative value is held at 0 so it never heals.",
                WhenUnset = "The spell deals its own rolled damage as usual.",
                Raise = "Harder spells before defenses.",
                Lower = "Weaker spells. At 0 the spell has no base damage, though True Damage can still land.",
                Range = "Any number from 0. Anything below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = "A monster keeps its own magic skill unless magic_skill is also set, so authored spell damage can still be resisted by most casts.",
                Related = new[] { ZoneStat.SpellVariance, ZoneStat.SpellDamageMult, ZoneStat.MagicSkill, ZoneStat.TrueDamageSpell },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.SpellVariance,
                Summary = "How much a spell cast varies below its base damage.",
                Details = "Each cast takes a random share off the base damage: damage times (1 minus this value times a random 0 to 1). "
                    + "At 0 every cast is the full base, at 0.5 casts land between 50 and 100 percent of it.",
                Shape = "A fraction, held between 0 and 1. It only ever lowers the damage from the base.",
                WhenUnset = "Casts are not spread by this stat.",
                Raise = "A wider spread, with a lower average cast.",
                Lower = "More even casts. At 0 every cast is the full base.",
                Range = "0 to 1. Anything outside is held to that range.",
                Timing = ZoneStatTiming.Live,
                Related = new[] { ZoneStat.SpellDamage },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.SpellDamageMult,
                Summary = "A multiplier on the final damage of the monster's spells.",
                Details = "Multiplies the monster's spell damage after the rating chain is worked out. Harm and Drain from a magic "
                    + "archetype monster use it as well.",
                Shape = "A multiplier. 1.5 is 50 percent more spell damage. It scales the spell's normal damage, not its True Damage.",
                WhenUnset = "No change (x1).",
                Raise = "Stronger spells.",
                Lower = "Weaker spells. 0 removes the spell's normal damage.",
                Range = "Any number from 0. Keep it at 0 or above: Harm and Drain ignore a negative value, and bolts apply it unchecked.",
                Timing = ZoneStatTiming.Live,
                Caution = "monster_damage_mult is the general damage multiplier and already covers spells. Using both stacks them.",
                Related = new[] { ZoneStat.SpellDamage, ZoneStat.MonsterDamageMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.MonsterDamageMult,
                Summary = "A multiplier on everything the monster deals to players.",
                Details = "Multiplies every hit the monster lands on a player: melee, missile, spells, the extra True Damage, and other "
                    + "damage the monster causes. It stacks with the monster's own damage multiplier and with any curse or archetype "
                    + "dial on the monster.",
                Shape = "A multiplier. 1.2 is 20 percent harder. Only values from 0.1 to 10 are used; a value outside that range is ignored.",
                WhenUnset = "No zone multiplier (x1). The monster's own damage multiplier still applies.",
                Raise = "Everything the monster deals to players hits harder.",
                Lower = "Everything the monster deals to players is softened.",
                Range = "0.1 to 10. A value outside that range is ignored and the stat has no effect.",
                Timing = ZoneStatTiming.Live,
                Caution = "0 does not make the monster harmless: it is ignored, as is anything under 0.1.",
                Related = new[] { ZoneStat.AttackDamage, ZoneStat.SpellDamageMult, ZoneStat.TrueDamage, ZoneStat.DamageRating },
            });
        }
    }
}
