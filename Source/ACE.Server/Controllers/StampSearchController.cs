using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Database.Models.Shard;
using ACE.Server.Managers;
using ACE.Server.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/stamp")]
    public class StampSearchController : BaseController
    {
        [HttpGet("suggest")]
        public IActionResult SuggestStamps([FromQuery] string q, [FromQuery] int limit = 30)
        {
            if (!HasPortalAccess(PortalPages.Stamps))
                return Forbid();

            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Ok(Array.Empty<string>());

            q = q.Trim();
            limit = Math.Clamp(limit, 1, 100);
            var pattern = WeenieSearchOrdering.ContainsLikePattern(q);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var shard = new ShardDbContext())
            {
                foreach (var name in shard.CharacterPropertiesQuestRegistry
                    .AsNoTracking()
                    .Where(r => EF.Functions.Like(r.QuestName, pattern))
                    .Select(r => r.QuestName)
                    .Distinct()
                    .Take(limit))
                    names.Add(name);
            }

            using (var auth = new AuthDbContext())
            {
                foreach (var name in auth.AccountQuest
                    .AsNoTracking()
                    .Where(r => EF.Functions.Like(r.Quest, pattern))
                    .Select(r => r.Quest)
                    .Distinct()
                    .Take(limit))
                    names.Add(name);
            }

            return Ok(WeenieSearchOrdering.SortStringsByRelevance(names, q, limit));
        }

        [HttpGet("lookup")]
        public IActionResult LookupStamp([FromQuery] string stamp, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            if (!HasPortalAccess(PortalPages.Stamps))
                return Forbid();

            if (string.IsNullOrWhiteSpace(stamp))
                return BadRequest(new { message = "Stamp name is required." });

            stamp = stamp.Trim();
            limit = Math.Clamp(limit, 1, 200);
            offset = Math.Max(0, offset);

            var resolved = ResolveStampName(stamp, out var candidates);
            if (resolved == null)
            {
                return NotFound(new
                {
                    message = $"No stamp found matching \"{stamp}\".",
                    candidates = candidates.Take(20).ToList(),
                });
            }

            var result = new StampLookupResultDto
            {
                StampName = resolved,
                ServerTotalCompletions = ShardDatabase.GetServerQuestCompletions(resolved),
            };

            using (var auth = new AuthDbContext())
            {
                var accountRows = auth.AccountQuest
                    .AsNoTracking()
                    .Where(q => q.Quest == resolved && q.NumTimesCompleted >= 1)
                    .Join(
                        auth.Account.AsNoTracking(),
                        q => q.AccountId,
                        a => a.AccountId,
                        (q, a) => new AccountStampHolderDto
                        {
                            AccountId = a.AccountId,
                            AccountName = a.AccountName,
                            NumTimesCompleted = (int)(q.NumTimesCompleted ?? 0),
                        })
                    .OrderBy(r => r.AccountName)
                    .ToList();

                result.AccountHolders = accountRows;
                result.AccountHolderCount = accountRows.Count;
            }

            using (var shard = new ShardDbContext())
            {
                var baseQuery = shard.CharacterPropertiesQuestRegistry
                    .AsNoTracking()
                    .Where(q => q.QuestName == resolved && q.NumTimesCompleted >= 1)
                    .Join(
                        shard.Character.AsNoTracking().Where(c => !c.IsDeleted),
                        q => q.CharacterId,
                        c => c.Id,
                        (q, c) => new { q, c });

                result.CharacterHolderCount = baseQuery.Count();

                var characterRows = baseQuery
                    .OrderBy(x => x.c.Name)
                    .Skip(offset)
                    .Take(limit)
                    .Select(x => new CharacterStampHolderDto
                    {
                        CharacterId = x.c.Id,
                        CharacterName = x.c.Name,
                        AccountId = x.c.AccountId,
                        NumTimesCompleted = x.q.NumTimesCompleted,
                        LastTimeCompletedUnix = x.q.LastTimeCompleted,
                    })
                    .ToList();

                foreach (var row in characterRows)
                    row.LastTimeCompletedUtc = FormatUnixTime(row.LastTimeCompletedUnix);

                result.CharacterHolders = characterRows;
                result.Limit = limit;
                result.Offset = offset;
            }

            return Ok(result);
        }

        private static string? ResolveStampName(string input, out List<string> candidates)
        {
            candidates = new List<string>();

            using var shard = new ShardDbContext();
            using var auth = new AuthDbContext();

            var exactShard = shard.CharacterPropertiesQuestRegistry
                .AsNoTracking()
                .Where(q => q.QuestName == input)
                .Select(q => q.QuestName)
                .FirstOrDefault();

            if (exactShard != null)
                return exactShard;

            var exactAuth = auth.AccountQuest
                .AsNoTracking()
                .Where(q => q.Quest == input)
                .Select(q => q.Quest)
                .FirstOrDefault();

            if (exactAuth != null)
                return exactAuth;

            var prefix = input + "@";
            candidates = shard.CharacterPropertiesQuestRegistry
                .AsNoTracking()
                .Where(q => q.QuestName.StartsWith(prefix))
                .Select(q => q.QuestName)
                .Union(auth.AccountQuest
                    .AsNoTracking()
                    .Where(q => q.Quest.StartsWith(prefix))
                    .Select(q => q.Quest))
                .Distinct()
                .OrderBy(n => n)
                .Take(20)
                .ToList();

            if (candidates.Count == 1)
                return candidates[0];

            return null;
        }

        private static string? FormatUnixTime(uint unix)
        {
            if (unix == 0)
                return null;

            try
            {
                return Time.GetDateTimeFromTimestamp(unix).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            }
            catch
            {
                return null;
            }
        }

        public class StampLookupResultDto
        {
            public string StampName { get; set; } = "";
            public int ServerTotalCompletions { get; set; }
            public int AccountHolderCount { get; set; }
            public int CharacterHolderCount { get; set; }
            public int Limit { get; set; }
            public int Offset { get; set; }
            public List<AccountStampHolderDto> AccountHolders { get; set; } = new();
            public List<CharacterStampHolderDto> CharacterHolders { get; set; } = new();
        }

        public class AccountStampHolderDto
        {
            public uint AccountId { get; set; }
            public string AccountName { get; set; } = "";
            public int NumTimesCompleted { get; set; }
        }

        public class CharacterStampHolderDto
        {
            public uint CharacterId { get; set; }
            public string CharacterName { get; set; } = "";
            public uint AccountId { get; set; }
            public int NumTimesCompleted { get; set; }
            public uint LastTimeCompletedUnix { get; set; }
            public string? LastTimeCompletedUtc { get; set; }
        }
    }
}
