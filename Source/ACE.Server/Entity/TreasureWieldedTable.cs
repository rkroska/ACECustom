using System;
using System.Collections.Generic;
using ACE.Database.Models.World;

namespace ACE.Server.Entity
{
    public class TreasureWieldedTable
    {
        public List<TreasureWieldedSet> Sets;

        public TreasureWieldedTable(List<TreasureWielded> items)
        {
            Sets = [];

            TreasureWieldedSet currentSet = null;

            for (var idx = 0; idx < items.Count; idx++)
            {
                var item = items[idx];
                if (item.SetStart)
                {
                    currentSet = new TreasureWieldedSet(items, idx);
                    Sets.Add(currentSet);
                    var totalNestedItems = currentSet.TotalNestedItems;
                    idx += totalNestedItems - 1;
                }
                else
                    Console.WriteLine($"Warning: started parsing set with no SetStart on line {idx}");
            }
        }
    }
}
