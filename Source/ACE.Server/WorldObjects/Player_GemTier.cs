using System;
using System.Collections.Generic;

using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private volatile int ownedGemTier = -1;
        private bool gemTierErrorLogged;

        /// <summary>The highest Tou Tou Endgame Portal Gem tier in this player's pack (main pack and side packs) as of the last
        /// look (login, then every heartbeat): 0 = none, -1 = not looked yet. Read by TierHitGate.RewardTier from any thread.</summary>
        public int OwnedGemTier => ownedGemTier;

        /// <summary>Looks through the pack for tier gems. Player's own thread only (login, Heartbeat). Nothing to do for a player
        /// who cannot hit T11 - their reward tier is 0 either way.</summary>
        private void RefreshOwnedGemTier()
        {
            try
            {
                if (!InventoryLoaded)
                    return;

                var hitTier = TierHitGate.PlayerTier(this);
                ownedGemTier = hitTier < TierHitGate.MinGatedVariation ? 0 : TierHitGate.HighestGemTier(PackWcids(), stopAt: hitTier);
            }
            catch (Exception ex)
            {
                // once per session: a fault that repeats would otherwise log every 5 s
                if (!gemTierErrorLogged)
                {
                    gemTierErrorLogged = true;
                    log.Error($"[TierHitGate] {Name} (0x{Guid}): gem tier refresh failed (logged once per session)", ex);
                }
            }
        }

        /// <summary>Every item in the main pack and the side packs, as weenie class ids.</summary>
        private IEnumerable<uint> PackWcids()
        {
            foreach (var item in Inventory.Values)
                yield return item.WeenieClassId;

            foreach (var pack in GetCachedSideContainers())
                foreach (var item in pack.Inventory.Values)
                    yield return item.WeenieClassId;
        }
    }
}
