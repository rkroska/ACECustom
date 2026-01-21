using System.Collections.Generic;
using System.Reflection;

namespace ACE.Entity.Enum.Properties
{
    /// <summary>
    /// Static selection of client enums that are [SendOnLogin]<para />
    /// These are properties that are sent in the Player Description Event
    /// </summary>
    public static class SendOnLoginProperties
    {
        /// <summary>
        /// Method to return a list of enums by attribute type - in this case [SendOnLogin] using generics to enhance code reuse.
        /// </summary>
        /// <typeparam name="T">Enum to list by [SendOnLogin]</typeparam>
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
        /// returns a list of values for PropertyInt that are NOT [SendOnLogin]
        /// </summary>
        public static readonly HashSet<PropertyInt> PropertiesInt = GetValues<PropertyInt>();

        /// <summary>
        /// returns a list of values for PropertyInt that are NOT [SendOnLogin]
        /// </summary>
        public static readonly HashSet<PropertyInt64> PropertiesInt64 = GetValues<PropertyInt64>();

        /// <summary>
        /// returns a list of values for PropertyInt that are NOT [SendOnLogin]
        /// </summary>
        public static readonly HashSet<PropertyBool> PropertiesBool = GetValues<PropertyBool>();

        /// <summary>
        /// returns a list of values for PropertyInt that are NOT [SendOnLogin]
        /// </summary>
        public static readonly HashSet<PropertyString> PropertiesString = GetValues<PropertyString>();

        /// <summary>
        /// returns a list of values for PropertyInt that are NOT [SendOnLogin]
        /// </summary>
        public static readonly HashSet<PropertyFloat> PropertiesDouble = GetValues<PropertyFloat>();

        /// <summary>
        /// returns a list of values for PropertyInt that are NOT [SendOnLogin]
        /// </summary>
        public static readonly HashSet<PropertyDataId> PropertiesDataId = GetValues<PropertyDataId>();

        /// <summary>
        /// returns a list of values for PropertyInt that are NOT [SendOnLogin]
        /// </summary>
        public static readonly HashSet<PropertyInstanceId> PropertiesInstanceId = GetValues<PropertyInstanceId>();
    }
}
