using System;

using ACE.Entity.Enum;

namespace ACE.Entity
{
    [AttributeUsage(AttributeTargets.Field)]
    public class CharacterOptions1Attribute(CharacterOptions1 option) : Attribute
    {
        public CharacterOptions1 Option { get; } = option;
    }
}
