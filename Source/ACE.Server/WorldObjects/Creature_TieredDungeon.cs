namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// Tiered dungeons (owner 2026-11-11): the arena room of a tier copy this monster belongs to, fixed the first time
        /// it is asked - at spawn, from its spawn position - so a monster that chases a player into the next room keeps
        /// its own room's health and damage. 0 = not asked yet, -1 = not in a tier copy room. Never saved.
        /// See TieredDungeonRegistry.RoomOf.
        /// </summary>
        public int TieredDungeonRoom;
    }
}
