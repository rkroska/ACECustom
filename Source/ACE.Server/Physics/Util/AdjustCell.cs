using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Numerics;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Common;

namespace ACE.Server.Physics.Util
{
    public class AdjustCell
    {
        public readonly List<Common.EnvCell> EnvCells = [];
        public static readonly ConcurrentDictionary<VariantCacheId, AdjustCell> AdjustCells = new();

        public AdjustCell(uint dungeonID, int? variationId)
        {
            uint blockInfoID = dungeonID << 16 | 0xFFFE;
            var blockinfo = DatManager.CellDat.ReadFromDat<LandblockInfo>(blockInfoID);
            var numCells = blockinfo.NumCells;
            uint firstCellID = 0x100;
            for (uint i = 0; i < numCells; i++)
            {
                uint cellID = firstCellID + i;
                uint blockCell = dungeonID << 16 | cellID;

                var objCell = Common.LScape.get_landcell(blockCell, variationId);
                if (objCell is Common.EnvCell envCell)
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
            VariantCacheId cacheKey = new() { Landblock = (ushort)dungeonID, Variant = variationId ?? 0};
            AdjustCells.TryGetValue(cacheKey, out AdjustCell adjustCell);
            if (adjustCell == null)
            {
                adjustCell = new AdjustCell(dungeonID, variationId);
                AdjustCells.TryAdd(cacheKey, adjustCell);
            }
            return adjustCell;
        }
    }
}
