using System;

using ACE.Entity;

using CellDatIndex = ACE.Server.Physics.Common.CellDatIndex;

namespace ACE.Server.Network.GameAction.Actions
{
    public static class GameActionAutonomousPosition
    {
        /// <summary>
        /// Sent every ~1 second by the client when a player is moving,
        /// with the latest position from the client
        /// </summary>
        [GameAction(GameActionType.AutonomousPosition)]
        public static void Handle(ClientMessage message, Session session)
        {
            //Console.WriteLine($"{session.Player.Name}.AutoPos");

            var position = new Position(message.Payload, session.Player.Location.Variation);
            //Console.WriteLine($"Client sent position: {position}");

            var instanceTimestamp = message.Payload.ReadUInt16();
            var serverControlTimestamp = message.Payload.ReadUInt16();
            var teleportTimestamp = message.Payload.ReadUInt16();
            var forcePositionTimestamp = message.Payload.ReadUInt16();

            session.Player.LastContact = message.Payload.ReadByte() != 0;   // TRUE if player is currently on ground

            // LastGroundPos is where the z-pos check in UpdatePosition puts the player back, so a position in a
            // landblock that doesn't exist must never be stored here (ValidateMovement refuses it as a move).
            // (not logged here: the same position reaches ValidateMovement as a move, which logs who sent it)
            if (session.Player.LastContact && CellDatIndex.LandblockExists(position.Cell))
                session.Player.LastGroundPos = position;

            if (!session.Player.Teleporting)
                session.Player.SetRequestedLocation(position);

            message.Payload.Align();
        }
    }
}
