using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Web;
using Weenie = ACE.Entity.Models.Weenie;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/item")]
    public class ItemSearchController : BaseController
    {
        private const ushort NameType = (ushort)PropertyString.Name;
        private const ushort ContainerType = (ushort)PropertyInstanceId.Container;
        private const ushort StackType = (ushort)PropertyInt.StackSize;

        [HttpGet("search")]
        public IActionResult SearchItems([FromQuery] string q, [FromQuery] int limit = 30)
        {
            if (!HasPortalAccess(PortalPages.Items))
                return Forbid();

            if (string.IsNullOrWhiteSpace(q))
                return Ok(Array.Empty<ItemSearchResultDto>());

            q = q.Trim();
            limit = Math.Clamp(limit, 1, 100);
            var likePattern = WeenieSearchOrdering.ContainsLikePattern(q);

            var results = new Dictionary<uint, ItemSearchResultDto>();

            if (uint.TryParse(q, out var wcid))
                TryAddWeenie(results, wcid);

            using var context = new WorldDbContext();

            if (q.Length >= 2)
            {
                var classMatches = context.Weenie
                    .AsNoTracking()
                    .Where(w => w.Type != (int)WeenieType.Creature && w.Type != (int)WeenieType.Admin)
                    .Where(w => EF.Functions.Like(w.ClassName, likePattern))
                    .OrderBy(w => w.ClassName)
                    .Take(limit)
                    .Select(w => w.ClassId)
                    .ToList();

                foreach (var id in classMatches)
                    TryAddWeenie(results, id);
            }

            if (q.Length >= 2 && results.Count < limit)
            {
                var nameMatches = context.Weenie
                    .AsNoTracking()
                    .Where(w => w.Type != (int)WeenieType.Creature && w.Type != (int)WeenieType.Admin)
                    .Join(
                        context.WeeniePropertiesString.AsNoTracking()
                            .Where(s => s.Type == NameType && EF.Functions.Like(s.Value, likePattern)),
                        w => w.ClassId,
                        s => s.ObjectId,
                        (w, s) => w.ClassId)
                    .Distinct()
                    .Take(limit)
                    .ToList();

                foreach (var id in nameMatches)
                    TryAddWeenie(results, id);
            }

            return Ok(WeenieSearchOrdering.SortByRelevance(
                results.Values,
                q,
                r => r.Name,
                r => r.ClassName,
                r => r.Wcid,
                limit));
        }

        [HttpGet("{wcid}/references")]
        public IActionResult GetItemReferences(uint wcid, [FromQuery] int shardLimit = 50, [FromQuery] int shardOffset = 0)
        {
            if (!HasPortalAccess(PortalPages.Items))
                return Forbid();

            shardLimit = Math.Clamp(shardLimit, 1, 200);
            shardOffset = Math.Max(0, shardOffset);

            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null)
                return NotFound(new { message = $"No weenie found for WCID {wcid}." });

            var item = ToSearchResult(weenie);

            using (var world = new WorldDbContext())
            {
                item.WorldReferences = GetWorldReferences(world, wcid);
            }

            using (var shard = new ShardDbContext())
            {
                shard.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                item.Shard = GetShardReferences(shard, wcid, shardLimit, shardOffset);
            }

            return Ok(item);
        }

        private static void TryAddWeenie(Dictionary<uint, ItemSearchResultDto> results, uint wcid)
        {
            if (results.ContainsKey(wcid))
                return;

            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie != null)
                results[wcid] = ToSearchResult(weenie);
        }

        private static ItemSearchResultDto ToSearchResult(Weenie weenie)
        {
            var name = weenie.GetProperty(PropertyString.Name);
            return new ItemSearchResultDto
            {
                Wcid = weenie.WeenieClassId,
                Name = name ?? $"WCID {weenie.WeenieClassId}",
                ClassName = weenie.ClassName,
                WeenieType = weenie.WeenieType.ToString(),
            };
        }

        private static WorldReferencesDto GetWorldReferences(WorldDbContext context, uint wcid)
        {
            var createList = context.WeeniePropertiesCreateList
                .AsNoTracking()
                .Where(c => c.WeenieClassId == wcid)
                .Join(context.Weenie.AsNoTracking(), c => c.ObjectId, w => w.ClassId, (c, w) => new { c, w })
                .OrderBy(x => x.w.ClassName)
                .Take(500)
                .ToList();

            var generators = context.WeeniePropertiesGenerator
                .AsNoTracking()
                .Where(g => g.WeenieClassId == wcid)
                .Join(context.Weenie.AsNoTracking(), g => g.ObjectId, w => w.ClassId, (g, w) => new { g, w })
                .OrderBy(x => x.w.ClassName)
                .Take(500)
                .ToList();

            var landblocks = context.LandblockInstance
                .AsNoTracking()
                .Where(l => l.WeenieClassId == wcid)
                .OrderBy(l => l.Landblock)
                .Take(500)
                .Select(l => new WorldLandblockDto
                {
                    Guid = l.Guid,
                    Landblock = l.Landblock,
                    LandblockHex = l.Landblock.HasValue ? $"0x{l.Landblock.Value:X4}" : null,
                    ObjCellId = $"0x{l.ObjCellId:X8}",
                })
                .ToList();

            var parentIds = createList.Select(x => x.w.ClassId)
                .Concat(generators.Select(x => x.w.ClassId))
                .Distinct()
                .ToList();

            var parentNames = parentIds.Count == 0
                ? new Dictionary<uint, string>()
                : context.WeeniePropertiesString
                    .AsNoTracking()
                    .Where(s => parentIds.Contains(s.ObjectId) && s.Type == NameType)
                    .ToDictionary(s => s.ObjectId, s => s.Value);

            return new WorldReferencesDto
            {
                CreateListCount = context.WeeniePropertiesCreateList.AsNoTracking().Count(c => c.WeenieClassId == wcid),
                GeneratorCount = context.WeeniePropertiesGenerator.AsNoTracking().Count(g => g.WeenieClassId == wcid),
                LandblockInstanceCount = context.LandblockInstance.AsNoTracking().Count(l => l.WeenieClassId == wcid),
                CreateList = createList.Select(x => new WorldCreateListDto
                {
                    ParentWcid = x.w.ClassId,
                    ParentClassName = x.w.ClassName,
                    ParentName = parentNames.GetValueOrDefault(x.w.ClassId, x.w.ClassName),
                    DestinationType = Enum.GetName(typeof(DestinationType), x.c.DestinationType) ?? x.c.DestinationType.ToString(),
                    StackSize = x.c.StackSize,
                }).ToList(),
                Generators = generators.Select(x => new WorldGeneratorDto
                {
                    ParentWcid = x.w.ClassId,
                    ParentClassName = x.w.ClassName,
                    ParentName = parentNames.GetValueOrDefault(x.w.ClassId, x.w.ClassName),
                    Probability = x.g.Probability,
                    MaxCreate = x.g.MaxCreate,
                    StackSize = x.g.StackSize,
                }).ToList(),
                LandblockInstances = landblocks,
            };
        }

        private static ShardReferencesDto GetShardReferences(ShardDbContext context, uint wcid, int limit, int offset)
        {
            var totalCount = context.Biota.AsNoTracking().Count(b => b.WeenieClassId == wcid);

            var biotas = context.Biota
                .AsNoTracking()
                .Where(b => b.WeenieClassId == wcid)
                .OrderBy(b => b.Id)
                .Skip(offset)
                .Take(limit)
                .Select(b => new { b.Id, b.WeenieType })
                .ToList();

            if (biotas.Count == 0)
            {
                return new ShardReferencesDto
                {
                    TotalCount = totalCount,
                    Limit = limit,
                    Offset = offset,
                    Instances = new List<ShardInstanceDto>(),
                };
            }

            var ids = biotas.Select(b => b.Id).ToList();

            var stacks = context.BiotaPropertiesInt
                .AsNoTracking()
                .Where(i => ids.Contains(i.ObjectId) && i.Type == StackType)
                .ToDictionary(i => i.ObjectId, i => i.Value);

            var itemNames = context.BiotaPropertiesString
                .AsNoTracking()
                .Where(s => ids.Contains(s.ObjectId) && s.Type == NameType)
                .ToDictionary(s => s.ObjectId, s => s.Value);

            var directContainers = context.BiotaPropertiesIID
                .AsNoTracking()
                .Where(i => ids.Contains(i.ObjectId) && i.Type == ContainerType)
                .ToDictionary(i => i.ObjectId, i => i.Value);

            var containerMap = BuildContainerMap(context, directContainers.Values);

            var characterLookupIds = new HashSet<uint>(directContainers.Values.Where(v => v != 0));
            foreach (var kv in containerMap)
            {
                characterLookupIds.Add(kv.Key);
                characterLookupIds.Add(kv.Value);
            }

            var characters = characterLookupIds.Count == 0
                ? new Dictionary<uint, string>()
                : context.Character
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted && characterLookupIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name })
                    .ToDictionary(c => c.Id, c => c.Name);

            var instances = biotas.Select(b =>
            {
                directContainers.TryGetValue(b.Id, out var containerId);
                var location = ResolveLocation(b.Id, containerId, containerMap, characters, context);

                return new ShardInstanceDto
                {
                    BiotaId = b.Id,
                    BiotaHex = $"0x{b.Id:X8}",
                    WeenieType = ((WeenieType)b.WeenieType).ToString(),
                    StackSize = stacks.GetValueOrDefault(b.Id, 1),
                    ItemName = itemNames.GetValueOrDefault(b.Id),
                    ContainerId = containerId == 0 ? null : containerId,
                    ContainerHex = containerId == 0 ? null : $"0x{containerId:X8}",
                    LocationKind = location.Kind,
                    OwnerName = location.OwnerName,
                    OwnerGuid = location.OwnerGuid,
                    LocationDetail = location.Detail,
                    CharacterLinkGuid = location.CharacterLinkGuid,
                };
            }).ToList();

            return new ShardReferencesDto
            {
                TotalCount = totalCount,
                Limit = limit,
                Offset = offset,
                Instances = instances,
            };
        }

        private static Dictionary<uint, uint> BuildContainerMap(
            ShardDbContext context,
            IEnumerable<uint> seedParentIds)
        {
            var map = new Dictionary<uint, uint>();
            var frontier = new HashSet<uint>(seedParentIds.Where(id => id != 0));

            for (var depth = 0; depth < 16 && frontier.Count > 0; depth++)
            {
                var batch = frontier.Where(id => !map.ContainsKey(id)).ToList();
                frontier.Clear();
                if (batch.Count == 0)
                    break;

                var links = context.BiotaPropertiesIID
                    .AsNoTracking()
                    .Where(i => batch.Contains(i.ObjectId) && i.Type == ContainerType)
                    .Select(i => new { i.ObjectId, i.Value })
                    .ToList();

                foreach (var link in links)
                {
                    map[link.ObjectId] = link.Value;
                    if (link.Value != 0)
                        frontier.Add(link.Value);
                }
            }

            return map;
        }

        private static (string Kind, string OwnerName, uint? OwnerGuid, string Detail, uint? CharacterLinkGuid) ResolveLocation(
            uint itemId,
            uint directContainerId,
            Dictionary<uint, uint> containerMap,
            Dictionary<uint, string> characters,
            ShardDbContext context)
        {
            var current = directContainerId;
            if (current == 0)
                return ("Unknown", null, null, "No container (world object or unlinked)", null);

            for (var depth = 0; depth < 16; depth++)
            {
                if (current == 0)
                    return ("Unknown", null, null, "Container chain ended", null);

                if (characters.TryGetValue(current, out var charName))
                    return ("Character", charName, current, $"In inventory (via 0x{current:X8})", current);

                if (!containerMap.TryGetValue(current, out var parent))
                {
                    var rootName = context.BiotaPropertiesString
                        .AsNoTracking()
                        .Where(s => s.ObjectId == current && s.Type == NameType)
                        .Select(s => s.Value)
                        .FirstOrDefault();

                    var rootType = context.Biota
                        .AsNoTracking()
                        .Where(b => b.Id == current)
                        .Select(b => b.WeenieType)
                        .FirstOrDefault();

                    var typeLabel = ((WeenieType)rootType).ToString();
                    var label = string.IsNullOrWhiteSpace(rootName) ? $"0x{current:X8}" : rootName;
                    return ("Container", label, current, $"Inside {typeLabel} 0x{current:X8}", null);
                }

                current = parent;
            }

            return ("Unknown", null, null, "Container chain too deep", null);
        }

        public class ItemSearchResultDto
        {
            public uint Wcid { get; set; }
            public string Name { get; set; } = "";
            public string ClassName { get; set; } = "";
            public string WeenieType { get; set; } = "";
            public WorldReferencesDto? WorldReferences { get; set; }
            public ShardReferencesDto? Shard { get; set; }
        }

        public class WorldReferencesDto
        {
            public int CreateListCount { get; set; }
            public int GeneratorCount { get; set; }
            public int LandblockInstanceCount { get; set; }
            public List<WorldCreateListDto> CreateList { get; set; } = new();
            public List<WorldGeneratorDto> Generators { get; set; } = new();
            public List<WorldLandblockDto> LandblockInstances { get; set; } = new();
        }

        public class WorldCreateListDto
        {
            public uint ParentWcid { get; set; }
            public string ParentClassName { get; set; } = "";
            public string ParentName { get; set; } = "";
            public string DestinationType { get; set; } = "";
            public int StackSize { get; set; }
        }

        public class WorldGeneratorDto
        {
            public uint ParentWcid { get; set; }
            public string ParentClassName { get; set; } = "";
            public string ParentName { get; set; } = "";
            public float Probability { get; set; }
            public int MaxCreate { get; set; }
            public int? StackSize { get; set; }
        }

        public class WorldLandblockDto
        {
            public uint Guid { get; set; }
            public int? Landblock { get; set; }
            public string? LandblockHex { get; set; }
            public string ObjCellId { get; set; } = "";
        }

        public class ShardReferencesDto
        {
            public int TotalCount { get; set; }
            public int Limit { get; set; }
            public int Offset { get; set; }
            public List<ShardInstanceDto> Instances { get; set; } = new();
        }

        public class ShardInstanceDto
        {
            public uint BiotaId { get; set; }
            public string BiotaHex { get; set; } = "";
            public string WeenieType { get; set; } = "";
            public int StackSize { get; set; }
            public string? ItemName { get; set; }
            public uint? ContainerId { get; set; }
            public string? ContainerHex { get; set; }
            public string LocationKind { get; set; } = "";
            public string? OwnerName { get; set; }
            public uint? OwnerGuid { get; set; }
            public string LocationDetail { get; set; } = "";
            public uint? CharacterLinkGuid { get; set; }
        }
    }
}
