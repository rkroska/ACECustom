using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

using log4net;

using Microsoft.EntityFrameworkCore;

using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Economy ledger, progression tracking (owner 2026-10-12): when did each character last gain an enlightenment or
    /// a luminance augmentation? The server only stores the current counts, so once a day (with the supply snapshot)
    /// they are compared with the counts stored here (economy_progress) and the date of any change is kept.
    /// The Stalled exporters report uses it to find accounts that stopped progressing but keep sending currency away.
    /// Read-only on game data.
    /// </summary>
    public static class EconomyProgress
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const int BatchRows = 200;

        /// <summary>Every luminance augmentation counter, old (PropertyInt) and new (PropertyInt64). Read once.</summary>
        private static readonly PropertyInt[] augInts = Enum.GetValues<PropertyInt>().Where(p => p.ToString().StartsWith("LumAug", StringComparison.Ordinal)).Distinct().ToArray();
        private static readonly PropertyInt64[] augInt64s = Enum.GetValues<PropertyInt64>().Where(p => p.ToString().StartsWith("LumAug", StringComparison.Ordinal)).Distinct().ToArray();

        public static int EnlightenmentOf(IPlayer player) => player.GetProperty(PropertyInt.Enlightenment) ?? 0;

        /// <summary>Total luminance augmentations of a character, over every counter.</summary>
        public static long AugsOf(IPlayer player)
        {
            long total = 0;

            foreach (var property in augInts)
                total += Math.Max(0, player.GetProperty(property) ?? 0);
            foreach (var property in augInt64s)
                total += Math.Max(0, player.GetProperty(property) ?? 0);

            return total;
        }

        internal readonly struct Counts
        {
            public readonly int Enlightenment;
            public readonly long Augs;

            public Counts(int enlightenment, long augs)
            {
                Enlightenment = enlightenment;
                Augs = augs;
            }
        }

        internal enum RowAction
        {
            /// <summary>Nothing to write.</summary>
            None,
            /// <summary>First time this character is seen with any progress: no change date yet.</summary>
            Insert,
            /// <summary>The counts differ from the stored ones: the character progressed since the last snapshot.</summary>
            Changed,
        }

        /// <summary>
        /// What to do with one character. A character with no enlightenment and no augs is not stored until it has
        /// some; its first row is then an Insert, and "first seen" stands in for the date it progressed. Pure; unit tested.
        /// </summary>
        internal static RowAction Decide(Counts? stored, Counts current)
        {
            if (!stored.HasValue)
                return current.Enlightenment > 0 || current.Augs > 0 ? RowAction.Insert : RowAction.None;

            return stored.Value.Enlightenment != current.Enlightenment || stored.Value.Augs != current.Augs ? RowAction.Changed : RowAction.None;
        }

        /// <summary>
        /// Compares every character's counts with the stored ones and writes the new and changed rows. Called from the
        /// daily supply snapshot, on its pool thread. Staff characters are left out. Never throws.
        /// </summary>
        public static void Update()
        {
            try
            {
                var started = DateTime.UtcNow;
                EnsureTable();

                var stored = new Dictionary<uint, Counts>();

                using (var ctx = new ShardDbContext())
                {
                    var con = OpenConnection(ctx);
                    using var cmd = con.CreateCommand();
                    cmd.CommandTimeout = 30;
                    cmd.CommandText = "SELECT `char_id`, `enlightenment`, `augs` FROM `economy_progress`";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        stored[Convert.ToUInt32(reader.GetValue(0))] = new Counts(Convert.ToInt32(reader.GetValue(1)), Convert.ToInt64(reader.GetValue(2)));
                }

                var firstRun = stored.Count == 0;
                var rows = new List<(uint charId, uint accountId, string name, Counts counts, bool changed)>();

                foreach (var player in PlayerManager.GetAllPlayers())
                {
                    if ((player.Account?.AccessLevel ?? 0) > 0)
                        continue;

                    var charId = player.Guid.Full;
                    var current = new Counts(EnlightenmentOf(player), AugsOf(player));
                    var action = Decide(stored.TryGetValue(charId, out var known) ? known : null, current);

                    if (action != RowAction.None)
                        rows.Add((charId, player.Account?.AccountId ?? 0, player.Name ?? "", current, action == RowAction.Changed));
                }

                if (rows.Count > 0)
                {
                    using var ctx = new ShardDbContext();
                    var con = OpenConnection(ctx);
                    using var tx = con.BeginTransaction();

                    foreach (var batch in rows.Chunk(BatchRows))
                    {
                        using var cmd = con.CreateCommand();
                        cmd.CommandTimeout = 30;
                        cmd.Transaction = tx;

                        var sb = new StringBuilder("INSERT INTO `economy_progress` (`char_id`,`account_id`,`char_name`,`enlightenment`,`augs`,`first_seen_utc`,`last_change_utc`,`updated_utc`) VALUES ");
                        for (var i = 0; i < batch.Length; i++)
                        {
                            if (i > 0) sb.Append(',');
                            sb.Append($"(@c{i},@a{i},@n{i},@e{i},@g{i},@now,@l{i},@now)");
                            AddParam(cmd, $"@c{i}", batch[i].charId);
                            AddParam(cmd, $"@a{i}", batch[i].accountId);
                            AddParam(cmd, $"@n{i}", batch[i].name.Length <= 64 ? batch[i].name : batch[i].name.Substring(0, 64));
                            AddParam(cmd, $"@e{i}", batch[i].counts.Enlightenment);
                            AddParam(cmd, $"@g{i}", batch[i].counts.Augs);
                            AddParam(cmd, $"@l{i}", batch[i].changed ? (object)started : null);
                        }
                        AddParam(cmd, "@now", started);

                        // first_seen_utc is kept from the first insert
                        sb.Append(" ON DUPLICATE KEY UPDATE `account_id`=VALUES(`account_id`),`char_name`=VALUES(`char_name`),`enlightenment`=VALUES(`enlightenment`)," +
                                  "`augs`=VALUES(`augs`),`last_change_utc`=VALUES(`last_change_utc`),`updated_utc`=VALUES(`updated_utc`)");
                        cmd.CommandText = sb.ToString();
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }

                log.Info($"[EconomyLedger] Progress snapshot: {rows.Count(r => !r.changed):N0} new, {rows.Count(r => r.changed):N0} progressed{(firstRun ? " (first run: baseline written)" : "")} " +
                         $"in {(DateTime.UtcNow - started).TotalSeconds:N1} s");
            }
            catch (Exception ex)
            {
                log.Error($"[EconomyLedger] Progress snapshot failed: {ex.Message}");
            }
        }

        private static void EnsureTable()
        {
            using var ctx = new ShardDbContext();
            var con = OpenConnection(ctx);
            using var cmd = con.CreateCommand();
            cmd.CommandTimeout = 10;
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `economy_progress` (
  `char_id`          int unsigned  NOT NULL,
  `account_id`       int unsigned  NOT NULL DEFAULT 0,
  `char_name`        varchar(64)   NOT NULL DEFAULT '',
  `enlightenment`    int           NOT NULL DEFAULT 0,
  `augs`             bigint        NOT NULL DEFAULT 0,
  `first_seen_utc`   datetime      NOT NULL,
  `last_change_utc`  datetime      NULL,
  `updated_utc`      datetime      NOT NULL,
  PRIMARY KEY (`char_id`),
  KEY `ix_account` (`account_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
            cmd.ExecuteNonQuery();
        }

        private static DbConnection OpenConnection(ShardDbContext ctx)
        {
            var con = ctx.Database.GetDbConnection();
            if (con.State != ConnectionState.Open)
                con.Open();
            return con;
        }

        private static void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
