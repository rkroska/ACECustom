using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// BOUNTY (owner 2026-09-23; called Kill Reward until 2026-09-26): an item every N kills, but never more often than once per cooldown - a per-area toggle
    /// on any Zone Control zone (ControlledArea.Bounty) and any room dungeon (PropertyString.RoomAssignBounty on its
    /// source weenie). The dungeon is checked first: it is the smaller, more specific area. Two shard-wide layers (2026-10-05/06):
    /// the ZONE-WIDE bounty (every T11-T25 zone, one progress key, replaces the zone bounties while on) and the SERVER-WIDE
    /// bounty (its target WCIDs anywhere, retail too; no targets = any kill that pays). ONE shared timer across all of them.
    ///
    /// Owner rules:
    ///   - each player's OWN kills count (the kill's top damager - the player the corpse and its loot belong to; a pet's kill
    ///     counts for its owner), and the item goes to that player. Players and pets being killed do not count;
    ///   - the killer AND the kill must both be in the area, and the kill must pay kill XP or luminance (review 2026-09-24:
    ///     sniping over a zone's edge, critters and no-XP landblocks do not count). The server-wide bounty has no area, and a
    ///     kill of one of its TARGETS counts even without XP (owner 2026-10-06: "any kill that matches the wcid(s)");
    ///   - NO PRE-HUNTING (owner 2026-09-23, replacing an earlier "held until the cooldown ends" rule): after a bounty, kills
    ///     do not count until its cooldown has run out; the next bounty is the full cooldown THEN N kills;
    ///   - delivered straight into the pack with one chat line;
    ///   - count and last award are saved on the character (PropertyString.BountyProgress), so a relog or a restart can
    ///     neither reset the cooldown nor lose progress;
    ///   - a full pack never loses the item (the Invasion branch's pending-reward rule): it is HELD on the character
    ///     (PropertyString.BountyOwed) and handed over as soon as there is room - retried on the player heartbeat, which
    ///     also covers login.
    ///
    /// Threading (review 2026-09-24): every read and write of a player's progress, held rewards and pack happens on that
    /// player's own thread - the kill hands its credit over with LandblockManager.RunOnThreadFor, as kill XP and kill tasks
    /// do, and the heartbeat already runs there. Nothing is shared between threads, so nothing can be delivered twice or lost
    /// between a kill and a delivery.
    /// </summary>
    public static class BountyManager
    {
        /// <summary>Command bounds (review 2026-09-24): an amount, a kill count and a cooldown an admin can actually mean.</summary>
        public const int MaxAmount = 10_000;
        public const int MaxKills = 1_000_000;
        public const double MaxCooldownMinutes = 525_600;   // one year

        /// <summary>How long a delivery that handed nothing over waits before the next try (full pack, over burden, a broken WCID).</summary>
        private const double DeliveryRetrySeconds = 30;

        /// <summary>Player guid -> unix time of the next delivery try. Only touched on the player's own thread; concurrent for logout races.</summary>
        private static readonly ConcurrentDictionary<uint, double> _nextDeliveryTry = new();

        // ── The kill ─────────────────────────────────────────────────────────

        /// <summary>
        /// Creature death (Creature_Death.OnDeath), on the victim's thread: one area lookup for the victim's spot, and nothing
        /// else unless it died in a Bounty area or the server-wide bounty is on (a target set lookup; with no targets, every
        /// death that pays XP / luminance is credited to its player).
        /// </summary>
        public static void OnCreatureKilled(Creature victim)
        {
            try
            {
                if (victim == null || victim is Player || victim is Pet)
                    return;

                // The kill's own area first - read here, on the victim's thread.
                var hasArea = TryResolve(victim, out var victimArea, out _);

                // SERVER-WIDE bounty (owner 2026-10-06): its target WCIDs count ANYWHERE ("Any kill that matches the wcid(s)" - even a
                // kill that pays no XP); with no targets, any kill that pays XP / luminance counts.
                var server = ZoneControlManager.ActiveServerWideBounty();
                var targeted = server != null && server.HasTargets;
                var serverMatch = server != null && (!targeted || server.IsTarget(victim.WeenieClassId));
                if (!hasArea && !serverMatch)
                    return;

                var pays = PaysBounty(victim);
                var areaOk = hasArea && pays;
                var serverOk = serverMatch && (targeted || pays);
                if (!areaOk && !serverOk)
                    return;

                // The ONLINE player, never the object the damage history remembers (review 2026-09-24): that is a weak
                // reference that can outlive a logout, and a reward given to it would go to an object that is no longer the
                // character - the same lookup kill XP does. Mules and Olthoi earn no bounties (owner 2026-09-24): they get no
                // kill XP and Zone Share leaves them out too.
                var attacker = victim.DamageHistory.TopDamager?.TryGetPetOwnerOrAttacker() as Player;
                var player = attacker != null ? PlayerManager.GetOnlinePlayer(attacker.Guid) : null;
                if (player == null || !ReferenceEquals(player, attacker) || player.IsLoggingOut || player.IsMule || player.IsOlthoiPlayer)
                    return;

                // The player may be ticked by another landblock group (a pet or a DoT finished the kill after a portal): the
                // credit, the progress and the pack are the player's, so they are touched on the player's thread only.
                LandblockManager.RunOnThreadFor(player, ActionType.Bounty_Credit, () => Credit(player, areaOk ? victimArea : null, serverOk));
            }
            catch (Exception ex)
            {
                log.Error($"[Bounty] OnCreatureKilled: {ex}");
            }
        }

        /// <summary>The player-thread half of a kill: count it for the area bounty (when <paramref name="victimArea"/> is set) and the
        /// server-wide bounty (when <paramref name="server"/>), in ONE pass, save, then hand over whatever it completed.</summary>
        private static void Credit(Player player, string victimArea, bool server)
        {
            try
            {
                // Still the online character when this runs (it may have been queued to the world thread meanwhile).
                if (player.IsLoggingOut || !ReferenceEquals(PlayerManager.GetOnlinePlayer(player.Guid), player))
                    return;

                var bounties = new List<(string AreaKey, BountyConfig Cfg)>(2);

                // Killer and kill in the same area (review 2026-09-24): standing just inside the edge and killing outside it
                // does not count, and neither does a kill that lands while the killer is somewhere else.
                if (victimArea != null && TryResolve(player, out var areaKey, out var cfg) && areaKey == victimArea)
                    bounties.Add((areaKey, cfg));

                // the server-wide bounty has no area to match - it counts anywhere
                var serverCfg = server ? ZoneControlManager.ActiveServerWideBounty() : null;
                if (serverCfg != null)
                    bounties.Add((ServerAreaKey, serverCfg));

                if (bounties.Count > 0)
                    CreditCore(player, bounties);
            }
            catch (Exception ex)
            {
                log.Error($"[Bounty] Credit: {ex}");
            }
        }

        /// <summary>
        /// Counts one kill for each bounty it reached (the area's, then the server-wide one) and hands over what it completed.
        /// Each bounty keeps its own cooldown. With the server setting bounty_shared_timer ON (default off), ONE SHARED TIMER -
        /// see SharedTimerPrefix:
        ///   - the lock is read ONCE, before this kill, so every bounty judges the kill the same way;
        ///   - while it runs, a row WITH a cooldown does not count kills (no pre-hunting); a row with NO cooldown is outside it;
        ///   - rows of ONE bounty that complete on the same kill all pay;
        ///   - when two bounties complete on the same kill, the one that has WAITED LONGER (oldest last award) pays; the other is
        ///     held one kill short and pays on its first kill after the lock - a tie never goes to the same bounty twice running.
        ///     (A slower bounty still only counts kills outside the lock, so beside a 1-kill bounty it gains one kill per lock.)
        /// </summary>
        private static void CreditCore(Player player, List<(string AreaKey, BountyConfig Cfg)> bounties)
        {
            try
            {
                var now = Time.GetUnixTime();
                var progress = LoadProgress(player);
                var lockUntil = SharedLockUntil(progress, now);

                // 1. count the kill on every row that may count it; collect the rows it completes
                var done = new List<(int Bounty, BountyEntry Reward, string Key, double PrevAward)>();
                for (var bi = 0; bi < bounties.Count; bi++)
                {
                    var (areaKey, cfg) = bounties[bi];
                    // Each reward row counts on its own: its own kills and its own cooldown (owner 2026-09-23: several rewards).
                    foreach (var reward in cfg.Entries)
                    {
                        if (reward == null || !reward.Valid) continue;

                        var key = areaKey + "#" + reward.ProgressKey;
                        progress.TryGetValue(key, out var entry);

                        // No pre-hunting (owner 2026-09-23, replacing "held until the cooldown ends"): while the cooldown after the
                        // last bounty runs, kills do not count at all. The next bounty is the full cooldown, THEN N kills.
                        var cooldownSeconds = reward.CooldownSeconds;
                        if (cooldownSeconds > 0 && (now - entry.LastAward < cooldownSeconds || now < lockUntil))
                            continue;

                        // The first kill that counts after a cooldown says so (owner 2026-09-23) - once per cycle, and not for a
                        // one-kill bounty, which this same kill completes.
                        if (entry.Kills == 0 && entry.LastAward > 0 && cooldownSeconds > 0 && reward.Kills > 1)
                            Tell(player, $"Bounty unlocked: {RoomAssignManager.ItemName(reward.Wcid)} - {KillsText(reward.Kills)} required.");

                        entry.Kills++;
                        progress[key] = entry;
                        if (entry.Kills >= reward.Kills)
                            done.Add((bi, reward, key, entry.LastAward));
                    }
                }

                // 2. which bounty pays: every no-cooldown row pays; of the bounties completing rows WITH a cooldown, only the one
                //    that has waited longest (oldest previous award; the area's on a tie)
                var payer = -1;
                var oldest = double.MaxValue;
                foreach (var d in done)
                    if (d.Reward.CooldownSeconds > 0 && d.PrevAward < oldest)
                    {
                        oldest = d.PrevAward;
                        payer = d.Bounty;
                    }

                var sharedTimer = ServerConfig.bounty_shared_timer.Value;
                var completed = new List<BountyEntry>();
                foreach (var d in done)
                {
                    var entry = progress[d.Key];
                    if (sharedTimer && d.Reward.CooldownSeconds > 0 && d.Bounty != payer)
                    {
                        // held one kill short: it pays on its first kill after the lock
                        entry.Kills = d.Reward.Kills - 1;
                        progress[d.Key] = entry;
                        Tell(player, $"Bounty ready: {RoomAssignManager.ItemName(d.Reward.Wcid)} - it pays on your first kill after the bounty timer.");
                        continue;
                    }
                    entry.Kills = 0;
                    entry.LastAward = now;
                    progress[d.Key] = entry;
                    completed.Add(d.Reward);
                    if (sharedTimer && d.Reward.CooldownSeconds > 0)
                        progress[SharedTimerPrefix + d.Key] = new Entry { LastAward = now };
                }

                // This area's progress for rewards it no longer has (removed rows, keys from an older format) is dropped, so the
                // string on the character does not grow forever (review 2026-09-24).
                foreach (var (areaKey, cfg) in bounties)
                {
                    var prefix = areaKey + "#";
                    var live = new HashSet<string>(cfg.Entries.Where(e => e != null && e.Valid).Select(e => prefix + e.ProgressKey), StringComparer.OrdinalIgnoreCase);
                    foreach (var stale in progress.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !live.Contains(k)).ToList())
                        progress.Remove(stale);
                }

                var unlockAfter = PruneSharedLock(progress, now);

                // Saved BEFORE anything is handed over: a failure while giving can never award the same bounty twice.
                SaveProgress(player, progress);

                // Each award on its own (review 2026-09-24): one that throws keeps what is left of it held (Award's finally)
                // and never stops the next bounty the same kill completed.
                foreach (var reward in completed)
                {
                    try
                    {
                        Award(player, reward, now, unlockAfter);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[Bounty] Award of {reward.Wcid} to {player.Name}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[Bounty] CreditCore: {ex}");
            }
        }

        /// <summary>The end of the shared lock: the latest of (award time + that reward's CURRENT cooldown) over the rewards that
        /// started it. 0 = none. Read live (CurrentCooldownSeconds), so an admin who shortens or removes the reward releases it.</summary>
        private static double SharedLockUntil(Dictionary<string, Entry> progress, double now)
        {
            // OFF (the default): every bounty keeps only its own cooldown
            if (!ServerConfig.bounty_shared_timer.Value)
                return 0;
            double until = 0;
            foreach (var kv in progress)
            {
                if (!kv.Key.StartsWith(SharedTimerPrefix, StringComparison.Ordinal)) continue;
                var cd = CurrentCooldownSeconds(kv.Key.Substring(SharedTimerPrefix.Length));
                if (cd > 0) until = Math.Max(until, kv.Value.LastAward + cd);
            }
            return until > now ? until : 0;
        }

        /// <summary>Drops the shared-lock entries that can no longer lock anything - older than the longest cooldown there can be -
        /// and the pre-release "*" form, so the progress string stays small. Never on a cooldown that reads 0 (a removed row, or a
        /// moment where the lookup could not see it): the lock is simply not counted then, and the entry waits out its year.
        /// Returns the lock that remains (SharedLockUntil).</summary>
        private static double PruneSharedLock(Dictionary<string, Entry> progress, double now)
        {
            progress.Remove(LegacySharedTimerKey);
            const double longestCooldownSeconds = MaxCooldownMinutes * 60;
            foreach (var key in progress.Keys.Where(k => k.StartsWith(SharedTimerPrefix, StringComparison.Ordinal)).ToList())
                if (now - progress[key].LastAward > longestCooldownSeconds)
                    progress.Remove(key);
            return SharedLockUntil(progress, now);
        }

        /// <summary>How long CurrentCooldownSeconds keeps an answer (every bounty edit through Edit clears the cache at once).</summary>
        private const double CooldownCacheSeconds = 5;

        private const int MaxCooldownCacheEntries = 4096;

        /// <summary>progress key -> (cooldown seconds, valid until unix). Read on player threads; a stale write is harmless.</summary>
        private static readonly ConcurrentDictionary<string, (double Cooldown, double Until)> _cooldownCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The CURRENT cooldown of the reward behind a progress key ("area#rowKey"), or 0 when that reward row is gone.
        /// Area keys: "server", "zonewide", "zone:name", "dungeon:WCIDvVARIATION". The row decides, not whether its bounty or Zone
        /// Control is switched on right now - a switch flipped for a minute neither drops nor shortens anyone's lock. Cached for
        /// CooldownCacheSeconds: every credited kill asks.</summary>
        private static double CurrentCooldownSeconds(string progressKey)
        {
            var now = Time.GetUnixTime();
            if (_cooldownCache.TryGetValue(progressKey, out var hit) && now < hit.Until)
                return hit.Cooldown;

            double cooldown = 0;
            var hash = progressKey.LastIndexOf('#');
            if (hash > 0)
            {
                var area = progressKey.Substring(0, hash);
                var rowKey = progressKey.Substring(hash + 1);
                var cfg = ConfigForAreaKey(area);
                var row = cfg?.Entries?.FirstOrDefault(e => e != null && e.Valid && string.Equals(e.ProgressKey, rowKey, StringComparison.OrdinalIgnoreCase));
                cooldown = row?.CooldownSeconds ?? 0;
            }
            if (_cooldownCache.Count > MaxCooldownCacheEntries)
                _cooldownCache.Clear();   // one entry per reward row ever seen - a ceiling, never an eviction policy
            _cooldownCache[progressKey] = (cooldown, now + CooldownCacheSeconds);
            return cooldown;
        }

        /// <summary>The bounty settings behind an area key, whether switched on or not (null = no such area any more).</summary>
        private static BountyConfig ConfigForAreaKey(string area)
        {
            if (string.Equals(area, ServerAreaKey, StringComparison.OrdinalIgnoreCase))
                return ZoneControlManager.GetServerWideBounty();
            if (string.Equals(area, ZoneWideAreaKey, StringComparison.OrdinalIgnoreCase))
                return ZoneControlManager.GetZoneWideBounty();
            if (area.StartsWith(ZoneKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // the lock-free sanitized map (never the live area under the manager lock): the reward ids match the kill path's
                var zones = ZoneControlManager.ZoneBountiesByName;
                if (zones.TryGetValue(area.Substring(ZoneKeyPrefix.Length), out var cfg))
                    return cfg;
                foreach (var kv in zones)   // a name Clean changed ('|' ';' '#')
                    if (string.Equals(ZoneAreaKey(kv.Key), area, StringComparison.OrdinalIgnoreCase))
                        return kv.Value;
                return null;
            }
            if (RoomAssignManager.TryParseDungeonAreaKey(area, out var source))
                return RoomAssignManager.BountyOf(source);
            return null;
        }

        /// <summary>
        /// Only kills that pay kill XP or luminance count (review 2026-09-24): the same numbers OnDeath_GrantXP uses - the
        /// weenie's, or the governing zone's when it authors them - and nothing on a no-death-XP landblock.
        /// </summary>
        private static bool PaysBounty(Creature victim)
        {
            if (victim.IsOnNoDeathXPLandblock || victim.DamageHistory.TotalHealth == 0)
                return false;

            var xp = victim.WeenieKillXp;   // the same read OnDeath_GrantXP uses
            long lum = victim.LuminanceAward ?? 0;

            var profile = ZoneControlManager.ResolveForCreature(victim);
            if (profile != null)
            {
                if (profile.Has(ZoneStat.XpKill))
                    xp = (long)Math.Round(profile.Get(ZoneStat.XpKill));
                if (profile.Has(ZoneStat.LumAward))
                    lum = (long)Math.Round(profile.Get(ZoneStat.LumAward));
            }

            return xp > 0 || lum > 0;
        }

        /// <summary>
        /// The Bounty area this object stands in: a dungeon first, else the governing Zone Control zone. Asked for the
        /// victim (on its thread) and for the killer (on theirs); the two keys must match. A dungeon's key carries its
        /// variation, so two copies of one dungeon are two areas.
        /// </summary>
        private static bool TryResolve(WorldObject wo, out string areaKey, out BountyConfig cfg)
        {
            areaKey = null;
            cfg = null;

            var location = wo.Location;
            if (location == null)
                return false;

            var source = RoomAssignManager.BountySourceAt(location.Cell, location.Variation);
            if (source != 0)
            {
                cfg = RoomAssignManager.BountyOf(source);
                areaKey = RoomAssignManager.DungeonAreaKey(source, location.Variation);
                return cfg.Active;
            }

            // A dungeon's own setting wins, even when it is off (owner 2026-09-24): a dungeon with no Bounty inside a
            // zone that has one does not inherit the zone's.
            if (RoomAssignManager.IsInRoomDungeon(location))
                return false;

            // ZONE-WIDE BOUNTY (owner 2026-10-05): one bounty + one timer per character across every T11-T25 zone - the progress
            // key is the same in every zone and tier, so hopping cannot restart a cooldown. While it is on it replaces the
            // per-zone bounties.
            var zoneWide = ZoneControlManager.ResolveZoneWideBounty(wo);
            if (zoneWide != null)
            {
                areaKey = ZoneWideAreaKey;
                cfg = zoneWide;
                return true;
            }

            var zone = ZoneControlManager.ResolveBounty(wo);
            if (zone == null)
                return false;

            areaKey = ZoneAreaKey(zone.Value.Name);
            cfg = zone.Value.Reward;
            return cfg.Active;
        }

        // ── /bounty (owner 2026-09-23) ───────────────────────────────────────

        /// <summary>
        /// Any player: every bounty where they stand - kills so far, or how long until it unlocks - and anything held for them
        /// while their pack was full. Read-only. The command itself lives in Command/Handlers/BountyCommands.cs.
        /// </summary>
        public static void ShowBounty(Player player)
        {
            if (player == null) return;

            try
            {
                var held = ParseOwed(player.GetProperty(PropertyString.BountyOwed));
                var heldLine = held.Count == 0 ? null
                    : "Held for you until you have room: " + string.Join(", ", held.Select(h => $"{h.Amount:N0} {RoomAssignManager.ItemName(h.Wcid)}")) + ".";

                var now = Time.GetUnixTime();
                var progress = LoadProgress(player);
                var lockUntil = SharedLockUntil(progress, now);   // the shared lock, once for every line below
                if (!TryResolve(player, out var areaKey, out var cfg))
                {
                    if (ZoneControlManager.ActiveServerWideBounty() == null)
                        Tell(player, "Bounty: there is no bounty here.");
                    ShowServerBounty(player, progress, now, lockUntil);
                    if (heldLine != null) Tell(player, "Bounty: " + heldLine);
                    return;
                }

                // the zone-wide bounty says so: its count and timer follow the player into every zone (owner 2026-10-05)
                var where = areaKey == ZoneWideAreaKey ? "Bounty (all zones)" : "Bounty";
                foreach (var reward in cfg.Entries)
                {
                    if (reward == null || !reward.Valid) continue;
                    Tell(player, $"{where}: {RewardText(reward)} - {StatusText(progress, areaKey, reward, now, lockUntil)}.");
                }

                ShowServerBounty(player, progress, now, lockUntil);
                if (heldLine != null) Tell(player, "Bounty: " + heldLine);
            }
            catch (Exception ex)
            {
                log.Error($"[Bounty] /bounty: {ex}");
            }
        }

        /// <summary>The server-wide bounty's lines in /bounty: what to kill, and where the player stands on it (2026-10-06).</summary>
        private static void ShowServerBounty(Player player, Dictionary<string, Entry> progress, double now, double lockUntil)
        {
            var cfg = ZoneControlManager.ActiveServerWideBounty();
            if (cfg == null) return;
            var targets = TargetNames(cfg);
            var what = targets != null ? "kill " + targets + " anywhere" : "any kill, anywhere";
            foreach (var reward in cfg.Entries)
            {
                if (reward == null || !reward.Valid) continue;
                Tell(player, $"Server bounty ({what}): {RewardText(reward)} - {StatusText(progress, ServerAreaKey, reward, now, lockUntil)}.");
            }
        }

        /// <summary>How many target names a player line spells out before "and N more".</summary>
        private const int TargetNamesShown = 3;

        /// <summary>"Mushy Marv or Sporecap Thrungus" - the server-wide bounty's targets, or null when it has none (any kill). At most
        /// TargetNamesShown names, then "or one of 4 more" - a chat line must stay short (long lines stall the client).</summary>
        private static string TargetNames(BountyConfig cfg)
        {
            if (cfg.TargetWcids == null || cfg.TargetWcids.Count == 0) return null;
            var names = string.Join(" or ", cfg.TargetWcids.Take(TargetNamesShown).Select(RoomAssignManager.ItemName));
            var more = cfg.TargetWcids.Count - TargetNamesShown;
            return more > 0 ? $"{names} or one of {more:N0} more" : names;
        }

        /// <summary>"3 Pyreal Nugget" - one reward's item and amount.</summary>
        private static string RewardText(BountyEntry reward) => $"{reward.Amount:N0} {RoomAssignManager.ItemName(reward.Wcid)}";

        /// <summary>
        /// Where the player stands on one reward of one area: "unlocks in 12 min, then 100 kills required" while its own cooldown
        /// or the shared timer runs, else "34 of 100 kills". /bounty and /bounty list both use it, so they always say the same
        /// thing. Kills already counted before the shared timer started are kept, so the wait line asks only for the rest.
        /// </summary>
        private static string StatusText(Dictionary<string, Entry> progress, string areaKey, BountyEntry reward, double now, double lockUntil)
        {
            progress.TryGetValue(areaKey + "#" + reward.ProgressKey, out var entry);
            var unlockAt = reward.CooldownSeconds > 0 ? Math.Max(entry.LastAward + reward.CooldownSeconds, lockUntil) : 0;
            var wait = (int)Math.Ceiling(Math.Min(unlockAt - now, int.MaxValue));
            return wait > 0
                ? $"unlocks in {FormatWait(wait)}, then {KillsText(Math.Max(1, reward.Kills - entry.Kills))} required"
                : $"{entry.Kills:N0} of {reward.Kills:N0} kills";
        }

        /// <summary>The same area key TryResolve builds for a zone (progress is saved under it).</summary>
        private static string ZoneAreaKey(string zoneName) => ZoneKeyPrefix + Clean(zoneName).ToLowerInvariant();

        /// <summary>ZoneAreaKey's prefix ("zone:name").</summary>
        private const string ZoneKeyPrefix = "zone:";

        /// <summary>The zone-wide bounty's progress key - one per character for every T11-T25 zone (owner 2026-10-05).</summary>
        public const string ZoneWideAreaKey = "zonewide";

        /// <summary>The server-wide bounty's progress key (2026-10-06).</summary>
        public const string ServerAreaKey = "server";

        /// <summary>The ONE SHARED TIMER (only while the server setting bounty_shared_timer is ON - default off): "*:area#rowKey|0|awardUnix" - one entry per reward that
        /// paid with a cooldown. The lock lasts until award + that reward's CURRENT cooldown (SharedLockUntil), and no reward with
        /// a cooldown counts kills before it. Rows with no cooldown neither wait for it nor set it.</summary>
        private const string SharedTimerPrefix = "*:";

        /// <summary>The test-shard-only form before 2026-10-06 evening ("*|0|unlockUnix"); dropped on the next credited kill.</summary>
        private const string LegacySharedTimerKey = "*";

        /// <summary>
        /// /bounty list (owner 2026-09-27): every bounty that can pay right now - each Vaulted Dungeon's, then each zone's -
        /// with its rewards and this player's progress on each, so players can choose where to go. Read-only. Lists active
        /// Bounties in dungeons placed in a v3+ layer and in zones at v11+ (see ZoneControlManager.ActiveZoneBounties for the
        /// one case a listed zone might not pay).
        /// </summary>
        public static void ShowAllBounties(Player player)
        {
            if (player == null) return;

            try
            {
                var now = Time.GetUnixTime();
                var progress = LoadProgress(player);
                var lockUntil = SharedLockUntil(progress, now);   // the shared lock, once for every line below
                var shown = 0;

                void ShowArea(string where, string areaKey, BountyConfig cfg)
                {
                    var rewards = cfg.Entries.Where(e => e != null && e.Valid).ToList();
                    if (rewards.Count == 0) return;
                    if (shown == 0) Tell(player, "Bounties:");
                    shown++;
                    foreach (var reward in rewards)
                    {
                        // No cooldown (0 is allowed): no "at most every 0 sec".
                        var cooldown = reward.CooldownSeconds > 0
                            ? $" (at most every {FormatWait((int)Math.Ceiling(Math.Min(reward.CooldownSeconds, int.MaxValue)))})" : "";
                        Tell(player, $"  {where}: {RewardText(reward)} every {KillsText(reward.Kills)}{cooldown}"
                            + $" - you: {StatusText(progress, areaKey, reward, now, lockUntil)}.");
                    }
                }

                var dungeons = RoomAssignManager.ActiveDungeonBounties();
                var multiLayer = new HashSet<uint>(dungeons.GroupBy(d => d.SourceWcid).Where(g => g.Count() > 1).Select(g => g.Key));
                foreach (var d in dungeons)
                    ShowArea(multiLayer.Contains(d.SourceWcid) ? $"{d.Name} (layer {d.Variation})" : d.Name,
                        RoomAssignManager.DungeonAreaKey(d.SourceWcid, d.Variation), d.Bounty);

                // the zone-wide bounty replaces every zone's own while it is on (owner 2026-10-05)
                var zoneWide = ZoneControlManager.GetZoneWideBounty();
                if (zoneWide.Active && ServerConfig.zonecontrol_enabled.Value)
                    ShowArea("All zones", ZoneWideAreaKey, zoneWide);
                else
                    foreach (var z in ZoneControlManager.ActiveZoneBounties())
                        ShowArea(z.Name, ZoneAreaKey(z.Name), z.Reward);

                var serverCfg = ZoneControlManager.ActiveServerWideBounty();
                if (serverCfg != null)
                {
                    var targets = TargetNames(serverCfg);
                    ShowArea(targets != null ? "Anywhere (kill " + targets + ")" : "Anywhere (any kill)", ServerAreaKey, serverCfg);
                }

                if (shown == 0)
                    Tell(player, "Bounty: there are no bounties anywhere right now.");
                else
                    Tell(player, "Bounty: type /bounty to see the bounties where you stand.");
            }
            catch (Exception ex)
            {
                log.Error($"[Bounty] /bounty list: {ex}");
            }
        }

        // ── Editing (the zone and dungeon commands share this) ───────────────

        public const string EditUsage = "on | off | add <wcid> <amount> <kills> <minutes> | set <id> <wcid> <amount> <kills> <minutes> | remove <id>";

        /// <summary>The server-wide bounty's own verb.</summary>
        public const string TargetUsage = "target add <creature wcid> | target remove <creature wcid> | target clear";

        /// <summary>
        /// Applies one edit to a copy of an area's Bounty: on / off / add / set &lt;id&gt; / remove &lt;id&gt;. Rewards are named by
        /// their permanent ID (show lists them), never by list position (owner 2026-09-23: two admins at once). The last save
        /// to a reward wins; an edit for a reward someone else removed is refused, never turned onto another row. Returns why
        /// it was refused, or null. <paramref name="at"/> is where the op's own arguments start. Turning on needs a reward with
        /// an item; removing the last one turns it off.
        /// </summary>
        public static string Edit(BountyConfig cfg, string op, IList<string> args, int at, bool allowTargets = false)
        {
            var refused = EditCore(cfg, op, args, at, allowTargets);
            if (refused == null)
                _cooldownCache.Clear();   // the shared bounty timer reads cooldowns: an edit is seen on the next kill
            return refused;
        }

        private static string EditCore(BountyConfig cfg, string op, IList<string> args, int at, bool allowTargets)
        {
            var inv = CultureInfo.InvariantCulture;
            cfg.EnsureIds();

            string ReadEntry(int from, out BountyEntry entry)
            {
                entry = null;
                if (args.Count < from + 4
                    || !uint.TryParse(args[from], NumberStyles.Integer, inv, out var wcid) || wcid == 0
                    || !int.TryParse(args[from + 1], NumberStyles.Integer, inv, out var amount) || amount < 1 || amount > MaxAmount
                    || !int.TryParse(args[from + 2], NumberStyles.Integer, inv, out var kills) || kills < 1 || kills > MaxKills
                    || !double.TryParse(args[from + 3], NumberStyles.Float, inv, out var minutes)
                    || !double.IsFinite(minutes) || minutes < 0 || minutes > MaxCooldownMinutes)
                    return $"Give <wcid> <amount 1-{MaxAmount:N0}> <kills 1-{MaxKills:N0}> <minutes 0-{MaxCooldownMinutes:N0}>.";

                var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
                if (weenie == null)
                    return $"There is no WCID {wcid}.";
                if (!IsGivableItem(wcid))
                    return $"WCID {wcid} is a {weenie.WeenieType}, not an item a player can carry.";

                entry = new BountyEntry { Wcid = wcid, Amount = amount, Kills = kills, CooldownMinutes = minutes };
                return null;
            }

            string ReadId(int from, out int index)
            {
                index = -1;
                if (args.Count <= from || !int.TryParse(args[from], NumberStyles.Integer, inv, out var id) || id < 1)
                    return "Which reward? Give its ID (the Bounty list shows them: bounty <zone> show, or the dungeon's Settings).";
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
                    entry.Id = cfg.Entries[index].Id;   // the same reward, edited: last save wins, its progress kept
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

                case "target":
                {
                    // server-wide bounty targets (2026-10-06): target add|remove <creature wcid> | target clear. Only the server-wide
                    // bounty reads them, so every other bounty refuses the verb rather than store targets that change nothing.
                    if (!allowTargets)
                        return "Targets belong to the server-wide bounty (serverbounty target ...); this bounty counts every kill in its area.";
                    cfg.TargetWcids ??= new List<uint>();
                    var sub = args.Count > at ? args[at].ToLowerInvariant() : "";
                    // emptying the list would silently turn a targeted bounty into "any kill, anywhere" - it is switched OFF instead
                    // (the precedent: removing the last reward turns a bounty off); turn it on again to mean every kill
                    if (sub == "clear")
                    {
                        if (cfg.TargetWcids.Count > 0) cfg.Enabled = false;
                        cfg.TargetWcids.Clear();
                        return null;
                    }
                    if ((sub != "add" && sub != "remove") || args.Count <= at + 1 || !uint.TryParse(args[at + 1], NumberStyles.Integer, inv, out var tw) || tw == 0)
                        return "Give: " + TargetUsage + ".";
                    if (sub == "add")
                    {
                        var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(tw);
                        if (weenie == null) return $"There is no WCID {tw}.";
                        // a monster: players, pets and objects never reach the kill hook, so a bounty on one could never pay
                        if (weenie.WeenieType != WeenieType.Creature && weenie.WeenieType != WeenieType.Cow)
                            return $"WCID {tw} is a {weenie.WeenieType}, not a monster.";
                        if (cfg.TargetWcids.Contains(tw)) return $"WCID {tw} is already a target.";
                        if (cfg.TargetWcids.Count >= BountyConfig.MaxTargets)
                            return $"A bounty holds at most {BountyConfig.MaxTargets} targets.";
                        cfg.TargetWcids.Add(tw);
                    }
                    else if (!cfg.TargetWcids.Remove(tw))
                        return $"WCID {tw} is not a target.";
                    else if (cfg.TargetWcids.Count == 0)
                        cfg.Enabled = false;   // the last target gone: off, not "any kill" (see clear)
                    return null;
                }

                default:
                    return "Usage: " + EditUsage + (allowTargets ? " | " + TargetUsage : "");
            }
        }

        /// <summary>
        /// A reward goes into a pack, so only the kinds of object a player carries are allowed (review 2026-09-24: an
        /// ALLOW-list - a deny-list let pets, combat pets and world-only objects through), and never one that is Stuck
        /// (placed in the world). Checked when a reward is set and again before one is handed over.
        /// </summary>
        public static bool IsGivableItem(uint wcid)
        {
            var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null)
                return false;

            if (weenie.PropertiesBool != null && weenie.PropertiesBool.TryGetValue(PropertyBool.Stuck, out var stuck) && stuck)
                return false;

            switch (weenie.WeenieType)
            {
                case WeenieType.Generic:
                case WeenieType.Clothing:
                case WeenieType.MissileLauncher:
                case WeenieType.Missile:
                case WeenieType.Ammunition:
                case WeenieType.MeleeWeapon:
                case WeenieType.Book:
                case WeenieType.Coin:
                case WeenieType.Food:
                case WeenieType.Container:
                case WeenieType.Key:
                case WeenieType.Lockpick:
                case WeenieType.Healer:
                case WeenieType.LightSource:
                case WeenieType.SpellComponent:
                case WeenieType.Scroll:
                case WeenieType.Caster:
                case WeenieType.ManaStone:
                case WeenieType.Gem:
                case WeenieType.CraftTool:
                case WeenieType.Stackable:
                case WeenieType.Deed:
                case WeenieType.SkillAlterationDevice:
                case WeenieType.AttributeTransferDevice:
                case WeenieType.AugmentationDevice:
                case WeenieType.PetDevice:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>One line for chat: "ON: 1x Pyreal every 100 kills (5 min); ...".</summary>
        public static string Describe(BountyConfig cfg)
        {
            if (cfg == null || cfg.Entries.Count == 0)
                return (cfg?.Enabled == true ? "ON" : "off") + ": no rewards";

            var list = string.Join("; ", cfg.Entries.Where(e => e != null).Select(e =>
                $"[{e.Id}] {e.Amount}x {RoomAssignManager.ItemName(e.Wcid)} every {e.Kills} kills ({e.CooldownMinutes.ToString("0.##", CultureInfo.InvariantCulture)} min)"));
            return (cfg.Enabled ? "ON" : "off") + ": " + list + TargetsText(cfg);
        }

        /// <summary>" - kill: Grandcap the Overgrown (730000711), ..." for a bounty with targets (server-wide), else "".</summary>
        public static string TargetsText(BountyConfig cfg)
        {
            if (cfg?.TargetWcids == null || cfg.TargetWcids.Count == 0) return "";
            // at most 10 named (a chat line must stay short); the plugin card lists them all
            const int shown = 10;
            var more = cfg.TargetWcids.Count - shown;
            return " - kill: " + string.Join(", ", cfg.TargetWcids.Take(shown).Select(w => $"{RoomAssignManager.ItemName(w)} ({w})"))
                + (more > 0 ? $" and {more} more" : "");
        }

        /// <summary>
        /// The rewards for the plugin: entries joined by '+', each "id:wcid:amount:kills:minutes:name" - the name with every
        /// separator the zone list or the dungeon line uses taken out. The id is what the plugin's edits name. Works on a copy:
        /// reading the list never changes the live settings (IDs for old data are given the same way Edit gives them).
        /// </summary>
        public static string Wire(BountyConfig cfg)
        {
            if (cfg?.Entries == null || cfg.Entries.Count == 0) return "";
            var copy = cfg.Clone();
            copy.EnsureIds();
            return string.Join("+", copy.Entries.Where(e => e != null).Select(e =>
                e.Id + ":" + e.Wcid + ":" + e.Amount + ":" + e.Kills + ":" + e.CooldownMinutes.ToString("0.###", CultureInfo.InvariantCulture) + ":"
                + RoomAssignManager.BuilderWireName(RoomAssignManager.ItemName(e.Wcid))));
        }

        // ── Delivery ─────────────────────────────────────────────────────────

        /// <summary>Why a hand-over stopped short. Only NoRoom is worth a quick retry; the others need the player or an admin.</summary>
        /// <remarks>More = the per-try stack limit was reached with room to spare: the rest follows on the next heartbeat.</remarks>
        private enum DeliverResult { Done, More, NoRoom, Refused, Broken }

        /// <summary>
        /// A completed bounty: into the pack, and whatever does not go in is HELD on the character - never dropped, never lost,
        /// even if handing it over throws part way. The chat line says why it was held.
        ///
        /// Wording (owner 2026-09-23), in chat as "Bounty":
        ///   completed: "Bounty complete! +1 Ascension Coin gained."  /  "Next bounty unlocks in 5 min - 100 kills required."
        ///   held:      "Bounty: Your inventory is full. +1 Ascension Coin gained (3 total). They're safe until you make room."
        /// </summary>
        private static void Award(Player player, BountyEntry reward, double awardedUnix, double sharedLockUntil)
        {
            var given = 0;
            var names = new ItemNames(reward.Wcid);
            var result = DeliverResult.Broken;

            try
            {
                result = Deliver(player, reward.Wcid, reward.Amount, ref given, names);
            }
            finally
            {
                var left = reward.Amount - given;
                if (left > 0)
                    AddOwed(player, reward.Wcid, left);
            }

            if (given > 0)
                Tell(player, $"Bounty complete! +{given:N0} {names.For(given)} gained.");

            var held = reward.Amount - given;
            if (held > 0)
                Tell(player, HeldLine(player, reward.Wcid, held, names, result));

            Tell(player, NextBountyLine(reward, awardedUnix, sharedLockUntil));
        }

        /// <summary>The held line, with the real reason (review 2026-09-24: "inventory is full" was said for every failure).</summary>
        private static string HeldLine(Player player, uint wcid, int held, ItemNames names, DeliverResult why)
        {
            switch (why)
            {
                case DeliverResult.More:
                    return $"Bounty: +{held:N0} more {names.For(held)} will follow in a moment.";

                case DeliverResult.Refused:
                    return $"Bounty: +{held:N0} {names.For(held)} could not be put in your pack (you may already carry one). Held for you - /bounty lists it.";

                case DeliverResult.Broken:
                    return $"Bounty: +{held:N0} {names.For(held)} could not be made right now. Held for you - /bounty lists it. Please tell an admin.";

                default:
                    var total = OwedOf(player, wcid);
                    return $"Bounty: Your inventory is full. +{held:N0} {names.For(held)} gained"
                        + (total > held ? $" ({total:N0} total)" : "")
                        + (total == 1 ? ". It's safe until you make room." : ". They're safe until you make room.");
            }
        }

        /// <summary>The item's singular and plural name, read off the first one made (or the weenie, if none was).</summary>
        private sealed class ItemNames
        {
            private readonly uint wcid;
            public string Single, Plural;

            public ItemNames(uint wcid) => this.wcid = wcid;

            public string For(int n)
            {
                Single ??= RoomAssignManager.ItemName(wcid);
                Plural ??= Single;
                return n == 1 ? Single : Plural;
            }
        }

        /// <summary>
        /// Puts up to <paramref name="amount"/> of the item into the pack, as many stacks as it needs, and stops at the first
        /// one that does not go in. <paramref name="given"/> counts what actually went in, updated after every stack, so a
        /// caller knows the real number even if this throws. Never holds anything itself. The result says why it stopped:
        /// NoRoom (slots or burden), Refused (there was room, the pack still said no - a second ability charm, say), Broken
        /// (the WCID cannot be made, or is no longer an item a player can carry).
        /// </summary>
        private static DeliverResult Deliver(Player player, uint wcid, int amount, ref int given, ItemNames names)
        {
            if (!IsGivableItem(wcid))
                return DeliverResult.Broken;

            var guard = 0;

            while (given < amount && guard++ < 64)
            {
                var item = WorldObjectFactory.CreateNewWorldObject(wcid);
                if (item == null)
                    return DeliverResult.Broken;

                // Always set the stack size of a stackable item, so a weenie whose default stack is 5 is not handed out as
                // "1"; a MaxStackSize of 0 is treated as not stackable.
                var stack = 1;
                if (item.MaxStackSize.HasValue && item.MaxStackSize.Value > 0)
                {
                    stack = Math.Min(amount - given, item.MaxStackSize.Value);
                    item.SetStackSize(stack);
                }
                names.Single ??= item.Name;
                names.Plural ??= item.GetPluralName();

                if (!player.HasEnoughBurdenToAddToInventory(item))
                {
                    item.Destroy();
                    return DeliverResult.NoRoom;
                }

                if (!player.TryCreateInInventoryWithNetworking(item))
                {
                    item.Destroy();

                    // No free slot is "no room" (a container reward needs a container slot, which this does not count, so
                    // it is always treated as room trouble); a free slot and still refused is something else.
                    return item is Container || player.GetFreeInventorySlots() < 1 ? DeliverResult.NoRoom : DeliverResult.Refused;
                }

                given += stack;
            }

            // The 64-stack guard stopped it with more still to give: not "no room" - the rest comes on the next heartbeat.
            return given < amount ? DeliverResult.More : DeliverResult.Done;
        }

        /// <summary>
        /// "Next bounty unlocks in 2 min 30 sec - 100 kills required." - or, with no cooldown, "Next bounty requires 100 kills."
        /// The wait is the REAL time until this player's next bounty can start (owner 2026-09-23): the cooldown's end, from when
        /// this bounty was awarded, to the second - never rounded (2.5 min is "2 min 30 sec", not "3 min").
        /// </summary>
        private static string NextBountyLine(BountyEntry reward, double awardedUnix, double sharedLockUntil)
        {
            var kills = KillsText(reward.Kills);
            // its own cooldown, or the shared lock when a longer reward paid on the same kill (no cooldown = no wait)
            var unlockAt = reward.CooldownSeconds > 0 ? Math.Max(awardedUnix + reward.CooldownSeconds, sharedLockUntil) : awardedUnix;
            var seconds = (int)Math.Ceiling(Math.Min(unlockAt - Time.GetUnixTime(), int.MaxValue));
            if (seconds <= 0)
                return $"Next bounty requires {kills}.";
            return $"Next bounty unlocks in {FormatWait(seconds)} - {kills} required.";
        }

        private static string KillsText(int kills) => kills == 1 ? "1 kill" : $"{kills:N0} kills";

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
        private static long OwedOf(Player player, uint wcid)
            => ParseOwed(player.GetProperty(PropertyString.BountyOwed)).Where(o => o.Wcid == wcid).Sum(o => (long)o.Amount);

        /// <summary>
        /// How long a delivery that handed nothing over waits before the next try, by why it stopped: room trouble is retried
        /// soon and silently; a refusal or a broken item only now and then, and the player is told why (review 2026-09-24:
        /// these used to retry every 30 s forever, warning each time).
        /// </summary>
        private static double RetryAfterSeconds(DeliverResult why)
            => why == DeliverResult.More ? 0 : why == DeliverResult.NoRoom ? DeliveryRetrySeconds : why == DeliverResult.Refused ? 10 * 60 : 60 * 60;

        /// <summary>
        /// Player heartbeat (the player's own thread): hands over anything held, as soon as it goes in. One property read when
        /// nothing is held. Also runs right after login, so a reward earned before a logout arrives on the next login. The held
        /// list is only ever rewritten with what is still owed - never cleared first - so a failure part way loses nothing.
        /// A try that hands nothing over waits (RetryAfterSeconds) before the next.
        /// </summary>
        public static void TryDeliverOwed(Player player)
        {
            try
            {
                if (player == null)
                    return;

                var raw = player.GetProperty(PropertyString.BountyOwed);
                if (string.IsNullOrEmpty(raw))
                {
                    _nextDeliveryTry.TryRemove(player.Guid.Full, out _);
                    return;
                }

                var now = Time.GetUnixTime();
                if (_nextDeliveryTry.TryGetValue(player.Guid.Full, out var nextTry) && now < nextTry)
                    return;

                var owed = ParseOwed(raw);
                var remaining = new List<(uint Wcid, int Amount)>(owed);
                var anyGiven = false;
                var worst = DeliverResult.Done;

                try
                {
                    for (var i = 0; i < owed.Count; i++)
                    {
                        var (wcid, amount) = owed[i];
                        var given = 0;
                        var names = new ItemNames(wcid);
                        var result = DeliverResult.Broken;

                        try
                        {
                            result = Deliver(player, wcid, amount, ref given, names);
                        }
                        finally
                        {
                            remaining[i] = (wcid, amount - given);
                        }

                        if (given > 0)
                        {
                            anyGiven = true;
                            Tell(player, $"Bounty: +{given:N0} {names.For(given)} delivered. Your held rewards are in your inventory.");
                        }

                        if (result == DeliverResult.Done)
                            continue;

                        if (result == DeliverResult.Broken)
                            log.Warn($"[Bounty] WCID {wcid} cannot be handed over to {player.Name} (cannot be made, or not a carryable item) - {amount - given} still held; next try in an hour.");
                        else if (result == DeliverResult.Refused && given == 0)
                            Tell(player, HeldLine(player, wcid, amount, names, result));

                        if (result > worst)
                            worst = result;
                    }
                }
                finally
                {
                    WriteOwed(player, remaining);

                    if (remaining.Any(o => o.Amount > 0) && !anyGiven)
                        SetNextTry(player.Guid.Full, now + RetryAfterSeconds(worst == DeliverResult.Done ? DeliverResult.NoRoom : worst), now);
                    else
                        _nextDeliveryTry.TryRemove(player.Guid.Full, out _);
                }
            }
            catch (Exception ex)
            {
                log.Error($"[Bounty] TryDeliverOwed: {ex}");
            }
        }

        /// <summary>Sets a player's next delivery try; past 512 entries, drops the ones already due (players long gone).</summary>
        private static void SetNextTry(uint guid, double at, double now)
        {
            _nextDeliveryTry[guid] = at;

            if (_nextDeliveryTry.Count > 512)
                foreach (var kv in _nextDeliveryTry)
                    if (kv.Value <= now)
                        _nextDeliveryTry.TryRemove(kv.Key, out _);
        }

        private static void AddOwed(Player player, uint wcid, int amount)
        {
            var owed = ParseOwed(player.GetProperty(PropertyString.BountyOwed));
            var at = owed.FindIndex(o => o.Wcid == wcid);
            if (at >= 0) owed[at] = (wcid, (int)Math.Min((long)owed[at].Amount + amount, int.MaxValue));
            else owed.Add((wcid, amount));

            WriteOwed(player, owed);
        }

        private static void WriteOwed(Player player, List<(uint Wcid, int Amount)> owed)
        {
            var left = owed.Where(o => o.Amount > 0).ToList();
            if (left.Count == 0)
                player.RemoveProperty(PropertyString.BountyOwed);
            else
                player.SetProperty(PropertyString.BountyOwed, string.Join(";", left.Select(o => o.Wcid + ":" + o.Amount)));
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
            var raw = player.GetProperty(PropertyString.BountyProgress);
            if (string.IsNullOrWhiteSpace(raw)) return map;

            foreach (var part in raw.Split(';'))
            {
                var f = part.Split('|');
                if (f.Length != 3 || f[0].Length == 0) continue;
                int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kills);
                double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var last);
                map[f[0]] = new Entry { Kills = Math.Max(kills, 0), LastAward = double.IsFinite(last) ? last : 0 };
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

            var text = sb.ToString();
            if (text != player.GetProperty(PropertyString.BountyProgress))
                player.SetProperty(PropertyString.BountyProgress, text);
        }

        /// <summary>A zone name as a key: the separators the progress string uses are taken out.</summary>
        private static string Clean(string s)
            => (s ?? "").Replace('|', ' ').Replace(';', ' ').Replace('#', ' ').Trim();

        private static void Tell(Player player, string text)
            => player.Session?.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
