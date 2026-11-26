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
    /// /killrate (owner 2026-11-26, admins only): T11+ kills per character per clock hour, from the in-memory counters
    /// (KillRateTracker). Read-only: it changes nothing and reads no database. Times are UTC. The portal's Kill Rates page
    /// shows the same numbers as a table.
    /// </summary>
    public static class KillRateCommands
    {
        private const int DefaultOverviewHours = 6;
        private const int DefaultCharacterHours = 12;

        /// <summary>
        /// /killrate [hours] | /killrate &lt;character name&gt; [hours] | /killrate tier &lt;N&gt; [hours]. A trailing number is the
        /// hours (1-48); anything else is a character name, matched exactly or by a unique prefix.
        /// </summary>
        /// <param name="session">The admin's session, or null at the server console.</param>
        /// <param name="parameters">The command's words.</param>
        [CommandHandler("killrate", AccessLevel.Admin, CommandHandlerFlag.None,
            "T11+ kills per hour per character, from the in-memory counters (read-only, times UTC).",
            "/killrate [hours] - everyone with T11+ kills in the last hours (default 6)\n" +
            "/killrate <character name> [hours] - that character hour by hour (default 12)\n" +
            "/killrate tier <N> [hours] - everyone whose own tier is N (default 6)")]
        public static void HandleKillRate(Session session, params string[] parameters)
        {
            // every line goes to the AC client: format numbers and day names the invariant way ("1,234", "Thu"), never a host
            // culture that groups with U+202F or names days in another script
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                Run(session, parameters);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        /// <summary>The command itself, under the invariant culture (see HandleKillRate).</summary>
        private static void Run(Session session, string[] parameters)
        {
            void Msg(string line) => CommandHandlerHelper.WriteOutputInfo(session, line);

            var args = (parameters ?? Array.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            bool BadHours(int? value)
            {
                if (!value.HasValue || (value >= 1 && value <= KillRateStore.HoursKept))
                    return false;
                Msg($"Hours must be 1 to {KillRateStore.HoursKept} (the counters keep {KillRateStore.HoursKept} clock hours).");
                return true;
            }

            // /killrate tier <N> [hours] - only when a number follows, so a character named "Tier ..." can still be looked up
            if (args.Count >= 2 && args[0].Equals("tier", StringComparison.OrdinalIgnoreCase) && TryInt(args[1], out var tier))
            {
                const string tierUsage = "Usage: /killrate tier <N> [hours]";
                if (args.Count > 3)
                {
                    Msg(tierUsage);
                    return;
                }

                int? tierHours = null;
                if (args.Count == 3)
                {
                    if (!TryInt(args[2], out var th))
                    {
                        Msg(tierUsage);
                        return;
                    }
                    tierHours = th;
                }

                if (!BadHours(tierHours))
                    Overview(Msg, tierHours ?? DefaultOverviewHours, tier);
                return;
            }

            // a trailing number is the hours, everything before it the name (names have spaces)
            int? hours = null;
            if (args.Count > 0 && TryInt(args[^1], out var h))
            {
                hours = h;
                args.RemoveAt(args.Count - 1);
            }
            if (BadHours(hours))
                return;

            if (args.Count == 0)
            {
                Overview(Msg, hours ?? DefaultOverviewHours, null);
                return;
            }

            Character(Msg, string.Join(" ", args), hours ?? DefaultCharacterHours);
        }

        /// <summary>One line per character: own tier, average own-tier kills per full hour ("~" when it rests on fewer than
        /// MinSteadyHours hours), own-tier share, main kill style, and this hour so far.</summary>
        private static void Overview(Action<string> msg, int hours, int? tier)
        {
            if (tier.HasValue && (tier < KillRateStore.MinTier || tier > KillRateStore.MaxTier))
            {
                msg($"Tier must be {KillRateStore.MinTier} to {KillRateStore.MaxTier}.");
                return;
            }

            var rows = KillRateTracker.Report(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), hours);
            if (tier.HasValue)
                rows = rows.Where(r => r.OwnTier == tier.Value).ToList();

            var scope = tier.HasValue ? $"own tier T{tier}" : "T11+";
            if (rows.Count == 0)
            {
                msg($"No {scope} kills in the last {hours} hour{(hours == 1 ? "" : "s")}. (The counters start empty after a restart.)");
                return;
            }

            msg($"Kill rates, {scope}, last {hours} hour{(hours == 1 ? "" : "s")} (UTC, this hour included). Avg = own-tier kills per full hour, over hours with 95 pct+ own-tier kills.");
            foreach (var r in rows)
            {
                var avg = !r.AvgOwnPerHour.HasValue
                    ? "avg - (no full clean hour)"
                    : r.Steady
                        ? $"avg {r.AvgOwnPerHour.Value:N0}/h over {r.CleanHours} h"
                        : $"avg ~{r.AvgOwnPerHour.Value:N0}/h over only {r.CleanHours} h";
                var now = r.Hours.LastOrDefault(x => x.Current);
                var thisHour = now != null ? $", this hour {now.Own:N0}" : "";
                var main = r.MainStyle != null ? $", mostly {r.MainStyle} ({Pct((double)r.ByStyle[r.MainStyle] / Math.Max(1, r.TotalKills))})" : "";
                msg($"{r.Name} - {TierText(r.OwnTier)}: {avg}, {Pct(r.OwnShare)} own tier of {r.TotalKills:N0} kills{main}{thisHour}");
            }
        }

        /// <summary>One character hour by hour.</summary>
        private static void Character(Action<string> msg, string name, int hours)
        {
            var all = KillRateTracker.Report(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), hours);
            var match = FindCharacter(all, name, out var ambiguous);
            if (match == null)
            {
                msg(ambiguous.Count > 1
                    ? $"\"{name}\" matches {ambiguous.Count} characters: {string.Join(", ", ambiguous.Take(8).Select(r => r.Name))}. Type more of the name."
                    : $"No T11+ kills by \"{name}\" in the last {hours} hours. (Usage: /killrate <character name> [hours])");
                return;
            }

            msg($"{match.Name} - own tier {TierText(match.OwnTier)}, last {hours} hour{(hours == 1 ? "" : "s")} (UTC):");
            foreach (var row in match.Hours)
            {
                var start = KillRateStore.HourStartUtc(row.Hour);
                var tiers = string.Join(", ", row.ByTier.Reverse().Select(kv => $"T{kv.Key} {kv.Value:N0}"));
                var own = row.OwnTier > 0 ? $"{row.Own:N0} own (T{row.OwnTier})" : "own tier unknown";
                var note = row.Current
                    ? " - running"
                    : row.Part ? $" - part hour ({Clock(row.First)}-{Clock(row.Last)})"
                    : row.Clean ? ""
                    : row.OwnTier <= 0 ? " - own tier unknown"
                    : " - off-tier kills";
                msg($"{start:ddd HH}:00 {own} of {row.Total:N0} [{tiers}]{note}");
            }

            msg(match.AvgOwnPerHour.HasValue
                ? $"Average {match.AvgOwnPerHour.Value:N0} own-tier kills per full hour over {match.CleanHours} hour{(match.CleanHours == 1 ? "" : "s")}; {Pct(match.OwnShare)} of {match.TotalKills:N0} kills were own tier."
                : $"No full hour with 95 pct+ own-tier kills yet; {Pct(match.OwnShare)} of {match.TotalKills:N0} kills were own tier.");
            if (match.ByStyle is { Count: > 0 })
                msg($"Kill styles: {StyleText(match.ByStyle, match.TotalKills)}.");
        }

        /// <summary>"Missile 92 pct, War 8 pct": each style's share of the kills, largest first.</summary>
        private static string StyleText(Dictionary<string, int> byStyle, int total)
            => string.Join(", ", byStyle.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Pct((double)kv.Value / Math.Max(1, total))}"));

        /// <summary>The name exactly as typed first, then exact ignoring a leading + (staff characters), else the one name
        /// starting with it. "Og" finds "Og" even when "+Og" also has kills.</summary>
        internal static KillRateStore.CharacterReport FindCharacter(List<KillRateStore.CharacterReport> rows, string name, out List<KillRateStore.CharacterReport> candidates)
        {
            static string Bare(string s) => (s ?? "").Trim().TrimStart('+');
            var wanted = Bare(name);

            candidates = rows.Where(r => string.Equals(r.Name, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count == 1)
                return candidates[0];

            candidates = rows.Where(r => string.Equals(Bare(r.Name), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count == 1)
                return candidates[0];

            candidates = rows.Where(r => Bare(r.Name).StartsWith(wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static bool TryInt(string s, out int value) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        /// <summary>"T15", or "own tier unknown" for 0.</summary>
        private static string TierText(int tier) => tier > 0 ? $"T{tier}" : "own tier unknown";

        private static string Pct(double share) => $"{Math.Round(share * 100):0} pct";

        /// <summary>UTC HH:mm, or "-" for a time not set yet.</summary>
        private static string Clock(long unix) => unix == long.MaxValue || unix == long.MinValue ? "-" : DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
