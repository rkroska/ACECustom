using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    /// <summary>
    /// Portal Zone Control page: read zones and tier Defaults, set and clear stats, and revert logged changes. Reading follows the
    /// "zone-control" portal page level. Editing also needs Developer, the same as /zonecontrol, checked against the account's
    /// CURRENT level on every edit: the login cookie carries the level from sign-in for up to 8 hours.
    /// </summary>
    [ApiController]
    [Route("api/zonecontrol")]
    public partial class ZoneControlController : BaseController
    {
        public const AccessLevel MinEditLevel = AccessLevel.Developer;

        internal static bool LevelMayEdit(AccessLevel level) =>
            level >= MinEditLevel && PortalAccessManager.CanAccess(level, PortalPages.ZoneControl);

        private bool CanEditNow()
        {
            var id = CurrentAccountId;
            if (id == null)
                return false;
            var account = DatabaseManager.Authentication.GetAccountById(id.Value);
            return account != null && LevelMayEdit((AccessLevel)account.AccessLevel);
        }

        private IActionResult Run(Func<IActionResult> action, string what)
        {
            try
            {
                // the change log rows this request's saves queue are waited for here, after the work has let go of the manager lock,
                // so History shows the change as soon as the page re-reads it (and a slow log never holds the game's readers)
                using (ZoneControlChangeLog.TrackWrites())
                {
                    var answer = action();
                    return ZoneControlChangeLog.WaitTracked() ? answer : NotLogged(answer);
                }
            }
            catch (Exception ex)
            {
                // the details stay in the server log: a database or config error must not reach every account that can read the page
                // (review 2026-11-19); refusals and conflicts are answered by the actions themselves, never here
                var correlationId = Guid.NewGuid().ToString();
                Log.Error($"[Correlation ID: {correlationId}] Zone Control {what} failed", ex);
                return StatusCode(500, new { Message = $"Zone Control {what} failed on the server. The server log has the details (id {correlationId}).", CorrelationId = correlationId });
            }
        }

        private static readonly System.Text.Json.JsonSerializerOptions WebJson = new(System.Text.Json.JsonSerializerDefaults.Web);

        /// <summary>
        /// A change that saved, but whose change log rows could not be written: the answer gets "logged": false and its message says so,
        /// so the page warns instead of showing a plain success (review 2026-11-19: bounty edits reported success while History missed
        /// them). Error answers pass through unchanged.
        /// </summary>
        private static IActionResult NotLogged(IActionResult answer)
        {
            if (answer is not ObjectResult result || result.Value == null || (result.StatusCode is int code && (code < 200 || code >= 300)))
                return answer;
            if (System.Text.Json.JsonSerializer.SerializeToNode(result.Value, result.Value.GetType(), WebJson) is not System.Text.Json.Nodes.JsonObject body)
                return answer;
            body["logged"] = false;
            const string note = "Saved, but the change log could not record it, so History can't revert it. Check the server log.";
            body["message"] = body["message"] is System.Text.Json.Nodes.JsonValue m && m.TryGetValue<string>(out var text) ? $"{text} ({note})" : note;
            return new OkObjectResult(body);
        }

        [HttpGet("overview")]
        public IActionResult GetOverview()
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();

            return Run(() =>
            {
                var zones = ZoneControlManager.ReadAreas(areas => areas
                    .OrderBy(a => a.Variation).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(a => new
                    {
                        a.Name,
                        a.Variation,
                        a.Enabled,
                        Landblocks = a.Landblocks?.Count ?? 0,
                        Stats = a.Profile?.Minion?.Stats?.Count ?? 0,
                        Wcids = (a.Profile?.WcidOverrides?.Keys ?? Enumerable.Empty<uint>()).OrderBy(w => w).ToList(),
                    }).ToList());

                var defaults = ZoneControlManager.ListVariationDefaults()
                    .Select(v => new
                    {
                        Variation = v,
                        Stats = ZoneControlManager.ReadVariationDefault(v, d => d?.Profile?.Stats?.Count ?? 0),
                    }).ToList();

                return Ok(new { Zones = zones, Defaults = defaults, CanEdit = CanEditNow(), Groups = ZoneStatCatalog.Groups });
            }, "overview");
        }

        /// <summary>Admin help for every known stat (ZoneStatHelp) and the "How Zone Control works" guide. A stat with no entry yet
        /// is left out; the page says so on that row.</summary>
        [HttpGet("help")]
        public IActionResult GetHelp()
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();

            return Run(() =>
            {
                var stats = ZoneStat.All
                    .Select(stat => (Stat: stat, Entry: ZoneStatHelp.For(stat, out var anchor), Anchor: anchor))
                    .Where(x => x.Entry != null)
                    .Select(x => new
                    {
                        x.Stat,
                        x.Entry.Summary,
                        x.Entry.Details,
                        x.Entry.Shape,
                        x.Entry.WhenUnset,
                        x.Entry.Raise,
                        x.Entry.Lower,
                        x.Entry.Range,
                        x.Entry.Caution,
                        x.Entry.Related,
                        Timing = x.Entry.Timing.ToString(),
                        TakesEffect = ZoneStatHelp.TakesEffect(x.Entry.Timing),
                        HowToUndo = ZoneStatHelp.HowToUndo(x.Entry.Timing),
                        AnchorNote = x.Anchor ? ZoneStatHelp.AnchorNote : null,
                    }).ToList();

                return Ok(new { Stats = stats, Guide = ZoneControlGuide.Sections, Total = ZoneStat.All.Length });
            }, "help");
        }

        [HttpGet("stats")]
        public IActionResult GetStats([FromQuery] string zone, [FromQuery] int? variation, [FromQuery] uint? wcid, [FromQuery] string rank)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();
            if (!TryTarget(zone, variation, wcid, rank, out var target, out var problem))
                return BadRequest(new { Message = problem });

            // the whole read under one hold of the manager lock (it is re-entrant): the stored row, its revision, the value in effect
            // and where it comes from all describe the same moment, never a game edit landing halfway (review 2026-11-19)
            return Run(() => ZoneControlManager.Locked<IActionResult>(() =>
            {
                var authored = ReadRow(target, out var tier);
                if (authored == null)
                    return NotFound(new { Message = $"No zone '{zone}'." });

                var eval = target.IsDefault
                    ? ZoneControlManager.EvaluateDefaultForDisplay(target.Variation, target.Rank)
                    : target.Rank != ZcRank.None
                        ? ZoneControlManager.ResolveZoneRankForDisplay(target.Zone, target.Rank)
                        : ZoneControlManager.EvaluateForDisplay(target.Zone, target.Wcid);

                // every known stat, plus any stored key that is no longer one, so a dead key can still be seen and cleared
                var keys = ZoneStat.All.Concat(authored.Keys.Where(k => !ZoneStat.All.Contains(k, StringComparer.OrdinalIgnoreCase)));
                var rows = keys.Select(stat =>
                {
                    authored.TryGetValue(stat, out var curve);
                    var hasEffective = eval?.Values != null && eval.Values.ContainsKey(stat);
                    var source = target.IsDefault
                        ? ZoneControlManager.ResolveDefaultStatSource(target.Variation, target.Rank, stat)
                        : ZoneControlManager.ResolveStatSource(target.Zone, target.Wcid, stat, target.Rank);
                    var turnedOff = !hasEffective && source != null && IsTurnedOff(target, stat);
                    return new
                    {
                        Stat = stat,
                        Label = ZoneStatCatalog.LabelOf(stat),
                        Group = ZoneStatCatalog.GroupOf(stat),
                        Known = ZoneStatEditor.NormalizeStat(stat) != null,
                        Value = curve?.Base,
                        // the whole stored curve's fingerprint: sent back with an edit, so a growth or tier pin added since is not overwritten
                        Revision = ZoneStatExpectation.RevisionOf(curve),
                        // Set replaces the whole curve, so the page warns before overwriting growth or tier pins
                        Curved = curve != null && (curve.Growth != 1.0 || curve.Additive || curve.Overrides is { Count: > 0 }),
                        Effective = hasEffective ? eval.GetT(stat, 0, tier) : (double?)null,
                        Source = source,
                        // a togglestat off at this scope. Set-but-absent is NOT enough: a _t25 twin dropped by the shadow rule
                        // (a closer row sets the T11 value alone) is absent too, and used to show as "turned off" (review 2026-11-11).
                        ToggledOff = turnedOff,
                        Shadowed = !hasEffective && source != null && !turnedOff,
                    };
                }).ToList();

                return Ok(new { Tier = tier, Rows = rows });
            }), "stats read");
        }

        /// <summary>True when the stat (or, for a _t25 key, the anchor itself) is set somewhere in the stack but turned off.</summary>
        private static bool IsTurnedOff(ZoneStatTarget target, string stat)
        {
            var ladder = ZoneControlManager.BuildStatLadder(target, stat);
            if (ladder == null)
                return false;
            return stat.EndsWith(ZoneStatHelp.AnchorSuffix, StringComparison.OrdinalIgnoreCase)
                ? ladder.Rungs.Any(r => r.AnchorState == ZoneStatRungState.Off)
                : ladder.TurnedOff;
        }

        /// <summary>One stat's whole stack at this place: every row that could set it, most specific first, what each holds and
        /// which one the game uses. Loaded when a stat is opened on the page, not for every stat.</summary>
        [HttpGet("stats/ladder")]
        public IActionResult GetStatLadder([FromQuery] string zone, [FromQuery] int? variation, [FromQuery] uint? wcid, [FromQuery] string rank, [FromQuery] string stat)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();
            if (!TryTarget(zone, variation, wcid, rank, out var target, out var problem))
                return BadRequest(new { Message = problem });
            if (string.IsNullOrWhiteSpace(stat))
                return BadRequest(new { Message = "No stat named." });
            var key = ZoneStatEditor.NormalizeStat(stat) ?? stat.Trim().ToLowerInvariant();

            return Run(() =>
            {
                var ladder = ZoneControlManager.BuildStatLadder(target, key);
                if (ladder == null)
                    return NotFound(new { Message = $"No zone '{zone}'." });
                return Ok(new
                {
                    ladder.Stat,
                    ladder.InEffect,
                    ladder.TurnedOff,
                    ladder.Anchored,
                    Rungs = ladder.Rungs.Select(r => new
                    {
                        r.Label,
                        r.Layer,
                        r.Variation,
                        r.Rank,
                        r.IsTarget,
                        r.Value,
                        r.Curved,
                        State = r.State.ToString(),
                        r.Anchor,
                        AnchorState = r.AnchorState.ToString(),
                        r.Toggle,
                    }).ToList(),
                });
            }, "ladder read");
        }

        public sealed class StatEditRequest
        {
            public string Zone { get; set; }
            public int? Variation { get; set; }
            public uint? Wcid { get; set; }
            public string Rank { get; set; }
            public string Stat { get; set; }
            /// <summary>Text, read by the same parser as the chat command. Ignored by clear.</summary>
            public string Value { get; set; }
            /// <summary>The value the editor was looking at; null = it was not set. The edit is refused if the row has moved on.</summary>
            public double? Expected { get; set; }
            /// <summary>The row's Revision as loaded (the whole curve); null = only Expected is checked.</summary>
            public string ExpectedRevision { get; set; }
            /// <summary>Why, as typed by the editor. Optional; stored in the change log (plain ASCII, cut to length).</summary>
            public string Reason { get; set; }
            /// <summary>Preview only: true previews a clear instead of a set.</summary>
            public bool Clear { get; set; }
        }

        /// <summary>What a set or clear WOULD do, without saving: the old and new value, and every zone the row reaches with the kinds of
        /// monster whose value changes at that zone's tier. Same checks as the real edit, so a refused edit is refused here too.</summary>
        [HttpPost("stat/preview")]
        public IActionResult PreviewStat([FromBody] StatEditRequest request)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();
            if (request == null)
                return BadRequest(new { Message = "No edit sent." });
            if (!TryTarget(request.Zone, request.Variation, request.Wcid, request.Rank, out var target, out var problem))
                return BadRequest(new { Message = problem });

            StatCurve next = null;
            string stat;
            if (request.Clear)
            {
                stat = ZoneStatEditor.NormalizeStat(request.Stat) ?? request.Stat?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(stat))
                    return BadRequest(new { Message = "No stat named." });
            }
            else
            {
                stat = ZoneStatEditor.NormalizeStat(request.Stat);
                if (stat == null)
                    return BadRequest(new { Message = "Unknown stat." });
                if (!ZoneStatEditor.TryParseValue(request.Value, out var value))
                    return BadRequest(new { Message = "value must be a number." });
                next = new StatCurve { Base = value, Growth = 1.0, Additive = false };
            }

            return Run(() =>
            {
                var impact = ZoneControlManager.PreviewStatEdit(target, stat, next, ZoneStatExpectation.Loaded(request.Expected, request.ExpectedRevision));
                if (impact.Status == ZoneStatEditStatus.NoSuchZone)
                    return NotFound(new { impact.Message });
                var help = ZoneStatHelp.For(stat);
                return Ok(new
                {
                    Status = impact.Status.ToString(),
                    impact.Message,
                    impact.Stat,
                    OldValue = impact.Old?.Base,
                    NewValue = impact.New?.Base,
                    // Set replaces the whole curve: say so when growth or pins would be lost
                    LosesCurve = impact.Old != null && (impact.Old.Growth != 1.0 || impact.Old.Additive || impact.Old.Overrides is { Count: > 0 }),
                    TakesEffect = help == null ? null : ZoneStatHelp.TakesEffect(help.Timing),
                    impact.Zones,
                });
            }, "preview");
        }

        [HttpPost("stat/set")]
        public IActionResult SetStat([FromBody] StatEditRequest request)
        {
            if (!CanEditNow())
                return Forbid();
            if (request == null)
                return BadRequest(new { Message = "No edit sent." });
            if (!TryTarget(request.Zone, request.Variation, request.Wcid, request.Rank, out var target, out var problem))
                return BadRequest(new { Message = problem });
            if (!ZoneStatEditor.TryParseValue(request.Value, out var value))
                return BadRequest(new { Message = "value must be a number." });

            return Run(() => Answer(target, ZoneStatEditor.Set(target, request.Stat, value, PortalActor(), ZoneStatExpectation.Loaded(request.Expected, request.ExpectedRevision), request.Reason), request.Reason), "edit");
        }

        [HttpPost("stat/clear")]
        public IActionResult ClearStat([FromBody] StatEditRequest request)
        {
            if (!CanEditNow())
                return Forbid();
            if (request == null)
                return BadRequest(new { Message = "No edit sent." });
            if (!TryTarget(request.Zone, request.Variation, request.Wcid, request.Rank, out var target, out var problem))
                return BadRequest(new { Message = problem });

            return Run(() => Answer(target, ZoneStatEditor.Clear(target, request.Stat, PortalActor(), ZoneStatExpectation.Loaded(request.Expected, request.ExpectedRevision), request.Reason), request.Reason), "edit");
        }

        [HttpGet("changes")]
        public IActionResult GetChanges([FromQuery] string zone, [FromQuery] int? variation, [FromQuery] int limit = 100)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();

            return Run(() => Ok(ZoneControlChangeLog.List(zone, variation, limit).Select(ChangeRow).ToList()), "change log read");
        }

        private static object ChangeRow(ZoneControlChange c) => new
        {
                c.Id,
                ChangedAtUtc = DateTime.SpecifyKind(c.ChangedAtUtc, DateTimeKind.Utc),
                c.Actor,
                c.Source,
                c.Zone,
                c.Variation,
                c.Wcid,
                c.Rank,
                c.Stat,
                OldValue = c.OldCurve?.Base,
                NewValue = c.NewCurve?.Base,
                c.RevertsId,
                c.Kind,
                c.Reason,
                c.BatchId,
                c.Section,
                // a whole-part change: what moved inside it, as short "path: old -> new" lines
                Details = c.Kind == ZoneControlChangeLog.KindSection ? ZoneControlManager.DescribeSectionChange(c.OldJson, c.NewJson) : null,
                Revertible = c.Kind != ZoneControlChangeLog.KindSection || c.Section != ZoneControlManager.SectionLadder,
        };

        /// <summary>The same rows as GET changes, in groups: one save, or one person's edits within a few minutes of each other.</summary>
        [HttpGet("changes/groups")]
        public IActionResult GetChangeGroups([FromQuery] string zone, [FromQuery] int? variation, [FromQuery] int limit = 300)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();

            return Run(() => Ok(ZoneControlRevert.Group(ZoneControlChangeLog.List(zone, variation, limit)).Select(g => new
            {
                g.Key,
                g.Actor,
                g.Source,
                FromUtc = DateTime.SpecifyKind(g.FromUtc, DateTimeKind.Utc),
                ToUtc = DateTime.SpecifyKind(g.ToUtc, DateTimeKind.Utc),
                Reasons = g.Changes.Select(c => c.Reason).Where(r => r != null).Distinct().ToList(),
                Changes = g.Changes.Select(ChangeRow).ToList(),
            }).ToList()), "change log read");
        }

        public sealed class GroupRevertRequest
        {
            public List<long> Ids { get; set; }
            public string Reason { get; set; }
            /// <summary>Revert all only: the ids the preview said it would revert, as confirmed. Anything else is left alone.</summary>
            public List<long> Approved { get; set; }
        }

        private const int GroupRevertMax = 500;

        /// <summary>
        /// Whether a logged change touches bounty settings, which need Admin to change (review 2026-11-19: History's Revert let a
        /// Developer undo a bounty change the bounty pages would refuse them). The global bounty parts, and a zone part whose Bounty
        /// block differs between its two sides.
        /// </summary>
        internal static bool TouchesBounties(ZoneControlChange c)
        {
            if (c.Kind != ZoneControlChangeLog.KindSection || c.Section == null)
                return false;
            if (c.Section.StartsWith("bounty:", StringComparison.Ordinal))
                return true;
            if (!c.Section.StartsWith("zone:", StringComparison.Ordinal))
                return false;
            static string BountyOf(string json) => json == null ? null
                : ZoneControlManager.Prop(Newtonsoft.Json.Linq.JToken.Parse(json), nameof(ControlledArea.Bounty)) is { } b && b.Type != Newtonsoft.Json.Linq.JTokenType.Null
                    ? ZoneControlManager.Canonical(b.ToString(Newtonsoft.Json.Formatting.None)) : null;
            return BountyOf(c.OldJson) != BountyOf(c.NewJson);
        }

        private const string BountyRevertRefused = "That change includes bounty settings, which need Admin access to change. Nothing was changed.";

        /// <summary>The rows sent plus every other row saved together with them (same batch id). History lists one zone's rows only and
        /// is cut at a row limit, so a group can hold half of one save: a rename's "created New" without its "removed Old". Reverting
        /// that half alone deletes the zone (review 2026-11-12). A save is undone whole or not at all. Added = the rows pulled in.</summary>
        private static (List<ZoneControlChange> All, List<ZoneControlChange> Added) WithWholeSaves(List<long> ids)
        {
            var rows = ZoneControlChangeLog.GetMany(ids);
            var have = rows.Select(r => r.Id).ToHashSet();
            var added = ZoneControlChangeLog.GetBatches(rows.Select(r => r.BatchId).ToList()).Where(r => have.Add(r.Id)).ToList();
            return (rows.Concat(added).OrderByDescending(r => r.Id).ToList(), added);
        }

        /// <summary>What Revert all would do with these changes: which revert, which are skipped and why. Writes nothing.</summary>
        [HttpPost("changes/revert-group/preview")]
        public IActionResult PreviewGroupRevert([FromBody] GroupRevertRequest request)
        {
            if (!HasPortalAccess(PortalPages.ZoneControl))
                return Forbid();
            if (request?.Ids == null || request.Ids.Count == 0 || request.Ids.Count > GroupRevertMax)
                return BadRequest(new { Message = $"Send 1-{GroupRevertMax} change ids." });

            return Run(() =>
            {
                var (all, added) = WithWholeSaves(request.Ids.Distinct().ToList());
                var plan = ZoneControlRevert.Preview(all);
                return Ok(new
                {
                    Revert = plan.Revert.Select(c => c.Id).ToList(),
                    Skipped = plan.Skipped.Select(s => new { s.Change.Id, s.Change.Stat, s.Why }).ToList(),
                    // rows saved together with the group's rows but not listed in it (another zone's half of a rename, say)
                    Added = added.Select(ChangeRow).ToList(),
                });
            }, "revert preview");
        }

        /// <summary>Reverts every change in a group, newest first, each with the same check as a single Revert; changes that moved on
        /// since are skipped. One hold of the manager lock, so nothing lands in between. Every revert is logged under one new batch.</summary>
        [HttpPost("changes/revert-group")]
        public IActionResult RevertGroup([FromBody] GroupRevertRequest request)
        {
            if (!CanEditNow())
                return Forbid();
            if (request?.Ids == null || request.Ids.Count == 0 || request.Ids.Count > GroupRevertMax)
                return BadRequest(new { Message = $"Send 1-{GroupRevertMax} change ids." });

            return Run(() =>
            {
                var actor = PortalActor();
                var all = WithWholeSaves(request.Ids.Distinct().ToList()).All;
                if (all.Any(TouchesBounties) && !CanEditBountiesNow())
                    return StatusCode(403, new { Message = BountyRevertRefused });
                var result = ZoneControlRevert.RevertAll(all, actor, request.Reason, request.Approved);
                if (result.Reverted.Count > 0)
                    PlayerManager.BroadcastToAuditChannel(null, $"[portal] {actor.Name} zonecontrol reverted {result.Reverted.Count} changes together"
                        + (result.Skipped.Count > 0 ? $", skipped {result.Skipped.Count}" : "")
                        + (result.StoppedAt.HasValue ? $", stopped at change {result.StoppedAt}" : "")
                        + (ZoneControlChangeLog.CleanReason(request.Reason) is { } why ? $" - {why}" : ""));
                return Ok(new
                {
                    result.Reverted,
                    Skipped = result.Skipped.Select(s => new { s.Id, s.Why }).ToList(),
                    result.StoppedAt,
                    result.StopReason,
                    result.Logged,
                });
            }, "revert group");
        }

        /// <summary>Puts one logged change back, as a new logged change. Refused if the stat has moved on since, so it can never
        /// silently undo a later edit.</summary>
        [HttpPost("changes/{id:long}/revert")]
        public IActionResult RevertChange(long id)
        {
            if (!CanEditNow())
                return Forbid();

            return Run(() =>
            {
                var change = ZoneControlChangeLog.Get(id);
                if (change == null)
                    return NotFound(new { Message = "No such change." });
                if (TouchesBounties(change) && !CanEditBountiesNow())
                    return StatusCode(403, new { Message = BountyRevertRefused });
                if (change.Kind == ZoneControlChangeLog.KindSection)
                {
                    // a zone created or removed in the same save as other changes is half of a rename (or a clone): reverting it alone
                    // leaves a zone missing, or two zones on the same landblocks
                    if ((change.OldJson == null || change.NewJson == null) && ZoneControlChangeLog.CountBatch(change.BatchId) > 1)
                        return BadRequest(new { Message = $"{change.Stat}{(change.Zone != null ? " '" + change.Zone + "'" : "")} was saved together with other changes (a rename does this). Use Revert all on its group so they go back together." });
                    var actor = PortalActor();
                    var done = ZoneControlManager.RevertSection(change, actor);
                    switch (done.Status)
                    {
                        case ZoneStatEditStatus.Applied:
                            PlayerManager.BroadcastToAuditChannel(null, $"[portal] {actor.Name} zonecontrol reverted change {change.Id} ({change.Stat}{(change.Zone != null ? " '" + change.Zone + "'" : "")})");
                            return Ok(new { change.Stat, OldValue = (double?)null, NewValue = (double?)null, done.Logged });
                        case ZoneStatEditStatus.Conflict:
                            return Conflict(new { done.Message });
                        default:
                            return BadRequest(new { done.Message });
                    }
                }
                if (!ZoneRank.TryParse(change.Rank, out var rank))
                    return BadRequest(new { Message = $"Unknown rank '{change.Rank}' on that change." });

                var target = change.Zone != null
                    ? ZoneStatTarget.ForZone(change.Zone, change.Wcid, rank)
                    : ZoneStatTarget.ForDefault(change.Variation ?? -1, rank);
                var expected = ZoneStatExpectation.Exactly(change.NewCurve);
                return Answer(target, ZoneStatEditor.Restore(target, change.Stat, change.OldCurve, PortalActor(), expected, change.Id));
            }, "revert");
        }

        private IActionResult Answer(ZoneStatTarget target, ZoneStatEditResult result, string reason = null)
        {
            switch (result.Status)
            {
                case ZoneStatEditStatus.Applied:
                    PlayerManager.BroadcastToAuditChannel(null, $"[portal] {PortalActor().Name} zonecontrol {target.Describe()} {result.Stat}: {Show(result.Old)} -> {Show(result.New)}"
                        + (ZoneControlChangeLog.CleanReason(reason) is { } why ? $" - {why}" : ""));
                    return Ok(new { result.Stat, OldValue = result.Old?.Base, NewValue = result.New?.Base, result.Logged });
                case ZoneStatEditStatus.NoSuchZone:
                    return NotFound(new { result.Message });
                case ZoneStatEditStatus.Conflict:
                    return Conflict(new { result.Message });
                default:
                    return BadRequest(new { result.Message });
            }
        }

        private static string Show(StatCurve curve) => curve == null ? "not set" : curve.Base.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        private ZoneStatActor PortalActor() => new ZoneStatActor { Name = User.Identity?.Name ?? ("account " + CurrentAccountId), Source = ZoneStatActor.SourcePortal };

        private static bool TryTarget(string zone, int? variation, uint? wcid, string rankText, out ZoneStatTarget target, out string problem)
        {
            target = null;
            problem = null;

            var rank = ZcRank.None;
            if (!string.IsNullOrWhiteSpace(rankText) && !ZoneRank.TryParse(rankText, out rank))
            {
                problem = $"Unknown rank '{rankText}'. Use default | regular | leader | boss.";
                return false;
            }
            if (wcid.HasValue && rank != ZcRank.None)
            {
                problem = ZoneStatEditor.WcidRankRefusal;
                return false;
            }
            if (string.IsNullOrWhiteSpace(zone) == (variation == null))
            {
                problem = "Name a zone or a variation.";
                return false;
            }
            if (variation != null)
            {
                if (variation < 0) { problem = "variation must be >= 0."; return false; }
                if (wcid.HasValue) { problem = "A tier Default has no monster overrides."; return false; }
                target = ZoneStatTarget.ForDefault(variation.Value, rank);
            }
            else
                target = ZoneStatTarget.ForZone(zone, wcid, rank);
            return true;
        }

        /// <summary>A copy of the stats authored on exactly this row, or null when the zone doesn't exist. tier = the variation in play.</summary>
        private static Dictionary<string, StatCurve> ReadRow(ZoneStatTarget target, out int tier)
        {
            if (target.IsDefault)
            {
                tier = target.Variation;
                return ZoneControlManager.ReadVariationDefault(target.Variation, d => CopyStats(d?.Profile?.RankLayer(target.Rank, create: false)));
            }

            var zoneTier = 0;
            var copy = ZoneControlManager.ReadArea(target.Zone, a =>
            {
                zoneTier = a.Variation;
                return CopyStats(ZoneStatEditor.RowFor(a.Profile, target.Wcid, target.Rank, create: false, out _));
            });
            tier = zoneTier;
            return copy;
        }

        private static Dictionary<string, StatCurve> CopyStats(ZoneVariantProfile row)
        {
            var copy = new Dictionary<string, StatCurve>(StringComparer.OrdinalIgnoreCase);
            if (row?.Stats != null)
                foreach (var kv in row.Stats)
                    copy[kv.Key] = kv.Value.Clone();
            return copy;
        }
    }
}
