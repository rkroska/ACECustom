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
        // - Your level, which reverts to 1. A Level Retention Stone used beforehand keeps it instead, up to
        //   enl_keeps_level_up_to (RetainLevel); skills reset either way.
        // - Skills, which revert to their base state.
        // - Any items that are required for enlightenment, as per tier requirements. 
        // - Enchantments / spells (you are dispelled).
        //
        // You KEEP:
        // - All unspent experience.
        // - All stats and augmentations.
        // - Learned spells.
        //
        // You GAIN:
        // - +1 to enlightenment property (used for calculating dynamic bonuses).
        // - +2 Vitality (via enlightenment property calculation).
        // - +1 to all skills (via enlightenment property calculation).

        /// <param name="confirmed">
        /// What the /enlighten prompt told the player would happen to their level (null from callers with no prompt). If it
        /// no longer holds, the ritual stops before it spends anything.
        /// </param>
        public static void HandleEnlightenment(Player player, Outcome? confirmed = null)
        {
            if (!VerifyRequirements(player)) return;

            if (!ConfirmedOutcomeHolds(player, confirmed, out _)) return;

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

                // Re-validate since 14 seconds have passed.
                if (!VerifyRequirements(player, isRevalidation: true)) return;

                // and what the player was told still holds (the level can pass enl_keeps_level_up_to while the prompt is open).
                // The ritual then carries out this outcome, without reading the settings again.
                if (!ConfirmedOutcomeHolds(player, confirmed, out var outcome)) return;

                if (!SpendLuminance(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to enlighten!", ChatMessageType.Broadcast));
                    return;
                }
                var targetEnlightenment = player.Enlightenment + 1;
                RemoveTierConsumable(player, targetEnlightenment);

                // Enlightenment can proceed.
                DequipAllItems(player);
                RemoveFromFellowships(player);
                player.ThreadSafeTeleportOnDeath();
                RemoveSociety(player);
                RemoveSkills(player);

                // A Level Retention Stone keeps the level (up to enl_keeps_level_up_to); each stone covers one enlightenment.
                // Skills reset either way.
                if (outcome.KeepsLevel)
                {
                    RetainLevel(player, outcome.LevelDropsTo ?? player.Level ?? 1);
                    player.LevelRetentionPending = false;
                }
                else
                    RemoveLevel(player);

                RemoveAllSpells(player);
                AddPerks(player);
                player.SaveBiotaToDatabase();
            });

            // Set the chain to run
            enlChain.EnqueueChain();

        }

        /// <param name="isRevalidation">
        /// True for the second pass at the end of the 14 second chain. That pass skips the
        /// start-only Contact check while retaining the other validation checks.
        /// The concurrency guards that actually prevent a double resolve - Teleporting, TooBusyToRecall
        /// and IsInDeathProcess - are still enforced on both passes.
        /// </param>
        private static bool VerifyRequirements(Player player, bool isRevalidation = false)
        {
            if (player.Level < (275 + player.Enlightenment))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must be level 275 Plus 1 per Previous Enlightenment to enlighten further.", ChatMessageType.Broadcast));
                return false;
            }

            // Room for what the ritual actually does: DequipAllItems drops every EquippedObjects
            // entry into the MAIN pack, so the requirement is one slot per equipped item plus one,
            // against the main pack only - a flat 25 against all packs refused characters the
            // dequip would have fit, and passed characters it would not.
            // The phrase "free inventory slots" is load-bearing: AutoEnlighten's chat classifier
            // matches it to recognize this refusal.
            var requiredSlots = player.EquippedObjects.Count + 1;
            var freeMainPackSlots = player.GetFreeInventorySlots(includeSidePacks: false);
            if (freeMainPackSlots < requiredSlots)
            {
                var shortBy = requiredSlots - freeMainPackSlots;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Enlightenment needs {shortBy} more free inventory slot{(shortBy == 1 ? "" : "s")} in your main pack.", ChatMessageType.Broadcast));
                return false;
            }

            if (player.HasVitae)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot reach enlightenment with a Vitae Penalty. Go find those lost pieces of your soul and try again. Check under the couch cushions, that's where I usually lose mine.", ChatMessageType.Broadcast));
                return false;
            }

            if (player.Teleporting || player.TooBusyToRecall || player.IsInDeathProcess)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot enlighten while teleporting or busy. Complete your movement and try again. Neener neener.", ChatMessageType.System));
                return false;
            }

            // Both feet on the ground. This replaces an IsAnimating check that was wrong in BOTH
            // directions:
            //
            //   * It did not stop what it looked like it was for. Jump, let the jump animation
            //     finish, answer the confirmation while still falling, and the ritual ran in
            //     mid-air - because falling is not animating. The 14 second chain is a blind timer
            //     and never verifies the motion played, so the animation was skipped entirely.
            //   * It DID refuse a character standing perfectly still whose motion flag had gone
            //     stale. Since the command handler does not check IsAnimating, the server raised
            //     the confirmation, took the player's Yes, and only then refused - on a flag it had
            //     never tested before asking.
            //
            // Contact rather than OnWalkable on purpose. Contact means "touching something", which
            // is the exact opposite of airborne. OnWalkable additionally fails on ground too steep
            // to stand on, and refusing someone for standing on a slope is precisely the kind of
            // over-strictness being removed here.
            //
            // Start only. Jumping mid-ritual is already answered by the distance check at the end,
            // and re-testing at re-validation would just move the failure to 14 seconds in.
            if (!isRevalidation && player.PhysicsObj != null
                && !player.PhysicsObj.TransientState.HasFlag(ACE.Server.Physics.TransientStateFlags.Contact))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot begin the enlightenment ritual in mid-air. Find your footing and try again.", ChatMessageType.System));
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
            long lumCost = EnlightenmentTierManager.CalculateLuminanceCost(targetEnlightenment);
            if (player.GetTotalLuminance() < lumCost)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have {lumCost:N0} luminance to enlighten to level {targetEnlightenment}.", ChatMessageType.Broadcast));
                return false;
            }

            if (!VerifyTierItemAndQuest(player, targetEnlightenment, isRevalidation))
                return false;

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

            return true;
        }

        private static bool VerifyTierItemAndQuest(Player player, int targetEnlightenment, bool isRevalidation)
        {
            if (!EnlightenmentTierManager.TryGetTier(targetEnlightenment, out var tier))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("Enlightenment configuration error: no tier matches your target level. Notify an administrator.", ChatMessageType.Broadcast));
                return false;
            }

            if (tier.ItemWcid.HasValue)
            {
                var need = tier.GetRequiredItemCount(targetEnlightenment);
                // Enlightenment Tokens count from the pack AND the bank (owner 2026-10-13)
                long have = player.GetNumInventoryItemsOfWCID((uint)tier.ItemWcid.Value);
                if ((uint)tier.ItemWcid.Value == Player.ENLIGHTENMENT_TOKEN_WCID)
                    have += player.BankedEnlightenmentTokens ?? 0;
                if (have < need)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"You have already been enlightened {player.Enlightenment} times. You must have {need} {tier.ItemLabel} to continue.",
                        ChatMessageType.Broadcast));
                    return false;
                }
            }

            // Any one of the tier's flags passes: the Paragon, or the gate fee flag (owner 2026-10-24)
            if (tier.QuestStamps.Count > 0 && !tier.QuestStamps.Any(stamp => player.QuestManager.GetCurrentSolves(stamp) >= 1))
            {
                var msg = tier.QuestFailureMessage ?? "You lack a required quest completion for this enlightenment tier.";
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

                // the refusal above stays first and unchanged; the offer follows it
                if (!isRevalidation)
                    EnlightenmentGate.OfferFee(player, tier);

                return false;
            }

            return true;
        }

        private static bool VerifySocietyMaster(Player player)
        {
            return player.SocietyRankCelhan == 1001 || player.SocietyRankEldweb == 1001 || player.SocietyRankRadblo == 1001;
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

        private static void RemoveTierConsumable(Player player, int targetEnlightenment)
        {
            if (!EnlightenmentTierManager.TryGetTier(targetEnlightenment, out var tier) || !tier.ItemWcid.HasValue)
                return;

            var amount = tier.GetRequiredItemCount(targetEnlightenment);
            if (amount <= 0)
                return;

            // economy ledger: the tokens or other items an enlightenment costs
            using var ledgerScope = PyrealLedger.Begin(EconomyLedger.SrcEnlighten, targetEnlightenment.ToString(), "Enlightenment " + targetEnlightenment, player);

            var wcid = (uint)tier.ItemWcid.Value;
            if (wcid != Player.ENLIGHTENMENT_TOKEN_WCID)
            {
                player.TryConsumeFromInventoryWithNetworking(wcid, amount);
                return;
            }

            // Enlightenment Tokens (owner 2026-10-13): the pack first, then the bank - the order Prestige Coins are spent in
            var fromPack = Math.Min(amount, player.GetNumInventoryItemsOfWCID(wcid));
            if (fromPack > 0)
                player.TryConsumeFromInventoryWithNetworking(wcid, fromPack);

            if (amount > fromPack)
                player.TrySpendBankedEnlightenmentTokens(amount - fromPack);
        }

        private static bool SpendLuminance(Player player)
        {
            int targetEnlightenment = player.Enlightenment + 1;
            long lumCost = EnlightenmentTierManager.CalculateLuminanceCost(targetEnlightenment);
            using (PyrealLedger.Begin(EconomyLedger.SrcEnlighten, targetEnlightenment.ToString(), "Enlightenment " + targetEnlightenment, player))
                return player.SpendLuminance(lumCost);
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

        /// <summary>
        /// Enlightening after a Level Retention Stone: the level stays, at most enl_keeps_level_up_to (above it, the level and
        /// total experience drop to that level's). RemoveSkills has just rebuilt the skill credit pool for a level 1 character,
        /// so the credits for levels 2 up to the kept level go back in, as CheckForLevelup gave them on the way up. Leveling
        /// past the kept level again gives the credits above it, as for any character.
        /// </summary>
        /// <param name="plannedLevel">The level of the outcome checked before anything was spent (PlannedOutcome).</param>
        private static void RetainLevel(Player player, int plannedLevel)
        {
            var level = player.Level ?? 1;
            var keptLevel = Math.Min(plannedLevel, level);

            if (keptLevel < level)
            {
                var totalXp = TotalExperienceForLevel(keptLevel);
                player.TotalExperience = (long)Math.Min(totalXp, long.MaxValue);
                player.TotalExperienceDouble = totalXp;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.TotalExperience, player.TotalExperience ?? 0));

                player.Level = keptLevel;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Level, keptLevel));
            }

            var xpTable = DatManager.PortalDat.XpTable;
            player.AvailableSkillCredits = (player.AvailableSkillCredits ?? 0) + LevelSkillCredits(keptLevel, l => xpTable.CharacterLevelSkillCreditList[l]);
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));

            player.SendMessage(keptLevel < level
                ? $"Your Level Retention Stone kept your level up to {keptLevel}: your level has been lowered to {keptLevel}."
                : $"Your Level Retention Stone kept your level ({keptLevel}).", ChatMessageType.Broadcast);
        }

        /// <summary>
        /// The skill credits a character earns by leveling from 1 to this level, as CheckForLevelup gives them: the chart for
        /// levels 2 to 274 (datCredits: the chart's credits at a level), then one at every 5th level from 275 on.
        /// </summary>
        public static int LevelSkillCredits(int level, Func<int, uint> datCredits)
        {
            long credits = 0;

            for (var l = 2; l <= Math.Min(level, 274); l++)
                credits += datCredits(l);

            if (level >= 275)
                credits += level / 5 - 274 / 5;   // 275, 280, ... up to level

            return (int)Math.Min(credits, int.MaxValue);
        }

        /// <summary>
        /// The level a Level Retention Stone keeps: the character's own, at most keepUpTo (0 or less = no limit)
        /// </summary>
        public static int KeptLevel(int level, long keepUpTo)
        {
            return keepUpTo > 0 && level > keepUpTo ? (int)keepUpTo : level;
        }

        /// <summary>
        /// The total experience a character has on reaching this level: the DAT table through 275, the dynamic curve past it
        /// </summary>
        private static double TotalExperienceForLevel(int level)
        {
            if (level > 275)
                return Player.GenerateDynamicLevelPostMax(level);

            return DatManager.PortalDat.XpTable.CharacterLevelXPList[Math.Clamp(level, 1, 275)];
        }

        /// <summary>
        /// What enlightening will do to a character's level (skills always reset): return it to 1 (KeepsLevel false), or keep it
        /// after a Level Retention Stone (LevelDropsTo null) or lower it to enl_keeps_level_up_to (LevelDropsTo). The /enlighten
        /// prompt promises one, and the ritual checks it still holds before spending anything.
        /// </summary>
        public readonly record struct Outcome(bool KeepsLevel, int? LevelDropsTo);

        /// <summary>The outcome with the values passed in.</summary>
        /// <param name="stoneUsed">True when the character used a Level Retention Stone since it last enlightened.</param>
        public static Outcome PlannedOutcome(int level, bool stoneUsed, long keepUpTo)
        {
            if (!stoneUsed)
                return new Outcome(false, null);

            var keptLevel = KeptLevel(level, keepUpTo);

            return new Outcome(true, keptLevel < level ? (int?)keptLevel : null);
        }

        /// <summary>The outcome for this character now, with the live setting.</summary>
        public static Outcome PlannedOutcome(Player player)
        {
            return PlannedOutcome(player.Level ?? 1, player.LevelRetentionPending, ServerConfig.enl_keeps_level_up_to.Value);
        }

        /// <summary>
        /// False, with a message, when what the player confirmed no longer holds: the character's level passed
        /// enl_keeps_level_up_to, or the setting changed. Called before anything is spent.
        /// </summary>
        /// <param name="planned">The outcome now, which the ritual carries out when this returns true.</param>
        private static bool ConfirmedOutcomeHolds(Player player, Outcome? confirmed, out Outcome planned)
        {
            planned = PlannedOutcome(player);

            if (!confirmed.HasValue || planned == confirmed.Value)
                return true;

            player.SendMessage("What enlightening would do to your level has changed since you confirmed. Nothing was spent. Use /enlighten again to see what it will do now.", ChatMessageType.Broadcast);
            return false;
        }

        /// <summary>The Level Retention Stone: stackable, Bonded and Sticky Attuned, used up on use (Gem.ActOnUse).</summary>
        public const uint LevelRetentionStoneWcid = 98760530;

        /// <summary>
        /// Why a character can't use a Level Retention Stone now, or null when it can: it needs Enlightenment minEnlightenment
        /// (enl_level_retention_from, 50 by default), and one stone covers the next enlightenment.
        /// </summary>
        public static string LevelRetentionStoneRefusal(int currentEnlightenment, long minEnlightenment, bool alreadyRetained)
        {
            if (currentEnlightenment < minEnlightenment)
                return $"You must be at Enlightenment {minEnlightenment} or higher to use a Level Retention Stone.";

            if (alreadyRetained)
                return "Your next enlightenment already keeps your level.";

            return null;
        }

        private static string LevelRetentionStoneRefusal(Player player)
        {
            return LevelRetentionStoneRefusal(player.Enlightenment, ServerConfig.enl_level_retention_from.Value, player.LevelRetentionPending);
        }

        /// <summary>
        /// Using a Level Retention Stone: after a Yes, one stone is used up and the character's next enlightenment keeps its level
        /// (up to enl_keeps_level_up_to) instead of returning it to 1. Skills still reset. That enlightenment removes the mark
        /// (PropertyInt.LevelRetentionPending).
        /// </summary>
        public static void UseLevelRetentionStone(Player player)
        {
            var refusal = LevelRetentionStoneRefusal(player);
            if (refusal != null)
            {
                player.SendTransientError(refusal);
                return;
            }

            var keepUpTo = ServerConfig.enl_keeps_level_up_to.Value;
            var upTo = keepUpTo > 0 ? $", up to level {keepUpTo}," : "";
            var msg = $"Use a Level Retention Stone? Your next enlightenment will keep your level{upTo} instead of returning it to 1. Your skills will still reset. The stone will be used up.";

            void onResponse(bool response, bool _)
            {
                if (!response)
                    return;

                // checked again: enl_level_retention_from may have changed while the box was open
                var refusedNow = LevelRetentionStoneRefusal(player);
                if (refusedNow != null)
                {
                    player.SendTransientError(refusedNow);
                    return;
                }

                // the Yes agreed to the cap in the box: when enl_keeps_level_up_to changed while it was open, nothing is used
                // up and the box comes back with the new cap
                if (ServerConfig.enl_keeps_level_up_to.Value != keepUpTo)
                {
                    player.SendMessage("The level a Level Retention Stone keeps up to changed while you were deciding. Nothing was used up. Please answer again.", ChatMessageType.Broadcast);
                    UseLevelRetentionStone(player);
                    return;
                }

                // TryConsumeFromInventoryWithNetworking(wcid, ...) reports success when there is nothing to consume: count first
                if (player.GetNumInventoryItemsOfWCID(LevelRetentionStoneWcid) < 1 || !player.TryConsumeFromInventoryWithNetworking(LevelRetentionStoneWcid, 1))
                {
                    player.SendTransientError("You no longer have a Level Retention Stone.");
                    return;
                }

                player.LevelRetentionPending = true;

                player.SendMessage($"Your next enlightenment will keep your level{(keepUpTo > 0 ? $" (up to level {keepUpTo})" : "")}. Your skills will still reset.", ChatMessageType.Broadcast);
            }

            if (!player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, onResponse), msg))
                player.SendWeenieError(WeenieError.ConfirmationInProgress);
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

            availableSkillCredits += player.SkillCreditsFromGems;   // Skill Credit Gems: the character's for good

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
