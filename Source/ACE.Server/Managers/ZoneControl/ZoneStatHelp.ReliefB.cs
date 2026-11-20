using System.Collections.Generic;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    // Relief curve mid-points (relief_aug / relief_dr / relief_critdr, x1..x4 and y1..y4). Checked against GetAxisRelief in
    // Creature_PercentHpDamage. The three callers pass: life augs (aug), Damage Resist rating (dr), Crit Damage Resist rating (critdr).
    // Points are read only from the zone profile (there is no server-setting default for them). Part A of the group is ZoneStatHelp.ReliefA.cs.
    public static partial class ZoneStatHelp
    {
        private const string ReliefPointModel =
            "Each curve can have up to 4 mid-points. Point number N is a pair: relief_<curve>_xN is where on the player's number the point sits, "
            + "and relief_<curve>_yN is how much relief the curve gives there. A point counts only when BOTH its x and its y are set. "
            + "Counting points are sorted by x, so the slot numbers do not have to be in order. "
            + "With at least one counting point, the curve is straight lines from (start, 0) through each point in turn to (max, cap), and the bend is not used. "
            + "With no counting points, the curve uses start, max, cap and bend as usual. ";

        private const string ReliefPointWhere =
            "The x is in the same units as that curve's input: relief_aug_x* are life aug counts (Triune Weave counts as life augs), "
            + "relief_dr_x* are Damage Resist ratings, and relief_critdr_x* are Crit Damage Resist ratings. "
            + "The y is a fraction of damage removed, like the cap: 0.25 removes a quarter. "
            + "What the curve cuts is the same as for the curve's cap: the %HP floor hit for life aug and Damage Resist, True Damage for life aug, "
            + "and only the extra damage of a crit for Crit Damage Resist. ";

        private const string ReliefPointGate =
            "Does nothing while Zone Control (zonecontrol_enabled) is off. The Damage Resist curve and the floor itself also need the %HP floor on (zc_pcthp_enabled) "
            + "and a floor amount above 0 (percent_hp_base, or the server's floor setting). Crit Damage Resist points only matter on crit hits. ";

        private const string ReliefPointTraps =
            "Points only shape the curve between start and max. A player at or below start gets 0, and at or above max gets the cap, whatever the points say. "
            + "If the cap is 0 or below the curve gives nothing, points or not. ";

        static partial void AddReliefB(List<ZoneStatHelpEntry> e)
        {
            e.Add(new ZoneStatHelpEntry
            {
                Key = "relief_aug_xN / relief_dr_xN / relief_critdr_xN (N = 1 to 4)",
                Pattern = new Regex("^relief_(aug|dr|critdr)_x[1-4]$", RegexOptions.IgnoreCase),
                Summary = "Where a mid-point sits on a relief curve: the player number at which the point applies.",
                Details = ReliefPointModel + ReliefPointWhere
                    + "This stat is the x of one mid-point. Pair it with the y of the same number on the same curve (for example relief_dr_x2 with relief_dr_y2). "
                    + "Between two neighbouring points the relief rises or falls in a straight line.",
                Shape = "A player number in the curve's own units (an aug count, or a rating). Not a fraction.",
                WhenUnset = "That point does not count, whatever its y is. If no point of the curve counts, the curve uses its bend instead. "
                    + "There is no server setting that supplies a default point.",
                Raise = "Moves the point to a higher player number, so the relief at the point's y is reached later.",
                Lower = "Moves the point to a lower player number, so the relief at the point's y is reached sooner.",
                Range = "Strictly above the curve's start and strictly below its max. A point at or outside those bounds is silently ignored.",
                Timing = ZoneStatTiming.Live,
                Caution = ReliefPointGate + ReliefPointTraps
                    + "An x at or below start, or at or above max, switches that point off with no warning, so changing start or max can switch points off. "
                    + "Setting only the x does nothing: the y must be set too.",
                Related = new[]
                {
                    ZoneStat.ReliefAugY1, ZoneStat.ReliefDrY1, ZoneStat.ReliefCritDrY1,
                    ZoneStat.ReliefAugStart, ZoneStat.ReliefAugMax, ZoneStat.ReliefAugCap,
                    ZoneStat.ReliefDrStart, ZoneStat.ReliefDrMax, ZoneStat.ReliefDrCap,
                    ZoneStat.ReliefCritDrStart, ZoneStat.ReliefCritDrMax, ZoneStat.ReliefCritDrCap,
                },
            });

            e.Add(new ZoneStatHelpEntry
            {
                Key = "relief_aug_yN / relief_dr_yN / relief_critdr_yN (N = 1 to 4)",
                Pattern = new Regex("^relief_(aug|dr|critdr)_y[1-4]$", RegexOptions.IgnoreCase),
                Summary = "How much relief a mid-point gives on a relief curve, as a fraction from 0 to 1.",
                Details = ReliefPointModel + ReliefPointWhere
                    + "This stat is the y of one mid-point. Pair it with the x of the same number on the same curve (for example relief_critdr_x3 with relief_critdr_y3).",
                Shape = "A fraction. 0.25 means 25 percent off at that point. The game holds it between 0 and 1. "
                    + "It is not held to the cap, so a point can sit above the cap and the curve will then come back down to the cap at max.",
                WhenUnset = "That point does not count, whatever its x is. If no point of the curve counts, the curve uses its bend instead. "
                    + "There is no server setting that supplies a default point.",
                Raise = "The curve gives more relief at and around the point's x.",
                Lower = "The curve gives less relief at and around the point's x.",
                Range = "0 to 1. Below 0 is read as 0 and above 1 is read as 1.",
                Timing = ZoneStatTiming.Live,
                Caution = ReliefPointGate + ReliefPointTraps
                    + "Setting only the y does nothing: the x must be set too. "
                    + "The y is not checked against the cap or against the other points, so a high y followed by a lower one makes the relief fall as players get stronger.",
                Related = new[]
                {
                    ZoneStat.ReliefAugX1, ZoneStat.ReliefDrX1, ZoneStat.ReliefCritDrX1,
                    ZoneStat.ReliefAugCap, ZoneStat.ReliefDrCap, ZoneStat.ReliefCritDrCap,
                    ZoneStat.ReliefAugBend, ZoneStat.ReliefDrBend, ZoneStat.ReliefCritDrBend,
                },
            });
        }
    }
}
