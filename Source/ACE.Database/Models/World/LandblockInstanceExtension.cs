using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Database.Models.World
{
    public partial class LandblockInstance
    {
        /// <summary>
        /// Used in advanced find operations where a static weenie, like an npc, can be found in a specific landblock. Plural where npc is generic - towncrier, etc.
        /// </summary>
        /// <param name="weenie_id"></param>
        /// <returns></returns>
        public static List<LandblockInstance> GetLandblockByStaticWeenieId(ulong weenie_id)
        {
            List<LandblockInstance> landblocks = new List<LandblockInstance>();
            using (var context = new WorldDbContext())
            {
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                var results = context.LandblockInstance.Where(x => x.WeenieClassId == weenie_id).ToList();
                if (results != null && results.Count >= 1)
                {
                    foreach (var item in results)
                    {
                        landblocks.Add(item);
                    }
                }
            }

            return landblocks;
        }
    }
}
