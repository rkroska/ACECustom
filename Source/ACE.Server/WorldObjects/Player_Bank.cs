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

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private int _bankAccount;
        public ulong BankedLuminance;
        public ulong BankedPyreals;
        public ulong BankedCoins;

        /// <summary>
        /// Generates a new bank account if the player doesn't have one
        /// </summary>
        /// <returns></returns>
        public int BankAccountNumber
        {
            get
            {
                if (_bankAccount == 0)
                {
                    _bankAccount = ThreadSafeRandom.Next(0, int.MaxValue - 1);
                }
                return _bankAccount;
            }
        }

    }
}
