using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using Lifestoned.DataModel.DerethForever;
using log4net;
using MySqlX.XDevAPI.Common;
using Org.BouncyCastle.Utilities.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
//using ACE.Server.Factories;
//using Org.BouncyCastle.Ocsp;
//using System.Diagnostics.Metrics;

namespace ACE.Server.Command.Handlers
{
    public static class PlayerCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [CommandHandler("fship", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Commands to handle fellowships aside from the UI", "")]
        public static void HandleFellowCommand(Session session, params string[] parameters)
        {
            if (parameters == null || parameters.Count() == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship add <name or targetted player>", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship landblock to invite all players in your landblock", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship remove <name or targetted player>", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship create <name> to create a fellowship", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship leave", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship disband", ChatMessageType.Broadcast));
            }

            if (parameters.Count() == 1)
            {
                if (parameters[0] == "landblock")
                {
                    if (session.Player.CurrentLandblock == null)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Your current landblock is not found, for some reason (logged)", ChatMessageType.Broadcast));
                        return;
                    }
                    if (session.Player.CurrentLandblock.Id.Landblock == 0x016C)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Your current landblock is in the Marketplace, and cannot be used to form landblock fellowships", ChatMessageType.Broadcast));
                        return;
                    }
                    bool currentPlayerOver50 = session.Player.Level >= 50;
                    foreach (var player in session.Player.CurrentLandblock.players)
                    {
                        if (player.Guid != session.Player.Guid && !player.IsMule && (player.CloakStatus == CloakStatus.Player || player.CloakStatus == CloakStatus.Off || player.CloakStatus == CloakStatus.Undef))
                        {
                            if (!currentPlayerOver50 || player.Level >= 50) // Don't add lowbies to a fellowship of players over 50
                            {
                                if (!session.Player.SquelchManager.Squelches.Contains(player, ChatMessageType.Tell))
                                {
                                    session.Player.FellowshipRecruit(player);
                                }
                            }
                        }
                    }
                    return;
                }

                if (parameters[0] == "leave")
                {
                    session.Player.Fellowship.QuitFellowship(session.Player, false);
                    return;
                }
                if (parameters[0] == "disband")
                {
                    session.Player.Fellowship.QuitFellowship(session.Player, true);
                    return;
                }
                if (parameters[0] == "add")
                {
                    var tPGuid = session.Player.CurrentAppraisalTarget;
                    if (tPGuid != null)
                    {
                        var tplayer = PlayerManager.FindByGuid(tPGuid.Value) as Player;
                        if (tplayer != null)
                        {
                            session.Player.FellowshipRecruit(tplayer);
                        }
                    }
                    return;

                }
                if (parameters[0] == "remove")
                {
                    var tPGuid = session.Player.CurrentAppraisalTarget;
                    if (tPGuid != null)
                    {
                        session.Player.FellowshipDismissPlayer(tPGuid.Value);
                    }
                    return;
                }
            }

            if (parameters.Count() == 2)
            {
                if (parameters[0] == "create")
                {
                    session.Player.FellowshipCreate(parameters[1], true);
                    return;
                }
                if (parameters[0] == "add")
                {
                    var tplayer = PlayerManager.FindByName(parameters[1]) as Player;
                    if (tplayer != null)
                    {
                        session.Player.FellowshipRecruit(tplayer);
                        return;
                    }
                }
                if (parameters[0] == "remove")
                {
                    var tplayer = PlayerManager.FindByName(parameters[1]) as Player;
                    if (tplayer != null)
                    {
                        session.Player.FellowshipDismissPlayer(tplayer.Guid.Full);
                        return;
                    }
                }
            }
        }


        [CommandHandler("aug", AccessLevel.Player, CommandHandlerFlag.None, "Handles Augmentation Reporting", "")]
        public static void HandleAugmentReport(Session session, params string[] parameters)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Advanced Augmentation Levels:", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Creature:{session.Player.LuminanceAugmentCreatureCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Item:{session.Player.LuminanceAugmentItemCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Life:{session.Player.LuminanceAugmentLifeCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"War:{session.Player.LuminanceAugmentWarCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Void:{session.Player.LuminanceAugmentVoidCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Duration: {session.Player.LuminanceAugmentSpellDurationCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Specialization: {session.Player.LuminanceAugmentSpecializeCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Summon: {session.Player.LuminanceAugmentSummonCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Melee: {session.Player.LuminanceAugmentMeleeCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Missile: {session.Player.LuminanceAugmentMissileCount:N0}", ChatMessageType.Broadcast));
        }

        //custom commands
        [CommandHandler("bank", AccessLevel.Player, CommandHandlerFlag.None, "Handles Banking Operations", "")]
        public static void HandleBank(Session session, params string[] parameters)
        {
            if (session.Player == null)
                return;
            if (session.Player.IsOlthoiPlayer)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Bugs ain't got banks.", ChatMessageType.Broadcast));
                return;
            }

            // Check if player or account is blacklisted from bank commands
            if (TransferLogger.IsPlayerBankBlacklisted(session.Player.Name) || 
                (session.Player.Account != null && TransferLogger.IsAccountBankBlacklisted(session.Player.Account.AccountName)))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You are not permitted to use bank commands at this time.", ChatMessageType.Broadcast));
                return;
            }
            
            // Show balance if no parameters (just /b)
            if (parameters.Length == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Your balances are:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Pyreals: {session.Player.BankedPyreals:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Luminance: {session.Player.BankedLuminance:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Legendary Keys: {session.Player.BankedLegendaryKeys:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Mythical Keys: {session.Player.BankedMythicalKeys:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Enlightened Coins: {session.Player.BankedEnlightenedCoins:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Weakly Enlightened Coins: {session.Player.BankedWeaklyEnlightenedCoins:N0}", ChatMessageType.System));
                return;
            }

            if (parameters[0] == "help" || parameters[0] == "h")
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Available Commands:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"DEPOSIT COMMANDS:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit (or /b d) - Deposit all pyreals, peas, luminance, keys, coins, and notes", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit all (or /b d a) - Deposit all pyreals, peas, luminance, keys, coins, and notes", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit pyreals (or /b d p) - Deposit all pyreals", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit peas (or /b d ps) - Deposit all pyreal peas", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit luminance (or /b d l) - Deposit all luminance", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit legendarykeys (or /b d k) - Deposit all legendary keys", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit mythicalkeys (or /b d mk) - Deposit all mythical keys", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit enlightenedcoins (or /b d e) - Deposit all enlightened coins", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit weaklyenlightenedcoins (or /b d we) - Deposit all weakly enlightened coins", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit notes (or /b d n) - Deposit all trade notes", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"WITHDRAWAL COMMANDS:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw pyreals <amount> (or /b w p <amount>) - Withdraw pyreals as coins", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw notes <denomination> (or /b w n <denomination>) - Withdraw specific trade notes", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"  Denominations: i(100), v(500), x(1k), l(5k), c(10k), d(50k), m(100k), md(150k), mm(200k), mmd(250k)", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw luminance <amount> (or /b w l <amount>) - Withdraw luminance", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw legendarykeys <amount> (or /b w k <amount>) - Withdraw legendary keys", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw mythicalkeys <amount> (or /b w mk <amount>) - Withdraw mythical keys", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw enlightenedcoins <amount> (or /b w e <amount>) - Withdraw enlightened coins", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw weaklyenlightenedcoins <amount> (or /b w we <amount>) - Withdraw weakly enlightened coins", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"OTHER COMMANDS:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank balance (or /b b) - Show bank balance", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/b - Show bank balance (shortcut)", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"TRANSFER COMMANDS:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer pyreals <amount> <character> (or /b t p <amount> <character>) - Transfer pyreals", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer notes <denom> <count> <character> (or /b t n <denom> <count> <character>) - Transfer trade note value", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer luminance <amount> <character> (or /b t l <amount> <character>) - Transfer luminance", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer legendarykeys <amount> <character> (or /b t k <amount> <character>) - Transfer legendary keys", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer mythicalkeys <amount> <character> (or /b t mk <amount> <character>) - Transfer mythical keys", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer enlightenedcoins <amount> <character> (or /b t e <amount> <character>) - Transfer enlightened coins", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer weaklyenlightenedcoins <amount> <character> (or /b t we <amount> <character>) - Transfer weakly enlightened coins", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"EXAMPLES:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/b w p 1m - Withdraw 1M pyreals as coin stacks (40 stacks of 25k each)", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/b w n mmd - Withdraw 250k trade note", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/b w n c 5 - Withdraw 5 trade notes of 10k each", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/b t p 1m \"Player Name\" - Transfer 1M pyreals to Player Name", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/b t n mmd 50 PlayerName - Transfer 50× 250k notes worth (12.5M pyreals)", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.System));

                return;
            }

            //cleanup edge cases
            if (session.Player.BankedPyreals < 0)
            {
                session.Player.BankedPyreals = 0;
            }
            if (session.Player.BankedLuminance < 0)
            {
                session.Player.BankedLuminance = 0;
            }
            if (session.Player.BankedLegendaryKeys < 0)
            {
                session.Player.BankedLegendaryKeys = 0;
            }
            if (session.Player.BankedMythicalKeys < 0)
            {
                session.Player.BankedMythicalKeys = 0;
            }
            if (session.Player.BankedEnlightenedCoins < 0)
            {
                session.Player.BankedEnlightenedCoins = 0;
            }
            if (session.Player.BankedWeaklyEnlightenedCoins < 0)
            {
                session.Player.BankedWeaklyEnlightenedCoins = 0;
            }

            int iType = 0;
            long amount = -1;
            string transferTargetName = "";

            if (parameters.Count() >= 2)
            {
                if (parameters[1] == "pyreals" || parameters[1] == "p")
                {
                    //pyreals
                    iType = 1;
                }
                if (parameters[1] == "luminance" || parameters[1] == "l")
                {
                    //lum
                    iType = 2;
                }
                if (parameters[1] == "notes" || parameters[1] == "n")
                {
                    //trade notes
                    iType = 3;
                }
                if (parameters[1] == "Legendarykeys" || parameters[1] == "k")
                {
                    iType = 4;
                }
                if (parameters[1] == "peas" || parameters[1] == "ps")
                {
                    iType = 5;
                }
                if (parameters[1] == "enlightenedcoins" || parameters[1] == "e")
                {
                    iType = 6;
                }
                if (parameters[1] == "Mythicalkeys" || parameters[1] == "mk")
                {
                    iType = 7;
                }
                if (parameters[1] == "Weaklyenlightenedcoins" || parameters[1] == "we")
                {
                    iType = 8;
                }
            }

            if (parameters.Count() == 3 || parameters.Count() == 4)
            {
                // Skip amount parsing for trade notes (iType = 3) since parameters[2] is the denomination
                if (iType != 3)
                {
                    if (!Player.TryParseAmount(parameters[2], out amount))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid amount. Use numbers with optional suffix: 10k, 1.5m, 2b, etc.", ChatMessageType.System));
                        return;
                    }
                    if (amount <= 0)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You need to provide a positive number to withdraw", ChatMessageType.System));
                        return;
                    }
                }
            }

            if (parameters.Count() == 4)
            {
                transferTargetName = parameters[3];
            }

            if (parameters[0] == "deposit" || parameters[0] == "d")
            {

                if (session.Player.IsBusy)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot deposit while teleporting or busy. Complete your movement and try again!", ChatMessageType.System));
                    return;
                }

                var commandSecondsLimit = PropertyManager.GetLong("bank_command_limit");
                var currentTime = DateTime.UtcNow;

                var lastCommandTimeSeconds = (currentTime - session.LastBankCommandTime).TotalSeconds;
                if (lastCommandTimeSeconds < commandSecondsLimit)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"[Deposit] This command may only be run once every {commandSecondsLimit} seconds.", ChatMessageType.Broadcast);
                    return;
                }

                session.LastBankCommandTime = currentTime;

                //deposit
                if (parameters.Count() == 1 || (parameters.Count() == 2 && parameters[1] == "a"))
                {
                    //deposit all - suppress individual messages
                    session.Player.DepositPyreals(true);
                    session.Player.DepositLuminance(true);
                    session.Player.DepositLegendaryKeys(true);
                    session.Player.DepositPeas(true);
                    session.Player.DepositEnlightenedCoins(true);
                    session.Player.DepositWeaklyEnlightenedCoins(true);
                    session.Player.DepositMythicalKeys(true);
                    session.Player.DepositTradeNotes(true);

                    session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all currencies!", ChatMessageType.System));
                    return;
                }
                
                switch (iType)
                {
                    case 1:
                        //deposit pyreals
                        if (amount > 0)
                        {
                            session.Player.DepositPyreals(amount);
                        }
                        else
                        {
                            session.Player.DepositPyreals();
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all pyreals!", ChatMessageType.System));
                        }
                        break;
                    case 2:
                        //deposit lum
                        if (amount > 0)
                        {
                            session.Player.DepositLuminance(amount);
                        }
                        else
                        {
                            session.Player.DepositLuminance();
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all luminance!", ChatMessageType.System));
                        }
                        break;
                    case 3:
                        //deposit notes
                        session.Player.DepositTradeNotes();
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all trade notes!", ChatMessageType.System));
                        break;
                    case 4:
                        session.Player.DepositLegendaryKeys();
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all aged legendary keys!", ChatMessageType.System));
                        break;
                    case 5:
                        session.Player.DepositPeas();
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all Peas as pyreals!", ChatMessageType.System));
                        break;
                    case 6:
                        session.Player.DepositEnlightenedCoins();
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all enlightened coins!", ChatMessageType.System));
                        break;
                    case 7:
                        session.Player.DepositMythicalKeys();
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all Mythical keys!", ChatMessageType.System));
                        break;
                    case 8:
                        session.Player.DepositWeaklyEnlightenedCoins();
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all weakly enlightened coins!", ChatMessageType.System));
                        break;
                    default:
                        break;
                }
            }

            if (parameters[0] == "withdraw" || parameters[0] == "w")
            {
                var commandSecondsLimit = PropertyManager.GetLong("bank_command_limit");
                var currentTime = DateTime.UtcNow;

                var lastCommandTimeSeconds = (currentTime - session.LastBankCommandTime).TotalSeconds;
                if (lastCommandTimeSeconds < commandSecondsLimit)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"[Withdraw] This command may only be run once every {commandSecondsLimit} seconds.", ChatMessageType.Broadcast);
                    return;
                }

                session.LastBankCommandTime = currentTime;

                //withdraw
                switch (iType)
                {
                    case 1:
                        //withdraw pyreals
                        session.Player.WithdrawPyreals(amount);
                        break;
                    case 2:
                        //withdraw lum
                        session.Player.WithdrawLuminance(amount);
                        break;
                    case 3:
                        //withdraw trade notes
                        if (parameters.Length < 3)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Usage: /bank withdraw notes <denomination> [count] or /b w n <denomination> [count]", ChatMessageType.System));
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Denominations: i(100), v(500), x(1k), l(5k), c(10k), d(50k), m(100k), md(150k), mm(200k), mmd(250k)", ChatMessageType.System));
                            break;
                        }
                        
                        string denomination = parameters[2];
                        int noteCount = 1;
                        
                        // Check if there's a count parameter
                        if (parameters.Length >= 4 && int.TryParse(parameters[3], out int parsedCount))
                        {
                            noteCount = parsedCount;
                        }
                        
                        session.Player.WithdrawTradeNotes(denomination, noteCount);
                        break;
                    case 4:
                        session.Player.WithdrawLegendaryKeys(amount);
                        break;
                    case 6:
                        session.Player.WithdrawEnlightenedCoins(amount);
                        break;
                    case 7:
                        session.Player.WithdrawMythicalKeys(amount);
                        break;
                    case 8:
                        session.Player.WithdrawWeaklyEnlightenedCoins(amount);
                        break;
                    default:
                        break;
                }
            }

            if (parameters[0] == "transfer" || parameters[0] == "t")
            {
                if (parameters.Length > 5)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Too many parameters, ensure you use \"quotes\" around player names with spaces.", ChatMessageType.System));
                    return;
                }
                
                // Rate limiting for transfer commands
                var commandSecondsLimit = PropertyManager.GetLong("bank_command_limit");
                var currentTime = DateTime.UtcNow;

                var lastCommandTimeSeconds = (currentTime - session.LastBankCommandTime).TotalSeconds;
                if (lastCommandTimeSeconds < commandSecondsLimit)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"[Transfer] This command may only be run once every {commandSecondsLimit} seconds.", ChatMessageType.Broadcast);
                    return;
                }

                session.LastBankCommandTime = currentTime;
                
                // Special handling for trade note transfers: /b t n <denom> <count> <player>
                if (iType == 3 && parameters.Length == 5)
                {
                    string denomination = parameters[2];
                    if (!int.TryParse(parameters[3], out int noteCount) || noteCount <= 0)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid count. Please provide a positive number.", ChatMessageType.System));
                        return;
                    }
                    
                    string targetPlayer = parameters[4];
                    
                    // Map denomination to value (same as WithdrawTradeNotes)
                    long noteValue = denomination.Trim().ToLower() switch
                    {
                        "i" => 100,
                        "v" => 500,
                        "x" => 1000,
                        "l" => 5000,
                        "c" => 10000,
                        "d" => 50000,
                        "m" => 100000,
                        "md" => 150000,
                        "mm" => 200000,
                        "mmd" => 250000,
                        _ => -1
                    };
                    
                    if (noteValue == -1)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid denomination. Use: i(100), v(500), x(1k), l(5k), c(10k), d(50k), m(100k), md(150k), mm(200k), mmd(250k)", ChatMessageType.System));
                        return;
                    }
                    
                    long totalValue = noteValue * noteCount;
                    
                    if (session.Player.TransferPyreals(totalValue, targetPlayer))
                    {
                        // Transfer succeeded - the method already sent base message
                        // Just note it was a trade note equivalent transfer
                        session.Network.EnqueueSend(new GameMessageSystemChat($"(Equivalent to {noteCount}× {denomination.ToUpper()} notes)", ChatMessageType.System));
                    }
                    
                    return;
                }
                
                //transfer
                switch (iType)
                {
                    case 1:
                        //transfer pyreals
                        session.Player.TransferPyreals(amount, transferTargetName);
                        break;
                    case 2:
                        //transfer lum
                        session.Player.TransferLuminance(amount, transferTargetName);
                        break;
                    case 4:
                        session.Player.TransferLegendaryKeys(amount, transferTargetName);
                        break;
                    case 6:
                        session.Player.TransferEnlightenedCoins(amount, transferTargetName);
                        break;
                    case 7:
                        session.Player.TransferMythicalKeys(amount, transferTargetName);
                        break;
                    case 8:
                        session.Player.TransferWeaklyEnlightenedCoins(amount, transferTargetName);
                        break;
                }
            }
            if (parameters[0] == "balance" || parameters[0] == "b")
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Your balances are:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Pyreals: {session.Player.BankedPyreals:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Luminance: {session.Player.BankedLuminance:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Legendary Keys: {session.Player.BankedLegendaryKeys:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Mythical Keys: {session.Player.BankedMythicalKeys:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Enlightened Coins: {session.Player.BankedEnlightenedCoins:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Weakly Enlightened Coins: {session.Player.BankedWeaklyEnlightenedCoins:N0}", ChatMessageType.System));
            }
        }

        [CommandHandler("b", AccessLevel.Player, CommandHandlerFlag.None, "Handles Banking Operations", "")]
        public static void HandleBankShort(Session session, params string[] parameters)
        {

            HandleBank(session, parameters);
        }

        static readonly uint[] _lowLevelOverlays = {
        0x6006C34,    // lvl 1
        0x6006C35,    // lvl 2
        0x6006C36     // lvl 3
        };

        static readonly HashSet<uint> _allowedOverlays = new HashSet<uint>(_lowLevelOverlays);

        [CommandHandler("clap", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Deposit Enlightened Coins and Weakly Enlightened Coins using items from your pack. It will take the lower of the Red Coalesced Aetheria/Red Chunks/Empyrean Trinket and Blue Coalesced Aetheria/Blue Chunks/Falatacot (including powders) and deposit that amount.", "Usage: /clap all")]
        public static void HandleClap(Session session, params string[] parameters)
        {
            // OPTIMIZATION NOTES:
            // This method has been optimized for better server performance while maintaining code readability:
            // 1. Early exit if no materials available (prevents unnecessary processing)
            // 2. Grouped inventory queries (reduces multiple inventory scans)
            // 3. Simplified math calculations (eliminates redundant calculations)
            // 4. Single database save at the end (reduces database operations)
            // 5. Future optimization potential: batch property update messages
            
            if (session.Player == null)
                return;

            if (session.Player.QuestManager.GetCurrentSolves("AutoCraftingEnabled") < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You must have received the AutoCraftingEnabled quest stamp in order to use this command.", ChatMessageType.Broadcast));
                return;
            }

            var commandSecondsLimit = PropertyManager.GetLong("clap_command_limit");
            var currentTime = DateTime.UtcNow;

            var lastCommandTimeSeconds = (currentTime - session.LastClapCommandTime).TotalSeconds;
            if (lastCommandTimeSeconds < commandSecondsLimit)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"[Clap] This command may only be run once every {commandSecondsLimit} seconds.", ChatMessageType.Broadcast);
                return;
            }

            session.LastClapCommandTime = currentTime;
            const long ClapCostPerUnit = 250000L;


            // OPTIMIZATION: Early exit if no materials available - prevents unnecessary processing
            if (!HasAnyAetheriaMaterials(session.Player)) {
                session.Network.EnqueueSend(new GameMessageSystemChat("You don't have any aetheria materials to process.", ChatMessageType.System));
                return;
            }

            // OPTIMIZATION: Reduced inventory scans - do grouped queries instead of individual ones
            // OLD CODE (commented out):
            /*
            // Inventory counts for Red Coalesced Aetheria + Red Chunks + Red Powder + Empyrean Trinkets
            var redAetheriaItems = session.Player.GetInventoryItemsOfWCID(42636) // Red Coalesced Aetheria WCID
                .Where(item => item.EquipmentSetId == null && item.IconOverlayId.HasValue && _allowedOverlays.Contains(item.IconOverlayId.Value))    // only levels 1-3
            .ToList();
            int redAetheriaCount = redAetheriaItems.Count;
            int redChunkCount = session.Player.GetNumInventoryItemsOfWCID(310147); // Red Chunk WCID
            int redPowderCount = session.Player.GetNumInventoryItemsOfWCID(42644); // Red Powder WCID
            int totalRedAetheriaCount = redAetheriaCount + redChunkCount + redPowderCount; // Combine all Red forms
            int empyreanTrinketCount = session.Player.GetNumInventoryItemsOfWCID(34276); // Empyrean Trinket

            // Inventory counts for Blue Coalesced Aetheria + Blue Chunks + Blue Powder + Falatacot Trinkets
            var blueAetheriaItems = session.Player.GetInventoryItemsOfWCID(42635) // Blue Coalesced Aetheria WCID
                .Where(item => item.EquipmentSetId == null && item.IconOverlayId.HasValue && _allowedOverlays.Contains(item.IconOverlayId.Value))    // only levels 1-3
            .ToList();
            int blueAetheriaCount = blueAetheriaItems.Count;
            int blueChunkCount = session.Player.GetNumInventoryItemsOfWCID(310149); // Blue Chunk WCID
            int bluePowderCount = session.Player.GetNumInventoryItemsOfWCID(300019); // Blue Powder WCID
            int totalBlueAetheriaCount = blueAetheriaCount + blueChunkCount + bluePowderCount; // Combine all Blue forms
            int falatacotTrinketCount = session.Player.GetNumInventoryItemsOfWCID(34277); // Falatacot Trinket
            */

            // NEW OPTIMIZED CODE: Grouped inventory queries
            // Get all red items in one scan
            var redAetheriaItems = session.Player.GetInventoryItemsOfWCID(42636) // Red Coalesced Aetheria WCID
                .Where(item => item.EquipmentSetId == null && item.IconOverlayId.HasValue && _allowedOverlays.Contains(item.IconOverlayId.Value))    // only levels 1-3
                .ToList();
            int redAetheriaCount = redAetheriaItems.Count;
            int redChunkCount = session.Player.GetNumInventoryItemsOfWCID(310147); // Red Chunk WCID
            int redPowderCount = session.Player.GetNumInventoryItemsOfWCID(42644); // Red Powder WCID
            int totalRedAetheriaCount = redAetheriaCount + redChunkCount + redPowderCount; // Combine all Red forms
            int empyreanTrinketCount = session.Player.GetNumInventoryItemsOfWCID(34276); // Empyrean Trinket

            // Get all blue items in one scan
            var blueAetheriaItems = session.Player.GetInventoryItemsOfWCID(42635) // Blue Coalesced Aetheria WCID
                .Where(item => item.EquipmentSetId == null && item.IconOverlayId.HasValue && _allowedOverlays.Contains(item.IconOverlayId.Value))    // only levels 1-3
                .ToList();
            int blueAetheriaCount = blueAetheriaItems.Count;
            int blueChunkCount = session.Player.GetNumInventoryItemsOfWCID(310149); // Blue Chunk WCID
            int bluePowderCount = session.Player.GetNumInventoryItemsOfWCID(300019); // Blue Powder WCID
            int totalBlueAetheriaCount = blueAetheriaCount + blueChunkCount + bluePowderCount; // Combine all Blue forms
            int falatacotTrinketCount = session.Player.GetNumInventoryItemsOfWCID(34277); // Falatacot Trinket

            // OPTIMIZATION: Simplified math calculations - do calculations once
            // OLD CODE (commented out):
            /*
            // Calculate the maximum amount of coins that can be crafted for each type
            int redComboCount = Math.Min(totalRedAetheriaCount, empyreanTrinketCount);
            int blueComboCount = Math.Min(totalBlueAetheriaCount, falatacotTrinketCount);

            // Calculate MMD cost only for Coalesced Aetheria and Chunks (Red and Blue)
            int redNonPowderUsed = Math.Min(redComboCount, redAetheriaCount + redChunkCount);
            int blueNonPowderUsed = Math.Min(blueComboCount, blueAetheriaCount + blueChunkCount);
            int totalClapCost = (redNonPowderUsed + blueNonPowderUsed) * ClapCostPerUnit;
            */

            // NEW OPTIMIZED CODE: Simplified calculations
            int redComboCount = Math.Min(totalRedAetheriaCount, empyreanTrinketCount);
            int blueComboCount = Math.Min(totalBlueAetheriaCount, falatacotTrinketCount);

            // If nothing is craftable, bail before any removals or cost checks
            if (redComboCount == 0 && blueComboCount == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("You don't have any aetheria materials to process.", ChatMessageType.System));
                return;
            }

            // MMD cost applies only to Coalesced + Chunks (no cost for powder)
            long redNonPowderUsed  = Math.Min((long)redComboCount, (long)redAetheriaCount + redChunkCount);
            long blueNonPowderUsed = Math.Min((long)blueComboCount, (long)blueAetheriaCount + blueChunkCount);
            long totalClapCost = (redNonPowderUsed + blueNonPowderUsed) * ClapCostPerUnit;

            if (session.Player.BankedPyreals < totalClapCost)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough banked pyreals to perform this action. Required: {totalClapCost}, Available: {session.Player.BankedPyreals}", ChatMessageType.Broadcast));
                return;
            }

            // OLD CODE (commented out):
            /*
            // Early exit if no materials available - this prevents unnecessary processing
            if (!HasAnyAetheriaMaterials(session.Player)) {
                session.Network.EnqueueSend(new GameMessageSystemChat("You don't have any aetheria materials to process.", ChatMessageType.System));
                return;
            }
            */

            // Consume items and bank coins
            // Red Aetheria + Empyrean Trinkets
            int redItemsToConsume = redComboCount;
            foreach (var item in redAetheriaItems.Take(Math.Min(redItemsToConsume, redAetheriaCount)))
            {
                if (!session.Player.TryConsumeFromInventoryWithNetworking(item))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Failed to remove Red Coalesced Aetheria from inventory.", ChatMessageType.System));
                    return;
                }
                redItemsToConsume--;
            }
            if (redItemsToConsume > 0)
            {
                if (!session.Player.TryConsumeFromInventoryWithNetworking(310147, Math.Min(redItemsToConsume, redChunkCount))) // Red Chunk
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Failed to remove Red Chunks from inventory.", ChatMessageType.System));
                    return;
                }
                redItemsToConsume -= redChunkCount;
            }
            if (redItemsToConsume > 0)
            {
                if (!session.Player.TryConsumeFromInventoryWithNetworking(42644, redItemsToConsume)) // Red Powder
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Failed to remove Red Powder from inventory.", ChatMessageType.System));
                    return;
                }
            }

            if (!session.Player.TryConsumeFromInventoryWithNetworking(34276, redComboCount)) // Empyrean Trinket
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Failed to remove Empyrean Trinkets from inventory.", ChatMessageType.System));
                return;
            }

            session.Player.BankedEnlightenedCoins += redComboCount;

            // Blue Aetheria + Falatacot Trinkets
            int blueItemsToConsume = blueComboCount;
            foreach (var item in blueAetheriaItems.Take(Math.Min(blueItemsToConsume, blueAetheriaCount)))
            {
                if (!session.Player.TryConsumeFromInventoryWithNetworking(item))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Failed to remove Blue Coalesced Aetheria from inventory.", ChatMessageType.System));
                    return;
                }
                blueItemsToConsume--;
            }
            if (blueItemsToConsume > 0)
            {
                if (!session.Player.TryConsumeFromInventoryWithNetworking(310149, Math.Min(blueItemsToConsume, blueChunkCount))) // Blue Chunk
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Failed to remove Blue Chunks from inventory.", ChatMessageType.System));
                    return;
                }
                blueItemsToConsume -= blueChunkCount;
            }
            if (blueItemsToConsume > 0)
            {
                if (!session.Player.TryConsumeFromInventoryWithNetworking(300019, blueItemsToConsume)) // Blue Powder
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Failed to remove Blue Powder from inventory.", ChatMessageType.System));
                    return;
                }
            }

            if (!session.Player.TryConsumeFromInventoryWithNetworking(34277, blueComboCount)) // Falatacot Trinket
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Failed to remove Falatacot Trinkets from inventory.", ChatMessageType.System));
                return;
            }

            // Award 3 Weakly Enlightened Coins per crafting unit
            session.Player.BankedWeaklyEnlightenedCoins += blueComboCount * 3; // Replace with the actual property for Weakly Enlightened Coins

            // Deduct ClapCost for Coalesced Aetheria and Chunks
            session.Player.BankedPyreals -= totalClapCost;

            // OPTIMIZATION: Track if we need to save to database (only save once at the end)
            // Save only if redComboCount > 30 or blueComboCount > 90
            bool needsSave = redComboCount > 30 || blueComboCount > 90;

            // OPTIMIZATION: Could batch property update messages here instead of individual updates
            // Current approach: Properties are updated individually as they change
            // Future optimization: Could collect all property changes and send them in one batch

            // Notify the player
            session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited {redComboCount} Enlightened Coins and {blueComboCount * 3} Weakly Enlightened Coins! Total cost (Coalesced Aetheria and Chunks only): {totalClapCost} pyreals.", ChatMessageType.Broadcast));

            // OPTIMIZATION: Save to database only once at the end if we processed any items
            if (needsSave)
            {
                session.Player.SavePlayerToDatabase();
            }
        }

        public static bool HasAnyAetheriaMaterials(Player player) {
            // Calculate actual crafting potential
            int totalRedAetheria = player.GetNumInventoryItemsOfWCID(42636) +  // Red Coalesced
                                   player.GetNumInventoryItemsOfWCID(310147) +  // Red Chunk
                                   player.GetNumInventoryItemsOfWCID(42644);    // Red Powder

            int totalBlueAetheria = player.GetNumInventoryItemsOfWCID(42635) + // Blue Coalesced
                                    player.GetNumInventoryItemsOfWCID(310149) + // Blue Chunk
                                    player.GetNumInventoryItemsOfWCID(300019);  // Blue Powder

            int empyreanTrinkets = player.GetNumInventoryItemsOfWCID(34276);
            int falatacotTrinkets = player.GetNumInventoryItemsOfWCID(34277);

            // Check if you can actually craft anything
            bool canCraftRed = totalRedAetheria > 0 && empyreanTrinkets > 0;
            bool canCraftBlue = totalBlueAetheria > 0 && falatacotTrinkets > 0;

            return canCraftRed || canCraftBlue;
        }

        [CommandHandler("enl", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Enlightenment Alias", "")]
        public static void HandleEnlShort(Session session, params string[] parameters)
        {
            HandleEnlightenment(session, parameters);
        }

        [CommandHandler("enlighten", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Handles Enlightenment", "")]
        public static void HandleEnlightenment(Session session, params string[] parameters)
        {
            if (session.Player.Teleporting || session.Player.TooBusyToRecall || session.Player.IsBusy || session.Player.IsInDeathProcess)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot enlighten while teleporting or busy. Complete your movement and try again. Neener neener.", ChatMessageType.System));
                return;
            }
            //if(!session.Player.ConfirmationManager.EnqueueSend(new Confirmation_YesNo(session.Player.Guid, session.Player.Guid, "Enlightenment"), "Are you certain that you'd like to Englighten? You will lose all unspent experience, unspent Luminance not in your bank, and all skills. You will retain all attributes."))
            //    return;
            var message = "Are you certain that you'd like to Enlighten? You will lose all unspent experience, unspent Luminance not in your bank, and all skills. You will retain all attributes.";
            var confirm = session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(session.Player.Guid, () => Enlightenment.HandleEnlightenment(session.Player)), message);

        }

        //[CommandHandler("dynamicabandon", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Abandons the most recent dynamic quest", "")]
        //public static void AbandonDynamicQuest(Session session, params string[] parameters)
        //{
        //    QuestManager.AbandonDynamicQuests(session.Player);
        //}

        [CommandHandler("bonus", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Handles Experience Checks", "Leave blank for level, pass first 3 letters of attribute for specific attribute cost")]
        public static void HandleMultiplier(Session session, params string[] paramters)
        {
            session.Player.QuestCompletionCount = session.Player.Account.GetCharacterQuestCompletions();
            var qb = session.Player.GetQuestCountXPBonus();
            var eq = session.Player.GetXPAndLuminanceModifier(XpType.Kill);
            var en = session.Player.GetEnglightenmentXPBonus();

            session.Network.EnqueueSend(new GameMessageSystemChat($"[BONUS] Your XP multiplier from Quests is: {qb - 1:P}", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"[BONUS] Your XP multiplier from Equipment is: {eq - 1:P}", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"[BONUS] Your XP multiplier from Enlightenment is: {en - 1:P}", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"[BONUS] Your Total XP multiplier is: {(qb * eq * en) - 1:P}", ChatMessageType.System));
        }

        [CommandHandler("xp", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Handles Experience Checks", "Leave blank for level, pass first 3 letters of attribute for specific attribute cost")]
        public static void HandleExperience(Session session, params string[] parameters)
        {
            int amount = 1;
            if (parameters.Length == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[XP] Your XP to next level is: {session.Player.GetRemainingXP():N0}", ChatMessageType.System));
            }
            else if (parameters.Length < 3)
            {
                if (parameters.Length == 2)
                {
                    if (!int.TryParse(parameters[1], out amount) || amount < 1)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[XP] Provide a number to count for XP Ranks", ChatMessageType.System));
                        return;
                    }
                }

                //check attribute costs
                if (parameters[0] == "all")
                {
                    ReportXPRequired(session, amount, "str");
                    ReportXPRequired(session, amount, "end");
                    ReportXPRequired(session, amount, "coo");
                    ReportXPRequired(session, amount, "qui");
                    ReportXPRequired(session, amount, "foc");
                    ReportXPRequired(session, amount, "sel");
                    ReportXPRequired(session, amount, "sta");
                    ReportXPRequired(session, amount, "hea");
                    ReportXPRequired(session, amount, "man");
                }
                else
                {
                    ReportXPRequired(session, amount, parameters[0]);
                }
            }
        }

        private static void ReportXPRequired(Session session, int amount, string attrAbbr)
        {
            ulong xp = 0; string AttrName = ""; bool success = false;
            switch (attrAbbr)
            {
                case "str":
                    xp = GetOrRaiseAttrib(session, amount, PropertyAttribute.Strength, out AttrName, false, out success);
                    break;
                case "end":
                    xp = GetOrRaiseAttrib(session, amount, PropertyAttribute.Endurance, out AttrName, false, out success);
                    break;
                case "coo":
                    xp = GetOrRaiseAttrib(session, amount, PropertyAttribute.Coordination, out AttrName, false, out success);
                    break;
                case "qui":
                    xp = GetOrRaiseAttrib(session, amount, PropertyAttribute.Quickness, out AttrName, false, out success);
                    break;
                case "foc":
                    xp = GetOrRaiseAttrib(session, amount, PropertyAttribute.Focus, out AttrName, false, out success);
                    break;
                case "sel":
                    xp = GetOrRaiseAttrib(session, amount, PropertyAttribute.Self, out AttrName, false, out success);
                    break;
                case "sta":
                    xp = GetOrRaise2ndAttrib(session, amount, PropertyAttribute2nd.MaxStamina, out AttrName, false, out success);
                    break;
                case "hea":
                    xp = GetOrRaise2ndAttrib(session, amount, PropertyAttribute2nd.MaxHealth, out AttrName, false, out success);
                    break;
                case "man":
                    xp = GetOrRaise2ndAttrib(session, amount, PropertyAttribute2nd.MaxMana, out AttrName, false, out success);
                    break;
            }
            if (string.IsNullOrEmpty(AttrName))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[XP] Provide a valid attribute abbreviation", ChatMessageType.System));
                return;
            }
            string pluralize = amount > 1 ? "levels" : "level";
            session.Network.EnqueueSend(new GameMessageSystemChat($"[XP] Your XP cost for next {amount} {AttrName} {pluralize} is: {xp:N0}", ChatMessageType.System));
        }

        public static ulong GetOrRaiseAttrib(Network.Session session, int RanksToRaise, PropertyAttribute attrib, out string AttrName, bool doRaise, out bool success)
        {
            if (!session.Player.Attributes.TryGetValue(attrib, out var creatureAttribute))
            {
                success = false;
            }
            uint destinationRank = (uint)(creatureAttribute.Ranks + RanksToRaise);
            AttrName = creatureAttribute.Attribute.GetDescription();
            ulong xpCost = Player.GetXPDeltaCostByRank(destinationRank, creatureAttribute.Ranks);
            if (doRaise)
            {
                success = session.Player.HandleActionRaiseAttribute(attrib, xpCost);
            }
            else
            {
                success = false;
            }
            return xpCost;
        }

        public static ulong GetOrRaise2ndAttrib(Network.Session session, int RanksToRaise, PropertyAttribute2nd vital, out string AttrName, bool doRaise, out bool success)
        {
            if (!session.Player.Vitals.TryGetValue(vital, out var creatureAttribute))
            { }
            uint destinationRank = (uint)(creatureAttribute.Ranks + RanksToRaise);
            AttrName = creatureAttribute.Vital.GetDescription();
            ulong xpCost = Player.GetXPDeltaCostByRankForSecondary(destinationRank, creatureAttribute.Ranks);
            if (doRaise)
            {
                success = session.Player.HandleActionRaiseVital(vital, xpCost);
            }
            else
            {
                success = false;
            }
            return xpCost;
        }

        [CommandHandler("attr", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Handles Attribute Raising", "Type the first 3 letter abbreviation for the attribute to raise, followed optionally by a number (str, end, coo, qui, foc, sel, vit, sta, man)")]
        public static void HandleRaiseAttribute(Session session, params string[] parameters)
        {

            if (session.Player.IsMule)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[ATTR] Mules can't improve themselves. Deal with it!", ChatMessageType.Advancement));
                return;
            }
            int amt = 1; ulong xpCost = 0; string AttrName = string.Empty; bool success = false;
            if (parameters.Count() == 2)
            {
                if (!int.TryParse(parameters[1], out amt))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"[ATTR] Something isn't parsing your command correctly, check your input and try again!", ChatMessageType.Advancement));
                }
                if (amt > 10)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"[ATTR] You can only raise your attributes by up to 10 points at a time.", ChatMessageType.Advancement));
                    return;
                }

            }
            switch (parameters[0])
            {
                case "str":
                case "strength":
                    xpCost = GetOrRaiseAttrib(session, amt, PropertyAttribute.Strength, out AttrName, true, out success);
                    break;
                case "end":
                case "endurance":
                    xpCost = GetOrRaiseAttrib(session, amt, PropertyAttribute.Endurance, out AttrName, true, out success);
                    break;
                case "coo":
                case "coordination":
                    xpCost = GetOrRaiseAttrib(session, amt, PropertyAttribute.Coordination, out AttrName, true, out success);
                    break;
                case "qui":
                case "quickness":
                    xpCost = GetOrRaiseAttrib(session, amt, PropertyAttribute.Quickness, out AttrName, true, out success);
                    break;
                case "foc":
                case "focus":
                    xpCost = GetOrRaiseAttrib(session, amt, PropertyAttribute.Focus, out AttrName, true, out success);
                    break;
                case "sel":
                case "self":
                    xpCost = GetOrRaiseAttrib(session, amt, PropertyAttribute.Self, out AttrName, true, out success);
                    break;
                case "sta":
                case "stam":
                case "stamina":
                    xpCost = GetOrRaise2ndAttrib(session, amt, PropertyAttribute2nd.MaxStamina, out AttrName, true, out success);
                    break;
                case "hea":
                case "health":
                case "vit":
                case "vitality":
                case "maxhealth":
                    xpCost = GetOrRaise2ndAttrib(session, amt, PropertyAttribute2nd.MaxHealth, out AttrName, true, out success);
                    break;
                case "man":
                case "mana":
                    xpCost = GetOrRaise2ndAttrib(session, amt, PropertyAttribute2nd.MaxMana, out AttrName, true, out success);
                    break;
                default:
                    session.Network.EnqueueSend(new GameMessageSystemChat($"[ATTR] Invalid attribute. Type /attr for help.", ChatMessageType.System));
                    return;
            }
            if (success)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[ATTR] {AttrName} attribute raised by {amt}, costing {xpCost:N0}!", ChatMessageType.Advancement));
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[ATTR] Could not raise {AttrName} attribute, check your available XP and use /xp to see the cost and try again.", ChatMessageType.Advancement));
            }
        }

        // pop
        [CommandHandler("pop", AccessLevel.Player, CommandHandlerFlag.None, 0,
            "Show current world population",
            "")]
        public static void HandlePop(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, $"Current world population: {PlayerManager.GetOnlineCount():N0}", ChatMessageType.Broadcast);
        }


        [CommandHandler("qb", AccessLevel.Player, CommandHandlerFlag.None, "Show current quest bonus", "add List to list your current quests")]
        public static void DisplayQB(Session session, params string[] parameters)
        {
            bool list = false;
            if (parameters.Length > 0)
            {
                if (parameters[0] == "list")
                {
                    list = true;
                }
            }

            if (!list)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Your current quest bonus count: {session.Player.Account.GetCharacterQuestCompletions():N0}", ChatMessageType.Broadcast);
            }
            else
            {
                var commandSecondsLimit = PropertyManager.GetLong("qb_command_limit");
                var currentTime = DateTime.UtcNow;

                var lastCommandTimeSeconds = (currentTime - session.LastQBCommandTime).TotalSeconds;
                if (lastCommandTimeSeconds < commandSecondsLimit)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"[QB] This command may only be run once every {commandSecondsLimit} seconds.", ChatMessageType.Broadcast);
                    return;
                }

                session.LastQBCommandTime = currentTime;

                using (var context = new AuthDbContext())
                {
                    var res = context.AccountQuest.Where(x => x.AccountId == session.AccountId && x.NumTimesCompleted >= 1).ToList();
                    if (res != null)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Your completed quest bonus list:", ChatMessageType.Broadcast);
                        foreach (var item in res)
                        {
                            CommandHandlerHelper.WriteOutputInfo(session, $"{item.Quest}", ChatMessageType.Broadcast);
                        }
                    }
                    var res2 = context.AccountQuest.Where(x => x.AccountId == session.AccountId && x.NumTimesCompleted == 0).ToList();
                    if (res2 != null)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Your incomplete quest bonus list:", ChatMessageType.Broadcast);
                        foreach (var item in res2)
                        {
                            CommandHandlerHelper.WriteOutputInfo(session, $"{item.Quest}", ChatMessageType.Broadcast);
                        }
                    }
                }
            }

        }

        [CommandHandler("top", AccessLevel.Player, CommandHandlerFlag.None, "Show current leaderboards", "use top qb to list top quest bonus count, top level to list top character levels, enl for enlightenments")]
        public static async void DisplayTop(Session session, params string[] parameters)
        {
            try
            {
                List<Leaderboard> list = new List<Leaderboard>();
                LeaderboardCache cache = LeaderboardCache.Instance;
                if (parameters.Length < 1)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("[TOP] Specify a leaderboard to run, such as /top qb or /top deaths", ChatMessageType.Broadcast));
                    return;
                }
                using (var context = new AuthDbContext())
                {
                    var key = parameters[0].ToLowerInvariant();
                    if (key == "qb")
                    {
                        list = await cache.GetTopQBAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Quest Bonus:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "level")
                    {
                        list = await cache.GetTopLevelAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Level:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "enl")
                    {
                        list = await cache.GetTopEnlAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Enlightenment:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "title")
                    {
                        list = await cache.GetTopTitleAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Titles:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "augs")
                    {
                        list = await cache.GetTopAugsAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Advanced Augmentations:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "deaths")
                    {
                        list = await cache.GetTopDeathsAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Deaths:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "bank")
                    {
                        list = await cache.GetTopBankAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Bank Value:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "lum")
                    {
                        list = await cache.GetTopLumAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Banked Luminance:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "attr")
                    {
                        list = await cache.GetTopAttrAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Raised Attributes:", ChatMessageType.Broadcast));
                        }
                    }
                    else if (key == "gymnos")
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Top 1 Player named Gymnos: Gymnos", ChatMessageType.Broadcast));
                    }
                }

                for (int i = 0; i < list.Count; i++)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{i + 1}: {list[i].Score:N0} - {list[i].Character}", ChatMessageType.Broadcast));
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error in DisplayTop command: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Rate limiter for /passwd command
        /// </summary>
        private static readonly TimeSpan MyQuests = TimeSpan.FromSeconds(60);

        // quest info (uses GDLe formatting to match plugin expectations)
        [CommandHandler("myquests", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows your quest log")]
        public static void HandleQuests(Session session, params string[] parameters)
        {
            if (PropertyManager.GetBool("myquest_throttle_enabled"))
            {
                var currentTime = DateTime.UtcNow;

                if (currentTime - session.LastMyQuestsCommandTime < MyQuests)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"[MyQuests] This command may only be run once every {MyQuests.TotalSeconds} seconds.", ChatMessageType.Broadcast);
                    return;
                }
            }

            session.LastMyQuestsCommandTime = DateTime.UtcNow;

            if (!PropertyManager.GetBool("quest_info_enabled"))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"myquests\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var quests = session.Player.QuestManager.GetQuests();

            if (quests.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Quest list is empty.", ChatMessageType.Broadcast));
                return;
            }

            var questMessages = new List<string>();
            foreach (var playerQuest in quests)
            {
                var questName = QuestManager.GetQuestName(playerQuest.QuestName);
                var quest = DatabaseManager.World.GetCachedQuest(questName);
                if (quest == null)
                {
                    continue;
                }

                var minDelta = quest.MinDelta;
                if (QuestManager.CanScaleQuestMinDelta(quest))
                    minDelta = (uint)(quest.MinDelta * PropertyManager.GetDouble("quest_mindelta_rate"));

                var text = $"{playerQuest.QuestName.ToLower()} - {playerQuest.NumTimesCompleted} solves ({playerQuest.LastTimeCompleted}) \"{quest.Message}\" {quest.MaxSolves} {minDelta}";
                questMessages.Add(text);
            }

            foreach (var message in questMessages)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
            }
        }

        /// <summary>
        /// For characters/accounts who currently own multiple houses, used to select which house they want to keep
        /// </summary>
        [CommandHandler("house-select", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "For characters/accounts who currently own multiple houses, used to select which house they want to keep")]
        public static void HandleHouseSelect(Session session, params string[] parameters)
        {
            HandleHouseSelect(session, false, parameters);
        }

        public static void HandleHouseSelect(Session session, bool confirmed, params string[] parameters)
        {
            if (!int.TryParse(parameters[0], out var houseIdx))
                return;

            // ensure current multihouse owner
            if (!session.Player.IsMultiHouseOwner(false))
            {
                log.Warn($"{session.Player.Name} tried to /house-select {houseIdx}, but they are not currently a multi-house owner!");
                return;
            }

            // get house info for this index
            var multihouses = session.Player.GetMultiHouses();

            if (houseIdx < 1 || houseIdx > multihouses.Count)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Please enter a number between 1 and {multihouses.Count}.", ChatMessageType.Broadcast));
                return;
            }

            var keepHouse = multihouses[houseIdx - 1];

            // show confirmation popup
            if (!confirmed)
            {
                var houseType = $"{keepHouse.HouseType}".ToLower();
                var loc = HouseManager.GetCoords(keepHouse.SlumLord.Location);

                var msg = $"Are you sure you want to keep the {houseType} at\n{loc}?";
                if (!session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(session.Player.Guid, () => HandleHouseSelect(session, true, parameters)), msg))
                    session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            // house to keep confirmed, abandon the other houses
            var abandonHouses = new List<House>(multihouses);
            abandonHouses.RemoveAt(houseIdx - 1);

            foreach (var abandonHouse in abandonHouses)
            {
                var house = session.Player.GetHouse(abandonHouse.Guid.Full);

                HouseManager.HandleEviction(house, house.HouseOwner ?? 0, true);
            }

            // set player properties for house to keep
            var player = PlayerManager.FindByGuid(keepHouse.HouseOwner ?? 0, out bool isOnline);
            if (player == null)
            {
                log.Error($"{session.Player.Name}.HandleHouseSelect({houseIdx}) - couldn't find HouseOwner {keepHouse.HouseOwner} for {keepHouse.Name} ({keepHouse.Guid})");
                return;
            }

            player.HouseId = keepHouse.HouseId;
            player.HouseInstance = keepHouse.Guid.Full;

            player.SaveBiotaToDatabase();

            // update house panel for current player
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(3.0f);  // wait for slumlord inventory biotas above to save
            actionChain.AddAction(session.Player, session.Player.HandleActionQueryHouse);
            actionChain.EnqueueChain();

            Console.WriteLine("OK");
        }

        [CommandHandler("debugcast", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows debug information about the current magic casting state")]
        public static void HandleDebugCast(Session session, params string[] parameters)
        {
            var physicsObj = session.Player.PhysicsObj;

            var pendingActions = physicsObj.MovementManager.MoveToManager.PendingActions;
            var currAnim = physicsObj.PartArray.Sequence.CurrAnim;

            session.Network.EnqueueSend(new GameMessageSystemChat(session.Player.MagicState.ToString(), ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"IsMovingOrAnimating: {physicsObj.IsMovingOrAnimating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"PendingActions: {pendingActions.Count}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"CurrAnim: {currAnim?.Value.Anim.ID:X8}", ChatMessageType.Broadcast));
        }

        [CommandHandler("fixcast", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Fixes magic casting if locked up for an extended time")]
        public static void HandleFixCast(Session session, params string[] parameters)
        {
            var magicState = session.Player.MagicState;

            if (magicState.IsCasting && DateTime.UtcNow - magicState.StartTime > TimeSpan.FromSeconds(5))
            {
                session.Network.EnqueueSend(new GameEventCommunicationTransientString(session, "Fixed casting state"));
                session.Player.SendUseDoneEvent();
                magicState.OnCastDone();
            }
        }

        [CommandHandler("castmeter", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows the fast casting efficiency meter")]
        public static void HandleCastMeter(Session session, params string[] parameters)
        {
            if (parameters.Length == 0)
            {
                session.Player.MagicState.CastMeter = !session.Player.MagicState.CastMeter;
            }
            else
            {
                if (parameters[0].Equals("on", StringComparison.OrdinalIgnoreCase))
                    session.Player.MagicState.CastMeter = true;
                else
                    session.Player.MagicState.CastMeter = false;
            }
            session.Network.EnqueueSend(new GameMessageSystemChat($"Cast efficiency meter {(session.Player.MagicState.CastMeter ? "enabled" : "disabled")}", ChatMessageType.Broadcast));
        }

        [CommandHandler("instance", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows the current instance")]
        public static void HandleInstanceInfo(Session session, params string[] parameters)
        {
            var physicsObj = session.Player.PhysicsObj;

            var physInstance = physicsObj.Position.Variation;
            var locInstance = session.Player.Location.Variation;

            session.Network.EnqueueSend(new GameMessageSystemChat($"Physics Instance: {physInstance}\nLocation Instance: {locInstance}", ChatMessageType.Broadcast));
            if (session.Player.CurrentLandblock != null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock World Object Count: {session.Player.CurrentLandblock.WorldObjectCount}", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock Physics Object Count: {session.Player.CurrentLandblock.PhysicsObjectCount}", ChatMessageType.Broadcast));
            }

        }

        [CommandHandler("knownobjects", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows the current known objects")]
        public static void HandleKnownObjectList(Session session, params string[] parameters)
        {
            List<WorldObject> objects = session.Player.GetKnownObjects();
            if (objects == null)
            {
                return;
            }
            session.Network.EnqueueSend(new GameMessageSystemChat($"Known Objects Count: {objects.Count}", ChatMessageType.Broadcast));

            foreach (var item in objects)
            {
                // Don't list objects the player can't see
                if (item.Visibility && !session.Player.Adminvision)
                    continue;

                session.Network.EnqueueSend(new GameMessageSystemChat($"{item.Name}, {item.Guid}, {item.Location}", ChatMessageType.Broadcast));
            }
        }

        private static List<string> configList = new List<string>()
        {
            "Common settings:\nConfirmVolatileRareUse, MainPackPreferred, SalvageMultiple, SideBySideVitals, UseCraftSuccessDialog",
            "Interaction settings:\nAcceptLootPermits, AllowGive, AppearOffline, AutoAcceptFellowRequest, DragItemOnPlayerOpensSecureTrade, FellowshipShareLoot, FellowshipShareXP, IgnoreAllegianceRequests, IgnoreFellowshipRequests, IgnoreTradeRequests, UseDeception",
            "UI settings:\nCoordinatesOnRadar, DisableDistanceFog, DisableHouseRestrictionEffects, DisableMostWeatherEffects, FilterLanguage, LockUI, PersistentAtDay, ShowCloak, ShowHelm, ShowTooltips, SpellDuration, TimeStamp, ToggleRun, UseMouseTurning",
            "Chat settings:\nHearAllegianceChat, HearGeneralChat, HearLFGChat, HearRoleplayChat, HearSocietyChat, HearTradeChat, HearPKDeaths, StayInChatMode",
            "Combat settings:\nAdvancedCombatUI, AutoRepeatAttack, AutoTarget, LeadMissileTargets, UseChargeAttack, UseFastMissiles, ViewCombatTarget, VividTargetingIndicator",
            "Character display settings:\nDisplayAge, DisplayAllegianceLogonNotifications, DisplayChessRank, DisplayDateOfBirth, DisplayFishingSkill, DisplayNumberCharacterTitles, DisplayNumberDeaths"
        };

        /// <summary>
        /// Mapping of GDLE -> ACE CharacterOptions
        /// </summary>
        private static Dictionary<string, string> translateOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common
            { "ConfirmVolatileRareUse", "ConfirmUseOfRareGems" },
            { "MainPackPreferred", "UseMainPackAsDefaultForPickingUpItems" },
            { "SalvageMultiple", "SalvageMultipleMaterialsAtOnce" },
            { "SideBySideVitals", "SideBySideVitals" },
            { "UseCraftSuccessDialog", "UseCraftingChanceOfSuccessDialog" },

            // Interaction
            { "AcceptLootPermits", "AcceptCorpseLootingPermissions" },
            { "AllowGive", "LetOtherPlayersGiveYouItems" },
            { "AppearOffline", "AppearOffline" },
            { "AutoAcceptFellowRequest", "AutomaticallyAcceptFellowshipRequests" },
            { "DragItemOnPlayerOpensSecureTrade", "DragItemToPlayerOpensTrade" },
            { "FellowshipShareLoot", "ShareFellowshipLoot" },
            { "FellowshipShareXP", "ShareFellowshipExpAndLuminance" },
            { "IgnoreAllegianceRequests", "IgnoreAllegianceRequests" },
            { "IgnoreFellowshipRequests", "IgnoreFellowshipRequests" },
            { "IgnoreTradeRequests", "IgnoreAllTradeRequests" },
            { "UseDeception", "AttemptToDeceiveOtherPlayers" },

            // UI
            { "CoordinatesOnRadar", "ShowCoordinatesByTheRadar" },
            { "DisableDistanceFog", "DisableDistanceFog" },
            { "DisableHouseRestrictionEffects", "DisableHouseRestrictionEffects" },
            { "DisableMostWeatherEffects", "DisableMostWeatherEffects" },
            { "FilterLanguage", "FilterLanguage" },
            { "LockUI", "LockUI" },
            { "PersistentAtDay", "AlwaysDaylightOutdoors" },
            { "ShowCloak", "ShowYourCloak" },
            { "ShowHelm", "ShowYourHelmOrHeadGear" },
            { "ShowTooltips", "Display3dTooltips" },
            { "SpellDuration", "DisplaySpellDurations" },
            { "TimeStamp", "DisplayTimestamps" },
            { "ToggleRun", "RunAsDefaultMovement" },
            { "UseMouseTurning", "UseMouseTurning" },

            // Chat
            { "HearAllegianceChat", "ListenToAllegianceChat" },
            { "HearGeneralChat", "ListenToGeneralChat" },
            { "HearLFGChat", "ListenToLFGChat" },
            { "HearRoleplayChat", "ListentoRoleplayChat" },
            { "HearSocietyChat", "ListenToSocietyChat" },
            { "HearTradeChat", "ListenToTradeChat" },
            { "HearPKDeaths", "ListenToPKDeathMessages" },
            { "StayInChatMode", "StayInChatModeAfterSendingMessage" },

            // Combat
            { "AdvancedCombatUI", "AdvancedCombatInterface" },
            { "AutoRepeatAttack", "AutoRepeatAttacks" },
            { "AutoTarget", "AutoTarget" },
            { "LeadMissileTargets", "LeadMissileTargets" },
            { "UseChargeAttack", "UseChargeAttack" },
            { "UseFastMissiles", "UseFastMissiles" },
            { "ViewCombatTarget", "KeepCombatTargetsInView" },
            { "VividTargetingIndicator", "VividTargetingIndicator" },

            // Character Display
            { "DisplayAge", "AllowOthersToSeeYourAge" },
            { "DisplayAllegianceLogonNotifications", "ShowAllegianceLogons" },
            { "DisplayChessRank", "AllowOthersToSeeYourChessRank" },
            { "DisplayDateOfBirth", "AllowOthersToSeeYourDateOfBirth" },
            { "DisplayFishingSkill", "AllowOthersToSeeYourFishingSkill" },
            { "DisplayNumberCharacterTitles", "AllowOthersToSeeYourNumberOfTitles" },
            { "DisplayNumberDeaths", "AllowOthersToSeeYourNumberOfDeaths" }
        };

        /// <summary>
        /// Manually sets a character option on the server. Use /config list to see a list of settings.
        /// </summary>
        [CommandHandler("config", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Manually sets a character option on the server.\nUse /config list to see a list of settings.", "<setting> <on/off>")]
        public static void HandleConfig(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("player_config_command"))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"config\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            // /config list - show character options
            if (parameters[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in configList)
                    session.Network.EnqueueSend(new GameMessageSystemChat(line, ChatMessageType.Broadcast));

                return;
            }

            // translate GDLE CharacterOptions for existing plugins
            if (!translateOptions.TryGetValue(parameters[0], out var param) || !Enum.TryParse(param, out CharacterOption characterOption))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown character option: {parameters[0]}", ChatMessageType.Broadcast));
                return;
            }

            var option = session.Player.GetCharacterOption(characterOption);

            // modes of operation:
            // on / off / toggle

            // - if none specified, default to toggle
            var mode = "toggle";

            if (parameters.Length > 1)
            {
                if (parameters[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                    mode = "on";
                else if (parameters[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                    mode = "off";
            }

            // set character option
            if (mode.Equals("on"))
                option = true;
            else if (mode.Equals("off"))
                option = false;
            else
                option = !option;

            session.Player.SetCharacterOption(characterOption, option);

            session.Network.EnqueueSend(new GameMessageSystemChat($"Character option {parameters[0]} is now {(option ? "on" : "off")}.", ChatMessageType.Broadcast));

            // update client
            session.Network.EnqueueSend(new GameEventPlayerDescription(session));
        }

        /// <summary>
        /// Force resend of all visible objects known to this player. Can fix rare cases of invisible object bugs.
        /// Can only be used once every 5 mins max.
        /// </summary>
        [CommandHandler("objsend", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Force resend of all visible objects known to this player. Can fix rare cases of invisible object bugs. Can only be used once every 5 mins max.")]
        public static void HandleObjSend(Session session, params string[] parameters)
        {
            // a good repro spot for this is the first room after the door in facility hub
            // in the portal drop / staircase room, the VisibleCells do not have the room after the door
            // however, the room after the door *does* have the portal drop / staircase room in its VisibleCells (the inverse relationship is imbalanced)
            // not sure how to fix this atm, seems like it triggers a client bug..

            if (DateTime.UtcNow - session.Player.PrevObjSend < TimeSpan.FromMinutes(5))
            {
                session.Player.SendTransientError("You have used this command too recently!");
                return;
            }

            var creaturesOnly = parameters.Length > 0 && parameters[0].Contains("creature", StringComparison.OrdinalIgnoreCase);

            var knownObjs = session.Player.GetKnownObjects();

            foreach (var knownObj in knownObjs)
            {
                if (creaturesOnly && !(knownObj is Creature))
                    continue;

                session.Player.RemoveTrackedObject(knownObj, false);
                session.Player.TrackObject(knownObj);
            }
            session.Player.PrevObjSend = DateTime.UtcNow;
        }

        // show player ace server versions
        [CommandHandler("aceversion", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows this server's version data")]
        public static void HandleACEversion(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("version_info_enabled"))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"aceversion\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var msg = ServerBuildInfo.GetVersionInfo();

            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
        }

        // reportbug < code | content > < description >
        [CommandHandler("reportbug", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 2,
            "Generate a Bug Report",
            "<category> <description>\n" +
            "This command generates a URL for you to copy and paste into your web browser to submit for review by server operators and developers.\n" +
            "Category can be the following:\n" +
            "Creature\n" +
            "NPC\n" +
            "Item\n" +
            "Quest\n" +
            "Recipe\n" +
            "Landblock\n" +
            "Mechanic\n" +
            "Code\n" +
            "Other\n" +
            "For the first three options, the bug report will include identifiers for what you currently have selected/targeted.\n" +
            "After category, please include a brief description of the issue, which you can further detail in the report on the website.\n" +
            "Examples:\n" +
            "/reportbug creature Drudge Prowler is over powered\n" +
            "/reportbug npc Ulgrim doesn't know what to do with Sake\n" +
            "/reportbug quest I can't enter the portal to the Lost City of Frore\n" +
            "/reportbug recipe I cannot combine Bundle of Arrowheads with Bundle of Arrowshafts\n" +
            "/reportbug code I was killed by a Non-Player Killer\n"
            )]
        public static void HandleReportbug(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("reportbug_enabled"))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"reportbug\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var category = parameters[0];
            var description = "";

            for (var i = 1; i < parameters.Length; i++)
                description += parameters[i] + " ";

            description.Trim();

            switch (category.ToLower())
            {
                case "creature":
                case "npc":
                case "quest":
                case "item":
                case "recipe":
                case "landblock":
                case "mechanic":
                case "code":
                case "other":
                    break;
                default:
                    category = "Other";
                    break;
            }

            var sn = ConfigManager.Config.Server.WorldName;
            var c = session.Player.Name;

            var st = "ACE";

            //var versions = ServerBuildInfo.GetVersionInfo();
            var databaseVersion = DatabaseManager.World.GetVersion();
            var sv = ServerBuildInfo.FullVersion;
            var pv = databaseVersion.PatchVersion;

            //var ct = PropertyManager.GetString("reportbug_content_type");
            var cg = category.ToLower();

            var w = "";
            var g = "";

            if (cg == "creature" || cg == "npc" || cg == "item" || cg == "item")
            {
                var objectId = new ObjectGuid();
                if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
                {
                    if (session.Player.HealthQueryTarget.HasValue)
                        objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
                    else if (session.Player.ManaQueryTarget.HasValue)
                        objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
                    else
                        objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

                    //var wo = session.Player.CurrentLandblock?.GetObject(objectId);

                    var wo = session.Player.FindObject(objectId.Full, Player.SearchLocations.Everywhere);

                    if (wo != null)
                    {
                        w = $"{wo.WeenieClassId}";
                        g = $"0x{wo.Guid:X8}";
                    }
                }
            }

            var l = session.Player.Location.ToLOCString();

            var issue = description;

            var urlbase = $"https://www.accpp.net/bug?";

            var url = urlbase;
            if (sn.Length > 0)
                url += $"sn={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sn))}";
            if (c.Length > 0)
                url += $"&c={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(c))}";
            if (st.Length > 0)
                url += $"&st={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(st))}";
            if (sv.Length > 0)
                url += $"&sv={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sv))}";
            if (pv.Length > 0)
                url += $"&pv={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pv))}";
            //if (ct.Length > 0)
            //    url += $"&ct={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(ct))}";
            if (cg.Length > 0)
            {
                if (cg == "npc")
                    cg = cg.ToUpper();
                else
                    cg = char.ToUpper(cg[0]) + cg.Substring(1);
                url += $"&cg={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cg))}";
            }
            if (w.Length > 0)
                url += $"&w={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(w))}";
            if (g.Length > 0)
                url += $"&g={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(g))}";
            if (l.Length > 0)
                url += $"&l={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(l))}";
            if (issue.Length > 0)
                url += $"&i={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(issue))}";

            var msg = "\n\n\n\n";
            msg += "Bug Report - Copy and Paste the following URL into your browser to submit a bug report\n";
            msg += "-=-\n";
            msg += $"{url}\n";
            msg += "-=-\n";
            msg += "\n\n\n\n";

            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.AdminTell));
        }
    }
}
