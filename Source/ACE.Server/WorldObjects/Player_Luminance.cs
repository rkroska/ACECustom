using System;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// Applies luminance modifiers before adding luminance
        /// </summary>
        public void EarnLuminance(long amount, XpType xpType, ShareType shareType = ShareType.All)
        {
            if (IsOlthoiPlayer || IsMule)
                return;

            // following the same model as Player_Xp
            var questModifier = PropertyManager.GetDouble("quest_lum_modifier").Item;
            var modifier = PropertyManager.GetDouble("luminance_modifier").Item;
            if (xpType == XpType.Quest)
                modifier *= questModifier;
            var quest = GetQuestCountXPBonus();

            var enlightenment = GetEnglightenmentXPBonus();
            // should this be passed upstream to fellowship?
            var enchantment = GetXPAndLuminanceModifier(xpType);

            var m_amount = (long)Math.Round(amount * quest * enlightenment * enchantment * modifier);

            GrantLuminance(m_amount, xpType, shareType);
        }

        /// <summary>
        /// Directly grants luminance to the player, without any additional luminance modifiers
        /// </summary>
        public void GrantLuminance(long amount, XpType xpType, ShareType shareType = ShareType.All)
        {
            if (IsOlthoiPlayer)
                return;

            if (Fellowship != null && Fellowship.ShareXP && shareType.HasFlag(ShareType.Fellowship))
            {
                // this will divy up the luminance, and re-call this function
                // with ShareType.Fellowship removed
                Fellowship.SplitLuminance((ulong)amount, xpType, shareType, this);
            }
            else
                AddLuminance(amount, xpType);
        }

        private void AddLuminance(long amount, XpType xpType)
        {
            BankedLuminance += amount;
            if (xpType == XpType.Quest || xpType == XpType.Kill)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You've banked {amount:N0} Luminance.", ChatMessageType.Broadcast));

            UpdateLumAllegiance(amount);

            UpdateLuminance();
        }

        /// <summary>
        /// Spends the amount of luminance specified, deducting it from available luminance
        /// </summary>
        public bool SpendLuminance(long amount)
        {

            if (!BankedLuminance.HasValue) { BankedLuminance = 0; }
            if (!AvailableLuminance.HasValue) { AvailableLuminance = 0; }
            var available = AvailableLuminance + BankedLuminance;

            if (amount > available)
                return false;

            if (BankedLuminance > 0 && BankedLuminance > amount)
            {
                BankedLuminance = BankedLuminance - amount;
            }
            else if (amount > BankedLuminance)
            {
                amount = amount - (long)BankedLuminance;
                BankedLuminance = 0;
            }
            if (amount > 0)
            {
                AvailableLuminance = available - amount;
            }
            

            UpdateLuminance();

            return true;
        }

        private void UpdateLumAllegiance(long amount)
        {
            if (!HasAllegiance)
            {
                return;
            }
            if (amount <= 0)
            {
                return;
            }

            AllegianceManager.PassXP(AllegianceNode, (ulong)amount, true, true);
        }

        /// <summary>
        /// Sends network message to update luminance
        /// </summary>
        private void UpdateLuminance()
        {
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, BankedLuminance ?? 0));
        }
    }
}
