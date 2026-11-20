using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Util;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// PERSONAL LIFESTONE (owner 2026-11-14). A carried item (Gem, never used up) that raises a lifestone in front of the
    /// player. The stone stands for personal_lifestone_lifetime_seconds (2 min), then rots away; it is never saved to the
    /// shard. Only the player who raised it may attune to it (double-click) or tie to it (Lifestone Tie) - see
    /// Lifestone.ActOnUse and WorldObject_Magic.HandleCastSpell_PortalLink. Attune and tie store a POSITION, so both stay
    /// after the stone fades - that is the point of the item, and why the blocked-area rules below matter.
    ///
    /// Cooldown: PropertyInt64.PersonalLifestoneNextUse on the character, unix seconds, REAL time (a cooldown enchantment
    /// only ticks while online, so 20 h would mean 20 h of play). The item has no SharedCooldown, so the stock item
    /// cooldown never applies to it.
    ///
    /// Blocked areas, in order (first hit wins, the reason is what /plstone shows):
    ///   always:   a house (inside it, or the outdoor cell it stands in), a Vaulted Dungeon (Room Assign dungeon).
    ///   a '-' token in personal_lifestone_excluded_landblocks for this landblock/variation ALLOWS the spot past
    ///             everything below this line.
    ///   a '+' token (or bare) blocks; the built-in list blocks (jail 00C0, PK arena 0066, Marketplace, apartments - every
    ///   variation); Thaelaryn Island (T10) + the Valley of Death, every variation, when personal_lifestone_block_t10_vod;
    ///   a no-log area blocks when personal_lifestone_block_nolog_areas; any indoor cell blocks when
    ///   personal_lifestone_block_dungeons.
    /// </summary>
    public partial class Player
    {
        /// <summary>The carried item (Gem). 777740001 - owner's 7777 item block, free on TEST 2026-11-14.</summary>
        public const uint PersonalLifestoneItemWcid = 777740001;

        /// <summary>The stone it raises (WeenieType LifeStone). 777740002.</summary>
        public const uint PersonalLifestoneStoneWcid = 777740002;

        /// <summary>How far in front of the player the stone is placed, in metres. Tried in order; the first spot that is
        /// inside a real cell wins (indoors a spot in a wall has no cell).</summary>
        private static readonly double[] PersonalLifestoneSpawnDistances = { 2.0, 1.5 };

        /// <summary>Built-in blocked landblocks - every variation. A '-' token in the excluded list lifts one.</summary>
        private static readonly Dictionary<ushort, string> PersonalLifestoneBuiltInBlocks = BuildPersonalLifestoneBuiltIns();

        private static Dictionary<ushort, string> BuildPersonalLifestoneBuiltIns()
        {
            var d = new Dictionary<ushort, string>
            {
                { 0x00C0, "the jail" },
                { 0x0066, "the PK arena" },
            };
            foreach (var lb in LandblockCollections.MarketplaceLandblocks)
                d[lb] = "the Marketplace";
            foreach (var kv in LandblockCollections.ApartmentBlocks)
                d[kv.Key] = "an apartment block";
            return d;
        }

        /// <summary>Owner guid -> the stone they have standing (if any). Survives the owner relogging; a destroyed stone is
        /// simply skipped when read. One entry per character at most.</summary>
        private static readonly ConcurrentDictionary<uint, Lifestone> ActivePersonalLifestones = new();

        // ---- excluded-list overrides (same token grammar and caching as nolog_landblocks) ------------------------

        private sealed class PlOverrides
        {
            public readonly string Raw;
            public readonly Dictionary<ushort, HashSet<int>> Adds;
            public readonly Dictionary<ushort, HashSet<int>> Removes;
            public PlOverrides(string raw, Dictionary<ushort, HashSet<int>> adds, Dictionary<ushort, HashSet<int>> removes)
            { Raw = raw; Adds = adds; Removes = removes; }
        }

        private static readonly object PlParseLock = new();
        private static volatile PlOverrides _plOverrides = new(null, new(), new());

        private static PlOverrides GetPersonalLifestoneOverrides()
        {
            var raw = ServerConfig.personal_lifestone_excluded_landblocks?.Value ?? "";
            var current = _plOverrides;
            if (current.Raw == raw)
                return current;

            lock (PlParseLock)
            {
                if (_plOverrides.Raw == raw)
                    return _plOverrides;
                _plOverrides = ParseOverrides(raw);
                return _plOverrides;
            }
        }

        private static PlOverrides ParseOverrides(string raw)
        {
            ParseLandblockTokens(raw, out var adds, out var removes);
            return new PlOverrides(raw, adds, removes);
        }

        /// <summary>The built-in list, for /plstone.</summary>
        public static IReadOnlyDictionary<ushort, string> PersonalLifestoneBuiltIns => PersonalLifestoneBuiltInBlocks;

        /// <summary>
        /// A house, by the game's own rule (Player.CheckHouseRestrictions): HouseCell.HouseCells is keyed by the OUTDOOR cell a
        /// house stands in, so a spot inside a house (an indoor cell of an outdoor landblock) is looked up by the outdoor cell
        /// under it. Dungeon cells keep their own id. The outdoor cell itself (the yard around the house) counts too.
        /// </summary>
        private static bool IsInHouseCell(Position pos)
        {
            if (HouseCell.HouseCells.ContainsKey(pos.Cell))
                return true;
            if (!pos.Indoors || IsDungeonLandblock((ushort)pos.Landblock))
                return false;
            return HouseCell.HouseCells.ContainsKey(pos.GetOutdoorCell());
        }

        private static readonly ConcurrentDictionary<ushort, bool> DungeonLandblockCache = new();

        /// <summary>
        /// The physics Landblock.IsDungeon rule (all terrain heights 0, at least one env cell, no buildings, the NW island
        /// excepted) read from the cell dat - so it never loads a physics landblock just to answer a question (/plstone audit
        /// checks every flagged character). Cached per landblock.
        /// </summary>
        public static bool IsDungeonLandblock(ushort landblock)
            => DungeonLandblockCache.GetOrAdd(landblock, lb =>
            {
                try
                {
                    var x = lb >> 8; var y = lb & 0xFF;
                    if (x * 8 < 64 && y * 8 > 1976)
                        return false;   // the physics rule's NW island exception
                    var cl = DatManager.CellDat.ReadFromDat<CellLandblock>(((uint)lb << 16) | 0xFFFF);
                    if (cl?.Height == null || cl.Height.Any(h => h != 0))
                        return false;
                    var info = DatManager.CellDat.ReadFromDat<LandblockInfo>(((uint)lb << 16) | 0xFFFE);
                    return info != null && info.NumCells > 0 && info.Buildings != null && info.Buildings.Count == 0;
                }
                catch
                {
                    return false;
                }
            });

        /// <summary>
        /// Why the Personal Lifestone may not be used at this spot, or null when it may. The text reads after "blocked by"
        /// / "in" in /plstone; players only ever see the generic refusal.
        /// </summary>
        public static string PersonalLifestoneBlockReason(Position pos)
        {
            if (pos == null)
                return "no position";

            // always blocked - no token can lift these
            if (IsInHouseCell(pos))
                return "a house";
            if (RoomAssignManager.IsInRoomDungeon(pos))
                return "a Vaulted Dungeon";
            if (pos.Variation.HasValue && pos.Variation.Value < 0)
                return "a negative layer (never allowed)";

            var rule = LandblockRule(GetPersonalLifestoneOverrides(), (ushort)pos.Landblock, NoLogVariationKey(pos.Variation));
            if (rule.Kind == RuleKind.Blocked)
                return rule.Reason;
            if (rule.Kind == RuleKind.None && ServerConfig.personal_lifestone_block_dungeons.Value && pos.Indoors)
                return OutdoorsOnlyReason;

            return null;   // allowed - an explicit '-' token also lifts the outdoors-only switch
        }

        private const string OutdoorsOnlyReason = "an indoor cell (outdoors-only switch)";

        /// <summary>/plstone check: what the AREA rules say about this spot, and whether the item is switched on at all.</summary>
        public static string PersonalLifestoneCheckText(Position loc)
        {
            var reason = PersonalLifestoneBlockReason(loc);
            var text = $"{loc.Landblock:X4} cell {loc.Cell:X8} at {NoLogScopeName(NoLogVariationKey(loc.Variation))}: "
                + (reason == null ? "ALLOWED by the area rules." : $"BLOCKED by {reason}.");
            if (!ServerConfig.personal_lifestone_enabled.Value)
                text += " The item is switched OFF (personal_lifestone_enabled), so it works nowhere right now.";
            return text;
        }

        /// <summary>What the landblock-level rules say: nothing (None), an explicit '-' token (Allowed), or a block.</summary>
        private enum RuleKind { None, Allowed, Blocked }
        /// <param name="Switchable">Blocked only by a rule that can be switched off (the T10 / Valley of Death switch, a no-log
        /// area) - an admin's 'add' still writes its own entry there, so the block outlives the switch.</param>
        private readonly record struct RuleResult(RuleKind Kind, string Reason, bool Switchable = false);

        /// <summary>
        /// The landblock-level rules for one scope key (a variation, NoLogBaseScopeKey or NoLogAllScopeKey). Tokens first, the
        /// MOST SPECIFIC scope winning (this variation / base before 'all'; at one scope a '-' beats a '+'), then the built-in
        /// list, the T10 + Valley of Death switch and the no-log areas.
        /// </summary>
        /// <param name="anyLayer">Scope 'all' only: true = blocked if a no-log entry hits ANY layer (what 'remove' must lift);
        /// false = only if EVERY layer is no-log (what 'add' may skip). Other scopes ignore it.</param>
        private static RuleResult LandblockRule(PlOverrides o, ushort landblock, int scopeKey, bool anyLayer = false)
        {
            var scopes = scopeKey == NoLogAllScopeKey ? AllScopeOnly : new[] { scopeKey, NoLogAllScopeKey };
            foreach (var scope in scopes)
            {
                if (o.Removes.TryGetValue(landblock, out var removes) && removes.Contains(scope))
                    return new RuleResult(RuleKind.Allowed, null);
                if (o.Adds.TryGetValue(landblock, out var adds) && adds.Contains(scope))
                    return new RuleResult(RuleKind.Blocked, "the excluded list");
            }
            if (PersonalLifestoneBuiltInBlocks.TryGetValue(landblock, out var builtIn))
                return new RuleResult(RuleKind.Blocked, builtIn + " (built-in list)");
            if (ServerConfig.personal_lifestone_block_t10_vod.Value)
            {
                if (LandblockCollections.ThaelarynIslandLandblocks.Contains(landblock))
                    return new RuleResult(RuleKind.Blocked, "Thaelaryn Island (T10 switch)", true);
                if (LandblockCollections.ValleyOfDeathLandblocks.Contains(landblock))
                    return new RuleResult(RuleKind.Blocked, "the Valley of Death (T10 switch)", true);
            }
            if (ServerConfig.personal_lifestone_block_nolog_areas.Value)
            {
                // one layer: that layer's no-log list (base = the base world). 'all': see anyLayer.
                var nolog = scopeKey == NoLogAllScopeKey
                    ? (anyLayer ? IsNoLogAnyLayer(landblock) : IsNoLogEveryLayer(landblock))
                    : IsNoLogArea(landblock, scopeKey == NoLogBaseScopeKey ? null : scopeKey);   // a rift layer (-1000 and down) is its own
                if (nolog)
                    return new RuleResult(RuleKind.Blocked, "a no-log area", true);
            }
            return new RuleResult(RuleKind.None, null);
        }

        private static readonly int[] AllScopeOnly = { NoLogAllScopeKey };

        /// <summary>
        /// /plstone add / remove for a list of landblocks at one scope. The token list is parsed ONCE and kept in step as tokens
        /// are written. Per landblock: drop our tokens at that scope (an 'all' request speaks for every layer, so it drops the
        /// landblock's per-variation tokens too), ask the real rule what the scope comes out as, and write a token only when
        /// that is not already what was asked. 'remove' also lifts the outdoors-only switch (only a '-' token can). Returns
        /// the new token string; <paramref name="results"/> says, per landblock, what the rule says afterwards.
        /// </summary>
        public static string EditPersonalLifestoneTokens(string original, IEnumerable<ushort> landblocks, int scopeKey, bool adding,
            out List<(ushort Landblock, string State)> results)
        {
            var tokens = new List<string>();
            foreach (var t in (original ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (!string.IsNullOrWhiteSpace(t))
                    tokens.Add(t.Trim());
            var o = ParseOverrides(string.Join(", ", tokens));
            var outdoorsOnly = ServerConfig.personal_lifestone_block_dungeons.Value;
            var all = scopeKey == NoLogAllScopeKey;

            results = new List<(ushort, string)>();
            foreach (var lb in landblocks.Distinct())
            {
                tokens.RemoveAll(t => TryParseNoLogToken(t, out var tlb, out var tkey, out _) && tlb == lb && (all || tkey == scopeKey));
                foreach (var map in new[] { o.Adds, o.Removes })
                    if (map.TryGetValue(lb, out var set))
                    {
                        if (all) set.Clear(); else set.Remove(scopeKey);
                    }

                // 'add' may skip only when every layer is already blocked; 'remove' lifts when any layer is
                var rule = LandblockRule(o, lb, scopeKey, anyLayer: !adding);
                if (adding && (rule.Kind != RuleKind.Blocked || rule.Switchable))
                {
                    tokens.Add(NoLogTokenFor(lb, scopeKey, false));
                    AddScope(o.Adds, lb, scopeKey);
                }
                else if (!adding && (rule.Kind == RuleKind.Blocked || (rule.Kind == RuleKind.None && outdoorsOnly)))
                {
                    tokens.Add(NoLogTokenFor(lb, scopeKey, true));
                    AddScope(o.Removes, lb, scopeKey);
                }

                var after = LandblockRule(o, lb, scopeKey, anyLayer: !adding);
                var state = after.Kind == RuleKind.Blocked ? "BLOCKED (" + after.Reason + ")"
                    : after.Kind == RuleKind.None && outdoorsOnly ? "allowed outdoors; indoor cells blocked (outdoors-only switch)"
                    : "allowed";
                if (after.Kind != RuleKind.Blocked && LandblockHasHouses(lb))
                    state += " - its houses stay blocked";
                results.Add((lb, state));
            }
            return string.Join(", ", tokens);
        }

        private static readonly Lazy<HashSet<ushort>> HouseLandblocks = new(() => HouseCell.HouseCells.Keys.Select(c => (ushort)(c >> 16)).ToHashSet());

        /// <summary>True when the landblock has house cells (always blocked - no token lifts them).</summary>
        public static bool LandblockHasHouses(ushort landblock) => HouseLandblocks.Value.Contains(landblock);

        private static void AddScope(Dictionary<ushort, HashSet<int>> map, ushort landblock, int scopeKey)
        {
            if (!map.TryGetValue(landblock, out var set))
                map[landblock] = set = new HashSet<int>();
            set.Add(scopeKey);
        }

        // ---- cooldown ---------------------------------------------------------------------------------------------

        /// <summary>Seconds of real time until this character may raise another stone (0 = ready).</summary>
        public double PersonalLifestoneCooldownRemaining()
        {
            var next = GetProperty(PropertyInt64.PersonalLifestoneNextUse) ?? 0;
            var left = next - Time.GetUnixTime();
            return left > 0 ? left : 0;
        }

        /// <summary>The cooldown and lifetime are used within these ranges (PR #616): a /modifylong typo can never overflow
        /// the time math or leave a stone standing for years.</summary>
        public const long MaxPersonalLifestoneCooldownSeconds = 365L * 24 * 3600;
        public const long MaxPersonalLifestoneLifetimeSeconds = 24L * 3600;

        public static long PersonalLifestoneCooldownSeconds
            => Math.Clamp(ServerConfig.personal_lifestone_cooldown_seconds.Value, 0, MaxPersonalLifestoneCooldownSeconds);

        public static long PersonalLifestoneLifetimeSeconds
            => Math.Clamp(ServerConfig.personal_lifestone_lifetime_seconds.Value, 1, MaxPersonalLifestoneLifetimeSeconds);

        /// <summary>"13h 42m", "4m 10s", "12s". Safe for any number (NaN, negative, huge).</summary>
        public static string FormatPersonalLifestoneTime(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0)
                seconds = 0;
            seconds = Math.Min(seconds, 100.0 * 365 * 24 * 3600);   // far inside TimeSpan's range
            var t = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1)
                return $"{t.Minutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }

        /// <summary>The refusal while on cooldown, with the real time left.</summary>
        private void SendPersonalLifestoneCooldownMessage()
        {
            var left = PersonalLifestoneCooldownRemaining();
            if (left > 0)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"The stone is still gathering its strength. You can raise another lifestone in {FormatPersonalLifestoneTime(left)}.", ChatMessageType.Broadcast));
            else
                Session.Network.EnqueueSend(new GameMessageSystemChat("The stone is ready. Try again.", ChatMessageType.Broadcast));
        }

        /// <summary>/pltest: clear this character's cooldown.</summary>
        public void ResetPersonalLifestoneCooldown()
            => RemoveProperty(PropertyInt64.PersonalLifestoneNextUse);

        /// <summary>/plstone reset: clear a character's cooldown, online or offline. Run it on the world thread.</summary>
        public static void ResetPersonalLifestoneCooldown(IPlayer target)
        {
            if (target is OfflinePlayer && PlayerManager.GetOnlinePlayer(target.Guid) is Player nowOnline)
                target = nowOnline;
            target.RemoveProperty(PropertyInt64.PersonalLifestoneNextUse);
            if (target is OfflinePlayer)
                target.SaveBiotaToDatabase();
        }

        // ---- the use ----------------------------------------------------------------------------------------------

        /// <summary>The stone this player has standing right now, or null.</summary>
        public Lifestone GetActivePersonalLifestone()
        {
            if (!ActivePersonalLifestones.TryGetValue(Guid.Full, out var stone))
                return null;
            if (stone != null && !stone.IsDestroyed)
                return stone;
            ActivePersonalLifestones.TryRemove(new KeyValuePair<uint, Lifestone>(Guid.Full, stone));   // faded: drop it
            return null;
        }

        /// <summary>/plstone resetls: remove the stone this character has standing, online or not. World thread.</summary>
        public static void DestroyActivePersonalLifestone(uint ownerGuid)
        {
            if (ActivePersonalLifestones.TryRemove(ownerGuid, out var stone) && stone != null && !stone.IsDestroyed)
                stone.Destroy();
        }

        /// <summary>Drop every faded stone from ActivePersonalLifestones (run on each raise - the map stays tiny).</summary>
        private static void PrunePersonalLifestones()
        {
            foreach (var kv in ActivePersonalLifestones)
                if (kv.Value == null || kv.Value.IsDestroyed)
                    ActivePersonalLifestones.TryRemove(kv);
        }

        /// <summary>Gem.ActOnUse for PersonalLifestoneItemWcid. Busy / in-the-air are already checked there.</summary>
        public void TryUsePersonalLifestone()
        {
            if (!ServerConfig.personal_lifestone_enabled.Value)
            {
                SendTransientError("The stone is dormant. It cannot be used right now.");
                return;
            }

            if (IsOlthoiPlayer)
            {
                SendWeenieError(WeenieError.OlthoiCannotInteractWithThat);
                return;
            }

            if (PKTimerActive)
            {
                SendWeenieError(WeenieError.YouHaveBeenInPKBattleTooRecently);
                return;
            }

            if (RecallsDisabled)
            {
                SendWeenieError(WeenieError.ExitTrainingAcademyToUseCommand);
                return;
            }

            if (IsInJail())
            {
                SendTransientError("You cannot raise a lifestone while jailed.");
                return;
            }

            if (Location == null || CurrentLandblock == null)
                return;

            if (PersonalLifestoneBlockReason(Location) != null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("The stone is silent. You cannot raise a lifestone here.", ChatMessageType.Broadcast));
                return;
            }

            if (PersonalLifestoneCooldownRemaining() > 0)
            {
                SendPersonalLifestoneCooldownMessage();
                return;
            }

            var lifetime = PersonalLifestoneLifetimeSeconds;
            var now = Time.GetUnixTime();

            // only one standing stone per player (matters only after an admin cooldown reset)
            var old = GetActivePersonalLifestone();

            // each candidate spot in turn (2 m, then 1.5 m): one the world will not take (EnterWorld false) falls through to
            // the next. The old stone and the cooldown stay untouched until a stone really stands.
            Lifestone stone = null;
            foreach (var spot in FindPersonalLifestoneSpots())
            {
                if (WorldObjectFactory.CreateNewWorldObject(PersonalLifestoneStoneWcid) is not Lifestone candidate)
                {
                    log.Error($"[PersonalLifestone] weenie {PersonalLifestoneStoneWcid} is missing or is not a LifeStone - {Name} could not raise one.");
                    SendTransientError("The stone fails to answer. Please tell an admin.");
                    return;
                }

                candidate.Location = spot;
                candidate.Name = $"{Name}'s Lifestone";
                candidate.PersonalLifestoneOwner = Guid.Full;
                candidate.PersonalLifestoneExpiresAt = now + lifetime;
                candidate.SuppressShardPersistence = true;   // never saved: gone after an unload or a restart
                candidate.TimeToRot = lifetime;              // the landblock heartbeat rots it away (same as a summoned gateway)

                if (candidate.EnterWorld())
                {
                    stone = candidate;
                    break;
                }
                candidate.Destroy();
            }

            if (stone == null)
            {
                SendTransientError("There is no room in front of you for a lifestone.");
                return;
            }

            old?.Destroy();
            PrunePersonalLifestones();
            ActivePersonalLifestones[Guid.Full] = stone;

            // the cooldown is spent only now, after the stone is really standing
            var cooldown = PersonalLifestoneCooldownSeconds;
            SetProperty(PropertyInt64.PersonalLifestoneNextUse, (long)Math.Ceiling(now + cooldown));

            EnqueueBroadcast(new GameMessageSound(Guid, Sound.LifestoneOn, 1.0f));
            Session.Network.EnqueueSend(new GameMessageSystemChat($"A lifestone rises before you. Only you can attune to it or tie to it, and it fades in {FormatPersonalLifestoneTime(lifetime)}.", ChatMessageType.Magic));

            log.Info($"[PersonalLifestone] {Name} raised 0x{stone.Guid.Full:X8} at {stone.Location} (variation {stone.Location.Variation?.ToString() ?? "base"}); next use in {cooldown}s.");
        }

        /// <summary>Every spot in front of the player (nearest rule first: 2 m, then 1.5 m) that lies inside a real cell the
        /// player can walk to and is not blocked, facing the player. Empty when none.</summary>
        private IEnumerable<Position> FindPersonalLifestoneSpots()
        {
            foreach (var dist in PersonalLifestoneSpawnDistances)
            {
                var spot = Location.InFrontOf(dist, true);
                spot.SetLandblock();   // outdoors a spot past the landblock edge moves into the next one (no-op indoors)
                spot.LandblockId = new LandblockId(spot.GetCell(), spot.Variation);

                if (spot.Indoors)
                {
                    // inside a dungeon a point in a wall or the void has no cell
                    var adjust = AdjustCell.Get(spot.Landblock, spot.Variation);
                    if (adjust?.GetCell(spot.Pos) == null)
                        continue;
                }

                // the stone must stand where the owner can walk to it: the same cell, or one joined to it by a portal
                // (a doorway, a corridor join, a building door). 2 m through a thin wall lands in the next room otherwise.
                if (!IsCellReachable(Location.Cell, spot.Cell))
                    continue;

                // never across into another landblock's rules: the check above was for the player's spot
                if (PersonalLifestoneBlockReason(spot) != null)
                    continue;

                yield return spot;
            }
        }
        /// <summary>
        /// True when <paramref name="to"/> is <paramref name="from"/> or one portal step from it. Outdoor cells reach each other;
        /// an indoor cell reaches the outdoors only through a portal to outside (OtherCellId 0xFFFF). Dat geometry (base layer).
        /// </summary>
        public static bool IsCellReachable(uint from, uint to)
        {
            if (from == to)
                return true;
            bool fromIndoor = (from & 0xFFFF) >= 0x100, toIndoor = (to & 0xFFFF) >= 0x100;
            if (!fromIndoor && !toIndoor)
                return true;
            try
            {
                if (fromIndoor)
                {
                    var env = DatManager.CellDat.ReadFromDat<ACE.DatLoader.FileTypes.EnvCell>(from);
                    if (env?.CellPortals == null)
                        return false;
                    if (toIndoor)
                        return (from & 0xFFFF0000) == (to & 0xFFFF0000) && env.CellPortals.Any(p => p.OtherCellId == (ushort)(to & 0xFFFF));
                    return env.CellPortals.Any(p => p.OtherCellId == 0xFFFF);
                }
                var target = DatManager.CellDat.ReadFromDat<ACE.DatLoader.FileTypes.EnvCell>(to);
                return target?.CellPortals != null && target.CellPortals.Any(p => p.OtherCellId == 0xFFFF);
            }
            catch
            {
                return false;
            }
        }

        // ---- /plstone resetls (owner 2026-11-14): clear a player's lifestone point AND tie -------------------------------

        /// <summary>Starter towns by name -> their Free Ride spell, whose destination is the town spot a new character falls
        /// back to (the same spells PlayerFactory uses for Instantiation).</summary>
        public static readonly IReadOnlyDictionary<string, uint> StarterTownFreeRideSpells = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            { "Holtburg", 3815 },
            { "Shoushi",  3813 },
            { "Yaraq",    3814 },
            { "Sanamar",  3535 },
        };

        /// <summary>
        /// /plstone resetls arguments. The WHOLE text is tried as a name / GUID first, so a character whose name ends in a town
        /// word ("Sir Holtburg") is never split. Only when that finds nobody is a trailing town word read as the town. A last
        /// word "@town" always means the town - the way to pick a town when the full name and "name + town" both exist.
        /// </summary>
        public static bool ParseResetlsTarget(IList<string> words, out IPlayer target, out string townName, out string error)
        {
            target = null; townName = null; error = null;
            var w = words.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (w.Count == 0) { error = "Usage: /plstone resetls <player name or guid> [town | @town]"; return false; }

            if (w[^1].StartsWith("@", StringComparison.Ordinal))
            {
                var town = w[^1].Substring(1);
                if (!StarterTownFreeRideSpells.ContainsKey(town)) { error = $"Unknown town '{town}'. Use holtburg, shoushi, yaraq or sanamar."; return false; }
                w.RemoveAt(w.Count - 1);
                target = FindPlayerByNameOrGuid(string.Join(" ", w), out _);
                if (target == null) { error = $"No character named '{string.Join(" ", w)}'."; return false; }
                townName = town;
                return true;
            }

            target = FindPlayerByNameOrGuid(string.Join(" ", w), out _);
            if (target != null)
                return true;

            if (w.Count > 1 && StarterTownFreeRideSpells.ContainsKey(w[^1]))
            {
                target = FindPlayerByNameOrGuid(string.Join(" ", w.Take(w.Count - 1)), out _);
                if (target != null) { townName = w[^1]; return true; }
            }

            error = $"No character named '{string.Join(" ", w)}'.";
            return false;
        }

        /// <summary>The town spot for a starter town name, or null when the name or its spell is unknown.</summary>
        public static Position StarterTownPosition(string town)
        {
            if (town == null || !StarterTownFreeRideSpells.TryGetValue(town, out var spellId))
                return null;
            var s = ACE.Database.DatabaseManager.World.GetCachedSpell(spellId);
            if (s?.PositionObjCellId == null || s.PositionOriginX == null)
                return null;
            return new Position(s.PositionObjCellId.Value, s.PositionOriginX.Value, s.PositionOriginY ?? 0, s.PositionOriginZ ?? 0,
                s.PositionAnglesX ?? 0, s.PositionAnglesY ?? 0, s.PositionAnglesZ ?? 0, s.PositionAnglesW ?? 1, false, null);
        }

        /// <summary>
        /// Where /plstone resetls puts the death point: the named town, else the character's starter town (Instantiation),
        /// else Holtburg. Null when none of them exists - then the reset changes nothing.
        /// </summary>
        public static Position ResolveResetlsDeathPoint(Position town, Position instantiation, Func<Position> holtburg, out string text)
        {
            if (town != null) { text = $"death point set to {town.Cell:X8}"; return town; }
            if (instantiation != null) { text = $"death point set to their starter town ({instantiation.Cell:X8})"; return instantiation; }
            var h = holtburg();
            text = h != null ? $"no starter town on record - death point set to Holtburg ({h.Cell:X8})" : null;
            return h;
        }

        /// <summary>
        /// Clear a character's Lifestone Tie and SET its lifestone point to a safe spot (ResolveResetlsDeathPoint): the named
        /// town, their starter town, or Holtburg. Set, never cleared (PR #616) - a cleared point would also leave a logout in a
        /// no-log area where it is (HandleNoLogLandblock goes to Sanctuary). When no safe spot exists nothing is changed.
        /// Works online and offline (offline: the biota is changed and saved). <paramref name="changed"/> says whether it did.
        /// </summary>
        public static string ResetLifestonePoints(IPlayer target, Position town, out bool changed)
        {
            changed = false;
            // logged in since the lookup? then the online object is the one that will be saved - change that one
            if (target is OfflinePlayer && PlayerManager.GetOnlinePlayer(target.Guid) is Player nowOnline)
                target = nowOnline;

            Position instantiation;
            if (target is Player p)
                instantiation = p.Instantiation;
            else if (target is OfflinePlayer o)
                instantiation = OfflinePosition(o, PositionType.Instantiation);
            else
                instantiation = null;

            var sanctuary = ResolveResetlsDeathPoint(town, instantiation, () => StarterTownPosition("Holtburg"), out var deathText);
            if (sanctuary == null)
                return "NOTHING changed - no starter town on record and no Holtburg spot found. Name a town: /plstone resetls <player> <town>";

            DestroyActivePersonalLifestone(target.Guid.Full);   // a stone still standing would re-attune them with one click

            if (target is Player online)
            {
                online.Sanctuary = new Position(sanctuary);
                online.RemovePosition(PositionType.LinkedLifestone);
                online.RemoveProperty(PropertyString.PersonalLifestoneAttuneSpot);
                online.RemoveProperty(PropertyString.PersonalLifestoneTieSpot);
            }
            else if (target is OfflinePlayer offline)
            {
                offline.Biota.SetPosition(PositionType.Sanctuary, new Position(sanctuary), offline.BiotaDatabaseLock);
                offline.Biota.TryRemoveProperty(PositionType.LinkedLifestone, offline.BiotaDatabaseLock);
                offline.Biota.TryRemoveProperty(PropertyString.PersonalLifestoneAttuneSpot, offline.BiotaDatabaseLock);
                offline.Biota.TryRemoveProperty(PropertyString.PersonalLifestoneTieSpot, offline.BiotaDatabaseLock);
                offline.ChangesDetected = true;
                offline.SaveBiotaToDatabase();
            }
            else
                return "not a player - nothing changed";

            changed = true;
            return deathText + "; Lifestone Tie cleared";
        }
        // ---- where a personal stone put a character's death point / tie, and the audit (owner 2026-11-14) ----------------
        //
        // On a personal stone attune / tie the exact spot is written to PropertyString.PersonalLifestoneAttuneSpot / TieSpot,
        // right AFTER the position itself. Any write or removal of that position (WorldObject.SetPosition / RemovePosition -
        // a normal lifestone, @save, a quest emote, an admin command, even one at the same coordinates) clears the record
        // first, so it only ever describes the latest write. A death point or tie also counts as personal only while it
        // still matches the record (belt and braces for writes that bypass SetPosition, e.g. an offline biota edit).

        private static string SpotText(Position p)
            => string.Format(CultureInfo.InvariantCulture, "{0:X8} {1:0.000} {2:0.000} {3:0.000} {4}",
                p.Cell, p.PositionX, p.PositionY, p.PositionZ, p.Variation.HasValue ? p.Variation.Value.ToString(CultureInfo.InvariantCulture) : "-");

        /// <summary>WorldObject.SetPosition / RemovePosition: the death point or tie was just written by something - its
        /// personal-stone record no longer describes it.</summary>
        public void ClearPersonalLifestoneSpot(PositionType type)
        {
            var prop = type == PositionType.Sanctuary ? PropertyString.PersonalLifestoneAttuneSpot : PropertyString.PersonalLifestoneTieSpot;
            if (GetProperty(prop) != null)
                RemoveProperty(prop);
        }

        /// <summary>Lifestone.ActOnUse (attune) and HandleCastSpell_PortalLink (tie), personal stones only.</summary>
        public void RecordPersonalLifestoneSpot(PositionType type, Position pos)
        {
            if (pos == null)
                return;
            SetProperty(type == PositionType.Sanctuary ? PropertyString.PersonalLifestoneAttuneSpot : PropertyString.PersonalLifestoneTieSpot, SpotText(pos));
        }

        /// <summary>True when <paramref name="current"/> is still the spot a personal stone recorded.</summary>
        private static bool SpotMatches(string recorded, Position current)
        {
            if (string.IsNullOrWhiteSpace(recorded) || current == null)
                return false;
            var parts = recorded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5
                || !uint.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cell)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return false;
            int? v = parts[4] == "-" ? null : int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vv) ? vv : null;
            if (cell != current.Cell || VariationManager.NormalizeBase(v) != VariationManager.NormalizeBase(current.Variation))
                return false;
            var dx = x - current.PositionX; var dy = y - current.PositionY; var dz = z - current.PositionZ;
            return dx * dx + dy * dy + dz * dz <= 0.25f;   // within 0.5 m
        }

        /// <summary>One character's death point and tie: where they are, whether a personal stone set them, and what (if
        /// anything) blocks a personal stone there now.</summary>
        public sealed class PersonalLifestoneSpots
        {
            public string Name; public uint Guid; public bool Online;
            public Position Death; public bool DeathPersonal; public string DeathBlock;
            public Position Tie; public bool TiePersonal; public string TieBlock;

            /// <summary>A personal-stone death point or tie that sits in a spot that is blocked NOW.</summary>
            public bool Flagged => (DeathPersonal && DeathBlock != null) || (TiePersonal && TieBlock != null);
        }

        /// <summary>A position from an offline character's biota, read under its database lock.</summary>
        private static Position OfflinePosition(OfflinePlayer o, PositionType type)
            => o.Biota.GetPosition(type, o.BiotaDatabaseLock);

        public static PersonalLifestoneSpots GetPersonalLifestoneSpots(IPlayer p)
        {
            var info = new PersonalLifestoneSpots { Name = p.Name, Guid = p.Guid.Full, Online = p is Player };
            if (p is Player online)
            {
                info.Death = online.Sanctuary;
                info.Tie = online.LinkedLifestone;
            }
            else if (p is OfflinePlayer offline)
            {
                info.Death = OfflinePosition(offline, PositionType.Sanctuary);
                info.Tie = OfflinePosition(offline, PositionType.LinkedLifestone);
            }
            info.DeathPersonal = SpotMatches(p.GetProperty(PropertyString.PersonalLifestoneAttuneSpot), info.Death);
            info.TiePersonal = SpotMatches(p.GetProperty(PropertyString.PersonalLifestoneTieSpot), info.Tie);
            info.DeathBlock = info.Death != null ? PersonalLifestoneBlockReason(info.Death) : null;
            info.TieBlock = info.Tie != null ? PersonalLifestoneBlockReason(info.Tie) : null;
            return info;
        }

        /// <summary>/plstone audit: every character (online and offline) whose personal-stone death point or tie sits in a
        /// spot that is blocked now. Only characters that ever used a personal stone are looked at.</summary>
        public static List<PersonalLifestoneSpots> PersonalLifestoneAudit()
            => PersonalLifestoneAudit(PersonalLifestoneAuditSnapshot());

        /// <summary>Everyone, online and offline, from ONE read of PlayerManager's lists (its lock): a character who logs in
        /// or out while the audit runs is in it exactly once.</summary>
        public static List<IPlayer> PersonalLifestoneAuditSnapshot() => PlayerManager.FindAllPlayers(_ => true);

        /// <summary>The audit over the given characters. Offline ones (biotas read under their own locks) are safe on any
        /// thread; pass online ones only on the world thread.</summary>
        public static List<PersonalLifestoneSpots> PersonalLifestoneAudit(IEnumerable<IPlayer> who)
        {
            var list = new List<PersonalLifestoneSpots>();
            foreach (var p in who)
            {
                if (p.GetProperty(PropertyString.PersonalLifestoneAttuneSpot) == null && p.GetProperty(PropertyString.PersonalLifestoneTieSpot) == null)
                    continue;
                var info = GetPersonalLifestoneSpots(p);
                if (info.Flagged)
                    list.Add(info);
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>A character by name, or by GUID: 0x5000856D or decimal 1342211437. Null when there is none.</summary>
        public static IPlayer FindPlayerByNameOrGuid(string text, out bool isOnline)
        {
            isOnline = false;
            if (string.IsNullOrWhiteSpace(text))
                return null;
            text = text.Trim();
            uint guid;
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && uint.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out guid))
                return PlayerManager.FindByGuid(new ObjectGuid(guid), out isOnline);
            if (text.Length >= 6 && text.All(char.IsDigit) && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out guid))
                return PlayerManager.FindByGuid(new ObjectGuid(guid), out isOnline);
            return PlayerManager.FindByName(text, out isOnline);
        }
    }
}
