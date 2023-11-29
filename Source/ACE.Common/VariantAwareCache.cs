using System;

namespace ACE.Common
{
    public struct VariantCacheId : IEquatable<VariantCacheId>
    {
        public ushort Landblock;
        public int Variant;

        public VariantCacheId(ushort landblock, int variant)
        {
            Landblock = landblock;
            Variant = variant;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is VariantCacheId cacheKey && Equals(cacheKey);
        }

        public readonly bool Equals(VariantCacheId other)
        {
            return Landblock == other.Landblock && Variant == other.Variant;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Landblock, Variant);
        }

        public static bool operator ==(VariantCacheId left, VariantCacheId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VariantCacheId left, VariantCacheId right)
        {
            return !(left == right);
        }
    }
}
