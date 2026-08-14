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
            // generic type check only for confirmed valid pairings — not for any potency tool on any target.
            var skipTargetTypeCheck = false;
            if (sourceItem.WeenieClassId == PetPotency.EssenceResidueWcid && target is PetDevice)
                skipTargetTypeCheck = true;
            else if (sourceItem.WeenieClassId == PetPotency.EssenceResonatorWcid && PetPotency.IsSalvageableCapturedEssence(target))
                skipTargetTypeCheck = true;
            else if (sourceItem.WeenieClassId == 98760399 && target is PetDevice)
                skipTargetTypeCheck = true;
            else if (sourceItem.WeenieClassId == 98760400 && target is PetDevice)
                skipTargetTypeCheck = true;
            else if (sourceItem.WeenieClassId == 98760401 && target is PetDevice)
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

            if (sourceItem.WeenieClassId == 98760399) // Neutering Kit
            {
                if (target is not PetDevice petDevice)
                {
                    SendTransientError("This tool can only be used on combat pet devices.");
                    SendUseDoneEvent();
                    return;
                }

                if (petDevice.GetProperty(global::ACE.Entity.Enum.Properties.PropertyBool.PetNeutered) == true)
                {
                    SendTransientError("This pet is already spayed/neutered.");
                    SendUseDoneEvent();
                    return;
                }

                petDevice.SetProperty(global::ACE.Entity.Enum.Properties.PropertyBool.PetNeutered, true);
                petDevice.ChangesDetected = true;
                petDevice.SaveBiotaToDatabase();

                if (!TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    SendTransientError("Failed to consume neutering kit tool.");
                    SendUseDoneEvent();
                    return;
                }

                PlayParticleEffect(PlayScript.AttribDownRed, target.Guid);
                SendMessage($"You have permanently spayed/neutered {petDevice.Name}. It can no longer be used for breeding!");
                SendUseDoneEvent();
                return;
            }

            if (sourceItem.WeenieClassId == 98760400) // Pet Tailoring Kit (Base Tool)
            {
                if (target is not PetDevice sourcePet)
                {
                    SendTransientError("This tool can only be used to extract skins from combat pet devices.");
                    SendUseDoneEvent();
                    return;
                }

                // Verify items can be consumed prior to creation
                if (!TryConsumeFromInventoryWithNetworking(sourcePet, 1) || !TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    SendTransientError("Failed to consume tailoring tool or source pet device.");
                    SendUseDoneEvent();
                    return;
                }

                // Create the Intermediate Kit
                var intermediateKit = global::ACE.Server.Factories.WorldObjectFactory.CreateNewWorldObject(98760401) as WorldObject;
                if (intermediateKit == null)
                {
                    SendTransientError("Failed to create Intermediate Pet Tailoring Kit.");
                    SendUseDoneEvent();
                    return;
                }

                // Copy visual override properties from sourcePet to intermediateKit
                intermediateKit.SetProperty(PropertyDataId.VisualOverrideSetup, sourcePet.VisualOverrideSetup ?? 0);
                intermediateKit.SetProperty(PropertyDataId.VisualOverrideMotionTable, sourcePet.VisualOverrideMotionTable ?? 0);
                intermediateKit.SetProperty(PropertyDataId.VisualOverrideCombatTable, sourcePet.VisualOverrideCombatTable ?? 0);
                intermediateKit.SetProperty(PropertyDataId.VisualOverrideSoundTable, sourcePet.VisualOverrideSoundTable ?? 0);
                intermediateKit.SetProperty(PropertyDataId.VisualOverridePaletteBase, sourcePet.VisualOverridePaletteBase ?? 0);
                intermediateKit.SetProperty(PropertyDataId.VisualOverrideClothingBase, sourcePet.VisualOverrideClothingBase ?? 0);
                intermediateKit.SetProperty(PropertyFloat.VisualOverrideScale, sourcePet.VisualOverrideScale ?? 0.0);
                intermediateKit.SetProperty(PropertyString.CapturedCreatureName, sourcePet.VisualOverrideName ?? "");
                intermediateKit.SetProperty(PropertyInt.CapturedCreatureVariant, sourcePet.VisualOverrideCreatureVariant ?? 0);
                intermediateKit.SetProperty(PropertyInt.CapturedCreatureType, sourcePet.VisualOverrideCreatureType ?? 0);

                var capAnim = sourcePet.GetProperty(PropertyString.CapturedObjDescAnimParts);
                if (!string.IsNullOrEmpty(capAnim)) intermediateKit.SetProperty(PropertyString.CapturedObjDescAnimParts, capAnim);

                var capPals = sourcePet.GetProperty(PropertyString.CapturedObjDescPalettes);
                if (!string.IsNullOrEmpty(capPals)) intermediateKit.SetProperty(PropertyString.CapturedObjDescPalettes, capPals);

                var capTex = sourcePet.GetProperty(PropertyString.CapturedObjDescTextures);
                if (!string.IsNullOrEmpty(capTex)) intermediateKit.SetProperty(PropertyString.CapturedObjDescTextures, capTex);

                var capWcid = sourcePet.GetProperty(PropertyInt.CapturedCreatureWCID);
                if (capWcid.HasValue) intermediateKit.SetProperty(PropertyInt.CapturedCreatureWCID, capWcid.Value);

                var capDmg = sourcePet.GetProperty(PropertyInt.CapturedSourceDamageType);
                if (capDmg.HasValue) intermediateKit.SetProperty(PropertyInt.CapturedSourceDamageType, capDmg.Value);

                var capPaletteTemplate = sourcePet.VisualOverridePaletteTemplate;
                if (capPaletteTemplate.HasValue) intermediateKit.SetProperty(PropertyInt.VisualOverridePaletteTemplate, capPaletteTemplate.Value);

                var capShade = sourcePet.VisualOverrideShade;
                if (capShade.HasValue) intermediateKit.SetProperty(PropertyFloat.VisualOverrideShade, capShade.Value);

                // Set dynamic display name on the intermediate kit
                var creatureName = sourcePet.VisualOverrideName ?? sourcePet.Name;
                intermediateKit.Name = $"Pet Tailoring Kit ({creatureName})";

                if (TryCreateInInventoryWithNetworking(intermediateKit))
                {
                    PlayParticleEffect(PlayScript.AttribDownRed, target.Guid);
                    SendMessage($"You extract the visual skin from {sourcePet.Name} and store it in the kit. The source pet is consumed.");
                }
                else
                {
                    SendTransientError("Inventory full. Could not extract pet skin.");
                    intermediateKit.Destroy();
                }

                SendUseDoneEvent();
                return;
            }

            if (sourceItem.WeenieClassId == 98760401) // Intermediate Pet Tailoring Kit
            {
                if (target is not PetDevice targetPet)
                {
                    SendTransientError("This tool can only be used on combat pet devices.");
                    SendUseDoneEvent();
                    return;
                }

                if (!TryConsumeFromInventoryWithNetworking(sourceItem, 1))
                {
                    SendTransientError("Failed to consume tailoring skin kit.");
                    SendUseDoneEvent();
                    return;
                }

                // Copy visual properties from intermediateKit to targetPet
                var setupVal = sourceItem.GetProperty(PropertyDataId.VisualOverrideSetup);
                targetPet.VisualOverrideSetup = setupVal > 0 ? setupVal : null;

                var motionVal = sourceItem.GetProperty(PropertyDataId.VisualOverrideMotionTable);
                targetPet.VisualOverrideMotionTable = motionVal > 0 ? motionVal : null;

                var combatVal = sourceItem.GetProperty(PropertyDataId.VisualOverrideCombatTable);
                targetPet.VisualOverrideCombatTable = combatVal > 0 ? combatVal : null;

                var soundVal = sourceItem.GetProperty(PropertyDataId.VisualOverrideSoundTable);
                targetPet.VisualOverrideSoundTable = soundVal > 0 ? soundVal : null;

                var palBaseVal = sourceItem.GetProperty(PropertyDataId.VisualOverridePaletteBase);
                targetPet.VisualOverridePaletteBase = palBaseVal > 0 ? palBaseVal : null;

                var clothBaseVal = sourceItem.GetProperty(PropertyDataId.VisualOverrideClothingBase);
                targetPet.VisualOverrideClothingBase = clothBaseVal > 0 ? clothBaseVal : null;

                var scaleVal = sourceItem.GetProperty(PropertyFloat.VisualOverrideScale);
                targetPet.VisualOverrideScale = scaleVal > 0.0 ? scaleVal : null;

                var nameVal = sourceItem.GetProperty(PropertyString.CapturedCreatureName);
                targetPet.VisualOverrideName = !string.IsNullOrEmpty(nameVal) ? nameVal : null;

                var variantVal = sourceItem.GetProperty(PropertyInt.CapturedCreatureVariant);
                targetPet.VisualOverrideCreatureVariant = variantVal > 0 ? variantVal : null;

                var typeVal = sourceItem.GetProperty(PropertyInt.CapturedCreatureType);
                targetPet.VisualOverrideCreatureType = typeVal > 0 ? typeVal : null;

                var capAnim = sourceItem.GetProperty(PropertyString.CapturedObjDescAnimParts);
                if (!string.IsNullOrEmpty(capAnim)) targetPet.SetProperty(PropertyString.CapturedObjDescAnimParts, capAnim);
                else targetPet.RemoveProperty(PropertyString.CapturedObjDescAnimParts);

                var capPals = sourceItem.GetProperty(PropertyString.CapturedObjDescPalettes);
                if (!string.IsNullOrEmpty(capPals)) targetPet.SetProperty(PropertyString.CapturedObjDescPalettes, capPals);
                else targetPet.RemoveProperty(PropertyString.CapturedObjDescPalettes);

                var capTex = sourceItem.GetProperty(PropertyString.CapturedObjDescTextures);
                if (!string.IsNullOrEmpty(capTex)) targetPet.SetProperty(PropertyString.CapturedObjDescTextures, capTex);
                else targetPet.RemoveProperty(PropertyString.CapturedObjDescTextures);

                var capWcid = sourceItem.GetProperty(PropertyInt.CapturedCreatureWCID);
                if (capWcid.HasValue) targetPet.SetProperty(PropertyInt.CapturedCreatureWCID, capWcid.Value);
                else targetPet.RemoveProperty(PropertyInt.CapturedCreatureWCID);

                var capDmg = sourceItem.GetProperty(PropertyInt.CapturedSourceDamageType);
                if (capDmg.HasValue) targetPet.SetProperty(PropertyInt.CapturedSourceDamageType, capDmg.Value);
                else targetPet.RemoveProperty(PropertyInt.CapturedSourceDamageType);

                var capPaletteTemplate = sourceItem.GetProperty(PropertyInt.VisualOverridePaletteTemplate);
                if (capPaletteTemplate.HasValue) targetPet.VisualOverridePaletteTemplate = capPaletteTemplate.Value;
                else targetPet.VisualOverridePaletteTemplate = null;

                var capShade = sourceItem.GetProperty(PropertyFloat.VisualOverrideShade);
                if (capShade.HasValue) targetPet.VisualOverrideShade = capShade.Value;
                else targetPet.VisualOverrideShade = null;

                // Rebuild target pet name
                var baseName = targetPet.Name ?? "";
                var index = baseName.IndexOf(" Essence");
                var baseClean = index >= 0 ? baseName.Substring(0, index + 8) : baseName;
                var rebuiltName = PetDevice.BuildDisplayNameAfterCaptureApply(baseClean, null, targetPet.VisualOverrideName);
                if (!string.IsNullOrEmpty(rebuiltName))
                    targetPet.Name = rebuiltName;

                targetPet.ChangesDetected = true;
                targetPet.SaveBiotaToDatabase();

                PlayParticleEffect(PlayScript.EnchantUpPurple, target.Guid);
                SendMessage($"You successfully tailored the appearance onto {targetPet.Name}!");
                TryConsumeFromInventoryWithNetworking(sourceItem, 1);

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
            var effSelf = PrestigeManager.GetEffectiveVariationForVisibility(this);
            var effOther = PrestigeManager.GetEffectiveVariationForVisibility(other);
            var sameVar = PrestigeManager.SameVariationForVisibility(effSelf, effOther);
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

            if (!ServerConfig.prestige_interaction_diag_verbose.Value)
                return;

            log.Warn(
                $"[PrestigeInteraction] {source}: actor={Name}({Guid.Full:X8}) other={other.Name}({other.Guid.Full:X8}) targetGuid={targetGuidFull:X8} " +
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
