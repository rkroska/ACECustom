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
            { 1, new AbilityEntry(p => p.HasManaBarrier, (p, v) => p.HasManaBarrier = v, "Mana Barrier") },
            { 2, new AbilityEntry(p => p.HasHeavySwing,   (p, v) => p.HasHeavySwing = v,   "Heavy Swing") },
            { 3, new AbilityEntry(p => p.HasHeavyDraw,    (p, v) => p.HasHeavyDraw = v,    "Heavy Draw")  },
            { 4, new AbilityEntry(p => p.HasFocusedCasting, (p, v) => p.HasFocusedCasting = v, "Focused Casting") },
            { 5, new AbilityEntry(p => p.HasChaining,       (p, v) => p.HasChaining = v,       "Chaining")        },
            { 6, new AbilityEntry(p => p.HasRepeater,       (p, v) => p.HasRepeater = v,       "Repeater")        },
            { 7, new AbilityEntry(p => p.HasBloodLetting,   (p, v) => p.HasBloodLetting = v,   "Blood Letting")   },
        };

        private static readonly Dictionary<uint, int> WCIDToAbilityId = new()
        {
            // Mana Barrier
            { 777700001, 1 }, { 777700054, 1 }, { 777710004, 1 }, { 777720004, 1 },
            // Heavy Swing
            { 777700005, 2 }, { 777700055, 2 }, { 777710005, 2 }, { 777720005, 2 },
            // Heavy Draw
            { 777700006, 3 }, { 777700056, 3 }, { 777710006, 3 }, { 777720006, 3 },
            // Focused Casting
            { 777700007, 4 }, { 777700057, 4 }, { 777710007, 4 }, { 777720007, 4 },
            // Chaining
            { 777700002, 5 }, { 777710002, 5 }, { 777720002, 5 },
            // Repeater
            { 777700003, 6 }, { 777710003, 6 }, { 777720003, 6 },
            // Blood Letting
            { 777700008, 7 }, { 777700058, 7 }, { 777710008, 7 }, { 777720008, 7 },
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
