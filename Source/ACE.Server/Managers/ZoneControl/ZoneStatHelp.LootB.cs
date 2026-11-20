using System.Collections.Generic;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Loot, part B: the drop budget (loot_drops_min / loot_drops_max), carried inventory, the category weights, the five
    // weapon group weights and the grade stats. Checked against Creature_Death (budget block, carried inventory gate),
    // LootGenerationFactory_ZoneSet (RollBudgetedCounts, WeaponGroupWeights, ApplyWeaponAugScaleStamp),
    // ZoneStatResolver (GradeFloorOf, RollGrade, OddsToInt) and WeaponScalingManager.RollQuality.
    public static partial class ZoneStatHelp
    {
        private const string LootBBudgetOnlyNote =
            "Only read in budget mode (when loot_drops_min is set). In the usual per-slot mode this number is ignored. ";

        private const string LootBNoProfileNote =
            "Only read for a kill that resolves to a zone profile. ";

        private static readonly string[] LootBBudgetRelated =
        {
            ZoneStat.LootDropsMin, ZoneStat.LootDropsMax,
        };

        private static readonly string[] LootBCategoryRelated =
        {
            ZoneStat.LootDropsMin, ZoneStat.LootWeightWeapon, ZoneStat.LootWeightArmor, ZoneStat.LootWeightJewelry,
            ZoneStat.LootWeightCloak, ZoneStat.LootWeightClothing, ZoneStat.LootSlotWeapons, ZoneStat.LootSlotShield,
            ZoneStat.LootSlotCloak, ZoneStat.LootSlotClothing,
        };

        private static readonly string[] LootBGroupRelated =
        {
            ZoneStat.LootDropsMin, ZoneStat.LootWeightWeapon, ZoneStat.LootSlotWeapons, ZoneStat.LootWeightMelee1H,
            ZoneStat.LootWeightMelee2H, ZoneStat.LootWeightMissile, ZoneStat.LootWeightWar, ZoneStat.LootWeightVoid,
        };

        private const string LootBGroupDetails =
            "In budget mode every weapon the corpse gets is drawn in two steps. First a weapon group is picked: one-handed melee, two-handed melee, missile, War caster or Void caster. "
            + "The chance of each group is its weight divided by the total of all five group weights, whatever the number of items on the corpse. Then a weapon family inside that group is picked. "
            + "Inside the two melee groups the families are drawn without repeating until every family has dropped once, then the list starts again. Missile and the two caster groups are one family each. ";

        private const string LootBGroupShape =
            "A relative weight, not a percent. Only the proportions between the five group weights matter. Zero, a negative number or a value that is not a normal number counts as 0.";

        private const string LootBGroupCaution =
            LootBBudgetOnlyNote
            + "A group at 0 is off. Switching the stat off with the stat toggle does not give 0: it falls back to the default, so use a value of 0 to turn a group off. "
            + "If all five groups are 0, no weapons drop and those picks go to the other categories. "
            + "A weapon pick only happens at all when the weapon category is allowed (loot_weight_weapon above 0 and loot_slot_weapons above 0).";

        static partial void AddLootB(List<ZoneStatHelpEntry> e)
        {
            // ---- the budget ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LootDropsMin,
                Summary = "Setting this turns on budget mode: the corpse gets a fixed number of items (this number, or a roll up to loot_drops_max).",
                Details = "When this stat is present on the kill's profile, the corpse no longer gets a count of pieces per slot. Instead it gets this many items in total. "
                    + "Each item is drawn by first picking a category (weapon, armor, jewelry, cloak, clothing) by the loot_weight_ stats, then a slot inside the category by the loot_slot_ numbers (which become weights). "
                    + "If loot_drops_max is also set, the total is a random whole number between the two. Having the stat present is what switches the mode, even if the number is small.",
                Shape = "A whole number of items (a decimal is rounded to the nearest whole number, and exactly half rounds to the even one). "
                    + "It is the bottom of the range, or the exact total when loot_drops_max is not set.",
                WhenUnset = "Per-slot mode: each slot drops its own count from the loot_slot_ stats, and the loot_weight_ and weapon group weights are ignored. " + LootBNoProfileNote,
                Raise = "More items on every corpse.",
                Lower = "Fewer items. At 0 or below the budget is empty and nothing is drawn from the loot set.",
                Range = "Any whole number; the total is capped at 100 items per corpse.",
                Timing = ZoneStatTiming.Loot,
                Caution = "The budget is a ceiling, not a promise. An armor piece that covers several slots can satisfy several draws at once, so a corpse can end up with fewer items. "
                    + "Pieces from the slot special roll are not counted in the budget. "
                    + "The loot_max_drops name in old notes was renamed: this is the lower end. "
                    + "If every category is off (all weights at 0, or no slot above 0 in any of them), drawing stops early and the corpse gets fewer items.",
                Related = LootBCategoryRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LootDropsMax,
                Summary = "In budget mode, the top of the item total (the corpse gets a random whole number from loot_drops_min up to this).",
                Details = "Only used together with loot_drops_min. Each kill picks the item total at random between loot_drops_min and this number. "
                    + "If the two are the wrong way round they are swapped, so the lower is always the minimum. Both are rounded to whole numbers first.",
                Shape = "A whole number of items (a decimal is rounded to the nearest whole number, and exactly half rounds to the even one). It is the top of the range.",
                WhenUnset = "No range: the total is exactly loot_drops_min.",
                Raise = "The range reaches higher, so corpses carry more items on average.",
                Lower = "The range narrows. At or equal to loot_drops_min the total is exact again.",
                Range = "Any whole number; the total is capped at 100 items per corpse.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Does nothing unless loot_drops_min is also set: on its own this stat does not turn budget mode on.",
                Related = LootBBudgetRelated,
            });

            // ---- carried inventory ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.DropCarriedInventory,
                Summary = "Above 0 lets a monster in a Zone Control level 11+ zone drop the items it carries, as well as the zone loot set.",
                Details = "Inside a zone variation of 11 or higher, a monster drops its zone loot set and, by default, nothing it carries. "
                    + "Items in the monster's create list that are carried (Contain) or marked as treasure without being wielded are skipped. "
                    + "Setting this above 0 lets those carried items drop as well. It is meant for quest monsters that hand over a quest item: set it on that one monster's per-WCID row so ordinary monsters stay suppressed. "
                    + "It does not affect gear the monster wields, which follows its own server setting.",
                Shape = "A switch: above 0 is on, 0 or below is off. The size of the number does not matter.",
                WhenUnset = "Off: carried items are not dropped by a monster inside a zone variation of 11 or higher.",
                Raise = "Any value above 0 turns it on; raising it further changes nothing.",
                Lower = "Setting it to 0 (or below) suppresses carried items again.",
                Range = "Any number; only above 0 versus not above 0 matters.",
                Timing = ZoneStatTiming.Kill,
                Caution = "Outside a zone variation of 11 or higher (a kill with no zone profile, or in a lower variation) carried items always drop, whatever this says. "
                    + "Setting it on a broad row such as a whole tier lets every monster there drop whatever junk it carries.",
                Related = new[] { ZoneStat.LootDropsMin },
            });

            // ---- category weights ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = "loot_weight_<category>",
                Pattern = new Regex(@"^loot_weight_(weapon|armor|jewelry|cloak)$", RegexOptions.CultureInvariant),
                Summary = "In budget mode, how likely the weapon, armor, jewelry or cloak category is to be drawn for each item.",
                Details = "Each item on the corpse first picks a category. The chance of a category is its weight divided by the total of the weights of the categories that are allowed. "
                    + "The categories are weapon, armor (shields count as armor), jewelry, cloak and clothing (loot_weight_clothing has its own entry). "
                    + "A category is only allowed if its weight is above 0 and the slot numbers behind it allow at least one slot: loot_slot_weapons above 0 for weapons (and at least one weapon group weight above 0), "
                    + "some armor slot above 0 for armor, some jewelry slot above 0 for jewelry, loot_slot_cloak above 0 for cloaks.",
                Shape = "A relative weight, not a percent. Only the proportions between the category weights matter.",
                WhenUnset = "1 for each category. With none set, weapon, armor, jewelry and cloak are equally likely (clothing defaults to a lower weight).",
                Raise = "That category is drawn more often against the others.",
                Lower = "That category is drawn less often. At 0 (or below) it is never drawn.",
                Range = "Any number; 0 or below means never.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootBBudgetOnlyNote + LootBNoProfileNote
                    + "Weights are shares, so raising one lowers the odds of the others without adding items; the item total comes from loot_drops_min and loot_drops_max.",
                Related = LootBCategoryRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LootWeightClothing,
                Summary = "In budget mode, how likely the clothing category (shirts and pants) is to be drawn for each item.",
                Details = "Works like the other category weights: the weight over the total of the allowed categories is the chance that an item is clothing. "
                    + "Clothing is only allowed when loot_slot_clothing is above 0 as well. Caps, cowls, shoes and gloves are drawn through the armor slots, not here.",
                Shape = "A relative weight, not a percent. Only the proportions between the category weights matter.",
                WhenUnset = "A code default of 0.5, half the weight that weapon, armor, jewelry and cloak get when unset.",
                Raise = "Clothing is drawn more often against the other categories.",
                Lower = "Clothing is drawn less often. At 0 (or below) it is never drawn.",
                Range = "Any number; 0 or below means never.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootBBudgetOnlyNote + LootBNoProfileNote
                    + "Setting it to 0 is how to remove clothing from a budget corpse.",
                Related = LootBCategoryRelated,
            });

            // ---- weapon group weights ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = "loot_weight_<group>",
                Pattern = new Regex(@"^loot_weight_(melee_1h|melee_2h|missile|war)$", RegexOptions.CultureInvariant),
                Summary = "In budget mode, how often a weapon pick is a one-handed melee (melee_1h), two-handed melee (melee_2h), missile or War caster weapon.",
                Details = LootBGroupDetails
                    + "melee_1h covers the one-handed melee families as one bucket, melee_2h the two-handed ones, missile the missile weapons, and war the casters of every element except nether.",
                Shape = LootBGroupShape,
                WhenUnset = "1 for each of these four groups. The Void group defaults lower (see loot_weight_void).",
                Raise = "That group is picked more often against the other four.",
                Lower = "That group is picked less often. At 0 it is never picked.",
                Range = "Any number; 0 or below means never.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootBGroupCaution,
                Related = LootBGroupRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LootWeightVoid,
                Summary = "In budget mode, how often a weapon pick is a Void (nether) caster weapon.",
                Details = LootBGroupDetails
                    + "This group is the nether casters only. Its default is one seventh of the default for the War group, "
                    + "because the code treats a War mage as needing one caster per element (seven) and a Void mage as needing one.",
                Shape = LootBGroupShape,
                WhenUnset = "One seventh of 1, about 0.14, so Void is drawn one seventh as often as War when both are unset.",
                Raise = "Void casters are picked more often against the other four groups.",
                Lower = "Void casters are picked less often. At 0 they never drop from budget picks.",
                Range = "Any number; 0 or below means never.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootBGroupCaution + " If you set the other groups and leave this unset, Void keeps its small default, not 0.",
                Related = LootBGroupRelated,
            });

            // ---- grade stats ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LootGradeFloor,
                Summary = "Squeezes the quality rolls of dropped loot into the top part of their range.",
                Details = "At 0 loot rolls its quality over the whole range as normal. A floor above 0 lifts the bottom of the range: each roll is squeezed into the top (1 minus the floor) share of the range, "
                    + "and the best result stays the best result. For example a floor of 0.5 means rolls land in the top half. "
                    + "It applies to the quality of dropped weapons, to the card and modifier values rolled on dropped items, and to the rare and slot special pieces of the same kill.",
                Shape = "A fraction from 0 to 0.9. Values outside that are clamped (anything below 0 acts as 0, anything above 0.9 acts as 0.9); a value that is not a normal number acts as 0.",
                WhenUnset = "0: no floor, normal quality rolls.",
                Raise = "Better minimum quality on every drop; the average rises towards the top.",
                Lower = "Wider quality spread. At 0 there is no floor.",
                Range = "0 to 0.9. Anything above 0.9 behaves as 0.9.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootBNoProfileNote
                    + "On weapon quality a floor never makes a Grade S: S stays its own rare roll (see grade_s_odds). "
                    + "It is meant for rank rows (Leader and Boss), not for the whole zone.",
                Related = new[] { ZoneStat.GradeSOdds },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.GradeSOdds,
                Summary = "Makes 1 in N dropped weapons Grade S (the perfect roll).",
                Details = "For every weapon the kill drops (melee weapons, missile launchers and casters) the game first rolls 1 in N. On a hit the weapon gets the perfect quality. "
                    + "On a miss the quality is rolled from the normal grade weights with Grade S left out, then lifted by loot_grade_floor if one is set.",
                Shape = "A 1-in-N chance: the number is the N. It is rounded to a whole number; a value below 1 (or one that is not a normal number) switches the roll off.",
                WhenUnset = "Off: the weapon quality comes from the normal grade weights table, which includes S at its own server-set rate.",
                Raise = "Grade S gets rarer (a bigger N is a longer shot).",
                Lower = "Grade S gets more common. At 1 every dropped weapon is Grade S.",
                Range = "Any number from 1 upward; below 1 acts as unset.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootBNoProfileNote
                    + "Weapons only: armor, jewelry and cloaks have no grade S roll from this stat. "
                    + "Setting a low N guarantees top-quality weapons. It is meant for Leader and Boss rank rows.",
                Related = new[] { ZoneStat.LootGradeFloor },
            });
        }
    }
}
