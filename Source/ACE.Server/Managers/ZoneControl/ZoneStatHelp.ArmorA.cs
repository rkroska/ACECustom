using System.Collections.Generic;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Armor and jewelry lines, part A: worn-gear caps, armor base values, per-line chances, and the card / line count cap and floor.
    // Checked against Creature_Equipment (GetGearCap, GetEquippedItemsRatingSumCapped, GetZoneModifierBonus), Creature_Rating (the
    // rating getters that call the cap), ZoneStatResolver (BaseArmorLevel, ArmorProtBase, ArmorProtEqualize),
    // LootGenerationFactory_ZoneSet (armor stamping and EqualizeZoneArmorResists), ZoneLootMutator (TryExtraModifier, WonT, CouldWinT,
    // PinnedT, ApplyModifierBounds, the rare branches in TrySpecialRolls), ZoneModifiers (the catalog) and GearEssences (CapAt).
    public static partial class ZoneStatHelp
    {
        private const string ArmorAGearCapNote =
            "The cap only trims the total worn on equipment. Enchantments, augmentations and enlightenment stack on top of it untouched. "
            + "A cap only trims a total above zero. "
            + "There is no cap at all while the zonecontrol_enabled setting is off, and none for a player wearing no Zone Control gear. "
            + "Monsters in a Zone Control zone always use the built-in ceiling and never this stat. ";

        private static readonly string[] ArmorAGearCapRelated = { ZoneStat.GearCapDr, ZoneStat.GearCapCdr, ZoneStat.GearCapLine };

        private static readonly string[] ArmorAArmorBaseRelated =
        {
            ZoneStat.ArmorBaseLevel, ZoneStat.ArmorProtBase, ZoneStat.ArmorProtEqualize,
        };

        private static readonly string[] ArmorACapRelated =
        {
            ZoneStat.WeaponModifierCap, ZoneStat.WeaponModifierMin, ZoneStat.ArmorModifierCap, ZoneStat.ArmorModifierMin,
        };

        private const string ArmorAAnchorNote =
            "Set the T11 value, and optionally the T25 twin: tiers in between sit on the straight line and are rounded to a whole number (halves round up). With no T25 twin the T11 value applies at every tier. "
            + "Unset means no limit (cap) or no floor (min). ";

        private static ZoneStatHelpEntry ArmorAGearCapEntry(string key, string what, string whatCode, string[] cdrExtra)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "A hard ceiling on the total " + what + " a player gets from worn equipment.",
                Details = "When the game adds up what a player wears, the equipment total for " + whatCode + " is cut down to this number if it is higher. "
                    + "A player is read through the Zone Control zone they stand in: if the zone sets this stat, that number is the cap, rounded to a whole number and never below 0. "
                    + "If the zone does not set it, the built-in ceiling applies (" + cdrExtra[0] + "). "
                    + ArmorAGearCapNote,
                Shape = "A plain rating total, as a whole number (a decimal is rounded). Compared against the worn total, not per piece.",
                WhenUnset = "The built-in ceiling applies: " + cdrExtra[0] + ".",
                Raise = "Worn gear can add up to a higher total before it is cut.",
                Lower = "Worn gear is cut off sooner. At 0 the worn gear adds nothing for this total.",
                Range = "0 or more. Below 0 is read as 0. There is no upper limit in the code.",
                Timing = ZoneStatTiming.Live,
                Caution = cdrExtra[1] + ArmorAGearCapNote + "This is read when ratings are worked out, not when gear drops, so it also applies to gear players already own.",
                Related = ArmorAGearCapRelated,
            };
        }

        private static ZoneStatHelpEntry ArmorAModifierCapEntry(string key, bool weapon)
        {
            var what = weapon ? "weapon cards" : "armor and jewelry lines";
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The most " + what + " one dropped " + (weapon ? "weapon" : "piece") + " can carry.",
                Details = "A drop first rolls every " + (weapon ? "weapon card" : "line") + " on its own chance. If more win than this number allows, "
                    + "the extras are removed at random. A winner whose chance is 1 or more is never removed, but it uses up a slot first, so the rest are trimmed to fit what is left. "
                    + "If more pinned winners than the cap, all of them still stay. "
                    + (weapon
                        ? "On a rare weapon the cap is the number of cards the rare is filled up to. "
                        : "On a rare piece, when this cap is set, the piece gets exactly this many extra lines drawn at random weighted by each line's own chance (Reinforced is added on top and the Always Rolled resist lines are handled separately). With no cap set, a rare rolls like any other piece. "
                            + "Always Rolled lines (the four resist lines) never use up a slot. ")
                    + "The same limit also stops players from adding more than the cap with bags or the forge on a piece of that tier (read from the tier's default row, not a zone's own).",
                Shape = "A count of " + what + ", read as a whole number. " + ArmorAAnchorNote,
                WhenUnset = "No cap: every card or line that wins its roll is kept.",
                Raise = "Drops can carry more " + what + ".",
                Lower = "Drops carry fewer " + what + ". At 0 nothing that rolled normally is kept" + (weapon ? "." : " (Always Rolled lines are not counted, so they still land)."),
                Range = "0 or more. Below 0 is read as 0. No upper limit in the code.",
                Timing = ZoneStatTiming.Loot,
                Caution = "The count is stamped when the item drops. Changing it only affects new drops, and it never adds or removes anything on items that already exist (the forge and bags still see the new number). "
                    + "A min above the cap is cut down to the cap. "
                    + (weapon ? "Needs the weapon cards' own chance stats to be set, or nothing wins at all." : "Needs the lines' own chance stats (modifier_chance_<N>) to be set, or nothing wins at all."),
                Related = ArmorACapRelated,
            };
        }

        private static ZoneStatHelpEntry ArmorAModifierMinEntry(string key, bool weapon)
        {
            var what = weapon ? "weapon cards" : "armor and jewelry lines";
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = "The fewest " + what + " a dropped " + (weapon ? "weapon" : "piece") + " is topped up to.",
                Details = "After the chance rolls (and the cap trim), a drop with fewer winners than this number is topped up at random from the " + (weapon ? "cards" : "lines")
                    + " that lost their roll. Only ones that could have won are used: switched on, with a chance set above 0. "
                    + "The pick is weighted by each one's own chance, so rare " + (weapon ? "cards" : "lines") + " stay rare; the count is guaranteed but which ones is still random. "
                    + "If the pool is smaller than the shortfall, everything in it is used and the drop stays short. "
                    + (weapon ? "" : "Always Rolled lines (the four resist lines) do not count toward the min. ")
                    + "On a rare drop" + (weapon ? "" : " in a zone that sets the line cap") + " the min is not used.",
                Shape = "A count of " + what + ", read as a whole number. " + ArmorAAnchorNote,
                WhenUnset = "No floor: a drop can come out with none.",
                Raise = "Fewer drops come up short, and the average drop carries more " + what + ".",
                Lower = "Drops are topped up less often. At 0 there is no floor.",
                Range = "0 or more. Below 0 is read as 0. A value above the cap is cut down to the cap.",
                Timing = ZoneStatTiming.Loot,
                Caution = "A min does not switch anything on: a " + (weapon ? "card" : "line") + " with no chance set, or switched off, is never forced in. "
                    + "Changing it only affects new drops.",
                Related = ArmorACapRelated,
            };
        }

        static partial void AddArmorA(List<ZoneStatHelpEntry> e)
        {
            // ---- worn-gear caps ----

            e.Add(ArmorAGearCapEntry(ZoneStat.GearCapDr, "Damage Resist rating",
                "Damage Resist rating", new[]
                {
                    "2500",
                    "The built-in ceiling is meant to match a maxed T25 set exactly. A lower number here makes full sets weaker for everyone standing in that zone. ",
                }));
            e.Add(ArmorAGearCapEntry(ZoneStat.GearCapCdr, "Crit Resist, Crit Damage Resist and Nether Resist ratings",
                "each of Crit Resist, Crit Damage Resist and Nether Resist (each is capped separately, not added together)", new[]
                {
                    "1500 for each one",
                    "One number caps all three totals, each on its own. ",
                }));
            e.Add(ArmorAGearCapEntry(ZoneStat.GearCapLine, "Damage, Crit Damage, Healing Boost, Max Health and attribute line",
                "Damage Rating, Crit Damage Rating, Healing Boost, worn Max Health and the six attribute lines (each its own total)", new[]
                {
                    "2500",
                    "Worn Max Health is counted in hit points, not rating points, so a low value here lowers players' maximum health directly. ",
                }));

            // ---- armor base values ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArmorBaseLevel,
                Summary = "The armor level every T11+ armor piece of a tier is built on.",
                Details = "Armor drops and re-priced armor take this number as their base armor level. It is read from the tier's default row only; a zone's own value is not used. "
                    + "It is read again whenever the piece is worked out (when equipped or on login, or at once when the ladder is applied), so changing it re-prices armor that players already own.",
                Shape = "An absolute armor level that replaces the built-in one. Rounded to a whole number.",
                WhenUnset = "The built-in ladder is used: 1100 at T11, plus 100 for every tier above it.",
                Raise = "All armor of that tier gets more armor level, including pieces already dropped.",
                Lower = "Armor of that tier gets less armor level. Protection multipliers still multiply this number, so every element drops with it.",
                Range = "Any number (rounded to a whole number). The code does not clamp it.",
                Timing = ZoneStatTiming.Live,
                Caution = "Set it on the tier's default row. While the zonecontrol_enabled setting is off, this stat is ignored and a flat fallback level is used instead. "
                    + "Only armor pieces use it, not jewelry or weapons. Unlike the two protection stats it is not stamped at drop: it follows the stat for armor already owned.",
                Related = ArmorAArmorBaseRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArmorProtBase,
                Summary = "The protection multiplier written into any element a dropped armor piece does not already have.",
                Details = "Each armor piece has a protection multiplier per damage element, and that multiplier multiplies the piece's armor level; an element with none counts as zero protection. "
                    + "When a T11+ armor piece is created, every element it lacks is given this number. A zone's own value wins; if the zone does not set it, the tier's default row is used. "
                    + "Elements the item already had keep their values at this step, but the equalize stat may then average them.",
                Shape = "A multiplier on armor level (1.0 = the armor level as it is, 0.5 = half of it).",
                WhenUnset = "1.0 is written into every missing element.",
                Raise = "New armor protects better against every element that was missing.",
                Lower = "New armor protects less against those elements. At 0 they have no protection.",
                Range = "Any number. The code does not clamp it.",
                Timing = ZoneStatTiming.Loot,
                Caution = "Stamped when the piece is made and never read again, so it only moves new drops. Armor players already own keeps its protections, even after equipping or applying the ladder. "
                    + "If the equalize stat is on (the default), all elements are then set to their average, so a piece that already had some elements ends up with a blend of those and this number.",
                Related = ArmorAArmorBaseRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ArmorProtEqualize,
                Summary = "Switch: average a dropped armor piece's protections into one value, or let each element keep its own roll.",
                Details = "When a T11+ armor piece is created, after any missing elements are filled, nonzero means every protection the piece has is replaced by the average of them. "
                    + "Zero turns that off, so each element keeps what it rolled and some pieces end up better or worse than others. "
                    + "A zone's own value wins; if the zone does not set it, the tier's default row is used.",
                Shape = "A switch: 0 is off, any other number is on.",
                WhenUnset = "On: protections are averaged.",
                Raise = "Any nonzero value is the same as on: protections are averaged to one value.",
                Lower = "At 0 each element keeps its own roll.",
                Range = "0 (off) or any nonzero number (on).",
                Timing = ZoneStatTiming.Loot,
                Caution = "Stamped when the piece is made and never read again, so it only changes new drops. Armor players already own keeps what it has.",
                Related = ArmorAArmorBaseRelated,
            });

            // ---- per-line chances ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = "modifier_chance_<N>",
                Pattern = new Regex(@"^modifier_chance_\d+$", RegexOptions.CultureInvariant),
                Summary = "The chance a dropped armor or jewelry piece carries this line (N is the line's catalog number; the page shows its name).",
                Details = "Each piece that drops gets one roll for every line it can carry, and each line uses its own chance stat. The roll wins if a random number from 0 to 1 is below the chance. "
                    + "A line can only land on the right kind of piece: Armor Level and Reinforced need armor or a shield, Life on Hit and Cast on Strike need jewelry, and the rest go on any piece the zone's slot rules allow. "
                    + "Cast on Strike does not land on a piece that already has a proc spell. "
                    + "Winning lines are then trimmed by the armor modifier cap and topped up by the armor modifier min. "
                    + "The four Always Rolled lines (Damage Resist, Crit Damage Resist, Crit Resist, Nether Resist) use the same chance, but they are never trimmed by the cap and do not count toward the min. "
                    + "A piece can end up with no lines, or with every line that wins. A zone can also switch a line off, which stops it winning whatever its chance is.",
                Shape = "A chance from 0 to 1 (0.25 = 25 percent of eligible pieces). Below 0 is read as 0 and above 1 as 1. "
                    + "At T11 this is the value; the T25 twin sets the T25 value and tiers in between sit on the line. A chance of 1 or more wins every roll and is never trimmed by the cap.",
                WhenUnset = "The line never drops on armor or jewelry in that zone. A line only rolls when its chance is set above 0.",
                Raise = "More pieces carry the line.",
                Lower = "Fewer pieces carry the line. At 0 none do.",
                Range = "0 to 1. Values outside that are clamped.",
                Timing = ZoneStatTiming.Loot,
                Caution = "The chance picks whether a line lands, not how big it is; the size comes from the line's own band. "
                    + "Changing it only affects new drops. Pieces already dropped keep their lines. "
                    + "On a rare piece the chance rolls are not used: a rare is given as many extra lines as the armor modifier cap says (when the cap is set), picked at random with each line's chance as its weight, so a line with no chance can never be picked. "
                    + "A rare armor piece always gets Reinforced, whatever its chance.",
                Related = new[] { ZoneStat.ArmorModifierCap, ZoneStat.ArmorModifierMin, ZoneStat.GearCapLine },
            });

            // ---- caps and floors ----

            e.Add(ArmorAModifierCapEntry(ZoneStat.WeaponModifierCap, true));
            e.Add(ArmorAModifierCapEntry(ZoneStat.ArmorModifierCap, false));
            e.Add(ArmorAModifierMinEntry(ZoneStat.WeaponModifierMin, true));
            e.Add(ArmorAModifierMinEntry(ZoneStat.ArmorModifierMin, false));
        }
    }
}
