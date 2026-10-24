using System;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Tier rewards (owner 2026-10-24, locked) - the per-kill side. Every T11+ Zone Control kill banks Prestige Coins for the
    /// TOP DAMAGER only (a pet's kill counts for its owner): the zone's value at that tier x the killer's tier-dip credit.
    /// Coins are not split like XP and are not loot - they go straight to BankedPrestigeCoins, and the part of a coin not
    /// yet whole is carried on the character (PropertyFloat.PrestigeCoinFraction). Valley of Death / T10 and every retail
    /// kill are untouched: a kill only pays when Zone Control governs the creature at T11 or higher. Bounties are separate
    /// and pay no coins from here.
    ///
    /// The victim's thread only reads the kill (tier, top damager). Everything about the player - their own tier, the
    /// fraction, the bank - is read and written on the player's own thread (LandblockManager.RunOnThreadFor), exactly as
    /// BountyManager hands off its credit. No save is forced: the regular character save stores both properties.
    /// </summary>
    public static class TierRewards
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Called once per creature death (Creature.OnDeath), on the victim's thread.</summary>
        public static void OnCreatureKilled(Creature victim)
        {
            try
            {
                if (victim == null || victim is Player || victim is Pet)
                    return;

                var killTier = TierRewardScale.KillTier(victim);
                if (killTier == 0)
                    return;

                // only kills that pay kill XP or luminance (the bounty rule): a summoned add or a no-XP spawn is no coin farm
                if (!BountyManager.PaysBounty(victim))
                    return;

                // read once here so a value changed mid-kill cannot disagree with itself; the dip is applied on the player's thread
                var zoneValue = TierRewardScale.ZoneValue(killTier);
                if (!(zoneValue > 0))
                    return;

                // The ONLINE player, never the object the damage history remembers (a weak reference that can outlive a logout).
                // Mules and Olthoi earn nothing here, as with bounties: they get no kill XP and Zone Share leaves them out too.
                var attacker = victim.DamageHistory.TopDamager?.TryGetPetOwnerOrAttacker() as Player;
                var player = attacker != null ? PlayerManager.GetOnlinePlayer(attacker.Guid) : null;
                if (player == null || !ReferenceEquals(player, attacker) || player.IsLoggingOut || player.IsMule || player.IsOlthoiPlayer)
                    return;

                LandblockManager.RunOnThreadFor(player, ActionType.TierRewards_PrestigeCoins, () => CreditCoins(player, victim, killTier, zoneValue));
            }
            catch (Exception ex)
            {
                log.Error($"[TierRewards] OnCreatureKilled: {ex}");
            }
        }

        /// <summary>The player-thread half: apply the dip, carry the fraction, bank the whole coins.</summary>
        private static void CreditCoins(Player player, Creature victim, int killTier, double zoneValue)
        {
            try
            {
                // Still the online character when this runs (it may have been queued to the world thread meanwhile). A bank write
                // after the final logout save was issued would never be stored.
                if (player.IsLoggingOut || !ReferenceEquals(PlayerManager.GetOnlinePlayer(player.Guid), player))
                    return;

                var credit = TierRewardScale.CreditFor(player, killTier);
                var earned = zoneValue * credit;
                if (!(earned > 0))
                    return;

                var (whole, fraction) = TierRewardScale.AddCoins(player.GetProperty(PropertyFloat.PrestigeCoinFraction) ?? 0, earned);

                if (fraction > 0)
                    player.SetProperty(PropertyFloat.PrestigeCoinFraction, fraction);
                else
                    player.RemoveProperty(PropertyFloat.PrestigeCoinFraction);

                if (whole <= 0)
                    return;

                // economy ledger: a labeled faucet (Kill, this creature), never an unexplained bank change
                using (PyrealLedger.BeginFor(EconomyLedger.SrcKill, victim, player))
                    player.BankedPrestigeCoins = (player.BankedPrestigeCoins ?? 0) + whole;

                player.SendMessage($"{whole:N0} Prestige Coin{(whole == 1 ? " has" : "s have")} been added to your bank.", ChatMessageType.Broadcast);
            }
            catch (Exception ex)
            {
                log.Error($"[TierRewards] CreditCoins for {player?.Name}: {ex}");
            }
        }

        // ---- kill-task credit (T11+ Zone Control kills only) ---------------------------------------------------------------

        /// <summary>One damager's line of the kill: who (a pet's damage counts for its owner), and who they share with.</summary>
        private sealed class KillTaskRoute
        {
            public Player Damager;
            public bool ByPet;
            /// <summary>Fellows in kill-task range, or the other members of the Zone Share area - never the damager.</summary>
            public List<Player> Sharers;
            /// <summary>Everyone in range of this damager, the damager and other taggers included - the fellowship share size.</summary>
            public int GroupSize;
        }

        /// <summary>
        /// Kill-task credit for a kill (owner 2026-10-24, locked) - ONLY when Zone Control governs the creature at T11+ and
        /// zc_kt_decimal_credit_enabled is on. Returns false for every other kill, and the caller then runs the stock kill-task
        /// code exactly as before.
        ///
        /// Who gets credit, and how many credits, is the stock rule unchanged (the same fellow_kt_killer gate, the same
        /// summoning_killtask_multicredit_cap per player and per pet, Zone Share in place of the fellowship). What one credit is
        /// WORTH is new: a full kill for anyone who damaged the monster (a tagger), the fellowship share
        /// (Fellowship.GetMemberSharePercent of everyone in range) for a sharer who did not - full on a one-solve task - and
        /// everyone's credit x their own tier-dip credit, which is read on their own thread. Progress is decimal
        /// (QuestManager.HandleKillTaskDecimal). The recipient set is built once per kill and reused for all three kill quests.
        /// </summary>
        public static bool TryHandleKillTasks(Creature victim, params string[] killQuests)
        {
            if (!ServerConfig.zc_kt_decimal_credit_enabled.Value || victim == null || killQuests == null)
                return false;

            var quests = killQuests.Where(q => !string.IsNullOrEmpty(q)).ToList();
            if (quests.Count == 0)
                return false;

            var killTier = TierRewardScale.KillTier(victim);
            if (killTier == 0)
                return false;

            try
            {
                var routes = BuildKillTaskRoutes(victim);
                var taggers = new HashSet<Player>(routes.Select(r => r.Damager));

                // per recipient: every kill quest it is credited for, and the share credit before its tier dip
                var credits = new Dictionary<Player, List<(string Quest, double Credit)>>();

                foreach (var killQuest in quests)
                {
                    var singleSolve = QuestManager.GetMaxSolves(killQuest) == 1;

                    foreach (var (player, units, groupSize) in CountKillTaskUnits(routes, killQuest))
                    {
                        var perUnit = TierRewardScale.KillTaskCredit(taggers.Contains(player), groupSize, singleSolve, 1.0, Fellowship.GetMemberSharePercent);
                        var credit = units * perUnit;
                        if (!(credit > 0))
                            continue;

                        if (!credits.TryGetValue(player, out var list))
                            credits[player] = list = new List<(string, double)>();
                        list.Add((killQuest, credit));
                    }
                }

                foreach (var kv in credits)
                {
                    var player = kv.Key;
                    var list = kv.Value;

                    // a damager or sharer may be ticked by another landblock group: the quest write is theirs, on their thread
                    LandblockManager.RunOnThreadFor(player, ActionType.CreatureDeath_KillTaskCredit, () =>
                    {
                        foreach (var (quest, credit) in list)
                        {
                            try
                            {
                                player.QuestManager.HandleKillTaskDecimal(quest, victim, credit, killTier);
                            }
                            catch (Exception ex)
                            {
                                log.Error($"[TierRewards] kill task {quest} for {player.Name}: {ex}");
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                log.Error($"[TierRewards] TryHandleKillTasks: {ex}");
            }

            return true;
        }

        /// <summary>Every damager of the kill in damage-history order, with the people they share with. Built once per kill.</summary>
        private static List<KillTaskRoute> BuildKillTaskRoutes(Creature victim)
        {
            var routes = new List<KillTaskRoute>();
            var sharersFor = new Dictionary<Player, (List<Player> Sharers, int GroupSize)>();

            foreach (var kvp in victim.DamageHistory.TotalDamage)
            {
                if (kvp.Value.TotalDamage <= 0)
                    continue;

                var byPet = false;
                var damager = kvp.Value.TryGetAttacker() as Player;
                if (damager == null && kvp.Value.PetOwner != null)
                {
                    damager = kvp.Value.TryGetPetOwner();
                    byPet = true;
                }
                if (damager == null)
                    continue;

                // the same player can appear twice (their own hits and their pet's): their sharers are looked up once
                if (!sharersFor.TryGetValue(damager, out var share))
                {
                    // Zone Share (owner 2026-09-23): everyone in the area, as one fellowship, in place of the damager's own fellows
                    var zoneMembers = ZoneShareManager.MembersFor(damager);
                    if (zoneMembers != null)
                        share = (zoneMembers.Select(m => m.Member).Where(m => m != damager).ToList(), zoneMembers.Count);
                    else if (damager.Fellowship != null)
                    {
                        var fellows = damager.Fellowship.WithinRange(damager);
                        share = (fellows, fellows.Count + 1);
                    }
                    else
                        share = (new List<Player>(), 1);

                    sharersFor[damager] = share;
                }

                routes.Add(new KillTaskRoute { Damager = damager, ByPet = byPet, Sharers = share.Sharers, GroupSize = share.GroupSize });
            }

            return routes;
        }

        /// <summary>
        /// The stock credit COUNT for one kill quest (OnDeath_HandleKillTask's rules, unchanged): per player, how many credits
        /// they would have received, and the smallest group they were reached through (their best fellowship share).
        /// </summary>
        private static List<(Player Player, int Units, int GroupSize)> CountKillTaskUnits(List<KillTaskRoute> routes, string killQuest)
        {
            var summonCap = (int)ServerConfig.summoning_killtask_multicredit_cap.Value - 1;
            if (!ServerConfig.allow_summoning_killtask_multicredit.Value)
                summonCap = 0;

            var playerCredits = new Dictionary<Player, int>();
            var summonCredits = new Dictionary<Player, int>();
            var groupSize = new Dictionary<Player, int>();

            void Give(Player p, Dictionary<Player, int> dict, int cap, int size)
            {
                dict.TryGetValue(p, out var have);
                if (have >= cap)
                    return;
                dict[p] = have + 1;
                groupSize[p] = groupSize.TryGetValue(p, out var g) ? Math.Min(g, size) : size;
            }

            foreach (var route in routes)
            {
                var dict = route.ByPet ? summonCredits : playerCredits;
                var cap = route.ByPet ? summonCap : 1;
                if (cap <= 0)
                {
                    dict = playerCredits;
                    cap = 1;
                }

                if (route.Damager.QuestManager.HasQuest(killQuest))
                    Give(route.Damager, dict, cap, route.GroupSize);
                else if (!ServerConfig.fellow_kt_killer.Value)
                    continue;   // the stock gate, kept as it is

                foreach (var fellow in route.Sharers)
                {
                    if (fellow.QuestManager.HasQuest(killQuest))
                        Give(fellow, dict, cap, route.GroupSize);
                }
            }

            var result = new List<(Player, int, int)>(groupSize.Count);
            foreach (var p in groupSize.Keys)
            {
                playerCredits.TryGetValue(p, out var a);
                summonCredits.TryGetValue(p, out var b);
                result.Add((p, a + b, groupSize[p]));
            }
            return result;
        }
    }
}
