using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// THE CHANGE LOG FOR EVERYTHING THAT IS NOT A STAT EDIT (review 2026-11-11, phase 2).
    ///
    /// Rather than logging code in each of the ~20 edit commands, the store save compares the store it is about to write with the one
    /// it wrote last, part by part, and logs every part that changed with its whole JSON before and after. A part is one zone, one tier
    /// Default, or one server-wide list (ladder versions, monster damage multipliers, gear lock spots, the zone-wide bounty, region
    /// bounties, allowed procs). So enable/disable, landblocks, togglestat, curves, tier pins, props, appearance, modifiers, currency,
    /// bounties, clone, copyfrom, rename and delete are all logged - and so is any command added later - with no code of their own.
    ///
    /// NAMING: the store's JSON property names depend on the server. A mod can set Newtonsoft's global defaults (CustomClothingBase sets
    /// camelCase + indented), so the same store reads "Areas"/"Name" on one server and "areas"/"name" on another. Everything here finds
    /// properties case-insensitively, writes back under the name already there, and compares parts with lowercased keys (Canonical).
    ///
    /// A revert puts one part back exactly as it was, through the same save-and-reload a settings import uses, and only when the part
    /// still holds what the change left (anything else would undo a later edit).
    /// </summary>
    public static partial class ZoneControlManager
    {
        internal sealed class SectionState
        {
            public string Key { get; init; }
            public string Label { get; init; }
            public string Zone { get; init; }
            public int? Variation { get; init; }
            /// <summary>The part as stored: what a revert writes back.</summary>
            public string Json { get; init; }
            /// <summary>The part with object keys sorted and number lists sorted, so a save that only reorders a set is not a change.</summary>
            public string Canonical { get; init; }
        }

        /// <summary>The parts of the last store written (or loaded). Under _lock.</summary>
        private static Dictionary<string, SectionState> _savedSections = new Dictionary<string, SectionState>(StringComparer.Ordinal);

        public const string SectionLadder = "ladder";
        private const string SectionZonePrefix = "zone:";
        private const string SectionDefaultPrefix = "default:";

        internal static string ZoneSectionKey(string zoneName) => SectionZonePrefix + zoneName.ToLowerInvariant();
        internal static string DefaultSectionKey(int variation) => SectionDefaultPrefix + variation;

        /// <summary>Global parts: store property, section key, label.</summary>
        internal static readonly (string Property, string Key, string Label)[] GlobalSections =
        {
            (nameof(Store.LadderApplies), SectionLadder, "Ladder versions"),
            (nameof(Store.DamageMults), "damage", "Monster damage multipliers"),
            (nameof(Store.GearLockSpots), "gearlock", "Gear lock spots"),
            (nameof(Store.ZoneWideBounty), "bounty:zonewide", "Zone-wide bounty"),
            (nameof(Store.RegionBounties), "bounty:regions", "Region bounties"),
            (nameof(Store.BountyRewardTables), "bounty:tables", "Bounty reward tables"),
            (nameof(Store.BountyPlaceWeights), "bounty:weights", "Item bounty drop weights"),
            (nameof(Store.AllowedProcSpells), "procallow", "Allowed procs"),
        };

        private static Dictionary<string, SectionState> SectionsOf(Store store)
        {
            var map = new Dictionary<string, SectionState>(StringComparer.Ordinal);
            void Add(string key, string label, string zone, int? variation, object value)
            {
                var json = JsonConvert.SerializeObject(value);
                map[key] = new SectionState { Key = key, Label = label, Zone = zone, Variation = variation, Json = json, Canonical = Canonical(json) };
            }

            foreach (var a in store.Areas.Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name)))
                Add(ZoneSectionKey(a.Name), "Zone settings", a.Name, null, a);
            foreach (var kv in store.VariationDefaults.Where(kv => kv.Value != null))
                Add(DefaultSectionKey(kv.Key), $"T{kv.Key} Default", null, kv.Key, kv.Value);
            var root = JObject.FromObject(store);
            foreach (var (property, key, label) in GlobalSections)
                Add(key, label, null, null, Prop(root, property));
            return map;
        }

        /// <summary>JSON with object keys lowercased and sorted, empty or null properties dropped, and lists of plain numbers sorted
        /// (landblocks, spell ids are sets).</summary>
        internal static string Canonical(string json)
        {
            if (json == null)
                return null;
            static JToken Sort(JToken t)
            {
                switch (t)
                {
                    case JObject o:
                        // keys lowercased: the same part saved under camelCase or PascalCase names is the same part (see NAMING above).
                        // Properties that are null or hold nothing (an empty object or list, after their own emptying) are dropped: they
                        // mean the same as no property. A stat set on a new Boss row creates that row and its revert empties it again; the
                        // empty row must not make the zone count as "changed since" and block older reverts (review 2026-11-12).
                        return new JObject(o.Properties()
                            .GroupBy(p => p.Name.ToLowerInvariant())
                            .OrderBy(g => g.Key, StringComparer.Ordinal)
                            .Select(g => new JProperty(g.Key, Sort(g.Last().Value)))
                            .Where(p => !IsNothing(p.Value)));
                    case JArray a:
                        var items = a.Select(Sort).ToList();
                        if (items.Count > 0 && items.All(i => i.Type is JTokenType.Integer or JTokenType.Float))
                            items = items.OrderBy(i => i.Value<double>()).ToList();
                        return new JArray(items);
                    default:
                        return t.DeepClone();
                }
            }
            return Sort(JToken.Parse(json)).ToString(Formatting.None);

            static bool IsNothing(JToken t) => t.Type == JTokenType.Null
                || (t is JObject jo && !jo.HasValues)
                || (t is JArray ja && ja.Count == 0);
        }

        /// <summary>The parts that differ between two snapshots, in a stable order (zones, then Defaults, then the global parts).</summary>
        internal static List<ZoneControlChangeLog.SectionChange> DiffSections(IReadOnlyDictionary<string, SectionState> before, IReadOnlyDictionary<string, SectionState> after, string skip = null)
        {
            var changes = new List<ZoneControlChangeLog.SectionChange>();
            foreach (var key in before.Keys.Union(after.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                if (key == skip)
                    continue;
                before.TryGetValue(key, out var b);
                after.TryGetValue(key, out var a);
                if (b?.Canonical == a?.Canonical)
                    continue;
                var any = a ?? b;
                changes.Add(new ZoneControlChangeLog.SectionChange
                {
                    Section = key,
                    Label = any.Label,
                    Zone = any.Zone,
                    Variation = any.Variation,
                    OldJson = b?.Json,
                    NewJson = a?.Json,
                });
            }
            return changes;
        }

        /// <summary>Called by Save after the store is written: logs what changed since the last write and moves the baseline.
        /// A failure to log never fails the save (the edit is already stored).</summary>
        private static void LogSavedSections(Store store, long? revertsId = null, string reason = null)
        {
            try
            {
                var now = SectionsOf(store);
                var changes = DiffSections(_savedSections, now, ZoneControlChangeLog.SkipSection);
                _savedSections = now;
                ZoneControlChangeLog.RecordSections(changes, ZoneControlChangeLog.CurrentActor, revertsId, reason ?? ZoneControlChangeLog.CurrentReason);
            }
            catch (Exception ex)
            {
                log.Error($"[ZoneControl] change log: could not compare the saved store: {ex.GetBaseException().Message}");
            }
        }

        /// <summary>Called by Load: the store just read is the baseline. Not logged (a start or a reload is not an edit).</summary>
        private static void ResetSavedSections()
        {
            try { _savedSections = SectionsOf(BuildStore()); }
            catch (Exception ex)
            {
                _savedSections = new Dictionary<string, SectionState>(StringComparer.Ordinal);
                log.Error($"[ZoneControl] change log: could not read the loaded store: {ex.GetBaseException().Message}");
            }
        }

        public sealed class SectionRevertResult
        {
            public ZoneStatEditStatus Status { get; init; }
            public string Message { get; init; }
            public bool Logged { get; init; } = true;
        }

        /// <summary>Puts one part back exactly as a logged change found it, as a new logged change. Refused when the part has changed
        /// since (it must still hold exactly what the change left), and for ladder versions (items compare them, so turning one back
        /// would re-apply a ladder to gear that already has it).</summary>
        public static SectionRevertResult RevertSection(ZoneControlChange change, ZoneStatActor actor, string reason = null, string batchId = null)
        {
            if (change?.Section == null)
                return new SectionRevertResult { Status = ZoneStatEditStatus.Refused, Message = "That change is not a whole-part change." };
            if (change.Section == SectionLadder)
                return new SectionRevertResult { Status = ZoneStatEditStatus.Refused, Message = "Ladder versions cannot be reverted: gear compares them, so going back would re-apply a ladder. Use /zonecontrol ladder." };

            EnsureInitialized();
            lock (_lock)
            {
                var current = SectionsOf(BuildStore());
                current.TryGetValue(change.Section, out var now);
                if (now?.Canonical != Canonical(change.NewJson))
                    return new SectionRevertResult
                    {
                        Status = ZoneStatEditStatus.Conflict,
                        Message = now == null
                            ? $"{change.Stat} no longer exists as that change left it (renamed or removed since). Nothing was changed."
                            : $"{change.Stat} has changed since. Revert the later changes first. Nothing was changed.",
                    };

                var root = StoreSnapshot();
                var refusal = ApplySectionToRoot(root, change);
                if (refusal != null)
                    return new SectionRevertResult { Status = ZoneStatEditStatus.Conflict, Message = refusal };

                // the save-and-reload a settings import uses: checked and size-guarded before writing, rolled back if the reload fails
                var before = _savedSections;
                ImportStoreJsonCore(root.ToString(Formatting.None));
                var after = _savedSections;   // the reload's Load reset the baseline to the store just written
                var logged = ZoneControlChangeLog.RecordSections(DiffSections(before, after), actor, change.Id, reason, batchId);
                return new SectionRevertResult { Status = ZoneStatEditStatus.Applied, Logged = logged };
            }
        }

        /// <summary>The live store as JSON, for edits on a copy. Under _lock (re-entrant).</summary>
        internal static JObject StoreSnapshot()
        {
            EnsureInitialized();
            lock (_lock)
                return JObject.Parse(JsonConvert.SerializeObject(BuildStore()));
        }

        internal static JToken Prop(JToken o, string name) =>
            (o as JObject)?.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

        /// <summary>Sets a property under the name it already has (any case), or adds it: never a second copy in another case.</summary>
        internal static void SetProp(JObject o, string name, JToken value)
        {
            var existing = o.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                existing.Value = value;
            else
                o[name] = value;
        }

        private static JArray AreasIn(JObject root)
        {
            if (Prop(root, nameof(Store.Areas)) is JArray areas)
                return areas;
            var made = new JArray();
            SetProp(root, nameof(Store.Areas), made);
            return made;
        }

        private static string NameOf(JToken area) => (string)Prop(area, nameof(ControlledArea.Name));

        private static JObject AreaIn(JObject root, string zoneName) =>
            AreasIn(root).OfType<JObject>().FirstOrDefault(a => string.Equals(NameOf(a), zoneName, StringComparison.OrdinalIgnoreCase));

        private static JObject DefaultsIn(JObject root, bool create)
        {
            if (Prop(root, nameof(Store.VariationDefaults)) is JObject defaults)
                return defaults;
            if (!create)
                return null;
            var made = new JObject();
            SetProp(root, nameof(Store.VariationDefaults), made);
            return made;
        }

        /// <summary>One part of a store copy, canonical (null = the part does not exist). Matches <see cref="SectionsOf"/> on the live store.</summary>
        internal static string SectionCanonicalIn(JObject root, string key)
        {
            if (key.StartsWith(SectionZonePrefix, StringComparison.Ordinal))
                return AreaIn(root, key.Substring(SectionZonePrefix.Length)) is { } area ? Canonical(area.ToString(Formatting.None)) : null;
            if (key.StartsWith(SectionDefaultPrefix, StringComparison.Ordinal))
            {
                var d = Prop(DefaultsIn(root, false), key.Substring(SectionDefaultPrefix.Length));
                return d == null || d.Type == JTokenType.Null ? null : Canonical(d.ToString(Formatting.None));
            }
            var global = GlobalSections.FirstOrDefault(g => g.Key == key);
            return global.Property == null ? null : Canonical((Prop(root, global.Property) ?? JValue.CreateNull()).ToString(Formatting.None));
        }

        /// <summary>Puts one whole part of a store copy back to a change's old side. Null = done; otherwise why it cannot be.</summary>
        internal static string ApplySectionToRoot(JObject root, ZoneControlChange change)
        {
            var old = change.OldJson == null ? null : JToken.Parse(change.OldJson);
            if (change.Section.StartsWith(SectionZonePrefix, StringComparison.Ordinal))
            {
                var areas = AreasIn(root);
                var name = change.Section.Substring(SectionZonePrefix.Length);
                bool IsThisPart(JObject a) => string.Equals(NameOf(a), name, StringComparison.OrdinalIgnoreCase);
                // check BEFORE touching the copy: Revert all plans on one shared copy, and a refusal must leave it as it was
                var oldName = old == null ? null : NameOf(old);
                if (oldName != null && areas.OfType<JObject>().Any(a => !IsThisPart(a) && string.Equals(NameOf(a), oldName, StringComparison.OrdinalIgnoreCase)))
                    return $"Another zone is named '{oldName}' now. Nothing was changed.";
                foreach (var a in areas.OfType<JObject>().Where(IsThisPart).ToList())
                    a.Remove();
                if (old != null)
                    areas.Add(old);
                return null;
            }
            if (change.Section.StartsWith(SectionDefaultPrefix, StringComparison.Ordinal))
            {
                var defaults = DefaultsIn(root, true);
                var variation = change.Section.Substring(SectionDefaultPrefix.Length);
                foreach (var p in defaults.Properties().Where(p => p.Name == variation).ToList())
                    p.Remove();
                if (old != null)
                    defaults[variation] = old;
                return null;
            }
            var global = GlobalSections.FirstOrDefault(g => g.Key == change.Section);
            if (global.Property == null)
                return $"Unknown part '{change.Section}'.";
            SetProp(root, global.Property, old ?? JValue.CreateNull());
            return null;
        }

        /// <summary>The Stats map of the row a logged stat change edited, in a store copy. Null when the row does not exist (and
        /// <paramref name="create"/> is false). <paramref name="zoneMissing"/> = the change names a zone that is gone.</summary>
        internal static JObject StatsIn(JObject root, ZoneControlChange c, bool create, out bool zoneMissing)
        {
            zoneMissing = false;
            ZoneScaling.ZoneRank.TryParse(c.Rank, out var rank);
            JObject profile;
            if (c.Zone != null)
            {
                var area = AreaIn(root, c.Zone);
                if (area == null)
                {
                    zoneMissing = true;
                    return null;
                }
                var scaling = Child(area, nameof(ControlledArea.Profile), create);
                if (c.Wcid.HasValue)
                    profile = Child(Child(scaling, nameof(ZoneScaling.ZoneScalingProfile.WcidOverrides), create), c.Wcid.Value.ToString(), create);
                else
                    profile = Child(scaling, nameof(ZoneScaling.ZoneScalingProfile.Minion), create);
            }
            else
            {
                var defaults = DefaultsIn(root, create);
                profile = Child(Child(defaults, (c.Variation ?? -1).ToString(), create), nameof(VariationDefault.Profile), create);
            }
            if (rank != ZoneScaling.ZcRank.None)
                profile = Child(Child(profile, nameof(ZoneScaling.ZoneVariantProfile.Ranks), create), ZoneScaling.ZoneRank.Key(rank), create);
            return Child(profile, nameof(ZoneScaling.ZoneVariantProfile.Stats), create);

            static JObject Child(JObject parent, string name, bool make)
            {
                if (parent == null)
                    return null;
                if (Prop(parent, name) is JObject found)
                    return found;
                if (!make)
                    return null;
                var made = new JObject();
                parent[name] = made;
                return made;
            }
        }

        /// <summary>Runs <paramref name="work"/> under the manager lock, so a multi-step job (Revert all) sees no edit land part way.
        /// The lock is re-entrant: the edits inside take it again.</summary>
        public static T Locked<T>(Func<T> work)
        {
            EnsureInitialized();
            lock (_lock)
                return work();
        }

        /// <summary>One part of the live store, canonical, or null when it does not exist.</summary>
        internal static string CurrentSectionCanonical(string key)
        {
            EnsureInitialized();
            lock (_lock)
                return SectionsOf(BuildStore()).TryGetValue(key, out var s) ? s.Canonical : null;
        }

        /// <summary>What changed inside one part, as short "path: old -> new" lines for the History page. Values are cut short.</summary>
        public static List<string> DescribeSectionChange(string oldJson, string newJson, int max = 12)
        {
            // keyed by the lowercased path (camelCase and PascalCase saves match), shown with the names as stored
            static void Flatten(JToken t, string path, Dictionary<string, (string Path, string Value)> into)
            {
                switch (t)
                {
                    case JObject o:
                        if (!o.HasValues && path.Length > 0) into[path.ToLowerInvariant()] = (path, "{}");
                        foreach (var p in o.Properties())
                            Flatten(p.Value, path.Length == 0 ? p.Name : path + "." + p.Name, into);
                        break;
                    case JArray a:
                        into[path.ToLowerInvariant()] = (path, Canonical(a.ToString(Formatting.None)));
                        break;
                    default:
                        into[path.ToLowerInvariant()] = (path, t.Type == JTokenType.Null ? "null" : t.ToString(Formatting.None));
                        break;
                }
            }
            static string Cut(string s) => s == null ? "(none)" : s.Length > 80 ? s.Substring(0, 77) + "..." : s;

            if (oldJson == null)
                return new List<string> { "Created." };
            if (newJson == null)
                return new List<string> { "Removed." };
            var a = new Dictionary<string, (string Path, string Value)>(StringComparer.Ordinal);
            var b = new Dictionary<string, (string Path, string Value)>(StringComparer.Ordinal);
            Flatten(JToken.Parse(oldJson), "", a);
            Flatten(JToken.Parse(newJson), "", b);
            var lines = a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal)
                .Where(k => (a.TryGetValue(k, out var x) ? x.Value : null) != (b.TryGetValue(k, out var y) ? y.Value : null))
                .Select(k =>
                {
                    var hasA = a.TryGetValue(k, out var x);
                    var hasB = b.TryGetValue(k, out var y);
                    var path = hasB ? y.Path : x.Path;
                    return $"{(path.Length == 0 ? "value" : path)}: {Cut(hasA ? x.Value : null)} -> {Cut(hasB ? y.Value : null)}";
                })
                .ToList();
            if (lines.Count > max)
                lines = lines.Take(max).Append($"... and {lines.Count - max} more").ToList();
            return lines;
        }
    }
}
