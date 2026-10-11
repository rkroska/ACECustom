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

        [HttpGet("overview")]
        public IActionResult GetOverview([FromQuery] int days = 30) => Run(() => EconomyReports.GetOverview(days));

        [HttpGet("currencies/{key}")]
        public IActionResult GetCurrency(string key, [FromQuery] int days = 30) => Run(() => EconomyReports.GetCurrency(key, days));

        [HttpGet("prompt")]
        public IActionResult GetPrompt([FromQuery] string currency = "all", [FromQuery] int days = 30) => Run(() => EconomyReports.GetPrompt(currency, days));

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
