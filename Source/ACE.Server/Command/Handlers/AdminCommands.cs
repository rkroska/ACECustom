using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.EntityFrameworkCore;

using log4net;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Models.Auth;
using ShardModels = ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;

using Position = ACE.Entity.Position;

namespace ACE.Server.Command.Handlers
{
    public static class AdminCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private const int MaxTransferLogDisplayCount = 20;
        private const int DefaultTransferPatternDays = 7;
        private const int SeparatorLineLength = 80;

        // // commandname parameters
        // [CommandHandler("commandname", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0)]
        // public static void HandleHelp(Session session, params string[] parameters)
        // {
        //     //TODO: output
        // }

        // bankaudit {subcommand} {parameters}
        [CommandHandler("bankaudit", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Bank transfer audit commands.",
            "log|patterns|suspicious|summaries|alerts|monitor|config|items|blacklist|status|cleanup|ip|migrate|fixsummaries|bankban|top|help\nUse 'bankaudit help' for detailed command information.")]
        public static void HandleBankAudit(Session session, params string[] parameters)
        {
            HandleBankAuditInternal(session, parameters);
        }

        // ba {subcommand} {parameters} - Alias for bankaudit
        [CommandHandler("ba", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Bank transfer audit commands (alias for bankaudit).",
            "log|patterns|suspicious|summaries|alerts|monitor|config|items|blacklist|status|cleanup|ip|migrate|fixsummaries|bankban|top|help\nUse 'ba help' for detailed command information.")]
        public static void HandleBA(Session session, params string[] parameters)
        {
            HandleBankAuditInternal(session, parameters);
        }

        private static void HandleBankAuditInternal(Session session, params string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit <subcommand> [parameters] (or /ba)", ChatMessageType.Help));
                session.Network.EnqueueSend(new GameMessageSystemChat("Subcommands: log, patterns, suspicious, summaries, alerts, monitor, config, items, blacklist, status, cleanup, ip, migrate, fixsummaries, bankban, top, help", ChatMessageType.Help));
                return;
            }

            var subcommand = parameters[0].ToLower();
            var subParams = parameters.Skip(1).ToArray();

            switch (subcommand)
            {
                case "log":
                    HandleTransferLog(session, subParams);
                    break;
                case "patterns":
                    HandleTransferPatterns(session, subParams);
                    break;
                case "suspicious":
                    HandleSuspiciousTransfers(session, subParams);
                    break;
                case "summaries":
                    HandleTransferSummaries(session, subParams);
                    break;
                case "alerts":
                    HandleTransferAlerts(session, subParams);
                    break;
                case "monitor":
                    HandleTransferMonitor(session, subParams);
                    break;
                case "config":
                    HandleTransferConfig(session, subParams);
                    break;
                case "blacklist":
                    HandleTransferBlacklist(session, subParams);
                    break;
                case "status":
                    HandleTransferStatus(session, subParams);
                    break;
                case "cleanup":
                    HandleTransferCleanup(session, subParams);
                    break;
                case "items":
                    HandleTransferItems(session, subParams);
                    break;
                case "ip":
                    HandleTransferIPCheck(session, subParams);
                    break;
                case "migrate":
                    HandleTransferMigration(session, subParams);
                    break;
                case "fixsummaries":
                    HandleFixSummaries(session, subParams);
                    break;
                case "bankban":
                    HandleBankBlacklist(session, subParams);
                    break;
                case "top":
                    HandleTransferTop(session, subParams);
                    break;
                case "help":
                    HandleTransferHelp(session, subParams);
                    break;
                default:
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown subcommand: {subcommand}", ChatMessageType.Help));
                    session.Network.EnqueueSend(new GameMessageSystemChat("Use 'bankaudit help' or 'ba help' for available commands", ChatMessageType.Help));
                    break;
            }
        }

        private static string ParsePlayerName(string[] parameters, out int days, out bool daysSpecified)
        {
            days = 7;
            daysSpecified = false;
            
            string playerName;
            
            // Check if last parameter is a number (days)
            if (parameters.Length > 1 && int.TryParse(parameters[parameters.Length - 1], out var parsedDays))
            {
                days = parsedDays;
                daysSpecified = true;
                // Join all parameters except the last one for the player name
                playerName = string.Join(" ", parameters.Take(parameters.Length - 1));
            }
            else
            {
                // Join all parameters for the player name
                playerName = string.Join(" ", parameters);
            }
            
            // Trim whitespace and surrounding quotes
            return playerName.Trim().Trim('"', '\'');
        }

        private static string ConsumeName(List<string> tokens)
        {
            if (tokens.Count == 0)
                return string.Empty;

            var first = tokens[0];
            if (!string.IsNullOrEmpty(first) && (first[0] == '"' || first[0] == '\''))
            {
                var quote = first[0];
                var pieces = new List<string>();

                while (tokens.Count > 0)
                {
                    var part = tokens[0];
                    tokens.RemoveAt(0);
                    pieces.Add(part);

                    if (!string.IsNullOrEmpty(part) && part[^1] == quote)
                    {
                        var joined = string.Join(" ", pieces);
                        return joined.Trim(quote).Trim();
                    }
                }

                return string.Join(" ", pieces).Trim('"', '\'').Trim();
            }

            tokens.RemoveAt(0);
            return first;
        }

        private static void HandleTransferLog(Session session, string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit log <player> [days] (or /ba log)", ChatMessageType.Help));
                return;
            }

            var playerName = ParsePlayerName(parameters, out var days, out var daysSpecified);

            // Validate player name is not empty
            if (string.IsNullOrWhiteSpace(playerName))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit log <player> [days] (or /ba log)", ChatMessageType.Help));
                return;
            }

            // Clamp days to prevent ArgumentOutOfRangeException
            days = Math.Max(0, Math.Min(days, 3650)); // ~10 years safety window
            var transfers = TransferLogger.GetTransferHistory(playerName, days);
            
            if (transfers.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"No transfers found for {playerName} in the last {days} days.", ChatMessageType.System));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer History for {playerName} (Last {days} days):", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("=".PadRight(SeparatorLineLength, '='), ChatMessageType.System));

            foreach (var transfer in transfers.Take(MaxTransferLogDisplayCount)) // Limit to most recent
            {
                var fromAccountAge = transfer.FromAccountCreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown";
                var toAccountAge = transfer.ToAccountCreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown";
                var fromCharAge = transfer.FromCharacterCreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown";
                var toCharAge = transfer.ToCharacterCreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown";
                
                var message = $"{transfer.Timestamp:MM/dd HH:mm} | {transfer.TransferType} | {transfer.FromPlayerName} ({transfer.FromPlayerAccount}) -> {transfer.ToPlayerName} ({transfer.ToPlayerAccount}) | {transfer.ItemName} x{transfer.Quantity} | From: Acc({fromAccountAge}) Char({fromCharAge}) | To: Acc({toAccountAge}) Char({toCharAge})";
                session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.System));
            }

            if (transfers.Count > MaxTransferLogDisplayCount)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"... and {transfers.Count - MaxTransferLogDisplayCount} more transfers", ChatMessageType.System));
            }
        }

        private static void HandleSuspiciousTransfers(Session session, string[] parameters)
        {
            var days = 7;
            if (parameters.Length > 0 && int.TryParse(parameters[0], out var parsedDays))
            {
                days = parsedDays;
            }

            // Clamp days to prevent ArgumentOutOfRangeException
            days = Math.Max(0, Math.Min(days, 3650)); // ~10 years safety window
            var transfers = TransferLogger.GetRecentTransfers(days);
            
            if (transfers.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"No suspicious transfers found in the last {days} days.", ChatMessageType.System));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Suspicious Transfers (Last {days} days):", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("=".PadRight(SeparatorLineLength, '='), ChatMessageType.System));

            foreach (var transfer in transfers.Take(MaxTransferLogDisplayCount)) // Limit to most recent
            {
                var message = $"{transfer.Timestamp:MM/dd HH:mm} | {transfer.TransferType} | {transfer.FromPlayerName} ({transfer.FromPlayerAccount}) -> {transfer.ToPlayerName} ({transfer.ToPlayerAccount}) | {transfer.ItemName} x{transfer.Quantity} | From Account: {transfer.FromPlayerAccount ?? "Unknown"} | To Account: {transfer.ToPlayerAccount ?? "Unknown"}";
                session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.System));
            }

            if (transfers.Count > MaxTransferLogDisplayCount)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"... and {transfers.Count - MaxTransferLogDisplayCount} more suspicious transfers", ChatMessageType.System));
            }
        }

        private static void HandleTransferPatterns(Session session, string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit patterns <player> [days] (or /ba patterns)", ChatMessageType.Help));
                return;
            }

            var playerName = ParsePlayerName(parameters, out var days, out var daysSpecified);
            if (days == 7 && !daysSpecified) days = DefaultTransferPatternDays; // Use default for patterns if not specified

            // Clamp days to prevent ArgumentOutOfRangeException
            days = Math.Max(0, Math.Min(days, 3650)); // ~10 years safety window
            var patterns = TransferLogger.GetTransferPatterns(playerName, days);
            
            if (patterns.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"No transfer patterns found for {playerName} in the last {days} days.", ChatMessageType.System));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer Patterns for {playerName} (Last {days} days):", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("=".PadRight(SeparatorLineLength, '='), ChatMessageType.System));

            foreach (var pattern in patterns.Take(MaxTransferLogDisplayCount)) // Limit to most recent
            {
                var fromAccountAge = pattern.FromAccountCreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown";
                var toAccountAge = pattern.ToAccountCreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown";
                var fromCharAge = pattern.FromCharacterCreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown";
                var toCharAge = pattern.ToCharacterCreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown";
                
                var message = $"{pattern.Timestamp:MM/dd HH:mm} | {pattern.TransferType} | {pattern.FromPlayerName} ({pattern.FromPlayerAccount}) -> {pattern.ToPlayerName} ({pattern.ToPlayerAccount}) | {pattern.ItemName} x{pattern.Quantity} | From: Acc({fromAccountAge}) Char({fromCharAge}) | To: Acc({toAccountAge}) Char({toCharAge})";
                session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.System));
            }

            if (patterns.Count > MaxTransferLogDisplayCount)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"... and {patterns.Count - MaxTransferLogDisplayCount} more pattern transfers", ChatMessageType.System));
            }
        }

        private static void HandleTransferConfig(Session session, string[] parameters)
        {
            if (parameters.Length < 2)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit config <setting> <value>", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("Settings: threshold, timewindow, patternthreshold, trackall, enabled, transferlogging, adminnotifications", ChatMessageType.System));
                return;
            }

            var setting = parameters[0].ToLower();
            var value = parameters[1];

            try
            {
                switch (setting)
            {
                case "threshold":
                        if (int.TryParse(value, out var threshold))
                        {
                            TransferLogger.UpdateSuspiciousTransferThreshold(threshold);
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Updated suspicious transfer threshold to {threshold}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Invalid threshold value. Must be a number.", ChatMessageType.System));
                        }
                    break;

                case "timewindow":
                        if (int.TryParse(value, out var hours))
                        {
                            TransferLogger.UpdateTimeWindowHours(hours);
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Updated time window to {hours} hours", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Invalid time window value. Must be a number.", ChatMessageType.System));
                        }
                    break;

                case "patternthreshold":
                        if (int.TryParse(value, out var patternThreshold))
                        {
                            TransferLogger.UpdatePatternDetectionThreshold(patternThreshold);
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Updated pattern detection threshold to {patternThreshold}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Invalid pattern threshold value. Must be a number.", ChatMessageType.System));
                        }
                    break;

                    case "trackall":
                        if (bool.TryParse(value, out var trackAll) || value.ToLower() == "on" || value.ToLower() == "off")
                        {
                            var trackAllValue = value.ToLower() == "on" || (value.ToLower() == "true");
                            TransferLogger.UpdateTrackAllItems(trackAllValue);
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Updated track all items to {(trackAllValue ? "ON" : "OFF")}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Invalid track all value. Use 'on', 'off', 'true', or 'false'.", ChatMessageType.System));
                        }
                        break;

                    case "enabled":
                        if (bool.TryParse(value, out var enabled) || value.ToLower() == "on" || value.ToLower() == "off")
                        {
                            var enabledValue = value.ToLower() == "on" || (value.ToLower() == "true");
                            TransferLogger.UpdateEnableItemTracking(enabledValue);
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Updated item tracking to {(enabledValue ? "ON" : "OFF")}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Invalid enabled value. Use 'on', 'off', 'true', or 'false'.", ChatMessageType.System));
                        }
                        break;

                    case "transferlogging":
                        if (bool.TryParse(value, out var transferLogging) || value.ToLower() == "on" || value.ToLower() == "off")
                        {
                            var transferLoggingValue = value.ToLower() == "on" || (value.ToLower() == "true");
                            TransferLogger.UpdateEnableTransferLogging(transferLoggingValue);
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Updated transfer logging to {(transferLoggingValue ? "ON" : "OFF")}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Invalid transfer logging value. Use 'on', 'off', 'true', or 'false'.", ChatMessageType.System));
                        }
                        break;

                    case "adminnotifications":
                        if (bool.TryParse(value, out var adminNotifications) || value.ToLower() == "on" || value.ToLower() == "off")
                        {
                            var adminNotificationsValue = value.ToLower() == "on" || (value.ToLower() == "true");
                            TransferLogger.UpdateEnableAdminNotifications(adminNotificationsValue);
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Updated admin notifications to {(adminNotificationsValue ? "ON" : "OFF")}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Invalid admin notifications value. Use 'on', 'off', 'true', or 'false'.", ChatMessageType.System));
                        }
                        break;

                default:
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown setting: {setting}", ChatMessageType.System));
                        session.Network.EnqueueSend(new GameMessageSystemChat("Available settings: threshold, timewindow, patternthreshold, trackall, enabled, transferlogging, adminnotifications", ChatMessageType.System));
                    break;
                }
            }
            catch (Exception ex)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Error updating configuration: {ex.Message}", ChatMessageType.System));
            }
        }

        private static void HandleTransferBlacklist(Session session, string[] parameters)
        {
            if (parameters.Length < 2)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit blacklist <add|remove|list> <player|account> [name] (or /ba blacklist)", ChatMessageType.Help));
                return;
            }

            var action = parameters[0].ToLower();
            var type = parameters[1].ToLower();

            switch (action)
            {
                case "add":
                    if (parameters.Length < 3)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit blacklist add <player|account> <name> (or /ba blacklist add)", ChatMessageType.Help));
                        return;
                    }
                    var name = string.Join(" ", parameters.Skip(2)).Trim().Trim('"', '\'');
                    if (type == "player")
                    {
                        TransferLogger.AddPlayerToBlacklist(name);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Added player '{name}' to transfer monitoring blacklist", ChatMessageType.System));
                    }
                    else if (type == "account")
                    {
                        TransferLogger.AddAccountToBlacklist(name);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Added account '{name}' to transfer monitoring blacklist", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Type must be 'player' or 'account'", ChatMessageType.Help));
                    }
                    break;

                case "remove":
                    if (parameters.Length < 3)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit blacklist remove <player|account> <name> (or /ba blacklist remove)", ChatMessageType.Help));
                        return;
                    }
                    name = string.Join(" ", parameters.Skip(2)).Trim().Trim('"', '\'');
                    if (type == "player")
                    {
                        TransferLogger.RemovePlayerFromBlacklist(name);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Removed player '{name}' from transfer monitoring blacklist", ChatMessageType.System));
                    }
                    else if (type == "account")
                    {
                        TransferLogger.RemoveAccountFromBlacklist(name);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Removed account '{name}' from transfer monitoring blacklist", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Type must be 'player' or 'account'", ChatMessageType.Help));
                    }
                    break;

                case "list":
                    if (type == "player")
                    {
                        var players = TransferLogger.GetBlacklistedPlayers();
                        if (players.Count == 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("No blacklisted players", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Blacklisted players: {string.Join(", ", players)}", ChatMessageType.System));
                        }
                    }
                    else if (type == "account")
                    {
                        var accounts = TransferLogger.GetBlacklistedAccounts();
                        if (accounts.Count == 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("No blacklisted accounts", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Blacklisted accounts: {string.Join(", ", accounts)}", ChatMessageType.System));
                        }
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Type must be 'player' or 'account'", ChatMessageType.Help));
                    }
                    break;

                default:
                    session.Network.EnqueueSend(new GameMessageSystemChat("Action must be 'add', 'remove', or 'list'", ChatMessageType.Help));
                    break;
            }
        }

        private static void HandleTransferStatus(Session session, string[] parameters)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Transfer Monitoring Configuration:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("=".PadRight(50, '='), ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Suspicious Transfer Threshold: {TransferLogger.SuspiciousTransferThreshold}", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Time Window: {TransferLogger.SuspiciousTransferTimeWindowHours} hours", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Pattern Detection Threshold: {TransferLogger.PatternDetectionThreshold}", ChatMessageType.System));
            
            var blacklistedPlayers = TransferLogger.GetBlacklistedPlayers();
            var blacklistedAccounts = TransferLogger.GetBlacklistedAccounts();
            
            session.Network.EnqueueSend(new GameMessageSystemChat($"Blacklisted Players: {blacklistedPlayers.Count}", ChatMessageType.System));
            if (blacklistedPlayers.Count > 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"  {string.Join(", ", blacklistedPlayers)}", ChatMessageType.System));
            }
            
            session.Network.EnqueueSend(new GameMessageSystemChat($"Blacklisted Accounts: {blacklistedAccounts.Count}", ChatMessageType.System));
            if (blacklistedAccounts.Count > 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"  {string.Join(", ", blacklistedAccounts)}", ChatMessageType.System));
            }
        }


        private static void HandleTransferHelp(Session session, string[] parameters)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Bank Transfer Audit Commands:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("=".PadRight(50, '='), ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("Use /bankaudit or /ba (short alias)", ChatMessageType.System));
            
            session.Network.EnqueueSend(new GameMessageSystemChat("", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("AUDIT COMMANDS:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit log <player> [days] - Show transfer history for a player", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit patterns <player> [days] - Show transfer patterns (repeated transfers)", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit suspicious [days] - Show flagged suspicious transfers", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit status - Show current configuration and blacklist", ChatMessageType.System));
            
            session.Network.EnqueueSend(new GameMessageSystemChat("", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("NEW FEATURES:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit summaries <player> [days] - Show transfer summaries for a player", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit alerts - Show recent transfer alerts", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit monitor - Show real-time monitoring stats", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit cleanup [days] - Clean up old transfer data", ChatMessageType.System));
            
            session.Network.EnqueueSend(new GameMessageSystemChat("", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("CONFIGURATION COMMANDS:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"/bankaudit config threshold <number> - Set transfer threshold (current: {TransferLogger.SuspiciousTransferThreshold})", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"/bankaudit config timewindow <hours> - Set time window (current: {TransferLogger.SuspiciousTransferTimeWindowHours})", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"/bankaudit config patternthreshold <number> - Set pattern threshold (current: {TransferLogger.PatternDetectionThreshold})", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit config transferlogging <on|off> - Enable/disable all transfer logging", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit config adminnotifications <on|off> - Enable/disable admin notifications", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit config trackall <on|off> - Track all items or only listed items", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit config enabled <on|off> - Enable/disable item tracking", ChatMessageType.System));
            
            session.Network.EnqueueSend(new GameMessageSystemChat("", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("ITEM TRACKING COMMANDS:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit items add <itemname> - Add item to tracking list", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit items remove <itemname> - Remove item from tracking list", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit items list - Show tracked items list", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit ip <player> [days] - Show IP address patterns for transfers", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit top [days] - Show most active transfer participants", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit migrate - Create/update all transfer monitoring tables", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit fixsummaries - Fix duplicate transfer summaries and create unique index", ChatMessageType.System));
            
            session.Network.EnqueueSend(new GameMessageSystemChat("", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("BLACKLIST COMMANDS:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit blacklist add player <name> - Add player to blacklist", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit blacklist add account <name> - Add account to blacklist", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit blacklist remove player <name> - Remove player from blacklist", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit blacklist remove account <name> - Remove account from blacklist", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit blacklist list player - Show blacklisted players", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit blacklist list account - Show blacklisted accounts", ChatMessageType.System));
            
            session.Network.EnqueueSend(new GameMessageSystemChat("", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("BANK COMMAND BLACKLIST:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit bankban add player <name> <reason> [days] - Ban player from using /bank commands", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit bankban add account <name> <reason> [days] - Ban account from using /bank commands", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit bankban remove player <name> - Remove player from bank command blacklist", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit bankban remove account <name> - Remove account from bank command blacklist", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("/bankaudit bankban list - Show all bank command blacklisted players/accounts", ChatMessageType.System));
        }

        private static void HandleTransferSummaries(Session session, string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit summaries <player> [days]", ChatMessageType.System));
                return;
            }

            var playerName = ParsePlayerName(parameters, out var days, out var daysSpecified);

            // Validate player name is not empty
            if (string.IsNullOrWhiteSpace(playerName))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Please specify a player name. Usage: /bankaudit summaries <player> [days]", ChatMessageType.System));
                return;
            }

            if (days == 7 && !daysSpecified) days = 30; // Use default 30 days for summaries if not specified

            // Clamp days to prevent ArgumentOutOfRangeException
            days = Math.Max(0, Math.Min(days, 3650)); // ~10 years safety window
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var summaries = DatabaseManager.Shard.BaseDatabase.GetTransferSummaries(playerName, cutoffDate);

            if (summaries.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"No transfer summaries found for {playerName} in the last {days} days.", ChatMessageType.System));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer Summaries for {playerName} (Last {days} days):", ChatMessageType.System));
            foreach (var summary in summaries.Take(15))
            {
                var riskFlag = summary.IsSuspicious ? " [SUSPICIOUS]" : "";
                var message = $"{summary.TransferType} | {summary.FromPlayerName} -> {summary.ToPlayerName} | {summary.TotalTransfers} transfers | {summary.TotalValue:N0} total value | {summary.SuspiciousTransfers} suspicious{riskFlag}";
                session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.System));
            }
        }


        private static void HandleTransferAlerts(Session session, string[] parameters)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Transfer Monitoring Stats (fixed windows: 1m/1h/1d):", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer Rate: {TransferMonitor.GetTransferRate():F1} transfers/minute", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Suspicious Rate: {TransferMonitor.GetSuspiciousRate():F1} suspicious/hour", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"High Value Rate: {TransferMonitor.GetHighValueRate():F1} high-value/day", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Transfers Last Minute: {TransferMonitor.TransfersLastMinute}", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Suspicious Last Hour: {TransferMonitor.SuspiciousLastHour}", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"High Value Last Day: {TransferMonitor.HighValueLastDay} events", ChatMessageType.System));
        }

        private static void HandleTransferMonitor(Session session, string[] parameters)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Real-Time Transfer Monitoring:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Current Transfer Rate: {TransferMonitor.GetTransferRate():F1} transfers/minute", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Current Suspicious Rate: {TransferMonitor.GetSuspiciousRate():F1} suspicious/hour", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Current High Value Rate: {TransferMonitor.GetHighValueRate():F1} high-value/day", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Current Rate Counters:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"  Transfers Last Minute: {TransferMonitor.TransfersLastMinute}", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"  Suspicious Last Hour: {TransferMonitor.SuspiciousLastHour}", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat($"  High Value Last Day: {TransferMonitor.HighValueLastDay} events", ChatMessageType.System));
        }

        private static void HandleTransferCleanup(Session session, string[] parameters)
        {
            var days = parameters.Length > 0 && int.TryParse(parameters[0], out var parsedDays) ? parsedDays : 30;

            // Clamp days to prevent DateTime overflow/underflow in downstream cleanup calls
            days = Math.Max(1, Math.Min(days, 36500)); // ~100 years safety window

            session.Network.EnqueueSend(new GameMessageSystemChat($"Cleaning up transfer data older than {days} days...", ChatMessageType.System));
            
            try
            {
                DatabaseManager.Shard.BaseDatabase.CleanupOldTransferLogs(days);
                DatabaseManager.Shard.BaseDatabase.CleanupOldSummaries(days * 12); // Keep summaries longer
                session.Network.EnqueueSend(new GameMessageSystemChat("Cleanup completed successfully.", ChatMessageType.System));
            }
            catch (Exception ex)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Cleanup failed: {ex.Message}", ChatMessageType.System));
            }
        }

        private static void HandleTransferTop(Session session, string[] parameters)
        {
            var days = 7;
            if (parameters.Length > 0 && int.TryParse(parameters[0], out var parsedDays))
            {
                days = parsedDays;
            }

            try
            {
                // Clamp days to prevent ArgumentOutOfRangeException
                days = Math.Max(0, Math.Min(days, 3650)); // ~10 years safety window
                var cutoffDate = DateTime.UtcNow.AddDays(-days);
                
                // Get most active players by transfer count
                var topPlayers = DatabaseManager.Shard.BaseDatabase.GetTopTransferParticipants(cutoffDate, 20);
                
                if (topPlayers.Count == 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"No transfer activity found in the last {days} days.", ChatMessageType.System));
                    return;
                }

                session.Network.EnqueueSend(new GameMessageSystemChat($"Top Transfer Participants (Last {days} days):", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat("=".PadRight(SeparatorLineLength, '='), ChatMessageType.System));
                
                int rank = 1;
                foreach (var player in topPlayers)
                {
                    var message = $"{rank,2}. {player.PlayerName,-20} | {player.TotalTransfers,3} transfers | {player.UniquePartners,2} partners | {player.TotalQuantity,6} items";
                    session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.System));
                    rank++;
                }
            }
            catch (Exception ex)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Error retrieving top transfer participants: {ex.Message}", ChatMessageType.System));
            }
        }

        private static void HandleTransferItems(Session session, string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit items <add|remove|list|trackall|enabled> [parameters]", ChatMessageType.System));
                return;
            }

            var action = parameters[0].ToLower();

            switch (action)
            {
                case "add":
                    if (parameters.Length < 2)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit items add <itemname>", ChatMessageType.System));
                        return;
                    }
                    var itemToAdd = string.Join(" ", parameters.Skip(1));
                    TransferLogger.AddTrackedItem(itemToAdd);
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Added '{itemToAdd}' to tracked items list", ChatMessageType.System));
                    break;

                case "remove":
                    if (parameters.Length < 2)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit items remove <itemname>", ChatMessageType.System));
                        return;
                    }
                    var itemToRemove = string.Join(" ", parameters.Skip(1));
                    TransferLogger.RemoveTrackedItem(itemToRemove);
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Removed '{itemToRemove}' from tracked items list", ChatMessageType.System));
                    break;

                case "list":
                    var trackedItems = TransferLogger.GetTrackedItems();
                    if (trackedItems.Count == 0)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("No items are currently being tracked", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Tracked items ({trackedItems.Count}): {string.Join(", ", trackedItems)}", ChatMessageType.System));
                    }
                    break;

                case "trackall":
                    if (parameters.Length < 2)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit items trackall <on|off>", ChatMessageType.System));
                        return;
                    }
                    var trackAllValue = parameters[1].ToLower();
                    if (trackAllValue == "on" || trackAllValue == "true" || trackAllValue == "1")
                    {
                        TransferLogger.SetTrackAllItems(true);
                        session.Network.EnqueueSend(new GameMessageSystemChat("All items will now be tracked", ChatMessageType.System));
                    }
                    else if (trackAllValue == "off" || trackAllValue == "false" || trackAllValue == "0")
                    {
                        TransferLogger.SetTrackAllItems(false);
                        session.Network.EnqueueSend(new GameMessageSystemChat("Only listed items will be tracked", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit items trackall <on|off>", ChatMessageType.System));
                    }
                    break;

                case "enabled":
                    if (parameters.Length < 2)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit items enabled <on|off>", ChatMessageType.System));
                        return;
                    }
                    var enabledValue = parameters[1].ToLower();
                    if (enabledValue == "on" || enabledValue == "true" || enabledValue == "1")
                    {
                        TransferLogger.SetItemTrackingEnabled(true);
                        session.Network.EnqueueSend(new GameMessageSystemChat("Item tracking enabled", ChatMessageType.System));
                    }
                    else if (enabledValue == "off" || enabledValue == "false" || enabledValue == "0")
                    {
                        TransferLogger.SetItemTrackingEnabled(false);
                        session.Network.EnqueueSend(new GameMessageSystemChat("Item tracking disabled", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit items enabled <on|off>", ChatMessageType.System));
                    }
                    break;

                default:
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown action: {action}", ChatMessageType.System));
                    session.Network.EnqueueSend(new GameMessageSystemChat("Available actions: add, remove, list, trackall, enabled", ChatMessageType.System));
                    break;
            }
        }

        private static void HandleTransferIPCheck(Session session, string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit ip <player> [days]", ChatMessageType.System));
                return;
            }

            var playerName = ParsePlayerName(parameters, out var days, out var daysSpecified);

            // Clamp days to prevent ArgumentOutOfRangeException
            days = Math.Max(0, Math.Min(days, 3650)); // ~10 years safety window
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var transfers = DatabaseManager.Shard.BaseDatabase.GetTransferHistory(playerName, cutoffDate);

            if (transfers.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"No transfers found for {playerName} in the last {days} days.", ChatMessageType.System));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"IP Address Analysis for {playerName} (Last {days} days):", ChatMessageType.System));

            // Group transfers by IP addresses
            var ipGroups = transfers.Where(t => !string.IsNullOrEmpty(t.FromPlayerIP) || !string.IsNullOrEmpty(t.ToPlayerIP))
                                   .GroupBy(t => new { FromIP = t.FromPlayerIP, ToIP = t.ToPlayerIP })
                                   .OrderByDescending(g => g.Count())
                                   .Take(10);

            foreach (var ipGroup in ipGroups)
            {
                var fromIP = ipGroup.Key.FromIP ?? "Unknown";
                var toIP = ipGroup.Key.ToIP ?? "Unknown";
                var count = ipGroup.Count();
                var transfersList = ipGroup.OrderByDescending(t => t.Timestamp).Take(3);
                
                session.Network.EnqueueSend(new GameMessageSystemChat($"  {count} transfers: {fromIP} -> {toIP}", ChatMessageType.System));
                
                foreach (var transfer in transfersList)
                {
                    var message = $"    {transfer.Timestamp:MM-dd HH:mm} | {transfer.TransferType} | {transfer.ItemName} x{transfer.Quantity} | From: {transfer.FromPlayerName} ({transfer.FromPlayerAccount ?? "Unknown"}) | To: {transfer.ToPlayerName} ({transfer.ToPlayerAccount ?? "Unknown"})";
                    session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.System));
                }
            }

            // Show different IP patterns (alt account detection)
            var differentIPTransfers = transfers.Where(t => 
                !string.IsNullOrEmpty(t.FromPlayerIP) && 
                !string.IsNullOrEmpty(t.ToPlayerIP) && 
                t.FromPlayerIP != t.ToPlayerIP).ToList();

            if (differentIPTransfers.Count > 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"DIFFERENT IP TRANSFERS (Alt Account Detection):", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"{differentIPTransfers.Count} transfers to players on different IPs", ChatMessageType.System));
                
                var groupedByTarget = differentIPTransfers.GroupBy(t => t.ToPlayerName)
                                                         .OrderByDescending(g => g.Count())
                                                         .Take(5);
                
                foreach (var group in groupedByTarget)
                {
                    var targetPlayer = group.Key;
                    var count = group.Count();
                    var uniqueIPs = group.Select(t => t.ToPlayerIP).Distinct().Count();
                    session.Network.EnqueueSend(new GameMessageSystemChat($"  {count} transfers to {targetPlayer} from {uniqueIPs} different IPs", ChatMessageType.System));
                }
            }
        }

        /// <summary>
        /// Handles database migration for all transfer monitoring tables.
        /// This command is safe to run multiple times and handles both scenarios:
        /// 1. Tables don't exist - Creates all tables from scratch
        /// 2. Tables exist - Adds missing columns or leaves existing tables alone
        /// </summary>
        private static void HandleTransferMigration(Session session, string[] parameters)
        {
            try
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Starting database migration for all transfer monitoring tables...", ChatMessageType.System));
                
                using (var context = new ShardModels.ShardDbContext())
                {
                    // Create transfer_logs table
                    CreateTransferLogsTableForMigration(context, session);
                    
                    // Create transfer_summaries table
                    CreateTransferSummariesTableForMigration(context, session);
                    
                    // Create tracked_items table
                    CreateTrackedItemsTableForMigration(context, session);
                    
                    // Create transfer_monitoring_configs table
                    CreateTransferMonitoringConfigsTableForMigration(context, session);
                    
                    // Create transfer_blacklist table
                    CreateTransferBlacklistTableForMigration(context, session);
                    
                    session.Network.EnqueueSend(new GameMessageSystemChat("All transfer monitoring tables migration completed successfully!", ChatMessageType.System));
                }
            }
            catch (Exception ex)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Database migration failed: {ex.Message}", ChatMessageType.System));
            }
        }

        private static void HandleFixSummaries(Session session, string[] parameters)
        {
            try
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Fixing duplicate transfer summaries...", ChatMessageType.System));
                
                using (var context = new ShardModels.ShardDbContext())
                {
                    // First, check if the unique index exists
                    var indexExists = false;
                    try
                    {
                        using (var command = context.Database.GetDbConnection().CreateCommand())
                        {
                            command.CommandText = @"
                                SELECT COUNT(*) FROM information_schema.statistics 
                                WHERE table_schema = DATABASE() 
                                AND table_name = 'transfer_summaries' 
                                AND index_name = 'idx_transfer_summary_unique'";
                            context.Database.OpenConnection();
                            var count = Convert.ToInt32(command.ExecuteScalar());
                            indexExists = count > 0;
                        }
                    }
                    catch
                    {
                        // If query fails, assume index doesn't exist
                        indexExists = false;
                    }
                    finally
                    {
                        context.Database.CloseConnection();
                    }

                    if (!indexExists)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Unique index does not exist. Cleaning up duplicates and creating index...", ChatMessageType.System));
                        
                        // Clean up any duplicate data first by keeping the record with the latest LastTransfer
                        var deletedCount = context.Database.ExecuteSqlRaw(@"
                            DELETE t1 FROM transfer_summaries t1
                            INNER JOIN transfer_summaries t2
                                ON t1.FromPlayerName = t2.FromPlayerName
                               AND t1.ToPlayerName   = t2.ToPlayerName
                               AND t1.TransferType   = t2.TransferType
                               AND t1.Id             > t2.Id");

                        session.Network.EnqueueSend(new GameMessageSystemChat($"Removed {deletedCount} duplicate summary records", ChatMessageType.System));

                        // Now create the unique index
                        context.Database.ExecuteSqlRaw(@"
                            CREATE UNIQUE INDEX `idx_transfer_summary_unique`
                            ON `transfer_summaries` (`FromPlayerName`,`ToPlayerName`,`TransferType`)");
                        
                        session.Network.EnqueueSend(new GameMessageSystemChat("✓ Unique index created successfully", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("✓ Unique index already exists", ChatMessageType.System));
                    }
                    
                    session.Network.EnqueueSend(new GameMessageSystemChat("Transfer summaries fix completed successfully!", ChatMessageType.System));
                }
            }
            catch (Exception ex)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Fix summaries failed: {ex.Message}", ChatMessageType.System));
            }
        }

        private static void CreateTransferLogsTableForMigration(ShardModels.ShardDbContext context, Session session)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_logs LIMIT 1");
                session.Network.EnqueueSend(new GameMessageSystemChat("transfer_logs table exists - checking for missing columns...", ChatMessageType.System));
                
                // Check and add missing columns individually (MySQL-safe approach)
                var connection = context.Database.GetDbConnection();
                try
                {
                    context.Database.OpenConnection();
                    using var command = connection.CreateCommand();
                
                // Check if columns exist and add them individually
                var columnsToAdd = new[]
                {
                    ("FromAccountCreatedDate", "datetime(6) DEFAULT NULL"),
                    ("ToAccountCreatedDate", "datetime(6) DEFAULT NULL"),
                    ("FromCharacterCreatedDate", "datetime(6) DEFAULT NULL"),
                    ("ToCharacterCreatedDate", "datetime(6) DEFAULT NULL"),
                    ("AdditionalData", "varchar(1000) DEFAULT NULL"),
                    ("FromPlayerIP", "varchar(45) DEFAULT NULL"),
                    ("ToPlayerIP", "varchar(45) DEFAULT NULL")
                };
                
                foreach (var (columnName, columnType) in columnsToAdd)
                {
                    command.CommandText = $@"
                        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_SCHEMA = DATABASE() 
                        AND TABLE_NAME = 'transfer_logs' 
                        AND COLUMN_NAME = '{columnName}'";
                    
                    var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
                    if (!exists)
                    {
                        command.CommandText = $"ALTER TABLE `transfer_logs` ADD COLUMN `{columnName}` {columnType}";
                        command.ExecuteNonQuery();
                    }
                }
                
                // Check and modify Quantity column to bigint
                command.CommandText = @"
                    SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'transfer_logs' 
                    AND COLUMN_NAME = 'Quantity'";
                
                var quantityType = command.ExecuteScalar()?.ToString();
                if (quantityType != "bigint")
                {
                    command.CommandText = "ALTER TABLE `transfer_logs` MODIFY COLUMN `Quantity` bigint(20) NOT NULL";
                    command.ExecuteNonQuery();
                }
                
                // Check and create missing indexes
                var indexesToAdd = new[]
                {
                    ("IX_transfer_logs_FromPlayerIP", "FromPlayerIP"),
                    ("IX_transfer_logs_ToPlayerIP", "ToPlayerIP"),
                    ("IX_transfer_logs_FromAccountCreatedDate", "FromAccountCreatedDate"),
                    ("IX_transfer_logs_ToAccountCreatedDate", "ToAccountCreatedDate"),
                    ("IX_transfer_logs_FromCharacterCreatedDate", "FromCharacterCreatedDate"),
                    ("IX_transfer_logs_ToCharacterCreatedDate", "ToCharacterCreatedDate")
                };
                
                foreach (var (indexName, columnName) in indexesToAdd)
                {
                    command.CommandText = $@"
                        SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                        WHERE TABLE_SCHEMA = DATABASE() 
                        AND TABLE_NAME = 'transfer_logs' 
                        AND INDEX_NAME = '{indexName}'";
                    
                    var indexExists = Convert.ToInt32(command.ExecuteScalar()) > 0;
                    if (!indexExists)
                    {
                        command.CommandText = $"ALTER TABLE `transfer_logs` ADD KEY `{indexName}` (`{columnName}`)";
                        command.ExecuteNonQuery();
                    }
                }
                
                    session.Network.EnqueueSend(new GameMessageSystemChat("✓ transfer_logs updated with missing columns, Quantity bigint migration, and indexes", ChatMessageType.System));
                }
                finally
                {
                    context.Database.CloseConnection();
                }
            }
            catch
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Creating transfer_logs table with IP address columns...", ChatMessageType.System));
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE `transfer_logs` (
                        `Id` int(11) NOT NULL AUTO_INCREMENT,
                        `TransferType` varchar(255) NOT NULL,
                        `FromPlayerName` varchar(255) NOT NULL,
                        `FromPlayerAccount` varchar(255) DEFAULT NULL,
                        `ToPlayerName` varchar(255) NOT NULL,
                        `ToPlayerAccount` varchar(255) DEFAULT NULL,
                        `ItemName` varchar(255) NOT NULL,
                        `Quantity` bigint(20) NOT NULL,
                        `Timestamp` datetime(6) NOT NULL,
                        `FromAccountCreatedDate` datetime(6) DEFAULT NULL,
                        `ToAccountCreatedDate` datetime(6) DEFAULT NULL,
                        `FromCharacterCreatedDate` datetime(6) DEFAULT NULL,
                        `ToCharacterCreatedDate` datetime(6) DEFAULT NULL,
                        `AdditionalData` varchar(1000) DEFAULT NULL,
                        `FromPlayerIP` varchar(45) DEFAULT NULL,
                        `ToPlayerIP` varchar(45) DEFAULT NULL,
                        PRIMARY KEY (`Id`),
                        KEY `IX_transfer_logs_FromPlayerName` (`FromPlayerName`),
                        KEY `IX_transfer_logs_ToPlayerName` (`ToPlayerName`),
                        KEY `IX_transfer_logs_Timestamp` (`Timestamp`),
                        KEY `IX_transfer_logs_FromPlayerAccount` (`FromPlayerAccount`),
                        KEY `IX_transfer_logs_ToPlayerAccount` (`ToPlayerAccount`),
                        KEY `IX_transfer_logs_FromPlayerIP` (`FromPlayerIP`),
                        KEY `IX_transfer_logs_ToPlayerIP` (`ToPlayerIP`),
                        KEY `IX_transfer_logs_FromAccountCreatedDate` (`FromAccountCreatedDate`),
                        KEY `IX_transfer_logs_ToAccountCreatedDate` (`ToAccountCreatedDate`),
                        KEY `IX_transfer_logs_FromCharacterCreatedDate` (`FromCharacterCreatedDate`),
                        KEY `IX_transfer_logs_ToCharacterCreatedDate` (`ToCharacterCreatedDate`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ transfer_logs table created", ChatMessageType.System));
            }
        }

        private static void CreateTransferSummariesTableForMigration(ShardModels.ShardDbContext context, Session session)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_summaries LIMIT 1");
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ transfer_summaries table exists", ChatMessageType.System));
            }
            catch
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Creating transfer_summaries table...", ChatMessageType.System));
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE `transfer_summaries` (
                        `Id` int(11) NOT NULL AUTO_INCREMENT,
                        `FromPlayerName` varchar(255) NOT NULL,
                        `FromPlayerAccount` varchar(255) DEFAULT NULL,
                        `ToPlayerName` varchar(255) NOT NULL,
                        `ToPlayerAccount` varchar(255) DEFAULT NULL,
                        `TransferType` varchar(255) NOT NULL,
                        `TotalTransfers` int(11) NOT NULL DEFAULT '0',
                        `TotalQuantity` bigint(20) NOT NULL DEFAULT '0',
                        `TotalValue` bigint(20) NOT NULL DEFAULT '0',
                        `FirstTransfer` datetime(6) NOT NULL,
                        `LastTransfer` datetime(6) NOT NULL,
                        `SuspiciousTransfers` int(11) NOT NULL DEFAULT '0',
                        `IsSuspicious` tinyint(1) NOT NULL DEFAULT '0',
                        `CreatedDate` datetime(6) NOT NULL,
                        `UpdatedDate` datetime(6) NOT NULL,
                        PRIMARY KEY (`Id`),
                        UNIQUE KEY `idx_transfer_summary_unique` (`FromPlayerName`,`ToPlayerName`,`TransferType`),
                        KEY `IX_transfer_summaries_FromPlayerName` (`FromPlayerName`),
                        KEY `IX_transfer_summaries_ToPlayerName` (`ToPlayerName`),
                        KEY `IX_transfer_summaries_LastTransfer` (`LastTransfer`),
                        KEY `IX_transfer_summaries_IsSuspicious` (`IsSuspicious`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ transfer_summaries table created", ChatMessageType.System));
            }
        }



        private static void CreateTrackedItemsTableForMigration(ShardModels.ShardDbContext context, Session session)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM tracked_items LIMIT 1");
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ tracked_items table exists", ChatMessageType.System));
            }
            catch
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Creating tracked_items table...", ChatMessageType.System));
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE `tracked_items` (
                        `Id` int(11) NOT NULL AUTO_INCREMENT,
                        `ItemName` varchar(255) NOT NULL,
                        `CreatedDate` datetime(6) NOT NULL,
                        `UpdatedDate` datetime(6) NOT NULL,
                        `IsActive` tinyint(1) NOT NULL DEFAULT '1',
                        PRIMARY KEY (`Id`),
                        UNIQUE KEY `IX_tracked_items_ItemName` (`ItemName`),
                        KEY `IX_tracked_items_IsActive` (`IsActive`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ tracked_items table created", ChatMessageType.System));
            }
        }

        private static void CreateTransferMonitoringConfigsTableForMigration(ShardModels.ShardDbContext context, Session session)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_monitoring_configs LIMIT 1");
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ transfer_monitoring_configs table exists", ChatMessageType.System));
            }
            catch
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Creating transfer_monitoring_configs table...", ChatMessageType.System));
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE `transfer_monitoring_configs` (
                        `Id` int(11) NOT NULL AUTO_INCREMENT,
                        `SuspiciousTransferThreshold` int(11) NOT NULL DEFAULT '100000',
                        `TimeWindowHours` int(11) NOT NULL DEFAULT '24',
                        `PatternDetectionThreshold` int(11) NOT NULL DEFAULT '10',
                        `EnableTransferLogging` tinyint(1) NOT NULL DEFAULT '1',
                        `EnableSuspiciousDetection` tinyint(1) NOT NULL DEFAULT '1',
                        `EnableAdminNotifications` tinyint(1) NOT NULL DEFAULT '1',
                        `EnableTransferSummaries` tinyint(1) NOT NULL DEFAULT '1',
                        `EnableTransferLogs` tinyint(1) NOT NULL DEFAULT '1',
                        `EnableItemTracking` tinyint(1) NOT NULL DEFAULT '1',
                        `TrackAllItems` tinyint(1) NOT NULL DEFAULT '0',
                        `CreatedDate` datetime(6) NOT NULL,
                        `UpdatedDate` datetime(6) NOT NULL,
                        PRIMARY KEY (`Id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ transfer_monitoring_configs table created", ChatMessageType.System));
                
                // Insert default configuration
                context.Database.ExecuteSqlRaw(@"
                    INSERT INTO `transfer_monitoring_configs` (
                        `SuspiciousTransferThreshold`, `TimeWindowHours`, `PatternDetectionThreshold`,
                        `EnableTransferLogging`, `EnableSuspiciousDetection`, `EnableAdminNotifications`,
                        `EnableTransferSummaries`, `EnableTransferLogs`, `EnableItemTracking`, `TrackAllItems`,
                        `CreatedDate`, `UpdatedDate`
                    ) VALUES (
                        100000, 24, 10, 1, 1, 1, 1, 1, 1, 0, NOW(), NOW()
                    )");
                
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ Default configuration inserted", ChatMessageType.System));
            }
        }

        private static void CreateTransferBlacklistTableForMigration(ShardModels.ShardDbContext context, Session session)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_blacklist LIMIT 1");
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ transfer_blacklist table exists", ChatMessageType.System));
            }
            catch
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Creating transfer_blacklist table...", ChatMessageType.System));
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE `transfer_blacklist` (
                        `Id` int(11) NOT NULL AUTO_INCREMENT,
                        `PlayerName` varchar(255) NOT NULL,
                        `AccountName` varchar(255) DEFAULT NULL,
                        `Reason` varchar(255) NOT NULL,
                        `AddedBy` varchar(255) NOT NULL,
                        `CreatedDate` datetime(6) NOT NULL,
                        `ExpiryDate` datetime(6) DEFAULT NULL,
                        `IsActive` tinyint(1) NOT NULL DEFAULT '1',
                        PRIMARY KEY (`Id`),
                        KEY `IX_transfer_blacklist_PlayerName` (`PlayerName`),
                        KEY `IX_transfer_blacklist_AccountName` (`AccountName`),
                        KEY `IX_transfer_blacklist_IsActive` (`IsActive`),
                        KEY `IX_transfer_blacklist_ExpiryDate` (`ExpiryDate`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                
                session.Network.EnqueueSend(new GameMessageSystemChat("✓ transfer_blacklist table created", ChatMessageType.System));
            }
        }

        private static void ShowBankBlacklistHelp(Session session)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit bankban <add|remove|list> <player|account> <name> [reason] [days]", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("Examples:", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("  /bankaudit bankban add player Sir Harry Suspicious activity 30", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("  /bankaudit bankban add account AccountName Alt account farming", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("  /bankaudit bankban remove player PlayerName", ChatMessageType.System));
            session.Network.EnqueueSend(new GameMessageSystemChat("  /bankaudit bankban list", ChatMessageType.System));
        }

        private static void HandleBankBlacklist(Session session, string[] parameters)
        {
            if (parameters.Length < 2)
            {
                ShowBankBlacklistHelp(session);
                return;
            }

            var action = parameters[0].ToLower();
            var adminName = session.Player?.Name ?? "Unknown";

            switch (action)
            {
                case "add":
                    if (parameters.Length < 4)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit bankban add <player|account> <name> <reason> [days]", ChatMessageType.System));
                        return;
                    }

                    var type = parameters[1].ToLower();
                    
                    // Handle multi-word names and reasons
                    var remainingParams = parameters.Skip(2).ToArray();
                    var name = "";
                    var reason = "";
                    var days = 0;
                    
                    // Find the last parameter that could be a number (days)
                    var lastParam = remainingParams[remainingParams.Length - 1];
                    if (int.TryParse(lastParam, out var parsedDays))
                    {
                        days = parsedDays;
                        remainingParams = remainingParams.Take(remainingParams.Length - 1).ToArray();
                    }
                    
                    if (remainingParams.Length >= 2)
                    {
                        var tokens = new List<string>(remainingParams);
                        var quotedReasonIndex = tokens.FindIndex(t => !string.IsNullOrEmpty(t) && (t[0] == '"' || t[0] == '\''));

                        int nameTokenCount = -1;
                        if (quotedReasonIndex > 0)
                        {
                            nameTokenCount = quotedReasonIndex;
                        }
                        else
                        {
                            for (var i = tokens.Count; i >= 1; i--)
                            {
                                var candidate = string.Join(" ", tokens.Take(i));
                                var exists = type == "player"
                                    ? PlayerManager.FindByName(candidate) != null
                                      || DatabaseManager.Shard.BaseDatabase.GetCharacterStubByName(candidate) != null
                                    : DatabaseManager.Authentication.GetAccountByName(candidate) != null;
                                if (exists)
                                {
                                    nameTokenCount = i;
                                    break;
                                }
                            }
                        }

                        List<string> reasonTokens;
                        if (nameTokenCount > 0)
                        {
                            name = string.Join(" ", tokens.Take(nameTokenCount));
                            reasonTokens = tokens.Skip(nameTokenCount).ToList();
                        }
                        else
                        {
                            // If no player/account found in DB, use all tokens as name
                            // This handles new/offline players not yet in cache
                            name = ConsumeName(tokens);
                            reasonTokens = tokens;
                            
                            // If ConsumeName only got the first word and there are more tokens, join them all
                            if (tokens.Count > 0 && !name.Contains(" "))
                            {
                                name = string.Join(" ", new[] { name }.Concat(tokens));
                                reasonTokens = new List<string>();
                            }
                        }

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Error: Missing name parameter", ChatMessageType.System));
                            return;
                        }

                        reason = string.Join(" ", reasonTokens).Trim().Trim('"', '\'');
                        if (string.IsNullOrWhiteSpace(reason))
                        {
                            reason = "No reason provided";
                        }
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Error: Missing name parameter", ChatMessageType.System));
                        return;
                    }

                    DateTime? expiryDate = null;
                    if (days > 0)
                    {
                        expiryDate = DateTime.UtcNow.AddDays(days);
                    }

                    if (type == "player")
                    {
                        TransferLogger.AddPlayerToBankBlacklist(name, reason, adminName, expiryDate);
                        var expiryText = expiryDate.HasValue ? $" (expires {expiryDate:yyyy-MM-dd HH:mm})" : " (permanent)";
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Added player '{name}' to bank command blacklist: {reason}{expiryText}", ChatMessageType.System));
                    }
                    else if (type == "account")
                    {
                        TransferLogger.AddAccountToBankBlacklist(name, reason, adminName, expiryDate);
                        var expiryText = expiryDate.HasValue ? $" (expires {expiryDate:yyyy-MM-dd HH:mm})" : " (permanent)";
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Added account '{name}' to bank command blacklist: {reason}{expiryText}", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Type must be 'player' or 'account'", ChatMessageType.System));
                    }
                    break;

                case "remove":
                    if (parameters.Length < 3)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /bankaudit bankban remove <player|account> <name>", ChatMessageType.System));
                        return;
                    }

                    var removeType = parameters[1].ToLower();
                    var removeName = string.Join(" ", parameters.Skip(2)).Trim().Trim('"', '\'');

                    if (removeType == "player")
                    {
                        TransferLogger.RemovePlayerFromBankBlacklist(removeName);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Removed player '{removeName}' from bank command blacklist", ChatMessageType.System));
                    }
                    else if (removeType == "account")
                    {
                        TransferLogger.RemoveAccountFromBankBlacklist(removeName);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Removed account '{removeName}' from bank command blacklist", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Type must be 'player' or 'account'", ChatMessageType.System));
                    }
                    break;

                case "list":
                    var blacklistedPlayers = TransferLogger.GetBankBlacklistedPlayers();
                    
                    if (blacklistedPlayers.Count == 0)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("No players are currently blacklisted from bank commands.", ChatMessageType.System));
                        return;
                    }

                    session.Network.EnqueueSend(new GameMessageSystemChat($"Bank Command Blacklist ({blacklistedPlayers.Count} entries):", ChatMessageType.System));
                    session.Network.EnqueueSend(new GameMessageSystemChat("=".PadRight(SeparatorLineLength, '='), ChatMessageType.System));

                    foreach (var entry in blacklistedPlayers)
                    {
                        var entryName = entry.PlayerName ?? entry.AccountName;
                        var entryType = entry.PlayerName != null ? "Player" : "Account";
                        var expiryText = entry.ExpiryDate.HasValue ? $" (expires {entry.ExpiryDate:yyyy-MM-dd HH:mm})" : " (permanent)";
                        var message = $"{entryType}: {entryName} - {entry.Reason} (added by {entry.AddedBy} on {entry.CreatedDate:yyyy-MM-dd HH:mm}){expiryText}";
                        session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.System));
                    }
                    break;

                default:
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown action: {action}", ChatMessageType.System));
                    session.Network.EnqueueSend(new GameMessageSystemChat("Available actions: add, remove, list", ChatMessageType.System));
                    break;
            }
        }

        // adminvision { on | off | toggle | check}
        [CommandHandler("adminvision", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1,
            "Allows the admin to see admin-only visible items.", "{ on | off | toggle | check }\n" +
            "Controls whether or not the admin can see admin-only visible items. Note that if you turn this feature off, you will need to log out and back in before the visible items become invisible.")]
        public static void HandleAdminvision(Session session, params string[] parameters)
        {
            // @adminvision { on | off | toggle | check}
            // Controls whether or not the admin can see admin-only visible items. Note that if you turn this feature off, you will need to log out and back in before the visible items become invisible.
            // @adminvision - Allows the admin to see admin - only visible items.

            switch (parameters?[0].ToLower())
            {
                case "1":
                case "on":
                    session.Player.HandleAdminvisionToggle(1);
                    break;
                case "0":
                case "off":
                    session.Player.HandleAdminvisionToggle(0);
                    break;
                case "toggle":
                    session.Player.HandleAdminvisionToggle(2);
                    break;
                case "check":
                default:
                    session.Player.HandleAdminvisionToggle(-1);
                    break;
            }
        }

        [CommandHandler("cleardynamic", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld)]
        public static void HandleClearDynamicEmotes(Session session, params string[] parameters)
        {
            QuestManager.ClearDynamicQuestEmotes();
        }

        // adminui
        [CommandHandler("adminui", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleAdminui(Session session, params string[] parameters)
        {
            // usage: @adminui
            // This command toggles whether the Admin UI is visible.

            // just a placeholder, probably not needed or should be handled by a decal plugin to replicate the admin ui
        }

        // delete
        [CommandHandler("delete", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0, "Deletes the selected object.", "Players may not be deleted this way.")]
        public static void HandleDeleteSelected(Session session, params string[] parameters)
        {
            // @delete - Deletes the selected object. Players may not be deleted this way.

            var objectId = ObjectGuid.Invalid;

            if (session.Player.HealthQueryTarget.HasValue)
                objectId = new ObjectGuid(session.Player.HealthQueryTarget.Value);
            else if (session.Player.ManaQueryTarget.HasValue)
                objectId = new ObjectGuid(session.Player.ManaQueryTarget.Value);
            else if (session.Player.CurrentAppraisalTarget.HasValue)
                objectId = new ObjectGuid(session.Player.CurrentAppraisalTarget.Value);

            if (objectId == ObjectGuid.Invalid)
                ChatPacket.SendServerMessage(session, "Delete failed. Please identify the object you wish to delete first.", ChatMessageType.Broadcast);

            if (objectId.IsPlayer())
            {
                ChatPacket.SendServerMessage(session, "Delete failed. Players cannot be deleted.", ChatMessageType.Broadcast);
                return;
            }

            var wo = session.Player.FindObject(objectId.Full, Player.SearchLocations.Everywhere, out _, out Container rootOwner, out bool wasEquipped);

            if (wo == null)
            {
                ChatPacket.SendServerMessage(session, "Delete failed. Object not found.", ChatMessageType.Broadcast);
                return;
            }

            if (parameters.Length == 1)
            {
                var objectType = parameters[0].ToLower();

                if (objectType != wo.GetType().Name.ToLower() && objectType != wo.WeenieType.ToString().ToLower())
                {
                    ChatPacket.SendServerMessage(session, $"Delete failed. Object type specified ({parameters[0]}) does not match object type ({wo.GetType().Name}) or weenie type ({wo.WeenieType.ToString()}) for 0x{wo.Guid}:{wo.Name}.", ChatMessageType.Broadcast);
                    return;
                }
            }

            wo.DeleteObject(rootOwner);
            session.Network.EnqueueSend(new GameMessageDeleteObject(wo));

            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has deleted 0x{wo.Guid}:{wo.Name}");
        }

        // Alias for delete
        [CommandHandler("del", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0, "Alias for delete - Deletes the selected object.", "Players may not be deleted this way.")]
        public static void HandleDeleteSelectedAlias(Session session, params string[] parameters)
        {
            HandleDeleteSelected(session, parameters);
        }

        // draw
        [CommandHandler("draw", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleDraw(Session session, params string[] parameters)
        {
            // @draw - Draws undrawable things.

            // TODO: output
        }

        // finger [ [-a] character] [-m account]
        [CommandHandler("finger", AccessLevel.Sentinel, CommandHandlerFlag.None, 1,
            "Show the given character's account name or vice-versa.",
            "[ [-a] character] [-m account]\n"
            + "Given a character name, this command displays the name of the owning account.\nIf the -m option is specified, the argument is considered an account name and the characters owned by that account are displayed.\nIf the -a option is specified, then the character name is fingered but their account is implicitly fingered as well.")]
        public static void HandleFinger(Session session, params string[] parameters)
        {
            // usage: @finger[ [-a] character] [-m account]
            // Given a character name, this command displays the name of the owning account.If the -m option is specified, the argument is considered an account name and the characters owned by that account are displayed.If the -a option is specified, then the character name is fingered but their account is implicitly fingered as well.
            // @finger - Show the given character's account name or vice-versa.

            var lookupCharAndAccount = parameters.Contains("-a");
            var lookupByAccount = parameters.Contains("-m");

            var charName = "";
            if (lookupByAccount || lookupCharAndAccount)
                charName = string.Join(" ", parameters.Skip(1));
            else
                charName = string.Join(" ", parameters);

            var message = "";
            if (!lookupByAccount && !lookupCharAndAccount)
            {
                var character = PlayerManager.FindByName(charName);

                if (character != null)
                {
                    if (character.Account != null)
                        message = $"Login name: {character.Account.AccountName}      Character: {character.Name}\n";
                    else
                        message = $"Login name: account not found, character is orphaned.      Character: {character.Name}\n";
                }
                else
                    message = $"There was no active character named \"{charName}\" found in the database.\n";
            }
            else
            {
                Account account;
                if (lookupCharAndAccount)
                {
                    var character = PlayerManager.FindByName(charName);

                    if (character == null)
                    {
                        message = $"There was no active character named \"{charName}\" found in the database.\n";
                        CommandHandlerHelper.WriteOutputInfo(session, message, ChatMessageType.WorldBroadcast);
                        return;
                    }
                    account = character.Account;
                    if (account == null)
                    {
                        message = $"Login name: account not found, character is orphaned.      Character: {character.Name}\n";
                        CommandHandlerHelper.WriteOutputInfo(session, message, ChatMessageType.WorldBroadcast);
                        return;
                    }
                    else // get updated data from db.
                        account = DatabaseManager.Authentication.GetAccountById(account.AccountId);
                }
                else
                    account = DatabaseManager.Authentication.GetAccountByName(charName);

                if (account != null)
                {
                    if (account.BannedTime != null)
                    {
                        var bannedbyAccount = account.BannedByAccountId > 0 ? $"account {DatabaseManager.Authentication.GetAccountById(account.BannedByAccountId.Value).AccountName}" : "CONSOLE";

                        message = $"Account '{account.AccountName}' was banned by {bannedbyAccount} until server time {account.BanExpireTime.Value.ToLocalTime():MMM dd yyyy  h:mmtt}.\n";
                    }
                    else
                        message = $"Account '{account.AccountName}' is not banned.\n";
                    if (account.AccessLevel > (int)AccessLevel.Player)
                        message += $"Account '{account.AccountName}' has been granted AccessLevel.{((AccessLevel)account.AccessLevel).ToString()} rights.\n";
                    message += $"Account created on {account.CreateTime.ToLocalTime()} by IP: {(account.CreateIP != null ? new IPAddress(account.CreateIP).ToString() : "N/A")} \n";
                    message += $"Account last logged on at {(account.LastLoginTime.HasValue ? account.LastLoginTime.Value.ToLocalTime().ToString() : "N/A")} by IP: {(account.LastLoginIP != null ? new IPAddress(account.LastLoginIP).ToString() : "N/A")}\n";
                    message += $"Account total times logged on {account.TotalTimesLoggedIn}\n";
                    var characters = DatabaseManager.Shard.BaseDatabase.GetCharacters(account.AccountId, true);
                    message += $"{characters.Count} Character(s) owned by: {account.AccountName}\n";
                    message += "-------------------\n";
                    foreach (var character in characters.Where(x => !x.IsDeleted && x.DeleteTime == 0))
                        message += $"\"{(character.IsPlussed ? "+" : "")}{character.Name}\", ID 0x{character.Id.ToString("X8")}\n";
                    var pendingDeletedCharacters = characters.Where(x => !x.IsDeleted && x.DeleteTime > 0).ToList();
                    if (pendingDeletedCharacters.Count > 0)
                    {
                        message += "-------------------\n";
                        foreach (var character in pendingDeletedCharacters)
                            message += $"\"{(character.IsPlussed ? "+" : "")}{character.Name}\", ID 0x{character.Id.ToString("X8")} -- Will be deleted at server time {new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(character.DeleteTime).ToLocalTime().ToString("MMM d yyyy h:mm tt")}\n";
                    }
                    message += "-------------------\n";
                    var deletedCharacters = characters.Where(x => x.IsDeleted).ToList();
                    if (deletedCharacters.Count > 0)
                    {
                        foreach (var character in deletedCharacters)
                            message += $"\"{(character.IsPlussed ? "+" : "")}{character.Name}\", ID 0x{character.Id.ToString("X8")} -- Deleted at server time {new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(character.DeleteTime).ToLocalTime().ToString("MMM d yyyy h:mm tt")}\n";
                    }
                    else
                        message += "No deleted characters.\n";
                }
                else
                    message = $"There was no account named \"{charName}\" found in the database.\n";
            }

            CommandHandlerHelper.WriteOutputInfo(session, message, ChatMessageType.WorldBroadcast);
        }

        // freeze
        [CommandHandler("freeze", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleFreeze(Session session, params string[] parameters)
        {
            // @freeze - Freezes the selected target for 10 minutes or until unfrozen.

            // TODO: output
        }

        // unfreeze
        [CommandHandler("unfreeze", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleUnFreeze(Session session, params string[] parameters)
        {
            // @unfreeze - Unfreezes the selected target.

            // TODO: output
        }

        // gag < char name >
        [CommandHandler("gag", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1,
            "Prevents a character from talking.",
            "< char name >\nThe character will not be able to @tell or use chat normally.")]
        public static void HandleGag(Session session, params string[] parameters)
        {
            // usage: @gag < char name >
            // This command gags the specified character for five minutes.  The character will not be able to @tell or use chat normally.
            // @gag - Prevents a character from talking.
            // @ungag -Allows a gagged character to talk again.

            if (parameters.Length > 0)
            {
                var playerName = string.Join(" ", parameters);

                var msg = "";
                if (PlayerManager.GagPlayer(session.Player, playerName))
                {
                    msg = $"{playerName} has been gagged for five minutes.";
                }
                else
                {
                    msg = $"Unable to gag a character named {playerName}, check the name and re-try the command.";
                }

                CommandHandlerHelper.WriteOutputInfo(session, msg, ChatMessageType.WorldBroadcast);
            }
        }

        // ungag < char name >
        [CommandHandler("ungag", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1,
            "Allows a gagged character to talk again.",
            "< char name >\nThe character will again be able to @tell and use chat normally.")]
        public static void HandleUnGag(Session session, params string[] parameters)
        {
            // usage: @ungag < char name >
            // @ungag -Allows a gagged character to talk again.

            if (parameters.Length > 0)
            {
                var playerName = string.Join(" ", parameters);

                var msg = "";
                if (PlayerManager.UnGagPlayer(session.Player, playerName))
                {
                    msg = $"{playerName} has been ungagged.";
                }
                else
                {
                    msg = $"Unable to ungag a character named {playerName}, check the name and re-try the command.";
                }

                CommandHandlerHelper.WriteOutputInfo(session, msg, ChatMessageType.WorldBroadcast);
            }
        }

        /// <summary>
        /// Teleports an admin to their sanctuary position. If a single uint value from 1 to 9 is provided as a parameter then the admin is teleported to the cooresponding named recall point.
        /// </summary>
        /// <param name="parameters">A single uint value from 0 to 9. Value 0 recalls to Sanctuary, values 1 through 9 teleports too the corresponding saved recall point.</param>
        [CommandHandler("home", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0,
            "Teleports you to your sanctuary position.",
            "< recall number > - Recalls to a saved position, valid values are 1 - 9.\n" +
            "NOTE: Calling @home without a number recalls your sanctuary position; calling it with a number will teleport you to the corresponding saved position.")]
        public static void HandleHome(Session session, params string[] parameters)
        {
            // @home has the alias @recall
            string parsePositionString = "0";

            // Limit the incoming parameter to 1 character
            if (parameters?.Length >= 1)
                parsePositionString = parameters[0].Length > 1 ? parameters[0].Substring(0, 1) : parameters[0];

            // Attempt to parse the integer
            if (uint.TryParse(parsePositionString, out var parsedPositionInt))
            {
                // parsedPositionInt value should be limited too a value from, 0-9
                // Create a new position from the current player location
                PositionType positionType = new PositionType();
                // Transform too the correct PositionType, based on the "Saved Positions" subset:
                switch (parsedPositionInt)
                {
                    case 0:
                        {
                            positionType = PositionType.Sanctuary;
                            break;
                        }
                    case 1:
                        {
                            positionType = PositionType.Save1;
                            break;
                        }
                    case 2:
                        {
                            positionType = PositionType.Save2;
                            break;
                        }
                    case 3:
                        {
                            positionType = PositionType.Save3;
                            break;
                        }
                    case 4:
                        {
                            positionType = PositionType.Save4;
                            break;
                        }
                    case 5:
                        {
                            positionType = PositionType.Save5;
                            break;
                        }
                    case 6:
                        {
                            positionType = PositionType.Save6;
                            break;
                        }
                    case 7:
                        {
                            positionType = PositionType.Save7;
                            break;
                        }
                    case 8:
                        {
                            positionType = PositionType.Save8;
                            break;
                        }
                    case 9:
                        {
                            positionType = PositionType.Save9;
                            break;
                        }
                }

                // If we have the position, teleport the player
                var position = session.Player.GetPosition(positionType);
                if (position != null)
                {
                    session.Player.TeleToPosition(positionType);
                    var positionMessage = new GameMessageSystemChat($"Recalling to {positionType}", ChatMessageType.Broadcast);
                    session.Network.EnqueueSend(positionMessage);
                    return;
                }
            }
            // Invalid character was receieved in the input (it was not 0-9)
            var homeErrorMessage = new GameMessageSystemChat("Could not find a valid recall position.", ChatMessageType.Broadcast);
            session.Network.EnqueueSend(homeErrorMessage);
        }

        // mrt
        [CommandHandler("mrt", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0,
            "Toggles the ability to bypass housing boundaries",
            "")]
        public static void HandleMRT(Session session, params string[] parameters)
        {
            // @mrt - Toggles the ability to bypass housing boundaries.
            session.Player.HandleMRT();
        }

        // limbo [on / off]
        [CommandHandler("limbo", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleLimbo(Session session, params string[] parameters)
        {
            // @limbo[on / off] - Puts the targeted player in 'limbo' which means that the player cannot damage anything or be damaged by anything.The player will not recieve direct tells, or channel messages, such as fellowship messages and allegiance chat.  The player will be unable to salvage.This status times out after 15 minutes, use '@limbo on' again on the player to reset the timer. You and the player will be notifed when limbo wears off.If neither on or off are specified, on is assumed.
            // @limbo - Puts the selected target in limbo.

            // TODO: output
        }

        // myiid
        [CommandHandler("myiid", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0,
            "Displays your Instance ID (IID)")]
        public static void HandleMyIID(Session session, params string[] parameters)
        {
            // @myiid - Displays your Instance ID(IID).

            session.Network.EnqueueSend(new GameMessageSystemChat($"GUID: {session.Player.Guid.Full}  - Low: {session.Player.Guid.Low} - High: {session.Player.Guid.High} - (0x{session.Player.Guid.Full:X})", ChatMessageType.Broadcast));
        }

        // myserver
        [CommandHandler("myserver", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleMyServer(Session session, params string[] parameters)
        {
            // @myserver - Displays the number of the game server on which you are currently located.

            // TODO: output
        }

        // pk
        [CommandHandler("pk", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "sets your own PK state.",
            "< npk / pk / pkl / free >\n" +
            "This command sets your current player killer state\n" +
            "This command also expects to be used '@cloak player' to properly reflect to other players\n" +
            "< npk > You can only attack monsters.\n" +
            "< pk > You can attack player killers and monsters.\n" +
            "< pkl > You can attack player killer lites and monsters\n" +
            "< free > You can attack players, player killers, player killer lites, monsters, and npcs")]
        public static void HandlePk(Session session, params string[] parameters)
        {
            // @pk - Toggles or sets your own PK state.

            if (parameters.Length == 0)
            {
                //player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PlayerKillerStatus, (int)player.PlayerKillerStatus));
                var message = $"Your current PK state is: {session.Player.PlayerKillerStatus.ToString()}\n"
                    + "You can change it to the following:\n"
                    + "NPK      = Non-Player Killer\n"
                    + "PK       = Player Killer\n"
                    + "PKL      = Player Killer Lite\n"
                    + "Free     = Can kill anything\n";
                CommandHandlerHelper.WriteOutputInfo(session, message, ChatMessageType.Broadcast);
            }
            else
            {
                switch (parameters[0].ToLower())
                {
                    case "npk":
                        session.Player.PlayerKillerStatus = PlayerKillerStatus.NPK;
                        session.Player.PkLevel = PKLevel.NPK;
                        break;
                    case "pk":
                        session.Player.PlayerKillerStatus = PlayerKillerStatus.PK;
                        session.Player.PkLevel = PKLevel.PK;
                        break;
                    case "pkl":
                        session.Player.PlayerKillerStatus = PlayerKillerStatus.PKLite;
                        session.Player.PkLevel = PKLevel.NPK;
                        break;
                    case "free":
                        session.Player.PlayerKillerStatus = PlayerKillerStatus.Free;
                        session.Player.PkLevel = PKLevel.Free;
                        break;
                }
                session.Player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(session.Player, PropertyInt.PlayerKillerStatus, (int)session.Player.PlayerKillerStatus));
                CommandHandlerHelper.WriteOutputInfo(session, $"Your current PK state is now set to: {session.Player.PlayerKillerStatus.ToString()}", ChatMessageType.Broadcast);

                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} changed their PK state to {session.Player.PlayerKillerStatus.ToString()}.");
            }
        }

        // querypluginlist
        [CommandHandler("querypluginlist", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleQuerypluginlist(Session session, params string[] parameters)
        {
            // @querypluginlist (-fresh) - View list of plug-ins the selected character is running. If you do not use the -fresh paramater, you will get results that where cached at login. That way, you will not be sending an alert to the player that you are querying thier plugin list.  If you use -fresh, then you will get fresh data from the player's client and they will recieve notification that you have asked for thier plugin list.NOTE: Results are dependent upon 3rd party authors providing correct information.
            // @querypluginlist - View list of plug - ins the selected character is running.
            // @querypluginlist<pluginname> - View information about a specific plugin.NOTE: Results are dependent upon 3rd party authors providing correct information.
            // @queryplugin < pluginname > -View information about a specific plugin.

            // TODO: output
        }

        // queryplugin
        [CommandHandler("queryplugin", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 1)]
        public static void HandleQueryplugin(Session session, params string[] parameters)
        {
            // @queryplugin < pluginname > -View information about a specific plugin.

            // TODO: output
        }

        // repeat < Num > < Command >
        [CommandHandler("repeat", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 2)]
        public static void HandleRepeat(Session session, params string[] parameters)
        {
            // @repeat < Num > < Command > -Repeat a command a number of times.
            // EX: "@repeat 5 @say Hi" - say Hi 5 times.
            // @repeat < Num > < Command > -Repeat a command a number of times.

            // TODO: output
        }

        // regen
        [CommandHandler("regen", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0,
            "Sends the selected generator a regeneration message.",
            "")]
        public static void HandleRegen(Session session, params string[] parameters)
        {
            // @regen - Sends the selected generator a regeneration message.

            var objectId = new ObjectGuid();

            if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
            {
                if (session.Player.HealthQueryTarget.HasValue)
                    objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
                else if (session.Player.ManaQueryTarget.HasValue)
                    objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
                else
                    objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

                var wo = session.Player.CurrentLandblock?.GetObject(objectId);

                if (objectId.IsPlayer())
                    return;

                if (wo != null & wo.IsGenerator)
                {
                    wo.ResetGenerator();
                    wo.GeneratorEnteredWorld = false;
                    wo.GeneratorRegeneration(Time.GetUnixTime());
                }
            }
        }

        /// <summary>
        /// Command for saving the Admin's current location as the sanctuary position. If a uint between 1-9 is provided as a parameter, the corresponding named recall is saved.
        /// </summary>
        /// <param name="parameters">A single uint value from 0 to 9. Value 0 saves the Sanctuary recall (default), values 1 through 9 save the corresponding named recall point.</param>
        [CommandHandler("save", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0,
            "Sets your sanctuary position or a named recall point.",
            "< recall number > - Saves your position into the numbered recall, valid values are 1 - 9.\n" +
            "NOTE: Calling @save without a number saves your sanctuary (Lifestone Recall) position.")]
        public static void HandleSave(Session session, params string[] parameters)
        {
            // Set the default of 0 to save the sanctuary portal if no parameter is passed.
            string parsePositionString = "0";

            // Limit the incoming parameter to 1 character
            if (parameters?.Length >= 1)
                parsePositionString = parameters[0].Length > 1 ? parameters[0].Substring(0, 1) : parameters[0];

            // Attempt to parse the integer
            if (uint.TryParse(parsePositionString, out var parsedPositionInt))
            {
                // parsedPositionInt value should be limited too a value from, 0-9
                // Create a new position from the current player location
                Position playerPosition = session.Player.Location;
                PositionType positionType = PositionType.Sanctuary;
                // Set the correct PositionType, based on the "Saved Positions" position type subset:
                switch (parsedPositionInt)
                {
                    case 0:
                        {
                            positionType = PositionType.Sanctuary;
                            break;
                        }
                    case 1:
                        {
                            positionType = PositionType.Save1;
                            break;
                        }
                    case 2:
                        {
                            positionType = PositionType.Save2;
                            break;
                        }
                    case 3:
                        {
                            positionType = PositionType.Save3;
                            break;
                        }
                    case 4:
                        {
                            positionType = PositionType.Save4;
                            break;
                        }
                    case 5:
                        {
                            positionType = PositionType.Save5;
                            break;
                        }
                    case 6:
                        {
                            positionType = PositionType.Save6;
                            break;
                        }
                    case 7:
                        {
                            positionType = PositionType.Save7;
                            break;
                        }
                    case 8:
                        {
                            positionType = PositionType.Save8;
                            break;
                        }
                    case 9:
                        {
                            positionType = PositionType.Save9;
                            break;
                        }
                }

                // Save the position
                session.Player.SetPosition(positionType, new Position(playerPosition));
                // Report changes to client
                var positionMessage = new GameMessageSystemChat($"Set: {positionType} to Loc: {playerPosition}", ChatMessageType.Broadcast);
                session.Network.EnqueueSend(positionMessage);
                return;
            }
            // Error parsing the text input, from parameter[0]
            var positionErrorMessage = new GameMessageSystemChat("Could not determine the correct PositionType. Please use an integer value from 1 to 9; or omit the parmeter entirely.", ChatMessageType.Broadcast);
            session.Network.EnqueueSend(positionErrorMessage);
        }

        // serverlist
        [CommandHandler("serverlist", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleServerlist(Session session, params string[] parameters)
        {
            // The @serverlist command shows a list of the servers in the current server farm. The format is as follows:
            // -The server ID
            // -The server's speed relative to the master server
            // - Number of users reported by ObjLoc and LoadBalancing
            // - Current total load reported by LoadBalancing
            // - Total load from blocks with no players in them
            // - Blocks with players / blocks loaded / blocks owned
            // - The owned block range from low to high(in hex)
            // - The external IP address to talk to clients on
            // -If the server is your current server or the master
            // @serverlist - Shows a list of the logical servers that control this world.

            // TODO: output
        }

        // snoop [start / stop] [Character Name]
        [CommandHandler("snoop", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 2)]
        public static void HandleSnoop(Session session, params string[] parameters)
        {
            // @snoop[start / stop][Character Name]
            // - If no character name is supplied, the currently selected character will be used.If neither start nor stop is specified, start will be assumed.
            // @snoop - Listen in on a player's private communication.

            // TODO: output
        }

        [CommandHandler("smite", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0, "Kills the selected target or all monsters in radar range if \"all\" is specified.", "[all, Player's Name]")]
        public static void HandleSmite(Session session, params string[] parameters)
        {
            // @smite [all] - Kills the selected target or all monsters in radar range if "all" is specified.

            if (parameters?.Length > 0)
            {
                if (parameters[0] == "all")
                {
                    foreach (var obj in session.Player.PhysicsObj.ObjMaint.GetVisibleObjectsValues(session.Player.Location.Variation))
                    {
                        var wo = obj.WeenieObj.WorldObject;

                        if (wo is Player) // I don't recall if @smite all would kill players in range, assuming it didn't
                            continue;

                        var useTakeDamage = PropertyManager.GetBool("smite_uses_takedamage").Item;

                        if (wo is Creature creature && creature.Attackable)
                            creature.Smite(session.Player, useTakeDamage);
                    }

                    PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} used smite all.");
                }
                else
                {
                    string characterName = "";

                    // if parameters are greater then 1, we may have a space in a character name
                    if (parameters.Length > 1)
                    {
                        foreach (string name in parameters)
                        {
                            // adds a space back inbetween each parameter
                            if (characterName.Length > 0)
                                characterName += " " + name;
                            else
                                characterName = name;
                        }
                    }
                    // if there are no spaces, just set the characterName to the first paramter
                    else
                        characterName = parameters[0];

                    // look up session
                    var player = PlayerManager.GetOnlinePlayer(characterName);

                    // playerSession will be null when the character is not found
                    if (player != null)
                    {
                        player.Smite(session.Player, PropertyManager.GetBool("smite_uses_takedamage").Item);

                        PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} used smite on {player.Name}");
                        return;
                    }

                    ChatPacket.SendServerMessage(session, "Select a target and use @smite, or use @smite all to kill all creatures in radar range or @smite [player's name].", ChatMessageType.Broadcast);
                }
            }
            else
            {
                if (session.Player.HealthQueryTarget.HasValue) // Only Creatures will trigger this.. Excludes vendors automatically as a result (Can change design to mimic @delete command)
                {
                    var objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);

                    var wo = session.Player.CurrentLandblock?.GetObject(objectId) as Creature;

                    if (objectId == session.Player.Guid) // don't kill yourself
                        return;

                    if (wo != null)
                    {
                        wo.Smite(session.Player, PropertyManager.GetBool("smite_uses_takedamage").Item);

                        PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} used smite on {wo.Name} (0x{wo.Guid:X8})");
                    }
                }
                else
                {
                    ChatPacket.SendServerMessage(session, "Select a target and use @smite, or use @smite all to kill all creatures in radar range or @smite [players' name].", ChatMessageType.Broadcast);
                }
            }
        }

        // teleto [char]
        [CommandHandler("teleto", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1,
            "Teleport yourself to a player",
            "[Player's Name]\n")]
        public static void HandleTeleto(Session session, params string[] parameters)
        {
            // @teleto - Teleports you to the specified character.
            var playerName = string.Join(" ", parameters);
            // Lookup the player in the world
            var player = PlayerManager.GetOnlinePlayer(playerName);
            // If the player is found, teleport the admin to the Player's location
            if (player != null)
                session.Player.Teleport(player.Location);
            else
                session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName} was not found.", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Teleports a player to your current location
        /// </summary>
        [CommandHandler("teletome", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1, "Teleports a player to your current location.", "PlayerName")]
        public static void HandleTeleToMe(Session session, params string[] parameters)
        {
            var playerName = string.Join(" ", parameters);
            var player = PlayerManager.GetOnlinePlayer(playerName);
            if (player == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName} was not found.", ChatMessageType.Broadcast));
                return;
            }
            var currentPos = new Position(player.Location);
            player.Teleport(session.Player.Location);
            player.SetPosition(PositionType.TeleportedCharacter, currentPos);
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{session.Player.Name} has teleported you.", ChatMessageType.Magic));

            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has teleported {player.Name} to them.");
        }

        /// <summary>
        /// Teleports a player to their previous position
        /// </summary>
        [CommandHandler("telereturn", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1, "Return a player to their previous location.", "PlayerName")]
        public static void HandleTeleReturn(Session session, params string[] parameters)
        {
            var playerName = string.Join(" ", parameters);
            var player = PlayerManager.GetOnlinePlayer(playerName);
            if (player == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName} was not found.", ChatMessageType.Broadcast));
                return;
            }

            if (player.TeleportedCharacter == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName} does not have a return position saved.", ChatMessageType.Broadcast));
                return;
            }

            player.Teleport(new Position(player.TeleportedCharacter));
            player.SetPosition(PositionType.TeleportedCharacter, null);
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{session.Player.Name} has returned you to your previous location.", ChatMessageType.Magic));

            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has returned {player.Name} to their previous location.");
        }

        // teleallto [char]
        [CommandHandler("teleallto", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Teleports all players to a player. If no target is specified, all players will be teleported to you.", "[Player's Name]\n")]
        public static void HandleTeleAllTo(Session session, params string[] parameters)
        {
            Player destinationPlayer = null;

            if (parameters.Length > 0)
                destinationPlayer = PlayerManager.GetOnlinePlayer(parameters[0]);

            if (destinationPlayer == null)
                destinationPlayer = session.Player;

            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player == destinationPlayer)
                    continue;

                player.SetPosition(PositionType.TeleportedCharacter, new Position(player.Location));

                player.Teleport(new Position(destinationPlayer.Location));
            }

            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has teleported all online players to their location.");
        }

        // telepoi location
        [CommandHandler("telepoi", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Teleport yourself to a named Point of Interest",
            "[POI|list]\n" +
            "@telepoi Arwic\n" +
            "Get the list of POIs\n" +
            "@telepoi list")]
        public static void HandleTeleportPoi(Session session, params string[] parameters)
        {
            var poi = String.Join(" ", parameters);

            if (poi.ToLower() == "list")
            {
                DatabaseManager.World.CacheAllPointsOfInterest();
                var pois = DatabaseManager.World.GetPointsOfInterestCache();
                var list = pois
                    .Select(k => k.Key)
                    .OrderBy(k => k)
                    .DefaultIfEmpty()
                    .Aggregate((a, b) => a + ", " + b);
                session.Network.EnqueueSend(new GameMessageSystemChat($"All POIs: {list}", ChatMessageType.Broadcast));
            }
            else
            {
                var teleportPOI = DatabaseManager.World.GetCachedPointOfInterest(poi);
                if (teleportPOI == null)
                    return;
                var weenie = DatabaseManager.World.GetCachedWeenie(teleportPOI.WeenieClassId);
                var portalDest = new Position(weenie.GetPosition(PositionType.Destination));
                portalDest.Variation = null;
                WorldObject.AdjustDungeon(portalDest);
                session.Player.Teleport(portalDest);
            }
        }

        // teleloc cell x y z [qx qy qz qw]
        [CommandHandler("teleloc", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 4,
            "Teleport yourself to the specified location.",
            "cell [x y z] (qw qx qy qz)\n" +
            "@teleloc follows the same number order as displayed from @loc output\n" +
            "Example: @teleloc 0x7F0401AD [12.319900 -28.482000 0.005000] -0.338946 0.000000 0.000000 -0.940806\n" +
            "Example: @teleloc 0x7F0401AD 12.319900 -28.482000 0.005000 -0.338946 0.000000 0.000000 -0.940806\n" +
            "Example: @teleloc 7F0401AD 12.319900 -28.482000 0.005000")]
        public static void HandleTeleportLOC(Session session, params string[] parameters)
        {
            try
            {
                uint cell;

                if (parameters[0].StartsWith("0x"))
                {
                    string strippedcell = parameters[0].Substring(2);
                    cell = (uint)int.Parse(strippedcell, System.Globalization.NumberStyles.HexNumber);
                }
                else
                    cell = (uint)int.Parse(parameters[0], System.Globalization.NumberStyles.HexNumber);

                var positionData = new float[7];
                for (uint i = 0u; i < 7u; i++)
                {
                    if (i > 2 && parameters.Length < 8)
                    {
                        positionData[3] = 1;
                        positionData[4] = 0;
                        positionData[5] = 0;
                        positionData[6] = 0;
                        break;
                    }

                    if (!float.TryParse(parameters[i + 1].Trim(new Char[] { ' ', '[', ']' }), out var position))
                        return;

                    positionData[i] = position;
                }

                session.Player.Teleport(new Position(cell, positionData[0], positionData[1], positionData[2], positionData[4], positionData[5], positionData[6], positionData[3]));
            }
            catch (Exception)
            {
                ChatPacket.SendServerMessage(session, "Invalid arguments for @teleloc", ChatMessageType.Broadcast);
                ChatPacket.SendServerMessage(session, "Hint: @teleloc follows the same number order as displayed from @loc output", ChatMessageType.Broadcast);
                ChatPacket.SendServerMessage(session, "Usage: @teleloc cell [x y z] (qw qx qy qz)", ChatMessageType.Broadcast);
                ChatPacket.SendServerMessage(session, "Example: @teleloc 0x7F0401AD [12.319900 -28.482000 0.005000] -0.338946 0.000000 0.000000 -0.940806", ChatMessageType.Broadcast);
                ChatPacket.SendServerMessage(session, "Example: @teleloc 0x7F0401AD 12.319900 -28.482000 0.005000 -0.338946 0.000000 0.000000 -0.940806", ChatMessageType.Broadcast);
                ChatPacket.SendServerMessage(session, "Example: @teleloc 7F0401AD 12.319900 -28.482000 0.005000", ChatMessageType.Broadcast);
            }
        }

        // time
        [CommandHandler("time", AccessLevel.Envoy, CommandHandlerFlag.None, 0,
            "Displays the server's current game time.")]
        public static void HandleTime(Session session, params string[] parameters)
        {
            // @time - Displays the server's current game time.

            var messageUTC = "The current server time in UtcNow is: " + DateTime.UtcNow;
            //var messagePY = "The current server time translated to DerethDateTime is:\n" + Timers.CurrentLoreTime;
            var messageIGPY = "The current server time shown in game client is:\n" + Timers.CurrentInGameTime;
            var messageTOD = $"It is currently {Timers.CurrentInGameTime.TimeOfDay} in game right now.";

            CommandHandlerHelper.WriteOutputInfo(session, messageUTC, ChatMessageType.WorldBroadcast);
            //CommandHandlerHelper.WriteOutputInfo(session, messagePY, ChatMessageType.WorldBroadcast);
            CommandHandlerHelper.WriteOutputInfo(session, messageIGPY, ChatMessageType.WorldBroadcast);
            CommandHandlerHelper.WriteOutputInfo(session, messageTOD, ChatMessageType.WorldBroadcast);
        }

        // trophies
        [CommandHandler("trophies", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0,
            "Shows a list of the trophies dropped by the target creature, and the percentage chance of dropping.",
            "")]
        public static void HandleTrophies(Session session, params string[] parameters)
        {
            // @trophies - Shows a list of the trophies dropped by the target creature, and the percentage chance of dropping.

            var objectId = new ObjectGuid();

            if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
            {
                if (session.Player.HealthQueryTarget.HasValue)
                    objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
                else if (session.Player.ManaQueryTarget.HasValue)
                    objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
                else
                    objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

                var wo = session.Player.CurrentLandblock?.GetObject(objectId);

                if (objectId.IsPlayer())
                    return;

                var msg = "";
                if (wo is Creature creature && wo.Biota.PropertiesCreateList != null && wo.Biota.PropertiesCreateList.Count > 0)
                {
                    var createList = creature.Biota.PropertiesCreateList.Where(i => (i.DestinationType & DestinationType.Contain) != 0 ||
                        (i.DestinationType & DestinationType.Treasure) != 0 && (i.DestinationType & DestinationType.Wield) == 0).ToList();

                    var wieldedTreasure = creature.Inventory.Values.Concat(creature.EquippedObjects.Values).Where(i => i.DestinationType.HasFlag(DestinationType.Treasure)).ToList();

                    msg = $"Trophy Dump for {creature.Name} (0x{creature.Guid})\n";
                    msg += $"WCID: {creature.WeenieClassId}\n";
                    msg += $"WeenieClassName: {creature.WeenieClassName}\n";

                    if (createList.Count > 0)
                    {
                        foreach (var item in createList)
                        {
                            if (item.WeenieClassId == 0)
                            {
                                msg += $"{((DestinationType)item.DestinationType).ToString()}: {item.Shade,7:P2} - {item.WeenieClassId,5} - Nothing\n";
                                continue;
                            }

                            var weenie = DatabaseManager.World.GetCachedWeenie(item.WeenieClassId);
                            msg += $"{((DestinationType)item.DestinationType).ToString()}: {item.Shade,7:P2} - {item.WeenieClassId,5} - {(weenie != null ? weenie.ClassName : "Item not found in DB")} - {(weenie != null ? weenie.GetProperty(PropertyString.Name) : "Item not found in DB")}\n";
                        }
                    }
                    else
                        msg += "Creature has no trophies to drop.\n";

                    if (wieldedTreasure.Count > 0)
                    {
                        foreach (var item in wieldedTreasure)
                        {
                            msg += $"{item.DestinationType.ToString()}: 100.00% - {item.WeenieClassId,5} - {item.WeenieClassName} - {item.Name}\n";
                        }
                    }
                    else
                        msg += "Creature has no wielded items to drop.\n";
                }
                else
                    msg = $"{wo.Name} (0x{wo.Guid}) has no trophies.";

                session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.System));
            }
        }

        // unlock {-all | IID}
        [CommandHandler("unlock", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 1)]
        public static void HandleUnlock(Session session, params string[] parameters)
        {
            // usage: @unlock {-all | IID}
            // Cleans the SQL lock on either everyone or the given player.
            // @unlock - Cleans the SQL lock on either everyone or the given player.

            // TODO: output
        }

        // gamecast <message>
        [CommandHandler("gamecast", AccessLevel.Envoy, CommandHandlerFlag.None, 1,
            "Sends a world-wide broadcast.",
            "<message>\n" +
            "This command sends a world-wide broadcast to everyone in the game. Text is prefixed with 'Broadcast from (admin-name)> '.\n" +
            "See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.")]
        public static void HandleGamecast(Session session, params string[] parameters)
        {
            // > Broadcast from usage: @gamecast<message>
            // This command sends a world-wide broadcast to everyone in the game. Text is prefixed with 'Broadcast from (admin-name)> '.
            // See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.
            // @gamecast - Sends a world-wide broadcast.

            //session.Player.HandleActionWorldBroadcast($"Broadcast from {session.Player.Name}> {string.Join(" ", parameters)}", ChatMessageType.WorldBroadcast);

            var msg = $"Broadcast from {(session != null ? session.Player.Name : "System")}> {string.Join(" ", parameters)}";
            GameMessageSystemChat sysMessage = new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast);
            DiscordChatManager.SendDiscordMessage("", msg, ConfigManager.Config.Chat.GeneralChannelId);
            DiscordChatManager.SendDiscordMessage("", msg, ConfigManager.Config.Chat.EventsChannelId);
            PlayerManager.BroadcastToAll(sysMessage);
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, session?.Player, msg);
        }

        // add <spell>
        [CommandHandler("addspell", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Adds the specified spell to your own spellbook.", "<spellid>")]
        public static void HandleAddSpell(Session session, params string[] parameters)
        {
            if (Enum.TryParse(parameters[0], true, out SpellId spellId))
            {
                if (Enum.IsDefined(typeof(SpellId), spellId))
                    session.Player.LearnSpellWithNetworking((uint)spellId);
            }
        }

        [CommandHandler("removespell", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Removes the specified spell to your own spellbook.", "<spellid>")]
        public static void HandleRemoveSpell(Session session, params string[] parameters)
        {
            if (!Enum.TryParse(parameters[0], true, out SpellId spellId))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown spell {parameters[0]}", ChatMessageType.Broadcast));
                return;
            }
            if (session.Player.RemoveKnownSpell((uint)spellId))
            {
                var spell = new Entity.Spell(spellId, false);
                session.Network.EnqueueSend(new GameMessageSystemChat($"{spell.Name} removed from spellbook.", ChatMessageType.Broadcast));
            }
            else
                session.Network.EnqueueSend(new GameMessageSystemChat($"You don't know that spell!", ChatMessageType.Broadcast));
        }

        // adminhouse
        [CommandHandler("adminhouse", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "House management tools for admins.")]
        public static void HandleAdminhouse(Session session, params string[] parameters)
        {
            // @adminhouse dump: dumps info about currently selected house or house owned by currently selected player.
            // @adminhouse dump name<name>: dumps info about house owned by the account of the named player.
            // @adminhouse dump account<account_name>: dumps info about house owned by named account.
            // @adminhouse dump hid<houseID>: dumps info about specified house.
            // @adminhouse dump_all: dumps one line about each house in the world.
            // @adminhouse dump_all summary: dumps info about total houses owned for each house type.
            // @adminhouse dump_all dangerous: dumps full info about all houses.Use with caution.
            // @adminhouse rent pay: fully pay the rent of the selected house.
            // @adminhouse rent warn: rent timestamp is pushed far enough back in time to cause the rent to be almost due for the selected house.
            // @adminhouse rent due: rent timestamp is pushed back to cause the rent to be due for the selected house.
            // @adminhouse rent overdue: sets the rent timestamp far enough back to cause the selected house's rent to be overdue.
            // @adminhouse rent payall: fully pay the rent for all houses.
            // @adminhouse payrent on / off: sets the targeted house to not require / require normal maintenance payments.
            // @adminhouse - House management tools for admins.

            if (parameters.Length >= 1 && parameters[0] == "dump")
            {
                if (parameters.Length == 1)
                {
                    if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
                    {
                        var house = GetSelectedHouse(session, out var wo);

                        if (house == null)
                            return;

                        DumpHouse(session, house, wo);
                    }
                    else
                    {
                        session.Player.SendMessage("No object is selected.");
                        return;
                    }
                }
                else if (parameters.Length > 1 && parameters[1] == "name")
                {
                    var playerName = "";
                    for (var i = 2; i < parameters.Length; i++)
                        playerName += $"{parameters[i]} ";
                    playerName = playerName.Trim();

                    if (playerName == "")
                    {
                        session.Player.SendMessage("You must specify a player's name.");
                        return;
                    }

                    var player = PlayerManager.FindFirstPlayerByName(playerName);

                    if (player == null)
                    {
                        session.Player.SendMessage($"Could not find {playerName} in PlayerManager!");
                        return;
                    }

                    //var houses = HouseManager.GetCharacterHouses(player.Guid.Full);
                    var houses = HouseManager.GetAccountHouses(player.Account.AccountId);

                    if (houses.Count == 0)
                    {
                        session.Player.SendMessage($"Player {playerName} does not own a house.");
                        return;
                    }

                    foreach (var house in houses)
                        DumpHouse(session, house, house);
                }
                else if (parameters.Length > 1 && parameters[1] == "account")
                {
                    var accountName = "";
                    for (var i = 2; i < parameters.Length; i++)
                        accountName += $"{parameters[i]} ";
                    accountName = accountName.Trim();

                    if (accountName == "")
                    {
                        session.Player.SendMessage("You must specify an account name.");
                        return;
                    }

                    var player = PlayerManager.FindFirstPlayer(p => p.Account?.AccountName?.Equals(accountName, StringComparison.OrdinalIgnoreCase) == true);

                    if (player == null)
                    {
                        session.Player.SendMessage($"Could not find {accountName} in PlayerManager!");
                        return;
                    }

                    var houses = HouseManager.GetAccountHouses(player.Account.AccountId);

                    if (houses.Count == 0)
                    {
                        session.Player.SendMessage($"Account {accountName} does not own a house.");
                        return;
                    }

                    foreach (var house in houses)
                        DumpHouse(session, house, house);
                }
                else if (parameters.Length > 1 && parameters[1] == "hid")
                {
                    if (parameters.Length < 2)
                    {
                        session.Player.SendMessage("You must specify a house id.");
                        return;
                    }

                    if (!uint.TryParse(parameters[2], out var houseId))
                    {
                        session.Player.SendMessage($"{parameters[2]} is not a valid house id.");
                        return;
                    }

                    var houses = HouseManager.GetHouseById(houseId);

                    if (houses.Count == 0)
                    {
                        session.Player.SendMessage($"HouseId {houseId} is not currently owned.");
                        return;
                    }

                    foreach (var house in houses)
                        DumpHouse(session, house, house);
                }
                else
                {
                    session.Player.SendMessage("You must specify either \"name\", \"account\" or \"hid\".");
                }
            }
            else if (parameters.Length >= 1 && parameters[0] == "dump_all")
            {
                if (parameters.Length == 1)
                {
                    for (var i = 1u; i < 6251; i++)
                    {
                        var msg = $"{i}: ";

                        var house = HouseManager.GetHouseById(i).FirstOrDefault();

                        if (house != null)
                        {
                            var houseData = house.GetHouseData(PlayerManager.FindByGuid(new ObjectGuid(house.HouseOwner ?? 0)));
                            msg += $"{house.HouseType} | Owner: {house.HouseOwnerName} (0x{house.HouseOwner:X8}) | BuyTime: {Time.GetDateTimeFromTimestamp(houseData.BuyTime).ToLocalTime()} ({houseData.BuyTime}) | RentTime: {Time.GetDateTimeFromTimestamp(houseData.RentTime).ToLocalTime()} ({houseData.RentTime}) | RentDue: {Time.GetDateTimeFromTimestamp(house.GetRentDue(houseData.RentTime)).ToLocalTime()} ({house.GetRentDue(houseData.RentTime)}) | Rent is {(house.SlumLord.IsRentPaid() ? "" : "NOT ")}paid{(house.HouseStatus != HouseStatus.Active ? $"  ({house.HouseStatus})" : "")}";
                        }
                        else
                        {
                            msg += "House is NOT currently owned";
                        }

                        session.Player.SendMessage(msg);
                    }
                }
                else if (parameters.Length > 1 && parameters[1] == "summary")
                {
                    var apartmentsTotal = 3000d;
                    var cottagesTotal   = 2600d;
                    var villasTotal     = 570d;
                    var mansionsTotal   = 80d;

                    var cottages   = 0;
                    var villas     = 0;
                    var mansions   = 0;
                    var apartments = 0;

                    for (var i = 1u; i < 6251; i++)
                    {
                        var house = HouseManager.GetHouseById(i).FirstOrDefault();

                        if (house == null)
                            continue;

                        //var houseData = house.GetHouseData(PlayerManager.FindByGuid(new ObjectGuid(house.HouseOwner ?? 0)));
                        switch (house.HouseType)
                        {
                            case HouseType.Apartment:
                                apartments++;
                                break;
                            case HouseType.Cottage:
                                cottages++;
                                break;
                            case HouseType.Mansion:
                                mansions++;
                                break;
                            case HouseType.Villa:
                                villas++;
                                break;
                        }
                    }

                    var apartmentsAvail = (apartmentsTotal - apartments) / apartmentsTotal;
                    var cottagesAvail   = (cottagesTotal - cottages) / cottagesTotal;
                    var villasAvail     = (villasTotal - villas) / villasTotal;
                    var mansionsAvail   = (mansionsTotal - mansions) / mansionsTotal;

                    var msg = "HUD Report:\n";
                    msg += "=========================================================\n";

                    msg += string.Format("{0, -12} {1, 4:0} / {2, 4:0} ({3, 7:P2} available for purchase)\n", "Apartments:", apartments, apartmentsTotal, apartmentsAvail);
                    msg += string.Format("{0, -12} {1, 4:0} / {2, 4:0} ({3, 7:P2} available for purchase)\n", "Cottages:", cottages, cottagesTotal, cottagesAvail);
                    msg += string.Format("{0, -12} {1, 4:0} / {2, 4:0} ({3, 7:P2} available for purchase)\n", "Villas:", villas, villasTotal, villasAvail);
                    msg += string.Format("{0, -12} {1, 4:0} / {2, 4:0} ({3, 7:P2} available for purchase)\n", "Mansions:", mansions, mansionsTotal, mansionsAvail);

                    var housesTotal = apartmentsTotal + cottagesTotal + villasTotal + mansionsTotal;
                    var housesSold = apartments + cottages + villas + mansions;
                    var housesAvail = (housesTotal - housesSold) / housesTotal;

                    msg += string.Format("{0, -12} {1, 4:0} / {2, 4:0} ({3, 7:P2} available for purchase)\n", "Total:", housesSold, housesTotal, housesAvail);

                    msg += "=========================================================\n";

                    session.Player.SendMessage(msg);
                }
                else if (parameters.Length > 1 && parameters[1] == "dangerous")
                {
                    for (var i = 1u; i < 6251; i++)
                    {
                        var houses = HouseManager.GetHouseById(i);

                        if (houses.Count == 0)
                        {
                            session.Player.SendMessage($"HouseId {i} is not currently owned.");
                            continue;
                        }

                        foreach (var house in houses)
                            DumpHouse(session, house, house);
                    }
                }
                else
                {
                    session.Player.SendMessage("You must specify either nothing, \"summary\" or \"dangerous\".");
                }
            }
            else if (parameters.Length >= 1 && parameters[0] == "rent")
            {
                if (parameters.Length > 1 && parameters[1] == "pay")
                {
                    if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
                    {
                        var house = GetSelectedHouse(session, out var wo);

                        if (house == null)
                            return;

                        if (HouseManager.PayRent(house))
                            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} paid rent for HouseId {house.HouseId} (0x{house.Guid}:{house.WeenieClassId})");
                    }
                    else
                    {
                        session.Player.SendMessage("No object is selected.");
                        return;
                    }
                }
                else if (parameters.Length > 1 && parameters[1] == "payall")
                {
                    HouseManager.PayAllRent();

                    PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} paid all rent for player housing.");
                }
                else
                {
                    session.Player.SendMessage("You must specify either \"pay\" or \"payall\".");
                }
            }
            else if (parameters.Length >= 1 && parameters[0] == "payrent")
            {
                if (parameters.Length > 1 && parameters[1] == "off")
                {
                    if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
                    {
                        var house = GetSelectedHouse(session, out _);

                        if (house == null)
                            return;

                        if (house.HouseStatus != HouseStatus.InActive)
                        {
                            house.HouseStatus = HouseStatus.InActive;
                            house.SaveBiotaToDatabase();

                            session.Player.SendMessage($"{house.Name} (0x{house.Guid}) is now maintenance free.");

                            if (house.HouseOwner > 0)
                            {
                                var onlinePlayer = PlayerManager.GetOnlinePlayer(house.HouseOwner ?? 0);
                                if (onlinePlayer != null)
                                {
                                    var updateHouseChain = new ActionChain();
                                    updateHouseChain.AddDelaySeconds(5.0f);
                                    updateHouseChain.AddAction(onlinePlayer, onlinePlayer.HandleActionQueryHouse);
                                    updateHouseChain.EnqueueChain();
                                }
                            }

                            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} set HouseStatus to {house.HouseStatus} for HouseId {house.HouseId} (0x{house.Guid}:{house.WeenieClassId}) which equates to MaintenanceFree = {house.HouseStatus == HouseStatus.InActive}");
                        }
                        else
                        {
                            session.Player.SendMessage($"{house.Name} (0x{house.Guid}) is already maintenance free.");
                        }
                    }
                    else
                    {
                        session.Player.SendMessage("No object is selected.");
                        return;
                    }
                }
                else if (parameters.Length > 1 && parameters[1] == "on")
                {
                    if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
                    {
                        var house = GetSelectedHouse(session, out _);

                        if (house == null)
                            return;

                        if (house.HouseStatus != HouseStatus.Active)
                        {
                            house.HouseStatus = HouseStatus.Active;
                            house.SaveBiotaToDatabase();

                            session.Player.SendMessage($"{house.Name} (0x{house.Guid}) now requires maintenance.");

                            if (house.HouseOwner > 0)
                            {
                                var onlinePlayer = PlayerManager.GetOnlinePlayer(house.HouseOwner ?? 0);
                                if (onlinePlayer != null)
                                {
                                    var updateHouseChain = new ActionChain();
                                    updateHouseChain.AddDelaySeconds(5.0f);
                                    updateHouseChain.AddAction(onlinePlayer, onlinePlayer.HandleActionQueryHouse);
                                    updateHouseChain.EnqueueChain();
                                }
                            }

                            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} set HouseStatus to {house.HouseStatus} for HouseId {house.HouseId} (0x{house.Guid}:{house.WeenieClassId}) which equates to MaintenanceFree = {house.HouseStatus == HouseStatus.InActive}");
                        }
                        else
                        {
                            session.Player.SendMessage($"{house.Name} (0x{house.Guid}) already requires maintenance.");
                        }
                    }
                    else
                    {
                        session.Player.SendMessage("No object is selected.");
                        return;
                    }
                }
                else
                {
                    session.Player.SendMessage("You must specify either \"on\" or \"off\".");
                }
            }
            else
            {
                var msg = "@adminhouse dump: dumps info about currently selected house or house owned by currently selected player.\n";
                msg += "@adminhouse dump name <name>: dumps info about house owned by the account of the named player.\n";
                msg += "@adminhouse dump account <account_name>: dumps info about house owned by named account.\n";
                msg += "@adminhouse dump hid <houseID>: dumps info about specified house.\n";
                msg += "@adminhouse dump_all: dumps one line about each house in the world.\n";
                msg += "@adminhouse dump_all summary: dumps info about total houses owned for each house type.\n";
                msg += "@adminhouse dump_all dangerous: dumps full info about all houses. Use with caution.\n";
                msg += "@adminhouse rent pay: fully pay the rent of the selected house.\n";
                msg += "@adminhouse rent payall: fully pay the rent for all houses.\n";
                msg += "@adminhouse payrent off / on: sets the targeted house to not require / require normal maintenance payments.\n";

                session.Player.SendMessage(msg);
            }
        }

        private static void DumpHouse(Session session, House targetHouse, WorldObject wo)
        {
            HouseManager.GetHouse(targetHouse.Guid.Full, (house) =>
            {
                var msg = "";
                msg = $"House Dump for {wo.Name} (0x{wo.Guid})\n";
                msg += $"===House=======================================\n";
                msg += $"Name: {house.Name} | {house.WeenieClassName} | WCID: {house.WeenieClassId} | GUID: 0x{house.Guid}\n";
                msg += $"Location: {house.Location.ToLOCString()}\n";
                msg += $"HouseID: {house.HouseId}\n";
                msg += $"HouseType: {house.HouseType} ({(int)house.HouseType})\n";
                msg += $"HouseStatus: {house.HouseStatus} ({(int)house.HouseStatus})\n";
                msg += $"RestrictionEffect: {(PlayScript)house.GetProperty(PropertyDataId.RestrictionEffect)} ({house.GetProperty(PropertyDataId.RestrictionEffect)})\n";
                msg += $"HouseMaxHooksUsable: {house.HouseMaxHooksUsable}\n";
                msg += $"HouseCurrentHooksUsable: {house.HouseCurrentHooksUsable}\n";
                msg += $"HouseHooksVisible: {house.HouseHooksVisible ?? false}\n";
                msg += $"OpenToEveryone: {house.OpenToEveryone}\n";
                session.Player.SendMessage(msg, ChatMessageType.System);

                if (house.LinkedHouses.Count > 0)
                {
                    msg = "";
                    msg += $"===LinkedHouses================================\n";
                    foreach (var link in house.LinkedHouses)
                    {
                        msg += $"Name: {link.Name} | {link.WeenieClassName} | WCID: {link.WeenieClassId} | GUID: 0x{link.Guid}\n";
                        msg += $"Location: {link.Location.ToLOCString()}\n";
                    }
                    session.Player.SendMessage(msg, ChatMessageType.System);
                }

                msg = "";
                msg += $"===SlumLord====================================\n";
                var slumLord = house.SlumLord;
                msg += $"Name: {slumLord.Name} | {slumLord.WeenieClassName} | WCID: {slumLord.WeenieClassId} | GUID: 0x{slumLord.Guid}\n";
                msg += $"Location: {slumLord.Location.ToLOCString()}\n";
                msg += $"MinLevel: {slumLord.MinLevel}\n";
                msg += $"AllegianceMinLevel: {slumLord.AllegianceMinLevel ?? 0}\n";
                msg += $"HouseRequiresMonarch: {slumLord.HouseRequiresMonarch}\n";
                msg += $"IsRentPaid: {slumLord.IsRentPaid()}\n";
                session.Player.SendMessage(msg, ChatMessageType.System);

                msg = "";
                msg += $"===HouseProfile================================\n";
                var houseProfile = slumLord.GetHouseProfile();

                msg += $"Type: {houseProfile.Type} | Bitmask: {houseProfile.Bitmask}\n";

                msg += $"MinLevel: {houseProfile.MinLevel} | MaxLevel: {houseProfile.MaxLevel}\n";
                msg += $"MinAllegRank: {houseProfile.MinAllegRank} | MaxAllegRank: {houseProfile.MaxAllegRank}\n";

                msg += $"OwnerID: 0x{houseProfile.OwnerID} | OwnerName: {(string.IsNullOrEmpty(houseProfile.OwnerName) ? "N/A" : $"{houseProfile.OwnerName}")}\n";
                msg += $"MaintenanceFree: {houseProfile.MaintenanceFree}\n";
                msg += "--== Buy Cost==--\n";
                foreach (var cost in houseProfile.Buy)
                    msg += $"{cost.Num:N0} {(cost.Num > 1 ? $"{cost.PluralName}" : $"{cost.Name}")} (WCID: {cost.WeenieID})\n";
                msg += "--==Rent Cost==--\n";
                foreach (var cost in houseProfile.Rent)
                    msg += $"{cost.Num:N0} {(cost.Num > 1 ? $"{cost.PluralName}" : $"{cost.Name}")} (WCID: {cost.WeenieID}) | Paid: {cost.Paid:N0}\n";
                session.Player.SendMessage(msg, ChatMessageType.System);

                var houseData = house.GetHouseData(PlayerManager.FindByGuid(houseProfile.OwnerID));
                if (houseData != null)
                {
                    msg = "";
                    msg += $"===HouseData===================================\n";
                    msg += $"Location: {houseData.Position.ToLOCString()}\n";
                    msg += $"Type: {houseData.Type}\n";
                    msg += $"BuyTime: {(houseData.BuyTime > 0 ? $"{Time.GetDateTimeFromTimestamp(houseData.BuyTime).ToLocalTime()}" : "N/A")} ({houseData.BuyTime})\n";
                    msg += $"RentTime: {(houseData.RentTime > 0 ? $"{Time.GetDateTimeFromTimestamp(houseData.RentTime).ToLocalTime()}" : "N/A")} ({houseData.RentTime})\n";
                    msg += $"RentDue: {(houseData.RentTime > 0 ? $"{Time.GetDateTimeFromTimestamp(house.GetRentDue(houseData.RentTime)).ToLocalTime()} ({house.GetRentDue(houseData.RentTime)})" : " N/A (0)")}\n";
                    msg += $"MaintenanceFree: {houseData.MaintenanceFree}\n";
                    session.Player.SendMessage(msg, ChatMessageType.System);
                }

                session.Player.SendMessage(AppendHouseLinkDump(house), ChatMessageType.System);

                if (house.HouseType == HouseType.Villa || house.HouseType == HouseType.Mansion)
                {
                    var basement = house.GetDungeonHouse();
                    if (basement != null)
                    {
                        msg = "";
                        msg += $"===Basement====================================\n";
                        msg += $"Name: {basement.Name} | {basement.WeenieClassName} | WCID: {basement.WeenieClassId} | GUID: 0x{basement.Guid}\n";
                        msg += $"Location: {basement.Location.ToLOCString()}\n";
                        msg += $"HouseMaxHooksUsable: {basement.HouseMaxHooksUsable}\n";
                        msg += $"HouseCurrentHooksUsable: {basement.HouseCurrentHooksUsable}\n";
                        session.Player.SendMessage(msg, ChatMessageType.System);
                        session.Player.SendMessage(AppendHouseLinkDump(basement), ChatMessageType.System);
                    }
                }

                var guestList = house.Guests.ToList();
                if (guestList.Count > 0)
                {
                    msg = "";
                    msg += $"===GuestList===================================\n";
                    foreach (var guest in guestList)
                    {
                        var player = PlayerManager.FindByGuid(guest.Key);
                        msg += $"{(player != null ? $"{player.Name}" : "[N/A]")} (0x{guest.Key}){(guest.Value ? " *" : "")}\n";
                    }
                    msg += "* denotes granted access to the home's storage\n";
                    session.Player.SendMessage(msg, ChatMessageType.System);
                }

                var restrictionDB = new RestrictionDB(house);
                msg = "";
                msg += $"===RestrictionDB===============================\n";
                var owner = PlayerManager.FindByGuid(restrictionDB.HouseOwner);
                msg += $"HouseOwner: {(owner != null ? $"{owner.Name}" : "N/A")} (0x{restrictionDB.HouseOwner:X8})\n";
                msg += $"OpenStatus: {restrictionDB.OpenStatus}\n";
                var monarchRDB = PlayerManager.FindByGuid(restrictionDB.MonarchID);
                msg += $"MonarchID: {(monarchRDB != null ? $"{monarchRDB.Name}" : "N/A")} (0x{restrictionDB.MonarchID:X8})\n";
                if (restrictionDB.Table.Count > 0)
                {
                    msg += "--==Guests==--\n";
                    foreach (var guest in restrictionDB.Table)
                    {
                        var player = PlayerManager.FindByGuid(guest.Key);
                        msg += $"{(player != null ? $"{player.Name}" : "[N/A]")} (0x{guest.Key}){(guest.Value == 1 ? " *" : "")}\n";
                    }
                    msg += "* denotes granted access to the home's storage\n";
                }
                session.Player.SendMessage(msg, ChatMessageType.System);

                var har = new HouseAccess(house);
                msg = "";
                msg += $"===HouseAccess=================================\n";
                msg += $"Bitmask: {har.Bitmask}\n";
                var monarchHAR = PlayerManager.FindByGuid(har.MonarchID);
                msg += $"MonarchID: {(monarchHAR != null ? $"{monarchHAR.Name}" : "N/A")} (0x{har.MonarchID:X8})\n";
                if (har.GuestList.Count > 0)
                {
                    msg += "--==Guests==--\n";
                    foreach (var guest in har.GuestList)
                    {
                        msg += $"{(guest.Value.GuestName != null ? $"{guest.Value.GuestName}" : "[N/A]")} (0x{guest.Key}){(guest.Value.ItemStoragePermission ? " *" : "")}\n";
                    }
                    msg += "* denotes granted access to the home's storage\n";
                }
                if (har.Roommates.Count > 0)
                {
                    msg += "--==Roommates==--\n";
                    foreach (var guest in har.Roommates)
                    {
                        var player = PlayerManager.FindByGuid(guest);
                        msg += $"{(player != null ? $"{player.Name}" : "[N/A]")} (0x{guest})\n";
                    }
                }
                session.Player.SendMessage(msg, ChatMessageType.System);
            });
        }

        private static House GetSelectedHouse(Session session, out WorldObject target)
        {
            ObjectGuid objectId;
            if (session.Player.HealthQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
            else if (session.Player.ManaQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
            else
                objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

            target = session.Player.CurrentLandblock?.GetObject(objectId);

            if (target == null)
            {
                session.Player.SendMessage("No object is selected or unable to locate in world.");
                return null;
            }

            House house;

            if (target is Player player)
            {
                if (player.House == null)
                {
                    session.Player.SendMessage($"Player {player.Name} does not own a house.");
                    return null;
                }

                house = player.House;
                //house = HouseManager.GetCharacterHouses(player.Guid.Full).FirstOrDefault();
            }
            else if (target is House house1)
                house = house1.RootHouse;
            else if (target is Hook hook)
                house = hook.House.RootHouse;
            else if (target is Storage storage)
                house = storage.House.RootHouse;
            else if (target is SlumLord slumLord1)
                house = slumLord1.House.RootHouse;
            else if (target is HousePortal housePortal)
                house = housePortal.House.RootHouse;
            else
            {
                session.Player.SendMessage("Selected object is not a player or housing object.");
                return null;
            }

            if (house == null)
            {
                session.Player.SendMessage("Selected house object is null");
                return null;
            }

            return house;
        }

        private static string AppendHouseLinkDump(House house)
        {
            var msg = "";

            if (house.Storage.Count > 0)
            {
                msg += $"===Storage for House 0x{house.Guid}================\n";
                msg += $"Storage.Count: {house.Storage.Count}\n";
                foreach (var chest in house.Storage)
                {
                    msg += $"Name: {chest.Name} | {chest.WeenieClassName} | WCID: {chest.WeenieClassId} | GUID: 0x{chest.Guid}\n";
                    msg += $"Location: {chest.Location.ToLOCString()}\n";
                }
            }

            if (house.Hooks.Count > 0)
            {
                msg += $"===Hooks for House 0x{house.Guid}==================\n";
                msg += $"Hooks.Count: {house.Hooks.Count(h => h.HasItem)} in use / {house.HouseMaxHooksUsable} max allowed usable / {house.Hooks.Count} total\n";
                msg += "--==HooksGroups==--\n";
                foreach (var hookGroup in (HookGroupType[])Enum.GetValues(typeof(HookGroupType)))
                {
                    msg += $"{hookGroup}.Count: {house.GetHookGroupCurrentCount(hookGroup)} in use / {house.GetHookGroupMaxCount(hookGroup)} max allowed per group\n";
                }
                msg += "--==Hooks==--\n";
                foreach (var hook in house.Hooks)
                {
                    msg += $"Name: {hook.Name} | {hook.WeenieClassName} | WCID: {hook.WeenieClassId} | GUID: 0x{hook.Guid}\n";
                    // msg += $"Location: {hook.Location.ToLOCString()}\n";
                    msg += $"HookType: {(HookType)hook.HookType} ({hook.HookType}){(hook.HasItem ? $" | Item on Hook: {hook.Item.Name} (0x{hook.Item.Guid}:{hook.Item.WeenieClassId}:{hook.Item.WeenieType}) | HookGroup: {hook.Item.HookGroup ?? HookGroupType.Undef} ({(int)(hook.Item.HookGroup ?? 0)})" : "")}\n";
                }
            }

            if (house.BootSpot != null)
            {
                msg += $"===BootSpot for House 0x{house.Guid}===============\n";
                msg += $"Name: {house.BootSpot.Name} | {house.BootSpot.WeenieClassName} | WCID: {house.BootSpot.WeenieClassId} | GUID: 0x{house.BootSpot.Guid}\n";
                msg += $"Location: {house.BootSpot.Location.ToLOCString()}\n";
            }

            if (house.HousePortal != null)
            {
                msg += $"===HousePortal for House 0x{house.Guid}============\n";
                msg += $"Name: {house.HousePortal.Name} | {house.HousePortal.WeenieClassName} | WCID: {house.HousePortal.WeenieClassId} | GUID: 0x{house.HousePortal.Guid}\n";
                msg += $"Location: {house.HousePortal.Location.ToLOCString()}\n";
                msg += $"Destination: {house.HousePortal.Destination.ToLOCString()}\n";
            }

            return msg;
        }

        // bornagain deletedCharID[, newCharName[, accountName]]
        [CommandHandler("bornagain", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1,
            "Restores a deleted character to an account.",
            "deletedCharID(, newCharName)(, accountName)\n" +
            "Given the ID of a deleted character, this command restores that character to its owner.  (You can find the ID of a deleted character using the @finger command.)\n" +
            "If the deleted character's name has since been taken by a new character, you can specify a new name for the restored character as the second parameter.  (You can find if the name has been taken by also using the @finger command.)  Use a comma to separate the arguments.\n" +
            "If needed, you can specify an account name as a third parameter if the character should be restored to an account other than its original owner.  Again, use a comma between the arguments.")]
        public static void HandleBornAgain(Session session, params string[] parameters)
        {
            // usage: @bornagain deletedCharID[, newCharName[, accountName]]
            // Given the ID of a deleted character, this command restores that character to its owner.  (You can find the ID of a deleted character using the @finger command.)
            // If the deleted character's name has since been taken by a new character, you can specify a new name for the restored character as the second parameter.  (You can find if the name has been taken by also using the @finger command.)  Use a comma to separate the arguments.
            // If needed, you can specify an account name as a third parameter if the character should be restored to an account other than its original owner.  Again, use a comma between the arguments.
            // @bornagain - Restores a deleted character to an account.

            var hexNumber = parameters[0];

            if (hexNumber.StartsWith("0x"))
                hexNumber = hexNumber.Substring(2);

            if (hexNumber.EndsWith(","))
                hexNumber = hexNumber[..^1];

            if (uint.TryParse(hexNumber, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var existingCharIID))
            {
                var args = string.Join(" ", parameters);

                if (args.Contains(','))
                {
                    var twoCommas = args.Count(c => c == ',') == 2;

                    var names = string.Join(" ", parameters).Split(",");

                    var newCharName = names[1].TrimStart(' ').TrimEnd(' ');

                    if (newCharName.StartsWith("+"))
                        newCharName = newCharName.Substring(1);
                    newCharName = newCharName.First().ToString().ToUpper() + newCharName.Substring(1);

                    string newAccountName;
                    if (twoCommas)
                    {
                        newAccountName = names[2].TrimStart(' ').TrimEnd(' ').ToLower();

                        var account = DatabaseManager.Authentication.GetAccountByName(newAccountName);

                        if (account == null)
                        {
                            CommandHandlerHelper.WriteOutputInfo(session, $"Error, cannot restore. Account \"{newAccountName}\" is not in database.", ChatMessageType.Broadcast);
                            return;
                        }

                        if (PlayerManager.IsAccountAtMaxCharacterSlots(account.AccountName))
                        {
                            CommandHandlerHelper.WriteOutputInfo(session, $"Error, cannot restore. Account \"{newAccountName}\" has no free character slots.", ChatMessageType.Broadcast);
                            return;
                        }

                        DoCopyChar(session, $"0x{existingCharIID:X8}", existingCharIID, true, newCharName, account.AccountId);
                    }
                    else
                    {
                        if (PlayerManager.IsAccountAtMaxCharacterSlots(session.Player.Account.AccountName))
                        {
                            CommandHandlerHelper.WriteOutputInfo(session, $"Error, cannot restore. Account \"{session.Player.Account.AccountName}\" has no free character slots.", ChatMessageType.Broadcast);
                            return;
                        }

                        DoCopyChar(session, $"0x{existingCharIID:X8}", existingCharIID, true, newCharName);
                    }
                }
                else
                {
                    if (PlayerManager.IsAccountAtMaxCharacterSlots(session.Player.Account.AccountName))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Error, cannot restore. Account \"{session.Player.Account.AccountName}\" has no free character slots.", ChatMessageType.Broadcast);
                        return;
                    }

                    DoCopyChar(session, $"0x{existingCharIID:X8}", existingCharIID, true);
                }
            }
            else
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Error, cannot restore. You must include an existing character id in hex form.\nExample: @copychar 0x500000AC\n         @copychar 0x500000AC, Newly Restored\n         @copychar 0x500000AC, Newly Restored, differentaccount\n", ChatMessageType.Broadcast);
            }
        }

        // copychar < character name >, < copy name >[, accountName]
        [CommandHandler("copychar", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2,
            "Copies an existing character into your character list.",
            "< Existing Character Name >, < New Character Name >(, < Account Name >)\n" +
            "Given the name of an existing character \"character name\", this command makes a copy of that character with the name \"copy name\" and places it into your character list or the specified account character list.")]
        public static void HandleCopychar(Session session, params string[] parameters)
        {
            // usage: @copychar < character name >, < copy name >
            // Given the name of an existing character "character name", this command makes a copy of that character with the name "copy name" and places it into your character list.
            // @copychar - Copies an existing character into your character list.

            if (!string.Join(" ", parameters).Contains(','))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Error, cannot copy. You must include the existing character name followed by a comma and then the new name.\n Example: @copychar Old Name, New Name", ChatMessageType.Broadcast);
                return;
            }

            var names = string.Join(" ", parameters).Split(",");

            var existingCharName = names[0].TrimStart(' ').TrimEnd(' ');
            var newCharName = names[1].TrimStart(' ').TrimEnd(' ');

            if (existingCharName.StartsWith("+"))
                existingCharName = existingCharName.Substring(1);
            if (newCharName.StartsWith("+"))
                newCharName = newCharName.Substring(1);

            newCharName = newCharName.First().ToString().ToUpper() + newCharName.Substring(1);

            var existingPlayer = PlayerManager.FindByName(existingCharName);
            if (existingPlayer == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to copy the character \"{existingCharName}\"! Character doesn't exist.", ChatMessageType.Broadcast);
                return;
            }

            if (names.Length > 2)
            {
                string newAccountName = names[2].TrimStart(' ').TrimEnd(' ').ToLower();

                var account = DatabaseManager.Authentication.GetAccountByName(newAccountName);

                if (account == null)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Error, cannot restore. Account \"{newAccountName}\" is not in database.", ChatMessageType.Broadcast);
                    return;
                }

                if (PlayerManager.IsAccountAtMaxCharacterSlots(account.AccountName))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Error, cannot restore. Account \"{newAccountName}\" has no free character slots.", ChatMessageType.Broadcast);
                    return;
                }

                DoCopyChar(session, existingCharName, existingPlayer.Guid.Full, false, newCharName, account.AccountId);
                return;
            }
            else if ( session.Characters.Count >= PropertyManager.GetLong("max_chars_per_account").Item)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to copy the character \"{existingCharName}\" to a new character \"{newCharName}\" for the account \"{session.Account}\"! Account is out of free character slots.", ChatMessageType.Broadcast);
                return;
            }

            DoCopyChar(session, existingCharName, existingPlayer.Guid.Full, false, newCharName);
        }

        private static void DoCopyChar(Session session, string existingCharName, uint existingCharId, bool isDeletedChar, string newCharacterName = null, uint newAccountId = 0)
        {
            DatabaseManager.Shard.GetCharacter(existingCharId, existingCharacter =>
            {
                if (existingCharacter != null)
                {
                    var newCharName = newCharacterName ?? existingCharacter.Name;

                    var existingPlayerBiota = DatabaseManager.Shard.BaseDatabase.GetBiota(existingCharId);

                    DatabaseManager.Shard.GetPossessedBiotasInParallel(existingCharId, existingPossessions =>
                    {
                        DatabaseManager.Shard.IsCharacterNameAvailable(newCharName, isAvailable =>
                        {
                            if (isAvailable)
                            {
                                isAvailable = (PlayerManager.FindByName(newCharName) == null);
                            }
                            if (!isAvailable)
                            {
                                CommandHandlerHelper.WriteOutputInfo(session, $"{newCharName} is not available to use for the {(isDeletedChar ? "restored" : "copied")} character name, try another name.", ChatMessageType.Broadcast);
                                return;
                            }

                            var newPlayerGuid = GuidManager.NewPlayerGuid();

                            var newCharacter = new Database.Models.Shard.Character
                            {
                                Id = newPlayerGuid.Full,
                                AccountId = newAccountId > 0 ? newAccountId : session.Player.Account.AccountId,
                                Name = newCharName,
                                CharacterOptions1 = existingCharacter.CharacterOptions1,
                                CharacterOptions2 = existingCharacter.CharacterOptions2,
                                DefaultHairTexture = existingCharacter.DefaultHairTexture,
                                GameplayOptions = existingCharacter.GameplayOptions,
                                HairTexture = existingCharacter.HairTexture,
                                IsPlussed = existingCharacter.IsPlussed,
                                SpellbookFilters = existingCharacter.SpellbookFilters,
                                TotalLogins = 1 // existingCharacter.TotalLogins
                            };

                            foreach (var entry in existingCharacter.CharacterPropertiesContractRegistry)
                                newCharacter.CharacterPropertiesContractRegistry.Add(new Database.Models.Shard.CharacterPropertiesContractRegistry { CharacterId = newPlayerGuid.Full, ContractId = entry.ContractId, DeleteContract = entry.DeleteContract, SetAsDisplayContract = entry.SetAsDisplayContract });

                            foreach (var entry in existingCharacter.CharacterPropertiesFillCompBook)
                                newCharacter.CharacterPropertiesFillCompBook.Add(new Database.Models.Shard.CharacterPropertiesFillCompBook { CharacterId = newPlayerGuid.Full, QuantityToRebuy = entry.QuantityToRebuy, SpellComponentId = entry.SpellComponentId });

                            foreach (var entry in existingCharacter.CharacterPropertiesFriendList)
                                newCharacter.CharacterPropertiesFriendList.Add(new Database.Models.Shard.CharacterPropertiesFriendList { CharacterId = newPlayerGuid.Full, FriendId = entry.FriendId });

                            foreach (var entry in existingCharacter.CharacterPropertiesQuestRegistry)
                                newCharacter.CharacterPropertiesQuestRegistry.Add(new Database.Models.Shard.CharacterPropertiesQuestRegistry { CharacterId = newPlayerGuid.Full, LastTimeCompleted = entry.LastTimeCompleted, NumTimesCompleted = entry.NumTimesCompleted, QuestName = entry.QuestName });

                            foreach (var entry in existingCharacter.CharacterPropertiesShortcutBar)
                                newCharacter.CharacterPropertiesShortcutBar.Add(new Database.Models.Shard.CharacterPropertiesShortcutBar { CharacterId = newPlayerGuid.Full, ShortcutBarIndex = entry.ShortcutBarIndex, ShortcutObjectId = entry.ShortcutObjectId });

                            foreach (var entry in existingCharacter.CharacterPropertiesSpellBar)
                                newCharacter.CharacterPropertiesSpellBar.Add(new Database.Models.Shard.CharacterPropertiesSpellBar { CharacterId = newPlayerGuid.Full, SpellBarIndex = entry.SpellBarIndex, SpellBarNumber = entry.SpellBarNumber, SpellId = entry.SpellId });

                            foreach (var entry in existingCharacter.CharacterPropertiesSquelch)
                                newCharacter.CharacterPropertiesSquelch.Add(new Database.Models.Shard.CharacterPropertiesSquelch { CharacterId = newPlayerGuid.Full, SquelchAccountId = entry.SquelchAccountId, SquelchCharacterId = entry.SquelchCharacterId, Type = entry.Type });

                            foreach (var entry in existingCharacter.CharacterPropertiesTitleBook)
                                newCharacter.CharacterPropertiesTitleBook.Add(new Database.Models.Shard.CharacterPropertiesTitleBook { CharacterId = newPlayerGuid.Full, TitleId = entry.TitleId });

                            var idSwaps = new ConcurrentDictionary<uint, uint>();

                            var newPlayerBiota = Database.Adapter.BiotaConverter.ConvertToEntityBiota(existingPlayerBiota);

                            idSwaps[newPlayerBiota.Id] = newPlayerGuid.Full;

                            newPlayerBiota.Id = newPlayerGuid.Full;
                            if (newPlayerBiota.PropertiesAllegiance != null)
                                newPlayerBiota.PropertiesAllegiance.Clear();
                            if (newPlayerBiota.HousePermissions != null)
                                newPlayerBiota.HousePermissions.Clear();

                            var newTempWieldedItems = new List<Biota>();
                            foreach (var item in existingPossessions.WieldedItems)
                            {
                                var newItemBiota = Database.Adapter.BiotaConverter.ConvertToEntityBiota(item);
                                var newGuid = GuidManager.NewDynamicGuid();
                                idSwaps[newItemBiota.Id] = newGuid.Full;
                                newItemBiota.Id = newGuid.Full;
                                newTempWieldedItems.Add(newItemBiota);
                            }

                            var newTempInventoryItems = new List<Biota>();
                            foreach (var item in existingPossessions.Inventory)
                            {
                                if (item.WeenieClassId == (uint)WeenieClassName.W_DEED_CLASS)
                                    continue;

                                var newItemBiota = Database.Adapter.BiotaConverter.ConvertToEntityBiota(item);
                                var newGuid = GuidManager.NewDynamicGuid();
                                idSwaps[newItemBiota.Id] = newGuid.Full;
                                newItemBiota.Id = newGuid.Full;
                                newTempInventoryItems.Add(newItemBiota);
                            }


                            var newWieldedItems = new List<Database.Models.Shard.Biota>();
                            foreach (var item in newTempWieldedItems)
                            {
                                if (item.PropertiesEnchantmentRegistry != null)
                                {
                                    foreach (var entry in item.PropertiesEnchantmentRegistry)
                                    {
                                        if (idSwaps.ContainsKey(entry.CasterObjectId))
                                            entry.CasterObjectId = idSwaps[entry.CasterObjectId];
                                    }
                                }

                                if (item.PropertiesIID != null)
                                {
                                    if (item.PropertiesIID.TryGetValue(PropertyInstanceId.Owner, out var ownerId))
                                    {
                                        if (idSwaps.ContainsKey(ownerId))
                                        {
                                            item.PropertiesIID.Remove(PropertyInstanceId.Owner);
                                            item.PropertiesIID.Add(PropertyInstanceId.Owner, idSwaps[ownerId]);
                                        }
                                    }

                                    if (item.PropertiesIID.TryGetValue(PropertyInstanceId.Wielder, out var wielderId))
                                    {
                                        if (idSwaps.ContainsKey(wielderId))
                                        {
                                            item.PropertiesIID.Remove(PropertyInstanceId.Wielder);
                                            item.PropertiesIID.Add(PropertyInstanceId.Wielder, idSwaps[wielderId]);
                                        }
                                    }

                                    if (item.PropertiesIID.TryGetValue(PropertyInstanceId.AllowedActivator, out var allowedActivatorId))
                                    {
                                        if (idSwaps.ContainsKey(allowedActivatorId))
                                        {
                                            item.PropertiesIID.Remove(PropertyInstanceId.AllowedActivator);
                                            item.PropertiesIID.Add(PropertyInstanceId.AllowedActivator, idSwaps[allowedActivatorId]);

                                            item.PropertiesString.Remove(PropertyString.CraftsmanName);
                                            item.PropertiesString.Add(PropertyString.CraftsmanName, newCharName);
                                        }
                                    }

                                    if (item.PropertiesIID.TryGetValue(PropertyInstanceId.AllowedWielder, out var allowedWielderId))
                                    {
                                        if (idSwaps.ContainsKey(allowedWielderId))
                                        {
                                            item.PropertiesIID.Remove(PropertyInstanceId.AllowedWielder);
                                            item.PropertiesIID.Add(PropertyInstanceId.AllowedWielder, idSwaps[allowedWielderId]);

                                            item.PropertiesString.Remove(PropertyString.CraftsmanName);
                                            item.PropertiesString.Add(PropertyString.CraftsmanName, newCharName);
                                        }
                                    }
                                }

                                newWieldedItems.Add(Database.Adapter.BiotaConverter.ConvertFromEntityBiota(item));
                            }

                            var newInventoryItems = new List<Database.Models.Shard.Biota>();
                            foreach (var item in newTempInventoryItems)
                            {
                                if (item.PropertiesEnchantmentRegistry != null)
                                {
                                    foreach (var entry in item.PropertiesEnchantmentRegistry)
                                    {
                                        if (idSwaps.ContainsKey(entry.CasterObjectId))
                                            entry.CasterObjectId = idSwaps[entry.CasterObjectId];
                                    }
                                }

                                if (item.PropertiesIID != null)
                                {
                                    if (item.PropertiesIID.TryGetValue(PropertyInstanceId.Owner, out var ownerId))
                                    {
                                        if (idSwaps.ContainsKey(ownerId))
                                        {
                                            item.PropertiesIID.Remove(PropertyInstanceId.Owner);
                                            item.PropertiesIID.Add(PropertyInstanceId.Owner, idSwaps[ownerId]);
                                        }
                                    }

                                    if (item.PropertiesIID.TryGetValue(PropertyInstanceId.Container, out var containerId))
                                    {
                                        if (idSwaps.ContainsKey(containerId))
                                        {
                                            item.PropertiesIID.Remove(PropertyInstanceId.Container);
                                            item.PropertiesIID.Add(PropertyInstanceId.Container, idSwaps[containerId]);
                                        }
                                    }

                                    if (item.PropertiesIID.TryGetValue(PropertyInstanceId.AllowedActivator, out var allowedActivatorId))
                                    {
                                        if (idSwaps.ContainsKey(allowedActivatorId))
                                        {
                                            item.PropertiesIID.Remove(PropertyInstanceId.AllowedActivator);
                                            item.PropertiesIID.Add(PropertyInstanceId.AllowedActivator, idSwaps[allowedActivatorId]);

                                            item.PropertiesString.Remove(PropertyString.CraftsmanName);
                                            item.PropertiesString.Add(PropertyString.CraftsmanName, $"{(existingCharacter.IsPlussed ? "+" : "")}{newCharName}");
                                        }
                                    }

                                    if (item.PropertiesIID.TryGetValue(PropertyInstanceId.AllowedWielder, out var allowedWielderId))
                                    {
                                        if (idSwaps.ContainsKey(allowedWielderId))
                                        {
                                            item.PropertiesIID.Remove(PropertyInstanceId.AllowedWielder);
                                            item.PropertiesIID.Add(PropertyInstanceId.AllowedWielder, idSwaps[allowedWielderId]);

                                            item.PropertiesString.Remove(PropertyString.CraftsmanName);
                                            item.PropertiesString.Add(PropertyString.CraftsmanName, $"{(existingCharacter.IsPlussed ? "+" : "")}{newCharName}");
                                        }
                                    }
                                }

                                newInventoryItems.Add(Database.Adapter.BiotaConverter.ConvertFromEntityBiota(item));
                            }

                            Player newPlayer;
                            if (newPlayerBiota.WeenieType == WeenieType.Admin)
                                newPlayer = new Admin(newPlayerBiota, newInventoryItems, newWieldedItems, newCharacter, session);
                            else if (newPlayerBiota.WeenieType == WeenieType.Sentinel)
                                newPlayer = new Sentinel(newPlayerBiota, newInventoryItems, newWieldedItems, newCharacter, session);
                            else
                                newPlayer = new Player(newPlayerBiota, newInventoryItems, newWieldedItems, newCharacter, session);

                            newPlayer.Name = newCharName;
                            newPlayer.ChangesDetected = true;
                            newPlayer.CharacterChangesDetected = true;

                            newPlayer.Allegiance = null;
                            newPlayer.AllegianceOfficerRank = null;
                            newPlayer.MonarchId = null;
                            newPlayer.PatronId = null;
                            newPlayer.HouseId = null;
                            newPlayer.HouseInstance = null;

                            if (newPlayer.Character.CharacterPropertiesShortcutBar != null)
                            {
                                foreach (var entry in newPlayer.Character.CharacterPropertiesShortcutBar)
                                {
                                    if (idSwaps.ContainsKey(entry.ShortcutObjectId))
                                        entry.ShortcutObjectId = idSwaps[entry.ShortcutObjectId];
                                }
                            }

                            if (newPlayer.Biota.PropertiesEnchantmentRegistry != null)
                            {
                                foreach (var entry in newPlayer.Biota.PropertiesEnchantmentRegistry)
                                {
                                    if (idSwaps.ContainsKey(entry.CasterObjectId))
                                        entry.CasterObjectId = idSwaps[entry.CasterObjectId];
                                }
                            }

                            var possessions = newPlayer.GetAllPossessions();
                            var possessedBiotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();
                            foreach (var possession in possessions)
                                possessedBiotas.Add((possession.Biota, possession.BiotaDatabaseLock));

                            // We must await here -- 
                            DatabaseManager.Shard.AddCharacterInParallel(newPlayer.Biota, newPlayer.BiotaDatabaseLock, possessedBiotas, newPlayer.Character, newPlayer.CharacterDatabaseLock, saveSuccess =>
                            {
                                if (!saveSuccess)
                                {
                                    //CommandHandlerHelper.WriteOutputInfo(session, $"Failed to copy the character \"{(existingCharacter.IsPlussed ? "+" : "")}{existingCharacter.Name}\" to a new character \"{newPlayer.Name}\" for the account \"{newPlayer.Account.AccountName}\"! Does the character exist _AND_ is not currently logged in? Is the new character name already taken, or is the account out of free character slots?", ChatMessageType.Broadcast);
                                    CommandHandlerHelper.WriteOutputInfo(session, $"Failed to {(isDeletedChar ? "restore" : "copy")} the character \"{(existingCharacter.IsPlussed ? "+" : "")}{existingCharacter.Name}\" to a new character \"{newPlayer.Name}\" for the account \"{newPlayer.Account.AccountName}\"! Does the character exist? Is the new character name already taken, or is the account out of free character slots?", ChatMessageType.Broadcast);
                                    return;
                                }

                                PlayerManager.AddOfflinePlayer(newPlayer);

                                if (newAccountId == 0)
                                    session.Characters.Add(new Database.Models.Shard.LoginCharacter { Id = newPlayer.Character.Id, IsPlussed = newPlayer.IsPlussed, AccountId = newPlayer.Account.AccountId, Name = newPlayer.Name });
                                else
                                {
                                    var foundActiveSession = Network.Managers.NetworkManager.Find(newAccountId);

                                    if (foundActiveSession != null)
                                        foundActiveSession.Characters.Add(new Database.Models.Shard.LoginCharacter { Id = newPlayer.Character.Id, IsPlussed = newPlayer.IsPlussed, AccountId = newPlayer.Account.AccountId, Name = newPlayer.Name });
                                }

                                var msg = $"Successfully {(isDeletedChar ? "restored" : "copied")} the character \"{(existingCharacter.IsPlussed ? "+" : "")}{existingCharacter.Name}\" to a new character \"{newPlayer.Name}\" for the account \"{newPlayer.Account.AccountName}\".";
                                CommandHandlerHelper.WriteOutputInfo(session, msg, ChatMessageType.Broadcast);
                                PlayerManager.BroadcastToAuditChannel(session.Player, msg);
                            });
                        });
                    });
                }
                else
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Failed to {(isDeletedChar ? "restore" : "copy")} the character \"{existingCharName}\" to a new character \"{newCharacterName}\" for the account \"{session.Account}\"! Does the character exist? Is the new character name already taken, or is the account out of free character slots?", ChatMessageType.Broadcast);
                }
            });
        }


        /// <summary>
        /// Creates an object or objects in the world
        /// </summary>
        [CommandHandler("create", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Creates an object or objects in the world.",
            "<wcid or classname> (amount) (palette) (shade)\n" +
            "This will attempt to spawn the weenie you specify. If you include an amount to spawn, it will attempt to create that many of the object.\n" +
            "Stackable items will spawn in stacks of their max stack size. All spawns will be limited by the physics engine placement, which may prevent the number you specify from actually spawning." +
            "Be careful with large numbers, especially with ethereal weenies.")]
        public static void HandleCreate(Session session, params string[] parameters)
        {
            if (ParseCreateParameters(session, parameters, false, out Weenie weenie, out int numToSpawn, out int? palette, out float? shade, out _))
            {
                TryCreateObject(session, weenie, numToSpawn, palette, shade);
            }
        }

        /// <summary>
        /// Creates an object or objects in the world -- with lifespans for live events
        /// </summary>
        [CommandHandler("createliveops", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Creates an object or objects with lifespans in the world for live events.",
            "<wcid or classname> (amount) (lifespan) (palette) (shade)\n" +
            "This will attempt to spawn the weenie you specify. If you include an amount to spawn, it will attempt to spawn that many of the object.\n" +
            "Stackable items will spawn in stacks of their max stack size. All spawns will be limited by the physics engine placement, which may prevent the number you specify from actually spawning.\n" +
            "Be careful with large numbers, especially with ethereal weenies.\n" +
            "If you include a lifespan, this value is in seconds, and defaults to 3600 (1 hour) if not specified.")]
        public static void HandleCreateLiveOps(Session session, params string[] parameters)
        {
            if (ParseCreateParameters(session, parameters, true, out Weenie weenie, out int numToSpawn, out int? palette, out float? shade, out int? lifespan))
            {
                TryCreateObject(session, weenie, numToSpawn, palette, shade, lifespan);
            }
        }

        [CommandHandler("clearquest", AccessLevel.Admin, CommandHandlerFlag.None, 1)]
        public static void HandleQuestClear(Session session, params string[] parameters)
        {
            DatabaseManager.World.ClearCachedQuest(parameters[0]);
        }

        [CommandHandler("clearallquests", AccessLevel.Admin, CommandHandlerFlag.None, 0)]
        public static void HandleQuestClearAll(Session session, params string[] parameters)
        {
            DatabaseManager.World.ClearAllCachedQuests();
        }

        [CommandHandler("clearspellcache", AccessLevel.Admin, CommandHandlerFlag.None, 0)]
        public static void HandleClearSpellCache(Session session, params string[] parameters)
        {
            DatabaseManager.World.ClearSpellCache();
        }

        [CommandHandler("testdynamic", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1)]
        public static void TestDynamicQuest(Session session, params string[] parameters)
        {
            session.Player.QuestManager.ComputeDynamicQuest(parameters[0], session, true);
        }

        /// <summary>
        /// Parses the command-line parameters for /create or /createliveops
        /// The only difference with /createliveops is that it includes a lifespan parameter in the middle
        /// </summary>
        private static bool ParseCreateParameters(Session session, string[] parameters, bool hasLifespan, out Weenie weenie, out int numToSpawn, out int? palette, out float? shade, out int? lifespan)
        {
            weenie = GetWeenieForCreate(session, parameters[0]);
            numToSpawn = 1;
            palette = null;
            shade = null;
            lifespan = null;

            if (weenie == null)
                return false;

            if (parameters.Length > 1)
            {
                if (!int.TryParse(parameters[1], out numToSpawn))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Amount to spawn must be a number between {int.MinValue} - {int.MaxValue}.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            var idx = 2;

            if (hasLifespan)
            {
                if (parameters.Length > 2)
                {
                    if (!int.TryParse(parameters[2], out int _lifespan))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Lifespan must be a number between {int.MinValue} - {int.MaxValue}.", ChatMessageType.Broadcast));
                        return false;
                    }
                    else
                        lifespan = _lifespan;
                }
                else
                    lifespan = 3600;

                idx++;
            }

            if (parameters.Length > idx)
            {
                if (!int.TryParse(parameters[idx], out int _palette))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Palette must be number between {int.MinValue} - {int.MaxValue}.", ChatMessageType.Broadcast));
                    return false;
                }
                else
                    palette = _palette;

                idx++;
            }

            if (parameters.Length > idx)
            {
                if (!float.TryParse(parameters[idx], out float _shade))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Shade must be number between {float.MinValue} - {float.MaxValue}.", ChatMessageType.Broadcast));
                    return false;
                }
                else
                    shade = _shade;
            }

            return true;
        }

        /// <summary>
        /// Returns a weenie for a wcid or classname for /create, /createliveops, and /ci
        /// Performs some basic verifications for weenie types that are safe to spawn with these commands
        /// </summary>
        private static Weenie GetWeenieForCreate(Session session, string weenieDesc, bool forInventory = false)
        {
            Weenie weenie = null;

            if (uint.TryParse(weenieDesc, out var wcid))
                weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            else
                weenie = DatabaseManager.World.GetCachedWeenie(weenieDesc);

            if (weenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{weenieDesc} is not a valid weenie.", ChatMessageType.Broadcast));
                return null;
            }

            if (!VerifyCreateWeenieType(weenie.WeenieType))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot spawn {weenie.ClassName} because it is a {weenie.WeenieType}", ChatMessageType.Broadcast));
                return null;
            }

            if (forInventory && weenie.IsStuck())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot spawn {weenie.ClassName} in your inventory because it cannot be picked up", ChatMessageType.Broadcast));
                return null;
            }

            if (weenie.DisableCreate())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot spawn {weenie.ClassName} because it is restricted", ChatMessageType.Broadcast));
                return null;
            }

            return weenie;
        }

        public static bool VerifyCreateWeenieType(WeenieType weenieType)
        {
            switch (weenieType)
            {
                case WeenieType.Admin:
                case WeenieType.AI:
                case WeenieType.Allegiance:
                case WeenieType.BootSpot:
                case WeenieType.Channel:
                case WeenieType.CombatPet:
                case WeenieType.Deed:
                case WeenieType.Entity:
                case WeenieType.EventCoordinator:
                case WeenieType.Game:
                case WeenieType.GamePiece:
                case WeenieType.GScoreGatherer:
                case WeenieType.GScoreKeeper:
                case WeenieType.GSpellEconomy:
                case WeenieType.Hook:
                case WeenieType.House:
                case WeenieType.HousePortal:
                case WeenieType.HUD:
                case WeenieType.InGameStatKeeper:
                case WeenieType.LScoreKeeper:
                case WeenieType.LSpellEconomy:
                case WeenieType.Machine:
                case WeenieType.Pet:
                case WeenieType.ProjectileSpell:
                case WeenieType.Sentinel:
                case WeenieType.SlumLord:
                case WeenieType.SocialManager:
                case WeenieType.Storage:
                case WeenieType.Undef:
                case WeenieType.UNKNOWN__GUESSEDNAME32:

                    return false;

            }
            return true;
        }

        /// <summary>
        /// Attempts to spawn some # of weenies in the world for /create or /createliveops
        /// </summary>
        private static void TryCreateObject(Session session, Weenie weenie, int numToSpawn, int? palette = null, float? shade = null, int? lifespan = null)
        {
            var obj = CreateObjectForCommand(session, weenie);

            if (obj == null || numToSpawn < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"No object was created.", ChatMessageType.Broadcast));
                return;
            }

            var objs = new List<WorldObject>();

            if (numToSpawn == 1)
            {
                objs.Add(obj);
            }
            else
            {
                int maxNumToSpawn = 100; // Don't hang the server by letting folks spawn too much
                if (weenie.IsStackable() && obj.MaxStackSize != null)
                {
                    var fullStacks = numToSpawn / (int)obj.MaxStackSize;
                    if (fullStacks > maxNumToSpawn)
                    {
                        fullStacks = maxNumToSpawn;
                        numToSpawn = maxNumToSpawn * (int)obj.MaxStackSize;
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Tried to spawn too many stacks, reduced to {maxNumToSpawn}.", ChatMessageType.Broadcast));
                    }

                    var lastStackAmount = numToSpawn % (int)obj.MaxStackSize;
                    for (int i = 0; i < fullStacks; i++)
                    {
                        var stack = CreateObjectForCommand(session, weenie);
                        stack.SetStackSize(obj.MaxStackSize);
                        objs.Add(stack);
                    }
                    if (lastStackAmount > 0)
                    {
                        obj.SetStackSize(lastStackAmount);
                        objs.Add(obj);
                    }
                }
                else
                {
                    if (numToSpawn > maxNumToSpawn)
                    {
                        numToSpawn = maxNumToSpawn;
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Tried to spawn too many objects, reduced to {maxNumToSpawn}.", ChatMessageType.Broadcast));
                    }
                    // The number of weenies to spawn will be limited by the physics engine.
                    for (int i = 0; i < numToSpawn; i++)
                    {
                        objs.Add(CreateObjectForCommand(session, weenie));
                    }
                }
            }

            foreach (var w in objs)
            {
                if (palette != null)
                    w.PaletteTemplate = palette;

                if (shade != null)
                    w.Shade = shade;

                if (lifespan != null)
                    w.Lifespan = lifespan;

                if (!w.EnterWorld()) // If the last object failed to add to the landblock, don't keep trying
                {
                    break;
                }
            }

            if (numToSpawn > 1)
                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has created {numToSpawn} {obj.Name} (0x{obj.Guid:X8}) near {obj.Location.ToLOCString()}.");
            else
                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has created {obj.Name} (0x{obj.Guid:X8}) at {obj.Location.ToLOCString()}.");
        }

        public static Position LastSpawnPos;

        /// <summary>
        /// Creates WorldObjects from Weenies for /create, /createliveops, and /ci
        /// </summary>
        private static WorldObject CreateObjectForCommand(Session session, Weenie weenie)
        {
            var obj = WorldObjectFactory.CreateNewWorldObject(weenie);

            //if (obj.TimeToRot == null)
            //obj.TimeToRot = double.MaxValue;

            if (obj.WeenieType == WeenieType.Creature)
                obj.Location = session.Player.Location.InFrontOf(5f, true);
            else
            {
                obj.CreatedByAccountId = session.Player.Account.AccountId;

                var dist = Math.Max(2, obj.UseRadius ?? 2);

                obj.Location = session.Player.Location.InFrontOf(dist);
            }

            obj.Location.LandblockId = new LandblockId(obj.Location.GetCell());

            LastSpawnPos = obj.Location;

            return obj;
        }

        /// <summary>
        /// Creates a named object in the world
        /// </summary>
        [CommandHandler("createnamed", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 3,
            "Creates a named object in the world.", "<wcid or classname> <count> <name>")]
        public static void HandleCreateNamed(Session session, params string[] parameters)
        {
            var weenie = GetWeenieForCreate(session, parameters[0]);

            if (weenie == null)
                return;

            if (!int.TryParse(parameters[1], out int count))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"count must be an integer value", ChatMessageType.Broadcast));
                return;
            }

            if (count < 1 || count > ushort.MaxValue)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"count must be a between 1 and {ushort.MaxValue}", ChatMessageType.Broadcast));
                return;
            }

            var named = string.Join(" ", parameters, 2, parameters.Length - 2);

            WorldObject first = null;

            for (int i = 0; i < count; i++)
            {
                var obj = CreateObjectForCommand(session, weenie);

                if (obj == null)
                    return;

                if (first == null)
                    first = obj;

                obj.Name = named;

                obj.EnterWorld();
            }

            if (count == 1)
                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has created {first.Name} (0x{first.Guid:X8}) at {first.Location.ToLOCString()}.");
            else
                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has created {count}x {first.Name} at {first.Location.ToLOCString()}.");
        }

        /// <summary>
        /// Creates an object in your inventory
        /// </summary>
        [CommandHandler("ci", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Creates an object in your inventory.", "wclassid (string or number), Amount to Spawn (optional [default:1]), Palette (optional), Shade (optional)\n")]
        public static void HandleCI(Session session, params string[] parameters)
        {
            var weenie = GetWeenieForCreate(session, parameters[0], true);

            if (weenie == null)
                return;

            ushort stackSize = 0;
            int? palette = null;
            float? shade = null;

            if (parameters.Length > 1)
            {
                if (!ushort.TryParse(parameters[1], out stackSize) || stackSize == 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"stacksize must be number between 1 - {ushort.MaxValue}", ChatMessageType.Broadcast));
                    return;
                }
            }

            if (parameters.Length > 2)
            {
                if (!int.TryParse(parameters[2], out int _palette))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"palette must be number between {int.MinValue} - {int.MaxValue}", ChatMessageType.Broadcast));
                    return;
                }
                else
                    palette = _palette;
            }

            if (parameters.Length > 3)
            {
                if (!float.TryParse(parameters[3], out float _shade))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"shade must be number between {float.MinValue} - {float.MaxValue}", ChatMessageType.Broadcast));
                    return;
                }
                else
                    shade = _shade;
            }

            var obj = CreateObjectForCommand(session, weenie);

            if (obj == null)
            {
                // already sent an error message
                return;
            }

            if (stackSize != 0 && obj.MaxStackSize != null)
            {
                stackSize = Math.Min(stackSize, (ushort)obj.MaxStackSize);

                obj.SetStackSize(stackSize);
            }

            if (palette != null)
                obj.PaletteTemplate = palette;

            if (shade != null)
                obj.Shade = shade;

            session.Player.TryCreateInInventoryWithNetworking(obj);

            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has created {obj.Name} (0x{obj.Guid:X8}) x{obj.StackSize} in their inventory.");
        }

        [CommandHandler("crack", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0, "Cracks the most recently appraised locked target.", "[. open it too]")]
        public static void HandleCrack(Session session, params string[] parameters)
        {
            bool openIt = (parameters?.Length > 0 && parameters[0] == ".");
            bool notOK = false;
            if (session.Player.CurrentAppraisalTarget.HasValue)
            {
                var objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);
                var wo = session.Player.CurrentLandblock?.GetObject(objectId);
                if (wo is Lock @lock)
                {
                    var opening = openIt ? $" Opening {wo.WeenieType}." : "";
                    string lockCode = LockHelper.GetLockCode(wo);
                    int? resistLockpick = LockHelper.GetResistLockpick(wo);
                    UnlockResults res = UnlockResults.IncorrectKey;

                    if (!string.IsNullOrWhiteSpace(lockCode))
                    {
                        res = @lock.Unlock(session.Player.Guid.Full, null, lockCode);
                        ChatPacket.SendServerMessage(session, $"Crack {wo.WeenieType} via {lockCode} result: {res}.{opening}", ChatMessageType.Broadcast);
                    }
                    else if (resistLockpick.HasValue && resistLockpick > 0)
                    {
                        var difficulty = 0;
                        res = @lock.Unlock(session.Player.Guid.Full, (uint)(resistLockpick * 2), ref difficulty);
                        ChatPacket.SendServerMessage(session, $"Crack {wo.WeenieType} with skill {resistLockpick}*2 result: {res}.{opening}", ChatMessageType.Broadcast);
                    }
                    else
                        ChatPacket.SendServerMessage(session, $"The {wo.WeenieType} has no key code or lockpick difficulty.  Unable to crack it.{opening}", ChatMessageType.Broadcast);

                    if (openIt)
                    {
                        if (wo is Door woDoor)
                            woDoor.Open(session.Player.Guid);
                        else if (wo is Chest woChest)
                            ChatPacket.SendServerMessage(session, $"The {wo.WeenieType} cannot be opened because it is not implemented yet!", ChatMessageType.Broadcast);
                    }
                }
                else notOK = true;
            }
            else notOK = true;
            if (notOK) ChatPacket.SendServerMessage(session, "Appraise a locked target before using @crack", ChatMessageType.Broadcast);
        }

        /// <summary>
        /// Displays how much experience the last appraised creature is worth when killed.
        /// </summary>
        [CommandHandler("deathxp", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Displays how much experience the last appraised creature is worth when killed.")]
        public static void HandleDeathxp(Session session, params string[] parameters)
        {
            var creature = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (creature == null) return;

            CommandHandlerHelper.WriteOutputInfo(session, $"{creature.Name} XP: {creature.XpOverride}");
        }

        // de_n name, text
        [CommandHandler("de_n", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2, "Sends text to named player, formatted exactly as entered.", "<name>, <text>")]
        public static void Handlede_n(Session session, params string[] parameters)
        {
            // usage: @de_n name, text
            // usage: @direct_emote_name name, text
            // Sends text to named player, formatted exactly as entered, with no prefix of any kind.
            // @direct_emote_name - Sends text to named player, formatted exactly as entered.

            Handledirect_emote_name(session, parameters);
        }

        // direct_emote_name name, text
        [CommandHandler("direct_emote_name", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2, "Sends text to named player, formatted exactly as entered.", "<name>, <text>")]
        public static void Handledirect_emote_name(Session session, params string[] parameters)
        {
            // usage: @de_n name, text
            // usage: @direct_emote_name name, text
            // Sends text to named player, formatted exactly as entered, with no prefix of any kind.
            // @direct_emote_name - Sends text to named player, formatted exactly as entered.

            var args = string.Join(" ", parameters);
            if (!args.Contains(","))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"There was no player name specified.", ChatMessageType.Broadcast));
            }
            else
            {
                var split = args.Split(",");
                var playerName = split[0];
                var msg = string.Join(" ", parameters).Remove(0, playerName.Length + 2);

                var player = PlayerManager.GetOnlinePlayer(playerName);
                if (player != null)
                    player.SendMessage(msg);
                else
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName} is not online.", ChatMessageType.Broadcast));
            }
        }

        // de_s text
        [CommandHandler("de_s", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Sends text to selected player, formatted exactly as entered, with no prefix of any kind.", "<text>")]
        public static void Handlede_s(Session session, params string[] parameters)
        {
            // usage: @de_s text
            // usage: @direct_emote_select text
            // Sends text to selected player, formatted exactly as entered, with no prefix of any kind.
            // @direct_emote_select - Sends text to selected player, formatted exactly as entered.

            Handledirect_emote_select(session, parameters);
        }

        // direct_emote_select text
        [CommandHandler("direct_emote_select", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Sends text to selected player, formatted exactly as entered, with no prefix of any kind.", "<text>")]
        public static void Handledirect_emote_select(Session session, params string[] parameters)
        {
            // usage: @de_s text
            // usage: @direct_emote_select text
            // Sends text to selected player, formatted exactly as entered, with no prefix of any kind.
            // @direct_emote_select - Sends text to selected player, formatted exactly as entered.

            var objectId = ObjectGuid.Invalid;

            if (session.Player.HealthQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
            else if (session.Player.ManaQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
            else if (session.Player.CurrentAppraisalTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

            if (objectId == ObjectGuid.Invalid)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You must select a player to send them a message.", ChatMessageType.Broadcast));
                return;
            }

            var wo = session.Player.CurrentLandblock?.GetObject(objectId);

            if (wo is null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unable to locate what you have selected.", ChatMessageType.Broadcast));
            }
            else if (wo is Player player)
            {
                var msg = string.Join(" ", parameters);

                player.SendMessage(msg);
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot send text to {wo.Name} because it is not a player.", ChatMessageType.Broadcast));
            }
        }

        // dispel
        [CommandHandler("dispel", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Removes all enchantments from the player", "/dispel")]
        public static void HandleDispel(Session session, params string[] parameters)
        {

            if (parameters.Length > 0)
            {
                var player = PlayerManager.GetOnlinePlayer(parameters[0]);
                player.EnchantmentManager.DispelAllEnchantments();
                // remove all enchantments from equipped items for now
                foreach (var item in player.EquippedObjects.Values)
                    item.EnchantmentManager.DispelAllEnchantments();
            }
            else
            {
                session.Player.EnchantmentManager.DispelAllEnchantments();
                // remove all enchantments from equipped items for now
                foreach (var item in session.Player.EquippedObjects.Values)
                    item.EnchantmentManager.DispelAllEnchantments();
            }



        }

        // event
        [CommandHandler("event", AccessLevel.Developer, CommandHandlerFlag.None, 2,
            "Maniuplates the state of an event",
            "[ start | stop | disable | enable | clear | status ] (name)\n"
            + "@event clear < name > - clears event with name <name> or all events if you put in 'all' (All clears registered generators, <name> does not)\n"
            + "@event status <eventSubstring> - get the status of all registered events or get all of the registered events that have <eventSubstring> in the name.")]
        public static void HandleEvent(Session session, params string[] parameters)
        {
            // usage: @event start| stop | disable | enable name
            // @event clear < name > -clears event with name <name> or all events if you put in 'all' (All clears registered generators, <name> does not).
            // @event status<eventSubstring> - get the status of all registered events or get all of the registered events that have <eventSubstring> in the name.
            // @event - Maniuplates the state of an event.

            // TODO: output

            var eventCmd = parameters?[0].ToLower();

            var eventName = parameters?[1];

            switch (eventCmd)
            {
                case "start":
                    if (EventManager.StartEvent(eventName, session?.Player, null))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Event {eventName} started successfully.", ChatMessageType.Broadcast);
                        PlayerManager.BroadcastToAuditChannel(session?.Player, $"{(session != null ? session.Player.Name : "CONSOLE")} has started event {eventName}.");
                    }
                    else
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Unable to start event named {eventName} .", ChatMessageType.Broadcast));
                    break;
                case "stop":
                    if (EventManager.StopEvent(eventName, session?.Player, null))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Event {eventName} stopped successfully.", ChatMessageType.Broadcast);
                        PlayerManager.BroadcastToAuditChannel(session?.Player, $"{(session != null ? session.Player.Name : "CONSOLE")} has stopped event {eventName}.");
                    }
                    else
                        CommandHandlerHelper.WriteOutputInfo(session, $"Unable to stop event named {eventName} .", ChatMessageType.Broadcast);
                    break;
                case "disable":
                    break;
                case "enable":
                    break;
                case "clear":
                    break;
                case "status":
                    if (eventName != "all" && eventName != "")
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Event {eventName} - GameEventState.{EventManager.GetEventStatus(eventName)}", ChatMessageType.Broadcast);
                    }
                    break;
                default:
                    CommandHandlerHelper.WriteOutputInfo(session, "That is not a valid event command", ChatMessageType.Broadcast);
                    break;
            }
        }

        // fumble
        [CommandHandler("fumble", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleFumble(Session session, params string[] parameters)
        {
            // @fumble - Forces the selected target to drop everything they contain to the ground.

            var objectId = ObjectGuid.Invalid;

            if (session.Player.HealthQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
            else if (session.Player.ManaQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
            else if (session.Player.CurrentAppraisalTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

            if (objectId == ObjectGuid.Invalid)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You must select a player to force them to drop everything.", ChatMessageType.Broadcast));
                return;
            }

            var wo = session.Player.CurrentLandblock?.GetObject(objectId);

            if (wo is null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unable to locate what you have selected.", ChatMessageType.Broadcast));
            }
            else if (wo is Player player)
            {
                var items = new List<WorldObject>();
                var playerLoc = new Position(player.Location);

                foreach (var item in player.Inventory)
                {
                    if (player.TryRemoveFromInventoryWithNetworking(item.Key, out var worldObject, Player.RemoveFromInventoryAction.DropItem))
                        items.Add(worldObject);
                }

                foreach (var item in player.EquippedObjects)
                {
                    if (player.TryDequipObjectWithNetworking(item.Key.Full, out var worldObject, Player.DequipObjectAction.DropItem))
                        items.Add(worldObject);
                }

                player.SavePlayerToDatabase();

                foreach (var item in items)
                {
                    item.Location = new Position(playerLoc);
                    item.Location.PositionZ += .5f;
                    item.Placement = Placement.Resting;  // This is needed to make items lay flat on the ground.

                    // increased precision for non-ethereal objects
                    var ethereal = item.Ethereal;
                    item.Ethereal = true;

                    if (session.Player.CurrentLandblock?.AddWorldObject(item, session.Player.Location.Variation) ?? false)
                    {
                        item.Location.LandblockId = new LandblockId(item.Location.GetCell());

                        // try slide to new position
                        var transit = item.PhysicsObj.transition(item.PhysicsObj.Position, new Physics.Common.Position(item.Location), false);

                        if (transit != null && transit.SpherePath.CurCell != null)
                        {
                            item.PhysicsObj.SetPositionInternal(transit);

                            item.SyncLocation(session.Player.Location.Variation);

                            item.SendUpdatePosition(true);
                        }
                        item.Ethereal = ethereal;

                        // drop success
                        player.Session.Network.EnqueueSend(
                            new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, ObjectGuid.Invalid),
                            new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Wielder, ObjectGuid.Invalid),
                            new GameEventItemServerSaysMoveItem(player.Session, item),
                            new GameMessageUpdatePosition(item));

                        player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.DropItem));

                        item.EmoteManager.OnDrop(player);
                        item.SaveBiotaToDatabase();
                    }
                    else
                        log.Warn($"0x{item.Guid}:{item.Name} for player {player.Name} lost from fumble failure.");
                }
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot force {wo.Name} to drop everything because it is not a player.", ChatMessageType.Broadcast));
            }
        }

        // god
        [CommandHandler("god", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0,
            "Turns current character into a god!",
            "Sets attributes and skills to higher than max levels.\n"
            + "To return to a mortal state, use the /ungod command.\n"
            + "Use the /god command with caution. While unlikely, there is a possibility that the character that runs the command will not be able to return to normal or will end up in a state that is unrecoverable.")]
        public static void HandleGod(Session session, params string[] parameters)
        {
            // @god - Sets your own stats to a godly level.
            // need to save stats so that we can return with /ungod
            DatabaseManager.Shard.SaveBiota(session.Player.Biota, session.Player.BiotaDatabaseLock, result => DoGodMode(result, session));
        }

        private static void DoGodMode(bool playerSaved, Session session, bool exceptionReturn = false)
        {
            if (!playerSaved)
            {
                ChatPacket.SendServerMessage(session, "Error saving player. Godmode not available.", ChatMessageType.Broadcast);
                Console.WriteLine($"Player {session.Player.Name} tried to enter god mode but there was an error saving player. Godmode not available.");
                return;
            }

            var biota = session.Player.Biota;

            string godString = session.Player.GodState;

            if (!exceptionReturn)
            {
                // if godstate starts with 1, you are in godmode

                if (godString != null)
                {
                    if (godString.StartsWith("1"))
                    {
                        ChatPacket.SendServerMessage(session, "You are already a god.", ChatMessageType.Broadcast);
                        return;
                    }
                }

                string returnState = "1=";
                returnState += $"{DateTime.UtcNow}=";

                // need level 25, available skill credits 24
                returnState += $"24={session.Player.AvailableSkillCredits}=25={session.Player.Level}=";

                // need total xp 1, unassigned xp 2
                returnState += $"1={session.Player.TotalExperience}=2={session.Player.AvailableExperience}=";

                // need all attributes
                // 1 through 6 str, end, coord, quick, focus, self
                foreach (var kvp in biota.PropertiesAttribute)
                {
                    var att = kvp.Value;

                    if (kvp.Key > 0 && (int)kvp.Key <= 6)
                    {
                        returnState += $"{(int)kvp.Key}=";
                        returnState += $"{att.InitLevel}=";
                        returnState += $"{att.LevelFromCP}=";
                        returnState += $"{att.CPSpent}=";
                    }
                }

                // need all vitals
                // 1, 3, 5 H,S,M (2,4,6 are current values and are not stored since they will be maxed entering/exiting godmode)
                foreach (var kvp in biota.PropertiesAttribute2nd)
                {
                    var attSec = kvp.Value;

                    if ((int)kvp.Key == 1 || (int)kvp.Key == 3 || (int)kvp.Key == 5)
                    {
                        returnState += $"{(int)kvp.Key}=";
                        returnState += $"{attSec.InitLevel}=";
                        returnState += $"{attSec.LevelFromCP}=";
                        returnState += $"{attSec.CPSpent}=";
                        returnState += $"{attSec.CurrentLevel}=";
                    }
                }

                // need all skills
                foreach (var kvp in biota.PropertiesSkill)
                {
                    var sk = kvp.Value;

                    if (SkillHelper.ValidSkills.Contains(kvp.Key))
                    {
                        returnState += $"{(int)kvp.Key}=";
                        returnState += $"{sk.LevelFromPP}=";
                        returnState += $"{(uint)sk.SAC}=";
                        returnState += $"{sk.PP}=";
                        returnState += $"{sk.InitLevel}=";
                    }
                }

                // Check string is correctly formatted before altering stats
                // correctly formatted return string should have 240 entries
                // if the construction of the string changes - this will need to be updated to match
                if (returnState.Split("=").Length != 240)
                {
                    ChatPacket.SendServerMessage(session, "Godmode is not available at this time.", ChatMessageType.Broadcast);
                    Console.WriteLine($"Player {session.Player.Name} tried to enter god mode but there was an error with the godString length. (length = {returnState.Split("=").Length}) Godmode not available.");
                    return;
                }

                // save return state to db in property string
                session.Player.SetProperty(PropertyString.GodState, returnState);
                session.Player.SaveBiotaToDatabase();
            }

            // Begin Godly Stats Increase

            var currentPlayer = session.Player;
            currentPlayer.Level = 999;
            currentPlayer.AvailableExperience = 0;
            currentPlayer.AvailableSkillCredits = 0;
            currentPlayer.TotalExperience = 191226310247;
            currentPlayer.TotalExperienceDouble = 191226310247;

            var levelMsg = new GameMessagePrivateUpdatePropertyInt(currentPlayer, PropertyInt.Level, (int)currentPlayer.Level);
            var expMsg = new GameMessagePrivateUpdatePropertyInt64(currentPlayer, PropertyInt64.AvailableExperience, (long)currentPlayer.AvailableExperience);
            var expDoubleMsg = new GameMessagePrivateUpdatePropertyFloat(currentPlayer, PropertyFloat.TotalExperienceDouble, currentPlayer.TotalExperienceDouble.Value);
            var skMsg = new GameMessagePrivateUpdatePropertyInt(currentPlayer, PropertyInt.AvailableSkillCredits, (int)currentPlayer.AvailableSkillCredits);
            var totalExpMsg = new GameMessagePrivateUpdatePropertyInt64(currentPlayer, PropertyInt64.TotalExperience, (long)currentPlayer.TotalExperience);

            currentPlayer.Session.Network.EnqueueSend(levelMsg, expMsg, expDoubleMsg, skMsg, totalExpMsg);

            foreach (var s in currentPlayer.Skills)
            {
                currentPlayer.TrainSkill(s.Key, 0);
                currentPlayer.SpecializeSkill(s.Key, 0);
                var playerSkill = currentPlayer.Skills[s.Key];
                playerSkill.Ranks = 226;
                playerSkill.ExperienceSpent = 4100490438u;
                playerSkill.InitLevel = 5000;
                currentPlayer.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(currentPlayer, playerSkill));
            }

            foreach (var a in currentPlayer.Attributes)
            {
                var playerAttr = currentPlayer.Attributes[a.Key];
                playerAttr.StartingValue = 9809u;
                playerAttr.Ranks = 190u;
                playerAttr.ExperienceSpent = 4019438644u;
                currentPlayer.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(currentPlayer, playerAttr));
            }

            currentPlayer.SetMaxVitals();

            foreach (var v in currentPlayer.Vitals)
            {
                var playerVital = currentPlayer.Vitals[v.Key];
                playerVital.Ranks = 196u;
                playerVital.ExperienceSpent = 4285430197u;
                // my OCD will not let health/stam not be equal due to the endurance calc
                playerVital.StartingValue = (v.Key == PropertyAttribute2nd.MaxHealth) ? 94803u : 89804u;
                currentPlayer.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(currentPlayer, playerVital));
            }

            currentPlayer.PlayParticleEffect(PlayScript.LevelUp, currentPlayer.Guid);
            currentPlayer.PlayParticleEffect(PlayScript.BaelZharonSmite, currentPlayer.Guid);

            currentPlayer.SetMaxVitals();

            ChatPacket.SendServerMessage(session, "You are now a god!!!", ChatMessageType.Broadcast);
        }

        // ungod
        [CommandHandler("ungod", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0,
            "Returns character to a mortal state.",
            "Sets attributes and skills back to the values they were when you became a god.\n"
            + "If the command fails to revert your state it will default to godmode.\n"
            + "In the event of failure, contact a server administrator to sort it out.")]
        public static void HandleUngod(Session session, params string[] parameters)
        {
            // @ungod - Returns skills and attributues to pre-god levels.
            Player currentPlayer = session.Player;
            string returnString = session.Player.GodState;

            if (returnString == null)
            {
                ChatPacket.SendServerMessage(session, "Can't get any more ungodly than you already are...", ChatMessageType.Broadcast);
                return;
            }
            else
            {
                try
                {
                    string[] returnStringArr = returnString.Split("=");

                    // correctly formatted return string should have 240 entries
                    // if the construction of the string changes - this will need to be updated to match
                    if (returnStringArr.Length != 240)
                    {
                        Console.WriteLine($"The returnString was not set to the correct length while {currentPlayer.Name} was attempting to return to normal from godmode.");
                        ChatPacket.SendServerMessage(session, "Error returning to mortal state, defaulting to godmode.", ChatMessageType.Broadcast);
                        return;
                    }

                    for (int i = 2; i < returnStringArr.Length;)
                    {
                        switch (i)
                        {
                            case int n when (n <= 5):
                                currentPlayer.SetProperty((PropertyInt)uint.Parse(returnStringArr[i]), int.Parse(returnStringArr[i + 1]));
                                i += 2;
                                break;
                            case int n when (n <= 9):
                                currentPlayer.SetProperty((PropertyInt64)uint.Parse(returnStringArr[i]), long.Parse(returnStringArr[i + 1]));
                                i += 2;
                                break;
                            case int n when (n <= 33):
                                var playerAttr = currentPlayer.Attributes[(PropertyAttribute)uint.Parse(returnStringArr[i])];
                                playerAttr.StartingValue = uint.Parse(returnStringArr[i + 1]);
                                playerAttr.Ranks = uint.Parse(returnStringArr[i + 2]);
                                playerAttr.ExperienceSpent = uint.Parse(returnStringArr[i + 3]);
                                currentPlayer.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(currentPlayer, playerAttr));
                                i += 4;
                                break;
                            case int n when (n <= 48):
                                var playerVital = currentPlayer.Vitals[(PropertyAttribute2nd)int.Parse(returnStringArr[i])];
                                playerVital.StartingValue = uint.Parse(returnStringArr[i + 1]);
                                playerVital.Ranks = uint.Parse(returnStringArr[i + 2]);
                                playerVital.ExperienceSpent = uint.Parse(returnStringArr[i + 3]);
                                playerVital.Current = uint.Parse(returnStringArr[i + 4]);
                                currentPlayer.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(currentPlayer, playerVital));
                                i += 5;
                                break;
                            case int n when (n <= 238):
                                var playerSkill = currentPlayer.Skills[(Skill)int.Parse(returnStringArr[i])];
                                playerSkill.Ranks = ushort.Parse(returnStringArr[i + 1]);

                                // Handle god users stuck in god mode due to bad godstate with Enum string
                                SkillAdvancementClass advancement;
                                if (Enum.TryParse(returnStringArr[i + 2], out advancement))
                                {
                                    playerSkill.AdvancementClass = advancement;
                                }
                                else
                                {
                                    playerSkill.AdvancementClass = (SkillAdvancementClass)uint.Parse(returnStringArr[i + 2]);
                                }

                                playerSkill.ExperienceSpent = uint.Parse(returnStringArr[i + 3]);
                                playerSkill.InitLevel = uint.Parse(returnStringArr[i + 4]);
                                currentPlayer.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(currentPlayer, playerSkill));
                                i += 5;
                                break;
                            case 239: //end of returnString, this will need to be updated if the length of the string changes
                                GameMessagePrivateUpdatePropertyInt levelMsg = new GameMessagePrivateUpdatePropertyInt(currentPlayer, PropertyInt.Level, (int)currentPlayer.Level);
                                GameMessagePrivateUpdatePropertyInt skMsg = new GameMessagePrivateUpdatePropertyInt(currentPlayer, PropertyInt.AvailableSkillCredits, (int)currentPlayer.AvailableSkillCredits);
                                GameMessagePrivateUpdatePropertyInt64 totalExpMsg = new GameMessagePrivateUpdatePropertyInt64(currentPlayer, PropertyInt64.TotalExperience, (long)currentPlayer.TotalExperience);
                                GameMessagePrivateUpdatePropertyFloat totalExpDoubleMsg = new GameMessagePrivateUpdatePropertyFloat(currentPlayer, PropertyFloat.TotalExperienceDouble, currentPlayer.TotalExperienceDouble.Value);
                                GameMessagePrivateUpdatePropertyInt64 unassignedExpMsg = new GameMessagePrivateUpdatePropertyInt64(currentPlayer, PropertyInt64.AvailableExperience, (long)currentPlayer.AvailableExperience);
                                currentPlayer.Session.Network.EnqueueSend(levelMsg, skMsg, totalExpMsg, totalExpDoubleMsg, unassignedExpMsg);
                                i++;
                                break;
                            default:
                                // A warning that will alert on the console if the returnString length changes. This should suffice until a smoother way can be found.
                                Console.WriteLine($"Hit default case in /ungod command with i = {i}, did you change the length of the PropertyString.GodState array?");
                                i++;
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception ( { e.Source } - {e.Message} ) caught while {currentPlayer.Name} was attempting to return to normal from godmode.");
                    ChatPacket.SendServerMessage(session, "Error returning to mortal state, defaulting to godmode.", ChatMessageType.Broadcast);
                    DoGodMode(true, session, true);
                    return;
                }

                currentPlayer.SetMaxVitals();

                currentPlayer.RemoveProperty(PropertyString.GodState);

                currentPlayer.SaveBiotaToDatabase();

                currentPlayer.PlayParticleEffect(PlayScript.DispelAll, currentPlayer.Guid);

                ChatPacket.SendServerMessage(session, "You have returned from your godly state.", ChatMessageType.Broadcast);
            }
        }

        // magic god
        [CommandHandler("magic god", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleMagicGod(Session session, params string[] parameters)
        {
            // @magic god - Sets your magic stats to the specfied level.

            // TODO: output

            // output: You are now a magic god!!!

            ChatPacket.SendServerMessage(session, "You are now a magic god!!!", ChatMessageType.Broadcast);
        }


        [CommandHandler("modifyvital", AccessLevel.Developer, CommandHandlerFlag.None, 2, "Adjusts the maximum vital attribute for the last appraised mob/player and restores full vitals", "<Health|Stamina|Mana> <delta>")]
        public static void HandleModifyVital(Session session, params string[] parameters)
        {
            var lastAppraised = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (lastAppraised == null || !(lastAppraised is Creature))
            {
                ChatPacket.SendServerMessage(session, "The last appraised object was not a mob/NPC/player.", ChatMessageType.Broadcast);
                return;
            }
            var creature = lastAppraised as Creature;

            if (parameters.Length < 2)
            {
                ChatPacket.SendServerMessage(session, "Usage: modifyvital <Invalid vital type, valid values are: Health,Stamina,Mana", ChatMessageType.Broadcast);
                return;
            }

            // determine the vital type
            if (!Enum.TryParse(parameters[0], out PropertyAttribute2nd vitalAttr))
            {
                ChatPacket.SendServerMessage(session, "Invalid vital type, valid values are: Health,Stamina,Mana", ChatMessageType.Broadcast);
                return;
            }

            if (!ulong.TryParse(parameters[1], out ulong delta))
            {
                ChatPacket.SendServerMessage(session, "Invalid vital value, values must be valid integers", ChatMessageType.Broadcast);
                return;
            }

            PropertyAttribute2nd maxAttr;
            switch (vitalAttr)
            {
                case PropertyAttribute2nd.Health:
                    maxAttr = PropertyAttribute2nd.MaxHealth;
                    break;
                case PropertyAttribute2nd.Stamina:
                    maxAttr = PropertyAttribute2nd.MaxStamina;
                    break;
                case PropertyAttribute2nd.Mana:
                    maxAttr = PropertyAttribute2nd.MaxMana;
                    break;
                default:
                    ChatPacket.SendServerMessage(session, "Unexpected vital type, valid values are: Health,Stamina,Mana", ChatMessageType.Broadcast);
                    return;
            }

            CreatureVital maxVital = new CreatureVital(creature, maxAttr);
            maxVital.Ranks = (uint)Math.Clamp(maxVital.Ranks + delta, 1, uint.MaxValue);
            creature.UpdateVital(maxVital, maxVital.MaxValue);

            CreatureVital vital = new CreatureVital(creature, vitalAttr);
            vital.Ranks = (uint)Math.Clamp(vital.Ranks + delta, 1, uint.MaxValue);
            //creature.UpdateVital(vital, maxVital.MaxValue);

            if (creature is Player)
            {
                Player player = creature as Player;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, maxVital));
            }

            creature.SetMaxVitals();

            // save changes
            if (creature is Player || creature.IsDynamicThatShouldPersistToShard())
            {
                creature.SaveBiotaToDatabase();
            }
        }

        [CommandHandler("modifyskill", AccessLevel.Developer, CommandHandlerFlag.None, 2, "Adjusts the skill for the last appraised mob/player", "<skillName> <delta>")]
        public static void HandleModifySkill(Session session, params string[] parameters)
        {
            var lastAppraised = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (lastAppraised == null || !(lastAppraised is Creature))
            {
                ChatPacket.SendServerMessage(session, "The last appraised object was not a mob/NPC/player.", ChatMessageType.Broadcast);
                return;
            }
            var creature = lastAppraised as Creature;

            if (parameters.Length < 2)
            {
                ChatPacket.SendServerMessage(session, "Usage: modifyskill <skillName> <delta>: missing skillId and/or delta", ChatMessageType.Broadcast);
                return;
            }
            if (!Enum.TryParse(parameters[0], out Skill skill))
            {
                String[] names = Enum.GetNames(typeof(Skill));
                ChatPacket.SendServerMessage(session, "Invalid skillName, must be a valid skill name (without spaces, with capitalization), valid values are: " + String.Join(", ", names), ChatMessageType.Broadcast);
                return;
            }
            if (!Int32.TryParse(parameters[1], out int delta))
            {
                ChatPacket.SendServerMessage(session, "Invalid delta, must be a valid integer", ChatMessageType.Broadcast);
                return;
            }

            CreatureSkill creatureSkill = creature is Player ? creature.Skills[skill] : creature.GetCreatureSkill(skill);
            creatureSkill.InitLevel = (ushort)Math.Clamp(creatureSkill.InitLevel + delta, 0, (Int32)ushort.MaxValue);

            // save changes
            if (creature is Player || creature.IsDynamicThatShouldPersistToShard())
            {
                creature.SaveBiotaToDatabase();
            }
            if (creature is Player)
            {
                Player player = creature as Player;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, creatureSkill));
            }
        }

        [CommandHandler("modifyattr", AccessLevel.Developer, CommandHandlerFlag.None, 2, "Adjusts an attribute for the last appraised mob/NPC/player", "<attribute> <delta>")]
        public static void HandleModifyAttribute(Session session, params string[] parameters)
        {
            var lastAppraised = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (lastAppraised == null || !(lastAppraised is Creature))
            {
                ChatPacket.SendServerMessage(session, "The last appraised object was not a mob/NPC/player.", ChatMessageType.Broadcast);
                return;
            }
            var creature = lastAppraised as Creature;

            if (parameters.Length < 2)
            {
                ChatPacket.SendServerMessage(session, "Usage: modifyattr <attribute> <delta>: missing attribute name and/or delta", ChatMessageType.Broadcast);
                return;
            }
            if (!Enum.TryParse(parameters[0], out PropertyAttribute attrType))
            {
                ChatPacket.SendServerMessage(session, "Invalid skillName, must be a valid skill name (without spaces, with capitalization), valid values are: Strength,Endurance,Coordination,Quickness,Focus,Self", ChatMessageType.Broadcast);
                return;
            }
            if (!Int32.TryParse(parameters[1], out int delta))
            {
                ChatPacket.SendServerMessage(session, "Invalid delta, must be a valid integer", ChatMessageType.Broadcast);
                return;
            }

            CreatureAttribute attr = creature.Attributes[attrType];
            attr.StartingValue = (uint)Math.Clamp(attr.StartingValue + delta, 1, 9999);

            if (creature is Player || creature.IsDynamicThatShouldPersistToShard())
            {
                creature.SaveBiotaToDatabase();
            }
            if (creature is Player)
            {
                Player player = creature as Player;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, attr));
            }
        }

        [CommandHandler("setattrrank", AccessLevel.Developer, CommandHandlerFlag.None, 2, "Adjusts an attribute for the last appraised mob/NPC/player", "<attribute> <delta>")]
        public static void HandleSetAttributeRank(Session session, params string[] parameters)
        {
            var lastAppraised = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (lastAppraised == null || !(lastAppraised is Player))
            {
                ChatPacket.SendServerMessage(session, "The last appraised object was not a player.", ChatMessageType.Broadcast);
                return;
            }
            var player = lastAppraised as Player;

            if (parameters.Length < 2)
            {
                ChatPacket.SendServerMessage(session, "Usage: setattrrank <attribute> <value>: missing attribute name and/or value", ChatMessageType.Broadcast);
                return;
            }
            if (!Enum.TryParse(parameters[0], out PropertyAttribute attrType))
            {
                ChatPacket.SendServerMessage(session, "Invalid attribute name, must be a valid attribute name (without spaces, with capitalization), valid values are: Strength,Endurance,Coordination,Quickness,Focus,Self", ChatMessageType.Broadcast);
                return;
            }
            if (!UInt32.TryParse(parameters[1], out uint value))
            {
                ChatPacket.SendServerMessage(session, "Invalid value, must be a valid integer greater than 0", ChatMessageType.Broadcast);
                return;
            }

            CreatureAttribute attr = player.Attributes[attrType];
            player.SetAttributeRank(attr, value);
            player.SaveBiotaToDatabase();
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, attr));
        }

        // heal
        [CommandHandler("heal", AccessLevel.Envoy, CommandHandlerFlag.RequiresWorld, 0,
            "Heals yourself (or the selected creature)",
            "\n" + "This command fully restores your(or the selected creature's) health, mana, and stamina")]
        public static void HandleHeal(Session session, params string[] parameters)
        {
            // usage: @heal
            // This command fully restores your(or the selected creature's) health, mana, and stamina.
            // @heal - Heals yourself(or the selected creature).

            var objectId = ObjectGuid.Invalid;

            if (session.Player.HealthQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
            else if (session.Player.ManaQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
            else if (session.Player.CurrentAppraisalTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

            if (objectId == ObjectGuid.Invalid)
                objectId = session.Player.Guid;

            var wo = session.Player.CurrentLandblock?.GetObject(objectId);

            if (wo is null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unable to locate what you have selected.", ChatMessageType.Broadcast));
            }
            else if (wo is Player player)
            {
                player.SetMaxVitals();
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot heal {wo.Name} because it is not a player.", ChatMessageType.Broadcast));
            }
        }

        // housekeep
        [CommandHandler("housekeep", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleHousekeep(Session session, params string[] parameters)
        {
            // @housekeep[never { off | on}] -With no parameters, this command displays the housekeeping info for the selected item.With the 'never' flag, it sets the item to never housekeep, or turns that state off.
            // @housekeep - Queries or sets the housekeeping status for the selected item.

            // TODO: output
        }

        // idlist
        [CommandHandler("idlist", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Shows the next ID that will be allocated from GuidManager.")]
        public static void HandleIDlist(Session session, params string[] parameters)
        {
            // @idlist - Shows the next ID that will be allocated from SQL.

            var sysChatMsg = new GameMessageSystemChat(GuidManager.GetIdListCommandOutput(), ChatMessageType.WorldBroadcast);
            session.Network.EnqueueSend(sysChatMsg);
        }

        // gamecastlocalemote <message>
        [CommandHandler("gamecastlocalemote", AccessLevel.Developer, CommandHandlerFlag.None, 1,
            "Sends text to all players within chat range, formatted exactly as entered.",
            "<message>\n" +
            "Sends text to all players within chat range, formatted exactly as entered, with no prefix of any kind.\n" +
            "See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.")]
        public static void HandleGameCastLocalEmote(Session session, params string[] parameters)
        {
            // usage: @gamecastlocalemote<message>
            // Sends text to all players within chat range, formatted exactly as entered, with no prefix of any kind.
            // See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.
            // @gamecastlocalemote - Sends text to all players within chat range, formatted exactly as entered.

            // Since we only have one server, this command will just call the other one
            HandleGameCastEmote(session, parameters);
        }

        // location
        [CommandHandler("location", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleLocation(Session session, params string[] parameters)
        {
            // @location - Causes your current location to be continuously displayed on the screen.

            // TODO: output
        }

        // morph
        [CommandHandler("morph", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Morphs your bodily form into that of the specified creature. Be careful with this one!",
            "<wcid or weenie class name> [character name]")]
        public static void HandleMorph(Session session, params string[] parameters)
        {
            // @morph - Morphs your bodily form into that of the specified creature. Be careful with this one!

            var weenieDesc = parameters[0];

            Weenie weenie = null;

            if (uint.TryParse(weenieDesc, out var wcid))
                weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            else
                weenie = DatabaseManager.World.GetCachedWeenie(weenieDesc);

            if (weenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Weenie {weenieDesc} not found in database, unable to morph.", ChatMessageType.Broadcast));
                return;
            }

            if (weenie.WeenieType != WeenieType.Creature && weenie.WeenieType != WeenieType.Cow
                && weenie.WeenieType != WeenieType.Admin && weenie.WeenieType != WeenieType.Sentinel && weenie.WeenieType != WeenieType.Vendor
                && weenie.WeenieType != WeenieType.Pet && weenie.WeenieType != WeenieType.CombatPet)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Weenie {weenie.GetProperty(PropertyString.Name)} ({weenieDesc}) is of WeenieType.{Enum.GetName(typeof(WeenieType), weenie.WeenieType)} ({weenie.WeenieType}), unable to morph because that is not allowed.", ChatMessageType.Broadcast));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Morphing you into {weenie.GetProperty(PropertyString.Name)} ({weenieDesc})... You will be logged out.", ChatMessageType.Broadcast));

            var guid = GuidManager.NewPlayerGuid();

            var player = new Player(weenie, guid, session.AccountId);

            player.Biota.WeenieType = session.Player.WeenieType;

            var name = string.Join(' ', parameters.Skip(1));
            if (parameters.Length > 1)
            {
                name = name.TrimStart('+').TrimStart(' ').TrimEnd(' ');
                player.Name = name;
                player.Character.Name = name;
            }
            else
                name = weenie.GetProperty(PropertyString.Name);

            DatabaseManager.Shard.IsCharacterNameAvailable(name, isAvailable =>
            {
                if (!isAvailable)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"{name} is not available to use for the morphed character, try another name.", ChatMessageType.Broadcast);
                    return;
                }

                player.Location = session.Player.Location;

                player.Character.CharacterOptions1 = session.Player.Character.CharacterOptions1;
                player.Character.CharacterOptions2 = session.Player.Character.CharacterOptions2;

                if (weenie.PropertiesCreateList != null)
                {
                    var wearables = weenie.PropertiesCreateList.Where(x => x.DestinationType == DestinationType.Wield || x.DestinationType == DestinationType.WieldTreasure).ToList();
                    foreach (var wearable in wearables)
                    {
                        var weenieOfWearable = DatabaseManager.World.GetCachedWeenie(wearable.WeenieClassId);

                        if (weenieOfWearable == null)
                            continue;

                        var worldObject = WorldObjectFactory.CreateNewWorldObject(weenieOfWearable);

                        if (worldObject == null)
                            continue;

                        if (wearable.Palette > 0)
                            worldObject.PaletteTemplate = wearable.Palette;
                        if (wearable.Shade > 0)
                            worldObject.Shade = wearable.Shade;

                        worldObject.CalculateObjDesc();

                        player.TryEquipObject(worldObject, worldObject.ValidLocations ?? 0);
                    }

                    var containables = weenie.PropertiesCreateList.Where(x => x.DestinationType == DestinationType.Contain || x.DestinationType == DestinationType.Shop
                    || x.DestinationType == DestinationType.Treasure || x.DestinationType == DestinationType.ContainTreasure || x.DestinationType == DestinationType.ShopTreasure).ToList();
                    foreach (var containable in containables)
                    {
                        var weenieOfWearable = DatabaseManager.World.GetCachedWeenie(containable.WeenieClassId);

                        if (weenieOfWearable == null)
                            continue;

                        var worldObject = WorldObjectFactory.CreateNewWorldObject(weenieOfWearable);

                        if (worldObject == null)
                            continue;

                        if (containable.Palette > 0)
                            worldObject.PaletteTemplate = containable.Palette;
                        if (containable.Shade > 0)
                            worldObject.Shade = containable.Shade;

                        worldObject.CalculateObjDesc();

                        player.TryAddToInventory(worldObject);
                    }
                }

                player.GenerateNewFace();

                var possessions = player.GetAllPossessions();
                var possessedBiotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();
                foreach (var possession in possessions)
                    possessedBiotas.Add((possession.Biota, possession.BiotaDatabaseLock));

                DatabaseManager.Shard.AddCharacterInParallel(player.Biota, player.BiotaDatabaseLock, possessedBiotas, player.Character, player.CharacterDatabaseLock, null);

                PlayerManager.AddOfflinePlayer(player);
                session.Characters.Add(new Database.Models.Shard.LoginCharacter { Id = player.Character.Id, IsPlussed = player.IsPlussed, AccountId = player.Account.AccountId, Name = player.Name });

                session.LogOffPlayer();
            });
        }

        // qst
        [CommandHandler("qst", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Query, stamp, and erase quests on the targeted player",
            "(fellow) [list | bestow | erase]\n"
            + "qst list - List the quest flags for the targeted player\n"
            + "qst bestow - Stamps the specific quest flag on the targeted player. If this fails, it's probably because you spelled the quest flag wrong.\n"
            + "qst stamp - Stamps the specific quest flag on the targeted player the specified number of times. If this fails, it's probably because you spelled the quest flag wrong.\n"
            + "qst erase - Erase the specific quest flag from the targeted player. If no quest flag is given, it erases the entire quest table for the targeted player.\n")]
        public static void Handleqst(Session session, params string[] parameters)
        {
            // fellow bestow  stamp erase
            // @qst list[filter]-List the quest flags for the targeted player, if a filter is provided, you will only get quest flags back that have the filter as a substring of the quest name. (Filter IS case sensitive!)
            // @qst erase < quest flag > -Erase the specific quest flag from the targeted player.If no quest flag is given, it erases the entire quest table for the targeted player.
            // @qst erase fellow < quest flag > -Erase a fellowship quest flag.
            // @qst bestow < quest flag > -Stamps the specific quest flag on the targeted player.If this fails, it's probably because you spelled the quest flag wrong.
            // @qst - Query, stamp, and erase quests on the targeted player.
            if (parameters.Length == 0)
            {
                // todo: display help screen
                return;
            }

            var objectId = new ObjectGuid();

            if (session.Player.HealthQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
            else if (session.Player.ManaQueryTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
            else if (session.Player.CurrentAppraisalTarget.HasValue)
                objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

            var wo = session.Player.CurrentLandblock?.GetObject(objectId);

            if (wo != null && wo is Creature creature)
            {
                if (parameters[0].Equals("list"))
                {
                    var questsHdr = $"Quest Registry for {creature.Name} (0x{creature.Guid}):\n";
                    questsHdr += "================================================\n";
                    session.Player.SendMessage(questsHdr);

                    var quests = creature.QuestManager.GetQuests();

                    if (quests.Count == 0)
                    {
                        session.Player.SendMessage("No quests found.");
                        return;
                    }

                    foreach (var quest in quests)
                    {
                        var questEntry = "";
                        questEntry += $"Quest Name: {quest.QuestName}\nCompletions: {quest.NumTimesCompleted} | Last Completion: {quest.LastTimeCompleted} ({Common.Time.GetDateTimeFromTimestamp(quest.LastTimeCompleted).ToLocalTime()})\n";
                        var nextSolve = creature.QuestManager.GetNextSolveTime(quest.QuestName);

                        if (nextSolve == TimeSpan.MinValue)
                            questEntry += "Can Solve: Immediately\n";
                        else if (nextSolve == TimeSpan.MaxValue)
                            questEntry += "Can Solve: Never again\n";
                        else
                            questEntry += $"Can Solve: In {nextSolve:%d} days, {nextSolve:%h} hours, {nextSolve:%m} minutes and, {nextSolve:%s} seconds. ({(DateTime.UtcNow + nextSolve).ToLocalTime()})\n";

                        questEntry += "--====--\n";
                        session.Player.SendMessage(questEntry);
                    }
                    return;
                }

                if (parameters[0].Equals("bestow"))
                {
                    if (parameters.Length < 2)
                    {
                        // delete all quests?
                        // seems unsafe, maybe a confirmation?
                        return;
                    }
                    var questName = parameters[1];
                    if (creature.QuestManager.HasQuest(questName))
                    {
                        session.Player.SendMessage($"{creature.Name} already has {questName}");
                        return;
                    }

                    var canSolve = creature.QuestManager.CanSolve(questName);
                    if (canSolve)
                    {
                        creature.QuestManager.Update(questName);
                        session.Player.SendMessage($"{questName} bestowed on {creature.Name}");
                        return;
                    }
                    else
                    {
                        session.Player.SendMessage($"Couldn't bestow {questName} on {creature.Name}");
                        return;
                    }
                }

                if (parameters[0].Equals("erase"))
                {
                    if (parameters.Length < 2)
                    {
                        // delete all quests?
                        // seems unsafe, maybe a confirmation?
                        session.Player.SendMessage($"You must specify a quest to erase, if you want to erase all quests use the following command: /qst erase *");
                        return;
                    }
                    var questName = parameters[1];

                    if (questName == "*")
                    {
                        creature.QuestManager.EraseAll();
                        session.Player.SendMessage($"All quests erased.");
                        return;
                    }

                    if (!creature.QuestManager.HasQuest(questName))
                    {
                        session.Player.SendMessage($"{questName} not found.");
                        return;
                    }
                    creature.QuestManager.Erase(questName);
                    session.Player.SendMessage($"{questName} erased.");
                    return;
                }

                if (parameters[0].Equals("stamp"))
                {
                    var numCompletions = int.MinValue;

                    if (parameters.Length > 2 && !int.TryParse(parameters[2], out numCompletions))
                    {
                        session.Player.SendMessage($"{parameters[2]} is not a valid int");
                        return;
                    }
                    var questName = parameters[1];

                    if (numCompletions != int.MinValue)
                        creature.QuestManager.SetQuestCompletions(questName, numCompletions);
                    else
                        creature.QuestManager.Update(questName);

                    var quest = creature.QuestManager.GetQuest(questName);
                    if (quest != null)
                    {
                        var numTimesCompleted = quest.NumTimesCompleted;
                        session.Player.SendMessage($"{questName} stamped with {numTimesCompleted} completions.");
                        PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has added {questName} with {numTimesCompleted} completions to {creature.Name} ");
                    }
                    else
                    {
                        session.Player.SendMessage($"Couldn't stamp {questName} on {creature.Name}");
                    }
                    return;
                }

                if (parameters[0].Equals("bits"))
                {
                    if (parameters.Length < 2)
                    {
                        var msg = "@qst - Query, stamp, and erase quests on the targeted player\n";
                        msg += "Usage: @qst bits [on | off | show] <questname> <bits>\n";
                        msg += "qst bits on  - Stamps the specific quest flag on the targeted player with specified bits ON. If this fails, it's probably because you spelled the quest flag wrong.\n";
                        msg += "qst bits off - Stamps the specific quest flag on the targeted player with specified bits OFF. If this fails, it's probably because you spelled the quest flag wrong.\n";
                        msg += "qst bits show - List the specific quest flag bits for the targeted player.\n";
                        session.Player.SendMessage(msg);
                        return;
                    }

                    if (parameters[1].Equals("on"))
                    {
                        if (parameters.Length < 3)
                        {
                            session.Player.SendMessage($"You must specify bits to turn on or off.");
                            return;
                        }
                        if (parameters.Length < 2)
                        {
                            session.Player.SendMessage($"You must specify a quest to set its bits.");
                            return;
                        }

                        var questName = parameters[2];

                        var questBits = parameters[3];

                        if (!uint.TryParse(questBits.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? questBits[2..] : questBits, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var bits))
                        {
                            session.Player.SendMessage($"{parameters[3]} is not a valid hex number");
                            return;
                        }

                        if (creature.QuestManager.HasQuestBits(questName, (int)bits))
                        {
                            session.Player.SendMessage($"{creature.Name} already has set 0x{bits:X} bits to ON for {questName}");
                            return;
                        }

                        creature.QuestManager.SetQuestBits(questName, (int)bits);
                        session.Player.SendMessage($"{creature.Name} has set 0x{bits:X} bits to ON for {questName}");
                        return;
                    }

                    if (parameters[1].Equals("off"))
                    {
                        if (parameters.Length < 3)
                        {
                            session.Player.SendMessage($"You must specify bits to turn on or off.");
                            return;
                        }
                        if (parameters.Length < 2)
                        {
                            session.Player.SendMessage($"You must specify a quest to set its bits.");
                            return;
                        }

                        var questName = parameters[2];

                        var questBits = parameters[3];

                        if (!uint.TryParse(questBits.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? questBits[2..] : questBits, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var bits))
                        {
                            session.Player.SendMessage($"{parameters[3]} is not a valid uint");
                            return;
                        }

                        if (creature.QuestManager.HasNoQuestBits(questName, (int)bits))
                        {
                            session.Player.SendMessage($"{creature.Name} already has set 0x{bits:X} bits to OFF for {questName}");
                            return;
                        }

                        creature.QuestManager.SetQuestBits(questName, (int)bits, false);
                        session.Player.SendMessage($"{creature.Name} has set 0x{bits:X} bits to OFF for {questName}");
                        return;
                    }

                    if (parameters[1].Equals("show"))
                    {
                        if (parameters.Length < 2)
                        {
                            session.Player.SendMessage($"You must specify a quest to show its bits.");
                            return;
                        }

                        var questName = parameters[2];

                        var questsHdr = $"Quest Bits Registry for {creature.Name} (0x{creature.Guid}):\n";
                        questsHdr += "================================================\n";

                        var quest = creature.QuestManager.GetQuest(questName);

                        if (quest == null)
                        {
                            session.Player.SendMessage($"{questName} not found.");
                            return;
                        }

                        var maxSolves = QuestManager.GetMaxSolves(questName);
                        var maxSolvesBinary = Convert.ToString(maxSolves, 2);

                        var questEntry = "";
                        questEntry += $"Quest Name: {quest.QuestName}\n";
                        questEntry += $"Current Set Bits: 0x{quest.NumTimesCompleted:X}\n";
                        questEntry += $"Allowed Max Bits: 0x{maxSolves:X}\n";
                        questEntry += $"Last Set On: {quest.LastTimeCompleted} ({Common.Time.GetDateTimeFromTimestamp(quest.LastTimeCompleted).ToLocalTime()})\n";

                        //var nextSolve = creature.QuestManager.GetNextSolveTime(quest.QuestName);

                        //if (nextSolve == TimeSpan.MinValue)
                        //    questEntry += "Can Solve: Immediately\n";
                        //else if (nextSolve == TimeSpan.MaxValue)
                        //    questEntry += "Can Solve: Never again\n";
                        //else
                        //    questEntry += $"Can Solve: In {nextSolve:%d} days, {nextSolve:%h} hours, {nextSolve:%m} minutes and, {nextSolve:%s} seconds. ({(DateTime.UtcNow + nextSolve).ToLocalTime()})\n";

                        questEntry += $"-= Binary String Representation =-\n  C: {Convert.ToString(quest.NumTimesCompleted, 2).PadLeft(maxSolvesBinary.Length, '0')}\n  A: {Convert.ToString(maxSolves, 2)}\n";

                        questEntry += "--====--\n";
                        session.Player.SendMessage(questsHdr + questEntry);
                    }
                }

                if (parameters[0].Equals("fellow"))
                {
                    if (creature is Player player)
                    {
                        var fellowship = player.Fellowship;

                        if (fellowship == null)
                        {
                            session.Player.SendMessage($"Selected player {wo.Name} (0x{objectId}) is not in a fellowship.");
                            return;
                        }

                        if (parameters.Length < 2)
                        {
                            var msg = "@qst - Query, stamp, and erase quests on the targeted player\n";
                            msg += "Usage: @qst fellow [list | bestow | erase]\n";
                            msg += "qst fellow list - List the quest flags for the Fellowship of targeted player\n";
                            msg += "qst fellow bestow - Stamps the specific quest flag on the Fellowship of targeted player. If this fails, it's probably because you spelled the quest flag wrong.\n";
                            msg += "qst fellow stamp - Stamps the specific quest flag on the Fellowship of targeted player the specified number of times. If this fails, it's probably because you spelled the quest flag wrong.\n";
                            msg += "qst fellow erase - Erase the specific quest flag from the Fellowship of targeted player. If no quest flag is given, it erases the entire quest table for the Fellowship of targeted player.\n";
                            session.Player.SendMessage(msg);
                            return;
                        }

                        if (parameters[1].Equals("list"))
                        {
                            var questsHdr = $"Quest Registry for Fellowship of {creature.Name} (0x{creature.Guid}):\n";
                            questsHdr += "================================================\n";
                            session.Player.SendMessage(questsHdr);

                            var quests = fellowship.QuestManager.GetQuests();

                            if (quests.Count == 0)
                            {
                                session.Player.SendMessage("No quests found.");
                                return;
                            }

                            foreach (var quest in quests)
                            {
                                var questEntry = "";
                                questEntry += $"Quest Name: {quest.QuestName}\nCompletions: {quest.NumTimesCompleted} | Last Completion: {quest.LastTimeCompleted} ({Common.Time.GetDateTimeFromTimestamp(quest.LastTimeCompleted).ToLocalTime()})\n";
                                var nextSolve = fellowship.QuestManager.GetNextSolveTime(quest.QuestName);

                                if (nextSolve == TimeSpan.MinValue)
                                    questEntry += "Can Solve: Immediately\n";
                                else if (nextSolve == TimeSpan.MaxValue)
                                    questEntry += "Can Solve: Never again\n";
                                else
                                    questEntry += $"Can Solve: In {nextSolve:%d} days, {nextSolve:%h} hours, {nextSolve:%m} minutes and, {nextSolve:%s} seconds. ({(DateTime.UtcNow + nextSolve).ToLocalTime()})\n";

                                questEntry += "--====--\n";
                                session.Player.SendMessage(questEntry);
                            }
                            return;
                        }

                        if (parameters[1].Equals("bestow"))
                        {
                            if (parameters.Length < 3)
                            {
                                // delete all quests?
                                // seems unsafe, maybe a confirmation?
                                return;
                            }
                            var questName = parameters[2];
                            if (fellowship.QuestManager.HasQuest(questName))
                            {
                                session.Player.SendMessage($"Fellowship of {creature.Name} already has {questName}");
                                return;
                            }

                            var canSolve = fellowship.QuestManager.CanSolve(questName);
                            if (canSolve)
                            {
                                fellowship.QuestManager.Update(questName);
                                session.Player.SendMessage($"{questName} bestowed on Fellowship of {creature.Name}");
                                return;
                            }
                            else
                            {
                                session.Player.SendMessage($"Couldn't bestow {questName} on Fellowship of {creature.Name}");
                                return;
                            }
                        }

                        if (parameters[1].Equals("erase"))
                        {
                            if (parameters.Length < 3)
                            {
                                // delete all quests?
                                // seems unsafe, maybe a confirmation?
                                session.Player.SendMessage($"You must specify a quest to erase, if you want to erase all quests use the following command: /qst fellow erase *");
                                return;
                            }
                            var questName = parameters[2];

                            if (questName == "*")
                            {
                                fellowship.QuestManager.EraseAll();
                                session.Player.SendMessage($"All quests erased.");
                                return;
                            }

                            if (!fellowship.QuestManager.HasQuest(questName))
                            {
                                session.Player.SendMessage($"{questName} not found.");
                                return;
                            }
                            fellowship.QuestManager.Erase(questName);
                            session.Player.SendMessage($"{questName} erased.");
                            return;
                        }

                        if (parameters[1].Equals("stamp"))
                        {
                            var numCompletions = int.MinValue;

                            if (parameters.Length > 3 && !int.TryParse(parameters[3], out numCompletions))
                            {
                                session.Player.SendMessage($"{parameters[3]} is not a valid int");
                                return;
                            }
                            var questName = parameters[2];

                            if (numCompletions != int.MinValue)
                                fellowship.QuestManager.SetQuestCompletions(questName, numCompletions);
                            else
                                fellowship.QuestManager.Update(questName);

                            var quest = fellowship.QuestManager.GetQuest(questName);
                            if (quest != null)
                            {
                                var numTimesCompleted = quest.NumTimesCompleted;
                                session.Player.SendMessage($"{questName} stamped with {numTimesCompleted} completions.");
                            }
                            else
                            {
                                session.Player.SendMessage($"Couldn't stamp {questName} on {creature.Name}");
                            }
                            return;
                        }
                    }
                    else
                        session.Player.SendMessage($"Selected object {wo.Name} (0x{objectId}) is not a player and cannot have a fellowship.");
                }
            }
            else
            {
                if (wo == null)
                    session.Player.SendMessage($"Selected object (0x{objectId}) not found.");
                else
                    session.Player.SendMessage($"Selected object {wo.Name} (0x{objectId}) is not a creature.");
            }
        }

        // raise
        [CommandHandler("raise", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2)]
        public static void HandleRaise(Session session, params string[] parameters)
        {
            // @raise - Raises your experience (or the experience in a skill) by the given amount.

            // TODO: output
        }

        // rename <Current Name> <New Name>
        [CommandHandler("rename", AccessLevel.Envoy, CommandHandlerFlag.None, 2,
            "Rename a character. (Do NOT include +'s for admin names)",
            "< Current Name >, < New Name >")]
        public static void HandleRename(Session session, params string[] parameters)
        {
            // @rename <Current Name>, <New Name> - Rename a character. (Do NOT include +'s for admin names)

            if (!string.Join(" ", parameters).Contains(','))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Error, cannot rename. You must include the old name followed by a comma and then the new name.\n Example: @rename Old Name, New Name", ChatMessageType.Broadcast);
                return;
            }

            var names = string.Join(" ", parameters).Split(",");

            var oldName = names[0].TrimStart(' ').TrimEnd(' ');
            var newName = names[1].TrimStart(' ').TrimEnd(' ');

            if (oldName.StartsWith("+"))
                oldName = oldName.Substring(1);
            if (newName.StartsWith("+"))
                newName = newName.Substring(1);

            newName = newName.First().ToString().ToUpper() + newName.Substring(1);

            var onlinePlayer = PlayerManager.GetOnlinePlayer(oldName);
            var offlinePlayer = PlayerManager.GetOfflinePlayer(oldName);
            if (onlinePlayer != null)
            {
                DatabaseManager.Shard.IsCharacterNameAvailable(newName, isAvailable =>
                {
                    if (!isAvailable)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Error, a player named \"{newName}\" already exists.", ChatMessageType.Broadcast);
                        return;
                    }

                    onlinePlayer.Character.Name = newName;
                    onlinePlayer.CharacterChangesDetected = true;
                    onlinePlayer.Name = newName;
                    onlinePlayer.SavePlayerToDatabase();

                    CommandHandlerHelper.WriteOutputInfo(session, $"Player named \"{oldName}\" renamed to \"{newName}\" successfully!", ChatMessageType.Broadcast);

                    onlinePlayer.Session.LogOffPlayer();
                });
            }
            else if (offlinePlayer != null)
            {
                DatabaseManager.Shard.IsCharacterNameAvailable(newName, isAvailable =>
                {
                    if (!isAvailable)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Error, a player named \"{newName}\" already exists.", ChatMessageType.Broadcast);
                        return;
                    }

                    var character = DatabaseManager.Shard.BaseDatabase.GetCharacterStubByName(oldName);

                    DatabaseManager.Shard.GetCharacters(character.AccountId, false, result =>
                    {
                        var foundCharacterMatch = result.Where(c => c.Id == character.Id).FirstOrDefault();

                        if (foundCharacterMatch == null)
                        {
                            CommandHandlerHelper.WriteOutputInfo(session, $"Error, a player named \"{oldName}\" cannot be found.", ChatMessageType.Broadcast);
                        }

                        DatabaseManager.Shard.RenameCharacter(foundCharacterMatch, newName, new ReaderWriterLockSlim(), null);
                    });

                    offlinePlayer.SetProperty(PropertyString.Name, newName);
                    offlinePlayer.SaveBiotaToDatabase();

                    CommandHandlerHelper.WriteOutputInfo(session, $"Player named \"{oldName}\" renamed to \"{newName}\" successfully!", ChatMessageType.Broadcast);
                });
            }
            else
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Error, a player named \"{oldName}\" cannot be found.", ChatMessageType.Broadcast);
                return;
            }

        }

        // setadvclass
        [CommandHandler("setadvclass", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2)]
        public static void Handlesetadvclass(Session session, params string[] parameters)
        {
            // @setadvclass - Sets the advancement class of one of your own skills.

            // TODO: output
        }

        // spendxp
        [CommandHandler("spendxp", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2)]
        public static void HandleSpendxp(Session session, params string[] parameters)
        {
            // @spendxp - Allows you to more quickly spend your available xp into the specified skill.

            // TODO: output
        }

        // trainskill
        [CommandHandler("trainskill", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1)]
        public static void Handletrainskill(Session session, params string[] parameters)
        {
            // @trainskill - Attempts to train the specified skill by spending skill credits on it.

            // TODO: output
        }

        // reloadsysmsg
        [CommandHandler("reloadsysmsg", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void Handlereloadsysmsg(Session session, params string[] parameters)
        {
            // @reloadsysmsg - Causes all servers to reload system_messages.txt.

            // TODO: output
        }

        // gamecastlocal <message>
        [CommandHandler("gamecastlocal", AccessLevel.Developer, CommandHandlerFlag.None, 1,
            "Sends a server-wide broadcast.",
            "<message>\n" +
            "This command sends the specified text to every player on the current server.\n" +
            "See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.")]
        public static void HandleGameCastLocal(Session session, params string[] parameters)
        {
            // Local Server Broadcast from
            // usage: @gamecastlocal<message>
            // This command sends the specified text to every player on the current server.
            // See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.
            // @gamecastlocal Sends a server-wide broadcast.

            // Since we only have one server, this command will just call the other one
            HandleGamecast(session, parameters);
        }

        /// <summary>
        /// Sets whether you lose items should you die.
        /// </summary>
        [CommandHandler("sticky", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Sets whether you lose items should you die.", "<off/on>")]
        public static void HandleSticky(Session session, params string[] parameters)
        {
            bool sticky = !(parameters.Length > 0 && parameters[0] == "off");

            if (sticky)
                CommandHandlerHelper.WriteOutputInfo(session, "You will no longer drop any items on death.");
            else
                CommandHandlerHelper.WriteOutputInfo(session, "You will now drop items on death normally.");

            session.Player.NoCorpse = sticky;
        }

        // userlimit { num }
        [CommandHandler("userlimit", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1)]
        public static void Handleuserlimit(Session session, params string[] parameters)
        {
            // @userlimit - Sets how many clients are allowed to connect to this world.

            // TODO: output
        }

        // watchmen
        [CommandHandler("watchmen", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Displays a list of accounts with the specified level of admin access.",
            "(accesslevel)")]
        public static void Handlewatchmen(Session session, params string[] parameters)
        {
            // @watchmen - Displays a list of accounts with the specified level of admin access.

            var defaultAccessLevel = AccessLevel.Advocate;

            var accessLevel = defaultAccessLevel;

            if (parameters.Length > 0)
            {
                if (Enum.TryParse(parameters[0], true, out accessLevel))
                {
                    if (!Enum.IsDefined(typeof(AccessLevel), accessLevel))
                        accessLevel = defaultAccessLevel;
                }
            }

            var list = DatabaseManager.Authentication.GetListofAccountsByAccessLevel(accessLevel);
            var message = "";

            if (list.Count > 0)
            {
                message = $"The following accounts have been granted {accessLevel.ToString()} rights:\n";
                foreach (var item in list)
                    message += item + "\n";
            }
            else
            {
                message = $"There are no accounts with {accessLevel.ToString()} rights.";
            }

            CommandHandlerHelper.WriteOutputInfo(session, message, ChatMessageType.WorldBroadcast);
        }

        // gamecastemote <message>
        [CommandHandler("gamecastemote", AccessLevel.Developer, CommandHandlerFlag.None, 1,
            "Sends text to all players, formatted exactly as entered.",
            "<message>\n" +
            "See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.")]
        public static void HandleGameCastEmote(Session session, params string[] parameters)
        {
            // usage: "@gamecastemote <message>" or "@we <message"
            // Sends text to all players, formatted exactly as entered, with no prefix of any kind.
            // See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.
            // @gamecastemote - Sends text to all players, formatted exactly as entered.

            string msg = string.Join(" ", parameters);
            msg = msg.Replace("\\n", "\n");
            //session.Player.HandleActionWorldBroadcast($"{msg}", ChatMessageType.WorldBroadcast);

            GameMessageSystemChat sysMessage = new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast);
            DiscordChatManager.SendDiscordMessage("BROADCAST", msg, ConfigManager.Config.Chat.GeneralChannelId);
            DiscordChatManager.SendDiscordMessage("BROADCAST", msg, ConfigManager.Config.Chat.EventsChannelId);
            PlayerManager.BroadcastToAll(sysMessage);
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, session?.Player, msg);
        }

        // we <message>
        [CommandHandler("we", AccessLevel.Developer, CommandHandlerFlag.None, 1,
            "Sends text to all players, formatted exactly as entered.",
            "<message>\n" +
            "See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.")]
        public static void HandleWe(Session session, params string[] parameters)
        {
            // usage: "@gamecastemote <message>" or "@we <message"
            // Sends text to all players, formatted exactly as entered, with no prefix of any kind.
            // See Also: @gamecast, @gamecastemote, @gamecastlocal, @gamecastlocalemote.
            // @gamecastemote - Sends text to all players, formatted exactly as entered.

            HandleGameCastEmote(session, parameters);
        }

        // dumpattackers
        [CommandHandler("dumpattackers", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void Handledumpattackers(Session session, params string[] parameters)
        {
            // @dumpattackers - Displays the detection and enemy information for the selected creature.

            // TODO: output
        }

        // knownobjs
        [CommandHandler("knownobjs", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void Handleknownobjs(Session session, params string[] parameters)
        {
            // @knownobjs - Display a list of objects that the client is aware of.

            // TODO: output
        }

        // lbinterval
        [CommandHandler("lbinterval", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1)]
        public static void Handlelbinterval(Session session, params string[] parameters)
        {
            // @lbinterval - Sets how often in seconds the server farm will rebalance the server farm load.

            // TODO: output
        }

        // lbthresh
        [CommandHandler("lbthresh", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1)]
        public static void Handlelbthresh(Session session, params string[] parameters)
        {
            // the @lbthresh command sets the maximum amount of load servers can trade at each balance. Large load transfers at once can cause poor server performance.  (Large would be about 400, small is about 20.)
            // @lbthresh - Set how much load can be transferred between two servers during a single load balance.

            // TODO: output
        }

        // radar
        [CommandHandler("radar", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void Handleradar(Session session, params string[] parameters)
        {
            // @radar - Toggles your radar on and off.

            // TODO: output
        }

        // rares dump
        [CommandHandler("rares dump", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0)]
        public static void HandleRaresDump(Session session, params string[] parameters)
        {
            // @rares dump - Lists all tiers of rare items.

            // TODO: output
        }

        // stormnumstormed
        [CommandHandler("stormnumstormed", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1)]
        public static void Handlestormnumstormed(Session session, params string[] parameters)
        {
            // @stormnumstormed - Sets how many characters are teleported away during a portal storm.

            // TODO: output
        }

        // stormthresh
        [CommandHandler("stormthresh", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1)]
        public static void Handlestormthresh(Session session, params string[] parameters)
        {
            // @stormthresh - Sets how many character can be in a landblock before we do a portal storm.

            // TODO: output
        }

        [CommandHandler("showprops", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Displays the name of all properties configurable via the modify commands")]
        public static void HandleDisplayProps(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, PropertyManager.ListProperties());
        }

        [CommandHandler("modifybool", AccessLevel.Admin, CommandHandlerFlag.None, 2, "Modifies a server property that is a bool", "modifybool (string) (bool)")]
        public static void HandleModifyServerBoolProperty(Session session, params string[] parameters)
        {
            try
            {
                var boolVal = bool.Parse(parameters[1]);

                var prevState = PropertyManager.GetBool(parameters[0]);

                if (prevState.Item == boolVal && !string.IsNullOrWhiteSpace(prevState.Description))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Bool property is already {boolVal} for {parameters[0]}!");
                    return;
                }

                if (PropertyManager.ModifyBool(parameters[0], boolVal))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "Bool property successfully updated!");
                    PlayerManager.BroadcastToAuditChannel(session?.Player, $"Successfully changed server bool property {parameters[0]} to {boolVal}");

                    if (parameters[0] == "pk_server" || parameters[0] == "pkl_server")
                    {
                        PlayerManager.UpdatePKStatusForAllPlayers(parameters[0], boolVal);
                    }
                }
                else
                    CommandHandlerHelper.WriteOutputInfo(session, "Unknown bool property was not updated. Type showprops for a list of properties.");
            }
            catch (Exception)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Please input a valid bool", ChatMessageType.Help);
            }
        }

        [CommandHandler("fetchbool", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Fetches a server property that is a bool", "fetchbool (string)")]
        public static void HandleFetchServerBoolProperty(Session session, params string[] parameters)
        {
            var boolVal = PropertyManager.GetBool(parameters[0], cacheFallback: false);
            CommandHandlerHelper.WriteOutputInfo(session, $"{parameters[0]} - {boolVal.Description ?? "No Description"}: {boolVal.Item}");
        }

        [CommandHandler("modifylong", AccessLevel.Admin, CommandHandlerFlag.None, 2, "Modifies a server property that is a long", "modifylong (string) (long)")]
        public static void HandleModifyServerLongProperty(Session session, params string[] paramters)
        {
            try
            {
                var longVal = long.Parse(paramters[1]);
                if (PropertyManager.ModifyLong(paramters[0], longVal))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "Long property successfully updated!");
                    PlayerManager.BroadcastToAuditChannel(session?.Player, $"Successfully changed server long property {paramters[0]} to {longVal}");
                }
                else
                    CommandHandlerHelper.WriteOutputInfo(session, "Unknown long property was not updated. Type showprops for a list of properties.");
            }
            catch (Exception)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Please input a valid long", ChatMessageType.Help);
            }
        }

        [CommandHandler("fetchlong", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Fetches a server property that is a long", "fetchlong (string)")]
        public static void HandleFetchServerLongProperty(Session session, params string[] parameters)
        {
            var intVal = PropertyManager.GetLong(parameters[0], cacheFallback: false);
            CommandHandlerHelper.WriteOutputInfo(session, $"{parameters[0]} - {intVal.Description ?? "No Description"}: {intVal.Item}");
        }

        [CommandHandler("modifydouble", AccessLevel.Admin, CommandHandlerFlag.None, 2, "Modifies a server property that is a double", "modifyfloat (string) (double)")]
        public static void HandleModifyServerFloatProperty(Session session, params string[] parameters)
        {
            try
            {
                var doubleVal = double.Parse(parameters[1]);
                if (PropertyManager.ModifyDouble(parameters[0], doubleVal))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, "Double property successfully updated!");
                    PlayerManager.BroadcastToAuditChannel(session?.Player, $"Successfully changed server double property {parameters[0]} to {doubleVal}");
                }
                else
                    CommandHandlerHelper.WriteOutputInfo(session, "Unknown double property was not updated. Type showprops for a list of properties.");
            }
            catch (Exception)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Please input a valid double", ChatMessageType.Help);
            }
        }

        [CommandHandler("fetchdouble", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Fetches a server property that is a double", "fetchdouble (string)")]
        public static void HandleFetchServerFloatProperty(Session session, params string[] parameters)
        {
            var floatVal = PropertyManager.GetDouble(parameters[0], cacheFallback: false);
            CommandHandlerHelper.WriteOutputInfo(session, $"{parameters[0]} - {floatVal.Description ?? "No Description"}: {floatVal.Item}");
        }

        [CommandHandler("modifystring", AccessLevel.Admin, CommandHandlerFlag.None, 2, "Modifies a server property that is a string", "modifystring (string) (string)")]
        public static void HandleModifyServerStringProperty(Session session, params string[] parameters)
        {
            if (PropertyManager.ModifyString(parameters[0], parameters[1]))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "String property successfully updated!");
                PlayerManager.BroadcastToAuditChannel(session?.Player, $"Successfully changed server string property {parameters[0]} to {parameters[1]}");
            }
            else
                CommandHandlerHelper.WriteOutputInfo(session, "Unknown string property was not updated. Type showprops for a list of properties.");
        }

        [CommandHandler("fetchstring", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Fetches a server property that is a string", "fetchstring (string)")]
        public static void HandleFetchServerStringProperty(Session session, params string[] parameters)
        {
            var stringVal = PropertyManager.GetString(parameters[0], cacheFallback: false);
            CommandHandlerHelper.WriteOutputInfo(session, $"{parameters[0]} - {stringVal.Description ?? "No Description"}: {stringVal.Item}");
        }

        [CommandHandler("modifypropertydesc", AccessLevel.Admin, CommandHandlerFlag.None, 3, "Modifies a server property's description", "modifypropertydesc <STRING|BOOL|DOUBLE|LONG> (string) (string)")]
        public static void HandleModifyPropertyDescription(Session session, params string[] parameters)
        {
            var isSession = session != null;
            switch (parameters[0])
            {
                case "STRING":
                    PropertyManager.ModifyStringDescription(parameters[1], parameters[2]);
                    break;
                case "BOOL":
                    PropertyManager.ModifyBoolDescription(parameters[1], parameters[2]);
                    break;
                case "DOUBLE":
                    PropertyManager.ModifyDoubleDescription(parameters[1], parameters[2]);
                    break;
                case "LONG":
                    PropertyManager.ModifyLongDescription(parameters[1], parameters[2]);
                    break;
                default:
                    CommandHandlerHelper.WriteOutputInfo(session, "Please pick from STRING, BOOL, DOUBLE, or LONG", ChatMessageType.Help);
                    return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, "Successfully updated property description!", ChatMessageType.Help);
        }

        [CommandHandler("resyncproperties", AccessLevel.Admin, CommandHandlerFlag.None, "Resync the properties database")]
        public static void HandleResyncServerProperties(Session session, params string[] parameters)
        {
            PropertyManager.ResyncVariables();
        }

        [CommandHandler("iterate-allegiances", AccessLevel.Admin, CommandHandlerFlag.ConsoleInvoke, "Runs through all allegiances and outputs to console")]
        public static void HandleIterateAllegiances(Session session, params string[] parameters)
        {
            var players = PlayerManager.GetAllPlayers();
            foreach (var p in players)
            {
                if (p.PatronId != null)
                {
                    //CommandHandlerHelper.WriteOutputInfo(session, $"{p.Name}: getting allegiance, patron: {p.PatronId}");
                }
                else
                {
                    //CommandHandlerHelper.WriteOutputInfo(session, $"{p.Guid} has null patron");
                }


                var m = AllegianceManager.GetMonarch(p);
                CommandHandlerHelper.WriteOutputInfo(session, $"{m.Name}, {p.Guid}: Monarch");

                var a = AllegianceManager.GetAllegiance(p);

                
            }
        }

        [CommandHandler("fix-allegiances", AccessLevel.Admin, CommandHandlerFlag.ConsoleInvoke, "Fixes the monarch data for allegiances")]
        public static void HandleFixAllegiances(Session session, params string[] parameters)
        {
            var players = PlayerManager.GetAllPlayers();

            // build allegiances
            foreach (var player in players)
                AllegianceManager.GetAllegiance(player);

            foreach (var player in players.Where(i => i.MonarchId != null))
            {
                var monarchID = player.MonarchId.Value;

                // find multi allegiances
                foreach (var allegiance in AllegianceManager.Allegiances.Values)
                {
                    if (allegiance.MonarchId == monarchID)
                        continue;

                    if (allegiance.Members.TryGetValue(new ObjectGuid(monarchID), out var member))
                    {
                        var desynced = PlayerManager.FindByGuid(monarchID);
                        Console.WriteLine($"{player.Name} has references to {desynced.Name} as monarch, but should be {allegiance.Monarch.Player.Name} -- fixing");

                        player.MonarchId = allegiance.MonarchId;
                        player.SaveBiotaToDatabase();
                    }
                }

                // find missing players
                var monarch = PlayerManager.FindByGuid(player.MonarchId.Value);
                var _allegiance = AllegianceManager.GetAllegiance(monarch);

                if (_allegiance != null && !_allegiance.Members.ContainsKey(player.Guid))
                {
                    // walk patrons to get the updated monarch
                    var patron = PlayerManager.FindByGuid(player.PatronId.Value);
                    if (patron == null)
                    {
                        Console.WriteLine($"{player.Name} has references to deleted patron {player.PatronId.Value:X8}, checking for vassals");
                        player.PatronId = null;

                        var vassals = players.Where(i => i.PatronId != null && i.PatronId == player.Guid.Full).ToList();
                        if (vassals.Count > 0)
                        {
                            Console.WriteLine($"Vassals found, {player.Name} is the monarch");
                            player.MonarchId = player.Guid.Full;
                        }
                        else
                        {
                            Console.WriteLine($"No vassals found, removing patron reference to deleted character");
                            player.MonarchId = null;
                        }
                        player.SaveBiotaToDatabase();
                        continue;
                    }

                    while (patron.PatronId != null)
                        patron = PlayerManager.FindByGuid(patron.PatronId.Value);

                    if (player.MonarchId != patron.Guid.Full)
                    {
                        Console.WriteLine($"{player.Name} has references to {monarch.Name} as monarch, but should be {patron.Name} -- fixing missing player");

                        player.MonarchId = patron.Guid.Full;
                        player.SaveBiotaToDatabase();
                    }
                }
            }

            foreach (var allegiance in AllegianceManager.Allegiances.Values.ToList())
                AllegianceManager.Rebuild(allegiance);
        }

        [CommandHandler("show-allegiances", AccessLevel.Admin, CommandHandlerFlag.None, "Shows all of the allegiance chains on the server.")]
        public static void HandleShowAllegiances(Session session, params string[] parameters)
        {
            var players = PlayerManager.GetAllPlayers();

            // build allegiances
            foreach (var player in players)
                AllegianceManager.GetAllegiance(player);

            foreach (var allegiance in AllegianceManager.Allegiances.Values)
            {
                allegiance.ShowInfo();
                Console.WriteLine("---------------");
            }
        }

        [CommandHandler("getenchantments", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, "Shows the enchantments for the last appraised item")]
        public static void HandleGetEnchantments(Session session, params string[] parameters)
        {
            var item = CommandHandlerHelper.GetLastAppraisedObject(session);
            if (item == null) return;

            var enchantments = item.Biota.PropertiesEnchantmentRegistry.GetEnchantmentsTopLayer(item.BiotaDatabaseLock, SpellSet.SetSpells);

            foreach (var enchantment in enchantments)
            {
                var e = new Network.Structure.Enchantment(item, enchantment);
                var info = e.GetInfo();
                session.Network.EnqueueSend(new GameMessageSystemChat(info, ChatMessageType.Broadcast));
            }
        }

        // cm <material type> <quantity> <ave. workmanship>
        [CommandHandler("cm", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Create a salvage bag in your inventory", "<material_type>, optional: <structure> <workmanship> <num_items>")]
        public static void HandleCM(Session session, params string[] parameters)
        {
            // Format is: @cm <material type> <quantity> <ave. workmanship>
            HandleCISalvage(session, parameters);
        }

        [CommandHandler("cisalvage", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Create a salvage bag in your inventory", "<material_type>, optional: <structure> <workmanship> <num_items>")]
        public static void HandleCISalvage(Session session, params string[] parameters)
        {
            if (!Enum.TryParse(parameters[0], true, out MaterialType materialType))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find material type {parameters[0]}", ChatMessageType.Broadcast));
                return;
            }

            var wcid = (uint)Player.MaterialSalvage[(int)materialType];
            var salvageBag = WorldObjectFactory.CreateNewWorldObject(wcid);

            ushort structure = 100;
            if (parameters.Length > 1)
                ushort.TryParse(parameters[1], out structure);

            var workmanship = 10f;
            if (parameters.Length > 2)
                float.TryParse(parameters[2], out workmanship);

            var numItemsInMaterial = (int)Math.Round(workmanship);
            if (parameters.Length > 3)
                int.TryParse(parameters[3], out numItemsInMaterial);

            var itemWorkmanship = (int)Math.Round(workmanship * numItemsInMaterial);

            salvageBag.Name = $"Salvage ({structure})";
            salvageBag.Structure = structure;
            salvageBag.ItemWorkmanship = itemWorkmanship;
            salvageBag.NumItemsInMaterial = numItemsInMaterial;

            session.Player.TryCreateInInventoryWithNetworking(salvageBag);
        }

        [CommandHandler("setlbenviron", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Sets or clears your current landblock's environment option",
            "(name or id of EnvironChangeType)\nleave blank to reset to default.\nlist to get a complete list of options.")]
        public static void HandleSetLBEnviron(Session session, params string[] parameters)
        {
            EnvironChangeType environChange = EnvironChangeType.Clear;

            if (parameters.Length > 0)
            {
                if (parameters[0] == "list")
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(EnvironListMsg(), ChatMessageType.Broadcast));
                    return;
                }

                if (Enum.TryParse(parameters[0], true, out environChange))
                {
                    if (!Enum.IsDefined(typeof(EnvironChangeType), environChange))
                        environChange = EnvironChangeType.Clear;
                }
            }

            if (environChange.IsFog())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Setting Landblock (0x{session.Player.CurrentLandblock.Id.Landblock:X4}), including direct adjacent landblocks, to EnvironChangeType.{environChange.ToString()}.", ChatMessageType.Broadcast));
                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} set Landblock (0x{session.Player.CurrentLandblock.Id.Landblock:X4}), including direct adjacent landblocks, to EnvironChangeType.{environChange.ToString()}.");
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Sending EnvironChangeType.{environChange.ToString()} to all players on Landblock (0x{session.Player.CurrentLandblock.Id.Landblock:X4}), including direct adjacent landblocks.", ChatMessageType.Broadcast));
                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} sent EnvironChangeType.{environChange.ToString()} to all players on Landblock (0x{session.Player.CurrentLandblock.Id.Landblock:X4}), including direct adjacent landblocks.");
            }

            session.Player.CurrentLandblock?.DoEnvironChange(environChange);
        }

        [CommandHandler("setglobalenviron", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Sets or clears server's global environment option",
            "(name or id of EnvironChangeType)\nleave blank to reset to default.\nlist to get a complete list of options.")]
        public static void HandleSetGlobalEnviron(Session session, params string[] parameters)
        {
            EnvironChangeType environChange = EnvironChangeType.Clear;

            if (parameters.Length > 0)
            {
                if (parameters[0] == "list")
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(EnvironListMsg(), ChatMessageType.Broadcast));
                    return;
                }

                if (Enum.TryParse(parameters[0], true, out environChange))
                {
                    if (!Enum.IsDefined(typeof(EnvironChangeType), environChange))
                        environChange = EnvironChangeType.Clear;
                }
            }

            if (environChange.IsFog())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Setting all landblocks to EnvironChangeType.{environChange.ToString()} .", ChatMessageType.Broadcast));
                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} set all landblocks to EnvironChangeType.{environChange.ToString()} .");
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Sending EnvironChangeType.{environChange.ToString()} to all players on all Landblocks.", ChatMessageType.Broadcast));
                PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} sent EnvironChangeType.{environChange.ToString()} to all players on all Landblocks.");
            }

            LandblockManager.DoEnvironChange(environChange);
        }

        private static string EnvironListMsg()
        {
            var msg = "Complete list of EnvironChangeType:\n";
            foreach (var name in Enum.GetNames(typeof(EnvironChangeType)))
                msg += name + "\n";

            msg += "Notes about above list:\n";
            msg += "Clear resets to default.\nAll options ending with Fog are continuous.\nAll options ending with Fog2 are continuous and blank radar.\nAll options ending with Sound play once and do not repeat.";

            return msg;
        }

        [CommandHandler("movetome", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, "Moves the last appraised object to the current player location.")]
        public static void HandleMoveToMe(Session session, params string[] parameters)
        {
            var obj = CommandHandlerHelper.GetLastAppraisedObject(session);

            if (obj == null)
                return;

            if (obj.CurrentLandblock == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) is not a landblock object", ChatMessageType.Broadcast));
                return;
            }

            if (obj is Player)
            {
                HandleTeleToMe(session, new string[] { obj.Name });
                return;
            }

            var prevLoc = obj.Location;
            var newLoc = new Position(session.Player.Location);
            newLoc.Rotation = prevLoc.Rotation;     // keep previous rotation

            var setPos = new Physics.Common.SetPosition(newLoc.PhysPosition(), Physics.Common.SetPositionFlags.Teleport | Physics.Common.SetPositionFlags.Slide);
            var result = obj.PhysicsObj.SetPosition(setPos);

            if (result != Physics.Common.SetPositionError.OK)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to move {obj.Name} ({obj.Guid}) to current location: {result}", ChatMessageType.Broadcast));
                return;

            }
            session.Network.EnqueueSend(new GameMessageSystemChat($"Moving {obj.Name} ({obj.Guid}) to current location", ChatMessageType.Broadcast));

            obj.Location = obj.PhysicsObj.Position.ACEPosition();

            if (prevLoc.Landblock != obj.Location.Landblock)
            {
                LandblockManager.RelocateObjectForPhysics(obj, true);
            }

            obj.SendUpdatePosition(true);
        }

        [CommandHandler("reload-loot-tables", AccessLevel.Admin, CommandHandlerFlag.None, "reloads the latest data from the loot tables", "optional profile folder")]
        public static void HandleReloadLootTables(Session session, params string[] parameters)
        {
            var sep = Path.DirectorySeparatorChar;

            var folder = $"..{sep}..{sep}..{sep}..{sep}Factories{sep}Tables{sep}";
            if (parameters.Length > 0)
                folder = parameters[0];

            var di = new DirectoryInfo(folder);

            if (!di.Exists)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{folder} not found");
                return;
            }
            LootSwap.UpdateTables(folder);
        }

        [CommandHandler("roll", AccessLevel.Developer, CommandHandlerFlag.None, "Rolls a number between 1-100")]
        public static void HandleRaffleroll(Session session, params string[] parameters)
        {

            var roll = new Random().Next(1, 100);
            var msg = $"-=Tonight's raffle number is {roll}. Congratz to tonight's winner!=-";
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            DiscordChatManager.SendDiscordMessage("", msg, ConfigManager.Config.Chat.GeneralChannelId);
            DiscordChatManager.SendDiscordMessage("", msg, ConfigManager.Config.Chat.EventsChannelId);
            DiscordChatManager.SendDiscordMessage("", msg, ConfigManager.Config.Chat.RaffleChannelId);
        }

        [CommandHandler("sqc", AccessLevel.Developer, CommandHandlerFlag.None, "Shortcut for serverquestcompletions", "")]
        public static void HandleServerQuestCompletionsShort(Session session, params string[] parameters)
        {
            HandleServerQuestCompletions(session, parameters);
        }

        [CommandHandler("serverquestcompletions", AccessLevel.Developer, CommandHandlerFlag.None, "Get Total Completions of a Quest for all Characters, if the top parameter is passed will list top 25 player completion counts. If the player parameter is passed with a player name then it will list completions for that player", "<quest_name>, optional: top, player <player_name>")]
        public static void HandleServerQuestCompletions(Session session, params string[] parameters)
        {
            if (parameters.Length > 0)
            {
                var questName = parameters[0];
                if (parameters.Length == 1)
                {
                    var completions = ShardDatabase.GetServerQuestCompletions(questName);
                    session.Network.EnqueueSend(new GameMessageSystemChat($"The quest {questName} has been completed {completions} times.", ChatMessageType.Broadcast));
                }
                else
                {
                    if (string.Equals("Top", parameters[1], StringComparison.OrdinalIgnoreCase))
                    {
                        var list = ShardDatabase.GetTopQuestCompletions(questName);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Top 25 Completions for quest {questName}", ChatMessageType.Broadcast));
                            for (int i = 0; i < list.Count; i++)
                            {
                                session.Network.EnqueueSend(new GameMessageSystemChat($"{i + 1}: {list[i].Score:N0} - {list[i].Character}", ChatMessageType.Broadcast));
                            }
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"The quest {questName} has not been completed yet.", ChatMessageType.Broadcast));
                        }
                    }
                    else if (string.Equals("Player", parameters[1], StringComparison.OrdinalIgnoreCase))
                    {
                        if (parameters.Length < 3 || string.IsNullOrEmpty(parameters[2]))
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"You must specify a player name.", ChatMessageType.Broadcast));
                        }
                        else
                        {
                            var playerName = parameters[2];
                            var count = ShardDatabase.GetPlayerQuestCompletions(questName, playerName);
                            session.Network.EnqueueSend(new GameMessageSystemChat($"{questName} - {count} - {playerName}", ChatMessageType.Broadcast));
                        }
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid parameter '{parameters[1]}'. Use 'top' or 'player <player_name>'.", ChatMessageType.Broadcast));
                    }
                }
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"You must specify a quest name.", ChatMessageType.Broadcast));
            }
        }

    }
}
