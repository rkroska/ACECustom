using System;
using System.Collections.Generic;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Result of attempting to apply an ability charm bonus.
    /// Used by all ability charm types (e.g. Heavy Swing, Focused Casting).
    /// </summary>
    public struct AbilityCharmResult
    {
        public bool Applied;            // true = bonus was applied this hit
        public uint StaminaSpent;
        public uint ManaSpent;
        public float DamageMultiplier;  // 1.0 = no bonus applied
    }

    public partial class Player
    {
        // ── Runtime State ─────────────────────────────────────────────────────────
        // Tracking active charm levels per ability ID. Populated on login and toggles.
        public Dictionary<int, int> ActiveCharmLevels { get; } = new();

        // ── Generic charm scaling helpers ─────────────────────────────────────────
        // Used by all tiered ability charms to scale damage multiplier and cost.

        /// <summary>
        /// Returns a damage multiplier scaled by the player's active charm tier for a specific ability.
        /// Base (level 1): baseMultiplier unchanged.
        /// Each additional level: +0.5x bonus (e.g. 2.0 → 2.5 → 3.0).
        /// </summary>
        public float GetCharmDamageMultiplier(int abilityId, float baseMultiplier)
        {
            if (!ActiveCharmLevels.TryGetValue(abilityId, out var level) || level <= 1)
                return baseMultiplier;

            return baseMultiplier + (level - 1) * 0.5f;
        }

        /// <summary>
        /// Returns a vital cost percentage scaled by the player's active charm tier for a specific ability.
        /// Base (level 1): baseCost unchanged.
        /// Each additional level: –2% (floored at 2%).
        /// </summary>
        public float GetCharmCostPct(int abilityId, float baseCost)
        {
            if (!ActiveCharmLevels.TryGetValue(abilityId, out var level) || level <= 1)
                return baseCost;

            return Math.Max(0.02f, baseCost - (level - 1) * 0.02f);
        }

        /// <summary>
        /// On login: ensures no ability is active without a corresponding activated charm in inventory.
        /// Silently clears orphaned abilities (e.g., charm lost or removed while offline).
        /// Performs a deep recursive scan so charms inside nested containers are detected.
        /// </summary>
        public void ValidateAbilityCharms()
        {
            ActiveCharmLevels.Clear();
            var activeCharmAbilities = new Dictionary<int, int>();

            foreach (var item in GetAllPossessionsDeep())
            {
                if (item.IsAbilityCharm && item.IsCharmActivated && item.CharmGrantsAbility.HasValue)
                {
                    var id = item.CharmGrantsAbility.Value;
                    var level = item.CharmLevel ?? 1;
                    
                    // If multiple charms are active for the same ability (shouldn't happen with guards),
                    // the highest level wins for the runtime map.
                    if (!activeCharmAbilities.TryGetValue(id, out var existingLevel) || level > existingLevel)
                        activeCharmAbilities[id] = level;
                }
            }

            foreach (var id in CharmAbilityRegistry.RegisteredIds)
            {
                if (activeCharmAbilities.TryGetValue(id, out var level))
                {
                    if (!CharmAbilityRegistry.IsActive(this, id))
                        CharmAbilityRegistry.Apply(this, id, true, level);
                    else
                        ActiveCharmLevels[id] = level; // Sync level if already active
                }
                else if (CharmAbilityRegistry.IsActive(this, id))
                {
                    CharmAbilityRegistry.Apply(this, id, false);
                }
            }
        }

        /// <summary>
        /// Recursively searches for a charm granting the specified ability within a world object.
        /// If the object is a container, it scans all contents.
        /// </summary>
        public static WorldObject FindCharmInObject(WorldObject obj, int abilityId)
        {
            if (obj.IsAbilityCharm && obj.CharmGrantsAbility == abilityId)
                return obj;

            if (obj is Container container)
            {
                foreach (var child in container.Inventory.Values)
                {
                    var found = FindCharmInObject(child, abilityId);
                    if (found != null) return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all items in the player's possession, recursively descending into nested containers.
        /// Unlike <see cref="GetAllPossessions"/>, this includes items inside bags-within-bags.
        /// </summary>
        public IEnumerable<WorldObject> GetAllPossessionsDeep()
        {
            foreach (var item in GetAllPossessions())
            {
                yield return item;

                if (item is Container bag)
                {
                    foreach (var nested in GetContainerContentsDeep(bag))
                        yield return nested;
                }
            }
        }

        private static IEnumerable<WorldObject> GetContainerContentsDeep(Container container)
        {
            foreach (var child in container.Inventory.Values)
            {
                yield return child;

                if (child is Container nested)
                {
                    foreach (var deeper in GetContainerContentsDeep(nested))
                        yield return deeper;
                }
            }
        }
    }
}
