using System;
using System.Linq;
using ACE.Common;
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
    /// </summary>
    public static class MonsterCapture
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// Siphon Lens System - Capture creature appearance
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
            
            // Get crystal tier early for debug bypass checks
            var crystalTier = crystal.GetProperty(PropertyInt.CrystalTier) ?? 2;
            var isDebugLens = crystalTier == 4;
            
            // Health threshold check (must be below 20%) - DEBUG LENS BYPASSES THIS
            var healthPercent = (float)targetCreature.Health.Current / targetCreature.Health.MaxValue;
            if (!isDebugLens && healthPercent > 0.20f)
            {
                player.SendTransientError($"The creature is too strong! Weaken it below 20% health first. (Currently {healthPercent:P0})");
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
            if (targetCreature.IsEnraged)
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
            
            // Calculate success rate
            var successRate = CalculateSuccessRate(player, crystal, targetCreature, healthPercent);
            var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
            var success = roll < successRate;
            
            // Consume crystal (DEBUG LENS tier 4 is never consumed)
            var consumeResult = isDebugLens ? true : player.TryConsumeFromInventoryWithNetworking(crystal, 1);
            
            if (success)
            {
                // Visual effect - Focus Self (white glow)
                targetCreature.EnqueueBroadcast(new GameMessageScript(
                    targetCreature.Guid, PlayScript.VisionUpWhite));
                
                // Make creature invulnerable and stop combat
                targetCreature.Invincible = true;
                targetCreature.IsBusy = true;
                targetCreature.SetCombatMode(CombatMode.NonCombat);
                
                // Create siphoned essence
                var capturedItem = CreateCapturedAppearance(targetCreature, crystal, player);
                if (capturedItem == null)
                {
                    player.SendTransientError("Failed to create siphoned essence!");
                    return;
                }
                
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
                
                
                // Trigger proper creature death after 3 seconds (instead of Destroy)
                // This ensures quest credit, XP, loot, and generator respawns work correctly
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(3.0);
                actionChain.AddAction(targetCreature, ActionType.MonsterCapture_DespawnCreature, () =>
                {
                    if (!targetCreature.IsDestroyed && targetCreature.Health.Current > 0)
                    {
                        // Record the player as the killer in damage history
                        targetCreature.DamageHistory.Add(player, DamageType.Health, (uint)targetCreature.Health.Current);
                        
                        // Trigger proper death sequence (quest credit, XP, loot, respawn)
                        // Die() uses DamageHistory.LastDamager and TopDamager internally
                        targetCreature.OnDeath(targetCreature.DamageHistory.LastDamager, DamageType.Health);
                        targetCreature.Die(); // Uses public overload
                    }
                });
                actionChain.EnqueueChain();
            }
            else
            {
                // Visual effect - dark burst (opposite of success white glow)
                targetCreature.EnqueueBroadcast(new GameMessageScript(
                    targetCreature.Guid, PlayScript.VisionDownBlack));
                
                player.SendMessage($"The siphon failed! The lens shatters!");
                
                // Enrage the creature!

                // Apply "Hardcore" stats: 2x Damage, ~3x Effective Health (0.67 reduction)
                targetCreature.SetProperty(PropertyFloat.EnrageDamageMultiplier, 2.0f);
                targetCreature.SetProperty(PropertyFloat.EnrageDamageReduction, 0.67f);
                
                // Random Enrage Visual
                var visualOptions = new[] { 
                    PlayScript.ShieldUpRed,  // Red Swarm
                    PlayScript.BreatheFlame, // Fire Breath
                    PlayScript.EnchantUpRed, // Magic Ring
                    PlayScript.Create        // Red Explosion
                };
                var chosenVisual = visualOptions[ThreadSafeRandom.Next(0, visualOptions.Length - 1)];
                targetCreature.SetProperty(PropertyInt.EnrageVisualEffect, (int)chosenVisual);

                // Random Enrage Sound
                var soundOptions = new[] {
                    101, // Roar
                    118, // Thunder1
                    107, // DarkLaugh
                    112  // Breathing
                };
                var chosenSound = soundOptions[ThreadSafeRandom.Next(0, soundOptions.Length - 1)];
                targetCreature.SetProperty(PropertyInt.EnrageSound, chosenSound);
                
                // Heal to Full
                targetCreature.Health.Current = targetCreature.Health.MaxValue;
                
                targetCreature.Enrage();
            }
        }
        
        /// <summary>
        /// Calculate success rate based on crystal tier, skill, and creature difficulty
        /// </summary>
        private static float CalculateSuccessRate(Player player, WorldObject crystal, Creature creature, float healthPercent)
        {
            // Get crystal tier (1=Flawed, 2=Pristine, 3=Perfect)
            var crystalTier = crystal.GetProperty(PropertyInt.CrystalTier) ?? 2;
            
            // Base rate and cap by tier
            var (baseRate, tierCap) = crystalTier switch {
                1 => (0.05f, 0.10f), // Flawed: 5% base, 10% max
                2 => (0.10f, 0.20f), // Pristine: 10% base, 20% max
                3 => (0.15f, 0.30f), // Perfect: 15% base, 30% max
                4 => (1.00f, 1.00f), // Debug: 100% capture rate for testing
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
            var difficultyPenalty = Math.Min(levelDiff / 400f, 0.25f);  // 0% at same level, 25% at 100+ levels higher
            
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
            
            // Clear properties from essence template that shouldn't be on the crate
            crate.RemoveProperty(PropertyDataId.IconUnderlay);  // Don't copy underlay from essence
            crate.RemoveProperty(PropertyInt.UiEffects);        // Don't copy purple outline from essence
            
            // Update client immediately using existing UpdateProperty method (same as /setproperty)
            player.UpdateProperty(crate, PropertyDataId.Icon, crate.IconId);
            if (crate.IconOverlayId.HasValue)
                player.UpdateProperty(crate, PropertyDataId.IconOverlay, crate.IconOverlayId.Value);
            
            // Debug logging
            log.Debug($"[MonsterCapture] Applied to Crate: CrateIcon={crate.IconId:X}, CrateOverlay={crate.IconOverlayId}, CrateUnderlay={crate.IconUnderlayId}, EssenceIcon={capturedItem.IconId:X}, EssenceOverlay={capturedItem.IconOverlayId}");
            
            // Save the crate with updated icons
            crate.SaveBiotaToDatabase();
            
            // Consume item
            player.TryConsumeFromInventoryWithNetworking(capturedItem, 1);
            
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
    }
}
