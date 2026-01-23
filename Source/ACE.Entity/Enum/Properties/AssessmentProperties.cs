using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ACE.Entity.Enum.Properties
{
    /// <summary>
    /// Static selection of client enums that are [AssessmentProperty]<para />
    /// These are properties sent to the client on id.
    /// </summary>
    public static class AssessmentProperties
    {
        /// <summary>
        /// Method to return a list of enums by attribute type - in this case [AssessmentProperty] using generics to enhance code reuse.
        /// </summary>
        /// <typeparam name="T">Enum to list by [AssessmentProperty]</typeparam>
        private static HashSet<T> GetValues<T>()
        {
            HashSet<T> results = [];
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetCustomAttribute<AssessmentPropertyAttribute>() == null) continue;
                if (field.GetValue(null) is T result) results.Add(result);
            }
            return results;
        }

        /// <summary>
        /// returns a list of values for PropertyInt that are [AssessmentProperty]
        /// </summary
        public readonly static HashSet<PropertyInt> PropertiesInt = GetValues<PropertyInt>();

        /// <summary>
        /// returns a list of values for PropertyInt that are [AssessmentProperty]
        /// </summary>
        public readonly static HashSet<PropertyInt64> PropertiesInt64 = GetValues<PropertyInt64>();

        /// <summary>
        /// returns a list of values for PropertyInt that are [AssessmentProperty]
        /// </summary>
        public readonly static HashSet<PropertyBool> PropertiesBool = GetValues<PropertyBool>();

        /// <summary>
        /// returns a list of values for PropertyInt that are [AssessmentProperty]
        /// </summary>
        public readonly static HashSet<PropertyString> PropertiesString = GetValues<PropertyString>();

        /// <summary>
        /// returns a list of values for PropertyInt that are [AssessmentProperty]
        /// </summary>
        public readonly static HashSet<PropertyFloat> PropertiesDouble = GetValues<PropertyFloat>();

        /// <summary>
        /// returns a list of values for PropertyInt that are [AssessmentProperty]
        /// </summary>
        public readonly static HashSet<PropertyDataId> PropertiesDataId = GetValues<PropertyDataId>();

        /// <summary>
        /// returns a list of values for PropertyInt that are [AssessmentProperty]
        /// </summary>
        public readonly static HashSet<PropertyInstanceId> PropertiesInstanceId = GetValues<PropertyInstanceId>();
    }
}
