using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Settings transfer (owner 2026-10-25): one JSON file carries a server's balance settings - the Zone Control store,
    /// the weapon scaling store and every charm's settings - from the test server to prod. /settings export writes it,
    /// /settings import previews it and, with "apply", backs up the target first and applies it live
    /// (SettingsTransferCommands). This class is the pure part: the file, the preview, the merge and the name rules.
    /// No database, no world - so it is unit tested.
    /// </summary>
    public static class SettingsTransfer
    {
        public const string Format = "acecustom-settings";
        public const int Version = 1;
        public const string Folder = "SettingsExports";

        /// <summary>Zone Control parts that belong to the SERVER, not the balance: an import keeps the target's own
        /// (bounty setup, gear-lock spots, which ladder version each tier has applied) unless asked to copy them too.</summary>
        public static readonly string[] ServerOwnedZoneKeys = { "ladderApplies", "gearLockSpots", "zoneWideBounty", "regionBounties" };

        /// <summary>One settings file. Zone / weapon scaling are the stores' own JSON; charms are charm -> key -> value.</summary>
        public sealed class Bundle
        {
            public string Source;
            public DateTime CreatedUtc;
            public JObject ZoneControl;
            public JObject WeaponScaling;
            public SortedDictionary<string, SortedDictionary<string, string>> Charms = new(StringComparer.Ordinal);
        }

        public static Bundle Make(string source, string zoneJson, string weaponJson, IDictionary<string, IDictionary<string, string>> charms, DateTime createdUtc)
        {
            var b = new Bundle
            {
                Source = source ?? "",
                CreatedUtc = createdUtc,
                ZoneControl = string.IsNullOrWhiteSpace(zoneJson) ? null : JObject.Parse(zoneJson),
                WeaponScaling = string.IsNullOrWhiteSpace(weaponJson) ? null : JObject.Parse(weaponJson),
            };
            if (charms != null)
                foreach (var (name, keys) in charms)
                    b.Charms[name] = new SortedDictionary<string, string>(keys ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            return b;
        }

        public static string Serialize(Bundle b)
        {
            var root = new JObject
            {
                ["format"] = Format,
                ["version"] = Version,
                ["source"] = b.Source ?? "",
                ["createdUtc"] = b.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["zoneControl"] = b.ZoneControl,
                ["weaponScaling"] = b.WeaponScaling,
                ["charms"] = JObject.FromObject(b.Charms),
            };
            return root.ToString(Formatting.Indented);
        }

        /// <summary>Reads a settings file; throws FormatException with a plain reason when it is not one this build reads.</summary>
        public static Bundle Parse(string json)
        {
            JObject root;
            try { root = JObject.Parse(json ?? ""); }
            catch (JsonException ex) { throw new FormatException("the file is not valid JSON: " + ex.Message); }

            if ((string)root["format"] != Format)
                throw new FormatException("this is not a settings export (no \"format\": \"" + Format + "\").");
            var version = (int?)root["version"] ?? 0;
            if (version < 1 || version > Version)
                throw new FormatException($"settings file version {version} - this server reads up to version {Version}. Update the server first.");

            var b = new Bundle
            {
                Source = (string)root["source"] ?? "",
                CreatedUtc = DateTime.TryParse((string)root["createdUtc"], null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var t) ? t : DateTime.MinValue,
                ZoneControl = root["zoneControl"] as JObject,
                WeaponScaling = root["weaponScaling"] as JObject,
            };
            if (root["charms"] is JObject charms)
                foreach (var charm in charms.Properties())
                    if (charm.Value is JObject keys)
                        b.Charms[charm.Name] = new SortedDictionary<string, string>(
                            keys.Properties().ToDictionary(p => p.Name, p => p.Value.Type == JTokenType.Null ? "" : p.Value.ToString()), StringComparer.Ordinal);
            return b;
        }

        // -- names ---------------------------------------------------------------------------------------------------

        private static readonly Regex NameRule = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled);

        /// <summary>A file NAME (never a path) in the transfer folder: letters, digits, dot, dash, underscore, at most 64,
        /// ".json" added when missing. Null when the name is not allowed.</summary>
        public static string FileNameFor(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim();
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5);
            if (!NameRule.IsMatch(name) || name.Contains("..")) return null;
            return name + ".json";
        }

        // -- import: what the target ends up with ---------------------------------------------------------------------

        /// <summary>The Zone Control store the target gets: the incoming store, with the target's own server-owned parts
        /// kept (unless copyServerOwned). Null when the file carries no Zone Control store.</summary>
        public static JObject MergeZoneControl(JObject target, JObject incoming, bool copyServerOwned)
        {
            if (incoming == null) return null;
            var merged = (JObject)incoming.DeepClone();
            if (copyServerOwned || target == null) return merged;
            foreach (var key in ServerOwnedZoneKeys)
            {
                var own = FindProperty(target, key);
                var theirs = FindProperty(merged, key);
                if (theirs != null) theirs.Remove();
                if (own != null) merged.Add(new JProperty(own.Name, own.Value.DeepClone()));
            }
            return merged;
        }

        private static JProperty FindProperty(JObject o, string name) =>
            o.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        // -- the preview -----------------------------------------------------------------------------------------------

        /// <summary>What an import would change, as short plain-ASCII lines: per tier and per zone for Zone Control, a count
        /// for weapon scaling, every changed key for charms. Empty = no changes. The target side is what it has NOW.</summary>
        public static List<string> Preview(Bundle current, Bundle incoming, bool copyServerOwned, int maxLines = 40)
        {
            var lines = new List<string>();

            if (incoming.ZoneControl != null)
            {
                var after = MergeZoneControl(current.ZoneControl, incoming.ZoneControl, copyServerOwned);
                var zone = ZonePreview(current.ZoneControl ?? new JObject(), after);
                lines.Add(zone.Count == 0 ? "Zone Control: no changes." : $"Zone Control: {zone.Count} place(s) change:");
                lines.AddRange(zone.Take(maxLines).Select(l => "  " + l));
                if (zone.Count > maxLines) lines.Add($"  ... and {zone.Count - maxLines} more.");
                if (!copyServerOwned)
                    lines.Add("  (kept as this server has them: " + string.Join(", ", ServerOwnedZoneKeys) + ")");
            }
            else lines.Add("Zone Control: not in the file - left as it is.");

            if (incoming.WeaponScaling != null)
            {
                var n = Differences(current.WeaponScaling ?? new JObject(), incoming.WeaponScaling, "", 1000).Count;
                lines.Add(n == 0 ? "Weapon scaling: no changes." : $"Weapon scaling: {n} value(s) change.");
            }
            else lines.Add("Weapon scaling: not in the file - left as it is.");

            var charmLines = new List<string>();
            foreach (var (name, keys) in incoming.Charms)
            {
                current.Charms.TryGetValue(name, out var now);
                foreach (var (key, value) in keys)
                {
                    var old = now != null && now.TryGetValue(key, out var v) ? v : null;
                    if (old != value) charmLines.Add($"{name}.{key}: {Show(old)} -> {Show(value)}");
                }
            }
            lines.Add(charmLines.Count == 0 ? "Charms: no changes." : $"Charms: {charmLines.Count} setting(s) change:");
            lines.AddRange(charmLines.Take(maxLines).Select(l => "  " + l));
            if (charmLines.Count > maxLines) lines.Add($"  ... and {charmLines.Count - maxLines} more.");
            return lines;
        }

        /// <summary>Zone Control changes grouped by place: "T15 Default: 3 stat(s) (monster_damage_mult 1.2 -> 1.26, ...)",
        /// "zone Tou Tou 15: 4 value(s)", "removed zone X" / "new zone X", and anything else by its top-level key.</summary>
        public static List<string> ZonePreview(JObject before, JObject after)
        {
            var lines = new List<string>();

            // tier Defaults, by tier number
            var vb = Prop(before, "variationDefaults") as JObject ?? new JObject();
            var va = Prop(after, "variationDefaults") as JObject ?? new JObject();
            foreach (var tier in vb.Properties().Select(p => p.Name).Union(va.Properties().Select(p => p.Name)).OrderBy(TierOrder))
            {
                var diffs = Differences(vb[tier] ?? new JObject(), va[tier] ?? new JObject(), "", 1000);
                if (diffs.Count == 0) continue;
                var stats = diffs.Where(d => d.Path.StartsWith("profile/stats/", StringComparison.Ordinal)).ToList();
                var sample = string.Join(", ", stats.Take(3).Select(d => $"{StatName(d.Path)} {Show(d.Before)} -> {Show(d.After)}"));
                lines.Add($"T{tier} Default: {diffs.Count} value(s)" + (sample.Length > 0 ? $" ({sample}{(stats.Count > 3 ? ", ..." : "")})" : ""));
            }

            // zones, by name
            var ab = (Prop(before, "areas") as JArray ?? new JArray()).OfType<JObject>().GroupBy(a => (string)a["name"] ?? "").ToDictionary(g => g.Key, g => g.First());
            var aa = (Prop(after, "areas") as JArray ?? new JArray()).OfType<JObject>().GroupBy(a => (string)a["name"] ?? "").ToDictionary(g => g.Key, g => g.First());
            foreach (var name in ab.Keys.Union(aa.Keys).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                if (!aa.ContainsKey(name)) { lines.Add($"zone {name}: REMOVED"); continue; }
                if (!ab.ContainsKey(name)) { lines.Add($"zone {name}: new"); continue; }
                var n = Differences(ab[name], aa[name], "", 1000).Count;
                if (n > 0) lines.Add($"zone {name}: {n} value(s)");
            }

            // everything else at the top level
            foreach (var key in TopKeys(before).Union(TopKeys(after), StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (key.Equals("variationDefaults", StringComparison.OrdinalIgnoreCase) || key.Equals("areas", StringComparison.OrdinalIgnoreCase)) continue;
                var n = Differences(Prop(before, key) ?? JValue.CreateNull(), Prop(after, key) ?? JValue.CreateNull(), "", 1000).Count;
                if (n > 0) lines.Add($"{key}: {n} value(s)");
            }
            return lines;
        }

        public sealed class Difference
        {
            public string Path;
            public JToken Before, After;
        }

        /// <summary>Every leaf that differs between two JSON values (a path like "profile/stats/x/base"); stops at limit.</summary>
        public static List<Difference> Differences(JToken before, JToken after, string path, int limit)
        {
            var list = new List<Difference>();
            Walk(before, after, path, list, limit);
            return list;
        }

        private static void Walk(JToken a, JToken b, string path, List<Difference> list, int limit)
        {
            if (list.Count >= limit) return;
            if (a is JObject oa && b is JObject ob)
            {
                foreach (var name in oa.Properties().Select(p => p.Name).Union(ob.Properties().Select(p => p.Name)))
                    Walk(oa[name], ob[name], path.Length == 0 ? name : path + "/" + name, list, limit);
                return;
            }
            if (a is JArray xa && b is JArray xb && xa.Count == xb.Count)
            {
                for (var i = 0; i < xa.Count; i++)
                    Walk(xa[i], xb[i], path + "/" + i, list, limit);
                return;
            }
            if (!JToken.DeepEquals(a ?? JValue.CreateNull(), b ?? JValue.CreateNull()))
                list.Add(new Difference { Path = path, Before = a, After = b });
        }

        private static JToken Prop(JObject o, string name) => o == null ? null : FindProperty(o, name)?.Value;

        private static IEnumerable<string> TopKeys(JObject o) => o?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>();

        private static int TierOrder(string tier) => int.TryParse(tier, out var t) ? t : int.MaxValue;

        private static string StatName(string path)
        {
            var parts = path.Split('/');
            return parts.Length >= 3 ? parts[2] : path;
        }

        private static string Show(JToken v)
        {
            if (v == null || v.Type == JTokenType.Null) return "unset";
            if (v is JObject o && o["base"] != null) return Show(o["base"]);
            var s = v.Type == JTokenType.Float ? ((double)v).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) : v.ToString(Formatting.None);
            return Show(s);
        }

        private static string Show(string v)
        {
            if (v == null) return "unset";
            var s = v.Length > 40 ? v.Substring(0, 37) + "..." : v;
            // the preview goes to the game client: 7-bit ASCII only
            return new string(s.Select(c => c < 32 || c > 126 ? '?' : c).ToArray());
        }
    }
}
