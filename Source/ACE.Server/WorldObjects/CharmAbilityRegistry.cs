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

        // ── Ability ID constants — use these instead of raw ints ──────────────
        public const int ManaBarrierAbilityId     = 1;
        public const int InfiniteCastingAbilityId = 16;
        public const int AsheronsFavorAbilityId   = 17;
        public const int ArtisansCharmAbilityId   = 18;
        public const int ShrapnelCharmAbilityId   = 19;
        public const int AgonyCharmAbilityId      = 20;

        private static readonly Dictionary<int, AbilityEntry> Registry = new()
        {
            // Format: { abilityId, (getter, setter, "Display Name") }
            { ManaBarrierAbilityId,     new AbilityEntry(p => p.HasManaBarrier,     (p, v) => p.HasManaBarrier     = v, "Mana Barrier")     },
            { InfiniteCastingAbilityId, new AbilityEntry(p => p.HasInfiniteCasting, (p, v) => p.HasInfiniteCasting = v, "Infinite Casting") },
            { AsheronsFavorAbilityId,   new AbilityEntry(p => p.HasAsheronsFavor,   (p, v) => p.HasAsheronsFavor   = v, "Asheron's Favor")  },
            { ArtisansCharmAbilityId,   new AbilityEntry(p => p.HasArtisanCharm,    (p, v) => p.HasArtisanCharm    = v, "Artisan's Charm")  },
            { ShrapnelCharmAbilityId,   new AbilityEntry(p => p.HasShrapnelCharm,   (p, v) => p.HasShrapnelCharm   = v, "Shrapnel Charm")   },
            { AgonyCharmAbilityId,       new AbilityEntry(p => p.HasAgonyCharm,      (p, v) => p.HasAgonyCharm      = v, "Agony Charm")      },
        };

        /// <summary>Returns all registered ability IDs. Prefer this over hardcoded numeric ranges.</summary>
        public static IEnumerable<int> RegisteredIds => Registry.Keys;

        private static readonly Dictionary<uint, int> WCIDToAbilityId = new()
        {
            // Mana Barrier
            { 777700001, ManaBarrierAbilityId }, { 777700054, ManaBarrierAbilityId }, { 777710004, ManaBarrierAbilityId }, { 777720004, ManaBarrierAbilityId },
            // Infinite Casting Stone
            { 777700019, InfiniteCastingAbilityId }, { 777700055, InfiniteCastingAbilityId },
            // Asheron's Favor (Tiers 1–3)
            { 777700020, AsheronsFavorAbilityId }, { 777710002, AsheronsFavorAbilityId }, { 777720002, AsheronsFavorAbilityId },
            // Artisan's Charm (Tiers 1–3)
            { 777700021, ArtisansCharmAbilityId }, { 777710003, ArtisansCharmAbilityId }, { 777720003, ArtisansCharmAbilityId },
            // Shrapnel Charm (Tier 1 only)
            { 777700022, ShrapnelCharmAbilityId },
            // Agony Charm (Tier 1 only)
            { 777700023, AgonyCharmAbilityId },
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
        public static void Apply(Player player, int abilityId, bool enable, int level = 1)
        {
            if (Registry.TryGetValue(abilityId, out var entry))
                entry.Set(player, enable);

            if (enable)
                player.ActiveCharmLevels[abilityId] = level;
            else
                player.ActiveCharmLevels.Remove(abilityId);
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
