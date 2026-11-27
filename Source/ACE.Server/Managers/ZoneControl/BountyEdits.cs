using System;
using System.Collections.Generic;
using System.Globalization;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Bounty edits shared by /zonecontrol and the portal's Bounties page, so both apply the same rules. The luminance-style
    /// bounties (a zone's, the all-zones one, a region's) go through <see cref="BountyManager.Edit"/>; this adds the reward
    /// table rows and the portal's typed form of a bounty edit.
    /// </summary>
    public static class BountyEdits
    {
        /// <summary>Why a reward table row cannot be saved (null = fine): a carryable item, 1..MaxAmount, 1..MaxWeight.</summary>
        public static string CheckRewardRow(uint wcid, int amount, int weight)
        {
            if (wcid == 0) return "give a WCID.";
            if (amount < 1 || amount > BountyManager.MaxAmount) return $"the amount must be 1-{BountyManager.MaxAmount:N0}.";
            if (weight < 1 || weight > BountyRewardTable.MaxWeight) return $"the weight must be 1-{BountyRewardTable.MaxWeight:N0}.";
            if (!BountyManager.IsGivableItem(wcid)) return $"{wcid} isn't an item a player can carry.";
            return null;
        }

        /// <summary>One reward table row, for audit lines: "5 x Bag of Rebirth (78780315), weight 10", or "(none)".</summary>
        public static string RewardRowText(BountyRewardEntry e)
            => e == null ? "(none)" : $"{e.Amount:N0} x {RoomAssignManager.ItemName(e.Wcid)} ({e.Wcid}), weight {e.Weight:N0}";

        /// <summary>
        /// A typed bounty edit as the argument list <see cref="BountyManager.Edit"/> reads, so the portal is checked exactly as the
        /// command is: "add" and "set" take wcid, amount, kills, minutes and an optional qb=N; "set" and "remove" lead with the id.
        /// </summary>
        public static List<string> EditArgs(string op, int? id, uint? wcid, int? amount, int? kills, double? minutes, int? minQb)
        {
            var inv = CultureInfo.InvariantCulture;
            var args = new List<string>();
            if (op == "set" || op == "remove")
                args.Add(id?.ToString(inv) ?? "");
            if (op == "add" || op == "set")
            {
                args.Add(wcid?.ToString(inv) ?? "");
                args.Add(amount?.ToString(inv) ?? "");
                args.Add(kills?.ToString(inv) ?? "");
                args.Add(minutes?.ToString("R", inv) ?? "");
                if (minQb.HasValue)
                    args.Add("qb=" + minQb.Value.ToString(inv));
            }
            return args;
        }
    }
}
