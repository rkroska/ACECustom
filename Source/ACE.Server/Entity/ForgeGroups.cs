using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.WorldObjects;

using DamageType = ACE.Entity.Enum.DamageType;
using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Which weapons may be forged together: "like with like", meaning the loot generator rolls both from the
    /// same tables (owner, 2026-09-29/30). A group is the loot table a weapon comes from (its skill) plus the
    /// TreasureWeaponType that table assigns it - so Broad Sword and Spada share a group, a sword and a mace do
    /// not, two-handed splits into sword / axe / mace / spear and the jitte stands apart from maces. Casters carry
    /// no weapon type in their table and split by school only: war casters and nether casters.
    ///
    /// A weapon in none of these tables (quest or named items, even with a workmanship) has no group and is never
    /// forgeable, so this lookup is also the forge's "was it loot?" test.
    /// </summary>
    public static class ForgeGroups
    {
        public static bool IsWeapon(WorldObject wo) => wo is MeleeWeapon || wo is MissileLauncher || wo is Caster;

        /// <summary>Group of any forgeable item: a weapon by its loot table, armour and shields by <see cref="GetArmorGroup"/>.</summary>
        public static string GetGroup(WorldObject wo)
        {
            if (wo == null)
                return null;
            return IsWeapon(wo) ? GetGroup(wo.WeenieClassId) : GetArmorGroup(wo);
        }

        private static readonly (EquipMask Mask, string Name)[] coverageNames =
        {
            (EquipMask.HeadWear, "head"), (EquipMask.ChestArmor, "chest"), (EquipMask.AbdomenArmor, "abdomen"),
            (EquipMask.UpperArmArmor, "upper arms"), (EquipMask.LowerArmArmor, "lower arms"), (EquipMask.HandWear, "hands"),
            (EquipMask.UpperLegArmor, "upper legs"), (EquipMask.LowerLegArmor, "lower legs"), (EquipMask.FootWear, "feet"),
            (EquipMask.Shield, "shield"),
        };

        /// <summary>
        /// Group of a loot-generated piece of armour or a shield, or null when the loot generator never rolls its weenie
        /// (owner, 2026-10-04). Two pieces forge together when they cover exactly the same slots - as the piece covers
        /// them NOW, so a tailored piece counts as what it has become - and are of the same class. Armour type does not
        /// matter (measured on 255,151 loot pieces: within one coverage every type rolls the same armour level), with
        /// two exceptions that roll far higher and so stand alone: Covenant, and plain Olthoi.
        /// </summary>
        public static string GetArmorGroup(WorldObject wo)
        {
            if (wo == null || IsWeapon(wo) || !ArmorWcids.TryGetValue((WeenieClassName)wo.WeenieClassId, out var type))
                return null;

            var covered = wo.ValidLocations ?? EquipMask.None;
            var parts = coverageNames.Where(c => (covered & c.Mask) != 0).Select(c => c.Name).ToList();
            if (parts.Count == 0)
                return null;

            var kind = type == TreasureArmorType.Covenant ? "Covenant " : type == TreasureArmorType.Olthoi ? "Olthoi " : "";
            if (parts.Count == 1 && parts[0] == "shield")
                return $"{kind}shield".Trim();
            // the under-layer bits ride along with boots and the like; they are part of the coverage too
            var under = (uint)(covered & (EquipMask.Clothing & ~(EquipMask.HeadWear | EquipMask.HandWear | EquipMask.FootWear)));
            return $"{kind}armour covering {string.Join(", ", parts)}{(under != 0 ? $" (under-layer {under:X})" : "")}";
        }

        /// <summary>Group of a weapon weenie, or null when the loot generator never rolls it.</summary>
        public static string GetGroup(uint wcid)
        {
            var w = (WeenieClassName)wcid;
            if (HeavyWeaponWcids.TryGetValue(w, out var t)) return Melee("Heavy Weapons", t);
            if (LightWeaponWcids.TryGetValue(w, out t)) return Melee("Light Weapons", t);
            if (FinesseWeaponWcids.TryGetValue(w, out t)) return Melee("Finesse Weapons", t);
            if (TwoHandedWeaponWcids.TryGetValue(w, out t)) return Melee("Two Handed Combat", t);
            if (BowWcids_Aluvian.TryGetValue(w, out _) || BowWcids_Gharundim.TryGetValue(w, out _) || BowWcids_Sho.TryGetValue(w, out _))
                return "Bow";
            if (CrossbowWcids.TryGetValue(w, out _)) return "Crossbow";
            if (AtlatlWcids.TryGetValue(w, out _)) return "Atlatl";
            if (CasterWcids.Contains(w))
                return TemplateDamageType(wcid) == DamageType.Nether ? "Nether caster" : "War caster";
            return null;
        }

        /// <summary>ASCII group name, e.g. "Light Weapons sword (multi-strike)", "Two Handed Combat axe".</summary>
        private static string Melee(string skill, TreasureWeaponType type) => $"{skill} {type switch
        {
            TreasureWeaponType.SwordMS => "sword (multi-strike)",
            TreasureWeaponType.DaggerMS => "dagger (multi-strike)",
            TreasureWeaponType.MaceJitte => "jitte",
            TreasureWeaponType.TwoHandedSword => "sword",
            TreasureWeaponType.TwoHandedAxe => "axe",
            TreasureWeaponType.TwoHandedMace => "mace",
            TreasureWeaponType.TwoHandedSpear => "spear",
            _ => type.ToString().ToLowerInvariant(),
        }}";

        /// <summary>The element a weenie is made with (its template DamageType), 0 when it has none.</summary>
        public static DamageType TemplateDamageType(uint wcid)
            => (DamageType)(DatabaseManager.World.GetCachedWeenie(wcid)?.GetProperty(PropertyInt.DamageType) ?? 0);

        // ---- element versions of one model

        /// <summary>Leading words that name an element version rather than the model ("Flaming Broad Sword").</summary>
        private static readonly Regex ElementWord = new(@"^(Acid|Flaming|Fire|Frost|Lightning|Electric|Slashing|Piercing|Blunt|Nether)\s+", RegexOptions.IgnoreCase);

        /// <summary>Model name with the element word removed: "Flaming Broad Sword" -> "Broad Sword".</summary>
        public static string ModelName(string name) => ElementWord.Replace(name ?? "", "").Trim();

        private static readonly Lazy<Dictionary<(string Group, string Model, DamageType Element), uint>> variants = new(BuildVariants);

        private static IEnumerable<WeenieClassName> AllLootWeapons()
            => HeavyWeaponWcids.All.Concat(LightWeaponWcids.All).Concat(FinesseWeaponWcids.All).Concat(TwoHandedWeaponWcids.All)
               .Concat(BowWcids_Aluvian.All).Concat(BowWcids_Gharundim.All).Concat(BowWcids_Sho.All)
               .Concat(CrossbowWcids.All).Concat(AtlatlWcids.All).Concat(CasterWcids.All).Distinct();

        private static Dictionary<(string, string, DamageType), uint> BuildVariants()
        {
            var map = new Dictionary<(string, string, DamageType), uint>();
            foreach (var w in AllLootWeapons())
            {
                var wcid = (uint)w;
                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                var group = GetGroup(wcid);
                if (weenie == null || group == null)
                    continue;
                var key = (group, ModelName(weenie.GetProperty(PropertyString.Name)), TemplateDamageType(wcid));
                map.TryAdd(key, wcid);   // first wins: the tables never list two weenies for one model + element
            }
            return map;
        }

        /// <summary>
        /// The weenie of <paramref name="wcid"/>'s model in <paramref name="element"/> (Flaming Broad Sword + Frost -> Frost
        /// Broad Sword), or null when that model has no version in that element.
        /// </summary>
        public static uint? FindElementVariant(uint wcid, DamageType element)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            var group = GetGroup(wcid);
            if (weenie == null || group == null)
                return null;
            if (TemplateDamageType(wcid) == element)
                return wcid;
            return variants.Value.TryGetValue((group, ModelName(weenie.GetProperty(PropertyString.Name)), element), out var v) ? v : null;
        }
    }
}
