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
        // - The luminance cost and any items that are required for enlightenment, as per tier requirements.
        // - Enchantments / spells (you are dispelled).
        // - Only below Enlightenment enl_keeps_level_and_skills_from (50 by default), or always while
        //   enl_resets_level_and_skills is TRUE (see ResetsLevelAndSkills):
        //   - Your level, which reverts to 1.
        //   - Skills, which revert to their base state.
        //
        // You KEEP:
        // - All unspent experience.
        // - All stats and augmentations.
        // - Learned spells.
        // - From Enlightenment enl_keeps_level_and_skills_from on (unless enl_resets_level_and_skills is TRUE): your skills,
        //   specializations and skill credits; and your level and experience (up to enl_keeps_level_up_to) after a Level
        //   Retention Stone, otherwise your level returns to 1.
        //
        // You GAIN:
        // - +1 to enlightenment property (used for calculating dynamic bonuses).
        // - +2 Vitality (via enlightenment property calculation).
        // - +1 to all skills (via enlightenment property calculation).

        /// <param name="confirmed">
        /// What the /enlighten prompt told the player would happen to their level and skills (null from callers with no
        /// prompt). If it no longer holds, the ritual stops before it spends anything.
        /// </param>
        public static void HandleEnlightenment(Player player, Outcome? confirmed = null)
        {
            if (!VerifyRequirements(player)) return;

            if (!ConfirmedOutcomeHolds(player, confirmed)) return;

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

                // and what the player was told still holds (the level can pass enl_keeps_level_up_to while the prompt is open)
                if (!ConfirmedOutcomeHolds(player, confirmed)) return;

                if (!SpendLuminance(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to enlighten!", ChatMessageType.Broadcast));
                    return;
                }
                var targetEnlightenment = player.Enlightenment + 1;
                RemoveTierConsumable(player, targetEnlightenment);

                // Enlightenment can proceed. Decided on the Enlightenment the character has now, before AddPerks raises it.
                var resetLevelAndSkills = ResetsLevelAndSkills(player.Enlightenment);
                var retainLevel = !resetLevelAndSkills && player.LevelRetentionPending;

                DequipAllItems(player);
                RemoveFromFellowships(player);
                player.ThreadSafeTeleportOnDeath();
                RemoveSociety(player);

                // Below enl_keeps_level_and_skills_from level and skills reset together: RemoveSkills rebuilds the skill
                // credit pool for a level 1 character (heritage + quest credits), so resetting skills alone would drop every
                // credit earned from levels. From it on skills, specializations and credits stay; the level stays (up to
                // enl_keeps_level_up_to) only after a Level Retention Stone, otherwise it returns to 1. Either way the credits
                // of the levels given up are marked as given, so leveling back up does not give them again.
                if (resetLevelAndSkills)
                {
                    RemoveSkills(player);
                    RemoveLevel(player);

                    // a stone used before the reset was forced on (enl_resets_level_and_skills, or a raised threshold) did
                    // nothing here, so its mark waits for the next enlightenment that keeps skills
                    if (player.LevelRetentionPending)
                        player.SendMessage("Your Level Retention Stone could not keep your level this time. It stays ready for your next enlightenment.", ChatMessageType.Broadcast);
                }
                else
                {
                    if (retainLevel)
                        RetainLevel(player);
                    else
                        ResetLevelKeepSkills(player);

                    // a stone covers one enlightenment: the next one needs another stone
                    player.LevelRetentionPending = false;
                }

                RemoveAllSpells(player);
                AddPerks(player, resetLevelAndSkills);
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
        /// Enlightening without the reset after a Level Retention Stone keeps the character's level up to enl_keeps_level_up_to
        /// (0 = any level). Above it the level drops to it and total experience to that level's. Skills, skill credits and
        /// unspent experience stay, and the credits of the levels given up are marked as given, so leveling back up does not
        /// give them again.
        /// </summary>
        private static void RetainLevel(Player player)
        {
            var level = player.Level ?? 1;
            var keptLevel = KeptLevel(level, ServerConfig.enl_keeps_level_up_to.Value);

            if (keptLevel >= level)
            {
                player.SendMessage("Your Level Retention Stone kept your level. You keep your skills and skill credits.", ChatMessageType.Broadcast);
                return;
            }

            LowerLevel(player, keptLevel);

            player.SendMessage($"Your Level Retention Stone kept your level up to {keptLevel}: your level has been lowered to {keptLevel}. You keep your skills and skill credits.", ChatMessageType.Broadcast);
        }

        /// <summary>
        /// Enlightening without the reset and without a Level Retention Stone: the level returns to 1, while skills,
        /// specializations, skill credits and unspent experience stay. The credits of the levels given up are marked as
        /// given, so leveling back up does not give them again.
        /// </summary>
        private static void ResetLevelKeepSkills(Player player)
        {
            LowerLevel(player, 1);

            player.SendMessage("Your level has returned to 1. You keep your skills and skill credits.", ChatMessageType.Broadcast);
        }

        /// <summary>
        /// Lowers the level to newLevel and total experience to that level's, without touching skills or the credit pool. The
        /// level-up credits up to the old level stay in the pool, so they are recorded as given (SkillCreditsGivenThroughLevel).
        /// </summary>
        private static void LowerLevel(Player player, int newLevel)
        {
            var level = player.Level ?? 1;

            if (newLevel >= level)
                return;

            player.SkillCreditsGivenThroughLevel = Math.Max(player.SkillCreditsGivenThroughLevel ?? 0, level);

            var totalXp = TotalExperienceForLevel(newLevel);
            player.TotalExperience = (long)Math.Min(totalXp, long.MaxValue);
            player.TotalExperienceDouble = totalXp;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.TotalExperience, player.TotalExperience ?? 0));

            player.Level = newLevel;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Level, newLevel));
        }

        /// <summary>
        /// The level a character keeps when it enlightens with a Level Retention Stone: its own level, at most keepUpTo
        /// (0 or less = no limit)
        /// </summary>
        public static int KeptLevel(int level, long keepUpTo)
        {
            return keepUpTo > 0 && level > keepUpTo ? (int)keepUpTo : level;
        }

        /// <summary>
        /// What enlightening will do to a character's level and skills: the full reset, or no reset with the level kept
        /// (LevelDropsTo null) or lowered (LevelDropsTo: enl_keeps_level_up_to with a Level Retention Stone, 1 without one).
        /// The /enlighten prompt promises one, and the ritual checks it still holds before spending anything.
        /// </summary>
        public readonly record struct Outcome(bool ResetsLevelAndSkills, int? LevelDropsTo);

        /// <summary>The outcome with the settings passed in.</summary>
        /// <param name="levelRetained">True when the character used a Level Retention Stone since it last enlightened.</param>
        public static Outcome PlannedOutcome(int currentEnlightenment, int level, bool alwaysReset, long keepFrom, long keepUpTo, bool levelRetained)
        {
            if (ResetsLevelAndSkills(currentEnlightenment, alwaysReset, keepFrom))
                return new Outcome(true, null);

            var newLevel = levelRetained ? KeptLevel(level, keepUpTo) : 1;

            return new Outcome(false, newLevel < level ? (int?)newLevel : null);
        }

        /// <summary>The outcome for this character now, with the live settings.</summary>
        public static Outcome PlannedOutcome(Player player)
        {
            return PlannedOutcome(player.Enlightenment, player.Level ?? 1, ServerConfig.enl_resets_level_and_skills.Value,
                ServerConfig.enl_keeps_level_and_skills_from.Value, ServerConfig.enl_keeps_level_up_to.Value, player.LevelRetentionPending);
        }

        /// <summary>The Level Retention Stone: stackable, Bonded and Sticky Attuned, used up on use (Gem.ActOnUse).</summary>
        public const uint LevelRetentionStoneWcid = 98760530;

        /// <summary>
        /// Why a character can't use a Level Retention Stone now, or null when it can: the stone only matters to characters who
        /// keep their skills when they enlighten (enl_keeps_level_and_skills_from, 50 by default), and one stone covers the
        /// next enlightenment.
        /// </summary>
        public static string LevelRetentionStoneRefusal(int currentEnlightenment, bool alwaysReset, long keepFrom, bool alreadyRetained)
        {
            if (alwaysReset)
                return "Enlightening resets your level and skills on this world right now, so the stone would do nothing.";

            if (ResetsLevelAndSkills(currentEnlightenment, alwaysReset, keepFrom))
                return $"You must be at Enlightenment {keepFrom} or higher to use a Level Retention Stone.";

            if (alreadyRetained)
                return "Your next enlightenment already keeps your level.";

            return null;
        }

        private static string LevelRetentionStoneRefusal(Player player)
        {
            return LevelRetentionStoneRefusal(player.Enlightenment, ServerConfig.enl_resets_level_and_skills.Value,
                ServerConfig.enl_keeps_level_and_skills_from.Value, player.LevelRetentionPending);
        }

        /// <summary>
        /// Using a Level Retention Stone: after a Yes, one stone is used up and the character's next enlightenment keeps its level
        /// (up to enl_keeps_level_up_to) instead of returning it to 1. The mark (PropertyInt.LevelRetentionPending) is removed by
        /// that enlightenment, unless it was a full reset the stone could not help with.
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
            var msg = $"Use a Level Retention Stone? Your next enlightenment will keep your level{upTo} instead of returning it to 1. The stone will be used up.";

            void onResponse(bool response, bool _)
            {
                if (!response)
                    return;

                // checked again: the character may have enlightened (an NPC can enlighten without a box), or the settings
                // changed, while the box was open
                var refusedNow = LevelRetentionStoneRefusal(player);
                if (refusedNow != null)
                {
                    player.SendTransientError(refusedNow);
                    return;
                }

                // TryConsumeFromInventoryWithNetworking(wcid, ...) reports success when there is nothing to consume: count first
                if (player.GetNumInventoryItemsOfWCID(LevelRetentionStoneWcid) < 1 || !player.TryConsumeFromInventoryWithNetworking(LevelRetentionStoneWcid, 1))
                {
                    player.SendTransientError("You no longer have a Level Retention Stone.");
                    return;
                }

                player.LevelRetentionPending = true;

                var keepUpToNow = ServerConfig.enl_keeps_level_up_to.Value;
                player.SendMessage($"Your next enlightenment will keep your level{(keepUpToNow > 0 ? $" (up to level {keepUpToNow})" : "")}.", ChatMessageType.Broadcast);
            }

            if (!player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, onResponse), msg))
                player.SendWeenieError(WeenieError.ConfirmationInProgress);
        }

        /// <summary>
        /// False, with a message, when what the player confirmed no longer holds: the character's level passed
        /// enl_keeps_level_up_to, a Level Retention Stone was used or its mark is gone, or an enlightenment setting changed.
        /// Called before anything is spent.
        /// </summary>
        private static bool ConfirmedOutcomeHolds(Player player, Outcome? confirmed)
        {
            if (!confirmed.HasValue || PlannedOutcome(player) == confirmed.Value)
                return true;

            player.SendMessage("What enlightening would do to your level and skills has changed since you confirmed. Nothing was spent. Use /enlighten again to see what it will do now.", ChatMessageType.Broadcast);
            return false;
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

            // the pool no longer holds any level credits: leveling up gives them all again
            player.SkillCreditsGivenThroughLevel = null;

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
        }


        /// <summary>
        /// Whether enlightening now resets level and skills: always while enl_resets_level_and_skills is TRUE, otherwise
        /// only for characters below enl_keeps_level_and_skills_from (50 by default). From that Enlightenment on, the
        /// character keeps its skills, specializations and skill credits, and its level only after a Level Retention Stone.
        /// </summary>
        /// <param name="currentEnlightenment">The character's Enlightenment before this ritual.</param>
        public static bool ResetsLevelAndSkills(int currentEnlightenment)
        {
            return ResetsLevelAndSkills(currentEnlightenment, ServerConfig.enl_resets_level_and_skills.Value, ServerConfig.enl_keeps_level_and_skills_from.Value);
        }

        /// <summary>The rule behind <see cref="ResetsLevelAndSkills(int)"/>, with both settings passed in.</summary>
        public static bool ResetsLevelAndSkills(int currentEnlightenment, bool alwaysReset, long keepFrom)
        {
            return alwaysReset || currentEnlightenment < keepFrom;
        }

        /// <summary>
        /// True for the one enlightenment that brings a character to keepFrom: every enlightenment after it keeps level
        /// and skills, so this is when the character is told.
        /// </summary>
        /// <param name="newEnlightenment">The character's Enlightenment after this ritual.</param>
        public static bool UnlocksKeepingLevelAndSkills(int newEnlightenment, bool alwaysReset, long keepFrom)
        {
            return !alwaysReset && newEnlightenment == keepFrom;
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

        private static void AddPerks(Player player, bool levelAndSkillsReset)
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
            if (levelAndSkillsReset)
                player.SendMessage("Your available skill credits have been adjusted.", ChatMessageType.Broadcast);
            player.SendMessage("You have risen to a higher tier of enlightenment!", ChatMessageType.Broadcast);
            if (UnlocksKeepingLevelAndSkills(player.Enlightenment, ServerConfig.enl_resets_level_and_skills.Value, ServerConfig.enl_keeps_level_and_skills_from.Value))
                player.SendMessage(ServerConfig.enl_keeps_level_up_to.Value > 0
                    ? $"From now on, enlightening will keep your skills. Use a Level Retention Stone before you enlighten to keep your level too, up to level {ServerConfig.enl_keeps_level_up_to.Value}."
                    : "From now on, enlightening will keep your skills. Use a Level Retention Stone before you enlighten to keep your level too.", ChatMessageType.Broadcast);

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
