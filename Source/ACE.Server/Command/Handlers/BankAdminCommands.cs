using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Staff bank commands (2026-11-20). Each works on a character online or offline, named by name or by id:
    /// <list type="bullet">
    /// <item>/bankwipe &lt;name or id&gt; shows the bank; /bankwipe confirm &lt;id&gt; sets every banked currency to 0.</item>
    /// <item>/banklock &lt;name or id&gt; freezes the bank completely until /bankunlock (BankLock.cs); /banklock alone lists the
    /// locked banks.</item>
    /// </list>
    /// Every change is saved at once and posted to the audit channel with the balances it found.
    /// </summary>
    public static class BankAdminCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [CommandHandler("bankwipe", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Wipes every banked currency of a character, online or offline, to 0. Shows the bank first; /bankwipe confirm <id> wipes it.",
            "<character name or id> - shows the bank\n/bankwipe confirm <character id> - wipes it to 0")]
        public static void HandleBankWipe(Session session, params string[] parameters)
        {
            // /bankwipe confirm <id>: only the character this staff member was just shown (BankWipePreviews). Player ids run in
            // sequence, so a mistyped id names another real character - it must never be wiped unseen.
            if (parameters.Length == 2 && parameters[0].Equals("confirm", StringComparison.OrdinalIgnoreCase)
                && CommandHandlerHelper.TryParseObjectId(parameters[1].Trim(), out var guid))
            {
                if (!BankWipePreviews.TryConsume(session.Player.Guid.Full, guid, DateTime.UtcNow))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Nothing wiped: you have not just been shown the bank of 0x{guid:X8}. Type /bankwipe <name or id> first, then confirm the id it shows within {BankWipePreviews.Lifetime.TotalMinutes:0} minutes.");
                    return;
                }

                var character = PlayerManager.FindByGuid(guid);   // online, else offline
                if (character == null)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"No character with id 0x{guid:X8} - nothing wiped.");
                    return;
                }

                Wipe(session, character);
                return;
            }

            // anything else names the character to show, a character called "Confirm ..." included
            if (!CommandHandlerHelper.TryGetCharacter(session, parameters, 0, out var target))
            {
                if (parameters[0].Equals("confirm", StringComparison.OrdinalIgnoreCase))
                    CommandHandlerHelper.WriteOutputInfo(session, "To wipe, confirm the id the bank was shown with: /bankwipe confirm 0x50001234");
                return;
            }

            var balances = BankLock.ReadStored(target);
            var coinFraction = target.GetProperty(PropertyFloat.PrestigeCoinFraction) ?? 0;

            var sb = new StringBuilder();
            sb.Append($"Bank of {Who(target)}{(BankLock.IsLocked(target) ? " - locked" : "")}:\n");
            foreach (var (name, property, amount) in balances)
            {
                sb.Append($"  {name}: {amount:N0}");
                if (property == PropertyInt64.BankedPrestigeCoins && coinFraction > 0)
                    sb.Append($" (and {coinFraction:0.00} of a coin in progress)");
                sb.Append('\n');
            }

            if (balances.All(b => b.Amount == 0) && coinFraction <= 0)
                sb.Append("It is empty - nothing to wipe.");
            else
            {
                BankWipePreviews.Remember(session.Player.Guid.Full, target.Guid.Full, DateTime.UtcNow);
                sb.Append($"To wipe all of it to 0, type within {BankWipePreviews.Lifetime.TotalMinutes:0} minutes: /bankwipe confirm 0x{target.Guid.Full:X8}");
            }

            CommandHandlerHelper.WriteOutputInfo(session, sb.ToString());
        }

        private static void Wipe(Session session, IPlayer character)
        {
            var before = BankLock.Wipe(character, out var coinFraction);

            var held = BankLock.Describe(before) + (coinFraction > 0 ? $" (and {coinFraction:0.00} of a Prestige Coin in progress)" : "");
            var line = $"wiped the bank of {Who(character)} to 0. It held: {held}.";

            CommandHandlerHelper.WriteOutputInfo(session, "You " + line);
            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} {line}");
            log.Info($"[BankWipe] {session.Player.Name} {line}");
        }

        [CommandHandler("banklock", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "Locks a character's bank, online or offline: nothing goes in or out, by any path, until /bankunlock. With no name, lists the locked banks.",
            "[character name or id]")]
        public static void HandleBankLock(Session session, params string[] parameters)
        {
            if (parameters.Length == 0)
            {
                ListLocked(session);
                return;
            }

            if (!CommandHandlerHelper.TryGetCharacter(session, parameters, 0, out var character))
                return;

            if (BankLock.IsLocked(character))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"The bank of {Who(character)} is already locked. /bankunlock unlocks it.");
                return;
            }

            BankLock.SetLocked(character, true);

            var line = $"locked the bank of {Who(character)}. Nothing goes in or out until /bankunlock. Frozen at: {BankLock.Describe(BankLock.ReadStored(character))}.";

            CommandHandlerHelper.WriteOutputInfo(session, "You " + line);
            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} {line}");
            log.Info($"[BankLock] {session.Player.Name} {line}");
        }

        [CommandHandler("bankunlock", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Unlocks a character's bank that /banklock locked, online or offline.",
            "<character name or id>")]
        public static void HandleBankUnlock(Session session, params string[] parameters)
        {
            if (!CommandHandlerHelper.TryGetCharacter(session, parameters, 0, out var character))
                return;

            if (!BankLock.IsLocked(character))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"The bank of {Who(character)} is not locked.");
                return;
            }

            BankLock.SetLocked(character, false);

            var line = $"unlocked the bank of {Who(character)}. It holds: {BankLock.Describe(BankLock.ReadStored(character))}.";

            CommandHandlerHelper.WriteOutputInfo(session, "You " + line);
            PlayerManager.BroadcastToAuditChannel(session.Player, $"{session.Player.Name} {line}");
            log.Info($"[BankLock] {session.Player.Name} {line}");
        }

        /// <summary>/banklock with no name: every character whose bank is locked, online or offline.</summary>
        private static void ListLocked(Session session)
        {
            var locked = PlayerManager.GetAllPlayers()
                .Where(BankLock.IsLocked)
                .OrderBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (locked.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "No bank is locked. /banklock <name> locks one.");
                return;
            }

            var sb = new StringBuilder($"Locked banks ({locked.Count}):\n");
            foreach (var character in locked)
                sb.Append($"  {Who(character)}\n");
            sb.Append("/bankunlock <name> unlocks one.");

            CommandHandlerHelper.WriteOutputInfo(session, sb.ToString());
        }

        /// <summary>"Name (0x50001234, online)", "(..., offline)" or "(..., offline, pending deletion)"</summary>
        private static string Who(IPlayer character)
        {
            return $"{character.Name} (0x{character.Guid.Full:X8}, {(character is Player ? "online" : CommandHandlerHelper.OfflineLabel(character))})";
        }
    }

    /// <summary>
    /// /bankwipe confirm only wipes the character its staff member was just shown. Player ids run in sequence, so a mistyped
    /// id would otherwise name another real character and wipe it unseen. One preview per staff member, good for
    /// <see cref="Lifetime"/>, used up by the wipe.
    /// </summary>
    internal static class BankWipePreviews
    {
        /// <summary>How long a preview can be confirmed.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

        private static readonly ConcurrentDictionary<uint, (uint Character, DateTime Until)> previews = new ConcurrentDictionary<uint, (uint Character, DateTime Until)>();

        /// <summary>Remembers that <paramref name="staff"/> was shown the bank of <paramref name="character"/>, in place of any earlier preview.</summary>
        public static void Remember(uint staff, uint character, DateTime nowUtc)
        {
            previews[staff] = (character, nowUtc + Lifetime);
        }

        /// <summary>True, and the preview used up, when <paramref name="staff"/>'s current preview is of <paramref name="character"/> and still good.</summary>
        public static bool TryConsume(uint staff, uint character, DateTime nowUtc)
        {
            if (!previews.TryGetValue(staff, out var preview) || preview.Character != character || nowUtc > preview.Until)
                return false;

            return previews.TryRemove(staff, out _);
        }
    }
}
