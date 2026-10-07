using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    /// <summary>
    /// Item spells on T11+ drops (owner 2026-10-05: "Items need spells - wards / minors / Blood Thirst / Spirit Thirst etc.",
    /// ruled "random from a per-slot list like retail loot; count AND level = zone stats, defaults 1-3 spells, Legendary").
    /// The per-slot lists are below (owner review pending). Every pick is a CANTRIP family
    /// (4 levels: 1 Minor, 2 Major, 3 Epic, 4 Legendary), one per family per piece. Rolled once, at drop - existing items
    /// never change. Called from Creature_Death right after ApplyZoneGearStats, before ZoneLootMutator (whose Cast on Strike
    /// stamps set the proc's own resist spellcraft).
    /// </summary>
    public static partial class LootGenerationFactory
    {
        private static readonly SpellId[] ZsWards =
        {
            SpellId.CANTRIPACIDWARD1, SpellId.CANTRIPBLUDGEONINGWARD1, SpellId.CANTRIPFROSTWARD1, SpellId.CANTRIPSTORMWARD1,
            SpellId.CANTRIPFLAMEWARD1, SpellId.CANTRIPSLASHINGWARD1, SpellId.CANTRIPPIERCINGWARD1,
        };
        private static readonly SpellId[] ZsDefenses = { SpellId.CANTRIPINVULNERABILITY1, SpellId.CANTRIPIMPREGNABILITY1, SpellId.CANTRIPMAGICRESISTANCE1 };
        private static readonly SpellId[] ZsWeaponAptitudes =
        {
            SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1, SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1, SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1,
            SpellId.CANTRIPTWOHANDEDAPTITUDE1, SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1,
        };
        private static readonly SpellId[] ZsMagicAptitudes =
        {
            SpellId.CANTRIPWARMAGICAPTITUDE1, SpellId.CantripVoidMagicAptitude1, SpellId.CANTRIPLIFEMAGICAPTITUDE1,
            SpellId.CANTRIPCREATUREENCHANTMENTAPTITUDE1, SpellId.CANTRIPITEMENCHANTMENTAPTITUDE1,
        };
        private static readonly SpellId[] ZsFighting =
        {
            SpellId.CantripDirtyFightingProwess1, SpellId.CantripRecklessnessProwess1, SpellId.CantripSneakAttackProwess1,
        };
        // owner 2026-10-06: Heart Thirst + Defender removed ("they dont do anything"); Blood Thirst / Spirit Thirst are NOT in the
        // pools - they roll on their own chance (item_spell_thirst_chance, default 2 pct) and take one of the drop's spell slots
        private static readonly SpellId[] ZsWeaponCore = { SpellId.CANTRIPSWIFTHUNTER1 };
        private static readonly SpellId[] ZsPhysicalAttributes = { SpellId.CANTRIPSTRENGTH1, SpellId.CANTRIPENDURANCE1, SpellId.CANTRIPCOORDINATION1, SpellId.CANTRIPQUICKNESS1 };

        /// <summary>The slot's spell list (minor ids, one per family), or null when the piece gets none.</summary>
        public static List<SpellId> ZoneSpellPool(WorldObject wo)
        {
            if (wo == null)
                return null;
            var pool = new List<SpellId>();

            if (wo is Caster)
            {
                pool.AddRange(new[] { SpellId.CantripHermeticLink1,
                    wo.W_DamageType == DamageType.Nether ? SpellId.CantripVoidMagicAptitude1 : SpellId.CANTRIPWARMAGICAPTITUDE1,
                    SpellId.CANTRIPCREATUREENCHANTMENTAPTITUDE1, SpellId.CANTRIPITEMENCHANTMENTAPTITUDE1, SpellId.CANTRIPLIFEMAGICAPTITUDE1,
                    SpellId.CANTRIPFOCUS1, SpellId.CANTRIPWILLPOWER1, SpellId.CANTRIPARCANEPROWESS1, SpellId.CANTRIPMANACONVERSIONPROWESS1 });
                return pool;
            }
            if (wo is MissileLauncher)
            {
                pool.AddRange(ZsWeaponCore);
                pool.Add(SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1);
                pool.AddRange(ZsPhysicalAttributes);
                pool.AddRange(ZsFighting);
                return pool;
            }
            if (wo is MeleeWeapon)
            {
                pool.AddRange(ZsWeaponCore);
                switch (wo.WeaponSkill)
                {
                    case Skill.HeavyWeapons: pool.Add(SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1); break;
                    case Skill.LightWeapons: pool.Add(SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1); break;
                    case Skill.FinesseWeapons: pool.Add(SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1); break;
                    case Skill.TwoHandedCombat: pool.Add(SpellId.CANTRIPTWOHANDEDAPTITUDE1); break;
                }
                pool.AddRange(ZsPhysicalAttributes);
                if (wo.WeaponSkill != Skill.TwoHandedCombat)
                    pool.Add(SpellId.CantripDualWieldAptitude1);
                pool.AddRange(ZsFighting);
                return pool;
            }

            switch (ZoneModifiers.PieceMask(wo))
            {
                case ZoneModifiers.SlotMask.Shield:
                    pool.Add(SpellId.CANTRIPIMPENETRABILITY1);
                    pool.Add(SpellId.CANTRIPARMOR1);
                    pool.AddRange(ZsWards);
                    pool.AddRange(ZsDefenses);
                    pool.AddRange(new[] { SpellId.CantripShieldAptitude1, SpellId.CANTRIPSTRENGTH1, SpellId.CANTRIPENDURANCE1 });
                    return pool;

                case ZoneModifiers.SlotMask.Armor:
                {
                    pool.Add(SpellId.CANTRIPIMPENETRABILITY1);
                    pool.Add(SpellId.CANTRIPARMOR1);
                    pool.AddRange(ZsWards);
                    pool.AddRange(ZsDefenses);
                    var loc = wo.ValidLocations ?? EquipMask.None;
                    var flavor = new HashSet<SpellId>();
                    if ((loc & EquipMask.HeadWear) != 0)
                        flavor.UnionWith(new[] { SpellId.CANTRIPFOCUS1, SpellId.CANTRIPWILLPOWER1, SpellId.CANTRIPARCANEPROWESS1, SpellId.CANTRIPMANACONVERSIONPROWESS1 });
                    if ((loc & (EquipMask.ChestArmor | EquipMask.UpperArmArmor | EquipMask.LowerArmArmor)) != 0)
                        flavor.UnionWith(new[] { SpellId.CANTRIPSTRENGTH1, SpellId.CANTRIPENDURANCE1 });
                    if ((loc & EquipMask.HandWear) != 0)
                    {
                        flavor.Add(SpellId.CANTRIPCOORDINATION1);
                        flavor.UnionWith(ZsWeaponAptitudes);
                        flavor.Add(SpellId.CantripShieldAptitude1);
                    }
                    if ((loc & (EquipMask.AbdomenArmor | EquipMask.UpperLegArmor | EquipMask.LowerLegArmor)) != 0)
                        flavor.UnionWith(new[] { SpellId.CANTRIPSTRENGTH1, SpellId.CANTRIPQUICKNESS1, SpellId.CANTRIPSPRINT1, SpellId.CANTRIPJUMPINGPROWESS1 });
                    if ((loc & EquipMask.FootWear) != 0)
                        flavor.UnionWith(new[] { SpellId.CANTRIPQUICKNESS1, SpellId.CANTRIPCOORDINATION1, SpellId.CANTRIPSPRINT1, SpellId.CANTRIPJUMPINGPROWESS1 });
                    pool.AddRange(flavor);
                    return pool;
                }

                case ZoneModifiers.SlotMask.Jewelry:
                    pool.AddRange(ZsWards);
                    pool.Add(SpellId.CANTRIPARMOR1);
                    pool.AddRange(ZsDefenses);
                    pool.AddRange(ZsMagicAptitudes);
                    pool.AddRange(ZsWeaponAptitudes);
                    pool.Add(SpellId.CantripDualWieldAptitude1);
                    pool.AddRange(ZsFighting);
                    pool.Add(SpellId.CANTRIPARCANEPROWESS1);
                    pool.Add(SpellId.CANTRIPMANACONVERSIONPROWESS1);
                    return pool;

                case ZoneModifiers.SlotMask.Clothing:
                case ZoneModifiers.SlotMask.Cloak:
                    pool.AddRange(ZsWards);
                    pool.Add(SpellId.CANTRIPARMOR1);
                    return pool;
            }
            return null;
        }

        /// <summary>The Thirst cantrip a weapon can roll: Blood Thirst on melee / missile, Spirit Thirst on an ELEMENTAL caster
        /// (retail: Spirit Drinker only there); Undef for everything else.</summary>
        public static SpellId ThirstFamily(WorldObject wo)
        {
            if (wo is Caster)
                return wo.W_DamageType != DamageType.Undef ? SpellId.CantripSpiritThirst1 : SpellId.Undef;
            if (wo is MeleeWeapon || wo is MissileLauncher)
                return SpellId.CANTRIPBLOODTHIRST1;
            return SpellId.Undef;
        }

        /// <summary>The mana every item-spell piece carries (as /asforge), enough to activate and run for hours.</summary>
        private const int ZoneSpellMana = 3500;

        /// <summary>A zone stat as a whole number; a non-finite value (a hand-edited store) reads as the fallback.</summary>
        private static int ZsStat(EvaluatedProfile p, string stat, int fallback, int tier)
        {
            var v = p?.GetT(stat, fallback, tier) ?? fallback;
            return double.IsFinite(v) ? (int)Math.Round(Math.Clamp(v, int.MinValue, int.MaxValue), MidpointRounding.AwayFromZero) : fallback;
        }

        /// <summary>Rolls the piece's item spells (see the class summary). Returns how many were added.</summary>
        public static int ApplyZoneSpells(WorldObject wo, int tier, EvaluatedProfile p)
        {
            var pool = ZoneSpellPool(wo);
            if (pool == null || pool.Count == 0)
                return 0;

            var countMax = Math.Clamp(ZsStat(p, ZoneStat.ItemSpellCountMax, 3, tier), 0, pool.Count);
            var countMin = Math.Clamp(ZsStat(p, ZoneStat.ItemSpellCountMin, 1, tier), 0, countMax);   // min above max acts as max
            var levelMax = Math.Clamp(ZsStat(p, ZoneStat.ItemSpellLevelMax, 4, tier), 1, 4);
            var levelMin = Math.Clamp(ZsStat(p, ZoneStat.ItemSpellLevelMin, 4, tier), 1, levelMax);
            var count = ThreadSafeRandom.Next(countMin, countMax);
            if (count <= 0)
                return 0;

            // Blood Thirst (melee / missile) / Spirit Thirst (elemental caster) - their own rare roll (owner 2026-10-06: "It should be
            // way more rare"), outside the slot pool; a hit takes one of this drop's spell slots, so the count never grows
            var thirst = ThirstFamily(wo);
            var thirstChance = p?.GetT(ZoneStat.ItemSpellThirstChance, 0.02, tier) ?? 0.02;
            var thirstHit = thirst != SpellId.Undef && double.IsFinite(thirstChance)
                && ThreadSafeRandom.Next(0.0f, 1.0f) < Math.Clamp(thirstChance, 0.0, 1.0);

            // one per family: the families already on the base weenie are skipped (read under the biota lock)
            var have = new HashSet<SpellId>();
            foreach (var id in wo.Biota.GetKnownSpellsIds(wo.BiotaDatabaseLock))
            {
                var fam = SpellLevelProgression.GetSpellLevels((SpellId)id);
                if (fam != null && fam.Count > 0) have.Add(fam[0]);
            }
            // a base that already carries the Thirst family keeps its full count of other spells
            thirstHit &= !have.Contains(thirst);

            var added = 0;
            var picks = pool.Where(s => !have.Contains(s)).OrderBy(_ => ThreadSafeRandom.Next(0, int.MaxValue - 1)).Take(thirstHit ? count - 1 : count).ToList();
            if (thirstHit)
                picks.Insert(0, thirst);
            foreach (var minor in picks)
            {
                var levels = SpellLevelProgression.GetSpellLevels(minor);
                if (levels == null || levels.Count != 4)
                    continue;
                var spell = levels[ThreadSafeRandom.Next(levelMin, levelMax) - 1];
                wo.Biota.GetOrAddKnownSpell((int)spell, wo.BiotaDatabaseLock, out _);
                added++;
            }
            if (added == 0)
                return 0;

            // mana + activation: enough mana to activate (CurMana must be > 1), retail drain off the strongest spell, the
            // spellcraft of the strongest spell (a Cast on Strike stamp later only raises it), NO Arcane Lore requirement
            // (a T11 drop must never be blocked by it), and the magical glow on pieces that carry no element tint
            var maxBase = GetMaxBaseMana(wo);
            wo.ItemMaxMana = Math.Max(wo.ItemMaxMana ?? 0, ZoneSpellMana);
            wo.ItemCurMana = wo.ItemMaxMana;
            wo.ManaRate = CalculateManaRate(maxBase);
            wo.ItemSpellcraft = Math.Max(wo.ItemSpellcraft ?? 0, GetMaxSpellPower(wo));
            wo.ItemDifficulty = null;
            if (!(wo is MeleeWeapon) && !(wo is MissileLauncher) && !(wo is Caster) && (wo.UiEffects ?? UiEffects.Undef) == UiEffects.Undef)
                wo.UiEffects = UiEffects.Magical;
            return added;
        }
    }
}
