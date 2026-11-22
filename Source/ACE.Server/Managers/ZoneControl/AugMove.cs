using System;
using System.Collections.Generic;
using System.Linq;

using log4net;
using Newtonsoft.Json;

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
    ///     and the admin undo reads it.
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
        /// An applied undo (saved in the Triune settings, so a restart onto this build keeps it): no hunting reward and no school
        /// gem is handed out after it, so nothing earned on this build between the undo and the switch to the old build is
        /// counted twice there (the undo already turned the earned-past-the-caps part back into Triune / charm counts).
        /// </summary>
        public static bool Undone => TriuneEarning.Current.UndoRan;

        /// <summary>
        /// '/zonecontrol triune move on' after an undo: the admin stays on THIS build. The weapon rows are saved folded again
        /// FIRST, then earning comes back and the move is on again (one save) - a failed rows save changes nothing. The undone
        /// characters are moved again at their next login.
        /// </summary>
        public static int CancelUndo()
        {
            // unreadable Triune settings would refuse the second save after the rows were already folded: refuse first
            if (TriuneEarning.LoadFailed)
                throw new InvalidOperationException("the stored Triune settings could not be read - fix them, then /zonecontrol triune reload (nothing was changed)");
            WeaponScalingManager.CancelRollback();
            try
            {
                TriuneEarning.Mutate(c => { c.MoveOff = false; c.UndoRan = false; });
            }
            catch (Exception ex)
            {
                // the undo stays saved: put the weapon rows back in the old build's shape, so a switch to it still reads them right
                log.Error($"[AugMove] 'move on' could not clear the saved undo - restoring the weapon rollback copy: {ex}");
                try { WeaponScalingManager.SaveUnfoldedForRollback(); }
                catch (Exception ex2) { log.Error($"[AugMove] the weapon rollback copy could NOT be restored - do not switch builds: {ex2}"); }
                throw;
            }
            return MoveOnline();
        }

        /// <summary>
        /// Move the characters ONLINE now (the move was just turned on, or an undo cancelled), not at their next login: until
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
                if (Undone && player.Session?.AccessLevel >= AccessLevel.Admin)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("ADMIN: the Triune move undo is saved - hunting rewards, school gems and the login move are OFF on this build. Switch to the old build, or '/zonecontrol triune move on' to stay.", ChatMessageType.Broadcast));
            }
            catch (Exception ex)
            {
                log.Error($"[AugMove] undo notice for {player.Name}: {ex}");
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
            // the undo switches the move off under the same lock: a login is either moved (record written) before the undo's
            // character loop reads it, or sees MoveOff - never moved after the loop passed it by
            lock (MoveLock)
                return RunMoveLocked(player);
        }

        /// <summary>Held by RunMove and by the undo while it switches the move off.</summary>
        private static readonly object MoveLock = new object();

        private static bool RunMoveLocked(Player player)
        {
            // the admin undo ran: nothing moves again until the old build is back (or the switch is reset). Settings that could
            // not be read may hide a saved MoveOff: no move then - it runs at a later login once they read again.
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

        // ---- the undo (admin, before switching back to the old build) ----------------------------------------------------

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
        /// /zonecontrol triune undomove: every MOVED character (a move record), online and offline; never-moved ones are
        /// untouched. Saves the weapon rows unfolded for the old build and switches the move off (MoveOff) so nobody is moved
        /// again before the restart - both FIRST: when either save fails it throws before any character is changed. From then
        /// on (Undone) this build hands out no hunting reward and no school gem.
        /// </summary>
        public static UndoResult UndoAll(bool apply)
        {
            var result = new UndoResult();
            if (apply)
            {
                var before = TriuneEarning.Current;
                var (wasOff, wasUndo) = (before.MoveOff, before.UndoRan);
                lock (MoveLock)
                    TriuneEarning.Mutate(c => { c.MoveOff = true; c.UndoRan = true; c.UndoAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); });
                try
                {
                    WeaponScalingManager.SaveUnfoldedForRollback();
                }
                catch (Exception ex)
                {
                    // nothing was undone: the switches go back to what they were. A failed put-back is logged (the move then
                    // stays OFF and earning stopped - 'triune movestatus' shows it) and the FIRST error is the one reported.
                    log.Error($"[AugMove] undo: the weapon rows could not be saved - no character was changed: {ex}");
                    try { TriuneEarning.Mutate(c => { c.MoveOff = wasOff; c.UndoRan = wasUndo; }); }
                    catch (Exception revert) { log.Error($"[AugMove] undo: the move switches could not be put back (move OFF, earning stopped): {revert}"); }
                    throw;
                }
            }
            foreach (var p in PlayerManager.GetAllPlayers())
            {
                if (p == null || p.IsDeleted)
                    continue;
                result.Players++;
                Record record = null;
                var json = p.GetProperty(PropertyString.TriuneMoveRecord);
                if (!string.IsNullOrEmpty(json))
                {
                    try { record = JsonConvert.DeserializeObject<Record>(json); }
                    catch (Exception ex)
                    {
                        // its counts may be moved: never "safe to switch" - counted as FAILED (fix the record, run it again)
                        result.Failed++;
                        result.Lines.Add($"  {p.Name}: FAILED - unreadable move record ({ex.Message}), nothing changed");
                        continue;
                    }
                    // a hand-edited "null" record: the move treats it as moved (the record exists), so it is never "never moved"
                    if (record == null)
                    {
                        result.Failed++;
                        result.Lines.Add($"  {p.Name}: FAILED - empty move record (\"{json}\"), nothing changed - fix the record, run it again");
                        continue;
                    }
                }

                long Read(PropertyInt64 prop) => p.GetProperty(prop) ?? 0;
                var plan = PlanUndo(Read, record);
                if (plan == null)
                    continue;
                result.Undone++;

                var (counts, oldCounters) = plan.Value;
                result.Lines.Add($"  {p.Name}: " + string.Join(", ", Schools.Select(s => $"{s.Name} {Read(s.Prop):N0}->{counts[s.Prop]:N0}"))
                    + (oldCounters.Count > 0 ? " | " + string.Join(", ", oldCounters.Select(o => $"{o.Key} {o.Value:N0}")) : ""));
                if (!apply)
                    continue;
                // the record is deleted by WriteUndo: the server log keeps what the move did and what the undo wrote
                log.Info($"[AugMove] undo 0x{p.Guid.Full:X8} record {json} ->{result.Lines[^1]}");

                try
                {
                    WriteUndo(p, record, counts, oldCounters);
                }
                catch (Exception ex)
                {
                    // one bad character never stops the rest; it keeps its record, so a re-run undoes it
                    result.Undone--;
                    result.Failed++;
                    result.Lines.Add($"  {p.Name}: FAILED ({ex.Message}) - re-run the undo");
                    log.Error($"[AugMove] undo of {p.Name} (0x{p.Guid.Full:X8}) failed: {ex}");
                }
            }
            return result;
        }

        private static void WriteUndo(IPlayer p, Record record, Dictionary<PropertyInt64, long> counts, Dictionary<PropertyInt64, long> oldCounters)
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

                if (p is Player online)
                {
                    foreach (var prop in counts.Keys.Concat(oldCounters.Keys))
                        online.Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(online, prop, online.GetProperty(prop) ?? 0));
                    online.CharacterChangesDetected = true;
                    online.SavePlayerToDatabase();
                }
                else if (p is OfflinePlayer offline)
                    offline.SaveBiotaToDatabase();
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
                + $"{(Undone ? $" - UNDO SAVED: no hunting rewards, school gems or login move on this build ('triune move on' resumes; a start {TriuneEarning.UndoKeepHours} h after the undo clears it)" : "")}.");
            return lines;
        }
    }
}
