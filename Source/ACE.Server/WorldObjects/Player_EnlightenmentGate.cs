using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Paying the enlightenment gate fee (see <see cref="EnlightenmentGate"/>). Every currency is taken from the pack
    /// first and then the bank, like vendors and enlightenment tokens.
    /// </summary>
    partial class Player
    {
        private const uint ENLIGHTENED_COIN_WCID = 300004;

        /// <summary>The pyreal trade notes, largest first (the denominations /bank withdraws).</summary>
        private static readonly (uint Wcid, long Value)[] PyrealTradeNotes =
        {
            (20630, 250_000), (20629, 200_000), (20628, 150_000), (2627, 100_000), (2626, 50_000),
            (2625, 10_000), (2624, 5_000), (2623, 1_000), (2622, 500), (2621, 100),
        };

        /// <summary>Pyreals carried in the pack: coin stacks plus trade notes at face value. Can exceed int.</summary>
        private long GetPackPyrealValue()
        {
            long total = GetNumInventoryItemsOfWCID(coinStackWcid);
            foreach (var (wcid, value) in PyrealTradeNotes)
                total += GetNumInventoryItemsOfWCID(wcid) * value;
            return total;
        }

        /// <summary>What the player is short of the fee, pack and bank together, e.g. "1,500 Enlightened Coins". Empty when they can pay.</summary>
        public List<string> GetEnlightenmentGateShortfall(EnlightenmentGate.Fee fee)
        {
            var shortfall = new List<string>();

            var pyreals = GetPackPyrealValue() + (BankedPyreals ?? 0);
            if (pyreals < fee.Pyreals)
                shortfall.Add($"{fee.Pyreals - pyreals:N0} pyreals");

            var enlightenedCoins = GetNumInventoryItemsOfWCID(ENLIGHTENED_COIN_WCID) + (BankedEnlightenedCoins ?? 0);
            if (enlightenedCoins < fee.EnlightenedCoins)
                shortfall.Add($"{fee.EnlightenedCoins - enlightenedCoins:N0} Enlightened Coins");

            var prestigeCoins = GetNumInventoryItemsOfWCID(PRESTIGE_COIN_WCID) + (BankedPrestigeCoins ?? 0);
            if (prestigeCoins < fee.PrestigeCoins)
                shortfall.Add($"{fee.PrestigeCoins - prestigeCoins:N0} Prestige Coins");

            return shortfall;
        }

        /// <summary>
        /// Takes the whole fee, or nothing when the player is short of any part of it (<paramref name="shortfall"/> says what).
        /// </summary>
        public bool TryPayEnlightenmentGateFee(EnlightenmentGate.Fee fee, int gateLevel, out List<string> shortfall)
        {
            lock (balanceLock)
            {
                shortfall = GetEnlightenmentGateShortfall(fee);
                if (shortfall.Count > 0)
                    return false;

                // ConsumeCurrency takes a uint; a coin fee that large is a config mistake, not a price
                if (fee.EnlightenedCoins > uint.MaxValue || fee.PrestigeCoins > uint.MaxValue)
                {
                    shortfall.Add("a valid fee (ask an administrator)");
                    log.Error($"[ENL_GATE] {Name}: gate {gateLevel} coin fee out of range ({fee.EnlightenedCoins} EC, {fee.PrestigeCoins} PC).");
                    return false;
                }

                using var ledgerScope = PyrealLedger.Begin(EconomyLedger.SrcEnlightenGate, gateLevel.ToString(), "Enlightenment gate " + gateLevel, this);

                SpendPyrealsPackFirst(fee.Pyreals);

                if (fee.EnlightenedCoins > 0)
                    ConsumeCurrency(ENLIGHTENED_COIN_WCID, (uint)fee.EnlightenedCoins);

                if (fee.PrestigeCoins > 0)
                    ConsumeCurrency(PRESTIGE_COIN_WCID, (uint)fee.PrestigeCoins);

                return true;
            }
        }

        /// <summary>
        /// Spends pyreals that may exceed int: coin stacks, then trade notes largest first, then the bank. When the
        /// bank cannot cover what is left, one more note is broken and the change goes to the bank. The caller has
        /// checked that pack and bank together hold enough.
        /// </summary>
        private void SpendPyrealsPackFirst(long amount)
        {
            if (amount <= 0)
                return;

            var left = amount;

            var fromCoins = (int)Math.Min(left, GetNumInventoryItemsOfWCID(coinStackWcid));
            if (fromCoins > 0)
            {
                TryConsumeFromInventoryWithNetworking(coinStackWcid, fromCoins);
                left -= fromCoins;
            }

            // largest notes first, never more than is owed; afterwards every note left is worth more than what is still owed
            foreach (var (wcid, value) in PyrealTradeNotes)
            {
                if (left < value)
                    continue;

                var take = (int)Math.Min(GetNumInventoryItemsOfWCID(wcid), left / value);
                if (take > 0)
                {
                    TryConsumeFromInventoryWithNetworking(wcid, take);
                    left -= take * value;
                }
            }

            if (left > 0)
            {
                var banked = BankedPyreals ?? 0;
                if (banked >= left)
                {
                    BankedPyreals = banked - left;
                }
                else
                {
                    // break the smallest note that covers the rest; the change goes to the bank
                    foreach (var (wcid, value) in PyrealTradeNotes.Reverse())
                    {
                        if (value < left || GetNumInventoryItemsOfWCID(wcid) == 0)
                            continue;

                        TryConsumeFromInventoryWithNetworking(wcid, 1);
                        BankedPyreals = banked + (value - left);
                        left = 0;
                        break;
                    }

                    if (left > 0)
                        log.Error($"[ENL_GATE] {Name}: {left:N0} pyreals of the gate fee could not be taken after the funds check passed.");
                }
            }

            UpdateCoinValue();
        }
    }
}
