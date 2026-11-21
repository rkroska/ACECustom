using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Debuff and DoT stats. Checked against Creature_DebuffCompression (the bonus model, DoT multipliers, DoT armor), Creature_Properties
    // GetResistanceMod (vuln_cap and where the bonus lands), DamageEvent (Imperil), EnchantmentManager (which augs are stamped on a debuff
    // at cast, and the DoT tick) and Creature_DotHit (the spell-hit DoT tick). These tune what a PLAYER's debuffs and DoTs do TO the
    // monster, not what the monster does. All are read per hit or per tick, so none is stamped at spawn.
    public static partial class ZoneStatHelp
    {
        private const string DebuffGateNote =
            "Does nothing while Zone Control (zonecontrol_enabled), zc_vuln_enabled or zc_combat_rules_enabled is off. "
            + "It also only applies to monsters in a variation at or above zc_vuln_min_variation (never below the endgame start), and never to players or combat pets.";

        private const string DebuffBonusModel =
            "The bonus is: floor + (cap - floor) x a ramp from 0 to 1, where the ramp is (the caster's augs minus debuff_entry_life_augs) divided by "
            + "debuff_ramp_augs, held between 0 and 1. A caster at or below the entry point gets the floor. At entry plus ramp or above they get the cap. "
            + "A lower spell level gets only a share of that bonus; the top level of the spell gets all of it. ";

        static partial void AddDebuffs(List<ZoneStatHelpEntry> e)
        {
            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.VulnCap,
                Summary = "The cap on the old-style Vulnerability multiplier, for monsters not using the new debuff bonus.",
                Details = "On monsters that do NOT use the newer capped debuff bonus, a player's Vulnerability spell is first shrunk by the server's "
                    + "vuln effectiveness setting (only that fraction of the vuln's extra damage lands), then held down to this cap. "
                    + "This stat replaces the per-monster cap and the server's default vuln cap. The weapon's own Rend still counts: "
                    + "the game uses the stronger of the capped vuln and Rend.",
                Shape = "A damage multiplier on the resist step. 1.5 means the vuln can add at most 50 percent. "
                    + "Only used when the vuln is above x1. A cap of 1 or below leaves the vuln worth nothing.",
                WhenUnset = "The monster's own cap override is used if it has one, otherwise the server's vuln cap setting.",
                Raise = "Vulnerability can raise damage taken higher before it stops.",
                Lower = "Vulnerability is held to a smaller boost. At 1 or below it adds nothing.",
                Range = "Any number. Use 1 or more. Values at or below 1 turn the vuln off for these monsters.",
                Timing = ZoneStatTiming.Live,
                Caution = "Monsters that use the new debuff bonus ignore this stat completely: for them use vuln_bonus_floor and vuln_bonus_cap. "
                    + "Does nothing while zc_vuln_enabled is off, and never applies to players.",
                Related = new[] { ZoneStat.VulnBonusFloor, ZoneStat.VulnBonusCap },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.VulnBonusFloor,
                Summary = "The weakest damage bonus a player's Vulnerability gives, as a fraction.",
                Details = "On a monster using the debuff bonus, a player's life Vulnerability no longer works the old way. It gives one bonus "
                    + "that multiplies all damage of that damage type the monster takes (melee, missile, spells and DoT ticks). "
                    + DebuffBonusModel + "The vuln uses the caster's LIFE augs. Where Rend and a vuln are both present, they stack. "
                    + "If several vulns are on the monster, only the strongest counts.",
                Shape = "A fraction added to damage. 0.25 means x1.25. The final vuln bonus is held between 0 and 10.",
                WhenUnset = "A fixed default floor of 0.25 is used.",
                Raise = "Casters with few life augs do more damage with a vuln.",
                Lower = "A caster just starting out gets less from a vuln.",
                Range = "Any number. The final bonus is held between 0 and 10, so a floor below 0 never makes a vuln hurt the caster.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " The nether DoT bonus (dot_bonus_floor and dot_bonus_cap) is added to the vuln bonus on every hit, so the two stack.",
                Related = new[] { ZoneStat.VulnBonusCap, ZoneStat.DebuffEntryLifeAugs, ZoneStat.DebuffRampAugs, ZoneStat.VulnCap, ZoneStat.DotBonusFloor },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.VulnBonusCap,
                Summary = "The strongest damage bonus a player's Vulnerability gives, as a fraction.",
                Details = "The top end of the vuln bonus, reached when the caster's life augs are at the entry point plus the ramp or higher. "
                    + DebuffBonusModel + "A lower level vuln gets only its share of this cap.",
                Shape = "A fraction added to damage. 0.5 means x1.5. The final vuln bonus is held between 0 and 10.",
                WhenUnset = "A fixed default cap of 0.50 is used.",
                Raise = "Fully augmented casters do more with a vuln.",
                Lower = "The top of the vuln bonus shrinks. If it is set below the floor, the bonus falls as the caster's augs rise.",
                Range = "Any number. The final bonus is held between 0 and 10.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " Keep it at or above vuln_bonus_floor, or more augs will lower the bonus.",
                Related = new[] { ZoneStat.VulnBonusFloor, ZoneStat.DebuffEntryLifeAugs, ZoneStat.DebuffRampAugs, ZoneStat.VulnCap },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ImperilBonusFloor,
                Summary = "The weakest damage bonus a player's Imperil gives, as a fraction.",
                Details = "On a monster using the debuff bonus, Imperil no longer lowers the monster's armor. It gives a damage bonus on melee and "
                    + "missile hits only, added to the vuln bonus (so x(1 + vuln + imperil)). Spells and DoT ticks never get an Imperil bonus. "
                    + DebuffBonusModel + "Imperil uses the caster's LIFE augs. If several Imperils are on the monster, only the strongest counts.",
                Shape = "A fraction added to damage. 0.25 means 25 percent more on melee and missile hits. Held between 0 and 10.",
                WhenUnset = "A fixed default floor of 0.25 is used.",
                Raise = "Casters with few life augs do more melee and missile damage with Imperil.",
                Lower = "A caster just starting out gets less from Imperil.",
                Range = "Any number. The final bonus is held between 0 and 10.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " Hits that ignore magic resist skip the Imperil bonus.",
                Related = new[] { ZoneStat.ImperilBonusCap, ZoneStat.DebuffEntryLifeAugs, ZoneStat.DebuffRampAugs, ZoneStat.VulnBonusFloor },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ImperilBonusCap,
                Summary = "The strongest damage bonus a player's Imperil gives, as a fraction.",
                Details = "The top end of the Imperil bonus, reached when the caster's life augs are at the entry point plus the ramp or higher. "
                    + "It applies to melee and missile hits only. " + DebuffBonusModel + "A lower level Imperil gets only its share of this cap.",
                Shape = "A fraction added to damage. 0.5 means 50 percent more on melee and missile hits. Held between 0 and 10.",
                WhenUnset = "A fixed default cap of 0.50 is used.",
                Raise = "Fully augmented casters add more to melee and missile damage.",
                Lower = "The top of the Imperil bonus shrinks. If it is set below the floor, the bonus falls as the caster's augs rise.",
                Range = "Any number. The final bonus is held between 0 and 10.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " Keep it at or above imperil_bonus_floor.",
                Related = new[] { ZoneStat.ImperilBonusFloor, ZoneStat.DebuffEntryLifeAugs, ZoneStat.DebuffRampAugs },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DebuffEntryLifeAugs,
                Summary = "The aug count where the debuff bonuses start to rise above their floor.",
                Details = "The entry point of the ramp used by the vuln, Imperil and nether DoT bonuses. A caster with this many augs or fewer gets the "
                    + "floor. Vuln and Imperil count the caster's life augs. The nether DoT bonus counts the caster's void augs, using this same number. "
                    + "The count is the one stamped on the spell when it was cast (or on the wielder for an item proc).",
                Shape = "An aug count. It is the same entry point for all three bonuses.",
                WhenUnset = "The entry is 2000 plus 500 for each tier above 11 (never less than 2000).",
                Raise = "Casters need more augs before the bonuses rise above the floor.",
                Lower = "The bonuses reach the cap with fewer augs.",
                Range = "Any number. Values below 0 simply mean everyone is already on the ramp.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " A spell cast before an edit keeps the aug count it was cast with, but the entry point is read on every hit.",
                Related = new[] { ZoneStat.DebuffRampAugs, ZoneStat.VulnBonusFloor, ZoneStat.ImperilBonusFloor, ZoneStat.DotBonusFloor },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DebuffRampAugs,
                Summary = "How many augs it takes to go from the floor to the cap.",
                Details = "The width of the ramp used by the vuln, Imperil and nether DoT bonuses. Going from the entry point up by this many augs "
                    + "takes the bonus from its floor to its cap, in a straight line.",
                Shape = "An aug count. Values below 1 are read as 1, so the bonus jumps straight from floor to cap.",
                WhenUnset = "A fixed default ramp of 500 augs is used.",
                Raise = "A slower climb: casters need more augs above the entry point to reach the cap.",
                Lower = "A faster climb. At 1 it is nearly a step from floor to cap.",
                Range = "1 or more. Anything below 1 is read as 1.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote,
                Related = new[] { ZoneStat.DebuffEntryLifeAugs, ZoneStat.VulnBonusCap, ZoneStat.ImperilBonusCap, ZoneStat.DotBonusCap },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DotBonusFloor,
                Summary = "The weakest damage bonus a player's void DoT gives, as a fraction.",
                Details = "On a monster using the debuff bonus, a player's void DoT (Corrosion, Corruption or Destructive Curse) gives a damage bonus "
                    + "added to the vuln bonus, so all damage of the vuln's type the monster takes is multiplied by x(1 + vuln + dot). "
                    + DebuffBonusModel + "The DoT bonus uses the caster's VOID augs. Any damage type gets it while a void DoT is on the monster. "
                    + "Only the strongest void DoT counts, not the sum.",
                Shape = "A fraction added to damage. 0.25 means x1.25. Held between 0 and 10. A lower spell level gets its level out of 8 as a share.",
                WhenUnset = "A fixed default floor of 0.25 is used.",
                Raise = "Casters with few void augs add more damage while their DoT is on.",
                Lower = "A caster just starting out gets less from a DoT.",
                Range = "Any number. The final bonus is held between 0 and 10.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " This bonus adds to what the vuln gives, so raising it also raises the effect of vulns.",
                Related = new[] { ZoneStat.DotBonusCap, ZoneStat.DebuffEntryLifeAugs, ZoneStat.DebuffRampAugs, ZoneStat.VulnBonusFloor },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DotBonusCap,
                Summary = "The strongest damage bonus a player's void DoT gives, as a fraction.",
                Details = "The top end of the DoT bonus, reached when the caster's void augs are at the entry point plus the ramp or higher. "
                    + DebuffBonusModel + "A lower level DoT gets its level out of 8 as a share of this cap.",
                Shape = "A fraction added to damage. 0.5 means x1.5. Held between 0 and 10.",
                WhenUnset = "A fixed default cap of 0.50 is used.",
                Raise = "Fully augmented void casters add more damage while their DoT is on.",
                Lower = "The top of the DoT bonus shrinks. If it is set below the floor, the bonus falls as the caster's augs rise.",
                Range = "Any number. The final bonus is held between 0 and 10.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " Keep it at or above dot_bonus_floor.",
                Related = new[] { ZoneStat.DotBonusFloor, ZoneStat.DebuffEntryLifeAugs, ZoneStat.DebuffRampAugs },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DotArmor,
                Summary = "An armor-style cut on a player's DoT ticks against the monster.",
                Details = "Each tick of a player's DoT is multiplied by 66.67 divided by (66.67 plus this number). For that tick it replaces "
                    + "Spell Armor, the monster's Damage Resist Rating, its DoT Resist Rating and its nether resist rating. "
                    + "Only a value above 0 does anything.",
                Shape = "An armor level, like armor_level. It can only cut damage, never raise it: a value of 0 or below does nothing. A value that is not a real number is ignored.",
                WhenUnset = "No DoT armor cut. Spell Armor and the three rating reductions are still skipped for these ticks, so the tick is not reduced by them at all.",
                Raise = "Player DoT ticks are cut harder.",
                Lower = "Player DoT ticks hit harder. At 0 or unset there is no cut.",
                Range = "Any number above 0. Zero or below is ignored.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " Void DoTs that carry a cast-time snapshot (server setting zc_dot_spell_math, on by default) tick like a spell hit "
                    + "instead: they use Spell Armor and the rating rows, and this stat is skipped for them. It still applies to DoTs without a snapshot, and to all void DoTs while that setting is off.",
                Related = new[] { ZoneStat.DotDamageMult, ZoneStat.ArmorLevel, ZoneStat.DamageResistRating },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DotDamageMult,
                Summary = "A multiplier on every tick of a player's DoT against the monster.",
                Details = "Each DoT tick from a player is multiplied by this number. Unlike dot_armor it can raise damage as well as cut it. "
                    + "It applies on top of dot_mult_corruption and dot_mult_destructive for those two DoTs.",
                Shape = "A multiplier. 2 doubles each tick, 0.5 halves it. Anything below 0 is read as 0. A value that is not a real number is read as 1.",
                WhenUnset = "No change (x1).",
                Raise = "Each DoT tick hits harder.",
                Lower = "Each DoT tick hits softer. At 0 the ticks deal nothing.",
                Range = "Any number from 0. Below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " A value of 0 turns player DoT damage off on that monster. It is applied to ticks only, not to the damage bonus from dot_bonus_floor and dot_bonus_cap.",
                Related = new[] { ZoneStat.DotMultCorruption, ZoneStat.DotMultDestructive, ZoneStat.DotArmor, ZoneStat.DotBonusFloor },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DotMultCorruption,
                Summary = "An extra multiplier on Corruption DoT ticks.",
                Details = "Applies only to the Corruption DoT (every level, including Incantation). Each tick is multiplied by this number on top "
                    + "of dot_damage_mult. Corrosion and Destructive Curse ticks are not touched by it.",
                Shape = "A multiplier. 0.1 means Corruption ticks at one tenth of what dot_damage_mult alone gives. Below 0 is read as 0. A value that is not a real number is read as 1.",
                WhenUnset = "No extra change (x1).",
                Raise = "Corruption ticks hit harder than the other DoTs.",
                Lower = "Corruption ticks hit softer. At 0 Corruption deals nothing.",
                Range = "Any number from 0. Below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " A value of 0 turns Corruption off on that monster.",
                Related = new[] { ZoneStat.DotDamageMult, ZoneStat.DotMultDestructive },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DotMultDestructive,
                Summary = "An extra multiplier on Destructive Curse DoT ticks.",
                Details = "Applies only to the Destructive Curse DoT (every level, including Incantation). Each tick is multiplied by this number on top "
                    + "of dot_damage_mult. Corrosion and Corruption ticks are not touched by it.",
                Shape = "A multiplier. 0.1 means Destructive Curse ticks at one tenth of what dot_damage_mult alone gives. Below 0 is read as 0. A value that is not a real number is read as 1.",
                WhenUnset = "No extra change (x1).",
                Raise = "Destructive Curse ticks hit harder than the other DoTs.",
                Lower = "Destructive Curse ticks hit softer. At 0 Destructive Curse deals nothing.",
                Range = "Any number from 0. Below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = DebuffGateNote + " A value of 0 turns Destructive Curse off on that monster.",
                Related = new[] { ZoneStat.DotDamageMult, ZoneStat.DotMultCorruption },
            });
        }
    }
}
