using System;
using System.Data;

using ACE.Database.Models.Shard;
using ACE.Database.Models.World;

using log4net;

using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Ensures ReceiveDamage per-type bitmask column exists on emote tables (shard + world).
    /// Idempotent: safe on every startup. Matches manual ALTER from PR #397 documentation.
    /// Must run before <see cref="ACE.Database.DatabaseManager.Initialize"/> (which loads weenies via EF including emote rows).
    /// </summary>
    public static class EmoteDamageTypeSchema
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EmoteDamageTypeSchema));

        private static readonly object Gate = new();
        private static volatile bool _applied;

        private const string BiotaEmoteTable = "biota_properties_emote";
        private const string WeenieEmoteTable = "weenie_properties_emote";

        private const string AlterShard =
            "ALTER TABLE `biota_properties_emote` ADD COLUMN `damage_type` INT NULL DEFAULT NULL";

        private const string AlterWorld =
            "ALTER TABLE `weenie_properties_emote` ADD COLUMN `damage_type` INT NULL DEFAULT NULL";

        /// <summary>
        /// Adds <c>damage_type</c> to <c>biota_properties_emote</c> and <c>weenie_properties_emote</c> when missing.
        /// </summary>
        public static void EnsureColumns()
        {
            if (_applied)
                return;

            lock (Gate)
            {
                if (_applied)
                    return;

                try
                {
                    EnsureShardColumn();
                    EnsureWorldColumn();
                    _applied = true;
                }
                catch (Exception ex)
                {
                    log.Error($"EmoteDamageTypeSchema: failed to ensure damage_type columns: {ex.Message}", ex);
                }
            }
        }

        private static void EnsureShardColumn()
        {
            using var context = new ShardDbContext();
            if (ColumnExists(context, BiotaEmoteTable))
            {
                log.Debug("biota_properties_emote.damage_type already present (shard).");
                return;
            }

            log.Info("Adding missing column biota_properties_emote.damage_type (shard)...");
            context.Database.ExecuteSqlRaw(AlterShard);
            log.Info("Shard emote table updated: damage_type added.");
        }

        private static void EnsureWorldColumn()
        {
            using var context = new WorldDbContext();
            if (ColumnExists(context, WeenieEmoteTable))
            {
                log.Debug("weenie_properties_emote.damage_type already present (world).");
                return;
            }

            log.Info("Adding missing column weenie_properties_emote.damage_type (world)...");
            context.Database.ExecuteSqlRaw(AlterWorld);
            log.Info("World emote table updated: damage_type added.");
        }

        /// <summary>Table name must be a fixed identifier (constants only).</summary>
        private static bool ColumnExists(DbContext context, string tableName)
        {
            var connection = context.Database.GetDbConnection();
            var openedHere = false;
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
                openedHere = true;
            }

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
SELECT COUNT(*) FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = '{tableName}'
  AND COLUMN_NAME = 'damage_type'";

                var scalar = cmd.ExecuteScalar();
                return Convert.ToInt64(scalar) > 0;
            }
            finally
            {
                if (openedHere)
                    connection.Close();
            }
        }
    }
}
