using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// KILL REWARD (owner 2026-09-23): an item every N kills, but never more often than once per cooldown - a per-area toggle
    /// on any Zone Control zone (ControlledArea.KillReward) and any room dungeon (PropertyString.RoomAssignKillReward on its
    /// source weenie). The dungeon is checked first: it is the smaller, more specific area.
    ///
    /// Owner rules:
    ///   - each player's OWN kills count (the kill's top damager - the player the corpse and its loot belong to; a pet's kill
    ///     counts for its owner), and the item goes to that player. Players and pets being killed do not count;
    ///   - NO PRE-HUNTING (owner 2026-09-23, replacing an earlier "held until the cooldown ends" rule): after a bounty, kills
    ///     do not count until its cooldown has run out; the next bounty is the full cooldown THEN N kills;
    ///   - delivered straight into the pack with one chat line;
    ///   - count and last award are saved on the character (PropertyString.KillRewardProgress), so a relog or a restart can
    ///     neither reset the cooldown nor lose progress;
    ///   - a full pack never loses the item (the Invasion branch's pending-reward rule): it is HELD on the character
    ///     (PropertyString.KillRewardOwed) and handed over as soon as there is room - retried on the player heartbeat, which
    ///     also covers login.
    /// </summary>
    public static class KillRewardManager
    {
        // ── The kill ─────────────────────────────────────────────────────────

        /// <summary>Creature death (Creature_Death.OnDeath). Cheap when the killer stands in no Kill Reward area.</summary>
        public static void OnCreatureKilled(Creature victim, DamageHistoryInfo topDamager)
        {
            try
            {
                if (victim == null || victim is Player || victim is Pet)
                    return;

                var player = topDamager?.TryGetPetOwnerOrAttacker() as Player;
                if (player == null || player.Session == null)
                    return;

                if (!TryResolve(player, out var areaKey, out var cfg))
                    return;

                var now = Time.GetUnixTime();
                var progress = LoadProgress(player);

                // Each reward row counts on its own: its own kills and its own cooldown (owner 2026-09-23: several rewards).
                foreach (var reward in cfg.Entries)
                {
                    if (reward == null || !reward.Valid) continue;

                    var key = areaKey + "#" + reward.ProgressKey;
                    progress.TryGetValue(key, out var entry);

                    // No pre-hunting (owner 2026-09-23, replacing "held until the cooldown ends"): while the cooldown after the
                    // last bounty runs, kills do not count at all. The next bounty is the full cooldown, THEN N kills.
                    var cooldownSeconds = Math.Max(reward.CooldownMinutes, 0) * 60.0;
                    if (now - entry.LastAward < cooldownSeconds)
                        continue;

                    // The first kill that counts after a cooldown says so (owner 2026-09-23) - once per cycle, and not for a
                    // one-kill bounty, which this same kill completes.
                    if (entry.Kills == 0 && entry.LastAward > 0 && cooldownSeconds > 0 && reward.Kills > 1)
                        Tell(player, $"Bounty unlocked: {RoomAssignManager.ItemName(reward.Wcid)} - {KillsText(reward.Kills)} required.");

                    entry.Kills++;

                    if (entry.Kills >= reward.Kills)
                    {
                        entry.Kills = 0;
                        entry.LastAward = now;
                        Give(player, reward.Wcid, reward.Amount, completed: reward);
                    }

                    progress[key] = entry;
                }

                SaveProgress(player, progress);
            }
            catch (Exception ex)
            {
                log.Error($"[KillReward] OnCreatureKilled: {ex}");
            }
        }

        /// <summary>The Kill Reward area this player stands in: a dungeon first, else the governing Zone Control zone.</summary>
        private static bool TryResolve(Player player, out string areaKey, out KillRewardConfig cfg)
        {
            areaKey = null;
            cfg = null;

            var location = player.Location;
            if (location == null)
                return false;

            var source = RoomAssignManager.KillRewardSourceAt(location.Cell, location.Variation);
            if (source != 0)
            {
                cfg = RoomAssignManager.KillRewardOf(source);
                areaKey = "dungeon:" + source;
                return cfg.Active;
            }

            var zone = ZoneControlManager.ResolveKillReward(player);
            if (zone == null)
                return false;

            areaKey = "zone:" + Clean(zone.Value.Name).ToLowerInvariant();
            cfg = zone.Value.Reward;
            return cfg.Active;
        }

        // ── /bounty (owner 2026-09-23) ───────────────────────────────────────

        /// <summary>
        /// Any player: every bounty where they stand - kills so far, or how long until it unlocks - and anything held for them
        /// while their pack was full. Read-only.
        /// </summary>
        [ACE.Server.Command.CommandHandler("bounty", ACE.Entity.Enum.AccessLevel.Player, ACE.Server.Command.CommandHandlerFlag.RequiresWorld,
            "Shows the bounties where you stand: kills so far, or how long until the next one unlocks.")]
        public static void HandleBounty(ACE.Server.Network.Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null) return;

            try
            {
                var held = ParseOwed(player.GetProperty(PropertyString.KillRewardOwed));
                var heldLine = held.Count == 0 ? null
                    : "Held for you until you have room: " + string.Join(", ", held.Select(h => $"{h.Amount:N0} {RoomAssignManager.ItemName(h.Wcid)}")) + ".";

                if (!TryResolve(player, out var areaKey, out var cfg))
                {
                    Tell(player, "Bounty: there is no bounty here.");
                    if (heldLine != null) Tell(player, "Bounty: " + heldLine);
                    return;
                }

                var now = Time.GetUnixTime();
                var progress = LoadProgress(player);

                foreach (var reward in cfg.Entries)
                {
                    if (reward == null || !reward.Valid) continue;

                    progress.TryGetValue(areaKey + "#" + reward.ProgressKey, out var entry);
                    var what = $"{reward.Amount:N0} {RoomAssignManager.ItemName(reward.Wcid)}";
                    var unlockAt = entry.LastAward + Math.Max(reward.CooldownMinutes, 0) * 60.0;
                    var wait = (int)Math.Ceiling(unlockAt - now);

                    Tell(player, wait > 0
                        ? $"Bounty: {what} - unlocks in {FormatWait(wait)}, then {KillsText(reward.Kills)} required."
                        : $"Bounty: {what} - {entry.Kills:N0} of {reward.Kills:N0} kills.");
                }

                if (heldLine != null) Tell(player, "Bounty: " + heldLine);
            }
            catch (Exception ex)
            {
                log.Error($"[KillReward] /bounty: {ex}");
            }
        }

        // ── Editing (the zone and dungeon commands share this) ───────────────

        public const string EditUsage = "on | off | add <wcid> <amount> <kills> <minutes> | set <id> <wcid> <amount> <kills> <minutes> | remove <id>";

        /// <summary>
        /// Applies one edit to a copy of an area's Kill Reward: on / off / add / set &lt;id&gt; / remove &lt;id&gt;. Rewards are named by
        /// their permanent ID (show lists them), never by list position (owner 2026-09-23: two admins at once). The last save
        /// to a reward wins; an edit for a reward someone else removed is refused, never turned onto another row. Returns why
        /// it was refused, or null. <paramref name="at"/> is where the op's own arguments start. Turning on needs a reward with
        /// an item; removing the last one turns it off.
        /// </summary>
        public static string Edit(KillRewardConfig cfg, string op, IList<string> args, int at)
        {
            var inv = CultureInfo.InvariantCulture;
            cfg.EnsureIds();

            string ReadEntry(int from, out KillRewardEntry entry)
            {
                entry = null;
                if (args.Count < from + 4
                    || !uint.TryParse(args[from], NumberStyles.Integer, inv, out var wcid) || wcid == 0
                    || !int.TryParse(args[from + 1], NumberStyles.Integer, inv, out var amount) || amount < 1
                    || !int.TryParse(args[from + 2], NumberStyles.Integer, inv, out var kills) || kills < 1
                    || !double.TryParse(args[from + 3], NumberStyles.Float, inv, out var minutes) || minutes < 0)
                    return "Give <wcid 1+> <amount 1+> <kills 1+> <minutes 0+>.";
                if (ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid) == null)
                    return $"There is no WCID {wcid}.";
                entry = new KillRewardEntry { Wcid = wcid, Amount = amount, Kills = kills, CooldownMinutes = minutes };
                return null;
            }

            string ReadId(int from, out int index)
            {
                index = -1;
                if (args.Count <= from || !int.TryParse(args[from], NumberStyles.Integer, inv, out var id) || id < 1)
                    return "Which reward? Give its ID (killreward ... show lists them).";
                index = cfg.Entries.FindIndex(e => e != null && e.Id == id);
                if (index < 0)
                    return $"Reward {id} is not there - it was removed (by you or another admin).";
                return null;
            }

            switch (op)
            {
                case "on":
                    if (!cfg.HasItem) return "Add a reward first: add <wcid> <amount> <kills> <minutes>.";
                    cfg.Enabled = true;
                    return null;

                case "off":
                    cfg.Enabled = false;
                    return null;

                case "add":
                {
                    var err = ReadEntry(at, out var entry);
                    if (err != null) return err;
                    entry.Id = cfg.NextId++;
                    cfg.Entries.Add(entry);
                    return null;
                }

                case "set":
                {
                    var err = ReadId(at, out var index);
                    if (err != null) return err;
                    err = ReadEntry(at + 1, out var entry);
                    if (err != null) return err;
                    entry.Id = cfg.Entries[index].Id;   // the same reward, edited: last save wins
                    cfg.Entries[index] = entry;
                    return null;
                }

                case "remove":
                {
                    var err = ReadId(at, out var index);
                    if (err != null) return err;
                    cfg.Entries.RemoveAt(index);
                    if (!cfg.HasItem) cfg.Enabled = false;
                    return null;
                }

                default:
                    return "Kill Reward: " + EditUsage;
            }
        }

        /// <summary>One line for chat: "ON: 1x Pyreal every 100 kills (5 min); ...".</summary>
        public static string Describe(KillRewardConfig cfg)
        {
            if (cfg == null || cfg.Entries.Count == 0)
                return (cfg?.Enabled == true ? "ON" : "off") + ": no rewards";

            var list = string.Join("; ", cfg.Entries.Where(e => e != null).Select(e =>
                $"[{e.Id}] {e.Amount}x {RoomAssignManager.ItemName(e.Wcid)} every {e.Kills} kills ({e.CooldownMinutes.ToString("0.##", CultureInfo.InvariantCulture)} min)"));
            return (cfg.Enabled ? "ON" : "off") + ": " + list;
        }

        /// <summary>
        /// The rewards for the plugin: entries joined by '+', each "id:wcid:amount:kills:minutes:name" - the name with every
        /// separator the zone list or the dungeon line uses taken out. The id is what the plugin's edits name.
        /// </summary>
        public static string Wire(KillRewardConfig cfg)
        {
            if (cfg?.Entries == null || cfg.Entries.Count == 0) return "";
            cfg.EnsureIds();
            return string.Join("+", cfg.Entries.Where(e => e != null).Select(e =>
                e.Id + ":" + e.Wcid + ":" + e.Amount + ":" + e.Kills + ":" + e.CooldownMinutes.ToString("0.###", CultureInfo.InvariantCulture) + ":"
                + WireName(RoomAssignManager.ItemName(e.Wcid))));
        }

        private static string WireName(string name)
        {
            var sb = new StringBuilder(name ?? "");
            foreach (var c in new[] { ':', '+', ',', '|', '=', ';', '~' })
                sb.Replace(c, ' ');
            return sb.ToString().Trim();
        }

        // ── Delivery ─────────────────────────────────────────────────────────

        /// <summary>
        /// Into the pack, as many stacks as the amount needs. Whatever does not fit is HELD on the character and handed over
        /// once there is room - never dropped, never lost. <paramref name="completed"/> is the reward whose bounty was just
        /// completed (its next-bounty line follows), or null when this is held items being handed over.
        ///
        /// Wording (owner 2026-09-23), in chat as "Bounty":
        ///   completed: "Bounty complete! +1 Ascension Coin gained."  /  "Next bounty unlocks in 5 min - 100 kills required."
        ///   held:      "Bounty: Your inventory is full. +1 Ascension Coin gained (3 total). They're safe until you make room."
        ///   arrived:   "Bounty: +3 Ascension Coins delivered. Your held rewards are in your inventory."
        /// </summary>
        private static void Give(Player player, uint wcid, int amount, KillRewardEntry completed)
        {
            var given = 0;
            var guard = 0;
            string single = null, plural = null;

            while (given < amount && guard++ < 64)
            {
                var item = WorldObjectFactory.CreateNewWorldObject(wcid);
                if (item == null)
                {
                    log.Warn($"[KillReward] WCID {wcid} could not be created - {amount - given} not given to {player.Name}.");
                    return;
                }

                var stack = 1;
                if (item.MaxStackSize.HasValue && amount - given > 1)
                {
                    stack = Math.Min(amount - given, item.MaxStackSize.Value);
                    item.SetStackSize(stack);
                }
                single ??= item.Name;
                plural ??= item.GetPluralName();

                if (!player.TryCreateInInventoryWithNetworking(item))
                {
                    item.Destroy();
                    break;
                }
                given += stack;
            }

            single ??= RoomAssignManager.ItemName(wcid);
            plural ??= single;
            string Named(int n) => n == 1 ? single : plural;

            var left = amount - given;
            if (left > 0)
                AddOwed(player, wcid, left);

            if (completed != null)
            {
                if (given > 0)
                    Tell(player, $"Bounty complete! +{given:N0} {Named(given)} gained.");

                if (left > 0)
                {
                    var total = OwedOf(player, wcid);
                    Tell(player, $"Bounty: Your inventory is full. +{left:N0} {Named(left)} gained"
                        + (total > left ? $" ({total:N0} total)" : "")
                        + (total == 1 ? ". It's safe until you make room." : ". They're safe until you make room."));
                }

                Tell(player, NextBountyLine(completed, Time.GetUnixTime()));   // awarded this instant
            }
            else if (given > 0)
            {
                Tell(player, $"Bounty: +{given:N0} {Named(given)} delivered. Your held rewards are in your inventory.");
            }
        }

        /// <summary>
        /// "Next bounty unlocks in 2 min 30 sec - 100 kills required." - or, with no cooldown, "Next bounty requires 100 kills."
        /// The wait is the REAL time until this player's next bounty can start (owner 2026-09-23): the cooldown's end, from when
        /// this bounty was awarded, to the second - never rounded (2.5 min is "2 min 30 sec", not "3 min").
        /// </summary>
        private static string KillsText(int kills) => kills == 1 ? "1 kill" : $"{kills:N0} kills";

        private static string NextBountyLine(KillRewardEntry reward, double awardedUnix)
        {
            var kills = KillsText(reward.Kills);
            var unlockAt = awardedUnix + Math.Max(reward.CooldownMinutes, 0) * 60.0;
            var seconds = (int)Math.Ceiling(unlockAt - Time.GetUnixTime());
            if (seconds <= 0)
                return $"Next bounty requires {kills}.";
            return $"Next bounty unlocks in {FormatWait(seconds)} - {kills} required.";
        }

        /// <summary>Exact: 45 sec / 5 min / 2 min 30 sec / 1 hr 30 min / 1 hr 0 min 15 sec -> "1 hr 15 sec".</summary>
        private static string FormatWait(int seconds)
        {
            var h = seconds / 3600;
            var m = seconds % 3600 / 60;
            var s = seconds % 60;
            var parts = new List<string>(3);
            if (h > 0) parts.Add($"{h} hr");
            if (m > 0) parts.Add($"{m} min");
            if (s > 0 || parts.Count == 0) parts.Add($"{s} sec");
            return string.Join(" ", parts);
        }

        /// <summary>How many of this item are held for the player now.</summary>
        private static int OwedOf(Player player, uint wcid)
            => ParseOwed(player.GetProperty(PropertyString.KillRewardOwed)).Where(o => o.Wcid == wcid).Sum(o => o.Amount);

        /// <summary>
        /// Player heartbeat: hands over anything held while the pack was full, as soon as it fits. One property read when
        /// nothing is held. Also runs right after login, so a reward earned before a logout arrives on the next login.
        /// </summary>
        public static void TryDeliverOwed(Player player)
        {
            try
            {
                if (player?.Session == null)
                    return;

                var raw = player.GetProperty(PropertyString.KillRewardOwed);
                if (string.IsNullOrEmpty(raw))
                    return;

                if (player.GetFreeInventorySlots() < 1)
                    return;

                var owed = ParseOwed(raw);
                player.RemoveProperty(PropertyString.KillRewardOwed);

                // Give puts back anything that still does not fit.
                foreach (var (wcid, amount) in owed)
                    Give(player, wcid, amount, completed: null);
            }
            catch (Exception ex)
            {
                log.Error($"[KillReward] TryDeliverOwed: {ex}");
            }
        }

        private static void AddOwed(Player player, uint wcid, int amount)
        {
            var owed = ParseOwed(player.GetProperty(PropertyString.KillRewardOwed));
            var at = owed.FindIndex(o => o.Wcid == wcid);
            if (at >= 0) owed[at] = (wcid, owed[at].Amount + amount);
            else owed.Add((wcid, amount));

            player.SetProperty(PropertyString.KillRewardOwed, string.Join(";", owed.Select(o => o.Wcid + ":" + o.Amount)));
        }

        private static List<(uint Wcid, int Amount)> ParseOwed(string raw)
        {
            var list = new List<(uint, int)>();
            if (string.IsNullOrWhiteSpace(raw)) return list;

            foreach (var part in raw.Split(';'))
            {
                var f = part.Split(':');
                if (f.Length == 2
                    && uint.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) && w != 0
                    && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) && a > 0)
                    list.Add((w, a));
            }
            return list;
        }

        // ── Progress on the character ────────────────────────────────────────

        private struct Entry
        {
            public int Kills;
            public double LastAward;   // unix seconds, 0 = never
        }

        private static Dictionary<string, Entry> LoadProgress(Player player)
        {
            var map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var raw = player.GetProperty(PropertyString.KillRewardProgress);
            if (string.IsNullOrWhiteSpace(raw)) return map;

            foreach (var part in raw.Split(';'))
            {
                var f = part.Split('|');
                if (f.Length != 3 || f[0].Length == 0) continue;
                int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kills);
                double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var last);
                map[f[0]] = new Entry { Kills = Math.Max(kills, 0), LastAward = last };
            }
            return map;
        }

        private static void SaveProgress(Player player, Dictionary<string, Entry> map)
        {
            var sb = new StringBuilder();
            foreach (var kv in map)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('|')
                  .Append(kv.Value.Kills.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(kv.Value.LastAward.ToString("0", CultureInfo.InvariantCulture));
            }
            player.SetProperty(PropertyString.KillRewardProgress, sb.ToString());
        }

        /// <summary>A zone name as a key: the separators the progress string uses are taken out.</summary>
        private static string Clean(string s)
            => (s ?? "").Replace('|', ' ').Replace(';', ' ').Trim();

        private static void Tell(Player player, string text)
            => player.Session?.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
