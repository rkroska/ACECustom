namespace ACE.Entity.Enum
{
    public enum CloakStatus
    {
        Undef,
        Off,
        On,
        Player,
        Creature,
        Hybrid,

        /// <summary>
        /// Like On to everyone else - invisible, ethereal, walks through doors - but YOUR OWN client still draws
        /// you, so you can see where you are (owner 2026-09-22; full opacity - see Player.SendSelf). Appended on purpose: this value
        /// is stored in PropertyInt.CloakStatus, so inserting it would renumber Player, Creature and Hybrid on
        /// every character that has one saved.
        /// </summary>
        Ghost
    }
}
