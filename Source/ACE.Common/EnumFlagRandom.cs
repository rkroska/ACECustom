using System;
using System.Numerics;

namespace ACE.Common
{
    public static class EnumFlagRandom
    {
        /// <summary>
        /// Caches metadata per Enum type.
        /// </summary>
        private static class Cache<T> where T : struct, Enum
        {
            // All acceptable values for this flag.
            public static readonly uint Mask;

            // The first value defined for this flag.
            public static readonly T FirstDefinedValue;

            static Cache()
            {
                uint combinedMask = 0;
                T firstValue = default;
                bool foundFirst = false;

                // Enum.GetValues returns values in increasing order.
                foreach (T value in Enum.GetValues<T>())
                {
                    var intValue = Convert.ToUInt32(value);
                    if (intValue == 0) continue;
                    combinedMask |= intValue;

                    if (!foundFirst)
                    {
                        firstValue = value;
                        foundFirst = true;
                    }
                }

                Mask = combinedMask;
                FirstDefinedValue = firstValue;
            }
        }

        /// <summary>
        /// Selects a single random flag from a bitmask.
        /// </summary>
        /// <param name="currentFlags">The bitmask to select from.</param>
        /// <param name="defaultFlags">Fallback bitmask if currentFlags is None/0.</param>
        /// <returns>A single bit as the Enum flag type T.</returns>
        public static T SelectRandomFlag<T>(T currentFlags, T defaultFlags) where T : struct, Enum
        {
            // Scrub the input against the mask, so that no bits for undefined flags are set.
            uint rawFlags = Convert.ToUInt32(currentFlags) & Cache<T>.Mask;

            // If there are no selected bits in our input, fall back to our default.
            // If the default has no selected bits, use our first value.
            if (rawFlags == 0)
            {
                rawFlags = Convert.ToUInt32(defaultFlags) & Cache<T>.Mask;
                if (rawFlags == 0) return Cache<T>.FirstDefinedValue;
            }

            // Retrieve the count of flags we have to choose from.
            int totalSetBits = BitOperations.PopCount(rawFlags);

            // Optimization: if only one bit is set, return the flag for this bit without using RNG.
            if (totalSetBits == 1) return (T)Enum.ToObject(typeof(T), rawFlags);

            // Randomly choose which bit we want to select (ThreadSafeRandom.Next is inclusive min, max).
            // Then, shave off the least significant bit until we reach our target bit.
            int targetIndex = ThreadSafeRandom.Next(0, totalSetBits - 1);
            for (int i = 0; i < targetIndex; i++) rawFlags &= (rawFlags - 1);

            // The remaining lowest bit is the flag we want to return.
            // This bit twiddling keeps the lowest significant bit and nothing else.
            uint finalBit = rawFlags & (uint)-(int)rawFlags;
            return (T)Enum.ToObject(typeof(T), finalBit);
        }
    }
}
