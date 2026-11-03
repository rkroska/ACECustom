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
            "triune [show] | get | on | off | gems on|off | gembase <n> | reload | reset <day> <hour 0-23> [time zone id...] | "
            + "tier <15-25|all> [floor <kph>] [ceiling <kph>] [slow <weeks>] [fast <weeks>] [points <n>] [gemslow <weeks>] [gemfast <weeks>]";

        private static readonly HashSet<string> TriuneWriteVerbs = new() { "on", "off", "gems", "gembase", "reload", "reset", "tier" };

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
                    ShowTriune(TriuneEarning.Mutate(c => c.GemBaseCap = b), Msg);
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
                        for (var t = TriuneEarning.MinTier; t <= TriuneEarning.MaxTier; t++) tiers.Add(t);
                    else if (int.TryParse(args[2].TrimStart('t', 'T'), out var one) && one >= TriuneEarning.MinTier && one <= TriuneEarning.MaxTier)
                        tiers.Add(one);
                    else
                    { Msg($"Tier must be {TriuneEarning.MinTier}-{TriuneEarning.MaxTier} or all."); return; }

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
        /// The Triune a tier completes at should be what the NEXT tier's hit / wield gate asks (WeaponScaling MinWieldTriune) -
        /// the two are tuned separately, so say so when they part (e.g. points 400 at T15 strands players at 400 of the 500
        /// T16 asks for).
        /// </summary>
        private static void WarnTriuneVsHitGate(TriuneEarning.Config cfg, Action<string> Msg)
        {
            for (var t = TriuneEarning.MinTier; t < TriuneEarning.MaxTier; t++)
            {
                var next = ACE.Server.Managers.WeaponScaling.WeaponScalingManager.GetTier(t + 1);
                var cap = TriuneEarning.TierCap(cfg, t);
                if (next != null && next.MinWieldTriune > 0 && next.MinWieldTriune != cap)
                    Msg($"  WARNING: T{t} completes at {cap:N0} Triune, but T{t + 1} asks for {next.MinWieldTriune:N0} (weapon scaling MinWieldTriune).");
            }
        }

        /// <summary>
        /// [[ZCTR]] (2026-11-02, the plugin's Triune page): on=0|1|gems=0|1|base=N|day=0-6|hour=0-23|tz=id|next=text|
        /// t=tier~floor~ceiling~slow~fast~points~gemslow~gemfast;... - fields by NAME, numbers invariant; tz ids hold none of | ~ ;
        /// </summary>
        private static string BuildTriunePayload(TriuneEarning.Config cfg)
        {
            static string N(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
            var tiers = string.Join(";", cfg.Tiers.Select(r => string.Join("~", r.Tier.ToString(CultureInfo.InvariantCulture), N(r.FloorKph), N(r.CeilingKph),
                N(r.SlowWeeks), N(r.FastWeeks), r.Points.ToString(CultureInfo.InvariantCulture), N(r.GemSlowWeeks), N(r.GemFastWeeks))));
            return "[[ZCTR]]" + string.Join("|",
                "on=" + (cfg.Enabled ? "1" : "0"),
                "gems=" + (cfg.GemsEnabled ? "1" : "0"),
                "base=" + cfg.GemBaseCap.ToString(CultureInfo.InvariantCulture),
                "day=" + cfg.ResetDay.ToString(CultureInfo.InvariantCulture),
                "hour=" + cfg.ResetHour.ToString(CultureInfo.InvariantCulture),
                "tz=" + (cfg.ResetTimeZone ?? "UTC").Replace("|", "").Replace("~", "").Replace(";", ""),
                "next=" + TriuneEarning.NextResetText(cfg).Replace("|", " "),
                "t=" + tiers);
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
            Msg($"Triune earning: {(cfg.Enabled ? "ON" : "OFF")}. Own-tier kills only, top damager, straight onto the character.");
            Msg($"School gems: {(cfg.GemsEnabled ? "ON" : "OFF")}. On the top damager's corpse, a roll per school bought to {cfg.GemBaseCap:N0}+ and under the tier's ceiling.");
            Msg($"  Week turns {(DayOfWeek)cfg.ResetDay} {cfg.ResetHour:00}:00 {cfg.ResetTimeZone} (next: {TriuneEarning.NextResetText(cfg)}).");
            foreach (var r in cfg.Tiers)
                Msg(DescribeTriuneTier(cfg, r));
        }

        private static string DescribeTriuneTier(TriuneEarning.Config cfg, TriuneTierRow r)
        {
            return $"  T{r.Tier}: {r.Points} points, complete at {TriuneEarning.TierCap(cfg, r.Tier):N0} | floor {r.FloorKph:0.#} kph = {r.SlowWeeks:0.#} wk"
                + $" (1 per {TriuneEarning.KillsPerPoint(TriuneEarning.Rate1(r))} kills to {TriuneEarning.Band1Kills(r):N0}/wk)"
                + $" | ceiling {r.CeilingKph:0.#} kph = {r.FastWeeks:0.#} wk (1 per {TriuneEarning.KillsPerPoint(TriuneEarning.Rate2(r))} kills to"
                + $" {TriuneEarning.Band1Kills(r) + TriuneEarning.Band2Kills(r):N0}/wk)"
                + $" | gems {r.GemSlowWeeks:0.#} / {r.GemFastWeeks:0.#} wk: 1 per {TriuneEarning.KillsPerPoint(TriuneEarning.GemChance1(r))}"
                + $" then 1 per {TriuneEarning.KillsPerPoint(TriuneEarning.GemChance2(r))} kills per school, school ceiling {TriuneEarning.GemCeiling(cfg, r.Tier):N0}";
        }
    }
}
