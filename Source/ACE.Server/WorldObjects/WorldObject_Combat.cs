using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Entity;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        /// <summary>
        /// Determines if WorldObject can damage a target via PlayerKillerStatus
        /// </summary>
        /// <returns>null if no errors, else pk error list</returns>
        public virtual List<WeenieErrorWithString> CheckPKStatusVsTarget(WorldObject target, Spell spell)
        {
            // no restrictions here
            // player attacker restrictions handled in override
            return null;
        }

        /// <summary>
        /// Tries to proc any relevant items for the attack
        /// </summary>
        public void TryProcEquippedItems(WorldObject attacker, Creature target, bool selfTarget, WorldObject weapon)
        {
            // handle procs directly on this item -- ie. phials
            // this could also be monsters with the proc spell directly on the creature
            if (HasProc && ProcSpellSelfTargeted == selfTarget)
            {
                // projectile
                // monster
                TryProcItem(attacker, target, selfTarget);
            }

            // handle proc spells for weapon
            // this could be a melee weapon, or a missile launcher
            if (weapon != null && weapon.HasProc && weapon.ProcSpellSelfTargeted == selfTarget)
            {
                // weapon
                weapon.TryProcItem(attacker, target, selfTarget);
            }

            if (attacker != this && attacker.HasProc && attacker.ProcSpellSelfTargeted == selfTarget)
            {
                // handle special case -- missile projectiles from monsters w/ a proc directly on the mob
                // monster
                attacker.TryProcItem(attacker, target, selfTarget);
            }

            // handle aetheria procs
            if (attacker is Creature wielder)
            {
                var equippedAetheria = wielder.EquippedObjects.Values.Where(i => i.HasProc && i.ProcSpellSelfTargeted == selfTarget);

                // aetheria
                foreach (var aetheria in equippedAetheria)
                    aetheria.TryProcItem(attacker, target, selfTarget);
            }
        }

        /// <summary>
        /// Tries to proc any relevant items for the attack, with filtering for cleaved targets
        /// </summary>
        public void TryProcEquippedItemsFiltered(WorldObject attacker, Creature target, bool selfTarget, WorldObject weapon)
        {
            // For cleaved targets, we only want single-target spells, not AoE or self-targeted spells
            // handle procs directly on this item -- ie. phials
            if (HasProc && ProcSpellSelfTargeted == selfTarget && IsProcSafeForCleave())
            {
                TryProcItem(attacker, target, selfTarget);
            }

            // handle proc spells for weapon
            if (weapon != null && weapon.HasProc && weapon.ProcSpellSelfTargeted == selfTarget && weapon.IsProcSafeForCleave())
            {
                weapon.TryProcItem(attacker, target, selfTarget);
            }

            if (attacker != this && attacker.HasProc && attacker.ProcSpellSelfTargeted == selfTarget && attacker.IsProcSafeForCleave())
            {
                attacker.TryProcItem(attacker, target, selfTarget);
            }

            // handle aetheria procs
            if (attacker is Creature wielder)
            {
                var equippedAetheria = wielder.EquippedObjects.Values.Where(i => i.HasProc && i.ProcSpellSelfTargeted == selfTarget && i.IsProcSafeForCleave());

                foreach (var aetheria in equippedAetheria)
                    aetheria.TryProcItem(attacker, target, selfTarget);
            }
        }

        /// <summary>
        /// Returns TRUE if the proc spell is safe to use on cleaved targets
        /// Filters out AoE spells and self-targeted spells
        /// </summary>
        public bool IsProcSafeForCleave()
        {
            if (!HasProc || ProcSpell == null)
                return false;

            var spell = new Spell(ProcSpell.Value);
            
            // Check if spell data loaded successfully
            if (spell.NotFound || spell._spellBase == null)
                return false;
            
            // Exclude self-targeted spells (they should only affect the attacker)
            if (spell.Flags.HasFlag(SpellFlags.SelfTargeted))
                return false;
                
            // Exclude fellowship spells (they affect multiple targets)
            if (spell.Flags.HasFlag(SpellFlags.FellowshipSpell))
                return false;
                
            // Exclude spells with multiple projectiles (potential AoE)
            if (spell.NumProjectiles > 1)
                return false;
                
            // Exclude ring spells (they have spread angles)
            if (spell.SpreadAngle > 0)
                return false;

            return true;
        }

        /// <summary>
        /// Attempts to proc only the weapon on a cleaved target, honoring content toggle and safety filters.
        /// Each call performs its own independent proc roll.
        /// </summary>
        public void TryProcWeaponOnCleaveTarget(WorldObject attacker, Creature target, WorldObject weapon)
        {
            if (weapon == null)
                return;

            // Only allow weapon procs on cleave, single-target only, not self-targeted
            if (!weapon.HasProc || weapon.ProcSpellSelfTargeted || !weapon.IsProcSafeForCleave())
                return;

            // Single content toggle: enables both projectile and non-projectile procs on cleaved/split targets
            if (!weapon.ProcOnCleaveTargets)
                return;

            // Geometric cleave decay based on additional targets (simultaneous hits)
            var divisorCount = weapon.CleaveTargets;
            var r = weapon.CleaveStrikeDecay;
            float cleaveDecay;
            if (r >= 1.0f)
            {
                // If r = 1.0f (stored 0.0f), no reduction
                cleaveDecay = 1.0f;
            }
            else
            {
                var exp = Math.Max(0, divisorCount);
                cleaveDecay = (float)Math.Pow(r, exp);
            }

            // Process weapon proc
            weapon.TryProcItemWithChanceMod(attacker, target, false, cleaveDecay);

            // Also process equipped items (necklace, armor, etc.) if weapon has toggle enabled
            if (attacker is Creature creatureAttacker)
            {
                foreach (var equippedItem in creatureAttacker.EquippedObjects.Values)
                {
                    // Skip the weapon itself and items without procs
                    if (equippedItem == weapon || !equippedItem.HasProc || equippedItem.ProcSpellSelfTargeted)
                        continue;

                    // Apply same safety filters as weapon
                    if (!equippedItem.IsProcSafeForCleave())
                        continue;

                    // Use same decay logic as weapon
                    equippedItem.TryProcItemWithChanceMod(attacker, target, false, cleaveDecay);
                }
            }
        }

        /// <summary>
        /// Overload that allows caller to specify divisor count explicitly (e.g., split arrows count)
        /// </summary>
        public void TryProcWeaponOnCleaveTarget(WorldObject attacker, Creature target, WorldObject weapon, int divisorCount)
        {
            if (weapon == null)
                return;

            if (!weapon.HasProc || weapon.ProcSpellSelfTargeted || !weapon.IsProcSafeForCleave())
                return;

            if (!weapon.ProcOnCleaveTargets)
                return;

            var r = weapon.CleaveStrikeDecay;
            float cleaveDecay;
            if (r >= 1.0f)
            {
                // If r = 1.0f (stored 0.0f), no reduction
                cleaveDecay = 1.0f;
            }
            else
            {
                var exp = Math.Max(0, divisorCount);
                cleaveDecay = (float)Math.Pow(r, exp);
            }

            // Process weapon proc
            weapon.TryProcItemWithChanceMod(attacker, target, false, cleaveDecay);

            // Also process equipped items (necklace, armor, etc.) if weapon has toggle enabled
            if (attacker is Creature creatureAttacker)
            {
                foreach (var equippedItem in creatureAttacker.EquippedObjects.Values)
                {
                    // Skip the weapon itself and items without procs
                    if (equippedItem == weapon || !equippedItem.HasProc || equippedItem.ProcSpellSelfTargeted)
                        continue;

                    // Apply same safety filters as weapon
                    if (!equippedItem.IsProcSafeForCleave())
                        continue;

                    // Use same decay logic as weapon
                    equippedItem.TryProcItemWithChanceMod(attacker, target, false, cleaveDecay);
                }
            }
        }

        /// <summary>
        /// Overload that applies cleave decay to the already strike-decayed chance
        /// </summary>
        public void TryProcWeaponOnCleaveTarget(WorldObject attacker, Creature target, WorldObject weapon, int divisorCount, int strikeIndex, float strikeMultiplier)
        {
            if (weapon == null || target == null || attacker == null)
                return;

            // First check if the toggle is enabled - this is required for both weapon and equipped items
            if (!weapon.ProcOnCleaveTargets)
                return;

            if (strikeMultiplier == 0f)
                return;

            // Cleave decay uses exponent = additional targets (simultaneous hits)
            var r = weapon.CleaveStrikeDecay;
            float cleaveDecay;
            if (r >= 1.0f)
            {
                // If r = 1.0f (stored 0.0f), no reduction
                cleaveDecay = 1.0f;
            }
            else
            {
                var exp = Math.Max(0, divisorCount);
                cleaveDecay = (float)Math.Pow(r, exp);
            }

            // Multiply the already strike-decayed chance by cleave decay
            var multiplier = strikeMultiplier * cleaveDecay;

            // Process weapon proc (only if weapon has a proc)
            if (weapon.HasProc && !weapon.ProcSpellSelfTargeted && weapon.IsProcSafeForCleave())
            {
                weapon.TryProcItemWithChanceMod(attacker, target, false, multiplier);
            }

            // Also process equipped items (necklace, armor, etc.) if weapon has toggle enabled
            // This allows equipped items to proc even if the weapon doesn't have a proc
            if (attacker is Creature creatureAttacker && creatureAttacker.EquippedObjects != null)
            {
                // Create a snapshot to avoid collection modification during iteration
                // Filter out null items and ensure all properties are accessible
                var equippedItems = creatureAttacker.EquippedObjects.Values
                    .Where(i => i != null && i != weapon && i.HasProc && !i.ProcSpellSelfTargeted && i.IsProcSafeForCleave())
                    .ToList();
                
                foreach (var equippedItem in equippedItems)
                {
                    if (equippedItem == null)
                        continue;

                    // Use same decay logic as weapon
                    equippedItem.TryProcItemWithChanceMod(attacker, target, false, multiplier);
                }
            }
        }

        /// <summary>
        /// Tries to proc equipped items with an external chance multiplier (for multi-strike scaling on primary).
        /// Mirrors TryProcEquippedItems but uses TryProcItemWithChanceMod.
        /// </summary>
        public void TryProcEquippedItemsWithChanceMod(WorldObject attacker, Creature target, bool selfTarget, WorldObject weapon, float chanceMultiplier)
        {
            // handle procs directly on this item -- ie. phials
            if (HasProc && ProcSpellSelfTargeted == selfTarget)
            {
                TryProcItemWithChanceMod(attacker, target, selfTarget, chanceMultiplier);
            }

            // handle proc spells for weapon
            if (weapon != null && weapon.HasProc && weapon.ProcSpellSelfTargeted == selfTarget)
            {
                weapon.TryProcItemWithChanceMod(attacker, target, selfTarget, chanceMultiplier);
            }

            if (attacker != this && attacker.HasProc && attacker.ProcSpellSelfTargeted == selfTarget)
            {
                attacker.TryProcItemWithChanceMod(attacker, target, selfTarget, chanceMultiplier);
            }

            // handle aetheria procs
            if (attacker is Creature wielder)
            {
                var equippedAetheria = wielder.EquippedObjects.Values.Where(i => i.HasProc && i.ProcSpellSelfTargeted == selfTarget);

                foreach (var aetheria in equippedAetheria)
                    aetheria.TryProcItemWithChanceMod(attacker, target, selfTarget, chanceMultiplier);
            }
        }
    }
}
