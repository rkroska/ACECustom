using System;
using System.Collections.Generic;
using System.Linq;

using log4net;
using Newtonsoft.Json;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
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
    ///     0, Points)), so the move hands out no second tier of rewards; a character moved with no own tier known is seeded
    ///     at its first login with one (SeedPending, once - Record.Seeded);
    ///   - what was added and the old values go in PropertyString.TriuneMoveRecord (server-only JSON): its presence = moved,
    ///     and the gates that still name a retired counter read the old value from it (MovedOld).
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
            /// <summary>The own tier's rolls were seeded from the moved Triune. False = moved while no own tier was known (0, or
            /// below T11): the first login with one seeds them (SeedPending). A record from before this flag reads false and
            /// seeds once more - harmless, the rolls only ever go up.</summary>
            public bool Seeded { get; set; }
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
        /// Move the characters ONLINE now (the move was just turned on), not at their next login: until
        /// moved they earn nothing (AwaitsMove). Each moved client gets its new school counts and the old counters at 0, so a
        /// plugin never sees both. Runs from an in-game command, on the world thread. Returns how many were moved.
        /// </summary>
        public static int MoveOnline()
        {
            var moved = 0;
            foreach (var p in PlayerManager.GetAllOnline())
            {
                var held = Moves.Select(m => m.Old).Where(old => p.GetProperty(old).HasValue).ToList();
                bool did;
                try
                {
                    did = RunMove(p);
                }
                catch (Exception ex)
                {
                    log.Error($"[AugMove] move of {p.Name} after 'move on': {ex}");
                    // a throw after the writes (the log or chat line) still moved them: the client must still get the new counts
                    did = held.Any(old => !p.GetProperty(old).HasValue);
                }
                if (!did)
                    continue;
                try
                {
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
                    log.Error($"[AugMove] client update for {p.Name} after 'move on': {ex}");
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

        /// <summary>
        /// The part-way reward after the move (tested): a Triune point was 3 rewards (one per school), so the old part-way
        /// point adds 3x onto the part-way reward - kept below one whole reward (owner 2026-11-22: the small loss is fine).
        /// </summary>
        public static double CarriedFraction(double dripFraction, double triuneFraction)
            => Math.Min(0.999, Math.Max(0, dripFraction) + 3 * Math.Max(0, triuneFraction));

        /// <summary>"T16", or "own tier unknown" when it could not be read.</summary>
        private static string TierText(int tier) => tier > 0 ? $"T{tier}" : "own tier unknown";

        // ---- login ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Called from Player.PlayerEnterWorld before the description goes out (so the hunting numbers it carries already
        /// match the moved counts): the move when it is due, the gem renames, and the hunting numbers for a plugin.
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
                SeedPending(player);
            }
            catch (Exception ex)
            {
                log.Error($"[AugMove] roll seed for {player.Name}: {ex}");
            }
            try
            {
                SchoolGems.UpdateHeldGems(player);   // the gem renames, and stacks of 1,000 (2026-11-24)
                TriuneEarning.PushHuntState(player, full: true, send: false);
            }
            catch (Exception ex)
            {
                log.Error($"[AugMove] login extras for {player.Name}: {ex}");
            }
        }

        /// <summary>
        /// The move, when this character has no record and holds something to move. True when it changed the character: a
        /// move, or stray 0 counters cleared (the caller then sends the client the new state either way).
        /// </summary>
        public static bool RunMove(Player player)
        {
            // world thread only: the hunting rewards, the gems and the luminance buys write the same counts without this lock.
            // It only keeps two moves of one character from overlapping.
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
            if (add.Count == 0 && !(oldFraction > 0))
            {
                // nothing to move - but a stray zero counter still goes, so nothing old is ever sent to a client. A record keeps
                // it, so a gate still reads it as held at 0 (not "never set"). A zero counter set again after an earlier cleanup
                // (an admin, /testchar) goes the same way, merged into the record that is already there.
                var held = Moves.Select(m => m.Old).Where(old => player.GetProperty(old).HasValue).ToList();
                if (held.Count == 0)
                    return false;   // nothing held: the record (if any) already keeps them
                var zeroRecord = new Record { At = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                foreach (var old in held)
                    zeroRecord.Old[(ushort)old] = player.GetProperty(old) ?? 0;
                if (zeroOnly != null)
                    foreach (var (id, v) in zeroOnly.Old)
                        zeroRecord.Old.TryAdd(id, v);
                var zeroJson = JsonConvert.SerializeObject(zeroRecord);
                foreach (var old in held)
                    player.RemoveProperty(old);
                player.SetProperty(PropertyString.TriuneMoveRecord, zeroJson);
                player.CharacterChangesDetected = true;
                return true;    // the client still holds the old counters: it gets them at 0 (a later run finds the record and changes nothing)
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
            // the record and the counts are worked out before the first write, so the move's own writes never stop half-way
            var recordJson = JsonConvert.SerializeObject(record);
            var newCounts = add.ToDictionary(kv => kv.Key, kv => Read(kv.Key) + kv.Value);
            foreach (var (school, v) in newCounts)
                player.SetProperty(school, v);
            foreach (var (old, _) in Moves)
                player.RemoveProperty(old);
            player.RemoveProperty(PropertyFloat.TriuneFraction);
            player.SetProperty(PropertyString.TriuneMoveRecord, recordJson);
            player.CharacterChangesDetected = true;
            SeedSettled.Remove(player);   // a new record: SeedPending looks at it again (a move made online, after 'move on')

            // the rolls the moved Triune already covers at the own tier (read with the NEW counts), per school: never more than
            // that school really climbed into the tier (a Triune holder below a luminance cap keeps the rolls to reach the end).
            // This reads the tier settings: an error here is logged and the move above stands (it is saved either way).
            var own = 0;
            var seeds = new List<string>();
            // the part-way drip reward the character already holds, read BEFORE the roll tier is set: a character with no roll
            // tier yet keeps it (setting the first tier must not wipe it). A roll tier that really moves to another tier still
            // starts fresh, as it does in play.
            var priorRollTier = player.GetProperty(PropertyInt64.AugRollTier) ?? 0;
            var dripFraction = player.GetProperty(PropertyFloat.AugDripFraction) ?? 0;
            try
            {
                var cfg = TriuneEarning.Current;
                own = TierRewardScale.OwnTier(player);
                // no own tier known right now (0: no zone of theirs is on, or below T11): the roll tier, its rolls and the part-way
                // reward stay as they are - a later kill or login sets them once a tier is known again
                if (own >= TriuneEarning.FirstTier)
                {
                    TriuneEarning.EnsureRollTier(player, own);
                    if (priorRollTier != 0 && priorRollTier != own)
                        dripFraction = 0;
                    SeedSchools(player, cfg, own, triune, seeds);
                    record.Seeded = true;
                    player.SetProperty(PropertyString.TriuneMoveRecord, JsonConvert.SerializeObject(record));
                }
            }
            catch (Exception ex)
            {
                log.Error($"[AugMove] {player.Name} (0x{player.Guid.Full:X8}): moved, but the {TierText(own)} rolls could not be seeded (seeded so far: {string.Join(", ", seeds)}): {ex}");
            }
            // the part-way point goes in AFTER the roll tier is set: a new roll tier clears the part-way reward it finds
            if (oldFraction > 0 || dripFraction > 0)
                player.SetProperty(PropertyFloat.AugDripFraction, CarriedFraction(dripFraction, oldFraction));

            var text = AddedText(add);
            log.Info($"[AugMove] {player.Name} (0x{player.Guid.Full:X8}): moved {(text.Length > 0 ? text : "a part-way Triune point only")}; {TierText(own)} rolls seeded {string.Join(", ", seeds)}.");
            if (text.Length > 0)
                player.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Your Triune Weave and charm counts now live in your augmentations: {text}.", ChatMessageType.Broadcast));
            return true;
        }

        /// <summary>
        /// Seeds each school's rolls at <paramref name="own"/> (the roll tier must already be <paramref name="own"/>): the rolls
        /// the moved Triune covers there, never more than the school climbed into the tier. Rolls only go up (max).
        /// </summary>
        private static void SeedSchools(Player player, TriuneEarning.Config cfg, int own, long triune, List<string> seeds)
        {
            foreach (var s in TriuneEarning.DripSchools)
            {
                var seed = TriuneEarning.SeedRolls(cfg, own, triune, s.Count(player), s.IsCreature ? cfg.DripBaseCreature : cfg.DripBaseItem);
                seeds.Add($"{s.Name} {seed}");
                if (seed > 0)
                    player.SetProperty(s.RollsProperty, Math.Max(player.GetProperty(s.RollsProperty) ?? 0, seed));
            }
        }

        /// <summary>
        /// The roll seed for a character moved while no own tier was known (0: no zone of theirs on, or below T11): it runs
        /// the first time an own tier is known - at login (OnLogin, before the hunting numbers go out), or mid-session before
        /// the first roll a kill takes or the hunting numbers set the roll tier (TriuneEarning.Credit / PushHuntState) - from
        /// the Triune count the record kept, and once (the record then says Seeded). The world thread or the player's own
        /// (a kill), so MoveLock keeps it apart from a move. Returns true when it changed the rolls (the caller sends the client
        /// every number).
        /// </summary>
        public static bool SeedPending(Player player)
        {
            // per kill: a character whose seed is settled this session costs one table lookup, never a record read
            if (player == null || SeedSettled.TryGetValue(player, out _))
                return false;
            lock (MoveLock)
            {
                if (TriuneEarning.LoadFailed)
                    return false;   // settings unreadable: try again later
                var json = player.GetProperty(PropertyString.TriuneMoveRecord);
                if (string.IsNullOrEmpty(json) || ZeroOnlyRecord(json) != null)
                    return Settle(player);   // not moved, or only stray 0 counters cleared: nothing was added, nothing to seed
                Record record;
                try
                {
                    record = JsonConvert.DeserializeObject<Record>(json);
                }
                catch (JsonException)
                {
                    return Settle(player);   // an unreadable record never seeds
                }
                if (record == null || record.Seeded)
                    return Settle(player);
                var own = TierRewardScale.OwnTier(player);
                if (own < TriuneEarning.FirstTier)
                    return false;   // still no own tier: a later kill, hunting update or login tries again

                var seeds = new List<string>();
                try
                {
                    var cfg = TriuneEarning.Current;
                    var triune = record.Old.TryGetValue((ushort)PropertyInt64.TriuneWeaveCount, out var t) ? Math.Max(0, t) : 0;
                    // EnsureRollTier keeps the part-way reward on a first roll tier; a roll tier that really moves starts fresh
                    TriuneEarning.EnsureRollTier(player, own);
                    SeedSchools(player, cfg, own, triune, seeds);
                    record.Seeded = true;
                    player.SetProperty(PropertyString.TriuneMoveRecord, JsonConvert.SerializeObject(record));
                    player.CharacterChangesDetected = true;
                    Settle(player);
                    log.Info($"[AugMove] {player.Name} (0x{player.Guid.Full:X8}): moved earlier, not seeded yet; T{own} rolls seeded {string.Join(", ", seeds)}.");
                    return true;
                }
                catch (Exception ex)
                {
                    // never per kill: logged once, then left for this session (the next login tries again). True = the caller
                    // sends the client every number, since the roll tier may have changed before the throw.
                    log.Error($"[AugMove] {player.Name} (0x{player.Guid.Full:X8}): the T{own} roll seed failed (seeded so far: {string.Join(", ", seeds)}): {ex}");
                    Settle(player);
                    return true;
                }
            }
        }

        /// <summary>Characters whose roll seed is settled this session (nothing to seed, or seeded). A move clears the entry.</summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Player, object> SeedSettled = new();
        private static readonly object SettledMark = new object();

        private static bool Settle(Player player)
        {
            SeedSettled.AddOrUpdate(player, SettledMark);
            return false;
        }

        /// <summary>/zonecontrol triune movestatus: how many characters are moved, still to move, and hold retired counters.</summary>
        public static List<string> Status()
        {
            int moved = 0, toMove = 0;
            var lines = new List<string>();
            foreach (var p in PlayerManager.GetAllPlayers())
            {
                // a biota without a character row throws on IsDeleted: skip it, never fail the whole status
                try { if (p == null || p.IsDeleted) continue; }
                catch (Exception) { continue; }
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
                var parts = new List<string>();
                var addedText = AddedText(add);
                if (addedText.Length > 0)
                    parts.Add(addedText);
                if (frac > 0)
                    parts.Add($"a part-way Triune point {frac:0.###}");
                lines.Add($"  to move at next login: {p.Name} - {string.Join(" + ", parts)}");
            }
            var blocked = TriuneEarning.LoadFailed ? " - THE SETTINGS COULD NOT BE READ, nobody is moved" : TriuneEarning.Current.MoveOff ? " - MOVE IS OFF" : "";
            lines.Insert(0, $"Triune move: {moved:N0} moved, {toMove:N0} still to move{blocked}.");
            return lines;
        }
    }
}
