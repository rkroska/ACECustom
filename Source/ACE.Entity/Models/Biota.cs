using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Entity.Models
{
    /// <summary>
    /// Only populated collections and dictionaries are initialized.
    /// We do this to conserve memory in ACE.Server
    /// Be sure to check for null first.
    /// </summary>
    public class Biota : IWeenie
    {
        public required uint Id { get; set; }
        public required uint WeenieClassId { get; set; }
        public required WeenieType WeenieType { get; set; }

        public IDictionary<PropertyBool, bool>? PropertiesBool { get; set; }
        public IDictionary<PropertyDataId, uint>? PropertiesDID { get; set; }
        public IDictionary<PropertyFloat, double>? PropertiesFloat { get; set; }
        public IDictionary<PropertyInstanceId, uint>? PropertiesIID { get; set; }
        public IDictionary<PropertyInt, int>? PropertiesInt { get; set; }
        public IDictionary<PropertyInt64, long>? PropertiesInt64 { get; set; }
        public IDictionary<PropertyString, string>? PropertiesString { get; set; }
        public IDictionary<PositionType, PropertiesPosition>? PropertiesPosition { get; set; }

        public IDictionary<int, float /* probability */>? PropertiesSpellBook { get; set; }

        public IList<PropertiesAnimPart>? PropertiesAnimPart { get; set; }
        public IList<PropertiesPalette>? PropertiesPalette { get; set; }
        public IList<PropertiesTextureMap>? PropertiesTextureMap { get; set; }

        // Properties for all world objects that typically aren't modified over the original weenie
        public ICollection<PropertiesCreateList>? PropertiesCreateList { get; set; }
        public IList<PropertiesEmote>? PropertiesEmote { get; set; }
        public HashSet<int>? PropertiesEventFilter { get; set; }
        public IList<PropertiesGenerator>? PropertiesGenerator { get; set; }

        // Properties for creatures
        public IDictionary<PropertyAttribute, PropertiesAttribute>? PropertiesAttribute { get; set; }
        public IDictionary<PropertyAttribute2nd, PropertiesAttribute2nd>? PropertiesAttribute2nd { get; set; }
        public IDictionary<CombatBodyPart, PropertiesBodyPart>? PropertiesBodyPart { get; set; }
        public IDictionary<Skill, PropertiesSkill>? PropertiesSkill { get; set; }

        // Properties for books
        public PropertiesBook? PropertiesBook { get; set; }
        public IList<PropertiesBookPageData>? PropertiesBookPageData { get; set; }

        // Biota additions over Weenie
        public IDictionary<uint /* Character ID */, PropertiesAllegiance>? PropertiesAllegiance { get; set; }
        public ICollection<PropertiesEnchantmentRegistry>? PropertiesEnchantmentRegistry { get; set; }
        public IDictionary<uint /* Player GUID */, bool /* Storage */>? HousePermissions { get; set; }

        // Biota dynamic quest addition
        public List<PropertiesEmote>? DynamicEmoteList { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is Biota biota &&
                   Id == biota.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        /// <summary>
        /// Creates a deep copy of the Biota, capturing a point-in-time snapshot of all property collections.
        /// If a ReaderWriterLockSlim is provided, the copy operation is performed under a read lock.
        /// </summary>
        public Biota Clone(System.Threading.ReaderWriterLockSlim? rwLock = null)
        {
            if (rwLock != null) rwLock.EnterReadLock();
            try
            {
                var clone = new Biota
                {
                    Id = Id,
                    WeenieClassId = WeenieClassId,
                    WeenieType = WeenieType,

                    // Basic Properties
                    PropertiesBool = PropertiesBool == null ? null : new Dictionary<PropertyBool, bool>(PropertiesBool),
                    PropertiesDID = PropertiesDID == null ? null : new Dictionary<PropertyDataId, uint>(PropertiesDID),
                    PropertiesFloat = PropertiesFloat == null ? null : new Dictionary<PropertyFloat, double>(PropertiesFloat),
                    PropertiesIID = PropertiesIID == null ? null : new Dictionary<PropertyInstanceId, uint>(PropertiesIID),
                    PropertiesInt = PropertiesInt == null ? null : new Dictionary<PropertyInt, int>(PropertiesInt),
                    PropertiesInt64 = PropertiesInt64 == null ? null : new Dictionary<PropertyInt64, long>(PropertiesInt64),
                    PropertiesString = PropertiesString == null ? null : new Dictionary<PropertyString, string>(PropertiesString),
                    PropertiesPosition = PropertiesPosition == null ? null : PropertiesPosition.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
                    PropertiesSpellBook = PropertiesSpellBook == null ? null : new Dictionary<int, float>(PropertiesSpellBook),

                    // Lists
                    PropertiesAnimPart = PropertiesAnimPart == null ? null : PropertiesAnimPart.Select(x => x.Clone()).ToList(),
                    PropertiesPalette = PropertiesPalette == null ? null : PropertiesPalette.Select(x => x.Clone()).ToList(),
                    PropertiesTextureMap = PropertiesTextureMap == null ? null : PropertiesTextureMap.Select(x => x.Clone()).ToList(),
                    PropertiesCreateList = PropertiesCreateList == null ? null : PropertiesCreateList.Select(x => x.Clone()).ToList(),
                    PropertiesEmote = PropertiesEmote == null ? null : PropertiesEmote.Select(x => x.Clone()).ToList(),
                    PropertiesGenerator = PropertiesGenerator == null ? null : PropertiesGenerator.Select(x => x.Clone()).ToList(),
                    PropertiesEventFilter = PropertiesEventFilter == null ? null : new HashSet<int>(PropertiesEventFilter),

                    // Creature Properties
                    PropertiesAttribute = PropertiesAttribute == null ? null : PropertiesAttribute.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
                    PropertiesAttribute2nd = PropertiesAttribute2nd == null ? null : PropertiesAttribute2nd.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
                    PropertiesBodyPart = PropertiesBodyPart == null ? null : PropertiesBodyPart.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
                    PropertiesSkill = PropertiesSkill == null ? null : PropertiesSkill.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),

                    // Book Properties
                    PropertiesBook = PropertiesBook?.Clone(),
                    PropertiesBookPageData = PropertiesBookPageData == null ? null : new List<PropertiesBookPageData>(PropertiesBookPageData),

                    // Biota Additions
                    PropertiesAllegiance = PropertiesAllegiance == null ? null : PropertiesAllegiance.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
                    PropertiesEnchantmentRegistry = PropertiesEnchantmentRegistry == null ? null : new List<PropertiesEnchantmentRegistry>(PropertiesEnchantmentRegistry),
                    HousePermissions = HousePermissions == null ? null : new Dictionary<uint, bool>(HousePermissions),
                    DynamicEmoteList = DynamicEmoteList == null ? null : DynamicEmoteList.Select(e => e.Clone()).ToList()
                };

                return clone;
            }
            finally
            {
                if (rwLock != null) rwLock.ExitReadLock();
            }
        }
    }
}
