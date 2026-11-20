using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    /// <summary>
    /// The portal's Bounties tab: every bounty Zone Control keeps - each zone's, the all-zones one, the regions' (with their item
    /// bounty odds), the mystery reward tables and the item bounty drop weights per place. Reading follows the Zone Control page.
    /// Changing needs Admin, the same as the /zonecontrol bounty commands, checked against the account's current level. Every
    /// change goes through the same edit the command uses, is logged in the change log (and so can be reverted there) and is
    /// announced on the audit channel.
    /// </summary>
    public partial class ZoneControlController
    {
        public const AccessLevel MinBountyEditLevel = AccessLevel.Admin;

        private bool CanEditBountiesNow()
        {
            var id = CurrentAccountId;
            if (id == null)
                return false;
            var account = DatabaseManager.Authentication.GetAccountById(id.Value);
            return account != null && (AccessLevel)account.AccessLevel >= MinBountyEditLevel && LevelMayEdit((AccessLevel)account.AccessLevel);
        }

        private const string BountyEditRefused = "Changing a bounty needs Admin access (the same as /zonecontrol bounty).";

        private static object BountyRow(BountyConfig cfg)
        {
            var copy = (cfg ?? new BountyConfig()).Clone();
            copy.EnsureIds();   // on the copy: reading never changes the live settings
            return new
            {
                copy.Enabled,
                copy.Active,
                Entries = copy.Entries.Select(e => new
                {
                    e.Id,
                    e.Wcid,
                    Name = RoomAssignManager.ItemName(e.Wcid),
                    e.Amount,
                    e.Kills,
                    Minutes = e.CooldownMinutes,
                    e.MinQb,
                }).ToList(),
            };
        }

        [HttpGet("bounties")]
        public IActionResult GetBounties()
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();

            return Run(() =>
            {
                var zones = ZoneControlManager.ReadAreas(areas => areas
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name))
                    .OrderBy(a => a.Variation).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(a => new { a.Name, a.Variation, a.Enabled, Bounty = a.Bounty?.Clone() })
                    .ToList())
                    .Select(a => new
                    {
                        a.Name,
                        a.Variation,
                        a.Enabled,
                        // the command refuses a zone below v11 (a retail layer); the page shows it, read-only
                        Editable = a.Variation >= VariationManager.EndgameMinVariation,
                        Bounty = BountyRow(a.Bounty),
                    }).ToList();

                var regions = ZoneControlManager.BountyRegions.Select(r =>
                {
                    var cfg = ZoneControlManager.GetRegionBounty(r.Key);
                    return new { r.Key, r.Name, Bounty = BountyRow(cfg), ItemOdds = cfg.ItemBountyOdds };
                }).ToList();

                var tables = ZoneControlManager.ListBountyRewardTables().Select(t => new
                {
                    t.Name,
                    TotalWeight = t.Table.Valid.Sum(e => (long)e.Weight),
                    Entries = t.Table.Entries.Select(e => new { e.Id, e.Wcid, Name = RoomAssignManager.ItemName(e.Wcid), e.Amount, e.Weight }).ToList(),
                }).ToList();

                // the drop pools: every bounty that can drop, plus any Zone Control has weights for that no longer can
                var places = ItemBounties.AllPlaces;
                var sets = ZoneControlManager.BountyPlaceWeightSnapshot();
                var table = ItemBounties.DropTable();
                var drops = table.Select(e =>
                {
                    sets.TryGetValue(e.Wcid, out var set);
                    return new
                    {
                        e.Wcid,
                        e.Name,
                        Own = e.Weight,
                        Region = string.IsNullOrEmpty(e.Region) ? null : e.Region,
                        e.MinTier,
                        e.MaxTier,
                        e.FixedTier,
                        CanDrop = true,
                        Off = set?.Off ?? false,
                        Set = set?.Places ?? new Dictionary<string, int>(),
                        Weights = places.ToDictionary(p => p, p => ItemBounties.CanDropAt(e, p) ? ItemBounties.WeightAt(e, p, set) : (int?)null),
                        // where its own weenie gives it a share (the server's rule, so the page never copies it): "Remove" elsewhere
                        // just takes back what Zone Control added
                        Natural = places.Where(p => ItemBounties.NaturalAt(e, p) && e.Weight > 0).ToList(),
                    };
                })
                .Concat(sets.Where(kv => table.All(e => e.Wcid != kv.Key)).Select(kv => new
                {
                    Wcid = kv.Key,
                    Name = RoomAssignManager.ItemName(kv.Key),
                    Own = 0,
                    Region = (string)null,
                    MinTier = 0,
                    MaxTier = 0,
                    FixedTier = false,
                    CanDrop = false,
                    kv.Value.Off,
                    Set = kv.Value.Places ?? new Dictionary<string, int>(),
                    Weights = places.ToDictionary(p => p, p => (int?)null),
                    Natural = new List<string>(),
                }))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();

                var totals = places.ToDictionary(p => p, p => ItemBounties.PoolAt(table, p, sets).Sum(x => (long)x.Weight));

                return Ok(new
                {
                    CanEdit = CanEditBountiesNow(),
                    Zones = zones,
                    ZoneWide = BountyRow(ZoneControlManager.GetZoneWideBounty()),
                    Regions = regions,
                    Tables = tables,
                    Places = places.Select(p => new { Key = p, Label = ItemBounties.PlaceLabel(p), Total = totals[p] }).ToList(),
                    Drops = drops,
                    Limits = new
                    {
                        BountyManager.MaxAmount,
                        BountyManager.MaxKills,
                        BountyManager.MaxCooldownMinutes,
                        BountyManager.MaxQb,
                        BountyConfig.MaxItemBountyOdds,
                        BountyRewardTable.MaxWeight,
                        BountyRewardTable.MaxEntries,
                    },
                });
            }, "bounties");
        }

        /// <summary>
        /// Bounty Items > Setup's server check: the item_bounty_* switches, every bounty weenie in the world database (broken
        /// terms, where it drops, where its reward can't be paid) and the drop table's state. A DB read, so the page asks for it
        /// apart from the main bounties read.
        /// </summary>
        [HttpGet("bounties/health")]
        public IActionResult GetBountyHealth()
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();
            return Run(() => Ok(HealthBody()), "bounty health");
        }

        private static object HealthBody()
        {
            var report = ItemBounties.Health();
            // the Bounty Ledger (#605): players start a bounty by using it on the ledger; 0 = ledgers off (two double-clicks),
            // and a wcid with no weenie behind it falls back to the same until the ledger SQL is imported
            var ledgerWcid = ItemBounties.LedgerWcid;
            var ledger = ledgerWcid == 0 ? null : DatabaseManager.World.GetCachedWeenie(ledgerWcid);
            return new
            {
                Enabled = ServerConfig.item_bounty_enabled.Value,
                MaxActiveSetting = ServerConfig.item_bounty_max_active.Value,
                MaxActive = ItemBounties.MaxActiveNow,
                MaxSlots = ItemBounties.MaxSlots,
                LedgerSetting = ServerConfig.item_bounty_ledger_wcid.Value,
                LedgerWcid = ledgerWcid,
                LedgerExists = ledger != null,
                LedgerName = ledger == null ? null : RoomAssignManager.ItemName(ledgerWcid),
                ConfirmSeconds = ServerConfig.item_bounty_confirm_seconds.Value,
                report.DropTableCount,
                report.DropTableAgeSeconds,
                report.Reading,
                report.RefreshSeconds,
                report.Weenies,
                // for the request builder's "one creature type" choice: every CreatureType, named as the bounty would name it
                CreatureTypes = CreatureTypeChoices.Value,
            };
        }

        /// <summary>
        /// Bounty Items' drop chance per tier: bounty_odds on each T11-T25 Default row, read the same way the stats page reads one row
        /// (what is stored there, what is in effect after the v11 anchor, where it comes from, a toggle). One request instead of 15
        /// whole stats reads (review 2026-11-19).
        /// </summary>
        [HttpGet("bounties/odds")]
        public IActionResult GetTierBountyOdds()
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();
            // one hold of the manager lock for all 15 tiers, so they describe the same moment
            return Run(() => ZoneControlManager.Locked<IActionResult>(() =>
            {
                var stat = ACE.Server.Managers.ZoneScaling.ZoneStat.BountyOdds;
                var tiers = Enumerable.Range(ItemBounties.MinTier, ItemBounties.MaxTier - ItemBounties.MinTier + 1).Select(v =>
                {
                    var target = ZoneStatTarget.ForDefault(v, ACE.Server.Managers.ZoneScaling.ZcRank.None);
                    var authored = ReadRow(target, out var tier) ?? new Dictionary<string, ACE.Server.Managers.ZoneScaling.StatCurve>();
                    authored.TryGetValue(stat, out var curve);
                    var eval = ZoneControlManager.EvaluateDefaultForDisplay(v, ACE.Server.Managers.ZoneScaling.ZcRank.None);
                    var inEffect = eval?.Values != null && eval.Values.ContainsKey(stat);
                    var source = ZoneControlManager.ResolveDefaultStatSource(v, ACE.Server.Managers.ZoneScaling.ZcRank.None, stat);
                    return new
                    {
                        Variation = v,
                        Value = curve?.Base,
                        Revision = ZoneStatExpectation.RevisionOf(curve),
                        Curved = curve != null && (curve.Growth != 1.0 || curve.Additive || curve.Overrides is { Count: > 0 }),
                        Effective = inEffect ? eval.GetT(stat, 0, tier) : (double?)null,
                        Source = source,
                        ToggledOff = !inEffect && source != null && IsTurnedOff(target, stat),
                    };
                }).ToList();
                return Ok(new { Tiers = tiers });
            }), "bounty odds");
        }

        private static readonly Lazy<object[]> CreatureTypeChoices = new(() => Enum.GetValues<ACE.Entity.Enum.CreatureType>()
            .Where(c => ItemBounties.IsHuntableSpecies((int)c))
            .Select(c => (object)new { Id = (int)c, Name = ACE.Server.Entity.GearEssences.SpeciesName(c) })
            .ToArray());

        /// <summary>
        /// The request builder's WCID check: does a weenie exist, its name, can it be paid as a reward (a carryable item), and
        /// is it already a bounty item. Reading access only; one cached-weenie read.
        /// </summary>
        [HttpGet("bounties/lookup")]
        public IActionResult LookupBountyWcid([FromQuery] uint wcid)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();
            return Run(() =>
            {
                var w = wcid == 0 ? null : ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
                var isBounty = w?.PropertiesInt != null && w.PropertiesInt.Keys.Any(k => k >= PropertyInt.ItemBountyTier && k <= PropertyInt.ItemBountyMystery);
                return Ok(new
                {
                    Wcid = wcid,
                    Exists = w != null,
                    Name = w == null ? null : RoomAssignManager.ItemName(wcid),
                    Givable = w != null && BountyManager.IsGivableItem(wcid),
                    IsBounty = isBounty,
                });
            }, "bounty lookup");
        }

        /// <summary>"Re-read now": fresh copies of the bounty weenies, then a new drop table read (waits up to 30 s each).</summary>
        [HttpPost("bounties/refresh")]
        public IActionResult RefreshBounties()
        {
            if (!HasPortalAccess(PortalPages.ZoneControl) || !CanEditBountiesNow())
                return StatusCode(403, new { Message = "Re-reading the bounty items needs Admin access." });
            return Run(() =>
            {
                var outcome = ItemBounties.RefreshNow();
                var body = HealthBody();
                var message = outcome switch
                {
                    ItemBounties.RefreshOutcome.Done => "Bounty items re-read from the world database.",
                    ItemBounties.RefreshOutcome.Failed => "The re-read FAILED (see the server log). Drops keep using the last good read, which the check shows.",
                    _ => "The re-read is still running; the check shows the last finished read.",
                };
                if (outcome == ItemBounties.RefreshOutcome.Failed)
                    return StatusCode(500, new { Message = message, Health = body });
                return Ok(new { Message = message, Health = body });
            }, "bounty refresh");
        }

        public class BountyEditRequest
        {
            /// <summary>zone | zonewide | region</summary>
            public string Kind { get; set; }
            /// <summary>The zone's name, or the region's key.</summary>
            public string Name { get; set; }
            /// <summary>on | off | add | set | remove | itemodds (regions only)</summary>
            public string Op { get; set; }
            public int? Id { get; set; }
            public uint? Wcid { get; set; }
            public int? Amount { get; set; }
            public int? Kills { get; set; }
            public double? Minutes { get; set; }
            public int? MinQb { get; set; }
            public int? Odds { get; set; }
            /// <summary>itemodds: the odds the page showed (0 = none). Refused when the region holds something else now; null = not checked.</summary>
            public int? ExpectedOdds { get; set; }
            public string Reason { get; set; }
        }

        [HttpPost("bounties/edit")]
        public IActionResult EditBounty([FromBody] BountyEditRequest request)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl) || !CanEditBountiesNow())
                return StatusCode(403, new { Message = BountyEditRefused });
            var op = (request?.Op ?? "").Trim().ToLowerInvariant();
            var kind = (request?.Kind ?? "").Trim().ToLowerInvariant();
            if (op is not ("on" or "off" or "add" or "set" or "remove" or "itemodds"))
                return BadRequest(new { Message = $"Unknown bounty action '{request?.Op}'." });
            if (op == "itemodds" && kind != "region")
                return BadRequest(new { Message = "Item bounty odds are set per region; T11+ zones use the bounty_odds stat." });

            return Run(() =>
            {
                var actor = PortalActor();
                var args = BountyEdits.EditArgs(op, request.Id, request.Wcid, request.Amount, request.Kills, request.Minutes, request.MinQb);
                string refused = null;
                BountyConfig after;
                string where;
                var oddsBefore = 0;

                Func<BountyConfig, string> change = c => BountyManager.Edit(c, op, args, 0);
                if (op == "itemodds")
                {
                    var odds = request.Odds ?? -1;
                    if (odds < 0 || odds > BountyConfig.MaxItemBountyOdds)
                        return BadRequest(new { Message = $"Item bounty odds: 1 in N kills, N from 1 to {BountyConfig.MaxItemBountyOdds:N0}; 0 = none drop." });
                    var expectedOdds = request.ExpectedOdds;
                    change = c =>
                    {
                        // like a tier's drop chance: a page left open never overwrites another admin's newer change (review 2026-11-19)
                        if (expectedOdds.HasValue && c.ItemBountyOdds != expectedOdds.Value)
                            return $"the drop chance changed since you loaded it: it is now {OddsText(c.ItemBountyOdds)}. Nothing was changed.";
                        oddsBefore = c.ItemBountyOdds;
                        c.ItemBountyOdds = odds;
                        return null;
                    };
                }

                using (ZoneControlChangeLog.ActingAs(actor))
                using (ZoneControlChangeLog.WithReason(request.Reason))
                {
                    switch (kind)
                    {
                        case "zone":
                        {
                            var zone = ZoneControlManager.ReadAreas(areas => areas.FirstOrDefault(a => string.Equals(a?.Name, request.Name, StringComparison.OrdinalIgnoreCase)));
                            if (zone == null)
                                return NotFound(new { Message = $"No zone '{request.Name}'." });
                            if (zone.Variation < VariationManager.EndgameMinVariation)
                                return BadRequest(new { Message = $"Bounty needs variation 11+ - '{zone.Name}' is on v{zone.Variation}, a retail layer. Nothing changed." });
                            where = $"'{zone.Name}' Bounty";
                            after = ZoneControlManager.EditBounty(zone.Name, change, out refused, VariationManager.EndgameMinVariation);
                            if (after == null)
                                return NotFound(new { Message = $"No zone '{request.Name}'." });
                            break;
                        }
                        case "zonewide":
                            where = "Zone-wide Bounty";
                            after = ZoneControlManager.EditZoneWideBounty(change, out refused);
                            break;
                        case "region":
                        {
                            var region = ZoneControlManager.FindBountyRegion(request.Name ?? "");
                            if (region == null)
                                return NotFound(new { Message = $"No bounty region '{request.Name}'." });
                            // itemodds is the region's bounty ITEM drop chance, not its kill-count Bounty
                            where = op == "itemodds" ? region.Value.Name : $"{region.Value.Name} Bounty";
                            after = ZoneControlManager.EditRegionBounty(region.Value.Key, change, out refused);
                            break;
                        }
                        default:
                            return BadRequest(new { Message = $"Unknown bounty kind '{request.Kind}': zone, zonewide or region." });
                    }
                }

                if (refused != null)
                    return Conflict(new { Message = $"{where}: {refused}" });

                var summary = op == "itemodds"
                    ? $"bounty item drop chance {OddsText(oddsBefore)} -> {OddsText(after.ItemBountyOdds)}"
                    : BountyManager.Describe(after);
                PlayerManager.BroadcastToAuditChannel(null, $"[portal] {actor.Name} {where} {op}{(op is "set" or "remove" ? " #" + request.Id : "")} -> {summary}"
                    + ReasonSuffix(request.Reason));
                return Ok(new { Message = $"{where} {summary}", Bounty = BountyRow(after), ItemOdds = after.ItemBountyOdds });
            }, "bounty edit");
        }

        /// <summary>" (reason)" for an audit line, cleaned to ASCII: the audit channel reaches the game client.</summary>
        private static string ReasonSuffix(string reason) => ZoneControlChangeLog.CleanReason(reason) is { } r ? $" ({r})" : "";

        private static string OddsText(int odds) => odds > 0 ? $"1 in {odds:N0} kills" : "none";

        public class RewardTableEditRequest
        {
            public string Name { get; set; }
            /// <summary>create | add | set | remove | delete</summary>
            public string Op { get; set; }
            public int? Id { get; set; }
            public uint? Wcid { get; set; }
            public int? Amount { get; set; }
            public int? Weight { get; set; }
            /// <summary>create only: every row of the new list, written in one go.</summary>
            public List<RewardRowRequest> Rows { get; set; }
            public string Reason { get; set; }
        }

        public class RewardRowRequest
        {
            public uint Wcid { get; set; }
            public int Amount { get; set; }
            public int Weight { get; set; }
        }

        [HttpPost("bounties/table")]
        public IActionResult EditRewardTable([FromBody] RewardTableEditRequest request)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl) || !CanEditBountiesNow())
                return StatusCode(403, new { Message = BountyEditRefused });
            var op = (request?.Op ?? "").Trim().ToLowerInvariant();
            var name = (request?.Name ?? "").Trim().ToLowerInvariant();
            if (!ZoneControlManager.IsValidRewardTableName(name))
                return BadRequest(new { Message = $"'{name}' isn't a table name: a-z, 0-9, _ and - (up to 32), optionally .t15 or .vod for one place." });

            return Run(() =>
            {
                var actor = PortalActor();
                using var acting = ZoneControlChangeLog.ActingAs(actor);
                using var why = ZoneControlChangeLog.WithReason(request.Reason);
                var reasonText = ReasonSuffix(request.Reason);

                if (op == "delete")
                {
                    var deleted = ZoneControlManager.GetBountyRewardTable(name);
                    if (!ZoneControlManager.DeleteBountyRewardTable(name))
                        return NotFound(new { Message = $"No bounty reward table '{name}'." });
                    var rows = (deleted?.Entries ?? new List<BountyRewardEntry>()).Select(e => $"#{e.Id} {BountyEdits.RewardRowText(e)}").ToList();
                    Log.Info($"[ZC] bountytable {name} deleted by {actor.Name} (portal): {(rows.Count == 0 ? "no rewards" : string.Join("; ", rows))}");
                    PlayerManager.BroadcastToAuditChannel(null, $"[portal] {actor.Name} bountytable {name} delete - it had {rows.Count} reward(s){reasonText}");
                    var fallback = name.Contains('.') ? name.Substring(0, name.IndexOf('.')) : null;
                    return Ok(new
                    {
                        Message = fallback != null
                            ? $"Bounty reward table '{name}' deleted. Bounties there now pay from '{fallback}' if it has a reward; they wait, unpaid, only while no list for that place can pay."
                            : $"Bounty reward table '{name}' deleted. Mystery bounties naming it pay from a one-place version ('{name}.t15' and the like) where one has a reward; elsewhere they wait, unpaid, until a list can pay.",
                    });
                }

                if (op == "create")
                {
                    // a whole new list in ONE store write (copying a list): every row is checked first, and a list that exists already
                    // is left alone, so payouts never come from half a copy (review 2026-11-19)
                    var newRows = request.Rows ?? new List<RewardRowRequest>();
                    if (newRows.Count == 0 || newRows.Count > BountyRewardTable.MaxEntries)
                        return BadRequest(new { Message = $"A new list needs 1 to {BountyRewardTable.MaxEntries} rewards." });
                    foreach (var r in newRows)
                        if (BountyEdits.CheckRewardRow(r.Wcid, r.Amount, r.Weight) is { } bad)
                            return BadRequest(new { Message = $"Bounty reward table '{name}': {bad} Nothing was created." });
                    var made = ZoneControlManager.EditBountyRewardTable(name, t =>
                    {
                        if (t.Entries.Count > 0)
                            return "it exists already. Nothing was created.";
                        foreach (var r in newRows)
                            t.Entries.Add(new BountyRewardEntry { Id = t.NextId++, Wcid = r.Wcid, Amount = r.Amount, Weight = r.Weight });
                        return null;
                    }, out var createRefused);
                    if (createRefused != null)
                        return Conflict(new { Message = $"Bounty reward table '{name}': {createRefused}" });
                    var madeRows = (made?.Entries ?? new List<BountyRewardEntry>()).Select(e => $"#{e.Id} {BountyEdits.RewardRowText(e)}").ToList();
                    PlayerManager.BroadcastToAuditChannel(null, $"[portal] {actor.Name} bountytable {name} create with {madeRows.Count} reward(s){reasonText}");
                    return Ok(new { Message = $"Bounty reward table '{name}' created with {madeRows.Count} reward(s)." });
                }

                if (op is "add" or "set")
                {
                    var problem = BountyEdits.CheckRewardRow(request.Wcid ?? 0, request.Amount ?? 0, request.Weight ?? 0);
                    if (problem != null)
                        return BadRequest(new { Message = $"Bounty reward table '{name}': {problem}" });
                }
                else if (op != "remove")
                    return BadRequest(new { Message = $"Unknown table action '{request.Op}': create, add, set, remove or delete." });

                var changedId = request.Id ?? 0;
                BountyRewardEntry rowBefore = null;
                Func<BountyRewardTable, string> change = op switch
                {
                    "add" => t =>
                    {
                        if (t.Entries.Count >= BountyRewardTable.MaxEntries) return $"a table holds at most {BountyRewardTable.MaxEntries} rewards.";
                        changedId = t.NextId++;
                        t.Entries.Add(new BountyRewardEntry { Id = changedId, Wcid = request.Wcid.Value, Amount = request.Amount.Value, Weight = request.Weight.Value });
                        return null;
                    },
                    "set" => t =>
                    {
                        var row = t.Entries.Find(e => e.Id == changedId);
                        if (row == null) return $"no reward #{changedId} - it was removed (by you or another admin).";
                        rowBefore = row.Clone();
                        row.Wcid = request.Wcid.Value; row.Amount = request.Amount.Value; row.Weight = request.Weight.Value;
                        return null;
                    },
                    _ => t =>
                    {
                        rowBefore = t.Entries.Find(e => e.Id == changedId)?.Clone();
                        return t.Entries.RemoveAll(e => e.Id == changedId) > 0 ? null : $"no reward #{changedId} - it was removed (by you or another admin).";
                    },
                };

                var after = ZoneControlManager.EditBountyRewardTable(name, change, out var refused);
                if (refused != null)
                    return Conflict(new { Message = $"Bounty reward table '{name}': {refused}" });
                var rowAfter = after?.Entries.Find(e => e.Id == changedId);
                var line = op switch
                {
                    "add" => $"add #{changedId}: {BountyEdits.RewardRowText(rowAfter)}",
                    "set" => $"set #{changedId}: {BountyEdits.RewardRowText(rowBefore)} -> {BountyEdits.RewardRowText(rowAfter)}",
                    _ => $"remove #{changedId}: {BountyEdits.RewardRowText(rowBefore)}",
                };
                PlayerManager.BroadcastToAuditChannel(null, $"[portal] {actor.Name} bountytable {name} {line}{reasonText}");
                return Ok(new { Message = $"Bounty reward table '{name}' {line}" });
            }, "reward table edit");
        }

        public class DropWeightEditRequest
        {
            public uint Wcid { get; set; }
            /// <summary>A weight 0..MaxWeight, or clear | off | on | reset.</summary>
            public string Action { get; set; }
            /// <summary>Places for a weight or clear: t15, t11-t15, t11,t13,vod, tiers, regions or all.</summary>
            public List<string> Places { get; set; }
            public string Reason { get; set; }
        }

        [HttpPost("bounties/weight")]
        public IActionResult EditDropWeight([FromBody] DropWeightEditRequest request)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl) || !CanEditBountiesNow())
                return StatusCode(403, new { Message = BountyEditRefused });
            if (request == null || request.Wcid == 0)
                return BadRequest(new { Message = "Give the bounty's WCID." });

            return Run(() =>
            {
                var actor = PortalActor();
                ItemBounties.WeightEditResult result;
                using (ZoneControlChangeLog.ActingAs(actor))
                using (ZoneControlChangeLog.WithReason(request.Reason))
                    result = ItemBounties.EditWeights(request.Wcid, request.Action, request.Places ?? new List<string>());
                if (result.Error != null)
                    return BadRequest(new { Message = result.Error });
                if (result.Audit != null)
                    PlayerManager.BroadcastToAuditChannel(null, $"[portal] {actor.Name} {result.Audit}"
                        + ReasonSuffix(request.Reason));
                // the first line says what happened; the bw and undo lines are for the command's console users
                return Ok(new { Message = result.Lines.FirstOrDefault() ?? "Done.", Lines = result.Lines.Where(l => !l.StartsWith("bw ", StringComparison.Ordinal)).ToList() });
            }, "drop weight edit");
        }
    }
}
