using System.Collections.Generic;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Gear specials and the T11+ cloak procs.
    // Checked against Creature_Death (the per-kill special roll), Player_Combat (ZcSpecialKnob, ZcTryCheatDeath, ZcTryBattleMend,
    // ZcTryPctHpDamage, ZcTryLifeOnHit), Creature_Equipment (GetZoneModifierMax / GetZoneModifierBonus), ZoneControlManager
    // (ResolveZoneDefaultForCreature, ResolveZoneDefaultForPlayer) and ZcCloakProcs (DialsFor, Dials.Clamped, PreHit, PostHit).
    public static partial class ZoneStatHelp
    {
        private const string SpecialsMonsterZoneNote =
            "This number follows the MONSTER's zone, not the player's. "
            + "The game finds the zone from the monster in the fight (the one hitting the player, or the one the player hit) and reads this stat from that zone's default row only. "
            + "A per-monster override never changes it. With no monster in the exchange (falling, zone damage over time) or no zone on the monster, the built-in number is used. ";

        private const string SpecialsGearNote =
            "It only matters to a player who wears the matching special. The special itself goes quiet where the armor zone lock suppresses worn lines. ";

        private const string SpecialsCloakNote =
            "This number follows the WEARER's position, not the monster's. "
            + "It only applies where the wearer stands in a T11 or higher spot. Retail spots below that always use the server setting. "
            + "There the zone's default row is used (its own stats laid over the tier default). On a spot no enabled zone covers, the tier's default row is used. "
            + "Each cloak stat is picked on its own: a zone that sets only one keeps the server setting for the others. A value that is not a number counts as unset. "
            + "Only T11+ cloaks use these stats, and only while the zc_cloak_procs_enabled and zonecontrol_enabled settings are both on. "
            + "While Zone Control is off, only the server settings count. ";

        private static readonly string[] SpecialsCloakRelated =
        {
            ZoneStat.CloakProcChance, ZoneStat.CloakProcCooldown, ZoneStat.CloakBreezeHeal, ZoneStat.CloakDodgerHits,
            ZoneStat.CloakDodgerWindow, ZoneStat.CloakDodgerStaminaMult, ZoneStat.CloakGraceSeconds,
        };

        static partial void AddSpecials(List<ZoneStatHelpEntry> e)
        {
            // ---- per-kill special drop ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.SpecialOdds,
                Summary = "Odds against a kill also producing a gear special: 1 in this number.",
                Details = "Every kill in zone loot makes one roll: a random whole number from 1 to this value, and a 1 wins. "
                    + "On a win, one of the enabled slot specials is picked at random and put on the dropped piece for its slot (the game makes one if the kill dropped none). "
                    + "That piece is a perfect piece: every line it rolls comes out at the top of its band. "
                    + "A special switched off for the zone is left out of the pick, and the odds do not change: the rest share the win. "
                    + "The number is read from the killed monster's own Zone Control stats, so each rank row can carry its own odds. "
                    + "The roll is one per kill, not one per item. The odds are rounded to a whole number.",
                Shape = "The bottom of a 1-in-N fraction. The number is rounded to a whole number, so 750000 is 1 in 750000. Higher means rarer.",
                WhenUnset = "The built-in odds of 1 in 750000 apply.",
                Raise = "Specials drop less often.",
                Lower = "Specials drop more often. At 1 every kill makes one.",
                Range = "Any number. 1 or lower (or a value that is not a number) means every kill wins. A very large value is held just under the largest whole number the game can hold.",
                Timing = ZoneStatTiming.Loot,
                Caution = "No special is ever rolled while the zonecontrol_enabled setting is off, or on a kill below the lowest Zone Control gear tier. "
                    + "A small number floods the zone with perfect pieces. "
                    + "A special that wins but finds no piece for its slot is logged and dropped. It only changes new kills.",
                Related = new string[0],
            });

            // ---- worn specials: knobs read in combat ----

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BattleMendThreshold,
                Summary = "Battle Mending fires when the wearer is below this fraction of max health.",
                Details = "After a hit lands and the wearer is still alive, Battle Mending checks their health. "
                    + "If it is below this fraction of max health and the special is off cooldown, the wearer is healed to full. "
                    + SpecialsMonsterZoneNote + SpecialsGearNote,
                Shape = "A fraction of max health from 0 to 1 (0.25 = 25 percent). Compared as: health below the fraction times max health.",
                WhenUnset = "The built-in fraction of 0.25 applies.",
                Raise = "Battle Mending fires earlier, while the wearer is healthier.",
                Lower = "Battle Mending waits until the wearer is closer to death. At 0 it never fires.",
                Range = "Any number. The code does not clamp it. 0 or below never fires, and 1 or above fires on any hit that leaves the wearer below full health.",
                Timing = ZoneStatTiming.Live,
                Caution = "Follows the monster's zone, not the player's, so the same gear acts differently against monsters from different zones. "
                    + "Battle Mending heals only after the hit, so a hit that would kill outright is not caught by it (Cheat Death is the one for that).",
                Related = new[] { ZoneStat.BattleMendCooldown, ZoneStat.CheatDeathCooldown },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.BattleMendCooldown,
                Summary = "Seconds a wearer waits before Battle Mending can fire again.",
                Details = "When Battle Mending heals someone, the wait starts at once and is kept per character. "
                    + "The number is read at the moment it fires. "
                    + SpecialsMonsterZoneNote + SpecialsGearNote,
                Shape = "A time in seconds. Used as given, with no rounding.",
                WhenUnset = "The built-in wait of 60 seconds applies.",
                Raise = "Battle Mending can save the wearer less often.",
                Lower = "Battle Mending can fire more often. At 0 or below there is no wait.",
                Range = "Any number of seconds. The code does not clamp it.",
                Timing = ZoneStatTiming.Live,
                Caution = "Follows the monster's zone, not the player's: the monster that landed the hit that triggered the heal decides the wait. "
                    + "With a low value and a high threshold it can keep a wearer topped up in a long fight.",
                Related = new[] { ZoneStat.BattleMendThreshold },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.PctHpCooldown,
                Summary = "Seconds a wearer waits between Percent HP Damage bonus hits.",
                Details = "The Percent HP special adds a flat chunk of the monster's max health to a landed hit, only while the monster is above that percent of its health, so it can never land the kill. "
                    + "This stat sets the per-character wait after one fires. The wait starts only when a bonus is actually added. "
                    + "The special does nothing against a monster with no Zone Control profile or one marked immune. "
                    + SpecialsMonsterZoneNote + SpecialsGearNote,
                Shape = "A time in seconds. Used as given, with no rounding.",
                WhenUnset = "The built-in wait of 2 seconds applies.",
                Raise = "The bonus hit lands less often.",
                Lower = "The bonus hit lands more often. At 0 or below there is no wait.",
                Range = "Any number of seconds. The code does not clamp it.",
                Timing = ZoneStatTiming.Live,
                Caution = "Follows the monster being hit, not the player's own zone. "
                    + "The bonus is a share of the monster's max health, so a short wait against a high-health monster adds a lot of damage.",
                Related = new string[0],
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CheatDeathCooldown,
                Summary = "Seconds a wearer waits before Cheat Death can save them again.",
                Details = "When a hit would kill a Cheat Death wearer, the damage is cut so they land on exactly 1 health, an immunity window opens, and this wait starts. "
                    + "The wait is per character and starts when the save fires. "
                    + SpecialsMonsterZoneNote + SpecialsGearNote,
                Shape = "A time in seconds. Used as given, with no rounding. It is counted from the moment the save fires, and includes the immunity time.",
                WhenUnset = "The built-in wait of 600 seconds applies.",
                Raise = "Cheat Death saves less often.",
                Lower = "Cheat Death saves more often. At 0 or below it is ready again at once.",
                Range = "Any number of seconds. The code does not clamp it.",
                Timing = ZoneStatTiming.Live,
                Caution = "Follows the monster's zone, not the player's. A hit with no monster behind it (falling, zone damage over time) uses the built-in number, whatever the zone says. "
                    + "The wait starts when the save fires, so a cooldown shorter than the immunity window leaves the wearer ready again before it ends.",
                Related = new[] { ZoneStat.CheatDeathImmunity },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.CheatDeathImmunity,
                Summary = "Seconds a Cheat Death save makes the wearer immune to all damage.",
                Details = "When Cheat Death fires, the wearer is immune to all damage for this long. Each absorbed hit is announced to them. "
                    + "The wearer is told when the window opens and when it closes. "
                    + SpecialsMonsterZoneNote + SpecialsGearNote,
                Shape = "A time in seconds. Used as given, with no rounding. The chat messages show it as a whole number.",
                WhenUnset = "The built-in window of 5 seconds applies.",
                Raise = "The wearer gets a longer break from damage after the save.",
                Lower = "A shorter break. At 0 or below the wearer is left on 1 health with no protection.",
                Range = "Any number of seconds. The code does not clamp it.",
                Timing = ZoneStatTiming.Live,
                Caution = "Follows the monster's zone, not the player's. Immunity covers every kind of damage, including damage over time. "
                    + "A long window plus a short cooldown makes the wearer close to unkillable.",
                Related = new[] { ZoneStat.CheatDeathCooldown },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LifeOnHitCap,
                Summary = "The most Life on Hit can heal on one hit, as a percent of the wearer's max health.",
                Details = "Life on Hit adds up the worn value across all the wearer's jewelry. "
                    + "On a damaging hit against a monster, that total is cut down to this cap, then the wearer is healed that percent of their own max health. "
                    + "The cap is rounded to a whole number and a negative cap counts as 0. "
                    + "The heal is at least 1 health and never more than the wearer is missing. It heals nothing at full health. "
                    + SpecialsMonsterZoneNote + SpecialsGearNote,
                Shape = "A percent of max health, as a whole number (a decimal is rounded). 25 means 25 percent of max health.",
                WhenUnset = "The built-in cap of 25 percent applies.",
                Raise = "Heavily stacked jewelry can heal for more per hit.",
                Lower = "Worn Life on Hit is cut off sooner. At 0 or below it heals nothing.",
                Range = "0 or more. Below 0 is read as 0. There is no upper limit in the code.",
                Timing = ZoneStatTiming.Live,
                Caution = "Follows the monster being hit, not the player's zone. It only trims the worn total, so it cannot raise a heal above what the jewelry gives. "
                    + "It is a percent of the wearer's own max health, not of the damage dealt.",
                Related = new[] { ZoneStat.LifeOnHitCooldown },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.LifeOnHitCooldown,
                Summary = "Seconds a wearer waits between Life on Hit heals.",
                Details = "The wait is per character and stops multi-strike attacks and split arrows from pumping the heal. "
                    + "It starts only when a heal actually happens: a hit at full health heals nothing and does not use it up. "
                    + SpecialsMonsterZoneNote + SpecialsGearNote,
                Shape = "A time in seconds. Used as given, with no rounding.",
                WhenUnset = "The built-in wait of 3 seconds applies.",
                Raise = "Life on Hit heals less often.",
                Lower = "Life on Hit heals more often. At 0 or below it can heal on every hit.",
                Range = "Any number of seconds. The code does not clamp it.",
                Timing = ZoneStatTiming.Live,
                Caution = "Follows the monster being hit, not the player's zone. With a low wait and a high cap, fast attackers heal very quickly.",
                Related = new[] { ZoneStat.LifeOnHitCap },
            });

            // ---- T11+ cloak procs ----

            e.Add(CloakEntry(ZoneStat.CloakProcChance,
                "The chance each hit a cloak wearer takes sets off the cloak's proc.",
                "Every hit that does damage to a wearer of a T11+ cloak rolls this chance, if the cloak's own wait is over. "
                    + "Which effect fires depends on the proc the cloak already carries. A hit the wearer's Artful Dodger is already soaking does not roll. ",
                "A chance from 0 to 1 (0.25 = 25 percent of hits).",
                "The matching server setting (zc_cloak_proc_chance) applies.",
                "Cloaks proc more often.",
                "Cloaks proc less often. At 0 the cloak does nothing there, and the item panel reads off here.",
                "0 to 1. Values outside that are clamped.",
                "At 1 (and cooldown 0) every hit procs. At 0 the cloak is dead in that zone. "));

            e.Add(CloakEntry(ZoneStat.CloakProcCooldown,
                "Seconds a cloak wearer waits after a proc before the next roll.",
                "The wait is per character and starts when a proc fires. During it, no hit rolls the chance. ",
                "A time in seconds.",
                "The matching server setting (zc_cloak_proc_cooldown) applies.",
                "Cloaks proc less often.",
                "Cloaks proc more often. At 0 there is no wait.",
                "0 to 3600 seconds. Values outside that are clamped.",
                "The number is read at the moment of the proc and stored as a ready time, so a later change does not shorten a wait already running. "));

            e.Add(CloakEntry(ZoneStat.CloakBreezeHeal,
                "Health restored by one Calming Breeze proc.",
                "Calming Breeze is the proc for cloaks that carried one of the eight ring spells. After the hit lands, and only if the wearer is still alive, it heals this many health points. "
                    + "The heal is a flat change to health: Healing Boost, buffs, ratings and augmentations do not change it. ",
                "A flat amount of health, rounded to a whole number (halves round away from zero).",
                "The matching server setting (zc_cloak_breeze_heal) applies.",
                "Each proc heals more.",
                "Each proc heals less. At 0 the proc still fires and announces itself but heals nothing.",
                "0 or more, up to the largest whole number the game holds. Values below 0 are read as 0.",
                "A flat number, so it is a big share of health on low-health characters and a small one on high-health ones. "));

            e.Add(CloakEntry(ZoneStat.CloakDodgerHits,
                "How many hits one Artful Dodger proc turns into stamina loss.",
                "Artful Dodger is the proc for cloaks that carried the damage weave. After the hit that fires it, the wearer's next hits inside the window strike stamina instead of health, up to this many hits. ",
                "A count of hits, rounded to a whole number (halves round away from zero).",
                "The matching server setting (zc_cloak_dodger_hits) applies.",
                "More hits are covered by one proc.",
                "Fewer hits are covered. At 0 the proc does nothing.",
                "0 to 1000. Values outside that are clamped.",
                "If the hit count or the window is 0, an Artful Dodger proc has no effect. The hit that fires it always lands on health as normal. "));

            e.Add(CloakEntry(ZoneStat.CloakDodgerWindow,
                "Seconds an Artful Dodger proc stays armed.",
                "After an Artful Dodger proc fires, hits that land within this many seconds (up to the hit count) strike stamina instead of health. Charges not used by then are lost, and the wearer is told it faded. ",
                "A time in seconds.",
                "The matching server setting (zc_cloak_dodger_window) applies.",
                "The proc stays armed longer, so more of its hits get used.",
                "The proc fades sooner. At 0 the proc does nothing.",
                "0 to 3600 seconds. Values outside that are clamped.",
                "If the window or the hit count is 0, an Artful Dodger proc has no effect. "));

            e.Add(CloakEntry(ZoneStat.CloakDodgerStaminaMult,
                "Stamina an Artful Dodger hit costs for each point of damage it turns away.",
                "While Artful Dodger is armed, each hit the wearer takes costs this many stamina per point of damage, True Damage included. "
                    + "The cost is rounded up, so a small hit never costs 0. Damage that the wearer's stamina cannot pay for goes through to health at 1 for 1. "
                    + "The number is read where the hit lands, not where the proc fired. ",
                "A multiplier on damage: stamina lost = damage x this number.",
                "The matching server setting (zc_cloak_dodger_stamina_mult) applies.",
                "Each hit drains stamina faster, so the pool runs dry sooner and more damage goes through to health.",
                "Each hit drains less stamina, so the proc covers more damage. At 1 stamina costs the same as the damage it stops.",
                "0.01 to 1000. Values outside that are clamped.",
                "A low value makes Artful Dodger very strong: a long fight barely touches stamina. "));

            e.Add(CloakEntry(ZoneStat.CloakGraceSeconds,
                "Seconds of damage immunity from one Asheron's Grace proc.",
                "Asheron's Grace is the proc for cloaks that carried Cloaked in Skill. After the hit that fires it, the wearer takes no damage from anything for this many seconds, and sees the lifestone shield. "
                    + "It shares its immunity window with Cheat Death. ",
                "A time in seconds.",
                "The matching server setting (zc_cloak_grace_seconds) applies.",
                "A longer protected window after each proc.",
                "A shorter window. At 0 the proc does nothing.",
                "0 to 3600 seconds. Values outside that are clamped.",
                "Immunity covers every kind of damage, so a long window is close to invincibility. "
                    + "The hit that fires the proc still lands. "));
        }

        private static ZoneStatHelpEntry CloakEntry(string key, string summary, string details, string shape, string whenUnset,
            string raise, string lower, string range, string caution)
        {
            return new ZoneStatHelpEntry
            {
                Key = key,
                Summary = summary,
                Details = details + SpecialsCloakNote,
                Shape = shape + " The number is held to the range below, whether it comes from a zone or the server setting.",
                WhenUnset = whenUnset + " If no cloak stat is set for the spot, all of them use the server settings.",
                Raise = raise,
                Lower = lower,
                Range = range,
                Timing = ZoneStatTiming.Live,
                Caution = caution + "Retail cloaks below T11 never change. These numbers also show on the cloak's item panel for the wearer. "
                    + "A change reaches every T11+ cloak at once, including ones players already own.",
                Related = SpecialsCloakRelated,
            };
        }
    }
}
