using System;
using System.Collections.Concurrent;

using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// A VOID DoT TICK IS A SPELL HIT (owner 2026-10-12, ZoneControl\Plan_2026-10-12_Void_Dots_SpellArmorRend_Crush.md 3.1:
    /// "make the base nether dots get boosted by rend and slayer and crits and everything else a normal war spell would
    /// benefit from" - for everybody, no charm; crit "per tick"; the caster's numbers "fixed at cast").
    ///
    /// Before, a player's void DoT tick on a Zone Control monster read no weapon card at all and could not crit: the same
    /// tick from a bare wand and from the best one. Now the tick is built the way a streak from that wand on that monster is
    /// built, from the DoT's own base:
    ///
    ///   AT THE CAST (EnchantmentManager.Add -> CalculateDotEnchantment_StatModValue) the caster's side is worked out once and
    ///   kept on the monster: (base + void augs + skill bonus) x the wand's elemental bonus x Slayer x the attribute bonus x
    ///   Damage Rating, the same again for a crit (x (1 + crit mod), Crit Damage Rating), the crit chance, the wand's Rend
    ///   and its Spell Armor Rend. Swapping wands after the cast changes nothing until the recast.
    ///
    ///   AT EVERY TICK the monster's side is read live: nether resist with that Rend (vuln and the DoT bonus as they stand),
    ///   Spell Armor less that Spell Armor Rend, Damage Resist Rating (plus Crit Damage Resist on a crit), then the DoT
    ///   multipliers (dot_damage_mult, dot_mult_corruption / _destructive). dot_armor is NOT read for such a tick - it stood
    ///   in for Spell Armor and the ratings, which now apply themselves. A crit is rolled per tick.
    ///
    /// GATE: a PLAYER's void DoT on a debuff-compressed (Zone Control, endgame variation) monster, zc_dot_spell_math on. A tick
    /// with no snapshot - retail, the base world, a monster's DoT, a pet, PvP - takes the old path untouched.
    /// </summary>
    partial class Creature
    {
        /// <summary>The caster's half of a void DoT, fixed at the cast. Per 5 s (the heartbeat the DoTs are written for).</summary>
        public sealed class ZcDotSnapshot
        {
            public uint SpellId;
            public float NormalPre;       // a normal tick before the monster's side
            public float CritPre;         // a crit tick before the monster's side
            public float CritChance;      // the caster's own, from the wand at the cast
            public float RendMod;         // the wand's resistance modifier (Rend), 1 = none
            public double SpellArmorRend; // fraction of the Spell Armor level ignored, 0 = none
            /// <summary>Which damage-taken dial the ticks use: Void for a hand cast, the weapon's kind for a Cast on Strike
            /// proc ("tie it to the weapon", owner 2026-10-12).</summary>
            public ACE.Server.Managers.ZoneControl.ZoneControlManager.ZcAttackKind AttackKind;
            // PANDEMIC charm (Creature_Pandemic.cs): 0 = this DoT does not spread; else the charm level it was cast with
            public int PandemicLevel;
            public int Generation;        // 0 = the cast itself, 1 = a copy of it, 2 = a copy of a copy ...
            public bool FractureHandCast; // Never inherited by copies or weapon procs.
            public double PandemicEndsAt; // unix time the ORIGINAL cast ends; no copy spreads or is made after it
            public float LeashCastDistance; // metres caster -> monster at the cast (0 for a copy); the leash's clear allows this much
            /// <summary>The Pandemic charm's extra crit chance, SET (never added) by the arm / a copy, kept apart from the
            /// caster's own so a refresh cannot stack it (review 2026-10-20).</summary>
            public float CharmCritBonus;

            /// <summary>The wand of the cast (round 2): Pandemic copies are built from it, never from whatever is in hand when
            /// they spread, so the caster's side stays "fixed at the cast" down the whole infection.</summary>
            public WorldObject CastWeapon;

            /// <summary>The crit chance a tick rolls against.</summary>
            public float EffectiveCritChance => Math.Clamp(CritChance + CharmCritBonus, 0f, 1f);
        }

        // keyed by the SPELL, not its family (review 2026-10-20): one caster can hold two entries of one family (a cast and a
        // weaker proc on their own layers), and the one that ticks must keep its own numbers
        private ConcurrentDictionary<(uint Caster, uint SpellId), ZcDotSnapshot> zcDotSnapshots;

        /// <summary>Called as a void DoT lands (or is refreshed) on this monster: work out and keep the caster's side.
        /// <paramref name="basePerTick"/> is the spell's own damage per heartbeat tick, before anything.</summary>
        public void ZcStoreDotSnapshot(Player caster, WorldObject weapon, Spell spell, float basePerTick)
        {
            if (caster == null || spell == null || !ZcDebuffCompressed || !ServerConfig.zc_dot_spell_math.Value)
                return;

            var wand = HandCastParityWeapon(weapon, caster);
            var skill = caster.GetCreatureSkill(spell.School);
            long augs = caster.EffectiveVoidAugCount;

            // the base a streak builds: the spell's own number + the school's augs, and the skill bonus on the spell's number
            var baseDamage = basePerTick + augs;
            var skillBonus = skill != null && skill.Current > spell.Power ? basePerTick * (skill.Current - spell.Power) / 1000.0f : 0.0f;

            var elementalMod = GetCasterElementalDamageModifier(wand, caster, this, spell.DamageType);
            var slayerMod = GetWeaponCreatureSlayerModifier(wand, caster, this);
            var attribBonus = 1.0f
                + SkillFormula.GetAttributeMod((int)caster.Focus.Current) * SpellProjectile.DefaultSpellAttributeMult
                + SkillFormula.GetAttributeMod((int)caster.Self.Current) * SpellProjectile.DefaultSpellAttributeMult;

            // crit: the hand-cast rule (SpellProjectile) - on Zone Control gear the melee floor and aug term, and Crit Damage
            // Rating on top; otherwise the stock combine
            var mirror = ZcSpellCritMirrorsMelee(caster, wand, this, false);
            var critMod = GetWeaponCritDamageMod(wand, caster, skill, this);
            if (mirror)
            {
                critMod = Math.Max(critMod, ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetSpellCritDamageBonus(wand, caster, augs));
                critMod += ZcSpellAugCritBonus(augs);
            }

            var heritageMod = caster.GetHeritageBonus(caster.GetEquippedWand()) ? 1.05f : 1.0f;
            var damageRatingMod = AdditiveCombine(GetPositiveRatingMod(caster.GetDamageRating()), heritageMod);
            var critDamageRatingMod = GetPositiveRatingMod(caster.GetCritDamageRating());

            // Deeds T13 (review 2026-10-24): the separate x1.10 every other attack style gets against monsters. The ticks use
            // this snapshot instead of the enchantment's value, which is where the old DoT path applied it.
            var deedsMod = Player.GetDeedsDamageMod(caster, this);

            var common = (baseDamage + skillBonus) * elementalMod * slayerMod * attribBonus * deedsMod;

            var snap = new ZcDotSnapshot
            {
                SpellId = spell.Id,
                NormalPre = common * damageRatingMod,
                CritPre = common * (1.0f + critMod) * (mirror ? damageRatingMod * critDamageRatingMod : AdditiveCombine(damageRatingMod, critDamageRatingMod)),
                CritChance = GetWeaponMagicCritFrequency(wand, caster, skill, this),
                RendMod = GetWeaponResistanceModifier(wand, caster, skill, spell.DamageType),
                SpellArmorRend = ZcSpellArmorRendOf(wand, caster),
                CastWeapon = wand,
                // a hand cast comes from a wand (or nothing); anything else is a Cast on Strike proc of that item
                AttackKind = ACE.Server.Managers.ZoneControl.ZoneControlManager.AttackKindOfSpell(caster, weapon, spell,
                    fromProc: weapon != null && !(weapon is Caster)),
            };

            // made once, whichever caster's thread gets here first (two DoTs can land on one monster at the same moment)
            var all = System.Threading.LazyInitializer.EnsureInitialized(ref zcDotSnapshots,
                () => new ConcurrentDictionary<(uint Caster, uint SpellId), ZcDotSnapshot>());
            var key = (caster.Guid.Full, spell.Id);

            if (!float.IsFinite(snap.NormalPre) || !float.IsFinite(snap.CritPre) || snap.NormalPre < 0)
            {
                all.TryRemove(key, out _);   // nothing sane to keep: the tick takes the old path (and no stale snapshot lingers)
                return;
            }

            // a REFRESH of the same DoT keeps its Pandemic marking (review 2026-10-20): a proc refresh never re-arms it, and
            // a hand cast always goes through ZcPandemicArm right after this (charm on or off), which clears or sets these
            // again. Only while that entry is still on the monster - a new entry (BuildEntry runs before it is added) never
            // inherits an old one's - and only from the CAST itself (generation 0): a proc landing on a spread COPY makes it
            // that proc's own full DoT, which does not spread (round 2: it kept spreading at full strength past its source).
            if (all.TryGetValue(key, out var old) && old.Generation == 0
                && EnchantmentManager.GetEnchantments(spell.Category).Exists(e => e.CasterObjectId == caster.Guid.Full && (uint)e.SpellId == spell.Id))
            {
                snap.PandemicLevel = old.PandemicLevel;
                snap.Generation = old.Generation;
                snap.PandemicEndsAt = old.PandemicEndsAt;
                snap.LeashCastDistance = old.LeashCastDistance;
                snap.CharmCritBonus = old.CharmCritBonus;
                // and the CAST's wand: a melee / missile proc refreshing a marked cast must not make later copies come from
                // the sword (round-2 fix-delta review)
                snap.CastWeapon = old.CastWeapon ?? snap.CastWeapon;
            }
            all[key] = snap;
        }

        /// <summary>The snapshot that belongs to this DoT entry, or null (then the tick takes the old path).</summary>
        public ZcDotSnapshot ZcDotSnapshotFor(PropertiesEnchantmentRegistry entry)
        {
            var all = zcDotSnapshots;
            if (all == null || entry == null || !ServerConfig.zc_dot_spell_math.Value)
                return null;
            return all.TryGetValue((entry.CasterObjectId, (uint)entry.SpellId), out var snap) ? snap : null;
        }

        /// <summary>One tick of a snapshotted void DoT on this monster, before the damage-taken multipliers.
        /// <paramref name="tickScale"/> is the share of the 5 s heartbeat this tick covers (dot_tick_seconds / 5).</summary>
        public float ZcDotTickAmount(ZcDotSnapshot snap, WorldObject damager, DamageType damageType, float tickScale, bool crit)
        {
            var amount = (crit ? snap.CritPre : snap.NormalPre) * tickScale;

            // the monster's side, as a spell hit meets it
            var resistanceMod = Math.Max(0.0f, GetResistanceMod(damageType, damager, null, snap.RendMod));
            resistanceMod *= GetZcSpellArmorModRended(damager, snap.SpellArmorRend);

            var damageResistRatingMod = GetDamageResistRatingMod(CombatType.Magic);
            if (crit)
                damageResistRatingMod = AdditiveCombine(damageResistRatingMod, GetNegativeRatingMod(GetCritDamageResistRating()));

            return IndependentVoidDots.Scale(this, snap, damager, amount * resistanceMod * damageResistRatingMod * GetZcDotMults(snap.SpellId));
        }
    }
}
