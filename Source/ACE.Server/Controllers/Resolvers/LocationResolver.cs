using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ACE.Database;
using ACE.Server.Managers;
using ACE.Server.Entity;

namespace ACE.Server.Controllers.Resolvers
{
    public static class LocationResolver
    {
        private static readonly ConcurrentDictionary<string, string> _dungeonNameCache = new();

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
            if (_dungeonNameCache.TryGetValue(cacheKey, out var cachedName))
                return cachedName;

            // Try World DB (synchronous)
            var name = DatabaseManager.World.GetLandblockName(landblockId, variationId);

            if (name == null)
            {
                // Try Shard DB (asynchronous callback)
                var tcs = new TaskCompletionSource<string>();
                DatabaseManager.Shard.GetLandblockName(landblockId, variationId, (n) =>
                {
                    tcs.SetResult(n);
                });
                name = await tcs.Task;
            }

            if (name != null)
                _dungeonNameCache.TryAdd(cacheKey, name);

            return name;
        }
    }
}
