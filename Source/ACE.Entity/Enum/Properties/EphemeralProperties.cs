using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ACE.Entity.Enum.Properties
{
    /// <summary>
    /// Static selection of client enums that are [Ephemeral]<para />
    /// These are properties that aren't saved to the shard.
    /// </summary>
    public static class EphemeralProperties
    {
        /// <summary>
        /// Method to return a list of enums by attribute type - in this case [Ephemeral] using generics to enhance code reuse.
        /// </summary>
        /// <typeparam name="T">Enum to list by [Ephemeral]</typeparam>
        private static HashSet<T> GetValues<T>()
        {
            HashSet<T> results = [];
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetCustomAttribute<EphemeralAttribute>() == null) continue;
                if (field.GetValue(null) is T result) results.Add(result);
            }
            return results;
        }

        /// <summary>
        /// returns a list of values for PropertyInt that are [Ephemeral]
        /// </summary
        public static readonly HashSet<PropertyInt> PropertiesInt = GetValues<PropertyInt>();

        /// <summary>
        /// returns a list of values for PropertyInt64 that are [Ephemeral]
        /// </summary>
        public static readonly HashSet<PropertyInt64> PropertiesInt64 = GetValues<PropertyInt64>();

        /// <summary>
        /// returns a list of values for PropertyBool that are [Ephemeral]
        /// </summary>
        public static readonly HashSet<PropertyBool> PropertiesBool = GetValues<PropertyBool>();

        /// <summary>
        /// returns a list of values for PropertyString that are [Ephemeral]
        /// </summary>
        public static readonly HashSet<PropertyString> PropertiesString = GetValues<PropertyString>();

        /// <summary>
        /// returns a list of values for PropertyFloat that are [Ephemeral]
        /// </summary>
        public static readonly HashSet<PropertyFloat> PropertiesDouble = GetValues<PropertyFloat>();

        /// <summary>
        /// returns a list of values for PropertyDataId that are [Ephemeral]
        /// </summary>
        public static readonly HashSet<PropertyDataId> PropertiesDataId = GetValues<PropertyDataId>();

        /// <summary>
        /// returns a list of values for PropertyInstanceId that are [Ephemeral]
        /// </summary>
        public static readonly HashSet<PropertyInstanceId> PropertiesInstanceId = GetValues<PropertyInstanceId>();


        /// <summary>
        /// returns a list of values for PositionType that are [Ephemeral]
        /// </summary>
        public static readonly HashSet<PositionType> PositionTypes = GetValues<PositionType>();
    }
}
