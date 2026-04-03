using System;
using System.Linq;
using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Common;
using ACE.Server.WorldObjects;
using System.Numerics;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Monster Capture System - Siphon Lens Implementation
    /// Allows players to capture creature appearances using tiered Siphon Lenses
    /// 
    /// Lens Tiers:
    /// - Tier 1 (Flawed): 5-10% base capture rate
    /// - Tier 2 (Pristine): 10-20% base capture rate  
    /// - Tier 3 (Perfect): 15-30% base capture rate
    /// - Tier 4 (Debug): 100% capture, not consumed (admin only)
    /// - Tier 5 (Resonance): Second-chance lens for failed shiny captures only
    /// - Tier 6 (Asheron's): Guaranteed 100% capture, consumed on use
    /// </summary>
    public static class MonsterCapture
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Lens tier constants for clarity
        private const int TIER_FLAWED = 1;
        private const int TIER_PRISTINE = 2;
        private const int TIER_PERFECT = 3;
        private const int TIER_DEBUG = 4;
        private const int TIER_RESONANCE = 5;
        private const int TIER_ASHERONS = 6;
        /// <summary>
        /// Siphon Lens System - Capture creature appearance
        /// Handles all lens tiers including Resonance (second-chance) and Asheron's (guaranteed)
        /// </summary>
        public static void UseCaptureCrystal(Player player, WorldObject crystal)
        {
            // Validation
            if (!IsCaptureCrystal(crystal))
            {
                player.SendTransientError("This is not a siphon lens.");
                return;
            }
            
            // Null safety checks
            if (player?.PhysicsObj?.ObjMaint == null)
            {
                player?.SendTransientError("Unable to detect nearby creatures.");
                return;
            }

            // Get crystal tier early for special lens handling
            var crystalTier = crystal.GetProperty(PropertyInt.CrystalTier) ?? TIER_PRISTINE;
            var isDebugLens = crystalTier == TIER_DEBUG;
            var isResonanceLens = crystalTier == TIER_RESONANCE || (crystal.GetProperty(PropertyBool.IsResonanceLens) ?? false);
            var isAsheronsLens = crystalTier == TIER_ASHERONS || (crystal.GetProperty(PropertyBool.IsGuaranteedCaptureLens) ?? false);
            
            // For Resonance Lens: validate that player has a valid failed shiny capture target
            if (isResonanceLens)
            {
                if (!ValidateResonanceLensUse(player, out var resonanceTarget, out var errorMessage))
                {
                    player.SendTransientError(errorMessage);
                    return;
                }
                
                // Use the stored failed shiny target for resonance lens
                UseResonanceLens(player, crystal, resonanceTarget);
                return;
            }
            
            // Find nearest creature within 5 units - must be attackable (hostile mobs only)
            var nearbyCreatures = player.PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature()
                .Where(c => c != null && c != player && !(c is Player) && c.Attackable && c.Location != null && c.Location.DistanceTo(player.Location) <= 5.0f)
                .OrderBy(c => c.Location.DistanceTo(player.Location))
                .ToList();
            
            if (!nearbyCreatures.Any())
            {
                player.SendTransientError("No creatures nearby to siphon!");
                return;
            }
            
            var targetCreature = nearbyCreatures.First();
            
            // Null safety checks
            if (targetCreature.Health == null || targetCreature.Health.MaxValue <= 0)
            {
                player.SendTransientError("Unable to assess creature health.");
                return;
            }
            
            // Prevent capturing dead/dying creatures
            if (targetCreature.Health.Current <= 0)
            {
                player.SendTransientError("The creature is already dead!");
                return;
            }

            // Prevent multiple captures on the same creature
            if (targetCreature.Invincible)
            {
                player.SendTransientError("This creature is already being captured!");
                return;
            }
            
            // Health threshold check - Debug and Asheron's Lenses bypass this
            var healthPercent = (float)targetCreature.Health.Current / targetCreature.Health.MaxValue;
            if (!isDebugLens && !isAsheronsLens && healthPercent > 0.20f && targetCreature.Health.Current >= 20)
            {
                player.SendTransientError($"The creature is too strong! Weaken it below 20% health or 20 HP first. (Currently {healthPercent:P0})");
                return;
            }
            
            // Prevent capturing tamed creatures (other players' pets)
            if (targetCreature.PetOwner != null && targetCreature.PetOwner.Value > 0)
            {
                player.SendTransientError("You cannot capture a tamed creature!");
                return;
            }
            
            // Check capture blacklist
            if (BlacklistManager.IsNoCapture(targetCreature.WeenieClassId))
            {
                player.SendTransientError("This creature cannot be captured!");
                return;
            }
            
            // Check for Enraged state (The "One Chance" Rule)
            // Asheron's Lens can capture enraged creatures
            if (targetCreature.IsEnraged && !isAsheronsLens)
            {
                // Check for Admin Override
                var isAdmin = player.Session.AccessLevel > AccessLevel.Player;
                
                if (!isAdmin)
                {
                    player.SendTransientError("The creature is in a blind rage and cannot be siphoned!");
                    return; 
                }
                else
                {
                    player.SendMessage("Admin Override: Attempting to capture enraged creature...");
                }
            }
            
            var successRate = CalculateSuccessRate(player, crystal, targetCreature, healthPercent);
            
            bool success;
            if (successRate >= 1.0f)
            {
                success = true;
            }
            else
            {
                var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
                success = roll < successRate;
            }
            
            // Consume crystal (DEBUG LENS tier 4 is never consumed)
            // Must check lens consumption BEFORE treating capture as successful
            if (!isDebugLens)
            {
                var consumeResult = player.TryConsumeFromInventoryWithNetworking(crystal, 1);
                if (!consumeResult)
                {
                    player.SendTransientError("Failed to consume the siphon lens!");
                    return;
                }
            }
            
            if (success)
            {
                // Do NOT clear failed shiny retry here - that state is only cleared when
                // Resonance Lens is successfully consumed. Unrelated normal siphon success
                // or failed consume must not discard the player's retry.
                ExecuteCaptureSuccess(player, targetCreature, crystal);
            }
            else
            {
                // Check if this was a shiny creature - track for Resonance Lens second chance
                var isShiny = targetCreature.CreatureVariant == CreatureVariant.Shiny;
                if (isShiny)
                {
                    SetFailedShinyCaptureState(player, targetCreature);
                    player.SendMessage("The shiny creature resisted your capture! You may use a Resonance Lens for a second chance.");
                }
                
                ExecuteCaptureFail(player, targetCreature);
            }
        }

        /// <summary>
        /// Validates that a Resonance Lens can be used (player has a valid failed shiny capture)
        /// </summary>
        private static bool ValidateResonanceLensUse(Player player, out Creature targetCreature, out string errorMessage)
        {
            targetCreature = null;
            errorMessage = null;

            // Check if player has a failed shiny capture state
            var failedWcid = player.GetProperty(PropertyInt.FailedShinyCaptureWCID);
            var failedGuidInt = player.GetProperty(PropertyInt.FailedShinyCaptureGuid);

            if (!failedWcid.HasValue || !failedGuidInt.HasValue)
            {
                errorMessage = "The Resonance Lens hums quietly but finds no echo to follow. You haven't failed a shiny capture recently.";
                return false;
            }

            var storedWcid = (uint)failedWcid.Value;
            var targetGuidFull = (uint)failedGuidInt.Value;

            // Find the creature - must match both WCID and Guid, and be nearby (same predicate as base capture path)
            if (player.PhysicsObj?.ObjMaint == null)
            {
                errorMessage = "Unable to detect nearby creatures.";
                return false;
            }

            var nearbyCreatures = player.PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature()
                .Where(c => c != null && c != player && !(c is Player) && c.Attackable && c.Location != null
                    && c.Location.DistanceTo(player.Location) <= 5.0f
                    && c.WeenieClassId == storedWcid
                    && c.Guid.Full == targetGuidFull)
                .OrderBy(c => c.Location.DistanceTo(player.Location))
                .ToList();

            if (!nearbyCreatures.Any())
            {
                // Clear the stale state
                ClearFailedShinyCaptureState(player);
                errorMessage = "The echo has faded. The shiny creature you failed to capture is no longer nearby.";
                return false;
            }

            targetCreature = nearbyCreatures.First();

            // Verify creature is still alive
            if (targetCreature.Health == null || targetCreature.Health.Current <= 0)
            {
                ClearFailedShinyCaptureState(player);
                errorMessage = "The creature has perished. The echo cannot reach beyond death.";
                return false;
            }

            // Verify creature is still being captured (invincible) would mean already captured
            if (targetCreature.Invincible)
            {
                ClearFailedShinyCaptureState(player);
                errorMessage = "This creature is already being captured!";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Executes the Resonance Lens second-chance capture attempt
        /// </summary>
        private static void UseResonanceLens(Player player, WorldObject crystal, Creature targetCreature)
        {
            // Defensive check: avoid division by zero when computing healthPercent
            if (targetCreature.Health == null || targetCreature.Health.MaxValue <= 0)
            {
                player.SendTransientError("Unable to assess creature health.");
                return;
            }

            // Resonance Lens grants a bonus to capture rate but is not guaranteed
            // It uses the same base mechanics but with a significant bonus
            var healthPercent = (float)targetCreature.Health.Current / targetCreature.Health.MaxValue;
            
            // Calculate success rate with resonance bonus
            var baseRate = CalculateSuccessRate(player, crystal, targetCreature, healthPercent);
            var resonanceBonus = 0.25f; // +25% bonus for resonance lens
            var finalRate = Math.Min(baseRate + resonanceBonus, 0.60f); // Cap at 60%

            var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
            var success = roll < finalRate;

            // Consume the Resonance Lens - must succeed before clearing retry state
            var consumeResult = player.TryConsumeFromInventoryWithNetworking(crystal, 1);
            if (!consumeResult)
            {
                player.SendTransientError("Failed to consume the Resonance Lens!");
                return;
            }

            // Clear the failed state only after lens consumption is confirmed
            // (prevents discarding retry on failed consume)
            ClearFailedShinyCaptureState(player);

            player.SendMessage($"The Resonance Lens pulses with stored energy... (Capture chance: {finalRate:P0})");

            if (success)
            {
                // Special visual for resonance capture
                targetCreature.EnqueueBroadcast(new GameMessageScript(
                    targetCreature.Guid, PlayScript.VisionUpWhite));
                targetCreature.EnqueueBroadcast(new GameMessageScript(
                    targetCreature.Guid, PlayScript.EnchantUpYellow));
                
                ExecuteCaptureSuccess(player, targetCreature, crystal);
                player.SendMessage("The echo resonates perfectly! The essence is captured!");
            }
            else
            {
                // Resonance lens failure - NO second second-chance
                player.SendMessage("The resonance fades into silence. The lens shatters without effect.");
                
                // Dark visual effect but creature does NOT enrage again
                // (It's already enraged from the first failure)
                targetCreature.EnqueueBroadcast(new GameMessageScript(
                    targetCreature.Guid, PlayScript.VisionDownBlack));
            }
        }

        /// <summary>
        /// Records a failed shiny capture on the player for Resonance Lens second-chance
        /// </summary>
        private static void SetFailedShinyCaptureState(Player player, Creature creature)
        {
            player.SetProperty(PropertyInt.FailedShinyCaptureWCID, (int)creature.WeenieClassId);
            // Store Guid.Full as int (it's a uint but fits in int for storage)
            player.SetProperty(PropertyInt.FailedShinyCaptureGuid, (int)creature.Guid.Full);
            
            log.Debug($"[MonsterCapture] Set failed shiny capture state for player {player.Name}: WCID={creature.WeenieClassId}, Guid={creature.Guid.Full}");
        }

        /// <summary>
        /// Clears the failed shiny capture state from the player
        /// </summary>
        private static void ClearFailedShinyCaptureState(Player player)
        {
            player.RemoveProperty(PropertyInt.FailedShinyCaptureWCID);
            player.RemoveProperty(PropertyInt.FailedShinyCaptureGuid);
        }

        /// <summary>
        /// Executes a successful capture sequence
        /// </summary>
        private static void ExecuteCaptureSuccess(Player player, Creature targetCreature, WorldObject crystal)
        {
            // Visual effect - Focus Self (white glow)
            targetCreature.EnqueueBroadcast(new GameMessageScript(
                targetCreature.Guid, PlayScript.VisionUpWhite));
            
            // Create siphoned essence BEFORE making creature invulnerable
            var capturedItem = CreateCapturedAppearance(targetCreature, crystal, player);
            if (capturedItem == null)
            {
                player.SendTransientError("Failed to create siphoned essence!");
                return;
            }
            
            // Only make creature invulnerable AFTER successful essence creation
            targetCreature.Invincible = true;
            targetCreature.IsBusy = true;
            targetCreature.SetCombatMode(CombatMode.NonCombat);
            
            if (player.TryCreateInInventoryWithNetworking(capturedItem))
            {
                player.SendMessage($"You successfully siphoned the essence of the {targetCreature.Name}!");
            }
            else
            {
                // Inventory full - drop the essence on the ground instead
                player.SendTransientError("Your inventory is full! The essence drops to the ground.");
                capturedItem.Location = new ACE.Entity.Position(player.Location);
                capturedItem.EnterWorld();
            }
            
            // Trigger proper creature death after 3 seconds
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(3.0);
            actionChain.AddAction(targetCreature, ActionType.MonsterCapture_DespawnCreature, () =>
            {
                if (!targetCreature.IsDestroyed && targetCreature.Health.Current > 0)
                {
                    targetCreature.DamageHistory.Add(player, DamageType.Health, (uint)targetCreature.Health.Current);
                    targetCreature.OnDeath(targetCreature.DamageHistory.LastDamager, DamageType.Health);
                    targetCreature.Die();
                }
            });
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Executes a failed capture sequence (enrages the creature)
        /// </summary>
        private static void ExecuteCaptureFail(Player player, Creature targetCreature)
        {
            // Visual effect - dark burst
            targetCreature.EnqueueBroadcast(new GameMessageScript(
                targetCreature.Guid, PlayScript.VisionDownBlack));
            
            player.SendMessage($"The siphon failed! The lens shatters!");
            
            // Only enrage if not already enraged
            if (!targetCreature.IsEnraged)
            {
                // Apply "Hardcore" stats: 2x Damage, ~3x Effective Health (0.67 reduction)
                targetCreature.SetProperty(PropertyFloat.EnrageDamageMultiplier, 2.0f);
                targetCreature.SetProperty(PropertyFloat.EnrageDamageReduction, 0.67f);
                
                // Random Enrage Visual
                var visualOptions = new[] { 
                    PlayScript.ShieldUpRed,
                    PlayScript.BreatheFlame,
                    PlayScript.EnchantUpRed,
                    PlayScript.Create
                };
                var chosenVisual = visualOptions[ThreadSafeRandom.Next(0, visualOptions.Length - 1)];
                targetCreature.SetProperty(PropertyInt.EnrageVisualEffect, (int)chosenVisual);

                // Random Enrage Sound
                var soundOptions = new[] { 101, 118, 107, 112 };
                var chosenSound = soundOptions[ThreadSafeRandom.Next(0, soundOptions.Length - 1)];
                targetCreature.SetProperty(PropertyInt.EnrageSound, chosenSound);
                
                // Heal to Full
                targetCreature.Health.Current = targetCreature.Health.MaxValue;
                
                targetCreature.Enrage();
            }
        }
        
        /// <summary>
        /// Calculate success rate based on crystal tier, skill, and creature difficulty
        /// 
        /// Tier overview:
        /// - Tier 1 (Flawed): 5% base, 10% cap
        /// - Tier 2 (Pristine): 10% base, 20% cap
        /// - Tier 3 (Perfect): 15% base, 30% cap
        /// - Tier 4 (Debug): 100% guaranteed (admin)
        /// - Tier 5 (Resonance): Uses base tier 2 rates, handled with bonus in UseResonanceLens
        /// - Tier 6 (Asheron's): 100% guaranteed capture
        /// </summary>
        private static float CalculateSuccessRate(Player player, WorldObject crystal, Creature creature, float healthPercent)
        {
            var crystalTier = crystal.GetProperty(PropertyInt.CrystalTier) ?? TIER_PRISTINE;
            
            // Debug Lens and Asheron's Lens: Guaranteed 100% capture
            if (crystalTier == TIER_DEBUG) return 1.0f;
            if (crystalTier == TIER_ASHERONS) return 1.0f;
            if (crystal.GetProperty(PropertyBool.IsGuaranteedCaptureLens) ?? false) return 1.0f;
            
            // Base rate and cap by tier
            // Resonance lens (tier 5) uses Pristine (tier 2) base rates
            var (baseRate, tierCap) = crystalTier switch {
                TIER_FLAWED => (0.05f, 0.10f),
                TIER_PRISTINE => (0.10f, 0.20f),
                TIER_PERFECT => (0.15f, 0.30f),
                TIER_DEBUG => (1.00f, 1.00f),
                TIER_RESONANCE => (0.10f, 0.20f), // Uses Pristine base for calculation
                TIER_ASHERONS => (1.00f, 1.00f),
                _ => (0.10f, 0.20f)
            };
            
            // Skill bonus (max +10%)
            var playerSkill = player.GetCreatureSkill(Skill.AssessCreature).Current;
            var skillBonus = Math.Min(playerSkill / 10000f, 0.10f);
            
            // Specialization bonus (+5% if specialized)
            var isSpecialized = player.GetCreatureSkill(Skill.AssessCreature).AdvancementClass == SkillAdvancementClass.Specialized;
            var specializationBonus = isSpecialized ? 0.05f : 0f;
            
            // Health bonus (max +5% at 0% health)
            var healthBonus = (1f - healthPercent) * 0.05f;
            
            // Creature difficulty penalty based on level DIFFERENCE (max -25% for 100+ levels higher)
            var creatureLevel = creature.Level ?? 1;
            var playerLevel = player.Level ?? 1;
            var levelDiff = Math.Max(0, creatureLevel - playerLevel);
            var difficultyPenalty = Math.Min(levelDiff / 400f, 0.25f);
            
            // Capture Difficulty Multiplier (default 1.0)
            var difficultyMultiplier = (float)(creature.GetProperty(PropertyFloat.CaptureDifficulty) ?? 1.0f);
            
            // Calculate final rate with tier cap
            var rawRate = (baseRate + skillBonus + specializationBonus + healthBonus - difficultyPenalty) * difficultyMultiplier;
            var finalRate = Math.Clamp(rawRate, 0.01f, tierCap);

            return finalRate;
        }
        
        /// <summary>
        /// Create captured appearance with ALL visual properties
        /// </summary>
        private static WorldObject CreateCapturedAppearance(Creature creature, WorldObject crystal, Player player)
        {
            var item = WorldObjectFactory.CreateNewWorldObject(78780004); // Captured Appearance Template
            
            if (item == null)
                return null;
            
            // Get the base creature name (strip ALL "Player's" prefixes - may be nested)
            var creatureName = creature.Name;
            int apostropheIdx;
            while ((apostropheIdx = creatureName.IndexOf("'s ")) > 0)
            {
                creatureName = creatureName.Substring(apostropheIdx + 3); // Skip past "'s "
            }
            
            item.Name = $"Siphoned {creatureName} Essence";
            item.LongDesc = $"The siphoned essence of a {creatureName}. Use on a pet device to apply this appearance to your summoned pet.";
            
            // Store creature reference, name, and species (use base name without ownership prefix)
            item.SetProperty(PropertyInt.CapturedCreatureWCID, (int)creature.WeenieClassId);
            item.SetProperty(PropertyString.CapturedCreatureName, creatureName);
            
            // Capture creature type (species) for spawned pet
            if (creature.CreatureType.HasValue)
                item.SetProperty(PropertyInt.CapturedCreatureType, (int)creature.CreatureType.Value);
            
            // Capture ALL visual properties
            if (creature.SetupTableId != 0)
                item.SetProperty(PropertyDataId.CapturedSetup, creature.SetupTableId);
            
            if (creature.MotionTableId != 0)
                item.SetProperty(PropertyDataId.CapturedMotionTable, creature.MotionTableId);
            
            if (creature.SoundTableId != 0)
                item.SetProperty(PropertyDataId.CapturedSoundTable, creature.SoundTableId);
            
            if (creature.PaletteBaseId.HasValue)
                item.SetProperty(PropertyDataId.CapturedPaletteBase, creature.PaletteBaseId.Value);
            
            if (creature.ClothingBase.HasValue)
                item.SetProperty(PropertyDataId.CapturedClothingBase, creature.ClothingBase.Value);
            
            if (creature.PaletteTemplate.HasValue)
                item.SetProperty(PropertyInt.CapturedPaletteTemplate, creature.PaletteTemplate.Value);
            
            if (creature.Shade.HasValue)
                item.SetProperty(PropertyFloat.CapturedShade, creature.Shade.Value);
            
            // Keep the template's base icon (78780004's icon from SQL)
            // Add creature icon as overlay on top
            if (creature.IconId != 0)
                item.IconOverlayId = creature.IconId;

            // Capture shiny variant status
            if (creature.CreatureVariant.HasValue)
                item.SetProperty(PropertyInt.CapturedCreatureVariant, (int)creature.CreatureVariant.Value);

            // Capture the full ObjDesc (visual appearance) for humanoid creatures
            // This preserves textures, palettes, and body part overrides from equipped items
            try
            {
                var objDesc = creature.CalculateObjDesc();
                
                // Serialize AnimPartChanges: Index:AnimationId,Index:AnimationId,...
                if (objDesc.AnimPartChanges != null && objDesc.AnimPartChanges.Count > 0)
                {
                    var animParts = string.Join(",", objDesc.AnimPartChanges.Select(a => $"{a.Index}:{a.AnimationId}"));
                    item.SetProperty(PropertyString.CapturedObjDescAnimParts, animParts);
                    log.Debug($"[MonsterCapture] Captured {objDesc.AnimPartChanges.Count} AnimPartChanges");
                }
                
                // Serialize SubPalettes: SubPaletteId:Offset:Length,SubPaletteId:Offset:Length,...
                if (objDesc.SubPalettes != null && objDesc.SubPalettes.Count > 0)
                {
                    var palettes = string.Join(",", objDesc.SubPalettes.Select(p => $"{p.SubPaletteId}:{p.Offset}:{p.Length}"));
                    item.SetProperty(PropertyString.CapturedObjDescPalettes, palettes);
                    log.Debug($"[MonsterCapture] Captured {objDesc.SubPalettes.Count} SubPalettes");
                }
                
                // Serialize TextureChanges: PartIndex:OldTexture:NewTexture,PartIndex:OldTexture:NewTexture,...
                if (objDesc.TextureChanges != null && objDesc.TextureChanges.Count > 0)
                {
                    var textures = string.Join(",", objDesc.TextureChanges.Select(t => $"{t.PartIndex}:{t.OldTexture}:{t.NewTexture}"));
                    item.SetProperty(PropertyString.CapturedObjDescTextures, textures);
                    log.Debug($"[MonsterCapture] Captured {objDesc.TextureChanges.Count} TextureChanges");
                }
            }
            catch (Exception ex)
            {
                log.Warn($"[MonsterCapture] Failed to capture ObjDesc for {creature.Name}: {ex.Message}");
            }

            // Capture Equipped Items (Armor, Shield, Weapons)
            if (creature.EquippedObjects != null && creature.EquippedObjects.Count > 0)
            {
                var capturedItems = new System.Collections.Generic.List<string>();
                var processedWCIDs = new System.Collections.Generic.HashSet<uint>();

                foreach (var itemObj in creature.EquippedObjects.Values)
                {
                    if (processedWCIDs.Contains(itemObj.WeenieClassId))
                        continue;

                    processedWCIDs.Add(itemObj.WeenieClassId);

                    // valid properties to capture?
                    var scale = itemObj.ObjScale ?? 1.0f;
                    var palette = itemObj.PaletteTemplate ?? 0;
                    var shade = itemObj.Shade ?? 0.0f;

                    // Format: WCID;Scale;Palette;Shade
                    // Use InvariantCulture to ensure dot decimal separator
                    var entry = $"{itemObj.WeenieClassId};{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)};{palette};{shade.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    capturedItems.Add(entry);
                }

                if (capturedItems.Count > 0)
                {
                    // Format: |Entry1|Entry2|...|
                    var capturedItemsString = "|" + string.Join("|", capturedItems) + "|";
                    item.SetProperty(PropertyString.CapturedItems, capturedItemsString);
                    log.Debug($"[MonsterCapture] Captured {capturedItems.Count} equipped items: {capturedItemsString}");
                }
            }
            
            // Debug logging
            log.Debug($"[MonsterCapture] Created Siphoned Essence: BaseIcon={item.IconId:X}, OverlayIcon={item.IconOverlayId}, Creature={creature.Name}");
            
            // Smart scale normalization for pets
            // We target a consistent physical size (e.g. human height) rather than just scaling relative to the model
            var normalizedScale = NormalizeScaleForPet(creature, player);
            item.SetProperty(PropertyFloat.CapturedScale, normalizedScale);
            
            return item;
        }
        
        /// <summary>
        /// Create a Hollow Essence (78780006) by cloning all properties from an Original Essence (78780004)
        /// Hollow Essences cannot be turned in for QB but can be applied to crates and freely stored/traded
        /// </summary>
        public static WorldObject CreateHollowEssence(WorldObject originalEssence)
        {
            if (originalEssence == null || originalEssence.WeenieClassId != 78780004)
            {
                log.Warn($"[MonsterCapture] CreateHollowEssence called with invalid essence (WCID: {originalEssence?.WeenieClassId})");
                return null;
            }

            // Create the hollow essence template
            var hollowEssence = WorldObjectFactory.CreateNewWorldObject(78780006);
            
            if (hollowEssence == null)
            {
                log.Error("[MonsterCapture] Failed to create Hollow Essence (WCID 78780006)");
                return null;
            }

            // Clone ALL captured properties from original essence
            // PropertyInt properties
            var capturedProps = new[] 
            {
                PropertyInt.CapturedCreatureWCID,
                PropertyInt.CapturedCreatureType,
                PropertyInt.CapturedCreatureVariant,
                PropertyInt.CapturedPaletteTemplate
            };

            foreach (var prop in capturedProps)
            {
                var value = originalEssence.GetProperty(prop);
                if (value.HasValue)
                    hollowEssence.SetProperty(prop, value.Value);
            }

            // PropertyString properties
            var capturedStringProps = new[]
            {
                PropertyString.CapturedCreatureName,
                PropertyString.CapturedObjDescAnimParts,
                PropertyString.CapturedObjDescPalettes,
                PropertyString.CapturedObjDescTextures,
                PropertyString.CapturedItems
            };

            foreach (var prop in capturedStringProps)
            {
                var value = originalEssence.GetProperty(prop);
                if (!string.IsNullOrEmpty(value))
                    hollowEssence.SetProperty(prop, value);
            }

            // PropertyDataId properties
            var capturedDataIdProps = new[]
            {
                PropertyDataId.CapturedSetup,
                PropertyDataId.CapturedMotionTable,
                PropertyDataId.CapturedSoundTable,
                PropertyDataId.CapturedPaletteBase,
                PropertyDataId.CapturedClothingBase
            };

            foreach (var prop in capturedDataIdProps)
            {
                var value = originalEssence.GetProperty(prop);
                if (value.HasValue)
                    hollowEssence.SetProperty(prop, value.Value);
            }

            // PropertyFloat properties
            var capturedFloatProps = new[]
            {
                PropertyFloat.CapturedShade,
                PropertyFloat.CapturedScale
            };

            foreach (var prop in capturedFloatProps)
            {
                var value = originalEssence.GetProperty(prop);
                if (value.HasValue)
                    hollowEssence.SetProperty(prop, value.Value);
            }

            // Copy icon overlay (creature icon)
            if (originalEssence.IconOverlayId.HasValue)
                hollowEssence.IconOverlayId = originalEssence.IconOverlayId;

            // Update the name to replace "Siphoned" with "Hollow"
            var creatureName = originalEssence.GetProperty(PropertyString.CapturedCreatureName);
            if (!string.IsNullOrEmpty(creatureName))
            {
                hollowEssence.Name = $"Hollow {creatureName} Essence";
                hollowEssence.LongDesc = $"A hollow essence of a {creatureName}. This essence has already been registered and cannot be turned in again for Quest Bonus. " +
                                        $"You can still use it on a pet summoning device to apply the appearance to your summoned pet, or trade/store it safely.";
            }

            log.Debug($"[MonsterCapture] Created Hollow Essence for {creatureName}");
            
            return hollowEssence;
        }
        
        /// <summary>
        /// Normalize creature scale based on PHYSICAL HEIGHT
        /// Targets a standard pet size (~0.75m tall) regardless of original model size
        /// </summary>
        private static float NormalizeScaleForPet(Creature creature, Player player)
        {
            const float TARGET_HEIGHT = 0.75f; // Target height in meters (compact pet size)
            const float MIN_SCALE = 0.01f;     // Allow massive creatures to shrink to target (75m → 0.75m)
            const float MAX_SCALE = 15.0f;     // Allow tiny creatures (rabbits, bugs) to scale up to target
            
            // Get current physical height (includes current scale)
            var currentHeight = creature.PhysicsObj.GetHeight();
            var currentScale = creature.ObjScale ?? 1.0f;
            
            // If height is 0 (some models), fallback to simple scale logic
            if (currentHeight <= 0.1f)
            {
                return 0.4f;
            }

            // Calculate scale needed to reach target height
            // CurrentHeight = BaseHeight * CurrentScale
            // TargetScale = (TargetHeight / CurrentHeight) * CurrentScale
            var neededScale = (TARGET_HEIGHT / currentHeight) * currentScale;
            var clampedScale = Math.Clamp(neededScale, MIN_SCALE, MAX_SCALE);
            
            // Apply bounds
            return clampedScale;
        }
        
        /// <summary>
        /// Apply captured appearance to pet crate
        /// </summary>
        public static void ApplyAppearanceToCrate(Player player, PetDevice crate, WorldObject capturedItem)
        {
            if (!IsCapturedAppearance(capturedItem))
            {
                player.SendTransientError("This is not a captured appearance.");
                return;
            }
            
            // Extract visual data
            var setupId = capturedItem.GetProperty(PropertyDataId.CapturedSetup);
            if (!setupId.HasValue)
            {
                player.SendTransientError("This captured appearance is corrupted.");
                return;
            }
            
            // Consume item FIRST to prevent duplication (exploit)
            if (!player.TryConsumeFromInventoryWithNetworking(capturedItem, 1))
            {
                player.SendTransientError("Failed to consume the essence.");
                return;
            }

            // Apply ALL visual properties to crate
            crate.VisualOverrideSetup = setupId;
            crate.VisualOverrideMotionTable = capturedItem.GetProperty(PropertyDataId.CapturedMotionTable);
            crate.VisualOverrideSoundTable = capturedItem.GetProperty(PropertyDataId.CapturedSoundTable);
            crate.VisualOverridePaletteBase = capturedItem.GetProperty(PropertyDataId.CapturedPaletteBase);
            crate.VisualOverrideClothingBase = capturedItem.GetProperty(PropertyDataId.CapturedClothingBase);
            // Don't copy VisualOverrideIcon - we set the crate icon directly below
            crate.VisualOverridePaletteTemplate = capturedItem.GetProperty(PropertyInt.CapturedPaletteTemplate);
            crate.VisualOverrideShade = capturedItem.GetProperty(PropertyFloat.CapturedShade);
            crate.VisualOverrideScale = capturedItem.GetProperty(PropertyFloat.CapturedScale);
            crate.VisualOverrideName = capturedItem.GetProperty(PropertyString.CapturedCreatureName);
            crate.VisualOverrideCreatureVariant = capturedItem.GetProperty(PropertyInt.CapturedCreatureVariant);
            crate.VisualOverrideCapturedItems = capturedItem.GetProperty(PropertyString.CapturedItems);
            crate.VisualOverrideCreatureType = capturedItem.GetProperty(PropertyInt.CapturedCreatureType);
            
            // Transfer ObjDesc properties for full humanoid appearance
            // IMPORTANT: Clear old values if new essence doesn't have them (prevents corruption when switching creature types)
            var animParts = capturedItem.GetProperty(PropertyString.CapturedObjDescAnimParts);
            if (!string.IsNullOrEmpty(animParts))
                crate.SetProperty(PropertyString.CapturedObjDescAnimParts, animParts);
            else
                crate.RemoveProperty(PropertyString.CapturedObjDescAnimParts);
            
            var palettes = capturedItem.GetProperty(PropertyString.CapturedObjDescPalettes);
            if (!string.IsNullOrEmpty(palettes))
                crate.SetProperty(PropertyString.CapturedObjDescPalettes, palettes);
            else
                crate.RemoveProperty(PropertyString.CapturedObjDescPalettes);
            
            var textures = capturedItem.GetProperty(PropertyString.CapturedObjDescTextures);
            if (!string.IsNullOrEmpty(textures))
                crate.SetProperty(PropertyString.CapturedObjDescTextures, textures);
            else
                crate.RemoveProperty(PropertyString.CapturedObjDescTextures);
            
            // Update the crate's icon: 0x060012F8 base + creature overlay from essence
            crate.IconId = 0x060012F8;
            
            // Copy the creature icon overlay from the essence
            if (capturedItem.IconOverlayId.HasValue && capturedItem.IconOverlayId.Value != 0)
            {
                crate.IconOverlayId = capturedItem.IconOverlayId.Value;
            }
            else
            {
                crate.IconOverlayId = 0;
            }
            
            // Clear properties from essence template that shouldn't be on the crate
            crate.RemoveProperty(PropertyDataId.IconUnderlay);  // Don't copy underlay from essence
            crate.RemoveProperty(PropertyInt.UiEffects);        // Don't copy purple outline from essence
            
            // Update client immediately using existing UpdateProperty method (same as /setproperty)
            player.UpdateProperty(crate, PropertyDataId.Icon, crate.IconId);
            if (crate.IconOverlayId.HasValue)
                player.UpdateProperty(crate, PropertyDataId.IconOverlay, crate.IconOverlayId.Value);
            else
                player.UpdateProperty(crate, PropertyDataId.IconOverlay, 0u);
            
            // Debug logging
            log.Debug($"[MonsterCapture] Applied to Crate: CrateIcon={crate.IconId:X}, CrateOverlay={crate.IconOverlayId}, CrateUnderlay={crate.IconUnderlayId}, EssenceIcon={capturedItem.IconId:X}, EssenceOverlay={capturedItem.IconOverlayId}");
            
            // Save the crate with updated icons
            crate.SaveBiotaToDatabase();
            
            player.SendMessage($"You have applied the siphoned essence to your {crate.Name}!");
            player.SendMessage("Resummon your pet to see the new appearance!");
        }
        
        public static bool IsCaptureCrystal(WorldObject wo)
        {
            return wo.GetProperty(PropertyBool.IsCaptureCrystal) ?? false;
        }
        
        public static bool IsCapturedAppearance(WorldObject wo)
        {
            return wo.GetProperty(PropertyBool.IsCapturedAppearance) ?? false;
        }

        /// <summary>
        /// Checks if a lens is a Resonance Lens (second-chance shiny capture)
        /// </summary>
        public static bool IsResonanceLens(WorldObject wo)
        {
            if (!IsCaptureCrystal(wo)) return false;
            var tier = wo.GetProperty(PropertyInt.CrystalTier);
            return tier == TIER_RESONANCE || (wo.GetProperty(PropertyBool.IsResonanceLens) ?? false);
        }

        /// <summary>
        /// Checks if a lens is Asheron's Lens (guaranteed capture)
        /// </summary>
        public static bool IsAsheronsLens(WorldObject wo)
        {
            if (!IsCaptureCrystal(wo)) return false;
            var tier = wo.GetProperty(PropertyInt.CrystalTier);
            return tier == TIER_ASHERONS || (wo.GetProperty(PropertyBool.IsGuaranteedCaptureLens) ?? false);
        }

        /// <summary>
        /// Checks if a player has a pending failed shiny capture that can be retried with a Resonance Lens.
        /// Requires both WCID and Guid to be present (same as ValidateResonanceLensUse).
        /// </summary>
        public static bool HasFailedShinyCaptureState(Player player)
        {
            var wcid = player.GetProperty(PropertyInt.FailedShinyCaptureWCID);
            var guid = player.GetProperty(PropertyInt.FailedShinyCaptureGuid);
            return wcid.HasValue && guid.HasValue;
        }
    }
}
