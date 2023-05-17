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
    }
}
