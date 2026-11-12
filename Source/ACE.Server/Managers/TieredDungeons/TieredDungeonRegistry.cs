using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.WeaponScaling;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.TieredDungeons
{
    /// <summary>
    /// One retail dungeon that can also be entered as a per-tier copy (variation N = tier N, 11+).
    /// The copy is built in memory from the retail (base) spawn rows; no retail row is edited or copied.
    /// </summary>
    public sealed class TieredDungeon
    {
        public string Name;

        /// <summary>The staging landblock. Only the wcids in <see cref="LobbyKeepWcids"/> are spawned in a tier copy of it.</summary>
        public ushort Lobby;
        public HashSet<ushort> Arenas = new();

        /// <summary>Retail lobby rows a tier copy keeps (keyed on the RETAIL wcid, before any swap).</summary>
        public HashSet<uint> LobbyKeepWcids = new();

        /// <summary>Retail wcid -> tier-only wcid, applied to the rows of a tier copy.</summary>
        public Dictionary<uint, uint> WcidSwaps = new();

        /// <summary>Extra rows a tier copy of the lobby gets (the tier exit portal). Positions are absolute.</summary>
        public List<LandblockInstance> LobbyExtraRows = new();

        /// <summary>World events the dungeon runs on. A tier copy gets its own tagged copy of each.</summary>
        public List<string> EventNames = new();

        /// <summary>Quest names that stay untagged inside a tier copy (the cross-tier pay timer).</summary>
        public HashSet<string> ExemptNames = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>LocalSignal a room coordinator sends when its last wave passes.</summary>
        public string RoomClearedSignal;

        /// <summary>Room N's coordinator stands near Y = Room1Y + (N - 1) * RoomSpacingY.</summary>
        public float Room1Y;
        public float RoomSpacingY;
        public int RoomCount;

        /// <summary>The first room of a tier run (the Advanced entrance).</summary>
        public int FirstRoom;

        public bool Contains(ushort landblock) => landblock == Lobby || Arenas.Contains(landblock);
    }

    /// <summary>
    /// Tiered dungeons (owner 2026-10-09): the registry, the tier scope for quest / event names, and the pay rules.
    ///
    /// RETAIL SAFETY: every branch that reads this class does nothing unless ALL THREE hold - the server switch
    /// tiered_dungeons_enabled is on, the landblock belongs to a registered dungeon, and the variation is an
    /// enabled tier (11+). Retail players stand at variation null, so none of it can reach them.
    /// </summary>
    public static class TieredDungeonRegistry
    {
        public const int MinTier = 11;
        public const int MaxTier = 25;

        /// <summary>Goes between a quest / event name and any '@' comment: "ColoArenaOneAccess~v11@UseFellow".
        /// QuestManager.GetQuestName and EventManager.GetEventName cut a name at the first '@', so the tag must sit before it.</summary>
        public const string TagMark = "~v";

        // Tier-only weenies (Database/Updates/World/2026-10-09-00-Tiered-Colosseum.sql)
        public const uint TierArbitratorWcid = 78780800;
        public const uint TierTicketWcid = 78780801;
        public const uint TierTicketSellerWcid = 78780802;
        public const uint TierExitPortalWcid = 78780803;

        /// <summary>Per-player room credits of the current run (bit N-1 = room N). Tagged per tier like every other flag.</summary>
        public const string RoomCreditQuest = "TieredColoRooms";
        /// <summary>The highest tier among the players credited beside this player in the current run (stored as solves).</summary>
        public const string TopTierQuest = "TieredColoTopTier";
        /// <summary>The 20 hour pay timer. EXEMPT from tagging: one timer across every tier.</summary>
        public const string PayTimerQuest = "TieredColoPayTimer";

        public static readonly TieredDungeon Colosseum = BuildColosseum();

        private static readonly Dictionary<ushort, TieredDungeon> byLandblock = BuildIndex();

        private static TieredDungeon BuildColosseum()
        {
            var d = new TieredDungeon
            {
                Name = "Colosseum",
                Lobby = 0x00AF,
                RoomClearedSignal = "RevealPortal",
                Room1Y = -1560f,      // bell generator 71703 in 0x00B0; bells sit 90 apart
                RoomSpacingY = 90f,
                RoomCount = 18,
                FirstRoom = 6,
            };
            foreach (ushort lb in new ushort[] { 0x00B0, 0x00B1, 0x00B2, 0x00B3, 0x00B4 })
                d.Arenas.Add(lb);

            // The tier lobby holds only what a run needs. NOT kept: mini-boss portals, statues, locked doors, vault,
            // coin collector, retail champions, seasonal arenas and their generators, the room-1 teleporter (34727).
            foreach (uint wcid in new uint[]
            {
                1154,   // linkmonstergen (parent of the arena teleporter links)
                28282,  // linkmonstergen10seconds (parent of the Arbitrator / ticket seller links)
                34441,  // Master Arbitrator        -> swapped for the tier Arbitrator
                34442,  // Colosseum Ticket Vendor  -> swapped for the tier ticket seller
                34728,  // Advanced Colosseum Arena teleporter (rooms 6-18)
                34726,  // garbage barrels
                71705, 71511, 71512, 71513, 71514,  // arena one-five stopgap generators (start / stop the arena events)
            })
                d.LobbyKeepWcids.Add(wcid);

            d.WcidSwaps[34441] = TierArbitratorWcid;
            d.WcidSwaps[34442] = TierTicketSellerWcid;

            // tier exit, beside the retail arrival point (0x00AF0118 40, -13.2, 0)
            d.LobbyExtraRows.Add(new LandblockInstance
            {
                WeenieClassId = TierExitPortalWcid,
                ObjCellId = 0x00AF0118,
                OriginX = 40f, OriginY = -6f, OriginZ = 0.005f,
                AnglesW = 1f, AnglesX = 0f, AnglesY = 0f, AnglesZ = 0f,
            });

            foreach (var arena in new[] { "One", "Two", "Three", "Four", "Five" })
            {
                d.EventNames.Add($"ColoArena{arena}InUse");
                d.EventNames.Add($"ColoArena{arena}InActive");
            }

            d.ExemptNames.Add(PayTimerQuest);
            return d;
        }

        private static Dictionary<ushort, TieredDungeon> BuildIndex()
        {
            var map = new Dictionary<ushort, TieredDungeon>();
            foreach (var d in new[] { Colosseum })
            {
                map[d.Lobby] = d;
                foreach (var lb in d.Arenas)
                    map[lb] = d;
            }
            return map;
        }

        // ------------------------------------------------------------------ switch + tiers

        // Parsed tiered_dungeon_tiers, re-parsed only when the string changes (read on hot paths).
        private sealed record TierSet(string Raw, HashSet<int> Tiers);
        private static volatile TierSet tierSet = new(null, new HashSet<int>());

        public static bool Enabled => ServerConfig.tiered_dungeons_enabled.Value;

        public static bool IsTierEnabled(int tier)
        {
            if (!Enabled || tier < MinTier || tier > MaxTier)
                return false;
            var raw = ServerConfig.tiered_dungeon_tiers.Value;
            var snapshot = tierSet;
            if (!ReferenceEquals(raw, snapshot.Raw))
                tierSet = snapshot = new TierSet(raw, ParseTiers(raw));   // swapped as one object
            return snapshot.Tiers.Contains(tier);
        }

        /// <summary>"11", "11,12", "11-15" or a mix. Anything outside 11-25 or unreadable is dropped.</summary>
        public static HashSet<int> ParseTiers(string raw)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(raw))
                return set;
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var dash = token.IndexOf('-');
                if (dash > 0
                    && int.TryParse(token.AsSpan(0, dash), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lo)
                    && int.TryParse(token.AsSpan(dash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hi))
                {
                    for (var t = Math.Max(lo, MinTier); t <= Math.Min(hi, MaxTier); t++)
                        set.Add(t);
                }
                else if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var one) && one >= MinTier && one <= MaxTier)
                    set.Add(one);
            }
            return set;
        }

        public static TieredDungeon Find(ushort landblock)
            => byLandblock.TryGetValue(landblock, out var d) ? d : null;

        /// <summary>The dungeon when a tier copy may be BUILT for (landblock, variation): registered, an enabled tier, and
        /// the switch on. Only the landblock loader and the entry check ask this.</summary>
        public static TieredDungeon FindTierCopy(ushort landblock, int? variation)
        {
            if (!variation.HasValue || variation.Value < MinTier)
                return null;
            if (!byLandblock.TryGetValue(landblock, out var d))
                return null;
            return IsTierEnabled(variation.Value) ? d : null;
        }

        public static bool IsTierCopy(ushort landblock, int? variation) => FindTierCopy(landblock, variation) != null;

        /// <summary>
        /// The dungeon when something STANDS in a tier copy: a registered landblock at variation 11+. Deliberately not
        /// tied to the switch. An object at variation 11+ on these landblocks can only be part of a tier copy (retail is
        /// variation null and no rows exist at 11+), and a copy that is already loaded must keep using its own tagged
        /// flags, events and teleports if an admin turns the switch off mid-run - falling back to the retail names at
        /// that moment is exactly how a tier run would reach retail state. Every scope, teleport and pay hook asks this.
        /// </summary>
        public static TieredDungeon FindCopyAt(ushort landblock, int? variation)
        {
            if (!variation.HasValue || variation.Value < MinTier || variation.Value > MaxTier)
                return null;
            return byLandblock.TryGetValue(landblock, out var d) ? d : null;
        }

        /// <summary>The tier of the copy this position is in, or 0.</summary>
        public static int TierAt(Position pos)
            => pos != null && FindCopyAt(pos.LandblockId.Landblock, pos.Variation) != null ? pos.Variation.Value : 0;

        /// <summary>True when a portal leads into a registered dungeon at a tier variation whose copy cannot be built
        /// right now (switch off, or the tier is not enabled). Such a landblock would load empty, with no way out.</summary>
        public static bool LeadsToClosedCopy(Position destination)
            => destination != null && FindCopyAt(destination.LandblockId.Landblock, destination.Variation) != null
               && FindTierCopy(destination.LandblockId.Landblock, destination.Variation) == null;

        /// <summary>
        /// The variation a teleport should land in. A teleport whose destination data carries no variation normally
        /// lands in the base world; when the traveller stands in a tier copy and the destination is a landblock of
        /// the SAME dungeon, the tier is kept. Everything else is returned unchanged.
        /// </summary>
        public static int? TeleportVariation(Position from, Position destination)
        {
            if (destination == null)
                return null;
            if (destination.Variation.HasValue || from == null)
                return destination.Variation;
            var d = FindCopyAt(from.LandblockId.Landblock, from.Variation);
            if (d == null || !d.Contains(destination.LandblockId.Landblock))
                return destination.Variation;
            return from.Variation;
        }

        /// <summary>
        /// Applies <see cref="TeleportVariation"/> to a destination in place. A no-op (true) for every teleport that does not
        /// start inside a tier copy and end inside the same dungeon.
        ///
        /// When the tier's copy can no longer be built (switch turned off or the tier removed mid-run), the kept tier would
        /// land the player in an empty landblock with no exit. Then: a destination in the dungeon's LOBBY is left at the
        /// base world, so the hourly boot or a lobby portal sends the player to the retail lobby and there is always a way
        /// out (true); any other destination is refused (false) and the caller must not teleport.
        /// </summary>
        public static bool KeepTier(Position from, Position destination)
        {
            if (destination == null || destination.Variation.HasValue)
                return true;
            var variation = TeleportVariation(from, destination);
            if (!variation.HasValue)
                return true;

            var landblock = destination.LandblockId.Landblock;
            if (FindTierCopy(landblock, variation) == null)
            {
                var dungeon = FindCopyAt(landblock, variation);
                return dungeon != null && landblock == dungeon.Lobby;   // destination untouched: the retail lobby
            }

            destination.Variation = variation;
            destination.LandblockId = new LandblockId(destination.LandblockId.Raw, variation);
            return true;
        }

        // ------------------------------------------------------------------ name tags

        public static bool IsTagged(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            var at = name.IndexOf('@');
            var head = at < 0 ? name : name.Substring(0, at);
            return TagIndex(head) >= 0;
        }

        // index of a trailing "~v<digits>" in a name with no '@' part, or -1
        private static int TagIndex(string head)
        {
            var i = head.LastIndexOf(TagMark, StringComparison.Ordinal);
            if (i < 0 || i + TagMark.Length >= head.Length)
                return -1;
            for (var k = i + TagMark.Length; k < head.Length; k++)
                if (head[k] < '0' || head[k] > '9')
                    return -1;
            return i;
        }

        /// <summary>"Name@comment" -> "Name~v11@comment". Safe to apply twice. Exempt names come back unchanged.</summary>
        public static string Tag(string name, int tier, TieredDungeon dungeon = null)
        {
            if (string.IsNullOrEmpty(name) || tier < MinTier)
                return name;
            var at = name.IndexOf('@');
            var head = at < 0 ? name : name.Substring(0, at);
            if (TagIndex(head) >= 0)
                return name;
            if ((dungeon ?? Colosseum).ExemptNames.Contains(head))
                return name;
            return at < 0 ? head + TagMark + tier.ToString(CultureInfo.InvariantCulture)
                          : head + TagMark + tier.ToString(CultureInfo.InvariantCulture) + name.Substring(at);
        }

        /// <summary>"Name~v11@comment" -> "Name@comment". For reading the retail quest / event row behind a tagged name.</summary>
        public static string StripTag(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            var at = name.IndexOf('@');
            var head = at < 0 ? name : name.Substring(0, at);
            var i = TagIndex(head);
            if (i < 0)
                return name;
            return at < 0 ? head.Substring(0, i) : head.Substring(0, i) + name.Substring(at);
        }

        // ------------------------------------------------------------------ tier scope

        // The tier of the object acting on THIS thread right now (an NPC running an emote, a portal being used, a
        // generator checking its event), 0 outside a tier copy. Set per call from the acting object's own location and
        // always restored - never left set across calls. Same pattern as ZoneStatResolver.ScopeDropFloor.
        [ThreadStatic] private static int scopeTier;
        [ThreadStatic] private static TieredDungeon scopeDungeon;

        public static int ScopeTier => scopeTier;

        public readonly struct Scope : IDisposable
        {
            private readonly int prevTier;
            private readonly TieredDungeon prevDungeon;
            private readonly bool entered;

            internal Scope(int tier, TieredDungeon dungeon)
            {
                prevTier = scopeTier;
                prevDungeon = scopeDungeon;
                scopeTier = tier;
                scopeDungeon = dungeon;
                entered = true;
            }

            public void Dispose()
            {
                if (!entered)
                    return;
                scopeTier = prevTier;
                scopeDungeon = prevDungeon;
            }
        }

        /// <summary>Enter the tier scope of an acting object. Outside a tier copy this CLEARS the scope for the call, so a
        /// retail object acting inside another object's scope (nested emotes) can never inherit a tag.</summary>
        public static Scope EnterScope(WorldObject actor)
        {
            var loc = actor?.Location;
            var d = loc != null ? FindCopyAt(loc.LandblockId.Landblock, loc.Variation) : null;
            return new Scope(d != null ? loc.Variation.Value : 0, d);
        }

        /// <summary>For tests: enter a scope without a world object.</summary>
        internal static Scope EnterScopeForTest(int tier) => new Scope(tier, tier >= MinTier ? Colosseum : null);

        /// <summary>Tags a quest / event name with the current scope's tier. Unchanged outside a scope.</summary>
        public static string ApplyScope(string name)
            => scopeTier >= MinTier ? Tag(name, scopeTier, scopeDungeon) : name;

        // ------------------------------------------------------------------ tier copy rows

        /// <summary>
        /// The spawn rows of a tier copy, from the retail rows of the same landblock: lobby rows off the keep list are
        /// dropped (with their link children), swaps applied, the extra rows added, and every row cloned under a fresh
        /// guid so a tier copy and the live retail landblock never share one. The cached retail list is not modified.
        /// </summary>
        public static List<LandblockInstance> BuildTierRows(TieredDungeon dungeon, ushort landblock, List<LandblockInstance> baseRows, Func<uint> newGuid)
        {
            var kept = new List<LandblockInstance>(baseRows.Count);
            if (landblock == dungeon.Lobby)
            {
                var keepGuids = new HashSet<uint>();
                foreach (var row in baseRows)
                    if (dungeon.LobbyKeepWcids.Contains(row.WeenieClassId))
                        keepGuids.Add(row.Guid);
                foreach (var row in baseRows)
                    if (keepGuids.Contains(row.Guid))
                        kept.Add(row);
            }
            else
                kept.AddRange(baseRows);

            var guidMap = new Dictionary<uint, uint>(kept.Count);
            foreach (var row in kept)
                guidMap[row.Guid] = newGuid();

            var clones = new List<LandblockInstance>(kept.Count + dungeon.LobbyExtraRows.Count);
            foreach (var row in kept)
            {
                var clone = CloneRow(row, guidMap[row.Guid], dungeon.WcidSwaps.TryGetValue(row.WeenieClassId, out var swap) ? swap : row.WeenieClassId);
                foreach (var link in row.LandblockInstanceLink)
                {
                    // a link to a row that was not kept is dropped with it
                    if (!guidMap.TryGetValue(link.ParentGuid, out var pg) || !guidMap.TryGetValue(link.ChildGuid, out var cg))
                        continue;
                    clone.LandblockInstanceLink.Add(new LandblockInstanceLink { ParentGuid = pg, ChildGuid = cg });
                }
                clones.Add(clone);
            }

            if (landblock == dungeon.Lobby)
                foreach (var extra in dungeon.LobbyExtraRows)
                {
                    var clone = CloneRow(extra, newGuid(), extra.WeenieClassId);
                    clone.Landblock = landblock;
                    clones.Add(clone);
                }

            return clones;
        }

        private static LandblockInstance CloneRow(LandblockInstance row, uint guid, uint wcid) => new LandblockInstance
        {
            Guid = guid,
            Landblock = row.Landblock,
            WeenieClassId = wcid,
            ObjCellId = row.ObjCellId,
            OriginX = row.OriginX, OriginY = row.OriginY, OriginZ = row.OriginZ,
            AnglesW = row.AnglesW, AnglesX = row.AnglesX, AnglesY = row.AnglesY, AnglesZ = row.AnglesZ,
            IsLinkChild = row.IsLinkChild,
            VariationId = row.VariationId,
        };

        // ------------------------------------------------------------------ player tier + pay

        /// <summary>The highest tier whose T11+ hit gate this player passes (TierHitGate reads the same three counts), 10 below T11.</summary>
        public static int PlayerTier(Player player)
        {
            if (player == null)
                return MinTier - 1;
            var creature = player.EffectiveCreatureAugCount;
            var item = player.EffectiveItemAugCount;
            var triune = player.GetProperty(PropertyInt64.TriuneWeaveCount) ?? 0;
            var tier = MinTier - 1;
            for (var t = MinTier; t <= MaxTier; t++)
            {
                var row = WeaponScalingManager.GetTier(t);
                if (row == null || creature < row.MinWieldCreature || item < row.MinWieldAugs || triune < row.MinWieldTriune)
                    break;
                tier = t;
            }
            return tier;
        }

        /// <summary>The room a coordinator stands in, from its Y (rooms are RoomSpacingY apart), or 0 when out of range.</summary>
        public static int RoomAt(TieredDungeon dungeon, float y)
        {
            var room = 1 + (int)Math.Round((y - dungeon.Room1Y) / dungeon.RoomSpacingY);
            return room >= 1 && room <= dungeon.RoomCount ? room : 0;
        }

        /// <summary>The last room N for which every room FirstRoom..N is credited (bit N-1 = room N), or 0.</summary>
        public static int FurthestRoom(int creditBits, int firstRoom, int roomCount)
        {
            var furthest = 0;
            for (var room = firstRoom; room <= roomCount; room++)
            {
                if ((creditBits & (1 << (room - 1))) == 0)
                    break;
                furthest = room;
            }
            return furthest;
        }

        /// <summary>"13:1,14:1,15:2,16:3,17:4,18:5" -> coins for the furthest room finished (0 when not listed).</summary>
        public static int CoinsForRoom(string table, int room)
        {
            if (string.IsNullOrWhiteSpace(table) || room <= 0)
                return 0;
            foreach (var token in table.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = token.IndexOf(':');
                if (colon > 0
                    && int.TryParse(token.AsSpan(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                    && int.TryParse(token.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var coins)
                    && r == room)
                    return Math.Clamp(coins, 0, 1000);
            }
            return 0;
        }

        /// <summary>
        /// Percent of full pay. Each player gets the LOWER of two rates: their own (how far the copy is below their tier)
        /// and the rate of the highest-tier player credited in the run, counted one tier kinder. So friends one tier apart
        /// are not punished, and a strong helper is never the easy way to full pay.
        /// <paramref name="percents"/> is "100,60,20": at tier, one below, two below; further below pays nothing.
        /// </summary>
        public static int PayPercent(string percents, int copyTier, int playerTier, int topTier)
        {
            var own = PercentForGap(percents, playerTier - copyTier);
            var top = PercentForGap(percents, Math.Max(0, topTier - copyTier - 1));
            return Math.Min(own, top);
        }

        private static int PercentForGap(string percents, int gap)
        {
            if (gap < 0)
                return 0;   // a player below the copy's tier cannot hit anything in it
            var parts = (percents ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (gap >= parts.Length)
                return 0;
            return int.TryParse(parts[gap], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct) ? Math.Clamp(pct, 0, 100) : 0;
        }

        /// <summary>Coins after the percent, rounded down.</summary>
        public static int ScaleCoins(int coins, int percent) => (int)((long)coins * percent / 100);

        /// <summary>
        /// The pay table is written in T11 coins; each tier above pays <paramref name="tierStep"/> times the tier below
        /// (owner 2026-11-11: the same step as Prestige Coins per kill, zc_pcoin_kill_tier_step). Rounded to the nearest
        /// coin, halves up. A step that is not a positive number reads as 1 (every tier pays the table).
        /// </summary>
        public static int ScaleForTier(int coins, int tier, double tierStep)
        {
            if (coins <= 0)
                return 0;
            if (!double.IsFinite(tierStep) || tierStep <= 0)
                tierStep = 1.0;
            var value = coins * Math.Pow(tierStep, Math.Max(0, tier - MinTier));
            return (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 100_000);
        }

        // ------------------------------------------------------------------ room ramp (owner 2026-11-11)

        /// <summary>
        /// Rooms get harder as a run goes on: x1 in the first room of a run, rising in a straight line to
        /// <paramref name="lastRoomMult"/> in the last room. The multipliers sit ON TOP of the copy's own tier stats - never a
        /// blend with the next tier, whose stats are tuned for that tier's gear and do not step evenly (review 2026-11-11:
        /// T15 -> T16 would make room 18 easier for melee and 3.5x harder for void). Rooms before the first room of a run
        /// stay x1. A value outside 0.1-10 reads as x1.
        /// </summary>
        public static double RoomRampMult(TieredDungeon dungeon, int room, double lastRoomMult)
        {
            if (dungeon == null || room <= dungeon.FirstRoom || !double.IsFinite(lastRoomMult) || lastRoomMult < 0.1 || lastRoomMult > 10)
                return 1.0;
            var span = dungeon.RoomCount - dungeon.FirstRoom;
            if (span <= 0)
                return 1.0;
            var t = Math.Min(1.0, (double)(room - dungeon.FirstRoom) / span);
            return 1.0 + (lastRoomMult - 1.0) * t;
        }

        /// <summary>
        /// The arena room of a tier copy a monster belongs to (0 when none). Fixed the first time it is asked - at spawn,
        /// from the spawn position - and kept on the creature, so a monster that follows a player into the next room keeps
        /// its own room's numbers and its health and damage always agree. Only arena landblocks have rooms; the lobby has none.
        /// </summary>
        public static int RoomOf(Creature creature)
        {
            if (creature == null || creature is Player)
                return 0;
            if (creature.TieredDungeonRoom != 0)
                return Math.Max(0, creature.TieredDungeonRoom);

            var pos = creature.Location;
            if (pos == null)
                return 0;   // not placed yet: ask again later
            var dungeon = FindCopyAt(pos.LandblockId.Landblock, pos.Variation);
            var room = dungeon != null && dungeon.Arenas.Contains(pos.LandblockId.Landblock) ? RoomAt(dungeon, pos.PositionY) : 0;
            creature.TieredDungeonRoom = room > 0 ? room : -1;
            return room;
        }

        /// <summary>The health multiplier for this monster's room (tiered_dungeon_room_health_ramp), x1 outside a tier copy.</summary>
        public static double RoomHealthMult(Creature creature) => RoomMult(creature, ServerConfig.tiered_dungeon_room_health_ramp.Value);

        /// <summary>The damage multiplier for this monster's room (tiered_dungeon_room_damage_ramp), x1 outside a tier copy.
        /// Read per hit: a cached room and one dictionary lookup.</summary>
        public static double RoomDamageMult(Creature creature) => RoomMult(creature, ServerConfig.tiered_dungeon_room_damage_ramp.Value);

        private static double RoomMult(Creature creature, double lastRoomMult)
        {
            var room = RoomOf(creature);
            if (room <= 0)
                return 1.0;
            var pos = creature.Location;
            var dungeon = pos == null ? null : FindCopyAt(pos.LandblockId.Landblock, pos.Variation);
            return RoomRampMult(dungeon, room, lastRoomMult);
        }

        // ------------------------------------------------------------------ room participation (owner 2026-11-11)

        /// <summary>How many of a room's <paramref name="kills"/> a player must have damaged to be credited with it
        /// (tiered_dungeon_min_room_tag_share, 0.25 = a quarter of the room's monsters). 0 when the share is off.</summary>
        public static int RequiredTags(int kills, double share)
        {
            if (kills <= 0 || !double.IsFinite(share) || share <= 0)
                return 0;
            return (int)Math.Ceiling(kills * Math.Min(1.0, share));
        }

        public static IEnumerable<TieredDungeon> All => byLandblock.Values.Distinct();
    }
}
