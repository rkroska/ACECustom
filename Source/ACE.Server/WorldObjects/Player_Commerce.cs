using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        // player buying items from vendor

        /// <summary>
        /// Called when player clicks 'Buy Items'
        /// </summary>
        public void HandleActionBuyItem(uint vendorGuid, List<ItemProfile> items)
        {
            if (IsBusy)
            {
                SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            if (IsTrading)
            {
                SendUseDoneEvent(WeenieError.CantDoThatTradeInProgress);
                return;
            }

            var vendor = CurrentLandblock?.GetObject(vendorGuid) as Vendor;

            if (vendor == null)
            {
                SendUseDoneEvent(WeenieError.NoObject);
                return;
            }

            // if this succeeds, it automatically calls player.FinalizeBuyTransaction()
            vendor.BuyItems_ValidateTransaction(items, this);

            SendUseDoneEvent();
        }

        private static readonly uint coinStackWcid = (uint)ACE.Entity.Enum.WeenieClassName.W_COINSTACK_CLASS;

        /// <summary>
        /// Vendor has validated the transactions and sent a list of items for processing.
        /// </summary>
        public void FinalizeBuyTransaction(Vendor vendor, List<WorldObject> genericItems, List<WorldObject> uniqueItems, uint cost)
        {
            // transaction has been validated by this point

            var currencyWcid = vendor.AlternateCurrency ?? coinStackWcid;

            ConsumeCurrency(currencyWcid, cost);

            vendor.MoneyIncome += (int)cost;

            foreach (var item in genericItems)
            {
                var service = item.GetProperty(PropertyBool.VendorService) ?? false;

                if (!service)
                {
                    // errors shouldn't be possible here, since the items were pre-validated, but just in case...
                    if (!TryCreateInInventoryWithNetworking(item))
                    {
                        log.Error($"[VENDOR] {Name}.FinalizeBuyTransaction({vendor.Name}) - couldn't add {item.Name} ({item.Guid}) to player inventory after validation, this shouldn't happen!");

                        item.Destroy();  // cleanup for guid manager
                    }

                    vendor.NumItemsSold++;
                }
                else
                    vendor.ApplyService(item, this);
            }

            foreach (var item in uniqueItems)
            {
                if (TryCreateInInventoryWithNetworking(item))
                {
                    vendor.UniqueItemsForSale.Remove(item.Guid);

                    // this was only for when the unique item was sold to the vendor,
                    // to determine when the item should rot on the vendor. it gets removed now
                    item.SoldTimestamp = null;

                    vendor.NumItemsSold++;
                }
                else
                    log.Error($"[VENDOR] {Name}.FinalizeBuyTransaction({vendor.Name}) - couldn't add {item.Name} ({item.Guid}) to player inventory after validation, this shouldn't happen!");
            }

            Session.Network.EnqueueSend(new GameMessageSound(Guid, Sound.PickUpItem));

            if (ServerConfig.player_receive_immediate_save.Value)
                RushNextPlayerSave(5);

            vendor.ApproachVendor(this, VendorType.Buy, /*justSpentAmount=*/cost);
        }

        // player selling items to vendor

        // whereas most of the logic for buying items is in vendor,
        // most of the logic for selling items is located in player_commerce
        // the functions have similar structure, just in different places
        // there's really no point in there being differences in location,
        // and it might be better to move them all to vendor for consistency.

        /// <summary>
        /// Called when player clicks 'Sell Items'
        /// </summary>
        public void HandleActionSellItem(uint vendorGuid, List<ItemProfile> itemProfiles)
        {
            if (IsBusy)
            {
                SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            var vendor = CurrentLandblock?.GetObject(vendorGuid) as Vendor;

            if (vendor == null)
            {
                SendUseDoneEvent(WeenieError.NoObject);
                return;
            }

            // perform validations on requested sell items,
            // and filter to list of validated items

            // one difference between sell and buy is here.
            // when an itemProfile is invalid in buy, the entire transaction is failed immediately.
            // when an itemProfile is invalid in sell, we just remove the invalid itemProfiles, and continue onwards
            // this might not be the best for safety, and it's a tradeoff between safety and player convenience
            // should we fail the entire transaction (similar to buy), if there are any invalids in the transaction request?

            var sellList = VerifySellItems(itemProfiles, vendor);

            if (sellList.Count == 0)
            {
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, Guid.Full));
                SendUseDoneEvent();
                return;
            }

            // calculate pyreals to receive
            var payoutCoinAmount = vendor.CalculatePayoutCoinAmount(sellList);

            if (payoutCoinAmount < 0)
            {
                log.Warn($"[VENDOR] {Name} (0x({Guid}) tried to sell something to {vendor.Name} (0x{vendor.Guid}) resulting in a payout of {payoutCoinAmount} pyreals.");

                SendTransientError("Transaction failed.");
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, Guid.Full));

                SendUseDoneEvent();

                return;
            }

            vendor.MoneyOutflow += payoutCoinAmount;

            // remove sell items from player inventory
            foreach (var item in sellList.Values)
            {
                if (TryRemoveFromInventoryWithNetworking(item.Guid, out _, RemoveFromInventoryAction.SellItem) || TryDequipObjectWithNetworking(item.Guid, out _, DequipObjectAction.SellItem))
                    Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, vendor));
                else
                    log.WarnFormat("[VENDOR] Item 0x{0:X8}:{1} for player {2} not found in HandleActionSellItem.", item.Guid.Full, item.Name, Name); // This shouldn't happen
            }

            // send the list of items to the vendor
            // for the vendor to determine what to do with each item (resell, destroy)
            vendor.ProcessItemsForPurchase(this, sellList);

            // Deposit to bank.
            BankedPyreals ??= 0;
            BankedPyreals += payoutCoinAmount;
            Session.Network.EnqueueSend(new GameMessageSystemChat($"Sold items for {payoutCoinAmount:N0} pyreals (deposited to bank).", ChatMessageType.System));
            UpdateCoinValue();

            Session.Network.EnqueueSend(new GameMessageSound(Guid, Sound.PickUpItem));

            SendUseDoneEvent();
        }

        /// <summary>
        /// Filters the list of ItemProfiles the player is attempting to sell to the vendor
        /// to the list of verified WorldObjects in the player's inventory w/ validations
        /// </summary>
        private Dictionary<uint, WorldObject> VerifySellItems(List<ItemProfile> sellItems, Vendor vendor)
        {
            var allPossessions = GetAllPossessions().ToDictionary(i => i.Guid.Full, i => i);

            var acceptedItemTypes = (ItemType)(vendor.MerchandiseItemTypes ?? 0);

            var verified = new Dictionary<uint, WorldObject>();

            foreach (var sellItem in sellItems)
            {
                if (!allPossessions.TryGetValue(sellItem.ObjectGuid, out var wo))
                {
                    log.Warn($"[VENDOR] {Name} tried to sell item {sellItem.ObjectGuid:X8} not in their inventory to {vendor.Name}");
                    continue;
                }

                // verify item profile (unique guids, amount)
                if (verified.ContainsKey(wo.Guid.Full))
                {
                    log.Warn($"[VENDOR] {Name} tried to sell duplicate item {wo.Name} ({wo.Guid}) to {vendor.Name}");
                    continue;
                }

                if (!sellItem.IsValidAmount)
                {
                    log.Warn($"[VENDOR] {Name} tried to sell {sellItem.Amount}x {wo.Name} ({wo.Guid}) to {vendor.Name}");
                    continue;
                }

                if (sellItem.Amount > (wo.StackSize ?? 1))
                {
                    log.Warn($"[VENDOR] {Name} tried to sell {sellItem.Amount}x {wo.Name} ({wo.Guid}) to {vendor.Name}, but they only have {wo.StackSize ?? 1}x");
                    continue;
                }

                // verify wo / vendor / player properties
                if ((acceptedItemTypes & wo.ItemType) == 0 || !wo.IsSellable || wo.Retained)
                {
                    var itemName = (wo.StackSize ?? 1) > 1 ? wo.GetPluralName() : wo.Name;
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"The {itemName} is unsellable.")); // retail message did not include item name, leaving in that for now.
                    continue;
                }

                if (wo.Value < 1)
                {
                    var itemName = (wo.StackSize ?? 1) > 1 ? wo.GetPluralName() : wo.Name;
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"The {itemName} has no value and cannot be sold.")); // retail message did not include item name, leaving in that for now.
                    continue;
                }

                if (IsTrading && wo.IsBeingTradedOrContainsItemBeingTraded(ItemsInTradeWindow))
                {
                    var itemName = (wo.StackSize ?? 1) > 1 ? wo.GetPluralName() : wo.Name;
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"You cannot sell that! The {itemName} is currently being traded.")); // custom message?
                    continue;
                }

                if (wo is Container container && container.Inventory.Count > 0)
                {
                    var itemName = (wo.StackSize ?? 1) > 1 ? wo.GetPluralName() : wo.Name;
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"You cannot sell that! The {itemName} must be empty.")); // custom message?
                    continue;
                }

                verified.Add(wo.Guid.Full, wo);
            }

            return verified;
        }

        private void UpdateCoinValue(bool sendUpdateMessageIfChanged = true)
        {
            long coins = 0;

            foreach (var coinStack in GetInventoryItemsOfTypeWeenieType(WeenieType.Coin))
                coins += coinStack.Value ?? 0;

            // Include banked pyreals for total wealth display
            coins += BankedPyreals ?? 0;

            // Clamp for safe display as int
            if (coins > int.MaxValue)
                coins = int.MaxValue;

            if (sendUpdateMessageIfChanged && CoinValue == (int)coins)
                sendUpdateMessageIfChanged = false;

            CoinValue = (int)coins;

            if (sendUpdateMessageIfChanged)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CoinValue, CoinValue ?? 0));
        }

        private void ConsumeCurrency(uint currentWcid, uint amount)
        {
            if (currentWcid == 0 || amount == 0) return;

            long inventoryAmount = 0;

            if (currentWcid == coinStackWcid)
            {
                foreach (var coinStack in GetInventoryItemsOfWCID(coinStackWcid)) inventoryAmount += coinStack.Value ?? 0;
                long takeFromInventory = Math.Min(amount, inventoryAmount);
                long takeFromBank = amount - takeFromInventory;

                if (takeFromInventory > 0)
                    TryConsumeFromInventoryWithNetworking(currentWcid, (int)takeFromInventory);

                if (takeFromBank > 0)
                {
                    BankedPyreals ??= 0;
                    BankedPyreals -= takeFromBank;
                    UpdateCoinValue();
                }
            }
            else if (currentWcid == 20630) // MMD Note
            {
                inventoryAmount = GetNumInventoryItemsOfWCID(currentWcid);
                long takeFromInventory = Math.Min(amount, inventoryAmount);
                long takeFromBank = amount - takeFromInventory;

                if (takeFromInventory > 0)
                    TryConsumeFromInventoryWithNetworking(currentWcid, (int)takeFromInventory);

                if (takeFromBank > 0)
                {
                    // Each MMD is worth 250,000 pyreals
                    long pyrealsNeeded = takeFromBank * 250000;
                    BankedPyreals ??= 0;
                    BankedPyreals -= pyrealsNeeded;
                    UpdateCoinValue();
                }
            }
            else if (currentWcid == 300004) // Enlightened Coin
            {
                inventoryAmount = GetNumInventoryItemsOfWCID(currentWcid);
                long takeFromInventory = Math.Min(amount, inventoryAmount);
                long takeFromBank = amount - takeFromInventory;

                if (takeFromInventory > 0)
                    TryConsumeFromInventoryWithNetworking(currentWcid, (int)takeFromInventory);

                if (takeFromBank > 0)
                {
                    BankedEnlightenedCoins ??= 0;
                    BankedEnlightenedCoins -= takeFromBank;
                }
            }
            else if (currentWcid == 300003) // Weakly Enlightened Coin
            {
                inventoryAmount = GetNumInventoryItemsOfWCID(currentWcid);
                long takeFromInventory = Math.Min(amount, inventoryAmount);
                long takeFromBank = amount - takeFromInventory;

                if (takeFromInventory > 0)
                    TryConsumeFromInventoryWithNetworking(currentWcid, (int)takeFromInventory);

                if (takeFromBank > 0)
                {
                    BankedWeaklyEnlightenedCoins ??= 0;
                    BankedWeaklyEnlightenedCoins -= takeFromBank;
                }
            }
            else
            {
                // Anything else just gets consumed from inventory.
                TryConsumeFromInventoryWithNetworking(currentWcid, (int)amount);
            }
        }
    }
}
