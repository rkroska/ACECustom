using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Server.Managers;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/audit")]
    public class AuditLogController : BaseController
    {
        private const int MaxTransferDays = 365;
        private const int MaxLoginDays = 90;

        [HttpGet("transfers")]
        public IActionResult GetTransfers([FromQuery] AuditLogSearchCriteria criteria)
        {
            if (!HasPortalAccess(PortalPages.AuditLog))
                return Forbid();

            try
            {
                criteria.Normalize();
                criteria.Days = Math.Min(criteria.Days, MaxTransferDays);
                var result = DatabaseManager.Shard.BaseDatabase.SearchTransferLogs(criteria);
                return Ok(MapPaged(result, MapTransfer));
            }
            catch (Exception ex)
            {
                return AuditError(ex);
            }
        }

        [HttpGet("logins")]
        public IActionResult GetLogins([FromQuery] AuditLogSearchCriteria criteria)
        {
            if (!HasPortalAccess(PortalPages.AuditLog))
                return Forbid();

            try
            {
                criteria.Normalize();
                criteria.Days = Math.Min(criteria.Days, MaxLoginDays);
                var result = DatabaseManager.Shard.BaseDatabase.SearchCharTrackerLogins(criteria);
                return Ok(MapPaged(result, MapLogin));
            }
            catch (Exception ex)
            {
                return AuditError(ex);
            }
        }

        [HttpGet("summaries")]
        public IActionResult GetSummaries([FromQuery] AuditLogSearchCriteria criteria)
        {
            if (!HasPortalAccess(PortalPages.AuditLog))
                return Forbid();

            try
            {
                criteria.Normalize();
                criteria.Days = Math.Min(criteria.Days, MaxTransferDays);
                var result = DatabaseManager.Shard.BaseDatabase.SearchTransferSummaries(criteria);
                return Ok(MapPaged(result, MapSummary));
            }
            catch (Exception ex)
            {
                return AuditError(ex);
            }
        }

        private static PagedResultDto<TOut> MapPaged<TIn, TOut>(PagedResult<TIn> source, Func<TIn, TOut> map)
        {
            return new PagedResultDto<TOut>
            {
                Items = source.Items.Select(map).ToList(),
                TotalCount = source.TotalCount,
                Page = source.Page,
                PageSize = source.PageSize,
                TotalPages = source.TotalPages
            };
        }

        private static TransferLogDto MapTransfer(TransferLog t) => new()
        {
            Id = t.Id,
            TransferType = t.TransferType,
            FromPlayerName = t.FromPlayerName,
            FromPlayerAccount = t.FromPlayerAccount,
            ToPlayerName = t.ToPlayerName,
            ToPlayerAccount = t.ToPlayerAccount,
            ItemName = t.ItemName,
            Quantity = t.Quantity,
            Timestamp = t.Timestamp,
            FromAccountCreatedDate = t.FromAccountCreatedDate,
            ToAccountCreatedDate = t.ToAccountCreatedDate,
            FromCharacterCreatedDate = t.FromCharacterCreatedDate,
            ToCharacterCreatedDate = t.ToCharacterCreatedDate,
            AdditionalData = t.AdditionalData,
            FromPlayerIP = t.FromPlayerIP,
            ToPlayerIP = t.ToPlayerIP
        };

        private static CharTrackerLoginDto MapLogin(CharTracker c) => new()
        {
            Id = c.Id,
            CharacterId = c.CharacterId,
            AccountName = c.AccountName,
            CharacterName = c.CharacterName,
            LoginIP = c.LoginIP,
            LoginTimestamp = c.LoginTimestamp,
            ConnectionDuration = c.ConnectionDuration,
            Landblock = c.Landblock
        };

        private static TransferSummaryDto MapSummary(TransferSummary s) => new()
        {
            Id = s.Id,
            FromPlayerName = s.FromPlayerName,
            FromPlayerAccount = s.FromPlayerAccount,
            ToPlayerName = s.ToPlayerName,
            ToPlayerAccount = s.ToPlayerAccount,
            TransferType = s.TransferType,
            TotalTransfers = s.TotalTransfers,
            TotalQuantity = s.TotalQuantity,
            TotalValue = s.TotalValue,
            SuspiciousTransfers = s.SuspiciousTransfers,
            IsSuspicious = s.IsSuspicious,
            FirstTransfer = s.FirstTransfer,
            LastTransfer = s.LastTransfer
        };

        private IActionResult AuditError(Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString();
            Log.Error($"[Correlation ID: {correlationId}] Audit log query failed", ex);
            return StatusCode(500, new
            {
                Message = "An unexpected error occurred while querying audit data.",
                CorrelationId = correlationId
            });
        }

        public class PagedResultDto<T>
        {
            public List<T> Items { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
        }

        public class TransferLogDto
        {
            public int Id { get; set; }
            public string TransferType { get; set; }
            public string FromPlayerName { get; set; }
            public string FromPlayerAccount { get; set; }
            public string ToPlayerName { get; set; }
            public string ToPlayerAccount { get; set; }
            public string ItemName { get; set; }
            public long Quantity { get; set; }
            public DateTime Timestamp { get; set; }
            public DateTime? FromAccountCreatedDate { get; set; }
            public DateTime? ToAccountCreatedDate { get; set; }
            public DateTime? FromCharacterCreatedDate { get; set; }
            public DateTime? ToCharacterCreatedDate { get; set; }
            public string AdditionalData { get; set; }
            public string FromPlayerIP { get; set; }
            public string ToPlayerIP { get; set; }
        }

        public class CharTrackerLoginDto
        {
            public int Id { get; set; }
            public uint CharacterId { get; set; }
            public string AccountName { get; set; }
            public string CharacterName { get; set; }
            public string LoginIP { get; set; }
            public DateTime LoginTimestamp { get; set; }
            public int ConnectionDuration { get; set; }
            public string Landblock { get; set; }
        }

        public class TransferSummaryDto
        {
            public int Id { get; set; }
            public string FromPlayerName { get; set; }
            public string FromPlayerAccount { get; set; }
            public string ToPlayerName { get; set; }
            public string ToPlayerAccount { get; set; }
            public string TransferType { get; set; }
            public int TotalTransfers { get; set; }
            public long TotalQuantity { get; set; }
            public long TotalValue { get; set; }
            public int SuspiciousTransfers { get; set; }
            public bool IsSuspicious { get; set; }
            public DateTime FirstTransfer { get; set; }
            public DateTime LastTransfer { get; set; }
        }
    }
}
