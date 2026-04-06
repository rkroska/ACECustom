using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Entity.Models
{
    public static class BiotaSnapshotExtensions
    {
        /*
         *  ARCHITECTURAL NOTE: SNAPSHOT EXTENSIONS
         *  ----------------------------------------
         *  These methods are EXCLUSIVELY for use on Biota CLONES (Snapshots).
         *  They are intentionally designed to be lock-free because they operate
         *  on private, thread-isolated copies of character data.
         *  
         *  NEVER use these methods on a live WorldObject/Player instance.
         */

        public static string? GetName(this Biota biota) => ((IWeenie)biota).GetName();
        public static string? GetPluralName(this Biota biota) => ((IWeenie)biota).GetPluralName();
        public static ItemType GetItemType(this Biota biota) => ((IWeenie)biota).GetItemType();
        public static bool IsStackable(this Biota biota) => ((IWeenie)biota).IsStackable();
        public static bool RequiresBackpackSlotOrIsContainer(this Biota biota) => ((IWeenie)biota).RequiresBackpackSlotOrIsContainer();
    }
}
