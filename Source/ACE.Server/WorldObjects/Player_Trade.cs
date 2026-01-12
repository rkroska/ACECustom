using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Entity.Actions;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public HashSet<ObjectGuid> ItemsInTradeWindow = new HashSet<ObjectGuid>();

        public ObjectGuid TradePartner;

        public bool IsTrading { get; private set; }

        private bool TradeAccepted;

        public bool TradeTransferInProgress;

        public void HandleActionOpenTradeNegotiations(uint tradePartnerGuid, bool initiator = false)
        {
            if (IsOlthoiPlayer)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"As a mindless engine of destruction an Olthoi cannot participate in trade negotiations!", ChatMessageType.Magic));
                return;
            }

            var tradePartner = PlayerManager.GetOnlinePlayer(tradePartnerGuid);
            if (tradePartner == null) return;

            //Check to see if potential trading partner is an Olthoi player
            if (initiator && tradePartner.IsOlthoiPlayer)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"The Olthoi's hunger for destruction is too great to understand a request for trade negotiations!", ChatMessageType.Broadcast));
                return;
            }

            //Check to see if partner is not allowing trades
            if (initiator && tradePartner.GetCharacterOption(CharacterOption.IgnoreAllTradeRequests))
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.TradeIgnoringRequests));
                return;
            }

            //Check to see if either party is already part of an in process trade session
            if (IsTrading || tradePartner.IsTrading)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.TradeAlreadyTrading));
                return;
            }

            //Check to see if either party is in combat mode
            if (CombatMode != CombatMode.NonCombat || tradePartner.CombatMode != CombatMode.NonCombat)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.TradeNonCombatMode));
                return;
            }

            //Check to see if trade partner is in range, if so, rotate and move to
            if (initiator)
            {
                CreateMoveToChain(tradePartner, (success) =>
                {
                    if (!success)
                    {
                        Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.TradeMaxDistanceExceeded));
                        return;
                    }

                    Session.Network.EnqueueSend(new GameEventRegisterTrade(Session, Guid, tradePartner.Guid));

                    tradePartner.HandleActionOpenTradeNegotiations(Guid.Full, false);
                });
            }
            else
            {
                IsTrading = true;
                tradePartner.IsTrading = true;
                TradeTransferInProgress = false;
                tradePartner.TradeTransferInProgress = false;

                TradePartner = tradePartner.Guid;
                tradePartner.TradePartner = Guid;

                ItemsInTradeWindow.Clear();
                tradePartner.ItemsInTradeWindow.Clear();

                Session.Network.EnqueueSend(new GameEventRegisterTrade(Session, tradePartner.Guid, tradePartner.Guid));
            }
        }

        public void HandleActionCloseTradeNegotiations(EndTradeReason endTradeReason = EndTradeReason.Normal)
        {
            if (TradeTransferInProgress) return;

            IsTrading = false;
            TradeAccepted = false;
            TradeTransferInProgress = false;
            ItemsInTradeWindow.Clear();
            TradePartner = ObjectGuid.Invalid;

            Session.Network.EnqueueSend(new GameEventCloseTrade(Session, endTradeReason));
            Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.TradeClosed));
        }

        public void HandleActionAddToTrade(uint itemGuid, uint tradeWindowSlotNumber)
        {
            if (TradeTransferInProgress)
                return;

            TradeAccepted = false;

            var target = PlayerManager.GetOnlinePlayer(TradePartner);

            if (target == null || itemGuid == 0)
                return;

            target.TradeAccepted = false;

            WorldObject wo = GetInventoryItem(itemGuid);

            if (wo == null)
            {
                wo = GetEquippedItem(itemGuid);

                if (wo == null)
                    return;
            }

            if (wo.IsAttunedOrContainsAttuned)
            {
                Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, "You cannot trade that!"));
                Session.Network.EnqueueSend(new GameEventTradeFailure(Session, itemGuid, WeenieError.AttunedItem));
                return;
            }

            if (wo.IsUniqueOrContainsUnique && !target.CheckUniques(wo, this))
            {
                // WeenieError.TooManyUniqueItems / WeenieErrorWithString._CannotCarryAnymore?
                Session.Network.EnqueueSend(new GameEventTradeFailure(Session, itemGuid, WeenieError.None));
                return;
            }

            ItemsInTradeWindow.Add(new ObjectGuid(itemGuid));

            Session.Network.EnqueueSend(new GameEventAddToTrade(Session, itemGuid, TradeSide.Self));

            target.AddKnownTradeObj(Guid, wo.Guid);
            target.TrackObject(wo);

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(0.001f);
            actionChain.AddAction(target, ActionType.PlayerTrade_EnqueueSendAddToTrade, () =>
            {
                    target.Session.Network.EnqueueSend(new GameEventAddToTrade(target.Session, itemGuid, TradeSide.Partner));
            });
            actionChain.EnqueueChain();
        }

        public void HandleActionResetTrade(ObjectGuid whoReset)
        {
            if (TradeTransferInProgress)
                return;

            ItemsInTradeWindow.Clear();
            TradeAccepted = false;

            Session.Network.EnqueueSend(new GameEventResetTrade(Session, whoReset));
        }

        public void ClearTradeAcceptance()
        {
            ItemsInTradeWindow.Clear();
            TradeAccepted = false;

            Session.Network.EnqueueSend(new GameEventClearTradeAcceptance(Session));
        }

        public void HandleActionAcceptTrade()
        {
            if (TradeTransferInProgress)
                return;

            TradeAccepted = true;

            Session.Network.EnqueueSend(new GameEventAcceptTrade(Session, Guid));
            Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, "You have accepted the offer"));

            var target = PlayerManager.GetOnlinePlayer(TradePartner);

            if (target == null)
                return;

            target.Session.Network.EnqueueSend(new GameEventAcceptTrade(target.Session, Guid));
            target.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(target.Session, $"{Name} has accepted the offer"));

            if (target.TradeAccepted)
                FinalizeTrade(target);
        }

        private void FinalizeTrade(Player target)
        {
            if (!VerifyTrade_BusyState(target) || !VerifyTrade_Inventory(target))
                return;

            IsBusy = true;
            target.IsBusy = true;

            TradeTransferInProgress = true;
            target.TradeTransferInProgress = true;

            Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, "The items are being traded"));
            target.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(target.Session, "The items are being traded"));

            var tradedItems = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();

            var myEscrow = new List<WorldObject>();
            var targetEscrow = new List<WorldObject>();

            foreach (ObjectGuid itemGuid in ItemsInTradeWindow)
            {
                if (TryRemoveFromInventoryWithNetworking(itemGuid, out var wo, RemoveFromInventoryAction.TradeItem) || TryDequipObjectWithNetworking(itemGuid, out wo, DequipObjectAction.TradeItem))
                {
                    targetEscrow.Add(wo);

                    tradedItems.Add((wo.Biota, wo.BiotaDatabaseLock));
                }
            }

            foreach (ObjectGuid itemGuid in target.ItemsInTradeWindow)
            {
                if (target.TryRemoveFromInventoryWithNetworking(itemGuid, out var wo, RemoveFromInventoryAction.TradeItem) || target.TryDequipObjectWithNetworking(itemGuid, out wo, DequipObjectAction.TradeItem))
                {
                    myEscrow.Add(wo);

                    tradedItems.Add((wo.Biota, wo.BiotaDatabaseLock));
                }
            }

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(0.5f);
            actionChain.AddAction(CurrentLandblock, ActionType.PlayerTrade_FinalizeTrade, () =>
            {
                foreach (var wo in myEscrow)
                    TryCreateInInventoryWithNetworking(wo);

                foreach (var wo in targetEscrow)
                    target.TryCreateInInventoryWithNetworking(wo);

                // Save both players after trade completes
                this.SavePlayerToDatabase(reason: SaveReason.ForcedShortWindow);
                target.SavePlayerToDatabase(reason: SaveReason.ForcedShortWindow);

                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.TradeComplete));
                target.Session.Network.EnqueueSend(new GameEventWeenieError(target.Session, WeenieError.TradeComplete));

                TradeTransferInProgress = false;
                target.TradeTransferInProgress = false;
                        
                IsBusy = false;
                target.IsBusy = false;

                // Cache values for audit logging - check if either party is admin
                bool isAdminTrade = IsAbovePlayerLevel || target.IsAbovePlayerLevel;
                Player adminPlayer = IsAbovePlayerLevel ? this : (target.IsAbovePlayerLevel ? target : null);
                Player otherPlayer = IsAbovePlayerLevel ? target : (target.IsAbovePlayerLevel ? this : null);

                DatabaseManager.Shard.SaveBiotasInParallel(tradedItems, (result) =>
                {
                    // Clear SaveInProgress flags for all traded items on both success and failure
                    // This prevents stuck SaveInProgress flags if the save fails or server restarts
                    var clearFlagsAction = new ACE.Server.Entity.Actions.ActionChain();
                    clearFlagsAction.AddAction(WorldManager.ActionQueue, ActionType.PlayerTrade_ClearFlagsAfterSave, () =>
                    {
                        foreach (var wo in myEscrow)
                        {
                            if (!wo.IsDestroyed)
                            {
                                wo.SaveInProgress = false;
                                wo.SaveStartTime = DateTime.MinValue;
                                wo.SaveServerBootId = null;
                                
                                if (!result)
                                {
                                    wo.ChangesDetected = true;
                                }
                            }
                        }
                        foreach (var wo in targetEscrow)
                        {
                            if (!wo.IsDestroyed)
                            {
                                wo.SaveInProgress = false;
                                wo.SaveStartTime = DateTime.MinValue;
                                wo.SaveServerBootId = null;
                                
                                if (!result)
                                {
                                    wo.ChangesDetected = true;
                                }
                            }
                        }

                        if (!result)
                        {
                            log.Warn($"[TRADE SAVE] Bulk save for trade between {Name} and {target.Name} returned false; SaveInProgress flags cleared to avoid stuck state.");
                        }
                    });
                    clearFlagsAction.EnqueueChain();

                    // Log audit after saves complete to avoid lock issues
                    if (isAdminTrade && adminPlayer != null && otherPlayer != null)
                    {
                        WorldManager.ActionQueue.EnqueueAction(new ActionEventDelegate(ActionType.ControlFlowDelay, () =>
                        {
                            try
                            {
                                // Build item lists for logging - coalesce duplicates
                                var adminGaveItems = new Dictionary<string, int>();
                                var adminReceivedItems = new Dictionary<string, int>();

                                if (IsAbovePlayerLevel)
                                {
                                    // Admin is giving targetEscrow, receiving myEscrow
                                    foreach (var wo in targetEscrow)
                                    {
                                        var stackSize = wo.StackSize ?? 1;
                                        var itemName = wo.GetNameWithMaterial(stackSize);
                                        if (adminGaveItems.ContainsKey(itemName))
                                            adminGaveItems[itemName] += (int)stackSize;
                                        else
                                            adminGaveItems[itemName] = (int)stackSize;
                                    }
                                    foreach (var wo in myEscrow)
                                    {
                                        var stackSize = wo.StackSize ?? 1;
                                        var itemName = wo.GetNameWithMaterial(stackSize);
                                        if (adminReceivedItems.ContainsKey(itemName))
                                            adminReceivedItems[itemName] += (int)stackSize;
                                        else
                                            adminReceivedItems[itemName] = (int)stackSize;
                                    }
                                }
                                else
                                {
                                    // Target is admin, giving myEscrow, receiving targetEscrow
                                    foreach (var wo in myEscrow)
                                    {
                                        var stackSize = wo.StackSize ?? 1;
                                        var itemName = wo.GetNameWithMaterial(stackSize);
                                        if (adminGaveItems.ContainsKey(itemName))
                                            adminGaveItems[itemName] += (int)stackSize;
                                        else
                                            adminGaveItems[itemName] = (int)stackSize;
                                    }
                                    foreach (var wo in targetEscrow)
                                    {
                                        var stackSize = wo.StackSize ?? 1;
                                        var itemName = wo.GetNameWithMaterial(stackSize);
                                        if (adminReceivedItems.ContainsKey(itemName))
                                            adminReceivedItems[itemName] += (int)stackSize;
                                        else
                                            adminReceivedItems[itemName] = (int)stackSize;
                                    }
                                }

                                // Convert dictionaries to formatted string lists
                                var adminGaveItemsList = adminGaveItems.Select(kvp => kvp.Value > 1 ? $"{kvp.Key} X {kvp.Value:N0}" : kvp.Key).ToList();
                                var adminReceivedItemsList = adminReceivedItems.Select(kvp => kvp.Value > 1 ? $"{kvp.Key} X {kvp.Value:N0}" : kvp.Key).ToList();

                                // Build audit log messages - split into multiple if needed
                                var adminName = adminPlayer.Name;
                                var otherName = otherPlayer.Name;
                                const int maxItemsPerMessage = 5; // Limit items per message to avoid overly long messages

                                // Log what admin gave
                                if (adminGaveItemsList.Count > 0)
                                {
                                    for (int i = 0; i < adminGaveItemsList.Count; i += maxItemsPerMessage)
                                    {
                                        var itemsChunk = adminGaveItemsList.Skip(i).Take(maxItemsPerMessage).ToList();
                                        var itemsList = string.Join(", ", itemsChunk);
                                        var message = adminGaveItemsList.Count > maxItemsPerMessage && i > 0
                                            ? $"{adminName} completed trade with {otherName} (0x{otherPlayer.Guid:X8}) - gave (cont.): {itemsList}"
                                            : $"{adminName} completed trade with {otherName} (0x{otherPlayer.Guid:X8}) - gave: {itemsList}";
                                        PlayerManager.BroadcastToAuditChannel(adminPlayer, message);
                                    }
                                }

                                // Log what admin received
                                if (adminReceivedItemsList.Count > 0)
                                {
                                    for (int i = 0; i < adminReceivedItemsList.Count; i += maxItemsPerMessage)
                                    {
                                        var itemsChunk = adminReceivedItemsList.Skip(i).Take(maxItemsPerMessage).ToList();
                                        var itemsList = string.Join(", ", itemsChunk);
                                        var message = adminReceivedItemsList.Count > maxItemsPerMessage && i > 0
                                            ? $"{adminName} completed trade with {otherName} (0x{otherPlayer.Guid:X8}) - received (cont.): {itemsList}"
                                            : $"{adminName} completed trade with {otherName} (0x{otherPlayer.Guid:X8}) - received: {itemsList}";
                                        PlayerManager.BroadcastToAuditChannel(adminPlayer, message);
                                    }
                                }
                                else if (adminGaveItemsList.Count == 0)
                                {
                                    // Trade with no items (shouldn't happen, but log it)
                                    PlayerManager.BroadcastToAuditChannel(adminPlayer, $"{adminName} completed trade with {otherName} (0x{otherPlayer.Guid:X8}) - no items exchanged");
                                }
                            }
                            catch (Exception logEx)
                            {
                                log.Error($"[TRADE] Error logging admin trade: {logEx.Message}");
                            }
                        }));
                    }
                }, this.Guid.ToString() + " : " + target.Guid.ToString());

                // Log the trade
                TransferLogger.LogTrade(this, target, targetEscrow, myEscrow);

                HandleActionResetTrade(Guid);
                target.HandleActionResetTrade(target.Guid);
            });

            actionChain.EnqueueChain();
        }

        private bool GetItemsInTradeWindow(Player player, out List<WorldObject> itemsToBeTraded)
        {
            itemsToBeTraded = null;
            var results = new List<WorldObject>();

            foreach (ObjectGuid itemGuid in player.ItemsInTradeWindow)
            {
                var wo = player.GetInventoryItem(itemGuid); // look in inventory for item

                if (wo == null) // if item is equipped, it won't be found above, so if not found, look in equipped objects
                    wo = player.GetEquippedItem(itemGuid);

                if (wo != null)
                    results.Add(wo);
                else // item was not found in inventory or equipped
                    return false;
            }

            itemsToBeTraded = results;
            return true;
        }

        public void HandleActionDeclineTrade(Session session)
        {
            if (session.Player.TradeTransferInProgress) return;

            session.Player.TradeAccepted = false;

            session.Network.EnqueueSend(new GameEventDeclineTrade(session,session.Player.Guid));
            session.Network.EnqueueSend(new GameEventCommunicationTransientString(session, "Trade confirmation failed..."));
            
            var target = PlayerManager.GetOnlinePlayer(session.Player.TradePartner);

            if (target != null)
            {
                target.Session.Network.EnqueueSend(new GameEventDeclineTrade(target.Session, session.Player.Guid));
                target.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(target.Session, "Trade confirmation failed..."));
            }
        }

        public void HandleActionTradeSwitchToCombatMode(Session session)
        {
            if (session.Player.CombatMode != CombatMode.NonCombat && session.Player.IsTrading)
            {
                var target = PlayerManager.GetOnlinePlayer(session.Player.TradePartner);

                session.Network.EnqueueSend(new GameEventWeenieError(session, WeenieError.TradeNonCombatMode));
                session.Player.HandleActionCloseTradeNegotiations(EndTradeReason.EnteredCombat);

                if (target != null)
                {
                    target.Session.Network.EnqueueSend(new GameEventWeenieError(target.Session, WeenieError.TradeNonCombatMode));
                    target.HandleActionCloseTradeNegotiations(EndTradeReason.EnteredCombat);
                }
            }
        }

        private bool VerifyTrade_BusyState(Player partner)
        {
            if (!IsBusy && !partner.IsBusy)
                return true;

            var selfBusy = "You are too busy to complete the trade!";
            var otherBusy = "Your trading partner is too busy to complete the trade!";

            var selfMsg = IsBusy ? selfBusy : otherBusy;
            var partnerMsg = IsBusy ? otherBusy : selfBusy;

            Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, selfMsg));
            partner.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(partner.Session, partnerMsg));

            ClearTradeAcceptance();
            partner.ClearTradeAcceptance();

            return false;
        }

        private bool VerifyTrade_Inventory(Player partner)
        {
            var selfItemsVerified = GetItemsInTradeWindow(this, out var self_items);
            var partnerItemsVerified = GetItemsInTradeWindow(partner, out var partner_items);

            if (!selfItemsVerified)
            {
                HandleActionDeclineTrade(Session);
                return false;
            }

            if (!partnerItemsVerified)
            {
                partner.HandleActionDeclineTrade(partner.Session);
                return false;
            }

            var playerACanAddToInventory = CanAddToInventory(partner_items, out var selfEncumbered, out var selfPackSpace);
            var playerBCanAddToInventory = partner.CanAddToInventory(self_items, out var partnerEncumbered, out var partnerPackSpace);

            if (playerACanAddToInventory && playerBCanAddToInventory)
                return true;

            var selfReason = "";
            var partnerReason = "";

            if (!playerACanAddToInventory)
            {
                selfReason = "You ";
                partnerReason = "Your trading partner ";

                if (selfEncumbered)
                {
                    selfReason += "are too encumbered to complete the trade!";
                    partnerReason += "is too encumbered to complete the trade!";
                }
                else if (selfPackSpace)
                {
                    selfReason += "do not have enough free slots to complete the trade!";
                    partnerReason += "does not have enough free slots to complete the trade!";
                }
            }
            else if (!playerBCanAddToInventory)
            {
                selfReason = "Your trading partner ";
                partnerReason = "You ";

                if (partnerEncumbered)
                {
                    selfReason += "is too encumbered to complete the trade!";
                    partnerReason += "are too encumbered to complete the trade!";
                }
                else if (partnerPackSpace)
                {
                    selfReason += "does not have enough free slots to complete the trade!";
                    partnerReason += "do not have enough free slots to complete the trade!";
                }
            }

            Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, selfReason));
            partner.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(partner.Session, partnerReason));

            ClearTradeAcceptance();
            partner.ClearTradeAcceptance();

            return false;
        }

        public Dictionary<ObjectGuid, HashSet<ObjectGuid>> KnownTradeObjs = new Dictionary<ObjectGuid, HashSet<ObjectGuid>>();

        public void AddKnownTradeObj(ObjectGuid playerGuid, ObjectGuid itemGuid)
        {
            if (!KnownTradeObjs.TryGetValue(playerGuid, out var knownTradeItems))
            {
                knownTradeItems = new HashSet<ObjectGuid>();
                KnownTradeObjs.Add(playerGuid, knownTradeItems);
            }
            knownTradeItems.Add(itemGuid);
        }

        public Player GetKnownTradeObj(ObjectGuid itemGuid)
        {
            if (KnownTradeObjs.Count() == 0)
                return null;

            PruneKnownTradeObjs();

            foreach (var knownTradeObj in KnownTradeObjs)
            {
                if (knownTradeObj.Value.Contains(itemGuid))
                {
                    var playerGuid = knownTradeObj.Key;
                    var player = ObjMaint.GetKnownObject(playerGuid.Full)?.WeenieObj?.WorldObject as Player;
                    if (player != null && player.Location != null && Location.DistanceTo(player.Location) <= LocalBroadcastRange)
                        return player;
                    else
                        return null;
                }
            }
            return null;
        }

        public void PruneKnownTradeObjs()
        {
            foreach (var playerGuid in KnownTradeObjs.Keys.ToList())
            {
                if (ObjMaint.GetKnownObject(playerGuid.Full) == null)
                    KnownTradeObjs.Remove(playerGuid);
            }
        }
    }
}
