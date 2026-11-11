using System;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public class GenericObject : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public GenericObject(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public GenericObject(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            //StackSize = null;
            //StackUnitEncumbrance = null;
            //StackUnitValue = null;
            //MaxStackSize = null;

            // Linkable Item Generator (linkitemgen2minutes) fix
            if (WeenieClassId == 4142)
            {
                MaxGeneratedObjects = 0;
                InitGeneratedObjects = 0;
            }
        }

        /// <summary>
        /// True for a lum token usable from the pack (the Bounty Reward Tokens and the event lum tokens): a use banks it through
        /// Player.UseLumToken. The one rule both RunsUseEmote and ActOnUse go by, so a token is always paid exactly once.
        /// </summary>
        private bool PaysAsLumTokenOnUse => ItemUseable == Usable.Contained && Player.IsLumToken(WeenieClassId);

        /// <summary>
        /// EVENT LUM TOKENS (2026-11-10): an event lum token's value is in its own Use emote, but ActOnUse pays it through
        /// Player.UseLumToken (bank lock, economy ledger, immediate save), so that emote must not run as well. Everything else
        /// runs its Use emote as before. The emote is skipped even if its value can't be read (UseLumToken then says the token
        /// can't be used): an emote without the BankedLuminance step would only delete the token without paying.
        /// </summary>
        /// <param name="activator">Who used the item.</param>
        /// <returns>False only for an event lum token that ActOnUse pays.</returns>
        protected override bool RunsUseEmote(WorldObject activator)
            => !(PaysAsLumTokenOnUse && Player.IsOwnUseEmoteLumToken(WeenieClassId));

        /// <summary>
        /// A player uses a generic item (WorldObject.OnActivate calls this after starting the item's Use emote, if it runs one):
        /// a bankable lum token usable from the pack goes to the bank, anything else just plays its use sound.
        /// </summary>
        /// <param name="activator">Who used the item; only players do anything here.</param>
        public override void ActOnUse(WorldObject activator)
        {
            if (!(activator is Player player))
                return;

            // BOUNTY REWARD TOKENS (owner 2026-11-02) and EVENT LUM TOKENS (2026-11-10): a lum token usable from the pack goes to
            // the bank, as /b d does for one. An event lum token's own Use emote is not run on that use (RunsUseEmote), so this
            // is the only payment.
            if (PaysAsLumTokenOnUse)
            {
                player.UseLumToken(this);
                return;
            }

            if (UseSound > 0)
                player.Session.Network.EnqueueSend(new GameMessageSound(player.Guid, UseSound));
        }
    }
}
