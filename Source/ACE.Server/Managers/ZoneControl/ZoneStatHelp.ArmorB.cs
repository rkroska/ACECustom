using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Armor and jewelry lines, part B: the jewelry Cast on Strike numbers and the item spell (cantrip) count, level and Thirst chance.
    // Checked against ZoneLootMutator (StampJewelProc, and the line stamp that calls it), LootGenerationFactory_ZoneSpells
    // (ApplyZoneSpells, ZoneSpellPool, ThirstFamily), Creature_Death (ApplyZoneDropSweep) and GearEssences (the bag Add, which reads
    // the tier's default row).
    public static partial class ZoneStatHelp
    {
        private const string ArmorBAnchorNote =
            "Set the T11 value, and optionally the T25 twin: tiers in between sit on the straight line. With no T25 twin the T11 value applies at every tier. ";

        private const string ArmorBSpellNote =
            "Item spells are rolled once when a piece drops at T11 or above, and only for pieces that have a spell list (weapons, casters, armor, jewelry, clothing and cloaks). "
            + "A piece never gets two spells of the same family, and families its base item already carries are skipped. ";

        private static readonly string[] ArmorBJewelProcRelated =
        {
            "modifier_chance_54", ZoneStat.JewelryProcRate, ZoneStat.JewelryProcRateRand, ZoneStat.JewelryProcSpellcraft,
        };

        private static readonly string[] ArmorBSpellRelated =
        {
            ZoneStat.ItemSpellCountMin, ZoneStat.ItemSpellCountMax, ZoneStat.ItemSpellLevelMin, ZoneStat.ItemSpellLevelMax, ZoneStat.ItemSpellThirstChance,
        };

        private const string ArmorBJewelProcCaution =
            "Only used when the Cast on Strike line (modifier_chance_54) lands on a jewelry piece, so it does nothing for pieces without that line. "
            + "Stamped when the line is added and never read again, so it only changes new drops and new bag adds. "
            + "A piece that already has a proc spell is left alone. "
            + "A salvage bag add reads the tier's default row, not a zone's own value. ";

        private const string ArmorBSpellCaution =
            "Rolled once at drop and never again, so changing it only affects new drops. ";

        static partial void AddArmorB(List<ZoneStatHelpEntry> e)
        {
            // ---- jewelry Cast on Strike ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.JewelryProcRate,
                Summary = "The base chance per hit that a jewelry Cast on Strike proc fires.",
                Details = "When a jewelry piece gets the Cast on Strike line, it is given a random proc spell and a chance to fire. "
                    + "The chance is this number plus a random part: the random-part stat times a random number from 0 to 1, drawn once for that piece. "
                    + "The total is then held between 0 and 1. Two pieces with the same setting can therefore differ a little.",
                Shape = "A chance from 0 to 1 (0.10 = 10 percent per hit), the starting point before the random part is added. " + ArmorBAnchorNote,
                WhenUnset = "0.10 (10 percent) before the random part is added.",
                Raise = "Every new Cast on Strike jewelry piece procs more often.",
                Lower = "New pieces proc less often. A negative base can cancel out some or all of the random part.",
                Range = "Any number, but the final chance (this plus the random part) is held between 0 and 1.",
                Timing = ZoneStatTiming.Loot,
                Caution = ArmorBJewelProcCaution,
                Related = ArmorBJewelProcRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.JewelryProcRateRand,
                Summary = "The random extra proc chance added on top of the base, drawn once per jewelry piece.",
                Details = "Each new Cast on Strike jewelry piece draws a random number from 0 to 1, multiplies it by this stat and adds the result to the base proc chance. "
                    + "So the piece's chance lands anywhere from the base up to the base plus this number, and the final chance is held between 0 and 1.",
                Shape = "A chance added on top of the base (0.05 = up to 5 percentage points). " + ArmorBAnchorNote,
                WhenUnset = "0.05 (up to 5 percentage points).",
                Raise = "New pieces vary more in how often they proc, and the average rises.",
                Lower = "New pieces are closer to the base chance. At 0 every piece gets exactly the base chance.",
                Range = "0 or more. Below 0 is read as 0.",
                Timing = ZoneStatTiming.Loot,
                Caution = ArmorBJewelProcCaution,
                Related = ArmorBJewelProcRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.JewelryProcSpellcraft,
                Summary = "The spellcraft number stamped on a jewelry piece when it gets the Cast on Strike line.",
                Details = "When a Cast on Strike line is added to jewelry, the piece's spellcraft is raised to this number (rounded to a whole number) if that is higher than what the piece already has from its own item spells. "
                    + "It is never lowered: the piece's own spell spellcraft stays as the floor.",
                Shape = "A whole number (a decimal is rounded). It only ever raises the piece's spellcraft, it does not replace a higher one. " + ArmorBAnchorNote,
                WhenUnset = "9999.",
                Raise = "New Cast on Strike pieces carry a higher spellcraft.",
                Lower = "New pieces carry less. Below what the piece's item spells already give, this stat has no effect.",
                Range = "0 up to the largest whole number the game stores. Below 0 is read as 0.",
                Timing = ZoneStatTiming.Loot,
                Caution = ArmorBJewelProcCaution
                    + "Because it can only raise the number, it cannot be used to cut a piece's spellcraft below its item spells' own.",
                Related = ArmorBJewelProcRelated,
            });

            // ---- item spells ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ItemSpellCountMin,
                Summary = "The fewest item spells (cantrips) a T11+ drop is given.",
                Details = "Each piece rolls a whole number between this stat and the item spell maximum, and gets that many spells picked at random from its slot's list. "
                    + "A rare piece does not roll: it takes the maximum instead. "
                    + ArmorBSpellNote
                    + "The roll can never be more than the number of spells the slot's list holds.",
                Shape = "A count of spells, read as a whole number (a decimal is rounded, halves round up). " + ArmorBAnchorNote,
                WhenUnset = "1.",
                Raise = "Drops carry more spells on average.",
                Lower = "Drops can carry fewer spells. At 0 a piece can come out with none.",
                Range = "0 or more, and cut down to the maximum if higher. Below 0 is read as 0.",
                Timing = ZoneStatTiming.Loot,
                Caution = ArmorBSpellCaution + "A min above the max acts as the max, so every piece gets the max.",
                Related = ArmorBSpellRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ItemSpellCountMax,
                Summary = "The most item spells (cantrips) a T11+ drop is given.",
                Details = "Each piece rolls a whole number between the item spell minimum and this stat, and gets that many spells picked at random from its slot's list. "
                    + "A rare piece takes this number as its count with no roll. "
                    + ArmorBSpellNote
                    + "The count is cut down to the number of spells the slot's list holds, and a piece whose base item already carries some of those families can end up with fewer.",
                Shape = "A count of spells, read as a whole number (a decimal is rounded, halves round up). " + ArmorBAnchorNote,
                WhenUnset = "3.",
                Raise = "Drops can carry more spells, and rare pieces always carry more.",
                Lower = "Drops carry fewer spells, and rare pieces too. At 0 no piece gets any item spells (not even Blood or Spirit Thirst).",
                Range = "0 up to the size of the slot's spell list. Below 0 is read as 0 and above the list size is cut to it.",
                Timing = ZoneStatTiming.Loot,
                Caution = ArmorBSpellCaution + "At 0 nothing is rolled, the Thirst chance included, and the piece gets no spell mana or glow either.",
                Related = ArmorBSpellRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ItemSpellLevelMin,
                Summary = "The lowest spell level an item spell can roll at (1 to 4).",
                Details = "Every item spell on a piece rolls its own level between this stat and the item spell level maximum. "
                    + "The levels run 1 Minor, 2 Major, 3 Epic, 4 Legendary. "
                    + "A rare piece does not roll: all of its spells take the maximum level.",
                Shape = "A level from 1 to 4, read as a whole number (a decimal is rounded, halves round up). " + ArmorBAnchorNote,
                WhenUnset = "4 (Legendary), so with the defaults every spell is Legendary.",
                Raise = "Spells roll higher on average.",
                Lower = "Spells can roll at weaker levels. At 1 they can be Minor.",
                Range = "1 to 4. Below 1 is read as 1, above 4 as 4, and a value above the maximum is cut to the maximum.",
                Timing = ZoneStatTiming.Loot,
                Caution = ArmorBSpellCaution + "The min is held at or below the max, so a min above the max acts as the max.",
                Related = ArmorBSpellRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ItemSpellLevelMax,
                Summary = "The highest spell level an item spell can roll at (1 to 4).",
                Details = "Every item spell on a piece rolls its own level between the item spell level minimum and this stat. "
                    + "The levels run 1 Minor, 2 Major, 3 Epic, 4 Legendary. "
                    + "A rare piece takes this level for all of its spells.",
                Shape = "A level from 1 to 4, read as a whole number (a decimal is rounded, halves round up). " + ArmorBAnchorNote,
                WhenUnset = "4 (Legendary).",
                Raise = "Spells can roll stronger, and rare pieces always carry the stronger level.",
                Lower = "Spells are capped at a weaker level. At 1 every spell is Minor.",
                Range = "1 to 4. Below 1 is read as 1 and above 4 as 4.",
                Timing = ZoneStatTiming.Loot,
                Caution = ArmorBSpellCaution + "The min is held at or below this stat, so lowering this also pulls the effective min down with it.",
                Related = ArmorBSpellRelated,
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.ItemSpellThirstChance,
                Summary = "The chance a weapon drop gets Blood Thirst or Spirit Thirst as one of its item spells.",
                Details = "Each weapon drop that can carry a Thirst spell rolls once against this chance. Melee weapons and missile launchers can win Blood Thirst; casters with an element can win Spirit Thirst; no other piece rolls it. "
                    + "A win takes one of the piece's spell slots, so the spell count does not grow. "
                    + "Thirst is not in the normal spell lists, so this is the only way it drops. "
                    + "If the base item already has the Thirst family, the roll does nothing and the piece keeps its full count of other spells. "
                    + "A rare weapon rolls it the same way as any other.",
                Shape = "A chance from 0 to 1 (0.02 = 2 percent of eligible weapons). " + ArmorBAnchorNote,
                WhenUnset = "0.02 (2 percent).",
                Raise = "More weapon drops carry Thirst.",
                Lower = "Fewer do. At 0 none do.",
                Range = "0 to 1. Below 0 is read as 0 and above 1 as 1.",
                Timing = ZoneStatTiming.Loot,
                Caution = ArmorBSpellCaution + "It is only rolled when the piece gets at least one item spell, so an item spell maximum of 0 (or a count that rolls 0) means no Thirst. "
                    + "The Thirst spell level follows the level min and max like every other item spell.",
                Related = ArmorBSpellRelated,
            });
        }
    }
}
