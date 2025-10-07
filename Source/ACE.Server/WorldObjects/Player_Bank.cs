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
using System;
using ACE.Common;
using ACE.Entity.Enum.Properties;
using Google.Protobuf.WellKnownTypes;
using MySqlX.XDevAPI;
using ACE.Server.Command.Handlers;
using ACE.Server.Factories;
using ACE.Server.Entity;
using ACE.Database.Models.Auth;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private readonly object balanceLock = new object();

        /// <summary>
        /// Deposit all pyreals
        /// </summary>
        public  void DepositPyreals()
        {

            var pyrealsList = this.GetInventoryItemsOfWCID(273);
            long cash = 0;

            foreach (var item in pyrealsList)
                cash += (long)item.StackSize;

            DepositPyreals(cash);
        }
        /// <summary>
        /// Deposit specified amount of pyreals
        /// </summary>
        /// <param name="Amount"></param>
        public void DepositPyreals(long Amount)
        {
            if (BankedPyreals == null)
            {
                BankedPyreals = 0;
            }
            lock (balanceLock)
            {
                var pyrealsList = this.GetInventoryItemsOfWCID(273);

                foreach (var item in pyrealsList)
                {
                    if (item.StackSize == 25000 && Amount >= 25000) //full stacks
                    {
                        Amount -= 25000;
                        if (this.TryConsumeFromInventoryWithNetworking(item))
                        {
                            BankedPyreals += 25000;
                        }
                    }
                    else if (Amount >= item.StackSize)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(item, (int)Amount))
                        {
                            Amount -= item.StackSize ?? 0;
                            BankedPyreals += item.StackSize;
                        }
                    }
                }
            }          
        }

        public void DepositLegendaryKeys()
        {
            if (BankedLegendaryKeys == null)
            {
                BankedLegendaryKeys = 0;
            }
            lock (balanceLock)
            {
                //int i = 0;
                var keysList = this.GetInventoryItemsOfWCID(48746);
                foreach (var item in keysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(item))
                    {
                        BankedLegendaryKeys += 1;
                    }
                    else
                    {
                        break;
                    }
                }

                var rtwKeysList = this.GetInventoryItemsOfWCID(52010); //Rynthid Keys
                foreach (var rtw in rtwKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(rtw))
                    {
                        BankedLegendaryKeys += rtw.Structure ?? 5;
                    }
                    else
                    {
                        break;
                    }
                }

                var durKeysList = this.GetInventoryItemsOfWCID(51954); //Durable legendary keys
                foreach (var dur in durKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(dur))
                    {
                        BankedLegendaryKeys += dur.Structure ?? 10;
                    }
                    else
                    {
                        break;
                    }
                }

                var legKeysList = this.GetInventoryItemsOfWCID(48748); //2-use Legendary keys
                foreach (var leg in legKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(leg))
                    {
                        BankedLegendaryKeys += leg.Structure ?? 2;
                    }
                    else
                    {
                        break;
                    }
                }

                var legeventKeysList = this.GetInventoryItemsOfWCID(500010); //25-use Legendary keys
                foreach (var legevent in legeventKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(legevent))
                    {
                        BankedLegendaryKeys += legevent.Structure ?? 25;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        public void DepositMythicalKeys()
        {
            if (BankedMythicalKeys == null)
            {
                BankedMythicalKeys = 0;
            }
            lock (balanceLock)
            {
                //int i = 0;
                var MythkeysList = this.GetInventoryItemsOfWCID(90000104);
                foreach (var item in MythkeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(item))
                    {
                        BankedMythicalKeys += 1;
                    }
                    else
                    {
                        break;
                    }
                }

                var FiveuseKeysList = this.GetInventoryItemsOfWCID(90000108); //5 use Keys
                foreach (var Fiveuse in FiveuseKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(Fiveuse))
                    {
                        BankedMythicalKeys += Fiveuse.Structure ?? 5;
                    }
                    else
                    {
                        break;
                    }
                }


                var durMythKeysList = this.GetInventoryItemsOfWCID(90000109); //Durable Mythical keys
                foreach (var durmyth in durMythKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(durmyth))
                    {
                        BankedMythicalKeys += durmyth.Structure ?? 10;
                    }
                    else
                    {
                        break;
                    }
                }

                var MythKeysList = this.GetInventoryItemsOfWCID(90000107); //2-use Mythical keys
                foreach (var Myth in MythKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(Myth))
                    {
                        BankedMythicalKeys += Myth.Structure ?? 2;
                    }
                    else
                    {
                        break;
                    }
                }

                var MytheventKeysList = this.GetInventoryItemsOfWCID(90000110); //25-use Mythical keys
                foreach (var Mythevent in MytheventKeysList)
                {
                    if (this.TryConsumeFromInventoryWithNetworking(Mythevent))
                    {
                        BankedMythicalKeys += Mythevent.Structure ?? 25;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        public void DepositPeas()
        {
            if (BankedPyreals == null)
            {
                BankedPyreals = 0;
            }
            lock (balanceLock)
            {
                // Single comprehensive scan: Get ALL items from main pack + side containers using iterative DFS
                var allItems = new List<WorldObject>();
                var processedContainers = new HashSet<ObjectGuid>(); // Prevent cycles
                var containerStack = new Stack<Container>();
                
                // Add main inventory
                allItems.AddRange(this.Inventory.Values);
                
                // Initialize stack with top-level side containers
                var sideContainers = this.Inventory.Values.OfType<Container>();
                foreach (var container in sideContainers)
                {
                    if (container != null && !processedContainers.Contains(container.Guid))
                    {
                        containerStack.Push(container);
                        processedContainers.Add(container.Guid);
                    }
                }
                
                // Iterative DFS: traverse arbitrarily nested containers
                while (containerStack.Count > 0)
                {
                    var currentContainer = containerStack.Pop();
                    
                    // Add items from current container
                    if (currentContainer.Inventory?.Values != null)
                    {
                        allItems.AddRange(currentContainer.Inventory.Values);
                        
                        // Find nested containers and add to stack
                        var nestedContainers = currentContainer.Inventory.Values.OfType<Container>();
                        foreach (var nested in nestedContainers)
                        {
                            if (nested != null && !processedContainers.Contains(nested.Guid))
                            {
                                containerStack.Push(nested);
                                processedContainers.Add(nested.Guid);
                            }
                        }
                    }
                }
                
                // Filter in memory - no more scans!
                var pyreals = allItems.Where(i => i.WeenieClassId == 8330);
                var gold = allItems.Where(i => i.WeenieClassId == 8327);
                var silver = allItems.Where(i => i.WeenieClassId == 8331);
                var copper = allItems.Where(i => i.WeenieClassId == 8326);
                
                // Process pyreals
                foreach (var pyreal in pyreals)
                {
                    int val = pyreal.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(pyreal))
                        {
                            BankedPyreals += val;
                        }
                    }
                }

                // Process gold
                foreach (var goldItem in gold)
                {
                    int val = goldItem.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(goldItem))
                        {
                            BankedPyreals += val;
                        }
                    }
                }

                // Process silver
                foreach (var silverItem in silver)
                {
                    int val = silverItem.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(silverItem))
                        {
                            BankedPyreals += val;
                        }
                    }
                }

                // Process copper
                foreach (var copperItem in copper)
                {
                    int val = copperItem.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(copperItem))
                        {
                            BankedPyreals += val;
                        }
                    }
                }
            }
        }

        public void DepositEnlightenedCoins()
        {
            if (BankedEnlightenedCoins == null)
            {
                BankedEnlightenedCoins = 0;
            }
            lock (balanceLock)
            {
                //int i = 0;
                var EnlList = this.GetInventoryItemsOfWCID(300004);
                foreach (var coin in EnlList)
                {
                    int val = coin.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(coin))
                        {
                            BankedEnlightenedCoins += val;
                        }
                    }
                }
            }

            this.SavePlayerToDatabase();
        }

        public void DepositWeaklyEnlightenedCoins()
        {
            if (BankedWeaklyEnlightenedCoins == null)
            {
                BankedWeaklyEnlightenedCoins = 0;
            }
            lock (balanceLock)
            {
                //int i = 0;
                var WeakEnlList = this.GetInventoryItemsOfWCID(300003);
                foreach (var coin in WeakEnlList)
                {
                    int val = coin.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(coin))
                        {
                            BankedWeaklyEnlightenedCoins += val;
                        }
                    }
                }
            }

            this.SavePlayerToDatabase();
        }


        /// <summary>
        /// Deposit all luminance
        /// </summary>
        public void DepositLuminance()
        {
            DepositLuminance(this.AvailableLuminance ?? 0);
        }

        /// <summary>
        /// Deposit specified amount of luminance
        /// </summary>
        /// <param name="Amount"></param>
        public void DepositLuminance(long Amount)
        {
            if (BankedLuminance == null) { BankedLuminance = 0;}
            if (Amount <= this.AvailableLuminance)
            {
                lock(balanceLock)
                {                    
                    this.AvailableLuminance -= Amount;
                    BankedLuminance += Amount;
                }                
            }
            else
            {
                lock(balanceLock)
                {
                    BankedLuminance += this.AvailableLuminance;
                    this.AvailableLuminance = 0;
                }

            }
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableLuminance, this.AvailableLuminance ?? 0));
            //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
        }

        /// <summary>
        /// Deposits all trade notes
        /// </summary>
        public void DepositTradeNotes()
        {
            if (BankedPyreals == null)
            {
                BankedPyreals = 0;
            }
            lock(balanceLock)
            {
                var notesList = this.GetTradeNotes();
                foreach (var note in notesList)
                {
                    int val = note.Value ?? 0;
                    if (val > 0)
                    {
                        if (this.TryConsumeFromInventoryWithNetworking(note))
                        {
                            BankedPyreals += val;
                        }
                    }                    
                }
            }
        }

        /// <summary>
        /// Withdraw luminance from the bank
        /// </summary>
        public void WithdrawLuminance(long Amount)
        {

            if (!this.MaximumLuminance.HasValue || this.MaximumLuminance == 0)
            {
                this.MaximumLuminance = 1500000;
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.MaximumLuminance, this.MaximumLuminance ?? 0));
            }
            

            if (Amount <= this.BankedLuminance)
            {
                lock (balanceLock)
                {
                    this.AvailableLuminance += Amount;
                    BankedLuminance -= Amount;
                }
            }
            else
            {
                lock (balanceLock)
                {
                    this.AvailableLuminance += this.BankedLuminance;
                    BankedLuminance = 0;
                }
            }
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableLuminance, this.AvailableLuminance ?? 0));
            //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
        }

        private bool CreatePyreals(int Amount)
        {
            WorldObject smallCoins = WorldObjectFactory.CreateNewWorldObject(273);
            if (smallCoins == null)
                return false;
            smallCoins.SetStackSize(Amount);
            var itemCreated = this.TryCreateInInventoryWithNetworking(smallCoins);
            if (itemCreated)
            {
                BankedPyreals -= Amount;
                return true;
            }
            return false;
        }

        private bool CreateMMDs(int Amount)
        {
            var woMMDs = WorldObjectFactory.CreateNewWorldObject(20630);
            if (woMMDs == null)
                return false;
            woMMDs.SetStackSize(Amount);
            var itemCreated = this.TryCreateInInventoryWithNetworking(woMMDs);
            if (itemCreated)
            {
                BankedPyreals -= (Amount * 250000);
                return true;
            }

            return false;
        }

        public void WithdrawPyreals(long Amount)
        {
            lock (balanceLock)
            {
                int mmdCount = 0;
                long pyrealAmount = Amount;
                while (pyrealAmount >= 250000)
                {
                    mmdCount++;
                    //this.TryCreateInInventoryWithNetworking(WorldObjectFactory.CreateNewWorldObject(20630));
                    pyrealAmount -= 250000;
                }

                if (mmdCount > 0)
                {
                    var woMMDs = WorldObjectFactory.CreateNewWorldObject(20630);
                    if (woMMDs == null)
                    {
                        return;
                    }
                    int mmdStackSize = (int)woMMDs.MaxStackSize;
                    while (mmdCount >= mmdStackSize)
                    {
                        if (CreateMMDs(mmdStackSize))
                        {
                            mmdCount -= mmdStackSize;
                            Amount -= (mmdStackSize * 250000);
                        }
                        else
                        {
                            return;
                        }
                    }
                    if (mmdCount > 0)
                    {
                        if (CreateMMDs(mmdCount))
                        {
                            Amount -= (mmdCount * 250000);
                            mmdCount = 0;
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                if (Amount > 25000)
                {
                    while (Amount > 25000)
                    {
                        if (CreatePyreals(25000))
                        {
                            Amount -= 25000;
                        }
                        else
                        {
                            return;
                        }
                    }

                }
                if (Amount > 0)
                {
                    CreatePyreals((int)Amount);
                    Amount = 0;
                }
            }            
        }

        private bool CreateLegendaryKey(uint weenieClassId, byte uses)
        {
            WorldObject key = WorldObjectFactory.CreateNewWorldObject(weenieClassId);
            if (key == null)
                return false;
            var itemCreated = this.TryCreateInInventoryWithNetworking(key);
            if (itemCreated)
            {
                BankedLegendaryKeys -= uses;
                return true;
            }
            return false;
        }

        public void WithdrawLegendaryKeys(long Amount)
        {
            long remainingAmount = Amount;
            lock (balanceLock)
            {
                while (remainingAmount >= 25)
                {
                    if (!CreateLegendaryKey(500010, 25)) return; // 25-use
                    remainingAmount -= 25;
                }
                while (remainingAmount >= 10)
                {
                    if (!CreateLegendaryKey(51954, 10)) return; // 10-use
                    remainingAmount -= 10;
                }
                while (remainingAmount > 0)
                {
                    if (!CreateLegendaryKey(48746, 1)) return; // single-use
                    remainingAmount -= 1;
                }
            }
        }

        private bool CreateMythicalKey(uint weenieClassId, byte uses)
        {
            WorldObject key = WorldObjectFactory.CreateNewWorldObject(weenieClassId);
            if (key == null)
                return false;
            var itemCreated = this.TryCreateInInventoryWithNetworking(key);
            if (itemCreated)
            {
                BankedMythicalKeys -= uses;
                return true;
            }
            return false;
        }

        public void WithdrawMythicalKeys(long Amount)
        {
            long remainingAmount = Amount;
            lock (balanceLock)
            {
                while (remainingAmount >= 25)
                {
                    if (!CreateMythicalKey(90000110, 25)) return; // 25-use
                    remainingAmount -= 25;
                }
                while (remainingAmount >= 10)
                {
                    if (!CreateMythicalKey(90000109, 10)) return; // 10-use
                    remainingAmount -= 10;
                }
                while (remainingAmount > 0)
                {
                    if (!CreateMythicalKey(90000104, 1)) return; // single-use
                    remainingAmount -= 1;
                }
            }
        }

        private bool CreateEnlightenedCoins(int Amount)
        {
            WorldObject wo = WorldObjectFactory.CreateNewWorldObject(300004);
            if (wo == null)
                return false;
            wo.SetStackSize(Amount);
            var itemCreated = this.TryCreateInInventoryWithNetworking(wo);
            if (itemCreated)
            {
                BankedEnlightenedCoins -= Amount;
                return true;
            }
            return false;
        }

        public void WithdrawEnlightenedCoins(long Amount)
        {
            lock (balanceLock)
            {
                long remainingAmount = Amount;
                WorldObject templateCoin = WorldObjectFactory.CreateNewWorldObject(300004); // Assume 300004 is the ID for Enlightened Coins
                if (templateCoin == null)
                {
                    return;
                }
                int maxStackSize = (int)templateCoin.MaxStackSize;

                while (remainingAmount >= maxStackSize)
                {
                    if (CreateEnlightenedCoins(maxStackSize))
                    {
                        remainingAmount -= maxStackSize;
                    }
                    else
                    {
                        return;
                    }
                }

                if (remainingAmount > 0)
                {
                    CreateEnlightenedCoins((int)remainingAmount);
                }
            }
        }

        private bool CreateWeaklyEnlightenedCoins(int Amount)
        {
            WorldObject wo = WorldObjectFactory.CreateNewWorldObject(300003);
            if (wo == null)
                return false;
            wo.SetStackSize(Amount);
            var itemCreated = this.TryCreateInInventoryWithNetworking(wo);
            if (itemCreated)
            {
                BankedWeaklyEnlightenedCoins -= Amount;
                return true;
            }
            return false;
        }

        public void WithdrawWeaklyEnlightenedCoins(long Amount)
        {
            lock (balanceLock)
            {
                long remainingAmount = Amount;
                WorldObject templateCoin = WorldObjectFactory.CreateNewWorldObject(300003); // Assume 300004 is the ID for Enlightened Coins
                if (templateCoin == null)
                {
                    return;
                }
                int maxStackSize = (int)templateCoin.MaxStackSize;

                while (remainingAmount >= maxStackSize)
                {
                    if (CreateWeaklyEnlightenedCoins(maxStackSize))
                    {
                        remainingAmount -= maxStackSize;
                    }
                    else
                    {
                        return;
                    }
                }

                if (remainingAmount > 0)
                {
                    CreateWeaklyEnlightenedCoins((int)remainingAmount);
                }
            }
        }

        private IPlayer FindTargetByName(string name)
        {
            return PlayerManager.FindByName(name);
        }

        public bool TransferPyreals(long Amount, string CharacterDestination)
        {
            // Check if sender has sufficient funds
            if ((this.BankedPyreals ?? 0) < Amount)
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Insufficient Pyreals. You have {this.BankedPyreals ?? 0:N0}, need {Amount:N0}", ChatMessageType.System));
                return false;
            }

            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                return false;
            }
            else
            {
                
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    if (offlinePlayer.BankedPyreals == null)
                    {
                        offlinePlayer.BankedPyreals = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedPyreals += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedPyreals == null)
                    {
                        onlinePlayer.BankedPyreals = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedPyreals += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedPyreals, onlinePlayer.BankedPyreals ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Pyreal from {this.Name}", ChatMessageType.System));
                }
                this.BankedPyreals -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedPyreals, this.BankedPyreals ?? 0));
                
                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Pyreals", Amount, TransferLogger.TransferTypeBankTransfer);
                
                return true;
            }
        }

        public bool TransferLegendaryKeys(long Amount, string CharacterDestination)
        {
            // Check if sender has sufficient funds
            if ((this.BankedLegendaryKeys ?? 0) < Amount)
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Insufficient Legendary Keys. You have {this.BankedLegendaryKeys ?? 0:N0}, need {Amount:N0}", ChatMessageType.System));
                return false;
            }

            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    if (offlinePlayer.BankedLegendaryKeys == null)
                    {
                        offlinePlayer.BankedLegendaryKeys = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedLegendaryKeys += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedLegendaryKeys == null)
                    {
                        onlinePlayer.BankedLegendaryKeys = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedLegendaryKeys += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedLegendaryKeys, onlinePlayer.BankedLegendaryKeys ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Legendary Keys from {this.Name}", ChatMessageType.System));
                }
                this.BankedLegendaryKeys -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLegendaryKeys, this.BankedLegendaryKeys ?? 0));
                
                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Legendary Keys", Amount, TransferLogger.TransferTypeBankTransfer);
                
                return true;
            }
        }

        public bool TransferMythicalKeys(long Amount, string CharacterDestination)
        {
            // Check if sender has sufficient funds
            if ((this.BankedMythicalKeys ?? 0) < Amount)
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Insufficient Mythical Keys. You have {this.BankedMythicalKeys ?? 0:N0}, need {Amount:N0}", ChatMessageType.System));
                return false;
            }

            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    if (offlinePlayer.BankedMythicalKeys == null)
                    {
                        offlinePlayer.BankedMythicalKeys = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedMythicalKeys += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedMythicalKeys == null)
                    {
                        onlinePlayer.BankedMythicalKeys = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedMythicalKeys += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedMythicalKeys, onlinePlayer.BankedMythicalKeys ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Mythical Keys from {this.Name}", ChatMessageType.System));
                    if (Amount > 1)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }
                }
                this.BankedMythicalKeys -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedMythicalKeys, this.BankedMythicalKeys ?? 0));
                if (Amount > 1)
                {
                    this.SavePlayerToDatabase();
                }

                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Mythical Keys", Amount, TransferLogger.TransferTypeBankTransfer);

                return true;
            }
        }

        public bool TransferLuminance(long Amount, string CharacterDestination)
        {
            // Check if sender has sufficient funds
            if ((this.BankedLuminance ?? 0) < Amount)
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Insufficient Luminance. You have {this.BankedLuminance ?? 0:N0}, need {Amount:N0}", ChatMessageType.System));
                return false;
            }

            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    if (offlinePlayer.BankedLuminance == null)
                    {
                        return false;
                    }
                    if (offlinePlayer.BankedLuminance == 0) 
                    {
                        return false;
                    }

                    this.BankedLuminance -= Amount;
                    (offlinePlayer).BankedLuminance += Amount;
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (!onlinePlayer.MaximumLuminance.HasValue)
                    {
                        return false;
                    }
                    this.BankedLuminance -= Amount;
                    onlinePlayer.BankedLuminance += Amount;
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedLuminance, onlinePlayer.BankedLuminance ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Luminance from {this.Name}", ChatMessageType.System));
                    if (Amount > 100000)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }
                }
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
                if (Amount > 100000)
                {
                    this.SavePlayerToDatabase();
                }
                
                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Luminance", Amount, TransferLogger.TransferTypeBankTransfer);
                
                return true;
            }          
        }
        public bool TransferEnlightenedCoins(long Amount, string CharacterDestination)
        {
            // Check if sender has sufficient funds
            if ((this.BankedEnlightenedCoins ?? 0) < Amount)
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Insufficient Enlightened Coins. You have {this.BankedEnlightenedCoins ?? 0:N0}, need {Amount:N0}", ChatMessageType.System));
                return false;
            }

            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    if (offlinePlayer.BankedEnlightenedCoins == null)
                    {
                        offlinePlayer.BankedEnlightenedCoins = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedEnlightenedCoins += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedEnlightenedCoins == null)
                    {
                        onlinePlayer.BankedEnlightenedCoins = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedEnlightenedCoins += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedEnlightenedCoins, onlinePlayer.BankedEnlightenedCoins ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Enlightened Coins from {this.Name}", ChatMessageType.System));
                    if (Amount > 10)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }

                }
                this.BankedEnlightenedCoins -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedEnlightenedCoins, this.BankedEnlightenedCoins ?? 0));
                if (Amount > 10)
                {
                    this.SavePlayerToDatabase();
                }
                
                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Enlightened Coins", Amount, TransferLogger.TransferTypeBankTransfer);
                
                return true;
            }
        }
        public bool TransferWeaklyEnlightenedCoins(long Amount, string CharacterDestination)
        {
            // Check if sender has sufficient funds
            if ((this.BankedWeaklyEnlightenedCoins ?? 0) < Amount)
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Insufficient Weakly Enlightened Coins. You have {this.BankedWeaklyEnlightenedCoins ?? 0:N0}, need {Amount:N0}", ChatMessageType.System));
                return false;
            }

            var tarplayer = FindTargetByName(CharacterDestination);
            if (tarplayer == null)
            {
                return false;
            }
            else
            {
                if (tarplayer is OfflinePlayer)
                {
                    var offlinePlayer = tarplayer as OfflinePlayer;
                    if (offlinePlayer.BankedWeaklyEnlightenedCoins == null)
                    {
                        offlinePlayer.BankedWeaklyEnlightenedCoins = Amount;
                    }
                    else
                    {
                        offlinePlayer.BankedWeaklyEnlightenedCoins += Amount;
                    }
                }
                else
                {
                    var onlinePlayer = tarplayer as Player;
                    if (onlinePlayer.BankedWeaklyEnlightenedCoins == null)
                    {
                        onlinePlayer.BankedWeaklyEnlightenedCoins = Amount;
                    }
                    else
                    {
                        onlinePlayer.BankedWeaklyEnlightenedCoins += Amount;
                    }
                    //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedWeaklyEnlightenedCoins, onlinePlayer.BankedWeaklyEnlightenedCoins ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Weakly Enlightened Coins from {this.Name}", ChatMessageType.System));
                    if (Amount > 10)
                    {
                        onlinePlayer.SavePlayerToDatabase();
                    }

                }
                this.BankedWeaklyEnlightenedCoins -= Amount;
                //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedWeaklyEnlightenedCoins, this.BankedWeaklyEnlightenedCoins ?? 0));
                if (Amount > 10)
                {
                    this.SavePlayerToDatabase();
                }
                
                // Log the transfer
                TransferLogger.LogBankTransfer(this, CharacterDestination, "Weakly Enlightened Coins", Amount, TransferLogger.TransferTypeBankTransfer);
                
                return true;
            }
        }
        public long? BankedLuminance
        {
            get => GetProperty(PropertyInt64.BankedLuminance) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedLuminance); else SetProperty(PropertyInt64.BankedLuminance, value.Value); }
        }

        public long? BankedPyreals
        {
            get => GetProperty(PropertyInt64.BankedPyreals) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedPyreals); else SetProperty(PropertyInt64.BankedPyreals, value.Value); }
        }

        public long? BankedLegendaryKeys
        {
            get => GetProperty(PropertyInt64.BankedLegendaryKeys) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedLegendaryKeys); else SetProperty(PropertyInt64.BankedLegendaryKeys, value.Value); }
        }
        public long? BankedEnlightenedCoins
        {
            get => GetProperty(PropertyInt64.BankedEnlightenedCoins) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedEnlightenedCoins); else SetProperty(PropertyInt64.BankedEnlightenedCoins, value.Value); }
        }
        public long? BankedWeaklyEnlightenedCoins
        {
            get => GetProperty(PropertyInt64.BankedWeaklyEnlightenedCoins) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedWeaklyEnlightenedCoins); else SetProperty(PropertyInt64.BankedWeaklyEnlightenedCoins, value.Value); }
        }
        public long? BankedMythicalKeys
        {
            get => GetProperty(PropertyInt64.BankedMythicalKeys) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedMythicalKeys); else SetProperty(PropertyInt64.BankedMythicalKeys, value.Value); }
        }
    }
}
