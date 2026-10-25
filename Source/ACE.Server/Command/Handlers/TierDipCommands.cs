using System.Globalization;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /tierdip (owner 2026-10-24): any player's view of the tier dip where they stand - their own tier, the tier of the
    /// zone they are in, and the share of a kill's credit they keep here. See TierRewardScale.
    /// </summary>
    public static class TierDipCommands
    {
        [CommandHandler("tierdip", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Shows your own tier, the tier of the zone you stand in, and how much credit a kill here gives you.",
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
            {
                Msg("The tier dip is off: every kill gives full credit.");
                return;
            }

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

        private static string TierText(int tier) => tier >= TierRewardScale.MinTier ? "T" + tier : "below T11";
    }
}
