using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace ACE.Server.WorldObjects
{
    public partial class PetDevice : WorldObject
    {
        /// <summary>
        /// The breeding parity harness. A [REPLAY] blob is one line of JSON that the website's
        /// breedingModel.ts emits in verbose mode (and that the live breed logs when
        /// pet_breeding_verbose_logging is on):
        ///
        ///   {"model":"...","parentA":{...},"parentB":{...},"config":{...},"options":{...},"rngDraws":[...],"baby":{...}}
        ///
        /// <see cref="Run"/> feeds the blob's inputs and draws through <see cref="BreedingMath.Simulate"/>
        /// and compares the result with the blob's baby. The unit tests (PetBreedingParityTests) and the
        /// @breed-replay developer command both go through here.
        /// </summary>
        public static class BreedingReplay
        {
            /// <summary>The model tag the server writes into blobs it logs itself.</summary>
            public const string ServerModel = "server";

            public sealed class Blob
            {
                public string Model;
                public BreedingMath.BreedingInputs Inputs;
                public double[] RngDraws = Array.Empty<double>();
                /// <summary>The baby the blob's author computed; null when the blob has no "baby" object.</summary>
                public BreedingMath.BreedingGenetics? ExpectedBaby;
            }

            /// <summary>Replays a fixed list of draws and refuses to run past its end.</summary>
            public sealed class ScriptedRng
            {
                private readonly double[] values;
                public int Consumed { get; private set; }
                public int Remaining => values.Length - Consumed;

                public ScriptedRng(double[] values) => this.values = values ?? Array.Empty<double>();

                public double Next()
                {
                    if (Consumed >= values.Length)
                        throw new InvalidOperationException($"replay ran out of rng draws after {values.Length}");
                    return values[Consumed++];
                }
            }

            /// <summary>The result of replaying one blob.</summary>
            public sealed class Result
            {
                public Blob Blob;
                public BreedingMath.BreedingOutcome Outcome;
                public int DrawsConsumed;
                /// <summary>True when the blob had an expected baby, it matched, and every draw was consumed.</summary>
                public bool Pass;
                public List<string> Mismatches = new();
            }

            // ----------------------------------------------------------------------------------
            // Parsing
            // ----------------------------------------------------------------------------------

            private static readonly Regex UnquotedKey = new(@"([{,])\s*([A-Za-z_][A-Za-z0-9_]*)\s*:", RegexOptions.Compiled);
            private static readonly Regex UnquotedModel = new(@"""model"":\s*([^,}""]+)", RegexOptions.Compiled);

            /// <summary>
            /// Pulls the JSON object out of a pasted line (a leading "[REPLAY]" or log prefix is fine) and
            /// repairs a blob whose double quotes were stripped by CommandManager.ParseCommand: keys get
            /// re-quoted, and the model value (the only string in the format) too. Blobs written with
            /// single quotes are accepted as they are (Newtonsoft reads them).
            /// </summary>
            public static string ExtractJson(string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    throw new FormatException("empty replay text");

                var start = text.IndexOf('{');
                var end = text.LastIndexOf('}');
                if (start < 0 || end <= start)
                    throw new FormatException("no JSON object found in the replay text");

                var json = text.Substring(start, end - start + 1);
                if (json.Contains('"') || json.Contains('\''))
                    return json;

                json = UnquotedKey.Replace(json, "$1\"$2\":");
                json = UnquotedModel.Replace(json, m => "\"model\":\"" + m.Groups[1].Value.Trim() + "\"");
                return json;
            }

            public static Blob Parse(string text)
            {
                var json = ExtractJson(text);
                var root = JObject.Parse(json);

                var blob = new Blob
                {
                    Model = root.Value<string>("model") ?? "",
                    Inputs = new BreedingMath.BreedingInputs
                    {
                        ParentA = ReadGenetics(RequireObject(root, "parentA")),
                        ParentB = ReadGenetics(RequireObject(root, "parentB")),
                        Config = ReadConfig(RequireObject(root, "config")),
                        Options = ReadOptions(root["options"] as JObject),
                    },
                };

                if (root["rngDraws"] is JArray draws)
                {
                    blob.RngDraws = new double[draws.Count];
                    for (var i = 0; i < draws.Count; i++)
                        blob.RngDraws[i] = draws[i].Value<double>();
                }

                if (root["baby"] is JObject baby)
                    blob.ExpectedBaby = ReadGenetics(baby);

                return blob;
            }

            private static JObject RequireObject(JObject root, string name)
                => root[name] as JObject ?? throw new FormatException($"replay is missing the \"{name}\" object");

            private static int Int(JObject o, string name, int fallback = 0)
            {
                var t = o[name];
                if (t == null || t.Type == JTokenType.Null) return fallback;
                return (int)Math.Truncate(t.Value<double>());
            }

            private static long Long(JObject o, string name, long fallback = 0)
            {
                var t = o[name];
                if (t == null || t.Type == JTokenType.Null) return fallback;
                return (long)Math.Truncate(t.Value<double>());
            }

            private static double Dbl(JObject o, string name, double fallback = 0.0)
            {
                var t = o[name];
                if (t == null || t.Type == JTokenType.Null) return fallback;
                return t.Value<double>();
            }

            private static bool Bool(JObject o, string name, bool fallback = false)
            {
                var t = o[name];
                if (t == null || t.Type == JTokenType.Null) return fallback;
                if (t.Type == JTokenType.Boolean) return t.Value<bool>();
                var s = t.ToString().Trim();
                return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";
            }

            public static BreedingMath.BreedingGenetics ReadGenetics(JObject o) => new()
            {
                GearDamage = Int(o, "gearDamage"),
                GearDamageResist = Int(o, "gearDamageResist"),
                GearCrit = Int(o, "gearCrit"),
                GearCritDamage = Int(o, "gearCritDamage"),
                GearCritResist = Int(o, "gearCritResist"),
                GearCritDamageResist = Int(o, "gearCritDamageResist"),
                Dmg = Int(o, "dmg"),
                Dr = Int(o, "dr"),
                Crit = Int(o, "crit"),
                Vit = Int(o, "vit"),
                Pot = Int(o, "pot"),
                PotencyStored = Int(o, "potencyStored"),
            };

            public static BreedingMath.BreedingConfig ReadConfig(JObject o) => new()
            {
                BaseMutationChance = Dbl(o, "baseMutationChance"),
                PotencyMutationChance = Dbl(o, "potencyMutationChance"),
                MutationDecayRate = Dbl(o, "mutationDecayRate"),
                MutationMinFloor = Dbl(o, "mutationMinFloor"),
                DamageMutationStep = Int(o, "damageMutationStep"),
                DrMutationStep = Int(o, "drMutationStep"),
                CritMutationStep = Int(o, "critMutationStep"),
                VitalityMutationStep = Int(o, "vitalityMutationStep"),
                PotencyMutationStep = Int(o, "potencyMutationStep"),
                PotencySoftCap = Int(o, "potencySoftCap"),
                PotencyHardCap = Long(o, "potencyHardCap"),
                PotencyMaxStored = Long(o, "potencyMaxStored"),
                MaxStatMutations = Int(o, "maxStatMutations"),
                ForceMutation = Bool(o, "forceMutation"),
                GuardianEnabled = Bool(o, "guardianEnabled"),
            };

            public static BreedingMath.BreedingOptions ReadOptions(JObject o)
            {
                if (o == null)
                    return new BreedingMath.BreedingOptions { GuardianKilled = true };
                return new BreedingMath.BreedingOptions
                {
                    IncenseA = Dbl(o, "incenseA"),
                    IncenseB = Dbl(o, "incenseB"),
                    CatalystA = Bool(o, "catalystA"),
                    CatalystB = Bool(o, "catalystB"),
                    GuardianKilled = Bool(o, "guardianKilled", true),
                };
            }

            // ----------------------------------------------------------------------------------
            // Serialising (same key order as breedingModel.ts so the two blobs diff cleanly)
            // ----------------------------------------------------------------------------------

            public static JObject GeneticsToJson(in BreedingMath.BreedingGenetics g) => new()
            {
                ["gearDamage"] = g.GearDamage,
                ["gearDamageResist"] = g.GearDamageResist,
                ["gearCrit"] = g.GearCrit,
                ["gearCritDamage"] = g.GearCritDamage,
                ["gearCritResist"] = g.GearCritResist,
                ["gearCritDamageResist"] = g.GearCritDamageResist,
                ["dmg"] = g.Dmg,
                ["dr"] = g.Dr,
                ["crit"] = g.Crit,
                ["vit"] = g.Vit,
                ["pot"] = g.Pot,
                ["potencyStored"] = g.PotencyStored,
            };

            public static JObject ConfigToJson(in BreedingMath.BreedingConfig c) => new()
            {
                ["baseMutationChance"] = c.BaseMutationChance,
                ["potencyMutationChance"] = c.PotencyMutationChance,
                ["mutationDecayRate"] = c.MutationDecayRate,
                ["mutationMinFloor"] = c.MutationMinFloor,
                ["damageMutationStep"] = c.DamageMutationStep,
                ["drMutationStep"] = c.DrMutationStep,
                ["critMutationStep"] = c.CritMutationStep,
                ["vitalityMutationStep"] = c.VitalityMutationStep,
                ["potencyMutationStep"] = c.PotencyMutationStep,
                ["potencySoftCap"] = c.PotencySoftCap,
                ["potencyHardCap"] = c.PotencyHardCap,
                ["potencyMaxStored"] = c.PotencyMaxStored,
                ["maxStatMutations"] = c.MaxStatMutations,
                ["forceMutation"] = c.ForceMutation,
                ["guardianEnabled"] = c.GuardianEnabled,
            };

            public static string ToJson(in BreedingMath.BreedingInputs inputs, IEnumerable<double> rngDraws, in BreedingMath.BreedingGenetics baby, string model = ServerModel)
            {
                var draws = new JArray();
                foreach (var d in rngDraws ?? Array.Empty<double>())
                    draws.Add(Math.Round(d, 6));

                var root = new JObject
                {
                    ["model"] = model,
                    ["parentA"] = GeneticsToJson(inputs.ParentA),
                    ["parentB"] = GeneticsToJson(inputs.ParentB),
                    ["config"] = ConfigToJson(inputs.Config),
                    ["options"] = new JObject
                    {
                        ["incenseA"] = inputs.Options.IncenseA,
                        ["incenseB"] = inputs.Options.IncenseB,
                        ["catalystA"] = inputs.Options.CatalystA,
                        ["catalystB"] = inputs.Options.CatalystB,
                        ["guardianKilled"] = inputs.Options.GuardianKilled,
                    },
                    ["rngDraws"] = draws,
                    ["baby"] = GeneticsToJson(baby),
                };
                return root.ToString(Newtonsoft.Json.Formatting.None);
            }

            // ----------------------------------------------------------------------------------
            // Running
            // ----------------------------------------------------------------------------------

            /// <summary>Runs the blob through BreedingMath.Simulate and compares with its expected baby.</summary>
            public static Result Run(Blob blob)
            {
                if (blob == null) throw new ArgumentNullException(nameof(blob));

                var rng = new ScriptedRng(blob.RngDraws);
                var result = new Result { Blob = blob };
                try
                {
                    result.Outcome = BreedingMath.Simulate(blob.Inputs, rng.Next);
                }
                catch (InvalidOperationException ex)
                {
                    result.Mismatches.Add(ex.Message);
                }
                result.DrawsConsumed = rng.Consumed;

                if (rng.Remaining > 0)
                    result.Mismatches.Add($"{rng.Remaining} of {blob.RngDraws.Length} rng draws were never consumed (draw order differs)");

                if (result.Outcome != null)
                {
                    if (blob.ExpectedBaby.HasValue)
                        CompareGenetics(blob.ExpectedBaby.Value, result.Outcome.Baby, result.Mismatches);
                    else
                        result.Mismatches.Add("blob has no \"baby\" to compare against");
                }

                result.Pass = result.Mismatches.Count == 0;
                return result;
            }

            /// <summary>Appends one line per differing field ("field: expected X, got Y").</summary>
            public static void CompareGenetics(in BreedingMath.BreedingGenetics expected, in BreedingMath.BreedingGenetics actual, List<string> mismatches)
            {
                void Check(string name, int e, int a)
                {
                    if (e != a) mismatches.Add($"{name}: expected {e}, got {a}");
                }
                Check("gearDamage", expected.GearDamage, actual.GearDamage);
                Check("gearDamageResist", expected.GearDamageResist, actual.GearDamageResist);
                Check("gearCrit", expected.GearCrit, actual.GearCrit);
                Check("gearCritDamage", expected.GearCritDamage, actual.GearCritDamage);
                Check("gearCritResist", expected.GearCritResist, actual.GearCritResist);
                Check("gearCritDamageResist", expected.GearCritDamageResist, actual.GearCritDamageResist);
                Check("dmg", expected.Dmg, actual.Dmg);
                Check("dr", expected.Dr, actual.Dr);
                Check("crit", expected.Crit, actual.Crit);
                Check("vit", expected.Vit, actual.Vit);
                Check("pot", expected.Pot, actual.Pot);
                Check("potencyStored", expected.PotencyStored, actual.PotencyStored);
            }

            /// <summary>
            /// Short, client-safe report (7-bit ASCII, "\n" line breaks) for a replay result: the baby
            /// the server computed, the summon ratings, and PASS/FAIL against the blob's baby.
            /// </summary>
            public static string FormatReport(Result r)
            {
                var sb = new StringBuilder();
                var o = r.Outcome;
                sb.Append($"[BreedReplay] model {(string.IsNullOrEmpty(r.Blob.Model) ? "?" : r.Blob.Model)}, draws {r.DrawsConsumed}/{r.Blob.RngDraws.Length} consumed\n");
                if (o != null)
                {
                    sb.Append($"  stat roll {o.StatRoll:0.0000} vs chance {o.StatChance:0.0000} -> {(o.StatMutated ? (r.Blob.Inputs.Config.ForceMutation ? "FORCED" : "MUTATED") : "no mutation")}");
                    if (o.StatLine != BreedingMath.MutationLine.None)
                        sb.Append($" ({BreedingMath.LineName(o.StatLine)} +{o.StatStep})");
                    else if (o.StatAllLinesCapped)
                        sb.Append(" (every line capped)");
                    sb.Append('\n');
                    sb.Append($"  potency roll {o.PotencyRoll:0.0000} vs chance {o.PotencyChance:0.0000} -> {(o.PotencyApplied ? $"+{o.PotencyStep}" : o.PotencyMutated ? "capped" : "no mutation")}\n");
                    sb.Append($"  guardian {(o.GuardianSpawned ? (o.GuardianKilled ? "spawned and killed" : "spawned, not killed") : "not spawned")}");
                    if (o.BlessingLine != BreedingMath.MutationLine.None)
                        sb.Append($"; blessing {BreedingMath.LineName(o.BlessingLine)} +{o.BlessingStep}");
                    else if (o.BlessingAllLinesCapped)
                        sb.Append("; blessing found every line capped");
                    sb.Append('\n');
                    sb.Append($"  baby   {o.Baby}\n");
                    sb.Append($"  adult  {BreedingMath.SummonedStats(o.Baby, r.Blob.Inputs.Config)}\n");
                    sb.Append($"  stage1 {BreedingMath.SummonedStats(o.Baby, r.Blob.Inputs.Config, 1)}\n");
                }
                if (r.Blob.ExpectedBaby.HasValue)
                    sb.Append($"  blob   {r.Blob.ExpectedBaby.Value}\n");

                if (r.Pass)
                    sb.Append("  RESULT: PASS - server BreedingMath reproduces the blob's baby exactly.");
                else
                {
                    sb.Append("  RESULT: FAIL");
                    foreach (var m in r.Mismatches)
                        sb.Append("\n    - ").Append(m);
                }
                return sb.ToString();
            }

            /// <summary>Parse + run + format in one call. Never throws: a bad blob yields a FAIL report.</summary>
            public static bool TryRunText(string text, out string report)
            {
                try
                {
                    var blob = Parse(text);
                    var result = Run(blob);
                    report = FormatReport(result);
                    return result.Pass;
                }
                catch (Exception ex)
                {
                    report = $"[BreedReplay] RESULT: FAIL - could not read the blob: {ex.Message}";
                    return false;
                }
            }
        }
    }
}
