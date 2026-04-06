using System;

using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Entity.Models
{
    public static class WeenieExtensions
    {
        // =====================================
        // Get
        // Bool, DID, Float, IID, Int, Int64, String, Position
        // =====================================

        public static bool? GetProperty(this Weenie weenie, PropertyBool property) => ((IWeenie)weenie).GetProperty(property);

        public static uint? GetProperty(this Weenie weenie, PropertyDataId property) => ((IWeenie)weenie).GetProperty(property);

        public static double? GetProperty(this Weenie weenie, PropertyFloat property) => ((IWeenie)weenie).GetProperty(property);

        public static uint? GetProperty(this Weenie weenie, PropertyInstanceId property) => ((IWeenie)weenie).GetProperty(property);

        public static int? GetProperty(this Weenie weenie, PropertyInt property) => ((IWeenie)weenie).GetProperty(property);

        public static long? GetProperty(this Weenie weenie, PropertyInt64 property) => ((IWeenie)weenie).GetProperty(property);

        public static string? GetProperty(this Weenie weenie, PropertyString property) => ((IWeenie)weenie).GetProperty(property);

        public static PropertiesPosition? GetProperty(this Weenie weenie, PositionType property) => ((IWeenie)weenie).GetProperty(property);

        public static Position? GetPosition(this Weenie weenie, PositionType property) => ((IWeenie)weenie).GetPosition(property);

        // =====================================
        // Utility
        // =====================================

        public static string? GetName(this Weenie weenie) => ((IWeenie)weenie).GetName();

        public static string? GetPluralName(this Weenie weenie) => ((IWeenie)weenie).GetPluralName();

        public static ItemType GetItemType(this Weenie weenie) => ((IWeenie)weenie).GetItemType();

        public static int? GetValue(this Weenie weenie) => ((IWeenie)weenie).GetValue();

        public static bool IsStackable(this Weenie weenie) => ((IWeenie)weenie).IsStackable();

        public static bool IsStuck(this Weenie weenie) => ((IWeenie)weenie).IsStuck();

        public static bool DisableCreate(this Weenie weenie) => ((IWeenie)weenie).DisableCreate();

        public static bool RequiresBackpackSlotOrIsContainer(this Weenie weenie) => ((IWeenie)weenie).RequiresBackpackSlotOrIsContainer();

        public static bool IsVendorService(this Weenie weenie) => ((IWeenie)weenie).IsVendorService();

        public static int GetStackUnitEncumbrance(this Weenie weenie) => ((IWeenie)weenie).GetStackUnitEncumbrance();

        public static int GetMaxStackSize(this Weenie weenie) => ((IWeenie)weenie).GetMaxStackSize();

        public static int? GetMaxStructure(this Weenie weenie) => ((IWeenie)weenie).GetMaxStructure();
    }
}
