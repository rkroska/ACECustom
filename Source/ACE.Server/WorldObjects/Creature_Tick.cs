using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// Called every ~5 seconds for Creatures
        /// </summary>
        public override void Heartbeat(double currentUnixTime)
        {
            var expireItems = new List<WorldObject>();

            foreach (var wo in EquippedObjects.Values)
            {
                if (!wo.EnchantmentManager.HasEnchantments && !wo.Lifespan.HasValue)
                    continue;

                // FIXME: wo.NextHeartbeatTime is double.MaxValue here
                //if (wo.NextHeartbeatTime <= currentUnixTime)
                    //wo.Heartbeat(currentUnixTime);

                // just go by parent heartbeats, only for enchantments?
                // TODO: handle players dropping / picking up items
                wo.EnchantmentManager.HeartBeat(CachedHeartbeatInterval);

                if (wo.IsLifespanSpent)
                    expireItems.Add(wo);
            }

            VitalHeartBeat();

            EmoteManager.HeartBeat();

            DamageHistory.TryPrune();

            // delete items when RemainingLifespan <= 0
            foreach (var expireItem in expireItems)
            {
                expireItem.DeleteObject(this);

                if (this is Player player)
                {
                    var msg = $"Its lifespan finished, your {expireItem.Name} crumbles to dust.";
                    if (expireItem.IsTestCharm)
                        msg += " We hope you enjoyed this trial! Please share any feedback in the #feedback channel on Discord.";
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
                }
            }

            base.Heartbeat(currentUnixTime);
        }
    }
}
