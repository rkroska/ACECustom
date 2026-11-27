using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// SKILL CREDIT GEMS (owner 2026-11-21). Sold by the Ancient Statue of unknown origin (300002) for 100 Enlightened Coins
    /// per credit (Database/Updates/World/2026-11-21-00-Skill-Credit-Gems.sql). Using one (Gem.ActOnUse) adds its credits to
    /// the character for good: available and total credits, and SkillCreditsFromGems, which Enlightenment.RemoveSkills adds
    /// back when it rebuilds the skill credit pool and verify-skill-credits counts.
    /// </summary>
    public partial class Player
    {
        /// <summary>The Skill Credit Gems and the skill credits each one gives</summary>
        public static readonly IReadOnlyDictionary<uint, int> SkillCreditGems = new Dictionary<uint, int>
        {
            { 98760540, 1 },    // Skill Credit Gem
            { 98760541, 10 },   // 10x Skill Credit Gem
            { 98760542, 50 },   // 50x Skill Credit Gem
            { 98760543, 100 },  // 100x Skill Credit Gem
        };

        /// <summary>
        /// The skill credits this character has from Skill Credit Gems (PropertyInt.SkillCreditsFromGems, server-only).
        /// Enlightenment.RemoveSkills adds them back to the rebuilt pool, so they are kept through enlightenment.
        /// </summary>
        public int SkillCreditsFromGems
        {
            get => Math.Max(0, GetProperty(PropertyInt.SkillCreditsFromGems) ?? 0);
            set { if (value <= 0) RemoveProperty(PropertyInt.SkillCreditsFromGems); else SetProperty(PropertyInt.SkillCreditsFromGems, value); }
        }

        /// <summary>The gem count after adding credits, at most int.MaxValue</summary>
        public static int AddSkillCreditsFromGems(int current, int credits)
        {
            return (int)Math.Min((long)Math.Max(0, current) + Math.Max(0, credits), int.MaxValue);
        }

        /// <summary>
        /// Using a Skill Credit Gem: the gem is used up first, then its credits are added to the available and total skill
        /// credits (AddSkillCredits) and to SkillCreditsFromGems, so they stay with the character through enlightenment.
        /// </summary>
        public void UseSkillCreditGem(Gem gem, int credits)
        {
            // only from the pack: HandleActionUseItem also finds objects on the ground (a gem an admin made with @create)
            if (FindObject(gem.Guid.Full, SearchLocations.MyInventory) == null)
            {
                SendTransientError($"The {gem.Name} must be in your pack to use it.");
                return;
            }

            if (!TryConsumeFromInventoryWithNetworking(gem, 1))
            {
                SendTransientError($"Unable to use the {gem.Name}.");
                return;
            }

            SkillCreditsFromGems = AddSkillCreditsFromGems(SkillCreditsFromGems, credits);
            AddSkillCredits(credits);

            SendMessage($"You gain {credits:N0} skill credit{(credits == 1 ? "" : "s")} for good: skill credits from gems stay with you through enlightenment.", ChatMessageType.Broadcast);
        }
    }
}
