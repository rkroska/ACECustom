using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum.Properties;
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
        /// If a pack item fails to leave the pack part way through, everything already taken goes back to the bank
        /// (as banked currency, not as the original items) and false is returned, so the gate is never stamped on a
        /// partial payment.
        /// </summary>
        public bool TryPayEnlightenmentGateFee(EnlightenmentGate.Fee fee, int gateLevel, out List<string> shortfall)
        {
            lock (balanceLock)
            {
                shortfall = GetEnlightenmentGateShortfall(fee);
                if (shortfall.Count > 0)
                    return false;

                using var ledgerScope = PyrealLedger.Begin(EconomyLedger.SrcEnlightenGate, gateLevel.ToString(), "Enlightenment gate " + gateLevel, this);

                var pyrealsTaken = SpendPyrealsPackFirst(fee.Pyreals);
                var enlightenedCoinsTaken = pyrealsTaken == fee.Pyreals ? SpendCoinsPackFirst(ENLIGHTENED_COIN_WCID, PropertyInt64.BankedEnlightenedCoins, fee.EnlightenedCoins) : 0;
                var prestigeCoinsTaken = enlightenedCoinsTaken == fee.EnlightenedCoins ? SpendCoinsPackFirst(PRESTIGE_COIN_WCID, PropertyInt64.BankedPrestigeCoins, fee.PrestigeCoins) : 0;

                if (pyrealsTaken == fee.Pyreals && enlightenedCoinsTaken == fee.EnlightenedCoins && prestigeCoinsTaken == fee.PrestigeCoins)
                    return true;

                // refund what was taken; the pack items are gone, so it is returned to the bank
                log.Error($"[ENL_GATE] {Name}: gate {gateLevel} fee failed part way ({pyrealsTaken:N0}/{fee.Pyreals:N0} pyreals, {enlightenedCoinsTaken:N0}/{fee.EnlightenedCoins:N0} EC, {prestigeCoinsTaken:N0}/{fee.PrestigeCoins:N0} PC); refunding to the bank.");
                BankedPyreals = (BankedPyreals ?? 0) + pyrealsTaken;
                BankedEnlightenedCoins = (BankedEnlightenedCoins ?? 0) + enlightenedCoinsTaken;
                BankedPrestigeCoins = (BankedPrestigeCoins ?? 0) + prestigeCoinsTaken;
                UpdateCoinValue();

                shortfall.Add("a successful payment (an item could not be taken; anything taken was returned to your bank)");
                return false;
            }
        }

        /// <summary>Takes up to <paramref name="count"/> of an item from the pack and returns how many actually left it.</summary>
        private int ConsumePackItems(uint wcid, int count)
        {
            var before = GetNumInventoryItemsOfWCID(wcid);
            TryConsumeFromInventoryWithNetworking(wcid, count);
            return before - GetNumInventoryItemsOfWCID(wcid);
        }

        /// <summary>Spends coins from the pack, then the bank. Returns how many were taken; less than asked means a pack item failed.</summary>
        private long SpendCoinsPackFirst(uint wcid, PropertyInt64 bankProperty, long amount)
        {
            if (amount <= 0)
                return 0;

            var fromPack = (int)Math.Min(amount, GetNumInventoryItemsOfWCID(wcid));
            var taken = fromPack > 0 ? ConsumePackItems(wcid, fromPack) : 0L;
            if (taken < fromPack)
                return taken;

            SetProperty(bankProperty, (GetProperty(bankProperty) ?? 0) - (amount - taken));
            return amount;
        }

        /// <summary>
        /// Spends pyreals that may exceed int: coin stacks, then trade notes largest first, then the bank. When the
        /// bank cannot cover what is left, one more note is broken and the change goes to the bank. The caller has
        /// checked that pack and bank together hold enough. Returns the pyreals taken (net of change); less than
        /// asked means a pack item failed to leave the pack.
        /// </summary>
        private long SpendPyrealsPackFirst(long amount)
        {
            if (amount <= 0)
                return 0;

            long taken = 0;

            var fromCoins = (int)Math.Min(amount, GetNumInventoryItemsOfWCID(coinStackWcid));
            if (fromCoins > 0)
            {
                var got = ConsumePackItems(coinStackWcid, fromCoins);
                taken += got;
                if (got < fromCoins)
                    return taken;
            }

            // largest notes first, never more than is owed; afterwards every note left is worth more than what is still owed
            foreach (var (wcid, value) in PyrealTradeNotes)
            {
                if (amount - taken < value)
                    continue;

                var take = (int)Math.Min(GetNumInventoryItemsOfWCID(wcid), (amount - taken) / value);
                if (take > 0)
                {
                    var got = ConsumePackItems(wcid, take);
                    taken += got * value;
                    if (got < take)
                        return taken;
                }
            }

            var left = amount - taken;
            if (left > 0)
            {
                var banked = BankedPyreals ?? 0;
                if (banked >= left)
                {
                    BankedPyreals = banked - left;
                    taken = amount;
                }
                else
                {
                    // break the smallest note that covers the rest; the change goes to the bank
                    foreach (var (wcid, value) in PyrealTradeNotes.Reverse())
                    {
                        if (value < left || GetNumInventoryItemsOfWCID(wcid) == 0)
                            continue;

                        if (ConsumePackItems(wcid, 1) == 1)
                        {
                            BankedPyreals = banked + (value - left);
                            taken = amount;
                        }
                        break;
                    }
                }
            }

            UpdateCoinValue();
            return taken;
        }
    }
}
