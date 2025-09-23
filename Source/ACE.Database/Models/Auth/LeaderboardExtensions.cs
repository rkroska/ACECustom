using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Database.Models.Auth
{
    public partial class Leaderboard
    {               
        public static List<Leaderboard> GetTopQBLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopQuestBonus").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopQBLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopQuestBonus").ToListAsync();
        }

        public static List<Leaderboard> GetTopLevelLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopLevel").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopLevelLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopLevel").ToListAsync();
        }

        public static List<Leaderboard> GetTopEnlightenmentLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopEnlightenment").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopEnlightenmentLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopEnlightenment").ToListAsync();
        }

        public static List<Leaderboard> GetTopTitleLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopTitles").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopTitleLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopTitles").ToListAsync();
        }

        public static List<Leaderboard> GetTopAugLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopAugments").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopAugLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopAugments").ToListAsync();
        }

        public static List<Leaderboard> GetTopDeathsLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopDeaths").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopDeathsLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopDeaths").ToListAsync();
        }

        public static List<Leaderboard> GetTopBankLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopBank").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopBankLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopBank").ToListAsync();
        }

        public static List<Leaderboard> GetTopLumLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopLum").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopLumLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopLum").ToListAsync();
        }

        public static List<Leaderboard> GetTopAttrLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopAttributes").ToList();
        }

        public static async Task<List<Leaderboard>> GetTopAttrLeaderboardAsync(AuthDbContext context)
        {
            return await context.Leaderboard.FromSql($"CALL TopAttributes").ToListAsync();
        }
    }

    public class LeaderboardCache
    {

        private static readonly LeaderboardCache instance = new LeaderboardCache();
        private const int cacheTimeout = 15; // minutes

        static LeaderboardCache()
        {
        }

        private LeaderboardCache()
        {
        }

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

        public DateTime QBLastUpdate = DateTime.Now;
        public DateTime LevelLastUpdate = DateTime.Now;
        public DateTime EnlLastUpdate = DateTime.Now;
        public DateTime TitleLastUpdate = DateTime.Now;
        public DateTime AugsLastUpdate = DateTime.Now;
        public DateTime DeathsLastUpdate = DateTime.Now;
        public DateTime BanksLastUpdate = DateTime.Now;
        public DateTime LumLastUpdate = DateTime.Now;
        public DateTime AttrLastUpdate = DateTime.Now;

        public void UpdateQBCache(List<Leaderboard> list)
        {
            QBCache = list;
            QBLastUpdate = DateTime.Now;
        }

        public void UpdateLevelCache(List<Leaderboard> list)
        {
            LevelCache = list;
            LevelLastUpdate = DateTime.Now;
        }

        public void UpdateEnlCache(List<Leaderboard> list)
        {
            EnlCache = list;
            EnlLastUpdate = DateTime.Now;
        }

        public void UpdateTitleCache(List<Leaderboard> list)
        {
            TitleCache = list;
            TitleLastUpdate = DateTime.Now;
        }

        public void UpdateAugsCache(List<Leaderboard> list)
        {
            AugsCache = list;
            AugsLastUpdate = DateTime.Now;
        }

        public void UpdateDeathsCache(List<Leaderboard> list)
        {
            DeathsCache = list;
            DeathsLastUpdate = DateTime.Now;
        }

        public void UpdateBankCache(List<Leaderboard> list)
        {
            BankCache = list;
            BanksLastUpdate = DateTime.Now;
        }

        public void UpdateLumCache(List<Leaderboard> list)
        {
            LumCache = list;
            LumLastUpdate = DateTime.Now;
        }

        public void UpdateAttrCache(List<Leaderboard> list)
        {
            AttrCache = list;
            AttrLastUpdate = DateTime.Now;
        }   

        public List<Leaderboard> GetTopQB(AuthDbContext context)
        {
            if (QBCache.Count == 0 || QBLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateQBCache(Leaderboard.GetTopQBLeaderboard(context));
            }
            return QBCache;
        }

        public async Task<List<Leaderboard>> GetTopQBAsync(AuthDbContext context)
        {
            if (QBCache.Count == 0 || QBLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopQBLeaderboardAsync(context);
                UpdateQBCache(result);
            }
            return QBCache;
        }

        public List<Leaderboard> GetTopLevel(AuthDbContext context)
        {
            if (LevelCache.Count == 0 || LevelLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateLevelCache(Leaderboard.GetTopLevelLeaderboard(context));
            }
            return LevelCache;
        }

        public async Task<List<Leaderboard>> GetTopLevelAsync(AuthDbContext context)
        {
            if (LevelCache.Count == 0 || LevelLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopLevelLeaderboardAsync(context);
                UpdateLevelCache(result);
            }
            return LevelCache;
        }

        public List<Leaderboard> GetTopEnl(AuthDbContext context)
        {
            if (EnlCache.Count == 0 || EnlLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateEnlCache(Leaderboard.GetTopEnlightenmentLeaderboard(context));
            }
            return EnlCache;
        }

        public async Task<List<Leaderboard>> GetTopEnlAsync(AuthDbContext context)
        {
            if (EnlCache.Count == 0 || EnlLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopEnlightenmentLeaderboardAsync(context);
                UpdateEnlCache(result);
            }
            return EnlCache;
        }

        public List<Leaderboard> GetTopTitle(AuthDbContext context)
        {
            if (TitleCache.Count == 0 || TitleLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateTitleCache(Leaderboard.GetTopTitleLeaderboard(context));
            }
            return TitleCache;
        }

        public async Task<List<Leaderboard>> GetTopTitleAsync(AuthDbContext context)
        {
            if (TitleCache.Count == 0 || TitleLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopTitleLeaderboardAsync(context);
                UpdateTitleCache(result);
            }
            return TitleCache;
        }

        public List<Leaderboard> GetTopAugs(AuthDbContext context)
        {
            if (AugsCache.Count == 0 || AugsLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateAugsCache(Leaderboard.GetTopAugLeaderboard(context));
            }
            return AugsCache;
        }

        public async Task<List<Leaderboard>> GetTopAugsAsync(AuthDbContext context)
        {
            if (AugsCache.Count == 0 || AugsLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopAugLeaderboardAsync(context);
                UpdateAugsCache(result);
            }
            return AugsCache;
        }

        public List<Leaderboard> GetTopDeaths(AuthDbContext context)
        {
            if (DeathsCache.Count == 0 || DeathsLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateDeathsCache(Leaderboard.GetTopDeathsLeaderboard(context));
            }
            return DeathsCache;
        }

        public async Task<List<Leaderboard>> GetTopDeathsAsync(AuthDbContext context)
        {
            if (DeathsCache.Count == 0 || DeathsLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopDeathsLeaderboardAsync(context);
                UpdateDeathsCache(result);
            }
            return DeathsCache;
        }

        public List<Leaderboard> GetTopBank(AuthDbContext context)
        {
            if (BankCache.Count == 0 || BanksLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateBankCache(Leaderboard.GetTopBankLeaderboard(context));
            }
            return BankCache;
        }

        public async Task<List<Leaderboard>> GetTopBankAsync(AuthDbContext context)
        {
            if (BankCache.Count == 0 || BanksLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopBankLeaderboardAsync(context);
                UpdateBankCache(result);
            }
            return BankCache;
        }

        public List<Leaderboard> GetTopLum(AuthDbContext context)
        {
            if (LumCache.Count == 0 || LumLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateLumCache(Leaderboard.GetTopLumLeaderboard(context));
            }
            return LumCache;
        }

        public async Task<List<Leaderboard>> GetTopLumAsync(AuthDbContext context)
        {
            if (LumCache.Count == 0 || LumLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopLumLeaderboardAsync(context);
                UpdateLumCache(result);
            }
            return LumCache;
        }

        public List<Leaderboard> GetTopAttr(AuthDbContext context)
        {
            if (AttrCache.Count == 0 || AttrLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateAttrCache(Leaderboard.GetTopAttrLeaderboard(context));   
            }
            return AttrCache;
        }

        public async Task<List<Leaderboard>> GetTopAttrAsync(AuthDbContext context)
        {
            if (AttrCache.Count == 0 || AttrLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                var result = await Leaderboard.GetTopAttrLeaderboardAsync(context);
                UpdateAttrCache(result);
            }
            return AttrCache;
        }
    }
}
