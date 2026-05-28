using System;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Command;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Developer command handler for on-the-fly charm tuning.
    /// All commands require AccessLevel.Developer.
    /// </summary>
    public static class CharmCommands
    {
        private static readonly string[] KnownCharms =
        {
            "manabarrier", "explosivearrow", "shrapnel", "agony",
            "pentacast", "prismaticstrike", "autorebuff"
        };

        // ── /charm ────────────────────────────────────────────────────────────────
        // Usage:
        //   /charm                              → show all settings
        //   /charm list                         → same
        //   /charm <name>                       → show settings for one charm
        //   /charm <name> <key> <value>         → set a numeric value
        //   /charm <name> true|false|on|off     → enable/disable a charm (2-arg form)
        //   /charm reset                        → reset ALL charms to defaults
        //   /charm reset <name>                 → reset one charm to defaults
        // ─────────────────────────────────────────────────────────────────────────

        [CommandHandler("charm", AccessLevel.Developer, CommandHandlerFlag.None, 0,
            "Dev: view or modify ability charm settings on the fly.",
            "Usage:\n" +
            "  /charm                           — show all charm settings\n" +
            "  /charm list                      — same\n" +
            "  /charm <name>                    — show settings for one charm\n" +
            "  /charm <name> <key> <value>      — set a value (e.g. /charm manabarrier slash 0.5)\n" +
            "  /charm <name> true|false|on|off  — enable or disable a charm\n" +
            "  /charm reset                     — reset ALL charms to defaults\n" +
            "  /charm reset <name>              — reset one charm to defaults\n" +
            "\nKnown charms: manabarrier, explosivearrow, shrapnel, agony, pentacast, prismaticstrike, autorebuff")]
        public static void HandleCharm(Session session, params string[] parameters)
        {
            var sub = parameters.Length > 0 ? parameters[0].ToLower() : "list";

            // ── /charm  or  /charm list ──────────────────────────────────────
            if (sub == "list" || parameters.Length == 0)
            {
                Reply(session, CharmSettingsManager.DumpAll());
                return;
            }

            // ── /charm reset [name] ──────────────────────────────────────────
            if (sub == "reset")
            {
                if (parameters.Length == 1)
                {
                    CharmSettingsManager.ResetAll();
                    Broadcast(session, "[CHARM] All charms reset to defaults.");
                }
                else
                {
                    var name = parameters[1].ToLower();
                    if (!CharmSettingsManager.TryReset(name))
                    {
                        Reply(session, $"Unknown charm '{name}'. Known: {string.Join(", ", KnownCharms)}");
                        return;
                    }
                    Broadcast(session, $"[CHARM] {name} reset to defaults.");
                }
                return;
            }

            // ── /charm <name> ────────────────────────────────────────────────
            var charmName = sub;
            if (parameters.Length == 1)
            {
                var dump = CharmSettingsManager.DumpCharm(charmName);
                if (dump == null)
                {
                    Reply(session, $"Unknown charm '{charmName}'. Known: {string.Join(", ", KnownCharms)}");
                    return;
                }
                Reply(session, dump);
                return;
            }

            // ── /charm <name> true|false|on|off  (2-arg enable/disable) ─────
            if (parameters.Length == 2)
            {
                var val = parameters[1].ToLower();
                if (val is "true" or "false" or "on" or "off")
                {
                    // Map to the block's TrySet: pass key=val, value="" (block handles bare key)
                    var result = CharmSettingsManager.TrySet(charmName, val, val, out var found);
                    if (!found)
                    {
                        Reply(session, $"Unknown charm '{charmName}'. Known: {string.Join(", ", KnownCharms)}");
                        return;
                    }
                    if (result == null)
                    {
                        Reply(session, $"'{val}' is not a valid setting for '{charmName}'.");
                        return;
                    }
                    Broadcast(session, $"[CHARM] {result}");
                    return;
                }

                Reply(session, $"Missing value. Usage: /charm {charmName} <key> <value>");
                return;
            }

            // ── /charm <name> <key> <value> ──────────────────────────────────
            if (parameters.Length >= 3)
            {
                var key   = parameters[1].ToLower();
                var value = parameters[2];

                var result = CharmSettingsManager.TrySet(charmName, key, value, out var found);
                if (!found)
                {
                    Reply(session, $"Unknown charm '{charmName}'. Known: {string.Join(", ", KnownCharms)}");
                    return;
                }
                if (result == null)
                {
                    Reply(session, $"Unknown key '{key}' for charm '{charmName}'. Use /charm {charmName} to see valid keys.");
                    return;
                }
                Broadcast(session, $"[CHARM] {result}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void Reply(Session session, string msg)
        {
            if (session == null)
            {
                Console.WriteLine(msg);
                return;
            }
            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Sends to the issuing dev AND all other online developers+.
        /// Also logs to server console for audit trail.
        /// </summary>
        private static void Broadcast(Session session, string msg)
        {
            Console.WriteLine($"[CHARM CMD] {msg}");

            foreach (var onlinePlayer in PlayerManager.GetAllOnline())
            {
                if (onlinePlayer.Session?.AccessLevel >= AccessLevel.Developer)
                    onlinePlayer.Session.Network.EnqueueSend(
                        new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
            }

            // If invoked from server console (session == null), we already wrote to Console above.
        }
    }
}
