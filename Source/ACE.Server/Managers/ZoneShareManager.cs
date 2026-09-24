using System.Collections.Generic;

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
    ///   - a Zone Control zone with ControlledArea.ZoneShare on (only while the zone is Enabled and the Zone Control master
    ///     switch is on) - keyed "zone:NAME";
    ///   - a Room Assign dungeon whose source weenie carries PropertyBool.RoomAssignZoneShare - keyed "dungeon:WCID". The
    ///     dungeon is checked first: it is the smaller, more specific area.
    /// Admins are left out unless the Room Assign Count Admin switch is on, the same rule the rooms use.
    ///
    /// Membership is worked out at the moment of the kill from PlayerManager.GetAllOnline() - the same way Room Assign counts
    /// occupants - never from another landblock's player list, which is only safe on its own thread.
    /// </summary>
    public static class ZoneShareManager
    {
        /// <summary>The Zone Share area this player stands in, or null. Staff (Count Admin off) are never in one.</summary>
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
                key = "dungeon:" + source;
            else
            {
                var zone = ZoneControlManager.ResolveZoneShareZone(player);
                if (zone != null)
                    key = "zone:" + zone.ToLowerInvariant();
            }

            // The staff check reads a property, so only for a player who is actually in an area.
            if (key != null && RoomAssignManager.IsStaff(player))
                return null;

            return key;
        }

        /// <summary>
        /// Everyone who shares with this earner, the earner included - or null when the earner is not in a Zone Share area,
        /// which leaves the normal fellowship rules in charge. Every member shares in full (scalar 1.0): distance plays no
        /// part inside an area.
        /// </summary>
        public static List<(Player Member, double Scalar)> MembersFor(Player earner)
        {
            var key = AreaKeyFor(earner);
            if (key == null)
                return null;

            var members = new List<(Player Member, double Scalar)> { (earner, 1.0) };

            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player == earner)
                    continue;

                // On their way out: saved where they stand, but no longer playing there.
                if (player.IsLoggingOut && player.CurrentLandblock == null)
                    continue;

                if (AreaKeyFor(player) == key)
                    members.Add((player, 1.0));
            }

            return members;
        }

        /// <summary>How many players are in this Zone Control zone's share right now (0 when Zone Share is not active there).</summary>
        public static int CountInZone(string zoneName) => string.IsNullOrWhiteSpace(zoneName) ? 0 : Count("zone:" + zoneName.ToLowerInvariant());

        /// <summary>How many players are in this dungeon's share right now (0 when its Zone Share is off).</summary>
        public static int CountInDungeon(uint sourceWcid) => sourceWcid == 0 ? 0 : Count("dungeon:" + sourceWcid);

        private static int Count(string key)
        {
            var count = 0;
            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player.IsLoggingOut && player.CurrentLandblock == null)
                    continue;

                if (AreaKeyFor(player) == key)
                    count++;
            }
            return count;
        }
    }
}
