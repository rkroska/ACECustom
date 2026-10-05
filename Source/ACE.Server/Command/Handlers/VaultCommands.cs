using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /vault (owner 2026-09-27): any player inside a Vaulted Dungeon sees how many other players are in it, then their
    /// names (at most 50, then "...and N more."). Their own name and staff are left out. A list can be had once every 30
    /// seconds per character; a refusal ("not in a Vaulted Dungeon") does not use it up. See RoomAssignManager.TryGetRoster.
    /// </summary>
    public static class VaultCommands
    {
        /// <summary>How often a character may get a list (owner 2026-09-27: 30 seconds), in milliseconds.</summary>
        internal const long CooldownMs = 30_000;

        /// <summary>Past this many remembered uses, the ones whose cooldown has run out are dropped.</summary>
        private const int CooldownPruneThreshold = 256;

        private const string Usage = "/vault  (inside a Vaulted Dungeon: who else is in it)";

        /// <summary>
        /// Character guid -> Environment.TickCount64 of its last list. TickCount64 never steps back, unlike the wall clock.
        /// Chat commands run one at a time on the world thread; a ConcurrentDictionary all the same, so a caller on any other
        /// thread (a future admin tool) cannot corrupt it.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, long> _lastUse = new ConcurrentDictionary<uint, long>();

        [CommandHandler("vault", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "In a Vaulted Dungeon: how many other players are inside, and who (at most 50 names; your own name and staff left out). Once every 30 seconds.",
            Usage)]
        public static void HandleVault(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null)
                return;

            if (parameters?.Length > 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Usage: " + Usage);
                return;
            }

            var now = Environment.TickCount64;
            var guid = player.Guid.Full;
            var wait = _lastUse.TryGetValue(guid, out var last) ? WaitSeconds(last, now) : 0;
            if (wait > 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"You can use /vault again in {wait} second{(wait == 1 ? "" : "s")}.");
                return;
            }

            if (!RoomAssignManager.TryGetRoster(player, out var dungeonName, out var others))
            {
                // Cheap (no player scan) and not a use: walking in and asking again straight away works.
                CommandHandlerHelper.WriteOutputInfo(session, "You are not in a Vaulted Dungeon.");
                return;
            }

            _lastUse[guid] = now;
            if (_lastUse.Count > CooldownPruneThreshold)
                foreach (var entry in _lastUse)
                    if (WaitSeconds(entry.Value, now) == 0)
                        _lastUse.TryRemove(new KeyValuePair<uint, long>(entry.Key, entry.Value));   // only if not used again since

            foreach (var line in RoomAssignManager.RosterLines(dungeonName, others))
                CommandHandlerHelper.WriteOutputInfo(session, line);
        }

        /// <summary>
        /// Whole seconds until a character whose last list was at <paramref name="lastUseMs"/> may have another, or 0 when it
        /// may now (at exactly CooldownMs). Rounded up and never 0 while waiting (RoomAssignManager.SwitchSeconds, the switch
        /// lockout's countdown). Internal for the tests.
        /// </summary>
        internal static int WaitSeconds(long lastUseMs, long nowMs)
        {
            var left = CooldownMs - (nowMs - lastUseMs);
            return left <= 0 ? 0 : RoomAssignManager.SwitchSeconds(TimeSpan.FromMilliseconds(left));
        }
    }
}
