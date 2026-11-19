using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>One kind of monster in a zone whose value a previewed edit would change.</summary>
    public sealed class ZoneStatImpactMonster
    {
        /// <summary>"No rank", "Regular", "Leader", "Boss", or "wcid 12345 (Boss)".</summary>
        public string Label { get; init; }
        public double? Before { get; init; }
        public double? After { get; init; }
    }

    /// <summary>What a previewed edit does to one zone, at the zone's own tier.</summary>
    public sealed class ZoneStatImpactZone
    {
        public string Zone { get; init; }
        public int Variation { get; init; }
        public bool Enabled { get; init; }
        /// <summary>"full": every kind of monster in it changes. "partial": some do (a rank row or monster override keeps its own
        /// value). "none": the zone sets its own value for everything.</summary>
        public string Reach { get; init; }
        public int MonsterKinds { get; init; }
        public List<ZoneStatImpactMonster> Changed { get; init; }
    }

    /// <summary>The dry run of one stat edit: nothing is saved.</summary>
    public sealed class ZoneStatImpact
    {
        /// <summary>Applied = the edit would go through (Zones says what it does); otherwise why it would be refused.</summary>
        public ZoneStatEditStatus Status { get; init; }
        public string Message { get; init; }
        public string Stat { get; init; }
        public StatCurve Old { get; init; }
        public StatCurve New { get; init; }
        /// <summary>Every zone the edited row reaches, changed or not.</summary>
        public List<ZoneStatImpactZone> Zones { get; init; } = new List<ZoneStatImpactZone>();
    }

    public static partial class ZoneControlManager
    {
        /// <summary>
        /// What a stat edit WOULD do, without doing it: which zones it reaches and, in each, which kinds of monster get a different value
        /// at that zone's tier. Runs the same stack the game builds (<see cref="BuildZoneRef"/>: the tier Default with the v11 anchor under
        /// it, then the zone, then the monster's bucket, each flattened for the rank) twice - once over the live rows, once with an edited
        /// COPY of the target row - and compares. Nothing live is touched.
        ///
        /// Reach: a tier Default reaches every zone on that tier; the T11 Default also reaches every zone above T11 (its anchor board).
        /// Rift instances (negative variations) never inherit a Default and are not counted. <paramref name="next"/> null = a clear.
        /// </summary>
        public static ZoneStatImpact PreviewStatEdit(ZoneStatTarget target, string stat, StatCurve next, ZoneStatExpectation expected)
        {
            if (target.IsDefault && target.Variation < 0)
                return new ZoneStatImpact { Status = ZoneStatEditStatus.Refused, Stat = stat, Message = "variation must be >= 0." };
            if (target.Wcid.HasValue && target.Rank != ZcRank.None)
                return new ZoneStatImpact { Status = ZoneStatEditStatus.Refused, Stat = stat, Message = ZoneStatEditor.WcidRankRefusal };

            var baseStat = stat.EndsWith(ZoneStatHelp.AnchorSuffix, StringComparison.OrdinalIgnoreCase)
                ? stat.Substring(0, stat.Length - ZoneStatHelp.AnchorSuffix.Length)
                : stat;

            EnsureInitialized();
            lock (_lock)
            {
                Func<int, ZoneVariantProfile> liveDefaults = v => DefaultFor(v)?.Profile;
                Func<int, ZoneVariantProfile> editedDefaults = liveDefaults;
                IEnumerable<ControlledArea> reached;
                ZoneStatEditResult edit;
                ZoneVariantProfile editedMinion = null, editedBucket = null;
                ControlledArea zoneArea = null;

                if (target.IsDefault)
                {
                    var copy = ZoneVariantProfile.Merge(liveDefaults(target.Variation));
                    edit = ZoneStatEditor.ApplyToRow(create => copy.RankLayer(target.Rank, create), stat, next, expected);
                    editedDefaults = v => v == target.Variation ? copy : liveDefaults(v);
                    reached = _areas.Values.Where(a => a.Variation == target.Variation || (target.Variation == 11 && a.Variation > 11));
                }
                else
                {
                    zoneArea = FindArea(target.Zone);
                    if (zoneArea == null)
                        return new ZoneStatImpact { Status = ZoneStatEditStatus.NoSuchZone, Stat = stat, Message = $"No zone '{target.Zone}'." };
                    editedMinion = ZoneVariantProfile.Merge(zoneArea.Profile.Minion);
                    if (target.Wcid.HasValue && zoneArea.Profile.WcidOverrides.TryGetValue(target.Wcid.Value, out var liveBucket) && liveBucket != null)
                        editedBucket = ZoneVariantProfile.Merge(liveBucket);
                    edit = ZoneStatEditor.ApplyToRow(create => target.Wcid.HasValue
                            ? editedBucket ??= create ? new ZoneVariantProfile() : null
                            : editedMinion.RankLayer(target.Rank, create),
                        stat, next, expected);
                    reached = new[] { zoneArea };
                }

                var result = new ZoneStatImpact { Status = edit.Status, Message = edit.Message, Stat = stat, Old = edit.Old?.Clone(), New = edit.New?.Clone() };
                if (edit.Status != ZoneStatEditStatus.Applied)
                    return result;

                foreach (var area in reached.OrderBy(a => a.Variation).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var edited = area == zoneArea;
                    var buckets = new List<ImpactBucket>();
                    var wcids = area.Profile.WcidOverrides.Keys.ToList();
                    if (edited && target.Wcid.HasValue && !wcids.Contains(target.Wcid.Value))
                        wcids.Add(target.Wcid.Value);
                    foreach (var wcid in wcids.OrderBy(w => w))
                    {
                        area.Profile.WcidOverrides.TryGetValue(wcid, out var before);
                        buckets.Add(new ImpactBucket(wcid, ResolveRankForWcid(area, wcid).Rank, before, edited && wcid == target.Wcid ? editedBucket : before));
                    }
                    result.Zones.Add(CompareZone(area.Name, area.Variation, area.Enabled, baseStat, liveDefaults, editedDefaults,
                        area.Profile.Minion, edited ? editedMinion : area.Profile.Minion, buckets));
                }
                return result;
            }
        }

        /// <summary>One monster override in a zone: its rank and its bucket before and after the edit.</summary>
        internal sealed record ImpactBucket(uint Wcid, ZcRank Rank, ZoneVariantProfile Before, ZoneVariantProfile After);

        /// <summary>One zone, before vs after: every rank's zone-wide profile and every monster override, built exactly as
        /// <see cref="BuildZoneRef"/> builds them, compared at the zone's tier. Pure (no store, no lock), so tests feed it plain rows.</summary>
        internal static ZoneStatImpactZone CompareZone(string zone, int variation, bool enabled, string baseStat,
            Func<int, ZoneVariantProfile> defaultsBefore, Func<int, ZoneVariantProfile> defaultsAfter,
            ZoneVariantProfile minionBefore, ZoneVariantProfile minionAfter, IReadOnlyList<ImpactBucket> buckets)
        {
            var kinds = 0;
            var changed = new List<ZoneStatImpactMonster>();
            void Compare(string label, ZcRank rank, ZoneVariantProfile bucketBefore, ZoneVariantProfile bucketAfter)
            {
                kinds++;
                var before = ValueAt(zone, variation, baseStat, ZoneVariantProfile.Merge(AnchoredDefault(variation, rank, defaultsBefore), minionBefore?.ForRank(rank), bucketBefore));
                var after = ValueAt(zone, variation, baseStat, ZoneVariantProfile.Merge(AnchoredDefault(variation, rank, defaultsAfter), minionAfter?.ForRank(rank), bucketAfter));
                if (before != after)
                    changed.Add(new ZoneStatImpactMonster { Label = label, Before = before, After = after });
            }

            foreach (var rank in ZoneRank.All)
                Compare(rank == ZcRank.None ? "No rank" : ZoneRank.Label(rank), rank, null, null);
            foreach (var b in buckets)
                Compare($"wcid {b.Wcid} ({(b.Rank == ZcRank.None ? "no rank" : ZoneRank.Label(b.Rank))})", b.Rank, b.Before, b.After);

            return new ZoneStatImpactZone
            {
                Zone = zone,
                Variation = variation,
                Enabled = enabled,
                Reach = changed.Count == 0 ? "none" : changed.Count == kinds ? "full" : "partial",
                MonsterKinds = kinds,
                Changed = changed,
            };
        }

        /// <summary>The stat's value at the zone's tier after the game's own evaluation (toggles applied), or null when it is not set.</summary>
        private static double? ValueAt(string zone, int tier, string baseStat, ZoneVariantProfile merged)
        {
            var eval = EvaluateVariant(zone, merged);
            return eval.Has(baseStat) ? Math.Round(eval.GetT(baseStat, 0, tier), 6) : null;
        }
    }
}
