using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

using log4net;

using ACE.Common;
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
    /// The victim's thread only reads the kill (tier, top damager, the falloff numbers of the zone it died in). Everything about
    /// the player - their own tier, the falloff count, the fraction, the bank - is read and written on the player's own thread
    /// (LandblockManager.RunOnThreadFor), exactly as BountyManager hands off its credit. No save is forced: the regular character
    /// save stores the properties.
    ///
    /// FALLOFF (2026-11-01): coins stay as they are up to a pace of zc_pcoin_falloff_kills_per_hour (375) full-value kills an
    /// hour; every kill past that pace pays zc_pcoin_falloff_mult (0.5) of its coins. See TierRewardScale.Falloff.
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

                // the falloff numbers of the zone the kill happened in (its Default row, never a rank row or wcid bucket)
                var falloff = TierRewardScale.FalloffFor(ZoneControlManager.ResolveZoneDefaultRowForCreature(victim));

                // The ONLINE player, never the object the damage history remembers (a weak reference that can outlive a logout).
                // Mules and Olthoi earn nothing here, as with bounties: they get no kill XP and Zone Share leaves them out too.
                var attacker = victim.DamageHistory.TopDamager?.TryGetPetOwnerOrAttacker() as Player;
                var player = attacker != null ? PlayerManager.GetOnlinePlayer(attacker.Guid) : null;
                if (player == null || !ReferenceEquals(player, attacker) || player.IsLoggingOut || player.IsMule || player.IsOlthoiPlayer)
                    return;

                LandblockManager.RunOnThreadFor(player, ActionType.TierRewards_PrestigeCoins, () => CreditCoins(player, victim, killTier, zoneValue, falloff));
            }
            catch (Exception ex)
            {
                log.Error($"[TierRewards] OnCreatureKilled: {ex}");
            }
        }

        /// <summary>The player-thread half: apply the dip, run the falloff, carry the fraction, bank the whole coins.</summary>
        private static void CreditCoins(Player player, Creature victim, int killTier, double zoneValue, TierRewardScale.FalloffDials falloff)
        {
            try
            {
                // Still the online character when this runs (it may have been queued to the world thread meanwhile). A bank write
                // after the final logout save was issued would never be stored.
                if (player.IsLoggingOut || !ReferenceEquals(PlayerManager.GetOnlinePlayer(player.Guid), player))
                    return;

                var credit = TierRewardScale.CreditFor(player, killTier);
                if (!(zoneValue * credit > 0))
                    return;

                // past the hourly pace a kill pays a share of its coins (a kill that pays nothing never reaches the count)
                var paid = ApplyFalloff(player, credit, falloff);
                var earned = zoneValue * paid;
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

        // ---- Prestige Coin falloff (2026-11-01) -----------------------------------------------------------------------------

        /// <summary>Least time between two "past the pace" messages to one character, in seconds, so a player who hovers around
        /// the pace is not told on every pull.</summary>
        private const double FalloffNoticeQuietSeconds = 600;

        /// <summary>Least time with no cut kill before a player who was told about the falloff is told they pay in full again, in
        /// seconds. The real wait is the longer of this and the falloff window (288 s at the defaults), so a player pulling in
        /// packs above the pace is not told "full again" between every pull.</summary>
        private const double FalloffFullAgainMinSeconds = 60;

        /// <summary>Per online character, never saved: whether their coin kills are being cut, when the last cut was, and
        /// whether they were told.</summary>
        private sealed class FalloffNotice
        {
            public bool Cut;
            public double LastCut;
            public bool Told;
            public double QuietUntil;
        }

        /// <summary>Keyed weakly on the Player object, so a logged-out character's entry goes with it.</summary>
        private static readonly ConditionalWeakTable<Player, FalloffNotice> falloffNotices = new();

        /// <summary>
        /// This kill through the falloff, on the player's own thread. Reads and saves how far ahead of the pace they are
        /// (PropertyFloat.PrestigeCoinPaceDebt / PrestigeCoinPaceDebtTime, in seconds - saved, so relogging never empties it,
        /// and time offline drains it; see TierRewardScale.FalloffOnDebt). Tells them when their kills start paying less and
        /// when they pay in full again. Returns the credit to pay coins on (= <paramref name="credit"/> while under the pace).
        /// </summary>
        private static double ApplyFalloff(Player player, double credit, TierRewardScale.FalloffDials dials)
        {
            // No falloff where this kill happened: full pay, and the saved count is left exactly as it was. Emptying it here
            // would let one kill in a no-falloff zone hand back the whole reserve.
            if (!(dials.KillsPerHour > 0))
                return credit;

            var now = Time.GetUnixTime();
            var debt = player.GetProperty(PropertyFloat.PrestigeCoinPaceDebt) ?? 0;
            var last = player.GetProperty(PropertyFloat.PrestigeCoinPaceDebtTime);
            var elapsed = last.HasValue ? now - last.Value : 0.0;

            var (paid, newDebt, reduced) = TierRewardScale.FalloffOnDebt(debt, elapsed, credit, dials);

            if (newDebt > 0)
            {
                player.SetProperty(PropertyFloat.PrestigeCoinPaceDebt, newDebt);
                player.SetProperty(PropertyFloat.PrestigeCoinPaceDebtTime, now);
            }
            else
            {
                player.RemoveProperty(PropertyFloat.PrestigeCoinPaceDebt);
                player.RemoveProperty(PropertyFloat.PrestigeCoinPaceDebtTime);
            }

            var notice = falloffNotices.GetValue(player, _ => new FalloffNotice());
            var pace = dials.KillsPerHour.ToString("0.#", CultureInfo.InvariantCulture);

            if (reduced)
            {
                notice.Cut = true;
                notice.LastCut = now;

                // checked on every cut kill, so a message held back by the quiet time still goes out if the cut outlasts it
                if (!notice.Told && now >= notice.QuietUntil)
                {
                    notice.Told = true;
                    notice.QuietUntil = now + FalloffNoticeQuietSeconds;
                    var pct = (dials.Mult * 100).ToString("0.#", CultureInfo.InvariantCulture);
                    player.SendMessage($"You are past {pace} kills an hour: kills past that pace pay {pct} pct of their Prestige Coins until you slow back down.", ChatMessageType.Broadcast);
                }
            }
            else if (notice.Cut && now - notice.LastCut >= Math.Max(FalloffFullAgainMinSeconds, dials.WindowSeconds))
            {
                notice.Cut = false;
                if (notice.Told)
                {
                    notice.Told = false;
                    player.SendMessage($"Your kill pace is back under {pace} an hour: Prestige Coins from your kills pay in full again.", ChatMessageType.Broadcast);
                }
            }

            return paid;
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

            // once any credit is handed off, a failure must not let the stock path credit the same kill again
            var scheduled = false;
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
                    scheduled = true;
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

                // failed before handing anything off: let the stock kill-task code credit the kill instead
                if (!scheduled)
                    return false;
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
