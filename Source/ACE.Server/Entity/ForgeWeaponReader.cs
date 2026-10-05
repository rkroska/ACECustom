using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;

using static ACE.Server.Entity.ForgeMath;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Reads a live weapon into a <see cref="ForgeWeapon"/> for ForgeMath, and decides whether it may be forged
    /// at all. Read side only: writing a forge result back onto a weapon is a separate step.
    /// </summary>
    public static class ForgeWeaponReader
    {
        public const int PackageSlayer = 1;
        public const int PackageCleaving = 2;
        public const int PackageSet = 3;

        /// <summary>
        /// A weapon the loot generator made: it has a workmanship and its weenie template does not. Quest and
        /// crafted weapons whose template carries a workmanship (Rusted *, Stormwood *) fail the second test.
        /// </summary>
        public static bool IsLootGenerated(WorldObject wo)
        {
            if (wo?.ItemWorkmanship == null)
                return false;
            var weenie = DatabaseManager.World.GetCachedWeenie(wo.WeenieClassId);
            return weenie != null && weenie.GetProperty(PropertyInt.ItemWorkmanship) == null;
        }

        /// <summary>Tinkered, but with no record of which tinkers: its true base values cannot be recovered.</summary>
        public static bool HasUnknownTinkerHistory(WorldObject wo)
            => (wo.NumTimesTinkered) > 0 && string.IsNullOrEmpty(wo.TinkerLog);

        /// <summary>
        /// Null when the weapon may be forged, else an ASCII reason for the player. A weapon needs a workmanship
        /// AND a forge group: quest and named weapons that carry a workmanship are in no loot table, so no group.
        /// </summary>
        public static string RefusalReason(WorldObject wo)
        {
            var weapon = ForgeGroups.IsWeapon(wo);
            if (!weapon && !IsArmorKind(wo))
                return $"{wo?.Name ?? "That"} is not a weapon or a piece of armour.";
            if (!IsLootGenerated(wo) || ForgeGroups.GetGroup(wo) == null)
                return $"{wo.Name} was not made by the loot generator, and only loot weapons and armour can be forged.";
            if (HasUnknownTinkerHistory(wo))
                return $"{wo.Name}'s tinkering history is unknown, so it cannot be reforged.";
            var tinkers = TinkerReversal.Strip(wo);
            if (tinkers.Status != TinkerReversal.Status.Ok)
                return $"{wo.Name}'s tinkering cannot be undone exactly, so it cannot be reforged.";
            return null;
        }

        /// <summary>Armour or a shield by what it is, before asking whether the loot generator made it.</summary>
        public static bool IsArmorKind(WorldObject wo)
            => wo != null && !ForgeGroups.IsWeapon(wo)
               && ((wo.ValidLocations ?? ACE.Entity.Enum.EquipMask.None) & (ACE.Entity.Enum.EquipMask.Armor | ACE.Entity.Enum.EquipMask.Extremity | ACE.Entity.Enum.EquipMask.Shield)) != 0;

        /// <summary>Null when the two may be forged together, else an ASCII reason.</summary>
        public static string PairRefusalReason(WorldObject main, WorldObject feeder)
        {
            var a = ForgeGroups.GetGroup(main);
            var b = ForgeGroups.GetGroup(feeder);
            if (a != b)
                return ForgeGroups.IsWeapon(main) && ForgeGroups.IsWeapon(feeder)
                    ? $"Only like weapons can be forged together: {main.Name} is {Article(a)} {a}, {feeder.Name} is {Article(b)} {b}."
                    : $"Only like pieces can be forged together: {main.Name} is {Article(a)} {a}, {feeder.Name} is {Article(b)} {b}.";
            return null;
        }

        private static string Article(string s) => !string.IsNullOrEmpty(s) && "AEIOUaeiou".IndexOf(s[0]) >= 0 ? "an" : "a";

        /// <summary>
        /// The weapon's forge state, with every stat line at its UNTINKERED value (TinkerReversal). Call only on a
        /// weapon RefusalReason accepted: for one whose tinkers cannot be undone the values read as they stand.
        /// </summary>
        public static ForgeWeapon Read(WorldObject wo)
        {
            var untinkered = TinkerReversal.Strip(wo);
            double? Base(PropertyInt p) => untinkered.Status == TinkerReversal.Status.Ok ? untinkered.Get(p) : wo.GetProperty(p);
            double? BaseF(PropertyFloat p) => untinkered.Status == TinkerReversal.Status.Ok ? untinkered.Get(p) : wo.GetProperty(p);

            var w = new ForgeWeapon
            {
                Wcid = wo.WeenieClassId,
                IsArmor = !ForgeGroups.IsWeapon(wo),
                Group = ForgeGroups.GetGroup(wo),
                Element = wo.GetProperty(PropertyInt.DamageType) ?? (int)ForgeGroups.TemplateDamageType(wo.WeenieClassId),
                Tier = ZoneStatResolver.TierOf(wo),
                Quality = wo.GetProperty(PropertyInt.WeaponAugScaleQuality),
                TinkerCount = wo.NumTimesTinkered,
                TinkerLog = wo.TinkerLog,
                ImbuedEffect = wo.GetProperty(PropertyInt.ImbuedEffect) ?? 0,
                DyePalette = wo.ForgeDyePalette,
                ForgeCount = wo.GetProperty(PropertyInt.ForgeCount) ?? 0,
                HoneMisfortune = wo.GetProperty(PropertyInt.ForgeHoneMisfortune) ?? 0,
            };

            // Hone levels sit between the base roll and the tinkers: take their stored amount back off.
            var hones = ForgeHones.Parse(wo.GetProperty(PropertyString.ForgeHoneLevels));
            foreach (var (line, levels) in hones.Levels)
                w.HoneLevels[line] = levels;

            void Line(ForgeLine line, double? value)
            {
                if (!value.HasValue)
                    return;
                w.Lines[line] = hones.Deltas.TryGetValue(line, out var delta) ? value.Value - delta : value.Value;
            }
            if (w.IsArmor)
            {
                Line(ForgeLine.ArmorLevel, Base(PropertyInt.ArmorLevel));
                Line(ForgeLine.ProtSlash, BaseF(PropertyFloat.ArmorModVsSlash));
                Line(ForgeLine.ProtPierce, BaseF(PropertyFloat.ArmorModVsPierce));
                Line(ForgeLine.ProtBludgeon, BaseF(PropertyFloat.ArmorModVsBludgeon));
                Line(ForgeLine.ProtCold, BaseF(PropertyFloat.ArmorModVsCold));
                Line(ForgeLine.ProtFire, BaseF(PropertyFloat.ArmorModVsFire));
                Line(ForgeLine.ProtAcid, BaseF(PropertyFloat.ArmorModVsAcid));
                Line(ForgeLine.ProtElectric, BaseF(PropertyFloat.ArmorModVsElectric));
                Line(ForgeLine.ProtNether, BaseF(PropertyFloat.ArmorModVsNether));
                // gear ratings: no tinker touches them, so they are read as they stand
                foreach (var rating in ArmorOrder.Where(l => l >= ForgeLine.RatingDamage && l <= ForgeLine.RatingMaxHealth))
                    Line(rating, wo.GetProperty((PropertyInt)ForgeWeaponWriter.LineProperty(rating, false).Idx));
            }
            else
            {
                Line(ForgeLine.MaxDamage, Base(PropertyInt.Damage));
                Line(ForgeLine.Variance, BaseF(PropertyFloat.DamageVariance));
                Line(ForgeLine.Speed, Base(PropertyInt.WeaponTime));
                Line(ForgeLine.AttackMod, BaseF(PropertyFloat.WeaponOffense));
                Line(ForgeLine.MeleeDefense, BaseF(PropertyFloat.WeaponDefense));
                Line(ForgeLine.MissileDefense, BaseF(PropertyFloat.WeaponMissileDefense));
                Line(ForgeLine.MagicDefense, BaseF(PropertyFloat.WeaponMagicDefense));
                Line(ForgeLine.DamageMod, wo is Caster ? BaseF(PropertyFloat.ElementalDamageMod) : BaseF(PropertyFloat.DamageMod));
            }
            Line(ForgeLine.Spellcraft, Base(PropertyInt.ItemSpellcraft));
            Line(ForgeLine.MaxMana, Base(PropertyInt.ItemMaxMana));

            foreach (var rec in ZoneStatResolver.Read(wo))
                w.ZcGrades[rec.Key] = rec.Grade;

            // Spells grouped by category: the retail grouping that decides which of two spells wins when both are up.
            foreach (var id in wo.Biota.GetKnownSpellsIds(wo.BiotaDatabaseLock))
            {
                // with the world-database row: Spell.NotFound is true without it, and every spell would be skipped
                var spell = new Spell((uint)id);
                if (spell.NotFound)
                    continue;
                var family = (uint)spell.Category;
                if (!w.Spells.TryGetValue(family, out var have) || spell.Level > have.Level)
                    w.Spells[family] = new SpellEntry { Family = family, SpellId = (uint)id, Level = (int)spell.Level };
            }

            var slot = 1;
            foreach (var p in new[] { PropertyInt.WieldDifficulty, PropertyInt.WieldDifficulty2, PropertyInt.WieldDifficulty3, PropertyInt.WieldDifficulty4 })
            {
                var d = wo.GetProperty(p);
                if (d.HasValue)
                    w.WieldDifficulty[slot] = d.Value;
                slot++;
            }

            var slayer = wo.GetProperty(PropertyInt.SlayerCreatureType);
            if (slayer.HasValue)
            {
                var bonus = wo.GetProperty(PropertyFloat.SlayerDamageBonus) ?? 0;
                w.Packages[PackageSlayer] = new TraitPackage { Key = PackageSlayer, Score = bonus, Value = $"{(ACE.Entity.Enum.CreatureType)slayer.Value} slayer x{bonus:0.##}" };
            }
            var cleave = wo.GetProperty(PropertyInt.ResistanceModifierType);
            if (cleave.HasValue)
            {
                var mod = wo.GetProperty(PropertyFloat.ResistanceModifier) ?? 0;
                w.Packages[PackageCleaving] = new TraitPackage { Key = PackageCleaving, Score = mod, Value = $"{(ACE.Entity.Enum.DamageType)cleave.Value} cleaving {mod:0.##}" };
            }
            var set = wo.GetProperty(PropertyInt.EquipmentSetId);
            if (set.HasValue)
                w.Packages[PackageSet] = new TraitPackage { Key = PackageSet, Score = 1, Value = $"{(ACE.Entity.Enum.EquipmentSet)set.Value} set" };

            return w;
        }

        /// <summary>
        /// The forge lines of the appraisal panel, or null for a weapon that was never forged or honed. Plain ASCII with
        /// explicit "\n": the client draws anything else as garbage.
        ///   Forged 3 times.
        ///   Honed +2: Damage +1, Attack +1.
        /// </summary>
        public static string BuildAppraisalBlock(WorldObject wo)
        {
            var count = wo.GetProperty(PropertyInt.ForgeCount) ?? 0;
            var hones = ForgeHones.Parse(wo.GetProperty(PropertyString.ForgeHoneLevels));
            var total = hones.Levels.Values.Sum();
            if (count <= 0 && total <= 0)
                return null;

            var lines = new List<string>();
            if (count > 0)
                lines.Add(count == 1 ? "Forged once." : $"Forged {count} times.");
            if (total > 0)
                lines.Add($"Honed +{total}: " + string.Join(", ", hones.Levels.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key).Select(kv => $"{LineName(kv.Key)} +{kv.Value}")) + ".");
            return string.Join("\n", lines);
        }

        /// <summary>Client-safe name of a spell family (its category).</summary>
        public static string FamilyName(uint family) => SplitWords(((ACE.Entity.Enum.SpellCategory)family).ToString());

        private static string SplitWords(string s)
            => string.Concat(s.Select((c, i) => i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1]) ? " " + c : c.ToString()));
    }
}
