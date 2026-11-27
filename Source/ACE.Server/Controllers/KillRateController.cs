using System;
using System.Linq;

using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    /// <summary>
    /// Kill Rates page (owner 2026-11-26): T11+ kills per character per clock hour from the in-memory counters
    /// (KillRateTracker, the same numbers as /killrate), or with ?days= per UTC day from the saved kill_rate_hour history
    /// plus memory (KillRateHistory). Read-only: the hourly windows touch no database, the daily ones one SELECT. Gated by
    /// the "kill-rates" portal page level (admins by default).
    /// </summary>
    [ApiController]
    [Route("api/audit/kill-rates")]
    public class KillRateController : BaseController
    {
        /// <summary>Longest history window, in UTC days.</summary>
        public const int MaxDays = 90;

        /// <summary>
        /// Every character with T11+ kills in the window, with its rows: the last <paramref name="hours"/> clock hours from
        /// memory (one row per hour), or with <paramref name="days"/> the last that many UTC days from the kill_rate_hour history
        /// plus memory (one row per day).
        /// </summary>
        /// <param name="hours">The hourly window, 1-48 clock hours including the running one (default 24).</param>
        /// <param name="days">A daily window instead, 1-90 UTC days including today; 0 (default) = hourly.</param>
        /// <returns>403 without page access, 400 for a window out of range, 500 when the history can't be read, else the report.</returns>
        [HttpGet]
        public IActionResult Get([FromQuery] int hours = 24, [FromQuery] int days = 0)
        {
            if (!HasPortalAccess(PortalPages.KillRates))
                return Forbid();

            if (days < 0 || days > MaxDays)
                return BadRequest(new { Message = $"days must be 1 to {MaxDays} (or 0 for hours).", Code = "invalid_days" });
            if (days == 0 && (hours < 1 || hours > KillRateStore.HoursKept))
                return BadRequest(new { Message = $"hours must be 1 to {KillRateStore.HoursKept}.", Code = "invalid_hours" });

            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var nowHour = KillRateStore.HourOf(now);
                // the same now for the report and its column starts, so they line up
                var report = days > 0 ? KillRateTracker.DayReport(now, days) : KillRateTracker.Report(now, hours);
                var bucket = days > 0 ? 24 * KillRateStore.HourSeconds : KillRateStore.HourSeconds;
                var count = days > 0 ? days : hours;
                var newest = days > 0 ? nowHour / 24 * 24 * KillRateStore.HourSeconds : nowHour * KillRateStore.HourSeconds;

                return Ok(new
                {
                    GeneratedAt = now,
                    Hours = days > 0 ? days * 24 : hours,
                    Days = days,
                    // the width of one column: 3600 (hours) or 86400 (UTC days)
                    BucketSeconds = bucket,
                    // days of history kept (kill_rate_history_days; 0 = all), so the page can say how far back 7d / 30d can go
                    HistoryDays = ServerConfig.kill_rate_history_days.Value,
                    HoursKept = KillRateStore.HoursKept,
                    PartHourSlackMinutes = KillRateStore.PartHourSlackSeconds / 60,
                    CleanOwnShare = KillRateStore.CleanOwnShare,
                    MinSteadyHours = KillRateStore.MinSteadyHours,
                    // every column (clock hour or UTC day) of the window, oldest first, so the page can show ones nobody killed in
                    HourStarts = Enumerable.Range(0, count).Select(i => newest - (count - 1 - i) * bucket).ToList(),
                    Characters = report.Select(c => new
                    {
                        c.Guid,
                        c.Name,
                        c.OwnTier,
                        c.AvgOwnPerHour,
                        c.CleanHours,
                        c.Steady,
                        // logged in right now (the "Playing now" filter): a dictionary lookup per character, no DB
                        Online = PlayerManager.GetOnlinePlayer(c.Guid) != null,
                        c.OwnKills,
                        c.TotalKills,
                        c.OwnShare,
                        // kills per style over the window ("Missile": 700), and the style with the most
                        c.MainStyle,
                        c.ByStyle,
                        Hours = c.Hours.Select(h => new
                        {
                            h.HourStart,
                            h.OwnTier,
                            h.Own,
                            h.Total,
                            ByTier = h.ByTier.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                            h.ByStyle,
                            h.First,
                            h.Last,
                            h.Current,
                            h.Part,
                            h.Clean,
                            h.ShowsTotal,
                            // day rows only (null on hour rows): that day's clean hours and their average own-tier kills
                            h.CleanHours,
                            h.AvgOwnPerHour,
                        }).ToList(),
                    }).ToList(),
                });
            }
            catch (Exception ex)
            {
                var correlationId = Guid.NewGuid().ToString();
                Log.Error($"[Correlation ID: {correlationId}] Kill rate report failed", ex);
                return StatusCode(500, new { Message = "The kill rate report failed.", CorrelationId = correlationId });
            }
        }
    }
}
