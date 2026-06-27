using System;
using ACE.Entity.Enum.Properties;

namespace ACE.Database.Models.Auth;

/// <summary>
/// Raw SELECTs for leaderboards (same logic as Database/Updates/Authentication/leaderboard_*_sp.sql).
/// Used so /top and the web portal work when auth DB has no stored procedures installed.
/// Requires auth MySQL user to have SELECT on ace_shard.* (same as CALL-based procs).
/// Character exclusion: <see cref="PropertyBool.ExcludeFromLeaderboards"/> (9011) on biota_properties_bool.
/// Account exclusion: non-player access levels (staff) and accounts with an active ban (ban_Expire_Time in the future).
/// Mule characters (PropertyBool.IsMule) are eligible unless individually flagged ExcludeFromLeaderboards.
/// </summary>
public static class LeaderboardInlineSql
{
    private static string ShardDb => Common.ConfigManager.Config?.MySql?.Shard?.Database ?? "ace_shard";

    private static string ResolveSql(string sql) =>
        sql.Replace("ace_shard.", $"`{ShardDb}`.");

    private static FormattableString ResolveFormattableSql(FormattableString sql) =>
        System.Runtime.CompilerServices.FormattableStringFactory.Create(
            sql.Format.Replace("ace_shard.", $"`{ShardDb}`."),
            sql.GetArguments()
        );
    private const ushort ExcludeFromLeaderboardsBoolType = (ushort)PropertyBool.ExcludeFromLeaderboards;

    /// <summary>Player accounts that are not currently banned.</summary>
    private const string EligibleAccountJoin =
        "INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())";

    private const string EligibleAccountJoinAliasAcc =
        "INNER JOIN account acc ON acc.accountId = a.accountId AND acc.accessLevel = 0 AND (acc.ban_Expire_Time IS NULL OR acc.ban_Expire_Time <= UTC_TIMESTAMP())";

    /// <summary>Output columns must match Leaderboard + AuthDbContext: Score, Account, Character, LeaderboardID.</summary>
    public static readonly string TopLum = ResolveSql("""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = 9005
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        ORDER BY i.value DESC, c.id DESC
        LIMIT 25
        """);

    public static readonly string TopBank = ResolveSql("""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = 9004
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        ORDER BY i.value DESC, c.id DESC
        LIMIT 25
        """);

    public static readonly string TopLevel = ResolveSql("""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 25
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        ORDER BY i.value DESC, c.id DESC
        LIMIT 25
        """);

    public static readonly string TopEnlightenment = ResolveSql("""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 390
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        ORDER BY i.value DESC, c.id DESC
        LIMIT 25
        """);

    public static readonly string TopTitles = ResolveSql("""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 262
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """);

    public static readonly string TopDeaths = ResolveSql("""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 43
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        ORDER BY i.value DESC, c.id DESC
        LIMIT 25
        """);

    /// <summary>QB is per account; entire account is omitted if any non-deleted character has ExcludeFromLeaderboards (9011).</summary>
    public static readonly string TopQuestBonus = ResolveSql("""
        SELECT (COUNT(*) + (SELECT COUNT(*) FROM account_quest a2 WHERE a.accountId = a2.accountId AND a2.num_Times_Completed >= 1)) AS Score,
               a.accountId AS Account,
               (SELECT c.name FROM ace_shard.character c
                WHERE c.account_Id = a.accountId AND c.is_Deleted = 0
                ORDER BY c.total_Logins DESC LIMIT 1) AS `Character`,
               a.accountId AS LeaderboardID
        FROM account_quest a
        INNER JOIN account acc ON acc.accountId = a.accountId AND acc.accessLevel = 0 AND (acc.ban_Expire_Time IS NULL OR acc.ban_Expire_Time <= UTC_TIMESTAMP())
        GROUP BY a.accountId
        HAVING `Character` IS NOT NULL
        AND NOT EXISTS (
            SELECT 1 FROM ace_shard.character cex
            INNER JOIN ace_shard.biota_properties_bool bx ON bx.object_id = cex.id AND bx.type = 9011 AND bx.value <> 0
            WHERE cex.account_Id = a.accountId AND cex.is_Deleted = 0
        )
        ORDER BY Score DESC, a.accountId DESC
        LIMIT 25
        """);

    /// <summary>
    /// Primary attributes leaderboard: sums <c>level_From_C_P</c> (ranks purchased) for types 1–6.
    /// This is intentionally NOT CP spent (which is very large and confusing on a leaderboard).
    /// </summary>
    public static readonly string TopAttributes = ResolveSql("""
        SELECT (
            COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 1), 0) +
            COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 2), 0) +
            COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 3), 0) +
            COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 4), 0) +
            COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 5), 0) +
            COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 6), 0)
        ) AS Score,
        c.account_Id AS Account,
        c.name AS `Character`,
        c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        HAVING Score > 0
        ORDER BY Score DESC, c.id DESC
        LIMIT 25
        """);

    /// <summary>
    /// Luminance aug total: int64 purchase counts (life/creature/war/etc.) plus int rating-style augs (333–345, 365).
    /// Int64 types match PropertyInt64 LumAug*Count (9007–9011, 9016–9018, 9022–9026); bool 9011 is a different table.
    /// </summary>
    public static readonly string TopAugments = ResolveSql("""
        SELECT (
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9007), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9008), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9009), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9010), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9011), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9016), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9017), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9018), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9022), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9023), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9024), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9025), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9026), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 333), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 334), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 335), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 336), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 337), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 338), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 339), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 340), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 341), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 342), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 343), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 344), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 345), 0) +
            COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 365), 0)
        ) AS Score,
        c.account_Id AS Account,
        c.name AS `Character`,
        c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        HAVING Score > 0
        ORDER BY Score DESC, c.id DESC
        LIMIT 25
        """);

    /// <summary>Top characters by a single PropertyInt64 row (e.g. banked currencies). Excludes <see cref="PropertyBool.ExcludeFromLeaderboards"/>.</summary>
    public static string TopByBiotaInt64Property(ushort int64PropertyType) => ResolveSql($"""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        {EligibleAccountJoin}
        INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = {int64PropertyType}
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = {ExcludeFromLeaderboardsBoolType}
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        ORDER BY i.value DESC, c.id DESC
        LIMIT 25
        """);

    public static readonly string TopBankedEnlightenedCoins = TopByBiotaInt64Property((ushort)PropertyInt64.BankedEnlightenedCoins);

    public static readonly string TopBankedWeaklyEnlightenedCoins = TopByBiotaInt64Property((ushort)PropertyInt64.BankedWeaklyEnlightenedCoins);

    public static readonly string TopBankedMythicalKeys = TopByBiotaInt64Property((ushort)PropertyInt64.BankedMythicalKeys);

    public static readonly string TopBankedLegendaryKeys = TopByBiotaInt64Property((ushort)PropertyInt64.BankedLegendaryKeys);

    /// <summary>Best character row for account + global rank (MySQL 8 ROW_NUMBER). Same filters/order as top list.</summary>
    public static FormattableString SelfPlacementLum(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = 9005
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementBank(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = 9004
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementLevel(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 25
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementEnlightenment(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 390
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementTitles(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 262
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementDeaths(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 43
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementQuestBonus(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT agg.Score, agg.Account, agg.Character, agg.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY agg.Score DESC, agg.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT (COUNT(*) + (SELECT COUNT(*) FROM account_quest a2 WHERE a.accountId = a2.accountId AND a2.num_Times_Completed >= 1)) AS Score,
                   a.accountId AS Account,
                   (SELECT c.name FROM ace_shard.character c
                    WHERE c.account_Id = a.accountId AND c.is_Deleted = 0
                    ORDER BY c.total_Logins DESC LIMIT 1) AS `Character`,
                   a.accountId AS LeaderboardID
            FROM account_quest a
            INNER JOIN account acc ON acc.accountId = a.accountId AND acc.accessLevel = 0 AND (acc.ban_Expire_Time IS NULL OR acc.ban_Expire_Time <= UTC_TIMESTAMP())
            GROUP BY a.accountId
            HAVING `Character` IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM ace_shard.character cex
                INNER JOIN ace_shard.biota_properties_bool bx ON bx.object_id = cex.id AND bx.type = 9011 AND bx.value <> 0
                WHERE cex.account_Id = a.accountId AND cex.is_Deleted = 0
            )
          ) agg
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementAttributes(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT (
                COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 1), 0) +
                COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 2), 0) +
                COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 3), 0) +
                COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 4), 0) +
                COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 5), 0) +
                COALESCE((SELECT level_From_C_P FROM ace_shard.biota_properties_attribute WHERE object_Id = c.id AND type = 6), 0)
            ) AS Score,
            c.account_Id AS Account,
            c.name AS `Character`,
            c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
            HAVING Score > 0
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementAugments(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT (
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9007), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9008), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9009), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9010), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9011), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9016), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9017), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9018), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9022), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9023), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9024), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9025), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int64 WHERE object_id = c.id AND type = 9026), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 333), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 334), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 335), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 336), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 337), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 338), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 339), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 340), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 341), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 342), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 343), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 344), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 345), 0) +
                COALESCE((SELECT value FROM ace_shard.biota_properties_int WHERE object_id = c.id AND type = 365), 0)
            ) AS Score,
            c.account_Id AS Account,
            c.name AS `Character`,
            c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
            HAVING Score > 0
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementBankedInt64(ushort int64PropertyType, uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = {int64PropertyType}
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = {ExcludeFromLeaderboardsBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    /// <summary>Top list by a persisted <see cref="PropertyInt"/> on the character (e.g. discipline stats).</summary>
    public static string TopCharacterIntStat(ushort intPropertyType) => ResolveSql($"""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        {EligibleAccountJoin}
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = {intPropertyType}
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = {ExcludeFromLeaderboardsBoolType}
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        ORDER BY i.value DESC, c.id DESC
        LIMIT 25
        """);

    public static readonly string TopTimesJailed = TopCharacterIntStat((ushort)PropertyInt.TimesJailed);

    public static readonly string TopUcmChecksPassed = TopCharacterIntStat((ushort)PropertyInt.TimesUcmCheckPassed);

    public static FormattableString SelfPlacementCharacterInt(ushort intPropertyType, uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = {intPropertyType}
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = {ExcludeFromLeaderboardsBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    /// <summary>Max pet bond level per character (PropertyInt64 9052 attuned character, PropertyInt 9053 bond level).</summary>
    public static readonly string TopBonds = ResolveSql("""
        SELECT MAX(COALESCE(bond.value, 1)) AS Score,
               c.account_Id AS Account,
               c.name AS `Character`,
               c.id AS LeaderboardID
        FROM ace_shard.biota_properties_int64 att
        INNER JOIN ace_shard.character c ON c.id = CAST(att.value AS UNSIGNED) AND att.type = 9052
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        LEFT JOIN ace_shard.biota_properties_int bond ON bond.object_id = att.object_id AND bond.type = 9053
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        GROUP BY c.id, c.account_Id, c.name
        ORDER BY Score DESC, c.name ASC, c.id DESC
        LIMIT 25
        """);

    /// <summary>Sum of pet bond levels per character.</summary>
    public static readonly string TopSumBonds = ResolveSql("""
        SELECT SUM(COALESCE(bond.value, 1)) AS Score,
               c.account_Id AS Account,
               c.name AS `Character`,
               c.id AS LeaderboardID
        FROM ace_shard.biota_properties_int64 att
        INNER JOIN ace_shard.character c ON c.id = CAST(att.value AS UNSIGNED) AND att.type = 9052
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        LEFT JOIN ace_shard.biota_properties_int bond ON bond.object_id = att.object_id AND bond.type = 9053
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        GROUP BY c.id, c.account_Id, c.name
        HAVING SUM(COALESCE(bond.value, 1)) > 0
        ORDER BY Score DESC, c.name ASC, c.id DESC
        LIMIT 25
        """);

    public static FormattableString SelfPlacementBonds(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.Character ASC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT MAX(COALESCE(bond.value, 1)) AS Score,
                   c.account_Id AS Account,
                   c.name AS `Character`,
                   c.id AS LeaderboardID
            FROM ace_shard.biota_properties_int64 att
            INNER JOIN ace_shard.character c ON c.id = CAST(att.value AS UNSIGNED) AND att.type = 9052
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            LEFT JOIN ace_shard.biota_properties_int bond ON bond.object_id = att.object_id AND bond.type = 9053
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
            GROUP BY c.id, c.account_Id, c.name
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    public static FormattableString SelfPlacementSumBonds(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.Character ASC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT SUM(COALESCE(bond.value, 1)) AS Score,
                   c.account_Id AS Account,
                   c.name AS `Character`,
                   c.id AS LeaderboardID
            FROM ace_shard.biota_properties_int64 att
            INNER JOIN ace_shard.character c ON c.id = CAST(att.value AS UNSIGNED) AND att.type = 9052
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            LEFT JOIN ace_shard.biota_properties_int bond ON bond.object_id = att.object_id AND bond.type = 9053
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
            GROUP BY c.id, c.account_Id, c.name
            HAVING SUM(COALESCE(bond.value, 1)) > 0
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);

    /// <summary>Max pet potency level per character (PropertyInt64 9052 attuned character, PropertyInt 9056 potency level).</summary>
    public static readonly string TopPotency = ResolveSql("""
        SELECT MAX(COALESCE(pot.value, 0)) AS Score,
               c.account_Id AS Account,
               c.name AS `Character`,
               c.id AS LeaderboardID
        FROM ace_shard.biota_properties_int64 att
        INNER JOIN ace_shard.character c ON c.id = CAST(att.value AS UNSIGNED) AND att.type = 9052
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
        LEFT JOIN ace_shard.biota_properties_int pot ON pot.object_id = att.object_id AND pot.type = 9056
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
        GROUP BY c.id, c.account_Id, c.name
        ORDER BY Score DESC, c.name ASC, c.id DESC
        LIMIT 25
        """);

    public static FormattableString SelfPlacementPotency(uint accountId) => ResolveFormattableSql($"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.Character ASC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT MAX(COALESCE(pot.value, 0)) AS Score,
                   c.account_Id AS Account,
                   c.name AS `Character`,
                   c.id AS LeaderboardID
            FROM ace_shard.biota_properties_int64 att
            INNER JOIN ace_shard.character c ON c.id = CAST(att.value AS UNSIGNED) AND att.type = 9052
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0 AND (a.ban_Expire_Time IS NULL OR a.ban_Expire_Time <= UTC_TIMESTAMP())
            LEFT JOIN ace_shard.biota_properties_int pot ON pot.object_id = att.object_id AND pot.type = 9056
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0)
            GROUP BY c.id, c.account_Id, c.name
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """);
}
