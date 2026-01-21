using System;

namespace ACE.Entity.Enum.Properties
{
    /// <summary>
    /// These are properties that aren't saved to the shard.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EphemeralAttribute : Attribute
    {
    }
}
