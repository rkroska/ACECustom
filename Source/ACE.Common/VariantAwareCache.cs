using System;

namespace ACE.Common
{
    public struct VariantCacheId : IEquatable<VariantCacheId>
    {
        public ushort Landblock;
        /// <summary>The base layer is null. 0 is always base (owner ruling 2026-09-14): every server-side key builder
        /// (LandblockManager.GetLandblock, LScape, AdjustCell, the DB instance cache) converts it with
        /// <see cref="NormalizeBase"/> before building a key, so no live key holds a 0.</summary>
        public int? Variant;

        /// <summary>Collapse the base bucket: null and 0 both become null; every other value is preserved. The one
        /// definition shared by ACE.Server (VariationManager.NormalizeBase) and ACE.Database.</summary>
        public static int? NormalizeBase(int? variation)
        {
            if (!variation.HasValue || variation.Value == 0)
                return null;
            return variation;
        }

        public VariantCacheId(ushort landblock, int variant)
        {
            Landblock = landblock;
            Variant = variant;
        }

        public VariantCacheId(ushort landblock, int? variant)
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
