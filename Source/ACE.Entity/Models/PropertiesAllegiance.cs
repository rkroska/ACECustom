using System;

namespace ACE.Entity.Models
{
    public class PropertiesAllegiance
    {
        public required bool Banned { get; set; }
        public required bool ApprovedVassal { get; set; }

        public PropertiesAllegiance Clone()
        {
            return new PropertiesAllegiance
            {
                Banned = Banned,
                ApprovedVassal = ApprovedVassal
            };
        }
    }
}
