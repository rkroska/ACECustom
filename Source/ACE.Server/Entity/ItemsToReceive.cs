using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Database;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Helper class to verify player has enough free inventory slots / container slots / burden to receive some items
    /// </summary>
    public class ItemsToReceive(Player player)
    {
        public int RequiredInventorySlots { get; set; }
        public int RequiredContainerSlots { get; set; }
        public int RequiredBurden { get; set; }

        /// <summary>
        /// The total amount of items needing to be created.
        /// </summary>
        public int RequiredSlots => RequiredContainerSlots + RequiredInventorySlots;

        private int playerFreeInventorySlots { get; set; } = player.GetFreeInventorySlots();
        private int playerFreeContainerSlots { get; set; } = player.GetFreeContainerSlots();
        private int playerAvailableBurden { get; set; } = player.GetAvailableBurden();

        public bool PlayerOutOfInventorySlots => RequiredInventorySlots > playerFreeInventorySlots;
        public bool PlayerOutOfContainerSlots => RequiredContainerSlots > playerFreeContainerSlots;
        public bool PlayerExceedsAvailableBurden => RequiredBurden > playerAvailableBurden;

        public bool PlayerExceedsLimits => PlayerOutOfInventorySlots || PlayerOutOfContainerSlots || PlayerExceedsAvailableBurden;

        /// <summary>
        /// How many MORE slots the player must free to receive the batch (the deficit), accounting for
        /// the slots they already have free (including side packs). This is what to show the player —
        /// not <see cref="RequiredSlots"/>, which is the gross size of the reward and over-states what
        /// they actually need to clear.
        /// </summary>
        public int RequiredAdditionalSlots =>
            (RequiredInventorySlots > playerFreeInventorySlots ? RequiredInventorySlots - playerFreeInventorySlots : 0)
            + (RequiredContainerSlots > playerFreeContainerSlots ? RequiredContainerSlots - playerFreeContainerSlots : 0);

        /// <summary>
        /// Slots this batch does not need because the items top up stacks the player already carries
        /// (see <see cref="Player.GetCarriedStacksWithRoom"/>). <see cref="RequiredSlots"/> excludes them.
        /// </summary>
        public int SlotsSavedByCarriedStacks { get; private set; }

        // room left in the player's carried stacks, per wcid, after earlier Adds in this batch claimed some
        private readonly Dictionary<uint, int> carriedStackRoom = new();

        /// <param name="intoCarriedStacks">true when the items will be handed out by <see cref="Player.GiveFromEmote"/>,
        /// which tops up matching stacks the player carries before making new ones</param>
        public bool Add(uint weenieClassId, int amount, bool intoCarriedStacks = false)
        {
            if (intoCarriedStacks && amount > 0 && ServerConfig.emote_give_merge_stacks.Value)
            {
                if (!carriedStackRoom.TryGetValue(weenieClassId, out var room))
                    room = player.GetCarriedStacksWithRoom(weenieClassId).Sum(s => (s.MaxStackSize ?? 1) - (s.StackSize ?? 1));

                var merged = Math.Min(amount, room);
                carriedStackRoom[weenieClassId] = room - merged;

                if (merged > 0)
                {
                    // burden is charged for the full amount; only the slots for the merged part are saved
                    var slotsWithout = GetItemSlotAndBurdenRequirements(weenieClassId, amount, out _, out _);
                    var slotsWith = GetItemSlotAndBurdenRequirements(weenieClassId, amount - merged, out _, out _);
                    SlotsSavedByCarriedStacks += slotsWithout - slotsWith;

                    RequiredBurden += GetBurden(weenieClassId, merged);
                    if (amount - merged == 0)
                        return !PlayerExceedsLimits;

                    return Process(weenieClassId, amount - merged);
                }
            }

            return Process(weenieClassId, amount);
        }

        public bool Remove(uint weenieClassId, int amount)
        {
            // stacks counted as room may be the ones being taken: assume no room is left in them
            carriedStackRoom[weenieClassId] = 0;

            return Process(weenieClassId, amount, true);
        }

        private static int GetBurden(uint weenieClassId, int amount)
        {
            GetItemSlotAndBurdenRequirements(weenieClassId, amount, out var burden, out _);
            return burden;
        }

        private bool Process(uint weenieClassId, int amount, bool negate = false)
        {
            var requiredSlots = GetItemSlotAndBurdenRequirements(weenieClassId, amount, out var requiredEncumbrance, out var itemRequiresBackpackSlot);

            if (negate)
            {
                requiredSlots *= -1;
                requiredEncumbrance *= -1;
            }

            RequiredBurden += requiredEncumbrance;

            if (itemRequiresBackpackSlot)
                RequiredContainerSlots += requiredSlots;
            else
                RequiredInventorySlots += requiredSlots;

            return !PlayerExceedsLimits;
        }

        /// <summary>
        /// Returns the number of slots required for the items
        /// </summary>
        private static int GetItemSlotAndBurdenRequirements(uint weenieClassId, int amount, out int requiredEncumbrance, out bool requiresBackpackSlot)
        {
            requiredEncumbrance = 0;
            requiresBackpackSlot = false;

            var item = DatabaseManager.World.GetCachedWeenie(weenieClassId);

            if (item == null || item.IsVendorService())
                return 0;

            requiresBackpackSlot = item.RequiresBackpackSlotOrIsContainer();

            if (!item.IsStackable())
            {
                requiredEncumbrance = amount * item.GetProperty(PropertyInt.EncumbranceVal) ?? 0;
                return amount;
            }

            var itemStackUnitEncumbrance = item.GetStackUnitEncumbrance();

            var itemMaxStackSize = item.GetMaxStackSize();

            requiredEncumbrance = amount * itemStackUnitEncumbrance;

            var itemStacks = amount / itemMaxStackSize;

            if (amount % itemMaxStackSize > 0)
                itemStacks++;

            return itemStacks;
        }
    }
}
