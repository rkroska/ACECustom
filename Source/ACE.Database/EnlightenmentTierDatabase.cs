using System.Collections.Generic;
using System.Linq;

using ACE.Database.Models.Shard;

using Microsoft.EntityFrameworkCore;

namespace ACE.Database
{
    public class EnlightenmentTierDatabase
    {
        public List<ConfigEnlightenmentTier> GetAllTiers()
        {
            using var context = new ShardDbContext();
            return context.ConfigEnlightenmentTier
                .AsNoTracking()
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.MinTargetEnl)
                .ToList();
        }
    }
}
