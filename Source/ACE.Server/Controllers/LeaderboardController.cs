using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ACE.Database.Models.Auth;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Controllers
{
    public class LeaderboardController : BaseController
    {
        private static readonly Dictionary<string, string> BoardTitles =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["qb"] = "Quest bonuses",
                ["level"] = "Character level",
                ["enl"] = "Enlightenment",
                ["title"] = "Titles",
                ["augs"] = "Augmentations",
                ["deaths"] = "Deaths",
                ["bank"] = "Bank value",
                ["lum"] = "Banked luminance",
                ["attr"] = "Attributes",
                ["enlcoins"] = "Banked enlightened coins",
                ["wenlcoins"] = "Banked weakly enlightened coins",
                ["mkeys"] = "Banked mythic keys",
                ["lkeys"] = "Banked legendary keys",
                ["pets"] = "Pet species",
                ["shinies"] = "Shiny pets",
                ["jails"] = "Times jailed",
                ["notguilty"] = "Not guilty (focus checks passed)",
                ["bond"] = "Max pet bond",
                ["sumbond"] = "Sum pet bonds",
                ["potency"] = "Max pet potency",
                ["mutations"] = "Most mutated pet",
                ["summutations"] = "Total pet mutations",
                ["litters"] = "Litters bred",
            };

        private static object MakeSelfRow(int rank, long score, string character, uint? account, string characterGuid, bool inTopList) =>
            new { rank, score, character = character ?? "", account, characterGuid, inTopList };

        [HttpGet("boards")]
        public IActionResult GetBoardCatalog()
        {
            if (!HasPortalAccess(PortalPages.Leaderboards))
                return Forbid();

            var boards = BoardTitles
                .OrderBy(kv => kv.Key)
                .Select(kv => new { id = kv.Key, title = kv.Value })
                .ToList();
            var defaultDays = ActiveWindow.LeaderboardDefault;
            return Ok(new { boards, activeDays = defaultDays, activeDayOptions = ActiveWindow.Options(defaultDays) });
        }

        /// <summary>
        /// Pet / shiny leaderboard drill-down: registered captures for an account (any authenticated portal user).
        /// </summary>
        [HttpGet("pet-registry/{accountId:long}")]
        public IActionResult GetPetRegistryDetails(long accountId, [FromQuery] bool shiniesOnly = false)
        {
            if (!HasPortalAccess(PortalPages.Leaderboards))
                return Forbid();

            if (accountId < 0 || accountId > uint.MaxValue)
                return BadRequest(new { message = "Invalid account id.", code = "invalid_account_id" });

            var id = (uint)accountId;
            var entries = PetRegistryManager.GetFullPetRegistry(id);
            if (shiniesOnly)
                entries = entries.Where(e => e.IsShiny).ToList();

            entries = entries
                .OrderBy(e => e.CreatureName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.IsShiny)
                .ToList();

            var rows = entries.Select(e => new
            {
                e.Wcid,
                creatureName = e.CreatureName,
                creatureType = e.CreatureType?.ToString(),
                e.IsShiny,
                registeredAt = e.RegisteredAt.ToUniversalTime().ToString("o"),
            }).ToList();

            return Ok(new { accountId = id, shiniesOnly, entries = rows });
        }

        [HttpGet("{boardId}")]
        public async Task<IActionResult> GetBoard(string boardId, [FromQuery] int? days = null)
        {
            if (!HasPortalAccess(PortalPages.Leaderboards))
                return Forbid();

            var norm = boardId?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(norm))
                return NotFound(new { message = "Unknown leaderboard.", code = "unknown_board" });

            if (norm is "bonds")
                norm = "bond";
            else if (norm is "sumbonds")
                norm = "sumbond";
            else if (norm is "potency" || norm is "toppotency")
                norm = "potency";

            if (!BoardTitles.TryGetValue(norm, out var title))
            {
                return NotFound(new { message = "Unknown leaderboard.", code = "unknown_board" });
            }

            var defaultDays = ActiveWindow.LeaderboardDefault;
            if (!ActiveWindow.TryResolve(days, defaultDays, out var activeDays))
                return BadRequest(new { message = "Unsupported activity window.", code = "invalid_days", options = ActiveWindow.Options(defaultDays) });

            var options = ActiveWindow.Options(defaultDays);
            var me = CurrentAccountId;

            if (norm == "pets" || norm == "shinies")
            {
                var shinyOnly = norm == "shinies";
                // list and placement from one ranked read, so the viewer's rank cannot clash with the list
                var (list, placement) = PetRegistryManager.GetPetSpeciesBoard(shinyOnly, 25, activeDays, me);
                var rows = list.Select((t, i) => new
                {
                    rank = i + 1,
                    score = (long)t.Count,
                    character = t.CharacterName,
                    account = t.AccountId,
                    characterGuid = (string)null,
                    you = me.HasValue && t.AccountId == me.Value,
                }).ToList();

                object petsSelfRow = null;
                if (me.HasValue)
                {
                    var you = rows.FirstOrDefault(r => r.you);
                    if (you != null)
                        petsSelfRow = MakeSelfRow(you.rank, you.score, you.character, you.account, you.characterGuid, true);
                    else if (placement.HasValue)
                        petsSelfRow = MakeSelfRow(placement.Value.Rank, placement.Value.Count, placement.Value.CharacterName, me.Value, null, false);
                }

                return Ok(new
                {
                    id = norm,
                    title,
                    nextRefreshApproxUtc = (DateTime?)null,
                    cached = false,
                    activeDays,
                    activeDayOptions = options,
                    rows,
                    selfRow = petsSelfRow,
                });
            }

            if (!LeaderboardInlineSql.TopBoards.ContainsKey(norm))
                return NotFound(new { message = "Unknown leaderboard.", code = "unknown_board" });

            await using var context = new AuthDbContext();
            var cache = LeaderboardCache.Instance;
            var data = await cache.GetTopAsync(context, norm, activeDays);

            var sqlRows = data.Select((x, i) => new
            {
                rank = i + 1,
                score = (long)(x.Score ?? 0),
                character = x.Character ?? "",
                account = x.Account,
                characterGuid = x.LeaderboardId.ToString(),
                you = me.HasValue && x.Account == me.Value,
            }).ToList();

            object sqlSelfRow = null;
            if (me.HasValue)
            {
                var you = sqlRows.FirstOrDefault(r => r.you);
                if (you != null)
                    sqlSelfRow = MakeSelfRow(you.rank, you.score, you.character, you.account, you.characterGuid, true);
                else
                {
                    var p = await Leaderboard.GetSelfPlacementRowAsync(context, norm, me.Value, activeDays);
                    if (p != null)
                        sqlSelfRow = MakeSelfRow((int)p.PlacementRank, (long)(p.Score ?? 0), p.Character, p.Account, p.LeaderboardId.ToString(), false);
                }
            }

            return Ok(new
            {
                id = norm,
                title,
                nextRefreshApproxUtc = cache.GetNextUpdate(norm, activeDays),
                cached = true,
                activeDays,
                activeDayOptions = options,
                rows = sqlRows,
                selfRow = sqlSelfRow,
            });
        }
    }
}
