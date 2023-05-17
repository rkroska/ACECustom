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

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private readonly object balanceLock = new object();
        /// <summary>
        /// Generates a new bank account if the player doesn't have one
        /// </summary>
        /// <returns></returns>
        public int BankAccountNumber
        {
            get
            {
                int _bankAccount = (int)(GetProperty(PropertyInt.BankAccountNumber));
                if (_bankAccount == 0)
                {
                    _bankAccount = ThreadSafeRandom.Next(0, int.MaxValue - 1);
                    SetProperty(PropertyInt.BankAccountNumber, _bankAccount);
                }
                return _bankAccount;
            }
        }

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
        }

        public void WithdrawPyreals(long Amount)
        {
            lock (balanceLock)
            {
                while (Amount > 250000)
                {
                    this.TryCreateInInventoryWithNetworking(WorldObjectFactory.CreateNewWorldObject(20630));
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
            }            
        }

        public long? BankedLuminance
        {
            get => GetProperty(PropertyInt64.BankedLuminance);
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedLuminance); else SetProperty(PropertyInt64.BankedLuminance, value.Value); }
        }

        public long? BankedPyreals
        {
            get => GetProperty(PropertyInt64.BankedPyreals);
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.BankedPyreals); else SetProperty(PropertyInt64.BankedPyreals, value.Value); }
        }
    }
}
