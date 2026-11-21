using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Weapon cards, part A: the Cast on Strike arc and ring cards and the Rending (imbue) card. Checked against
    // ZoneLootMutator.TrySpecialRolls (the card rolls and stamps), WonT / CouldWinT (the chance gate and the per-zone
    // off switch), ZoneStatResolver.WeaponDropBand (the damage band), SpellProjectile.CalculateDamage (arc damage, aug cap,
    // variance at hit time), Player_Magic / WorldObject_Magic (the ring path) and WorldObject_Weapon.TryProcItemWithChanceMod
    // (the per-hit proc roll).
    public static partial class ZoneStatHelp
    {
        private const string WeaponAProcGateNote =
            "Cast on Strike is only offered on a weapon (melee, missile or caster) that has no Cast on Strike spell already, "
            + "and whose own damage type has a matching arc and ring. A weapon with no known element, such as a plain bow, never gets one. "
            + "Only kills in a Zone Control zone roll weapon cards. ";

        private const string WeaponAStampNote =
            "The number is stamped on the weapon when it drops, so weapons that have already dropped keep what they were given. ";

        private const string WeaponAOffNote =
            "The card can also be switched off for one zone with /zonecontrol weaponcard; that off switch beats any chance set here, "
            + "even one inherited from the tier default, and the zone's own number is kept for when it is switched back on. ";

        private static readonly string[] WeaponAArcRelated =
        {
            ZoneStat.WeaponProcArcChance, ZoneStat.WeaponProcArcRate, ZoneStat.WeaponProcArcDmgMin, ZoneStat.WeaponProcArcDmgMax,
            ZoneStat.WeaponProcArcVariance, ZoneStat.WeaponProcArcSpellcraft, ZoneStat.WeaponProcArcAugCap,
        };

        private static readonly string[] WeaponARingRelated =
        {
            ZoneStat.WeaponProcRingChance, ZoneStat.WeaponProcRingRate, ZoneStat.WeaponProcRingDmgMin, ZoneStat.WeaponProcRingDmgMax,
            ZoneStat.WeaponProcRingVariance, ZoneStat.WeaponProcRingSpellcraft, ZoneStat.WeaponProcRingAugCap,
        };

        private static ZoneStatHelpEntry WeaponAChanceEntry(string key, string slot, string slotWhat, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The chance a dropped weapon carries the Cast on Strike " + slot + ".",
                Details = "Each weapon that drops from a kill in this zone gets one roll for the " + slot + " card. If it wins, the weapon is given a " + slotWhat
                    + " in the weapon's own element (a Fire weapon can only get a Fire spell). "
                    + "The arc and the ring are separate cards with separate rolls, so one weapon can get neither, one, or both. "
                    + "The card also stamps the rate, damage, variance, spellcraft and aug cap for this slot (see the Related stats). "
                    + "If the zone has a cap or a floor on the number of weapon cards per drop, a winning roll can be trimmed away, and a short drop can be topped up from cards that lost their roll.",
                Shape = "A chance from 0 to 1 (0.25 = 25 percent of eligible weapons). Below 0 is read as 0 and above 1 as 1. "
                    + "A chance of 1 or more always wins its roll and is never trimmed by the card cap.",
                WhenUnset = "The card never drops. A weapon rolls this card only when this stat is set above 0.",
                Raise = "More weapons carry the " + slot + ". At 1 every eligible weapon does.",
                Lower = "Fewer weapons carry it. At 0 none do.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAProcGateNote + WeaponAStampNote + WeaponAOffNote
                    + "A weapon that wins the roll but cannot take the card (no element, or it already has a proc) gets nothing and spends no card slot.",
                Related = related,
            };
        }

        private static ZoneStatHelpEntry WeaponARateEntry(string key, string slot, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "How often the " + slot + " fires when the weapon hits.",
                Details = "When a weapon that carries the " + slot + " lands a hit, the " + slot + " has this chance to fire. "
                    + "The arc and the ring roll separately, each on its own rate, so a weapon carrying both can fire both on one hit. "
                    + "The value is copied onto the weapon when it drops; the roll itself is made on every hit.",
                Shape = "A chance per hit from 0 to 1 (0.05 = 5 percent). Below 0 is read as 0 and above 1 as 1.",
                WhenUnset = "A fixed default of 0.05 (5 percent per hit) is stamped on the weapon.",
                Raise = "The " + slot + " fires more often per hit. Damage over time goes up in proportion.",
                Lower = "It fires less often. At 0 it never fires.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAStampNote + "Weapons already dropped keep their old rate. "
                    + "A weapon the zone lock holds is held to the lock's own proc rate cap. "
                    + "A wand casts faster than a sword swings, so the same rate gives a caster more fires than a melee weapon.",
                Related = related,
            };
        }

        private static ZoneStatHelpEntry WeaponADmgEntry(string key, string slot, bool isMin, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The " + (isMin ? "lowest" : "highest") + " base damage a dropped weapon's " + slot + " can be given.",
                Details = "When a weapon wins the " + slot + " card, a base damage number is picked between the min and the max. "
                    + "It is stamped on the weapon. When the " + slot + " fires, this number REPLACES the spell's own rolled base damage. "
                    + "The wielder's War Magic or Void Magic augs (by the spell's school) are then added flat on top, up to the weapon's aug cap, "
                    + "then the weapon's variance is applied to the whole base, then crits and the usual resists and ratings. "
                    + "This replacement only happens while Zone Control's endgame rules apply to the hit and the weapon is not zone locked; otherwise the spell hits for its normal damage. "
                    + "Min and max are used as a pair: if only one is set, the roll is that exact number; if both are set the roll is between them (reversed values are swapped). "
                    + "The T25 twins of both set the same pair at T25, and tiers between sit on the line.",
                Shape = "An absolute damage number, not a multiplier. Neither min nor max set uses a fixed default band of 1 to 2 at every tier. "
                    + "Each end is clamped to 1 to 100000.",
                WhenUnset = "If neither min nor max is set, the default band of 1 to 2 is used. Damage then comes almost entirely from the wielder's augs.",
                Raise = "The base of each proc hit goes up by the same amount. Because wielder augs are added on top, a change here matters most for wielders with few augs.",
                Lower = "The base goes down, to as little as 1.",
                Range = "1 to 100000 (values outside are clamped).",
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAStampNote + "Exception: after an admin ladder apply, a weapon is re-stamped from the tier default band the next time it is equipped, "
                    + "so a number set on a single zone shapes new drops from that zone only and can drift to the tier default later. "
                    + "Rare drops take the top of the band. Needs the " + slot + " card to drop at all (see its chance stat).",
                Related = related,
            };
        }

        private static ZoneStatHelpEntry WeaponAVarianceEntry(string key, string slot, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "How much each " + slot + " hit can fall below its full damage.",
                Details = "Every time the " + slot + " fires, its whole base (the stamped damage plus the wielder's aug term) is multiplied by a random amount "
                    + "between (1 minus this number) and 1. The roll is made on every hit, so one weapon does not hit for the same number every time. "
                    + "The value is copied onto the weapon when it drops, and only if it is above 0.",
                Shape = "A fraction from 0 to 1. 0 = every hit is the full base. 0.5 = hits land between 50 and 100 percent of the base. "
                    + "Below 0 is read as 0 and above 1 as 1. It only ever lowers damage, so the full base is the ceiling.",
                WhenUnset = "0: no spread, every hit is the full base.",
                Raise = "Hits spread further down; the average hit goes lower.",
                Lower = "Hits are more even. At 0 every hit is the full base.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAStampNote + "Does nothing unless the weapon also carries this " + slot + " and its damage is the zone proc damage (the same condition as the damage stats).",
                Related = related,
            };
        }

        private static ZoneStatHelpEntry WeaponAAugCapEntry(string key, string slot, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The most wielder augs that can be added to this " + slot + " hit.",
                Details = "When the " + slot + " fires, the wielder's War Magic aug count (or Void Magic, by the spell's school) is added to the damage. "
                    + "This number limits how many augs count. It is stamped on the weapon and applies to this card's proc hits only, never to a spell the player casts by hand. "
                    + "If this stat is not set, each weapon gets a random cap picked between 500 and the tier's weapon scaling aug cap (a server setting), "
                    + "so the drop's damage quality is decided by that roll. Rare drops get the top of that range.",
                Shape = "An exact aug count, rounded when the aug term is applied. Setting the stat gives every weapon exactly this cap. "
                    + "A value of 0 or below stamps no cap at all.",
                WhenUnset = "Each weapon rolls its own cap between 500 and the tier's weapon scaling aug cap.",
                Raise = "More of the wielder's augs count, so proc hits hit harder for high-aug wielders.",
                Lower = "Fewer augs count. A wielder with fewer augs than the cap is not affected.",
                Range = "Any number above 0 sets a cap. 0 or below means no cap.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Setting 0 does NOT mean a cap of 0: it stamps nothing, and the weapon's augs are then added without limit. "
                    + "Clear the stat to get the random 500-to-tier-cap roll instead. " + WeaponAStampNote,
                Related = related,
            };
        }

        private static ZoneStatHelpEntry WeaponASpellcraftEntry(string key, string slot, string otherSlot, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The spellcraft stamped on a weapon that carries the " + slot + ".",
                Details = "A weapon that wins the " + slot + " card gets this number as its item spellcraft. "
                    + "When a Cast on Strike spell is checked for resist, a caster item that has a spellcraft uses that number as the casting skill, "
                    + "so a high value makes the proc much harder for monsters to resist than the wielder's own skill would. "
                    + "A weapon has only one spellcraft, so when it carries both the arc and the ring it gets the HIGHER of the two numbers. "
                    + "The stamp never lowers a spellcraft the weapon already had.",
                Shape = "An absolute skill-like number, rounded to a whole number. Zero or below stamps nothing.",
                WhenUnset = "A fixed default of 9999 is used.",
                Raise = "The proc is resisted less often.",
                Lower = "The proc is resisted more often. At 0 nothing is stamped and the check falls back to the wielder's skill in that school.",
                Range = "Any whole number from 1. There is no upper clamp.",
                Timing = ZoneStatTiming.Loot,
                Caution = "On a weapon with both cards, the " + slot + " number is only used if it is higher than the " + otherSlot + " number. "
                    + "The lower of the two has no effect on that weapon. " + WeaponAStampNote,
                Related = related,
            };
        }

        static partial void AddWeaponA(List<ZoneStatHelpEntry> e)
        {
            // ---- Cast on Strike: arc ----

            e.Add(WeaponAChanceEntry(ZoneStat.WeaponProcArcChance, "arc", "single-target arc spell (Blade Arc, Flame Arc and so on)", WeaponAArcRelated));
            e.Add(WeaponARateEntry(ZoneStat.WeaponProcArcRate, "arc", WeaponAArcRelated));
            e.Add(WeaponADmgEntry(ZoneStat.WeaponProcArcDmgMin, "arc", true, WeaponAArcRelated));
            e.Add(WeaponADmgEntry(ZoneStat.WeaponProcArcDmgMax, "arc", false, WeaponAArcRelated));
            e.Add(WeaponAVarianceEntry(ZoneStat.WeaponProcArcVariance, "arc", WeaponAArcRelated));
            e.Add(WeaponASpellcraftEntry(ZoneStat.WeaponProcArcSpellcraft, "arc", "ring", WeaponAArcRelated));
            e.Add(WeaponAAugCapEntry(ZoneStat.WeaponProcArcAugCap, "arc", WeaponAArcRelated));

            // ---- Cast on Strike: ring ----

            e.Add(WeaponAChanceEntry(ZoneStat.WeaponProcRingChance, "ring", "ring spell (a ring of projectiles around the wielder, such as Searing Disc or Halo of Frost)", WeaponARingRelated));
            e.Add(WeaponARateEntry(ZoneStat.WeaponProcRingRate, "ring", WeaponARingRelated));
            e.Add(WeaponADmgEntry(ZoneStat.WeaponProcRingDmgMin, "ring", true, WeaponARingRelated));
            e.Add(WeaponADmgEntry(ZoneStat.WeaponProcRingDmgMax, "ring", false, WeaponARingRelated));
            e.Add(WeaponAVarianceEntry(ZoneStat.WeaponProcRingVariance, "ring", WeaponARingRelated));
            e.Add(WeaponASpellcraftEntry(ZoneStat.WeaponProcRingSpellcraft, "ring", "arc", WeaponARingRelated));
            e.Add(WeaponAAugCapEntry(ZoneStat.WeaponProcRingAugCap, "ring", WeaponARingRelated));

            // ---- Rending (imbue) ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponImbueChance,
                Summary = "The chance a dropped weapon carries an elemental rend (the Rending card).",
                Details = "Each weapon that drops from a kill in this zone gets one roll for the Rending card. "
                    + "If it wins, one rend that matches the weapon's own damage type is added to the weapon (a Fire weapon gets Fire Rend). "
                    + "A weapon with several damage types picks one of the matching rends at random, skipping any rend it already has. "
                    + "Casters can get it as well as melee and missile weapons. "
                    + "A rend the card adds also gets its strength rolled from the Rend Power band, so the Rend Power min and max decide how strong it is. "
                    + "If the zone has a cap or a floor on the number of weapon cards per drop, a winning roll can be trimmed away, and a short drop can be topped up from cards that lost their roll.",
                Shape = "A chance from 0 to 1 (0.25 = 25 percent of eligible weapons). Below 0 is read as 0 and above 1 as 1. "
                    + "A chance of 1 or more always wins its roll and is never trimmed by the card cap.",
                WhenUnset = "The card never drops. A weapon rolls this card only when this stat is set above 0.",
                Raise = "More weapons carry a rend. At 1 every eligible weapon does.",
                Lower = "Fewer weapons carry one. At 0 none do.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Only kills in a Zone Control zone roll weapon cards. "
                    + "A weapon with no known damage type (a plain bow, whose element comes from its ammo), or one that already has every matching rend, gets nothing and spends no card slot. "
                    + "This card adds the elemental rend only. Armor Rending and the other imbues are separate cards with their own chance stats. "
                    + WeaponAStampNote + WeaponAOffNote,
                Related = new[] { ZoneStat.WeaponRendPowerMin, ZoneStat.WeaponRendPowerMax, ZoneStat.WeaponModifierCap, ZoneStat.WeaponModifierMin },
            });
        }
    }
}
