using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Kill reward stats and Salvage Bag stats. Checked against Creature_Death.cs (OnDeath_GrantXP for xp_kill and lum_award,
    // InjectZoneBonusCurrency for bonus_currency, the bag roll after the loot loop), GearEssences.TryRollDrop (the bag roll),
    // BountyManager.PaysBounty and CharmAbilityRegistry.ZoneCharmLevelCap.
    public static partial class ZoneStatHelp
    {
        private const string BagGateNote =
            "Bags drop only on kills in a Zone Control zone at T11 or higher, and only while Zone Control (zonecontrol_enabled) is on. "
            + "One bag roll is made per kill, not per item. Bags never drop unless bag_odds is set. ";

        private const string BagTableNote =
            "When a kill wins the bag roll (see bag_odds), the bag weights decide which bag it is. "
            + "A bag's chance is its weight divided by the total of all the bag weights. ";

        private static ZoneStatHelpEntry BagWeightEntry(string key, string bag, string effect, bool core, string extra, string[] related)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "How often a bag that drops is a " + bag + ", compared to the other bags.",
                Details = BagTableNote + "This is the weight of the " + bag + ". Using it on an item: " + effect + extra,
                Shape = "A relative weight, not a percent. Below 0 is read as 0. A value that is not a real number is read as 0. "
                    + "If every weight is 0 no bag drops, even when bag_odds hits.",
                WhenUnset = core
                    ? "A fixed default weight of 1. The four core bags (Forgetfulness, Memory, Second Thoughts, Tempering) are equally likely until you set weights."
                    : "A fixed default weight of 0, so this bag never drops until you set it.",
                Raise = "More of the bags that drop are this one, and fewer are the others.",
                Lower = "Fewer of the bags that drop are this one. At 0 it never drops.",
                Range = "Any number from 0. Only the size compared to the other weights matters.",
                Timing = ZoneStatTiming.Loot,
                Caution = BagGateNote + "Setting one weight does not change the other weights, so a core bag keeps its default weight of 1 unless you set it.",
                Related = related,
            };
        }

        private static readonly string[] BagRelated = { ZoneStat.BagOdds };

        private const string BagUseNote =
            "Read from the tier Default of the item the bag is used on (the item's own tier), when the bag is used: a zone row does not "
            + "change it. ";

        private static readonly string[] MadnessChances =
            { ZoneStat.BagMadnessNothingChance, ZoneStat.BagMadnessOverMaxChance, ZoneStat.BagMadnessShiftChance, ZoneStat.BagMadnessRemoveChance };

        private const string MadnessWeightNote =
            "The four Madness chances (bag_madness_nothing_chance, _overmax_chance, _shift_chance, _remove_chance) are read as weights: "
            + "each outcome's chance is its value divided by the total of the four. The defaults 0.30 / 0.30 / 0.30 / 0.10 add up to 1, "
            + "so they read as plain chances. ";

        private const string MadnessListNote =
            "Madness only touches Max Health, Damage Rating, Crit Damage Rating, Healing Boost, All Attributes and Armor Level, and on "
            + "weapons Biting Strike, Crushing Blow, Rending and Slayer. A locked property is skipped by every outcome, and the item is "
            + "Tainted every time (no bag works on it again). ";

        private static ZoneStatHelpEntry MadnessChanceEntry(string key, string outcome, string unset, string raise)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "How likely a Bag of Madness is to " + outcome + ".",
                Details = "A Bag of Madness has four outcomes: nothing changes; one property is set past its maximum; every property "
                    + "shifts up or down; one property is removed. This is the weight of: " + outcome + ". " + MadnessWeightNote
                    + MadnessListNote + BagUseNote,
                Shape = "A relative weight: this outcome's chance is its weight divided by the four weights' total. The defaults "
                    + "(0.3, 0.3, 0.3, 0.1) add up to 1, so each reads as its percent; change one and every share moves (with the others at "
                    + "their defaults, a weight of 0.6 on \"nothing changes\" makes it 0.6 / 1.3 = about 46%; 0.6 on the removal makes it "
                    + "0.6 / 1.5 = 40%).",
                WhenUnset = unset,
                Raise = raise,
                Lower = "This outcome happens less often. At 0 it never happens.",
                Range = "Any number from 0. Below 0 is read as 0. A value that is not a real number is read as the default. "
                    + "If all four are 0, the defaults (30 / 30 / 30 / 10%) are used, so zero weights can't switch the bag off: set "
                    + "bag_weight_madness to 0 to stop it dropping.",
                Timing = ZoneStatTiming.Use,
                Caution = "Changing one weight changes the real chance of all four, because they are shares of their total.",
                Related = MadnessChances,
            };
        }

        private static readonly string[] TemperingRelated =
            { ZoneStat.BagWeightTempering, ZoneStat.BagTemperingUpChance, ZoneStat.BagTemperingDownChance, ZoneStat.BagTemperingMaxSubgrade };

        static partial void AddRewardsBags(List<ZoneStatHelpEntry> e)
        {
            // ---- Kill rewards ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.XpKill,
                Summary = "The kill XP a monster is worth in this zone.",
                Details = "When a monster dies, this number replaces the experience the monster would normally pay. It is the whole pot for the kill: "
                    + "each player who damaged it (their pets count for them) gets the pot times their share of the damage. "
                    + "After that the player's own XP bonuses are applied on top (the server XP modifier, their enchantments, quest and enlightenment bonuses). "
                    + "A kill that pays no XP and no luminance also counts for nothing toward bounties and tier reward coins.",
                Shape = "An absolute amount of XP per kill, rounded to a whole number. It replaces the monster's own value, it is not a multiplier. "
                    + "Doubling the number doubles the XP every player earns from that monster before their bonuses, so income from it goes up x2.",
                WhenUnset = "The monster's own kill XP is paid, as in any other zone.",
                Raise = "Each kill pays more XP. The change is linear: x2 here is x2 XP income from these kills.",
                Lower = "Each kill pays less XP. At 0 (or below) the kill pays no XP.",
                Range = "Any whole number from 0. There is no upper clamp, so a very large value pays a very large amount.",
                Timing = ZoneStatTiming.Kill,
                Caution = "A value of 0 turns kill XP off for that monster; it does not fall back to the monster's own value. "
                    + "If the server setting zc_tier_dip_xp_enabled is on, each player's share is also cut or raised by the tier dip for kills at a tier other than their own. "
                    + "Set it per rank: the rank rows (Leader, Boss and so on) read their own xp_kill.",
                Related = new[] { ZoneStat.LumAward, ZoneStat.BonusCurrency },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LumAward,
                Summary = "The kill luminance a monster is worth in this zone.",
                Details = "When a monster dies, this number replaces the luminance it would normally pay. Like xp_kill it is the whole pot: "
                    + "each player who damaged it gets the pot times their share of the damage, and then the server luminance modifier and the player's own bonuses apply. "
                    + "The luminance paid for kills is recorded in the economy ledger as coming from the killed creature. "
                    + "A kill that pays no XP and no luminance also counts for nothing toward bounties and tier reward coins.",
                Shape = "An absolute amount of luminance per kill, rounded to a whole number. It replaces the monster's own value, it is not a multiplier. "
                    + "Doubling the number doubles the luminance every player earns from that monster before their bonuses, so luminance income from it goes up x2.",
                WhenUnset = "The monster's own luminance award is paid, as in any other zone.",
                Raise = "Each kill pays more luminance. The change is linear: x2 here is x2 luminance income from these kills.",
                Lower = "Each kill pays less luminance. At 0 (or below) the kill pays no luminance.",
                Range = "Any whole number from 0. There is no upper clamp.",
                Timing = ZoneStatTiming.Kill,
                Caution = "A value of 0 means no luminance at all for that monster; it does not fall back to the monster's own value. "
                    + "A clone zone pays only a share of this award, set by the clone zone reward multiplier. "
                    + "If the server setting zc_tier_dip_xp_enabled is on, each player's share is also cut or raised by the tier dip. "
                    + "Luminance is a currency: doubling this stat doubles a source in the economy ledger, so watch the supply.",
                Related = new[] { ZoneStat.XpKill, ZoneStat.BonusCurrency },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BonusCurrency,
                Summary = "How many bonus currency tokens each kill adds to the corpse.",
                Details = "On every kill in the zone this many of the server's bonus token item are added to the corpse's loot, on top of whatever else it drops. "
                    + "It is not tied to the loot table, so it drops even from a monster with no table. "
                    + "The token item is the one named by the server setting zonescale_bonus_currency_wcid. "
                    + "An amount larger than one stack of the token is made as several full stacks. "
                    + "This is separate from the zone's own currency drop list, which has its own items, amounts and chances.",
                Shape = "A token count per kill, rounded to a whole number. Not a multiplier and not a chance: every kill adds the full amount. "
                    + "Doubling it doubles the tokens each kill adds, so token income goes up x2.",
                WhenUnset = "No bonus tokens are added by this stat.",
                Raise = "Each corpse carries more tokens. The change is linear.",
                Lower = "Each corpse carries fewer tokens. At 0 or below none are added.",
                Range = "Any whole number from 1. There is no upper clamp, but one kill cannot make more than 64 stacks of the token.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Does nothing while the server setting zonescale_bonus_currency_wcid is 0 (0 means no token is injected). "
                    + "The tokens are loot on the corpse, so they go to whoever loots it. "
                    + "If the amount needs more than 64 stacks the rest is not made and the server logs a warning.",
                Related = new[] { ZoneStat.XpKill, ZoneStat.LumAward },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CharmLevelCap,
                Summary = "The highest level a levelled charm works at in this tier or zone.",
                Details = "A levelled charm (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker, Shield Arc) works at its own level, "
                    + "but never above this cap where the player is standing. A charm above the cap works as if it were at the cap. "
                    + "The charm item and its level are not changed, so it counts at full level again in a tier or zone with a higher cap. "
                    + "Levelled charms only work at T11 and higher. The cap is read from the zone the player stands in (the zone's own row over the tier default); "
                    + "at a T11+ spot no zone covers, it is read from the tier's default row.",
                Shape = "A charm level. Fractions round down. Below 1 is read as 1 (0 does NOT mean no cap). 100 or more means no cap. "
                    + "A value that is not a real number means no cap.",
                WhenUnset = "No cap: a levelled charm works at its own level.",
                Raise = "Higher level charms work at more of their strength. At 100 or more there is no cap.",
                Lower = "Charms above the cap are cut down to it. At 1 (or 0) every levelled charm works at level 1.",
                Range = "1 to 99 sets a cap. 100 or more removes it.",
                Timing = ZoneStatTiming.Live,
                Caution = "Setting 0 gives the strictest cap (level 1), not no cap. To remove a cap, clear the stat or use 100 or more. "
                    + "It is normally set on the tier default rows, for example one value per tier. It only limits charms; it does not touch any reward.",
                Related = new[] { ZoneStat.XpKill },
            });

            // ---- Prestige Coin falloff (TierRewardScale.FalloffFor / Falloff) ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.PcoinFalloffKillsPerHour,
                Summary = "How many kills an hour pay full Prestige Coins here before the falloff starts.",
                Details = "Prestige Coins pay in full up to this pace. Every kill past it pays only pcoin_falloff_mult of its coins. "
                    + "The pace is counted per character across every zone, and it drains over time, so a steady pace at or under this number always pays in full. "
                    + "A kill below the player's own tier counts as its tier-dip credit, not as a whole kill. "
                    + "It is read from the zone's Default row (the zone's own value over the tier Default), not from rank or WCID rows.",
                Shape = "Kills an hour. The reserve (how far ahead of the pace a character may get before the cut starts) is a server setting only, "
                    + "kept the same in minutes in every zone: a zone with a faster pace holds more kills in its reserve.",
                WhenUnset = "The server setting zc_pcoin_falloff_kills_per_hour (375 unless changed).",
                Raise = "Players can kill faster before their coins are cut.",
                Lower = "The cut starts at a slower pace. At 0 there is no falloff in this zone: every kill pays in full.",
                Range = "0 to 1,000,000. Outside that it is held to the range; a value that is not a number uses the server setting.",
                Timing = ZoneStatTiming.Kill,
                Caution = "0 turns the falloff OFF here, it does not stop coins. Only Prestige Coins are affected.",
                Related = new[] { ZoneStat.PcoinFalloffMult },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.PcoinFalloffMult,
                Summary = "The share of its Prestige Coins a kill pays once the player is past the falloff pace.",
                Details = "Once a character is ahead of pcoin_falloff_kills_per_hour, each extra kill pays this share of its normal coins. "
                    + "Example at 0.5: 500 kills in an hour against a pace of 375 pays 375 + 125 x 0.5 = 437.5 kills' worth. "
                    + "It is read from the zone's Default row (the zone's own value over the tier Default), not from rank or WCID rows.",
                Shape = "A share from 0 to 1: 0.5 = half, 1 = no cut, 0 = nothing past the pace.",
                WhenUnset = "The server setting zc_pcoin_falloff_mult (0.5 unless changed).",
                Raise = "Kills past the pace pay more. At 1 they pay in full.",
                Lower = "Kills past the pace pay less. At 0 they pay no Prestige Coins.",
                Range = "0 to 1. Outside that it is held to the range; a value that is not a number uses the server setting.",
                Timing = ZoneStatTiming.Kill,
                Caution = "It has no effect where the pace is 0 (falloff off).",
                Related = new[] { ZoneStat.PcoinFalloffKillsPerHour },
            });

            // ---- Item bounties (ItemBounties.TryRollDrop) ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BountyOdds,
                Summary = "The chance a kill drops an item bounty: 1 in this many.",
                Details = "On each kill a single roll is made: 1 in this number. If it hits, the kill tier's pool picks which bounty drops, by drop weight "
                    + "(each bounty's own weight in its own tiers, changed per tier in the portal under Bounty Items > Drop mix). "
                    + "It is read from the killed monster's rank row, so Leader and Boss kills can have their own odds. "
                    + "A kill that pays no XP and no luminance drops no bounty.",
                Shape = "A denominator: 1000 means 1 kill in 1000. It is rounded to a whole number. Below 1 or not a real number is read as off.",
                WhenUnset = "Off. Item bounties never drop here.",
                Raise = "Bounties drop less often. Doubling the number halves the number of bounties.",
                Lower = "Bounties drop more often. Halving the number doubles them.",
                Range = "Any whole number from 1. Below 1 turns drops off.",
                Timing = ZoneStatTiming.Loot,
                Caution = "A LARGER number gives FEWER bounties. Drops also need the server setting item_bounty_enabled on. "
                    + "If the tier's pool is empty, a winning roll drops nothing. The Valley of Death and Thaelaryn Island (below T11) use their region's odds instead, set in the portal under Bounty Items > Drop chance.",
                Related = new[] { ZoneStat.BagOdds },
            });

            // ---- Salvage bags ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BagOdds,
                Summary = "The chance a kill drops a Salvage Bag: 1 in this many.",
                Details = "On each kill a single roll is made: 1 in this number. If it hits, one bag is added to the corpse and the bag weights choose which one. "
                    + "A bag is an item a player uses on a T11+ item in their pack to change its properties. "
                    + "It is read from the killed monster's rank row, so Leader and Boss kills can have their own odds. "
                    + "A Bag of Vengeance remembers the type of monster that dropped it.",
                Shape = "A denominator: 100 means 1 kill in 100. It is rounded to a whole number. Below 1 or not a real number is read as off.",
                WhenUnset = "Off. Bags never drop.",
                Raise = "Bags drop less often. Doubling the number halves the number of bags.",
                Lower = "Bags drop more often. At 1 every kill drops a bag, and bag income goes up x2 each time you halve the number.",
                Range = "Any whole number from 1. Below 1 turns bags off.",
                Timing = ZoneStatTiming.Loot,
                Caution = BagGateNote + "This is the opposite of a multiplier: a LARGER number gives FEWER bags. A bag that cannot be made because its item is missing from the world database is logged and lost.",
                Related = new[] { ZoneStat.BagWeightForgetfulness, ZoneStat.BagWeightMemory, ZoneStat.BagWeightSecondThoughts, ZoneStat.BagWeightTempering },
            });

            e.Add(BagWeightEntry(ZoneStat.BagWeightForgetfulness, "Bag of Forgetfulness",
                "one random property is removed. It never takes an item's last property, and protected or locked lines stay.", true, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightMemory, "Bag of Memory",
                "one random property is added, picked by each line's own loot chance.", true, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightSecondThoughts, "Bag of Second Thoughts",
                "the item's LOWEST property is rerolled at the tier's normal odds. It can go up or down. A locked property is never picked; when several are equally low, the first one listed is rerolled.", true, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightTempering, "Bag of Tempering",
                "the Weapon Grade moves one sub-grade (F- to F to F+ to D- and so on): 60% up, 30% no change, 10% down by default "
                + "(see bag_tempering_up_chance, bag_tempering_down_chance). It never goes above the top set by bag_tempering_max_subgrade (A+ by default) and "
                + "never reaches S; an S weapon, or one already at the top, is refused and the bag is not used.", true, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightFortune, "Bag of Fortune",
                "one random property is rerolled, and if the new roll is no better the old one stays.", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightRebirth, "Bag of Rebirth",
                "every property is rerolled (a locked one stays). Each can go up or down.", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightLocking, "Bag of Locking",
                "one random property is locked so that other bags will not change it.", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightVengeance, "Bag of Vengeance",
                "a weapon's Slayer becomes the type of monster that dropped the bag.", false,
                " The bag can only be rolled when the killed monster has a known creature type; on a kill where it does not, this weight counts as 0 for that roll.",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightMadness, "Bag of Madness",
                "30% nothing changes, 30% one random property is set past its maximum (by 10% to 25% of its range), 30% every property "
                + "moves up to 15% of its range up or down from where it is (and may pass its maximum), 10% one random property is "
                + "removed. It only touches the Madness list and never a locked property, and the item is Tainted every time: no bag "
                + "works on it again. The chances and ranges are the bag_madness_* stats.", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightTransmutation, "Bag of Transmutation",
                "a weapon's damage type changes to a random new one of the eight, and any rending, Cast on Strike and damage word in its name change to match.", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightEmptiness, "Bag of Emptiness",
                "every property that bags can change is removed.", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightExchange, "Bag of Exchange",
                "one random property is removed and another is added.", false, "",
                BagRelated));
            // ---- Bag of Madness (2026-11-26 redesign) ----

            e.Add(MadnessChanceEntry(ZoneStat.BagMadnessNothingChance, "change nothing (the item is still Tainted)",
                "0.30.", "More bags are used up for nothing but the Taint."));
            e.Add(MadnessChanceEntry(ZoneStat.BagMadnessOverMaxChance,
                "set one random property past its maximum (see bag_madness_overmax_min and _max)",
                "0.30.", "More items get one property past its maximum, whatever it was before - even on a junk item."));
            e.Add(MadnessChanceEntry(ZoneStat.BagMadnessShiftChance,
                "shift every property up or down from where it is (see bag_madness_shift)",
                "0.30.", "More items gamble every property at once. A polished item is what can pass its maximum this way."));
            e.Add(MadnessChanceEntry(ZoneStat.BagMadnessRemoveChance, "remove one random property",
                "0.10.", "More items lose a property. A Bandit Hilt keeps Biting Strike and Crushing Blow; if nothing else can go, nothing is removed."));

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BagMadnessOverMaxMin,
                Summary = "How far past its maximum a Bag of Madness sets a property, at the least: a share of the property's range.",
                Details = "In the over-max outcome one random Madness property is SET to its maximum plus a random amount between this and "
                    + "bag_madness_overmax_max, counted in its range (the band's max minus its min). 0.10 on a 14-69 line is about +5.5 past "
                    + "69. The item stores this as a grade above 1000 (1100 = max + 10%), so a later change to the band moves it too. "
                    + "Weapon cards still stop at their hard limits, and the over-max part of Biting Strike stops at 62.5% crit chance. " + BagUseNote,
                Shape = "A share of the range: 0.1 = 10%.",
                WhenUnset = "0.10 (10%).",
                Raise = "Over-max results land further past the maximum.",
                Lower = "Over-max results can land closer to the maximum. At 0 one can land exactly on it.",
                Range = "0 to 0.25. Above 0.25 is read as 0.25 (the highest any property can go: max + 25%). If it is above "
                    + "bag_madness_overmax_max the two are swapped.",
                Timing = ZoneStatTiming.Use,
                Caution = "Items already over max keep what they rolled.",
                Related = new[] { ZoneStat.BagMadnessOverMaxMax, ZoneStat.BagMadnessOverMaxChance },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BagMadnessOverMaxMax,
                Summary = "How far past its maximum a Bag of Madness sets a property, at the most: a share of the property's range.",
                Details = "The top of the over-max outcome's range (see bag_madness_overmax_min). 0.25 is grade 1250, the highest grade "
                    + "any property can hold. " + BagUseNote,
                Shape = "A share of the range: 0.25 = 25%.",
                WhenUnset = "0.25 (25%).",
                Raise = "Over-max results can land further past the maximum, up to 0.25.",
                Lower = "Over-max results stay closer to the maximum.",
                Range = "0 to 0.25. Above 0.25 is read as 0.25. If it is set below bag_madness_overmax_min, the two are swapped, so "
                    + "to bring the ceiling below the current minimum, lower both.",
                Timing = ZoneStatTiming.Use,
                Caution = "The shift outcome is limited by the same 25% ceiling.",
                Related = new[] { ZoneStat.BagMadnessOverMaxMin, ZoneStat.BagMadnessOverMaxChance },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BagMadnessShift,
                Summary = "How far a Bag of Madness shift moves each property, up or down: a share of the property's range.",
                Details = "In the shift outcome every Madness property moves from where it is now by a random amount between minus and plus "
                    + "this share of its range, each property on its own roll. A property at 50% of its range lands between 35% and 65% "
                    + "with 0.15; one at its maximum can land up to 15% past it. Nothing goes below the band's minimum or past max + 25%. "
                    + BagUseNote,
                Shape = "A share of the range: 0.15 = 15% either way.",
                WhenUnset = "0.15 (15%).",
                Raise = "Shifts swing further both ways: bigger wins past the maximum and bigger losses.",
                Lower = "Shifts stay closer to where the item was. At 0 the shift outcome changes nothing.",
                Range = "0 to 1. Above 1 is read as 1.",
                Timing = ZoneStatTiming.Use,
                Caution = "The shift starts from the item's current values, so only a property already near its maximum can pass it.",
                Related = new[] { ZoneStat.BagMadnessShiftChance },
            });

            // ---- Bag of Tempering (2026-11-26) ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BagTemperingUpChance,
                Summary = "The chance a Bag of Tempering raises a weapon's grade one sub-grade.",
                Details = "A Bag of Tempering moves a weapon's Weapon Grade one sub-grade at a time (F- F F+ D- D D+ C- C C+ B- B B+ A- A A+). "
                    + "Each use rolls once: this chance goes up one, bag_tempering_down_chance goes down one, and the rest changes nothing. "
                    + "A step lands on the middle of the new sub-grade, the point the weapon ladder prices it at. " + BagUseNote,
                Shape = "A chance from 0 to 1: 0.6 means 60% of uses go up.",
                WhenUnset = "0.60 (60%).",
                Raise = "More uses go up, so a weapon climbs with fewer bags. With the defaults F to A takes about 21 bags (median).",
                Lower = "Fewer uses go up. At 0 the bag never raises a grade.",
                Range = "0 to 1. Below 0 is read as 0, above 1 as 1. A value that is not a real number is read as the default.",
                Timing = ZoneStatTiming.Use,
                Caution = "Up and down together never pass 1: bag_tempering_down_chance is cut to 1 minus this chance when they would. "
                    + "The grade never goes above bag_tempering_max_subgrade, and S is never reached.",
                Related = TemperingRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BagTemperingDownChance,
                Summary = "The chance a Bag of Tempering lowers a weapon's grade one sub-grade.",
                Details = "The bad outcome of a Bag of Tempering: the Weapon Grade drops one sub-grade, landing on the middle of the lower one. "
                    + "Never below F- (a weapon at F- that rolls down keeps its grade). " + BagUseNote,
                Shape = "A chance from 0 to 1: 0.1 means 10% of uses go down.",
                WhenUnset = "0.10 (10%).",
                Raise = "More uses go down, so climbing takes more bags.",
                Lower = "Fewer uses go down. At 0 a Bag of Tempering never lowers a grade.",
                Range = "0 to 1 minus bag_tempering_up_chance. A larger value is cut to that. A value that is not a real number is read as the default.",
                Timing = ZoneStatTiming.Use,
                Caution = "The chance a use rolls no change is what is left: 1 minus the up chance minus this one (30% with the defaults). "
                    + "At F- a down roll changes nothing too, so there 40% of uses leave the grade as it is.",
                Related = TemperingRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BagTemperingMaxSubgrade,
                Summary = "The highest Weapon Grade a Bag of Tempering can raise a weapon to.",
                Details = "A sub-grade, counted up from the bottom: 0 = F-, 1 = F, 2 = F+, 3 = D-, 4 = D, 5 = D+, 6 = C-, 7 = C, 8 = C+, 9 = B-, "
                    + "10 = B, 11 = B+, 12 = A-, 13 = A, 14 = A+. A weapon at or above it is refused (it could only stay or go down) and "
                    + "the bag is not used. S is never reached by a bag: it stays a drop-only chase. " + BagUseNote,
                Shape = "A whole number from 0 to 14, a sub-grade on the list above. It is rounded.",
                WhenUnset = "14 (A+).",
                Raise = "Bags can push weapons higher, up to A+ at 14.",
                Lower = "Bags stop at a lower grade, and weapons already above it are refused.",
                Range = "0 to 14. Below 0 is read as 0 (F-), above 14 as 14 (A+). A value that is not a real number is read as the default.",
                Timing = ZoneStatTiming.Use,
                Caution = "Lowering it never lowers a weapon: a weapon already above it keeps its grade, it just takes no more Bags of Tempering.",
                Related = TemperingRelated,
            });

            e.Add(BagWeightEntry(ZoneStat.BagWeightGenesis, "Bag of Genesis",
                "one random property is added, picked by each line's own loot chance. It only works on an item with no properties (the item's \"Properties: N of cap\" line reads 0).", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightAugmentation, "Bag of Augmentation",
                "one random property is added, picked by each line's own loot chance. It only works on an item with exactly one property.", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightAlteration, "Bag of Alteration",
                "the properties a bag can change are removed and ONE new random property is added. It only works on an item with one or two properties. A locked property, a Slayer and built-in ones such as Reinforced or Split Arrows stay.", false, "",
                BagRelated));
            e.Add(BagWeightEntry(ZoneStat.BagWeightReforging, "Bag of Reforging",
                "every property a bag can change is replaced with a new random one and the item keeps how many it has (an item above its tier's limit comes down to it). A locked property, a Slayer and built-in ones such as Reinforced or Split Arrows stay.", false, "",
                BagRelated));
        }
    }
}
