using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using ACE.Database;
using ACE.Server.Managers;
using ACE.Server.Entity;

namespace ACE.Server.Controllers.Resolvers
{
    public static class LocationResolver
    {
        private static readonly IMemoryCache _dungeonNameCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 10000 // Limit to 10k entries to prevent memory growth
        });

        private static readonly MemoryCacheEntryOptions _cacheOptions = new MemoryCacheEntryOptions()
            .SetSize(1) // Each entry counts as 1 towards the SizeLimit
            .SetSlidingExpiration(TimeSpan.FromHours(1))
            .SetAbsoluteExpiration(TimeSpan.FromHours(4));

        public struct LocationResolution
        {
            public string Name { get; set; }
            public string CategoryName { get; set; }
            public int CategoryOrdinal { get; set; }
        }

        public static async Task<LocationResolution> ResolveLocationAsync(uint landblockId, int? variationId, bool? isDungeonForced = null)
        {
            // Normalize: use high 16 bits if present, else low 16 bits
            var normalizedLandblock = (ushort)(landblockId >> 16);
            if (normalizedLandblock == 0) normalizedLandblock = (ushort)(landblockId & 0xFFFF);

            // 1. Special: Marketplace (0x016C)
            if (LandblockCollections.MarketplaceLandblocks.Contains(normalizedLandblock))
            {
                return new LocationResolution 
                { 
                    CategoryName = "Special", 
                    CategoryOrdinal = 1,
                    Name = "Marketplace"
                };
            }

            // 2. Special: Apartments
            if (LandblockCollections.ApartmentLandblocks.Contains(normalizedLandblock))
            {
                LandblockCollections.ApartmentBlocks.TryGetValue(normalizedLandblock, out var aptName);
                return new LocationResolution 
                { 
                    CategoryName = "Special", 
                    CategoryOrdinal = 1,
                    Name = "Apartments",
                };
            }

            // 3. Outdoors: Specific Islands
            if (LandblockCollections.ThaelarynIslandLandblocks.Contains(normalizedLandblock))
            {
                return new LocationResolution 
                { 
                    CategoryName = "Outdoors", 
                    CategoryOrdinal = 2,
                    Name = "Thaelaryn Island",
                };
            }
            if (LandblockCollections.ValleyOfDeathLandblocks.Contains(normalizedLandblock))
            {
                return new LocationResolution 
                { 
                    CategoryName = "Outdoors", 
                    CategoryOrdinal = 2,
                    Name = "Valley of Death",
                };
            }
            if (LandblockCollections.DarkIsleLandblocks.Contains(normalizedLandblock))
            {
                return new LocationResolution 
                { 
                    CategoryName = "Outdoors", 
                    CategoryOrdinal = 2,
                    Name = "Dark Isle",
                };
            }

            // Determination of "Interior/Dungeon" status:
            // Fix: Only treat low bits as "cell bits" if it's a 32-bit ID (has high bits)
            // or if the regional byte is in the 0xFF00 range.
            bool isDungeon = isDungeonForced ?? (normalizedLandblock >= 0xFF00 || (landblockId > 0xFFFF && (landblockId & 0xFFFF) >= 0x0100));

            if (!isDungeon)
            {
                // Outdoor / Landscape Landblocks
                return new LocationResolution 
                { 
                    CategoryName = "Outdoors", 
                    CategoryOrdinal = 2,
                    Name = "Outside",
                };
            }
            else
            {
                // Dungeon Landblocks - Query DB for portal/dungeon name
                string name = await GetDungeonNameCachedAsync(landblockId, variationId);

                return new LocationResolution 
                { 
                    CategoryName = "Dungeons", 
                    CategoryOrdinal = 3,
                    Name = name ?? "Unknown Dungeon",
                };
            }
        }

        private static async Task<string> GetDungeonNameCachedAsync(uint landblockId, int? variationId)
        {
            // Normalize landblock to the 4-digit prefix (dungeon range) used by DB resolvers
            var lb = landblockId >> 16;
            if (lb == 0) lb = landblockId;

            var cacheKey = $"{lb:X4}:{variationId?.ToString() ?? "null"}";
            if (_dungeonNameCache.TryGetValue(cacheKey, out string cachedName))
                return cachedName;

            // Try World DB (synchronous) - Soft failure pattern
            string name = null;
            try
            {
                name = DatabaseManager.World.GetLandblockName(landblockId, variationId);
            }
            catch (Exception ex)
            {
                // Log and swallow - allow fallback to Shard DB
                Console.WriteLine($"[LocationResolver] Warning: World DB lookup failed for LB 0x{landblockId:X8}: {ex.Message}");
            }

            if (name == null)
            {
                // Try Shard DB (asynchronous callback)
                var tcs = new TaskCompletionSource<string>();
                try
                {
                    DatabaseManager.Shard.GetLandblockName(landblockId, variationId, (n) =>
                    {
                        try
                        {
                            tcs.TrySetResult(n);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    });

                    // Wait for result with a 5-second timeout
                    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                    if (completedTask == tcs.Task)
                        name = await tcs.Task;
                    else
                        name = null; // Timeout
                }
                catch
                {
                    name = null;
                }
            }

            if (name != null)
                _dungeonNameCache.Set(cacheKey, name, _cacheOptions);

            return name;
        }
    }
}
