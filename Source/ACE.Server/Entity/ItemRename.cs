using System;
using System.Data;
using System.Text.RegularExpressions;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Item rename (owner 2026-10-30): a player asks for a new name for a weapon or caster with @item-name and pays
    /// item_rename_fee up front; staff approve or deny it on the portal's Name Approvals page, next to pet renames. The
    /// requests share pet_name_requests (kind = 1). A denied request (by staff, automatically, or replaced by a newer one)
    /// gets its fee back in the bank, exactly once.
    /// </summary>
    public static class ItemRename
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const string Tag = "[ItemRename]";

        /// <summary>pet_name_requests.kind</summary>
        public const int KindPet = 0;
        public const int KindItem = 1;

        public const int MinNameLength = 3;
        public const int MaxNameLength = 32;

        /// <summary>The same characters @pet-name allows; plain ASCII, so the client can draw every name.</summary>
        private static readonly Regex NamePattern = new(@"^[a-zA-Z0-9' -]+$", RegexOptions.Compiled);

        /// <summary>Words a requested name may not use: the T11+ rare prefixes, which only a real rare carries (an approved
        /// rename of a real rare gets its prefix back automatically, see <see cref="FinalName"/>).</summary>
        private static readonly Regex ReservedWords = new(@"\b(pristine|ascendant)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Why <paramref name="name"/> cannot be requested, or null when it can. Expects a trimmed name.</summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < MinNameLength || name.Length > MaxNameLength)
                return $"Item names must be between {MinNameLength} and {MaxNameLength} characters.";
            if (!NamePattern.IsMatch(name))
                return "Item names may only contain letters, numbers, spaces, apostrophes and hyphens.";
            if (name.Contains("  "))
                return "Item names may not contain double spaces.";
            if (ReservedWords.IsMatch(name))
                return "Item names may not use the words Pristine or Ascendant; a real rare keeps its own.";
            return null;
        }

        /// <summary>A weapon or caster that is a single item (not arrows, bolts or other stacks).</summary>
        public static bool IsRenamable(WorldObject wo)
            => wo != null && IsRenamable((int?)wo.ItemType, (int?)wo.WeenieType, wo.MaxStackSize);

        /// <summary>The same test on raw properties, for an item read straight from the shard database.</summary>
        public static bool IsRenamable(int? itemType, int? weenieType, int? maxStackSize)
        {
            if (itemType == null || ((ItemType)itemType.Value & ItemType.WeaponOrCaster) == 0)
                return false;
            if (weenieType == (int)WeenieType.Ammunition || weenieType == (int)WeenieType.Missile)
                return false;
            return (maxStackSize ?? 1) <= 1;
        }

        /// <summary>The name the item gets on approval: the requested name, with a real rare's tier word in front.</summary>
        public static string FinalName(string requestedName, int? zcRare)
        {
            var tier = zcRare == (int)ZoneRareTier.Pristine || zcRare == (int)ZoneRareTier.Ascendant ? (ZoneRareTier)zcRare.Value : ZoneRareTier.None;
            return tier == ZoneRareTier.None ? requestedName : ZoneRare.PrefixName(requestedName, tier);
        }

        /// <summary>The appraisal line for a renamed item, or null. Plain ASCII: names are validated, and an original name
        /// with anything else in it is not shown.</summary>
        public static string OriginalNameLine(WorldObject wo)
        {
            var original = wo?.GetProperty(PropertyString.OriginalItemName);
            if (string.IsNullOrWhiteSpace(original) || string.Equals(original, wo.Name, StringComparison.Ordinal))
                return null;
            foreach (var c in original)
                if (c < 0x20 || c > 0x7E)
                    return null;
            return "- Originally: " + original;
        }

        /// <summary>True when the item's name was chosen by a player, so automatic renames (an element change) leave it alone.</summary>
        public static bool HasCustomName(WorldObject wo) => wo?.GetProperty(PropertyString.OriginalItemName) != null;

        // -- database ----------------------------------------------------------------------------------------------

        private static volatile bool schemaReady;
        private static readonly object schemaLock = new();

        /// <summary>
        /// Makes sure pet_name_requests exists and has the item-rename columns (Database/Updates/Shard/2026-10-30-00-Item-Name-Requests.sql
        /// does the same, and running it is optional). Runs once per server start; every caller that touches the table calls it first,
        /// because the pet queries filter on kind too.
        /// </summary>
        public static void EnsureSchema(IDbConnection con)
        {
            if (schemaReady)
                return;

            lock (schemaLock)
            {
                if (schemaReady)
                    return;

                Execute(con, @"
CREATE TABLE IF NOT EXISTS `pet_name_requests` (
  `id`               bigint       NOT NULL AUTO_INCREMENT,
  `kind`             tinyint      NOT NULL DEFAULT 0,
  `character_id`     bigint       NOT NULL,
  `character_name`   varchar(64)  NOT NULL,
  `pet_guid`         int unsigned NOT NULL,
  `old_name`         varchar(64)  NOT NULL,
  `requested_name`   varchar(64)  NOT NULL,
  `status`           tinyint      NOT NULL DEFAULT 0,
  `review_note`      varchar(255) NULL,
  `created_at`       datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `reviewed_at`      datetime     NULL,
  `reviewed_by`      varchar(64)  NULL,
  `fee_paid`         bigint       NOT NULL DEFAULT 0,
  `fee_refunded`     tinyint      NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `IX_pet_name_req_status` (`status`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

                AddColumnIfMissing(con, "kind", "`kind` tinyint NOT NULL DEFAULT 0 AFTER `id`");
                AddColumnIfMissing(con, "fee_paid", "`fee_paid` bigint NOT NULL DEFAULT 0");
                AddColumnIfMissing(con, "fee_refunded", "`fee_refunded` tinyint NOT NULL DEFAULT 0");

                schemaReady = true;
            }
        }

        private static void AddColumnIfMissing(IDbConnection con, string column, string ddl)
        {
            using (var check = con.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests' AND column_name = @col";
                AddParam(check, "@col", column);
                if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                    return;
            }
            Execute(con, "ALTER TABLE `pet_name_requests` ADD COLUMN " + ddl);
            log.Info($"{Tag} added column pet_name_requests.{column}");
        }

        private static void Execute(IDbConnection con, string sql)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        internal static void AddParam(IDbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        // -- refunds -----------------------------------------------------------------------------------------------

        /// <summary>
        /// Claims the fee of a DENIED request for refunding: flips fee_refunded 0 -> 1, so only one caller ever gets the amount.
        /// Returns the fee to credit (0 when there is nothing to refund: a pet request, a free one, still pending or approved,
        /// or already refunded). The caller then hands it to <see cref="QueueRefund"/>.
        /// </summary>
        public static long ClaimRefund(IDbConnection con, long requestId, out uint characterId, out string itemName, IDbTransaction tx = null)
        {
            characterId = 0;
            itemName = null;

            using (var claim = con.CreateCommand())
            {
                claim.Transaction = tx;
                claim.CommandText =
                    "UPDATE `pet_name_requests` SET `fee_refunded` = 1 " +
                    "WHERE `id` = @id AND `kind` = @kind AND `status` = 2 AND `fee_paid` > 0 AND `fee_refunded` = 0";
                AddParam(claim, "@id", requestId);
                AddParam(claim, "@kind", KindItem);
                if (claim.ExecuteNonQuery() == 0)
                    return 0;
            }

            using var select = con.CreateCommand();
            select.Transaction = tx;
            select.CommandText = "SELECT `character_id`, `fee_paid`, `old_name` FROM `pet_name_requests` WHERE `id` = @id LIMIT 1";
            AddParam(select, "@id", requestId);
            using var reader = select.ExecuteReader();
            if (!reader.Read())
                return 0;
            characterId = (uint)reader.GetInt64(0);
            itemName = reader.GetString(2);
            return reader.GetInt64(1);
        }

        /// <summary>
        /// Credits <paramref name="amount"/> pyreals back to the character's bank on the world queue (it drains between landblock
        /// ticks, so nothing else is touching the player), online or offline, and tells them why. Safe to call from any thread.
        /// </summary>
        public static void QueueRefund(uint characterId, long amount, string reason)
        {
            if (amount <= 0 || characterId == 0)
                return;

            WorldManager.EnqueueAction(new ActionEventDelegate(ActionType.ItemRename_Refund, () => CreditRefund(characterId, amount, reason)));
        }

        private static void CreditRefund(uint characterId, long amount, string reason)
        {
            try
            {
                using var ledgerScope = PyrealLedger.Begin(PyrealLedger.SrcFee, "ItemRename", "Item rename refund");

                var online = PlayerManager.GetOnlinePlayer(characterId);
                if (online != null)
                {
                    online.BankedPyreals = (online.BankedPyreals ?? 0) + amount;
                    online.RefreshCoinValueAfterBankChange();
                    online.Session?.Network.EnqueueSend(new GameMessageSystemChat(
                        $"{reason} Your fee of {amount:N0} pyreals has been returned to your bank.", ChatMessageType.Broadcast));
                    log.Info($"{Tag} refunded {amount} pyreals to {online.Name} (0x{characterId:X8}, online): {reason}");
                    return;
                }

                var offline = PlayerManager.GetOfflinePlayer(characterId);
                if (offline == null)
                {
                    log.Error($"{Tag} could not refund {amount} pyreals to character 0x{characterId:X8}: no such character ({reason}). Credit it by hand.");
                    return;
                }

                offline.BankedPyreals = (offline.BankedPyreals ?? 0) + amount;
                offline.SaveBiotaToDatabase();
                log.Info($"{Tag} refunded {amount} pyreals to {offline.Name} (0x{characterId:X8}, offline): {reason}");
            }
            catch (Exception ex)
            {
                log.Error($"{Tag} refund of {amount} pyreals to character 0x{characterId:X8} failed ({reason}). Credit it by hand.", ex);
            }
        }
    }
}
