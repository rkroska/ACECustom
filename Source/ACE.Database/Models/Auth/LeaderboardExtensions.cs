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

        public static List<Leaderboard> GetTopLevelLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopLevel").ToList();
        }

        public static List<Leaderboard> GetTopEnlightenmentLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopEnlightenment").ToList();
        }

        public static List<Leaderboard> GetTopTitleLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopTitles").ToList();
        }

        public static List<Leaderboard> GetTopAugLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopAugments").ToList();
        }

        public static List<Leaderboard> GetTopDeathsLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopDeaths").ToList();
        }

        public static List<Leaderboard> GetTopBankLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopBank").ToList();
        }

        public static List<Leaderboard> GetTopLumLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopLum").ToList();
        }

        public static List<Leaderboard> GetTopAttrLeaderboard(AuthDbContext context)
        {
            return context.Leaderboard.FromSql($"CALL TopAttributes").ToList();
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

        public List<Leaderboard> GetTopLevel(AuthDbContext context)
        {
            if (LevelCache.Count == 0 || LevelLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateLevelCache(Leaderboard.GetTopLevelLeaderboard(context));
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

        public List<Leaderboard> GetTopTitle(AuthDbContext context)
        {
            if (TitleCache.Count == 0 || TitleLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateTitleCache(Leaderboard.GetTopTitleLeaderboard(context));
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

        public List<Leaderboard> GetTopDeaths(AuthDbContext context)
        {
            if (DeathsCache.Count == 0 || DeathsLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateDeathsCache(Leaderboard.GetTopDeathsLeaderboard(context));
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

        public List<Leaderboard> GetTopLum(AuthDbContext context)
        {
            if (LumCache.Count == 0 || LumLastUpdate.AddMinutes(cacheTimeout) < DateTime.Now)
            {
                UpdateLumCache(Leaderboard.GetTopLumLeaderboard(context));
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
    }
}
