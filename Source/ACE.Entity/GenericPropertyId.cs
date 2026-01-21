using ACE.Entity.Enum.Properties;

namespace ACE.Entity
{
    public struct GenericPropertyId(uint propertyId, PropertyType propertyType)
    {
        public uint PropertyId { get; set; } = propertyId;

        public PropertyType PropertyType { get; set; } = propertyType;
    }
}
