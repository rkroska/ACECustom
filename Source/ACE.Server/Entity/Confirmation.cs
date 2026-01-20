using System;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public abstract class Confirmation(ObjectGuid playerGuid, ConfirmationType confirmationType)
    {
        public ObjectGuid PlayerGuid = playerGuid;

        public ConfirmationType ConfirmationType = confirmationType;

        public uint ContextId;

        public virtual void ProcessConfirmation(bool response, bool timeout = false)
        {
            // empty base
        }

        public Player Player => PlayerManager.GetOnlinePlayer(PlayerGuid);
    }

    public class Confirmation_AlterAttribute(ObjectGuid playerGuid, ObjectGuid attributeTransferDevice) : Confirmation(playerGuid, ConfirmationType.AlterAttribute)
    {
        public ObjectGuid AttributeTransferDevice = attributeTransferDevice;

        public override void ProcessConfirmation(bool response, bool timeout = false)
        {
            if (!response) return;

            var player = Player;
            if (player == null) return;

            var attributeTransferDevice = player.FindObject(AttributeTransferDevice.Full, Player.SearchLocations.MyInventory) as AttributeTransferDevice;

            attributeTransferDevice?.ActOnUse(player, true);
        }
    }

    public class Confirmation_AlterSkill(ObjectGuid playerGuid, ObjectGuid skillAlterationDevice) : Confirmation(playerGuid, ConfirmationType.AlterSkill)
    {
        public ObjectGuid SkillAlterationDevice = skillAlterationDevice;

        public override void ProcessConfirmation(bool response, bool timeout = false)
        {
            if (!response) return;

            var player = Player;
            if (player == null) return;

            var skillAlterationDevice = player.FindObject(SkillAlterationDevice.Full, Player.SearchLocations.MyInventory) as SkillAlterationDevice;

            skillAlterationDevice?.ActOnUse(player, true);
        }
    }

    public class Confirmation_Augmentation(ObjectGuid playerGuid, ObjectGuid augmentationGuid) : Confirmation(playerGuid, ConfirmationType.Augmentation)
    {
        public ObjectGuid AugmentationGuid = augmentationGuid;

        public override void ProcessConfirmation(bool response, bool timeout = false)
        {
            if (!response) return;

            var player = Player;
            if (player == null) return;

            var augmentation = player.FindObject(AugmentationGuid.Full, Player.SearchLocations.MyInventory) as AugmentationDevice;

            augmentation?.ActOnUse(player, true);
        }
    }

    public class Confirmation_CraftInteraction(ObjectGuid playerGuid, ObjectGuid sourceGuid, ObjectGuid targetGuid) : Confirmation(playerGuid, ConfirmationType.CraftInteraction)
    {
        public ObjectGuid SourceGuid = sourceGuid;
        public ObjectGuid TargetGuid = targetGuid;

        public bool Tinkering;

        public override void ProcessConfirmation(bool response, bool timeout = false)
        {
            var player = Player;
            if (player == null) return;

            if (!response)
            {
                player.SendWeenieError(WeenieError.YouChickenOut);
                return;
            }

            // inventory only?
            var source = player.FindObject(SourceGuid.Full, Player.SearchLocations.LocationsICanMove);
            var target = player.FindObject(TargetGuid.Full, Player.SearchLocations.LocationsICanMove);

            if (source == null || target == null) return;

            RecipeManager.UseObjectOnTarget(player, source, target, true);
        }
    }

    public class Confirmation_Fellowship(ObjectGuid inviterGuid, ObjectGuid invitedGuid) : Confirmation(invitedGuid, ConfirmationType.Fellowship)
    {
        public ObjectGuid InviterGuid = inviterGuid;

        public override void ProcessConfirmation(bool response, bool timeout = false)
        {
            var player = Player;
            if (player == null) return;

            var inviter = PlayerManager.GetOnlinePlayer(InviterGuid);

            if (!response)
            {
                inviter?.SendMessage($"{player.Name} {(timeout ? "did not respond to" : "has declined")} your offer of fellowship.");
                return;
            }

            if (player != null && inviter != null && inviter.Fellowship != null)
                inviter.Fellowship.AddConfirmedMember(inviter, player, response);
        }
    }

    public class Confirmation_SwearAllegiance(ObjectGuid patronGuid, ObjectGuid vassalGuid) : Confirmation(patronGuid, ConfirmationType.SwearAllegiance)
    {
        public ObjectGuid VassalGuid = vassalGuid;

        public override void ProcessConfirmation(bool response, bool timeout = false)
        {
            var player = Player;
            if (player == null) return;

            var vassal = PlayerManager.GetOnlinePlayer(VassalGuid);

            if (!response)
            {
                vassal?.SendMessage($"{player.Name} {(timeout ? "did not respond to" : "has declined")} your offer of allegiance.");
                return;
            }

            vassal?.SwearAllegiance(player.Guid.Full, true, true);
        }
    }

    public class Confirmation_YesNo(ObjectGuid sourceGuid, ObjectGuid targetPlayerGuid, string quest) : Confirmation(targetPlayerGuid, ConfirmationType.Yes_No)
    {
        public ObjectGuid SourceGuid = sourceGuid;

        public string Quest = quest;

        public override void ProcessConfirmation(bool response, bool timeout = false)
        {
            var player = Player;
            if (player == null) return;

            var source = player.FindObject(SourceGuid.Full, Player.SearchLocations.Landblock);

            if (source is Hook hook && hook.Item != null)
                source = hook.Item;

            source?.EmoteManager.ExecuteEmoteSet(response ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, Quest, player);
        }
    }

    public class Confirmation_Custom(ObjectGuid playerGuid, Action action) : Confirmation(playerGuid, ConfirmationType.Yes_No)
    {
        public Action Action = action;

        public override void ProcessConfirmation(bool response, bool timeout = false)
        {
            if (!response) return;

            var player = Player;
            if (player == null) return;

            Action();
        }
    }
}
