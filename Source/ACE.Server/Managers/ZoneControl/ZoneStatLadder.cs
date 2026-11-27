using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>What one row of the stack does to a stat.</summary>
    public enum ZoneStatRungState
    {
        /// <summary>This row does not set it.</summary>
        NotSet,
        /// <summary>This row's value is the one the game uses.</summary>
        Wins,
        /// <summary>Set here, but a more specific row sets it too and wins.</summary>
        Overridden,
        /// <summary>A T25 anchor dropped because a more specific row set the T11 value alone (that value then applies flat at every tier).</summary>
        Shadowed,
        /// <summary>This row's value would win, but the stat is turned off (togglestat), so the game treats it as not set.</summary>
        Off,
    }

    /// <summary>One row of a stat's stack, as the portal's ladder shows it.</summary>
    public sealed class ZoneStatRung
    {
        public string Label { get; init; }
        /// <summary>"wcid", "zone", "tier" or "anchor" (the v11 board under every T12+ tier).</summary>
        public string Layer { get; init; }
        public int? Variation { get; init; }
        /// <summary>The rank row key ("regular", "leader", "boss"), or null for the Default row.</summary>
        public string Rank { get; init; }
        /// <summary>The row the page is editing.</summary>
        public bool IsTarget { get; init; }

        public double? Value { get; init; }
        /// <summary>The value has growth, the additive flag or tier pins on top of its number.</summary>
        public bool Curved { get; init; }
        public ZoneStatRungState State { get; init; }

        /// <summary>The T25 anchor (stat_t25) set on this row, if any.</summary>
        public double? Anchor { get; init; }
        public ZoneStatRungState AnchorState { get; init; }

        /// <summary>A togglestat on this row: false = turned off here, true = turned back on here, null = no toggle.</summary>
        public bool? Toggle { get; init; }
    }

    /// <summary>A stat's whole stack for one place: every row that could set it, most specific first.</summary>
    public sealed class ZoneStatLadder
    {
        public string Stat { get; init; }
        /// <summary>Most specific first: the order an admin reads "who wins".</summary>
        public IReadOnlyList<ZoneStatRung> Rungs { get; init; }
        /// <summary>A row sets it and it is not turned off: the game uses it.</summary>
        public bool InEffect { get; init; }
        /// <summary>A row sets it but the stat is turned off.</summary>
        public bool TurnedOff { get; init; }
        /// <summary>The T25 anchor survives the stack, so tiers 12-24 sit on the line between T11 and T25.</summary>
        public bool Anchored { get; init; }
    }

    /// <summary>
    /// Works out a stat's ladder from the rows in its stack. Pure: no store, no lock - the manager hands it the rows (under its lock)
    /// and tests hand it plain profiles.
    ///
    /// KEEP IT IN STEP with ZoneVariantProfile.Merge and ZoneControlManager.EvaluateVariant, which is what the game runs:
    /// - rows apply lowest first; a row that sets a key wins over every row below it;
    /// - THE SHADOW RULE: a row that sets the T11 value without its _t25 twin drops any twin from the rows below;
    /// - toggles merge the same way (the most specific toggle wins), and a final "off" removes the key after the merge.
    /// ZoneStatLadderTests checks the winner against a real Merge of the same rows.
    /// </summary>
    public static class ZoneStatLadderBuilder
    {
        /// <summary>One row of the stack, given lowest (least specific) first. Row may be null (that row does not exist).</summary>
        public sealed class Layer
        {
            public string Label { get; init; }
            public string Kind { get; init; }
            public int? Variation { get; init; }
            public string Rank { get; init; }
            public bool IsTarget { get; init; }
            public ZoneVariantProfile Row { get; init; }
        }

        public static ZoneStatLadder Build(string stat, IReadOnlyList<Layer> lowestFirst)
        {
            if (stat.EndsWith(ZoneStatHelp.AnchorSuffix, StringComparison.OrdinalIgnoreCase))
                stat = stat.Substring(0, stat.Length - ZoneStatHelp.AnchorSuffix.Length);
            var twin = stat + ZoneStatHelp.AnchorSuffix;

            int baseWin = -1, twinWin = -1;
            bool? baseToggle = null, twinToggle = null;
            var shadowed = new HashSet<int>();
            for (var i = 0; i < lowestFirst.Count; i++)
            {
                var row = lowestFirst[i].Row;
                if (row == null)
                    continue;
                var hasBase = row.Stats?.ContainsKey(stat) == true;
                var hasTwin = row.Stats?.ContainsKey(twin) == true;
                if (hasBase)
                {
                    baseWin = i;
                    if (!hasTwin && twinWin >= 0)
                    {
                        shadowed.Add(twinWin);
                        twinWin = -1;
                    }
                }
                if (hasTwin)
                    twinWin = i;
                if (row.StatToggles != null)
                {
                    if (row.StatToggles.TryGetValue(stat, out var t))
                        baseToggle = t;
                    if (row.StatToggles.TryGetValue(twin, out var tt))
                        twinToggle = tt;
                }
            }

            var baseOff = baseToggle == false;
            var twinOff = twinToggle == false;
            var rungs = new List<ZoneStatRung>(lowestFirst.Count);
            for (var i = lowestFirst.Count - 1; i >= 0; i--)
            {
                var layer = lowestFirst[i];
                StatCurve curve = null, anchor = null;
                layer.Row?.Stats?.TryGetValue(stat, out curve);
                layer.Row?.Stats?.TryGetValue(twin, out anchor);
                bool? toggle = null;
                if (layer.Row?.StatToggles != null && layer.Row.StatToggles.TryGetValue(stat, out var tg))
                    toggle = tg;

                rungs.Add(new ZoneStatRung
                {
                    Label = layer.Label,
                    Layer = layer.Kind,
                    Variation = layer.Variation,
                    Rank = layer.Rank,
                    IsTarget = layer.IsTarget,
                    Value = curve?.Base,
                    Curved = curve != null && (curve.Growth != 1.0 || curve.Additive || curve.Overrides is { Count: > 0 }),
                    State = curve == null ? ZoneStatRungState.NotSet : i != baseWin ? ZoneStatRungState.Overridden : baseOff ? ZoneStatRungState.Off : ZoneStatRungState.Wins,
                    Anchor = anchor?.Base,
                    AnchorState = anchor == null ? ZoneStatRungState.NotSet
                        : shadowed.Contains(i) ? ZoneStatRungState.Shadowed
                        : i != twinWin ? ZoneStatRungState.Overridden
                        : twinOff ? ZoneStatRungState.Off
                        : ZoneStatRungState.Wins,
                    Toggle = toggle,
                });
            }

            var inEffect = baseWin >= 0 && !baseOff;
            return new ZoneStatLadder
            {
                Stat = stat,
                Rungs = rungs,
                InEffect = inEffect,
                TurnedOff = baseWin >= 0 && baseOff,
                Anchored = inEffect && twinWin >= 0 && !twinOff,
            };
        }

        /// <summary>The rows for a zone (or one monster in it), lowest first, in the order the game merges them.
        /// <paramref name="rank"/> is the monster's rank (from the bucket) or the rank tab being viewed.</summary>
        public static List<Layer> ZoneStack(string zone, int variation, ZcRank rank, uint? wcid,
            ZoneVariantProfile anchorDefault, ZoneVariantProfile tierDefault, ZoneVariantProfile zoneMinion, ZoneVariantProfile bucket)
        {
            var list = TierStack(variation, rank, anchorDefault, tierDefault, markTarget: false);
            var rankKey = rank == ZcRank.None ? null : ZoneRank.Key(rank);
            list.Add(new Layer { Label = $"Zone '{zone}' - Default row", Kind = "zone", IsTarget = !wcid.HasValue && rank == ZcRank.None, Row = zoneMinion });
            if (rankKey != null)
                list.Add(new Layer { Label = $"Zone '{zone}' - {ZoneRank.Label(rank)} row", Kind = "zone", Rank = rankKey, IsTarget = !wcid.HasValue, Row = zoneMinion?.RankLayer(rank) });
            if (wcid.HasValue)
                list.Add(new Layer { Label = $"This monster (wcid {wcid.Value})", Kind = "wcid", IsTarget = true, Row = bucket });
            return list;
        }

        /// <summary>The rows for a tier's Default, lowest first: the v11 anchor board under every T12+ tier, then the tier's own rows.</summary>
        public static List<Layer> TierStack(int variation, ZcRank rank, ZoneVariantProfile anchorDefault, ZoneVariantProfile tierDefault, bool markTarget = true)
        {
            var list = new List<Layer>();
            var rankKey = rank == ZcRank.None ? null : ZoneRank.Key(rank);
            if (variation > 11)
            {
                list.Add(new Layer { Label = "T11 anchor board - Default row", Kind = "anchor", Variation = 11, Row = anchorDefault });
                if (rankKey != null)
                    list.Add(new Layer { Label = $"T11 anchor board - {ZoneRank.Label(rank)} row", Kind = "anchor", Variation = 11, Rank = rankKey, Row = anchorDefault?.RankLayer(rank) });
            }
            list.Add(new Layer { Label = $"T{variation} Default - Default row", Kind = "tier", Variation = variation, IsTarget = markTarget && rankKey == null, Row = tierDefault });
            if (rankKey != null)
                list.Add(new Layer { Label = $"T{variation} Default - {ZoneRank.Label(rank)} row", Kind = "tier", Variation = variation, Rank = rankKey, IsTarget = markTarget, Row = tierDefault?.RankLayer(rank) });
            return list;
        }
    }
}
