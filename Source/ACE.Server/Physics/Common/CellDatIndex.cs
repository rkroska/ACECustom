using System.Collections.Generic;

using ACE.DatLoader;

namespace ACE.Server.Physics.Common
{
    /// <summary>
    /// Asks the cell dat whether a landblock exists, without loading it.
    ///
    /// A landblock that is not in the cell dat cannot be loaded: ReadFromDat hands back an empty CellLandblock and
    /// LandblockStruct.ConstructVertices throws on it. Positions sent by a client are not otherwise checked, so they
    /// must pass this before anything loads the landblock they name.
    /// </summary>
    public static class CellDatIndex
    {
        /// <summary>True when the landblock holding this cell id is in the cell dat.</summary>
        public static bool LandblockExists(uint cellId)
        {
            var cellDat = DatManager.CellDat;

            // no dat loaded (tools, tests): nothing to check against, so don't refuse anything
            if (cellDat == null)
                return true;

            return LandblockExists(cellDat.AllFiles, cellId);
        }

        /// <summary>
        /// The index is the file table read once when the dat opens and never written afterwards,
        /// so reading it from parallel physics threads is safe.
        /// </summary>
        public static bool LandblockExists(IReadOnlyDictionary<uint, DatFile> index, uint cellId)
        {
            return index.ContainsKey(cellId | 0xFFFF);
        }
    }
}
