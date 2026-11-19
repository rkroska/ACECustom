using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Weapon cards, part B2: Biting Strike, Crushing Blow, Armor Rend, Shield Cleaving, Rend Power and Spell Armor Rend.
    // Checked against ZoneLootMutator.TrySpecialRolls (the rolls and stamps), WonT (chance gate and per-zone off switch),
    // StampWeaponCard and ZoneStatResolver.WeaponDropBand (min/max and the grade roll), ZoneModifiers bands (the built-in ladders),
    // WorldObject_Weapon (crit chance, crit damage, rend strength, shield ignore at hit time), DamageEvent (armor rend at hit time),
    // Creature_SpellArmor (spell armor rend), ZoneControlManager.WeaponPowerSuppressed and ZoneLockFallback (the lock caps).
    public static partial class ZoneStatHelp
    {
        private const string WeaponB2LockNote =
            "Player weapons from Zone Control drops do nothing special while the zonecontrol_enabled setting is off. "
            + "While the zone lock holds a weapon (and Zone Control is on), this card is cut back as described below, so a bigger number only shows once the lock is off. ";

        private const string WeaponB2BandNote =
            "Min and max are used as a pair: if only one is set, every weapon gets that exact number; if both are set, each drop gets a number between them (reversed values are swapped). "
            + "Each drop rolls a grade, and the grade picks the spot in the band; higher tiers roll better grades on average. "
            + "The T25 twins of both set the same pair at T25, and tiers between sit on the line. "
            + "The number is stamped with its grade, so changing the band later also moves weapons that have already dropped, the next time they are equipped.";

        private static readonly string[] WeaponB2BiteRelated =
        {
            ZoneStat.WeaponBiteChance, ZoneStat.WeaponBiteMin, ZoneStat.WeaponBiteMax,
        };

        private static readonly string[] WeaponB2CrushRelated =
        {
            ZoneStat.WeaponCrushChance, ZoneStat.WeaponCrushMin, ZoneStat.WeaponCrushMax,
        };

        private static readonly string[] WeaponB2ArmorRendRelated =
        {
            ZoneStat.WeaponArmorRendChance, ZoneStat.WeaponArmorRendMin, ZoneStat.WeaponArmorRendMax,
        };

        private static readonly string[] WeaponB2ShieldRelated =
        {
            ZoneStat.WeaponShieldCleaveChance, ZoneStat.WeaponShieldCleaveMin, ZoneStat.WeaponShieldCleaveMax,
        };

        private static readonly string[] WeaponB2RendPowerRelated =
        {
            ZoneStat.WeaponImbueChance, ZoneStat.WeaponRendPowerMin, ZoneStat.WeaponRendPowerMax,
        };

        private static readonly string[] WeaponB2SpellArmorRelated =
        {
            ZoneStat.WeaponSpellArmorRendChance, ZoneStat.WeaponSpellArmorRendMin, ZoneStat.WeaponSpellArmorRendMax, ZoneStat.SpellArmor,
        };

        private static ZoneStatHelpEntry WeaponB2ChanceEntry(string key, string cardName, string who, string whoNever, string what, string extraCaution, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The chance a dropped " + who + " carries the " + cardName + " card.",
                Details = "Each " + who + " that drops from a kill in this zone gets one roll for the " + cardName + " card. If it wins, " + what + " "
                    + whoNever
                    + "If the zone has a cap or a floor on the number of weapon cards per drop, a winning roll can be trimmed away, and a short drop can be topped up from cards that lost their roll.",
                Shape = "A chance from 0 to 1 (0.25 = 25 percent of eligible weapons). Below 0 is read as 0 and above 1 as 1. "
                    + "A chance of 1 or more always wins its roll and is never trimmed by the card cap.",
                WhenUnset = "The card never drops. A weapon rolls this card only when this stat is set above 0.",
                Raise = "More weapons carry the card. At 1 every eligible weapon does.",
                Lower = "Fewer weapons carry the card. At 0 none do.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Only kills in a Zone Control zone roll weapon cards. " + extraCaution
                    + "The card does nothing in the hands of a player while the zonecontrol_enabled setting is off. "
                    + WeaponAStampNote + WeaponAOffNote,
                Related = related,
            };
        }

        private static ZoneStatHelpEntry WeaponB2BandEntry(string key, bool isMin, string cardName, string what, string shape, string whenUnset,
            string raise, string lower, string range, string extraCaution, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The " + (isMin ? "lowest" : "highest") + " " + what + " a dropped weapon can be given by the " + cardName + " card.",
                Details = "When a weapon wins the " + cardName + " card, a number is picked between the min and the max and stamped on the weapon. " + WeaponB2BandNote,
                Shape = shape,
                WhenUnset = whenUnset,
                Raise = raise,
                Lower = lower,
                Range = range,
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAStampNote + WeaponB2LockNote + extraCaution + " Needs the " + cardName + " card to drop at all (see its chance stat).",
                Related = related,
            };
        }

        static partial void AddWeaponB2(List<ZoneStatHelpEntry> e)
        {
            // ---- Biting Strike ----

            e.Add(WeaponB2ChanceEntry(ZoneStat.WeaponBiteChance, "Biting Strike", "weapon", "",
                "the weapon crits more often (the chance comes from the Bite min and max). ",
                "Any weapon type can roll it (melee, missile or caster). A rare drop only gets it as a last-resort filler when the weapon is still under the card cap. ",
                WeaponB2BiteRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponBiteMin, true, "Biting Strike", "crit chance",
                "A chance from 0 to 1 (0.40 = 40 percent of hits can crit). Clamped to 0 to 1.",
                "If neither min nor max is set, the built-in tier band is used: 0.33 to 0.40 at T11, rising in a straight line to 0.50 to 0.60 at T25.",
                "Hits with the weapon crit more often.",
                "Hits crit less often. At 0 the weapon never crits on its own chance.",
                "0 to 1 (values outside are clamped).",
                "The number replaces the weapon's own crit chance (the normal base is lower); it is then added to by the wielder's crit rating and cut by the target's crit resist rating. "
                    + "The lock's cap for Biting Strike is 0.33 (0.25 more if the weapon carries a Bandit Hilt), never below the weapon's normal base.",
                WeaponB2BiteRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponBiteMax, false, "Biting Strike", "crit chance",
                "A chance from 0 to 1 (0.40 = 40 percent of hits can crit). Clamped to 0 to 1.",
                "If neither min nor max is set, the built-in tier band is used: 0.33 to 0.40 at T11, rising in a straight line to 0.50 to 0.60 at T25.",
                "Hits with the weapon crit more often.",
                "Hits crit less often. At 0 the weapon never crits on its own chance.",
                "0 to 1 (values outside are clamped).",
                "The number replaces the weapon's own crit chance (the normal base is lower); it is then added to by the wielder's crit rating and cut by the target's crit resist rating. "
                    + "The lock's cap for Biting Strike is 0.33 (0.25 more if the weapon carries a Bandit Hilt), never below the weapon's normal base.",
                WeaponB2BiteRelated));

            // ---- Crushing Blow ----

            e.Add(WeaponB2ChanceEntry(ZoneStat.WeaponCrushChance, "Crushing Blow", "weapon", "",
                "the weapon's crits hit harder (the multiplier comes from the Crush min and max). ",
                "Any weapon type can roll it (melee, missile or caster). A rare drop only gets it as a last-resort filler when the weapon is still under the card cap. ",
                WeaponB2CrushRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponCrushMin, true, "Crushing Blow", "crit damage multiplier",
                "The crit damage multiplier as a player reads it (4 = a crit does 4x the damage of a normal hit). Each end is clamped to 2 to 10.",
                "If neither min nor max is set, the built-in tier band is used: 3.25 to 4.00 at T11, rising in a straight line to 5.50 to 6.80 at T25.",
                "Crits with the weapon hit harder.",
                "Crits hit softer, to as little as 2x.",
                "2 to 10 (values outside are clamped).",
                "The number is the final crit multiplier, not a bonus on top: the server stores one less than the number you set and adds the one back at hit time. "
                    + "The weapon scaling crit bonus only counts when it is higher than this number. "
                    + "The lock's cap for Crushing Blow is 3.25x (a little more if the weapon carries a Bandit Hilt).",
                WeaponB2CrushRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponCrushMax, false, "Crushing Blow", "crit damage multiplier",
                "The crit damage multiplier as a player reads it (4 = a crit does 4x the damage of a normal hit). Each end is clamped to 2 to 10.",
                "If neither min nor max is set, the built-in tier band is used: 3.25 to 4.00 at T11, rising in a straight line to 5.50 to 6.80 at T25.",
                "Crits with the weapon hit harder.",
                "Crits hit softer, to as little as 2x.",
                "2 to 10 (values outside are clamped).",
                "The number is the final crit multiplier, not a bonus on top: the server stores one less than the number you set and adds the one back at hit time. "
                    + "The weapon scaling crit bonus only counts when it is higher than this number. "
                    + "The lock's cap for Crushing Blow is 3.25x (a little more if the weapon carries a Bandit Hilt).",
                WeaponB2CrushRelated));

            // ---- Armor Rend ----

            e.Add(WeaponB2ChanceEntry(ZoneStat.WeaponArmorRendChance, "Armor Rend", "melee or missile weapon", "Casters never roll it. ",
                "the weapon gets the Armor Rending imbue and ignores part of the target's armor on a hit (the share comes from the Armor Rend min and max). ",
                "On a rare drop the chance roll is skipped and the card is forced on wherever the weapon can have it, but it must still be available (chance set above 0). ",
                WeaponB2ArmorRendRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponArmorRendMin, true, "Armor Rend", "share of armor ignored",
                "A fraction from 0 to 1 (0.65 = a hit ignores 65 percent of the target's armor). Clamped to 0 to 1.",
                "If neither min nor max is set, the built-in tier band is used: 0.62 to 0.68 at T11, rising in a straight line to 0.74 to 0.80 at T25.",
                "Hits ignore more armor.",
                "Hits ignore less armor. At 0 the card does nothing.",
                "0 to 1 (values outside are clamped).",
                "This number replaces the normal skill-based Armor Rending strength (which tops out lower). It is not added to the weapon's Armor Cleaving: the hit uses whichever of the two ignores more armor. "
                    + "While the zone lock holds the weapon the card is switched off completely, with no cap.",
                WeaponB2ArmorRendRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponArmorRendMax, false, "Armor Rend", "share of armor ignored",
                "A fraction from 0 to 1 (0.65 = a hit ignores 65 percent of the target's armor). Clamped to 0 to 1.",
                "If neither min nor max is set, the built-in tier band is used: 0.62 to 0.68 at T11, rising in a straight line to 0.74 to 0.80 at T25.",
                "Hits ignore more armor.",
                "Hits ignore less armor. At 0 the card does nothing.",
                "0 to 1 (values outside are clamped).",
                "This number replaces the normal skill-based Armor Rending strength (which tops out lower). It is not added to the weapon's Armor Cleaving: the hit uses whichever of the two ignores more armor. "
                    + "While the zone lock holds the weapon the card is switched off completely, with no cap.",
                WeaponB2ArmorRendRelated));

            // ---- Shield Cleaving ----

            e.Add(WeaponB2ChanceEntry(ZoneStat.WeaponShieldCleaveChance, "Shield Cleaving", "melee or missile weapon", "Casters never roll it, because shields do not stop spells the way they stop swings and shots. ",
                "the weapon ignores part of the target's shield when it hits (the share comes from the Shield Cleave min and max). ",
                "",
                WeaponB2ShieldRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponShieldCleaveMin, true, "Shield Cleaving", "share of shield ignored",
                "A fraction from 0 to 1 (0.75 = the target's shield counts for 25 percent of its level against this weapon). At 1 the shield is ignored entirely. Clamped to 0 to 1.",
                "If neither min nor max is set, the built-in tier band is used: 0.50 to 0.75 at T11, rising in a straight line to 0.90 to 1.00 at T25.",
                "More of the target's shield is ignored.",
                "Less of the shield is ignored. At 0 the card does nothing.",
                "0 to 1 (values outside are clamped).",
                "If the target has its own shield-ignore value, the larger of the two is used, not both. "
                    + "While the zone lock holds the weapon the card is switched off completely, with no cap.",
                WeaponB2ShieldRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponShieldCleaveMax, false, "Shield Cleaving", "share of shield ignored",
                "A fraction from 0 to 1 (0.75 = the target's shield counts for 25 percent of its level against this weapon). At 1 the shield is ignored entirely. Clamped to 0 to 1.",
                "If neither min nor max is set, the built-in tier band is used: 0.50 to 0.75 at T11, rising in a straight line to 0.90 to 1.00 at T25.",
                "More of the target's shield is ignored.",
                "Less of the shield is ignored. At 0 the card does nothing.",
                "0 to 1 (values outside are clamped).",
                "If the target has its own shield-ignore value, the larger of the two is used, not both. "
                    + "While the zone lock holds the weapon the card is switched off completely, with no cap.",
                WeaponB2ShieldRelated));

            // ---- Rend Power (strength of the elemental rend stamped by the Rending card) ----

            e.Add(WeaponB2RendPowerEntry(ZoneStat.WeaponRendPowerMin, true));
            e.Add(WeaponB2RendPowerEntry(ZoneStat.WeaponRendPowerMax, false));

            // ---- Spell Armor Rend ----

            e.Add(WeaponB2ChanceEntry(ZoneStat.WeaponSpellArmorRendChance, "Spell Armor Rend", "caster weapon (wand, staff or orb)", "Melee and missile weapons never roll it. ",
                "the weapon's spells ignore part of the target's Spell Armor level (the share comes from the Spell Armor Rend min and max). ",
                "It does nothing against a monster whose zone has no Spell Armor set (see the Spell Armor stat). ",
                WeaponB2SpellArmorRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponSpellArmorRendMin, true, "Spell Armor Rend", "share of Spell Armor ignored",
                "A fraction from 0 to 1 (0.65 = the target's Spell Armor level counts for 35 percent of itself against this weapon's spells). Clamped to 0 to 1.",
                "If neither min nor max is set, the built-in tier band is used: 0.62 to 0.68 at T11, rising in a straight line to 0.74 to 0.80 at T25.",
                "More of the monster's Spell Armor is ignored, so spells hit harder where Spell Armor is set.",
                "Less is ignored. At 0 the card does nothing.",
                "0 to 1 (values outside are clamped).",
                "The share is taken off the Spell Armor level before the damage cut is worked out, so it only matters on monsters whose zone sets Spell Armor, and only for spells cast by a player. "
                    + "A void damage-over-time spell keeps the share the wand had when it was cast. "
                    + "While the zone lock holds the weapon the card is switched off completely, with no cap.",
                WeaponB2SpellArmorRelated));
            e.Add(WeaponB2BandEntry(ZoneStat.WeaponSpellArmorRendMax, false, "Spell Armor Rend", "share of Spell Armor ignored",
                "A fraction from 0 to 1 (0.65 = the target's Spell Armor level counts for 35 percent of itself against this weapon's spells). Clamped to 0 to 1.",
                "If neither min nor max is set, the built-in tier band is used: 0.62 to 0.68 at T11, rising in a straight line to 0.74 to 0.80 at T25.",
                "More of the monster's Spell Armor is ignored, so spells hit harder where Spell Armor is set.",
                "Less is ignored. At 0 the card does nothing.",
                "0 to 1 (values outside are clamped).",
                "The share is taken off the Spell Armor level before the damage cut is worked out, so it only matters on monsters whose zone sets Spell Armor, and only for spells cast by a player. "
                    + "A void damage-over-time spell keeps the share the wand had when it was cast. "
                    + "While the zone lock holds the weapon the card is switched off completely, with no cap.",
                WeaponB2SpellArmorRelated));
        }

        private static ZoneStatHelpEntry WeaponB2RendPowerEntry(string key, bool isMin)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The " + (isMin ? "lowest" : "highest") + " strength a dropped weapon's elemental rend can be given.",
                Details = "This is not a card of its own and has no chance stat. The Rending card (the Weapon imbue chance) gives a weapon an elemental rend matching its own damage type, "
                    + "and every rend the card gives also gets a strength picked between the min and the max. Rends that come from normal loot or crafting keep the normal skill-based strength. "
                    + "When the weapon hits a monster, the rend raises the damage the monster takes from that element: the hit is multiplied by 1 plus this number, replacing the normal skill-based rend strength and its cap. "
                    + WeaponB2BandNote,
                Shape = "A bonus added to 1: 2.0 means the monster takes 3x from that element, 4.0 means 5x. "
                    + "Each end is clamped to 1.5 to 5.0. This is a plain number, not a percent.",
                WhenUnset = "If neither min nor max is set, the built-in tier band is used: 1.50 to 2.13 at T11, rising in a straight line to 3.75 to 5.00 at T25.",
                Raise = "Rended weapons hit monsters much harder (every point is a whole extra multiple of the hit).",
                Lower = "Rended weapons hit less hard, to as little as 2.5x at the floor of 1.5.",
                Range = "1.5 to 5.0 (values outside are clamped).",
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAStampNote + "Needs the Rending card to drop at all: that is the Weapon imbue chance stat, not a chance of its own. "
                    + "The number is a direct bonus, so a small change here is a large change in damage; this is the one channel that skips the zone vulnerability cap. "
                    + "While the zone lock holds the weapon (and Zone Control is on), the rolled strength is off and the rend works at its normal skill-based strength; while the zonecontrol_enabled setting is off the rend does nothing. "
                    + "A rend only applies when it matches the element of the hit.",
                Related = WeaponB2RendPowerRelated,
            };
        }
    }
}
