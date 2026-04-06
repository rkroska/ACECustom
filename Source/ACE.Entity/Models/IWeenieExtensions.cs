using System;
using System.Collections.Generic;

using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Entity.Models
{
    /// <summary>
    /// Unified logic for objects implementing IWeenie (Weenie and Biota).
    /// IMPORTANT: These extensions are LOCK-FREE. They perform direct property lookups.
    /// - Use these on static DAT data (Weenie).
    /// - Use these on thread-safe snapshots/clones (Biota.Clone).
    /// - DO NOT use these on live Biota objects without external synchronization.
    /// </summary>
    public static class IWeenieExtensions
    {
        // =====================================
        // Get
        // Bool, DID, Float, IID, Int, Int64, String, Position
        // =====================================

        public static bool? GetProperty(this IWeenie weenie, PropertyBool property)
        {
            if (weenie.PropertiesBool != null && weenie.PropertiesBool.TryGetValue(property, out var value))
                return value;

            return null;
        }

        public static uint? GetProperty(this IWeenie weenie, PropertyDataId property)
        {
            if (weenie.PropertiesDID != null && weenie.PropertiesDID.TryGetValue(property, out var value))
                return value;

            return null;
        }

        public static double? GetProperty(this IWeenie weenie, PropertyFloat property)
        {
            if (weenie.PropertiesFloat != null && weenie.PropertiesFloat.TryGetValue(property, out var value))
                return value;

            return null;
        }

        public static uint? GetProperty(this IWeenie weenie, PropertyInstanceId property)
        {
            if (weenie.PropertiesIID != null && weenie.PropertiesIID.TryGetValue(property, out var value))
                return value;

            return null;
        }

        public static int? GetProperty(this IWeenie weenie, PropertyInt property)
        {
            if (weenie.PropertiesInt != null && weenie.PropertiesInt.TryGetValue(property, out var value))
                return value;

            return null;
        }

        public static long? GetProperty(this IWeenie weenie, PropertyInt64 property)
        {
            if (weenie.PropertiesInt64 != null && weenie.PropertiesInt64.TryGetValue(property, out var value))
                return value;

            return null;
        }

        public static string? GetProperty(this IWeenie weenie, PropertyString property)
        {
            if (weenie.PropertiesString != null && weenie.PropertiesString.TryGetValue(property, out var value))
                return value;

            return null;
        }

        public static PropertiesPosition? GetProperty(this IWeenie weenie, PositionType property)
        {
            if (weenie.PropertiesPosition != null && weenie.PropertiesPosition.TryGetValue(property, out var value))
                return value;

            return null;
        }

        public static Position? GetPosition(this IWeenie weenie, PositionType property)
        {
            if (weenie.PropertiesPosition != null && weenie.PropertiesPosition.TryGetValue(property, out var value))
                return new Position(value.ObjCellId, value.PositionX, value.PositionY, value.PositionZ, value.RotationX, value.RotationY, value.RotationZ, value.RotationW, property == PositionType.RelativeDestination, value.VariationId);

            return null;
        }

        // =====================================
        // Utility Logic Helpers
        // =====================================

        public static string? GetName(this IWeenie weenie)
        {
            return weenie.GetProperty(PropertyString.Name);
        }

        public static string? GetPluralName(this IWeenie weenie)
        {
            var pluralName = weenie.GetProperty(PropertyString.PluralName);
            pluralName ??= weenie.GetProperty(PropertyString.Name)?.Pluralize();
            return pluralName;
        }

        public static ItemType GetItemType(this IWeenie weenie)
        {
            return (ItemType)(weenie.GetProperty(PropertyInt.ItemType) ?? 0);
        }

        public static int? GetValue(this IWeenie weenie)
        {
            return weenie.GetProperty(PropertyInt.Value);
        }

        public static bool IsStackable(this IWeenie weenie)
        {
            switch (weenie.WeenieType)
            {
                case WeenieType.Stackable:
                case WeenieType.Ammunition:
                case WeenieType.Coin:
                case WeenieType.CraftTool:
                case WeenieType.Food:
                case WeenieType.Gem:
                case WeenieType.Missile:
                case WeenieType.SpellComponent:
                    return true;
            }
            return false;
        }

        public static bool IsStuck(this IWeenie weenie)
        {
            return weenie.GetProperty(PropertyBool.Stuck) ?? false;
        }

        public static bool DisableCreate(this IWeenie weenie)
        {
            return weenie.GetProperty(PropertyBool.DisableCreate) ?? false;
        }

        public static bool RequiresBackpackSlotOrIsContainer(this IWeenie weenie)
        {
            return (weenie.GetProperty(PropertyBool.RequiresBackpackSlot) ?? false) || weenie.WeenieType == WeenieType.Container;
        }

        public static bool IsVendorService(this IWeenie weenie)
        {
            return weenie.GetProperty(PropertyBool.VendorService) ?? false;
        }

        public static int GetStackUnitEncumbrance(this IWeenie weenie)
        {
            if (weenie.IsStackable())
            {
                var stackUnitEncumbrance = weenie.GetProperty(PropertyInt.StackUnitEncumbrance);
                if (stackUnitEncumbrance != null)
                    return stackUnitEncumbrance.Value;
            }
            return weenie.GetProperty(PropertyInt.EncumbranceVal) ?? 0;
        }

        public static int GetMaxStackSize(this IWeenie weenie)
        {
            if (weenie.IsStackable())
            {
                var maxStackSize = weenie.GetProperty(PropertyInt.MaxStackSize);
                if (maxStackSize != null)
                    return maxStackSize.Value;
            }
            return 1;
        }

        public static int? GetMaxStructure(this IWeenie weenie)
        {
            return weenie.GetProperty(PropertyInt.MaxStructure);
        }
    }
}
