using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

using log4net;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

using ACE.Database.Models.Shard;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Long-term kill rate history (2026-11-26): every finished hour of <see cref="KillRateTracker"/>, one row per character,
    /// in the shard table kill_rate_hour, so trends go back further than the 48 hours kept in memory.
    ///
    /// Written by the tracker's 1-minute timer when an hour ends (about one row per active character per hour, in one
    /// transaction) and at shutdown (the running hour, partial = 1); never on the kill path or a game thread. Rows are upserted
    /// on (character_id, hour_utc, run_id), so a retry of a batch that did land is harmless, and a restart mid-hour leaves one
    /// row per server run that reads add up. A batch that fails stays queued and is retried each minute for up to
    /// <see cref="RetryHours"/> hours. Rows older than kill_rate_history_days (default 365; 0 = keep all) are deleted once a day.
    ///
    /// The server creates the table the first time it needs it; Database/Updates/Shard/2026-11-26-00-Kill-Rate-Hour.sql holds
    /// the same statement for anyone who prefers to run it by hand.
    /// </summary>
    public static class KillRateHistory
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(KillRateHistory));

        public const string Table = "kill_rate_hour";

        /// <summary>Hours a batch that cannot be written is kept and retried before it is dropped (with an error in the log).</summary>
        public const int RetryHours = 48;

        private static readonly object writeLock = new();
        private static readonly object schemaLock = new();
        private static volatile bool schemaReady;

        /// <summary>Records waiting to be written (normally empty: a batch is written the minute its hour ends).</summary>
        private static readonly List<KillRateStore.HourRecord> pending = new();

        /// <summary>Unix seconds of the first failure in the current run of failures (0 = the last write worked).</summary>
        private static long failingSince;

        /// <summary>
        /// Queues <paramref name="records"/> and writes everything queued in one transaction. On failure the records stay
        /// queued for the next call (each minute); records whose hour ended more than RetryHours ago are dropped with an error.
        /// Called from the tracker's timer and at shutdown, never from a game thread.
        /// </summary>
        /// <param name="records">Finished (or, at shutdown, partial) hours to save; may be empty (then only a retry runs).</param>
        /// <param name="final">The server is stopping: there is no next minute, so a failure is logged as lost (the [KILLRATE]
        /// lines in the server log still hold the counts).</param>
        public static void Save(IEnumerable<KillRateStore.HourRecord> records, bool final = false)
        {
            lock (writeLock)
            {
                pending.AddRange(records);
                if (pending.Count == 0)
                    return;

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var tooOld = KillRateStore.HourOf(now) - RetryHours;
                var dropped = pending.RemoveAll(r => r.Hour < tooOld);
                if (dropped > 0)
                    log.Error($"[KillRateHistory] dropped {dropped} hour row(s) that could not be saved for {RetryHours} hours");
                if (pending.Count == 0)
                    return;

                try
                {
                    EnsureSchema();
                    using var ctx = new ShardDbContext();
                    var con = Open(ctx);
                    using var tx = con.BeginTransaction();
                    foreach (var r in pending)
                        Upsert(con, tx, r);
                    tx.Commit();

                    if (failingSince != 0)
                        log.Info($"[KillRateHistory] saving works again ({pending.Count} queued row(s) written)");
                    failingSince = 0;
                    pending.Clear();
                }
                catch (Exception ex)
                {
                    if (final)
                    {
                        log.Error($"[KillRateHistory] shutdown: {pending.Count} hour row(s) not saved (the server is stopping; the [KILLRATE] log lines hold them): {ex.GetBaseException().Message}");
                        return;
                    }
                    // one error when the trouble starts, then quiet retries (the dropped-rows error above says if it never ends)
                    if (failingSince == 0)
                    {
                        failingSince = now;
                        log.Error($"[KillRateHistory] saving {pending.Count} hour row(s) failed, retrying each minute: {ex.GetBaseException().Message}");
                    }
                }
            }
        }

        /// <summary>Deletes rows older than kill_rate_history_days (0 = keep everything). Once a day, from the tracker's timer.</summary>
        /// <param name="now">Unix seconds now.</param>
        public static void Prune(long now)
        {
            var days = ServerConfig.kill_rate_history_days.Value;
            if (days <= 0)
                return;

            try
            {
                EnsureSchema();
                using var ctx = new ShardDbContext();
                var con = Open(ctx);
                using var cmd = NewCommand(con);
                cmd.CommandText = $"DELETE FROM `{Table}` WHERE hour_utc < @cutoff";
                AddParam(cmd, "@cutoff", KillRateStore.HourStartUtc(KillRateStore.HourOf(now) - days * 24));
                var n = cmd.ExecuteNonQuery();
                if (n > 0)
                    log.Info($"[KillRateHistory] pruned {n} hour row(s) older than {days} days");
            }
            catch (Exception ex)
            {
                log.Error($"[KillRateHistory] prune failed: {ex.GetBaseException().Message}");
            }
        }

        /// <summary>
        /// Every saved row from <paramref name="fromHour"/> to <paramref name="toHour"/> (UTC hour numbers, both included), all
        /// server runs. Throws when the database can't be read: the portal turns that into an error, never an empty report.
        /// </summary>
        public static List<KillRateStore.HourRecord> Load(long fromHour, long toHour)
        {
            EnsureSchema();
            using var ctx = new ShardDbContext();
            var con = Open(ctx);
            using var cmd = NewCommand(con);
            cmd.CommandText = $@"SELECT character_id, character_name, hour_utc, run_id, own_tier, kills_by_tier,
                melee_kills, missile_kills, war_kills, void_kills, pet_kills, other_kills, first_kill, last_kill, `partial`
                FROM `{Table}` WHERE hour_utc BETWEEN @from AND @to";
            AddParam(cmd, "@from", KillRateStore.HourStartUtc(fromHour));
            AddParam(cmd, "@to", KillRateStore.HourStartUtc(toHour));

            var list = new List<KillRateStore.HourRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var styles = new int[KillRateStore.StyleCount];
                for (var s = 0; s < KillRateStore.StyleCount; s++)
                    styles[s] = r.GetInt32(6 + s);
                list.Add(new KillRateStore.HourRecord
                {
                    Guid = Convert.ToUInt32(r.GetValue(0)),
                    Name = r.GetString(1),
                    Hour = KillRateStore.HourOf(Unix(r.GetDateTime(2))),
                    RunId = r.GetInt64(3),
                    OwnTier = Convert.ToInt32(r.GetValue(4)),
                    Kills = ParseTiers(r.GetString(5)),
                    Styles = styles,
                    First = Unix(r.GetDateTime(12)),
                    Last = Unix(r.GetDateTime(13)),
                    Partial = Convert.ToBoolean(r.GetValue(14)),
                });
            }
            return list;
        }

        /// <summary>Kills per tier as stored: a JSON object of the tiers with kills, {"15": 730, "14": 2}.</summary>
        internal static string TiersJson(int[] kills)
        {
            var map = new SortedDictionary<int, int>();
            for (var i = 0; i < kills.Length; i++)
                if (kills[i] > 0)
                    map[KillRateStore.MinTier + i] = kills[i];
            return JsonConvert.SerializeObject(map);
        }

        /// <summary>The stored JSON back to kills per tier (index tier - MinTier); tiers outside T11-T25 and bad values are ignored.</summary>
        internal static int[] ParseTiers(string json)
        {
            var kills = new int[KillRateStore.TierCount];
            if (string.IsNullOrWhiteSpace(json))
                return kills;
            var map = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
            foreach (var (key, value) in map)
                if (int.TryParse(key, out var tier) && tier >= KillRateStore.MinTier && tier <= KillRateStore.MaxTier && value > 0)
                    kills[tier - KillRateStore.MinTier] = value;
            return kills;
        }

        /// <summary>Inserts one hour row, or replaces it when this run already saved that hour (a retried batch).</summary>
        private static void Upsert(DbConnection con, DbTransaction tx, KillRateStore.HourRecord r)
        {
            using var cmd = NewCommand(con);
            cmd.Transaction = tx;
            cmd.CommandText = $@"INSERT INTO `{Table}`
                (character_id, hour_utc, run_id, character_name, own_tier, own_kills, total_kills, kills_by_tier,
                 melee_kills, missile_kills, war_kills, void_kills, pet_kills, other_kills, first_kill, last_kill, `partial`)
                VALUES (@cid, @hour, @run, @name, @own_tier, @own, @total, @tiers,
                 @melee, @missile, @war, @void, @pet, @other, @first, @last, @partial)
                ON DUPLICATE KEY UPDATE character_name = VALUES(character_name), own_tier = VALUES(own_tier),
                 own_kills = VALUES(own_kills), total_kills = VALUES(total_kills), kills_by_tier = VALUES(kills_by_tier),
                 melee_kills = VALUES(melee_kills), missile_kills = VALUES(missile_kills), war_kills = VALUES(war_kills),
                 void_kills = VALUES(void_kills), pet_kills = VALUES(pet_kills), other_kills = VALUES(other_kills),
                 first_kill = VALUES(first_kill), last_kill = VALUES(last_kill), `partial` = VALUES(`partial`)";
            AddParam(cmd, "@cid", r.Guid);
            AddParam(cmd, "@hour", KillRateStore.HourStartUtc(r.Hour));
            AddParam(cmd, "@run", r.RunId);
            AddParam(cmd, "@name", r.Name ?? "");
            AddParam(cmd, "@own_tier", r.OwnTier);
            AddParam(cmd, "@own", r.Own);
            AddParam(cmd, "@total", r.Total);
            AddParam(cmd, "@tiers", TiersJson(r.Kills));
            AddParam(cmd, "@melee", r.Styles[(int)KillStyle.Melee]);
            AddParam(cmd, "@missile", r.Styles[(int)KillStyle.Missile]);
            AddParam(cmd, "@war", r.Styles[(int)KillStyle.War]);
            AddParam(cmd, "@void", r.Styles[(int)KillStyle.Void]);
            AddParam(cmd, "@pet", r.Styles[(int)KillStyle.Pet]);
            AddParam(cmd, "@other", r.Styles[(int)KillStyle.Other]);
            AddParam(cmd, "@first", DateTimeOffset.FromUnixTimeSeconds(r.First).UtcDateTime);
            AddParam(cmd, "@last", DateTimeOffset.FromUnixTimeSeconds(r.Last).UtcDateTime);
            AddParam(cmd, "@partial", r.Partial);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Creates kill_rate_hour if it is missing (once per run). Same statement as the update file.</summary>
        private static void EnsureSchema()
        {
            if (schemaReady)
                return;
            lock (schemaLock)
            {
                if (schemaReady)
                    return;
                using var ctx = new ShardDbContext();
                var con = Open(ctx);
                using var cmd = NewCommand(con);
                cmd.CommandText = CreateTableSql;
                cmd.ExecuteNonQuery();
                schemaReady = true;
            }
        }

        /// <summary>Same statement as Database/Updates/Shard/2026-11-26-00-Kill-Rate-Hour.sql.</summary>
        internal const string CreateTableSql = @"
CREATE TABLE IF NOT EXISTS `kill_rate_hour` (
  `character_id` INT UNSIGNED NOT NULL,
  `hour_utc` DATETIME NOT NULL,
  `run_id` BIGINT NOT NULL,
  `character_name` VARCHAR(64) NOT NULL,
  `own_tier` TINYINT UNSIGNED NOT NULL DEFAULT 0,
  `own_kills` INT NOT NULL DEFAULT 0,
  `total_kills` INT NOT NULL DEFAULT 0,
  `kills_by_tier` JSON NOT NULL,
  `melee_kills` INT NOT NULL DEFAULT 0,
  `missile_kills` INT NOT NULL DEFAULT 0,
  `war_kills` INT NOT NULL DEFAULT 0,
  `void_kills` INT NOT NULL DEFAULT 0,
  `pet_kills` INT NOT NULL DEFAULT 0,
  `other_kills` INT NOT NULL DEFAULT 0,
  `first_kill` DATETIME NOT NULL,
  `last_kill` DATETIME NOT NULL,
  `partial` TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`character_id`, `hour_utc`, `run_id`),
  KEY `ix_kill_rate_hour_hour` (`hour_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Staff kill rates: T11+ kills per character per UTC hour, one row per server run (KillRateHistory)';";

        /// <summary>A stored UTC DATETIME (read back without a kind) as unix seconds.</summary>
        private static long Unix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

        /// <summary>The shard connection of <paramref name="ctx"/>, opened if it is not yet.</summary>
        private static DbConnection Open(ShardDbContext ctx)
        {
            var con = ctx.Database.GetDbConnection();
            if (con.State != ConnectionState.Open)
                con.Open();
            return con;
        }

        /// <summary>A command on <paramref name="con"/> with a short timeout: this runs on a timer, never worth a long wait.</summary>
        private static DbCommand NewCommand(DbConnection con)
        {
            var cmd = con.CreateCommand();
            cmd.CommandTimeout = 10;
            return cmd;
        }

        /// <summary>Adds a parameter; null becomes DBNull.</summary>
        private static void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
