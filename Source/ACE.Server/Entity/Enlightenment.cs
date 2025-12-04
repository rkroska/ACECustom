using System;
using System.Linq;

using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Common;
using ACE.Server.Entity.Actions;
using System.Runtime.CompilerServices;

namespace ACE.Server.Entity
{
    public class Enlightenment
    {
        // https://asheron.fandom.com/wiki/Enlightenment

        // Reset your character to level 1, losing all experience and luminance but gaining a title, two points in vitality and one point in all of your skills.
        // In order to be eligible for enlightenment, you must be level 275, Master rank in a Society, and have all luminance auras with the exception of the skill credit auras.

        // As stated in the Spring 2014 patch notes, Enlightenment is a process for the most devoted players of Asheron's Call to continue enhancing characters which have been "maxed out" in terms of experience and abilities.
        // It was not intended to be a quest that every player would undertake or be interested in.

        // Requirements:
        // - Level 275
        // - Have all luminance auras (crafting aura included) except the 2 skill credit auras. (20 million total luminance)
        // - Have mastery rank in a society
        // - Have 25 unused pack spaces
        // - Max # of times for enlightenment: 5

        // You lose:
        // - All experience, reverting to level 1.
        // - All luminance, and luminance auras with the exception of the skill credit auras.
        // - The ability to use aetheria (until you attain sufficient level and re-open aetheria slots).
        // - The ability to gain luminance (until you attain level 200 and re-complete Nalicana's Test).
        // - The ability to equip and use items which have skill and level requirements beyond those of a level 1 character.
        //   Any equipped items are moved to your pack automatically.

        // You keep:
        // - All augmentations obtained through Augmentation Gems.
        // - Skill credits from luminance auras, Aun Ralirea, and Chasing Oswald quests.
        // - All quest flags with the exception of aetheria and luminance.

        // You gain:
        // - A new title each time you enlighten
        // - +2 to vitality
        // - +1 to all of your skills
        // - An attribute reset certificate

        public static void HandleEnlightenment(Player player)
        {
            if (!VerifyRequirements(player))
                return;

            DequipAllItems(player);

            RemoveFromFellowships(player);

            player.SendMotionAsCommands(MotionCommand.MarketplaceRecall, MotionStance.NonCombat);

            var startPos = new ACE.Entity.Position(player.Location);
            ActionChain enlChain = new ActionChain();
            enlChain.AddDelaySeconds(14);

            // Then do teleport
            player.IsBusy = true;
            enlChain.AddAction(player, ActionType.Enlightenment_DoEnlighten, () =>
            {
                player.IsBusy = false;
                var endPos = new ACE.Entity.Position(player.Location);
                if (startPos.SquaredDistanceTo(endPos) > Player.RecallMoveThresholdSq)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have moved too far during the enlightenment animation!", ChatMessageType.Broadcast));
                    return;
                }

                player.ThreadSafeTeleportOnDeath();
                if (!SpendLuminance(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to enlighten!", ChatMessageType.Broadcast));
                    return;
                }
                if (player.Enlightenment + 1 > 5 && player.Enlightenment < 150)
                {
                    RemoveTokens(player);
                }
                else if
                (player.Enlightenment >= 150 && player.Enlightenment < 300)
                {
                    RemoveMedallion(player);
                }
                else if
                (player.Enlightenment >= 300)
                {
                    RemoveSigil(player);
                }
                RemoveAbility(player);
                AddPerks(player);
                if (player.Enlightenment >= 25)
                {
                    DequipAllItems(player);
                }
                player.SaveBiotaToDatabase();
            });

            // Set the chain to run
            enlChain.EnqueueChain();

        }

        public static bool VerifyRequirements(Player player)
        {
            if (player.Level < (275 + player.Enlightenment))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must be level 275 Plus 1 per Previous Enlightenment to enlighten further.", ChatMessageType.Broadcast));
                return false;
            }

            if (player.GetFreeInventorySlots() < 25)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have at least 25 free inventory slots in your main pack for enlightenment, to unequip your gear automatically.", ChatMessageType.Broadcast));
                return false;
            }

            if (player.HasVitae)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot reach enlightenment with a Vitae Penalty. Go find those lost pieces of your soul and try again. Check under the couch cushions, that's where I usually lose mine.", ChatMessageType.Broadcast));
                return false;
            }

            if (player.Teleporting || player.TooBusyToRecall || player.IsAnimating || player.IsInDeathProcess)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot enlighten while teleporting or busy. Complete your movement and try again. Neener neener.", ChatMessageType.System));
                return false;
            }

            Landblock currentLandblock = LandblockManager.GetLandblock(player.Location.LandblockId, false, player.Location.Variation, false);
            if (currentLandblock != null && currentLandblock.IsDungeon)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot enlighten while inside a dungeon. Find an exit or recall to begin your enlightenment.", ChatMessageType.System));
                return false;
            }

            if (player.CombatMode != CombatMode.NonCombat)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot enlighten while in combat mode. Be at peace, friend.", ChatMessageType.System));
                return false;
            }


            if (player.LastPortalTeleportTimestamp.HasValue)
            {
                var timeSinceLastPortal = Time.GetUnixTime() - player.LastPortalTeleportTimestamp.Value;
                if (timeSinceLastPortal <= 10.0f)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You've teleported too recently to enlighten.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            var targetEnlightenment = player.Enlightenment + 1;

            //todo: check for trophies that are enl level appropriate
            //first, 1 enlightenment token per enlightenment past 5.
            if (targetEnlightenment > 5 && targetEnlightenment <= 150)
            {
                var count = player.GetNumInventoryItemsOfWCID(300000); //magic number - EnlightenmentToken
                if (count < player.Enlightenment + 1 - 5)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have already been enlightened {player.Enlightenment} times. You must have {player.Enlightenment + 1 - 5} Enlightenment Tokens to continue.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            if (targetEnlightenment > 10)
            {
                if (!VerifyLumAugs(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have all Standard Luminance Augmentations to continue your enlightenment beyond 10.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            if (targetEnlightenment > 30)
            {
                if (!VerifySocietyMaster(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must be a Master of your Society to enlighten beyond level 30.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            if (targetEnlightenment > 50 && targetEnlightenment <= 150)
            {
                var baseLumCost = PropertyManager.GetLong("enl_50_base_lum_cost");
                long reqLum = targetEnlightenment * baseLumCost;
                if (!VerifyLuminance(player, reqLum))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have {reqLum:N0} luminance to enlighten to level {targetEnlightenment}.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            if (targetEnlightenment > 150 && targetEnlightenment < 300)
            {
                var baseLumCost = PropertyManager.GetLong("enl_150_base_lum_cost");
                long reqLum150 = targetEnlightenment * baseLumCost;
                if (!VerifyLuminance(player, reqLum150))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have {reqLum150:N0} luminance to enlighten to level {targetEnlightenment}.", ChatMessageType.Broadcast));
                    return false;
                }
                var count2 = player.GetNumInventoryItemsOfWCID(90000217); //magic number - EnlightenmentToken
                if (count2 < player.Enlightenment + 1 - 5)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have already been enlightened {player.Enlightenment} times. You must have {player.Enlightenment + 1 - 5} Enlightenment Medallions to continue.", ChatMessageType.Broadcast));
                    return false;
                }
                if (!VerifyParagonCompleted(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have completed 50th Paragon to enlighten beyond level 150.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            if (targetEnlightenment > 300)
            {
                var baseLumCost = (decimal)PropertyManager.GetLong("enl_300_base_lum_cost");

                // how far past 300 we’re going
                int over = targetEnlightenment - 300;

                // step increases begin AFTER the first 50 levels
                // 301–350 => steps = 0 (1.0×)
                // 351–400 => steps = 1 (1.5×)
                // 401–450 => steps = 2 (2.0×), etc.
                int steps = (over - 1) / 50;         // integer division
                decimal costModifier = 1.0m + (0.5m * steps);

                long reqLum300 = (long)Math.Ceiling((targetEnlightenment * baseLumCost) * costModifier);

                if (!VerifyLuminance(player, reqLum300))
                {
                    player.Session.Network.EnqueueSend(
                        new GameMessageSystemChat($"You must have {reqLum300:N0} luminance to enlighten to level {targetEnlightenment}.",
                        ChatMessageType.Broadcast));
                    return false;
                }

                var count2 = player.GetNumInventoryItemsOfWCID(300101189); // EnlightenmentSigil
                if (count2 < player.Enlightenment + 1 - 5)
                {
                    player.Session.Network.EnqueueSend(
                        new GameMessageSystemChat($"You have already been enlightened {player.Enlightenment} times. You must have {player.Enlightenment + 1 - 5} Enlightenment Sigils to continue.",
                        ChatMessageType.Broadcast));
                    return false;
                }

                if (!VerifyParagonArmorCompleted(player))
                {
                    player.Session.Network.EnqueueSend(
                        new GameMessageSystemChat($"You must have completed 50th Armor Paragon to enlighten beyond level 300.",
                        ChatMessageType.Broadcast));
                    return false;
                }
            }
            return true;
        }

        public static bool VerifyLuminance(Player player, long reqLum)
        {
            return player.BankedLuminance >= reqLum;
        }

        public static bool VerifySocietyMaster(Player player)
        {
            return player.SocietyRankCelhan == 1001 || player.SocietyRankEldweb == 1001 || player.SocietyRankRadblo == 1001;
        }

        public static bool VerifyParagonCompleted(Player player)
        {
            return player.QuestManager.GetCurrentSolves("ParagonEnlCompleted") >= 1;
        }

        public static bool VerifyParagonArmorCompleted(Player player)
        {
            return player.QuestManager.GetCurrentSolves("ParagonArmorCompleted") >= 1;
        }

        public static bool VerifyLumAugs(Player player)
        {
            var lumAugCredits = 0;

            lumAugCredits += player.LumAugAllSkills;
            lumAugCredits += player.LumAugSurgeChanceRating;
            lumAugCredits += player.LumAugCritDamageRating;
            lumAugCredits += player.LumAugCritReductionRating;
            lumAugCredits += player.LumAugDamageRating;
            lumAugCredits += player.LumAugDamageReductionRating;
            lumAugCredits += player.LumAugItemManaUsage;
            lumAugCredits += player.LumAugItemManaGain;
            lumAugCredits += player.LumAugHealingRating;
            lumAugCredits += player.LumAugSkilledCraft;
            lumAugCredits += player.LumAugSkilledSpec;

            return lumAugCredits == 65;
        }

        public static void RemoveFromFellowships(Player player)
        {
            player.FellowshipQuit(false);
        }

        public static void DequipAllItems(Player player)
        {
            var equippedObjects = player.EquippedObjects.Keys.ToList();

            foreach (var equippedObject in equippedObjects)
                player.HandleActionPutItemInContainer(equippedObject.Full, player.Guid.Full, 0);
        }

        public static void RemoveAllSpells(Player player)
        {
            player.EnchantmentManager.DispelAllEnchantments();
        }

        public static void RemoveAbility(Player player)
        {
            RemoveSociety(player);
            //RemoveLuminance(player);
            RemoveSkills(player);
            RemoveLevel(player);
            RemoveAllSpells(player);
        }

        public static void RemoveTokens(Player player)
        {
            player.TryConsumeFromInventoryWithNetworking(300000, player.Enlightenment + 1 - 5);
        }

        public static void RemoveMedallion(Player player)
        {
            player.TryConsumeFromInventoryWithNetworking(90000217, player.Enlightenment + 1 - 5);
        }

        public static void RemoveSigil(Player player)
        {
            player.TryConsumeFromInventoryWithNetworking(300101189, player.Enlightenment + 1 - 5);
        }

        public static bool SpendLuminance(Player player)
        {
            if (player.Enlightenment + 1 > 50 && player.Enlightenment < 150)
            {
                var baseLumCost = PropertyManager.GetLong("enl_50_base_lum_cost");
                var targetEnlightenment = player.Enlightenment + 1;
                long reqLum = targetEnlightenment * baseLumCost;
                return player.SpendLuminance(reqLum);
            }

            else if (player.Enlightenment + 1 > 150 && player.Enlightenment < 300)
            {
                var baseLumCost = PropertyManager.GetLong("enl_150_base_lum_cost");
                var targetEnlightenment = player.Enlightenment + 1;
                long reqLum150 = targetEnlightenment * baseLumCost;
                return player.SpendLuminance(reqLum150);
            }

            else if (player.Enlightenment + 1 > 300)
            {
                var baseLumCost = PropertyManager.GetLong("enl_300_base_lum_cost");
                var targetEnlightenment = player.Enlightenment + 1;
                long reqLum300 = targetEnlightenment * baseLumCost;
                return player.SpendLuminance(reqLum300);
            }
            return true;

        }

        public static void RemoveSociety(Player player)
        {
            // Leave society alone if server prop is false
            if (PropertyManager.GetBool("enl_removes_society"))
            {
                player.QuestManager.Erase("SocietyMember");
                player.QuestManager.Erase("CelestialHandMember");
                player.QuestManager.Erase("EnlightenedCelestialHandMaster");
                player.QuestManager.Erase("EldrytchWebMember");
                player.QuestManager.Erase("EnlightenedEldrytchWebMaster");
                player.QuestManager.Erase("RadiantBloodMember");
                player.QuestManager.Erase("EnlightenedRadiantBloodMaster");

                if (player.SocietyRankCelhan == 1001)
                    player.QuestManager.Stamp("EnlightenedCelestialHandMaster"); // after rejoining society, player can get promoted instantly to master when speaking to promotions officer
                if (player.SocietyRankEldweb == 1001)
                    player.QuestManager.Stamp("EnlightenedEldrytchWebMaster");   // after rejoining society, player can get promoted instantly to master when speaking to promotions officer
                if (player.SocietyRankRadblo == 1001)
                    player.QuestManager.Stamp("EnlightenedRadiantBloodMaster");  // after rejoining society, player can get promoted instantly to master when speaking to promotions officer

                player.Faction1Bits = null;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Faction1Bits, 0));
                //player.SocietyRankCelhan = null;
                //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.SocietyRankCelhan, 0));
                //player.SocietyRankEldweb = null;
                //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.SocietyRankEldweb, 0));
                //player.SocietyRankRadblo = null;
                //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.SocietyRankRadblo, 0));
            }
        }

        public static void RemoveLevel(Player player)
        {
            player.TotalExperience = 0; player.TotalExperienceDouble = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.TotalExperience, player.TotalExperience ?? 0));
            //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyFloat(player, PropertyFloat.TotalExperienceDouble, player.TotalExperienceDouble ?? 0));

            player.Level = 1;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Level, player.Level ?? 0));
        }

        public static void RemoveAetheria(Player player)
        {
            player.QuestManager.Erase("EFULNorthManaFieldUsed");
            player.QuestManager.Erase("EFULSouthManaFieldUsed");
            player.QuestManager.Erase("EFULEastManaFieldUsed");
            player.QuestManager.Erase("EFULWestManaFieldUsed");
            player.QuestManager.Erase("EFULCenterManaFieldUsed");

            player.QuestManager.Erase("EFMLNorthManaFieldUsed");
            player.QuestManager.Erase("EFMLSouthManaFieldUsed");
            player.QuestManager.Erase("EFMLEastManaFieldUsed");
            player.QuestManager.Erase("EFMLWestManaFieldUsed");
            player.QuestManager.Erase("EFMLCenterManaFieldUsed");

            player.QuestManager.Erase("EFLLNorthManaFieldUsed");
            player.QuestManager.Erase("EFLLSouthManaFieldUsed");
            player.QuestManager.Erase("EFLLEastManaFieldUsed");
            player.QuestManager.Erase("EFLLWestManaFieldUsed");
            player.QuestManager.Erase("EFLLCenterManaFieldUsed");

            player.AetheriaFlags = AetheriaBitfield.None;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AetheriaBitfield, 0));

            player.SendMessage("Your mastery of Aetheric magics fades.", ChatMessageType.Broadcast);
        }

        public static void RemoveAttributes(Player player)
        {
            var propertyCount = Enum.GetNames(typeof(PropertyAttribute)).Length;
            for (var i = 1; i < propertyCount; i++)
            {
                var attribute = (PropertyAttribute)i;

                player.Attributes[attribute].Ranks = 0;
                player.Attributes[attribute].ExperienceSpent = 0;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[attribute]));
            }

            propertyCount = Enum.GetNames(typeof(PropertyAttribute2nd)).Length;
            for (var i = 1; i < propertyCount; i += 2)
            {
                var attribute = (PropertyAttribute2nd)i;

                player.Vitals[attribute].Ranks = 0;
                player.Vitals[attribute].ExperienceSpent = 0;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[attribute]));
            }

            player.SendMessage("Your attribute training fades.", ChatMessageType.Broadcast);
        }

        public static void RemoveSkills(Player player)
        {
            var propertyCount = Enum.GetNames(typeof(Skill)).Length;
            for (var i = 1; i < propertyCount; i++)
            {
                var skill = (Skill)i;

                player.ResetSkill(skill, false);
            }

            player.AvailableExperience = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableExperience, 0));

            var heritageGroup = DatManager.PortalDat.CharGen.HeritageGroups[(uint)player.Heritage];
            var availableSkillCredits = 0;

            availableSkillCredits += (int)heritageGroup.SkillCredits; // base skill credits allowed

            availableSkillCredits += player.QuestManager.GetCurrentSolves("ArantahKill1");       // additional quest skill credit
            availableSkillCredits += player.QuestManager.GetCurrentSolves("OswaldManualCompleted");  // additional quest skill credit
            availableSkillCredits += player.QuestManager.GetCurrentSolves("LumAugSkillQuest");   // additional quest skill credits

            player.AvailableSkillCredits = availableSkillCredits;

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
        }

        public static void RemoveLuminance(Player player)
        {
            player.AvailableLuminance = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableLuminance, 0));

            player.SendMessage("Your Luminance fades from your spirit.", ChatMessageType.Broadcast);
        }

        public static uint AttributeResetCertificate => 46421;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetEnlightenmentRatingBonus(int EnlightenmentAmt)
        {
            int bonus = 0;
            for (int x = 0; x < EnlightenmentAmt; x++)
            {
                if (x < 10)
                {
                    bonus++;
                }
                else if (x < 20)
                {
                    if (x % 2 == 0)
                    {
                        bonus++;
                    }
                }
                else if (x < 50)
                {
                    if (x % 5 == 0)
                    {
                        bonus++;
                    }
                }
                else
                {
                    if (x % 10 == 0)
                    {
                        bonus++;
                    }
                }
            }
            return bonus;
        }

        public static void AddPerks(Player player)
        {
            // +1 to all skills
            // this could be handled through InitLevel, since we are always using deltas when modifying that field
            // (ie. +5/-5, instead of specifically setting to 5 trained / 10 specialized in SkillAlterationDevice)
            // however, it just feels safer to handle this dynamically in CreatureSkill, based on Enlightenment (similar to augs)
            //var enlightenment = player.Enlightenment + 1;
            //player.UpdateProperty(player, PropertyInt.Enlightenment, enlightenment);

            player.Enlightenment += 1;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Enlightenment, player.Enlightenment));

            player.SendMessage("You have become enlightened and view the world with new eyes.", ChatMessageType.Broadcast);
            player.SendMessage("Your available skill credits have been adjusted.", ChatMessageType.Broadcast);
            player.SendMessage("You have risen to a higher tier of enlightenment!", ChatMessageType.Broadcast);

            var lvl = "";

            switch (player.Enlightenment % 100)
            {
                case 11:
                case 12:
                case 13:
                    lvl = player.Enlightenment + "th";
                    break;
            }
            if (string.IsNullOrEmpty(lvl))
            {
                switch (player.Enlightenment % 10)
                {
                    case 1:
                        lvl = player.Enlightenment + "st";
                        break;
                    case 2:
                        lvl = player.Enlightenment + "nd";
                        break;
                    case 3:
                        lvl = player.Enlightenment + "rd";
                        break;
                    default:
                        lvl = player.Enlightenment + "th";
                        break;
                }
            }
            
            // add title
            switch (player.Enlightenment)
            {
                case 1:
                    player.AddTitle(CharacterTitle.Awakened);                   
                    break;
                case 2:
                    player.AddTitle(CharacterTitle.Enlightened);
                    break;
                case 3:
                    player.AddTitle(CharacterTitle.Illuminated);
                    break;
                case 4:
                    player.AddTitle(CharacterTitle.Transcended);
                    break;
                case 5:
                    player.AddTitle(CharacterTitle.CosmicConscious);
                    break;                
            }

            var msg = $"{player.Name} has achieved the {lvl} level of Enlightenment!";
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            DiscordChatManager.SendDiscordMessage(player.Name, msg, ConfigManager.Config.Chat.GeneralChannelId);
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);

            // +2 vitality
            // handled automatically via PropertyInt.Enlightenment * 2

            /*var vitality = player.LumAugVitality + 2;
            player.UpdateProperty(player, PropertyInt.LumAugVitality, vitality);*/
        }
    }
}
