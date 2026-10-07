using System;

using log4net;

using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Charging a pyreal fee for a service: the bank first, then pyreal coins from the pack, all or nothing.
    /// Shared by anything that takes a flat fee (Dressing Room; the forge keeps its own copy until it moves here).
    /// </summary>
    public static class PyrealFee
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Pyreal coin weenie, for the part of a fee the bank cannot cover.</summary>
        public const uint PyrealWcid = 273;

        /// <summary>
        /// Pyreal coins (wcid 273) in the player's packs, counted off the stacks themselves. Never Player.CoinValue: on this
        /// server that number already INCLUDES the banked pyreals (Player.UpdateCoinValue, for the wealth display) and every
        /// other coin-type item, so adding it to the bank counts the bank twice.
        /// </summary>
        public static long PackPyreals(Player player)
        {
            long total = 0;
            foreach (var stack in player.GetInventoryItemsOfWCID(PyrealWcid))
                total += stack.StackSize ?? 1;
            return total;
        }

        /// <summary>Pyreals the player can pay with: banked pyreals plus pyreal coins carried.</summary>
        public static long Funds(Player player) => (player.BankedPyreals ?? 0) + PackPyreals(player);

        /// <summary>
        /// How a fee is split: the bank first, then coins from the pack. Null when the two together cannot cover it (or
        /// the pack's share is more than one consume call can take).
        /// </summary>
        public static (long FromBank, long FromPack)? Split(long bank, long pack, long fee)
        {
            if (fee <= 0)
                return (0, 0);
            var fromBank = Math.Min(Math.Max(0, bank), fee);
            var fromPack = fee - fromBank;
            if (fromPack > Math.Max(0, pack) || fromPack > int.MaxValue)
                return null;
            return (fromBank, fromPack);
        }

        public static bool CanPay(Player player, long fee)
            => fee <= 0 || Split(player.BankedPyreals ?? 0, PackPyreals(player), fee) != null;

        /// <summary>
        /// Takes <paramref name="fee"/> from the bank first, then from pyreal coins in the pack. All or nothing: false
        /// means nothing was taken. <paramref name="logTag"/> names the caller in the error line.
        /// </summary>
        public static bool Charge(Player player, long fee, string logTag)
        {
            if (fee <= 0)
                return true;
            var bank = player.BankedPyreals ?? 0;
            var pack = PackPyreals(player);
            var split = Split(bank, pack, fee);
            if (split == null)
                return false;
            var (fromBank, fromPack) = split.Value;

            // Coins first: this is the step that can fail, so the bank is only touched once it has succeeded. The consume
            // call reports success even when it ran out of stacks early, so what was really taken is counted, not assumed.
            if (fromPack > 0)
            {
                var consumed = player.TryConsumeFromInventoryWithNetworking(PyrealWcid, (int)fromPack);
                var taken = pack - PackPyreals(player);
                if (!consumed || taken != fromPack)
                {
                    // put back whatever the partial consume removed, so "false" really does mean nothing was taken
                    if (taken > 0)
                    {
                        player.BankedPyreals = bank + taken;
                        player.RefreshCoinValueAfterBankChange();
                    }
                    log.Error($"{logTag} {player.Name}: fee {fee} needed {fromPack} pyreal coins from the pack but {taken} were taken (consume returned {consumed}); {(taken > 0 ? $"{taken} credited to the bank instead, " : "")}the fee was not charged");
                    return false;
                }
            }
            if (fromBank > 0)
            {
                player.BankedPyreals = bank - fromBank;
                player.RefreshCoinValueAfterBankChange();
            }
            return true;
        }
    }
}
