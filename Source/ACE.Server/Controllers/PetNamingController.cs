using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
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

        /// <summary>How long Approve waits for the world queue to run the rename before giving the row back.</summary>
        private static readonly TimeSpan OnlineRenameTimeout = TimeSpan.FromSeconds(10);

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

        private enum RenameOutcome
        {
            /// <summary>The device was renamed (live or in the shard DB).</summary>
            Renamed,
            /// <summary>The device no longer matches the request (gone, recycled GUID, wrong owner, renamed since). The request is auto-denied.</summary>
            Mismatch,
            /// <summary>Transient failure (DB save failed, world queue did not run in time). The request goes back to pending.</summary>
            Failed,
        }

        private readonly struct RenameResult
        {
            public readonly RenameOutcome Outcome;
            public readonly string Reason;

            public RenameResult(RenameOutcome outcome, string reason)
            {
                Outcome = outcome;
                Reason = reason;
            }

            public static RenameResult Renamed() => new RenameResult(RenameOutcome.Renamed, null);
            public static RenameResult Mismatch(string reason) => new RenameResult(RenameOutcome.Mismatch, reason);
            public static RenameResult Failed(string reason) => new RenameResult(RenameOutcome.Failed, reason);
        }

        private bool HasAccess() => IsPortalAdmin || HasPortalAccess(PortalPages.PetNaming);

        private string ReviewerName => User.Identity?.Name ?? CurrentAccountId?.ToString() ?? "unknown";

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

        /// <summary>
        /// Approves a request. The row is claimed atomically first (pending -> approved), so two reviewers or a
        /// double-click cannot both rename; the rename itself runs on the world queue when the owner is online.
        /// On a mismatch (device gone, GUID recycled, different owner, renamed since) the request is auto-denied
        /// with the reason; on a transient failure the row is returned to pending.
        /// </summary>
        [HttpPost("approve/{id}")]
        public async Task<IActionResult> Approve(long id)
        {
            if (!HasAccess())
                return Forbid();

            var reviewer = ReviewerName;

            try
            {
                using var ctx = new ShardDbContext();
                var con = ctx.Database.GetDbConnection();
                if (con.State != ConnectionState.Open) con.Open();

                // Claim: only a pending row moves to approved, and only one caller sees rows == 1.
                using (var claimCmd = con.CreateCommand())
                {
                    claimCmd.CommandText =
                        "UPDATE `pet_name_requests` SET `status` = @status, `reviewed_at` = UTC_TIMESTAMP(), `reviewed_by` = @reviewer " +
                        "WHERE `id` = @id AND `status` = @pending";
                    AddParam(claimCmd, "@status", StatusApproved);
                    AddParam(claimCmd, "@reviewer", reviewer);
                    AddParam(claimCmd, "@id", id);
                    AddParam(claimCmd, "@pending", StatusPending);

                    if (claimCmd.ExecuteNonQuery() == 0)
                    {
                        if (!RequestExists(con, id))
                            return NotFound(new { message = "Pet name request not found." });

                        return BadRequest(new { message = "This request has already been reviewed." });
                    }
                }

                long characterId;
                uint petGuid;
                string oldName;
                string requestedName;

                using (var selectCmd = con.CreateCommand())
                {
                    selectCmd.CommandText = "SELECT `character_id`, `pet_guid`, `old_name`, `requested_name` FROM `pet_name_requests` WHERE `id` = @id LIMIT 1";
                    AddParam(selectCmd, "@id", id);
                    using var reader = selectCmd.ExecuteReader();
                    if (!reader.Read())
                        return NotFound(new { message = "Pet name request not found." });

                    characterId = reader.GetInt64(0);
                    petGuid = (uint)reader.GetInt64(1);
                    oldName = reader.GetString(2);
                    requestedName = reader.GetString(3);
                }

                var result = await RenamePetAsync((uint)characterId, petGuid, oldName, requestedName);

                switch (result.Outcome)
                {
                    case RenameOutcome.Renamed:
                        return Ok(new { message = $"Pet renamed to \"{requestedName}\"." });

                    case RenameOutcome.Mismatch:
                        SetStatus(con, id, StatusDenied, "Auto-denied: " + result.Reason, reviewer);
                        return Conflict(new { message = $"Request denied automatically: {result.Reason}" });

                    default:
                        SetStatus(con, id, StatusPending, null, null);
                        Log.Warn($"[PetNaming] Approve of request {id} (pet 0x{petGuid:X8}) failed and was returned to pending: {result.Reason}");
                        return StatusCode(500, new { message = $"Failed to rename the pet ({result.Reason}). The request is still pending; try again." });
                }
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
                AddParam(cmd, "@reviewer", ReviewerName);
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
        /// Renames the pet device the request points at. Online owner: the work is handed to the WORLD action
        /// queue (it drains between landblock ticks, so no landblock thread can be touching the player or the
        /// device while it runs) and this HTTP thread awaits the outcome. Offline owner: direct biota edit in
        /// the shard DB. Both paths verify the object is still the requested device: a PetDevice, possessed by
        /// the requesting character, and still carrying the request's old name (dynamic GUIDs are recycled).
        /// </summary>
        private static async Task<RenameResult> RenamePetAsync(uint characterId, uint petGuid, string oldName, string newName)
        {
            if (PlayerManager.GetOnlinePlayer(characterId) != null)
            {
                var tcs = new TaskCompletionSource<RenameResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
                var claimed = 0; // 0 = open, 1 = the queued action owns the outcome, 2 = the HTTP side gave up

                WorldManager.EnqueueAction(new ActionEventDelegate(ActionType.PetNaming_ApproveRename, () =>
                {
                    if (Interlocked.CompareExchange(ref claimed, 1, 0) != 0)
                        return; // timed out: the row went back to pending, do not rename behind the reviewer's back

                    try
                    {
                        // Re-resolve on the world thread: the player may have logged out since the check above.
                        var player = PlayerManager.GetOnlinePlayer(characterId);
                        tcs.TrySetResult(player == null ? null : RenameOnline(player, petGuid, oldName, newName));
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }));

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(OnlineRenameTimeout));
                if (completed != tcs.Task)
                {
                    if (Interlocked.CompareExchange(ref claimed, 2, 0) == 0)
                        return RenameResult.Failed("the world queue did not process the rename in time");

                    // The action grabbed the claim right at the deadline; it will finish shortly.
                }

                RenameResult? online;
                try
                {
                    online = await tcs.Task;
                }
                catch (Exception ex)
                {
                    return RenameResult.Failed("the rename threw on the world queue: " + ex.Message);
                }

                if (online.HasValue)
                    return online.Value;

                // Owner logged out before the action ran: fall through to the offline path.
            }

            return RenameOffline(characterId, petGuid, oldName, newName);
        }

        /// <summary>Runs on the world queue. Verifies and renames the live device in the owner's possession.</summary>
        private static RenameResult RenameOnline(Player owner, uint petGuid, string oldName, string newName)
        {
            var device = owner.FindObject(petGuid, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) as PetDevice;
            if (device == null)
            {
                // Not on the character. The search covers the main pack, every side pack and equipped
                // items (Container.GetInventoryItem recurses), so this means the essence really is
                // elsewhere - dropped, in a chest, sold or traded away.
                //
                // Do NOT fall through to RenameOffline here. That reads the biota straight from the
                // shard with skipCache, edits the detached copy and writes the whole thing back, while
                // the server still has the object loaded somewhere else. The in-memory copy knows
                // nothing about the write, so whichever saves last wins and any property changed since
                // the last save can be silently reverted. It also runs synchronous DB I/O on the world
                // queue, stalling every landblock for the duration.
                //
                // Auto-deny instead; the owner can request again once it is back in their possession.
                return RenameResult.Mismatch("the device is not in the owner's possession");
            }

            if (!string.Equals(device.Name, oldName, StringComparison.Ordinal))
                return RenameResult.Mismatch($"the device is now named \"{device.Name}\", not \"{oldName}\"");

            device.Name = newName;
            device.VisualOverrideName = newName;
            // Authoritative owner-chosen name: the summon path uses this verbatim rather than running it
            // through the "Owner's " prefix cleanup, which would eat a legitimate possessive in the name.
            device.SetProperty(PropertyString.PetCustomName, newName);
            device.ChangesDetected = true;
            device.SaveBiotaToDatabase();
            device.ChangesDetected = true;

            owner.UpdateProperty(device, PropertyString.Name, newName);
            owner.UpdateProperty(device, PropertyString.CapturedCreatureName, newName);
            owner.EnqueueBroadcast(new GameMessageUpdateObject(device));
            owner.RushNextPlayerSave(0);

            if (owner.CurrentActivePet is CombatPet activePet && activePet.SummoningDeviceGuid.Full == petGuid)
            {
                activePet.Name = newName;
                activePet.EnqueueBroadcast(new GameMessageUpdateObject(activePet));
            }

            owner.Session?.Network.EnqueueSend(new GameMessageSystemChat(
                $"Your pet rename request has been approved: {oldName} is now \"{newName}\".", ChatMessageType.Broadcast));

            return RenameResult.Renamed();
        }

        /// <summary>
        /// Verifies and renames the device's shard biota. Ownership is the biota's Container or Owner IID
        /// (the character, or a pack whose own Container/Owner is the character).
        /// </summary>
        private static RenameResult RenameOffline(uint characterId, uint petGuid, string oldName, string newName)
        {
            var rawBiota = DatabaseManager.Shard.BaseDatabase.GetBiota(petGuid, skipCache: true);
            if (rawBiota == null)
                return RenameResult.Mismatch("the pet device no longer exists");

            if (rawBiota.WeenieType != (int)WeenieType.PetDevice)
                return RenameResult.Mismatch("the object with that GUID is no longer a pet device");

            var biota = ACE.Database.Adapter.BiotaConverter.ConvertToEntityBiota(rawBiota);

            if (!IsPossessedBy(biota, characterId))
                return RenameResult.Mismatch("the pet device is no longer in the requesting character's possession");

            string currentName = null;
            biota.PropertiesString?.TryGetValue(PropertyString.Name, out currentName);
            if (!string.Equals(currentName, oldName, StringComparison.Ordinal))
                return RenameResult.Mismatch($"the device is now named \"{currentName}\", not \"{oldName}\"");

            // Only reached when the owner is genuinely offline, so nothing else holds this biota and a
            // local lock is sufficient. Disposed rather than left for the finalizer.
            using var rwLock = new ReaderWriterLockSlim();
            biota.SetProperty(PropertyString.Name, newName, rwLock, out _);
            biota.SetProperty(PropertyString.CapturedCreatureName, newName, rwLock, out _);
            // Same authoritative record as the online path, so the name survives the next summon.
            biota.SetProperty(PropertyString.PetCustomName, newName, rwLock, out _);

            if (!DatabaseManager.Shard.BaseDatabase.SaveBiota(biota, rwLock))
                return RenameResult.Failed("the shard database rejected the biota save");

            return RenameResult.Renamed();
        }

        private static bool IsPossessedBy(ACE.Entity.Models.Biota biota, uint characterId)
        {
            if (biota.PropertiesIID == null)
                return false;

            biota.PropertiesIID.TryGetValue(PropertyInstanceId.Container, out var containerId);
            biota.PropertiesIID.TryGetValue(PropertyInstanceId.Owner, out var ownerId);

            if (containerId == characterId || ownerId == characterId)
                return true;

            // One level of nesting: a pack in the character's inventory.
            if (containerId == 0)
                return false;

            var pack = DatabaseManager.Shard.BaseDatabase.GetBiota(containerId, skipCache: true);
            if (pack?.BiotaPropertiesIID == null)
                return false;

            foreach (var iid in pack.BiotaPropertiesIID)
            {
                if ((iid.Type == (ushort)PropertyInstanceId.Container || iid.Type == (ushort)PropertyInstanceId.Owner) && iid.Value == characterId)
                    return true;
            }

            return false;
        }

        private static bool RequestExists(IDbConnection con, long id)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM `pet_name_requests` WHERE `id` = @id LIMIT 1";
            AddParam(cmd, "@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read();
        }

        /// <summary>Sets the review outcome; a pending status clears the reviewer so the row looks untouched again.</summary>
        private static void SetStatus(IDbConnection con, long id, int status, string note, string reviewer)
        {
            if (note?.Length > 255)
                note = note.Substring(0, 255);

            using var cmd = con.CreateCommand();
            cmd.CommandText = status == StatusPending
                ? "UPDATE `pet_name_requests` SET `status` = @status, `review_note` = NULL, `reviewed_at` = NULL, `reviewed_by` = NULL WHERE `id` = @id"
                : "UPDATE `pet_name_requests` SET `status` = @status, `review_note` = @note, `reviewed_at` = UTC_TIMESTAMP(), `reviewed_by` = @reviewer WHERE `id` = @id";
            AddParam(cmd, "@status", status);
            AddParam(cmd, "@id", id);
            if (status != StatusPending)
            {
                AddParam(cmd, "@note", (object)note ?? DBNull.Value);
                AddParam(cmd, "@reviewer", reviewer);
            }
            cmd.ExecuteNonQuery();
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
