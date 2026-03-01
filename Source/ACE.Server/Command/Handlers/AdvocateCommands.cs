using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Common;
using ACE.Server.WorldObjects;
using System.Collections.Generic;

namespace ACE.Server.Command.Handlers
{
    public static class AdvocateCommands
    {
        // attackable { on | off }
        [CommandHandler("attackable", AccessLevel.Advocate, CommandHandlerFlag.RequiresWorld, 1,
            "Sets whether monsters will attack you or not.",
            "[ on | off ]\n"
            + "This command sets whether monsters will attack you unprovoked.\n When turned on, monsters will attack you as if you are a normal player.\n When turned off, monsters will ignore you.")]
        public static void HandleAttackable(Session session, params string[] parameters)
        {
            // usage: @attackable { on,off}
            // This command sets whether monsters will attack you unprovoked.When turned on, monsters will attack you as if you are a normal player.  When turned off, monsters will ignore you.
            // @attackable - Sets whether monsters will attack you or not.

            if (session.Player.IsAdvocate && session.Player.AdvocateLevel < 5)
                return;

            var param = parameters[0];

            switch (param)
            {
                case "off":
                    session.Player.UpdateProperty(session.Player, PropertyBool.Attackable, false, true);
                    session.Network.EnqueueSend(new GameMessageSystemChat("You can no longer be attacked.", ChatMessageType.Broadcast));
                    break;
                case "on":
                default:
                    session.Player.UpdateProperty(session.Player, PropertyBool.Attackable, true, true);
                    session.Network.EnqueueSend(new GameMessageSystemChat("You can now be attacked.", ChatMessageType.Broadcast));
                    break;
            }
        }

        // bestow <name> <level>
        [CommandHandler("bestow", AccessLevel.Advocate, CommandHandlerFlag.RequiresWorld, 2,
            "Sets a character's Advocate Level.",
            "<name> <level>\nAdvocates can bestow any level less than their own.")]
        public static void HandleBestow(Session session, params string[] parameters)
        {
            var charName = string.Join(" ", parameters).Trim();

            var level = parameters[parameters.Length - 1];

            if (!int.TryParse(level, out var advocateLevel) || advocateLevel < 1 || advocateLevel > 7)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{level} is not a valid advocate level.", ChatMessageType.Broadcast));
                return;
            }

            var advocateName = charName.TrimEnd((" " + level).ToCharArray());

            var playerToFind = PlayerManager.FindByName(advocateName);

            if (playerToFind != null)
            {
                if (playerToFind is Player player)
                {
                    //if (!Advocate.IsAdvocate(player))
                    //{
                    //    session.Network.EnqueueSend(new GameMessageSystemChat($"{playerToFind.Name} is not an Advocate.", ChatMessageType.Broadcast));
                    //    return;
                    //}

                    if (player.IsPK || ServerConfig.pk_server.Value)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"{playerToFind.Name} in a Player Killer and cannot be an Advocate.", ChatMessageType.Broadcast));
                        return;
                    }

                    if (session.Player.AdvocateLevel <= player.AdvocateLevel)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot change {playerToFind.Name}'s Advocate status because they are equal to or out rank you.", ChatMessageType.Broadcast));
                        return;
                    }

                    if (advocateLevel >= session.Player.AdvocateLevel && !session.Player.IsAdmin)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot bestow {playerToFind.Name}'s Advocate rank to {advocateLevel} because that is equal to or higher than your rank.", ChatMessageType.Broadcast));
                        return;
                    }

                    if (advocateLevel == player.AdvocateLevel)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"{playerToFind.Name}'s Advocate rank is already at level {advocateLevel}.", ChatMessageType.Broadcast));
                        return;
                    }

                    if (!Advocate.CanAcceptAdvocateItems(player, advocateLevel))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot change {playerToFind.Name}'s Advocate status because they do not have capacity for the advocate items.", ChatMessageType.Broadcast));
                        return;
                    }

                    if (Advocate.Bestow(player, advocateLevel))
                        session.Network.EnqueueSend(new GameMessageSystemChat($"{playerToFind.Name} is now an Advocate, level {advocateLevel}.", ChatMessageType.Broadcast));
                    else
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Advocate bestowal of {playerToFind.Name} failed.", ChatMessageType.Broadcast));
                }
                else
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{playerToFind.Name} is not online. Cannot complete bestowal process.", ChatMessageType.Broadcast));
            }
            else
                session.Network.EnqueueSend(new GameMessageSystemChat($"{advocateName} was not found in the database.", ChatMessageType.Broadcast));
        }

        // remove <name>
        [CommandHandler("remove", AccessLevel.Advocate, CommandHandlerFlag.RequiresWorld, 1,
            "Removes the specified character from the Advocate ranks.",
            "<character name>\nAdvocates can remove Advocate status for any Advocate of lower level than their own.")]
        public static void HandleRemove(Session session, params string[] parameters)
        {
            var charName = string.Join(" ", parameters).Trim();

            var playerToFind = PlayerManager.FindByName(charName);

            if (playerToFind != null)
            {
                if (playerToFind is Player player)
                {
                    if (!Advocate.IsAdvocate(player))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"{playerToFind.Name} is not an Advocate.", ChatMessageType.Broadcast));
                        return;
                    }

                    if (session.Player.AdvocateLevel < player.AdvocateLevel)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot remove {playerToFind.Name}'s Advocate status because they out rank you.", ChatMessageType.Broadcast));
                        return;
                    }

                    if (Advocate.Remove(player))
                        session.Network.EnqueueSend(new GameMessageSystemChat($"{playerToFind.Name} is no longer an Advocate.", ChatMessageType.Broadcast));
                    else
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Advocate removal of {playerToFind.Name} failed.", ChatMessageType.Broadcast));
                }
                else
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{playerToFind.Name} is not online. Cannot complete removal process.", ChatMessageType.Broadcast));
            }
            else
                session.Network.EnqueueSend(new GameMessageSystemChat($"{charName} was not found in the database.", ChatMessageType.Broadcast));
        }


    }
}
