using System.Collections.Generic;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Loot, part A: the drop rules (attuned / bonded / unenchantable) and the per-slot drop counts (loot_slot_<slot> and its _max).
    // Checked against Creature_Death (the slot counts block and the budget block, ApplyZoneDropSweep), ZoneLootMutator (MutateLootItem)
    // and LootGenerationFactory_ZoneSet (ZoneLootSetCounts, RollBudgetedCounts, CreateZoneLootSet, AddZoneSetArmorSlots).
    public static partial class ZoneStatHelp
    {
        private const string LootAUnsetNote =
            "Only read for a kill that resolves to a zone profile; a kill with no zone profile uses the tier default of 1 per slot at tier 11 and above and 0 below. ";

        private const string LootARuleNote =
            "Only applies to items made by the kill's loot roll (including its rare piece and slot special piece). Coin stacks are skipped. "
            + "It is stamped on the item as it drops, so items already dropped or owned are not changed. ";

        private static readonly string[] LootAWeaponRuleRelated =
        {
            ZoneStat.WeaponAttuned, ZoneStat.WeaponBonded, ZoneStat.WeaponUnenchantable,
        };

        private static readonly string[] LootAArmorRuleRelated =
        {
            ZoneStat.ArmorAttuned, ZoneStat.ArmorBonded, ZoneStat.ArmorUnenchantable,
        };

        private static readonly string[] LootASlotRelated =
        {
            ZoneStat.LootSlotWeapons, ZoneStat.LootSlotHelm, ZoneStat.LootSlotChest, ZoneStat.LootSlotShoulder, ZoneStat.LootSlotBracer,
            ZoneStat.LootSlotGlove, ZoneStat.LootSlotGirth, ZoneStat.LootSlotUpperLeg, ZoneStat.LootSlotLowerLeg, ZoneStat.LootSlotBoot,
            ZoneStat.LootSlotShield, ZoneStat.LootSlotAmulet, ZoneStat.LootSlotRing, ZoneStat.LootSlotBracelet, ZoneStat.LootSlotTrinket,
            ZoneStat.LootSlotCloak, ZoneStat.LootSlotClothing, ZoneStat.LootDropsMin, ZoneStat.LootDropsMax,
        };

        private const string LootASlotModeNote =
            "There are two modes. With no loot_drops_min set (the usual per-slot mode), the number is a count of pieces for that slot. "
            + "Once loot_drops_min is set (budget mode), the corpse gets a fixed number of items instead, and these numbers are only weights that steer which slot each item is drawn for. ";

        static partial void AddLootA(List<ZoneStatHelpEntry> e)
        {
            // ---- drop rules: weapons ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponAttuned,
                Summary = "Any non-zero value makes dropped weapons Attuned.",
                Details = "Every weapon the kill drops (melee weapons, missile launchers and casters) is flagged Attuned when this is anything other than 0. "
                    + "Any other value, including a negative one, counts as on.",
                Shape = "A switch: 0 is off, anything else is on. The size of the number does not matter.",
                WhenUnset = "Off (read as 0): weapons keep whatever attuned setting they were made with.",
                Raise = "Any non-zero value turns it on; raising it further changes nothing.",
                Lower = "Setting it to 0 stops flagging new weapons. It does not remove the flag from weapons already dropped.",
                Range = "Any number; only 0 versus non-zero matters.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootARuleNote + "Armor, clothing, jewelry and cloaks use the armor_ twin of this stat, not this one.",
                Related = LootAWeaponRuleRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponBonded,
                Summary = "Any non-zero value makes dropped weapons Bonded.",
                Details = "Every weapon the kill drops (melee weapons, missile launchers and casters) is flagged Bonded when this is anything other than 0. "
                    + "Any other value, including a negative one, counts as on.",
                Shape = "A switch: 0 is off, anything else is on. The size of the number does not matter.",
                WhenUnset = "Off (read as 0): weapons keep whatever bonded setting they were made with.",
                Raise = "Any non-zero value turns it on; raising it further changes nothing.",
                Lower = "Setting it to 0 stops flagging new weapons. It does not remove the flag from weapons already dropped.",
                Range = "Any number; only 0 versus non-zero matters.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootARuleNote + "Armor, clothing, jewelry and cloaks use the armor_ twin of this stat, not this one.",
                Related = LootAWeaponRuleRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.WeaponUnenchantable,
                Summary = "Any non-zero value makes dropped weapons unenchantable.",
                Details = "Every weapon the kill drops (melee weapons, missile launchers and casters) gets its magic resistance set to 9999 when this is anything other than 0, "
                    + "which is how the game makes an item unenchantable. Any other value, including a negative one, counts as on.",
                Shape = "A switch: 0 is off, anything else is on. The size of the number does not matter.",
                WhenUnset = "Off (read as 0): weapons keep the magic resistance they were made with.",
                Raise = "Any non-zero value turns it on; raising it further changes nothing.",
                Lower = "Setting it to 0 stops stamping new weapons. It does not change weapons already dropped.",
                Range = "Any number; only 0 versus non-zero matters.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootARuleNote + "Armor, clothing, jewelry and cloaks use the armor_ twin of this stat, not this one.",
                Related = LootAWeaponRuleRelated,
            });

            // ---- drop rules: everything that is not a weapon ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArmorAttuned,
                Summary = "Any non-zero value makes dropped armor, clothing, jewelry and cloaks Attuned.",
                Details = "Every dropped item that is not a weapon (armor, shields, clothing, jewelry, cloaks and trinkets) is flagged Attuned when this is anything other than 0. "
                    + "Any other value, including a negative one, counts as on. The name says armor, but it covers every non-weapon item.",
                Shape = "A switch: 0 is off, anything else is on. The size of the number does not matter.",
                WhenUnset = "Off (read as 0): items keep whatever attuned setting they were made with.",
                Raise = "Any non-zero value turns it on; raising it further changes nothing.",
                Lower = "Setting it to 0 stops flagging new items. It does not remove the flag from items already dropped.",
                Range = "Any number; only 0 versus non-zero matters.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootARuleNote + "Weapons use the weapon_ twin of this stat, not this one.",
                Related = LootAArmorRuleRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArmorBonded,
                Summary = "Any non-zero value makes dropped armor, clothing, jewelry and cloaks Bonded.",
                Details = "Every dropped item that is not a weapon (armor, shields, clothing, jewelry, cloaks and trinkets) is flagged Bonded when this is anything other than 0. "
                    + "Any other value, including a negative one, counts as on. The name says armor, but it covers every non-weapon item.",
                Shape = "A switch: 0 is off, anything else is on. The size of the number does not matter.",
                WhenUnset = "Off (read as 0): items keep whatever bonded setting they were made with.",
                Raise = "Any non-zero value turns it on; raising it further changes nothing.",
                Lower = "Setting it to 0 stops flagging new items. It does not remove the flag from items already dropped.",
                Range = "Any number; only 0 versus non-zero matters.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootARuleNote + "Weapons use the weapon_ twin of this stat, not this one.",
                Related = LootAArmorRuleRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArmorUnenchantable,
                Summary = "Any non-zero value makes dropped armor, clothing, jewelry and cloaks unenchantable.",
                Details = "Every dropped item that is not a weapon (armor, shields, clothing, jewelry, cloaks and trinkets) gets its magic resistance set to 9999 when this is anything other than 0, "
                    + "which is how the game makes an item unenchantable. Any other value, including a negative one, counts as on. The name says armor, but it covers every non-weapon item.",
                Shape = "A switch: 0 is off, anything else is on. The size of the number does not matter.",
                WhenUnset = "Off (read as 0): items keep the magic resistance they were made with.",
                Raise = "Any non-zero value turns it on; raising it further changes nothing.",
                Lower = "Setting it to 0 stops stamping new items. It does not change items already dropped.",
                Range = "Any number; only 0 versus non-zero matters.",
                Timing = ZoneStatTiming.Loot,
                Caution = LootARuleNote + "Weapons use the weapon_ twin of this stat, not this one.",
                Related = LootAArmorRuleRelated,
            });

            // ---- slot counts ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LootSlotWeapons,
                Summary = "How many weapons of each family drop per kill (or, in budget mode, whether weapons can drop at all).",
                Details = LootASlotModeNote
                    + "In per-slot mode this is a multiplier over the whole weapon set: 1 drops one weapon of every family the loot set has, 2 drops two of each, and so on. "
                    + "In budget mode only 0 versus above 0 matters here: above 0 lets weapons be drawn (their share against the other categories comes from the loot_weight_ stats, and which kind of weapon from the weapon group weights), and 0 or less stops weapons dropping.",
                Shape = "A whole number of weapons per family (a decimal is rounded to the nearest whole number, and exactly half rounds to the even one, so 0.5 is 0 and 2.5 is 2). "
                    + "With loot_slot_weapons_max also set, each kill rolls a whole number between the two.",
                WhenUnset = "1 at tier 11 and above, 0 below tier 11. " + LootAUnsetNote,
                Raise = "More weapons per kill in per-slot mode. In budget mode there is no further effect once it is above 0.",
                Lower = "Fewer weapons. At 0 (or below) no weapons drop from this kill.",
                Range = "Any number; below 0 acts as 0. Higher values only matter in per-slot mode.",
                Timing = ZoneStatTiming.Loot,
                Caution = "In per-slot mode this multiplies by every weapon family, so one step up adds a whole set of weapons to the corpse. "
                    + "A corpse holds at most 100 items. If the per-slot counts add up to more than that, the kill falls back to rolling 100 items drawn by these numbers as weights, and logs a warning. "
                    + "In budget mode the budget is capped at that same 100.",
                Related = LootASlotRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "loot_slot_<slot>",
                Pattern = new Regex(@"^loot_slot_(helm|chest|shoulder|bracer|glove|girth|upperleg|lowerleg|boot|shield|amulet|ring|bracelet|trinket|cloak|clothing)$",
                    RegexOptions.CultureInvariant),
                Summary = "How many pieces for that gear slot drop per kill (or, in budget mode, how often that slot is drawn).",
                Details = LootASlotModeNote
                    + "The slots are helm, chest, shoulder, bracer, glove, girth, upperleg, lowerleg, boot, shield (armor), amulet, ring, bracelet, trinket (jewelry), cloak and clothing (shirts and pants). "
                    + "In per-slot mode each one drops that many pieces for its slot. A piece that covers more than one body slot counts for the slot it was asked for only, so overlap between slots is normal. "
                    + "In budget mode, armor slots (shield included) are drawn against each other by their numbers, and jewelry slots against each other by theirs. "
                    + "Cloak and clothing have no rivals inside their group, so for them only 0 versus above 0 matters; their share of the corpse comes from their loot_weight_ stats.",
                Shape = "A whole number (a decimal is rounded to the nearest whole number, and exactly half rounds to the even one, so 0.5 is 0 and 2.5 is 2). "
                    + "With the matching _max stat also set, each kill rolls a whole number between the two, separately for each slot.",
                WhenUnset = "1 at tier 11 and above, 0 below tier 11. " + LootAUnsetNote,
                Raise = "Per-slot mode: more pieces for that slot on every corpse. Budget mode: that slot is drawn more often against the other slots in its group.",
                Lower = "Fewer pieces, or a smaller share in budget mode. At 0 (or below) that slot never drops.",
                Range = "Any number; below 0 acts as 0. A value of 0.5 or less rounds to 0 and switches the slot off.",
                Timing = ZoneStatTiming.Loot,
                Caution = "A slot at 0 is simply off: there is no separate switch. "
                    + "A zone can turn a slot on below tier 11 by giving it a count. "
                    + "A corpse holds at most 100 items. If the per-slot counts add up to more than that (weapons count as nine times their number), the kill falls back to rolling 100 items drawn by these numbers as weights, and logs a warning. "
                    + "In budget mode these numbers are weights, not counts, so raising one does not add items to the corpse.",
                Related = LootASlotRelated,
            });

            // ---- slot count ranges ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = "loot_slot_<slot>_max",
                Pattern = new Regex(@"^loot_slot_(weapons|helm|chest|shoulder|bracer|glove|girth|upperleg|lowerleg|boot|shield|amulet|ring|bracelet|trinket|cloak|clothing)_max$",
                    RegexOptions.CultureInvariant),
                Summary = "Turns the matching loot_slot_<slot> number into a range, rolled fresh for every kill.",
                Details = "When this is set, the slot's count is a whole number picked at random between the matching loot_slot_<slot> number and this one, both ends included. "
                    + "The pick is made separately for each slot on each kill (for example 1 to 2 weapons and 3 to 5 chest pieces). "
                    + "If the two are the wrong way round they are swapped, so the lower is always the minimum. Both are rounded to whole numbers first. "
                    + "The result is used the same way as a plain count: a count in per-slot mode, a weight in budget mode.",
                Shape = "A whole number (a decimal is rounded to the nearest whole number, and exactly half rounds to the even one). It is the top of the range.",
                WhenUnset = "No range: the slot uses its plain loot_slot_<slot> number as an exact value.",
                Raise = "The range reaches higher, so the slot averages more.",
                Lower = "The range narrows. At or equal to the minimum the slot is an exact count again.",
                Range = "Any number. This stat is read only if it is present.",
                Timing = ZoneStatTiming.Loot,
                Caution = "If the matching loot_slot_<slot> is not set, the minimum is the tier default (1 at tier 11 and above, 0 below), not 0. "
                    + "Setting only this stat on a tier 11 zone gives a range from 1 up to this number.",
                Related = LootASlotRelated,
            });
        }
    }
}
