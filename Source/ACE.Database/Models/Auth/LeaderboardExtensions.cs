using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using log4net;
using ACE.Common;
using ACE.Entity.Enum.Properties;
using System.Threading;

namespace ACE.Database.Models.Auth
{
    public partial class Leaderboard
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Top list for one SQL board (see <see cref="LeaderboardInlineSql.TopBoards"/>), limited to accounts active within
        /// <paramref name="activeDays"/> days (0 = everyone). Returns an empty list if the query fails.
        /// </summary>
        public static async Task<List<Leaderboard>> GetTopBoardAsync(AuthDbContext context, string boardId, string sql, int activeDays)
        {
            try
            {
                return await context.Leaderboard.FromSqlRaw(LeaderboardInlineSql.ForWindow(sql, activeDays)).AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get {boardId} leaderboard: {ex.Message}", ex);
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Best row for the account on this board (highest score among qualifying characters) and global rank, among
        /// accounts active within <paramref name="activeDays"/> days (0 = everyone).
        /// Returns null if the account has no qualifying entry or the query fails.
        /// </summary>
        public static async Task<LeaderboardPlacementRow> GetSelfPlacementRowAsync(AuthDbContext context, string boardId, uint accountId, int activeDays)
        {
            try
            {
                FormattableString sql;
                switch (boardId)
                {
                    case "lum":
                        sql = LeaderboardInlineSql.SelfPlacementLum(accountId);
                        break;
                    case "bank":
                        sql = LeaderboardInlineSql.SelfPlacementBank(accountId);
                        break;
                    case "level":
                        sql = LeaderboardInlineSql.SelfPlacementLevel(accountId);
                        break;
                    case "enl":
                        sql = LeaderboardInlineSql.SelfPlacementEnlightenment(accountId);
                        break;
                    case "title":
                        sql = LeaderboardInlineSql.SelfPlacementTitles(accountId);
                        break;
                    case "deaths":
                        sql = LeaderboardInlineSql.SelfPlacementDeaths(accountId);
                        break;
                    case "qb":
                        sql = LeaderboardInlineSql.SelfPlacementQuestBonus(accountId);
                        break;
                    case "attr":
                        sql = LeaderboardInlineSql.SelfPlacementAttributes(accountId);
                        break;
                    case "augs":
                        sql = LeaderboardInlineSql.SelfPlacementAugments(accountId);
                        break;
                    case "enlcoins":
                        sql = LeaderboardInlineSql.SelfPlacementBankedInt64((ushort)PropertyInt64.BankedEnlightenedCoins, accountId);
                        break;
                    case "wenlcoins":
                        sql = LeaderboardInlineSql.SelfPlacementBankedInt64((ushort)PropertyInt64.BankedWeaklyEnlightenedCoins, accountId);
                        break;
                    case "mkeys":
                        sql = LeaderboardInlineSql.SelfPlacementBankedInt64((ushort)PropertyInt64.BankedMythicalKeys, accountId);
                        break;
                    case "lkeys":
                        sql = LeaderboardInlineSql.SelfPlacementBankedInt64((ushort)PropertyInt64.BankedLegendaryKeys, accountId);
                        break;
                    case "jails":
                        sql = LeaderboardInlineSql.SelfPlacementCharacterInt((ushort)PropertyInt.TimesJailed, accountId);
                        break;
                    case "notguilty":
                        sql = LeaderboardInlineSql.SelfPlacementCharacterInt((ushort)PropertyInt.TimesUcmCheckPassed, accountId);
                        break;
                    case "bond":
                    case "bonds":
                        sql = LeaderboardInlineSql.SelfPlacementBonds(accountId);
                        break;
                    case "sumbond":
                    case "sumbonds":
                        sql = LeaderboardInlineSql.SelfPlacementSumBonds(accountId);
                        break;
                    case "potency":
                        sql = LeaderboardInlineSql.SelfPlacementPotency(accountId);
                        break;
                    case "mutations":
                        sql = LeaderboardInlineSql.SelfPlacementPetMutations(accountId);
                        break;
                    case "summutations":
                        sql = LeaderboardInlineSql.SelfPlacementSumPetMutations(accountId);
                        break;
                    case "litters":
                        sql = LeaderboardInlineSql.SelfPlacementLittersBred(accountId);
                        break;
                    case "jailbreak":
                        sql = LeaderboardInlineSql.SelfPlacementJailbreak(accountId);
                        break;
                    default:
                        return null;
                }

                return await context.Set<LeaderboardPlacementRow>()
                    .FromSqlInterpolated(LeaderboardInlineSql.ForWindow(sql, activeDays))
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get self placement for board {boardId}: {ex.Message}", ex);
                return null;
            }
        }
    }

    /// <summary>
    /// Thread-safe cache for the SQL leaderboards: one slot per board and activity window, refreshed every 15 minutes
    /// (plus a random 15-120 s so the boards do not all hit the database at once).
    /// </summary>
    public class LeaderboardCache
    {
        private const int cacheTimeout = 15; // minutes

        /// <summary>An empty result (no entries yet, or a failed query) is retried sooner.</summary>
        private const int emptyRetrySeconds = 60;

        private sealed class Slot
        {
            public List<Leaderboard> Cache = new List<Leaderboard>();
            public bool Loaded;
            public DateTime NextUpdate = DateTime.MinValue;
            public readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        }

        private readonly ConcurrentDictionary<string, Slot> slots = new ConcurrentDictionary<string, Slot>(StringComparer.OrdinalIgnoreCase);

        private LeaderboardCache()
        {
        }

        /// <summary>Gets the singleton instance of the LeaderboardCache.</summary>
        public static LeaderboardCache Instance { get; } = new LeaderboardCache();

        private static string Key(string boardId, int activeDays) => $"{boardId}|{Math.Max(0, activeDays)}";

        /// <summary>When the cached list for this board and window is next refreshed (UTC), or now if never loaded.</summary>
        public DateTime GetNextUpdate(string boardId, int activeDays) =>
            slots.TryGetValue(Key(boardId, activeDays), out var slot) && slot.Loaded ? slot.NextUpdate : DateTime.UtcNow;

        /// <summary>
        /// Cached top list for a board in <see cref="LeaderboardInlineSql.TopBoards"/>, limited to accounts active within
        /// <paramref name="activeDays"/> days (0 = everyone). Returns an empty list for an unknown board.
        /// </summary>
        public async Task<List<Leaderboard>> GetTopAsync(AuthDbContext context, string boardId, int activeDays)
        {
            if (!LeaderboardInlineSql.TopBoards.TryGetValue(boardId, out var sql))
                return new List<Leaderboard>();

            activeDays = Math.Max(0, activeDays);
            var slot = slots.GetOrAdd(Key(boardId, activeDays), _ => new Slot());
            if (!slot.Loaded || slot.NextUpdate < DateTime.UtcNow)
            {
                await slot.Gate.WaitAsync();
                try
                {
                    // double-check inside the lock: another request may have refreshed it
                    if (!slot.Loaded || slot.NextUpdate < DateTime.UtcNow)
                    {
                        var result = await Leaderboard.GetTopBoardAsync(context, boardId, sql, activeDays);
                        slot.Cache = result;
                        slot.Loaded = true;
                        slot.NextUpdate = result.Count == 0
                            ? DateTime.UtcNow.AddSeconds(emptyRetrySeconds)
                            : DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120));
                    }
                }
                finally
                {
                    slot.Gate.Release();
                }
            }
            return slot.Cache;
        }
    }
}
