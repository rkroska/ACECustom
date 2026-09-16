using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Web.Controllers;
using ACE.Server.WorldObjects;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PetNamingController : BaseController
    {
        private const int StatusPending = 0;
        private const int StatusApproved = 1;
        private const int StatusDenied = 2;

        public class PetNameRequestDto
        {
            public long Id { get; set; }
            public long CharacterId { get; set; }
            public string CharacterName { get; set; }
            public uint PetGuid { get; set; }
            public string OldName { get; set; }
            public string RequestedName { get; set; }
            public int Status { get; set; }
            public string ReviewNote { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? ReviewedAt { get; set; }
            public string ReviewedBy { get; set; }
        }

        public class DenyRequestDto
        {
            public string Reason { get; set; }
        }

        private bool HasAccess() => IsPortalAdmin || HasPortalAccess(PortalPages.PetNaming);

        [HttpGet("requests")]
        public IActionResult GetRequests([FromQuery] int limit = 100)
        {
            if (!HasAccess())
                return Forbid();

            try
            {
                var take = Math.Clamp(limit, 1, 500);
                var results = new List<PetNameRequestDto>();

                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText =
                    "SELECT `id`, `character_id`, `character_name`, `pet_guid`, `old_name`, `requested_name`, " +
                    "`status`, `review_note`, `created_at`, `reviewed_at`, `reviewed_by` " +
                    "FROM `pet_name_requests` ORDER BY (`status` = 0) DESC, `created_at` DESC LIMIT " + take;

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new PetNameRequestDto
                    {
                        Id = reader.GetInt64(0),
                        CharacterId = reader.GetInt64(1),
                        CharacterName = reader.GetString(2),
                        PetGuid = (uint)reader.GetInt64(3),
                        OldName = reader.GetString(4),
                        RequestedName = reader.GetString(5),
                        Status = reader.GetByte(6),
                        ReviewNote = reader.IsDBNull(7) ? null : reader.GetString(7),
                        CreatedAt = reader.GetDateTime(8),
                        ReviewedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                        ReviewedBy = reader.IsDBNull(10) ? null : reader.GetString(10),
                    });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                Log.Error("[PetNaming] Failed to load pet name requests", ex);
                return StatusCode(500, new { message = "Failed to load pet name requests." });
            }
        }

        [HttpPost("approve/{id}")]
        public IActionResult Approve(long id)
        {
            if (!HasAccess())
                return Forbid();

            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();

                uint petGuid;
                string requestedName;
                int status;

                using (var selectCmd = con.CreateCommand())
                {
                    selectCmd.CommandText = "SELECT `pet_guid`, `requested_name`, `status` FROM `pet_name_requests` WHERE `id` = @id LIMIT 1";
                    AddParam(selectCmd, "@id", id);
                    using var reader = selectCmd.ExecuteReader();
                    if (!reader.Read())
                        return NotFound(new { message = "Pet name request not found." });

                    petGuid = (uint)reader.GetInt64(0);
                    requestedName = reader.GetString(1);
                    status = reader.GetByte(2);
                }

                if (status != StatusPending)
                    return BadRequest(new { message = "This request has already been reviewed." });

                if (!TryRenamePet(petGuid, requestedName))
                    return StatusCode(500, new { message = "Failed to locate or rename the target pet." });

                using (var updateCmd = con.CreateCommand())
                {
                    updateCmd.CommandText =
                        "UPDATE `pet_name_requests` SET `status` = @status, `reviewed_at` = UTC_TIMESTAMP(), `reviewed_by` = @reviewer " +
                        "WHERE `id` = @id";
                    AddParam(updateCmd, "@status", StatusApproved);
                    AddParam(updateCmd, "@reviewer", User.Identity?.Name ?? CurrentAccountId?.ToString() ?? "unknown");
                    AddParam(updateCmd, "@id", id);
                    updateCmd.ExecuteNonQuery();
                }

                return Ok(new { message = $"Pet renamed to \"{requestedName}\"." });
            }
            catch (Exception ex)
            {
                Log.Error($"[PetNaming] Failed to approve request {id}", ex);
                return StatusCode(500, new { message = "Failed to approve pet name request." });
            }
        }

        [HttpPost("deny/{id}")]
        public IActionResult Deny(long id, [FromBody] DenyRequestDto body)
        {
            if (!HasAccess())
                return Forbid();

            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();

                using var cmd = con.CreateCommand();
                cmd.CommandText =
                    "UPDATE `pet_name_requests` SET `status` = @status, `review_note` = @note, " +
                    "`reviewed_at` = UTC_TIMESTAMP(), `reviewed_by` = @reviewer " +
                    "WHERE `id` = @id AND `status` = @pending";
                AddParam(cmd, "@status", StatusDenied);
                var reason = string.IsNullOrWhiteSpace(body?.Reason) ? null : body.Reason.Trim();
                if (reason?.Length > 255)
                    reason = reason.Substring(0, 255);
                AddParam(cmd, "@note", (object)reason ?? DBNull.Value);
                AddParam(cmd, "@reviewer", User.Identity?.Name ?? CurrentAccountId?.ToString() ?? "unknown");
                AddParam(cmd, "@id", id);
                AddParam(cmd, "@pending", StatusPending);

                var rows = cmd.ExecuteNonQuery();
                if (rows == 0)
                    return NotFound(new { message = "Pet name request not found or already reviewed." });

                return Ok(new { message = "Pet name request denied." });
            }
            catch (Exception ex)
            {
                Log.Error($"[PetNaming] Failed to deny request {id}", ex);
                return StatusCode(500, new { message = "Failed to deny pet name request." });
            }
        }

        /// <summary>
        /// Renames the pet device with the given GUID: live in-memory update when the owner is
        /// online and the device can be found, otherwise a direct biota edit in the Shard DB.
        /// </summary>
        private static bool TryRenamePet(uint petGuid, string newName)
        {
            var online = FindOnlineOwnerOf(petGuid);

            if (online != null)
            {
                var device = online.FindObject(petGuid, Player.SearchLocations.Everywhere) as PetDevice;
                if (device != null)
                {
                    device.Name = newName;
                    device.VisualOverrideName = newName;
                    device.ChangesDetected = true;
                    device.SaveBiotaToDatabase();
                    device.ChangesDetected = true;

                    online.UpdateProperty(device, PropertyString.Name, newName);
                    online.UpdateProperty(device, PropertyString.CapturedCreatureName, newName);
                    online.EnqueueBroadcast(new GameMessageUpdateObject(device));
                    online.RushNextPlayerSave(0);

                    if (online.CurrentActivePet is CombatPet activePet &&
                        activePet.SummoningDeviceGuid.Full == petGuid)
                    {
                        activePet.Name = newName;
                        activePet.EnqueueBroadcast(new GameMessageUpdateObject(activePet));
                    }

                    return true;
                }
            }

            var rawBiota = DatabaseManager.Shard.BaseDatabase.GetBiota(petGuid);
            if (rawBiota == null)
                return false;

            var biota = ACE.Database.Adapter.BiotaConverter.ConvertToEntityBiota(rawBiota);

            var rwLock = new ReaderWriterLockSlim();
            biota.SetProperty(PropertyString.Name, newName, rwLock, out _);
            biota.SetProperty(PropertyString.CapturedCreatureName, newName, rwLock, out _);

            return DatabaseManager.Shard.BaseDatabase.SaveBiota(biota, rwLock);
        }

        /// <summary>Scans online players for one carrying the given pet device GUID in inventory.</summary>
        private static Player FindOnlineOwnerOf(uint petGuid)
        {
            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player.FindObject(petGuid, Player.SearchLocations.Everywhere) is PetDevice)
                    return player;
            }
            return null;
        }

        private static void AddParam(IDbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
