using System;

using ACE.Server.Managers;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    /// <summary>
    /// Admin Economy page: supply of every currency over time, what creates and removes it, and a prompt for Claude.
    /// Read-only apart from asking for a supply snapshot; gated by the "economy" portal page level.
    /// </summary>
    [ApiController]
    [Route("api/audit/economy")]
    public class EconomyController : BaseController
    {
        private IActionResult Run(Func<object> query)
        {
            if (!HasPortalAccess(PortalPages.Economy))
                return Forbid();

            try
            {
                var result = query();
                return result == null ? NotFound(new { Message = "Unknown currency." }) : Ok(result);
            }
            catch (Exception ex)
            {
                var correlationId = Guid.NewGuid().ToString();
                Log.Error($"[Correlation ID: {correlationId}] Economy query failed", ex);
                return StatusCode(500, new { Message = "The economy query failed.", CorrelationId = correlationId });
            }
        }

        /// <summary>
        /// The page's "active in the last N days" filter for the per-account figures: none given = economy_active_days;
        /// otherwise one of the offered windows (so a caller cannot fill the caches with arbitrary windows).
        /// </summary>
        private IActionResult WithActiveDays(int? activeDays, Func<int, object> query)
        {
            var defaultDays = ActiveWindow.EconomyDefault;
            if (!ActiveWindow.TryResolve(activeDays, defaultDays, out var resolved))
            {
                if (!HasPortalAccess(PortalPages.Economy))
                    return Forbid();
                return BadRequest(new { Message = "Unsupported activity window.", Options = ActiveWindow.Options(defaultDays) });
            }
            return Run(() => query(resolved));
        }

        [HttpGet("overview")]
        public IActionResult GetOverview([FromQuery] int days = 30, [FromQuery] int? activeDays = null) =>
            WithActiveDays(activeDays, a => EconomyReports.GetOverview(days, a));

        [HttpGet("currencies/{key}")]
        public IActionResult GetCurrency(string key, [FromQuery] int days = 30, [FromQuery] int? activeDays = null) =>
            WithActiveDays(activeDays, a => EconomyReports.GetCurrency(key, days, a));

        [HttpGet("prompt")]
        public IActionResult GetPrompt([FromQuery] string currency = "all", [FromQuery] int days = 30, [FromQuery] int? activeDays = null) =>
            WithActiveDays(activeDays, a => EconomyReports.GetPrompt(currency, days, a));

        [HttpGet("stalled")]
        public IActionResult GetStalled([FromQuery] int days = 30, [FromQuery] int stallDays = 30, [FromQuery] int minEnl = 10, [FromQuery] int maxEnl = 0,
            [FromQuery] string currency = "all", [FromQuery] long minExport = 0)
            => Run(() => EconomyStalledReport.Get(days, stallDays, minEnl, maxEnl, currency, minExport));

        [HttpPost("snapshot")]
        public IActionResult TakeSnapshot() => Run(() =>
        {
            var started = EconomyLedger.StartSnapshot("portal");
            return new
            {
                Started = started,
                Message = started
                    ? "Supply snapshot started. It takes up to a minute; reload the page after that."
                    : (EconomyLedger.SnapshotRunning ? "A supply snapshot is already running." : "The economy ledger is not running on this server."),
            };
        });
    }
}
