using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>Which row a stat edit lands on: a zone (optionally one monster's bucket) or a variation's Default, and the rank row inside it.</summary>
    public sealed class ZoneStatTarget
    {
        public string Zone { get; private init; }
        public int Variation { get; private init; }
        public uint? Wcid { get; private init; }
        public ZcRank Rank { get; private init; }

        public bool IsDefault => Zone == null;

        public static ZoneStatTarget ForZone(string zone, uint? wcid, ZcRank rank) => new ZoneStatTarget { Zone = zone, Wcid = wcid, Rank = rank };
        public static ZoneStatTarget ForDefault(int variation, ZcRank rank) => new ZoneStatTarget { Variation = variation, Rank = rank };

        public string Describe() =>
            (IsDefault ? $"Default v{Variation}" : $"'{Zone}'")
            + (Wcid.HasValue ? $" [wcid {Wcid.Value}]" : "")
            + (Rank == ZcRank.None ? "" : $" [{ZoneRank.Label(Rank)} row]");
    }

    public sealed class ZoneStatActor
    {
        public const string SourcePortal = "portal";
        public const string SourceGame = "game";
        public const string SourceConsole = "console";

        public string Name { get; init; }
        public string Source { get; init; }
    }

    public enum ZoneStatEditStatus
    {
        Applied,
        NothingToClear,
        NoSuchZone,
        Refused,
        Conflict,
    }

    public sealed class ZoneStatEditResult
    {
        public ZoneStatEditStatus Status { get; init; }
        public string Stat { get; init; }
        public string Message { get; init; }
        public StatCurve Old { get; init; }
        public StatCurve New { get; init; }

        /// <summary>The change log write this edit queued (null = nothing to log).</summary>
        internal System.Threading.Tasks.Task<bool> LogWrite { get; set; }

        /// <summary>False when the change applied but its change log row could not be written. Waits for the write (up to 15 s), so read
        /// it only after the manager lock is released (the portal's answer does; game commands never read it).</summary>
        public bool Logged => ZoneControlChangeLog.Outcome(LogWrite);
    }

    /// <summary>
    /// Stat edits for every front end: /zonecontrol set / clearstat / default and the portal's Zone Control page. The rules live
    /// here so the two cannot drift, and every applied change lands in <see cref="ZoneControlChangeLog"/> whichever one made it.
    /// </summary>
    public static class ZoneStatEditor
    {
        public const string WcidRankRefusal = "A monster override has no rank rows - it IS one rank. Drop --rank, or set the rank row on the zone / tier without --wcid.";

        public static string NormalizeStat(string s)
        {
            s = s?.Trim().ToLowerInvariant();
            return ZoneStat.All.FirstOrDefault(k => k.Equals(s, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A finite number - NaN / Infinity would poison every reader of the stat (review 2026-10-04: a NaN bag_odds
        /// dropped a bag on every kill, a NaN loot_grade_floor forced every grade to 0). Thousands separators are allowed only
        /// as real 3-digit groups after a non-zero lead ("151,200" for odds): "0,5" used to read as 5 (x10 a vuln cap or a DoT
        /// multiplier) and is now refused, like "0,500", currency signs and parentheses (review 2026-10-04).</summary>
        public static bool TryParseValue(string s, out double value)
        {
            value = 0;
            s = s?.Trim();
            if (string.IsNullOrEmpty(s) || (s.Contains(',') && !ThousandsGroups.IsMatch(s)))
                return false;
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value)
                && double.IsFinite(value);
        }

        private static readonly Regex ThousandsGroups =
            new Regex(@"^[+-]?[1-9]\d{0,2}(,\d{3})+(\.\d*)?([eE][+-]?\d+)?$", RegexOptions.CultureInvariant);

        /// <summary>The row a stat verb edits: the zone layer or the per-WCID bucket, then the rank row
        /// inside it. Returns null with <paramref name="refusal"/> set for --wcid + --rank (a bucket has
        /// no rows). <paramref name="create"/> = author (set) vs inspect/clear.</summary>
        public static ZoneVariantProfile RowFor(ZoneScalingProfile profile, uint? wcid, ZcRank rank, bool create, out string refusal)
        {
            refusal = null;
            if (wcid.HasValue && rank != ZcRank.None)
            {
                refusal = WcidRankRefusal;
                return null;
            }
            var layer = wcid.HasValue ? profile.VariantForWcid(wcid.Value, create) : profile.Minion;
            return layer?.RankLayer(rank, create);
        }

        public static ZoneStatEditResult Set(ZoneStatTarget target, string statText, double value, ZoneStatActor actor, ZoneStatExpectation expected = null, string reason = null)
        {
            var stat = NormalizeStat(statText);
            if (stat == null)
                return Refuse(statText, "Unknown stat. Stats: " + string.Join(", ", ZoneStat.All));
            if (!double.IsFinite(value))
                return Refuse(stat, "value must be a number.");
            return Apply(target, stat, new StatCurve { Base = value, Growth = 1.0, Additive = false }, actor, expected, null, reason);
        }

        /// <summary>CLEARING accepts any key PRESENT IN THE STORE, not only a currently-known stat: renaming a stat otherwise strands
        /// its old key forever. Setting still validates - you may only clear rubbish, never author it.</summary>
        public static ZoneStatEditResult Clear(ZoneStatTarget target, string statText, ZoneStatActor actor, ZoneStatExpectation expected = null, string reason = null)
        {
            var stat = NormalizeStat(statText) ?? statText?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(stat))
                return Refuse(statText, "No stat named.");
            return Apply(target, stat, null, actor, expected, null, reason);
        }

        /// <summary>Put a logged change back: restores the exact curve (growth and tier pins included), or clears when the stat was unset.</summary>
        public static ZoneStatEditResult Restore(ZoneStatTarget target, string stat, StatCurve curve, ZoneStatActor actor, ZoneStatExpectation expected, long revertsId, string reason = null, string batchId = null)
            => Apply(target, stat, curve, actor, expected, revertsId, reason, batchId);

        private static ZoneStatEditResult Apply(ZoneStatTarget target, string stat, StatCurve next, ZoneStatActor actor, ZoneStatExpectation expected, long? revertsId, string reason, string batchId = null)
        {
            if (target.IsDefault && target.Variation < 0)
                return Refuse(stat, "variation must be >= 0 (negative variations are rift instances and never inherit a Default).");
            if (target.Wcid.HasValue && target.Rank != ZcRank.None)
                return Refuse(stat, WcidRankRefusal);

            // the whole edit, save AND log row, under one hold of the manager lock: the row is queued before any other save can log,
            // so the change log keeps edits in the order they were made (ZoneControlChangeLog ordered writer)
            return ZoneControlManager.Locked(() => ApplyLocked(target, stat, next, actor, expected, revertsId, reason, batchId));
        }

        private static ZoneStatEditResult ApplyLocked(ZoneStatTarget target, string stat, StatCurve next, ZoneStatActor actor, ZoneStatExpectation expected, long? revertsId, string reason, string batchId)
        {
            ZoneStatEditResult result = null;
            ZoneControlChangeLog.StatSkip edit = null;
            // The save this edit triggers logs it: the stat row first (old value from the last SAVED copy), then anything else that
            // differs in the part, one batch (review 2026-11-19). The row names a zone by its real name ("my_zone" can find "My Zone"),
            // so History, its whole-zone check and Revert all look for the name the store holds.
            ZoneControlChangeLog.StatSkip Describe(string section, ZoneStatTarget logged) =>
                new ZoneControlChangeLog.StatSkip(section, logged, stat, next) { Actor = actor, RevertsId = revertsId, Reason = reason, BatchId = batchId };
            using var skip = ZoneControlChangeLog.StatEditOn(null);
            if (target.IsDefault)
            {
                ZoneControlManager.MutateVariationDefaultIf(target.Variation, d =>
                {
                    result = ApplyToRow(create => d.Profile.RankLayer(target.Rank, create), stat, next, expected);
                    if (result.Status == ZoneStatEditStatus.Applied)
                        ZoneControlChangeLog.SetSkipSection(edit = Describe(ZoneControlManager.DefaultSectionKey(target.Variation), target));
                    return result.Status == ZoneStatEditStatus.Applied;
                });
            }
            else
            {
                var found = ZoneControlManager.MutateAreaIf(target.Zone, a =>
                {
                    result = ApplyToRow(create => RowFor(a.Profile, target.Wcid, target.Rank, create, out _), stat, next, expected);
                    if (result.Status == ZoneStatEditStatus.Applied)
                        ZoneControlChangeLog.SetSkipSection(edit = Describe(ZoneControlManager.ZoneSectionKey(a.Name), ZoneStatTarget.ForZone(a.Name, target.Wcid, target.Rank)));
                    return result.Status == ZoneStatEditStatus.Applied;
                });
                if (!found)
                    return new ZoneStatEditResult { Status = ZoneStatEditStatus.NoSuchZone, Stat = stat, Message = $"No zone '{target.Zone}'." };
            }

            if (result.Status == ZoneStatEditStatus.Applied)
                result.LogWrite = edit?.LogWrite;
            return result;
        }

        /// <summary>The edit itself, against one row. <paramref name="row"/> takes create: only a real write may create a row, so a
        /// refused edit or a clear leaves no empty rows behind. Call under the manager lock (MutateAreaIf / MutateVariationDefaultIf).</summary>
        internal static ZoneStatEditResult ApplyToRow(Func<bool, ZoneVariantProfile> row, string stat, StatCurve next, ZoneStatExpectation expected)
        {
            var existing = row(false);
            StatCurve old = null;
            existing?.Stats?.TryGetValue(stat, out old);

            if (expected != null && !expected.Matches(old))
                return new ZoneStatEditResult
                {
                    Status = ZoneStatEditStatus.Conflict,
                    Stat = stat,
                    Old = old,
                    Message = ConflictMessage(stat, old, expected),
                };

            if (next == null)
            {
                if (old == null)
                    return new ZoneStatEditResult { Status = ZoneStatEditStatus.NothingToClear, Stat = stat, Message = "That stat wasn't set." };
                existing.Stats.Remove(stat);
                return new ZoneStatEditResult { Status = ZoneStatEditStatus.Applied, Stat = stat, Old = old };
            }

            row(true).Stats[stat] = next;
            return new ZoneStatEditResult { Status = ZoneStatEditStatus.Applied, Stat = stat, Old = old, New = next };
        }

        private static string ConflictMessage(string stat, StatCurve now, ZoneStatExpectation expected)
        {
            var sameBase = now != null && (expected.WholeCurve ? expected.Curve != null && expected.Curve.Base == now.Base : expected.Base == now.Base);
            if (sameBase)
                return $"{stat} changed since: the value is the same, but its growth, additive flag or tier pins were edited. Nothing was changed.";
            return $"{stat} changed since you loaded it: it is now {(now == null ? "not set" : now.Base.ToString("0.####", CultureInfo.InvariantCulture))}. Nothing was changed.";
        }

        internal static bool SameCurve(StatCurve a, StatCurve b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            if (a.Base != b.Base || a.Growth != b.Growth || a.Additive != b.Additive)
                return false;
            var ao = a.Overrides ?? new Dictionary<int, double>();
            var bo = b.Overrides ?? new Dictionary<int, double>();
            return ao.Count == bo.Count && ao.All(kv => bo.TryGetValue(kv.Key, out var v) && v == kv.Value);
        }

        private static ZoneStatEditResult Refuse(string stat, string message) =>
            new ZoneStatEditResult { Status = ZoneStatEditStatus.Refused, Stat = stat, Message = message };
    }

    /// <summary>What an editor saw when it loaded the stat: the edit only lands if the row still says that, so a web form left open
    /// never overwrites a newer change from the game or another browser. Base null = the stat was not set.
    /// A form knows only the number, so it checks Base. A revert knows the whole curve it expects (the change's new side), so it
    /// uses <see cref="Exactly"/>: growth, the additive flag and tier pins must match too, or the revert would silently throw away
    /// a curve edit made since (review 2026-11-11).</summary>
    public sealed class ZoneStatExpectation
    {
        public double? Base { get; init; }

        /// <summary>Set by <see cref="Exactly"/>: the whole curve must match <see cref="Curve"/> (null = not set).</summary>
        public bool WholeCurve { get; private init; }
        public StatCurve Curve { get; private init; }

        /// <summary>The whole curve the editor loaded, as <see cref="RevisionOf"/> gave it (null = only the number is checked). The
        /// portal sends it so a stale form can't overwrite a growth or tier pin added since with the same base (review 2026-11-19).</summary>
        public string Revision { get; init; }

        public static ZoneStatExpectation Exactly(StatCurve curve) => new ZoneStatExpectation { Base = curve?.Base, Curve = curve, WholeCurve = true };

        /// <summary>The number the form showed, and when it sent one, the revision of the whole curve.</summary>
        public static ZoneStatExpectation Loaded(double? baseValue, string revision) =>
            new ZoneStatExpectation { Base = baseValue, Revision = string.IsNullOrWhiteSpace(revision) ? null : revision.Trim() };

        public bool Matches(StatCurve current)
        {
            if (WholeCurve)
                return ZoneStatEditor.SameCurve(Curve, current);
            var baseMatches = Base.HasValue ? current != null && current.Base == Base.Value : current == null;
            return baseMatches && (Revision == null || Revision == RevisionOf(current));
        }

        /// <summary>A short fingerprint of a whole stored curve (base, growth, additive, tier pins), null when the stat is not set.
        /// Two curves get the same revision exactly when <see cref="ZoneStatEditor.SameCurve"/> says they are the same.</summary>
        public static string RevisionOf(StatCurve c)
        {
            if (c == null)
                return null;
            var pins = string.Join(",", (c.Overrides ?? new Dictionary<int, double>()).OrderBy(kv => kv.Key)
                .Select(kv => kv.Key.ToString(CultureInfo.InvariantCulture) + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture)));
            var text = string.Join("|", c.Base.ToString("R", CultureInfo.InvariantCulture), c.Growth.ToString("R", CultureInfo.InvariantCulture), c.Additive ? "1" : "0", pins);
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
        }
    }
}
