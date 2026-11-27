using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Linq;

using log4net;

using ACE.Common;
using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Physics.Util;

namespace ACE.Server.Physics.Common
{
    public static class LScape
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
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
        // Deep dungeon fix (2026-11-17): a new EnvCell's PostInit = build_visible_cells (loads every cell it can see, via
        // get_landcell) + init_static_objects. Each newly seen cell ran its OWN PostInit inside that call - recursion as deep
        // as the dungeon's visibility chain. 00E5 (Temple of the Three) went ~1080 levels deep and overflowed the 1 MB World
        // Manager stack on a login into the unloaded dungeon (0xc00000fd, server gone).
        //
        // Now a new cell starts a GROUP, run in two flat stages:
        //   A. build visible cells breadth-first - a cell first seen while some cell of the group builds its list joins the
        //      queue instead of recursing;
        //   B. init_static_objects for every cell of the group (last found first - no ordering is relied on).
        // Stage B runs only after EVERY cell of the group has its VisibleCells, which static placement relies on
        // (GetVisible during find_transit_cells). A cell first loaded during stage B is a new, normal group of its own.
        // Same cells, same caching; every cell fully set up before the outermost get_landcell returns. Per thread.
        // One cell failing is logged and skipped, so it can never leave the REST of a big group half-built. A failed cell is
        // never served as complete:
        //   - it leaves the cache before anything outside the group can find it (a stage A failure when stage A ends - no
        //     other group runs during stage A - a stage B failure at once). A later get_landcell loads it fresh and runs the
        //     whole setup again; that includes the groups stage B itself starts.
        //   - NO cached cell of its landblock keeps the broken copy as a neighbour (not only this group: a group stage B
        //     started may have picked it up before it failed). A fresh copy loaded meanwhile takes its place. A stage A
        //     failure is dropped before stage B, so static placement never goes through a cell with no visible cells.
        //   - its partial static objects are destroyed.
        // When it later loads fine, EVERY cached cell that lists it takes the new copy back (LinkToCachedNeighbours).
        [ThreadStatic] private static Queue<(EnvCell Cell, int? Variation)> envCellGroupQueue;

        /// <returns>false when THIS cell failed its setup (it has been evicted - the caller must not use it).</returns>
        private static bool PostInitEnvCell(EnvCell envCell, int? variation)
        {
            if (envCellGroupQueue != null)
            {
                envCellGroupQueue.Enqueue((envCell, variation));   // seen during stage A: built by the running group
                return true;
            }

            var group = new List<(EnvCell Cell, int? Variation)>();
            var queue = new Queue<(EnvCell Cell, int? Variation)>();
            HashSet<EnvCell> failed = null;
            queue.Enqueue((envCell, variation));

            envCellGroupQueue = queue;
            try
            {
                while (queue.Count > 0)
                {
                    var item = queue.Dequeue();
                    try { item.Cell.build_visible_cells(); }
                    catch (Exception ex)
                    {
                        log.Error($"[PHYSICS] build_visible_cells failed for cell 0x{item.Cell.ID:X8} v{item.Variation?.ToString() ?? "base"} - evicted, it loads fresh next time: {ex}");
                        (failed ??= new HashSet<EnvCell>()).Add(item.Cell);
                    }
                    group.Add(item);
                }
            }
            finally
            {
                envCellGroupQueue = null;
            }

            // out of the cache and out of every neighbour list before stage B: static placement must not go through a cell
            // with no visible cells, and a group stage B starts must load a fresh copy, never find this one
            if (failed != null)
            {
                foreach (var cell in failed)
                    RemoveFromCellCache(cell);
                DropFailedFromNeighbours(failed);
            }

            for (var i = group.Count - 1; i >= 0; i--)
            {
                if (failed != null && failed.Contains(group[i].Cell))
                    continue;   // no visible cells: placing its static objects would only fail again
                try { group[i].Cell.init_static_objects(group[i].Variation); }
                catch (Exception ex)
                {
                    log.Error($"[PHYSICS] init_static_objects failed for cell 0x{group[i].Cell.ID:X8} v{group[i].Variation?.ToString() ?? "base"} - evicted, it loads fresh next time: {ex}");
                    (failed ??= new HashSet<EnvCell>()).Add(group[i].Cell);
                    RemoveFromCellCache(group[i].Cell);   // at once: the rest of stage B may start groups that would find it
                }
            }

            if (failed != null)
            {
                DestroyPartialStaticObjects(failed);
                DropFailedFromNeighbours(failed);   // again: a stage B failure, and anything stage B's groups picked up
            }
            LinkToCachedNeighbours(group, failed);
            return failed == null || !failed.Contains(envCell);
        }

        /// <summary>
        /// Cells evicted after a failed setup, per physics landblock (weak: they go with the landblock). Only these can be
        /// missing from a cached neighbour's VisibleCells - any other cell was loaded by the neighbour's own build.
        /// </summary>
        private static readonly ConditionalWeakTable<Landblock, ConcurrentDictionary<VariantCacheId, byte>> evictedEnvCells = new();

        /// <summary>
        /// A cell that loads fine after an earlier copy of it failed and was evicted goes back into the VisibleCells of every
        /// cached cell that lists it - not only the cells it sees itself (visibility lists need not be mutual). Cells of the
        /// group built their lists with it cached, so they have it already. Nothing to do (one lookup) when nothing failed.
        /// </summary>
        private static void LinkToCachedNeighbours(List<(EnvCell Cell, int? Variation)> group, HashSet<EnvCell> failed)
        {
            foreach (var (cell, _) in group)
            {
                if (failed != null && failed.Contains(cell))
                    continue;   // stays out until a load of it succeeds
                var landblock = cell.CurLandblock;
                if (landblock?.LandCells == null || !evictedEnvCells.TryGetValue(landblock, out var evicted) || evicted.IsEmpty
                    || !evicted.TryRemove(CellCacheKey(cell.ID, cell.VariationId), out _))
                    continue;

                var id = (ushort)(cell.ID & 0xFFFF);
                foreach (var cached in landblock.LandCells.Values)
                    if (cached is EnvCell other && !ReferenceEquals(other, cell) && other.VariationId == cell.VariationId
                        && other.VisibleCellIDs.Contains(id))
                        other.VisibleCells.TryAdd(id, cell);
            }
        }

        /// <summary>The cache key get_landcell stored this cell under.</summary>
        private static VariantCacheId CellCacheKey(uint cellId, int? variation)
            => new VariantCacheId { Landblock = (ushort)(cellId & 0xFFFF), Variant = variation };

        /// <summary>Takes this very copy out of its landblock's cell cache (a newer copy under the same key stays) and notes
        /// the cell as evicted, so a later good load of it relinks every cached cell that lists it.</summary>
        private static void RemoveFromCellCache(EnvCell cell)
        {
            var landblock = cell.CurLandblock;
            if (landblock?.LandCells == null)
                return;
            var key = CellCacheKey(cell.ID, cell.VariationId);
            if (landblock.LandCells.TryRemove(new KeyValuePair<VariantCacheId, ObjCell>(key, cell)))
                evictedEnvCells.GetOrCreateValue(landblock)[key] = 0;
        }

        /// <summary>A failed cell's partly placed static objects are destroyed, as init_static_objects does for one it cannot place.</summary>
        private static void DestroyPartialStaticObjects(HashSet<EnvCell> failed)
        {
            foreach (var cell in failed)
            {
                var partial = cell.StaticObjects;
                cell.StaticObjects = null;
                if (partial != null)
                    foreach (var staticObj in partial)
                    {
                        try { staticObj.DestroyObject(); }
                        catch (Exception ex) { log.Error($"[PHYSICS] could not remove a static object of failed cell 0x{cell.ID:X8}: {ex.Message}"); }
                    }
            }
        }

        /// <summary>
        /// A cell whose setup failed must not be served as complete. Already out of the cache (RemoveFromCellCache); here every
        /// cached cell of its landblock drops it from VisibleCells - this group's cells and any a group started during stage B
        /// may have built with it. Where a fresh copy of that cell has been loaded since, it takes the broken one's place;
        /// otherwise GetVisible reports the cell as not loaded until a load of it succeeds (LinkToCachedNeighbours).
        /// Only runs after a failure, so the whole-landblock pass costs nothing on a normal load.
        /// </summary>
        private static void DropFailedFromNeighbours(HashSet<EnvCell> failed)
        {
            var landblocks = new HashSet<Landblock>();
            foreach (var cell in failed)
                if (cell.CurLandblock?.LandCells != null)
                    landblocks.Add(cell.CurLandblock);

            foreach (var landblock in landblocks)
                foreach (var cached in landblock.LandCells.Values)
                {
                    if (cached is not EnvCell envCell || failed.Contains(envCell))
                        continue;
                    foreach (var entry in envCell.VisibleCells.ToArray())
                    {
                        if (!failed.Contains(entry.Value))
                            continue;
                        envCell.VisibleCells.TryRemove(entry);
                        if (landblock.LandCells.TryGetValue(CellCacheKey(entry.Key, envCell.VariationId), out var fresh)
                            && fresh is EnvCell freshCell && !failed.Contains(freshCell))
                            envCell.VisibleCells.TryAdd(entry.Key, freshCell);
                    }
                }
        }

        public static Landblock get_landblock(uint blockCellID, int? variationId)
        {
            var landblockID = blockCellID | 0xFFFF;
            var lbid = new LandblockId(landblockID);
            if (PhysicsEngine.Instance.Server)
            {
                
                var lbmLandblock = LandblockManager.GetLandblock(lbid, false, variationId, false);
                return lbmLandblock?.PhysicsLandblock;
            }
            // Non-server (client/tool) mode: same 0-is-base key rule as the server path, so unload_landblock's key matches.
            VariantCacheId cacheKey = new() { Landblock = lbid.Landblock, Variant = VariationManager.NormalizeBase(variationId) };

            // check if landblock is already cached
            if (Landblocks.TryGetValue(cacheKey, out var landblock))
                return landblock;
            
            // if not, load into cache
            landblock = new Landblock(DBObj.GetCellLandblock(landblockID), cacheKey.Variant);
            if (Landblocks.TryAdd(cacheKey, landblock))
                landblock.PostInit();
            else
                Landblocks.TryGetValue(cacheKey, out landblock);

            return landblock;
            
        }

        public static bool unload_landblock(uint landblockID, int? variationId = null)
        {
            // Variant review 2026-09-12 (item 12, eviction half): the callers pass the RAW id (block | 0xFFFF), so
            // (ushort)landblockID was always 0xFFFF and no AdjustCell was ever evicted: every (dungeon, variation) ever
            // loaded stayed cached, each holding EnvCells that pointed at the released physics landblock. Build the key
            // the way the inserts do - since 2026-09-17 that is the normalized variation (0 is always base), the same key
            // for the physics landblock cache and for AdjustCell.
            var lbid = new LandblockId(landblockID);
            VariantCacheId cacheKey = new() { Landblock = lbid.Landblock, Variant = VariationManager.NormalizeBase(variationId) };
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
            // Variation 0 is always base (2026-09-14 ruling). The landblock lookup normalizes on its own, but the cell
            // cache keys below use THIS value: a raw 0 against a base landblock (VariationId null) keyed a second EnvCell
            // under (cell, 0) in the same landblock and loaded it from the DB as layer 0.
            variationId = VariationManager.NormalizeBase(variationId);

            var landblock = get_landblock(blockCellID, variationId);
            if (landblock == null)
                return null;

            var cellID = blockCellID & 0xFFFF;
            if (cellID == 0xFFFF) return null;
            // Align cache keys with EnvCell load: same envVariation for DB + dictionary (caller wins when landblock instance has no VariationId).
            var envVariation = landblock.VariationId ?? variationId;
            var variantForCache = envVariation;
            var cacheKey = new VariantCacheId { Landblock = (ushort)cellID, Variant = variantForCache };
            ObjCell cell;

            // outdoor cells
            if (cellID < 0x100)
            {
                var lcoord = LandDefs.gid_to_lcoord(blockCellID, false);
                if (lcoord == null) return null;
                // Match Landblock.get_landcell: mask to block-local coords (LandblockMask == 7); % 8 diverges for negatives.
                var landCellIdx = ((int)lcoord.Value.Y & LandDefs.LandblockMask)
                    + ((int)lcoord.Value.X & LandDefs.LandblockMask) * landblock.SideCellCount;

                var outdoorKey = new VariantCacheId { Landblock = (ushort)landCellIdx, Variant = variantForCache };
                if (!landblock.LandCells.TryGetValue(outdoorKey, out cell))
                {
                    if (!Nullable.Equals(variationId, variantForCache)
                        && landblock.LandCells.TryGetValue(new VariantCacheId { Landblock = (ushort)landCellIdx, Variant = variationId }, out cell))
                    {
                        PhysicsLogGates.GetLandcellVariantRecover.Warn(
                            () =>
                                $"[PHYSICS] get_landcell variant mismatch for 0x{blockCellID:X8}: caller v={variationId?.ToString() ?? "null"} landblock v={landblock.VariationId?.ToString() ?? "null"}; recovered with caller variant — fix variation propagation.",
                            15_000);
                    }
                    else
                    {
                        PhysicsLogGates.GetLandcellOutdoorMiss.Warn(
                            () =>
                            {
                                var keySample = string.Join(", ", landblock.LandCells.Keys.Where(k => k.Variant == variantForCache).Select(k => $"{k.Landblock}:{k.Variant}").Take(10));
                                return $"[PHYSICS] get_landcell outdoor cache miss blockCell=0x{blockCellID:X8} landCellIdx={landCellIdx:X8} variantForCache={variantForCache?.ToString() ?? "null"} landblock=0x{landblock.ID:X8} sampleKeys=[{keySample}]";
                            },
                            5_000);
                    }
                }
            }
            // indoor cells
            else
            {
                var cacheHit = landblock.LandCells.TryGetValue(cacheKey, out cell);
                if (IndoorPlacementDiagLogging.Enabled && IndoorPlacementDiagLogging.IsColo(blockCellID) && log.IsDebugEnabled)
                    log.Debug($"[IndoorPlaceDiag] get_landcell indoor blockCell={blockCellID:X8} callerV={variationId?.ToString() ?? "null"} landblockV={landblock.VariationId?.ToString() ?? "null"} envV={envVariation?.ToString() ?? "null"} cacheKeyStub={cacheKey.Landblock:X4} cacheKeyVar={cacheKey.Variant?.ToString() ?? "null"} cacheHit={cacheHit}");
                if (cacheHit)
                    return cell;

                cell = DBObj.GetEnvCell(blockCellID, envVariation);
                if (cell == null)
                {
                    if (IndoorPlacementDiagLogging.Enabled && IndoorPlacementDiagLogging.IsColo(blockCellID))
                        log.Info($"[IndoorPlaceDiag] get_landcell DBObj.GetEnvCell returned null blockCell={blockCellID:X8} envV={envVariation?.ToString() ?? "null"}");
                    return null;
                }

                landblock.LandCells.TryAdd(cacheKey, cell);
                if (!landblock.LandCells.TryGetValue(cacheKey, out var stored) || stored == null)
                    return cell;

                if (!ReferenceEquals(stored, cell))
                    return stored;

                stored.CurLandblock = landblock;
                stored.Pos.Variation = envVariation; //todo - gross
                stored.VariationId = envVariation;
                var envCell = (EnvCell)stored;
                if (!PostInitEnvCell(envCell, envVariation))
                    return null;   // its setup failed: evicted, never served as complete (logged with cell + variation)
                if (IndoorPlacementDiagLogging.Enabled && IndoorPlacementDiagLogging.IsColo(blockCellID))
                    log.Info($"[IndoorPlaceDiag] get_landcell loaded new env cell id={stored.ID:X8} origin={envCell.Pos.Frame.Origin} envV={envVariation?.ToString() ?? "null"} cacheKeyVar={cacheKey.Variant?.ToString() ?? "null"}");
                cell = stored;
            }
            return cell;
        }
    }
}
