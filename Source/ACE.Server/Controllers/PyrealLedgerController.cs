using System;

using ACE.Server.Managers;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    /// <summary>
    /// Admin Pyreal Ledger page: who gained pyreals, from where, and which gains do not add up.
    /// Read-only; gated by the "pyreal-ledger" portal page level.
    /// </summary>
    [ApiController]
    [Route("api/audit/pyreals")]
    public class PyrealLedgerController : BaseController
    {
        private IActionResult Run(Func<object> query)
        {
            if (!HasPortalAccess(PortalPages.PyrealLedger))
                return Forbid();

            try
            {
                return Ok(query());
            }
            catch (Exception ex)
            {
                var correlationId = Guid.NewGuid().ToString();
                Log.Error($"[Correlation ID: {correlationId}] Pyreal ledger query failed", ex);
                return StatusCode(500, new { Message = "The pyreal ledger query failed.", CorrelationId = correlationId });
            }
        }

        [HttpGet("overview")]
        public IActionResult GetOverview([FromQuery] int days = 7) => Run(() => PyrealLedgerReports.GetOverview(days));

        [HttpGet("suspects")]
        public IActionResult GetSuspects([FromQuery] int days = 30, [FromQuery] int limit = 100) => Run(() => PyrealLedgerReports.GetSuspects(days, limit));

        [HttpGet("earners")]
        public IActionResult GetEarners([FromQuery] int days = 7, [FromQuery] string by = "account", [FromQuery] int limit = 100)
            => Run(() => PyrealLedgerReports.GetEarners(days, !string.Equals(by, "character", StringComparison.OrdinalIgnoreCase), limit));

        [HttpGet("vendors")]
        public IActionResult GetVendors([FromQuery] int days = 30) => Run(() => PyrealLedgerReports.GetVendors(days));

        [HttpGet("vendors/{wcid}")]
        public IActionResult GetVendorSellers(uint wcid, [FromQuery] int days = 30) => Run(() => PyrealLedgerReports.GetVendorSellers(wcid, days));

        [HttpGet("items")]
        public IActionResult GetItemSales([FromQuery] int days = 30, [FromQuery] int limit = 200) => Run(() => PyrealLedgerReports.GetItemSales(days, limit));

        [HttpGet("items/{wcid}")]
        public IActionResult GetItemSellers(uint wcid, [FromQuery] int days = 30) => Run(() => PyrealLedgerReports.GetItemSellers(wcid, days));

        [HttpGet("npcs")]
        public IActionResult GetNpcs([FromQuery] int days = 30, [FromQuery] int limit = 200) => Run(() => PyrealLedgerReports.GetNpcs(days, limit));

        [HttpGet("npcs/{wcid}")]
        public IActionResult GetNpcReceivers(uint wcid, [FromQuery] int days = 30) => Run(() => PyrealLedgerReports.GetNpcReceivers(wcid, days));

        [HttpGet("flags")]
        public IActionResult GetFlags([FromQuery] int days = 30, [FromQuery] string flag = null, [FromQuery] uint? charId = null, [FromQuery] uint? accountId = null, [FromQuery] int limit = 200)
            => Run(() => PyrealLedgerReports.GetFlags(days, flag, charId, accountId, limit));

        [HttpGet("characters/{charId}")]
        public IActionResult GetCharacter(uint charId, [FromQuery] int days = 30) => Run(() => PyrealLedgerReports.GetCharacter(charId, days));

        [HttpGet("accounts/{accountId}")]
        public IActionResult GetAccount(uint accountId, [FromQuery] int days = 30) => Run(() => PyrealLedgerReports.GetAccount(accountId, days));

        [HttpGet("search")]
        public IActionResult Search([FromQuery] string q) => Run(() => PyrealLedgerReports.Search(q));
    }
}
