using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    public partial class PetDevice
    {
        /// <summary>
        /// After visual overrides are applied on summon, merges capture body parts, resolves combat stance,
        /// and optionally reverts motion/CMT to template when melee cannot be resolved.
        /// </summary>
        /// <returns>Resolved stance when combat sync succeeded; null when disabled, fallback, or no valid stance.</returns>
        private MotionStance? TryApplyCaptureSkinCombatSync(CombatPet pet, Player player, uint petClassWcid, uint baselineMotionTableId, uint? baselineCombatTableDid)
        {
            if (!ServerConfig.pet_capture_combat_sync_enabled.Value)
                return null;

            var captureWcid = CaptureSkinCreatureWcid;
            if (captureWcid.HasValue && captureWcid.Value > 0)
                MergeCaptureSkinBodyParts(pet, (uint)captureWcid.Value, petClassWcid);

            pet.GetCombatTable();
            if (pet.CombatTable == null)
                return null;

            var meanDelay = (float)((pet.PowerupTime ?? 1.0f) * 0.5);

            if (!pet.TryFindValidUnarmedMeleeStance(pet.MotionTableId, meanDelay, out var resolvedStance))
            {
                if (ServerConfig.pet_capture_combat_sync_fallback_cosmetic_only.Value)
                    RevertCaptureSkinToTemplateCombat(pet, baselineMotionTableId, baselineCombatTableDid, player);
                return null;
            }

            var rate = pet.EstimateMeleeDamageEventsPerSecond(pet.MotionTableId, meanDelay, resolvedStance);
            if (rate <= float.Epsilon)
            {
                if (ServerConfig.pet_capture_combat_sync_fallback_cosmetic_only.Value)
                    RevertCaptureSkinToTemplateCombat(pet, baselineMotionTableId, baselineCombatTableDid, player);
                return null;
            }

            return resolvedStance;
        }

        private static void RevertCaptureSkinToTemplateCombat(CombatPet pet, uint baselineMotionTableId, uint? baselineCombatTableDid, Player player)
        {
            pet.MotionTableId = baselineMotionTableId;

            if (baselineCombatTableDid.HasValue && baselineCombatTableDid.Value > 0)
            {
                pet.CombatTableDID = baselineCombatTableDid.Value;
                pet.GetCombatTable();
            }
            else
            {
                pet.CombatTableDID = null;
                pet.CombatTable = null;
            }

            if (player?.Session != null)
            {
                player.SendTransientError(
                    "That creature's fighting style does not match this essence; your pet will use essence combat animations.");
            }
        }

        /// <summary>
        /// Ensures <see cref="CombatBodyPart"/> keys from the capture weenie exist on the pet biota with tier damage from the PetClass template.
        /// </summary>
        internal static void MergeCaptureSkinBodyParts(Creature pet, uint captureWcid, uint petClassWcid)
        {
            var captureWeenie = DatabaseManager.World.GetCachedWeenie(captureWcid);
            var petClassWeenie = DatabaseManager.World.GetCachedWeenie(petClassWcid);

            if (captureWeenie?.PropertiesBodyPart == null || captureWeenie.PropertiesBodyPart.Count == 0)
                return;

            if (petClassWeenie?.PropertiesBodyPart == null || petClassWeenie.PropertiesBodyPart.Count == 0)
                return;

            var primary = FindPrimaryAttackBodyPart(petClassWeenie.PropertiesBodyPart);
            if (primary == null)
                return;

            pet.Biota.PropertiesBodyPart ??= new Dictionary<CombatBodyPart, PropertiesBodyPart>();

            foreach (var capKvp in captureWeenie.PropertiesBodyPart)
            {
                if (capKvp.Key == CombatBodyPart.Breath)
                    continue;

                if (petClassWeenie.PropertiesBodyPart.TryGetValue(capKvp.Key, out var tierPart))
                {
                    pet.Biota.PropertiesBodyPart[capKvp.Key] = tierPart.Clone();
                    continue;
                }

                var merged = primary.Clone();
                CopyBodyPartHitData(capKvp.Value, merged);
                pet.Biota.PropertiesBodyPart[capKvp.Key] = merged;
            }
        }

        private static PropertiesBodyPart FindPrimaryAttackBodyPart(IDictionary<CombatBodyPart, PropertiesBodyPart> parts)
        {
            PropertiesBodyPart best = null;
            var bestDVal = 0;

            foreach (var kvp in parts)
            {
                if (kvp.Key == CombatBodyPart.Breath || kvp.Value.DVal <= 0)
                    continue;

                if (kvp.Value.DVal > bestDVal)
                {
                    bestDVal = kvp.Value.DVal;
                    best = kvp.Value;
                }
            }

            return best;
        }

        private static void CopyBodyPartHitData(PropertiesBodyPart from, PropertiesBodyPart to)
        {
            to.BH = from.BH;
            to.HLF = from.HLF;
            to.MLF = from.MLF;
            to.LLF = from.LLF;
            to.HRF = from.HRF;
            to.MRF = from.MRF;
            to.LRF = from.LRF;
            to.HLB = from.HLB;
            to.MLB = from.MLB;
            to.LLB = from.LLB;
            to.HRB = from.HRB;
            to.MRB = from.MRB;
            to.LRB = from.LRB;
        }
    }
}
