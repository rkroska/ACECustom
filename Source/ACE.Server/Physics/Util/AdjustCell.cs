using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Numerics;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Common;
using ACE.Entity;
using System;

namespace ACE.Server.Physics.Util
{
    public class AdjustCell
    {
        public List<Common.EnvCell> EnvCells;
        public static ConcurrentDictionary<VariantCacheId, AdjustCell> AdjustCells = new ConcurrentDictionary<VariantCacheId, AdjustCell>();

        public AdjustCell(uint dungeonID, int? variationId)
        {
            uint blockInfoID = dungeonID << 16 | 0xFFFE;
            var blockinfo = DatManager.CellDat.ReadFromDat<LandblockInfo>(blockInfoID);
            var numCells = blockinfo.NumCells;

            BuildEnv(dungeonID, numCells, variationId);
        }

        public void BuildEnv(uint dungeonID, uint numCells, int? variationId)
        {
            EnvCells = new List<Common.EnvCell>();
            uint firstCellID = 0x100;
            for (uint i = 0; i < numCells; i++)
            {
                uint cellID = firstCellID + i;
                uint blockCell = dungeonID << 16 | cellID;

                var objCell = Common.LScape.get_landcell(blockCell, variationId);
                var envCell = objCell as Common.EnvCell;
                if (envCell != null)
                    EnvCells.Add(envCell);
            }
        }

        public uint? GetCell(Vector3 point)
        {
            foreach (var envCell in EnvCells)
                if (envCell.point_in_cell(point))
                    return envCell.ID;
            return null;
        }

        public static AdjustCell Get(uint dungeonID, int? variationId)
        {            
            VariantCacheId cacheKey = new VariantCacheId { Landblock = (ushort)dungeonID, Variant = variationId ?? 0};
            AdjustCell adjustCell = null;
            AdjustCells.TryGetValue(cacheKey, out adjustCell);
            if (adjustCell == null)
            {
                adjustCell = new AdjustCell(dungeonID, variationId);
                //Console.WriteLine($"AdjustCell created: {dungeonID} v: {variationId}");
                AdjustCells.TryAdd(cacheKey, adjustCell);
            }
            return adjustCell;
        }
    }
}
