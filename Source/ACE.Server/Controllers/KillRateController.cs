using System;
using System.Linq;

using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    /// <summary>
    /// Kill Rates page (owner 2026-11-26): T11+ kills per character per clock hour from the in-memory counters
    /// (KillRateTracker), the same numbers as /killrate. Read-only, no database. Gated by the "kill-rates" portal page level
    /// (admins by default).
    /// </summary>
    [ApiController]
    [Route("api/audit/kill-rates")]
    public class KillRateController : BaseController
    {
        /// <summary>Every character with T11+ kills in the last <paramref name="hours"/> clock hours, with their hours.</summary>
        /// <param name="hours">The window, 1-48 clock hours including the running one (default 24).</param>
        /// <returns>403 without page access, 400 for an hours value out of range, else the report.</returns>
        [HttpGet]
        public IActionResult Get([FromQuery] int hours = 24)
        {
            if (!HasPortalAccess(PortalPages.KillRates))
                return Forbid();

            if (hours < 1 || hours > KillRateStore.HoursKept)
                return BadRequest(new { Message = $"hours must be 1 to {KillRateStore.HoursKept}.", Code = "invalid_hours" });

            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var nowHour = KillRateStore.HourOf(now);
                var report = KillRateTracker.Report(now, hours);   // the same now as HourStarts, so the hours line up

                return Ok(new
                {
                    GeneratedAt = now,
                    Hours = hours,
                    HoursKept = KillRateStore.HoursKept,
                    PartHourSlackMinutes = KillRateStore.PartHourSlackSeconds / 60,
                    CleanOwnShare = KillRateStore.CleanOwnShare,
                    MinSteadyHours = KillRateStore.MinSteadyHours,
                    // every clock hour of the window, oldest first, so the page can show hours nobody killed in
                    HourStarts = Enumerable.Range(0, hours).Select(i => (nowHour - hours + 1 + i) * KillRateStore.HourSeconds).ToList(),
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
