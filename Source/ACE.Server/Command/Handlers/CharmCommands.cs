using System;
using System.Collections.Generic;
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
        // ── Name aliases ─────────────────────────────────────────────────────
        // Maps any reasonable variant (with or without spaces, case-insensitive)
        // to the canonical command key used by CharmSettingsManager.
        private static readonly Dictionary<string, string> CharmAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["manabarrier"]       = "manabarrier",
            ["mana barrier"]      = "manabarrier",
            ["greater mana barrier"] = "manabarrier",
            ["master mana barrier"]  = "manabarrier",

            ["explosivearrow"]    = "explosivearrow",
            ["explosive arrow"]   = "explosivearrow",
            ["greater explosive arrow"] = "explosivearrow",
            ["master explosive arrow"]  = "explosivearrow",

            ["shrapnel"]          = "shrapnel",
            ["rocky shrapnel"]    = "shrapnel",

            ["agony"]             = "agony",

            ["pentacast"]         = "splitcast",
            ["penta cast"]        = "splitcast",
            ["splitcast"]         = "splitcast",
            ["split cast"]        = "splitcast",

            ["prismaticstrike"]   = "omnistrike",
            ["prismatic strike"]  = "omnistrike",
            ["prismatic"]         = "omnistrike",
            ["omnistrike"]        = "omnistrike",
            ["omni strike"]       = "omnistrike",
            ["omni"]              = "omnistrike",

            ["universalsummoning"]    = "universalsummoning",
            ["universal summoning"]   = "universalsummoning",
            ["universal summoning mastery"] = "universalsummoning",

            ["autorebuff"]            = "autorebuff",
            ["auto rebuff"]           = "autorebuff",
            ["auto-rebuff"]           = "autorebuff",
            ["auto rebuffing"]        = "autorebuff",

            ["infinitecasting"]       = "infinitecasting",
            ["infinite casting"]      = "infinitecasting",
            ["infinite casting stone"] = "infinitecasting",

            ["asheronsfavor"]         = "asheronsfavor",
            ["asherons favor"]        = "asheronsfavor",
            ["asheron's favor"]       = "asheronsfavor",

            ["artisans"]              = "artisans",
            ["artisan"]               = "artisans",
            ["artisans charm"]        = "artisans",
            ["artisan's charm"]       = "artisans",

            ["essencerefill"]         = "essencerefill",
            ["essence refill"]        = "essencerefill",
            ["summon essence refill"] = "essencerefill",
            ["pyreal refill"]         = "essencerefill",

            ["fork"]                  = "fork",
            ["farshot"]               = "farshot",
            ["far shot"]              = "farshot",
        };

        // Brief descriptions shown by /charms and /charm (no args)
        private static readonly string CharmList =
            "=== Available Charms ===\n" +
            "  • agony              — Tectonic (Bludgeon Ring) spells now cast Ring of Unspeakable Agony.\n" +
            "  • artisans           — Provides Crafting and Tinkering bonuses.\n" +
            "  • asheronsfavor      — Grants Health% and Natural Armor bonuses.\n" +
            "  • autorebuff         — Automatically re-applies expiring beneficial buffs.\n" +
            "  • essencerefill      — Pay pyreals to automatically refill empty summoning essence charges.\n" +
            "  • explosivearrow     — Arrows detonate on hit, dealing AOE blast damage to nearby enemies.\n" +
            "  • farshot            — Increases missile weapon attack range and final damage.\n" +
            "  • fork               — Spell projectiles fork to nearby enemies on hit.\n" +
            "  • infinitecasting    — Spell components are not consumed while spellcasting.\n" +
            "  • manabarrier        — Drains Mana before HP while taking damage.\n" +
            "  • splitcast          — Streak, Arc, and Bolt spells target multiple nearby enemies simultaneously.\n" +
            "  • omnistrike         — Melee attacks strike with the target's absolute weakest damage type.\n" +
            "  • shrapnel           — Tectonic (Bludgeon Ring) spells now cast Rocky Shrapnel.\n" +
            "  • universalsummoning — Bypasses summoning mastery skill checks (can use all pet types).\n" +
            "\nUse /charm <name> to see current settings and all adjustable keys.";


        // ── /charms ──────────────────────────────────────────────────────────
        [CommandHandler("charms", AccessLevel.Developer, CommandHandlerFlag.None, 0,
            "Dev: list all ability charms and what they do.",
            "Usage: /charms")]
        public static void HandleCharms(Session session, params string[] parameters)
        {
            Reply(session, CharmList);
        }

        // ── /charm ───────────────────────────────────────────────────────────
        [CommandHandler("charm", AccessLevel.Developer, CommandHandlerFlag.None, 0,
            "Dev: view or modify ability charm settings on the fly.",
            "Usage:\n" +
            "  /charm                           — list all charms\n" +
            "  /charm <name>                    — show current settings + all adjustable keys\n" +
            "  /charm <name> <key> <value>      — set a value (e.g. /charm manabarrier ratio 0.5)\n" +
            "  /charm <name> true|false|on|off  — enable or disable a charm\n" +
            "\nCharm names are flexible: 'manabarrier' or 'Mana Barrier' both work.")]
        public static void HandleCharm(Session session, params string[] parameters)
        {
            // ── /charm  (no args) ─────────────────────────────────────────────
            if (parameters.Length == 0)
            {
                Reply(session, CharmList);
                return;
            }

            // ── Resolve charm name (supports spaces: "Mana Barrier") ──────────
            // Try to match 1, 2, or 3 leading params as a charm name
            var charmName = ResolveCharmName(parameters, 0, out int consumed);
            if (charmName == null)
            {
                Reply(session, $"Unknown charm '{string.Join(" ", parameters)}'. Use /charms to see all available charms.");
                return;
            }

            var remaining = parameters.Skip(consumed).ToArray();

            // ── /charm <name>  (show values + help) ──────────────────────────
            if (remaining.Length == 0)
            {
                var dump = CharmSettingsManager.DumpCharm(charmName);
                var help = CharmSettingsManager.DumpHelp(charmName);
                Reply(session, "\u200B\n" + dump + "\n" + help);
                return;
            }

            var arg1 = remaining[0].ToLower();

            // ── /charm <name> true|false|on|off ──────────────────────────────
            if (remaining.Length == 1 && arg1 is "true" or "false" or "on" or "off")
            {
                var result = CharmSettingsManager.TrySet(charmName, "enabled", arg1, out _);
                if (result == null)
                {
                    Reply(session, $"Could not set enabled state for '{charmName}'.");
                    return;
                }

                var displayName = GetCharmDisplayName(charmName);
                var isEnabled = arg1 is "true" or "on";
                var action = isEnabled ? "activated" : "disabled";

                Broadcast(session, $"[CHARM] {displayName} charm has been {action}.");
                return;
            }

            // ── /charm <name> <key> <value> ───────────────────────────────────
            if (remaining.Length >= 2)
            {
                var key   = arg1;
                var value = remaining[1];

                var result = CharmSettingsManager.TrySet(charmName, key, value, out _);
                if (result == null)
                {
                    Reply(session, $"Unknown key '{key}' for '{charmName}'. Use /charm {charmName} to see valid keys.");
                    return;
                }
                Broadcast(session, $"[CHARM] {result}");
                return;
            }

            Reply(session, $"Usage: /charm {charmName} <key> <value>  — use /charm {charmName} to see valid keys.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Tries to resolve a charm name from one, two, or three consecutive params starting at offset.
        /// Returns the canonical key (e.g. "manabarrier") or null if not found.
        /// </summary>
        private static string ResolveCharmName(string[] parameters, int offset, out int consumed)
        {
            // Try longest match first (3 words, then 2, then 1)
            for (int len = Math.Min(3, parameters.Length - offset); len >= 1; len--)
            {
                var candidate = string.Join(" ", parameters.Skip(offset).Take(len));
                if (CharmAliases.TryGetValue(candidate, out var key))
                {
                    consumed = offset + len;
                    return key;
                }
            }
            consumed = offset + 1;
            return null;
        }

        private static void Reply(Session session, string msg)
        {
            if (session == null) { Console.WriteLine(msg); return; }
            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Broadcasts to in-game Audit channel, Discord audit channel, and server console.
        /// Also prints directly to general chat of the player who ran the command.
        /// </summary>
        private static void Broadcast(Session session, string msg)
        {
            PlayerManager.BroadcastToAuditChannel(session?.Player, msg);

            if (session != null)
                session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }

        private static string GetCharmDisplayName(string charmName)
        {
            return charmName.ToLower() switch
            {
                "manabarrier"        => "Mana Barrier",
                "explosivearrow"     => "Explosive Arrow",
                "shrapnel"           => "Rocky Shrapnel",
                "agony"              => "Ring of Agony",
                "pentacast"          => "Split Cast",
                "splitcast"          => "Split Cast",
                "prismaticstrike"    => "Omni Strike",
                "omnistrike"         => "Omni Strike",
                "autorebuff"         => "Auto-Rebuff",
                "infinitecasting"    => "Infinite Casting",
                "asheronsfavor"      => "Asheron's Favor",
                "artisans"           => "Artisan's",
                "essencerefill"      => "Summon Essence Refill",
                "universalsummoning" => "Universal Summoning",
                "fork"               => "Fork",
                "farshot"            => "Far Shot",
                _                    => charmName
            };
        }
    }
}
