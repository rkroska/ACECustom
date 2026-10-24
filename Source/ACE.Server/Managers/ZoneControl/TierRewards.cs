using System;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
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
    }
}
