using System;

namespace ACE.Entity.Enum
{
    /// <summary>
    /// Determines optional AI abilities
    /// </summary>
    [Flags]
    public enum AiOption
    {
        None         = 0,
        CanOpenDoors = 1
    };
}
