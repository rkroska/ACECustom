using System;
using System.Net;
using System.Linq;
using System.Collections.Generic;

using log4net;

using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Managers;

namespace ACE.Server.Command.Handlers
{
    public static class AccountCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // accountcreate username password (accesslevel)
        [CommandHandler("accountcreate", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Creates a new account.",
            "username password (accesslevel)\n" +
            "accesslevel can be a number or enum name\n" +
            "0 = Player | 1 = Advocate | 2 = Sentinel | 3 = Envoy | 4 = Developer | 5 = Admin")]
        public static void HandleAccountCreate(Session session, params string[] parameters)
        {
            AccessLevel defaultAccessLevel = (AccessLevel)Common.ConfigManager.Config.Server.Accounts.DefaultAccessLevel;

            if (!Enum.IsDefined(typeof(AccessLevel), defaultAccessLevel))
                defaultAccessLevel = AccessLevel.Player;

            var accessLevel = defaultAccessLevel;

            if (parameters.Length > 2)
            {
                if (Enum.TryParse(parameters[2], true, out accessLevel))
                {
                    if (!Enum.IsDefined(typeof(AccessLevel), accessLevel))
                        accessLevel = defaultAccessLevel;
                }
            }

            string articleAorAN = "a";
            if (accessLevel == AccessLevel.Advocate || accessLevel == AccessLevel.Admin || accessLevel == AccessLevel.Envoy)
                articleAorAN = "an";

            string message = "";

            var accountExists = DatabaseManager.Authentication.GetAccountByName(parameters[0]);
                      
            if (accountExists != null)
            {
                message= "Account already exists. Try a new name.";
            }
            else
            {
                try
                {
                    var account = DatabaseManager.Authentication.CreateAccount(parameters[0].ToLower(), parameters[1], accessLevel, IPAddress.Parse("127.0.0.1"));

                    if (DatabaseManager.AutoPromoteNextAccountToAdmin && accessLevel == AccessLevel.Admin)
                        DatabaseManager.AutoPromoteNextAccountToAdmin = false;

                    message = ("Account successfully created for " + account.AccountName + " (" + account.AccountId + ") with access rights as " + articleAorAN + " " + Enum.GetName(typeof(AccessLevel), accessLevel) + ".");
                }
                catch
                {
                    message = "Account already exists. Try a new name.";
                }
            }

            CommandHandlerHelper.WriteOutputInfo(session, message, ChatMessageType.WorldBroadcast);
        }
  
        [CommandHandler("accountget", AccessLevel.Admin, CommandHandlerFlag.ConsoleInvoke, 1,
            "Gets an account.",
            "username")]
        public static void HandleAccountGet(Session session, params string[] parameters)
        {
            var account = DatabaseManager.Authentication.GetAccountByName(parameters[0]);
            Console.WriteLine($"User: {account.AccountName}, ID: {account.AccountId}");
        }

        // set-accountaccess accountname (accesslevel)
        [CommandHandler("set-accountaccess", AccessLevel.Admin, CommandHandlerFlag.None, 1, 
            "Change the access level of an account.", 
            "accountname (accesslevel)\n" +
            "accesslevel can be a number or enum name\n" +
            "0 = Player | 1 = Advocate | 2 = Sentinel | 3 = Envoy | 4 = Developer | 5 = Admin")]
        public static void HandleAccountUpdateAccessLevel(Session session, params string[] parameters)
        {
            string accountName  = parameters[0].ToLower();

            var accountId = DatabaseManager.Authentication.GetAccountIdByName(accountName);

            if (accountId == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Account " + accountName + " does not exist.", ChatMessageType.Broadcast);
                return;
            }

            AccessLevel accessLevel = AccessLevel.Player;

            if (parameters.Length > 1)
            {
                if (Enum.TryParse(parameters[1], true, out accessLevel))
                {
                    if (!Enum.IsDefined(typeof(AccessLevel), accessLevel))
                        accessLevel = AccessLevel.Player;
                }
            }

            string articleAorAN = "a";
            if (accessLevel == AccessLevel.Advocate || accessLevel == AccessLevel.Admin || accessLevel == AccessLevel.Envoy)
                articleAorAN = "an";

            if (accountId == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Account " + accountName + " does not exist.", ChatMessageType.Broadcast);
                return;
            }

            DatabaseManager.Authentication.UpdateAccountAccessLevel(accountId, accessLevel);

            if (DatabaseManager.AutoPromoteNextAccountToAdmin && accessLevel == AccessLevel.Admin)
                DatabaseManager.AutoPromoteNextAccountToAdmin = false;

            CommandHandlerHelper.WriteOutputInfo(session, "Account " + accountName + " updated with access rights set as " + articleAorAN + " " + Enum.GetName(typeof(AccessLevel), accessLevel) + ".", ChatMessageType.Broadcast);
        }

        // set-accountpassword accountname newpassword
        [CommandHandler("set-accountpassword", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Set the account password.",
            "accountname newpassword\n")]
        public static void HandleAccountSetPassword(Session session, params string[] parameters)
        {
            string accountName = parameters[0].ToLower();

            var account = DatabaseManager.Authentication.GetAccountByName(accountName);

            if (account == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Account " + accountName + " does not exist.", ChatMessageType.Broadcast);
                return;
            }

            if (parameters.Length < 1)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "You must specify a password for the account.", ChatMessageType.Broadcast);
                return;
            }

            account.SetPassword(parameters[1]);
            account.SetSaltForBCrypt();

            DatabaseManager.Authentication.UpdateAccount(account);

            CommandHandlerHelper.WriteOutputInfo(session, $"Account password for {accountName} successfully changed.", ChatMessageType.Broadcast);
        }

        /// <summary>
        /// Rate limiter for /passwd command
        /// </summary>
        private static readonly TimeSpan PasswdInterval = TimeSpan.FromSeconds(5);

        // passwd oldpassword newpassword
        [CommandHandler("passwd", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 2,
            "Change your account password.",
            "oldpassword newpassword\n")]
        public static void HandlePasswd(Session session, params string[] parameters)
        {
            if (session == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "This command is run from ingame client only", ChatMessageType.Broadcast);
                return;
            }

            log.Debug($"{session.Player.Name} is changing their password");

            var currentTime = DateTime.UtcNow;

            if (currentTime - session.LastPassTime < PasswdInterval)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"This command may only be run once every {PasswdInterval.TotalSeconds} seconds.", ChatMessageType.Broadcast);
                return;
            }
            session.LastPassTime = currentTime;

            if (parameters.Length <= 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "You must specify the current password for the account.", ChatMessageType.Broadcast);
                return;
            }

            if (parameters.Length < 1)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "You must specify a new password for the account.", ChatMessageType.Broadcast);
                return;
            }

            var account = DatabaseManager.Authentication.GetAccountById(session.AccountId);

            if (account == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Account {session.Account} ({session.AccountId}) wasn't found in the database! How are you in world without a valid account?", ChatMessageType.Broadcast);                
                return;
            }

            var oldpassword = parameters[0];
            var newpassword = parameters[1];

            if (account.PasswordMatches(oldpassword))
            {
                account.SetPassword(newpassword);
                account.SetSaltForBCrypt();
            }
            else
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Unable to change password: Password provided in first parameter does not match current account password for this account!", ChatMessageType.Broadcast);
                return;
            }


            DatabaseManager.Authentication.UpdateAccount(account);

            CommandHandlerHelper.WriteOutputInfo(session, "Account password successfully changed.", ChatMessageType.Broadcast);
        }

        // -----------------------------------------------------------------------
        // PREVIOUS /unstuck IMPLEMENTATION (preserved, no longer active)
        // Issues: AccessLevel.Developer (players couldn't use it), required account
        // name as argument, used IP matching as security gate (broke on VPN/NAT),
        // and called NetworkManager.RemoveSession() manually after Terminate() which
        // is redundant and unsafe. Replaced below with a corrected version.
        // -----------------------------------------------------------------------
        //
        // // Add a cooldown dictionary to track last unstuck usage per session
        // private static readonly Dictionary<uint, DateTime> UnstuckCooldowns = new Dictionary<uint, DateTime>();
        //
        // [CommandHandler("unstuck", AccessLevel.Developer, CommandHandlerFlag.None, 1,
        //     "Kicks all online players for the specified account if the IP matches the command issuer.",
        //     "accountname")]
        // public static void HandleUnstuck(Session session, params string[] parameters)
        // {
        //     // Cooldown check
        //     var now = DateTime.UtcNow;
        //     var sessionId = session?.Player?.Guid.Full ?? 0;
        //     lock (UnstuckCooldowns)
        //     {
        //         if (UnstuckCooldowns.TryGetValue(sessionId, out var lastUsed))
        //         {
        //             if ((now - lastUsed).TotalSeconds < 15)
        //             {
        //                 CommandHandlerHelper.WriteOutputInfo(session, $"/unstuck is on cooldown. Please wait {15 - (int)(now - lastUsed).TotalSeconds} seconds.", ChatMessageType.Broadcast);
        //                 return;
        //             }
        //         }
        //         UnstuckCooldowns[sessionId] = now;
        //     }
        //
        //     string accountName = parameters[0].ToLower();
        //
        //     var account = DatabaseManager.Authentication.GetAccountByName(accountName);
        //     if (account == null)
        //     {
        //         CommandHandlerHelper.WriteOutputInfo(session, "Account does not exist.", ChatMessageType.Broadcast);
        //         return;
        //     }
        //
        //     // Only target sessions for the account that are NOT the issuer's session
        //     var playersToKick = PlayerManager.GetAllOnline()
        //         .Where(p => p.Account != null
        //             && p.Account.AccountId == account.AccountId
        //             && p.Session != session) // Exclude the issuer's session
        //         .ToList();
        //
        //     if (playersToKick.Count == 0)
        //     {
        //         CommandHandlerHelper.WriteOutputInfo(session, "Account is not online.", ChatMessageType.Broadcast);
        //         return;
        //     }
        //
        //     // Check if the IP of the command issuer matches the IP of the target account's online session(s)
        //     var issuerIP = session?.EndPoint?.Address;
        //     var targetIPs = playersToKick.Select(p => p.Session?.EndPoint?.Address).Distinct().ToList();
        //     if (!targetIPs.Contains(issuerIP))
        //     {
        //         CommandHandlerHelper.WriteOutputInfo(session, "IP mismatch - failed to kick.", ChatMessageType.Broadcast);
        //         return;
        //     }
        //
        //     foreach (var player in playersToKick)
        //     {
        //         player.Session.Terminate(
        //             ACE.Server.Network.Enum.SessionTerminationReason.AccountBooted,
        //             new ACE.Server.Network.GameMessages.Messages.GameMessageBootAccount("! You have been kicked by /unstuck command."));
        //     }
        //
        //     // Capture the sessions to remove
        //     var sessionsToRemove = playersToKick.Select(p => p.Session).ToList();
        //
        //     System.Threading.Tasks.Task.Run(async () =>
        //     {
        //         await System.Threading.Tasks.Task.Delay(3000);
        //         foreach (var sessionToRemove in sessionsToRemove)
        //         {
        //             if (sessionToRemove != null)
        //             {
        //                 try
        //                 {
        //                     ACE.Server.Network.Managers.NetworkManager.RemoveSession(sessionToRemove);
        //                 }
        //                 catch (Exception ex)
        //                 {
        //                     // Log error but do not freeze server
        //                     log.Error($"Error removing session in /unstuck: {ex.Message}", ex);
        //                 }
        //             }
        //         }
        //     });
        //
        //     // Write to Audit channel
        //     if (session?.Player != null)
        //     {
        //         PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} has issued a stuck command for {accountName} - Verified by IP - KICKING");
        //     }
        //
        //     var kickedNames = string.Join(", ", playersToKick.Select(p => p.Name));
        //     CommandHandlerHelper.WriteOutputInfo(session, $"Unstuck: {playersToKick.Count} player(s) on account '{accountName}' have been kicked: {kickedNames}", ChatMessageType.Broadcast);
        // }
        // -----------------------------------------------------------------------

        /// <summary>
        /// Per-account cooldown tracker for @unstuck. Keyed by AccountId.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, DateTime> _unstuckCooldowns
            = new System.Collections.Concurrent.ConcurrentDictionary<uint, DateTime>();

        /// <summary>
        /// Per-account lock gates for @unstuck. Ensures the cooldown check, boot, and stamp
        /// are serialized per account so two concurrent calls cannot both pass the cooldown check.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, object> _unstuckLocks
            = new System.Collections.Concurrent.ConcurrentDictionary<uint, object>();

        private static readonly TimeSpan UnstuckCooldownDuration = TimeSpan.FromSeconds(30);

        [CommandHandler("unstuck", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
            "Boots any other stuck characters on your account so you can log back in to them.",
            "Usage: @unstuck\n" +
            "If another character on your account is stuck and preventing you from logging in,\n" +
            "log in to any other character and type @unstuck. The stuck character will be\n" +
            "disconnected and you will be able to log back in to them normally.\n" +
            "A 30-second cooldown applies after a successful boot. Checking when nothing is stuck is always free.")]
        public static void HandleUnstuck(Session session, params string[] parameters)
        {
            var accountId = session.AccountId;

            // Acquire a per-account gate so the cooldown check → boot → stamp sequence
            // is atomic. Without this, two concurrent @unstuck calls on the same account
            // could both pass the cooldown check before either stamps it.
            var gate = _unstuckLocks.GetOrAdd(accountId, _ => new object());
            lock (gate)
            {
                var now = DateTime.UtcNow;

                // --- Cooldown check ---
                if (_unstuckCooldowns.TryGetValue(accountId, out var lastUsed))
                {
                    var elapsed = now - lastUsed;
                    if (elapsed < UnstuckCooldownDuration)
                    {
                        var remaining = Math.Max(1, (int)Math.Ceiling((UnstuckCooldownDuration - elapsed).TotalSeconds));
                        CommandHandlerHelper.WriteOutputInfo(session,
                            $"Silas the Unsticker whispers: \"Easy there — I just helped you. Give it another {remaining} second{(remaining == 1 ? "" : "s")} before asking again.\"",
                            ChatMessageType.Broadcast);
                        return;
                    }
                }

                // NOTE: Cooldown is stamped ONLY after a real boot (below).
                // The "nothing found" path is a read-only no-op with negligible cost
                // and no reason to penalise the player for checking.

                // --- Find all OTHER sessions and player objects on this account ---
                // 1. Find sessions via NetworkManager to catch active or zombie network sessions.
                // 2. Find player objects via PlayerManager to catch session-less memory zombies.
                // The caller's own session/player is excluded — only other sessions/players on the account are booted.
                var sessionsToKick = ACE.Server.Network.Managers.NetworkManager.FindAllByAccount(accountId)
                    .Where(s => s != session)
                    .ToList();

                var playersToKick = PlayerManager.GetAllOnline()
                    .Where(p => p.Account != null && p.Account.AccountId == accountId && p != session.Player)
                    .ToList();

                if (sessionsToKick.Count == 0 && playersToKick.Count == 0)
                {
                    CommandHandlerHelper.WriteOutputInfo(session,
                        "Silas the Unsticker whispers: \"Hmm... I don't see any other characters or active sessions from your account stuck out there. You look fine to me!\"",
                        ChatMessageType.Broadcast);
                    return;
                }

                // session.Player is guaranteed non-null by CommandHandlerFlag.RequiresWorld.
                var callerName = session.Player.Name;

                var kickedNames = new List<string>(sessionsToKick.Count + playersToKick.Count);

                // First, terminate all active/zombie network sessions
                foreach (var stuckSession in sessionsToKick)
                {
                    // stuckSession.Player may be null for zombie sessions.
                    // Keep the raw name for the server log but use a neutral label in player-visible text.
                    var charName = stuckSession.Player?.Name;
                    var displayName = charName ?? "another login session";
                    log.Info($"[Unstuck] Booting session for '{charName ?? "[no character selected]"}' (Account: {session.Account ?? "[unknown]"}, AccountId: {accountId}) requested by '{callerName}'.");

                    stuckSession.Terminate(
                        ACE.Server.Network.Enum.SessionTerminationReason.AccountBooted,
                        new ACE.Server.Network.GameMessages.Messages.GameMessageBootAccount(
                            " - Freed by Silas the Unsticker at your request."));

                    kickedNames.Add(displayName);
                }

                // Second, clean up any session-less player objects (memory zombies)
                foreach (var stuckPlayer in playersToKick)
                {
                    // If this player was already handled by the session termination loop, skip it
                    if (stuckPlayer.Session != null && sessionsToKick.Contains(stuckPlayer.Session))
                        continue;

                    log.Info($"[Unstuck] Cleaning up session-less zombie player '{stuckPlayer.Name}' (Account: {session.Account ?? "[unknown]"}, AccountId: {accountId}) requested by '{callerName}'.");

                    // Force logoff the session-less player
                    stuckPlayer.ForcedLogOffRequested = true;
                    stuckPlayer.ForceLogoff();

                    kickedNames.Add(stuckPlayer.Name);
                }

                // Prune entries older than the cooldown window to keep the dictionary
                // bounded over long server uptimes. Filter first, then remove — avoids
                // a redundant TryGetValue inside the loop.
                var staleKeys = _unstuckCooldowns
                    .Where(kvp => (now - kvp.Value) > UnstuckCooldownDuration)
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var staleKey in staleKeys)
                    _unstuckCooldowns.TryRemove(staleKey, out _);
                _unstuckCooldowns[accountId] = now;

                // --- Audit log ---
                PlayerManager.BroadcastToAuditChannel(session.Player,
                    $"[Unstuck] {callerName} used @unstuck — booted {kickedNames.Count} session(s) on account '{session.Account ?? "[unknown]"}': {string.Join(", ", kickedNames)}");

                // --- Confirmation to the player ---
                var names = string.Join(", ", kickedNames);
                CommandHandlerHelper.WriteOutputInfo(session,
                    $"Silas the Unsticker whispers: \"Consider it done! I've sent {names} packing. They should be free to log back in now.\"",
                    ChatMessageType.Broadcast);
            }
        }
    }
}
