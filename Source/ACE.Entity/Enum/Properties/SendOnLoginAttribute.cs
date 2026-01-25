using System;

namespace ACE.Entity.Enum.Properties
{
    /// <summary>
    /// These are properties that are sent in the Player Description Event
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SendOnLoginAttribute : Attribute
    {
    }
}
