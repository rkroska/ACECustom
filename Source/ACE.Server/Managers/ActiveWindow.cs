using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Managers
{
    /// <summary>
    /// The "active in the last N days" filter shared by the leaderboards and the Economy page. An account is active
    /// when one of its characters logged into the game within N days (character.last_Login_Timestamp; the web portal
    /// sign-in does not count). 0 means everyone.
    /// </summary>
    public static class ActiveWindow
    {
        /// <summary>
        /// Longest window accepted from a setting (ten years). Larger values are capped at this and still leave out
        /// accounts idle for longer; only 0 (or less) shows every account.
        /// </summary>
        public const int MaxDays = 3650;

        /// <summary>The windows a web viewer can pick. The configured default is always allowed as well.</summary>
        public static readonly IReadOnlyList<int> Presets = new[] { 7, 14, 30, 60, 90, 0 };

        /// <summary>A setting value as a window: negative = 0 (everyone), capped at <see cref="MaxDays"/>.</summary>
        public static int FromSetting(long value) => (int)Math.Clamp(value, 0, MaxDays);

        /// <summary>The in-game /top window and the web leaderboards' starting filter.</summary>
        public static int LeaderboardDefault => FromSetting(ServerConfig.leaderboard_active_days.Value);

        /// <summary>The Economy page's starting filter.</summary>
        public static int EconomyDefault => FromSetting(ServerConfig.economy_active_days.Value);

        /// <summary>The choices offered on a page: the presets plus the configured default, everyone last.</summary>
        public static List<int> Options(int defaultDays) =>
            Presets.Append(defaultDays).Distinct().OrderBy(d => d == 0 ? int.MaxValue : d).ToList();

        /// <summary>
        /// The window for a web request: none asked = the default; otherwise one of <see cref="Options"/>. Other
        /// values are refused so a caller cannot make the server cache (and query) an unbounded number of windows.
        /// </summary>
        public static bool TryResolve(int? requested, int defaultDays, out int days)
        {
            days = requested ?? defaultDays;
            return !requested.HasValue || Options(defaultDays).Contains(requested.Value);
        }

        /// <summary>Plain ASCII description for in-game text and page labels.</summary>
        public static string Describe(int days) =>
            days <= 0 ? "all accounts" : days == 1 ? "accounts active in the last day" : $"accounts active in the last {days} days";
    }
}
