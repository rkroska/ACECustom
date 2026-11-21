using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Weapon cards, part B1: the Slayer, Cleaving and Split Arrows cards, and the Slayer species pool share. Checked against
    // ZoneLootMutator.TrySpecialRolls (the rolls and stamps), WonT (chance gate and per-zone off switch), RollRangeBand and
    // CleaveSplitBandAt (the count bands), ZoneStatResolver.WeaponDropBand and ZoneModifiers.WeaponSlayer (the slayer band),
    // ZoneSlayerPool (the species pool), WorldObject_Weapon.GetWeaponCreatureSlayerModifier (slayer at hit time),
    // Player_Melee (cleave at hit time), Creature_Missile.CreateSplitArrows and DamageEvent (split arrows at hit time),
    // and ZoneLockFallback (the zone lock caps).
    public static partial class ZoneStatHelp
    {
        private const string WeaponB1LockNote =
            "While the zone lock holds a weapon (and Zone Control is on), the hit is held to a fixed cap, so a bigger number than the cap only shows once the lock is off. ";

        private static readonly string[] WeaponB1SlayerRelated =
        {
            ZoneStat.WeaponSlayerChance, ZoneStat.WeaponSlayerMin, ZoneStat.WeaponSlayerMax, ZoneStat.SlayerPoolShare,
        };

        private static readonly string[] WeaponB1CleaveRelated =
        {
            ZoneStat.WeaponCleaveChance, ZoneStat.WeaponCleaveMin, ZoneStat.WeaponCleaveMax,
        };

        private static readonly string[] WeaponB1SplitRelated =
        {
            ZoneStat.WeaponSplitChance, ZoneStat.WeaponSplitMin, ZoneStat.WeaponSplitMax, ZoneStat.WeaponSplitRange, ZoneStat.WeaponSplitDmg,
        };

        private static ZoneStatHelpEntry WeaponB1SlayerBandEntry(string key, bool isMin)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The " + (isMin ? "lowest" : "highest") + " Slayer damage multiplier a dropped weapon can be given.",
                Details = "When a weapon wins the Slayer card, a damage multiplier is picked between the min and the max and stamped on the weapon. "
                    + "Each drop rolls a grade, and the grade picks the spot in the band; higher tiers roll better grades on average. "
                    + "When a player hits a monster of the weapon's Slayer species, the hit is multiplied by this number. Other species take no bonus. "
                    + "Min and max are used as a pair: if only one is set, the weapon gets that exact number; if both are set, the roll is between them (reversed values are swapped). "
                    + "The T25 twins of both set the same pair at T25, and tiers between sit on the line.",
                Shape = "A raw damage multiplier (2.0 = double damage against the Slayer species). Each end is clamped to 1.5 to 10.",
                WhenUnset = "If neither min nor max is set, the built-in tier ladder is used: 1.75 to 2.10 at T11, rising to 3.00 to 4.10 at T25.",
                Raise = "Slayer weapons hit their species harder.",
                Lower = "Slayer weapons hit their species softer, to as little as 1.5x.",
                Range = "1.5 to 10 (values outside are clamped).",
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAStampNote + WeaponB1LockNote
                    + "The lock's cap for Slayer is 5.0x. Rare drops take the top of the band. "
                    + "Needs the Slayer card to drop at all (see its chance stat). The number is a multiplier on the whole hit, so every point here is felt on every hit against that species.",
                Related = WeaponB1SlayerRelated,
            };
        }

        private static ZoneStatHelpEntry WeaponB1CountBandEntry(string key, string what, string whatPlural, bool isMin, string[] related, string extraCaution)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The " + (isMin ? "lowest" : "highest") + " number of " + whatPlural + " a dropped weapon can be given.",
                Details = "When a weapon wins the " + what + " card, a count is picked between the min and the max and rounded to a whole number. "
                    + "Min and max are used as a pair: if only one is set, every weapon gets that exact count; if both are set, each drop rolls between them (reversed values are swapped). "
                    + "The T25 twins of both set the same pair at T25, and tiers between sit on the line.",
                Shape = "A whole number of " + whatPlural + " (the roll is rounded). Each end is clamped to 1 to 10.",
                WhenUnset = "If neither min nor max is set, the built-in tier band is used: 1 to 2 at T11, rising in a straight line to 8 to 10 at T25.",
                Raise = "Weapons with the card hit more " + whatPlural + ".",
                Lower = "Weapons with the card hit fewer " + whatPlural + ", to as little as 1.",
                Range = "1 to 10 (values outside are clamped).",
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAStampNote + "Rare drops take the top of the band. Needs the " + what + " card to drop at all (see its chance stat). " + extraCaution,
                Related = related,
            };
        }

        static partial void AddWeaponB1(List<ZoneStatHelpEntry> e)
        {
            // ---- Slayer ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponSlayerChance,
                Summary = "The chance a dropped weapon carries a Slayer attunement against a monster species.",
                Details = "Each weapon that drops from a kill in this zone gets one roll for the Slayer card. If it wins, the weapon is attuned to one monster species "
                    + "and does extra damage to that species (the multiplier comes from the Slayer min and max). "
                    + "Normally the species is the monster that dropped the weapon. The Slayer pool share can name any species in the zone instead. "
                    + "A rare drop always takes the species of the monster that dropped it. "
                    + "If the zone has a cap or a floor on the number of weapon cards per drop, a winning roll can be trimmed away, and a short drop can be topped up from cards that lost their roll.",
                Shape = "A chance from 0 to 1 (0.25 = 25 percent of eligible weapons). Below 0 is read as 0 and above 1 as 1. "
                    + "A chance of 1 or more always wins its roll and is never trimmed by the card cap.",
                WhenUnset = "The card never drops. A weapon rolls this card only when this stat is set above 0.",
                Raise = "More weapons carry a Slayer. At 1 every eligible weapon does.",
                Lower = "Fewer weapons carry one. At 0 none do.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Any weapon type can roll it (melee, missile or caster). A weapon that already has a Slayer species, or a drop with no known monster species, gets nothing and spends no card slot. "
                    + "Only kills in a Zone Control zone roll weapon cards. "
                    + WeaponAStampNote + WeaponAOffNote,
                Related = WeaponB1SlayerRelated,
            });
            e.Add(WeaponB1SlayerBandEntry(ZoneStat.WeaponSlayerMin, true));
            e.Add(WeaponB1SlayerBandEntry(ZoneStat.WeaponSlayerMax, false));

            // ---- Cleaving ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponCleaveChance,
                Summary = "The chance a dropped melee weapon carries the Cleaving card.",
                Details = "Each melee weapon that drops from a kill in this zone gets one roll for the Cleaving card. If it wins, a swing that connects also hits extra monsters near the target. "
                    + "The number of extra targets comes from the Cleave min and max. "
                    + "Missile weapons and casters never roll it. "
                    + "If the zone has a cap or a floor on the number of weapon cards per drop, a winning roll can be trimmed away, and a short drop can be topped up from cards that lost their roll.",
                Shape = "A chance from 0 to 1 (0.25 = 25 percent of melee weapons). Below 0 is read as 0 and above 1 as 1. "
                    + "A chance of 1 or more always wins its roll and is never trimmed by the card cap.",
                WhenUnset = "The card never drops. A weapon rolls this card only when this stat is set above 0.",
                Raise = "More melee weapons cleave. At 1 every melee weapon does.",
                Lower = "Fewer melee weapons cleave. At 0 none do.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Only kills in a Zone Control zone roll weapon cards. "
                    + "The card always replaces the weapon's own Cleaving if that is higher: a weapon that already cleaves (two-handers do) ends up with one more extra target than its own count or the roll, whichever is bigger, so the card is never wasted. "
                    + WeaponAStampNote + WeaponAOffNote,
                Related = WeaponB1CleaveRelated,
            });
            e.Add(WeaponB1CountBandEntry(ZoneStat.WeaponCleaveMin, "Cleaving", "extra targets", true, WeaponB1CleaveRelated,
                "The count is extra targets besides the one you hit. " + WeaponB1LockNote + "The lock's cap is 3 extra targets."));
            e.Add(WeaponB1CountBandEntry(ZoneStat.WeaponCleaveMax, "Cleaving", "extra targets", false, WeaponB1CleaveRelated,
                "The count is extra targets besides the one you hit. " + WeaponB1LockNote + "The lock's cap is 3 extra targets."));

            // ---- Split Arrows ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponSplitChance,
                Summary = "The chance a dropped missile weapon carries the Split Arrows card.",
                Details = "Each missile weapon that drops from a kill in this zone gets one roll for the Split Arrows card. If it wins, each shot fires extra arrows at other monsters near the target. "
                    + "The number of extra arrows comes from the Split min and max, how far they look for targets from the Split range, and how hard they hit from the Split damage. "
                    + "Melee weapons and casters never roll it. "
                    + "If the zone has a cap or a floor on the number of weapon cards per drop, a winning roll can be trimmed away, and a short drop can be topped up from cards that lost their roll.",
                Shape = "A chance from 0 to 1 (0.25 = 25 percent of missile weapons). Below 0 is read as 0 and above 1 as 1. "
                    + "A chance of 1 or more always wins its roll and is never trimmed by the card cap.",
                WhenUnset = "The card never drops. A weapon rolls this card only when this stat is set above 0.",
                Raise = "More missile weapons split. At 1 every missile weapon does.",
                Lower = "Fewer missile weapons split. At 0 none do.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Only kills in a Zone Control zone roll weapon cards. "
                    + "The range and damage stats do nothing unless the weapon wins this card. "
                    + WeaponAStampNote + WeaponAOffNote,
                Related = WeaponB1SplitRelated,
            });
            e.Add(WeaponB1CountBandEntry(ZoneStat.WeaponSplitMin, "Split Arrows", "extra arrows", true, WeaponB1SplitRelated,
                "The count is extra arrows besides the main shot. " + WeaponB1LockNote + "A locked launcher that has its own Split Arrows keeps its own count."));
            e.Add(WeaponB1CountBandEntry(ZoneStat.WeaponSplitMax, "Split Arrows", "extra arrows", false, WeaponB1SplitRelated,
                "The count is extra arrows besides the main shot. " + WeaponB1LockNote + "A locked launcher that has its own Split Arrows keeps its own count."));

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponSplitRange,
                Summary = "How far from the main target a split arrow can look for another monster, in meters.",
                Details = "When a weapon wins the Split Arrows card, this distance is stamped on it. When it shoots, the extra arrows pick targets within this distance. "
                    + "It is a single number, not a pair. The T25 twin sets it at T25 and tiers between sit on the line.",
                Shape = "A distance in meters. Clamped to 0 to 50.",
                WhenUnset = "A fixed default of 8 meters is stamped.",
                Raise = "Split arrows reach monsters further from the main target.",
                Lower = "Split arrows only find close neighbours. At 0 there is no distance to look in.",
                Range = "0 to 50 (values outside are clamped).",
                Timing = ZoneStatTiming.Loot,
                Caution = WeaponAStampNote + WeaponB1LockNote + "The lock's cap for range is 10 meters. Needs the Split Arrows card to drop at all.",
                Related = WeaponB1SplitRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponSplitDmg,
                Summary = "The share of full damage each split arrow deals.",
                Details = "When a weapon wins the Split Arrows card, this fraction is stamped on it. Each extra arrow's hit is multiplied by it, on top of the usual damage rules. "
                    + "It is a single number, not a pair. The T25 twin sets it at T25 and tiers between sit on the line.",
                Shape = "A fraction from 0 to 1 (0.6 = each split arrow does 60 percent of a full hit). Clamped to 0 to 1.",
                WhenUnset = "A fixed default of 1 (full damage) is stamped.",
                Raise = "Split arrows hit harder. At 1 they hit as hard as the main shot.",
                Lower = "Split arrows hit softer. At 0 they do no damage.",
                Range = "0 to 1 (values outside are clamped).",
                Timing = ZoneStatTiming.Loot,
                Caution = "At 0 the extra arrows still fly but do nothing. "
                    + WeaponAStampNote + WeaponB1LockNote + "The lock's cap for split damage is 0.95. Needs the Split Arrows card to drop at all.",
                Related = WeaponB1SplitRelated,
            });

            // ---- Slayer species pool ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.SlayerPoolShare,
                Summary = "The chance a normal Slayer drop names any species in the zone, not just the one that was killed.",
                Details = "When a weapon wins the Slayer card, this stat is rolled once. If it wins, the Slayer species is picked from all the monsters the zone has spawned, "
                    + "weighted by how many of each can be alive at once, instead of being the monster that dropped it. "
                    + "The pick ignores the weapon's own element. It does not change how often the Slayer card drops or how big the bonus is. "
                    + "Rare drops always keep the species of the monster that dropped them and never use the pool.",
                Shape = "A chance from 0 to 1. 0 or below never uses the pool. 1 or more always does.",
                WhenUnset = "The pool is never used: every normal Slayer names the species that was killed.",
                Raise = "Slayer weapons are more often attuned to species other than the one killed, so they spread across the zone's monsters.",
                Lower = "More Slayers name the killed species. At 0 all of them do.",
                Range = "0 to 1. 0 or below is off and 1 or more is always.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Does nothing unless the Slayer card wins its roll first (see its chance stat). "
                    + "The pool is kept in memory only: it starts empty after a restart and fills as monsters spawn, and a drop keeps the killed species until the zone has something in its pool. "
                    + "Monsters are counted as they spawn, only up to what each spawn source can hold at once, so one-off bosses stay rare in the mix. "
                    + "Clearing the pool with /zcslayerpool clear empties it. "
                    + WeaponAStampNote,
                Related = WeaponB1SlayerRelated,
            });
        }
    }
}
