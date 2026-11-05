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
    /// the Prestige Coin falloff in force here and how far the player is from it.
    /// </summary>
    public static class TierDipCommands
    {
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
        /// The Prestige Coin falloff where the player stands (2026-11-01): the pace and share in force here, and how much room is
        /// left before their kills start paying less. Read only - the saved count is drained for display, never written.
        /// </summary>
        private static void ShowFalloff(Player player, Action<string> Msg)
        {
            if (!(ServerConfig.zc_pcoin_kill_base.Value > 0))
                return;   // no coins per kill at all

            var dials = TierRewardScale.FalloffFor(ZoneControlManager.ResolveZoneDefaultForPlayer(player));
            if (!(dials.KillsPerHour > 0) || dials.Mult >= 1.0)
            {
                Msg("Prestige Coins from kills here have no hourly falloff.");
                return;
            }

            var pace = dials.KillsPerHour.ToString("0.#", CultureInfo.InvariantCulture);
            var pct = (dials.Mult * 100).ToString("0.#", CultureInfo.InvariantCulture);
            Msg($"Prestige Coins: full rate up to {pace} kills an hour here; every kill past that pace pays {pct} pct of its coins.");

            var debt = player.GetProperty(PropertyFloat.PrestigeCoinPaceDebt) ?? 0;
            var last = player.GetProperty(PropertyFloat.PrestigeCoinPaceDebtTime);
            var elapsed = last.HasValue ? ACE.Common.Time.GetUnixTime() - last.Value : 0.0;

            // a kill worth 0 just drains the count to now; room is in kills at this zone's pace
            var (_, debtNow, _) = TierRewardScale.FalloffOnDebt(debt, elapsed, 0.0, dials);
            var room = Math.Max(0.0, (dials.WindowSeconds - debtNow) * dials.KillsPerHour / 3600.0);

            if (room < 1.0)
            {
                // At the edge, not necessarily cut: a kill pays in full if enough time has passed since the last one.
                var every = (3600.0 / dials.KillsPerHour).ToString("0.#", CultureInfo.InvariantCulture);
                Msg($"You are at that pace now: kill faster than one every {every} seconds and the part of each kill past it pays {pct} pct.");
            }
            else
                Msg($"You can get {room.ToString("0.#", CultureInfo.InvariantCulture)} kills ahead of that pace before the falloff starts (the most is {dials.Reserve.ToString("0.#", CultureInfo.InvariantCulture)}).");
        }

        private static string TierText(int tier) => tier >= TierRewardScale.MinTier ? "T" + tier : "below T11";
    }
}
