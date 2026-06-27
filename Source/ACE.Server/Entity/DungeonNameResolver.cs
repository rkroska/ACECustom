using System;
using System.Collections.Concurrent;
using System.Linq;

using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.Entity
{
    public static class DungeonNameResolver
    {
        private static readonly ConcurrentDictionary<(uint Landblock, int Variation), string> _dungeonNameCache = new ConcurrentDictionary<(uint, int), string>();

        public static string Resolve(uint landblock, int variation)
        {
            var key = (landblock, variation);
            if (_dungeonNameCache.TryGetValue(key, out var cachedName))
            {
                return cachedName;
            }

            var blockStart = landblock << 16;
            var blockEnd = blockStart | 0xFFFF;

            string resolvedName = null;
            var lookupFailed = false;

            try
            {
                using (var ctx = new WorldDbContext())
                {
                    var query = from weenie in ctx.Weenie
                                join wstr in ctx.WeeniePropertiesString on weenie.ClassId equals wstr.ObjectId
                                join wpos in ctx.WeeniePropertiesPosition on weenie.ClassId equals wpos.ObjectId
                                where weenie.Type == (int)WeenieType.Portal
                                    && wstr.Type == (int)PropertyString.Name
                                    && wpos.PositionType == (int)PositionType.Destination
                                    && wpos.ObjCellId >= blockStart
                                    && wpos.ObjCellId <= blockEnd
                                    && (wpos.VariationId ?? 0) == variation
                                select wstr.Value;

                    var results = query.ToList();

                    if (results.Count == 0)
                    {
                        var fallbackQuery = from weenie in ctx.Weenie
                                            join wstr in ctx.WeeniePropertiesString on weenie.ClassId equals wstr.ObjectId
                                            join wpos in ctx.WeeniePropertiesPosition on weenie.ClassId equals wpos.ObjectId
                                            where weenie.Type == (int)WeenieType.Portal
                                                && wstr.Type == (int)PropertyString.Name
                                                && wpos.PositionType == (int)PositionType.Destination
                                                && wpos.ObjCellId >= blockStart
                                                && wpos.ObjCellId <= blockEnd
                                            select wstr.Value;

                        results = fallbackQuery.ToList();
                    }

                    if (results.Count > 0)
                    {
                        // Clean all names: TrimStart("Portal to ") and TrimEnd(" Portal")
                        var cleanedNames = results.Select(name => {
                            var cleaned = name.TrimStart("Portal to ").TrimEnd(" Portal");
                            return new { Original = name, Cleaned = cleaned };
                        }).ToList();

                        // Select the best name:
                        // 1. A name that doesn't contain "portal" (case insensitive) is preferred.
                        // 2. Shortest cleaned name is preferred.
                        var bestMatch = cleanedNames
                            .OrderBy(n => n.Cleaned.Contains("Portal", StringComparison.OrdinalIgnoreCase))
                            .ThenBy(n => n.Cleaned.Length)
                            .FirstOrDefault();

                        if (bestMatch != null)
                        {
                            resolvedName = bestMatch.Cleaned;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback on database error
                lookupFailed = true;
            }

            if (string.IsNullOrEmpty(resolvedName))
            {
                resolvedName = $"Unknown Dungeon (0x{landblock:X4})";
            }

            if (!lookupFailed)
            {
                _dungeonNameCache.TryAdd(key, resolvedName);
            }
            return resolvedName;
        }
    }
}
