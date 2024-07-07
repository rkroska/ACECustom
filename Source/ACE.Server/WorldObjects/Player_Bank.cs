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
                        this.TryConsumeFromInventoryWithNetworking(item);
                        BankedPyreals += 25000;
                    }
                    else if (Amount >= item.StackSize)
                    {
                        this.TryConsumeFromInventoryWithNetworking(item, (int)Amount);
                        Amount -= item.StackSize ?? 0;                        
                        BankedPyreals += item.StackSize;
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
                //int i = 0;
                var PyrealList = this.GetInventoryItemsOfWCID(8330);
                foreach (var Pyreal in PyrealList)
                {
                    int val = Pyreal.Value ?? 0;
                    if (val > 0)
                    {
                        this.TryConsumeFromInventoryWithNetworking(Pyreal);
                        BankedPyreals += val;
                    }
                }

                var GoldList = this.GetInventoryItemsOfWCID(8327);
                foreach (var Gold in GoldList)
                {
                    int val = Gold.Value ?? 0;
                    if (val > 0)
                    {
                        this.TryConsumeFromInventoryWithNetworking(Gold);
                        BankedPyreals += val;
                    }
                }


                var SilverList = this.GetInventoryItemsOfWCID(8331);
                foreach (var Silver in SilverList)
                {
                    int val = Silver.Value ?? 0;
                    if (val > 0)
                    {
                        this.TryConsumeFromInventoryWithNetworking(Silver);
                        BankedPyreals += val;
                    }
                }

                var CopperList = this.GetInventoryItemsOfWCID(8326);
                foreach (var Copper in CopperList)
                {
                    int val = Copper.Value ?? 0;
                    if (val > 0)
                    {
                        this.TryConsumeFromInventoryWithNetworking(Copper);
                        BankedPyreals += val;
                    }
                }

            }
        }

        public void DepositEnlightenedCoins()
        {

            var EnlList = this.GetInventoryItemsOfWCID(300004);
            long coin = 0;

            foreach (var item in EnlList)
                coin += (long)item.StackSize;

            DepositEnlightenedCoins(coin);
        }

        public void DepositEnlightenedCoins(long amount)
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
                        this.TryConsumeFromInventoryWithNetworking(coin);
                        BankedEnlightenedCoins += val;
                    }
                }
            }
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
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
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
                        this.TryConsumeFromInventoryWithNetworking(note);
                        BankedPyreals += val;
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
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
        }

        public void WithdrawPyreals(long Amount)
        {
            lock (balanceLock)
            {
                int mmdCount = 0;
                while (Amount >= 250000)
                {
                    mmdCount++;
                    //this.TryCreateInInventoryWithNetworking(WorldObjectFactory.CreateNewWorldObject(20630));
                    Amount -= 250000;
                    BankedPyreals -= 250000;
                }

                if (Amount > 25000)
                {
                    while (Amount > 25000)
                    {
                        WorldObject coins = WorldObjectFactory.CreateNewWorldObject(273);
                        coins.SetStackSize((int)25000);
                        this.TryCreateInInventoryWithNetworking(coins);
                        BankedPyreals -= 25000;
                        Amount -= 25000;
                    }

                }
                if (Amount > 0)
                {
                    WorldObject smallCoins = WorldObjectFactory.CreateNewWorldObject(273);
                    smallCoins.SetStackSize((int)Amount);
                    this.TryCreateInInventoryWithNetworking(smallCoins);
                    BankedPyreals -= Amount;
                    Amount = 0;
                }
                if (mmdCount > 0)
                {
                    var woMMDs = WorldObjectFactory.CreateNewWorldObject(20630);
                    while (mmdCount >= woMMDs.MaxStackSize)
                    {
                        var woMMDStack = WorldObjectFactory.CreateNewWorldObject(20630);
                        woMMDStack.SetStackSize(woMMDStack.MaxStackSize);
                        this.TryCreateInInventoryWithNetworking(woMMDStack);
                        mmdCount -= (int)woMMDStack.MaxStackSize;
                    }
                    if (mmdCount > 0)
                    {
                        var woMMDStack = WorldObjectFactory.CreateNewWorldObject(20630);
                        woMMDStack.SetStackSize(mmdCount);
                        this.TryCreateInInventoryWithNetworking(woMMDStack);
                    }
                }
            }            
        }

        public void WithdrawLegendaryKeys(long Amount)
        {
            long remainingAmount = Amount;
            lock (balanceLock)
            {
                if (Amount >= 25)
                {
                    for (int x = 25; x < Amount; x += 25)
                    {
                        remainingAmount -= 25;
                        WorldObject key = WorldObjectFactory.CreateNewWorldObject(500010); //25 Durable legendary key
                        this.TryCreateInInventoryWithNetworking(key);
                        BankedLegendaryKeys -= 25;
                    }
                }
                else if (Amount >= 10)
                {
                    for (int x = 10; x < Amount; x+=10)
                    {
                        remainingAmount -= 10;
                        WorldObject key = WorldObjectFactory.CreateNewWorldObject(51954); //Durable legendary key
                        this.TryCreateInInventoryWithNetworking(key);
                        BankedLegendaryKeys -= 10;
                    }
                }
                for (int i = 0; i < remainingAmount; i++)
                {
                    WorldObject key = WorldObjectFactory.CreateNewWorldObject(48746); //Regular legendary key
                    this.TryCreateInInventoryWithNetworking(key);
                    BankedLegendaryKeys -= 1;
                }
            }
        }

        public void WithdrawMythicalKeys(long Amount)
        {
            long remainingAmount = Amount;
            lock (balanceLock)
            {
                if (Amount >= 25)
                {
                    for (int x = 25; x < Amount; x += 25)
                    {
                        remainingAmount -= 25;
                        WorldObject key = WorldObjectFactory.CreateNewWorldObject(90000110); //25 Durable legendary key
                        this.TryCreateInInventoryWithNetworking(key);
                        BankedMythicalKeys -= 25;
                    }
                }
                else if (Amount >= 10)
                {
                    for (int x = 10; x < Amount; x += 10)
                    {
                        remainingAmount -= 10;
                        WorldObject key = WorldObjectFactory.CreateNewWorldObject(90000109); //Durable legendary key
                        this.TryCreateInInventoryWithNetworking(key);
                        BankedMythicalKeys -= 10;
                    }
                }
                for (int i = 0; i < remainingAmount; i++)
                {
                    WorldObject key = WorldObjectFactory.CreateNewWorldObject(90000104); //Regular legendary key
                    this.TryCreateInInventoryWithNetworking(key);
                    BankedMythicalKeys -= 1;
                }
            }
        }

        public void WithdrawEnlightenedCoins(long Amount)
        {
            long remainingAmount = Amount;
            lock (balanceLock)
            {
                if (Amount > 0)
                {
                    WorldObject coins = WorldObjectFactory.CreateNewWorldObject(300004);
                    coins.SetStackSize((int)Amount);
                    this.TryCreateInInventoryWithNetworking(coins);
                    BankedEnlightenedCoins -= Amount;
                    Amount = 0;
                }
            }
        }

        public bool TransferPyreals(long Amount, string CharacterDestination)
        {
            var tarplayer = PlayerManager.GetAllPlayers().Where(p => p.Name == CharacterDestination && !p.IsDeleted && !p.IsPendingDeletion).FirstOrDefault();
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
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedPyreals, onlinePlayer.BankedPyreals ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Pyreal from {this.Name}", ChatMessageType.System));
                }
                this.BankedPyreals -= Amount;
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedPyreals, this.BankedPyreals ?? 0));
                
                return true;
            }
        }

        public bool TransferLegendaryKeys(long Amount, string CharacterDestination)
        {
            var tarplayer = PlayerManager.GetAllPlayers().Where(p => p.Name == CharacterDestination && !p.IsDeleted && !p.IsPendingDeletion ).FirstOrDefault();
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
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedLegendaryKeys, onlinePlayer.BankedLegendaryKeys ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Legendary Keys from {this.Name}", ChatMessageType.System));
                }
                this.BankedLegendaryKeys -= Amount;
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLegendaryKeys, this.BankedLegendaryKeys ?? 0));
                return true;
            }
        }

        public bool TransferMythicalKeys(long Amount, string CharacterDestination)
        {
            var tarplayer = PlayerManager.GetAllPlayers().Where(p => p.Name == CharacterDestination && !p.IsDeleted && !p.IsPendingDeletion).FirstOrDefault();
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
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedMythicalKeys, onlinePlayer.BankedMythicalKeys ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Mythical Keys from {this.Name}", ChatMessageType.System));
                }
                this.BankedMythicalKeys -= Amount;
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedMythicalKeys, this.BankedMythicalKeys ?? 0));
                return true;
            }
        }

        public bool TransferLuminance(long Amount, string CharacterDestination)
        {
            var tarplayer = PlayerManager.GetAllPlayers().Where(p => p.Name == CharacterDestination && !p.IsDeleted && !p.IsPendingDeletion).FirstOrDefault();
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
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedLuminance, onlinePlayer.BankedLuminance ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Luminance from {this.Name}", ChatMessageType.System));
                }
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, this.BankedLuminance ?? 0));
                return true;
            }          
        }
        public bool TransferEnlightenedCoins(long Amount, string CharacterDestination)
        {
            var tarplayer = PlayerManager.GetAllPlayers().Where(p => p.Name == CharacterDestination && !p.IsDeleted && !p.IsPendingDeletion).FirstOrDefault();
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
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(onlinePlayer, PropertyInt64.BankedLegendaryKeys, onlinePlayer.BankedLegendaryKeys ?? 0));
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Received {Amount:N0} Enlightend Coins from {this.Name}", ChatMessageType.System));
                }
                this.BankedEnlightenedCoins -= Amount;
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedEnlightenedCoins, this.BankedEnlightenedCoins ?? 0));
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
        public long? BankedMythicalKeys
        {
            get => GetProperty(PropertyInt64.BankedMythicalKeys) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedMythicalKeys); else SetProperty(PropertyInt64.BankedMythicalKeys, value.Value); }
        }
    }
}
