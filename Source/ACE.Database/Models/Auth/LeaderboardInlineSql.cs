using System;
using ACE.Entity.Enum.Properties;

namespace ACE.Database.Models.Auth;

/// <summary>
/// Raw SELECTs for leaderboards (same logic as Database/Updates/Authentication/leaderboard_*_sp.sql).
/// Used so /top and the web portal work when auth DB has no stored procedures installed.
/// Requires auth MySQL user to have SELECT on ace_shard.* (same as CALL-based procs).
/// Character exclusion: <see cref="PropertyBool.ExcludeFromLeaderboards"/> (9011) on biota_properties_bool.
/// Admin/staff exclusion: filters out accounts where ace_auth.account.accessLevel &gt; 0.
/// </summary>
public static class LeaderboardInlineSql
{
    private const ushort ExcludeFromLeaderboardsBoolType = (ushort)PropertyBool.ExcludeFromLeaderboards;
    private const ushort IsMuleBoolType = (ushort)PropertyBool.IsMule;
    private const string NonAdminAccountJoin = "INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0";

    /// <summary>Output columns must match Leaderboard + AuthDbContext: Score, Account, Character, LeaderboardID.</summary>
    public const string TopLum = """
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = 9005
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = 131
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """;

    public const string TopBank = """
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = 9004
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = 131
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """;

    public const string TopLevel = """
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 25
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = 131
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """;

    public const string TopEnlightenment = """
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 390
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = 131
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """;

    public const string TopTitles = """
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 262
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = 131
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """;

    public const string TopDeaths = """
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 43
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = 131
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """;

    /// <summary>QB is per account; entire account is omitted if any non-deleted character has ExcludeFromLeaderboards (9011).</summary>
    public const string TopQuestBonus = """
        SELECT (COUNT(*) + (SELECT COUNT(*) FROM account_quest a2 WHERE a.accountId = a2.accountId AND a2.num_Times_Completed >= 1)) AS Score,
               a.accountId AS Account,
               (SELECT c.name FROM ace_shard.character c
                WHERE c.account_Id = a.accountId AND c.is_Deleted = 0
                ORDER BY c.total_Logins DESC LIMIT 1) AS `Character`,
               a.accountId AS LeaderboardID
        FROM account_quest a
        INNER JOIN account acc ON acc.accountId = a.accountId AND acc.accessLevel = 0
        GROUP BY a.accountId
        HAVING `Character` IS NOT NULL
        AND NOT EXISTS (
            SELECT 1 FROM ace_shard.character cex
            INNER JOIN ace_shard.biota_properties_bool bx ON bx.object_id = cex.id AND bx.type = 9011 AND bx.value <> 0
            WHERE cex.account_Id = a.accountId AND cex.is_Deleted = 0
        )
        AND NOT EXISTS (
            SELECT 1 FROM ace_shard.character cm
            INNER JOIN ace_shard.biota_properties_bool bm ON bm.object_id = cm.id AND bm.type = 131 AND bm.value <> 0
            WHERE cm.account_Id = a.accountId AND cm.is_Deleted = 0
        )
        ORDER BY Score DESC
        LIMIT 25
        """;

    /// <summary>
    /// Primary attributes leaderboard: sums <c>level_From_C_P</c> (ranks purchased) for types 1–6.
    /// This is intentionally NOT CP spent (which is very large and confusing on a leaderboard).
    /// </summary>
    public const string TopAttributes = """
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
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = 131
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        HAVING Score > 0
        ORDER BY Score DESC
        LIMIT 25
        """;

    /// <summary>
    /// Luminance aug total: int64 purchase counts (life/creature/war/etc.) plus int rating-style augs (333–345, 365).
    /// Int64 types match PropertyInt64 LumAug*Count (9007–9011, 9016–9018, 9022–9026); bool 9011 is a different table.
    /// </summary>
    public const string TopAugments = """
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
        INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = 131
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        HAVING Score > 0
        ORDER BY Score DESC
        LIMIT 25
        """;

    /// <summary>Top characters by a single PropertyInt64 row (e.g. banked currencies). Excludes <see cref="PropertyBool.ExcludeFromLeaderboards"/>.</summary>
    public static string TopByBiotaInt64Property(ushort int64PropertyType) => $"""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        {NonAdminAccountJoin}
        INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = {int64PropertyType}
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = {ExcludeFromLeaderboardsBoolType}
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """;

    public static readonly string TopBankedEnlightenedCoins = TopByBiotaInt64Property((ushort)PropertyInt64.BankedEnlightenedCoins);

    public static readonly string TopBankedWeaklyEnlightenedCoins = TopByBiotaInt64Property((ushort)PropertyInt64.BankedWeaklyEnlightenedCoins);

    public static readonly string TopBankedMythicalKeys = TopByBiotaInt64Property((ushort)PropertyInt64.BankedMythicalKeys);

    public static readonly string TopBankedLegendaryKeys = TopByBiotaInt64Property((ushort)PropertyInt64.BankedLegendaryKeys);

    /// <summary>Best character row for account + global rank (MySQL 8 ROW_NUMBER). Same filters/order as top list.</summary>
    public static FormattableString SelfPlacementLum(uint accountId) => $"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = 9005
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementBank(uint accountId) => $"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = 9004
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementLevel(uint accountId) => $"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 25
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementEnlightenment(uint accountId) => $"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 390
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementTitles(uint accountId) => $"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 262
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementDeaths(uint accountId) => $"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = 43
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementQuestBonus(uint accountId) => $"""
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
            INNER JOIN account acc ON acc.accountId = a.accountId AND acc.accessLevel = 0
            GROUP BY a.accountId
            HAVING `Character` IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM ace_shard.character cex
                INNER JOIN ace_shard.biota_properties_bool bx ON bx.object_id = cex.id AND bx.type = 9011 AND bx.value <> 0
                WHERE cex.account_Id = a.accountId AND cex.is_Deleted = 0
            )
            AND NOT EXISTS (
                SELECT 1 FROM ace_shard.character cm
                INNER JOIN ace_shard.biota_properties_bool bm ON bm.object_id = cm.id AND bm.type = 131 AND bm.value <> 0
                WHERE cm.account_Id = a.accountId AND cm.is_Deleted = 0
            )
          ) agg
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementAttributes(uint accountId) => $"""
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
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
            HAVING Score > 0
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementAugments(uint accountId) => $"""
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
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = 9011
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
            HAVING Score > 0
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    public static FormattableString SelfPlacementBankedInt64(ushort int64PropertyType, uint accountId) => $"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            INNER JOIN ace_shard.biota_properties_int64 i ON i.object_id = c.id AND i.type = {int64PropertyType}
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = {ExcludeFromLeaderboardsBoolType}
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;

    /// <summary>Top list by a persisted <see cref="PropertyInt"/> on the character (e.g. discipline stats).</summary>
    public static string TopCharacterIntStat(ushort intPropertyType) => $"""
        SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
        FROM ace_shard.character c
        {NonAdminAccountJoin}
        INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = {intPropertyType}
        LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = {ExcludeFromLeaderboardsBoolType}
        LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
        WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
        ORDER BY i.value DESC
        LIMIT 25
        """;

    public static readonly string TopTimesJailed = TopCharacterIntStat((ushort)PropertyInt.TimesJailed);

    public static readonly string TopUcmChecksPassed = TopCharacterIntStat((ushort)PropertyInt.TimesUcmCheckPassed);

    public static FormattableString SelfPlacementCharacterInt(ushort intPropertyType, uint accountId) => $"""
        SELECT ranked.PlacementRank, ranked.Score, ranked.Account, ranked.Character, ranked.LeaderboardID
        FROM (
          SELECT innerq.Score, innerq.Account, innerq.Character, innerq.LeaderboardID,
                 ROW_NUMBER() OVER (ORDER BY innerq.Score DESC, innerq.LeaderboardID DESC) AS PlacementRank
          FROM (
            SELECT i.value AS Score, c.account_Id AS Account, c.name AS `Character`, c.id AS LeaderboardID
            FROM ace_shard.character c
            INNER JOIN account a ON a.accountId = c.account_Id AND a.accessLevel = 0
            INNER JOIN ace_shard.biota_properties_int i ON i.object_id = c.id AND i.type = {intPropertyType}
            LEFT JOIN ace_shard.biota_properties_bool b ON b.object_id = c.id AND b.type = {ExcludeFromLeaderboardsBoolType}
            LEFT JOIN ace_shard.biota_properties_bool bm ON bm.object_id = c.id AND bm.type = {IsMuleBoolType}
            WHERE c.is_Deleted = 0 AND (b.value IS NULL OR b.value = 0) AND (bm.value IS NULL OR bm.value = 0)
          ) innerq
        ) ranked
        WHERE ranked.Account = {accountId}
        ORDER BY ranked.Score DESC
        LIMIT 1
        """;
}
