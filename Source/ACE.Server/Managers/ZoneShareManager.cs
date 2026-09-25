using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// ZONE SHARE (owner 2026-09-23): everyone standing in a Zone Share area shares kill XP, kill luminance and kill-task
    /// credit as if they were all in ONE fellowship - whatever real fellowships they are in, or none. Same share table, same
    /// fellowship_additive, same rules as a fellowship (Fellowship.ShareXpAmong / ShareLuminanceAmong); only WHO shares
    /// changes. Quest XP is not shared this way. No chat messages.
    ///
    /// Two kinds of area, one rule:
    ///   - a Zone Control zone with ControlledArea.ZoneShare on (only while the zone is Enabled, at v11+, and the Zone Control
    ///     master switch is on) - keyed "zone:NAME";
    ///   - a Room Assign dungeon whose source weenie carries PropertyBool.RoomAssignZoneShare - keyed per source AND variation
    ///     (RoomAssignManager.DungeonAreaKey), so two copies of one dungeon are two groups. The dungeon is checked first: it
    ///     is the smaller, more specific area.
    ///
    /// Who is in (review 2026-09-24, owner rulings):
    ///   - one character per ACCOUNT - an account's alts parked in the area add nothing;
    ///   - at most a real fellowship's size (Fellowship.MaxFellows): past that, the earner and the members nearest to them;
    ///   - staff are left out - admins (unless the Room Assign Count Admin test switch is on), and any Sentinel-or-higher
    ///     account or cloaked character, so an invisible GM never dilutes anyone's share.
    ///
    /// Membership is worked out from PlayerManager.GetAllOnline() - the same way Room Assign counts occupants - never from
    /// another landblock's player list, which is only safe on its own thread. One kill asks several times (XP, luminance, each
    /// kill quest), so an area's member list is kept for <see cref="MemberCacheMs"/> and reused.
    /// </summary>
    public static class ZoneShareManager
    {
        /// <summary>How long an area's member list is reused. A player crossing the border counts from the next second.</summary>
        private const long MemberCacheMs = 1000;

        /// <summary>Area key -> when its members were counted, and their GUIDS (never Player objects, which would keep logged-out
        /// characters alive). Entries older than a minute are dropped when a new one is stored.</summary>
        private static readonly ConcurrentDictionary<string, (long Stamp, List<uint> Members)> _members = new();

        /// <summary>The Zone Share area this player stands in, or null. Staff are never in one.</summary>
        public static string AreaKeyFor(Player player)
        {
            if (player == null || player.IsMule || player.IsOlthoiPlayer)
                return null;

            // Read Location once: another thread may move the player while we look.
            var location = player.Location;
            if (location == null)
                return null;

            string key = null;

            var source = RoomAssignManager.ZoneShareSourceAt(location.Cell, location.Variation);
            if (source != 0)
                key = RoomAssignManager.DungeonAreaKey(source, location.Variation);
            else if (RoomAssignManager.IsInRoomDungeon(location))
            {
                // A dungeon's own setting wins, even when it is off (owner 2026-09-24): a dungeon without Zone Share inside
                // a zone that has it does not inherit the zone's.
                return null;
            }
            else
            {
                var zone = ZoneControlManager.ResolveZoneShareZone(player);
                if (zone != null)
                    key = "zone:" + zone.ToLowerInvariant();
            }

            // The staff checks read properties, so only for a player who is actually in an area.
            if (key != null && IsLeftOut(player))
                return null;

            return key;
        }

        /// <summary>Admins (Count Admin off), any Sentinel-or-higher account, and anyone cloaked.</summary>
        private static bool IsLeftOut(Player player)
        {
            if (RoomAssignManager.IsStaff(player))
                return true;

            if (player.CloakStatus == CloakStatus.On || player.CloakStatus == CloakStatus.Ghost)
                return true;

            return !RoomAssignManager.TestAdminCounts && (player.Session?.AccessLevel ?? AccessLevel.Player) >= AccessLevel.Sentinel;
        }

        /// <summary>
        /// Everyone who shares with this earner, the earner first - or null when the earner is not in a Zone Share area,
        /// which leaves the normal fellowship rules in charge. Every member shares in full (scalar 1.0): distance plays no
        /// part inside an area, except in choosing who is in when the area holds more accounts than a fellowship can.
        /// </summary>
        public static List<(Player Member, double Scalar)> MembersFor(Player earner)
        {
            var key = AreaKeyFor(earner);
            if (key == null)
                return null;

            var accounts = new HashSet<uint> { AccountOf(earner) };
            var others = new List<Player>();

            foreach (var guid in AreaMembers(key))
            {
                if (guid == earner.Guid.Full)
                    continue;

                // The list may be up to a second old: only a character still online, and not on its way out, shares.
                var player = PlayerManager.GetOnlinePlayer(guid);
                if (player == null || (player.IsLoggingOut && player.CurrentLandblock == null))
                    continue;

                // One character per account. An account id of 0 (no account on record) never matches another.
                var account = AccountOf(player);
                if (account != 0 && !accounts.Add(account))
                    continue;

                others.Add(player);
            }

            var room = Fellowship.MaxFellows - 1;
            if (others.Count > room)
            {
                var from = earner.Location;
                others = others
                    .OrderBy(p => from != null && p.Location != null ? from.SquaredDistanceTo(p.Location) : float.MaxValue)
                    .Take(room)
                    .ToList();
            }

            var members = new List<(Player Member, double Scalar)>(others.Count + 1) { (earner, 1.0) };
            foreach (var player in others)
                members.Add((player, 1.0));

            return members;
        }

        /// <summary>How many share in this Zone Control zone right now (0 when Zone Share is not active there).</summary>
        public static int CountInZone(string zoneName)
            => string.IsNullOrWhiteSpace(zoneName) ? 0 : CountSharing("zone:" + zoneName.ToLowerInvariant());

        /// <summary>How many share in this dungeon right now, in this copy of it (0 when its Zone Share is off).</summary>
        public static int CountInDungeon(uint sourceWcid, int? variation)
            => sourceWcid == 0 ? 0 : CountSharing(RoomAssignManager.DungeonAreaKey(sourceWcid, variation));

        /// <summary>
        /// The number that really shares, as MembersFor counts it (review 2026-09-24): one per account, at most a fellowship's
        /// size - not every character standing there.
        /// </summary>
        private static int CountSharing(string key)
        {
            var accounts = new HashSet<uint>();
            var noAccount = 0;
            foreach (var guid in AreaMembers(key))
            {
                var player = PlayerManager.GetOnlinePlayer(guid);
                if (player == null || (player.IsLoggingOut && player.CurrentLandblock == null))
                    continue;

                var account = AccountOf(player);
                if (account == 0) noAccount++;
                else accounts.Add(account);
            }
            return Math.Min(accounts.Count + noAccount, Fellowship.MaxFellows);
        }

        /// <summary>Every online player in this area now, by guid - reused for <see cref="MemberCacheMs"/>. The list is never
        /// changed after it is stored.</summary>
        private static List<uint> AreaMembers(string key)
        {
            var now = Environment.TickCount64;
            if (_members.TryGetValue(key, out var cached) && now - cached.Stamp < MemberCacheMs)
                return cached.Members;

            var list = new List<uint>();
            foreach (var player in PlayerManager.GetAllOnline())
            {
                // On their way out: saved where they stand, but no longer playing there.
                if (player.IsLoggingOut && player.CurrentLandblock == null)
                    continue;

                if (AreaKeyFor(player) == key)
                    list.Add(player.Guid.Full);
            }

            _members[key] = (now, list);

            // Areas nobody has asked about for a minute (Zone Share turned off, a dungeon copy emptied) are forgotten.
            foreach (var kv in _members)
                if (now - kv.Value.Stamp > 60_000)
                    _members.TryRemove(kv.Key, out _);

            return list;
        }

        private static uint AccountOf(Player player) => player.Account?.AccountId ?? player.Session?.AccountId ?? 0;
    }
}
