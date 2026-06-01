using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ACE.Database.Models.Auth;
using ACE.Server.Entity;
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
            };

        /// <summary>PropertyInt player discipline stats (9044–9045).</summary>
        private static readonly Dictionary<string, string> DisciplineStatBoardSql =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["jails"] = LeaderboardInlineSql.TopTimesJailed,
                ["notguilty"] = LeaderboardInlineSql.TopUcmChecksPassed,
            };

        /// <summary>PropertyInt64 banked counters (9020 enlightened coins, 9027 weakly enlightened, 9021 mythic keys, 9015 legendary keys).</summary>
        private static readonly Dictionary<string, string> BankedInt64BoardSql =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["enlcoins"] = LeaderboardInlineSql.TopBankedEnlightenedCoins,
                ["wenlcoins"] = LeaderboardInlineSql.TopBankedWeaklyEnlightenedCoins,
                ["mkeys"] = LeaderboardInlineSql.TopBankedMythicalKeys,
                ["lkeys"] = LeaderboardInlineSql.TopBankedLegendaryKeys,
            };

        private static object MakeSelfRow(int rank, long score, string character, uint? account, string characterGuid, bool inTopList) =>
            new { rank, score, character = character ?? "", account, characterGuid, inTopList };

        [HttpGet("boards")]
        public IActionResult GetBoardCatalog()
        {
            var boards = BoardTitles
                .OrderBy(kv => kv.Key)
                .Select(kv => new { id = kv.Key, title = kv.Value })
                .ToList();
            return Ok(new { boards });
        }

        /// <summary>
        /// Pet / shiny leaderboard drill-down: registered captures for an account (any authenticated portal user).
        /// </summary>
        [HttpGet("pet-registry/{accountId:long}")]
        public IActionResult GetPetRegistryDetails(long accountId, [FromQuery] bool shiniesOnly = false)
        {
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
        public async Task<IActionResult> GetBoard(string boardId)
        {
            var norm = boardId?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(norm) || !BoardTitles.TryGetValue(norm, out var title))
            {
                return NotFound(new { message = "Unknown leaderboard.", code = "unknown_board" });
            }

            var me = CurrentAccountId;

            if (norm == "pets")
            {
                var list = PetRegistryManager.GetTopPets(25);
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
                    else if (PetRegistryManager.TryGetPetSpeciesPlacement(me.Value, shinyOnly: false, out var pr, out var pc, out var pn))
                        petsSelfRow = MakeSelfRow(pr, pc, pn, me.Value, null, false);
                }

                return Ok(new
                {
                    id = norm,
                    title,
                    nextRefreshApproxUtc = (DateTime?)null,
                    cached = false,
                    rows,
                    selfRow = petsSelfRow,
                });
            }

            if (norm == "shinies")
            {
                var list = PetRegistryManager.GetTopShinies(25);
                var rows = list.Select((t, i) => new
                {
                    rank = i + 1,
                    score = (long)t.Count,
                    character = t.CharacterName,
                    account = t.AccountId,
                    characterGuid = (string)null,
                    you = me.HasValue && t.AccountId == me.Value,
                }).ToList();

                object shiniesSelfRow = null;
                if (me.HasValue)
                {
                    var you = rows.FirstOrDefault(r => r.you);
                    if (you != null)
                        shiniesSelfRow = MakeSelfRow(you.rank, you.score, you.character, you.account, you.characterGuid, true);
                    else if (PetRegistryManager.TryGetPetSpeciesPlacement(me.Value, shinyOnly: true, out var pr, out var pc, out var pn))
                        shiniesSelfRow = MakeSelfRow(pr, pc, pn, me.Value, null, false);
                }

                return Ok(new
                {
                    id = norm,
                    title,
                    nextRefreshApproxUtc = (DateTime?)null,
                    cached = false,
                    rows,
                    selfRow = shiniesSelfRow,
                });
            }

            await using var context = new AuthDbContext();
            var cache = LeaderboardCache.Instance;

            if (BankedInt64BoardSql.TryGetValue(norm, out var bankedSql))
            {
                var bankedData = await cache.GetTopBankedInt64Async(context, norm, bankedSql);
                var bankedRows = bankedData.Select((x, i) => new
                {
                    rank = i + 1,
                    score = (long)(x.Score ?? 0),
                    character = x.Character ?? "",
                    account = x.Account,
                    characterGuid = x.LeaderboardId.ToString(),
                    you = me.HasValue && x.Account == me.Value,
                }).ToList();

                object bankedSelfRow = null;
                if (me.HasValue)
                {
                    var you = bankedRows.FirstOrDefault(r => r.you);
                    if (you != null)
                        bankedSelfRow = MakeSelfRow(you.rank, you.score, you.character, you.account, you.characterGuid, true);
                    else
                    {
                        var p = await Leaderboard.GetSelfPlacementRowAsync(context, norm, me.Value);
                        if (p != null)
                            bankedSelfRow = MakeSelfRow((int)p.PlacementRank, (long)(p.Score ?? 0), p.Character, p.Account, p.LeaderboardId.ToString(), false);
                    }
                }

                return Ok(new
                {
                    id = norm,
                    title,
                    nextRefreshApproxUtc = cache.GetBankedInt64NextUpdate(norm),
                    cached = true,
                    rows = bankedRows,
                    selfRow = bankedSelfRow,
                });
            }

            if (DisciplineStatBoardSql.TryGetValue(norm, out var disciplineSql))
            {
                var disciplineData = await cache.GetTopBankedInt64Async(context, norm, disciplineSql);
                var disciplineRows = disciplineData.Select((x, i) => new
                {
                    rank = i + 1,
                    score = (long)(x.Score ?? 0),
                    character = x.Character ?? "",
                    account = x.Account,
                    characterGuid = x.LeaderboardId.ToString(),
                    you = me.HasValue && x.Account == me.Value,
                }).ToList();

                object disciplineSelfRow = null;
                if (me.HasValue)
                {
                    var you = disciplineRows.FirstOrDefault(r => r.you);
                    if (you != null)
                        disciplineSelfRow = MakeSelfRow(you.rank, you.score, you.character, you.account, you.characterGuid, true);
                    else
                    {
                        var p = await Leaderboard.GetSelfPlacementRowAsync(context, norm, me.Value);
                        if (p != null)
                            disciplineSelfRow = MakeSelfRow((int)p.PlacementRank, (long)(p.Score ?? 0), p.Character, p.Account, p.LeaderboardId.ToString(), false);
                    }
                }

                return Ok(new
                {
                    id = norm,
                    title,
                    nextRefreshApproxUtc = cache.GetBankedInt64NextUpdate(norm),
                    cached = true,
                    rows = disciplineRows,
                    selfRow = disciplineSelfRow,
                });
            }

            List<Leaderboard> data = norm switch
            {
                "qb" => await cache.GetTopQBAsync(context),
                "level" => await cache.GetTopLevelAsync(context),
                "enl" => await cache.GetTopEnlAsync(context),
                "title" => await cache.GetTopTitleAsync(context),
                "augs" => await cache.GetTopAugsAsync(context),
                "deaths" => await cache.GetTopDeathsAsync(context),
                "bank" => await cache.GetTopBankAsync(context),
                "lum" => await cache.GetTopLumAsync(context),
                "attr" => await cache.GetTopAttrAsync(context),
                _ => new List<Leaderboard>(),
            };

            DateTime nextRefresh = norm switch
            {
                "qb" => cache.QBLastUpdate,
                "level" => cache.LevelLastUpdate,
                "enl" => cache.EnlLastUpdate,
                "title" => cache.TitleLastUpdate,
                "augs" => cache.AugsLastUpdate,
                "deaths" => cache.DeathsLastUpdate,
                "bank" => cache.BanksLastUpdate,
                "lum" => cache.LumLastUpdate,
                "attr" => cache.AttrLastUpdate,
                _ => DateTime.UtcNow,
            };

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
                    var p = await Leaderboard.GetSelfPlacementRowAsync(context, norm, me.Value);
                    if (p != null)
                        sqlSelfRow = MakeSelfRow((int)p.PlacementRank, (long)(p.Score ?? 0), p.Character, p.Account, p.LeaderboardId.ToString(), false);
                }
            }

            return Ok(new
            {
                id = norm,
                title,
                nextRefreshApproxUtc = nextRefresh,
                cached = true,
                rows = sqlRows,
                selfRow = sqlSelfRow,
            });
        }
    }
}
