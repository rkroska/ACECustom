using ACE.Common;
using ACE.Database.Models.Auth;
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
        public const double AttributeXpMod = 0.077;
        public bool HandleActionRaiseAttribute(PropertyAttribute attribute, ulong amount)
        {
            if (!Attributes.TryGetValue(attribute, out var creatureAttribute))
            {
                log.Error($"{Name}.HandleActionRaiseAttribute({attribute}, {amount}) - invalid attribute");
                return false;
            }

            if ((long)amount > AvailableExperience)
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

        private bool SpendAttributeXp(CreatureAttribute creatureAttribute, ulong amount, bool sendNetworkUpdate = true)
        {

            // everything looks good at this point,
            // spend xp on attribute
            if (!SpendXP(amount, sendNetworkUpdate))
            {
                log.Error($"{Name}.SpendAttributeXp({creatureAttribute.Attribute}, {amount}) - SpendXP failed");
                return false;
            }
            UpdateExtendedAttributeExperience(creatureAttribute, amount);

            creatureAttribute.ExperienceSpent = creatureAttribute.ExperienceSpent + (uint)amount;

            double calcedXp = creatureAttribute.ExperienceSpent;
            if (GetExtendedAttributeExperience(creatureAttribute) > creatureAttribute.ExperienceSpent)
            {
                calcedXp = GetExtendedAttributeExperience(creatureAttribute).Value;
            }
            // calculate new rank
            creatureAttribute.Ranks = CalcAttributeRank(calcedXp);

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
        public static uint CalcAttributeRank(double xpAmount)
        {
            var rankXpTable = DatManager.PortalDat.XpTable.AttributeXpList;
            var prevRankAmount = (double)rankXpTable[190];
            if (xpAmount < prevRankAmount)
            {
                for (var i = rankXpTable.Count - 1; i >= 0; i--) //count down from 190
                {
                    var rankAmount = rankXpTable[i];
                    if (xpAmount >= rankAmount)
                        return (uint)i;
                }
            }
                        
            uint x = 190;
            while (true) //count up from 190 until we find a rank
            {
                if (xpAmount <= prevRankAmount)
                {
                    return x;
                }
                prevRankAmount += (double)(prevRankAmount * AttributeXpMod);
                x++;
            }
            //return -1;
        }

        public static ulong GetXPCostByRank(uint rank)
        {
            var rankXpTable = DatManager.PortalDat.XpTable.AttributeXpList;
            if (rank < rankXpTable.Count)
                return rankXpTable[(int)rank];            
            else
            {
                var prevRankAmount = (ulong)rankXpTable[190];
                for (int i = 190; i <= rank; i++)
                {
                    prevRankAmount += (ulong)(prevRankAmount * .076);
                }
                return prevRankAmount;
            }
        }

        public static ulong GetXPDeltaCostByRank(uint destinationRank, uint currentRank)
        {
            var rankXpTable = DatManager.PortalDat.XpTable.AttributeXpList;           
            if (destinationRank < rankXpTable.Count)
            {
                if (currentRank < rankXpTable.Count)
                    return rankXpTable[(int)destinationRank] - rankXpTable[(int)currentRank];
                else
                {
                    var prevRankAmount = (ulong)rankXpTable[190];
                    for (int i = 190; i < currentRank; i++)
                    {
                        prevRankAmount += (ulong)(prevRankAmount * AttributeXpMod);
                    }
                    return (ulong)rankXpTable[(int)destinationRank] - prevRankAmount;
                }
            }
            else
            {
                var prevRankAmount = (ulong)rankXpTable[190];
                for (int i = 190; i < destinationRank; i++)
                {
                    prevRankAmount += (ulong)(prevRankAmount * AttributeXpMod);
                }
                if (currentRank < rankXpTable.Count)
                    return (ulong)prevRankAmount - (ulong)rankXpTable[(int)currentRank];
                else
                {
                    var prevRankAmount2 = (ulong)rankXpTable[190];
                    for (int i = 190; i < currentRank; i++)
                    {
                        prevRankAmount2 += (ulong)(prevRankAmount2 * AttributeXpMod);
                    }
                    return prevRankAmount - prevRankAmount2;
                }
            }
        }

        public void UpdateExtendedAttributeExperience(CreatureAttribute attrib, ulong amount)
        {
            switch (attrib.Attribute)
            {
                case PropertyAttribute.Undef:
                    break;
                case PropertyAttribute.Strength:
                    if (!SpentExperienceStrength.HasValue || SpentExperienceStrength == 0)
                    {
                        SpentExperienceStrength = attrib.ExperienceSpent;
                    }
                    SpentExperienceStrength += amount;
                    break;
                case PropertyAttribute.Endurance:
                    if (!SpentExperienceEndurance.HasValue || SpentExperienceEndurance == 0)
                    {
                        SpentExperienceEndurance = attrib.ExperienceSpent;
                    }
                    SpentExperienceEndurance += amount;
                    break;
                case PropertyAttribute.Quickness:
                    if (!SpentExperienceQuickness.HasValue || SpentExperienceQuickness == 0)
                    {
                        SpentExperienceQuickness = attrib.ExperienceSpent;
                    }
                    SpentExperienceQuickness += amount;
                    break;
                case PropertyAttribute.Coordination:
                    if (!SpentExperienceCoordination.HasValue || SpentExperienceCoordination == 0)
                    {
                        SpentExperienceCoordination = attrib.ExperienceSpent;
                    }
                    SpentExperienceCoordination += amount;
                    break;
                case PropertyAttribute.Focus:
                    if (!SpentExperienceFocus.HasValue || SpentExperienceFocus == 0)
                    {
                        SpentExperienceFocus = attrib.ExperienceSpent;
                    }
                    SpentExperienceFocus += amount;
                    break;
                case PropertyAttribute.Self:
                    if (!SpentExperienceSelf.HasValue || SpentExperienceSelf == 0)
                    {
                        SpentExperienceSelf = attrib.ExperienceSpent;
                    }
                    SpentExperienceSelf += amount;
                    break;
                default:
                    break;
            }
        }

        public double? GetExtendedAttributeExperience(CreatureAttribute attrib)
        {
            switch (attrib.Attribute)
            {
                case PropertyAttribute.Undef:
                    return null;
                case PropertyAttribute.Strength:
                    return SpentExperienceStrength;
                case PropertyAttribute.Endurance:
                    return SpentExperienceEndurance;
                case PropertyAttribute.Quickness:
                    return SpentExperienceQuickness;
                case PropertyAttribute.Coordination:
                    return SpentExperienceCoordination;
                case PropertyAttribute.Focus:
                    return SpentExperienceFocus;
                case PropertyAttribute.Self:
                    return SpentExperienceSelf;
                default:
                    return null;
            }
        }
    }
}
