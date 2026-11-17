using System;
using System.Globalization;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /tierdip (owner 2026-10-24): any player's view of the tier dip where they stand - their own tier, the tier of the
    /// zone they are in, and the share of a kill's credit they keep here. See TierRewardScale. Since 2026-11-01 it also shows
    /// the Prestige Coin falloff in force here, and since 2026-11-17 where the player is in their coin hour.
    /// </summary>
    public static class TierDipCommands
    {
        /// <summary>/tierdip: the player's own tier, the zone's tier, the credit a kill here gives them, and the Prestige Coin
        /// falloff in force here.</summary>
        [CommandHandler("tierdip", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows your own tier, the tier of the zone you stand in, how much credit a kill here gives you, and your Prestige Coin falloff.",
            "/tierdip")]
        public static void HandleTierDip(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null)
                return;

            ZoneControlManager.EnsureLoaded();

            void Msg(string line) => CommandHandlerHelper.WriteOutputInfo(session, line);

            var playerTier = TierHitGate.PlayerTier(player);
            var ownTier = TierRewardScale.OwnTier(player);

            Msg($"Your tier: {TierText(playerTier)} (the highest tier whose monsters you can hit).");
            if (ownTier != playerTier)
                Msg($"Your own tier for rewards: {TierText(ownTier)} (the highest tier at or below yours that has a zone).");

            var here = ZoneControlManager.GetEffectiveVariation(player);
            var inZone = here >= TierRewardScale.MinTier && here <= ZoneRare.MaxTier && ZoneControlManager.ResolveZoneDefaultForPlayer(player) != null;
            if (!inZone)
            {
                Msg("You are not in a T11+ zone: kills here give their normal rewards.");
                return;
            }

            Msg($"This zone: T{here}.");

            if (!ServerConfig.zc_tier_dip_enabled.Value)
                Msg("The tier dip is off: every kill gives full credit.");
            else
                ShowTierDip(ownTier, here, Msg);

            ShowFalloff(player, Msg);
        }

        /// <summary>The share of a kill's credit kept here, and which rewards the dip reaches.</summary>
        private static void ShowTierDip(int ownTier, int here, Action<string> Msg)
        {
            var credit = TierRewardScale.Credit(ownTier, here);
            var pct = (credit * 100).ToString("0.#", CultureInfo.InvariantCulture);

            if (ownTier < TierRewardScale.MinTier || here >= ownTier)
                Msg($"Kills here give you full credit ({pct} pct).");
            else
            {
                var below = ownTier - here;
                Msg($"Kills here give you {pct} pct credit: {below} tier{(below == 1 ? "" : "s")} below your own tier.");
            }

            var step = (ServerConfig.zc_tier_dip_step.Value * 100).ToString("0.#", CultureInfo.InvariantCulture);
            var floor = (ServerConfig.zc_tier_dip_floor.Value * 100).ToString("0.#", CultureInfo.InvariantCulture);
            // only the rewards the dip actually reaches right now
            var rewards = new System.Collections.Generic.List<string>();
            if (ServerConfig.zc_pcoin_kill_base.Value > 0)
                rewards.Add("Prestige Coins");
            if (ServerConfig.zc_kt_decimal_credit_enabled.Value)
                rewards.Add("kill task credit");
            rewards.Add("bounty credit");   // always: BountyManager follows zc_tier_dip_enabled alone
            if (ServerConfig.zc_tier_dip_xp_enabled.Value)
                rewards.Add("XP and luminance");

            if (rewards.Count == 0)
                Msg("No kill reward is reduced by the tier dip right now.");
            else
            {
                var list = rewards.Count == 1 ? rewards[0] : string.Join(", ", rewards.GetRange(0, rewards.Count - 1)) + " and " + rewards[^1];
                Msg($"Each tier below your own costs {step} pct of a kill's {list} (never below {floor} pct).");
            }
        }

        /// <summary>
        /// The Prestige Coin falloff where the player stands (2026-11-01; bands per coin hour 2026-11-17): the schedule in force
        /// here, with this zone's limits, and where they are in their coin hour. Read only - nothing is written.
        /// </summary>
        private static void ShowFalloff(Player player, Action<string> Msg)
        {
            if (!(ServerConfig.zc_pcoin_kill_base.Value > 0))
                return;   // no coins per kill at all

            var dials = TierRewardScale.FalloffFor(ZoneControlManager.ResolveZoneDefaultForPlayer(player));
            if (!dials.On)
            {
                Msg("Prestige Coins from kills here have no hourly falloff.");
                return;
            }

            Msg($"Prestige Coins from kills here, per coin hour: {TierRewardScale.DescribeFalloff(dials)}.");

            var now = ACE.Common.Time.GetUnixTime();
            var (count, start, fresh) = TierRewardScale.FalloffHour(
                player.GetProperty(PropertyFloat.PrestigeCoinHourKills), player.GetProperty(PropertyFloat.PrestigeCoinHourStart), now);

            if (fresh)
            {
                Msg("Your next coin kill starts a new coin hour (60 minutes).");
                return;
            }

            var minutes = (int)Math.Max(1, Math.Ceiling((start + TierRewardScale.CoinHourSeconds - now) / 60.0));
            var rate = TierRewardScale.FalloffBandAt(count, dials).Rate;
            Msg($"You have made {TierRewardScale.Kills(count * dials.Scale)} kills this coin hour, which ends in {minutes} minute{(minutes == 1 ? "" : "s")}: your next kill pays {TierRewardScale.Pct(rate)} pct of its coins.");
        }

        /// <summary>"T15", or "below T11" for a tier under the endgame.</summary>
        private static string TierText(int tier) => tier >= TierRewardScale.MinTier ? "T" + tier : "below T11";
    }
}
