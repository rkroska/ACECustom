using System.Collections.Generic;
using ACE.Database.Models.World;

namespace ACE.Server.Entity
{
    public class TreasureWieldedNode(List<TreasureWielded> items, int idx)
    {
        public TreasureWielded Item = items[idx];

        public TreasureWieldedSet Subset;

        public int TotalNestedItems
        {
            get
            {
                var totalItems = 1;

                if (Subset != null)
                    totalItems += Subset.TotalNestedItems;

                return totalItems;
            }
        }

        public int TotalNestedSets { get => Subset != null ? Subset.TotalNestedSets : 0; }

        public int GetMaxDepth(int depth)
        {
            var subsetDepth = Subset != null ? Subset.GetMaxDepth(depth) : 0;
            return depth + subsetDepth + 1;
        }
    }
}
