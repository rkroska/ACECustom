using System;

using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private volatile int ownedGemTier = -1;

        /// <summary>The highest Tou Tou Endgame Portal Gem tier in this player's pack (main pack and side packs) as of their
        /// last heartbeat: 0 = none, -1 = not looked yet (just logged in). Read by TierHitGate.RewardTier from any thread.</summary>
        public int OwnedGemTier => ownedGemTier;

        /// <summary>Looks through the pack for tier gems. Player's own thread only (Heartbeat). Nothing to do for a player who
        /// cannot hit T11 - their reward tier is 0 either way.</summary>
        private void RefreshOwnedGemTier()
        {
            try
            {
                if (!InventoryLoaded)
                    return;

                if (TierHitGate.PlayerTier(this) < TierHitGate.MinGatedVariation)
                {
                    ownedGemTier = 0;
                    return;
                }

                var best = 0;
                foreach (var item in Inventory.Values)
                {
                    best = Math.Max(best, TierHitGate.GemTier(item.WeenieClassId));
                    if (item is Container pack)
                        foreach (var inner in pack.Inventory.Values)
                            best = Math.Max(best, TierHitGate.GemTier(inner.WeenieClassId));
                }
                ownedGemTier = best;
            }
            catch (Exception ex)
            {
                log.Error($"[TierHitGate] {Name} (0x{Guid}): gem tier refresh failed", ex);
            }
        }
    }
}
