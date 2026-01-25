using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

using ACE.Common;
using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Physics.Util;

namespace ACE.Server.Physics.Common
{
    public static class LScape
    {
        //private static readonly object landblockMutex = new object();
        /// <summary>
        /// This is not used if PhysicsEngine.Instance.Server is true
        /// </summary>
        private static readonly ConcurrentDictionary<VariantCacheId, Landblock> Landblocks = new();

        /// <summary>
        /// Loads the backing store landblock structure
        /// This function is thread safe
        /// </summary>
        /// <param name="blockCellID">Any landblock + cell ID within the landblock</param>
        public static Landblock get_landblock(uint blockCellID, int? variationId)
        {
            var landblockID = blockCellID | 0xFFFF;
            var lbid = new LandblockId(landblockID);
            if (PhysicsEngine.Instance.Server)
            {
                
                var lbmLandblock = LandblockManager.GetLandblock(lbid, false, variationId, false);
                return lbmLandblock.PhysicsLandblock;
            }
            VariantCacheId cacheKey = new() { Landblock = lbid.Landblock, Variant = variationId ?? 0 };

            // check if landblock is already cached
            if (Landblocks.TryGetValue(cacheKey, out var landblock))
                return landblock;
            
            // if not, load into cache
            landblock = new Landblock(DBObj.GetCellLandblock(landblockID), variationId);
            if (Landblocks.TryAdd(cacheKey, landblock))
                landblock.PostInit();
            else
                Landblocks.TryGetValue(cacheKey, out landblock);

            return landblock;
            
        }

        public static bool unload_landblock(uint landblockID, int? variationId = null)
        {
            VariantCacheId cacheKey = new() { Landblock = (ushort)landblockID, Variant = variationId ?? 0 };
            if (PhysicsEngine.Instance.Server)
            {
                // todo: Instead of ACE.Server.Entity.Landblock.Unload() calling this function, it should be calling PhysicsLandblock.Unload()
                // todo: which would then call AdjustCell.AdjustCells.Remove()                
                AdjustCell.AdjustCells.TryRemove(cacheKey, out _);
                return true;
            }
            
            var result = Landblocks.TryRemove(cacheKey, out _);
            // todo: Like mentioned above, the following function should be moved to ACE.Server.Physics.Common.Landblock.Unload()
            AdjustCell.AdjustCells.TryRemove(cacheKey, out _);
            return result;
        }

        /// <summary>
        /// Gets the landcell from a landblock. If the cell is an indoor cell and hasn't been loaded, it will be loaded.<para />
        /// This function is thread safe
        /// </summary>
        public static ObjCell get_landcell(uint blockCellID, int? variationId)
        {
            var landblock = get_landblock(blockCellID, variationId);
            if (landblock == null)
                return null;

            var cellID = blockCellID & 0xFFFF;
            var cacheKey = new VariantCacheId { Landblock = (ushort)cellID, Variant = variationId ?? 0 };
            ObjCell cell;

            // outdoor cells
            if (cellID < 0x100)
            {
                var lcoord = LandDefs.gid_to_lcoord(blockCellID, false);
                if (lcoord == null) return null;
                var landCellIdx = ((int)lcoord.Value.Y % 8) + ((int)lcoord.Value.X % 8) * landblock.SideCellCount;
                
                if(!landblock.LandCells.TryGetValue(new VariantCacheId { Landblock = (ushort)landCellIdx, Variant = variationId ?? 0 }, out cell))
                {
                    Console.WriteLine($"get_landcell({blockCellID:X8} - {landCellIdx:X8} - {variationId:X8}) failed to get from dictionary, cache miss");
                }
            }
            // indoor cells
            else
            {
                if (landblock.LandCells.TryGetValue(cacheKey, out cell))
                    return cell;

                cell = DBObj.GetEnvCell(blockCellID, variationId);
                cell.CurLandblock = landblock;
                cell.Pos.Variation = variationId; //todo - gross
                cell.VariationId = variationId;
                landblock.LandCells.TryAdd(cacheKey, cell);
                var envCell = (EnvCell)cell;
                envCell.PostInit(variationId);
                
            }
            return cell;
        }
    }
}
