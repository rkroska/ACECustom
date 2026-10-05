using System;
using System.Collections.Generic;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// /vault (owner 2026-09-27): who else is in the Vaulted Dungeon the player stands in. Read-only.
    /// </summary>
    public static partial class RoomAssignManager
    {
        /// <summary>At most this many names in a /vault reply; the rest are "...and N more." (owner 2026-09-27: 50).</summary>
        internal const int MaxRosterNames = 50;

        /// <summary>
        /// The Vaulted Dungeon <paramref name="player"/> stands in - a chamber, a hall, any indoor cell of it, in a layer it is
        /// placed at - and the names of everyone else in it, sorted. False when the player is not in one. Left out: the player
        /// themself, anyone already out of the world on the way to logging off, and staff (IsLeftOutOfRoster).
        ///
        /// The caller's dungeon comes from DungeonSpotAt, which also knows a chamber whose source is not registered in this
        /// layer yet (right after a restart). Everyone else only needs InSameDungeon. A dungeon spread over two landblocks
        /// (the parked linked-dungeon plan) would need a group key here, as the switch lockout's DungeonKey does.
        ///
        /// Threading: runs on the command thread. PlayerManager.GetAllOnline takes its own lock and returns a copy, before and
        /// never inside _lock; DungeonSpotAt takes _lock itself.
        /// </summary>
        public static bool TryGetRoster(Player player, out string dungeonName, out List<string> others)
        {
            dungeonName = null;
            others = null;

            var location = player?.Location;
            if (location == null)
                return false;

            var spot = DungeonSpotAt(location);
            if (spot.Source == 0)
                return false;

            others = new List<string>();
            foreach (var other in PlayerManager.GetAllOnline())
            {
                if (other == null || other.Guid == player.Guid || IsLeftOutOfRoster(other))
                    continue;

                // Out of the world, their save still landing: gone, as BuildOccupancy counts them.
                if (other.IsLoggingOut && other.CurrentLandblock == null)
                    continue;

                if (InSameDungeon(location, other.Location))
                    others.Add(other.Name);
            }

            others.Sort(StringComparer.OrdinalIgnoreCase);

            // DungeonName's own fallback is "that Vaulted Dungeon"; this line starts with it, so it gets a capital.
            var name = DungeonName(spot.Source);
            dungeonName = char.ToUpperInvariant(name[0]) + name.Substring(1);
            return true;
        }

        /// <summary>
        /// True when <paramref name="other"/> stands in the same dungeon space as <paramref name="here"/> (a spot already known
        /// to be in a Vaulted Dungeon): the same landblock, the same layer (0 and null both base), an indoor cell. The same
        /// space SourceAt and the switch lockout's DungeonKey mean. Internal for the tests.
        /// </summary>
        internal static bool InSameDungeon(Position here, Position other)
            => here != null && other != null
                && other.Cell >> 16 == here.Cell >> 16
                && (other.Cell & 0xFFFF) >= 0x0100
                && VariationManager.NormalizeBase(other.Variation) == VariationManager.NormalizeBase(here.Variation);

        /// <summary>
        /// An access level that cannot be read (no session) counts as staff: hidden is the safe side of "leave staff out".
        /// </summary>
        private static bool IsLeftOutOfRoster(Player player)
            => IsLeftOutOfRoster(player.Session?.AccessLevel ?? AccessLevel.Admin, player.CloakStatus, player.IsPlussed);

        /// <summary>
        /// Staff never show on /vault (owner 2026-09-27: "leave admin / staff out", and "+" characters too): any
        /// Sentinel-or-higher account, anyone cloaked, and any "+" character - one made as staff keeps its "+" even on an
        /// account later demoted to Player. Deliberately NOT IsStaff, which is Admin-and-up and honours the Count Admin test
        /// switch - that decides who takes up a chamber; this decides whose name players see, and staff stay hidden whatever the
        /// test switch says. Internal for the tests.
        /// </summary>
        internal static bool IsLeftOutOfRoster(AccessLevel accessLevel, CloakStatus cloak, bool plussed)
            => accessLevel >= AccessLevel.Sentinel || cloak == CloakStatus.On || cloak == CloakStatus.Ghost || plussed;

        /// <summary>
        /// The /vault reply, one chat line each: "Chamber of Idols - 2 other players:" then "  - Name" per player, at most
        /// MaxRosterNames of them and then "  ...and N more.", or "Chamber of Idols - no other players." The count is always
        /// everyone. Internal for the tests.
        /// </summary>
        internal static List<string> RosterLines(string dungeonName, IReadOnlyList<string> others)
        {
            var lines = new List<string>();
            if (others == null || others.Count == 0)
            {
                lines.Add($"{dungeonName} - no other players.");
                return lines;
            }

            lines.Add($"{dungeonName} - {others.Count} other player{(others.Count == 1 ? "" : "s")}:");

            var shown = Math.Min(others.Count, MaxRosterNames);
            for (var i = 0; i < shown; i++)
                lines.Add("  - " + others[i]);

            if (others.Count > shown)
                lines.Add($"  ...and {others.Count - shown} more.");

            return lines;
        }
    }
}
