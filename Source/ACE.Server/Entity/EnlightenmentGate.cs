using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Enlightenment gate fee (owner 2026-10-24): a very expensive way past the Paragon requirement at the 150 and 300
    /// gates. A character refused at a gate for lacking the Paragon is offered the fee in a yes/no dialog of its own.
    /// Paying stamps the gate's flag, which the tier row accepts in place of the Paragon (see
    /// <see cref="EnlightenmentTierSnapshot.QuestStamps"/>). Paying does not enlighten: the player enlightens again.
    ///
    /// The real Paragon flags are never stamped, because world content (the Paragon temple and others) reads them.
    /// Characters who finished the Paragon never see the offer: their flag already passes the gate.
    /// </summary>
    public static class EnlightenmentGate
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const string Gate150Flag = "EnlGate150Bypass";
        public const string Gate300Flag = "EnlGate300Bypass";

        public sealed class Fee
        {
            public long Pyreals { get; init; }
            public long EnlightenedCoins { get; init; }
            public long PrestigeCoins { get; init; }

            /// <summary>"20,000,000,000 pyreals, 50,000 Enlightened Coins and 200 Prestige Coins"; parts that cost nothing are left out.</summary>
            public override string ToString()
            {
                var parts = new List<string>();
                if (Pyreals > 0) parts.Add($"{Pyreals:N0} pyreals");
                if (EnlightenedCoins > 0) parts.Add($"{EnlightenedCoins:N0} Enlightened Coins");
                if (PrestigeCoins > 0) parts.Add($"{PrestigeCoins:N0} Prestige Coins");
                return JoinList(parts);
            }
        }

        /// <summary>The fee for a gate flag, read live from server config. Null for a flag that is not a gate.</summary>
        public static Fee GetFee(string flag)
        {
            if (string.Equals(flag, Gate150Flag, StringComparison.OrdinalIgnoreCase))
                return new Fee
                {
                    Pyreals = Math.Max(0, ServerConfig.enl_gate_150_pyreals.Value),
                    EnlightenedCoins = Math.Max(0, ServerConfig.enl_gate_150_enlightened_coins.Value),
                    PrestigeCoins = Math.Max(0, ServerConfig.enl_gate_150_prestige_coins.Value),
                };

            if (string.Equals(flag, Gate300Flag, StringComparison.OrdinalIgnoreCase))
                return new Fee
                {
                    Pyreals = Math.Max(0, ServerConfig.enl_gate_300_pyreals.Value),
                    EnlightenedCoins = Math.Max(0, ServerConfig.enl_gate_300_enlightened_coins.Value),
                    PrestigeCoins = Math.Max(0, ServerConfig.enl_gate_300_prestige_coins.Value),
                };

            return null;
        }

        /// <summary>The gate flag a tier accepts, or null when the tier has no gate fee.</summary>
        private static string GetGateFlag(EnlightenmentTierSnapshot tier)
        {
            return tier.QuestStamps.FirstOrDefault(stamp => GetFee(stamp) != null);
        }

        /// <summary>
        /// Called after the tier's Paragon refusal (first validation pass only). Offers the fee when the player can
        /// pay it, otherwise says what it costs and what they are short.
        /// </summary>
        public static void OfferFee(Player player, EnlightenmentTierSnapshot tier)
        {
            if (!ServerConfig.enl_gate_fee_enabled.Value)
                return;

            var flag = GetGateFlag(tier);
            if (flag == null)
                return;

            var gateLevel = EnlightenmentTierManager.GetGateLevel(flag) ?? tier.MinTargetEnl - 1;
            var fee = GetFee(flag);

            var shortfall = player.GetEnlightenmentGateShortfall(fee);
            if (shortfall.Count > 0)
            {
                player.SendMessage($"You may instead pay a gate fee of {fee} to enlighten past level {gateLevel}. You are short {JoinList(shortfall)}.", ChatMessageType.Broadcast);
                return;
            }

            var text = $"Pay the enlightenment gate fee to enlighten past level {gateLevel} without the Paragon?\n\n" +
                $"{fee}\n\n" +
                "Taken from your pack first, then your bank. This does not enlighten you: enlighten again after paying.";

            void onResponse(bool response, bool timeout)
            {
                if (!response)
                {
                    if (!timeout)
                        player.SendMessage("You did not pay the enlightenment gate fee.", ChatMessageType.Broadcast);
                    return;
                }

                Pay(player, flag);
            }

            if (!player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, onResponse), text))
                player.SendMessage($"You may instead pay a gate fee of {fee} to enlighten past level {gateLevel}. Close your open confirmation and enlighten again to be offered it.", ChatMessageType.Broadcast);
        }

        private static void Pay(Player player, string flag)
        {
            // Everything is checked again: up to 30 seconds have passed since the offer.
            if (!ServerConfig.enl_gate_fee_enabled.Value)
            {
                player.SendMessage("The enlightenment gate fee is not available right now. Nothing was taken.", ChatMessageType.Broadcast);
                return;
            }

            if (!EnlightenmentTierManager.TryGetTier(player.Enlightenment + 1, out var tier) || !string.Equals(GetGateFlag(tier), flag, StringComparison.OrdinalIgnoreCase))
            {
                player.SendMessage("Your next enlightenment is no longer behind that gate. Nothing was taken.", ChatMessageType.Broadcast);
                return;
            }

            if (tier.QuestStamps.Any(stamp => player.QuestManager.GetCurrentSolves(stamp) >= 1))
            {
                player.SendMessage("You may already enlighten past this gate. Nothing was taken.", ChatMessageType.Broadcast);
                return;
            }

            var gateLevel = EnlightenmentTierManager.GetGateLevel(flag) ?? tier.MinTargetEnl - 1;
            var fee = GetFee(flag);

            if (!player.TryPayEnlightenmentGateFee(fee, gateLevel, out var shortfall))
            {
                player.SendMessage($"The gate fee of {fee} was not paid: you are short {JoinList(shortfall)}. You were not charged.", ChatMessageType.Broadcast);
                return;
            }

            player.QuestManager.Stamp(flag);
            player.SaveBiotaToDatabase();

            log.Info($"[ENL_GATE] {player.Name} (0x{player.Guid.Full:X8}) paid the enlightenment {gateLevel} gate fee: {fee}. Enlightenment {player.Enlightenment}.");
            player.SendMessage($"You paid the enlightenment gate fee of {fee}. You may now enlighten past level {gateLevel}.", ChatMessageType.Broadcast);
        }

        private static string JoinList(List<string> parts)
        {
            if (parts.Count == 0) return "nothing";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
        }
    }
}
