using System;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// This is set by HandleActionUseItem / TryUseItem
        /// </summary>
        public ObjectGuid LastOpenedContainerId { get; set; }

        /// <summary>
        /// This is set by Hook.ActOnUse
        /// </summary>
        public ObjectGuid LasUsedHookId { get; set; }

        /// <summary>
        /// Handles the 'GameAction 0x35 - UseWithTarget' network message
        /// when player double clicks an inventory item resulting in a target indicator
        /// and then clicks another item
        /// </summary>
        public void HandleActionUseWithTarget(uint sourceObjectGuid, uint targetObjectGuid)
        {
            if (PKLogout)
            {
                SendUseDoneEvent(WeenieError.YouHaveBeenInPKBattleTooRecently);
                return;
            }

            StopExistingMoveToChains();

            // source item is always in our possession
            var sourceItem = FindObject(sourceObjectGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out _, out _, out var sourceItemIsEquipped);

            if (sourceItem == null)
            {
                //log.Warn($"{Name}.HandleActionUseWithTarget({sourceObjectGuid:X8}, {targetObjectGuid:X8}): couldn't find {sourceObjectGuid:X8}");
                SendUseDoneEvent();
                return;
            }

            // Resolve the guid to an object that is either in our possession or on the Landblock
            var target = FindObject(targetObjectGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems | SearchLocations.Landblock);

            if (target == null)
            {
                if (PlayerManager.GetOnlinePlayer(targetObjectGuid) is Player otherOnline && otherOnline != this)
                    LogPrestigePlayerInteractionDiagnostics("HandleActionUseWithTarget", targetObjectGuid, otherOnline, null);

                //log.Warn($"{Name}.HandleActionUseWithTarget({sourceObjectGuid:X8}, {targetObjectGuid:X8}): couldn't find {targetObjectGuid:X8}");
                SendUseDoneEvent();
                return;
            }

            // handle objects with built-in spells
            if (sourceItem.SpellDID != null)
            {
                if (!RecipeManager.VerifyUse(this, sourceItem, target))
                {
                    //var spell = new Spell((int)sourceItem.SpellDID);
                    //if (spell != null)
                    //    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"{spell.Name} cannot be cast on {target.Name}."));
                    var usable = sourceItem.ItemUseable ?? Usable.Undef;
                    var action = "";
                    if (usable.HasFlag(Usable.Wielded))
                        action = "wield";
                    else if (usable.HasFlag(Usable.Contained))
                        action = "contain";
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"You must {action} the {sourceItem.Name} to use it."));
                    SendUseDoneEvent();
                    return;
                }
                // check activation requirements
                var result = sourceItem.CheckUseRequirements(this);
                if (!result.Success)
                {
                    if (result.Message != null)
                        Session.Network.EnqueueSend(result.Message);

                    SendUseDoneEvent();
                    return;
                }
                else
                {
                    HandleActionCastTargetedSpell(targetObjectGuid, sourceItem.SpellDID ?? 0, sourceItem);
                    return;
                }
            }

            // handle casters with built-in spells
            //if (sourceItemIsEquipped)
            //{
            //    if (sourceItem.SpellDID != null)
            //    {
            //        // check activation requirements
            //        var result = sourceItem.CheckUseRequirements(this);
            //        if (!result.Success)
            //        {
            //            if (result.Message != null)
            //                Session.Network.EnqueueSend(result.Message);

            //            SendUseDoneEvent();
            //        }
            //        else
            //        {
            //            HandleActionCastTargetedSpell(targetObjectGuid, sourceItem.SpellDID ?? 0, true);
            //            return;
            //        }
            //    }
            //    else
            //    {
            //        SendUseDoneEvent();
            //    }

            //    return;
            //}

            if (IsTrading)
            {
                if (sourceItem.IsBeingTradedOrContainsItemBeingTraded(ItemsInTradeWindow))
                {
                    SendUseDoneEvent(WeenieError.TradeItemBeingTraded);
                    //SendWeenieError(WeenieError.TradeItemBeingTraded);
                    return;
                }
                if (target.IsBeingTradedOrContainsItemBeingTraded(ItemsInTradeWindow))
                {
                    SendUseDoneEvent(WeenieError.TradeItemBeingTraded);
                    //SendWeenieError(WeenieError.TradeItemBeingTraded);
                    return;
                }
            }

            // re-verify client checks
            // Potency tools have non-standard source/target ItemType combinations, so we skip the
            // generic type check only for confirmed valid pairings -- not for any potency tool on any target.
            var skipTargetTypeCheck = false;
            if (sourceItem.WeenieClassId == PetPotency.EssenceResidueWcid && target is PetDevice)
                skipTargetTypeCheck = true;
            else if (sourceItem.WeenieClassId == PetPotency.EssenceResonatorWcid && PetPotency.IsSalvageableCapturedEssence(target))
                skipTargetTypeCheck = true;
            else if (sourceItem.WeenieClassId == ACE.Server.Entity.PetTailoring.NeuteringKitWcid && target is PetDevice)
                skipTargetTypeCheck = true;
            else if (sourceItem.WeenieClassId == ACE.Server.Entity.PetTailoring.TailoringKitWcid && target is PetDevice)
                skipTargetTypeCheck = true;
            else if (sourceItem.WeenieClassId == ACE.Server.Entity.PetTailoring.FilledTailoringKitWcid && target is PetDevice)
                skipTargetTypeCheck = true;
            else if ((sourceItem.WeenieClassId >= 78780250 && sourceItem.WeenieClassId <= 78780255) && target is PetDevice)
                skipTargetTypeCheck = true;
            // 78780256 (Ancestral Gene Re-roller) is reserved and unbuilt, so the serum is matched on its own.
            else if (sourceItem.WeenieClassId == ACE.Server.Services.PetMutationService.MutagenicSerumWcid && target is PetDevice)
                skipTargetTypeCheck = true;

            var sourceTargetType = sourceItem.TargetType ?? ItemType.None;
            var targetItemType = target.ItemType;
            var targetTypeMatch = (sourceTargetType & targetItemType) != ItemType.None;

            if (!skipTargetTypeCheck && !targetTypeMatch)
            {
                // ItemHolder::TargetCompatibleWithObject
                SendTransientError($"Cannot use the {sourceItem.Name} with the {target.Name}");
                SendUseDoneEvent();
                return;
            }

            if (target is PetDevice && target.CurrentLandblock != null)
            {
                SendTransientError("The pet device must be in your inventory.");
                SendUseDoneEvent();
                return;
            }

            if (sourceItem.WeenieClassId == ACE.Server.Entity.PetTailoring.NeuteringKitWcid) // Neutering Kit
            {
                if (target is not PetDevice petDevice)
                {
                    if (PetTrace.Enabled) PetTrace.Neuter(this, sourceItem, target, false, false, "target is not a pet device");
                    SendTransientError("This tool can only be used on combat pet devices.");
                    SendUseDoneEvent();
                    return;
                }

                if (petDevice.GetProperty(global::ACE.Entity.Enum.Properties.PropertyBool.PetNeutered) == true)
                {
                    if (PetTrace.Enabled) PetTrace.Neuter(this, sourceItem, target, true, false, "already neutered");
                    SendTransientError("This pet is already spayed/neutered.");
                    SendUseDoneEvent();
                    return;
                }

                if (!TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    if (PetTrace.Enabled) PetTrace.Neuter(this, sourceItem, target, false, false, "consume failed");
                    SendTransientError("Failed to consume neutering kit tool.");
                    SendUseDoneEvent();
                    return;
                }

                petDevice.SetProperty(global::ACE.Entity.Enum.Properties.PropertyBool.PetNeutered, true);
                petDevice.ChangesDetected = true;
                petDevice.SaveBiotaToDatabase();
                if (PetTrace.Enabled) PetTrace.Neuter(this, sourceItem, target, false, true, null);

                PlayParticleEffect(PlayScript.AttribDownRed, target.Guid);
                SendMessage($"You have permanently spayed/neutered {petDevice.Name}. It can no longer be used for breeding!");
                SendUseDoneEvent();
                return;
            }

            if (sourceItem.WeenieClassId == ACE.Server.Entity.PetTailoring.TailoringKitWcid)
            {
                ACE.Server.Entity.PetTailoring.HandleExtract(this, sourceItem, target);
                SendUseDoneEvent();
                return;
            }

            if (sourceItem.WeenieClassId == ACE.Server.Entity.PetTailoring.FilledTailoringKitWcid)
            {
                ACE.Server.Entity.PetTailoring.HandleApply(this, sourceItem, target);
                SendUseDoneEvent();
                return;
            }

            // Courtship Incense (78780250 - 78780252)
            if (sourceItem.WeenieClassId >= 78780250 && sourceItem.WeenieClassId <= 78780252)
            {
                if (target is not PetDevice petDevice)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetIncenseBonus", null, null, false, "target is not a pet device");
                    SendTransientError("Courtship Incense can only be used on combat pet devices.");
                    SendUseDoneEvent();
                    return;
                }

                if (petDevice.GetProperty(PropertyBool.PetNeutered) == true)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetIncenseBonus", petDevice.GetProperty(PropertyFloat.PetIncenseBonus) ?? 0.0, null, false, "neutered");
                    SendTransientError("A spayed or neutered pet cannot be anointed with Courtship Incense.");
                    SendUseDoneEvent();
                    return;
                }

                var bonus = sourceItem.WeenieClassId switch
                {
                    78780250 => 0.025f, // Lesser: +2.5%
                    78780251 => 0.050f, // Refined: +5.0%
                    78780252 => 0.100f, // Exquisite: +10.0%
                    _ => 0.025f
                };

                var currentBonus = petDevice.GetProperty(PropertyFloat.PetIncenseBonus) ?? 0.0f;
                if (currentBonus >= bonus)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetIncenseBonus", currentBonus, bonus, false, "existing bonus is equal or stronger");
                    SendTransientError($"{petDevice.Name} is already primed with equal or stronger Courtship Incense (+{currentBonus * 100:0.#}%).");
                    SendUseDoneEvent();
                    return;
                }

                if (!TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetIncenseBonus", currentBonus, bonus, false, "consume failed");
                    SendTransientError("Failed to consume Courtship Incense.");
                    SendUseDoneEvent();
                    return;
                }

                petDevice.SetProperty(PropertyFloat.PetIncenseBonus, bonus);
                petDevice.ChangesDetected = true;
                petDevice.SaveBiotaToDatabase();
                if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetIncenseBonus", currentBonus, bonus, true, null);

                PlayParticleEffect(PlayScript.HealthUpRed, target.Guid);
                SendMessage($"You have anointed {petDevice.Name} with {sourceItem.Name}! Its next breeding will grant a +{bonus * 100:0.#}% mutation bonus.");
                SendUseDoneEvent();
                return;
            }

            // Chromatic Catalyst (78780254)
            if (sourceItem.WeenieClassId == 78780254)
            {
                if (target is not PetDevice petDevice)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetChromaticCatalystActive", null, null, false, "target is not a pet device");
                    SendTransientError("The Chromatic Catalyst can only be used on combat pet devices.");
                    SendUseDoneEvent();
                    return;
                }

                if (petDevice.GetProperty(PropertyBool.PetChromaticCatalystActive) == true)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetChromaticCatalystActive", true, true, false, "already active");
                    SendTransientError($"{petDevice.Name} is already infused with a Chromatic Catalyst.");
                    SendUseDoneEvent();
                    return;
                }

                if (!TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetChromaticCatalystActive", false, true, false, "consume failed");
                    SendTransientError("Failed to consume Chromatic Catalyst.");
                    SendUseDoneEvent();
                    return;
                }

                petDevice.SetProperty(PropertyBool.PetChromaticCatalystActive, true);
                petDevice.ChangesDetected = true;
                petDevice.SaveBiotaToDatabase();
                if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetChromaticCatalystActive", false, true, true, null);

                PlayParticleEffect(PlayScript.EnchantUpBlue, target.Guid);
                SendMessage($"You infuse {petDevice.Name} with the Chromatic Catalyst! If a palette mutation occurs on its next breed, it will roll vibrant, high-saturation colors.");
                SendUseDoneEvent();
                return;
            }

            // Nurturing Draught (78780253)
            if (sourceItem.WeenieClassId == 78780253)
            {
                if (target is not PetDevice petDevice)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetMaturityXpMultiplier", null, null, false, "target is not a pet device");
                    SendTransientError("Nurturing Draughts can only be given to combat pet devices.");
                    SendUseDoneEvent();
                    return;
                }

                if (petDevice.GetProperty(PropertyBool.PetIsJuvenile) != true)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetMaturityXpMultiplier", petDevice.GetProperty(PropertyFloat.PetMaturityXpMultiplier) ?? 1.0, null, false, "not juvenile");
                    SendTransientError("Nurturing Draughts can only be given to juvenile combat pets that have not yet reached adulthood.");
                    SendUseDoneEvent();
                    return;
                }

                if ((petDevice.GetProperty(PropertyFloat.PetMaturityXpMultiplier) ?? 1.0f) >= 2.0f)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetMaturityXpMultiplier", petDevice.GetProperty(PropertyFloat.PetMaturityXpMultiplier) ?? 1.0, 2.0, false, "already at 2.0 or more");
                    SendTransientError($"{petDevice.Name} is already under the effects of a Nurturing Draught.");
                    SendUseDoneEvent();
                    return;
                }

                var xpMultBefore = petDevice.GetProperty(PropertyFloat.PetMaturityXpMultiplier) ?? 1.0;

                if (!TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetMaturityXpMultiplier", xpMultBefore, 2.0, false, "consume failed");
                    SendTransientError("Failed to consume Nurturing Draught.");
                    SendUseDoneEvent();
                    return;
                }

                petDevice.SetProperty(PropertyFloat.PetMaturityXpMultiplier, 2.0f);
                petDevice.ChangesDetected = true;
                petDevice.SaveBiotaToDatabase();
                if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetMaturityXpMultiplier", xpMultBefore, 2.0, true, null);

                PlayParticleEffect(PlayScript.HealthUpYellow, target.Guid);
                SendMessage($"You administer the Nurturing Draught to {petDevice.Name}. It now earns 2x maturity kill credit until adulthood!");
                SendUseDoneEvent();
                return;
            }

            // Offering of Subjugation (78780255)
            if (sourceItem.WeenieClassId == 78780255)
            {
                if (target is not PetDevice petDevice)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetGuardianWeakened", null, null, false, "target is not a pet device");
                    SendTransientError("The Offering of Subjugation can only be used on combat pet devices.");
                    SendUseDoneEvent();
                    return;
                }

                if (!ServerConfig.pet_breeding_guardian_enabled.Value)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetGuardianWeakened", petDevice.GetProperty(PropertyBool.PetGuardianWeakened) == true, null, false, "pet_breeding_guardian_enabled is false");
                    SendTransientError("Mating guardians are not enabled on this server, so the Offering of Subjugation would have no effect. It was not consumed.");
                    SendUseDoneEvent();
                    return;
                }

                if (petDevice.GetProperty(PropertyBool.PetGuardianWeakened) == true)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetGuardianWeakened", true, true, false, "already active");
                    SendTransientError($"{petDevice.Name} is already under the effects of an Offering of Subjugation.");
                    SendUseDoneEvent();
                    return;
                }

                if (!TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetGuardianWeakened", false, true, false, "consume failed");
                    SendTransientError("Failed to consume Offering of Subjugation.");
                    SendUseDoneEvent();
                    return;
                }

                petDevice.SetProperty(PropertyBool.PetGuardianWeakened, true);
                petDevice.ChangesDetected = true;
                petDevice.SaveBiotaToDatabase();
                if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, "PetGuardianWeakened", false, true, true, null);

                PlayParticleEffect(PlayScript.EnchantUpRed, target.Guid);
                SendMessage($"You consecrate {petDevice.Name} with the Offering of Subjugation. Its next mating guardian will be swiftly overcome!");
                SendUseDoneEvent();
                return;
            }

            // Mutagenic Serum (78780257): re-rolls a combat pet essence's colour from the master
            // palette pool. Appearance only - stats, mutation counts and potency are untouched.
            if (sourceItem.WeenieClassId == ACE.Server.Services.PetMutationService.MutagenicSerumWcid)
            {
                const string serumProperty = "VisualOverridePaletteTemplate";

                // Any combat pet essence qualifies: captured, looted or bred, juvenile or adult.
                if (target is not PetDevice petDevice || !petDevice.IsCombatPetDevice())
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, serumProperty, null, null, false, "target is not a combat pet essence");
                    SendTransientError("The Mutagenic Serum can only be used on combat pet essences.");
                    SendUseDoneEvent();
                    return;
                }

                // A capture that retextured essentially the whole body hides any palette underneath it,
                // so the serum would roll a colour nobody can see. Refuse before consuming it - but only
                // when the textures really do cover the body. A few replacements leave the rest of the
                // parts tinting normally, and refusing those blocked the serum on most captured essences.
                var visibility = ACE.Server.Services.PetMutationService.GetColourChangeVisibility(petDevice);
                if (visibility.Coverage == ACE.Server.Services.PetMutationService.ColourCoverage.FixedColour)
                {
                    // Most of the model uses full-colour textures that ignore palettes, so the serum would change
                    // the stored colour and nothing would look different. Same refusal, different reason.
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, serumProperty, null, null, false,
                        $"colour fixed: {visibility.FixedColourPercent}% of the model uses full-colour textures");
                    SendTransientError($"{petDevice.Name} cannot visibly change colour: most of its body is drawn with full-colour textures that ignore palettes. The serum was not used.");
                    SendUseDoneEvent();
                    return;
                }
                if (visibility.BlocksColour)
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, serumProperty, null, null, false,
                        $"colour hidden: {visibility.TexturedParts}/{visibility.TotalParts} parts retextured by {visibility.TextureCount} captured textures");
                    SendTransientError($"{petDevice.Name} cannot have its colour changed: its captured appearance retextures {visibility.TexturedParts} of its {visibility.TotalParts} body parts, which cover any colour underneath. The serum was not used.");
                    SendUseDoneEvent();
                    return;
                }

                var paletteBefore = petDevice.VisualOverridePaletteTemplate.HasValue
                    ? PetTrace.Hex((uint)petDevice.VisualOverridePaletteTemplate.Value)
                    : "none";

                // Roll before consuming: an empty pool must refuse without eating the serum.
                if (!ACE.Server.Services.PetMutationService.TryRollMasterPalette(out var newPalette, out var poolIndex, out var poolCount))
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, serumProperty, paletteBefore, null, false, "palette pool is empty");
                    SendTransientError("The Mutagenic Serum has nothing to draw from: the mutation palette pool is empty. It was not consumed.");
                    SendUseDoneEvent();
                    return;
                }
                var paletteAfter = PetTrace.Hex(newPalette);

                if (!TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, serumProperty, paletteBefore, paletteAfter, false, "consume failed");
                    SendTransientError("Failed to consume Mutagenic Serum.");
                    SendUseDoneEvent();
                    return;
                }

                // Same base/template/captured-palette write a bred mutation makes; the device keeps its own setup.
                var recolour = ACE.Server.Services.PetMutationService.ApplyMutationPalette(petDevice, null, newPalette);
                petDevice.ChangesDetected = true;
                petDevice.SaveBiotaToDatabase();

                // If this essence's pet is out, repaint it in place the way @mutate_pet does - but only
                // while it is ticking on this thread's landblock group. World objects belong to their
                // landblock thread; a pet that has strayed into another group waits for the next summon.
                var liveRecoloured = false;
                var pet = CurrentActivePet as CombatPet;
                var liveSummoned = pet != null && !pet.IsDestroyed && pet.SummoningDeviceGuid == petDevice.Guid;
                if (liveSummoned)
                {
                    var petLandblock = pet.CurrentLandblock;
                    var sameGroup = petLandblock != null && CurrentLandblock != null
                        && (!LandblockManager.CurrentlyTickingLandblockGroupsMultiThreaded
                            || petLandblock.CurrentLandblockGroup == CurrentLandblock.CurrentLandblockGroup);
                    if (sameGroup)
                    {
                        ACE.Server.Services.PetMutationService.ApplyMutationPalette(pet, pet.SetupTableId, newPalette);
                        ACE.Server.Services.PetMutationService.ForceClientRedraw(pet);
                        liveRecoloured = true;
                    }
                }

                if (PetTrace.Enabled) PetTrace.ConsumableUse(this, sourceItem, target, serumProperty, paletteBefore, paletteAfter, true, null);
                log.Info($"[MutagenicSerum] {Name} recoloured {petDevice.Name} (0x{petDevice.Guid.Full:X8}): palette {paletteBefore} -> {paletteAfter} " +
                         $"(pool {poolIndex}/{poolCount}), base 0x{recolour.OldPaletteBase:X8} -> 0x{recolour.NewPaletteBase:X8} " +
                         $"(native={recolour.NativeBaseApplied}), capturedPalettesCleared={recolour.CapturedPalettesCleared}, " +
                         $"summoned={liveSummoned}, liveRecoloured={liveRecoloured}.");

                PlayParticleEffect(PlayScript.EnchantUpPurple, target.Guid);

                // Textures present but the capture stored no part list, so whether the colour shows
                // could not be measured. Say so rather than silently taking the serum.
                if (visibility.Coverage == ACE.Server.Services.PetMutationService.ColourCoverage.Unknown)
                    SendMessage($"[WARNING] {petDevice.Name} carries {visibility.TextureCount} captured textures and no part list, so the new colour may be covered where they sit.");

                if (liveRecoloured)
                    SendMessage($"You inject {petDevice.Name} with the Mutagenic Serum. Its colour has changed and your summoned pet has been recoloured.");
                else if (liveSummoned)
                    SendMessage($"You inject {petDevice.Name} with the Mutagenic Serum. Its colour has changed; dismiss and re-summon it to see the new look.");
                else
                    SendMessage($"You inject {petDevice.Name} with the Mutagenic Serum. Its colour has changed; summon it to see the new look.");
                SendUseDoneEvent();
                return;
            }

            if (target.CurrentLandblock != null && target != this)
            {
                // todo: verify target can be used remotely
                // move RecipeManager.VerifyUse logic into base Player_Use
                // this was avoided because i didn't want to deal with the ramifications of random items missing the correct ItemUseable flags,
                // and because there are still some ItemUseable flags with missing logic we haven't quite figured out yet

                if (IsBusy)
                {
                    SendUseDoneEvent(WeenieError.YoureTooBusy);
                    return;
                }

                CreateMoveToChain(target, (success) =>
                {
                    if (success)
                        sourceItem.HandleActionUseOnTarget(this, target);
                    else
                        SendUseDoneEvent();
                });
            }
            else
                sourceItem.HandleActionUseOnTarget(this, target);
        }

        /// <summary>
        /// Handles the 'GameAction 0x36 - UseItem' network message
        /// when player double clicks an item
        /// </summary>
        public void HandleActionUseItem(uint itemGuid)
        {
            if (PKLogout)
            {
                SendUseDoneEvent(WeenieError.YouHaveBeenInPKBattleTooRecently);
                return;
            }

            StopExistingMoveToChains();

            var item = FindObject(itemGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems | SearchLocations.Landblock);

            Player otherOnlineForDiag = null;
            if (PlayerManager.GetOnlinePlayer(itemGuid) is Player op && op != this)
                otherOnlineForDiag = op;

            if (item == null)
            {
                log.Warn($"{itemGuid} not found in {this.Location.LandblockId}, {this.Location.Variation}");
                if (otherOnlineForDiag != null)
                    LogPrestigePlayerInteractionDiagnostics("HandleActionUseItem", itemGuid, otherOnlineForDiag, null);
                log.Debug($"{Name}.HandleActionUseItem({itemGuid:X8}): couldn't find object");
                SendUseDoneEvent();
                return;
            }

            if (otherOnlineForDiag != null)
                LogPrestigePlayerInteractionDiagnostics("HandleActionUseItem", itemGuid, otherOnlineForDiag, item);

            if (IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(ItemsInTradeWindow))
            {
                SendUseDoneEvent(WeenieError.TradeItemBeingTraded);
                //SendWeenieError(WeenieError.TradeItemBeingTraded);
                return;
            }

            if (item.CurrentLandblock != null && !item.Visibility && item.Guid != LastOpenedContainerId)
            {
                if (IsBusy)
                {
                    SendUseDoneEvent(WeenieError.YoureTooBusy);
                    return;
                }

                CreateMoveToChain(item, (success) => TryUseItem(item, success));
            }
            else
                TryUseItem(item);
        }

        public DateTime NextUseTime { get; set; }
        public float LastUseTime { get; set; }

        /// <summary>
        /// Attempts to use an item - checks activation requirements
        /// </summary>
        public void TryUseItem(WorldObject item, bool success = true)
        {
            //Console.WriteLine($"{Name}.TryUseItem({item.Name}, {success})");
            LastUseTime = 0.0f;

            if (success)
                item.OnActivate(this);

            // manually managed
            if (LastUseTime == float.MinValue)
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(LastUseTime);
            actionChain.AddAction(this, ActionType.PlayerUse_SendUseDoneEvent, () => SendUseDoneEvent());
            actionChain.EnqueueChain();

            NextUseTime = DateTime.UtcNow + TimeSpan.FromSeconds(LastUseTime);
        }

        /// <summary>
        /// Sends the GameEventUseDone network message for a player
        /// </summary>
        /// <param name="errorType">An optional error message</param>
        public void SendUseDoneEvent(WeenieError errorType = WeenieError.None)
        {
            Session.Network.EnqueueSend(new GameEventUseDone(Session, errorType));
        }


        /// <summary>
        /// This method processes the Game Action (F7B1) No Longer Viewing Contents (0x0195)
        /// This is raised when we:
        /// - have a container open and open up a second container without closing the first container.
        /// </summary>
        public void HandleActionNoLongerViewingContents(uint objectGuid)
        {
            var container = CurrentLandblock?.GetObject(objectGuid) as Container;

            if (container != null && container.Viewer == Guid.Full)
                container.Close(this);
        }

        public Pet CurrentActivePet { get; set; }

        public void StartBarber()
        {
            BarberActive = true;
            Session.Network.EnqueueSend(new GameEventStartBarber(Session));
        }

        public void ApplyConsumable(MotionCommand useMotion, Action action, float animMod = 1.0f)
        {
            if (ServerConfig.allow_fast_chug.Value && FastTick)
            {
                ApplyConsumableWithAnimationCallbacks(useMotion, action);
                return;
            }
            IsBusy = true;

            var actionChain = new ActionChain();

            // if something other that NonCombat.Ready,
            // manually send this swap
            var prevStance = CurrentMotionState.Stance;

            var animTime = 0.0f;

            if (prevStance != MotionStance.NonCombat)
                animTime = EnqueueMotion_Force(actionChain, MotionStance.NonCombat, MotionCommand.Ready, (MotionCommand)prevStance);

            // start the eat/drink motion
            var useAnimTime = EnqueueMotion_Force(actionChain, MotionStance.NonCombat, useMotion, null, 1.0f, animMod);
            animTime += useAnimTime;

            // apply consumable
            actionChain.AddAction(this, ActionType.PlayerUse_ApplyConsumableAction, action);

            if (animMod == 1.0f)
            {
                // return to ready stance
                animTime += EnqueueMotion_Force(actionChain, MotionStance.NonCombat, MotionCommand.Ready, useMotion);
            }
            else
                actionChain.AddDelaySeconds(useAnimTime * (1.0f - animMod));

            if (prevStance != MotionStance.NonCombat)
                animTime += EnqueueMotion_Force(actionChain, prevStance, MotionCommand.Ready, MotionCommand.NonCombat);

            actionChain.AddAction(this, ActionType.PlayerUse_SetNonBusy, () => { IsBusy = false; });

            actionChain.EnqueueChain();

            LastUseTime = animTime;
        }

        /// <summary>
        /// Fast chugging state variable
        /// </summary>
        public FoodState FoodState { get; set; }

        public void ApplyConsumableWithAnimationCallbacks(MotionCommand useMotion, Action action)
        {
            IsBusy = true;

            var actionChain = new ActionChain();

            // if combat mode, temporarily drop to non-combat
            var prevStance = CurrentMotionState.Stance;

            var animTime = 0.0f;

            if (prevStance != MotionStance.NonCombat)
                animTime = EnqueueMotion_Force(actionChain, MotionStance.NonCombat, MotionCommand.Ready, (MotionCommand)prevStance);

            // start the eat/drink motion
            var useAnimTime = EnqueueMotion_Force(actionChain, MotionStance.NonCombat, useMotion);

            animTime += useAnimTime;

            // the rest is based on animation callback now
            FoodState.StartChugging(useMotion, action, useAnimTime, prevStance);

            actionChain.EnqueueChain();

            // manually managed
            LastUseTime = float.MinValue;
        }

        /// <summary>
        /// Logs landblock / variation / ObjMaint state when the use target is another online player (prestige interaction debugging).
        /// </summary>
        private void LogPrestigePlayerInteractionDiagnostics(string source, uint targetGuidFull, Player other, WorldObject findObjectResult)
        {
            if (other == null || other == this)
                return;

            var og = new ObjectGuid(targetGuidFull);
            var rawOnActorLandblock = CurrentLandblock?.GetObject(og, true, true);
            var effSelf = VariationManager.GetEffectiveVariationForVisibility(this);
            var effOther = VariationManager.GetEffectiveVariationForVisibility(other);
            var sameVar = VariationManager.SameVariationForVisibility(effSelf, effOther);
            var dist = (Location != null && other.Location != null) ? Location.DistanceTo(other.Location) : float.NaN;

            var actorMaint = ObjMaint;
            var otherMaint = other.ObjMaint;
            var aKnowsB = actorMaint != null && actorMaint.KnownObjectsContainsKey(other.Guid.Full);
            var bKnowsA = otherMaint != null && otherMaint.KnownObjectsContainsKey(Guid.Full);
            var aVisibleB = actorMaint != null && actorMaint.VisibleObjectsContainsKey(other.Guid.Full);
            var bVisibleA = otherMaint != null && otherMaint.VisibleObjectsContainsKey(Guid.Full);

            string ResolutionNote()
            {
                if (findObjectResult != null)
                    return $"FindObject ok -> {findObjectResult.Name}";
                if (rawOnActorLandblock == null)
                    return "FindObject=null: CurrentLandblock.GetObject returned null (not on this landblock graph, pending removal, or wrong instance)";
                if (rawOnActorLandblock.Visibility && !Adminvision)
                    return $"FindObject=null: landblock had {rawOnActorLandblock.Name} but PropertyBool.Visibility hides it from use resolution";
                return $"FindObject=null: unexpected; raw object {rawOnActorLandblock.Name} ({rawOnActorLandblock.GetType().Name})";
            }

            var actorCell = Location != null ? $"{Location.Cell:X8}" : "?";
            var otherCell = other.Location != null ? $"{other.Location.Cell:X8}" : "?";
            var actorLb = CurrentLandblock != null ? $"{CurrentLandblock.Id.Landblock:X4}" : "null";
            var otherLb = other.CurrentLandblock != null ? $"{other.CurrentLandblock.Id.Landblock:X4}" : "null";

            if (!ServerConfig.variation_interaction_diag_verbose.Value)
                return;

            log.Warn(
                $"[VariationInteraction] {source}: actor={Name}({Guid.Full:X8}) other={other.Name}({other.Guid.Full:X8}) targetGuid={targetGuidFull:X8} " +
                $"distance={(float.IsNaN(dist) ? "n/a" : dist.ToString("N1"))} " +
                $"actorCell={actorCell} actorLocVar={Location?.Variation?.ToString() ?? "null"} actorLB={actorLb} actorLB.InstanceVar={CurrentLandblock?.VariationId?.ToString() ?? "null"} " +
                $"otherCell={otherCell} otherLocVar={other.Location?.Variation?.ToString() ?? "null"} otherLB={otherLb} otherLB.InstanceVar={other.CurrentLandblock?.VariationId?.ToString() ?? "null"} " +
                $"effVisVar_self={effSelf?.ToString() ?? "null"} effVisVar_other={effOther?.ToString() ?? "null"} sameVariationForVisibility={sameVar} sameLandblockInstance={ReferenceEquals(CurrentLandblock, other.CurrentLandblock)} " +
                $"ObjMaint A:knowsB={aKnowsB} A:visibleB={aVisibleB} B:knowsA={bKnowsA} B:visibleA={bVisibleA} " +
                $"{ResolutionNote()}");
        }

        public void HandleMotionDone_UseConsumable(uint motionID, bool success)
        {
            //Console.WriteLine($"HandleMotionDone_UseConsumable({(MotionCommand)motionID}, {success})");

            if (!FastTick || !FoodState.IsChugging) return;

            if (motionID != (uint)FoodState.UseMotion)
                return;

            // restore state vars
            var animTime = 0.0f;
            var actionChain = new ActionChain();
            var useMotion = FoodState.UseMotion;
            var useAnimTime = FoodState.UseAnimTime;
            var prevStance = FoodState.PrevStance;

            if (motionID != (uint)MotionCommand.Ready)
            {
                if (FoodState.Callback != null)
                {
                    FoodState.Callback();
                    FoodState.Callback = null;
                }

                FoodState.UseMotion = MotionCommand.Ready;

                animTime += EnqueueMotion_Force(actionChain, MotionStance.NonCombat, MotionCommand.Ready, useMotion);
            }
            else
            {
                FoodState.FinishChugging();

                if (prevStance != MotionStance.NonCombat)
                    animTime += EnqueueMotion_Force(actionChain, prevStance, MotionCommand.Ready, MotionCommand.NonCombat);

                actionChain.AddAction(this, ActionType.PlayerUse_SendUseDoneEvent, () =>
                {
                    SendUseDoneEvent();
                    IsBusy = false;
                });
            }

            actionChain.EnqueueChain();
        }
    }
}
