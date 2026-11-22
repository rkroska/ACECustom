using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using log4net;
using Newtonsoft.Json;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// The Triune retire move (owner 2026-11-22), once per character, in code at login (no live SQL): the Triune Weave and the
    /// four attack school charm counts (and the TEST-only T11-T14 drip counters) are added straight onto the LumAug counts and
    /// removed, so a plugin never sees both and adds them twice. Power is unchanged: every gate reads the one count.
    ///   - Triune N  -> Creature, Item and Life +N each;
    ///   - each school charm -> its school (War / Void / Melee / Missile);
    ///   - the drip counters -> their schools;
    ///   - the rolls already taken at the own tier are seeded from Triune (clamp(Triune - the Triune the tiers below asked,
    ///     0, Points)), so the move hands out no second tier of rewards;
    ///   - what was added and the old values go in PropertyString.TriuneMoveRecord (server-only JSON): its presence = moved,
    ///     and the offline undo (RunOfflineUndo, at startup) reads it.
    /// It rides the character's normal save: a crash before the save runs it again from the same old values, nothing doubled.
    /// </summary>
    public static class AugMove
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const int RecordVersion = 1;

        /// <summary>The moved counter -> the school it lands on. Triune lands on three.</summary>
        public static readonly (PropertyInt64 Old, PropertyInt64[] Onto)[] Moves =
        {
            (PropertyInt64.TriuneWeaveCount, new[] { PropertyInt64.LumAugCreatureCount, PropertyInt64.LumAugItemCount, PropertyInt64.LumAugLifeCount }),
            (PropertyInt64.BattlemagesWrathCharmCount, new[] { PropertyInt64.LumAugWarCount }),
            (PropertyInt64.NetherVeilCharmCount, new[] { PropertyInt64.LumAugVoidCount }),
            (PropertyInt64.CrashingSteelCharmCount, new[] { PropertyInt64.LumAugMeleeCount }),
            (PropertyInt64.TrueShotCharmCount, new[] { PropertyInt64.LumAugMissileCount }),
            (PropertyInt64.AugDripCreatureCount, new[] { PropertyInt64.LumAugCreatureCount }),
            (PropertyInt64.AugDripItemCount, new[] { PropertyInt64.LumAugItemCount }),
            (PropertyInt64.AugDripLifeCount, new[] { PropertyInt64.LumAugLifeCount }),
            (PropertyInt64.AugDripWarCount, new[] { PropertyInt64.LumAugWarCount }),
            (PropertyInt64.AugDripVoidCount, new[] { PropertyInt64.LumAugVoidCount }),
            (PropertyInt64.AugDripMeleeCount, new[] { PropertyInt64.LumAugMeleeCount }),
            (PropertyInt64.AugDripMissileCount, new[] { PropertyInt64.LumAugMissileCount }),
        };

        /// <summary>The seven schools in the order the chat line names them.</summary>
        public static readonly (PropertyInt64 Prop, string Name)[] Schools =
        {
            (PropertyInt64.LumAugCreatureCount, "Creature"), (PropertyInt64.LumAugItemCount, "Item"), (PropertyInt64.LumAugLifeCount, "Life"),
            (PropertyInt64.LumAugWarCount, "War"), (PropertyInt64.LumAugVoidCount, "Void"),
            (PropertyInt64.LumAugMeleeCount, "Melee"), (PropertyInt64.LumAugMissileCount, "Missile"),
        };

        /// <summary>What one character's move did - written as JSON into PropertyString.TriuneMoveRecord.</summary>
        public sealed class Record
        {
            public int V { get; set; } = RecordVersion;
            /// <summary>Unix seconds of the move.</summary>
            public long At { get; set; }
            private Dictionary<ushort, long> _added = new(), _old = new();
            /// <summary>Added per school (PropertyInt64 id -> amount). A hand-edited "null" reads as empty.</summary>
            public Dictionary<ushort, long> Added { get => _added; set => _added = value ?? new(); }
            /// <summary>The moved counters as they were (PropertyInt64 id -> value). A hand-edited "null" reads as empty.</summary>
            public Dictionary<ushort, long> Old { get => _old; set => _old = value ?? new(); }
            /// <summary>The old Triune fraction (PropertyFloat.TriuneFraction), 0 when none.</summary>
            public double OldFraction { get; set; }
        }

        /// <summary>
        /// The pure move (tested): from the old counters, what each school gains. Zero / missing counters add nothing.
        /// </summary>
        public static Dictionary<PropertyInt64, long> Plan(Func<PropertyInt64, long> read)
        {
            var add = new Dictionary<PropertyInt64, long>();
            foreach (var (old, onto) in Moves)
            {
                var n = Math.Max(0, read(old));
                if (n <= 0)
                    continue;
                foreach (var school in onto)
                    add[school] = (add.TryGetValue(school, out var a) ? a : 0) + n;
            }
            return add;
        }

        /// <summary>
        /// The value a retired counter held when this character was moved. Gates that still name Triune Weave or a school charm
        /// read at least this (Creature.GetInt64ForGate), so a holder whose bought counts sat below the luminance caps keeps
        /// every gate the old counter opened. Null = no move record, or the counter was never set (a quest's "no quality"
        /// branch still fires); 0 = a record that cannot be read.
        /// </summary>
        public static long? MovedOld(WorldObject wo, PropertyInt64 old) => MovedOld(wo?.GetProperty(PropertyString.TriuneMoveRecord), old);

        /// <summary>MovedOld from the stored record JSON (tests).</summary>
        public static long? MovedOld(string json, PropertyInt64 old)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                var record = JsonConvert.DeserializeObject<Record>(json);
                // the record lists every counter the character HELD (0 included): one it does not list was never set (null)
                return record?.Old != null && record.Old.TryGetValue((ushort)old, out var v) ? Math.Max(0, v) : null;
            }
            catch (JsonException)
            {
                return 0;
            }
        }

        /// <summary>
        /// The switch that ARMS the offline undo (owner 2026-11-22: the undo runs with nobody online, not live). A shard bool in
        /// config_properties_boolean, read directly (not a server property, so the old build never sees it). Set by
        /// '/zonecontrol triune undomove confirm' or one line of SQL with the server off; it STAYS on after the undo, so a
        /// restart of this build (a crash restarter) never lets anyone in to be moved again. Once this build has stopped on it,
        /// only SQL turns it off (UPDATE ... value = 0: this build stops before commands run, the old build cannot set it).
        /// </summary>
        public const string UndoAtStartKey = "zc_triune_undo_at_start";

        /// <summary>The startup read (Program): throws when the row cannot be read - the server must not open on a guess.</summary>
        public static bool ReadUndoAtStart() => DatabaseManager.ShardConfig.GetBool(UndoAtStartKey)?.Value == true;

        /// <summary>For status lines only: a read error shows as not armed.</summary>
        public static bool IsUndoAtStartArmed()
        {
            try { return ReadUndoAtStart(); }
            catch (Exception ex) { log.Error($"[AugMove] {UndoAtStartKey} could not be read: {ex}"); return false; }
        }

        public static void ArmUndoAtStart(bool on)
        {
            const string description = "Triune retire rollback: when 1, this build runs the move undo at startup (nobody online), saves, and stops - every start, until set to 0. The old build ignores it.";
            if (DatabaseManager.ShardConfig.BoolExists(UndoAtStartKey))
                DatabaseManager.ShardConfig.SaveBool(new ConfigPropertiesBoolean { Key = UndoAtStartKey, Value = on, Description = description });
            else
                DatabaseManager.ShardConfig.AddBool(UndoAtStartKey, on, description);
        }

        /// <summary>
        /// The OFFLINE undo (Program, at startup, after every character is loaded and before the network opens): moves every
        /// moved character back for the old build and saves the weapon rows in the old shape, then waits until every save is
        /// written. The caller stops the server. Returns true only when nothing failed AND the database holds no move record on
        /// a live character any more (a character the server could not load at start is never seen by the loop, but its record
        /// is). A character whose undo or save failed keeps its move record in the database (nothing half-done is ever saved),
        /// so the next start of this build re-runs it for exactly those.
        /// </summary>
        public static bool RunOfflineUndo()
        {
            log.Warn($"[AugMove] OFFLINE UNDO ({UndoAtStartKey} = 1): moving every moved character back for the old build. Nobody can log in; the server stops when it is done.");
            var saveFailed = 0;
            UndoResult r;
            try
            {
                r = UndoAll(apply: true, onSaved: ok => { if (!ok) Interlocked.Increment(ref saveFailed); });
            }
            catch (Exception ex)
            {
                // only the weapon rows save throws out of UndoAll (each character's error is its own FAILED line)
                log.Fatal($"[AugMove] OFFLINE UNDO stopped before any character was changed: {ex}. Do NOT start the old build: fix it, then start THIS build again - the undo runs again.");
                return false;
            }
            foreach (var line in r.Lines.Where(l => l.Contains("FAILED")))
                log.Error("[AugMove] undo:" + line);
            // every save went onto the one shard save thread in order: a marker queued after them runs once they are all
            // written. Not disposed: after a timeout the marker still runs later and sets it.
            var written = new ManualResetEventSlim();
            DatabaseManager.Shard.GetCurrentQueueWaitTime(_ => written.Set());
            var allWritten = written.Wait(TimeSpan.FromMinutes(30));
            if (!allWritten)
                log.Error("[AugMove] OFFLINE UNDO: the saves are still being written after 30 minutes - not confirmed (the server stop still waits for them).");
            var failedSaves = Volatile.Read(ref saveFailed);
            int? left = null;
            if (allWritten)
            {
                try { left = RecordsLeftInDatabase(); }
                catch (Exception ex) { log.Error($"[AugMove] OFFLINE UNDO: the move records left could not be counted: {ex}"); }
            }
            var ok = allWritten && r.Failed == 0 && failedSaves == 0 && left == 0;
            if (ok)
                log.Warn($"[AugMove] OFFLINE UNDO DONE: {r.Undone:N0} of {r.Players:N0} characters moved back, no move record left. Start the OLD build now. {UndoAtStartKey} stays 1: THIS build stops at every start until it is set to 0 by SQL.");
            else
                log.Error($"[AugMove] OFFLINE UNDO INCOMPLETE: {r.Failed:N0} characters failed, {failedSaves:N0} saves failed, {(left?.ToString("N0") ?? "unknown")} move records still in the database{(allWritten ? "" : ", saves not confirmed")} (lines above). Do NOT start the old build: fix them and start THIS build again - it re-runs the undo for the ones left.");
            return ok;
        }

        /// <summary>Move records still on a non-deleted character, read from the database after the saves are written.</summary>
        private static int RecordsLeftInDatabase()
        {
            var type = (ushort)PropertyString.TriuneMoveRecord;
            using var ctx = new ShardDbContext();
            return ctx.BiotaPropertiesString.Count(s => s.Type == type && ctx.Character.Any(c => c.Id == s.ObjectId && !c.IsDeleted));
        }

        /// <summary>
        /// Move the characters ONLINE now (the move was just turned on), not at their next login: until
        /// moved they earn nothing (AwaitsMove). Each moved client gets its new school counts and the old counters at 0, so a
        /// plugin never sees both. Runs from an in-game command, on the world thread. Returns how many were moved.
        /// </summary>
        public static int MoveOnline()
        {
            var moved = 0;
            foreach (var p in PlayerManager.GetAllOnline())
            {
                try
                {
                    var held = Moves.Select(m => m.Old).Where(old => p.GetProperty(old).HasValue).ToList();
                    if (!RunMove(p))
                        continue;
                    moved++;
                    foreach (var s in Schools)
                        p.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(p, s.Prop, p.GetProperty(s.Prop) ?? 0));
                    foreach (var old in held)
                        if (!p.GetProperty(old).HasValue)
                            p.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(p, old, 0));
                    TriuneEarning.PushHuntState(p, full: true);
                }
                catch (Exception ex)
                {
                    log.Error($"[AugMove] move of {p?.Name} after 'move on': {ex}");
                }
            }
            return moved;
        }

        /// <summary>
        /// The character still holds a retired counter the login move has not taken (the move was off or skipped): it earns no
        /// hunting reward and no school gem until it is moved, so nothing is earned twice (on the old counts, then the move).
        /// </summary>
        public static bool AwaitsMove(WorldObject wo)
        {
            if (wo == null)
                return false;
            // per kill: the cheap reads first - a moved character holds none of these, so the record is never parsed for it
            var holds = (wo.GetProperty(PropertyFloat.TriuneFraction) ?? 0) > 0;
            foreach (var (old, _) in Moves)
                holds |= (wo.GetProperty(old) ?? 0) > 0;
            if (!holds)
                return false;
            var json = wo.GetProperty(PropertyString.TriuneMoveRecord);
            return string.IsNullOrEmpty(json) || ZeroOnlyRecord(json) != null;
        }

        /// <summary>The record when it only kept stray 0 counters (nothing added, no fraction), else null.</summary>
        private static Record ZeroOnlyRecord(string json)
        {
            try
            {
                var r = JsonConvert.DeserializeObject<Record>(json);
                return r != null && r.Added.Count == 0 && !(r.OldFraction > 0) && r.Old.Values.All(v => v <= 0) ? r : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>"Creature +141, Item +141, Life +141, War +227" - the schools that gained, in the fixed order.</summary>
        public static string AddedText(IReadOnlyDictionary<PropertyInt64, long> added)
            => string.Join(", ", Schools.Where(s => added.TryGetValue(s.Prop, out var n) && n > 0).Select(s => $"{s.Name} +{added[s.Prop]:N0}"));

        // ---- login ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Called from Player.PlayerEnterWorld before the description goes out (so the login packet already carries the new
        /// counts and no retired counter): the move when it is due, the gem renames, and the hunting numbers for a plugin.
        /// </summary>
        public static void OnLogin(Player player)
        {
            if (player == null)
                return;
            try
            {
                RunMove(player);
            }
            catch (Exception ex)
            {
                log.Error($"[AugMove] move for {player.Name}: {ex}");
            }
            try
            {
                SchoolGems.RenameHeldGems(player);
                TriuneEarning.PushHuntState(player, full: true, send: false);
            }
            catch (Exception ex)
            {
                log.Error($"[AugMove] login extras for {player.Name}: {ex}");
            }
        }

        /// <summary>The move, when this character has no record and holds something to move. True when it moved.</summary>
        public static bool RunMove(Player player)
        {
            // one move at a time (its callers run on the world thread today; the lock keeps it safe for any other)
            lock (MoveLock)
                return RunMoveLocked(player);
        }

        /// <summary>Held by RunMove.</summary>
        private static readonly object MoveLock = new object();

        private static bool RunMoveLocked(Player player)
        {
            // 'triune move off': nobody is moved at login. Settings that could not be read may hide a saved MoveOff: no move
            // then - it runs at a later login once they read again.
            var cfg0 = TriuneEarning.Current;
            if (TriuneEarning.LoadFailed || cfg0.MoveOff)
                return false;
            // moved once - unless that record only kept stray 0 counters: a counter set since (an admin) still moves
            Record zeroOnly = null;
            var existing = player.GetProperty(PropertyString.TriuneMoveRecord);
            if (!string.IsNullOrEmpty(existing) && (zeroOnly = ZeroOnlyRecord(existing)) == null)
                return false;

            long Read(PropertyInt64 p) => player.GetProperty(p) ?? 0;
            var add = Plan(Read);
            var oldFraction = player.GetProperty(PropertyFloat.TriuneFraction) ?? 0;
            if (add.Count == 0 && !(oldFraction > 0) && zeroOnly != null)
                return false;   // still nothing but zeros: the record already keeps them
            if (add.Count == 0 && !(oldFraction > 0))
            {
                // nothing to move - but a stray zero counter still goes, so nothing old is ever sent to a client. A record keeps
                // it, so a gate still reads it as held at 0 (not "never set") and the undo puts it back.
                var held = Moves.Select(m => m.Old).Where(old => player.GetProperty(old).HasValue).ToList();
                if (held.Count == 0)
                    return false;
                var zeroRecord = new Record { At = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                foreach (var old in held)
                    zeroRecord.Old[(ushort)old] = player.GetProperty(old) ?? 0;
                var zeroJson = JsonConvert.SerializeObject(zeroRecord);
                foreach (var old in held)
                    player.RemoveProperty(old);
                player.SetProperty(PropertyString.TriuneMoveRecord, zeroJson);
                player.CharacterChangesDetected = true;
                return false;
            }

            var record = new Record { At = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), OldFraction = oldFraction };
            foreach (var (old, _) in Moves)
                if (player.GetProperty(old) is long v)
                    record.Old[(ushort)old] = v;
            foreach (var (school, n) in add)
                record.Added[(ushort)school] = n;
            if (zeroOnly != null)
                foreach (var (id, v) in zeroOnly.Old)
                    record.Old.TryAdd(id, v);

            var triune = Read(PropertyInt64.TriuneWeaveCount);
            // everything that can throw is worked out before the first write, so the writes below never stop half-way
            var recordJson = JsonConvert.SerializeObject(record);
            var newCounts = add.ToDictionary(kv => kv.Key, kv => Read(kv.Key) + kv.Value);
            foreach (var (school, v) in newCounts)
                player.SetProperty(school, v);
            foreach (var (old, _) in Moves)
                player.RemoveProperty(old);
            player.RemoveProperty(PropertyFloat.TriuneFraction);
            player.SetProperty(PropertyString.TriuneMoveRecord, recordJson);

            // the rolls the moved Triune already covers at the own tier (read with the NEW counts), per school: never more than
            // that school really climbed into the tier (a Triune holder below a luminance cap keeps the rolls to reach the end)
            var cfg = TriuneEarning.Current;
            var own = TierRewardScale.OwnTier(player);
            TriuneEarning.EnsureRollTier(player, own);
            var seeds = new List<string>();
            foreach (var s in TriuneEarning.DripSchools)
            {
                var seed = TriuneEarning.SeedRolls(cfg, own, triune, s.Count(player), s.IsCreature ? cfg.DripBaseCreature : cfg.DripBaseItem);
                seeds.Add($"{s.Name} {seed}");
                if (seed > 0)
                    player.SetProperty(s.RollsProperty, Math.Max(player.GetProperty(s.RollsProperty) ?? 0, seed));
            }
            // a Triune point was 3 rewards (one per school): the part-way point carries over, kept below a whole reward
            if (oldFraction > 0)
                player.SetProperty(PropertyFloat.AugDripFraction, Math.Min(0.999, (player.GetProperty(PropertyFloat.AugDripFraction) ?? 0) + 3 * oldFraction));

            player.CharacterChangesDetected = true;
            var text = AddedText(add);
            log.Info($"[AugMove] {player.Name} (0x{player.Guid.Full:X8}): moved {text}; T{own} rolls seeded {string.Join(", ", seeds)}.");
            if (text.Length > 0)
                player.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Your Triune Weave and charm counts now live in your augmentations: {text}.", ChatMessageType.Broadcast));
            return true;
        }

        // ---- the undo (offline, at startup, before switching back to the old build) ----------------------------------------------------

        public sealed class UndoResult
        {
            public int Players, Undone, Failed;
            public List<string> Lines = new();
        }

        /// <summary>
        /// The pure undo of one character (tested): from the current counts and the record, the counts and old counters to write.
        /// Only a MOVED character is undone (owner 2026-11-22): no record = never moved = left exactly as it is, whatever it holds. Subtracts what the move added, gives back the old counters, then turns what was EARNED since
        /// past the T15 caps back into Triune (the part Creature, Item and Life all hold past 6,000 / 4,000 / 4,000) and into
        /// school charms (each attack school past 4,000), so the old build sees the same power. Returns null only when there is no record.
        /// </summary>
        public static (Dictionary<PropertyInt64, long> Counts, Dictionary<PropertyInt64, long> OldCounters)? PlanUndo(
            Func<PropertyInt64, long> read, Record record)
        {
            if (record == null)
                return null;
            var counts = Schools.ToDictionary(s => s.Prop, s => read(s.Prop));
            var oldCounters = new Dictionary<PropertyInt64, long>();
            foreach (var (id, n) in record.Added)
                if (counts.ContainsKey((PropertyInt64)id))
                    counts[(PropertyInt64)id] = Math.Max(0, counts[(PropertyInt64)id] - n);
            // every counter the record held comes back, a 0 included (a quest reads "held at 0" and "never set" differently)
            foreach (var (id, v) in record.Old)
                oldCounters[(PropertyInt64)id] = v;
            // Triune or a school charm held NOW was set after the move (the old build ran in between, or staff set it) - the move took
            // every one off, and this build counts it in power unmoved: it goes back on top, never deleted by WriteUndo. A drip
            // counter held now is NOT in power on this build (the Effective counts skip it), so it is not given back.
            foreach (var (old, _) in Moves.Where(m => m.Old < PropertyInt64.AugDripCreatureCount || m.Old > PropertyInt64.AugDripMissileCount))
            {
                var held = read(old);
                if (held > 0)
                    oldCounters[old] = (oldCounters.TryGetValue(old, out var o) ? o : 0) + held;
            }

            // the old build never had the drip: a restored drip counter would sit unread, so it is folded into its school FIRST (then the
            // conversion below sees it)
            foreach (var (old, onto) in Moves.Where(m => m.Old >= PropertyInt64.AugDripCreatureCount && m.Old <= PropertyInt64.AugDripMissileCount))
                if (oldCounters.Remove(old, out var d) && d > 0)
                    counts[onto[0]] += d;

            // since-earned past the caps -> the old counters (the drip counters never existed on the old build: they go to the
            // schools the same way, so only Triune and the charms are written)
            var excess = Math.Max(0, Math.Min(counts[PropertyInt64.LumAugCreatureCount] - Creature.CreatureAugBase,
                Math.Min(counts[PropertyInt64.LumAugItemCount] - Creature.SchoolAugBase, counts[PropertyInt64.LumAugLifeCount] - Creature.SchoolAugBase)));
            if (excess > 0)
            {
                counts[PropertyInt64.LumAugCreatureCount] -= excess;
                counts[PropertyInt64.LumAugItemCount] -= excess;
                counts[PropertyInt64.LumAugLifeCount] -= excess;
                oldCounters[PropertyInt64.TriuneWeaveCount] = (oldCounters.TryGetValue(PropertyInt64.TriuneWeaveCount, out var t) ? t : 0) + excess;
            }
            foreach (var (school, charm) in new[]
            {
                (PropertyInt64.LumAugWarCount, PropertyInt64.BattlemagesWrathCharmCount), (PropertyInt64.LumAugVoidCount, PropertyInt64.NetherVeilCharmCount),
                (PropertyInt64.LumAugMeleeCount, PropertyInt64.CrashingSteelCharmCount), (PropertyInt64.LumAugMissileCount, PropertyInt64.TrueShotCharmCount),
            })
            {
                var past = counts[school] - Creature.SchoolAugBase;
                if (past <= 0)
                    continue;
                counts[school] -= past;
                oldCounters[charm] = (oldCounters.TryGetValue(charm, out var c) ? c : 0) + past;
            }
            return (counts, oldCounters);
        }

        /// <summary>
        /// The move undo over every MOVED character (a move record); never-moved ones are untouched. Without apply it only
        /// lists what it would do ('/zonecontrol triune undomove', read only). With apply it runs ONLY offline (RunOfflineUndo, at
        /// startup): the weapon rows are saved unfolded FIRST - a failed save throws before any character is changed - and an
        /// online character is refused.
        /// </summary>
        public static UndoResult UndoAll(bool apply, Action<bool> onSaved = null)
        {
            var result = new UndoResult();
            if (apply)
                WeaponScalingManager.SaveUnfoldedForRollback();
            foreach (var p in PlayerManager.GetAllPlayers())
            {
                if (p == null)
                    continue;
                // ANY error on one character (IsDeleted reads the character row: a biota without one throws) is that
                // character's FAILED line, never an abort of the run: its changes are never saved and it keeps its record
                var planned = false;
                try
                {
                    UndoOne(p, apply, onSaved, result, ref planned);
                }
                catch (Exception ex)
                {
                    if (planned)
                        result.Undone--;
                    result.Failed++;
                    result.Lines.Add($"  {p.Name}: FAILED ({ex.Message}) - nothing saved for it, re-run the undo");
                    log.Error($"[AugMove] undo of {p.Name} (0x{p.Guid.Full:X8}) failed: {ex}");
                }
            }
            return result;
        }

        /// <summary>
        /// Reads a stored move record for the undo (tested). Returns null when it is usable, else what is wrong with it. A record
        /// that is there counts as moved, an empty one too ("" or "null" by hand): never "never moved", or the DB count of
        /// records left (RecordsLeftInDatabase) would keep the undo INCOMPLETE without naming the character. Every record the
        /// move writes has its time and this version (a zero-only one too): "{}" or another version would be deleted without
        /// putting anything back.
        /// </summary>
        public static string ReadUndoRecord(string json, out Record record)
        {
            record = null;
            Record r;
            try { r = JsonConvert.DeserializeObject<Record>(json ?? ""); }
            catch (Exception ex) { return $"unreadable move record ({ex.Message})"; }
            if (r == null)
                return $"empty move record (\"{json}\")";
            if (r.At <= 0 || r.V != RecordVersion)
                return $"incomplete move record (version {r.V}, time {r.At})";
            record = r;
            return null;
        }

        /// <summary>One character of UndoAll. Sets planned once it is counted in Undone (the caller takes it back on an error).</summary>
        private static void UndoOne(IPlayer p, bool apply, Action<bool> onSaved, UndoResult result, ref bool planned)
        {
            var json = p.GetProperty(PropertyString.TriuneMoveRecord);
            if (json == null)
            {
                // never moved: nothing to undo, and never a FAILED line - a biota without a character row (IsDeleted throws)
                // holds no record, so it must not keep the undo from DONE
                try { if (!p.IsDeleted) result.Players++; } catch (Exception) { }
                return;
            }
            if (p.IsDeleted)
                return;
            result.Players++;
            var problem = ReadUndoRecord(json, out var record);
            if (problem != null)
            {
                // its counts may be moved: never "safe to switch" - counted as FAILED (fix the record, run it again)
                result.Failed++;
                result.Lines.Add($"  {p.Name}: FAILED - {problem}, nothing changed - fix the record, run it again");
                return;
            }

            long Read(PropertyInt64 prop) => p.GetProperty(prop) ?? 0;
            var plan = PlanUndo(Read, record);
            if (plan == null)
                return;
            result.Undone++;
            planned = true;

            var (counts, oldCounters) = plan.Value;
            result.Lines.Add($"  {p.Name}: " + string.Join(", ", Schools.Select(s => $"{s.Name} {Read(s.Prop):N0}->{counts[s.Prop]:N0}"))
                + (oldCounters.Count > 0 ? " | " + string.Join(", ", oldCounters.Select(o => $"{o.Key} {o.Value:N0}")) : ""));
            if (!apply)
                return;
            if (p is not OfflinePlayer offline)
                throw new InvalidOperationException("online - the undo runs only at startup, with nobody online");
            // the record is deleted by WriteUndo: the server log keeps what the move did and what the undo wrote
            log.Info($"[AugMove] undo 0x{p.Guid.Full:X8} record {json} ->{result.Lines[^1]}");
            // one bad character never stops the rest (the caller counts it): it keeps its record, so a re-run undoes it
            WriteUndo(offline, counts, oldCounters, onSaved);
        }

        private static void WriteUndo(OfflinePlayer p, Dictionary<PropertyInt64, long> counts, Dictionary<PropertyInt64, long> oldCounters, Action<bool> onSaved)
        {
            {
                foreach (var (prop, v) in counts)
                    p.SetProperty(prop, v);
                foreach (var (old, _) in Moves)
                    p.RemoveProperty(old);
                foreach (var (prop, v) in oldCounters)
                    p.SetProperty(prop, v);
                // the part-way reward goes back as the part-way Triune point it was (a Triune point = 3 rewards). The move turned
                // OldFraction into the drip fraction, which this build may have spent since: what is LEFT goes back, never both.
                var leftFraction = Math.Min(0.999, (p.GetProperty(PropertyFloat.AugDripFraction) ?? 0) / 3);
                if (leftFraction > 0)
                    p.SetProperty(PropertyFloat.TriuneFraction, leftFraction);
                p.RemoveProperty(PropertyString.TriuneMoveRecord);
                foreach (var prop in new[] { PropertyInt64.AugRollTier, PropertyInt64.AugRollsCreature, PropertyInt64.AugRollsItem, PropertyInt64.AugRollsLife })
                    p.RemoveProperty(prop);
                p.RemoveProperty(PropertyFloat.AugDripFraction);

                p.SaveBiotaToDatabase(true, onSaved);
            }
        }

        /// <summary>/zonecontrol triune movestatus: how many characters are moved, still to move, and hold retired counters.</summary>
        public static List<string> Status()
        {
            int moved = 0, toMove = 0;
            var lines = new List<string>();
            foreach (var p in PlayerManager.GetAllPlayers())
            {
                if (p == null || p.IsDeleted)
                    continue;
                // the same "still to move" test as the login move: no record, or a zero-only record with something new to move
                var json = p.GetProperty(PropertyString.TriuneMoveRecord);
                var add = Plan(prop => p.GetProperty(prop) ?? 0);
                var frac = p.GetProperty(PropertyFloat.TriuneFraction) ?? 0;
                var pending = add.Count > 0 || frac > 0;
                if (!string.IsNullOrEmpty(json) && !(pending && ZeroOnlyRecord(json) != null))
                {
                    moved++;
                    continue;
                }
                if (!pending)
                    continue;
                toMove++;
                lines.Add($"  to move at next login: {p.Name} - {AddedText(add)}{(frac > 0 ? $" (+ a part-way Triune point {frac:0.###})" : "")}");
            }
            lines.Insert(0, $"Triune move: {moved:N0} moved, {toMove:N0} still to move{(TriuneEarning.Current.MoveOff ? " - MOVE IS OFF" : "")}"
                + $"{(IsUndoAtStartArmed() ? $" - UNDO ARMED: it runs at the next server start, then the server stops ({UndoAtStartKey})" : "")}.");
            return lines;
        }
    }
}
