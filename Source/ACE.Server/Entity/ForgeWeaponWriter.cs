using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;

using static ACE.Server.Entity.ForgeMath;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Turns a ForgeMath outcome into a real item. The result is always a NEW object, so a changed element can
    /// become a different weenie (Flaming Broad Sword -> Frost Broad Sword) and both inputs can be destroyed by the
    /// caller AFTER the new item is safely saved. Nothing here touches either input, the world or the database.
    ///
    /// How the new item is assembled:
    ///   1. created from the result weenie (the main weapon's, or its version in the rolled element);
    ///   2. every property of the main weapon is copied over, EXCEPT the ones that make up an element version - those
    ///      that differ between the two weenie templates (name, damage type, model, icon...) come from the new weenie;
    ///   3. the forged values are written: stat lines (base + hone delta), spells, slayer / cleaving / set, the stricter
    ///      requirements, quality and Zone Control grades (re-resolved), forge count and hone levels;
    ///   4. the main weapon's tinkers are replayed on the forged stat lines with the game's own effects;
    ///   5. it is Attuned and Bonded.
    /// </summary>
    public static class ForgeWeaponWriter
    {
        public sealed class Built
        {
            public WorldObject Item;
            /// <summary>Null on success, else an ASCII reason (the new object, if any, was not kept).</summary>
            public string Error;
        }

        /// <summary>Property of a stat line on this weapon kind (casters keep their damage modifier in ElementalDamageMod).</summary>
        public static (StatType Type, int Idx) LineProperty(ForgeLine line, bool isCaster) => line switch
        {
            ForgeLine.MaxDamage => (StatType.Int, (int)PropertyInt.Damage),
            ForgeLine.Variance => (StatType.Float, (int)PropertyFloat.DamageVariance),
            ForgeLine.Speed => (StatType.Int, (int)PropertyInt.WeaponTime),
            ForgeLine.AttackMod => (StatType.Float, (int)PropertyFloat.WeaponOffense),
            ForgeLine.MeleeDefense => (StatType.Float, (int)PropertyFloat.WeaponDefense),
            ForgeLine.MissileDefense => (StatType.Float, (int)PropertyFloat.WeaponMissileDefense),
            ForgeLine.MagicDefense => (StatType.Float, (int)PropertyFloat.WeaponMagicDefense),
            ForgeLine.DamageMod => isCaster ? (StatType.Float, (int)PropertyFloat.ElementalDamageMod) : (StatType.Float, (int)PropertyFloat.DamageMod),
            ForgeLine.Spellcraft => (StatType.Int, (int)PropertyInt.ItemSpellcraft),
            ForgeLine.MaxMana => (StatType.Int, (int)PropertyInt.ItemMaxMana),
            ForgeLine.ArmorLevel => (StatType.Int, (int)PropertyInt.ArmorLevel),
            ForgeLine.ProtSlash => (StatType.Float, (int)PropertyFloat.ArmorModVsSlash),
            ForgeLine.ProtPierce => (StatType.Float, (int)PropertyFloat.ArmorModVsPierce),
            ForgeLine.ProtBludgeon => (StatType.Float, (int)PropertyFloat.ArmorModVsBludgeon),
            ForgeLine.ProtCold => (StatType.Float, (int)PropertyFloat.ArmorModVsCold),
            ForgeLine.ProtFire => (StatType.Float, (int)PropertyFloat.ArmorModVsFire),
            ForgeLine.ProtAcid => (StatType.Float, (int)PropertyFloat.ArmorModVsAcid),
            ForgeLine.ProtElectric => (StatType.Float, (int)PropertyFloat.ArmorModVsElectric),
            ForgeLine.ProtNether => (StatType.Float, (int)PropertyFloat.ArmorModVsNether),
            ForgeLine.RatingDamage => (StatType.Int, (int)PropertyInt.GearDamage),
            ForgeLine.RatingDamageResist => (StatType.Int, (int)PropertyInt.GearDamageResist),
            ForgeLine.RatingCrit => (StatType.Int, (int)PropertyInt.GearCrit),
            ForgeLine.RatingCritResist => (StatType.Int, (int)PropertyInt.GearCritResist),
            ForgeLine.RatingCritDamage => (StatType.Int, (int)PropertyInt.GearCritDamage),
            ForgeLine.RatingCritDamageResist => (StatType.Int, (int)PropertyInt.GearCritDamageResist),
            ForgeLine.RatingHealingBoost => (StatType.Int, (int)PropertyInt.GearHealingBoost),
            ForgeLine.RatingNetherResist => (StatType.Int, (int)PropertyInt.GearNetherResist),
            ForgeLine.RatingLifeResist => (StatType.Int, (int)PropertyInt.GearLifeResist),
            ForgeLine.RatingMaxHealth => (StatType.Int, (int)PropertyInt.GearMaxHealth),
            _ => throw new ArgumentOutOfRangeException(nameof(line)),
        };

        /// <param name="create">Makes the new item from a weenie id; defaults to WorldObjectFactory.CreateNewWorldObject (a new
        /// dynamic guid). Offline audits pass one that skips the guid allocator, since their items are never saved.</param>
        public static Built Build(WorldObject main, WorldObject feeder, ForgeOutcome outcome, ForgeConfig config, Func<uint, WorldObject> create = null)
        {
            var result = outcome.Result;

            // 1. The weenie: the main model, in the rolled element when it has a version in it.
            var resultWcid = main.WeenieClassId;
            var mainElement = main.GetProperty(PropertyInt.DamageType) ?? (int)ForgeGroups.TemplateDamageType(main.WeenieClassId);
            if (result.Element != mainElement)
                resultWcid = ForgeGroups.FindElementVariant(main.WeenieClassId, (DamageType)result.Element) ?? main.WeenieClassId;

            var item = (create ?? WorldObjectFactory.CreateNewWorldObject)(resultWcid);
            if (item == null)
                return new Built { Error = $"could not create weenie {resultWcid}" };

            // 2. The main weapon's properties, minus the element-version identity.
            CopyFromMain(main, item);

            // 3a. Stat lines: base value plus the hone delta.
            var isCaster = item is Caster;
            var hones = new ForgeHones(result.HoneLevels.ToDictionary(kv => kv.Key, kv => kv.Value), new Dictionary<ForgeLine, double>());
            foreach (var line in OrderFor(result.IsArmor))
            {
                var prop = LineProperty(line, isCaster);
                if (!result.Lines.TryGetValue(line, out var baseValue))
                {
                    Remove(item, prop);
                    continue;
                }
                var levels = result.HoneLevel(line);
                var stored = Store(item, prop, levels > 0 ? HonedValue(line, baseValue, levels, config) : baseValue);
                if (levels > 0)
                    hones.Deltas[line] = stored - baseValue;   // what was really added: whole-number stats round
            }

            // 3b. Spells.
            item.Biota.ClearSpells(item.BiotaDatabaseLock);
            foreach (var spell in result.Spells.Values)
                item.Biota.GetOrAddKnownSpell((int)spell.SpellId, item.BiotaDatabaseLock, out _);

            // 3c. Trait packages: whole from the chosen weapon, or gone.
            var chosenSlayer = Chosen(outcome, ForgeWeaponReader.PackageSlayer, main, feeder);
            CopyOrClear(chosenSlayer, item, PropertyInt.SlayerCreatureType, PropertyFloat.SlayerDamageBonus);
            var chosenCleave = Chosen(outcome, ForgeWeaponReader.PackageCleaving, main, feeder);
            CopyOrClear(chosenCleave, item, PropertyInt.ResistanceModifierType, PropertyFloat.ResistanceModifier);
            var chosenSet = Chosen(outcome, ForgeWeaponReader.PackageSet, main, feeder);
            if (chosenSet?.GetProperty(PropertyInt.EquipmentSetId) is int set) item.SetProperty(PropertyInt.EquipmentSetId, set);
            else item.RemoveProperty(PropertyInt.EquipmentSetId);

            // 3d. Requirements: never rolled, always the stricter.
            ApplyStricterRequirements(main, feeder, item);

            // 3e. Tier, quality and Zone Control grades; a graded weapon re-resolves its stats from them.
            if (result.Tier > 0)
            {
                if (main.GetProperty(PropertyInt.WeaponAugScaleTier).HasValue || feeder.GetProperty(PropertyInt.WeaponAugScaleTier).HasValue)
                    item.SetProperty(PropertyInt.WeaponAugScaleTier, result.Tier);
                if (main.GetProperty(PropertyInt.ZcTier).HasValue || feeder.GetProperty(PropertyInt.ZcTier).HasValue)
                    item.SetProperty(PropertyInt.ZcTier, result.Tier);
            }
            if (result.Quality.HasValue) item.SetProperty(PropertyInt.WeaponAugScaleQuality, result.Quality.Value);
            else item.RemoveProperty(PropertyInt.WeaponAugScaleQuality);
            // The item is a copy of the main one, record included. Its tier 11+ properties become what the forge rolled,
            // through the same add / remove / regrade steps a salvage bag takes, so the stats, the baked text and the
            // "Properties: X of Y" count all follow the record.
            if (ZoneStatResolver.HasRecord(main) || ZoneStatResolver.HasRecord(feeder))
            {
                var want = result.ZcGrades.Where(kv => result.PoolKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                var regrade = result.ZcGrades.Where(kv => !result.PoolKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                GearEssences.ForgeApplyProperties(item, want, regrade, feeder);
            }

            // 3f. Forge state.
            item.SetProperty(PropertyInt.ForgeCount, result.ForgeCount);
            if (result.HoneMisfortune > 0) item.SetProperty(PropertyInt.ForgeHoneMisfortune, result.HoneMisfortune);
            else item.RemoveProperty(PropertyInt.ForgeHoneMisfortune);
            var honeText = hones.Format();
            if (honeText.Length > 0) item.SetProperty(PropertyString.ForgeHoneLevels, honeText);
            else item.RemoveProperty(PropertyString.ForgeHoneLevels);

            // 4. The main weapon's tinkers, back on top of the forged stats.
            var mainTinkers = TinkerReversal.Strip(main);
            if (mainTinkers.Status != TinkerReversal.Status.Ok || !TinkerReversal.ReplayOnForgeStats(item, mainTinkers.Tinkers))
            {
                // The half-built result is never handed over: release it, or its guid stays allocated until a restart.
                // Only when this method made it - a caller's own factory (the offline audits) owns what it creates.
                if (create == null)
                    item.Destroy();
                return new Built { Error = "the main weapon's tinkers could not be applied again" };
            }

            // Mana never exceeds the new maximum.
            if (item.ItemMaxMana.HasValue && (item.ItemCurMana ?? 0) > item.ItemMaxMana)
                item.ItemCurMana = item.ItemMaxMana;

            // 5. Bound to its smith.
            Bind(item);

            return new Built { Item = item };
        }

        // ---------------------------------------------------------------- copying

        /// <summary>Properties never carried from the main weapon: they belong to the object, not the weapon.</summary>
        private static readonly HashSet<PropertyInt> NeverCopyInt = new() { PropertyInt.CurrentWieldedLocation, PropertyInt.PlacementPosition, PropertyInt.StackSize };

        /// <summary>
        /// Copies every Bool / DID / Float / Int / Int64 / String property of <paramref name="main"/> onto
        /// <paramref name="item"/>, and removes item properties the main weapon does not have - except the element-version
        /// identity: properties whose value differs between the two weenie templates keep the new weenie's value.
        /// For the same weenie there is no identity, so the item becomes a full copy.
        /// </summary>
        private static void CopyFromMain(WorldObject main, WorldObject item)
        {
            var a = DatabaseManager.World.GetCachedWeenie(main.WeenieClassId);
            var b = DatabaseManager.World.GetCachedWeenie(item.WeenieClassId);
            var same = main.WeenieClassId == item.WeenieClassId;

            bool Identity<TK, TV>(IDictionary<TK, TV> ta, IDictionary<TK, TV> tb, TK key)
            {
                if (same) return false;
                var ha = ta != null && ta.TryGetValue(key, out var va);
                var hb = tb != null && tb.TryGetValue(key, out var vb);
                if (ha != hb) return true;
                return ha && !Equals(ta[key], tb[key]);
            }

            Sync(main.GetAllPropertyBools(), item.GetAllPropertyBools(), k => Identity(a.PropertiesBool, b.PropertiesBool, k), (k, v) => item.SetProperty(k, v), k => item.RemoveProperty(k));
            Sync(main.GetAllPropertyDataId(), item.GetAllPropertyDataId(), k => Identity(a.PropertiesDID, b.PropertiesDID, k), (k, v) => item.SetProperty(k, v), k => item.RemoveProperty(k));
            Sync(main.GetAllPropertyFloat(), item.GetAllPropertyFloat(), k => Identity(a.PropertiesFloat, b.PropertiesFloat, k), (k, v) => item.SetProperty(k, v), k => item.RemoveProperty(k));
            Sync(main.GetAllPropertyInt(), item.GetAllPropertyInt(), k => NeverCopyInt.Contains(k) || Identity(a.PropertiesInt, b.PropertiesInt, k), (k, v) => item.SetProperty(k, v), k => item.RemoveProperty(k));
            Sync(main.GetAllPropertyInt64(), item.GetAllPropertyInt64(), k => Identity(a.PropertiesInt64, b.PropertiesInt64, k), (k, v) => item.SetProperty(k, v), k => item.RemoveProperty(k));
            Sync(main.GetAllPropertyString(), item.GetAllPropertyString(), k => Identity(a.PropertiesString, b.PropertiesString, k), (k, v) => item.SetProperty(k, v), k => item.RemoveProperty(k));
        }

        private static void Sync<TK, TV>(Dictionary<TK, TV> from, Dictionary<TK, TV> to, Func<TK, bool> keepItems, Action<TK, TV> set, Action<TK> remove)
        {
            foreach (var key in to.Keys.ToList())
                if (!from.ContainsKey(key) && !keepItems(key))
                    remove(key);
            foreach (var (key, value) in from)
                if (!keepItems(key))
                    set(key, value);
        }

        private static WorldObject Chosen(ForgeOutcome o, int packageKey, WorldObject main, WorldObject feeder)
        {
            var pick = o.Picks.FirstOrDefault(p => p.Kind == PickKind.Package && p.Key == packageKey);
            if (pick == null)
                return null;
            return pick.FromMain ? main : feeder;
        }

        private static void CopyOrClear(WorldObject from, WorldObject item, PropertyInt type, PropertyFloat amount)
        {
            var t = from?.GetProperty(type);
            if (t.HasValue)
            {
                item.SetProperty(type, t.Value);
                if (from.GetProperty(amount) is double v) item.SetProperty(amount, v);
                else item.RemoveProperty(amount);
            }
            else
            {
                item.RemoveProperty(type);
                item.RemoveProperty(amount);
            }
        }

        private static readonly (PropertyInt Req, PropertyInt Skill, PropertyInt Diff)[] WieldSlots =
        {
            (PropertyInt.WieldRequirements, PropertyInt.WieldSkillType, PropertyInt.WieldDifficulty),
            (PropertyInt.WieldRequirements2, PropertyInt.WieldSkillType2, PropertyInt.WieldDifficulty2),
            (PropertyInt.WieldRequirements3, PropertyInt.WieldSkillType3, PropertyInt.WieldDifficulty3),
            (PropertyInt.WieldRequirements4, PropertyInt.WieldSkillType4, PropertyInt.WieldDifficulty4),
        };

        /// <summary>
        /// Each wield slot: the stricter of the two when they ask for the same thing, the feeder's when only it has
        /// one; the arcane lore difficulty is the higher of the two. A slot whose kind differs keeps the main's and the
        /// feeder's moves to a free slot, so no requirement is ever dropped.
        /// </summary>
        private static void ApplyStricterRequirements(WorldObject main, WorldObject feeder, WorldObject item)
        {
            var extra = new List<(int Req, int? Skill, int Diff)>();
            for (var i = 0; i < WieldSlots.Length; i++)
            {
                var (req, skill, diff) = WieldSlots[i];
                var fr = feeder.GetProperty(req) ?? 0;
                var fd = feeder.GetProperty(diff);
                if (fr == 0 || !fd.HasValue)
                    continue;
                var mr = item.GetProperty(req) ?? 0;
                var md = item.GetProperty(diff);
                if (mr == 0 || !md.HasValue)
                {
                    item.SetProperty(req, fr);
                    if (feeder.GetProperty(skill) is int fs) item.SetProperty(skill, fs);
                    item.SetProperty(diff, fd.Value);
                }
                else if (mr == fr && item.GetProperty(skill) == feeder.GetProperty(skill))
                    item.SetProperty(diff, Math.Max(md.Value, fd.Value));
                else
                    extra.Add((fr, feeder.GetProperty(skill), fd.Value));
            }
            foreach (var e in extra)
            {
                var free = WieldSlots.FirstOrDefault(s => (item.GetProperty(s.Req) ?? 0) == 0);
                if (free.Req == 0)
                    break;   // every slot taken: the main weapon's requirements stand
                item.SetProperty(free.Req, e.Req);
                if (e.Skill.HasValue) item.SetProperty(free.Skill, e.Skill.Value);
                item.SetProperty(free.Diff, e.Diff);
            }

            var lore = Math.Max(main.GetProperty(PropertyInt.ItemDifficulty) ?? 0, feeder.GetProperty(PropertyInt.ItemDifficulty) ?? 0);
            if (lore > 0) item.SetProperty(PropertyInt.ItemDifficulty, lore);
        }

        /// <summary>Writes a stat and returns the value as stored (an integer stat rounds half up).</summary>
        public static double Store(WorldObject wo, (StatType Type, int Idx) prop, double value)
        {
            if (prop.Type == StatType.Int)
            {
                var whole = (int)Math.Round(value, MidpointRounding.AwayFromZero);
                wo.SetProperty((PropertyInt)prop.Idx, whole);
                return whole;
            }
            wo.SetProperty((PropertyFloat)prop.Idx, value);
            return value;
        }

        /// <summary>
        /// Sets one line of a real weapon to <paramref name="levels"/> hone levels, IN PLACE (honing at a grindstone, where
        /// nothing else about the weapon changes). The weapon's tinkers are taken off, the line is re-honed from its base
        /// roll, and the tinkers are replayed. Returns null on success, else an ASCII reason and the weapon is untouched.
        /// </summary>
        public static string ApplyHone(WorldObject weapon, ForgeLine line, int levels, ForgeConfig config)
        {
            var untinkered = TinkerReversal.Strip(weapon);
            if (untinkered.Status != TinkerReversal.Status.Ok)
                return "its tinkering cannot be undone exactly";

            var prop = LineProperty(line, weapon is Caster);
            if (!untinkered.Base.TryGetValue(prop, out var withHone) || !withHone.HasValue)
                return $"it has no {LineName(line)} to hone";

            var hones = ForgeHones.Parse(weapon.GetProperty(PropertyString.ForgeHoneLevels));
            var baseValue = withHone.Value - (hones.Deltas.TryGetValue(line, out var oldDelta) ? oldDelta : 0);

            // Every forge stat back to its untinkered value, the honed line to its new value, then the tinkers again.
            foreach (var (key, value) in untinkered.Base)
            {
                if (key == prop)
                    continue;
                if (value.HasValue) Store(weapon, key, value.Value);
                else Remove(weapon, key);
            }
            var stored = Store(weapon, prop, levels > 0 ? HonedValue(line, baseValue, levels, config) : baseValue);
            if (!TinkerReversal.ReplayOnForgeStats(weapon, untinkered.Tinkers))
                return "its tinkers could not be applied again";

            if (levels > 0)
            {
                hones.Levels[line] = levels;
                hones.Deltas[line] = stored - baseValue;
            }
            else
            {
                hones.Levels.Remove(line);
                hones.Deltas.Remove(line);
            }
            var text = hones.Format();
            if (text.Length > 0) weapon.SetProperty(PropertyString.ForgeHoneLevels, text);
            else weapon.RemoveProperty(PropertyString.ForgeHoneLevels);
            return null;
        }

        /// <summary>Makes a weapon Attuned and Bonded: forged and honed weapons belong to their owner.</summary>
        public static void Bind(WorldObject weapon)
        {
            weapon.SetProperty(PropertyInt.Attuned, (int)AttunedStatus.Attuned);
            weapon.SetProperty(PropertyInt.Bonded, (int)BondedStatus.Bonded);
            weapon.RemoveProperty(PropertyBool.ForgeRebindOnWield);
        }

        private static void Remove(WorldObject wo, (StatType Type, int Idx) prop)
        {
            if (prop.Type == StatType.Int) wo.RemoveProperty((PropertyInt)prop.Idx);
            else wo.RemoveProperty((PropertyFloat)prop.Idx);
        }
    }

    /// <summary>
    /// A weapon's hone levels and the exact amount each line's levels added: PropertyString.ForgeHoneLevels,
    /// "line:levels:delta;...". The delta is stored so the base value comes back exactly even if the per-level step
    /// in ServerConfig changes later.
    /// </summary>
    public sealed class ForgeHones
    {
        public Dictionary<ForgeLine, int> Levels;
        public Dictionary<ForgeLine, double> Deltas;

        public ForgeHones(Dictionary<ForgeLine, int> levels, Dictionary<ForgeLine, double> deltas)
        {
            Levels = levels;
            Deltas = deltas;
        }

        public static ForgeHones Parse(string text)
        {
            var h = new ForgeHones(new Dictionary<ForgeLine, int>(), new Dictionary<ForgeLine, double>());
            if (string.IsNullOrEmpty(text))
                return h;
            foreach (var entry in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = entry.Split(':');
                if (bits.Length != 3 || !int.TryParse(bits[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var line) ||
                    !int.TryParse(bits[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var levels) ||
                    !double.TryParse(bits[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var delta) || levels <= 0)
                    continue;
                h.Levels[(ForgeLine)line] = levels;
                h.Deltas[(ForgeLine)line] = delta;
            }
            return h;
        }

        public string Format()
            => string.Join(";", Levels.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key)
                .Select(kv => $"{(int)kv.Key}:{kv.Value}:{(Deltas.TryGetValue(kv.Key, out var d) ? d : 0).ToString("R", CultureInfo.InvariantCulture)}"));
    }
}
