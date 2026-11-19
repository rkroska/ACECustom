using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using log4net;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

using ACE.Database.Models.Shard;
using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    public sealed class ZoneControlChange
    {
        public long Id { get; set; }
        public DateTime ChangedAtUtc { get; set; }
        public string Actor { get; set; }
        public string Source { get; set; }
        public string Zone { get; set; }
        public int? Variation { get; set; }
        public uint? Wcid { get; set; }
        public string Rank { get; set; }
        public string Stat { get; set; }
        public string OldCurveJson { get; set; }
        public string NewCurveJson { get; set; }
        public long? RevertsId { get; set; }
        /// <summary>What kind of edit this row is. Only stat set/clear/revert is logged today ("stat"); other edits get their own kind.</summary>
        public string Kind { get; set; } = ZoneControlChangeLog.KindStat;
        /// <summary>Why the change was made, as typed by the editor (plain ASCII, at most <see cref="ZoneControlChangeLog.ReasonMaxLength"/>). Null = none given.</summary>
        public string Reason { get; set; }
        /// <summary>Rows saved together share this id, so they can be shown and reverted as one batch. Null = a change on its own.</summary>
        public string BatchId { get; set; }
        /// <summary>Kind "section" only: which part of the store changed ("zone:tou tou", "default:14", "gearlock"...).</summary>
        public string Section { get; set; }
        /// <summary>Kind "section" only: the whole part before and after, as stored JSON (null = it did not exist).</summary>
        public string OldJson { get; set; }
        public string NewJson { get; set; }

        public StatCurve OldCurve => ZoneControlChangeLog.ReadCurve(OldCurveJson);
        public StatCurve NewCurve => ZoneControlChangeLog.ReadCurve(NewCurveJson);
    }

    /// <summary>
    /// Who changed which Zone Control stat, from what to what (shard table zone_control_change_log, created on first use). Holds the
    /// whole curve on both sides so a revert restores growth and tier pins as well as the number. A failed write never undoes the edit.
    /// </summary>
    public static class ZoneControlChangeLog
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ZoneControlChangeLog));
        private static readonly object _schemaLock = new object();
        private static volatile bool _schemaReady;

        public const string KindStat = "stat";
        /// <summary>Any other edit: a whole part of the store (one zone, one tier Default, or one server-wide list) before and after.
        /// Written by the store save itself, so every command that saves is covered without logging code of its own.</summary>
        public const string KindSection = "section";

        // WHO is editing, for edits that do not pass an actor in (everything except stat set/clear/revert): the /zonecontrol command
        // and the portal set it around their work, and the store save reads it. AsyncLocal so a portal request and a game command on
        // two threads never see each other's actor. Unset = "server" (a save nobody claimed).
        private static readonly AsyncLocal<ZoneStatActor> _actor = new AsyncLocal<ZoneStatActor>();

        public static ZoneStatActor CurrentActor => _actor.Value ?? new ZoneStatActor { Name = "server", Source = ZoneStatActor.SourceConsole };

        /// <summary>Everything saved inside the using block is logged as done by <paramref name="actor"/>.</summary>
        public static IDisposable ActingAs(ZoneStatActor actor)
        {
            var previous = _actor.Value;
            _actor.Value = actor;
            return new Restore(() => _actor.Value = previous);
        }

        // WHY, for the same saves: the portal's Bounties page passes the admin's reason; the store save reads it. Unset = none.
        private static readonly AsyncLocal<string> _reason = new AsyncLocal<string>();

        public static string CurrentReason => _reason.Value;

        /// <summary>Everything saved inside the using block is logged with <paramref name="reason"/> (cleaned; blank = none).</summary>
        public static IDisposable WithReason(string reason)
        {
            var previous = _reason.Value;
            _reason.Value = CleanReason(reason);
            return new Restore(() => _reason.Value = previous);
        }

        // A stat edit logs its own exact row (old and new curve), so the save it triggers must not ALSO log the whole zone it touched.
        // Set by ZoneStatEditor inside the mutate (same thread as the save); every other part that changed is still logged.
        [ThreadStatic] private static string _skipSection;

        internal static string SkipSection => _skipSection;

        internal static void SetSkipSection(string sectionKey) => _skipSection = sectionKey;

        internal static IDisposable StatEditOn(string sectionKey)
        {
            var previous = _skipSection;
            _skipSection = sectionKey;
            return new Restore(() => _skipSection = previous);
        }

        private sealed class Restore : IDisposable
        {
            private Action _undo;
            public Restore(Action undo) => _undo = undo;
            public void Dispose()
            {
                _undo?.Invoke();
                _undo = null;
            }
        }

        /// <summary>One changed part of the store, from one save.</summary>
        internal sealed class SectionChange
        {
            public string Section { get; init; }
            public string Label { get; init; }
            public string Zone { get; init; }
            public int? Variation { get; init; }
            public string OldJson { get; init; }
            public string NewJson { get; init; }
        }

        /// <summary>Logs the parts one save changed, as one batch. Portal edits write on the request thread (so a revert can say when
        /// the log missed it); game and server saves write in the background, like stat edits.</summary>
        internal static bool RecordSections(IReadOnlyList<SectionChange> changes, ZoneStatActor actor, long? revertsId, string reason = null, string batchId = null)
        {
            if (changes == null || changes.Count == 0)
                return true;
            var batch = batchId ?? (changes.Count > 1 ? Guid.NewGuid().ToString("N") : null);
            var rows = changes.Select(c => new ZoneControlChange
            {
                Actor = actor?.Name ?? "unknown",
                Source = actor?.Source ?? ZoneStatActor.SourceConsole,
                Zone = c.Zone,
                Variation = c.Variation,
                Rank = "-",
                Stat = c.Label,
                Kind = KindSection,
                Section = c.Section,
                OldJson = c.OldJson,
                NewJson = c.NewJson,
                RevertsId = revertsId,
                Reason = CleanReason(reason),
                BatchId = batch,
            }).ToList();

            return Write(rows, wait: actor?.Source == ZoneStatActor.SourcePortal);
        }

        // ORDERED WRITER (review 2026-11-12): every row goes through ONE queue drained by ONE thread, so rows reach the table - and get
        // their ids and timestamps - in the order the saves were made. Revert all and the History chain checks read id order as edit
        // order; separate Task.Run inserts could swap two quick edits. Callers enqueue under the manager lock (saves hold it), so
        // enqueue order is save order. Portal callers wait for their rows, so the page can say when the log missed one.
        private static readonly BlockingCollection<(ZoneControlChange Row, TaskCompletionSource<bool> Done)> _queue =
            new BlockingCollection<(ZoneControlChange, TaskCompletionSource<bool>)>();
        private static readonly object _writerLock = new object();
        private static Thread _writer;
        private static readonly TimeSpan PortalWait = TimeSpan.FromSeconds(15);

        private static bool Write(IReadOnlyList<ZoneControlChange> rows, bool wait)
        {
            EnsureWriter();
            var done = wait ? rows.Select(_ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)).ToList() : null;
            for (var i = 0; i < rows.Count; i++)
                _queue.Add((rows[i], done?[i]));
            if (!wait)
                return true;
            // wait for every row (never stop at the first failure: the rest of the batch is still written)
            var tasks = done.Select(d => d.Task).ToArray();
            return Task.WaitAll(tasks, PortalWait) && tasks.All(t => t.Result);
        }

        private static void EnsureWriter()
        {
            if (_writer != null) return;
            lock (_writerLock)
            {
                if (_writer != null) return;
                _writer = new Thread(() =>
                {
                    foreach (var (row, done) in _queue.GetConsumingEnumerable())
                        done?.TrySetResult(TryInsert(row));
                })
                { IsBackground = true, Name = "ZoneControl change log" };
                _writer.Start();
            }
        }
        public const int ReasonMaxLength = 255;
        public const int BatchIdMaxLength = 64;

        /// <summary>A reason fit to store and to echo to the game client: printable 7-bit ASCII only (anything else becomes a space),
        /// whitespace runs collapsed, trimmed, cut to <see cref="ReasonMaxLength"/>. Null or blank gives null.</summary>
        public static string CleanReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return null;
            var sb = new System.Text.StringBuilder(reason.Length);
            var space = false;
            foreach (var ch in reason)
            {
                var c = ch >= 33 && ch <= 126 ? ch : ' ';
                if (c == ' ')
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (space)
                    sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            var text = sb.ToString();
            if (text.Length > ReasonMaxLength)
                text = text.Substring(0, ReasonMaxLength).TrimEnd();
            return text.Length == 0 ? null : text;
        }

        /// <summary>Portal edits write on the request thread, so the page can say when the log missed one. Everything else writes in the
        /// background: a command runs on a game thread, which must not wait on this table.</summary>
        internal static bool Record(ZoneStatTarget target, string stat, StatCurve oldCurve, StatCurve newCurve, ZoneStatActor actor, long? revertsId,
            string reason = null, string batchId = null)
        {
            var change = new ZoneControlChange
            {
                Actor = actor?.Name ?? "unknown",
                Source = actor?.Source ?? ZoneStatActor.SourceConsole,
                Zone = target.Zone,
                Variation = target.IsDefault ? target.Variation : (int?)null,
                Wcid = target.Wcid,
                Rank = ZoneRank.Key(target.Rank),
                Stat = stat,
                OldCurveJson = WriteCurve(oldCurve),
                NewCurveJson = WriteCurve(newCurve),
                RevertsId = revertsId,
                Kind = KindStat,
                Reason = CleanReason(reason),
                BatchId = string.IsNullOrWhiteSpace(batchId) ? null : batchId.Trim(),
            };
            if (change.BatchId != null && change.BatchId.Length > BatchIdMaxLength)
                change.BatchId = change.BatchId.Substring(0, BatchIdMaxLength);

            return Write(new[] { change }, wait: change.Source == ZoneStatActor.SourcePortal);
        }

        internal static string WriteCurve(StatCurve curve) => curve == null ? null : JsonConvert.SerializeObject(curve);

        internal static StatCurve ReadCurve(string json) => string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<StatCurve>(json);

        private static bool TryInsert(ZoneControlChange c)
        {
            try
            {
                EnsureSchema();
                using var ctx = new ShardDbContext();
                var con = Open(ctx);
                using var cmd = NewCommand(con);
                cmd.CommandText = @"
INSERT INTO zone_control_change_log (changed_at, actor, source, zone_name, variation, wcid, rank_key, stat, old_curve, new_curve, reverts_id, kind, reason, batch_id, section, old_json, new_json)
VALUES (UTC_TIMESTAMP(), @actor, @source, @zone, @variation, @wcid, @rank, @stat, @old, @new, @reverts, @kind, @reason, @batch, @section, @oldjson, @newjson)";
                AddParam(cmd, "@actor", c.Actor);
                AddParam(cmd, "@source", c.Source);
                AddParam(cmd, "@zone", c.Zone);
                AddParam(cmd, "@variation", c.Variation);
                AddParam(cmd, "@wcid", c.Wcid);
                AddParam(cmd, "@rank", c.Rank);
                AddParam(cmd, "@stat", c.Stat);
                AddParam(cmd, "@old", c.OldCurveJson);
                AddParam(cmd, "@new", c.NewCurveJson);
                AddParam(cmd, "@reverts", c.RevertsId);
                AddParam(cmd, "@kind", c.Kind);
                AddParam(cmd, "@reason", c.Reason);
                AddParam(cmd, "@batch", c.BatchId);
                AddParam(cmd, "@section", c.Section);
                AddParam(cmd, "@oldjson", c.OldJson);
                AddParam(cmd, "@newjson", c.NewJson);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[ZoneControl] change log write failed: {c.Actor} {c.Stat} on {c.Zone ?? "v" + c.Variation}: {ex.GetBaseException().Message}");
                return false;
            }
        }

        /// <summary>Newest first. Zone or variation narrows it to one scope; neither lists everything.</summary>
        public static List<ZoneControlChange> List(string zone, int? variation, int limit)
        {
            EnsureSchema();
            using var ctx = new ShardDbContext();
            var con = Open(ctx);
            using var cmd = NewCommand(con);
            var where = zone != null ? "WHERE zone_name = @zone" : variation.HasValue ? "WHERE variation = @variation" : "";
            cmd.CommandText = SelectColumns + where + " ORDER BY id DESC LIMIT @limit";
            if (zone != null) AddParam(cmd, "@zone", zone);
            else if (variation.HasValue) AddParam(cmd, "@variation", variation.Value);
            AddParam(cmd, "@limit", Math.Clamp(limit, 1, 500));
            return Read(cmd);
        }

        public static ZoneControlChange Get(long id)
        {
            EnsureSchema();
            using var ctx = new ShardDbContext();
            var con = Open(ctx);
            using var cmd = NewCommand(con);
            cmd.CommandText = SelectColumns + "WHERE id = @id";
            AddParam(cmd, "@id", id);
            var rows = Read(cmd);
            return rows.Count > 0 ? rows[0] : null;
        }

        /// <summary>How many rows share a batch id (one save that changed several parts, or one Revert all).</summary>
        public static int CountBatch(string batchId)
        {
            if (string.IsNullOrEmpty(batchId))
                return 0;
            EnsureSchema();
            using var ctx = new ShardDbContext();
            var con = Open(ctx);
            using var cmd = NewCommand(con);
            cmd.CommandText = "SELECT COUNT(*) FROM zone_control_change_log WHERE batch_id = @batch";
            AddParam(cmd, "@batch", batchId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>Every row of these batches, newest first.</summary>
        public static List<ZoneControlChange> GetBatches(IReadOnlyCollection<string> batchIds)
        {
            var ids = batchIds?.Where(b => !string.IsNullOrEmpty(b)).Distinct().ToList() ?? new List<string>();
            if (ids.Count == 0)
                return new List<ZoneControlChange>();
            EnsureSchema();
            using var ctx = new ShardDbContext();
            var con = Open(ctx);
            using var cmd = NewCommand(con);
            var names = ids.Select((id, i) => "@b" + i).ToList();
            cmd.CommandText = SelectColumns + "WHERE batch_id IN (" + string.Join(",", names) + ") ORDER BY id DESC";
            for (var i = 0; i < ids.Count; i++)
                AddParam(cmd, "@b" + i, ids[i]);
            return Read(cmd);
        }

        /// <summary>The rows with these ids, newest first. Unknown ids are left out.</summary>
        public static List<ZoneControlChange> GetMany(IReadOnlyCollection<long> ids)
        {
            if (ids == null || ids.Count == 0)
                return new List<ZoneControlChange>();
            EnsureSchema();
            using var ctx = new ShardDbContext();
            var con = Open(ctx);
            using var cmd = NewCommand(con);
            var names = ids.Select((id, i) => "@id" + i).ToList();
            cmd.CommandText = SelectColumns + "WHERE id IN (" + string.Join(",", names) + ") ORDER BY id DESC";
            var n = 0;
            foreach (var id in ids)
                AddParam(cmd, "@id" + n++, id);
            return Read(cmd);
        }

        private const string SelectColumns = "SELECT id, changed_at, actor, source, zone_name, variation, wcid, rank_key, stat, old_curve, new_curve, reverts_id, kind, reason, batch_id, section, old_json, new_json FROM zone_control_change_log ";

        private static List<ZoneControlChange> Read(DbCommand cmd)
        {
            var rows = new List<ZoneControlChange>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                rows.Add(new ZoneControlChange
                {
                    Id = r.GetInt64(0),
                    ChangedAtUtc = DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc),
                    Actor = r.GetString(2),
                    Source = r.GetString(3),
                    Zone = r.IsDBNull(4) ? null : r.GetString(4),
                    Variation = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                    Wcid = r.IsDBNull(6) ? (uint?)null : Convert.ToUInt32(r.GetValue(6)),
                    Rank = r.GetString(7),
                    Stat = r.GetString(8),
                    OldCurveJson = r.IsDBNull(9) ? null : r.GetString(9),
                    NewCurveJson = r.IsDBNull(10) ? null : r.GetString(10),
                    RevertsId = r.IsDBNull(11) ? (long?)null : r.GetInt64(11),
                    Kind = r.IsDBNull(12) ? KindStat : r.GetString(12),
                    Reason = r.IsDBNull(13) ? null : r.GetString(13),
                    BatchId = r.IsDBNull(14) ? null : r.GetString(14),
                    Section = r.IsDBNull(15) ? null : r.GetString(15),
                    OldJson = r.IsDBNull(16) ? null : r.GetString(16),
                    NewJson = r.IsDBNull(17) ? null : r.GetString(17),
                });
            return rows;
        }

        private static void EnsureSchema()
        {
            if (_schemaReady) return;
            lock (_schemaLock)
            {
                if (_schemaReady) return;
                using var ctx = new ShardDbContext();
                var con = Open(ctx);
                using var cmd = NewCommand(con);
                // same statements as Database/Updates/Shard/2026-11-02-00-ZoneControl-Change-Log.sql
                cmd.CommandText = CreateTableSql;
                cmd.ExecuteNonQuery();

                // CREATE TABLE IF NOT EXISTS leaves a table made by an older build alone, so add any column it lacks. MySQL has no
                // ADD COLUMN IF NOT EXISTS, hence the information_schema check; each ALTER runs only when its column is missing.
                var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'zone_control_change_log'";
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        have.Add(r.GetString(0));
                foreach (var (column, ddl) in AddedColumns)
                {
                    if (have.Contains(column))
                        continue;
                    cmd.CommandText = "ALTER TABLE `zone_control_change_log` " + ddl;
                    cmd.ExecuteNonQuery();
                    log.Info($"[ZoneControl] change log: added column {column}");
                }
                _schemaReady = true;
            }
        }

        private const string CreateTableSql = @"
CREATE TABLE IF NOT EXISTS `zone_control_change_log` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `changed_at` DATETIME NOT NULL,
  `actor` VARCHAR(64) NOT NULL,
  `source` VARCHAR(16) NOT NULL,
  `zone_name` VARCHAR(128) NULL,
  `variation` INT NULL,
  `wcid` INT UNSIGNED NULL,
  `rank_key` VARCHAR(16) NOT NULL,
  `stat` VARCHAR(128) NOT NULL,
  `old_curve` TEXT NULL,
  `new_curve` TEXT NULL,
  `reverts_id` BIGINT NULL,
  `kind` VARCHAR(16) NOT NULL DEFAULT 'stat',
  `reason` VARCHAR(255) NULL,
  `batch_id` VARCHAR(64) NULL,
  `section` VARCHAR(160) NULL,
  `old_json` MEDIUMTEXT NULL,
  `new_json` MEDIUMTEXT NULL,
  PRIMARY KEY (`id`),
  KEY `ix_zone_name` (`zone_name`, `id`),
  KEY `ix_variation` (`variation`, `id`),
  KEY `ix_batch` (`batch_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

        /// <summary>Columns added after the first version of the table, in order. The column name is checked; the DDL adds it.</summary>
        private static readonly (string Column, string Ddl)[] AddedColumns =
        {
            ("kind", "ADD COLUMN `kind` VARCHAR(16) NOT NULL DEFAULT 'stat' AFTER `reverts_id`"),
            ("reason", "ADD COLUMN `reason` VARCHAR(255) NULL AFTER `kind`"),
            ("batch_id", "ADD COLUMN `batch_id` VARCHAR(64) NULL AFTER `reason`, ADD KEY `ix_batch` (`batch_id`)"),
            ("section", "ADD COLUMN `section` VARCHAR(160) NULL AFTER `batch_id`"),
            ("old_json", "ADD COLUMN `old_json` MEDIUMTEXT NULL AFTER `section`"),
            ("new_json", "ADD COLUMN `new_json` MEDIUMTEXT NULL AFTER `old_json`"),
        };

        private static DbConnection Open(ShardDbContext ctx)
        {
            var con = ctx.Database.GetDbConnection();
            if (con.State != ConnectionState.Open)
                con.Open();
            return con;
        }

        private static DbCommand NewCommand(DbConnection con)
        {
            var cmd = con.CreateCommand();
            cmd.CommandTimeout = 10;
            return cmd;
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
