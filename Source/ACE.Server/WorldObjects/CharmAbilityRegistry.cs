using System;
using System.Collections.Generic;

namespace ACE.Server.WorldObjects
{
    public static class CharmAbilityRegistry
    {
        private readonly struct AbilityEntry
        {
            public readonly Func<Player, bool> Get;
            public readonly Action<Player, bool> Set;
            public readonly string DisplayName;

            public AbilityEntry(Func<Player, bool> get, Action<Player, bool> set, string displayName)
            {
                Get = get;
                Set = set;
                DisplayName = displayName;
            }
        }

        private static readonly Dictionary<int, AbilityEntry> Registry = new()
        {
            // Format: { abilityId, (getter, setter, "Display Name") }
            { 1,  new AbilityEntry(p => p.HasManaBarrier,      (p, v) => p.HasManaBarrier      = v, "Mana Barrier")     },
            { 16, new AbilityEntry(p => p.HasInfiniteCasting,  (p, v) => p.HasInfiniteCasting  = v, "Infinite Casting") },
        };

        /// <summary>Ability ID for the Infinite Casting Stone charm. Use this constant instead of the magic number 16.</summary>
        public const int InfiniteCastingAbilityId = 16;

        private static readonly Dictionary<uint, int> WCIDToAbilityId = new()
        {
            // Mana Barrier
            { 777700001, 1 }, { 777700054, 1 }, { 777710004, 1 }, { 777720004, 1 },
            // Infinite Casting Stone
            { 777700019, 16 },
        };

        /// <summary>
        /// Returns the ability ID for a given WCID, or null if it's not a charm.
        /// </summary>
        public static int? GetAbilityIdForWCID(uint wcid)
        {
            return WCIDToAbilityId.TryGetValue(wcid, out var abilityId) ? abilityId : null;
        }

        /// <summary>
        /// Apply or remove an ability on the player for the given ability ID.
        /// </summary>
        public static void Apply(Player player, int abilityId, bool enable)
        {
            if (Registry.TryGetValue(abilityId, out var entry))
                entry.Set(player, enable);
        }

        /// <summary>
        /// Returns the display name for an ability ID, or null if not found.
        /// </summary>
        public static string GetDisplayName(int abilityId)
        {
            return Registry.TryGetValue(abilityId, out var entry) ? entry.DisplayName : null;
        }

        /// <summary>
        /// Returns true if the player currently has this ability active.
        /// </summary>
        public static bool IsActive(Player player, int abilityId)
        {
            return Registry.TryGetValue(abilityId, out var entry) && entry.Get(player);
        }

        /// <summary>
        /// Returns a list of display names for all currently active charms on the player.
        /// </summary>
        public static List<string> GetActiveDisplayNames(Player player)
        {
            var active = new List<string>();
            foreach (var entry in Registry.Values)
            {
                if (entry.Get(player))
                    active.Add(entry.DisplayName);
            }
            return active;
        }
    }
}
