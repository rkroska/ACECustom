using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Mutations;
using ACE.Server.Factories;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Recovers a weapon's untinkered values for the forge, by inverting the tinkers in its TinkerLog.
    ///
    /// Tinkers are the embedded mutation scripts in Entity/Mutations/Recipes ("3800001A - Iron.txt": Damage += 1).
    /// The inverse is computed from those same parsed scripts, so a script edit never leaves a hand-written
    /// table out of date. Only the stats the forge reads (<see cref="ForgeStats"/>) are inverted; a tinker's
    /// effect on anything else (imbues, value, item difficulty) is the main weapon's own business.
    ///
    /// Every result is VERIFIED before it is trusted: the recovered values are put on a throwaway copy of the
    /// weapon and the game's own MutationFilter replays the tinkers, in log order. Unless that lands exactly on
    /// the weapon's current values the weapon is refused. So a clamp that lost information (Oak at 0), a float
    /// that does not round-trip or a script that does something new can never create or erase a stat - the
    /// worst case is a refusal.
    /// </summary>
    public static class TinkerReversal
    {
        public enum Status
        {
            /// <summary>Base values recovered and verified (or the weapon was never tinkered).</summary>
            Ok,
            /// <summary>Tinkered, but no TinkerLog.</summary>
            NoLog,
            /// <summary>The log's entry count differs from NumTimesTinkered (tinkers from before logging existed).</summary>
            CountMismatch,
            /// <summary>A log entry is not a material with a tinker script.</summary>
            UnknownEntry,
            /// <summary>A tinker's effect on a forge stat cannot be inverted, or the replay did not reproduce the weapon.</summary>
            Irreversible,
        }

        public sealed class Result
        {
            public Status Status;
            /// <summary>ASCII, for logs and admin output.</summary>
            public string Detail;
            /// <summary>Untinkered value of each forge stat; null = the stat is absent.</summary>
            public Dictionary<(StatType Type, int Idx), double?> Base = new();
            public List<MaterialType> Tinkers = new();

            public double? Get(PropertyInt p) => Base.TryGetValue((StatType.Int, (int)p), out var v) ? v : null;
            public double? Get(PropertyFloat p) => Base.TryGetValue((StatType.Float, (int)p), out var v) ? v : null;
        }

        /// <summary>The stats ForgeWeaponReader reads. Only these are inverted and verified.</summary>
        public static readonly (StatType Type, int Idx)[] ForgeStats =
        {
            (StatType.Int, (int)PropertyInt.Damage),
            (StatType.Int, (int)PropertyInt.WeaponTime),
            (StatType.Int, (int)PropertyInt.ItemSpellcraft),
            (StatType.Int, (int)PropertyInt.ItemMaxMana),
            (StatType.Float, (int)PropertyFloat.DamageVariance),
            (StatType.Float, (int)PropertyFloat.WeaponOffense),
            (StatType.Float, (int)PropertyFloat.WeaponDefense),
            (StatType.Float, (int)PropertyFloat.WeaponMissileDefense),
            (StatType.Float, (int)PropertyFloat.WeaponMagicDefense),
            (StatType.Float, (int)PropertyFloat.DamageMod),
            (StatType.Float, (int)PropertyFloat.ElementalDamageMod),
            // armour and shields
            (StatType.Int, (int)PropertyInt.ArmorLevel),
            (StatType.Float, (int)PropertyFloat.ArmorModVsSlash),
            (StatType.Float, (int)PropertyFloat.ArmorModVsPierce),
            (StatType.Float, (int)PropertyFloat.ArmorModVsBludgeon),
            (StatType.Float, (int)PropertyFloat.ArmorModVsCold),
            (StatType.Float, (int)PropertyFloat.ArmorModVsFire),
            (StatType.Float, (int)PropertyFloat.ArmorModVsAcid),
            (StatType.Float, (int)PropertyFloat.ArmorModVsElectric),
            (StatType.Float, (int)PropertyFloat.ArmorModVsNether),
        };

        /// <summary>Floats are compared with this tolerance; the stored values are doubles from float-ish math.</summary>
        private const double Epsilon = 1e-6;

        private static readonly Lazy<Dictionary<MaterialType, uint>> materialScripts = new(BuildMaterialScripts);

        /// <summary>
        /// Material -> tinker script id, from the embedded resource names ("...Recipes.3800001A - Iron.txt").
        /// Names are matched to MaterialType by letters only ("Black Opal" -> BlackOpal).
        /// </summary>
        private static Dictionary<MaterialType, uint> BuildMaterialScripts()
        {
            var byName = Enum.GetValues(typeof(MaterialType)).Cast<MaterialType>()
                .GroupBy(m => Normalize(m.ToString())).ToDictionary(g => g.Key, g => g.First());

            var map = new Dictionary<MaterialType, uint>();
            var pattern = new Regex(@"Recipes\.([0-9A-Fa-f]{8}) - (.+)\.txt$");
            foreach (var resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                var m = pattern.Match(resource);
                if (!m.Success)
                    continue;
                if (byName.TryGetValue(Normalize(m.Groups[2].Value), out var material))
                    map[material] = Convert.ToUInt32(m.Groups[1].Value, 16);
            }
            return map;
        }

        private static string Normalize(string s) => new string(s.Where(char.IsLetter).ToArray()).ToLowerInvariant();

        /// <summary>Tinker script id for a material, if it has one.</summary>
        public static bool TryGetScript(MaterialType material, out uint scriptId) => materialScripts.Value.TryGetValue(material, out scriptId);

        /// <summary>Recovers and verifies the untinkered forge stats of <paramref name="wo"/>. Never changes the weapon.</summary>
        public static Result Strip(WorldObject wo)
        {
            var r = new Result();
            foreach (var key in ForgeStats)
                r.Base[key] = Read(wo, key);

            var count = wo.NumTimesTinkered;
            if (count <= 0)
            {
                r.Status = Status.Ok;
                r.Detail = "never tinkered";
                return r;
            }

            if (string.IsNullOrEmpty(wo.TinkerLog))
                return Fail(r, Status.NoLog, $"tinkered {count} times but has no tinker log");

            var entries = wo.TinkerLog.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (entries.Length != count)
                return Fail(r, Status.CountMismatch, $"tinker log has {entries.Length} entries but the weapon was tinkered {count} times");

            var scripts = new List<MutationFilter>();
            foreach (var entry in entries)
            {
                if (!uint.TryParse(entry.Trim(), out var raw) || !Enum.IsDefined(typeof(MaterialType), (MaterialType)raw) ||
                    !TryGetScript((MaterialType)raw, out var scriptId))
                    return Fail(r, Status.UnknownEntry, $"tinker log entry '{entry}' is not a material with a tinker script");

                var filter = MutationCache.GetMutation(scriptId);
                var effects = FixedEffects(filter);
                if (effects == null)
                    return Fail(r, Status.Irreversible, $"{(MaterialType)raw}: tinker script is not a single fixed effect list");

                r.Tinkers.Add((MaterialType)raw);
                scripts.Add(filter);
            }

            // Invert: last tinker first, and within a tinker its last effect first.
            for (var t = scripts.Count - 1; t >= 0; t--)
            {
                var effects = FixedEffects(scripts[t]);
                for (var e = effects.Count - 1; e >= 0; e--)
                {
                    var error = Invert(effects[e], r.Base);
                    if (error != null)
                        return Fail(r, Status.Irreversible, $"{r.Tinkers[t]}: {error}");
                }
            }

            var mismatch = VerifyByReplay(wo, r.Base, scripts);
            if (mismatch != null)
                return Fail(r, Status.Irreversible, $"replaying the tinkers did not reproduce the weapon ({mismatch})");

            r.Status = Status.Ok;
            r.Detail = $"{count} tinkers reversed: {string.Join(", ", r.Tinkers)}";
            return r;
        }

        /// <summary>
        /// Re-applies <paramref name="tinkers"/>, in order, to the FORGE STATS of <paramref name="target"/> with the game's own
        /// effects. Effects on any other property are skipped: a forged weapon copies those from the main weapon, which
        /// already carries them (Gold's Value x1.25 must not land twice). Returns false when a tinker has no usable script.
        /// </summary>
        /// <param name="skip">Forge stats to leave alone: on a tier 11+ piece Zone Control re-stamps some of them from the
        /// record and adds the tinkers back itself (PropertyString.ZcTinkerBonus), so replaying them here would count
        /// them twice.</param>
        public static bool ReplayOnForgeStats(WorldObject target, IEnumerable<MaterialType> tinkers, IEnumerable<(StatType Type, int Idx)> skip = null)
        {
            var forgeStats = new HashSet<(StatType, int)>(ForgeStats);
            if (skip != null)
                forgeStats.ExceptWith(skip);
            foreach (var material in tinkers)
            {
                if (!TryGetScript(material, out var scriptId))
                    return false;
                var effects = FixedEffects(MutationCache.GetMutation(scriptId));
                if (effects == null)
                    return false;
                foreach (var effect in effects)
                    if (effect.Quality.Type == EffectArgumentType.Quality && forgeStats.Contains((effect.Quality.StatType, effect.Quality.StatIdx)))
                        effect.TryMutate(target);
            }
            return true;
        }

        private static Result Fail(Result r, Status status, string detail)
        {
            r.Status = status;
            r.Detail = detail;
            return r;
        }

        /// <summary>The effect list of a deterministic script (one mutation, 100%, one outcome, one effect list at 100%), else null.</summary>
        private static List<Effect> FixedEffects(MutationFilter filter)
        {
            if (filter?.Mutations == null || filter.Mutations.Count != 1)
                return null;
            var m = filter.Mutations[0];
            if (m.Chances.Count < 1 || m.Chances[0] < 1.0f || m.Outcomes.Count != 1)
                return null;
            var lists = m.Outcomes[0].EffectLists;
            if (lists.Count != 1 || lists[0].Chance < 1.0f)
                return null;
            return lists[0].Effects;
        }

        /// <summary>Undoes one effect on the tracked values. Returns an error when it cannot be undone exactly.</summary>
        private static string Invert(Effect effect, Dictionary<(StatType, int), double?> values)
        {
            var key = (effect.Quality.StatType, effect.Quality.StatIdx);
            if (effect.Quality.Type != EffectArgumentType.Quality || !values.ContainsKey(key))
                return null;   // not a forge stat: the main weapon keeps its own

            var name = effect.Quality.StatType == StatType.Int ? ((PropertyInt)key.Item2).ToString() : ((PropertyFloat)key.Item2).ToString();
            var v = values[key];

            if (!Constant(effect.Arg1, out var a1) && Needs(effect.Type, 1))
                return $"{name}: {effect.Type} uses a non-constant argument";
            if (!Constant(effect.Arg2, out var a2) && Needs(effect.Type, 2))
                return $"{name}: {effect.Type} uses a non-constant argument";

            switch (effect.Type)
            {
                // The game reads a missing stat as 0 (EffectArgument.ResolveValue) and stores the result, so these
                // never leave a stat missing. Inverting such a stat gives 0 where the weapon had none; the replay
                // check still holds, because 0 and missing produce the same forward result.
                case MutationEffectType.Add: values[key] = v - a1; break;
                case MutationEffectType.Subtract: values[key] = v + a1; break;
                case MutationEffectType.Multiply:
                    if (Math.Abs(a1) < Epsilon) return $"{name}: multiplied by 0";
                    values[key] = v / a1;
                    break;
                case MutationEffectType.Divide: values[key] = v * a1; break;

                // forward: (missing or v < a1) ? a1 : v + a2
                case MutationEffectType.AtLeastAdd:
                    if (!v.HasValue) return $"{name}: missing after an at-least effect";
                    if (Math.Abs(v.Value - a1) < Epsilon)
                    {
                        // Landed on the floor: the original was missing, below a1, or exactly a1 - a2. That is only
                        // recoverable when every candidate means "none" - a float modifier whose floor step starts
                        // at nothing (Green Garnet: set 0.01 or add 0.01 from 0).
                        if (effect.Quality.StatType == StatType.Float && a1 - a2 <= Epsilon)
                        {
                            values[key] = null;
                            break;
                        }
                        return $"{name} sits on its floor of {a1}; the value before the tinker is lost";
                    }
                    values[key] = v - a2;
                    break;

                // forward: (missing or v > a1) ? a1 : v - a2
                case MutationEffectType.AtMostSubtract:
                    if (!v.HasValue) return $"{name}: missing after an at-most effect";
                    if (Math.Abs(v.Value - a1) < Epsilon)
                        return $"{name} sits on its ceiling of {a1}; the value before the tinker is lost";
                    values[key] = v + a2;
                    break;

                default:
                    return $"{name}: {effect.Type} cannot be inverted";
            }

            if (values[key].HasValue && effect.Quality.StatType == StatType.Int)
            {
                var rounded = Math.Round(values[key].Value);
                if (Math.Abs(rounded - values[key].Value) > Epsilon)
                    return $"{name}: inverse is not a whole number";
                values[key] = rounded;
            }
            return null;
        }

        private static bool Needs(MutationEffectType type, int arg) => type switch
        {
            MutationEffectType.AtLeastAdd or MutationEffectType.AtMostSubtract => true,
            _ => arg == 1,
        };

        private static bool Constant(EffectArgument a, out double value)
        {
            value = 0;
            if (a == null)
                return false;
            switch (a.Type)
            {
                case EffectArgumentType.Int: value = a.IntVal; return true;
                case EffectArgumentType.Int64: value = a.LongVal; return true;
                case EffectArgumentType.Double: value = a.DoubleVal; return true;
                default: return false;
            }
        }

        private static double? Read(WorldObject wo, (StatType Type, int Idx) key)
            => key.Type == StatType.Int ? wo.GetProperty((PropertyInt)key.Idx) : wo.GetProperty((PropertyFloat)key.Idx);

        /// <summary>
        /// Puts the recovered values on a throwaway copy of the weapon and replays the tinkers with the game's own
        /// MutationFilter. Returns null when every forge stat comes back to the weapon's current value.
        /// </summary>
        private static string VerifyByReplay(WorldObject wo, Dictionary<(StatType, int), double?> recovered, List<MutationFilter> scripts)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wo.WeenieClassId);
            if (weenie == null)
                return "weenie not found";

            // Never registered, saved or sent: the guid only has to exist.
            var scratch = WorldObjectFactory.CreateWorldObject(weenie, new ObjectGuid(0xFFFFFFFE));
            if (scratch == null)
                return "could not build a scratch copy";

            foreach (var (key, value) in recovered)
            {
                if (key.Item1 == StatType.Int)
                {
                    if (value.HasValue) scratch.SetProperty((PropertyInt)key.Item2, (int)value.Value);
                    else scratch.RemoveProperty((PropertyInt)key.Item2);
                }
                else
                {
                    if (value.HasValue) scratch.SetProperty((PropertyFloat)key.Item2, value.Value);
                    else scratch.RemoveProperty((PropertyFloat)key.Item2);
                }
            }
            scratch.RemoveProperty(PropertyInt.NumTimesTinkered);

            foreach (var script in scripts)
                script.TryMutate(scratch);

            foreach (var key in recovered.Keys)
            {
                var want = Read(wo, key);
                var got = Read(scratch, key);
                if (want.HasValue != got.HasValue || (want.HasValue && Math.Abs(want.Value - got.Value) > Epsilon))
                {
                    var name = key.Item1 == StatType.Int ? ((PropertyInt)key.Item2).ToString() : ((PropertyFloat)key.Item2).ToString();
                    return $"{name}: weapon {want?.ToString() ?? "none"}, replay {got?.ToString() ?? "none"}";
                }
            }
            return null;
        }
    }
}
