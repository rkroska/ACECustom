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

            ["splitarrow"]            = "splitarrow",
            ["split arrow"]           = "splitarrow",
            ["splitarrows"]           = "splitarrow",
            ["split arrows"]          = "splitarrow",

            ["chain"]                 = "chain",

            ["pandemic"]              = "pandemic",

            ["improvedcleave"]        = "improvedcleave",
            ["improved cleave"]       = "improvedcleave",
            ["cleave"]                = "improvedcleave",

            ["armorbreaker"]          = "armorbreaker",
            ["armor breaker"]         = "armorbreaker",

            ["shieldarc"]             = "shieldarc",
            ["shield arc"]            = "shieldarc",
        };

        // Brief descriptions shown by /charms and /charm (no args)
        private static readonly string CharmList =
            "=== Available Charms ===\n" +
            "  - agony              - Tectonic (Bludgeon Ring) spells now cast Ring of Unspeakable Agony.\n" +
            "  - armorbreaker       - (level 1-100) Dual wield hits ignore a share of the armor left after Armor Rend.\n" +
            "  - artisans           - Provides Crafting and Tinkering bonuses.\n" +
            "  - asheronsfavor      - Grants Health% and Natural Armor bonuses.\n" +
            "  - autorebuff         - Automatically re-applies expiring beneficial buffs.\n" +
            "  - chain              - (level 1-100) War arcs, bolts and streaks jump from monster to monster.\n" +
            "  - essencerefill      - Pay pyreals to automatically refill empty summoning essence charges.\n" +
            "  - explosivearrow     - Arrows detonate on hit, dealing AOE blast damage to nearby enemies.\n" +
            "  - fork               - Spell projectiles fork to nearby enemies on hit.\n" +
            "  - improvedcleave     - (level 1-100) Melee cleave reaches further, all around, more targets.\n" +
            "  - infinitecasting    - Spell components are not consumed while spellcasting.\n" +
            "  - manabarrier        - Drains Mana before HP while taking damage.\n" +
            "  - pandemic           - (level 1-100) A ticking void DoT spreads to nearby monsters.\n" +
            "  - splitarrow         - (level 1-100) T11+ launchers fire extra arrows at nearby monsters.\n" +
            "  - splitcast          - Streak, Arc, and Bolt spells target multiple nearby enemies simultaneously.\n" +
            "  - omnistrike         - Melee attacks strike with the target's absolute weakest damage type.\n" +
            "  - shieldarc          - (level 1-100) Your shield guards a wider arc, and melee hits deal more with a shield.\n" +
            "  - shrapnel           - Tectonic (Bludgeon Ring) spells now cast Rocky Shrapnel.\n" +
            "  - universalsummoning - Bypasses summoning mastery skill checks (can use all pet types).\n" +
            "\nUse /charm <name> to see current settings and all adjustable keys.\n" +
            "Use /charm level <name> <1-100> to set the level of a levelled charm you carry.\n" +
            "Use /charm curve <name> to see the coin price of every level, and /charm coins <name> [set <N>] [player] to see or set the coins put in.";


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
            "  /charm                           - list all charms\n" +
            "  /charm <name>                    - show current settings + all adjustable keys\n" +
            "  /charm <name> <key> <value>      - set a value (e.g. /charm manabarrier ratio 0.5)\n" +
            "  /charm <name> true|false|on|off  - enable or disable a charm\n" +
            "  /charm level <name> <1-100>      - set the level of the levelled charm you carry (test tool)\n" +
            "  /charm curve <name>              - the Prestige Coin price of every level of a levelled charm\n" +
            "  /charm coins <name> [set <N>] [player] - coins put into a levelled charm (yours, or an online player's)\n" +
            "  /charm sync [help]               - every charm's settings as plugin wire lines\n" +
            "\nCharm names are flexible: 'manabarrier' or 'Mana Barrier' both work.")]
        public static void HandleCharm(Session session, params string[] parameters)
        {
            // ── /charm  (no args) ─────────────────────────────────────────────
            if (parameters.Length == 0)
            {
                Reply(session, CharmList);
                return;
            }

            // -- /charm sync [help]  (the client plugin's settings page) -------
            if (parameters[0].Equals("sync", StringComparison.OrdinalIgnoreCase))
            {
                var withHelp = parameters.Length > 1 && parameters[1].Equals("help", StringComparison.OrdinalIgnoreCase);
                foreach (var line in CharmSettingsManager.WireLines()) Reply(session, line);
                if (withHelp)
                    foreach (var block in CharmSettingsManager.LeveledBlocks.Values)
                        foreach (var line in block.HelpLines()) Reply(session, line);
                SendMyLevels(session);
                return;
            }

            // -- /charm level <name> <1-100>  (test tool) ----------------------
            if (parameters[0].Equals("level", StringComparison.OrdinalIgnoreCase))
            {
                HandleCharmLevel(session, parameters.Skip(1).ToArray());
                return;
            }

            // -- /charm curve <name>  (the coin price of every level) ----------
            if (parameters[0].Equals("curve", StringComparison.OrdinalIgnoreCase))
            {
                HandleCharmCurve(session, parameters.Skip(1).ToArray());
                return;
            }

            // -- /charm coins <name> [set <N>] [player]  (the coin ledger) -----
            if (parameters[0].Equals("coins", StringComparison.OrdinalIgnoreCase))
            {
                HandleCharmCoins(session, parameters.Skip(1).ToArray());
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
                Reply(session, dump + "\n" + help);
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
                SendWire(session, charmName);
                return;
            }

            // ── /charm <name> <key> <value> ───────────────────────────────────
            if (remaining.Length >= 2)
            {
                var key   = arg1;
                // the rest of the line: a schedule like "25:1, 50:1" may come with spaces (2026-10-17)
                var value = string.Join(" ", remaining.Skip(1));

                var result = CharmSettingsManager.TrySet(charmName, key, value, out _);
                if (result == null)
                {
                    Reply(session, $"Unknown key '{key}' for '{charmName}'. Use /charm {charmName} to see valid keys.");
                    return;
                }
                Broadcast(session, $"[CHARM] {result}");
                SendWire(session, charmName);

                // a new price curve re-levels everyone from the coins they have put in: the online players now, on their
                // own threads; the others at their next login (Player.SyncLevelledCharms)
                if (CharmSettingsManager.LeveledAbilityId(charmName) != 0 && CharmSettingsManager.LeveledCharmBlock.IsCurveKey(key))
                {
                    foreach (var online in PlayerManager.GetAllOnline())
                    {
                        var p = online;
                        LandblockManager.RunOnThreadFor(p, ACE.Server.Entity.Actions.ActionType.Charm_RefreshLevels, () => p.SyncLevelledCharms());
                    }
                    Reply(session, "Every online player's charm levels were worked out again from the coins they have put in.");
                }
                return;
            }

            Reply(session, $"Usage: /charm {charmName} <key> <value>  - use /charm {charmName} to see valid keys.");
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

        private static void SendWire(Session session, string charmName)
        {
            if (session == null) return;
            var line = CharmSettingsManager.WireLine(charmName);
            if (line != null) Reply(session, line);
        }

        /// <summary>The levels of the levelled charms the caller carries, for the plugin: name=level~active, 0 = none.</summary>
        private static void SendMyLevels(Session session)
        {
            var player = session?.Player;
            if (player == null) return;
            var sb = new System.Text.StringBuilder("[[ZCCHL]]");
            var first = true;
            foreach (var block in CharmSettingsManager.LeveledBlocks.Values)
            {
                var abilityId = CharmSettingsManager.LeveledAbilityId(block.Name);
                var item = FindCarriedCharm(player, abilityId);
                if (!first) sb.Append('|');
                first = false;
                sb.Append(block.Name).Append('=').Append(item != null ? Math.Max(1, player.GetCharmLevel(abilityId)) : 0).Append('~').Append(item != null && item.IsCharmActivated ? 1 : 0);
            }
            Reply(session, sb.ToString());
        }

        private static WorldObject FindCarriedCharm(Player player, int abilityId)
        {
            foreach (var item in player.GetAllPossessionsDeep())
                if (item.IsAbilityCharm && item.CharmGrantsAbility == abilityId) return item;
            return null;
        }

        /// <summary>/charm level &lt;name&gt; &lt;1-100&gt;: sets the caller's level of a levelled charm they carry (the level lives on
        /// the player; every copy carried shows it) and, when it is switched on, the level the ability uses at once. A test
        /// tool - players raise levels by putting Prestige Coins in with Charm Infusion Gems (Player_CharmLevels.cs).</summary>
        private static void HandleCharmLevel(Session session, string[] parameters)
        {
            var player = session?.Player;
            if (player == null) { Reply(session, "/charm level needs a character in the world."); return; }
            if (parameters.Length < 2) { Reply(session, "Usage: /charm level <name> <1-100>"); return; }

            var charmName = ResolveCharmName(parameters, 0, out int consumed);
            var abilityId = charmName != null ? CharmSettingsManager.LeveledAbilityId(charmName) : 0;
            if (abilityId == 0) { Reply(session, "Not a levelled charm. Levelled charms: " + string.Join(", ", CharmSettingsManager.LeveledBlocks.Keys) + "."); return; }
            if (consumed >= parameters.Length || !int.TryParse(parameters[consumed], out var level) || level < 1 || level > CharmSettingsManager.LeveledCharmBlock.MaxLevel)
            { Reply(session, $"The level must be a whole number from 1 to {CharmSettingsManager.LeveledCharmBlock.MaxLevel}."); return; }

            var item = FindCarriedCharm(player, abilityId);
            if (item == null) { Reply(session, $"You carry no {GetCharmDisplayName(charmName)} charm."); return; }

            player.SetCharmLevel(abilityId, level);
            Reply(session, $"{item.Name} is now level {level}" + (item.IsCharmActivated ? " (active - in use at once)." : " (switched off)."));
            Reply(session, CharmSettingsManager.DescribeAtLevel(charmName, level));
            SendMyLevels(session);
        }

        /// <summary>/charm curve &lt;name&gt;: the Prestige Coin price of a levelled charm's levels, from its live coinbase /
        /// coingrowth.</summary>
        private static void HandleCharmCurve(Session session, string[] parameters)
        {
            var charmName = parameters.Length > 0 ? ResolveCharmName(parameters, 0, out _) : null;
            var block = charmName != null && CharmSettingsManager.LeveledBlocks.TryGetValue(charmName, out var b) ? b : null;
            if (block == null) { Reply(session, "Usage: /charm curve <name>. Levelled charms: " + string.Join(", ", CharmSettingsManager.LeveledBlocks.Keys) + "."); return; }

            var sb = new System.Text.StringBuilder();
            sb.Append($"[{block.DisplayName}] coin price curve: coinbase {block.CoinBase:0.##}, coingrowth {block.CoinGrowth:0.####}\n");
            sb.Append("  To level   This level    Total coins\n");
            foreach (var level in new[] { 2, 5, 10, 15, 20, 25, 30, 40, 45, 50, 60, 70, 75, 80, 90, 95, 100 })
                sb.Append($"  {level,8}   {block.CoinsForLevel(level - 1),10:N0}   {block.CoinsToReach(level),12:N0}\n");
            sb.Append($"Level 100 needs {block.MaxCoins:N0} Prestige Coins in all.");
            Reply(session, sb.ToString());
        }

        /// <summary>/charm coins &lt;name&gt; [set &lt;N&gt;] [player]: shows or sets the Prestige Coins put into a levelled charm -
        /// the caller's, or an online player's. Setting them re-levels the charm at once (a test / fix tool).</summary>
        private static void HandleCharmCoins(Session session, string[] parameters)
        {
            const string usage = "Usage: /charm coins <name> [set <N>] [player name]";
            var consumed = 0;
            var charmName = parameters.Length > 0 ? ResolveCharmName(parameters, 0, out consumed) : null;
            var abilityId = charmName != null ? CharmSettingsManager.LeveledAbilityId(charmName) : 0;
            var block = abilityId != 0 ? CharmSettingsManager.LeveledBlockFor(abilityId) : null;
            if (block == null) { Reply(session, usage + ". Levelled charms: " + string.Join(", ", CharmSettingsManager.LeveledBlocks.Keys) + "."); return; }

            var rest = parameters.Skip(consumed).ToList();
            long? set = null;
            if (rest.Count > 0 && rest[0].Equals("set", StringComparison.OrdinalIgnoreCase))
            {
                if (rest.Count < 2 || !long.TryParse(rest[1].Replace(",", ""), out var n) || n < 0)
                { Reply(session, usage + " - N is a whole number of coins, 0 or more."); return; }
                set = n;
                rest = rest.Skip(2).ToList();
            }

            var who = rest.Count > 0 ? string.Join(" ", rest) : null;
            var target = who != null ? PlayerManager.GetOnlinePlayer(who) : session?.Player;
            if (target == null) { Reply(session, who != null ? $"{who} is not online." : "/charm coins needs a character in the world, or a player name."); return; }

            if (set.HasValue)
            {
                var value = set.Value;
                LandblockManager.RunOnThreadFor(target, ACE.Server.Entity.Actions.ActionType.Charm_RefreshLevels, () =>
                {
                    target.SetCharmCoins(abilityId, value);
                    target.SaveBiotaToDatabase();
                });
                var shown = Math.Clamp(value, 0, block.MaxCoins);
                Broadcast(session, $"[CHARM] {target.Name}: {block.DisplayName} coins set to {shown:N0} ({Player.CharmLevelText(block, shown)}).");
                return;
            }

            Reply(session, $"{target.Name}: {Player.CharmProgressText(block, target.GetCharmCoins(abilityId))}");
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
                "splitarrow"         => "Split Arrow",
                "chain"              => "Chain",
                "pandemic"           => "Pandemic",
                "improvedcleave"     => "Improved Cleave",
                "armorbreaker"       => "Armor Breaker",
                "shieldarc"          => "Shield Arc",
                _                    => charmName
            };
        }
    }
}
