using System.Collections.Generic;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Relief and aug curve shape stats (start / max / cap / bend). Checked against Creature_PercentHpDamage (GetAxisRelief,
    // GetLinearRelief, the %HP floor, True Damage and crit bonus relief) and Creature_ZoneAugCurves (aug_curves, protections and
    // armor), plus the callers in Creature_Properties (GetResistanceMod), Monster_Melee (GetArmorMod) and Creature_Combat (shield).
    // All are read per hit, so none is stamped at spawn. Each stat can also be unset: the server's zc_relief_* / zc_aug_* setting
    // of the same name then supplies the number.
    public static partial class ZoneStatHelp
    {
        private const string ReliefCurveModel =
            "The curve reads one player number (the 'input'). At or below start the relief is 0. At or above max the relief is the cap. "
            + "In between it rises from 0 to the cap with progress t = (input - start) / (max - start), as cap x t^bend. "
            + "If max is at or below start, any input above start gets the full cap. ";

        private const string ReliefWhere =
            "relief_aug measures the player's life augs (Triune Weave counts as life augs). relief_dr measures the player's total Damage Resist rating. "
            + "relief_critdr measures the player's Crit Damage Resist rating. ";

        private const string ReliefUse =
            "Life aug relief and Damage Resist relief shrink the %HP floor hit, and the two multiply (each takes its share of what the other left). "
            + "Life aug relief alone also shrinks True Damage. Crit Damage Resist relief shrinks only the extra damage of a crit on the %HP floor or on True Damage. "
            + "Normal mitigated hit damage is not touched by any of the three. ";

        private const string ReliefGate =
            "Does nothing while Zone Control (zonecontrol_enabled) is off. The Damage Resist curve and the floor itself also need the %HP floor on (zc_pcthp_enabled) "
            + "and a floor amount above 0 (percent_hp_base, or the server's floor setting). Crit Damage Resist relief only matters on crit hits.";

        private const string ReliefPoints =
            "If any mid-points are set for the curve (relief_*_x1..y4), they replace the bend and the curve runs in straight pieces through them, "
            + "but the start, max and cap here still bound the curve. A mid-point whose x is not strictly between start and max is ignored, "
            + "so moving start or max can switch mid-points off.";

        private const string AugCurveGate =
            "Does nothing unless aug_curves is on for the monster. Also does nothing while Zone Control (zonecontrol_enabled) is off. "
            + "It applies only to hits from monsters on players (melee, missile and spells), never to players or pets as attackers.";

        static partial void AddReliefA(List<ZoneStatHelpEntry> e)
        {
            e.Add(new ZoneStatHelpEntry
            {
                Key = "relief_aug_start / relief_dr_start / relief_critdr_start",
                Pattern = new Regex("^relief_(aug|dr|critdr)_start$", RegexOptions.IgnoreCase),
                Summary = "Where a relief curve begins: the player number below which that curve gives no relief.",
                Details = ReliefCurveModel + ReliefWhere + "This stat is the start point of the curve named in the key. " + ReliefUse,
                Shape = "A player number, in the same units as the curve's input: an aug count for relief_aug_start, a rating for relief_dr_start and relief_critdr_start.",
                WhenUnset = "The server's setting of the same name with a zc_ prefix (for example zc_relief_aug_start) is used.",
                Raise = "Players need more of that number before they get any relief, and the whole curve slides up.",
                Lower = "Relief starts earlier, so weaker players get some.",
                Range = "Any number. An input at or below the start gets 0 relief.",
                Timing = ZoneStatTiming.Live,
                Caution = ReliefGate + " A start at or above the max turns the curve into a step from 0 to the full cap. " + ReliefPoints,
                Related = new[]
                {
                    ZoneStat.ReliefAugStart, ZoneStat.ReliefDrStart, ZoneStat.ReliefCritDrStart,
                    ZoneStat.ReliefAugMax, ZoneStat.ReliefDrMax, ZoneStat.ReliefCritDrMax,
                },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "relief_aug_max / relief_dr_max / relief_critdr_max",
                Pattern = new Regex("^relief_(aug|dr|critdr)_max$", RegexOptions.IgnoreCase),
                Summary = "Where a relief curve tops out: the player number at which that curve reaches its cap.",
                Details = ReliefCurveModel + ReliefWhere + "This stat is the max point of the curve named in the key. " + ReliefUse,
                Shape = "A player number, in the same units as the curve's input: an aug count for relief_aug_max, a rating for relief_dr_max and relief_critdr_max.",
                WhenUnset = "The server's setting of the same name with a zc_ prefix (for example zc_relief_dr_max) is used.",
                Raise = "The climb to the cap is longer and gentler, and players need more to get the full relief.",
                Lower = "The climb is steeper and players reach the full relief sooner.",
                Range = "Any number. Keep it above the start. At or below the start, any input above the start gets the full cap at once.",
                Timing = ZoneStatTiming.Live,
                Caution = ReliefGate + " A max at or below the start turns the curve into a step from 0 to the full cap. " + ReliefPoints,
                Related = new[]
                {
                    ZoneStat.ReliefAugMax, ZoneStat.ReliefDrMax, ZoneStat.ReliefCritDrMax,
                    ZoneStat.ReliefAugStart, ZoneStat.ReliefDrStart, ZoneStat.ReliefCritDrStart,
                },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "relief_aug_cap / relief_dr_cap / relief_critdr_cap",
                Pattern = new Regex("^relief_(aug|dr|critdr)_cap$", RegexOptions.IgnoreCase),
                Summary = "The most a relief curve can take off, as a fraction from 0 to 1.",
                Details = ReliefCurveModel + ReliefWhere + "This stat is the top of the curve named in the key. " + ReliefUse
                    + "For life aug and Damage Resist the cap is the largest share of the hit removed (0.5 removes half). For Crit Damage Resist it is the largest "
                    + "share of the crit's extra damage removed.",
                Shape = "A fraction. 0.5 means 50 percent off at the top of the curve. The game holds it between 0 and 1.",
                WhenUnset = "The server's setting of the same name with a zc_ prefix (for example zc_relief_aug_cap) is used.",
                Raise = "Strong players take a bigger cut from the damage this curve covers.",
                Lower = "The curve cuts less at its top. At 0 the curve does nothing at all.",
                Range = "0 to 1. Below 0 is read as 0 and above 1 is read as 1.",
                Timing = ZoneStatTiming.Live,
                Caution = ReliefGate + " A cap of 0 or below turns that curve off. A cap of 1 on life aug or Damage Resist relief lets a player take no %HP floor damage at the top of the curve "
                    + "(the two curves multiply, so either one at 1 removes it all). " + ReliefPoints,
                Related = new[]
                {
                    ZoneStat.ReliefAugCap, ZoneStat.ReliefDrCap, ZoneStat.ReliefCritDrCap,
                    ZoneStat.ReliefAugStart, ZoneStat.ReliefAugMax,
                },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "relief_aug_bend / relief_dr_bend / relief_critdr_bend",
                Pattern = new Regex("^relief_(aug|dr|critdr)_bend$", RegexOptions.IgnoreCase),
                Summary = "The shape of a relief curve between its start and max.",
                Details = ReliefCurveModel + ReliefWhere + "This stat is the bend of the curve named in the key. " + ReliefUse
                    + "A bend of 1 is a straight line. Below 1 gives strong relief early that tapers off. Above 1 gives a slow start that ramps up late.",
                Shape = "A power applied to progress. 1 is straight. Only a bend above 0 is used: 0 or below is ignored and the curve is drawn straight. "
                    + "A bend very close to 1 is also drawn straight.",
                WhenUnset = "The server's setting of the same name with a zc_ prefix (for example zc_relief_critdr_bend) is used.",
                Raise = "The curve starts slower and ramps late, so mid-range players get less relief than on a straight line.",
                Lower = "The curve starts faster and tapers off, so mid-range players get more relief than on a straight line.",
                Range = "Above 0 (for example 0.5 to 3). 0 or below is read as a straight line.",
                Timing = ZoneStatTiming.Live,
                Caution = ReliefGate + " The bend does not move the start, the max or the cap, only the way between them. "
                    + "It is ignored when mid-points (relief_*_x1..y4) are set for that curve: they replace the bend entirely.",
                Related = new[]
                {
                    ZoneStat.ReliefAugBend, ZoneStat.ReliefDrBend, ZoneStat.ReliefCritDrBend,
                    ZoneStat.ReliefAugX1, ZoneStat.ReliefAugY1,
                },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = ZoneStat.AugCurves,
                Summary = "Switches on the aug curves for this tier's monsters: the player's augs cut their normal hits by a capped curve.",
                Details = "When on (a value of 0.5 or more), the game stops using its own aug math for hits from these monsters on players. "
                    + "Protection spells count at their base strength only, times one minus the life aug curve (aug_prot_*). "
                    + "Armor, Impenetrability, Banes and Armor Self count without their aug parts, times one minus the item aug curve (aug_armor_*). "
                    + "Item augs and life augs both include Triune Weave. It covers melee, missile and spell hits. "
                    + "A shield's Impenetrability and Banes also drop their aug part against these monsters.",
                Shape = "A switch. 0.5 or more is on, anything below is off.",
                WhenUnset = "Off: the game's own aug math is used.",
                Raise = "Turning it on replaces the game's own aug math for this monster's hits.",
                Lower = "Turning it off puts the game's own aug math back.",
                Range = "0 or 1. Anything from 0.5 up counts as on.",
                Timing = ZoneStatTiming.Live,
                Caution = "Does nothing while Zone Control (zonecontrol_enabled) is off. It affects only monster hits on players. "
                    + "True Damage and the %HP floor do not use these curves (they use relief_*). "
                    + "A weapon or monster that ignores magic resist skips the protection part of the curve. "
                    + "The aug_prot_* and aug_armor_* stats do nothing until this is on.",
                Related = new[]
                {
                    ZoneStat.AugProtStart, ZoneStat.AugProtMax, ZoneStat.AugProtCap, ZoneStat.AugProtBend,
                    ZoneStat.AugArmorStart, ZoneStat.AugArmorMax, ZoneStat.AugArmorCap, ZoneStat.AugArmorBend,
                },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "aug_prot_start / aug_armor_start",
                Pattern = new Regex("^aug_(prot|armor)_start$", RegexOptions.IgnoreCase),
                Summary = "Where an aug curve begins: the aug count below which it removes nothing.",
                Details = "aug_prot_start belongs to the protections curve and counts the player's life augs. aug_armor_start belongs to the armor curve and counts "
                    + "the player's item augs. Both count Triune Weave. At or below the start the curve removes 0. "
                    + "Past it the curve rises with the same rule as the relief curves: progress t = (augs - start) / (max - start), curve = cap x t^bend, "
                    + "full cap at the max or above. If max is at or below start, any count above start gets the full cap.",
                Shape = "An aug count.",
                WhenUnset = "The server's setting of the same name with a zc_ prefix (for example zc_aug_prot_start) is used.",
                Raise = "Players need more augs before the curve starts to cut the monster's hits.",
                Lower = "The curve starts cutting at fewer augs.",
                Range = "Any number. A count at or below the start gets 0.",
                Timing = ZoneStatTiming.Live,
                Caution = AugCurveGate + " A start at or above the max turns the curve into a step from 0 to the full cap. "
                    + "Unlike relief_*, these curves have no mid-point stats: the start, max, cap and bend are the whole shape.",
                Related = new[] { ZoneStat.AugCurves, ZoneStat.AugProtStart, ZoneStat.AugArmorStart, ZoneStat.AugProtMax, ZoneStat.AugArmorMax },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "aug_prot_max / aug_armor_max",
                Pattern = new Regex("^aug_(prot|armor)_max$", RegexOptions.IgnoreCase),
                Summary = "Where an aug curve tops out: the aug count at which it reaches its cap.",
                Details = "aug_prot_max belongs to the protections curve (life augs). aug_armor_max belongs to the armor curve (item augs). "
                    + "At this count or above the curve removes its full cap. Below it the curve rises from 0 at the start, following the bend.",
                Shape = "An aug count.",
                WhenUnset = "The server's setting of the same name with a zc_ prefix (for example zc_aug_armor_max) is used.",
                Raise = "The climb is longer and gentler: players need more augs for the full cut.",
                Lower = "The climb is steeper: players reach the full cut with fewer augs.",
                Range = "Any number. Keep it above the start. At or below the start, any count above the start gets the full cap at once.",
                Timing = ZoneStatTiming.Live,
                Caution = AugCurveGate + " A max at or below the start turns the curve into a step from 0 to the full cap.",
                Related = new[] { ZoneStat.AugCurves, ZoneStat.AugProtMax, ZoneStat.AugArmorMax, ZoneStat.AugProtStart, ZoneStat.AugArmorStart },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "aug_prot_cap / aug_armor_cap",
                Pattern = new Regex("^aug_(prot|armor)_cap$", RegexOptions.IgnoreCase),
                Summary = "The most of a monster's normal hit that an aug curve can remove, as a fraction.",
                Details = "aug_prot_cap is the top of the protections curve (life augs): the protection layer is cut to (1 minus this) of its base strength at the max. "
                    + "aug_armor_cap is the top of the armor curve (item augs): the armor multiplier is cut to (1 minus this) at the max. "
                    + "The curve reaches the cap at the max count and holds it above.",
                Shape = "A fraction. 0.9 means the curve removes 90 percent at the top. The game holds it between 0 and 0.99, so a player is never fully immune.",
                WhenUnset = "The server's setting of the same name with a zc_ prefix (for example zc_aug_prot_cap) is used.",
                Raise = "Heavily augmented players take a bigger cut from this monster's normal hits.",
                Lower = "The cut at the top shrinks. At 0 or below the curve does nothing.",
                Range = "0 to 0.99. Above 0.99 is read as 0.99. Below 0 is read as 0.",
                Timing = ZoneStatTiming.Live,
                Caution = AugCurveGate + " A cap of 0 or below turns that curve off.",
                Related = new[] { ZoneStat.AugCurves, ZoneStat.AugProtCap, ZoneStat.AugArmorCap, ZoneStat.AugProtMax, ZoneStat.AugArmorMax },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "aug_prot_bend / aug_armor_bend",
                Pattern = new Regex("^aug_(prot|armor)_bend$", RegexOptions.IgnoreCase),
                Summary = "The shape of an aug curve between its start and max.",
                Details = "aug_prot_bend shapes the protections curve (life augs). aug_armor_bend shapes the armor curve (item augs). "
                    + "Between start and max the curve is cap x t^bend, where t is progress from 0 to 1. A bend of 1 is a straight line. "
                    + "Below 1 gives strong cuts early that taper off. Above 1 gives a slow start that ramps up late.",
                Shape = "A power applied to progress. 1 is straight. Only a bend above 0 is used: 0 or below is ignored and the curve is drawn straight. "
                    + "A bend very close to 1 is also drawn straight.",
                WhenUnset = "The server's setting of the same name with a zc_ prefix (for example zc_aug_armor_bend) is used.",
                Raise = "The curve starts slower and ramps late, so mid-range players get less cut than on a straight line.",
                Lower = "The curve starts faster and tapers off, so mid-range players get more cut than on a straight line.",
                Range = "Above 0 (for example 0.5 to 3). 0 or below is read as a straight line.",
                Timing = ZoneStatTiming.Live,
                Caution = AugCurveGate + " The bend does not move the start, the max or the cap. These curves have no mid-point stats to override it.",
                Related = new[] { ZoneStat.AugCurves, ZoneStat.AugProtBend, ZoneStat.AugArmorBend, ZoneStat.AugProtCap, ZoneStat.AugArmorCap },
            });
        }
    }
}
