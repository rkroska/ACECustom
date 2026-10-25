using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Nearby health updates: an opt-in that lets a player receive the health of the monsters around them, not only of
    /// the one they have selected.
    ///
    /// Without it nothing changes: a creature reports its health only to the players who have it selected
    /// (Creature.OnHealthUpdate), and selecting another creature drops the previous one. A player who has switched this on
    /// with /nearbyhealth also receives the same health message for every other creature their client knows about, when
    /// that creature's health changes - limited to one message per creature every nearby_health_min_ms, with a reading of
    /// zero (a death) always sent at once. The player's selection is not touched.
    ///
    /// Not every creature in view either: each player is told about the nearest nearby_health_max_creatures only
    /// (Player.ChooseNearbyHealthSet, once a second), so a crowd of players round a crowd of monsters stays bounded.
    ///
    /// Never for Player objects: health of other players is not handed out. The switch lives in memory only - it is gone
    /// at logout and after a server restart. nearby_health_updates = false turns the whole thing off for everyone.
    ///
    /// The list of listeners is read on every health change of every creature, so it is a lock-free dictionary and the
    /// first test is "is it empty" - with nobody opted in the cost of this feature is that one read.
    /// </summary>
    public static class NearbyHealth
    {
        private static readonly ConcurrentDictionary<uint, Player> listeners = new ConcurrentDictionary<uint, Player>();

        /// <summary>At least one player has it switched on.</summary>
        public static bool Any => !listeners.IsEmpty;

        /// <summary>On for THIS player object - an entry left by an earlier login of the same character never counts
        /// (review 2026-10-20: it switched the next login on by itself).</summary>
        public static bool IsOn(Player player) => player != null && listeners.TryGetValue(player.Guid.Full, out var p) && ReferenceEquals(p, player);

        /// <summary>Switches it on or off for a player. Returns the new state. Never on while the player is logging out:
        /// the logout already took them off the list, and nothing would take them off again.</summary>
        public static bool Set(Player player, bool on)
        {
            if (player == null)
                return false;
            if (on && !player.IsLoggingOut)
            {
                listeners[player.Guid.Full] = player;
                return true;
            }
            Remove(player);
            player.NearbyHealthSet = null;
            return false;
        }

        /// <summary>The player is leaving the world (or switched it off): removes THIS player object only, so it never
        /// takes a newer login of the same character off the list.</summary>
        public static void Remove(Player player)
        {
            // WorldObject.Equals compares guids, so the object check is done here by reference first
            if (player != null && listeners.TryGetValue(player.Guid.Full, out var stored) && ReferenceEquals(stored, player))
                listeners.TryRemove(new KeyValuePair<uint, Player>(player.Guid.Full, stored));
        }

        /// <summary>The server's creature limit: 0 = no limit (the documented value), a negative value = the default 20 rather
        /// than "no limit" (review 2026-10-20), never above 1000.</summary>
        public static int MaxCreatures
        {
            get
            {
                var max = ServerConfig.nearby_health_max_creatures.Value;
                return max < 0 ? 20 : (int)Math.Min(max, 1000);
            }
        }

        /// <summary>The shortest time between two readings of one creature, from the server property (50 ms to 10 s).</summary>
        public static TimeSpan MinGap => TimeSpan.FromMilliseconds(Math.Clamp(ServerConfig.nearby_health_min_ms.Value, 50, 10000));

        /// <summary>
        /// The creatures a player is told about: the nearest <paramref name="max"/> of the candidates, nearest first.
        /// max = 0 means no limit - every candidate. Equal distances keep the order they came in.
        /// </summary>
        public static List<uint> Nearest(List<(uint Id, float Distance)> candidates, int max)
        {
            var result = new List<uint>();
            if (candidates == null || candidates.Count == 0)
                return result;

            if (max > 0 && candidates.Count > max)
            {
                // a stable sort: List.Sort is not, so the index breaks the ties
                var order = new List<(uint Id, float Distance, int Index)>(candidates.Count);
                for (var i = 0; i < candidates.Count; i++)
                    order.Add((candidates[i].Id, candidates[i].Distance, i));
                order.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Index.CompareTo(b.Index));
                for (var i = 0; i < max; i++)
                    result.Add(order[i].Id);
                return result;
            }

            foreach (var c in candidates)
                result.Add(c.Id);
            return result;
        }

        /// <summary>
        /// The rule for one health change: send it now, or hold it for the next tick? A reading of zero or less is a
        /// death and is never held - a client that waits for it to know the creature is dead must not wait.
        /// </summary>
        public static bool SendNow(double healthFraction, DateTime lastSent, DateTime now, TimeSpan minGap)
        {
            if (healthFraction <= 0.0)
                return true;
            return now - lastSent >= minGap;
        }
    }
}
