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
            public int GrouperType { get; set; }
            public string Hex { get; set; }
        }

        public static async Task<LocationResolution> ResolveLocationAsync(uint landblockId, int? variationId, bool? isDungeonForced = null)
        {
            var lb16 = (ushort)(landblockId >> 16);
            if (lb16 == 0) lb16 = (ushort)landblockId;

            var hex = $"0x{lb16:x4}";
            if (variationId.HasValue) hex += $":{variationId.Value}";

            // 1. Marketplace (0x016C)
            if (LandblockCollections.MarketplaceLandblocks.Contains(lb16))
            {
                return new LocationResolution { Name = "Marketplace", GrouperType = 1, Hex = hex };
            }

            // 2. Apartments
            if (LandblockCollections.ApartmentLandblocks.Contains(lb16))
            {
                LandblockCollections.ApartmentBlocks.TryGetValue(lb16, out var aptName);
                return new LocationResolution { Name = aptName ?? "Apartments", GrouperType = 2, Hex = hex };
            }

            // 3. Special Outside
            if (LandblockCollections.ValleyOfDeathLandblocks.Contains(lb16))
            {
                return new LocationResolution { Name = "Valley of Death", GrouperType = 3, Hex = hex };
            }
            if (LandblockCollections.ThaelarynIslandLandblocks.Contains(lb16))
            {
                return new LocationResolution { Name = "Thaelaryn Island", GrouperType = 3, Hex = hex };
            }
            if (LandblockCollections.DarkIsleLandblocks.Contains(lb16))
            {
                return new LocationResolution { Name = "Dark Isle", GrouperType = 3, Hex = hex };
            }

            // Determination of "Interior/Dungeon" status:
            // Priority 1: Client-provided / Engine-authoritative flag (online players)
            // Priority 2: Bitwise heuristic (offline players / fallback)
            bool isDungeon = isDungeonForced ?? (lb16 >= 0xFF00 || (landblockId & 0xFFFF) >= 0x0100);

            if (!isDungeon)
            {
                // Outdoor / Landscape Landblocks
                return new LocationResolution { Name = "Outside", GrouperType = 3, Hex = hex };
            }
            else
            {
                // Dungeon Landblocks - Query DB for portal/dungeon name
                string name = await GetDungeonNameCachedAsync(landblockId, variationId);

                return new LocationResolution 
                { 
                    Name = name ?? "Unknown Dungeon", 
                    GrouperType = 4, 
                    Hex = hex 
                };
            }
        }

        private static async Task<string> GetDungeonNameCachedAsync(uint landblockId, int? variationId)
        {
            var cacheKey = $"{landblockId:X8}:{variationId ?? 0}";
            if (_dungeonNameCache.TryGetValue(cacheKey, out string cachedName))
                return cachedName;

            // Try World DB (synchronous)
            var name = DatabaseManager.World.GetLandblockName(landblockId, variationId);

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
