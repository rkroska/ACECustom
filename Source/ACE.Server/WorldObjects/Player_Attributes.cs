using ACE.Common;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public bool HandleActionRaiseAttribute(PropertyAttribute attribute, uint amount)
        {
            if (!Attributes.TryGetValue(attribute, out var creatureAttribute))
            {
                log.Error($"{Name}.HandleActionRaiseAttribute({attribute}, {amount}) - invalid attribute");
                return false;
            }

            if (amount > AvailableExperience)
            {
                log.Error($"{Name}.HandleActionRaiseAttribute({attribute}, {amount}) - amount > AvaiableExperience ({AvailableExperience})");
                return false;
            }

            var prevRank = creatureAttribute.Ranks;

            if (!SpendAttributeXp(creatureAttribute, amount))
            {
                ChatPacket.SendServerMessage(Session, $"Your attempt to raise {attribute} has failed.", ChatMessageType.Broadcast);
                return false;
            }

            Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(this, creatureAttribute));

            if (prevRank != creatureAttribute.Ranks)
            {
                // checks if max rank is achieved and plays fireworks w/ special text
                var suffix = "";
                if (creatureAttribute.IsMaxRank)
                {
                    // fireworks
                    PlayParticleEffect(PlayScript.WeddingBliss, Guid);
                    suffix = " and has reached its upper limit. You need to issue the /attr command to increase attributes.";
                }

                var sound = new GameMessageSound(Guid, Sound.RaiseTrait);
                var msg = new GameMessageSystemChat($"Your base {attribute} is now {creatureAttribute.Base}{suffix}!", ChatMessageType.Advancement);

                Session.Network.EnqueueSend(sound, msg);

                if (attribute == PropertyAttribute.Endurance)
                {
                    // this packet appears to trigger client to update both health and stamina
                    var updateHealth = new GameMessagePrivateUpdateVital(this, Health);

                    Session.Network.EnqueueSend(updateHealth);
                }
                else if (attribute == PropertyAttribute.Self)
                {
                    var updateMana = new GameMessagePrivateUpdateVital(this, Mana);

                    Session.Network.EnqueueSend(updateMana);
                }

                // retail was missing the 'raise attribute' runrate hook here
                if ((attribute == PropertyAttribute.Strength || attribute == PropertyAttribute.Quickness) && PropertyManager.GetBool("runrate_add_hooks").Item)
                    HandleRunRateUpdate();
            }

            return true;
        }

        private bool SpendAttributeXp(CreatureAttribute creatureAttribute, uint amount, bool sendNetworkUpdate = true)
        {

            // everything looks good at this point,
            // spend xp on attribute
            if (!SpendXP(amount, sendNetworkUpdate))
            {
                log.Error($"{Name}.SpendAttributeXp({creatureAttribute.Attribute}, {amount}) - SpendXP failed");
                return false;
            }

            creatureAttribute.ExperienceSpent += amount;

            // calculate new rank
            creatureAttribute.Ranks = (uint)CalcAttributeRank(creatureAttribute.ExperienceSpent);

            return true;
        }

        public void SpendAllAvailableAttributeXp(CreatureAttribute creatureAttribute, bool sendNetworkUpdate = true)
        {
            var amountRemaining = creatureAttribute.ExperienceLeft;

            if (amountRemaining > AvailableExperience)
                amountRemaining = (uint)AvailableExperience;

            SpendAttributeXp(creatureAttribute, amountRemaining, sendNetworkUpdate);
        }

        /// <summary>
        /// Returns the maximum rank that can be purchased with an xp amount
        /// </summary>
        /// <param name="xpAmount">The amount of xp used to make the purchase</param>
        public static uint CalcAttributeRank(uint xpAmount)
        {
            var rankXpTable = DatManager.PortalDat.XpTable.AttributeXpList;
            for (var i = rankXpTable.Count - 1; i >= 0; i--) //count down from 190
            {
                var rankAmount = rankXpTable[i];
                if (xpAmount >= rankAmount)
                    return (uint)i;
            }

            var prevRankAmount = rankXpTable[190];
            uint x = 190;
            while (true) //count up from 190 until we find a rank
            {
                if (xpAmount <= prevRankAmount)
                {
                    return x;
                }
                prevRankAmount += (uint)(prevRankAmount * .076);
                x++;
            }
            //return -1;
        }

        public static uint GetXPCostByRank(uint rank)
        {
            var rankXpTable = DatManager.PortalDat.XpTable.AttributeXpList;
            if (rank < rankXpTable.Count)
                return rankXpTable[(int)rank];            
            else
            {
                var prevRankAmount = rankXpTable[190];
                for (int i = 190; i <= rank; i++)
                {
                    prevRankAmount += (uint)(prevRankAmount * .076);
                }
                return prevRankAmount;
            }
        }

        public static uint GetXPDeltaCostByRank(uint destinationRank, uint currentRank)
        {
            var rankXpTable = DatManager.PortalDat.XpTable.AttributeXpList;
            if (destinationRank < rankXpTable.Count)
            {
                if (currentRank < rankXpTable.Count)
                    return rankXpTable[(int)destinationRank] - rankXpTable[(int)currentRank];
                else
                {
                    var prevRankAmount = rankXpTable[190];
                    for (int i = 190; i <= currentRank; i++)
                    {
                        prevRankAmount += (uint)(prevRankAmount * .076);
                    }
                    return rankXpTable[(int)destinationRank] - prevRankAmount;
                }
            }
            else
            {
                var prevRankAmount = rankXpTable[190];
                for (int i = 190; i <= destinationRank; i++)
                {
                    prevRankAmount += (uint)(prevRankAmount * .076);
                }
                if (currentRank < rankXpTable.Count)
                    return prevRankAmount - rankXpTable[(int)currentRank];
                else
                {
                    var prevRankAmount2 = rankXpTable[190];
                    for (int i = 190; i <= currentRank; i++)
                    {
                        prevRankAmount2 += (uint)(prevRankAmount2 * .076);
                    }
                    return prevRankAmount - prevRankAmount2;
                }
            }
        }
    }
}
