using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;

using Microsoft.EntityFrameworkCore;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /zonecontrol clonelayer &lt;zone&gt; &lt;variation|lo-hi&gt; [update] [prune] [restore] [dry]   (owner 2026-10-03)
    ///
    /// Copies the zone's OWN layer - every landblock_instance placement on its landblocks at the zone's variation (Tou Tou =
    /// v11: NPCs, generators, decor, markers) - onto other variation layers (12-25), so a T11 content change reaches every
    /// tier with one command, on test and on live, against that server's OWN current data.
    ///
    /// SAFETY RULES
    /// - It only ever writes rows IT created: each copy is recorded in the world-DB table zc_layer_clone (dst guid, src guid,
    ///   variations, zone). Anything placed directly on a target tier (content unique to T16, say) is never touched.
    /// - sync (always): adds a copy of every source placement that has none on the target layer. A matching UNTRACKED row
    ///   already there (same weenie, cell, position within 1 cm, rotation) is ADOPTED instead of duplicated - safe on layers
    ///   copied by hand or by SQL before. A second run adds nothing.
    /// - A tracked copy someone DELETED BY HAND stays off that tier (the record is kept, so it is not re-added) - 'restore'
    ///   puts those back.
    /// - update (opt-in): a tracked copy whose source moved / changed weenie / rotation is rewritten to match.
    /// - prune (opt-in): a tracked copy whose source is gone is deleted, with its link rows.
    /// - dry: plans everything (guids included) and reports per tier; writes nothing, not even the tracking table.
    /// - Guids: each copy gets a free STATIC guid 0x7LLLLXXX in its own landblock's range, never one used by any row in that
    ///   range (whatever landblock it belongs to) or still listed in zc_layer_clone. Not enough free slots = refused up front.
    /// - Targets 11+ only (never retail) and never the source layer.
    /// - Generator links between source rows are copied between their copies; pruned rows lose their links.
    ///
    /// Review 2026-10-03 (research agent): the world context runs MySqlRetryingExecutionStrategy, which refuses EF queries
    /// and SaveChanges inside a user transaction - so ALL reads happen before the transaction and ALL writes are raw SQL
    /// (batched multi-row INSERTs) inside it.
    /// </summary>
    public static class ZoneControlCloneLayer
    {
        private const string TrackTable = "zc_layer_clone";
        private const float PosEpsilon = 0.01f;
        private const float RotEpsilon = 0.0001f;
        private const int Batch = 500;

        private sealed class Track { public uint Dst, Src; public int SrcVar, DstVar; public string Zone; }

        private sealed class NewRow { public LandblockInstance Src; public uint Guid; public int Variation; }

        private sealed class TierPlan
        {
            public int Variation;
            public readonly List<NewRow> Add = new();
            public readonly List<(LandblockInstance Src, uint Dst)> Adopt = new();
            public readonly List<(LandblockInstance Src, uint Dst)> Update = new();
            public readonly List<uint> Prune = new();          // dst rows to delete (source gone)
            public readonly List<uint> DropTracks = new();     // tracking rows to delete (restored, stale, or copy and source both gone)
            public readonly Dictionary<uint, uint> Alive = new(); // src guid -> its live tracked copy on this tier (the ONE the plan chose)
            public int Unchanged, KeptOff, Drifted, Stale;
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void Run(Session session, List<string> args, Action<string> chatMsg)
        {
            // every report line also goes to the server log, so a run started from outside the game can be read back
            void Msg(string line) { chatMsg(line); log.Info("[clonelayer] " + line); }

            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dry", "update", "prune", "restore", "sync" };
            int? lo = null, hi = null;
            // trailing flags come off first; then the LAST token is the tier range and everything before it is the zone name
            // (review 2026-10-03: "clonelayer Tou Tou 12" with the range forgotten must not read as zone Tou Tou -> v12)
            var tokens = args.Skip(1).ToList();
            while (tokens.Count > 0 && known.Contains(tokens[^1])) { flags.Add(tokens[^1]); tokens.RemoveAt(tokens.Count - 1); }
            var nameTokens = new List<string>();
            if (tokens.Count >= 2 && TryRange(tokens[^1], out var rangeLo, out var rangeHi))
            {
                lo = rangeLo; hi = rangeHi;
                nameTokens = tokens.Take(tokens.Count - 1).ToList();
            }
            var bad = nameTokens.FirstOrDefault(t => known.Contains(t));
            if (bad != null) { Msg($"'{bad}' must come after the tier range - nothing done."); return; }
            if (nameTokens.Count == 0 || lo == null)
            {
                Msg("Usage: clonelayer <zone> <variation|lo-hi> [update] [prune] [restore] [dry]   e.g. clonelayer Tou Tou 12-25 dry"
                    + "   (the tier range is the LAST word before the flags)");
                Msg("  Copies the zone's own layer (its variation) onto the target layers. Never touches rows it did not create.");
                return;
            }
            var area = ZoneControlManager.GetArea(string.Join(" ", nameTokens));
            if (area == null) { Msg($"No zone '{string.Join(" ", nameTokens)}'."); return; }
            var srcVar = area.Variation;
            if (srcVar < 11) { Msg($"'{area.Name}' is variation {srcVar} - clonelayer only copies Zone Control layers (11+)."); return; }
            if (lo < 11 || hi < lo || hi - lo > 50) { Msg("Target variations must be 11+ (lo <= hi, at most 50 wide) - never retail."); return; }
            var targets = Enumerable.Range(lo.Value, hi.Value - lo.Value + 1).Where(v => v != srcVar).ToList();
            if (targets.Count == 0) { Msg("The only target is the source layer itself - nothing to do."); return; }
            var dry = flags.Contains("dry");
            var doUpdate = flags.Contains("update");
            var doPrune = flags.Contains("prune");
            var doRestore = flags.Contains("restore");

            List<ushort> lbs = null;
            for (var attempt = 0; attempt < 3 && lbs == null; attempt++)
                try { lbs = area.Landblocks.ToList(); } catch (InvalidOperationException) { }   // a concurrent addlb / removelb
            if (lbs == null || lbs.Count == 0) { Msg($"'{area.Name}' has no landblocks."); return; }

            try
            {
                using var ctx = new WorldDbContext();

                // ---------------------------------------------------------- READ (no transaction) ----------
                var lbList = lbs.Select(l => (int?)l).ToList();   // landblock = COMPUTED column (obj_Cell_Id >> 16), int? in the model
                var rows = ctx.LandblockInstance.AsNoTracking().Where(i => lbList.Contains(i.Landblock)).ToList();
                var src = rows.Where(r => r.VariationId == srcVar).ToList();
                var srcGuids = src.Select(s => s.Guid).ToHashSet();
                var tableExists = TrackTableExists(ctx);
                var allTracks = tableExists ? ReadTracks(ctx, $"dst_variation IN ({string.Join(",", targets)})") : new List<Track>();

                // which tracked copies still exist - by guid across the WHOLE table (a landblock may have left the zone) AND on
                // the tier they were made for (review: /createinst can hand a freed guid to an unrelated row on another layer)
                var guidVar = new Dictionary<uint, int?>();   // guid -> its variation, for every guid any track names
                var lookup = allTracks.Select(t => t.Dst).Concat(allTracks.Select(t => t.Src)).Distinct().ToList();
                for (var i = 0; i < lookup.Count; i += 1000)
                {
                    var chunk = lookup.Skip(i).Take(1000).ToList();
                    foreach (var r in ctx.LandblockInstance.AsNoTracking().Where(r => chunk.Contains(r.Guid)).Select(r => new { r.Guid, r.VariationId }))
                        guidVar[r.Guid] = r.VariationId;
                }
                bool AliveOn(uint guid, int variation) => guidVar.TryGetValue(guid, out var gv) && gv == variation;
                var existing = new HashSet<uint>(allTracks.Where(t => AliveOn(t.Dst, t.DstVar)).Select(t => t.Dst));

                // reserved guids per landblock range: every row in the range (any landblock, any variation) + every
                // dst_guid still in the tracking table (a hand-deleted copy keeps its record, so its guid stays reserved)
                var reserved = new Dictionary<ushort, HashSet<uint>>();
                var allTrackGuids = tableExists ? ReadTracks(ctx, "1=1").Select(t => t.Dst).ToHashSet() : new HashSet<uint>();
                // a source row that is itself a tracked COPY of another layer (cloning from v12 that holds v11's copies) is
                // never re-copied - it would duplicate every placement (review finding 3)
                var copiesInSource = src.Count(s => allTrackGuids.Contains(s.Guid));
                if (copiesInSource > 0) { src = src.Where(s => !allTrackGuids.Contains(s.Guid)).ToList(); srcGuids = src.Select(s => s.Guid).ToHashSet(); }
                HashSet<uint> Reserved(ushort lb)
                {
                    if (reserved.TryGetValue(lb, out var set)) return set;
                    var first = ObjectGuid.LandblockInstanceGuidBase | ((uint)lb << 12);
                    var last = first | 0xFFF;
                    set = ctx.LandblockInstance.AsNoTracking().Where(i => i.Guid >= first && i.Guid <= last).Select(i => i.Guid).ToHashSet();
                    set.UnionWith(allTrackGuids.Where(g => g >= first && g <= last));
                    reserved[lb] = set;
                    return set;
                }
                var nextSlot = new Dictionary<ushort, uint>();
                uint? Allocate(ushort lb)
                {
                    var first = ObjectGuid.LandblockInstanceGuidBase | ((uint)lb << 12);
                    var set = Reserved(lb);
                    var n = nextSlot.TryGetValue(lb, out var cur) ? cur : 1u;
                    while (n <= 0xFFF && set.Contains(first | n)) n++;
                    if (n > 0xFFF) return null;
                    var g = first | n;
                    set.Add(g);
                    nextSlot[lb] = n + 1;
                    return g;
                }

                // ---------------------------------------------------------- PLAN ----------------------------
                var plans = new List<TierPlan>();
                var noRoom = new Dictionary<ushort, int>();
                void NoRoom(ushort lb) => noRoom[lb] = noRoom.TryGetValue(lb, out var c) ? c + 1 : 1;
                foreach (var v in targets)
                {
                    var plan = new TierPlan { Variation = v };
                    var tierTracks = allTracks.Where(t => t.DstVar == v).ToList();
                    var owned = tierTracks.Select(t => t.Dst).ToHashSet();   // tracked copies from ANY source - never adopted again
                    var mine = tierTracks.Where(t => t.SrcVar == srcVar).GroupBy(t => t.Src).ToDictionary(g => g.Key, g => g.ToList());
                    var layer = rows.Where(r => r.VariationId == v).ToDictionary(r => r.Guid);
                    var adoptPool = layer.Values.Where(r => !owned.Contains(r.Guid) && !allTrackGuids.Contains(r.Guid))
                        .GroupBy(r => (r.WeenieClassId, r.ObjCellId)).ToDictionary(g => g.Key, g => g.ToList());

                    // a track whose dst guid now belongs to an unrelated row on ANOTHER layer is stale: clear the record, never
                    // touch that row (review finding 2)
                    foreach (var t in tierTracks.Where(t => !AliveOn(t.Dst, v) && guidVar.ContainsKey(t.Dst)))
                    { plan.DropTracks.Add(t.Dst); plan.Stale++; }
                    var staleSet = plan.DropTracks.ToHashSet();

                    foreach (var s in src)
                    {
                        var myTracks = mine.TryGetValue(s.Guid, out var allMine)
                            ? allMine.Where(t => !staleSet.Contains(t.Dst)).ToList() : new List<Track>();
                        if (myTracks.Count > 0)
                        {
                            var alive = myTracks.FirstOrDefault(t => existing.Contains(t.Dst));
                            if (alive != null)
                            {
                                plan.Alive[s.Guid] = alive.Dst;
                                if (layer.TryGetValue(alive.Dst, out var d) && !SamePlacement(s, d))
                                {
                                    if (doUpdate) plan.Update.Add((s, alive.Dst));
                                    else plan.Drifted++;
                                }
                                else plan.Unchanged++;
                                continue;
                            }
                            if (!doRestore) { plan.KeptOff++; continue; }   // deleted by hand on purpose - stays off
                            var g = Allocate(LbOf(s));
                            if (g == null) { NoRoom(LbOf(s)); continue; }
                            plan.Add.Add(new NewRow { Src = s, Guid = g.Value, Variation = v });
                            plan.DropTracks.AddRange(myTracks.Select(t => t.Dst));
                            continue;
                        }
                        if (adoptPool.TryGetValue((s.WeenieClassId, s.ObjCellId), out var cands))
                        {
                            var match = cands.FirstOrDefault(c => SamePlacement(s, c));
                            if (match != null) { cands.Remove(match); plan.Adopt.Add((s, match.Guid)); continue; }
                        }
                        var ng = Allocate(LbOf(s));
                        if (ng == null) { NoRoom(LbOf(s)); continue; }
                        plan.Add.Add(new NewRow { Src = s, Guid = ng.Value, Variation = v });
                    }
                    // prune: ONLY when the source row is really gone from the source layer (checked by guid + variation across the
                    // whole table - review finding 1: another zone at the same variation, or a removelb'd landblock, must never
                    // count as "source gone") and only this zone's records
                    if (doPrune)
                        foreach (var t in tierTracks.Where(t => t.SrcVar == srcVar && !staleSet.Contains(t.Dst) && !AliveOn(t.Src, srcVar)
                                                            && string.Equals(t.Zone, area.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (existing.Contains(t.Dst)) plan.Prune.Add(t.Dst);
                            else plan.DropTracks.Add(t.Dst);
                        }
                    plans.Add(plan);
                }

                Report(plans, area.Name, srcVar, src.Count, dry, doUpdate, doPrune, doRestore, Msg);
                if (copiesInSource > 0)
                    Msg($"  ({copiesInSource} rows on the source layer are copies of another layer - left out, never copied again)");
                if (noRoom.Count > 0)
                {
                    Msg($"REFUSED: {noRoom.Values.Sum()} copies have no free static guid on {noRoom.Count} landblock(s) "
                        + $"(4,095 slots per landblock, shared by every tier): {string.Join(", ", noRoom.Keys.Take(8).Select(k => k.ToString("X4")))}. Nothing written.");
                    return;
                }
                if (dry) { Msg("DRY RUN - nothing written. Run it again without 'dry' to apply."); return; }
                if (plans.All(p => p.Add.Count == 0 && p.Adopt.Count == 0 && p.Update.Count == 0 && p.Prune.Count == 0 && p.DropTracks.Count == 0))
                { Msg("Everything is already in sync - nothing to write."); return; }

                // links between source rows, and the links already on the copies (read BEFORE the transaction)
                var srcLinks = ctx.LandblockInstanceLink.AsNoTracking().Where(l => srcGuids.Contains(l.ParentGuid)).ToList()
                    .Where(l => srcGuids.Contains(l.ChildGuid)).ToList();
                var copyGuids = new HashSet<uint>(allTracks.Select(t => t.Dst));
                foreach (var p in plans) copyGuids.UnionWith(p.Adopt.Select(a => a.Dst));
                var copyGuidList = copyGuids.ToList();
                var destLinks = new HashSet<(uint, uint)>();
                for (var i = 0; i < copyGuidList.Count; i += 1000)
                {
                    var chunk = copyGuidList.Skip(i).Take(1000).ToList();
                    foreach (var l in ctx.LandblockInstanceLink.AsNoTracking().Where(l => chunk.Contains(l.ParentGuid)).Select(l => new { l.ParentGuid, l.ChildGuid }))
                        destLinks.Add((l.ParentGuid, l.ChildGuid));
                }

                // ---------------------------------------------------------- WRITE (raw SQL only) -------------
                var written = Apply(ctx, plans, allTracks, srcVar, area.Name, srcLinks, destLinks, existing);
                foreach (var v in targets)
                    foreach (var lb in lbs)
                        DatabaseManager.World.ClearCachedInstancesByLandblock(lb, v);
                Msg($"Done: {written}. World cache cleared for {targets.Count} layer(s) x {lbs.Count} landblock(s) - /reload-landblock a loaded landblock (or restart) to see it.");
                PlayerManager.BroadcastToAuditChannel(session?.Player, $"clonelayer {area.Name} v{srcVar} -> v{lo}-v{hi}{(doUpdate ? " update" : "")}{(doPrune ? " prune" : "")}{(doRestore ? " restore" : "")}: {written}");
            }
            catch (Exception ex)
            {
                Msg($"clonelayer failed - the write is all-or-nothing, so nothing was changed: {ex.Message}");
            }
        }

        private static string Apply(WorldDbContext ctx, List<TierPlan> plans, List<Track> allTracks, int srcVar, string zone,
            List<LandblockInstanceLink> srcLinks, HashSet<(uint, uint)> destLinks, HashSet<uint> existing)
        {
            EnsureTrackTable(ctx);   // DDL commits implicitly - so it runs BEFORE the transaction opens
            var zoneSql = "'" + zone.Replace("\\", "\\\\").Replace("'", "''") + "'";
            var now = "'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";
            int added = 0, adopted = 0, updated = 0, pruned = 0, links = 0, dropped = 0;

            using var tx = ctx.Database.BeginTransaction();
            foreach (var p in plans)
            {
                // src guid -> dst guid on this tier after the write: the plan's chosen live copies + adopted + new
                var copyOf = new Dictionary<uint, uint>(p.Alive);

                if (p.DropTracks.Count > 0) { Exec(ctx, $"DELETE FROM {TrackTable} WHERE dst_guid IN ({string.Join(",", p.DropTracks)})"); dropped += p.DropTracks.Count; }

                // new rows are copied INSIDE the database (INSERT .. SELECT through a temporary map) so every float is
                // bit-exact - EF reads FLOAT at 6 significant digits (review finding 7). CREATE TEMPORARY TABLE does not
                // commit the transaction.
                if (p.Add.Count > 0)
                {
                    Exec(ctx, "CREATE TEMPORARY TABLE IF NOT EXISTS zc_clone_tmp (new_guid INT UNSIGNED NOT NULL PRIMARY KEY, src_guid INT UNSIGNED NOT NULL, variation INT NOT NULL)");
                    Exec(ctx, "DELETE FROM zc_clone_tmp");
                    for (var i = 0; i < p.Add.Count; i += Batch)
                        Exec(ctx, "INSERT INTO zc_clone_tmp (new_guid, src_guid, variation) VALUES "
                            + string.Join(",", p.Add.Skip(i).Take(Batch).Select(n => $"({n.Guid},{n.Src.Guid},{n.Variation})")));
                    Exec(ctx, "INSERT INTO landblock_instance (guid, weenie_Class_Id, obj_Cell_Id, origin_X, origin_Y, origin_Z, "
                        + "angles_W, angles_X, angles_Y, angles_Z, is_Link_Child, last_Modified, variation_Id) "
                        + "SELECT m.new_guid, s.weenie_Class_Id, s.obj_Cell_Id, s.origin_X, s.origin_Y, s.origin_Z, "
                        + $"s.angles_W, s.angles_X, s.angles_Y, s.angles_Z, s.is_Link_Child, {now}, m.variation "
                        + "FROM zc_clone_tmp m JOIN landblock_instance s ON s.guid = m.src_guid");
                    for (var i = 0; i < p.Add.Count; i += Batch)
                        InsertTracks(ctx, p.Add.Skip(i).Take(Batch).Select(n => (n.Guid, n.Src.Guid, n.Variation)), srcVar, zoneSql, now);
                    foreach (var n in p.Add) copyOf[n.Src.Guid] = n.Guid;
                    added += p.Add.Count;
                }
                for (var i = 0; i < p.Adopt.Count; i += Batch)
                {
                    var chunk = p.Adopt.Skip(i).Take(Batch).ToList();
                    InsertTracks(ctx, chunk.Select(a => (a.Dst, a.Src.Guid, p.Variation)), srcVar, zoneSql, now);
                    foreach (var a in chunk) copyOf[a.Src.Guid] = a.Dst;
                    adopted += chunk.Count;
                }
                foreach (var (s, d) in p.Update)
                {
                    Exec(ctx, "UPDATE landblock_instance d JOIN landblock_instance s ON s.guid = " + s.Guid + " SET d.weenie_Class_Id = s.weenie_Class_Id, "
                        + "d.obj_Cell_Id = s.obj_Cell_Id, d.origin_X = s.origin_X, d.origin_Y = s.origin_Y, d.origin_Z = s.origin_Z, "
                        + "d.angles_W = s.angles_W, d.angles_X = s.angles_X, d.angles_Y = s.angles_Y, d.angles_Z = s.angles_Z, "
                        + $"d.is_Link_Child = s.is_Link_Child, d.last_Modified = {now} WHERE d.guid = {d}");
                    updated++;
                }
                if (p.Prune.Count > 0)
                {
                    var ids = string.Join(",", p.Prune);
                    Exec(ctx, $"DELETE FROM landblock_instance_link WHERE parent_GUID IN ({ids}) OR child_GUID IN ({ids})");
                    Exec(ctx, $"DELETE FROM landblock_instance WHERE guid IN ({ids})");
                    Exec(ctx, $"DELETE FROM {TrackTable} WHERE dst_guid IN ({ids})");
                    pruned += p.Prune.Count;
                }

                // generator links between source rows -> the same link between their copies, when missing
                var newLinks = new List<(uint, uint)>();
                foreach (var l in srcLinks)
                {
                    if (!copyOf.TryGetValue(l.ParentGuid, out var pd) || !copyOf.TryGetValue(l.ChildGuid, out var cd)) continue;
                    if (destLinks.Contains((pd, cd)) || p.Prune.Contains(pd) || p.Prune.Contains(cd)) continue;
                    destLinks.Add((pd, cd));
                    newLinks.Add((pd, cd));
                }
                for (var i = 0; i < newLinks.Count; i += Batch)
                {
                    var chunk = newLinks.Skip(i).Take(Batch);
                    Exec(ctx, "INSERT INTO landblock_instance_link (parent_GUID, child_GUID, last_Modified) VALUES "
                        + string.Join(",", chunk.Select(x => $"({x.Item1},{x.Item2},{now})")));
                }
                links += newLinks.Count;
            }
            tx.Commit();
            return $"added {added}, adopted {adopted}, updated {updated}, pruned {pruned}, links {links}" + (dropped > 0 ? $", {dropped} record(s) cleared" : "");
        }

        private static void InsertTracks(WorldDbContext ctx, IEnumerable<(uint Dst, uint Src, int DstVar)> rows, int srcVar, string zoneSql, string now)
        {
            var list = rows.ToList();
            if (list.Count == 0) return;
            Exec(ctx, $"INSERT INTO {TrackTable} (dst_guid, src_guid, src_variation, dst_variation, zone, created) VALUES "
                + string.Join(",", list.Select(r => $"({r.Dst},{r.Src},{srcVar},{r.DstVar},{zoneSql},{now})")));
        }

        // raw SQL with no parameters - EF still runs string.Format over it, so a brace in user text (a zone name) is doubled
        private static void Exec(WorldDbContext ctx, string sql) => ctx.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));

        // the landblock from the cell, never the (computed, nullable) landblock column
        private static ushort LbOf(LandblockInstance r) => (ushort)(r.ObjCellId >> 16);

        private static bool TryRange(string a, out int lo, out int hi)
        {
            lo = hi = 0;
            a = a.TrimStart('v', 'V');
            var dash = a.IndexOf('-');
            if (dash > 0)
                return int.TryParse(a.Substring(0, dash).TrimStart('v', 'V'), out lo) && int.TryParse(a.Substring(dash + 1).TrimStart('v', 'V'), out hi);
            if (int.TryParse(a, out lo)) { hi = lo; return true; }
            return false;
        }

        private static bool SamePlacement(LandblockInstance a, LandblockInstance b) =>
            a.WeenieClassId == b.WeenieClassId && a.ObjCellId == b.ObjCellId
            && Math.Abs(a.OriginX - b.OriginX) < PosEpsilon && Math.Abs(a.OriginY - b.OriginY) < PosEpsilon && Math.Abs(a.OriginZ - b.OriginZ) < PosEpsilon
            && Math.Abs(a.AnglesW - b.AnglesW) < RotEpsilon && Math.Abs(a.AnglesX - b.AnglesX) < RotEpsilon
            && Math.Abs(a.AnglesY - b.AnglesY) < RotEpsilon && Math.Abs(a.AnglesZ - b.AnglesZ) < RotEpsilon
            && a.IsLinkChild == b.IsLinkChild;

        private static void Report(List<TierPlan> plans, string zone, int srcVar, int srcCount, bool dry, bool upd, bool prune, bool restore, Action<string> Msg)
        {
            Msg($"clonelayer {zone}: source v{srcVar}, {srcCount} placement(s){(dry ? " - DRY RUN" : "")}{(upd ? ", update on" : "")}{(prune ? ", prune on" : "")}{(restore ? ", restore on" : "")}");
            foreach (var p in plans)
                Msg($"  v{p.Variation}: add {p.Add.Count}, adopt {p.Adopt.Count}"
                    + (upd ? $", update {p.Update.Count}" : "") + (prune ? $", prune {p.Prune.Count}" : "")
                    + $", already in sync {p.Unchanged}"
                    + (p.Drifted > 0 ? $", {p.Drifted} differ from their source (run with 'update')" : "")
                    + (p.Stale > 0 ? $", {p.Stale} stale record(s) cleared (their guid now belongs to another row)" : "")
                    + (p.KeptOff > 0 ? $", {p.KeptOff} kept off (deleted by hand - 'restore' brings them back)" : ""));
            // what is being added, by name, when it is a small change (a new NPC, say)
            var adds = plans.SelectMany(p => p.Add).GroupBy(n => n.Src.WeenieClassId).ToList();
            if (adds.Count > 0 && adds.Count <= 12)
                foreach (var g in adds)
                {
                    var w = DatabaseManager.World.GetCachedWeenie(g.Key);
                    var name = (w != null ? ACE.Entity.Models.WeenieExtensions.GetName(w) : null) ?? "?";
                    Msg($"    + {name} ({g.Key}) x{g.Count()} copies");
                }
        }

        private static bool TrackTableExists(WorldDbContext ctx)
        {
            var conn = ctx.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{TrackTable}'";
                return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
            finally { if (!wasOpen) conn.Close(); }
        }

        private static void EnsureTrackTable(WorldDbContext ctx) =>
            Exec(ctx, $"CREATE TABLE IF NOT EXISTS {TrackTable} ("
                + "dst_guid INT UNSIGNED NOT NULL PRIMARY KEY, src_guid INT UNSIGNED NOT NULL, src_variation INT NOT NULL, "
                + "dst_variation INT NOT NULL, zone VARCHAR(255) NULL, created DATETIME NOT NULL, "
                + "KEY ix_src (src_guid, dst_variation))");

        private static List<Track> ReadTracks(WorldDbContext ctx, string where)
        {
            var list = new List<Track>();
            var conn = ctx.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT dst_guid, src_guid, src_variation, dst_variation, zone FROM {TrackTable} WHERE {where}";
                using DbDataReader r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(new Track { Dst = Convert.ToUInt32(r.GetValue(0)), Src = Convert.ToUInt32(r.GetValue(1)),
                        SrcVar = Convert.ToInt32(r.GetValue(2)), DstVar = Convert.ToInt32(r.GetValue(3)),
                        Zone = r.IsDBNull(4) ? null : r.GetString(4) });
            }
            finally { if (!wasOpen) conn.Close(); }
            return list;
        }
    }
}
