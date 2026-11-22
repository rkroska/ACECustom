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
            /// <summary>Added per school (PropertyInt64 id -> amount).</summary>
            public Dictionary<ushort, long> Added { get; set; } = new();
            /// <summary>The moved counters as they were (PropertyInt64 id -> value).</summary>
            public Dictionary<ushort, long> Old { get; set; } = new();
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
            if (TriuneEarning.Current.MoveOff)
                return false;   // the admin undo ran: nothing moves again until the old build is back (or the switch is reset)
            if (!string.IsNullOrEmpty(player.GetProperty(PropertyString.TriuneMoveRecord)))
                return false;

            long Read(PropertyInt64 p) => player.GetProperty(p) ?? 0;
            var add = Plan(Read);
            var oldFraction = player.GetProperty(PropertyFloat.TriuneFraction) ?? 0;
            if (add.Count == 0 && !(oldFraction > 0))
            {
                // nothing to move - but a stray zero counter still goes, so nothing old is ever sent to a client
                foreach (var (old, _) in Moves)
                    if (player.GetProperty(old).HasValue)
                        player.RemoveProperty(old);
                return false;
            }

            var record = new Record { At = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), OldFraction = oldFraction };
            foreach (var (old, _) in Moves)
                if (player.GetProperty(old) is long v)
                    record.Old[(ushort)old] = v;
            foreach (var (school, n) in add)
                record.Added[(ushort)school] = n;

            var triune = Read(PropertyInt64.TriuneWeaveCount);
            foreach (var (school, n) in add)
                player.SetProperty(school, Read(school) + n);
            foreach (var (old, _) in Moves)
                player.RemoveProperty(old);
            player.RemoveProperty(PropertyFloat.TriuneFraction);
            player.SetProperty(PropertyString.TriuneMoveRecord, JsonConvert.SerializeObject(record));

            // the rolls the moved Triune already covers at the own tier (read with the NEW counts)
            var cfg = TriuneEarning.Current;
            var own = TierRewardScale.OwnTier(player);
            var seed = TriuneEarning.SeedRolls(cfg, own, triune);
            TriuneEarning.EnsureRollTier(player, own);
            if (seed > 0)
                foreach (var s in TriuneEarning.DripSchools)
                    player.SetProperty(s.RollsProperty, Math.Max(player.GetProperty(s.RollsProperty) ?? 0, seed));
            // a Triune point was 3 rewards (one per school): the part-way point carries over, kept below a whole reward
            if (oldFraction > 0)
                player.SetProperty(PropertyFloat.AugDripFraction, Math.Min(0.999, (player.GetProperty(PropertyFloat.AugDripFraction) ?? 0) + 3 * oldFraction));

            player.CharacterChangesDetected = true;
            var text = AddedText(add);
            log.Info($"[AugMove] {player.Name} (0x{player.Guid.Full:X8}): moved {text}; T{own} rolls seeded {seed}.");
            if (text.Length > 0)
                player.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Your Triune Weave and charm counts now live in your augmentations: {text}.", ChatMessageType.Broadcast));
            return true;
        }

        // ---- the undo (admin, before switching back to the old build) ----------------------------------------------------

        public sealed class UndoResult
        {
            public int Players, Undone;
            public List<string> Lines = new();
        }

        /// <summary>
        /// The pure undo of one character (tested): from the current counts and the record, the counts and old counters to write.
        /// Only a MOVED character is undone (owner 2026-11-22): no record = never moved = left exactly as it is, whatever it holds. Subtracts what the move added, gives back the old counters, then turns what was EARNED since
        /// past the T15 caps back into Triune (the part Creature, Item and Life all hold past 6,000 / 4,000 / 4,000) and into
        /// school charms (each attack school past 4,000), so the old build sees the same power. Returns null when nothing changes.
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
            foreach (var (id, v) in record.Old)
                if (v != 0)
                    oldCounters[(PropertyInt64)id] = v;

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
        /// /zonecontrol triune undomove: every MOVED character (a move record), online and offline; never-moved ones are untouched. Saves the weapon rows unfolded for the old build
        /// and switches the move off (MoveOff) so nobody is moved again before the restart.
        /// </summary>
        public static UndoResult UndoAll(bool apply)
        {
            var result = new UndoResult();
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
                    catch (Exception ex) { result.Lines.Add($"  {p.Name}: unreadable move record ({ex.Message}) - skipped"); continue; }
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

                foreach (var (prop, v) in counts)
                    p.SetProperty(prop, v);
                foreach (var (old, _) in Moves)
                    p.RemoveProperty(old);
                foreach (var (prop, v) in oldCounters)
                    p.SetProperty(prop, v);
                if (record?.OldFraction > 0)
                    p.SetProperty(PropertyFloat.TriuneFraction, record.OldFraction);
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

            if (apply)
            {
                TriuneEarning.Mutate(c => c.MoveOff = true);
                WeaponScalingManager.SaveUnfoldedForRollback();
            }
            return result;
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
                if (!string.IsNullOrEmpty(p.GetProperty(PropertyString.TriuneMoveRecord)))
                {
                    moved++;
                    continue;
                }
                var add = Plan(prop => p.GetProperty(prop) ?? 0);
                if (add.Count == 0)
                    continue;
                toMove++;
                lines.Add($"  to move at next login: {p.Name} - {AddedText(add)}");
            }
            lines.Insert(0, $"Triune move: {moved:N0} moved, {toMove:N0} still to move{(TriuneEarning.Current.MoveOff ? " - MOVE IS OFF (undo ran)" : "")}.");
            return lines;
        }
    }
}
