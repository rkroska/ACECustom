using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.ZoneScaling;

using Newtonsoft.Json.Linq;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>A run of changes made together: one save (same batch id), or one person's edits close together in time.</summary>
    public sealed class ZoneControlChangeGroup
    {
        public string Key { get; init; }
        public string Actor { get; init; }
        public string Source { get; init; }
        public DateTime FromUtc { get; init; }
        public DateTime ToUtc { get; init; }
        /// <summary>Newest first, like the log.</summary>
        public List<ZoneControlChange> Changes { get; init; }
    }

    /// <summary>What "Revert all" would do with a group, or did.</summary>
    public sealed class ZoneControlGroupRevertPlan
    {
        /// <summary>The changes to revert, newest first: the order they are undone in.</summary>
        public List<ZoneControlChange> Revert { get; } = new List<ZoneControlChange>();
        /// <summary>Changes left alone, with why.</summary>
        public List<(ZoneControlChange Change, string Why)> Skipped { get; } = new List<(ZoneControlChange, string)>();
    }

    public sealed class ZoneControlGroupRevertResult
    {
        public List<long> Reverted { get; } = new List<long>();
        public List<(long Id, string Why)> Skipped { get; } = new List<(long, string)>();
        /// <summary>Set when a revert failed part way: that change, and why. The ones before it stay reverted (each is logged).</summary>
        public long? StoppedAt { get; set; }
        public string StopReason { get; set; }
        public bool Logged { get; set; } = true;
    }

    /// <summary>
    /// History groups and "Revert all" (review 2026-11-11, step 5).
    ///
    /// GROUPS: rows saved by one save share a batch id and always group. Otherwise one person's rows (same actor and source - the
    /// portal logs the account, the game the character, so those stay apart) group while each is within <see cref="SessionGap"/> of
    /// the last. Groups are worked out on the server over the rows read, newest first.
    ///
    /// REVERT ALL undoes a group's changes one by one, NEWEST FIRST, each with the same exact check as a single Revert: the stat or
    /// part must still hold what that change left. Undoing newest first means each change sees the state right after it was made,
    /// so stat and whole-part changes to the same zone undo cleanly in any mix. The plan is first worked out on a copy of the store,
    /// reverting in the copy as it goes, so the preview shows exactly what the run will do: a change whose stat or part no longer
    /// holds what it left (edited after the group, or by someone else in between) is skipped; so is a stat on a zone that is gone,
    /// and ladder versions are never reverted.
    /// Everything runs under the manager lock, so no edit lands between the check and the writes. Each revert is logged, all with
    /// one new batch id, so the revert itself shows as one group and can be reverted the same way.
    /// </summary>
    public static class ZoneControlRevert
    {
        public static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(5);

        public static List<ZoneControlChangeGroup> Group(IReadOnlyList<ZoneControlChange> newestFirst, TimeSpan? gap = null)
        {
            var limit = gap ?? SessionGap;
            var groups = new List<ZoneControlChangeGroup>();
            List<ZoneControlChange> current = null;
            foreach (var c in newestFirst)
            {
                var last = current?.LastOrDefault();
                var joins = last != null
                    && string.Equals(last.Actor, c.Actor, StringComparison.OrdinalIgnoreCase)
                    && last.Source == c.Source
                    && ((c.BatchId != null && c.BatchId == last.BatchId) || last.ChangedAtUtc - c.ChangedAtUtc <= limit);
                if (!joins)
                {
                    current = new List<ZoneControlChange>();
                    groups.Add(null);
                }
                current.Add(c);
                groups[^1] = Make(current);
            }
            return groups;

            static ZoneControlChangeGroup Make(List<ZoneControlChange> rows) => new ZoneControlChangeGroup
            {
                Key = rows[0].Id.ToString(),
                Actor = rows[0].Actor,
                Source = rows[0].Source,
                ToUtc = rows[0].ChangedAtUtc,
                FromUtc = rows[^1].ChangedAtUtc,
                Changes = rows,
            };
        }

        /// <summary>What a change edits: one stat on one row, or one whole part.</summary>
        internal static string KeyOf(ZoneControlChange c) =>
            c.Kind == ZoneControlChangeLog.KindSection
                ? "part|" + c.Section
                : $"stat|{c.Zone?.ToLowerInvariant()}|{c.Variation}|{c.Wcid}|{c.Rank?.ToLowerInvariant()}|{c.Stat?.ToLowerInvariant()}";

        /// <summary>The value a change left equals the value the next (newer) change started from.</summary>
        internal static bool Continues(ZoneControlChange older, ZoneControlChange newer) =>
            older.Kind == ZoneControlChangeLog.KindSection
                ? ZoneControlManager.Canonical(older.NewJson) == ZoneControlManager.Canonical(newer.OldJson)
                : ZoneStatEditor.SameCurve(older.NewCurve, newer.OldCurve);

        /// <summary>
        /// The plan for a group, worked out on a COPY of the store (<paramref name="root"/>, changed in place): each change, newest first,
        /// is checked against the copy as earlier reverts left it, and reverted in the copy when it still holds what the change left.
        /// So a whole-zone change and a later stat edit to the same zone undo in turn, exactly as the real run will do them.
        /// </summary>
        public static ZoneControlGroupRevertPlan Plan(IReadOnlyList<ZoneControlChange> group, JObject root)
        {
            var plan = new ZoneControlGroupRevertPlan();
            var seen = new HashSet<string>();
            foreach (var c in group.OrderByDescending(c => c.Id))
            {
                var key = KeyOf(c);
                var newerInGroup = !seen.Add(key);
                var moved = newerInGroup ? "Changed by someone else in between." : "Changed again after this group.";
                string why = null;

                if (c.Kind == ZoneControlChangeLog.KindSection)
                {
                    if (c.Section == ZoneControlManager.SectionLadder)
                        why = "Ladder versions cannot be reverted.";
                    else if (ZoneControlManager.SectionCanonicalIn(root, c.Section) != ZoneControlManager.Canonical(c.NewJson))
                        why = moved;
                    else
                        why = ZoneControlManager.ApplySectionToRoot(root, c);
                }
                else
                {
                    var stats = ZoneControlManager.StatsIn(root, c, false, out var zoneMissing);
                    if (zoneMissing)
                        why = $"Zone '{c.Zone}' no longer exists (renamed or removed).";
                    else
                    {
                        var current = stats != null && Prop(stats, c.Stat) is JObject cur ? cur.ToObject<StatCurve>() : null;
                        if (!ZoneStatEditor.SameCurve(current, c.NewCurve))
                            why = moved;
                        else
                        {
                            var row = ZoneControlManager.StatsIn(root, c, c.OldCurve != null, out _);
                            foreach (var p in row?.Properties().Where(p => string.Equals(p.Name, c.Stat, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<JProperty>())
                                p.Remove();
                            if (c.OldCurve != null)
                                ZoneControlManager.SetProp(row, c.Stat, JObject.FromObject(c.OldCurve));
                        }
                    }
                }

                if (why == null)
                    plan.Revert.Add(c);
                else
                    plan.Skipped.Add((c, why));
            }
            return plan;

            static JToken Prop(JObject o, string name) =>
                o.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        /// <summary>The row a logged stat change edited, or null with <paramref name="problem"/> set.</summary>
        public static ZoneStatTarget TargetOf(ZoneControlChange change, out string problem)
        {
            problem = null;
            if (!ZoneRank.TryParse(change.Rank, out var rank))
            {
                problem = $"Unknown rank '{change.Rank}' on that change.";
                return null;
            }
            return change.Zone != null
                ? ZoneStatTarget.ForZone(change.Zone, change.Wcid, rank)
                : ZoneStatTarget.ForDefault(change.Variation ?? -1, rank);
        }

        /// <summary>The plan against the live store, without writing anything.</summary>
        public static ZoneControlGroupRevertPlan Preview(IReadOnlyList<ZoneControlChange> group)
            => ZoneControlManager.Locked(() => Plan(group, ZoneControlManager.StoreSnapshot()));

        /// <summary>Plans on a copy, then carries the plan out under one hold of the manager lock. Each revert still makes its own live
        /// check: one that no longer matches is skipped (it is never forced) and the run goes on. Any other failure, or an exception,
        /// stops the run; the reverts before it stay done (each is logged) and the result says where it stopped.</summary>
        public static ZoneControlGroupRevertResult RevertAll(IReadOnlyList<ZoneControlChange> group, ZoneStatActor actor, string reason)
        {
            var batch = Guid.NewGuid().ToString("N");
            return ZoneControlManager.Locked(() =>
            {
                var plan = Plan(group, ZoneControlManager.StoreSnapshot());
                var result = new ZoneControlGroupRevertResult();
                foreach (var (change, why) in plan.Skipped)
                    result.Skipped.Add((change.Id, why));

                foreach (var change in plan.Revert)
                {
                    ZoneStatEditStatus status;
                    string message;
                    bool logged;
                    try
                    {
                        if (change.Kind == ZoneControlChangeLog.KindSection)
                        {
                            var done = ZoneControlManager.RevertSection(change, actor, reason, batch);
                            (status, message, logged) = (done.Status, done.Message, done.Logged);
                        }
                        else
                        {
                            var target = TargetOf(change, out var problem);
                            if (target == null)
                                (status, message, logged) = (ZoneStatEditStatus.Refused, problem, true);
                            else
                            {
                                var done = ZoneStatEditor.Restore(target, change.Stat, change.OldCurve, actor, ZoneStatExpectation.Exactly(change.NewCurve), change.Id, reason, batch);
                                (status, message, logged) = (done.Status, done.Message, done.Logged);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.StoppedAt = change.Id;
                        result.StopReason = ex.GetBaseException().Message;
                        break;
                    }

                    if (status == ZoneStatEditStatus.Applied)
                    {
                        result.Reverted.Add(change.Id);
                        result.Logged &= logged;
                    }
                    else if (status == ZoneStatEditStatus.Conflict)
                        result.Skipped.Add((change.Id, message ?? "Changed since."));
                    else
                    {
                        result.StoppedAt = change.Id;
                        result.StopReason = message;
                        break;
                    }
                }
                return result;
            });
        }
    }
}
