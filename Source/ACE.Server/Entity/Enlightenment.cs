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
        // Requirements:
        // - Level 275 + 1 per previous enlightenment
        // - Have all luminance auras (crafting aura included) except the skill credit auras.
        // - Have mastery rank in a Society.
        // - Inventory space requirements.
        //
        // You lose:
        // - Your level, which reverts to 1.
        // - Any items that are required for enlightenment, as per tier requirements. 
        // - Buffs and spells.
        //
        // You KEEP:
        // - All unspent experience.
        // - All skills and augmentations.
        //
        // You GAIN:
        // - +1 to enlightenment property (used for calculating dynamic bonuses).
        // - +2 Vitality (via enlightenment property calculation).
        // - +1 to all skills (via enlightenment property calculation).

        public static void HandleEnlightenment(Player player)
        {
            if (!VerifyRequirements(player))
                return;


            player.SendMotionAsCommands(MotionCommand.MarketplaceRecall, MotionStance.NonCombat);

            var startPos = new ACE.Entity.Position(player.Location);
            ActionChain enlChain = new();
            enlChain.AddDelaySeconds(14);

            player.IsBusy = true;
            enlChain.AddAction(player, ActionType.Enlightenment_DoEnlighten, () =>
            {
                player.IsBusy = false;
                var endPos = new ACE.Entity.Position(player.Location);

                // Attempt to enlighten. This involves checking distance and luminance.
                // Tokens / Medallions / Sigils are not checked but should be present due to validation running first.
                if (startPos.SquaredDistanceTo(endPos) > Player.RecallMoveThresholdSq)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have moved too far during the enlightenment animation!", ChatMessageType.Broadcast));
                    return;
                }
                if (!SpendLuminance(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to enlighten!", ChatMessageType.Broadcast));
                    return;
                }
                var targetEnlightenment = player.Enlightenment + 1;
                if (targetEnlightenment > 5 && targetEnlightenment <= 150)
                {
                    RemoveTokens(player);
                }
                else if (targetEnlightenment > 150 && targetEnlightenment <= 300)
                {
                    RemoveMedallion(player);
                }
                else if (targetEnlightenment > 300)
                {
                    RemoveSigil(player);
                }

                // Enlightenment can proceed.
                DequipAllItems(player);
                RemoveFromFellowships(player);
                player.ThreadSafeTeleportOnDeath();
                RemoveSociety(player);
                RemoveSkills(player);
                RemoveLevel(player);
                RemoveAllSpells(player);
                AddPerks(player);
                player.SaveBiotaToDatabase();
            });

            // Set the chain to run
            enlChain.EnqueueChain();

        }

        private static bool VerifyRequirements(Player player)
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
            long lumCost = CalculateLuminanceCost(targetEnlightenment);
            if (player.GetTotalLuminance() < lumCost)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have {lumCost:N0} luminance to enlighten to level {targetEnlightenment}.", ChatMessageType.Broadcast));
                return false;
            }

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

            if (targetEnlightenment > 150 && targetEnlightenment <= 300)
            {
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

        private static bool VerifySocietyMaster(Player player)
        {
            return player.SocietyRankCelhan == 1001 || player.SocietyRankEldweb == 1001 || player.SocietyRankRadblo == 1001;
        }

        private static bool VerifyParagonCompleted(Player player)
        {
            return player.QuestManager.GetCurrentSolves("ParagonEnlCompleted") >= 1;
        }

        private static bool VerifyParagonArmorCompleted(Player player)
        {
            return player.QuestManager.GetCurrentSolves("ParagonArmorCompleted") >= 1;
        }

        private static bool VerifyLumAugs(Player player)
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

        private static void RemoveFromFellowships(Player player)
        {
            player.FellowshipQuit(false);
        }

        private static void DequipAllItems(Player player)
        {
            var equippedObjects = player.EquippedObjects.Keys.ToList();

            foreach (var equippedObject in equippedObjects)
                player.HandleActionPutItemInContainer(equippedObject.Full, player.Guid.Full, 0);
        }

        private static void RemoveAllSpells(Player player)
        {
            player.EnchantmentManager.DispelAllEnchantments();
        }

        private static void RemoveTokens(Player player)
        {
            player.TryConsumeFromInventoryWithNetworking(300000, player.Enlightenment + 1 - 5);
        }

        private static void RemoveMedallion(Player player)
        {
            player.TryConsumeFromInventoryWithNetworking(90000217, player.Enlightenment + 1 - 5);
        }

        private static void RemoveSigil(Player player)
        {
            player.TryConsumeFromInventoryWithNetworking(300101189, player.Enlightenment + 1 - 5);
        }

        private static bool SpendLuminance(Player player)
        {
            int targetEnlightenment = player.Enlightenment + 1;
            long lumCost = CalculateLuminanceCost(targetEnlightenment);
            return player.SpendLuminance(lumCost);
        }

        // Calculates the lum needed for enlightening.
        private static long CalculateLuminanceCost(int targetEnlightenment)
        {
            // From 1-50: 0
            if (targetEnlightenment <= 50)
                return 0;

            // From 51-150: use the first threshold config
            if (targetEnlightenment <= 150)
                return targetEnlightenment * ServerConfig.enl_50_base_lum_cost.Value;

            // From 151-300: use the second threshold config
            if (targetEnlightenment <= 300)
                return targetEnlightenment * ServerConfig.enl_150_base_lum_cost.Value;

            // For 301+, use the third threshold config but apply a multiplier every 50 levels.
            // 301–350 => steps = 0 (1.0×)
            // 351–400 => steps = 1 (1.5×)
            // 401–450 => steps = 2 (2.0×), etc.
            int over = targetEnlightenment - 300;
            int steps = (over - 1) / 50;         // integer division
            decimal costModifier = 1.0m + (0.5m * steps);
            return (long)Math.Ceiling((targetEnlightenment * ServerConfig.enl_300_base_lum_cost.Value) * costModifier);
        }

        private static void RemoveSociety(Player player)
        {
            // Leave society alone if server prop is false
            if (ServerConfig.enl_removes_society.Value)
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
            }
        }

        private static void RemoveLevel(Player player)
        {
            player.TotalExperience = 0; player.TotalExperienceDouble = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.TotalExperience, player.TotalExperience ?? 0));
            
            player.Level = 1;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Level, player.Level ?? 0));
        }

        private static void RemoveSkills(Player player)
        {
            foreach (Skill skill in Enum.GetValues<Skill>())
            {
                if (skill == Skill.None) continue;
                player.ResetSkill(skill, false);
            }

            var heritageGroup = DatManager.PortalDat.CharGen.HeritageGroups[(uint)player.Heritage];
            var availableSkillCredits = 0;

            availableSkillCredits += (int)heritageGroup.SkillCredits; // base skill credits allowed

            availableSkillCredits += player.QuestManager.GetCurrentSolves("ArantahKill1");       // additional quest skill credit
            availableSkillCredits += player.QuestManager.GetCurrentSolves("OswaldManualCompleted");  // additional quest skill credit
            availableSkillCredits += player.QuestManager.GetCurrentSolves("LumAugSkillQuest");   // additional quest skill credits

            player.AvailableSkillCredits = availableSkillCredits;

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetEnlightenmentRatingBonus(int enlightenmentAmt)
        {
            // From 1-10: 1 Damage Rating per Enlightenment (Total of 10)
            if (enlightenmentAmt <= 10)
                return enlightenmentAmt;

            // From 11-20: 1 Damage Rating per 2 Enlightenments (Total of 5)
            if (enlightenmentAmt <= 20)
                return 10 + (enlightenmentAmt - 10) / 2;

            // From 21-50: 1 Damage Rating per 5 Enlightenments (Total of 6)
            if (enlightenmentAmt <= 50)
                return 15 + (enlightenmentAmt - 20) / 5;

            // From 51 and beyond: 1 Damage Rating per 10 Enlightenments
            return 21 + (enlightenmentAmt - 50) / 10;
        }

        private static void AddPerks(Player player)
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
            
            if (ACE.Server.Managers.ServerConfig.discord_broadcast_level.Value >= (long)ACE.Common.DiscordLogLevel.Info)
                _ = DiscordChatManager.SendDiscordMessage(player.Name, msg, ConfigManager.Config.Chat.GeneralChannelId);
            
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);

            // +2 vitality
            // handled automatically via PropertyInt.Enlightenment * 2

            /*var vitality = player.LumAugVitality + 2;
            player.UpdateProperty(player, PropertyInt.LumAugVitality, vitality);*/
        }
    }
}
