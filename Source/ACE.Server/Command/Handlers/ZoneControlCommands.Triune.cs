using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /zonecontrol triune ... - Triune earning per tier (TriuneEarning.cs, owner 2026-11-01): on / off, the weekly reset, and
    /// each tier's floor / ceiling kills an hour, slow / fast weeks and points. Every edit is saved and live at once.
    /// </summary>
    public static partial class ZoneControlCommands
    {
        private const string TriuneUsage =
            "triune [show] | get | on | off | gems on|off | gembase <n> | drip on|off | dripbase <creature> <item/life> | reload | "
            + "movestatus | undomove [confirm|cancel] | move on|off | "
            + "reset <day> <hour 0-23> [time zone id...] | tier <11-25|all> [floor <kph>] [ceiling <kph>] [slow <weeks>] [fast <weeks>] [points <n>] [gemslow <weeks>] [gemfast <weeks>]";

        private static readonly HashSet<string> TriuneWriteVerbs = new() { "on", "off", "gems", "gembase", "drip", "dripbase", "reload", "reset", "tier", "undomove", "move" };

        /// <param name="args">The re-tokenized /zonecontrol arguments: args[0] is "triune", args[1] the verb.</param>
        private static void HandleTriune(Session session, List<string> args, Action<string> Msg)
        {
            var verb = args.Count > 1 ? args[1].ToLowerInvariant() : "show";

            // Triune is augmentation power: looking is Developer (the /zonecontrol level), changing it is Admin, like a Bounty
            // (a mistyped verb gets the usage line below, not this)
            if (TriuneWriteVerbs.Contains(verb) && session != null && session.AccessLevel < AccessLevel.Admin)
            { Msg("Changing Triune earning needs Admin access."); return; }

            try
            {
                HandleTriuneVerb(session, verb, args, Msg);
            }
            catch (InvalidOperationException ex)
            {
                // the store could not be read (TriuneEarning refuses to save over it) - nothing changed
                Msg("Triune earning: " + ex.Message);
            }
        }

        private static void HandleTriuneVerb(Session session, string verb, List<string> args, Action<string> Msg)
        {
            switch (verb)
            {
                case "get":
                {
                    // the plugin's Triune page (2026-11-02): one machine line, eaten before the chat window
                    var payload = BuildTriunePayload(TriuneEarning.Current);
                    if (session != null) SendChunked(session, payload);
                    else Msg(payload);
                    return;
                }

                case "show":
                case "list":
                    ShowTriune(TriuneEarning.Current, Msg);
                    return;

                case "on":
                case "off":
                {
                    var on = verb == "on";
                    ShowTriune(TriuneEarning.Mutate(c => c.Enabled = on), Msg);
                    return;
                }

                case "gems":
                {
                    var onOff = args.Count < 3 ? "" : args[2].ToLowerInvariant();
                    if (onOff != "on" && onOff != "off") { Msg("Usage: triune gems on|off"); return; }
                    var on = onOff == "on";
                    ShowTriune(TriuneEarning.Mutate(c => c.GemsEnabled = on), Msg);
                    return;
                }

                case "gembase":
                {
                    if (args.Count < 3 || !int.TryParse(args[2], out var b) || b < 0 || b > TriuneEarning.MaxGemBaseCap)
                    { Msg($"Usage: triune gembase <0-{TriuneEarning.MaxGemBaseCap:N0}>   (bought school augs a gem needs; 4000 default)"); return; }
                    var cfg = TriuneEarning.Mutate(c => c.GemBaseCap = b);
                    ShowTriune(cfg, Msg);
                    WarnTriuneVsHitGate(cfg, Msg);   // the T15+ gem ladder starts here: it must still meet what T16+ asks
                    return;
                }

                case "drip":
                {
                    var onOff = args.Count < 3 ? "" : args[2].ToLowerInvariant();
                    if (onOff != "on" && onOff != "off") { Msg("Usage: triune drip on|off   (T11-T14 own-tier kills add +1 Creature / Item / Life)"); return; }
                    var on = onOff == "on";
                    var cfg = TriuneEarning.Mutate(c => c.DripEnabled = on);
                    ShowTriune(cfg, Msg);
                    WarnTriuneVsHitGate(cfg, Msg);
                    return;
                }

                case "dripbase":
                {
                    if (args.Count < 4 || !int.TryParse(args[2], out var bc) || !int.TryParse(args[3], out var bi)
                        || bc < 0 || bi < 0 || bc > TriuneEarning.MaxGemBaseCap || bi > TriuneEarning.MaxGemBaseCap)
                    { Msg("Usage: triune dripbase <creature> <item/life>   (the T11 entry ladder; 4000 2000 default)"); return; }
                    var cfg = TriuneEarning.Mutate(c => { c.DripBaseCreature = bc; c.DripBaseItem = bi; });
                    ShowTriune(cfg, Msg);
                    WarnTriuneVsHitGate(cfg, Msg);
                    return;
                }

                case "movestatus":
                    foreach (var line in AugMove.Status())
                        Msg(line);
                    return;

                case "undomove":
                {
                    // the rollback of the Triune retire (owner 2026-11-22) runs OFFLINE: 'confirm' arms it for the next server
                    // start (before anyone can log in), 'cancel' disarms it (until that start); without either it only lists what it would do
                    var sub = args.Count > 2 ? args[2].ToLowerInvariant() : "";
                    if (sub == "confirm" || sub == "cancel")
                    {
                        var arm = sub == "confirm";
                        AugMove.ArmUndoAtStart(arm);
                        ACE.Server.Managers.PlayerManager.BroadcastToAuditChannel(session?.Player, $"triune undomove {sub} ({AugMove.UndoAtStartKey} = {(arm ? 1 : 0)})");
                        Msg(arm
                            ? $"Triune move undo ARMED ({AugMove.UndoAtStartKey} = 1). Restart THIS build: at startup, before anyone can log in, it moves every moved character back, saves the weapon scaling rows for the old build, and stops. Start the OLD build only when the log says OFFLINE UNDO DONE (exit code 0); on INCOMPLETE fix the FAILED lines and start THIS build again. Before that restart 'undomove cancel' disarms it; after it, only SQL can (UPDATE config_properties_boolean SET value = 0 WHERE `key` = '{AugMove.UndoAtStartKey}')."
                            : $"Triune move undo disarmed ({AugMove.UndoAtStartKey} = 0).");
                        return;
                    }
                    var r = AugMove.UndoAll(apply: false);
                    Msg($"Triune move undo (dry run): {r.Players:N0} characters read, {r.Undone:N0} would be moved back (only characters the move changed; the rest are left as they are)."
                        + (r.Failed > 0 ? $" {r.Failed:N0} have an unreadable move record - fix them first." : "")
                        + (AugMove.IsUndoAtStartArmed() ? " The undo is ARMED for the next start." : " To run it: 'undomove confirm', then restart this build (the undo runs at startup)."));
                    foreach (var line in r.Lines)
                        Msg(line);
                    return;
                }

                case "move":
                {
                    var onOff = args.Count < 3 ? "" : args[2].ToLowerInvariant();
                    if (onOff != "on" && onOff != "off") { Msg("Usage: triune move on|off   (the login move of the retired Triune / charm counts)"); return; }
                    if (session == null)
                    { Msg("Run 'triune move on|off' in game, not from the server console (it moves online characters)."); return; }
                    ACE.Server.Managers.PlayerManager.BroadcastToAuditChannel(session.Player, $"triune move {onOff}");
                    var off = onOff == "off";
                    var movedNow = 0;
                    TriuneEarning.Mutate(c => c.MoveOff = off);
                    if (!off)
                        movedNow = AugMove.MoveOnline();   // online characters held back while it was off move now
                    Msg($"Triune move at login: {(off ? "OFF" : "ON")}."
                        + (!off ? $" {movedNow:N0} online characters were moved now; the rest move at their next login." : ""));
                    return;
                }

                case "reload":
                    try { TriuneEarning.Reload(); }
                    catch (Exception ex)
                    {
                        Msg($"Triune earning: the stored settings could not be read ({ex.Message}) - the running settings are kept.");
                        return;
                    }
                    ShowTriune(TriuneEarning.Current, Msg);
                    return;

                case "reset":
                {
                    if (args.Count < 4 || !TryParseDay(args[2], out var day) || !int.TryParse(args[3], out var hour) || hour < 0 || hour > 23)
                    { Msg("Usage: triune reset <day> <hour 0-23> [time zone id...]   e.g. triune reset friday 12 Eastern Standard Time"); return; }
                    var tzId = args.Count > 4 ? string.Join(" ", args.Skip(4)) : null;
                    if (tzId != null)
                    {
                        try { TimeZoneInfo.FindSystemTimeZoneById(tzId); }
                        catch (Exception) { Msg($"Unknown time zone '{tzId}'. Examples: Eastern Standard Time, Central Standard Time, UTC."); return; }
                    }
                    ShowTriune(TriuneEarning.Mutate(c => { c.ResetDay = (int)day; c.ResetHour = hour; if (tzId != null) c.ResetTimeZone = tzId; }), Msg);
                    return;
                }

                case "tier":
                {
                    if (args.Count < 5 || (args.Count - 3) % 2 != 0)
                    { Msg("Usage: " + TriuneUsage); return; }

                    var tiers = new List<int>();
                    if (args[2].Equals("all", StringComparison.OrdinalIgnoreCase))
                        for (var t = TriuneEarning.FirstTier; t <= TriuneEarning.MaxTier; t++) tiers.Add(t);
                    else if (int.TryParse(args[2].TrimStart('t', 'T'), out var one) && one >= TriuneEarning.FirstTier && one <= TriuneEarning.MaxTier)
                        tiers.Add(one);
                    else
                    { Msg($"Tier must be {TriuneEarning.FirstTier}-{TriuneEarning.MaxTier} or all ({TriuneEarning.DripMinTier}-{TriuneEarning.DripMaxTier} = the aug drip, {TriuneEarning.MinTier}+ = Triune)."); return; }

                    var edits = new List<Action<TriuneTierRow>>();
                    for (var i = 3; i + 1 < args.Count; i += 2)
                    {
                        var field = args[i].ToLowerInvariant();
                        if (!double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v))
                        { Msg($"'{args[i + 1]}' is not a number."); return; }

                        // every value finite and inside its range (never NaN / Infinity / 1e308 / a tiny divisor)
                        bool InLimits(double min, double max, string unit)
                        {
                            if (v >= min && v <= max) return true;
                            Msg($"{field} must be {min:0.##} - {max:N0} {unit}.");
                            return false;
                        }
                        switch (field)
                        {
                            case "floor": if (!InLimits(TriuneEarning.MinKph, TriuneEarning.MaxKph, "kills an hour")) return; edits.Add(r => r.FloorKph = v); break;
                            case "ceiling": if (!InLimits(TriuneEarning.MinKph, TriuneEarning.MaxKph, "kills an hour")) return; edits.Add(r => r.CeilingKph = v); break;
                            case "slow": if (!InLimits(TriuneEarning.MinWeeks, TriuneEarning.MaxWeeks, "weeks")) return; edits.Add(r => r.SlowWeeks = v); break;
                            case "fast": if (!InLimits(TriuneEarning.MinWeeks, TriuneEarning.MaxWeeks, "weeks")) return; edits.Add(r => r.FastWeeks = v); break;
                            case "points": if (v != Math.Floor(v)) { Msg("points must be a whole number."); return; } if (!InLimits(0, TriuneEarning.MaxPoints, "points")) return; edits.Add(r => r.Points = (int)v); break;
                            case "gemslow": if (!InLimits(TriuneEarning.MinWeeks, TriuneEarning.MaxWeeks, "weeks")) return; edits.Add(r => r.GemSlowWeeks = v); break;
                            case "gemfast": if (!InLimits(TriuneEarning.MinWeeks, TriuneEarning.MaxWeeks, "weeks")) return; edits.Add(r => r.GemFastWeeks = v); break;
                            default: Msg($"Unknown field '{field}'. Use floor / ceiling / slow / fast / points / gemslow / gemfast."); return;
                        }
                    }

                    var cfg = TriuneEarning.Mutate(c =>
                    {
                        foreach (var r in c.Tiers.Where(r => tiers.Contains(r.Tier)))
                            foreach (var e in edits) e(r);
                    });
                    foreach (var r in cfg.Tiers.Where(r => tiers.Contains(r.Tier)))
                        Msg(DescribeTriuneTier(cfg, r));
                    Msg("(A ceiling at or below the floor, or fast weeks above slow weeks, is corrected when saved.)");
                    WarnTriuneVsHitGate(cfg, Msg);
                    return;
                }

                default:
                    Msg("Usage: " + TriuneUsage);
                    return;
            }
        }

        /// <summary>
        /// Each tier's rewards should end where the NEXT tier's gates start (weapon scaling MinWieldCreature / MinWieldAugs /
        /// MinWieldLife) - the two are tuned separately, so say so when they part (e.g. points 400 at T15 strands players 100
        /// short of what T16 asks).
        /// </summary>
        private static void WarnTriuneVsHitGate(TriuneEarning.Config cfg, Action<string> Msg)
        {
            // the coin-bought Triune and charms are retired: past the luminance caps only rewards and school gems climb
            if (!cfg.Enabled || !cfg.GemsEnabled)
                Msg($"  WARNING: hunting rewards {(cfg.Enabled ? "ON" : "OFF")}, school gems {(cfg.GemsEnabled ? "ON" : "OFF")} - with either OFF nobody new can climb past T15 (T16 asks past the luminance caps).");
            for (var t = TriuneEarning.FirstTier; t < TriuneEarning.MaxTier; t++)
            {
                var next = ACE.Server.Managers.WeaponScaling.WeaponScalingManager.GetTier(t + 1);
                if (next == null) continue;
                var c = TriuneEarning.DripCeiling(cfg, t, cfg.DripBaseCreature);
                var i = TriuneEarning.DripCeiling(cfg, t, cfg.DripBaseItem);
                if (next.MinWieldCreature > 0 && next.MinWieldCreature != c)
                    Msg($"  WARNING: T{t} rewards end at Creature {c:N0}, but T{t + 1} asks for {next.MinWieldCreature:N0} (weapon scaling MinWieldCreature).");
                if (next.MinWieldAugs > 0 && next.MinWieldAugs != i)
                    Msg($"  WARNING: T{t} rewards end at Item {i:N0}, but T{t + 1} asks for {next.MinWieldAugs:N0} (weapon scaling MinWieldAugs).");
                if (next.MinWieldLife > 0 && next.MinWieldLife != i)
                    Msg($"  WARNING: T{t} rewards end at Life {i:N0}, but T{t + 1} asks for {next.MinWieldLife:N0} (weapon scaling MinWieldLife).");
                // the attack school gate (every tier since 2026-11-22): a tier's school gems should end where the next tier asks
                var g = TriuneEarning.GemCeiling(cfg, t);
                if (next.MinWieldSkillCharm > 0 && next.MinWieldSkillCharm != g)
                    Msg($"  WARNING: T{t} school gems end at {g:N0}, but T{t + 1} asks for {next.MinWieldSkillCharm:N0} attack school augs (weapon scaling minwieldskillcharm).");
            }
        }

        /// <summary>
        /// [[ZCTR]] (2026-11-02, the plugin's Triune page): on=0|1|gems=0|1|base=N|day=0-6|hour=0-23|tz=id|next=text|
        /// t=tier~floor~ceiling~slow~fast~points~gemslow~gemfast;... - fields by NAME, numbers invariant; tz ids hold none of | ~ ;
        /// </summary>
        private static string BuildTriunePayload(TriuneEarning.Config cfg)
        {
            static string N(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
            static string Row(TriuneTierRow r) => string.Join("~", r.Tier.ToString(CultureInfo.InvariantCulture), N(r.FloorKph), N(r.CeilingKph),
                N(r.SlowWeeks), N(r.FastWeeks), r.Points.ToString(CultureInfo.InvariantCulture), N(r.GemSlowWeeks), N(r.GemFastWeeks));
            // t= stays T15+ (what the plugin page has always read); the T11-T14 drip rows ride at the END as dt= (append-only)
            var tiers = string.Join(";", cfg.Tiers.Where(r => r.Tier >= TriuneEarning.MinTier).Select(Row));
            var dripTiers = string.Join(";", cfg.Tiers.Where(r => TriuneEarning.IsDripTier(r.Tier)).Select(Row));
            return "[[ZCTR]]" + string.Join("|",
                "on=" + (cfg.Enabled ? "1" : "0"),
                "gems=" + (cfg.GemsEnabled ? "1" : "0"),
                "base=" + cfg.GemBaseCap.ToString(CultureInfo.InvariantCulture),
                "day=" + cfg.ResetDay.ToString(CultureInfo.InvariantCulture),
                "hour=" + cfg.ResetHour.ToString(CultureInfo.InvariantCulture),
                "tz=" + (cfg.ResetTimeZone ?? "UTC").Replace("|", "").Replace("~", "").Replace(";", ""),
                "next=" + TriuneEarning.NextResetText(cfg).Replace("|", " "),
                "t=" + tiers,
                "drip=" + (cfg.DripEnabled ? "1" : "0"),
                "dbase=" + cfg.DripBaseCreature.ToString(CultureInfo.InvariantCulture) + "~" + cfg.DripBaseItem.ToString(CultureInfo.InvariantCulture),
                "dt=" + dripTiers);
        }

        private static bool TryParseDay(string s, out DayOfWeek day)
        {
            day = DayOfWeek.Friday;
            if (int.TryParse(s, out var n) && n >= 0 && n <= 6) { day = (DayOfWeek)n; return true; }
            foreach (DayOfWeek d in Enum.GetValues(typeof(DayOfWeek)))
                if (d.ToString().StartsWith(s, StringComparison.OrdinalIgnoreCase) && s.Length >= 2) { day = d; return true; }
            return false;
        }

        private static void ShowTriune(TriuneEarning.Config cfg, Action<string> Msg)
        {
            Msg($"Hunting rewards T15-T25 ('on' / 'off'): {(cfg.Enabled ? "ON" : "OFF")}. T11-T14 ('drip on' / 'drip off'): {(cfg.DripEnabled ? "ON" : "OFF")}. Own-tier kills only, top damager; each reward +1 to ONE of Creature / Item / Life at random (Points rolls per school per tier), straight onto the aug count, up to the tier's end.");
            Msg($"  Ladder from Creature {cfg.DripBaseCreature:N0} / Item-Life {cfg.DripBaseItem:N0} at T11 ('dripbase'). Login move of the retired Triune / charm counts: {(cfg.MoveOff ? "OFF" : "ON")}{(AugMove.IsUndoAtStartArmed() ? " - the move UNDO is ARMED for the next start" : "")}.");
            Msg($"Attack school gems: {(cfg.GemsEnabled ? "ON" : "OFF")}. On the top damager's corpse, a roll per school at the tier's floor (T15+: {cfg.GemBaseCap:N0}) and under its ceiling. T11-T14 gems need drip AND gems on.");
            Msg($"  Week turns {(DayOfWeek)cfg.ResetDay} {cfg.ResetHour:00}:00 {cfg.ResetTimeZone} (next: {TriuneEarning.NextResetText(cfg)}).");
            foreach (var r in cfg.Tiers)
                Msg(DescribeTriuneTier(cfg, r));
        }

        private static string DescribeTriuneTier(TriuneEarning.Config cfg, TriuneTierRow r)
        {
            var done = $"ends at Creature {TriuneEarning.DripCeiling(cfg, r.Tier, cfg.DripBaseCreature):N0} / Item-Life {TriuneEarning.DripCeiling(cfg, r.Tier, cfg.DripBaseItem):N0}";
            return $"  T{r.Tier}: {r.Points} rolls per school, {done} | floor {r.FloorKph:0.#} kph = {r.SlowWeeks:0.#} wk"
                + $" (1 reward per {TriuneEarning.KillsPerReward(TriuneEarning.Rate1(r)):N0} kills to {TriuneEarning.Band1Kills(r):N0}/wk)"
                + $" | ceiling {r.CeilingKph:0.#} kph = {r.FastWeeks:0.#} wk (1 reward per {TriuneEarning.KillsPerReward(TriuneEarning.Rate2(r)):N0} kills to"
                + $" {TriuneEarning.Band1Kills(r) + TriuneEarning.Band2Kills(r):N0}/wk)"
                + $" | gems {r.GemSlowWeeks:0.#} / {r.GemFastWeeks:0.#} wk: 1 per {TriuneEarning.KillsPerPoint(TriuneEarning.GemChance1(r))}"
                + $" then 1 per {TriuneEarning.KillsPerPoint(TriuneEarning.GemChance2(r))} kills per school, school ceiling {TriuneEarning.GemCeiling(cfg, r.Tier):N0}";
        }
    }
}
