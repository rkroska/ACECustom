using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using ACE.Common;
using System.Threading;

namespace ACE.Database.Models.Auth
{
    public partial class Leaderboard
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// Retrieves the top players by quest bonus count from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by quest bonus count, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopQBLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopQuestBonus").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopQuestBonus leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Retrieves the top players by character level from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by character level, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopLevelLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopLevel").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopLevel leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Retrieves the top players by enlightenment count from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by enlightenment count, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopEnlightenmentLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopEnlightenment").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopEnlightenment leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Retrieves the top players by title count from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by title count, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopTitleLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopTitles").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopTitles leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Retrieves the top players by augmentation count from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by augmentation count, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopAugLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopAugments").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopAugments leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Retrieves the top players by death count from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by death count, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopDeathsLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopDeaths").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopDeaths leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Retrieves the top players by bank value from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by bank value, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopBankLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopBank").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopBank leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Retrieves the top players by luminance value from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by luminance value, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopLumLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopLum").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopLum leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }

        /// <summary>
        /// Retrieves the top players by attribute values from the database asynchronously.
        /// </summary>
        /// <param name="context">The database context to use for the query.</param>
        /// <returns>A list of Leaderboard entries sorted by attribute values, or an empty list if the query fails.</returns>
        public static async Task<List<Leaderboard>> GetTopAttrLeaderboardAsync(AuthDbContext context)
        {
            try
            {
                return await context.Leaderboard.FromSql($"CALL TopAttributes").AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get TopAttributes leaderboard: {ex.Message}");
                return new List<Leaderboard>();
            }
        }
    }

    /// <summary>
    /// Provides thread-safe caching for leaderboard data with automatic refresh and variance to prevent database load spikes.
    /// </summary>
    public class LeaderboardCache
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly LeaderboardCache instance = new LeaderboardCache();
        private const int cacheTimeout = 15; // minutes

        // Semaphores for thread-safe async operations
        private static readonly SemaphoreSlim _qbCacheSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _levelCacheSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _enlCacheSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _titleCacheSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _augsCacheSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _deathsCacheSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _bankCacheSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _lumCacheSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _attrCacheSemaphore = new SemaphoreSlim(1, 1);

        static LeaderboardCache()
        {
        }

        private LeaderboardCache()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the LeaderboardCache.
        /// </summary>
        public static LeaderboardCache Instance
        {
            get
            {
                return instance;
            }
        }

        public List<Leaderboard> QBCache = new List<Leaderboard>();
        public List<Leaderboard> LevelCache = new List<Leaderboard>();
        public List<Leaderboard> EnlCache = new List<Leaderboard>();
        public List<Leaderboard> TitleCache = new List<Leaderboard>();
        public List<Leaderboard> AugsCache = new List<Leaderboard>();
        public List<Leaderboard> DeathsCache = new List<Leaderboard>();
        public List<Leaderboard> BankCache = new List<Leaderboard>();
        public List<Leaderboard> LumCache = new List<Leaderboard>();
        public List<Leaderboard> AttrCache = new List<Leaderboard>();

        /// <summary>
        /// Timestamp indicating when the cache was last updated (UTC). This represents the time when the cache was refreshed with new data from the database.
        /// </summary>
        public DateTime QBLastUpdate = DateTime.UtcNow;
        public DateTime LevelLastUpdate = DateTime.UtcNow;
        public DateTime EnlLastUpdate = DateTime.UtcNow;
        public DateTime TitleLastUpdate = DateTime.UtcNow;
        public DateTime AugsLastUpdate = DateTime.UtcNow;
        public DateTime DeathsLastUpdate = DateTime.UtcNow;
        public DateTime BanksLastUpdate = DateTime.UtcNow;
        public DateTime LumLastUpdate = DateTime.UtcNow;
        public DateTime AttrLastUpdate = DateTime.UtcNow;

        /// <summary>
        /// Updates the QB cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateQBCache(List<Leaderboard> list)
        {
            QBCache = list;
            QBLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }

        /// <summary>
        /// Updates the Level cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateLevelCache(List<Leaderboard> list)
        {
            LevelCache = list;
            LevelLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }

        /// <summary>
        /// Updates the Enlightenment cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateEnlCache(List<Leaderboard> list)
        {
            EnlCache = list;
            EnlLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }

        /// <summary>
        /// Updates the Title cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateTitleCache(List<Leaderboard> list)
        {
            TitleCache = list;
            TitleLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }

        /// <summary>
        /// Updates the Augmentations cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateAugsCache(List<Leaderboard> list)
        {
            AugsCache = list;
            AugsLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }

        /// <summary>
        /// Updates the Deaths cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateDeathsCache(List<Leaderboard> list)
        {
            DeathsCache = list;
            DeathsLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }

        /// <summary>
        /// Updates the Bank cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateBankCache(List<Leaderboard> list)
        {
            BankCache = list;
            BanksLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }

        /// <summary>
        /// Updates the Luminance cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateLumCache(List<Leaderboard> list)
        {
            LumCache = list;
            LumLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }

        /// <summary>
        /// Updates the Attributes cache with new data and sets the next update time with variance to prevent database load spikes.
        /// </summary>
        /// <param name="list">The new leaderboard data to cache.</param>
        public void UpdateAttrCache(List<Leaderboard> list)
        {
            AttrCache = list;
            AttrLastUpdate = DateTime.UtcNow.AddMinutes(cacheTimeout).AddSeconds(ThreadSafeRandom.Next(15, 120)); //vary the cache duration to prevent DB slamming
        }   

        /// <summary>
        /// Gets the top players by quest bonus count from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by quest bonus count.</returns>
        public async Task<List<Leaderboard>> GetTopQBAsync(AuthDbContext context)
        {
            if (QBCache.Count == 0 || QBLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"QB Cache miss - Count: {QBCache.Count}, NextUpdate: {QBLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _qbCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (QBCache.Count == 0 || QBLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("QB Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopQBLeaderboardAsync(context);
                        UpdateQBCache(result);
                        log.Debug($"QB Cache refresh completed - {result.Count} records cached, next update: {QBLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("QB Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _qbCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"QB Cache hit - using {QBCache.Count} cached records, last updated: {QBLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return QBCache;
        }

        /// <summary>
        /// Gets the top players by character level from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by character level.</returns>
        public async Task<List<Leaderboard>> GetTopLevelAsync(AuthDbContext context)
        {
            if (LevelCache.Count == 0 || LevelLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"Level Cache miss - Count: {LevelCache.Count}, NextUpdate: {LevelLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _levelCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (LevelCache.Count == 0 || LevelLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("Level Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopLevelLeaderboardAsync(context);
                        UpdateLevelCache(result);
                        log.Debug($"Level Cache refresh completed - {result.Count} records cached, next update: {LevelLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("Level Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _levelCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"Level Cache hit - using {LevelCache.Count} cached records, last updated: {LevelLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return LevelCache;
        }

        /// <summary>
        /// Gets the top players by enlightenment count from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by enlightenment count.</returns>
        public async Task<List<Leaderboard>> GetTopEnlAsync(AuthDbContext context)
        {
            if (EnlCache.Count == 0 || EnlLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"Enlightenment Cache miss - Count: {EnlCache.Count}, NextUpdate: {EnlLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _enlCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (EnlCache.Count == 0 || EnlLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("Enlightenment Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopEnlightenmentLeaderboardAsync(context);
                        UpdateEnlCache(result);
                        log.Debug($"Enlightenment Cache refresh completed - {result.Count} records cached, next update: {EnlLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("Enlightenment Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _enlCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"Enlightenment Cache hit - using {EnlCache.Count} cached records, last updated: {EnlLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return EnlCache;
        }

        /// <summary>
        /// Gets the top players by title count from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by title count.</returns>
        public async Task<List<Leaderboard>> GetTopTitleAsync(AuthDbContext context)
        {
            if (TitleCache.Count == 0 || TitleLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"Title Cache miss - Count: {TitleCache.Count}, NextUpdate: {TitleLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _titleCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (TitleCache.Count == 0 || TitleLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("Title Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopTitleLeaderboardAsync(context);
                        UpdateTitleCache(result);
                        log.Debug($"Title Cache refresh completed - {result.Count} records cached, next update: {TitleLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("Title Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _titleCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"Title Cache hit - using {TitleCache.Count} cached records, last updated: {TitleLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return TitleCache;
        }

        /// <summary>
        /// Gets the top players by augmentation count from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by augmentation count.</returns>
        public async Task<List<Leaderboard>> GetTopAugsAsync(AuthDbContext context)
        {
            if (AugsCache.Count == 0 || AugsLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"Augmentations Cache miss - Count: {AugsCache.Count}, NextUpdate: {AugsLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _augsCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (AugsCache.Count == 0 || AugsLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("Augmentations Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopAugLeaderboardAsync(context);
                        UpdateAugsCache(result);
                        log.Debug($"Augmentations Cache refresh completed - {result.Count} records cached, next update: {AugsLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("Augmentations Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _augsCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"Augmentations Cache hit - using {AugsCache.Count} cached records, last updated: {AugsLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return AugsCache;
        }

        /// <summary>
        /// Gets the top players by death count from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by death count.</returns>
        public async Task<List<Leaderboard>> GetTopDeathsAsync(AuthDbContext context)
        {
            if (DeathsCache.Count == 0 || DeathsLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"Deaths Cache miss - Count: {DeathsCache.Count}, NextUpdate: {DeathsLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _deathsCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (DeathsCache.Count == 0 || DeathsLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("Deaths Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopDeathsLeaderboardAsync(context);
                        UpdateDeathsCache(result);
                        log.Debug($"Deaths Cache refresh completed - {result.Count} records cached, next update: {DeathsLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("Deaths Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _deathsCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"Deaths Cache hit - using {DeathsCache.Count} cached records, last updated: {DeathsLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return DeathsCache;
        }

        /// <summary>
        /// Gets the top players by bank value from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by bank value.</returns>
        public async Task<List<Leaderboard>> GetTopBankAsync(AuthDbContext context)
        {
            if (BankCache.Count == 0 || BanksLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"Bank Cache miss - Count: {BankCache.Count}, NextUpdate: {BanksLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _bankCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (BankCache.Count == 0 || BanksLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("Bank Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopBankLeaderboardAsync(context);
                        UpdateBankCache(result);
                        log.Debug($"Bank Cache refresh completed - {result.Count} records cached, next update: {BanksLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("Bank Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _bankCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"Bank Cache hit - using {BankCache.Count} cached records, last updated: {BanksLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return BankCache;
        }

        /// <summary>
        /// Gets the top players by luminance value from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by luminance value.</returns>
        public async Task<List<Leaderboard>> GetTopLumAsync(AuthDbContext context)
        {
            if (LumCache.Count == 0 || LumLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"Luminance Cache miss - Count: {LumCache.Count}, NextUpdate: {LumLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _lumCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (LumCache.Count == 0 || LumLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("Luminance Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopLumLeaderboardAsync(context);
                        UpdateLumCache(result);
                        log.Debug($"Luminance Cache refresh completed - {result.Count} records cached, next update: {LumLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("Luminance Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _lumCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"Luminance Cache hit - using {LumCache.Count} cached records, last updated: {LumLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return LumCache;
        }

        /// <summary>
        /// Gets the top players by attribute values from cache, refreshing if necessary.
        /// </summary>
        /// <param name="context">The database context to use for refreshing the cache.</param>
        /// <returns>A list of Leaderboard entries sorted by attribute values.</returns>
        public async Task<List<Leaderboard>> GetTopAttrAsync(AuthDbContext context)
        {
            if (AttrCache.Count == 0 || AttrLastUpdate < DateTime.UtcNow)
            {
                log.Debug($"Attributes Cache miss - Count: {AttrCache.Count}, NextUpdate: {AttrLastUpdate:yyyy-MM-dd HH:mm:ss}, Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                await _attrCacheSemaphore.WaitAsync();
                try
                {
                    // Double-check inside the lock
                    if (AttrCache.Count == 0 || AttrLastUpdate < DateTime.UtcNow)
                    {
                        log.Debug("Attributes Cache refresh starting - calling database");
                        var result = await Leaderboard.GetTopAttrLeaderboardAsync(context);
                        UpdateAttrCache(result);
                        log.Debug($"Attributes Cache refresh completed - {result.Count} records cached, next update: {AttrLastUpdate:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        log.Debug("Attributes Cache was refreshed by another thread - using cached data");
                    }
                }
                finally
                {
                    _attrCacheSemaphore.Release();
                }
            }
            else
            {
                log.Debug($"Attributes Cache hit - using {AttrCache.Count} cached records, last updated: {AttrLastUpdate:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return AttrCache;
        }
    }
}
