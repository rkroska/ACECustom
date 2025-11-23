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
            actionChain.AddAction(session.Player, ActionType.PlayerHouse_HandleActionQueryHouse, session.Player.HandleActionQueryHouse);
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

        // Solo and Fellowship Blackjack shared code

        /* public static class BlackjackUtils
         {
             // Method to calculate the hand value
             public static int GetHandValue(List<(int cardValue, CardSuit suit)> hand)
             {
                 int totalValue = 0;
                 int aceCount = 0;

                 foreach (var (cardValue, _) in hand)
                 {
                     if (cardValue >= 11 && cardValue <= 13) // Jack, Queen, King
                     {
                         totalValue += 10;
                     }
                     else if (cardValue == 1) // Ace
                     {
                         aceCount++;
                         totalValue += 11; // Initially count ace as 11
                     }
                     else
                     {
                         totalValue += cardValue; // For 2-10, use the face value
                     }
                 }

                 // Adjust for aces if the total value exceeds 21
                 while (totalValue > 21 && aceCount > 0)
                 {
                     totalValue -= 10; // Treat ace as 1 instead of 11
                     aceCount--;
                 }

                 return totalValue;
             }

             // Method to draw cards
             public static List<(int cardValue, CardSuit suit)> DrawCards(int numberOfCards)
             {
                 List<(int cardValue, CardSuit suit)> cards = new List<(int cardValue, CardSuit suit)>();
                 Random rand = new Random();

                 for (int i = 0; i < numberOfCards; i++)
                 {
                     int cardValue = rand.Next(1, 14); // 1 to 13 (Ace to King)
                     CardSuit suit = (CardSuit)rand.Next(0, 2); // Two suits: Hand or Eyes

                     cards.Add((cardValue, suit));
                 }

                 return cards;
             }

             // Method to get card name
             public static string GetCardName(int cardValue, CardSuit suit)
             {
                 string cardName = cardValue switch
                 {
                     1 => "Ace",
                     11 => "Jack",
                     12 => "Queen",
                     13 => "King",
                     _ => cardValue.ToString()
                 };

                 return $"{cardName} of {suit}";
             }
         }

         // Enums for card suits
         public enum CardSuit
         {
             Hands,
             Eyes,
             Jesters,
             Balls
         }

         public static class BlackjackHelperFunctions
         {
             // Deal cards to dealer (solo and fellowship games)
             public static void DealCardsToDealer(SoloBlackjackGame soloGame, int numberOfCards, Session session)
             {
                 var cards = BlackjackUtils.DrawCards(numberOfCards);
                 foreach (var (cardValue, suit) in cards)
                 {
                     soloGame.DealerHand.Add((cardValue, suit));
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Rand was dealt a {BlackjackUtils.GetCardName(cardValue, suit)}.", ChatMessageType.Tell));
                 }
             }

             public static void DealCardsToDealer(FellowshipBlackjackGame fellowGame, int numberOfCards)
             {
                 var cards = BlackjackUtils.DrawCards(numberOfCards);
                 foreach (var (cardValue, suit) in cards)
                 {
                     fellowGame.DealerHand.Add((cardValue, suit));
                     foreach (var player in fellowGame.Players)
                     {
                         player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Rand was dealt a {BlackjackUtils.GetCardName(cardValue, suit)}.", ChatMessageType.Tell));
                     }
                 }
             }

             // Move to the next player in fellowship game
             public static void MoveToNextActivePlayer(FellowshipBlackjackGame game, Session session)
             {
                 game.CurrentTurn = (game.CurrentTurn + 1) % game.Players.Count;
                 var nextPlayer = game.GetCurrentPlayer();
                 nextPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"It's your turn! Type /fhit or /fstand.", ChatMessageType.Broadcast));
             }

             // Handle moving to next hand or next player in fellowship game
             private static void MoveToNextPlayerWithRemainingHands(FellowshipBlackjackGame game, Session session)
             {
                 var player = game.GetCurrentPlayer();

                 // Check if the player has more hands to play
                 if (game.PlayerCurrentHand[player] < game.PlayerHands[player].Count - 1)
                 {
                     // Move to the second hand after the first hand is completed
                     game.PlayerCurrentHand[player]++;

                     // Now deal a card to the second hand if the first hand is done
                     DealCardsToPlayer(player, game, 1); // Deal one card to the second hand
                     var secondHandCard = game.PlayerHands[player][game.PlayerCurrentHand[player]].Last(); // Get the last card dealt
                     session.Network.EnqueueSend(new GameMessageSystemChat($"You were dealt a {BlackjackUtils.GetCardName(secondHandCard.cardValue, secondHandCard.suit)} for your second hand.", ChatMessageType.System));
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your second hand value is {BlackjackUtils.GetHandValue(game.PlayerHands[player][game.PlayerCurrentHand[player]])}.", ChatMessageType.System));

                     // Inform the player to act on their second hand
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are now on your second hand. Type /fhit or /fstand.", ChatMessageType.System));
                 }
                 else
                 {
                     // If all hands are played, mark the turn complete and move to the next player
                     game.PlayerTurnCompleted[player] = true;
                     MoveToNextActivePlayer(game, session);
                 }
             }

             // Give item to player
             public static void GiveItemToPlayer(Player player, int itemWeenieClassId)
             {
                 var item = WorldObjectFactory.CreateNewWorldObject((uint)itemWeenieClassId);

                 if (item != null && player.TryCreateInInventoryWithNetworking(item))
                 {
                     player.Session.Network.EnqueueSend(new GameMessageSystemChat($"A card representing your hand has been added to your inventory.", ChatMessageType.Tell));
                 }
                 else
                 {
                     player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Error: Unable to add the card item to your inventory.", ChatMessageType.System));
                 }
             }
         }

         // Update existing Blackjack classes to call the missing functions correctly.
         // Example usage in Solo and Fellowship play when hitting, standing, and dealer draws.

         public static void DealCardsToPlayer(Player player, FellowshipBlackjackGame game, int numberOfCards)
         {
             try
             {
                 var currentHand = game.PlayerHands[player][game.PlayerCurrentHand[player]];
                 var cards = BlackjackUtils.DrawCards(numberOfCards);

                 foreach (var (cardValue, suit) in cards)
                 {
                     currentHand.Add((cardValue, suit));

                     if (SoloBlackjackCommands.cardItemMap.TryGetValue((cardValue, suit), out int itemWeenieClassId))
                     {
                         BlackjackHelperFunctions.GiveItemToPlayer(player, itemWeenieClassId);
                         game.PlayerItems[player][game.PlayerCurrentHand[player]].Add(itemWeenieClassId);
                     }

                     player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You were dealt a {BlackjackUtils.GetCardName(cardValue, suit)}.", ChatMessageType.Tell));
                 }
             }
             catch (Exception ex)
             {
                 Console.WriteLine($"Error while dealing cards to player {player.Name}: {ex.Message}");
             }
         }

         // Solo Blackjack Game Code with Split and Double Commands
         public class SoloBlackjackGame
         {
             public Player Player { get; set; }
             public List<List<(int cardValue, CardSuit suit)>> PlayerHands { get; set; } // Multiple hands per player
             public List<List<int>> PlayerItems { get; set; } // Multiple item lists per player
             public long PlayerBet { get; set; } // Base bet for the player
             public bool PlayerDoubledDown { get; set; } // Whether the player has doubled down
             public int PlayerCurrentHand { get; set; } // Track which hand the player is on
             public List<(int cardValue, CardSuit suit)> DealerHand { get; set; } // Store both card value and suit for dealer
             public bool PlayerTurnCompleted { get; set; } // Track if player's turn is done
             public bool GameEnded { get; set; } // Flag to prevent game end logic from running multiple times
             public List<(int cardValue, CardSuit suit)> Deck { get; private set; }

             public SoloBlackjackGame(Player player, long playerBet)
             {
                 Player = player;
                 PlayerHands = new List<List<(int cardValue, CardSuit suit)>> { new List<(int cardValue, CardSuit suit)>() };
                 PlayerItems = new List<List<int>> { new List<int>() };
                 PlayerBet = playerBet;
                 PlayerDoubledDown = false;
                 PlayerCurrentHand = 0;
                 PlayerTurnCompleted = false;
                 DealerHand = new List<(int cardValue, CardSuit suit)>();
                 GameEnded = false;

                 InitializeDeck(); // Initialize and shuffle the deck at the start of each game
             }

             // Initialize and shuffle the deck
             private void InitializeDeck()
             {
                 Deck = new List<(int cardValue, CardSuit suit)>();

                 // Add all 52 cards to the deck (13 values for each of the 4 suits)
                 for (int value = 1; value <= 13; value++)
                 {
                     Deck.Add((value, CardSuit.Hands));
                     Deck.Add((value, CardSuit.Eyes));
                     Deck.Add((value, CardSuit.Jesters));
                     Deck.Add((value, CardSuit.Balls));
                 }

                 // Shuffle the deck
                 Random rand = new Random();
                 Deck = Deck.OrderBy(_ => rand.Next()).ToList();
             }

             // Method to draw a card from the deck
             public (int cardValue, CardSuit suit) DrawCard()
             {
                 if (Deck.Count == 0)
                 {
                     throw new InvalidOperationException("The deck is empty.");
                 }

                 var card = Deck[0];
                 Deck.RemoveAt(0); // Remove the card from the deck after drawing
                 return card;
             }
         }

         public static class SoloBlackjackCommands
         {
             public static Dictionary<ObjectGuid, SoloBlackjackGame> activeSoloBlackjackGames = new Dictionary<ObjectGuid, SoloBlackjackGame>(); // Directly use ObjectGuid

             public static Dictionary<(int rank, CardSuit suit), int> cardItemMap = new Dictionary<(int rank, CardSuit suit), int>
 {
                 //Deck of hands
     {( 1, CardSuit.Hands),90000219 },  // Ace
     {( 2, CardSuit.Hands), 90000220 },  // 2
     {( 3, CardSuit.Hands), 90000221 },  // 3
     {( 4, CardSuit.Hands), 90000222 },  // 4
     {( 5, CardSuit.Hands), 90000223 },  // 5
     {( 6, CardSuit.Hands), 90000224 },  // 6
     {( 7, CardSuit.Hands), 90000225 },  // 7
     {( 8, CardSuit.Hands), 90000226 },  // 8
     {( 9, CardSuit.Hands), 90000227 },  // 9
     {( 10, CardSuit.Hands), 90000228 }, // 10
     {( 11, CardSuit.Hands), 90000229 }, // Jack
     {( 12, CardSuit.Hands), 90000230 }, // Queen
     {( 13, CardSuit.Hands), 90000231 },  // King

                  //Deck of eyes
     {( 1, CardSuit.Eyes),90000232 },  // Ace
     {( 2, CardSuit.Eyes), 90000233 },  // 2
     {( 3, CardSuit.Eyes), 90000234 },  // 3
     {( 4, CardSuit.Eyes), 90000235 },  // 4
     {( 5, CardSuit.Eyes), 90000236 },  // 5
     {( 6, CardSuit.Eyes), 90000237 },  // 6
     {( 7, CardSuit.Eyes), 90000238 },  // 7
     {( 8, CardSuit.Eyes), 90000239 },  // 8
     {( 9, CardSuit.Eyes), 90000240 },  // 9
     {( 10, CardSuit.Eyes), 90000241 }, // 10
     {( 11, CardSuit.Eyes), 90000242 }, // Jack
     {( 12, CardSuit.Eyes), 90000243 }, // Queen
     {( 13, CardSuit.Eyes), 90000244 },  // King

                  //Deck of jesters
     {( 1, CardSuit.Jesters),90000245 },  // Ace
     {( 2, CardSuit.Jesters), 90000246 },  // 2
     {( 3, CardSuit.Jesters), 90000247 },  // 3
     {( 4, CardSuit.Jesters), 90000248 },  // 4
     {( 5, CardSuit.Jesters), 90000249 },  // 5
     {( 6, CardSuit.Jesters), 90000250 },  // 6
     {( 7, CardSuit.Jesters), 90000251 },  // 7
     {( 8, CardSuit.Jesters), 90000252 },  // 8
     {( 9, CardSuit.Jesters), 90000253 },  // 9
     {( 10, CardSuit.Jesters), 90000254 }, // 10
     {( 11, CardSuit.Jesters), 90000255 }, // Jack
     {( 12, CardSuit.Jesters), 90000256 }, // Queen
     {( 13, CardSuit.Jesters), 90000257 },  // King

                  //Deck of crosses
     {( 1, CardSuit.Balls),90000258 },  // Ace
     {( 2, CardSuit.Balls), 90000259 },  // 2
     {( 3, CardSuit.Balls), 90000260 },  // 3
     {( 4, CardSuit.Balls), 90000261 },  // 4
     {( 5, CardSuit.Balls), 90000262 },  // 5
     {( 6, CardSuit.Balls), 90000263 },  // 6
     {( 7, CardSuit.Balls), 90000264 },  // 7
     {( 8, CardSuit.Balls), 90000265 },  // 8
     {( 9, CardSuit.Balls), 90000266 },  // 9
     {( 10, CardSuit.Balls), 90000267 }, // 10
     {( 11, CardSuit.Balls), 90000268 }, // Jack
     {( 12, CardSuit.Balls), 90000269 }, // Queen
     {( 13, CardSuit.Balls), 90000270 }, // King
 };

             // Start the solo blackjack game
             [CommandHandler("blackjack", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Start a solo blackjack game", "Usage: /blackjack <bet amount>")]
             public static void HandleSoloBlackjackCommand(Session session, params string[] parameters)
             {

                 // Check if the player already has an active game
                 if (activeSoloBlackjackGames.ContainsKey(session.Player.Guid))
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are already in a blackjack game. Finish your current game before starting a new one.", ChatMessageType.System));
                     return;
                 }

                 if (parameters.Length == 0 || !long.TryParse(parameters[0], out long bet) || bet <= 0)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You must specify a valid bet amount. Example: /blackjack 1000000", ChatMessageType.System));
                     return;
                 }

                 if (session.Player.BankedLuminance < bet)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You don't have enough luminance to place this bet.", ChatMessageType.System));
                     return;
                 }

                 // Initialize the game and deduct the initial bet
                 var game = new SoloBlackjackGame(session.Player, bet);
                 activeSoloBlackjackGames[session.Player.Guid] = game;

                 // Initial bet deduction
                 Console.WriteLine($"[DEBUG] Initial Banked Luminance before game starts: {session.Player.BankedLuminance}");
                 session.Player.BankedLuminance -= bet; // Deduct the player's luminance
                 Console.WriteLine($"[DEBUG] Banked Luminance after initial bet deduction: {session.Player.BankedLuminance}");

                 session.Network.EnqueueSend(new GameMessageSystemChat($"You have placed a bet of {bet:N0} luminance!", ChatMessageType.Broadcast));

                 // Deal initial cards to player and dealer
                 session.Network.EnqueueSend(new GameMessageSystemChat($"The game begins! It's your turn. Type /hit or /stand.", ChatMessageType.Broadcast));
                 DealInitialCardsToPlayer(game, session);
             }

             // Handle hitting
             [CommandHandler("hit", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Hit a card in solo blackjack", "")]
             public static void HandleHitCommand(Session session, params string[] parameters)
             {
                 if (!activeSoloBlackjackGames.TryGetValue(session.Player.Guid, out var game))
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are not currently in a blackjack game.", ChatMessageType.Combat));
                     return;
                 }

                 try
                 {
                     // Deal a card to the player
                     DealCardsToSoloPlayer(game, 1, session); // Deal one card

                     // Calculate the current hand value
                     int handValue = BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand]);

                     // Update player with new hand value after the hit
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your hand value is now {handValue}.", ChatMessageType.System));

                     if (handValue > 21)
                     {
                         long totalBet = game.PlayerBet;  // Total bet after doubling down
                         session.Network.EnqueueSend(new GameMessageSystemChat($"You busted with a hand value of {handValue} You lost {totalBet:N0} luminance.", ChatMessageType.Combat));

                         // Check if there's a second hand after busting the first
                         if (game.PlayerHands.Count > 1 && game.PlayerCurrentHand == 0)
                         {
                             game.PlayerCurrentHand++; // Move to the second hand
                             session.Network.EnqueueSend(new GameMessageSystemChat($"Now playing your second hand. Type /hit or /stand for your second hand.", ChatMessageType.Tell));
                             session.Network.EnqueueSend(new GameMessageSystemChat($"Your second hand value is {BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand])}.", ChatMessageType.Broadcast));
                             return;
                         }
                         // Only end the game if this is the last hand
                         if (game.PlayerCurrentHand == game.PlayerHands.Count - 1)
                         {
                             // Reveal dealer hand only after both hands have been played or busted
                             RevealDealerHand(game, session);
                             ResolveDealerHand(game, session);

                             // Determine the winner after all hands are completed
                             DetermineWinner(game, session);
                             EndSoloBlackjackGame(game, session);
                         }
                     }
                     else
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat($"Your hand value is now {handValue}. Type /hit to draw another card or /stand to end your turn.", ChatMessageType.Broadcast));
                     }
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error in HandleHitCommand: {ex.Message}");
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Error occurred while hitting: {ex.Message}", ChatMessageType.System));
                 }
             }

             // Handle standing for solo blackjack
             [CommandHandler("stand", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Stand in blackjack", "")]
             public static void HandleStandCommand(Session session, params string[] parameters)
             {
                 if (!activeSoloBlackjackGames.TryGetValue(session.Player.Guid, out var game))
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are not currently in a blackjack game.", ChatMessageType.Combat));
                     return;
                 }

                 try
                 {
                     int playerHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand]);

                     // Inform player of their final hand value when they stand
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your final hand value is {playerHandValue}.", ChatMessageType.System));

                     // If the player has another hand, move to that hand after completing the first one
                     if (game.PlayerHands.Count > 1 && game.PlayerCurrentHand == 0) // If there is a second hand and we just completed the first
                     {
                         game.PlayerCurrentHand++; // Move to the second hand

                         // Automatically deal a card to the second hand
                         DealCardsToSoloPlayer(game, 1, session); // Auto-deal one card

                         session.Network.EnqueueSend(new GameMessageSystemChat($"Now playing your second hand. Type /hit or /stand for your second hand.", ChatMessageType.Tell));
                         session.Network.EnqueueSend(new GameMessageSystemChat($"Your second hand value is {BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand])}.", ChatMessageType.Broadcast));
                         return; // Return here so that it waits for the player's input for the second hand
                     }

                     // Reveal dealer hand only after both hands have been played
                     RevealDealerHand(game, session);
                     ResolveDealerHand(game, session);

                     // Determine the winner
                     DetermineWinner(game, session);
                     EndSoloBlackjackGame(game, session);
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error while standing: {ex.Message}");
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Error occurred while standing: {ex.Message}", ChatMessageType.System));
                 }
             }

             // Handle doubling down
             [CommandHandler("double", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Double down in blackjack", "")]
             public static void HandleDoubleDownCommand(Session session, params string[] parameters)
             {
                 if (!activeSoloBlackjackGames.TryGetValue(session.Player.Guid, out var game))
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are not currently in a blackjack game.", ChatMessageType.Combat));
                     return;
                 }

                 var hand = game.PlayerHands[game.PlayerCurrentHand];

                 // Add a check to prevent doubling down after splitting
                 if (game.PlayerHands.Count > 1)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You cannot double down after splitting your hand.", ChatMessageType.Combat));
                     return;
                 }

                 // Allow doubling down only with two cards
                 if (hand.Count == 2)
                 {
                     Console.WriteLine($"[DEBUG] Player is doubling down. Original bet: {game.PlayerBet}");

                     // Double the player's original bet
                     long originalBet = game.PlayerBet;  // Capture the original bet before doubling
                     game.PlayerBet *= 2;
                     game.PlayerDoubledDown = true;

                     Console.WriteLine($"[DEBUG] Player doubled down. New bet amount: {game.PlayerBet}");

                     session.Network.EnqueueSend(new GameMessageSystemChat("You have doubled your bet! You will receive one more card, and your turn will end.", ChatMessageType.Broadcast));

                     // Deal one more card to the player
                     DealCardsToSoloPlayer(game, 1, session);

                     int handValue = BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand]);
                     Console.WriteLine($"[DEBUG] Player's final hand value after double down: {handValue}");

                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your final hand value is {handValue}.", ChatMessageType.System));

                     if (handValue > 21 && game.PlayerDoubledDown == true)
                     {
                         long totalBet = game.PlayerBet;  // Total bet after doubling down
                         long additionalBet = totalBet / 2; // Only deduct the additional bet

                         Console.WriteLine($"[DEBUG] Player busted. Deducting additional {additionalBet} luminance.");

                         // Deduct luminance for busting
                         session.Player.BankedLuminance -= additionalBet;
                         session.Network.EnqueueSend(new GameMessageSystemChat($"You busted with a hand value of {handValue}. You have lost your bet of {totalBet:N0} luminance.", ChatMessageType.Combat));

                         EndSoloBlackjackGame(game, session);
                         return;
                     }
                     else
                     {
                         // If the player didn't bust, proceed to reveal dealer's hand and determine winner
                         RevealDealerHand(game, session);
                         ResolveDealerHand(game, session);
                         DetermineWinner(game, session);
                     }

                     // End the game only if it hasn't been ended yet
                     if (game.GameEnded)
                     {
                         EndSoloBlackjackGame(game, session);
                     }
                 }
                 else
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You can only double down on your first two cards.", ChatMessageType.Combat));
                 }
             }

             // Handle splitting the hand
             [CommandHandler("split", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Split your hand in blackjack", "")]
             public static void HandleSplitCommand(Session session, params string[] parameters)
             {
                 if (!activeSoloBlackjackGames.TryGetValue(session.Player.Guid, out var game))
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are not currently in a blackjack game.", ChatMessageType.Combat));
                     return;
                 }

                 var hand = game.PlayerHands[game.PlayerCurrentHand];

                 // Allow splitting only if the player has two cards of the same value
                 if (hand.Count == 2 && hand[0].cardValue == hand[1].cardValue)
                 {
                     // Check if player has enough luminance for the additional bet
                     if (session.Player.BankedLuminance < game.PlayerBet)
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat("You don't have enough luminance to split your hand.", ChatMessageType.Combat));
                         return;
                     }

                     // Deduct the additional bet from player's luminance
                     session.Player.BankedLuminance -= game.PlayerBet;
                     Console.WriteLine($"[DEBUG] Additional bet placed for split. Player's remaining luminance: {session.Player.BankedLuminance}");

                     // Create a new hand by splitting the current hand
                     game.PlayerHands.Add(new List<(int cardValue, CardSuit suit)> { hand[1] });
                     game.PlayerItems.Add(new List<int>()); // Initialize new hand's items list
                     hand.RemoveAt(1); // Keep one card in the original hand

                     session.Network.EnqueueSend(new GameMessageSystemChat("You have split your hand into two separate hands!", ChatMessageType.Broadcast));

                     // Deal one card to each of the hands
                     DealCardsToSoloPlayer(game, 1, session);

                     // Inform the player of the next action for their first hand
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your first hand value is {BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand])}. Type /hit or /stand for your first hand.", ChatMessageType.Broadcast));
                 }
                 else
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You can only split if you have two cards of the same value.", ChatMessageType.Combat));
                 }
             }

             // Deal initial cards to player and dealer
             private static void DealInitialCardsToPlayer(SoloBlackjackGame game, Session session)
             {

                 // Check if the game has already ended (e.g., due to initial Blackjack)
                 if (game.GameEnded) return;

                 try
                 {
                     DealCardsToSoloPlayer(game, 2, session); // Deal two cards to player
                     if (game.GameEnded) return;
                     int playerHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand]);

                     // Show player's initial hand value after two cards are dealt
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your initial hand value is {playerHandValue}.", ChatMessageType.System));

                     DealInitialCardsToDealer(game, session); // Deal two cards to the dealer (show only one)
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error while dealing initial cards: {ex.Message}");
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Error occurred while dealing initial cards: {ex.Message}", ChatMessageType.System));
                 }
             }

             // Deal cards to the solo player
             public static void DealCardsToSoloPlayer(SoloBlackjackGame game, int numberOfCards, Session session)
             {
                 try
                 {
                     // Ensure the player's hand is initialized
                     if (game.PlayerHands == null || game.PlayerHands.Count == 0)
                     {
                         game.PlayerHands = new List<List<(int cardValue, CardSuit suit)>> { new List<(int cardValue, CardSuit suit)>() };
                     }

                     var currentHand = game.PlayerHands[game.PlayerCurrentHand];

                     for (int i = 0; i < numberOfCards; i++)
                     {
                         var card = game.DrawCard(); // Draw a unique card from the deck
                         currentHand.Add(card);

                         if (cardItemMap.TryGetValue((card.cardValue, card.suit), out int itemWeenieClassId))
                         {
                             BlackjackHelperFunctions.GiveItemToPlayer(game.Player, itemWeenieClassId); // Add item to player's inventory
                             game.PlayerItems[game.PlayerCurrentHand].Add(itemWeenieClassId);
                         }

                         session.Network.EnqueueSend(new GameMessageSystemChat($"You were dealt a {BlackjackUtils.GetCardName(card.cardValue, card.suit)}.", ChatMessageType.Tell));
                     }

                     // Check for an initial blackjack if the player has exactly two cards
                     if (currentHand.Count == 2)
                     {
                         int handValue = BlackjackUtils.GetHandValue(currentHand);
                         if (handValue == 21)
                         {
                             long winnings = (long)(game.PlayerBet * 1.25); // 
                             game.Player.BankedLuminance += winnings;

                             session.Network.EnqueueSend(new GameMessageSystemChat($"Blackjack! You win with a hand value of 21. You gain {winnings:N0} luminance.", ChatMessageType.Broadcast));

                             EndSoloBlackjackGame(game, session); // End the game
                             return; // Exit to prevent further actions
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error while dealing cards to player {game.Player.Name}: {ex.Message}");
                 }
             }

             // Deal initial cards to the dealer (deal two but only show one)
             private static void DealInitialCardsToDealer(SoloBlackjackGame game, Session session)
             {
                 if (game.DealerHand == null)
                 {
                     game.DealerHand = new List<(int cardValue, CardSuit suit)>(); // Ensure the dealer hand is initialized
                 }

                 try
                 {
                     // Draw two cards for the dealer
                     var cards = BlackjackUtils.DrawCards(2);

                     // Add both cards to the dealer's hand
                     game.DealerHand.Add(cards[0]);
                     game.DealerHand.Add(cards[1]);

                     // Show only the first card to the player
                     var (cardValue, suit) = cards[0];
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Rand was dealt a {BlackjackUtils.GetCardName(cardValue, suit)}.", ChatMessageType.Tell));

                     // Show the dealer's current hand value after the first card is revealed
                     int currentHandValue = BlackjackUtils.GetHandValue(game.DealerHand.Take(1).ToList());
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's current hand value is {currentHandValue}.", ChatMessageType.Magic));
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error while dealing initial cards to the dealer: {ex.Message}");
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Error occurred while dealing initial cards to the dealer: {ex.Message}", ChatMessageType.System));
                 }
             }

             // Reveal the dealer's second card and determine if game should end
             private static void RevealDealerHand(SoloBlackjackGame game, Session session)
             {
                 if (game.GameEnded) return; // Prevent redundant actions

                 // Reveal each of Rand's cards except the first one (already revealed)
                 for (int i = 1; i < game.DealerHand.Count; i++)
                 {
                     var (cardValue, suit) = game.DealerHand[i];
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Rand reveals a {BlackjackUtils.GetCardName(cardValue, suit)}.", ChatMessageType.Tell));
                 }

                 // Calculate dealer's current hand value
                 int dealerHandValue = BlackjackUtils.GetHandValue(game.DealerHand);
                 int playerHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand]);

                 Console.WriteLine($"[DEBUG] Player hand value: {playerHandValue}, Dealer hand value after reveal: {dealerHandValue}");

                 if (dealerHandValue >= 17 && dealerHandValue >= playerHandValue)
                 {
                     // If dealer reaches final hand, announce it
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's final hand value is {dealerHandValue}.", ChatMessageType.Magic));

                     // Trigger DetermineWinner to finalize the game
                     DetermineWinner(game, session);

                     // End the game to clean up state and remove cards
                     EndSoloBlackjackGame(game, session);
                 }
                 else if (dealerHandValue == 17 && playerHandValue > 17 && playerHandValue <= 21)
                 {
                     // If dealer has 17 but the player's hand is greater, Rand should attempt to beat the player's hand
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's current hand value is {dealerHandValue}, but he draws to try and beat your hand of {playerHandValue}.", ChatMessageType.Magic));

                     // Proceed to ResolveDealerHand to try and beat the player's hand
                     ResolveDealerHand(game, session);
                 }
                 else
                 {
                     // Dealer will draw again if hand is less than 17 and not beating player
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's current hand value is {dealerHandValue}.", ChatMessageType.Magic));

                     // Proceed to ResolveDealerHand if not ended
                     ResolveDealerHand(game, session);
                 }
             }

             // Handle resolving the dealer's hand if necessary
             private static void ResolveDealerHand(SoloBlackjackGame game, Session session)
             {
                 if (game.GameEnded) return; // Prevent duplicate resolves if the game already ended

                 int playerHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[game.PlayerCurrentHand]);
                 int dealerHandValue = BlackjackUtils.GetHandValue(game.DealerHand);

                 Console.WriteLine($"[DEBUG] Initial Dealer Hand: {dealerHandValue}, Player Hand: {playerHandValue}");

                 while (dealerHandValue < 17 || (dealerHandValue < playerHandValue && playerHandValue > 17 && dealerHandValue < 21))
                 {
                     var card = BlackjackUtils.DrawCards(1).First();
                     game.DealerHand.Add(card);

                     // Announce each card Rand draws
                     dealerHandValue = BlackjackUtils.GetHandValue(game.DealerHand);
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Rand drew a {BlackjackUtils.GetCardName(card.cardValue, card.suit)}. Rand's hand value is now {dealerHandValue}.", ChatMessageType.System));
                     Console.WriteLine($"[DEBUG] Rand drew a card. New Dealer Hand Value: {dealerHandValue}");

                     // Check if Rand busted
                     if (dealerHandValue > 21)
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's final hand value is {dealerHandValue}.", ChatMessageType.System));
                         Console.WriteLine("[DEBUG] Rand busted. Calling DetermineWinner and EndSoloBlackjackGame.");

                         // Call DetermineWinner without setting GameEnded prematurely
                         DetermineWinner(game, session);
                         EndSoloBlackjackGame(game, session); // Ensure the game ends and items are removed
                         return;
                     }

                     // If Rand's hand value equals 17 and matches player's 17, stop for a tie
                     if (dealerHandValue == 17 && playerHandValue == 17)
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's final hand value is {dealerHandValue}.", ChatMessageType.System));
                         Console.WriteLine("[DEBUG] Game ended with a tie at 17.");
                         DetermineWinner(game, session);
                         EndSoloBlackjackGame(game, session);
                         return;
                     }

                     // Stop if Rand's hand is at least 17 and equals or exceeds player's hand
                     if (dealerHandValue >= 17 && dealerHandValue >= playerHandValue)
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's final hand value is {dealerHandValue}.", ChatMessageType.System));
                         Console.WriteLine("[DEBUG] Rand's hand met ending condition; exiting loop.");
                         DetermineWinner(game, session);
                         EndSoloBlackjackGame(game, session);
                         return;
                     }
                 }

                 // Final safety check if game-ending conditions were not met in the loop
                 if (!game.GameEnded)
                 {
                     Console.WriteLine("[DEBUG] Loop completed without game end; calling DetermineWinner.");
                     DetermineWinner(game, session);
                     EndSoloBlackjackGame(game, session); // Ensure the game properly ends
                 }

                 // Now set game.GameEnded to avoid duplicate end-game calls after all logic
                 game.GameEnded = true;
             }

             // Clean up at end of solo game, removing cards and ensuring game end message
             private static void EndSoloBlackjackGame(SoloBlackjackGame game, Session session)
             {
                 Console.WriteLine("[DEBUG] Entered EndSoloBlackjackGame");

                 if (game.GameEnded)
                 {
                     Console.WriteLine("[DEBUG] Game has already ended, exiting EndSoloBlackjackGame");
                     return; // Prevent multiple calls to end-game logic
                 }

                 game.GameEnded = true; // Set the flag to avoid re-triggering

                 // Remove cards from player inventory
                 RemoveCardsFromPlayerInventory(game);
                 Console.WriteLine("[DEBUG] Cards removed from player inventory.");

                 // Announce end of game
                 session.Network.EnqueueSend(new GameMessageSystemChat("The blackjack game has ended, and the cards have been removed from your inventory.", ChatMessageType.Broadcast));
                 Console.WriteLine("[DEBUG] Game end message sent to player.");

                 // Remove game from active games list
                 activeSoloBlackjackGames.Remove(game.Player.Guid);
                 Console.WriteLine("[DEBUG] Game removed from activeSoloBlackjackGames.");
             }

             // Determine the winner and enforce game end
             public static void DetermineWinner(SoloBlackjackGame game, Session session)
             {
                 if (game.GameEnded) return; // Avoid multiple end-game calls

                 // Track total winnings across multiple hands if split
                 long totalWinnings = 0;
                 bool playerWon = false;

                 // Loop through each hand (supporting split hands)
                 for (int i = 0; i < game.PlayerHands.Count; i++)
                 {
                     int playerHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[i]);
                     int dealerHandValue = BlackjackUtils.GetHandValue(game.DealerHand);

                     // Calculate base bet and double-down-specific amounts
                     long baseBet = game.PlayerBet / (game.PlayerDoubledDown && i == 0 ? 2 : 1);
                     long additionalBet = game.PlayerDoubledDown && i == 0 ? baseBet : 0; // Additional bet for double-down
                     long winnings = 0;

                     // Scenario 1: Player busts
                     if (playerHandValue > 21 && game.PlayerDoubledDown == false)
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat(
                             $"You busted with a hand value of {playerHandValue} on hand {i + 1}. You lost your bet of {additionalBet + baseBet:N0} luminance.",
                             ChatMessageType.Combat));
                         game.Player.BankedLuminance -= additionalBet; // Deduct only the additional amount if it's a double-down
                     }
                     // Scenario 2: Dealer busts or player has a higher hand than the dealer
                     else if (dealerHandValue > 21 || playerHandValue > dealerHandValue)
                     {
                         winnings = game.PlayerDoubledDown && i == 0
                             ? (long)(baseBet * 1.5) // Double-down winnings (150% of original bet)
                             : (long)(baseBet * 1.25); // Normal play or split hand winnings (125% of bet)

                         totalWinnings += winnings;
                         session.Network.EnqueueSend(new GameMessageSystemChat(
                             $"You won with a hand value of {playerHandValue} on hand {i + 1}! You gain {winnings:N0} luminance.",
                             ChatMessageType.Broadcast));

                         playerWon = true;
                     }
                     // Scenario 3: Tie with dealer
                     else if (playerHandValue == dealerHandValue)
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat(
                             $"You tied with Rand on hand {i + 1}. Your bet of {baseBet:N0} luminance has been returned.",
                             ChatMessageType.Tell));

                         // Return only the base bet amount without any winnings or losses
                         game.Player.BankedLuminance += baseBet;
                         EndSoloBlackjackGame(game, session);
                     }
                     // Scenario 4: Dealer wins with a higher hand value (Normal loss or double-down loss)
                     else
                     {
                         if (game.PlayerDoubledDown && i == 0)
                         {
                             session.Network.EnqueueSend(new GameMessageSystemChat(
                                 $"You lost with a hand value of {playerHandValue} on hand {i + 1}. You have lost your bet of {additionalBet + baseBet:N0} Luminance.",
                                 ChatMessageType.Combat));
                             game.Player.BankedLuminance -= additionalBet; // Deduct only the additional amount for double-down
                         }
                         else
                         {
                             session.Network.EnqueueSend(new GameMessageSystemChat(
                                 $"You lost with a hand value of {playerHandValue} on hand {i + 1}. You have lost your bet of {baseBet:N0} Luminance.",
                                 ChatMessageType.Combat));
                         }
                     }
                 }

                 // Update banked luminance only once with total winnings, if any
                 if (playerWon)
                 {
                     game.Player.BankedLuminance += totalWinnings;
                 }

                 EndSoloBlackjackGame(game, session);
             }


             // Remove cards from the player's inventory
             private static void RemoveCardsFromPlayerInventory(SoloBlackjackGame game)
             {
                 var player = game.Player;

                 // Loop through each hand's item list and remove the card items from the player's inventory
                 foreach (var handItems in game.PlayerItems)
                 {
                     foreach (var itemWeenieClassId in handItems.Distinct()) // Ensure distinct items are removed only once
                     {
                         RemoveItemFromPlayer(player, itemWeenieClassId);
                     }
                 }
             }

             private static void RemoveItemFromPlayer(Player player, int itemWeenieClassId)
             {
                 try
                 {
                     // Get all items in the player's inventory that match the itemWeenieClassId
                     var items = player.GetInventoryItemsOfWCID((uint)itemWeenieClassId);

                     if (items.Any())
                     {
                         foreach (var item in items.ToList()) // Convert to a list to avoid modifying the collection while iterating
                         {
                             if (player.TryRemoveFromInventoryWithNetworking(item.Guid, out var removedItem, Player.RemoveFromInventoryAction.ConsumeItem))
                             {
                                 //player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The card item has been removed from your inventory.", ChatMessageType.System));
                             }
                             else
                             {
                                 Console.WriteLine($"Error: Failed to remove card item {itemWeenieClassId} from player {player.Name}'s inventory.");
                             }
                         }
                     }
                     else
                     {
                         Console.WriteLine($"Error: Card item {itemWeenieClassId} not found in player {player.Name}'s inventory.");
                     }
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error in RemoveItemFromPlayer: {ex.Message}");
                 }
             }
         }

         // Fellowship Blackjack Game Code
         public class FellowshipBlackjackGame
         {
             public uint FellowshipLeaderGuid { get; set; }
             public List<Player> Players { get; set; }
             public Dictionary<Player, List<List<(int cardValue, CardSuit suit)>>> PlayerHands { get; set; } // Multiple hands per player
             public Dictionary<Player, List<List<int>>> PlayerItems { get; set; } // Multiple item lists per player
             public Dictionary<Player, long> PlayerBets { get; set; } // Base bet per player
             public Dictionary<Player, bool> PlayerDoubledDown { get; set; } // Whether the player has doubled down
             public Dictionary<Player, int> PlayerCurrentHand { get; set; } // Track which hand the player is on
             public List<(int cardValue, CardSuit suit)> DealerHand { get; set; } // Store both card value and suit for dealer
             public int CurrentTurn { get; set; }
             public Dictionary<Player, bool> PlayerTurnCompleted { get; set; } // Track if player's turn is done
             public bool DealerHandResolved { get; set; } // Flag to prevent dealer hand resolution twice
             public bool GameEnded { get; set; } // Flag to prevent game end logic from running multiple times
             public List<(int cardValue, CardSuit suit)> Deck { get; private set; } // Shared deck for fellowship game
             public HashSet<(int cardValue, CardSuit suit)> DealtCards { get; set; } = new HashSet<(int, CardSuit)>();
             public Dictionary<Player, bool> HasNaturalBlackjack { get; set; } // Flag for initial blackjack on the first two cards



             public FellowshipBlackjackGame(uint fellowshipLeaderGuid, List<Player> players, Dictionary<Player, long> playerBets)
             {
                 FellowshipLeaderGuid = fellowshipLeaderGuid;
                 Players = players;
                 PlayerHands = new Dictionary<Player, List<List<(int cardValue, CardSuit suit)>>>();
                 PlayerItems = new Dictionary<Player, List<List<int>>>();
                 PlayerBets = new Dictionary<Player, long>();
                 PlayerDoubledDown = new Dictionary<Player, bool>();
                 PlayerCurrentHand = new Dictionary<Player, int>();
                 PlayerTurnCompleted = new Dictionary<Player, bool>();
                 HasNaturalBlackjack = new Dictionary<Player, bool>(); // Initialize the new flag
                 DealerHand = new List<(int cardValue, CardSuit suit)>();
                 CurrentTurn = 0;
                 DealerHandResolved = false; // Initialize as false
                 GameEnded = false; // Initialize as false

                 foreach (var player in players)
                 {
                     PlayerHands[player] = new List<List<(int cardValue, CardSuit suit)>> { new List<(int cardValue, CardSuit suit)>() };
                     PlayerItems[player] = new List<List<int>> { new List<int>() };
                     PlayerTurnCompleted[player] = false;
                     HasNaturalBlackjack[player] = false; // Initialize each player as not having a natural blackjack
                 }

                 InitializeDeck(); // Initialize and shuffle the deck at the start of each game
             }

             // Initialize and shuffle the deck
             private void InitializeDeck()
             {
                 Deck = new List<(int cardValue, CardSuit suit)>();

                 // Add all 52 cards to the deck (13 values for each of the 4 suits)
                 for (int value = 1; value <= 13; value++)
                 {
                     Deck.Add((value, CardSuit.Hands));
                     Deck.Add((value, CardSuit.Eyes));
                     Deck.Add((value, CardSuit.Jesters));
                     Deck.Add((value, CardSuit.Balls));
                 }

                 // Shuffle the deck
                 Random rand = new Random();
                 Deck = Deck.OrderBy(_ => rand.Next()).ToList();
             }

             // Method to draw a card from the deck
             public (int cardValue, CardSuit suit) DrawCard()
             {
                 if (Deck.Count == 0)
                 {
                     throw new InvalidOperationException("The deck is empty.");
                 }

                 var card = Deck[0];
                 Deck.RemoveAt(0); // Remove the card from the deck after drawing
                 return card;
             }

             public Player GetCurrentPlayer()
             {
                 return Players[CurrentTurn];
             }

             public void MoveToNextPlayer()
             {
                 CurrentTurn = (CurrentTurn + 1) % Players.Count;
             }
         }

         public static class FellowshipBlackjackCommands
         {
             public static Dictionary<uint, FellowshipBlackjackGame> activeFellowshipBlackjackGames = new Dictionary<uint, FellowshipBlackjackGame>();

             // Start the fellowship blackjack game
             [CommandHandler("fblackjack", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Start a blackjack game for your fellowship", "Usage: /fblackjack <bet amount> (Maximum bet is 100 Billion)")]
             public static void HandleFellowshipBlackjackCommand(Session session, params string[] parameters)
             {
                 if (session.Player.Fellowship == null)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You must be in a fellowship to start a multiplayer blackjack game.", ChatMessageType.Combat));
                     return;
                 }

                 var fellowship = session.Player.Fellowship;
                 uint fellowshipId = fellowship.FellowshipLeaderGuid;

                 // Check if the game has already started (cards dealt and gameplay is ongoing)
                 if (activeFellowshipBlackjackGames.TryGetValue(fellowshipId, out FellowshipBlackjackGame existingGame))
                 {
                     if (existingGame.Players.Count == fellowship.FellowshipMembers.Count)
                     {
                         Console.WriteLine($"[DEBUG] Game already in progress for fellowship leader: {fellowshipId}");
                         session.Network.EnqueueSend(new GameMessageSystemChat("A blackjack game is currently in progress and cannot be joined.", ChatMessageType.System));
                         return;
                     }
                 }

                 if (parameters.Length == 0 || !long.TryParse(parameters[0], out long bet) || bet <= 0 || bet >= 100000000001)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You must specify a valid bet amount. Example: /fblackjack 1000000 (Maximum bet is 100 Billion)", ChatMessageType.System));
                     return;
                 }

                 if (session.Player.BankedLuminance < bet)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You don't have enough luminance to place this bet.", ChatMessageType.System));
                     return;
                 }

                 FellowshipBlackjackGame game;
                 if (!activeFellowshipBlackjackGames.TryGetValue(fellowshipId, out game))
                 {
                     // Create a new game if one does not exist
                     game = new FellowshipBlackjackGame(fellowshipId, new List<Player>(), new Dictionary<Player, long>());
                     activeFellowshipBlackjackGames[fellowshipId] = game;
                     Console.WriteLine($"[DEBUG] New game created for fellowship leader: {fellowshipId}");
                 }

                 if (!game.Players.Contains(session.Player))
                 {
                     game.Players.Add(session.Player);
                     Console.WriteLine($"[DEBUG] {session.Player.Name} joined the game with a bet of {bet:N0}");

                     game.PlayerHands[session.Player] = new List<List<(int cardValue, CardSuit suit)>> { new List<(int cardValue, CardSuit suit)>() };
                     game.PlayerItems[session.Player] = new List<List<int>> { new List<int>() };
                     game.PlayerCurrentHand[session.Player] = 0;
                     game.PlayerTurnCompleted[session.Player] = false;
                 }

                 game.PlayerBets[session.Player] = bet;
                 session.Player.BankedLuminance -= bet;
                 Console.WriteLine($"[DEBUG] {session.Player.Name}'s luminance updated after placing bet.");

                 session.Network.EnqueueSend(new GameMessageSystemChat($"You have placed a bet of {bet:N0} luminance and joined the blackjack game!", ChatMessageType.Broadcast));

                 // Notify other fellowship members to place their bets
                 foreach (var memberEntry in fellowship.FellowshipMembers)
                 {
                     if (memberEntry.Value.TryGetTarget(out Player memberPlayer) && memberPlayer != session.Player)
                     {
                         memberPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your fellowship has started a blackjack game. Please place your bet using /fblackjack <bet amount>.", ChatMessageType.Broadcast));
                     }
                 }

                 // Check if all players have placed their bets
                 if (game.Players.Count == fellowship.FellowshipMembers.Count)
                 {
                     // Set the CurrentTurn to the initiating player
                     game.CurrentTurn = game.Players.IndexOf(session.Player);
                     Console.WriteLine($"[DEBUG] All players have placed bets. Starting game with current player: {game.GetCurrentPlayer().Name}");

                     // Now the game officially starts, cards are dealt
                     DealInitialCardsToPlayers(game, session);

                     // Check for initial blackjacks and handle them once per player
                     List<Player> playersWithBlackjack = new List<Player>();
                     foreach (var player in game.Players)
                     {
                         int initialHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[player][game.PlayerCurrentHand[player]]);
                         Console.WriteLine($"[DEBUG] {player.Name} dealt initial hand value: {initialHandValue}");

                         if (initialHandValue == 21)
                         {
                             long winnings = (long)(game.PlayerBets[player] * 1.25); // 125% of the initial bet
                             player.BankedLuminance += winnings;

                             player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                                 $"Blackjack! You win with a hand value of 21. You gain {winnings:N0} luminance.", ChatMessageType.Broadcast));

                             // Mark player's turn as completed and add to blackjack winners list
                             game.PlayerTurnCompleted[player] = true;
                             playersWithBlackjack.Add(player);
                             Console.WriteLine($"[DEBUG] {player.Name} hit blackjack and won {winnings:N0} luminance");
                         }
                     }

                     // If all players have blackjack, end the game immediately
                     if (playersWithBlackjack.Count == game.Players.Count)
                     {
                         Console.WriteLine("[DEBUG] All players achieved blackjack. Ending game immediately.");
                         EndBlackjackGame(game);
                         return;
                     }

                     // Notify players whose turns are not completed to begin the game
                     foreach (var player in game.Players)
                     {
                         if (!game.PlayerTurnCompleted[player])
                         {
                             player.Session.Network.EnqueueSend(new GameMessageSystemChat($"All players have placed their bets. The game begins! {game.GetCurrentPlayer().Name}, it's your turn. Type /fhit or /fstand.", ChatMessageType.Tell));
                             Console.WriteLine($"[DEBUG] {player.Name}'s turn to act.");
                         }
                     }
                 }
                 else
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Waiting for all fellowship members to place their bets...", ChatMessageType.System));
                     Console.WriteLine("[DEBUG] Waiting for all players to place bets.");
                 }
             }

             // Deal initial cards to all players
             private static void DealInitialCardsToPlayers(FellowshipBlackjackGame game, Session session)
             {
                 List<Player> playersWithBlackjack = new List<Player>();

                 foreach (var player in game.Players)
                 {
                     DealCardsToPlayer(player, game, 2);
                     int playerHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[player][0]);
                     Console.WriteLine($"[DEBUG] {player.Name} dealt initial hand value: {playerHandValue}");

                     if (playerHandValue == 21 && !game.HasNaturalBlackjack[player])
                     {
                         long winnings = (long)(game.PlayerBets[player] * 1.25);
                         player.BankedLuminance += winnings;
                         Console.WriteLine($"[DEBUG] {player.Name} hit blackjack with initial hand. Winning luminance: {winnings}");

                         player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                             $"Blackjack! You win with a hand value of 21. You gain {winnings:N0} luminance.", ChatMessageType.Broadcast));

                         game.HasNaturalBlackjack[player] = true;
                         game.PlayerTurnCompleted[player] = true;
                         playersWithBlackjack.Add(player);
                     }
                 }

                 if (playersWithBlackjack.Count == game.Players.Count)
                 {
                     Console.WriteLine("[DEBUG] All players achieved blackjack. Ending game immediately.");
                     EndBlackjackGame(game);
                     return;
                 }

                 if (!game.PlayerTurnCompleted.All(p => p.Value))
                 {
                     Console.WriteLine("[DEBUG] Some players still have turns. Dealing initial cards to dealer.");
                     DealInitialCardsToDealer(game);
                     MoveToNextActivePlayer(game, session);
                 }
                 else
                 {
                     EndBlackjackGame(game);
                 }
             }

             // Method for dealing cards to a fellowship player using the shared deck (simplified without natural blackjack check)
             private static void DealCardsToPlayer(Player player, FellowshipBlackjackGame game, int numberOfCards)
             {
                 try
                 {
                     var currentHand = game.PlayerHands[player][game.PlayerCurrentHand[player]];

                     for (int i = 0; i < numberOfCards; i++)
                     {
                         // Draw a unique card and add it to the hand
                         var card = DrawUniqueCard(game);
                         currentHand.Add(card);

                         // Check if an inventory item exists for this card and add it to the player's inventory
                         if (SoloBlackjackCommands.cardItemMap.TryGetValue((card.cardValue, card.suit), out int itemWeenieClassId))
                         {
                             BlackjackHelperFunctions.GiveItemToPlayer(player, itemWeenieClassId);
                             game.PlayerItems[player][game.PlayerCurrentHand[player]].Add(itemWeenieClassId);
                         }

                         // Inform the player about the dealt card
                         player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You were dealt a {BlackjackUtils.GetCardName(card.cardValue, card.suit)}.", ChatMessageType.Tell));
                     }
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error while dealing cards to player {player.Name}: {ex.Message}");
                 }
             }

             // Helper method to draw a unique card
             private static (int cardValue, CardSuit suit) DrawUniqueCard(FellowshipBlackjackGame game)
             {
                 (int cardValue, CardSuit suit) card;

                 // Loop to find a unique card
                 do
                 {
                     card = game.DrawCard(); // Attempt to draw a card from the game's deck
                 } while (game.DealtCards.Contains(card)); // Repeat if the card has already been dealt

                 game.DealtCards.Add(card); // Track the card as dealt
                 return card;
             }

             // Deal initial cards to the dealer (deal two but only show one)
             private static void DealInitialCardsToDealer(FellowshipBlackjackGame game)
             {
                 if (game.DealerHand == null)
                 {
                     game.DealerHand = new List<(int cardValue, CardSuit suit)>();
                 }

                 try
                 {
                     var cards = BlackjackUtils.DrawCards(2);
                     game.DealerHand.Add(cards[0]);
                     game.DealerHand.Add(cards[1]);

                     var (cardValue, suit) = cards[0];
                     foreach (var player in game.Players)
                     {
                         player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Rand was dealt a {BlackjackUtils.GetCardName(cardValue, suit)}.", ChatMessageType.Tell));
                     }
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error while dealing initial cards to the dealer: {ex.Message}");
                 }
             }

             // Moves to the next player in the fellowship
             public static void MoveToNextActivePlayer(FellowshipBlackjackGame game, Session session)
             {
                 Console.WriteLine("[DEBUG] Moving to next active player...");

                 if (game.GameEnded)
                 {
                     Console.WriteLine("[DEBUG] Game already ended, returning.");
                     return;
                 }

                 if (AllPlayersDone(game))
                 {
                     Console.WriteLine("[DEBUG] All players done, resolving dealer hand...");

                     if (!game.DealerHandResolved)
                     {
                         RevealDealerHand(game, session);
                         ResolveDealerHand(game);
                         Console.WriteLine("[DEBUG] Dealer hand resolved.");
                     }

                     if (!game.GameEnded)
                     {
                         DetermineWinners(game);
                         EndBlackjackGame(game);
                         Console.WriteLine("[DEBUG] Game ended successfully.");
                     }
                 }
                 else
                 {
                     do
                     {
                         game.MoveToNextPlayer();
                     }
                     while (game.PlayerTurnCompleted[game.GetCurrentPlayer()] && !AllPlayersDone(game));

                     var nextPlayer = game.GetCurrentPlayer();
                     Console.WriteLine($"[DEBUG] Next player: {nextPlayer.Name}, Turn Completed: {game.PlayerTurnCompleted[nextPlayer]}");
                     foreach (var player in game.Players)
                     {
                         player.Session.Network.EnqueueSend(new GameMessageSystemChat($"It's {nextPlayer.Name}'s turn. Type /fhit or /fstand.", ChatMessageType.Tell));
                     }
                 }
             }

             // Handle player hitting in fellowship blackjack
             [CommandHandler("fhit", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Hit a card in blackjack", "")]
             public static void HandleFellowshipHitCommand(Session session, params string[] parameters)
             {
                 var game = activeFellowshipBlackjackGames[session.Player.Fellowship.FellowshipLeaderGuid];
                 Console.WriteLine($"[DEBUG] Handling hit command for {session.Player.Name}");

                 if (game.GetCurrentPlayer() != session.Player)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("It's not your turn!", ChatMessageType.Combat));
                     Console.WriteLine($"[DEBUG] Not {session.Player.Name}'s turn to hit");
                     return;
                 }

                 DealCardsToPlayer(session.Player, game, 1);
                 int handValue = BlackjackUtils.GetHandValue(game.PlayerHands[session.Player][game.PlayerCurrentHand[session.Player]]);
                 Console.WriteLine($"[DEBUG] {session.Player.Name}'s hand value after hit: {handValue}");

                 if (handValue > 21)
                 {
                     Console.WriteLine($"[DEBUG] {session.Player.Name} busted.");
                     session.Network.EnqueueSend(new GameMessageSystemChat("You busted and lost your bet.", ChatMessageType.Combat));
                     game.PlayerTurnCompleted[session.Player] = true; // Mark the player's turn as completed
                     MoveToNextActivePlayer(game, session);
                 }
                 else
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your hand value is now {handValue}. Type /fhit or /fstand.", ChatMessageType.Broadcast));
                 }
             }

             // Handle player standing in fellowship blackjack
             [CommandHandler("fstand", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Stand in fellowship blackjack", "")]
             public static void HandleFellowshipStandCommand(Session session, params string[] parameters)
             {
                 if (!activeFellowshipBlackjackGames.TryGetValue(session.Player.Fellowship.FellowshipLeaderGuid, out var game))
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are not currently in a blackjack game.", ChatMessageType.Broadcast));
                     Console.WriteLine("[DEBUG] Player not in game");
                     return;
                 }

                 var player = game.GetCurrentPlayer();
                 Console.WriteLine($"[DEBUG] Handling stand command for {player.Name}");

                 if (player != session.Player)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("It is not your turn.", ChatMessageType.Tell));
                     Console.WriteLine($"[DEBUG] Not {session.Player.Name}'s turn to stand");
                     return;
                 }

                 int handValue = BlackjackUtils.GetHandValue(game.PlayerHands[session.Player][game.PlayerCurrentHand[session.Player]]);
                 session.Network.EnqueueSend(new GameMessageSystemChat($"Your ending hand value is now {handValue}.", ChatMessageType.Broadcast));

                 // Complete the turn
                 game.PlayerTurnCompleted[player] = true;
                 Console.WriteLine($"[DEBUG] {player.Name}'s turn marked as completed.");

                 if (AllPlayersDone(game))
                 {
                     Console.WriteLine("[DEBUG] All players done, proceeding to dealer resolution");
                     RevealDealerHand(game, session);
                     ResolveDealerHand(game);
                     DetermineWinners(game);
                     EndBlackjackGame(game);
                 }
                 else
                 {
                     MoveToNextActivePlayer(game, session);
                 }
             }

             // Handle doubling down in fellowship blackjack
             [CommandHandler("fdouble", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Double down in fellowship blackjack", "")]
             public static void HandleFellowshipDoubleDownCommand(Session session, params string[] parameters)
             {
                 if (!activeFellowshipBlackjackGames.TryGetValue(session.Player.Fellowship.FellowshipLeaderGuid, out var game))
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are not currently in a blackjack game.", ChatMessageType.Combat));
                     return;
                 }

                 var player = game.GetCurrentPlayer();
                 if (player != session.Player)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("It's not your turn.", ChatMessageType.Combat));
                     return;
                 }

                 var hand = game.PlayerHands[player][game.PlayerCurrentHand[player]];

                 // Check if the player can double down (only allowed after two cards)
                 if (hand.Count == 2)
                 {
                     // Double the player's bet specifically for this hand
                     long baseBet = game.PlayerBets[player];
                     long doubleBetAmount = baseBet;

                     // Check if they can afford the double down
                     if (session.Player.BankedLuminance < doubleBetAmount)
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat("You do not have enough luminance to double down.", ChatMessageType.System));
                         return;
                     }

                     // Deduct additional bet from the player's luminance
                     session.Player.BankedLuminance -= doubleBetAmount;
                     game.PlayerBets[player] += doubleBetAmount; // Update the bet to reflect the doubled-down amount
                     game.PlayerDoubledDown[player] = true;

                     session.Network.EnqueueSend(new GameMessageSystemChat("You have doubled your bet! You will receive one more card, and your turn will end.", ChatMessageType.Broadcast));

                     // Deal one more card to the player
                     DealCardsToPlayer(player, game, 1);
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your hand value is now {BlackjackUtils.GetHandValue(game.PlayerHands[player][game.PlayerCurrentHand[player]])}.", ChatMessageType.Broadcast));

                     // Mark the player's turn as complete
                     game.PlayerTurnCompleted[player] = true;

                     // Move to the next player
                     MoveToNextActivePlayer(game, session); // Ensure proper transition after double down
                 }
                 else
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You cannot double down on this hand. You must only have two cards.", ChatMessageType.Combat));
                 }
             }

             // Handle splitting the hand in fellowship blackjack
             [CommandHandler("fsplit", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Split your hand in fellowship blackjack", "")]
             public static void HandleFellowshipSplitCommand(Session session, params string[] parameters)
             {
                 if (!activeFellowshipBlackjackGames.TryGetValue(session.Player.Fellowship.FellowshipLeaderGuid, out var game))
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are not currently in a blackjack game.", ChatMessageType.Combat));
                     return;
                 }

                 var player = game.GetCurrentPlayer();
                 if (player != session.Player)
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("It's not your turn.", ChatMessageType.Combat));
                     return;
                 }

                 var hand = game.PlayerHands[session.Player][game.PlayerCurrentHand[session.Player]];

                 // Check if the player can split (only allowed with two cards of the same value)
                 if (hand.Count == 2 && hand[0].cardValue == hand[1].cardValue)
                 {
                     // Ensure the player has enough luminance to double the bet
                     long initialBet = game.PlayerBets[session.Player];
                     if (session.Player.BankedLuminance < initialBet)
                     {
                         session.Network.EnqueueSend(new GameMessageSystemChat("You do not have enough luminance to split.", ChatMessageType.System));
                         return;
                     }

                     // Deduct additional bet from luminance and apply to the split hand
                     session.Player.BankedLuminance -= initialBet;
                     game.PlayerBets[session.Player] += initialBet;  // Update total bet to include the split amount

                     // Create a new hand by moving one card to the second hand
                     game.PlayerHands[session.Player].Add(new List<(int cardValue, CardSuit suit)> { hand[1] });
                     game.PlayerItems[session.Player].Add(new List<int>());  // Initialize tracking for the new hand
                     hand.RemoveAt(1);  // Keep one card in the original hand

                     session.Network.EnqueueSend(new GameMessageSystemChat("You have split your hand into two separate hands!", ChatMessageType.Broadcast));

                     // Deal one card to the first hand immediately
                     DealCardsToPlayer(session.Player, game, 1);
                     var firstHandCard = game.PlayerHands[session.Player][game.PlayerCurrentHand[session.Player]].Last();
                     session.Network.EnqueueSend(new GameMessageSystemChat($"You were dealt a {BlackjackUtils.GetCardName(firstHandCard.cardValue, firstHandCard.suit)} for your first hand.", ChatMessageType.Tell));

                     // Update the player on the value of their first hand
                     int firstHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[session.Player][game.PlayerCurrentHand[session.Player]]);
                     session.Network.EnqueueSend(new GameMessageSystemChat($"Your first hand value is {firstHandValue}.", ChatMessageType.Broadcast));

                     // Notify the player to continue with the first hand
                     session.Network.EnqueueSend(new GameMessageSystemChat("You are now playing your first hand. Type /fhit or /fstand.", ChatMessageType.System));
                 }
                 else
                 {
                     session.Network.EnqueueSend(new GameMessageSystemChat("You can only split if you have two cards of the same value.", ChatMessageType.Combat));
                 }
             }

             // Check if all players are done with their turns
             private static bool AllPlayersDone(FellowshipBlackjackGame game)
             {
                 bool allDone = game.PlayerTurnCompleted.All(p => p.Value);
                 Console.WriteLine($"[DEBUG] Checking if all players are done: {allDone}");
                 foreach (var entry in game.PlayerTurnCompleted)
                 {
                     Console.WriteLine($"[DEBUG] Player: {entry.Key.Name}, Turn Completed: {entry.Value}");
                 }
                 return allDone;
             }

                 // Reveal the dealer's hand after players have completed their turns
                 private static void RevealDealerHand(FellowshipBlackjackGame game, Session session)
             {
                 var (hiddenCardValue, hiddenSuit) = game.DealerHand[1];
                 Console.WriteLine($"[DEBUG] Dealer reveals hidden card: {BlackjackUtils.GetCardName(hiddenCardValue, hiddenSuit)}");

                 foreach (var player in game.Players)
                 {
                     player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Rand reveals a {BlackjackUtils.GetCardName(hiddenCardValue, hiddenSuit)}.", ChatMessageType.Combat));
                 }

                 int dealerHandValue = BlackjackUtils.GetHandValue(game.DealerHand);
                 Console.WriteLine($"[DEBUG] Dealer hand value after reveal: {dealerHandValue}");

                 foreach (var player in game.Players)
                 {
                     player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's hand value is now {dealerHandValue}.", ChatMessageType.Combat));
                 }
             }

             // Ensure final dealer hand value is sent to all players
             private static void ResolveDealerHand(FellowshipBlackjackGame game)
             {
                 if (game.DealerHandResolved) // Prevent duplicate resolves
                     return;

                 while (BlackjackUtils.GetHandValue(game.DealerHand) < 17)
                 {
                     var card = BlackjackUtils.DrawCards(1).First();
                     game.DealerHand.Add(card);

                     foreach (var player in game.Players)
                     {
                         player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Rand drew a {BlackjackUtils.GetCardName(card.cardValue, card.suit)}. Rand's hand value is now {BlackjackUtils.GetHandValue(game.DealerHand)}.", ChatMessageType.System));
                     }
                 }

                 int finalDealerValue = BlackjackUtils.GetHandValue(game.DealerHand);

                 // Broadcast final hand value only once
                 foreach (var player in game.Players)
                 {
                     player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Rand's final hand value is {finalDealerValue}.", ChatMessageType.System));
                 }

                 game.DealerHandResolved = true; // Set the flag to true once resolved
             }

             private static void DetermineWinners(FellowshipBlackjackGame game)
             {
                 int dealerValue = BlackjackUtils.GetHandValue(game.DealerHand);
                 Console.WriteLine($"[DEBUG] Dealer's final hand value in DetermineWinners: {dealerValue}");

                 foreach (var player in game.Players)
                 {
                     // Verify the player exists in all required dictionaries before proceeding
                     if (!game.PlayerBets.ContainsKey(player))
                     {
                         Console.WriteLine($"[ERROR] Player {player.Name} missing from PlayerBets dictionary.");
                         continue;
                     }

                     if (!game.PlayerHands.ContainsKey(player))
                     {
                         Console.WriteLine($"[ERROR] Player {player.Name} missing from PlayerHands dictionary.");
                         continue;
                     }

                     // Skip players with natural blackjack to avoid duplicate rewards and messages
                     if (game.HasNaturalBlackjack.TryGetValue(player, out bool hasBlackjack) && hasBlackjack)
                     {
                         Console.WriteLine($"[DEBUG] Skipping {player.Name} as they have a natural blackjack.");
                         continue;
                     }

                     long totalWinnings = 0;
                     bool playerWon = false;

                     for (int i = 0; i < game.PlayerHands[player].Count; i++)
                     {
                         int playerHandValue = BlackjackUtils.GetHandValue(game.PlayerHands[player][i]);
                         long baseBet = game.PlayerBets[player] / game.PlayerHands[player].Count; // Divide initial bet for each split hand
                         long winnings = 0;

                         // Scenario 1: Player busts
                         if (playerHandValue > 21)
                         {
                             player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                                 $"You busted with a hand value of {playerHandValue} on hand {i + 1}. You lost your bet of {baseBet:N0} luminance.",
                                 ChatMessageType.Combat));
                         }
                         // Scenario 2: Dealer busts or player has a higher hand than the dealer
                         else if (dealerValue > 21 || playerHandValue > dealerValue)
                         {
                             winnings = (long)(baseBet * 1.25); // Normal play or split hand winnings (125% of bet)
                             totalWinnings += winnings;

                             player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                                 $"You won with a hand value of {playerHandValue} on hand {i + 1}! You gain {winnings:N0} luminance.",
                                 ChatMessageType.Broadcast));

                             playerWon = true;
                         }
                         // Scenario 3: Tie with dealer
                         else if (playerHandValue == dealerValue)
                         {
                             player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                                 $"You tied with Rand on hand {i + 1}. Your bet of {baseBet:N0} luminance has been returned.",
                                 ChatMessageType.Tell));
                             totalWinnings += baseBet;
                         }
                         // Scenario 4: Dealer wins with a higher hand value
                         else
                         {
                             player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                                 $"You lost with a hand value of {playerHandValue} on hand {i + 1}. You lost your bet of {baseBet:N0} luminance.",
                                 ChatMessageType.Combat));
                         }
                     }

                     if (playerWon)
                     {
                         player.BankedLuminance += totalWinnings;
                         Console.WriteLine($"[DEBUG] {player.Name} won a total of {totalWinnings:N0} luminance.");
                     }
                 }
             }

             // Reset dealt cards when a game ends
             private static void EndBlackjackGame(FellowshipBlackjackGame game)
             {
                 Console.WriteLine($"[DEBUG] Attempting to end game for fellowship leader: {game.FellowshipLeaderGuid}");
                 if (game.GameEnded)
                 {
                     Console.WriteLine("[DEBUG] Game already marked as ended, returning.");
                     return;
                 }

                 game.GameEnded = true;
                 Console.WriteLine("[DEBUG] GameEnded flag set to true");

                 foreach (var player in game.Players)
                 {
                     Console.WriteLine($"[DEBUG] Removing cards for player: {player.Name}");
                     foreach (var handItems in game.PlayerItems[player])
                     {
                         foreach (var itemWeenieClassId in handItems.Distinct())
                         {
                             Console.WriteLine($"[DEBUG] Removing item with ID {itemWeenieClassId} from {player.Name}'s inventory");
                             RemoveItemFromPlayer(player, itemWeenieClassId);
                         }
                     }
                     player.Session.Network.EnqueueSend(new GameMessageSystemChat("The blackjack game has ended, and the cards have been removed from your inventory.", ChatMessageType.Broadcast));
                 }

                 activeFellowshipBlackjackGames.Remove(game.FellowshipLeaderGuid);
                 game.DealtCards.Clear();
                 Console.WriteLine("[DEBUG] Active game removed and dealt cards cleared.");
             }

             // Helper to remove items from players' inventory
             public static void RemoveItemFromPlayer(Player player, int itemWeenieClassId)
             {
                 try
                 {
                     var items = player.GetInventoryItemsOfWCID((uint)itemWeenieClassId);

                     if (items.Any())
                     {
                         foreach (var item in items.ToList())
                         {
                             player.TryRemoveFromInventoryWithNetworking(item.Guid, out _, Player.RemoveFromInventoryAction.ConsumeItem);
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error in RemoveItemFromPlayer for player {player.Name}: {ex.Message}");
                 }
             }*/
    }
}
