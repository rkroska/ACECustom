using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Web;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Managers.QuestBuilder
{
    public static class QuestBuilderCreatureSearch
    {
        private const ushort NameType = (ushort)PropertyString.Name;

        /// <summary>Weenie types that are valid clone templates for quest mobs.</summary>
        private static readonly int[] CreatureLikeTypes =
        {
            (int)WeenieType.Creature,
            (int)WeenieType.Cow,
        };

        public static List<CreatureSearchResultDto> Search(string q, int limit)
        {
            if (string.IsNullOrWhiteSpace(q))
                return new List<CreatureSearchResultDto>();

            q = q.Trim();
            limit = Math.Clamp(limit, 1, 100);

            var results = new Dictionary<uint, CreatureSearchResultDto>();
            var likePattern = WeenieSearchOrdering.ContainsLikePattern(q);

            using var context = new WorldDbContext();

            if (uint.TryParse(q, out var wcid))
                TryAdd(context, results, wcid);

            if (q.Length >= 1)
            {
                var classMatches = context.Weenie
                    .AsNoTracking()
                    .Where(w => CreatureLikeTypes.Contains(w.Type))
                    .Where(w => EF.Functions.Like(w.ClassName, likePattern))
                    .OrderBy(w => w.ClassName)
                    .Take(limit)
                    .Select(w => w.ClassId)
                    .ToList();

                foreach (var id in classMatches)
                    TryAdd(context, results, id);
            }

            if (q.Length >= 2 && results.Count < limit)
            {
                var nameMatches = context.Weenie
                    .AsNoTracking()
                    .Where(w => CreatureLikeTypes.Contains(w.Type))
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
                    TryAdd(context, results, id);
            }

            return WeenieSearchOrdering.SortByRelevance(
                results.Values,
                q,
                r => r.Name,
                r => r.ClassName,
                r => r.Wcid,
                limit);
        }

        public static CreatureSearchResultDto Get(uint wcid)
        {
            using var context = new WorldDbContext();
            return BuildDto(context, wcid);
        }

        private static void TryAdd(WorldDbContext context, Dictionary<uint, CreatureSearchResultDto> results, uint wcid)
        {
            if (results.ContainsKey(wcid))
                return;

            var dto = BuildDto(context, wcid);
            if (dto != null)
                results[wcid] = dto;
        }

        private static CreatureSearchResultDto BuildDto(WorldDbContext context, uint classId)
        {
            var row = context.Weenie.AsNoTracking().FirstOrDefault(w => w.ClassId == classId);
            if (row == null || !CreatureLikeTypes.Contains(row.Type))
                return null;

            var name = context.WeeniePropertiesString.AsNoTracking()
                .Where(s => s.ObjectId == classId && s.Type == NameType)
                .Select(s => s.Value)
                .FirstOrDefault();

            return new CreatureSearchResultDto
            {
                Wcid = classId,
                Name = string.IsNullOrWhiteSpace(name) ? $"WCID {classId}" : name,
                ClassName = row.ClassName,
                WeenieType = ((WeenieType)row.Type).ToString(),
            };
        }

    }
}
