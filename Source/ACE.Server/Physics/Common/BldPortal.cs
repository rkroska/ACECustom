using System;
using System.Collections.Generic;
using ACE.Entity.Enum;

namespace ACE.Server.Physics.Common
{
    public class BldPortal
    {
        public PortalFlags Flags;
        public bool ExactMatch;
        public bool PortalSide;
        public ushort OtherCellId;
        public ushort OtherPortalId;
        public List<ushort> StabList;

        public BldPortal() { }

        public BldPortal(DatLoader.Entity.CBldPortal bldPortal)
        {
            Flags = bldPortal.Flags;
            ExactMatch = bldPortal.ExactMatch;
            PortalSide = bldPortal.PortalSide;
            OtherCellId = bldPortal.OtherCellId;
            OtherPortalId = bldPortal.OtherPortalId;
            StabList = bldPortal.StabList;
        }

        public EnvCell GetOtherCell(uint landblockID, int? variationId)
        {
            var blockCellID = landblockID & 0xFFFF0000 | OtherCellId;

            //if (variationId == 12) Console.WriteLine($"[DEBUG-PORT] GetOtherCell: LB={landblockID:X8} Var={variationId} -> Target={blockCellID:X8}");
            var cell = (EnvCell)LScape.get_landcell(blockCellID, variationId);
            
            if (cell == null && (variationId == 12 || variationId == 15))
                 Console.WriteLine($"[DEBUG-PORT] GetOtherCell FAILED: LB={landblockID:X8} Var={variationId} -> Target={blockCellID:X8}");
            
            return cell;
        }

        public void add_to_stablist(ref List<ushort> stabList, ref uint maxSize, ref uint stabNum)
        {
            // is maxSize needed with list?
            for (var i = 0; i < StabList.Count; i++)
            {
                var j = (int)stabNum;
                while (j > 0)
                {
                    if (StabList[i] == stabList[j - 1])
                        break;
                    j--;
                }
                if (j > 0)
                {
                    if (stabNum >= maxSize)
                    {
                        var old = stabList;
                        stabList = new List<ushort>();
                        maxSize += 10;
                        foreach (var stab in StabList)
                            stabList.Add(stab);
                    }
                    stabList.Add(StabList[i]);
                    stabNum++;
                }
            }
        }
    }
}
