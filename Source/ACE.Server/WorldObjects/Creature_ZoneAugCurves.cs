using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        // ────────────────────────────────────────────────────────────────────────────────────────────────────────
        // Zone Control AUG CURVES (owner 2026-10-02, C:\AI\ZoneControl\ZoneAugCurves_Plan_2026-10-02.md).
        //
        // The game's own aug math drives a player's protections to 100 pct (~7,500 life augs) and stacks armor through
        // Impenetrability (+1 armor level per item aug), so the NORMAL part of an endgame monster hit vanishes - no swing
        // gets through and the tiers above lose any normal / True balance. A tier that turns `aug_curves` on replaces
        // those aug parts, for ITS monsters' hits on players only (melee, missile, spells):
        //   protections = each protection spell at its BASE strength x (1 - life curve(effective life augs))
        //   armor       = armor + Impen / Banes / Armor Self WITHOUT their aug parts -> armor formula x (1 - item curve)
        // Buffs still matter (no spell = no layer - option A: a missing buff is much more dangerous, not instant death),
        // and the curves cap below 1, so nobody becomes immune. Unset = the game's own math, unchanged.
        // ────────────────────────────────────────────────────────────────────────────────────────────────────────

        private static readonly string[] NoCurvePoints = Array.Empty<string>();

        /// <summary>The attacker's zone profile when its tier has aug curves on; null otherwise (the game's own math).
        /// A spell projectile counts as its caster.</summary>
        public static EvaluatedProfile ZoneAugCurveProfile(WorldObject attacker)
        {
            if (attacker is SpellProjectile sp) attacker = sp.ProjectileSource;
            if (attacker is not Creature c || attacker is Player) return null;
            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(c);
            return zp != null && zp.Get(ZoneStat.AugCurves) >= 0.5 ? zp : null;
        }

        /// <summary>Share of the normal part the player's life augs (incl. Triune) remove through protections.</summary>
        public static double ZoneLifeAugCut(EvaluatedProfile zp, Player p) =>
            GetAxisRelief(zp, NoCurvePoints, p.EffectiveLifeAugCount,
                ReliefAnchor(zp, ZoneStat.AugProtStart, ServerConfig.zc_aug_prot_start.Value),
                ReliefAnchor(zp, ZoneStat.AugProtMax, ServerConfig.zc_aug_prot_max.Value),
                Math.Min(0.99, ReliefAnchor(zp, ZoneStat.AugProtCap, ServerConfig.zc_aug_prot_cap.Value)),
                ReliefAnchor(zp, ZoneStat.AugProtBend, ServerConfig.zc_aug_prot_bend.Value));

        /// <summary>Share of the normal part the player's item augs (incl. Triune) remove through armor.</summary>
        public static double ZoneItemAugCut(EvaluatedProfile zp, Player p) =>
            GetAxisRelief(zp, NoCurvePoints, p.EffectiveItemAugCount,
                ReliefAnchor(zp, ZoneStat.AugArmorStart, ServerConfig.zc_aug_armor_start.Value),
                ReliefAnchor(zp, ZoneStat.AugArmorMax, ServerConfig.zc_aug_armor_max.Value),
                Math.Min(0.99, ReliefAnchor(zp, ZoneStat.AugArmorCap, ServerConfig.zc_aug_armor_cap.Value)),
                ReliefAnchor(zp, ZoneStat.AugArmorBend, ServerConfig.zc_aug_armor_bend.Value));

        /// <summary>The player's protection multiplier vs a curves-on tier: spell base strength x (1 - life curve).</summary>
        public static float ZoneProtectionMod(Player p, DamageType damageType, EvaluatedProfile zp)
        {
            var baseProt = p.EnchantmentManager.GetProtectionResistanceModNoAugs(damageType);
            return (float)(baseProt * (1.0 - ZoneLifeAugCut(zp, p)));
        }

        /// <summary>
        /// The armor multiplier vs a curves-on tier: the game's armor formula with every buff at its base strength (no
        /// item-aug Impen / Banes, no life-aug Armor Self), then x (1 - item curve). Mirrors Monster_Melee.GetArmorMod.
        /// </summary>
        public static float ZoneArmorMod(Creature attacker, Player defender, DamageType damageType, List<WorldObject> armors,
            WorldObject weapon, float armorRendingMod, EvaluatedProfile zp)
        {
            var ignoreMagicArmor = (weapon?.IgnoreMagicArmor ?? false) || attacker.IgnoreMagicArmor;
            var ignoreMagicResist = (weapon?.IgnoreMagicResist ?? false) || attacker.IgnoreMagicResist;

            var effectiveAL = 0.0f;
            if (armors != null)
            {
                foreach (var armor in armors)
                {
                    var baseArmor = armor.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.ArmorLevel) ?? 0;
                    var armorMod = ignoreMagicArmor ? 0 : armor.EnchantmentManager.GetArmorModNoAugs();
                    var al = baseArmor + armorMod;
                    var bane = ignoreMagicArmor ? 0f : armor.EnchantmentManager.GetArmorModVsTypeNoAugs(damageType);
                    var rl = Math.Clamp((float)(GetResistance(armor, damageType) + bane), -2.0f, 2.0f);
                    effectiveAL += al * rl;
                }
            }

            var bodyArmor = ignoreMagicResist ? 0 : defender.EnchantmentManager.GetBodyArmorModNoAugs();
            effectiveAL += bodyArmor;
            if (effectiveAL > 0)
                effectiveAL *= armorRendingMod;

            var mod = SkillFormula.CalcArmorMod(effectiveAL);
            return (float)(mod * (1.0 - ZoneItemAugCut(zp, defender)));
        }
    }
}
