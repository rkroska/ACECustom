using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.WorldObjects
{
    public static class IndependentVoidDots
    {
        public static string Family(uint id) => Creature.IsCorrosionSpell(id) ? "corrosion" : Creature.IsCorruptionSpell(id) ? "corruption" : Creature.IsDestructiveCurseSpell(id) ? "curse" : "";
        private static Player Caster(Creature target, PropertiesEnchantmentRegistry e) => target.CurrentLandblock?.GetObject(e.CasterObjectId) as Player;
        private static bool Enabled(Creature c) => NetherFractureManager.Monster(c) && c.ZcDebuffCompressed && NetherFractureManager.Tuned(c);
        private static bool Eligible(Creature c, PropertiesEnchantmentRegistry e)
        {
            if (!Creature.IsVoidDotSpell((uint)e.SpellId) || e.Duration >= 0 && e.Duration + e.StartTime <= 0) return false;
            var p = Caster(c, e); var snap = c.ZcDotSnapshotFor(e);
            return p != null && p.IsAlive && NetherFractureManager.CanTarget(p, c) && snap != null && snap.AttackKind == ZoneControlManager.ZcAttackKind.Void;
        }
        private static double Source(NetherFractureZone z, uint spell) => Family(spell) switch
        {
            "corrosion" => z.Corrosion, "corruption" => z.Corruption, "curse" => z.Curse, _ => 1
        };
        private static double Expected(Creature c, PropertiesEnchantmentRegistry e)
        {
            var p = Caster(c, e); var snap = c.ZcDotSnapshotFor(e);
            if (p == null || snap == null || !NetherFractureManager.TryTuning(p, c, out var z)) return 0;
            var resist = Math.Max(0, c.GetResistanceMod(DamageType.Nether, p, null, snap.RendMod)) * c.GetZcSpellArmorModRended(p, snap.SpellArmorRend);
            var normal = snap.NormalPre * resist * c.GetDamageResistRatingMod(CombatType.Magic);
            var crit = snap.CritPre * resist * Creature.AdditiveCombine(c.GetDamageResistRatingMod(CombatType.Magic), Creature.GetNegativeRatingMod(c.GetCritDamageResistRating()));
            var value = (normal * (1 - snap.EffectiveCritChance) + crit * snap.EffectiveCritChance) * c.GetZcDotMults(snap.SpellId) * Source(z, snap.SpellId);
            return double.IsFinite(value) ? Math.Max(0, value) : 0;
        }
        private static List<PropertiesEnchantmentRegistry> Best(Creature c) =>
            (c.Biota.PropertiesEnchantmentRegistry.Clone(c.BiotaDatabaseLock) ?? new List<PropertiesEnchantmentRegistry>()).Where(e => Eligible(c, e))
            .GroupBy(e => (e.CasterObjectId, Family((uint)e.SpellId)))
            .Select(g => g.OrderByDescending(e => e.PowerLevel).ThenByDescending(e => Expected(c, e)).ThenByDescending(e => e.LayerId).First()).ToList();

        public static List<PropertiesEnchantmentRegistry> Select(Creature target, List<PropertiesEnchantmentRegistry> native)
        {
            if (native == null || !Enabled(target)) return native;
            var groups = Best(target).GroupBy(e => Family((uint)e.SpellId)).Where(g => g.Select(e => e.CasterObjectId).Distinct().Count() > 1)
                .ToDictionary(g => g.Key, g => g.ToList());
            if (groups.Count == 0) return native; // Exact native list and ordering for solo/non-Void effects.
            var result = native.Where(e => !groups.ContainsKey(Family((uint)e.SpellId)) || !Eligible(target, e)).ToList();
            foreach (var group in groups.Values) result.AddRange(group);
            return result;
        }

        public static float Scale(Creature target, Creature.ZcDotSnapshot snap, WorldObject damager, float amount)
        {
            if (damager is not Player p || snap.AttackKind != ZoneControlManager.ZcAttackKind.Void || !NetherFractureManager.TrySourceTuning(p, target, out var z)) return amount;
            amount = ApplySource(z, snap.SpellId, snap.AttackKind, amount);
            if (!Enabled(target) || !NetherFractureManager.CharmEligible(p)) return amount;
            var entries = Best(target).Where(e => Family((uint)e.SpellId) == Family(snap.SpellId)).ToList();
            if (!entries.Any(e => e.CasterObjectId == p.Guid.Full && e.SpellId == (int)snap.SpellId)) return amount;
            var weights = entries.Select(e => Expected(target, e)).ToArray();
            return amount * (float)NetherFractureMath.GroupFactor(entries.Count, weights.Sum(), weights.DefaultIfEmpty().Max());
        }
        internal static float ApplySource(NetherFractureZone zone, uint spell, ZoneControlManager.ZcAttackKind kind, float amount) =>
            kind == ZoneControlManager.ZcAttackKind.Void ? amount * (float)Source(zone, spell) : amount;
    }
}
