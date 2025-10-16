using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Container
    {
        public override void Heartbeat(double currentUnixTime)
        {
            // TODO: fix bug for landblock containers w/ no heartbeat
            Inventory_Tick(this);

            foreach (var item in Inventory.Values)
            {
                if (item is Container subcontainer)
                    subcontainer.Inventory_Tick(this);
            }

            // for landblock containers
            if (IsOpen && CurrentLandblock != null)
            {
                var viewer = CurrentLandblock.GetObject(Viewer) as Player;
                if (viewer == null)
                {
                    Close(null);
                    return;
                }
                var withinUseRadius = CurrentLandblock.WithinUseRadius(viewer, Guid, out var targetValid);
                if (!withinUseRadius)
                {
                    Close(viewer);
                    return;
                }
            }
            base.Heartbeat(currentUnixTime);
        }

        public void Inventory_Tick(Container rootOwner)
        {
            var expireItems = new List<WorldObject>();

            foreach (var wo in Inventory.Values)
            {
                if (!wo.EnchantmentManager.HasEnchantments && !wo.Lifespan.HasValue)
                    continue;

                // FIXME: wo.NextHeartbeatTime is double.MaxValue here
                //if (wo.NextHeartbeatTime <= currentUnixTime)
                    //wo.Heartbeat(currentUnixTime);

                // just go by parent heartbeats, only for enchantments?
                if (wo.EnchantmentManager.HasEnchantments)
                    wo.EnchantmentManager.HeartBeat(CachedHeartbeatInterval);

                if (wo.IsLifespanSpent)
                    expireItems.Add(wo);
            }

            // delete items when RemainingLifespan <= 0
            foreach (var expireItem in expireItems)
            {
                expireItem.DeleteObject(rootOwner);

                if (rootOwner is Player player)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Its lifespan finished, your {expireItem.Name} crumbles to dust.", ChatMessageType.Broadcast));
            }
        }
    }
}
