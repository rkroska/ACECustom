using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.TieredDungeons
{
    /// <summary>
    /// Room credit and pay for a tiered dungeon run (owner 2026-10-09).
    ///
    /// Retail credits a room when a player walks through its exit portal, and those portals stay open - a late joiner
    /// could walk the whole arena in minutes and claim full pay. A tier copy credits the players STANDING IN the room
    /// when its last wave passes, and pays from those credits.
    ///
    /// Everything here runs inside the tier scope of the acting object (a room coordinator, the tier Arbitrator), so the
    /// credit flags are tier-tagged like every other flag. Only the pay timer is shared across tiers.
    /// </summary>
    public static class TieredDungeonRuns
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Marks the start of a player's current run: stamped when the first room is credited.</summary>
        public const string RunQuest = "TieredColoRun";

        /// <summary>A credit belongs to the current run only this long after the run's first room (the arena lock is one hour).</summary>
        public const int RunWindowSeconds = 3900;

        private static bool contentChecked, contentOk;

        /// <summary>
        /// The credit and timer flags need their world quest rows (Database/Updates/World/2026-10-09-00-Tiered-Colosseum.sql).
        /// Without them SetQuestCompletions stores every credit as 0 and the pay timer can never be solved again - both
        /// silently. A missing row is logged as an error and nothing is credited or paid.
        /// </summary>
        private static bool ContentReady()
        {
            if (contentChecked)
                return contentOk;

            var missing = new[] { TieredDungeonRegistry.RoomCreditQuest, TieredDungeonRegistry.TopTierQuest, RunQuest, TieredDungeonRegistry.PayTimerQuest }
                .Where(q => Database.DatabaseManager.World.GetCachedQuest(q) == null).ToList();
            if (WeaponScaling.WeaponScalingManager.GetTier(TieredDungeonRegistry.MinTier) == null)
                missing.Add("weapon scaling tier rows");

            contentOk = missing.Count == 0;
            contentChecked = contentOk;   // keep checking until the content is there
            if (!contentOk)
                log.Error($"[TIERED] tiered dungeon content is missing ({string.Join(", ", missing)}) - no room is credited and nothing is paid. Apply 2026-10-09-00-Tiered-Colosseum.sql.");
            return contentOk;
        }

        /// <summary>Players this far from the coordinator along the arena's long axis are in its room (rooms are 90 apart).</summary>
        private const float RoomHalfDepth = 45f;

        // ------------------------------------------------------------------ who fought in a room (owner 2026-11-11)
        //
        // Standing in a room when it clears is not enough: an idle character in the corner would be paid in full. Every
        // kill in a tier copy room records which players (or their pets) damaged it; when the room clears, only players who
        // damaged at least tiered_dungeon_min_room_tag_share of its monsters are credited. Counting monsters rather than
        // damage keeps a weak player who fights in a strong group eligible (same-tier damage spreads about 37:1).

        private sealed class RoomTally
        {
            public double Started;
            public int Kills;
            public readonly Dictionary<uint, int> Tags = new();
        }

        private static readonly object tallyLock = new();
        private static readonly Dictionary<(ushort Landblock, int Variation, int Room), RoomTally> tallies = new();

        /// <summary>Called for every creature death. Does nothing unless the creature belongs to a tier copy room.</summary>
        public static void OnMonsterDied(Creature victim)
        {
            try
            {
                if (victim == null || victim is Player || victim is Pet)
                    return;
                var room = TieredDungeonRegistry.RoomOf(victim);
                var pos = victim.Location;
                if (room <= 0 || pos?.Variation == null)
                    return;

                var fought = new HashSet<uint>();
                foreach (var d in victim.DamageHistory.Damagers)
                {
                    if (d == null || d.TotalDamage <= 0)
                        continue;
                    var player = d.TryGetPetOwnerOrAttacker() as Player;
                    if (player != null)
                        fought.Add(player.Guid.Full);
                }

                var now = Time.GetUnixTime();
                var key = (pos.LandblockId.Landblock, pos.Variation.Value, room);
                lock (tallyLock)
                {
                    if (!tallies.TryGetValue(key, out var tally) || now - tally.Started > RunWindowSeconds)
                        tallies[key] = tally = new RoomTally { Started = now };
                    tally.Kills++;
                    foreach (var guid in fought)
                        tally.Tags[guid] = tally.Tags.GetValueOrDefault(guid) + 1;
                }
            }
            catch (Exception ex)
            {
                log.Error($"[TIERED] OnMonsterDied: {ex}");
            }
        }

        /// <summary>Takes the tally of a room that just cleared. Clearing the first room starts a run, so any tally left in
        /// that arena by an earlier, abandoned run is dropped too.</summary>
        private static RoomTally TakeTally(ushort landblock, int variation, int room, int firstRoom)
        {
            lock (tallyLock)
            {
                tallies.Remove((landblock, variation, room), out var tally);
                if (room == firstRoom)
                    foreach (var k in tallies.Keys.Where(k => k.Landblock == landblock && k.Variation == variation).ToList())
                        tallies.Remove(k);
                return tally;
            }
        }

        /// <summary>
        /// Called for every LocalSignal an object sends. Does nothing unless the sender is inside a tier copy and the
        /// signal is that dungeon's room-cleared signal.
        /// </summary>
        public static void OnLocalSignal(WorldObject source, string signal)
        {
            if (TieredDungeonRegistry.ScopeTier < TieredDungeonRegistry.MinTier || source?.Location == null || string.IsNullOrEmpty(signal))
                return;

            var dungeon = TieredDungeonRegistry.FindCopyAt(source.Location.LandblockId.Landblock, source.Location.Variation);
            if (dungeon == null || !signal.Equals(dungeon.RoomClearedSignal, StringComparison.OrdinalIgnoreCase))
                return;

            var room = TieredDungeonRegistry.RoomAt(dungeon, source.Location.PositionY);
            var landblock = source.CurrentLandblock;
            if (room == 0 || landblock == null || !ContentReady())
                return;

            var present = landblock.players
                .Where(p => p?.Location != null && Math.Abs(p.Location.PositionY - source.Location.PositionY) <= RoomHalfDepth)
                .ToList();

            // only players who fought in the room (see RoomTally)
            var tally = TakeTally(source.Location.LandblockId.Landblock, source.Location.Variation.Value, room, dungeon.FirstRoom);
            var share = ServerConfig.tiered_dungeon_min_room_tag_share.Value;
            if (tally == null || tally.Kills == 0)
            {
                if (share > 0 && present.Count > 0)
                    log.Warn($"[TIERED] {dungeon.Name} T{TieredDungeonRegistry.ScopeTier} room {room} cleared with no kills recorded - credited by presence");
            }
            else
            {
                var need = TieredDungeonRegistry.RequiredTags(tally.Kills, share);
                var idle = present.Where(p => tally.Tags.GetValueOrDefault(p.Guid.Full) < need).ToList();
                if (idle.Count > 0)
                {
                    log.Info($"[TIERED] {dungeon.Name} T{TieredDungeonRegistry.ScopeTier} room {room}: not credited (damaged under {need} of {tally.Kills}): {string.Join(", ", idle.Select(p => $"{p.Name} {tally.Tags.GetValueOrDefault(p.Guid.Full)}"))}");
                    present = present.Except(idle).ToList();
                }
            }
            if (present.Count == 0)
                return;

            var now = (uint)Time.GetUnixTime();
            var runName = QuestManager.GetQuestName(RunQuest);   // tier-tagged by the scope

            // the highest tier among everyone credited in this room, and whatever each of them already carries
            var top = 0;
            foreach (var p in present)
            {
                var run = p.QuestManager.GetQuest(runName);
                var inRun = room != dungeon.FirstRoom && run != null && now - run.LastTimeCompleted <= RunWindowSeconds;
                top = Math.Max(top, TieredDungeonRegistry.PlayerTier(p));
                if (inRun)
                    top = Math.Max(top, p.QuestManager.GetCurrentSolves(TieredDungeonRegistry.TopTierQuest));
            }

            foreach (var p in present)
            {
                var run = p.QuestManager.GetQuest(runName);
                var bits = 0;

                // The first room starts a new run. A credit for a later room joins the run only while that run is still
                // inside its window, so credits left over from an older run never combine with this one.
                if (room == dungeon.FirstRoom)
                {
                    p.QuestManager.Erase(RunQuest);
                    p.QuestManager.Stamp(RunQuest);
                }
                else if (run != null && now - run.LastTimeCompleted <= RunWindowSeconds)
                    bits = p.QuestManager.GetCurrentSolves(TieredDungeonRegistry.RoomCreditQuest);

                bits |= 1 << (room - 1);
                p.QuestManager.SetQuestCompletions(TieredDungeonRegistry.RoomCreditQuest, bits);
                p.QuestManager.SetQuestCompletions(TieredDungeonRegistry.TopTierQuest, top);
            }

            log.Debug($"[TIERED] {dungeon.Name} T{TieredDungeonRegistry.ScopeTier} room {room} cleared in 0x{source.Location.LandblockId.Landblock:X4}: credited {string.Join(", ", present.Select(p => p.Name))} (top tier {top})");
        }

        /// <summary>
        /// The tier Arbitrator's pay action (EmoteType.AwardTieredDungeonPay). Pays Prestige Coins into the player's
        /// bank for the furthest room they were credited with, scaled by the tier rule, at most once per pay timer.
        /// </summary>
        public static void Pay(WorldObject npc, Player player)
        {
            if (player == null || npc?.Location == null)
                return;

            var tier = TieredDungeonRegistry.ScopeTier;
            var dungeon = TieredDungeonRegistry.FindCopyAt(npc.Location.LandblockId.Landblock, npc.Location.Variation);
            if (tier < TieredDungeonRegistry.MinTier || dungeon == null)
                return;

            if (!ContentReady())
            {
                Tell(npc, player, "I cannot reward anyone at the moment. Please tell an admin.");
                return;
            }

            var quests = player.QuestManager;

            // Credits are only ever written for the current run (OnLocalSignal), and they wait here until claimed -
            // the pay timer may hold a claim back for hours, so they must not expire at the Arbitrator.
            var bits = quests.GetCurrentSolves(TieredDungeonRegistry.RoomCreditQuest);
            var furthest = TieredDungeonRegistry.FurthestRoom(bits, dungeon.FirstRoom, dungeon.RoomCount);
            // the table is in T11 coins; each tier above pays the per-kill tier step more (owner 2026-11-11)
            var fullCoins = TieredDungeonRegistry.ScaleForTier(
                TieredDungeonRegistry.CoinsForRoom(ServerConfig.tiered_dungeon_pay_table.Value, furthest), tier, ServerConfig.zc_pcoin_kill_tier_step.Value);

            if (fullCoins <= 0)
            {
                Tell(npc, player, furthest == 0
                    ? "You have no arena rooms to your name. Clear the rooms with your fellowship and return to me."
                    : $"You reached room {furthest}. I reward those who clear room 13 and beyond.");
                return;
            }

            // the shared pay timer - checked before anything is spent, so a claim made too early keeps its credits
            if (quests.HasQuest(TieredDungeonRegistry.PayTimerQuest) && !quests.CanSolve(TieredDungeonRegistry.PayTimerQuest))
            {
                var wait = quests.GetNextSolveTime(TieredDungeonRegistry.PayTimerQuest).GetFriendlyString();
                Tell(npc, player, $"You were rewarded too recently. Return in {wait} and I will honor room {furthest}.");
                return;
            }

            var playerTier = TieredDungeonRegistry.PlayerTier(player);
            var topTier = Math.Max(playerTier, quests.GetCurrentSolves(TieredDungeonRegistry.TopTierQuest));
            var percents = ServerConfig.tiered_dungeon_pay_percents.Value;
            if (TieredDungeonRegistry.PayPercent(percents, tier, tier, tier) <= 0)
            {
                // the setting cannot be read (an on-tier player would get nothing): keep the credits, pay no one
                log.Error($"[TIERED] tiered_dungeon_pay_percents \"{percents}\" gives an on-tier player 0 percent - nothing paid, credits kept.");
                Tell(npc, player, "I cannot reward anyone at the moment. Please tell an admin.");
                return;
            }
            var percent = TieredDungeonRegistry.PayPercent(percents, tier, playerTier, topTier);
            var coins = TieredDungeonRegistry.ScaleCoins(fullCoins, percent);

            // the run is settled either way
            quests.Erase(TieredDungeonRegistry.RoomCreditQuest);
            quests.Erase(TieredDungeonRegistry.TopTierQuest);
            quests.Erase(RunQuest);

            if (coins <= 0)
            {
                Tell(npc, player, topTier > playerTier
                    ? "Your fellowship was carried by one far beyond this arena. There is no reward in that."
                    : "This arena is far beneath you. There is no reward for it.");
                return;
            }

            player.BankedPrestigeCoins = (player.BankedPrestigeCoins ?? 0) + coins;
            quests.Stamp(TieredDungeonRegistry.PayTimerQuest);

            var reduced = percent < 100 ? $" ({percent} percent of the full reward)" : "";
            Tell(npc, player, $"Well fought. You cleared room {furthest}.");
            player.SendMessage($"You have won {coins} Prestige Coin{(coins == 1 ? "" : "s")}{reduced}. They are in your bank.", ChatMessageType.Broadcast);

            log.Info($"[TIERED] {player.Name} paid {coins} Prestige Coins: {dungeon.Name} T{tier}, room {furthest}, player tier {playerTier}, top tier {topTier}, {percent} pct");
        }

        private static void Tell(WorldObject npc, Player player, string message)
            => player.SendMessage($"{npc.Name} tells you, \"{message}\"", ChatMessageType.Tell);
    }
}
