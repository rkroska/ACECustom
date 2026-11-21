using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Monster archetype stats. Checked against WorldObjects/Creature_Archetype.cs (the roll, the kit, the attack choice, the damage dial),
    // ZoneSpawnScaler.ApplyToSpawn (the roll is the last step of a spawn) and ZoneControlManager.MonsterDamageMultForKind (where the damage
    // dial is applied). The roll, the kit, the pacing, the melee range and the name are all done ONCE when the monster spawns. Only the cast
    // chance and the two damage dials are read later, on each attack.
    public static partial class ZoneStatHelp
    {
        private const string ArchetypeGateNote =
            "Does nothing while Zone Control (zonecontrol_enabled) is off. Monsters that are players, pets, NPCs, vendors or not attackable never roll an archetype. "
            + "A monster that already carries its own launcher or ammo is left alone and stays as it was.";

        private const string ArchetypeRollNote =
            "Each monster rolls ONE combination of melee, missile and magic when it spawns, from the combinations its body allows. "
            + "A monster whose body allows neither missile nor magic never rolls and stays a plain melee monster. ";

        private static ZoneStatHelpEntry ArchetypeWeightEntry(string key, string what, string needs, int fixedDefault, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "How likely a monster is to roll " + what + ", compared to the other combinations.",
                Details = ArchetypeRollNote + "This is the weight of the combination " + what + ". "
                    + "Its chance is this weight divided by the total of the weights of every combination that monster is allowed to roll. "
                    + needs,
                Shape = "A relative weight, not a percent. Below 0 is read as 0. A value that is not a real number is read as the default. "
                    + "If every allowed weight is 0 the monster becomes plain melee.",
                WhenUnset = "A fixed default weight of " + fixedDefault + " is used.",
                Raise = "More monsters roll this combination, and fewer roll the others.",
                Lower = "Fewer monsters roll this combination. At 0 none do.",
                Range = "Any number from 0. Only the size compared to the other weights matters.",
                Timing = ZoneStatTiming.Spawn,
                Caution = ArchetypeGateNote
                    + " If the launcher or ammo for a missile roll cannot be made, the missile part is dropped and the monster keeps whatever else it rolled (plain melee if nothing else).",
                Related = related,
            };
        }

        static partial void AddArchetypes(List<ZoneStatHelpEntry> e)
        {
            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArchetypeCanMissile,
                Summary = "Switch: this monster body may roll a missile (bow or thrown) archetype.",
                Details = ArchetypeRollNote + "This stat is the body flag for missile. It only counts as on when archer_launcher_wcid is also set to something above 0. "
                    + "A monster that rolls missile is handed the launcher (and ammo) at spawn and fights with it from range.",
                Shape = "A switch. 0.5 or more is on, below 0.5 is off. A value that is not a real number is read as off.",
                WhenUnset = "Off. The monster never rolls a missile combination, so a retail monster never grows a bow.",
                Raise = "At 0.5 or more the monster may roll missile, if a launcher is set.",
                Lower = "Below 0.5 the monster never rolls missile.",
                Range = "Use 0 or 1.",
                Timing = ZoneStatTiming.Spawn,
                Caution = ArchetypeGateNote + " It is normally set on a single monster type, not a whole zone, because it needs a launcher that fits that body.",
                Related = new[] { ZoneStat.ArcherLauncherWcid, ZoneStat.ArcherAmmoWcid, ZoneStat.ArchetypeCanMagic, ZoneStat.ArchetypeWeightMissile },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArchetypeCanMagic,
                Summary = "Switch: this monster body may roll a magic archetype.",
                Details = ArchetypeRollNote + "This stat is the body flag for magic. It only counts as on when the monster has something to cast: "
                    + "a spell on the zone's caster list, or a damage spell (a bolt or other projectile, Harm Health or Drain Health) in its own spell book or added by the zone, with a chance above 0. "
                    + "A book of only heals, buffs and debuffs does not count. A monster that rolls magic casts free of mana, and picks its spell from the zone's caster list if there is one, otherwise from its own damage spells.",
                Shape = "A switch. 0.5 or more is on, below 0.5 is off. A value that is not a real number is read as off.",
                WhenUnset = "Off. The monster never rolls a magic combination.",
                Raise = "At 0.5 or more the monster may roll magic, if it has a damage spell.",
                Lower = "Below 0.5 the monster never rolls magic.",
                Range = "Use 0 or 1.",
                Timing = ZoneStatTiming.Spawn,
                Caution = ArchetypeGateNote + " On with no castable spell does nothing: the monster stays without magic.",
                Related = new[] { ZoneStat.ArchetypeCanMissile, ZoneStat.CasterMagicDelay, ZoneStat.ArchetypeCastChance, ZoneStat.ArchetypeWeightMagic },
            });

            e.Add(ArchetypeWeightEntry(ZoneStat.ArchetypeWeightMelee, "plain melee",
                "Plain melee is always allowed. A plain melee monster is exactly the monster it was and gets no role word after its name.",
                15, new[] { ZoneStat.ArchetypeWeightMissile, ZoneStat.ArchetypeWeightMagic, ZoneStat.ArchetypeWeightAll }));

            e.Add(ArchetypeWeightEntry(ZoneStat.ArchetypeWeightMissile, "missile only",
                "It counts only for monsters whose body allows missile (archetype_can_missile and a launcher). A missile only monster never casts and never swaps to melee.",
                15, new[] { ZoneStat.ArchetypeCanMissile, ZoneStat.ArchetypeWeightMelee, ZoneStat.ArchetypeWeightMissileMelee, ZoneStat.ArchetypeWeightMissileMagic }));

            e.Add(ArchetypeWeightEntry(ZoneStat.ArchetypeWeightMagic, "magic only",
                "It counts only for monsters whose body allows magic (archetype_can_magic and a castable spell). A magic only monster casts on every attack.",
                15, new[] { ZoneStat.ArchetypeCanMagic, ZoneStat.ArchetypeWeightMelee, ZoneStat.ArchetypeWeightMagicMelee, ZoneStat.ArchetypeWeightMissileMagic }));

            e.Add(ArchetypeWeightEntry(ZoneStat.ArchetypeWeightMissileMelee, "missile plus melee",
                "It counts only for monsters whose body allows missile. These monsters shoot from range and swap to a melee weapon when the target gets close (see archetype_melee_range).",
                15, new[] { ZoneStat.ArchetypeCanMissile, ZoneStat.ArchetypeMeleeRange, ZoneStat.ArchetypeWeightMissile, ZoneStat.ArchetypeWeightMelee }));

            e.Add(ArchetypeWeightEntry(ZoneStat.ArchetypeWeightMagicMelee, "magic plus melee",
                "It counts only for monsters whose body allows magic. These monsters cast, and may also swing up close (see archetype_cast_chance and archetype_melee_range).",
                15, new[] { ZoneStat.ArchetypeCanMagic, ZoneStat.ArchetypeCastChance, ZoneStat.ArchetypeMeleeRange, ZoneStat.ArchetypeWeightMagic }));

            e.Add(ArchetypeWeightEntry(ZoneStat.ArchetypeWeightMissileMagic, "missile plus magic",
                "It counts only for monsters whose body allows both missile and magic. These monsters shoot and cast, but have no melee mode (see archetype_cast_chance).",
                15, new[] { ZoneStat.ArchetypeCanMissile, ZoneStat.ArchetypeCanMagic, ZoneStat.ArchetypeCastChance, ZoneStat.ArchetypeWeightMissile, ZoneStat.ArchetypeWeightMagic }));

            e.Add(ArchetypeWeightEntry(ZoneStat.ArchetypeWeightAll, "all three (missile, magic and melee)",
                "It counts only for monsters whose body allows both missile and magic. These monsters shoot, cast and swap to melee up close.",
                10, new[] { ZoneStat.ArchetypeCanMissile, ZoneStat.ArchetypeCanMagic, ZoneStat.ArchetypeMeleeRange, ZoneStat.ArchetypeCastChance }));

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArcherLauncherWcid,
                Summary = "The weapon (bow, crossbow or thrown weapon) a missile monster is handed at spawn.",
                Details = "The item id of the launcher or thrown weapon given to a monster that rolls a missile combination. "
                    + "It is made fresh for each monster and wielded. If the monster carries a melee weapon in its main hand, that weapon goes into its pack and comes back when it swaps to melee. "
                    + "The weapon and its ammo are never dropped on the corpse. The monster is only given the kit if the item is a real missile weapon and it can shoot at least 5 metres. "
                    + "A thrown weapon needs no ammo, so set archer_ammo_wcid to 0 for it.",
                Shape = "An item id (whole number). It is rounded to the nearest whole number. 0 or below means no launcher.",
                WhenUnset = "No launcher, so the monster can never roll a missile combination (archetype_can_missile does nothing without it).",
                Raise = "Not a size. Use the item id of the weapon you want the monsters to carry.",
                Lower = "Not a size. Set it to 0 to take the launcher away: new spawns then cannot roll missile.",
                Range = "A valid missile weapon item id, or 0 for none.",
                Timing = ZoneStatTiming.Spawn,
                Caution = ArchetypeGateNote + " If the id is not a missile weapon, or it shoots under 5 metres, or the ammo does not fit it, the server logs a warning and "
                    + "the monster goes without the missile part (a missile only roll becomes plain melee).",
                Related = new[] { ZoneStat.ArcherAmmoWcid, ZoneStat.ArchetypeCanMissile, ZoneStat.ArchetypeWeightMissile },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArcherAmmoWcid,
                Summary = "The ammo (arrows, bolts, shot) a missile monster is handed with its launcher.",
                Details = "The item id of the ammunition wielded together with archer_launcher_wcid. It must be ammunition of the type that launcher fires. "
                    + "It is made fresh for each monster and is never dropped on the corpse. A launcher that is not an ammo launcher, such as a thrown weapon, ignores it.",
                Shape = "An item id (whole number). It is rounded to the nearest whole number. 0 or below means no ammo.",
                WhenUnset = "No ammo. A bow or crossbow then fails to make its kit (the monster goes without the missile part); a thrown weapon works as it needs none.",
                Raise = "Not a size. Use the item id of the ammunition that fits the launcher.",
                Lower = "Not a size. Set to 0 for thrown weapons.",
                Range = "A valid ammunition item id of the right type for the launcher, or 0 for a thrown weapon.",
                Timing = ZoneStatTiming.Spawn,
                Caution = ArchetypeGateNote + " If the ammo is missing, is not ammunition, or does not fit the launcher, the server logs a warning and the monster goes without the bow.",
                Related = new[] { ZoneStat.ArcherLauncherWcid, ZoneStat.ArchetypeCanMissile },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArchetypeMissileDamageMult,
                Summary = "A multiplier on the damage a missile monster does to players with its missile hits.",
                Details = "Applies to the whole landed hit (normal damage plus True Damage) of a missile attack against a player, for monsters that rolled missile. "
                    + "It stacks with the monster's own damage multiplier and monster_damage_mult. Melee swings of the same monster are not changed, "
                    + "so a missile plus melee monster keeps its normal melee damage. Monsters that did not roll missile are not changed.",
                Shape = "A multiplier. 2 doubles missile hits, 0.5 halves them. Held between 0 and 10. A value that is not a real number is read as 1.",
                WhenUnset = "No change (x1).",
                Raise = "Missile hits against players hurt more.",
                Lower = "Missile hits against players hurt less. At 0 they deal no damage.",
                Range = "0 to 10. Anything above 10 is held at 10, and below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = ArchetypeGateNote + " A value of 0 means that monster's arrows do no damage at all. It only changes damage dealt TO players.",
                Related = new[] { ZoneStat.ArchetypeMagicDamageMult, ZoneStat.MonsterDamageMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArchetypeMagicDamageMult,
                Summary = "A multiplier on the damage a caster monster does to players with its spells.",
                Details = "Applies to the monster's own spell hits against a player, for monsters that rolled magic: its projectile spells, its Harm Health and Drain Health casts, "
                    + "and the DoT ticks it puts on the player. It stacks with the monster's own damage multiplier and monster_damage_mult. "
                    + "Spells cast by a weapon's proc are not changed. Melee swings of the same monster are not changed. Monsters that did not roll magic are not changed.",
                Shape = "A multiplier. 2 doubles spell hits, 0.5 halves them. Held between 0 and 10. A value that is not a real number is read as 1.",
                WhenUnset = "No change (x1).",
                Raise = "Spell hits against players hurt more.",
                Lower = "Spell hits against players hurt less. At 0 they deal no damage.",
                Range = "0 to 10. Anything above 10 is held at 10, and below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = ArchetypeGateNote + " A value of 0 means that monster's spells do no damage at all. It only changes damage dealt TO players.",
                Related = new[] { ZoneStat.ArchetypeMissileDamageMult, ZoneStat.MonsterDamageMult, ZoneStat.SpellDamage, ZoneStat.SpellDamageMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CasterMagicDelay,
                Summary = "Seconds a caster monster waits between casts.",
                Details = "For a monster that rolled magic, this is stamped on it at spawn as its gap between spell casts. "
                    + "A magic monster always casts without using mana. Only a value above 0 does anything.",
                Shape = "A number of seconds. Held between 0.5 and 60. Zero, below 0, or not a real number leaves the monster's own gap alone.",
                WhenUnset = "The monster keeps its own cast gap, or 3 seconds if it has none.",
                Raise = "Casters cast less often.",
                Lower = "Casters cast more often. The shortest gap is 0.5 seconds.",
                Range = "0.5 to 60 seconds. A value from 0 up to 0.5 is raised to 0.5, and anything above 60 is held at 60.",
                Timing = ZoneStatTiming.Spawn,
                Caution = ArchetypeGateNote + " It only affects monsters that rolled magic. Plain melee and missile only monsters do not use it.",
                Related = new[] { ZoneStat.ArchetypeCanMagic, ZoneStat.ArchetypeCastChance, ZoneStat.ArchetypeMagicDamageMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArchetypeCastChance,
                Summary = "How often a hybrid caster casts instead of using its other attack.",
                Details = "For a monster that rolled magic plus another mode, this is the chance that each attack is a spell. If the roll fails, it shoots or swings instead. "
                    + "It does not apply when the monster should always cast: a magic only monster casts every attack, and a magic plus melee monster casts every attack "
                    + "while its target is farther than twice archetype_melee_range. It does apply to magic plus missile, and to all three together. "
                    + "A hybrid with a bow out whose target is beyond spell reach shoots instead of casting.",
                Shape = "A chance from 0 to 1. 0.5 means half of the attacks. Values outside 0 to 1 are held to it. A value that is not a real number is read as the default.",
                WhenUnset = "A fixed default of 0.5.",
                Raise = "Hybrids cast more and use their bow or melee less.",
                Lower = "Hybrids cast less. At 0 they cast only when the always-cast case above applies.",
                Range = "0 to 1.",
                Timing = ZoneStatTiming.Live,
                Caution = ArchetypeGateNote + " It does nothing to magic only monsters.",
                Related = new[] { ZoneStat.ArchetypeCanMagic, ZoneStat.ArchetypeMeleeRange, ZoneStat.CasterMagicDelay, ZoneStat.ArchetypeWeightMagicMelee },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArchetypeMeleeRange,
                Summary = "The distance where a hybrid monster switches between melee and its ranged mode.",
                Details = "Stamped on the monster at spawn. A missile plus melee monster draws its melee weapon when the target is closer than this, "
                    + "and goes back to the bow once the target is farther than twice this. A magic plus melee monster may swing when the target is within twice this distance, "
                    + "and beyond twice this it casts on every attack. Swaps are also held apart by a short fixed lockout. It does nothing for monsters with no melee mode.",
                Shape = "A distance. Held between 0 and 50. A value that is not a real number is read as the default.",
                WhenUnset = "A fixed default of 4.",
                Raise = "Hybrids switch to melee sooner, and keep casting or shooting only at longer range.",
                Lower = "Hybrids let the target get closer before swapping to melee.",
                Range = "0 to 50.",
                Timing = ZoneStatTiming.Spawn,
                Caution = ArchetypeGateNote + " At 0 a missile plus melee monster never swaps to melee for closeness.",
                Related = new[] { ZoneStat.ArchetypeCastChance, ZoneStat.ArchetypeWeightMissileMelee, ZoneStat.ArchetypeWeightMagicMelee, ZoneStat.ArchetypeWeightAll },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArchetypeKeepName,
                Summary = "Switch: leave the monster's name alone after it rolls an archetype.",
                Details = "A monster that rolls anything other than plain melee gets a role word added after its name at spawn: Bowyer (bow), Grenadier (thrown), Sorcerer (magic), "
                    + "Skirmisher (missile plus melee), Battlemage (magic plus melee), Spellslinger (missile plus magic) or Warmaster (all three). This stat turns that off.",
                Shape = "A switch. 1 (0.5 or more) keeps the plain name. Below 0.5 adds the role word. A value that is not a real number is read as off.",
                WhenUnset = "Off. The role word is added.",
                Raise = "At 0.5 or more monsters keep their plain name.",
                Lower = "Below 0.5 the role word is added to the name.",
                Range = "Use 0 or 1.",
                Timing = ZoneStatTiming.Spawn,
                Caution = ArchetypeGateNote + " Plain melee monsters never get a role word whatever this is set to. Players cannot tell the archetype from the name when this is on.",
                Related = new[] { ZoneStat.ArchetypeCanMissile, ZoneStat.ArchetypeCanMagic },
            });
        }
    }
}
