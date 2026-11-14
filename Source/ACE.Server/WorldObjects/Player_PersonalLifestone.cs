using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
    ///   variation); a no-log area blocks when personal_lifestone_block_nolog_areas; any indoor cell blocks when
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

                var adds = new Dictionary<ushort, HashSet<int>>();
                var removes = new Dictionary<ushort, HashSet<int>>();

                foreach (var token in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!TryParseNoLogToken(token, out var landblock, out var key, out var suppress))
                        continue;
                    var target = suppress ? removes : adds;
                    if (!target.TryGetValue(landblock, out var set))
                        target[landblock] = set = new HashSet<int>();
                    set.Add(key);
                }

                _plOverrides = new PlOverrides(raw, adds, removes);
                return _plOverrides;
            }
        }

        private static bool PlSetMatches(Dictionary<ushort, HashSet<int>> map, ushort landblock, int variationKey)
            => map.TryGetValue(landblock, out var set) && (set.Contains(NoLogAllScopeKey) || set.Contains(variationKey));

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
            if (!pos.Indoors)
                return false;
            var landblock = ACE.Server.Physics.Common.LScape.get_landblock(pos.LandblockId.Raw, pos.Variation);
            if (landblock != null && landblock.IsDungeon)
                return false;
            return HouseCell.HouseCells.ContainsKey(pos.GetOutdoorCell());
        }

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

            var landblock = (ushort)pos.Landblock;
            var key = NoLogVariationKey(pos.Variation);
            var o = GetPersonalLifestoneOverrides();

            if (PlSetMatches(o.Removes, landblock, key))
                return null;   // explicitly allowed
            if (PlSetMatches(o.Adds, landblock, key))
                return "the excluded list";
            if (PersonalLifestoneBuiltInBlocks.TryGetValue(landblock, out var builtIn))
                return builtIn + " (built-in list)";
            if (ServerConfig.personal_lifestone_block_nolog_areas.Value && IsNoLogArea(landblock, pos.Variation))
                return "a no-log area";
            if (ServerConfig.personal_lifestone_block_dungeons.Value && pos.Indoors)
                return "an indoor cell (outdoors-only switch)";

            return null;
        }

        // ---- cooldown ---------------------------------------------------------------------------------------------

        /// <summary>Seconds of real time until this character may raise another stone (0 = ready).</summary>
        public double PersonalLifestoneCooldownRemaining()
        {
            var next = GetProperty(PropertyInt64.PersonalLifestoneNextUse) ?? 0;
            var left = next - Time.GetUnixTime();
            return left > 0 ? left : 0;
        }

        /// <summary>"13h 42m", "4m 10s", "12s".</summary>
        public static string FormatPersonalLifestoneTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, seconds)));
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

        /// <summary>/plstone reset: clear the cooldown.</summary>
        public void ResetPersonalLifestoneCooldown()
            => RemoveProperty(PropertyInt64.PersonalLifestoneNextUse);

        // ---- the use ----------------------------------------------------------------------------------------------

        /// <summary>The stone this player has standing right now, or null.</summary>
        public Lifestone GetActivePersonalLifestone()
        {
            if (ActivePersonalLifestones.TryGetValue(Guid.Full, out var stone) && stone != null && !stone.IsDestroyed)
                return stone;
            return null;
        }

        /// <summary>Gem.ActOnUse for PersonalLifestoneItemWcid. Busy / in-the-air are already checked there.</summary>
        public void TryUsePersonalLifestone(WorldObject item)
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

            var spot = FindPersonalLifestoneSpot();
            if (spot == null)
            {
                SendTransientError("There is no room in front of you for a lifestone.");
                return;
            }

            if (WorldObjectFactory.CreateNewWorldObject(PersonalLifestoneStoneWcid) is not Lifestone stone)
            {
                log.Error($"[PersonalLifestone] weenie {PersonalLifestoneStoneWcid} is missing or is not a LifeStone - {Name} could not raise one.");
                SendTransientError("The stone fails to answer. Please tell an admin.");
                return;
            }

            var lifetime = Math.Max(1, ServerConfig.personal_lifestone_lifetime_seconds.Value);
            var now = Time.GetUnixTime();

            stone.Location = spot;
            stone.Name = $"{Name}'s Lifestone";
            stone.PersonalLifestoneOwner = Guid.Full;
            stone.PersonalLifestoneExpiresAt = now + lifetime;
            stone.SuppressShardPersistence = true;   // never saved: gone after an unload or a restart
            stone.TimeToRot = lifetime;              // the landblock heartbeat rots it away (same as a summoned gateway)

            // only one standing stone per player (matters only after an admin cooldown reset)
            var old = GetActivePersonalLifestone();

            if (!stone.EnterWorld())
            {
                stone.Destroy();
                SendTransientError("There is no room in front of you for a lifestone.");
                return;
            }

            old?.Destroy();
            ActivePersonalLifestones[Guid.Full] = stone;

            // the cooldown is spent only now, after the stone is really standing
            var cooldown = Math.Max(0, ServerConfig.personal_lifestone_cooldown_seconds.Value);
            SetProperty(PropertyInt64.PersonalLifestoneNextUse, (long)Math.Ceiling(now + cooldown));

            EnqueueBroadcast(new GameMessageSound(Guid, Sound.LifestoneOn, 1.0f));
            Session.Network.EnqueueSend(new GameMessageSystemChat($"A lifestone rises before you. Only you can attune to it or tie to it, and it fades in {FormatPersonalLifestoneTime(lifetime)}.", ChatMessageType.Magic));

            log.Info($"[PersonalLifestone] {Name} raised 0x{stone.Guid.Full:X8} at {spot} (variation {spot.Variation?.ToString() ?? "base"}); next use in {cooldown}s.");
        }

        /// <summary>A spot in front of the player that lies inside a real cell, facing the player; null when none.</summary>
        private Position FindPersonalLifestoneSpot()
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

                // never across into another landblock's rules: the check above was for the player's spot
                if (PersonalLifestoneBlockReason(spot) != null)
                    continue;

                return spot;
            }
            return null;
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
        /// Clear a character's Lifestone Tie, and either clear its lifestone point (death then falls back to the character's
        /// own starter town, Instantiation - Player_Death: Sanctuary ?? Instantiation ?? Location) or set the point to
        /// <paramref name="town"/>. A character with no Instantiation gets Holtburg's spot, so death never falls through to
        /// "where you died". Works online and offline (offline: the biota is changed and saved). Returns what was done.
        /// </summary>
        public static string ResetLifestonePoints(IPlayer target, Position town)
        {
            Position instantiation;
            if (target is Player p)
                instantiation = p.Instantiation;
            else if (target is OfflinePlayer o && o.Biota.PropertiesPosition != null
                     && o.Biota.PropertiesPosition.TryGetValue(PositionType.Instantiation, out var pp))
                instantiation = new Position(pp.ObjCellId, pp.PositionX, pp.PositionY, pp.PositionZ, pp.RotationX, pp.RotationY, pp.RotationZ, pp.RotationW, false, pp.VariationId);
            else
                instantiation = null;

            var sanctuary = town;
            string deathText;
            if (town != null)
                deathText = $"death point set to {town.Cell:X8}";
            else if (instantiation != null)
                deathText = $"death point cleared - they come back at their starter town ({instantiation.Cell:X8})";
            else
            {
                sanctuary = StarterTownPosition("Holtburg");
                deathText = sanctuary != null
                    ? $"no starter town on record - death point set to Holtburg ({sanctuary.Cell:X8})"
                    : "death point cleared (no starter town on record and no Holtburg spot found - they come back where they die)";
            }

            if (target is Player online)
            {
                if (sanctuary != null) online.Sanctuary = new Position(sanctuary); else online.RemovePosition(PositionType.Sanctuary);
                online.RemovePosition(PositionType.LinkedLifestone);
            }
            else if (target is OfflinePlayer offline)
            {
                if (sanctuary != null) offline.Biota.SetPosition(PositionType.Sanctuary, sanctuary, offline.BiotaDatabaseLock);
                else offline.Biota.TryRemoveProperty(PositionType.Sanctuary, offline.BiotaDatabaseLock);
                offline.Biota.TryRemoveProperty(PositionType.LinkedLifestone, offline.BiotaDatabaseLock);
                offline.ChangesDetected = true;
                offline.SaveBiotaToDatabase();
            }
            else
                return "not a player";

            return deathText + "; Lifestone Tie cleared";
        }
    }
}
