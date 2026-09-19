using System;
using System.Collections.Generic;
using System.Globalization;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Combat half of the trace (see PetDevice_Trace.cs for the format). Under the same pet_trace
    /// switch, every combat exchange server-wide is recorded: melee and missile through
    /// <see cref="DamageEvent"/>, spell projectiles, life-magic boosts and transfers, DoT ticks,
    /// healing kits and deaths, for players, monsters, combat pets and the mating guardian alike.
    ///
    /// Volume rule: a landed hit is ONE record (combat.damage, which carries the attack section);
    /// combat.attack is written only for a swing or cast that dealt nothing (evade, resist,
    /// lifestone, invincible, hit gate). A DoT tick is one record per creature per tick, however
    /// many DoTs stack on it. Nothing in here changes a roll or a number: the rolls are captured
    /// where they are drawn (DamageEvent / SpellProjectile fields), everything else is re-read.
    /// </summary>
    public static partial class PetTrace
    {
        /// <summary>
        /// Session id for a combat exchange: a mating guardian's breed session when one is involved
        /// (so its fight joins the breeding session), otherwise a fresh id per exchange.
        /// </summary>
        public static string CombatSession(WorldObject a, WorldObject b)
            => (a as MatingGuardian)?.TraceSession ?? (b as MatingGuardian)?.TraceSession ?? NewSessionId();

        /// <summary>The vital a damage type drains (stamina / mana), else health.</summary>
        public static uint VitalCurrent(Creature c, DamageType dt)
        {
            if (c == null) return 0;
            if (dt == DamageType.Stamina) return c.Stamina?.Current ?? 0;
            if (dt == DamageType.Mana) return c.Mana?.Current ?? 0;
            return c.Health?.Current ?? 0;
        }

        public static string VitalName(DamageType dt)
            => dt == DamageType.Stamina ? "stamina" : dt == DamageType.Mana ? "mana" : "health";

        // ------------------------------------------------------------------------------------------
        // Melee / missile (DamageEvent)
        // ------------------------------------------------------------------------------------------

        /// <summary>A swing that dealt nothing: evaded, lifestone, invincible, hit gate, failure.</summary>
        public static void CombatAttack(DamageEvent de)
        {
            if (de?.Attacker == null || de.Defender == null)
                return;
            de.TraceSession ??= CombatSession(de.Attacker, de.Defender);
            var r = Begin("combat.attack", de.TraceSession);
            r.AddPhysicalAttack(de);
            string reason;
            if (de.LifestoneProtection) reason = "lifestone";
            else if (de.Defender.Invincible) reason = "invincible";
            else if (de.GeneralFailure) reason = "generalFailure";
            else if (de.Evaded && de.EvadeRoll < 0) reason = "hitGateOrBodyPart";
            else if (de.Evaded) reason = "evaded";
            else reason = "noDamage";
            r.Add("hit", false).Add("reason", reason).Emit();
        }

        /// <summary>
        /// A landed melee / missile hit: the attack section, then every term of the damage
        /// arithmetic in the order DamageEvent applies it, then what the defender actually lost.
        /// </summary>
        public static void CombatDamage(DamageEvent de, uint vitalBefore, uint dealt)
        {
            if (de?.Attacker == null || de.Defender == null)
                return;
            de.TraceSession ??= CombatSession(de.Attacker, de.Defender);
            var r = Begin("combat.damage", de.TraceSession);
            r.AddPhysicalAttack(de).Add("hit", true);
            r.AddPhysicalDamage(de);

            if (de.Attacker is CombatPet petAttacker)
                r.AddPetSwing("pet.", petAttacker);
            if (de.Defender is CombatPet petDefender)
                r.AddPetDefense("petDef.", petDefender);

            if (de.Attacker is MatingGuardian gOut && de.Defender is CombatPet gPet)
            {
                gOut.TraceOutgoing(dealt);
                var petMaxHp = gPet.Health?.MaxValue ?? 500;
                var clamped = Math.Clamp(petMaxHp * 0.08f, 20.0f, 500.0f);
                var mult = ServerConfig.pet_breeding_guardian_damage_mult.Value;
                if (double.IsNaN(mult) || double.IsInfinity(mult) || mult < 0.0) mult = 1.0;
                r.Add("gOut.rule", "base=clamp(petMaxHp*0.08,20,500)*damageMult*(weakened?0.5:1); replaces the rolled base before ratings")
                 .Add("gOut.petMaxHp", petMaxHp).Add("gOut.clamped", clamped).Add("gOut.damageMult", mult)
                 .Add("gOut.weakened", gOut.IsWeakened).Add("gOut.base", clamped * (float)mult * (gOut.IsWeakened ? 0.5f : 1.0f));
            }
            if (de.Defender is MatingGuardian gIn)
            {
                var t = gIn.LastIncoming;
                r.Add("gIn.rule", "nRaw=maxHp/max(1,raw); mod=clamp((nRaw/30)^0.81,0.01,1); scaled=raw*mod*(weakened?2.5:1); capped at maxHp*(weakened?0.25:0.10); min 1")
                 .Add("gIn.maxHp", gIn.Health?.MaxValue ?? 0).Add("gIn.raw", t.Raw).Add("gIn.nRaw", t.NRaw).Add("gIn.mod", t.Mod)
                 .Add("gIn.weakMult", t.WeakMult).Add("gIn.scaled", t.Scaled).Add("gIn.cap", t.Cap).Add("gIn.capped", t.Capped).Add("gIn.final", t.Final);
            }

            var after = VitalCurrent(de.Defender, de.DamageType);
            r.Add("vital", VitalName(de.DamageType)).Add("dealt", dealt).Add("before", vitalBefore).Add("after", after)
             .Add("died", de.Defender.IsDead || (de.Defender.Health?.Current ?? 1) <= 0)
             .Emit();
        }

        private static Record AddPhysicalAttack(this Record r, DamageEvent de)
        {
            r.Add("kind", de.CombatType == CombatType.Missile ? "missile" : "melee");
            r.AddCreature("atk.", de.Attacker).AddCreature("def.", de.Defender);
            if (de.Weapon != null)
                r.Add("weapon", de.Weapon.Name).Add("weaponWcid", de.Weapon.WeenieClassId).AddGuid("weaponGuid", de.Weapon.Guid.Full);
            else
                r.Add("weapon", "none");
            if (de.DamageSource != null && de.DamageSource != de.Weapon && de.DamageSource != de.Attacker)
                r.Add("source", de.DamageSource.Name).Add("sourceWcid", de.DamageSource.WeenieClassId);
            r.Add("dmgType", de.DamageType).Add("attackType", de.AttackType).Add("height", de.AttackHeight);
            if (de.AttackMotion.HasValue)
                r.Add("atkMotion", de.AttackMotion.Value);
            if (de.AttackPart.Value != null)
                r.Add("atkPart", de.AttackPart.Key);
            if (de.BodyPart != 0)
                r.Add("defPart", de.BodyPart);
            else if (de.CreaturePart != null)
                r.Add("defPart", de.PropertiesBodyPart.Key).Add("quadrant", de.Quadrant);
            r.Add("atkSkill", de.EffectiveAttackSkill).Add("defSkill", de.EffectiveDefenseSkill).Add("accuracyMod", de.AccuracyMod)
             .Add("evadeRule", "evaded=evadeChance>evadeRoll; evadeChance=1-skillChance(atkSkill,defSkill)")
             .Add("evadeChance", de.EvasionChance).Add("evadeRoll", de.EvadeRoll).Add("overpower", de.Overpower).Add("evaded", de.Evaded)
             .Add("lifestone", de.LifestoneProtection);
            return r;
        }

        private static Record AddPhysicalDamage(this Record r, DamageEvent de)
        {
            var atk = de.Attacker; var def = de.Defender;
            var bdm = de.BaseDamageMod;

            // Base: the body part (or weapon) range, the roll inside it, and the flats layered on it.
            if (de.AttackPart.Value != null)
                r.Add("partDVal", de.AttackPart.Value.DVal).Add("partDVar", de.AttackPart.Value.DVar);
            if (bdm != null)
            {
                r.Add("baseRule", "baseMax=(partOrWeaponMax+damageBonus+elemental)*damageMod; baseMin=baseMax*(1-variance*varianceMod); baseRoll uniform in [baseMin,baseMax)")
                 .Add("baseMaxRaw", bdm.BaseDamage.MaxDamage).Add("baseVariance", bdm.BaseDamage.Variance)
                 .Add("baseDamageBonus", bdm.DamageBonus).Add("baseElemental", bdm.ElementalBonus).Add("baseDamageMod", bdm.DamageMod).Add("baseVarianceMod", bdm.VarianceMod)
                 .Add("baseMin", bdm.MinDamage).Add("baseMax", bdm.MaxDamage);
            }
            r.Add("baseRoll", de.BaseDamageRoll);
            if (atk is CombatPet mp)
                r.Add("maturityMult", mp.MaturityDamageMult);
            if (atk.IsEnraged && atk is not Player)
                r.Add("enrageMult", atk.EnrageDamageMultiplier ?? 1.0f);
            r.Add("lumFlat", de.DebugLuminanceFlatDamageBonus).Add("wsFlat", de.WeaponScalingFlatBonus);
            if (de.SchemeCRoll >= 0)
                r.Add("schemeCRoll", de.SchemeCRoll);
            r.Add("base", de.BaseDamage);

            // Pre-mitigation chain.
            r.Add("preMitRule", "preMit=base*attrMod*powerMod*slayerMod*dmgRatingMod; ratingMod=(100+rating)/100; mods combine additively on their ratings")
             .Add("attrMod", de.AttributeMod).Add("powerMod", de.PowerMod).Add("slayerMod", de.SlayerMod)
             .Add("dmgRating", atk.GetDamageRating()).Add("dmgRatingBaseMod", de.DamageRatingBaseMod)
             .Add("recklessMod", de.RecklessnessMod).Add("sneakMod", de.SneakAttackMod).Add("heritageMod", de.HeritageMod).Add("pkDmgMod", de.PkDamageMod)
             .Add("dmgRatingMod", de.DamageRatingMod);

            // Crit.
            r.Add("critRule", "crit=critChance>critRoll (critDefense may cancel); critChance=(weaponCrit + critRating*0.01)*100/(100+critResistRating); on crit preMit=maxNormalHit*critDmgMod*critDmgRatingMod")
             .Add("critRating", atk.GetCritRating()).Add("critResistRating", def.GetCritResistRating())
             .Add("critChance", de.CriticalChance).Add("critRoll", de.CritRoll).Add("critDefenseRoll", de.CritDefenseRoll).Add("critDefended", de.CriticalDefended).Add("crit", de.IsCritical);
            if (de.IsCritical)
                r.Add("critDmgMod", de.CriticalDamageMod).Add("critDmgRating", atk.GetCritDamageRating()).Add("critDmgRatingMod", de.CriticalDamageRatingMod);
            r.Add("preMit", de.DamageBeforeMitigation);

            // Mitigation.
            if (de.CreaturePart != null)
                r.Add("defBaseArmor", de.CreaturePart.Biota.Value.BaseArmor);
            if (de.Armor != null)
                r.Add("armorLayers", de.Armor.Count);
            r.Add("mitRule", "damage=preMit*armorMod*shieldMod*resistMod*drrMod; armorMod=100/(100+effectiveArmor); drrMod=100/(100+drr)")
             .Add("armorMod", de.ArmorMod).Add("shieldMod", de.ShieldMod).Add("weaponResistMod", de.WeaponResistanceMod).Add("resistMod", de.ResistanceMod);
            if (!float.IsNaN(de.DebugCombatPetOwnerResistanceMod))
                r.Add("ownerResistMod", de.DebugCombatPetOwnerResistanceMod);
            r.Add("drr", def.GetDamageResistRating(de.CombatType)).Add("drrBaseMod", de.DamageResistanceRatingBaseMod);
            if (de.IsCritical)
                r.Add("critDrr", def.GetCritDamageResistRating()).Add("critDrrMod", de.CriticalDamageResistanceRatingMod);
            if (de.PkDamageResistanceMod != 0.0f)
                r.Add("pkDrrMod", de.PkDamageResistanceMod);
            r.Add("drrMod", de.DamageResistanceRatingMod);

            // Post-mitigation extras, in order.
            if (de.DamageSource?.GetProperty(PropertyBool.IsSplitArrow) == true)
                r.Add("splitArrow", true);
            if (def.IsEnraged && def is not Player)
                r.Add("enrageReduction", def.EnrageDamageReduction ?? 0.0f);
            if (def is Player)
                r.Add("pctHpFloor", de.DebugPctHpFloor).Add("preFloor", de.DebugPreFloorDamage).Add("floorWon", de.DebugPctHpFloorWon);
            if (def is CombatPet)
                r.Add("prePetMit", de.DebugDamagePrePetPhysicalMitigation).Add("petCritMult", de.DebugCombatPetCritDamageTakenMultiplier).Add("petPhysMult", de.DebugCombatPetPhysicalMitigationMultiplier);
            r.Add("mitigated", de.DamageMitigated).Add("damage", de.Damage).Add("absorbed", de.AmountAbsorbed);
            return r;
        }

        /// <summary>
        /// The attacking pet's ratings AT THE MOMENT OF THE SWING (property and effective, enchantments
        /// included), its mutation counts and steps, its maturity multiplier and its potency: stored,
        /// active, the per-level fraction and the body-part multiplier that was baked into partDVal.
        /// </summary>
        public static Record AddPetSwing(this Record r, string p, CombatPet pet)
        {
            r.Add(p + "dmgRatingProp", pet.DamageRating ?? 0).Add(p + "dmgRatingEff", pet.GetDamageRating())
             .Add(p + "critRatingProp", pet.CritRating ?? 0).Add(p + "critRatingEff", pet.GetCritRating())
             .Add(p + "critDmgRatingProp", pet.CritDamageRating ?? 0).Add(p + "critDmgRatingEff", pet.GetCritDamageRating())
             .Add(p + "maturityMult", pet.MaturityDamageMult).Add(p + "dpsFactor", pet.MeleeMotionDpsFactor)
             .Add(p + "potencyApplied", pet.PotencyApplied);

            var device = pet.TryGetSummoningDevice();
            if (device == null)
                return r.Add(p + "device", "none");

            var config = PetDevice.BreedingMath.BreedingConfig.FromServerConfig();
            var g = device.ReadBreedingGenetics(config);
            var active = PetPotency.GetActivePotency(device);
            r.AddGuid(p + "device", device.Guid.Full).Add(p + "deviceName", device.Name)
             .Add(p + "gearDmg", g.GearDamage).Add(p + "mutDmg", g.Dmg).Add(p + "dmgStep", config.DamageMutationStep)
             .Add(p + "expectedDmgRating", g.GearDamage + g.Dmg * config.DamageMutationStep)
             .Add(p + "gearCrit", g.GearCrit).Add(p + "mutCrit", g.Crit).Add(p + "critStep", config.CritMutationStep)
             .Add(p + "mutVit", g.Vit).Add(p + "bond", device.PetBondLevel ?? 0)
             .Add(p + "juvenile", device.IsJuvenile).Add(p + "stage", device.MaturityStage).Add(p + "strengthMult", device.MaturityStrengthMult)
             .Add(p + "potencyStored", device.PetPotencyStored ?? 0).Add(p + "potencyActive", active)
             .Add(p + "potencyPerLevel", ServerConfig.pet_potency_damage_per_level.Value)
             .Add(p + "potencyMult", PetPotency.GetBodyPartDamageMult(active))
             .Add(p + "potencyRule", "partDVal=roundHalfUp(weenieDVal*potencyMult) at summon when potencyApplied; potencyMult from potencyActive*potencyPerLevel");
            return r;
        }

        /// <summary>The defending pet's resist-side ratings at the moment of the hit.</summary>
        public static Record AddPetDefense(this Record r, string p, CombatPet pet)
        {
            return r.Add(p + "drrProp", pet.DamageResistRating ?? 0).Add(p + "critResProp", pet.CritResistRating ?? 0)
                    .Add(p + "critDmgResProp", pet.CritDamageResistRating ?? 0).Add(p + "maxHp", pet.Health?.MaxValue ?? 0)
                    .Add(p + "strengthMult", pet.TryGetSummoningDevice()?.MaturityStrengthMult ?? 1.0);
        }

        // ------------------------------------------------------------------------------------------
        // Spell projectiles
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Everything SpellProjectile.CalculateDamage decided, carried to DamageTarget where the rating
        /// chain and the vital change are known. One instance per collision, created only when the
        /// trace is on.
        /// </summary>
        public sealed class SpellTrace
        {
            public string Session;
            public bool Resisted, Crit, CritDefended, Overpower, IsLife, IsPvp, ZcProc, EndgameCrit;
            public uint MagicSkill, MagicDefense;
            public float CritChance = -1;
            public double CritRoll = -1, CritDefenseRoll = -1;
            public float BaseMin, BaseMax, BaseRoll;
            public float LifeBase, Augs, SkillBonus, CritBonus, WeaponCritDmgMod = 1, Elemental = 1, Slayer = 1, WeaponResist = 1, Resist = 1, Absorb = 1, Attrib = 1, Fork = 1, ZoneMult = 1, PetSpellMult = 1, PreRating;
            public string Reason;
        }

        private static Record AddSpellAttack(this Record r, SpellProjectile sp, Creature target, SpellTrace t)
        {
            var source = sp.ProjectileSource;
            r.Add("kind", "magic");
            if (source is Creature sc) r.AddCreature("atk.", sc);
            else if (source != null) r.Add("atk.name", source.Name).AddGuid("atk.guid", source.Guid.Full).Add("atk.kind", "Object");
            else r.Add("atk.guid", "null");
            r.AddCreature("def.", target);
            r.Add("spell", sp.Spell.Name).Add("spellId", sp.Spell.Id).Add("school", sp.Spell.School).Add("dmgType", sp.Spell.DamageType).Add("power", sp.Spell.Power);
            if (sp.ProjectileLauncher != null)
                r.Add("weapon", sp.ProjectileLauncher.Name).Add("weaponWcid", sp.ProjectileLauncher.WeenieClassId);
            r.Add("fromProc", sp.FromProc).Add("weaponSpell", sp.IsWeaponSpell).Add("fork", sp.IsForkProjectile);
            r.Add("resistRule", "resisted by MagicDefenseCheck(magicSkill, magicDefense) - the roll is internal to that check")
             .Add("magicSkill", t.MagicSkill).Add("magicDefense", t.MagicDefense).Add("resisted", t.Resisted).Add("overpower", t.Overpower);
            return r;
        }

        /// <summary>A cast that dealt nothing: resisted, lifestone, invincible, immune, dead.</summary>
        public static void CombatSpellMiss(SpellProjectile sp, Creature target, SpellTrace t)
        {
            if (sp?.Spell == null || target == null || t == null)
                return;
            t.Session ??= CombatSession(sp.ProjectileSource, target);
            Begin("combat.attack", t.Session).AddSpellAttack(sp, target, t).Add("hit", false).Add("reason", t.Reason ?? "noDamage").Emit();
        }

        /// <summary>A landed spell: CalculateDamage's composition, then DamageTarget's rating chain and the vital change.</summary>
        public static void CombatSpellDamage(SpellProjectile sp, Creature target, SpellTrace t,
            float heritageMod, float sneakAttackMod, float critDamageRatingMod, float critDamageResistRatingMod,
            float pkDamageRatingMod, float pkDamageResistRatingMod, float damageRatingMod, float damageResistRatingMod,
            float damage, DamageType vitalType, uint vitalBefore, uint dealt, uint absorbed)
        {
            if (sp?.Spell == null || target == null || t == null)
                return;
            t.Session ??= CombatSession(sp.ProjectileSource, target);
            var sourceCreature = sp.ProjectileSource as Creature;

            var r = Begin("combat.damage", t.Session).AddSpellAttack(sp, target, t).Add("hit", true);
            r.Add("critRule", "crit=critRoll<critChance (critDefense may cancel); critChance=(weaponCrit + critRating*0.01)*100/(100+critResistRating)")
             .Add("critRating", sourceCreature?.GetCritRating() ?? 0).Add("critResistRating", target.GetCritResistRating())
             .Add("critChance", t.CritChance).Add("critRoll", t.CritRoll).Add("critDefenseRoll", t.CritDefenseRoll).Add("critDefended", t.CritDefended).Add("crit", t.Crit)
             .Add("endgameCrit", t.EndgameCrit).Add("pvp", t.IsPvp);
            if (t.IsLife)
                r.Add("baseRule", "life: base=lifeProjectileDamage*damageRatio (+lifeAugs); crit adds base*critDmgMod (endgame) or preAugBase*0.5*critDmgMod (retail); pre=(base+critBonus)*elemental*slayer*resist*absorb*attrib")
                 .Add("lifeBase", t.LifeBase).Add("base", t.BaseRoll);
            else
                r.Add("baseRule", "war/void: base=roll[spellMin,spellMax] (max on endgame crit) (+augs, zone/proc replacement, variance); crit adds (base+skillBonus)*critDmgMod (endgame) or spellMax*0.5*critDmgMod (retail); pre=(base+critBonus+skillBonus)*elemental*slayer*resist*absorb*attrib")
                 .Add("spellMin", t.BaseMin).Add("spellMax", t.BaseMax).Add("zcProc", t.ZcProc).Add("base", t.BaseRoll).Add("augs", t.Augs).Add("skillBonus", t.SkillBonus);
            r.Add("critDmgMod", t.WeaponCritDmgMod).Add("critBonus", t.CritBonus)
             .Add("elementalMod", t.Elemental).Add("slayerMod", t.Slayer).Add("weaponResistMod", t.WeaponResist).Add("resistMod", t.Resist).Add("absorbMod", t.Absorb).Add("attribMod", t.Attrib)
             .Add("forkMult", t.Fork).Add("zoneMult", t.ZoneMult).Add("petSpellMult", t.PetSpellMult).Add("preRating", t.PreRating);
            r.Add("ratingRule", "damage=preRating*dmgRatingMod*drrMod; dmgRatingMod combines dmgRating, heritage, sneak, critDmgRating, pk; drrMod combines drr, critDrr, pk")
             .Add("dmgRating", sourceCreature?.GetDamageRating() ?? 0).Add("heritageMod", heritageMod).Add("sneakMod", sneakAttackMod)
             .Add("critDmgRating", sourceCreature?.GetCritDamageRating() ?? 0).Add("critDmgRatingMod", critDamageRatingMod)
             .Add("pkDmgRatingMod", pkDamageRatingMod).Add("dmgRatingMod", damageRatingMod)
             .Add("drr", target.GetDamageResistRating(CombatType.Magic)).Add("critDrr", target.GetCritDamageResistRating()).Add("critDrrMod", critDamageResistRatingMod)
             .Add("pkDrrMod", pkDamageResistRatingMod).Add("drrMod", damageResistRatingMod);
            if (target.IsEnraged && target is not Player)
                r.Add("enrageReduction", target.EnrageDamageReduction ?? 0.0f);
            if (sourceCreature is CombatPet petAttacker)
                r.AddPetSwing("pet.", petAttacker);
            if (target is CombatPet petDefender)
                r.AddPetDefense("petDef.", petDefender);
            var after = VitalCurrent(target, vitalType);
            r.Add("damage", damage).Add("absorbed", absorbed).Add("vital", VitalName(vitalType)).Add("dealt", dealt).Add("before", vitalBefore).Add("after", after)
             .Add("died", target.IsDead || (target.Health?.Current ?? 1) <= 0)
             .Emit();
        }

        // ------------------------------------------------------------------------------------------
        // Life magic boost / transfer, DoT ticks, healing kits, deaths
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// A Boost spell (heal or harm). Positive boosts are combat.heal, negative ones combat.damage
        /// with kind=boost. The motel own-pet heal scaling is recomputed here from the same inputs.
        /// </summary>
        public static void CombatBoost(WorldObject caster, Creature target, Spell spell, int minBoost, int maxBoost, int rawRoll,
            ResistanceType resistanceType, double resistanceMod, bool useHarmCap, int afterResist, int afterAugs, int requested, int applied,
            uint mbAbsorbed, uint vitalBefore)
        {
            if (caster == null || target == null || spell == null)
                return;
            var heal = minBoost > 0;
            var r = Begin(heal ? "combat.heal" : "combat.damage", CombatSession(caster, target)).Add("kind", "boost");
            if (caster is Creature cc) r.AddCreature("atk.", cc); else r.Add("atk.name", caster.Name).AddGuid("atk.guid", caster.Guid.Full);
            r.AddCreature("def.", target).Add("spell", spell.Name).Add("spellId", spell.Id).Add("vital", spell.VitalDamageType);
            var lifeAugs = (caster as Player)?.EffectiveLifeAugCount ?? 0;
            var motelScale = 1.0f;
            if (!useHarmCap && caster is Player casterPlayer && heal && spell.VitalDamageType == DamageType.Health && target is CombatPet targetPet && targetPet.IsInMotelOrEncounter())
            {
                var casterHp = casterPlayer.Health?.MaxValue ?? 0;
                var petHp = targetPet.Health?.MaxValue ?? 0;
                if (casterHp > 0 && petHp > casterHp)
                    motelScale = Math.Clamp((float)petHp / casterHp, 1.0f, 8.0f);
            }
            r.Add("rule", "roll uniform in [minBoost,maxBoost]; afterResist=round(roll*resistMod) (skipped when harmCap); heal: afterAugs=round((afterResist+lifeAugs)*motelScale); harm: afterAugs=afterResist-lifeAugs; cloak then mana barrier then apply")
             .Add("minBoost", minBoost).Add("maxBoost", maxBoost).Add("roll", rawRoll)
             .Add("resistType", resistanceType).Add("resistMod", resistanceMod).Add("harmCap", useHarmCap).Add("afterResist", afterResist)
             .Add("lifeAugs", lifeAugs).Add("motelScale", motelScale).Add("afterAugs", afterAugs)
             .Add("mbAbsorbed", mbAbsorbed).Add("requested", requested).Add("applied", applied)
             .Add("before", vitalBefore).Add("after", VitalCurrent(target, spell.VitalDamageType));
            if (!heal)
                r.Add("died", target.IsDead || (target.Health?.Current ?? 1) <= 0);
            r.Emit();
        }

        /// <summary>Beneficial self-boost overflow handed to the caster's combat pet.</summary>
        public static void CombatBoostOverflow(Player caster, CombatPet pet, Spell spell, int overflow, int applied)
        {
            Begin("combat.heal", CombatSession(caster, pet)).Add("kind", "overflow").AddCreature("atk.", caster).AddCreature("def.", pet)
                .Add("spell", spell.Name).Add("spellId", spell.Id).Add("vital", spell.VitalDamageType).Add("overflow", overflow).Add("applied", applied)
                .Add("after", VitalCurrent(pet, spell.VitalDamageType)).Emit();
        }

        /// <summary>A Transfer spell (drain / infuse): what left the source and what reached the destination.</summary>
        public static void CombatTransfer(WorldObject caster, Creature target, Spell spell, Creature source, Creature destination,
            bool isDrain, float drainMod, float boostMod, uint srcRequested, uint destRequested, uint mbAbsorbed, uint srcApplied, uint destApplied,
            uint srcBefore, uint destBefore, uint srcAfter, uint destAfter)
        {
            if (caster == null || target == null || spell == null)
                return;
            var r = Begin("combat.heal", CombatSession(caster, target)).Add("kind", "transfer");
            if (caster is Creature cc) r.AddCreature("atk.", cc); else r.Add("atk.name", caster.Name).AddGuid("atk.guid", caster.Guid.Full);
            r.AddCreature("def.", target).AddCreature("src.", source).AddCreature("dst.", destination)
             .Add("spell", spell.Name).Add("spellId", spell.Id).Add("srcVital", spell.Source).Add("dstVital", spell.Destination)
             .Add("rule", "src=round(srcCurrent*proportion*drainMod) capped by transferCap; dst=round(src*(1-lossPercent)*boostMod) capped by missing dst vital; +lifeAugs each")
             .Add("drain", isDrain).Add("proportion", spell.Proportion).Add("drainMod", drainMod).Add("transferCap", spell.TransferCap).Add("lossPercent", spell.LossPercent).Add("boostMod", boostMod)
             .Add("lifeAugs", (caster as Player)?.EffectiveLifeAugCount ?? 0)
             .Add("mbAbsorbed", mbAbsorbed).Add("srcRequested", srcRequested).Add("srcApplied", srcApplied).Add("srcBefore", srcBefore).Add("srcAfter", srcAfter)
             .Add("dstRequested", destRequested).Add("dstApplied", destApplied).Add("dstBefore", destBefore).Add("dstAfter", destAfter)
             .Add("srcDied", source.IsDead || (source.Health?.Current ?? 1) <= 0)
             .Emit();
        }

        /// <summary>Life magic projectile cost paid by the caster (Blight / Tenacity / Martyr's).</summary>
        public static void CombatLifeCost(Creature caster, Spell spell, DamageType vital, int requested, uint applied, uint before)
        {
            if (caster == null || spell == null)
                return;
            Begin("combat.damage", CombatSession(caster, null)).Add("kind", "lifeCost").AddCreature("atk.", caster).AddCreature("def.", caster)
                .Add("spell", spell.Name).Add("spellId", spell.Id).Add("vital", VitalName(vital)).Add("drainPercentage", spell.DrainPercentage)
                .Add("rule", "requested=round(currentVital*drainPercentage)").Add("requested", requested).Add("applied", applied)
                .Add("before", before).Add("after", VitalCurrent(caster, vital)).Add("died", caster.IsDead || (caster.Health?.Current ?? 1) <= 0).Emit();
        }

        /// <summary>One DoT enchantment's share of a tick.</summary>
        public struct DotPart
        {
            public WorldObject Damager;
            public int SpellId;
            public float Base, ResistMod, DrrMod, DotResistMod, NetherMod, Amount;
        }

        /// <summary>One record per creature per tick, summarising every DoT that ticked on it.</summary>
        public static void CombatDot(Creature target, DamageType damageType, bool aetheria, float total, uint before, List<DotPart> parts)
        {
            if (target == null)
                return;
            var r = Begin("combat.damage", CombatSession(target, null)).Add("kind", "dot").AddCreature("def.", target)
                .Add("dmgType", damageType).Add("aetheria", aetheria)
                .Add("rule", "per DoT: amount=statModValue*resistMod*drrMod*dotResistMod*netherMod, capped at remaining health; tick=sum")
                .Add("dots", parts?.Count ?? 0);
            if (parts != null)
            {
                var n = Math.Min(parts.Count, 4);
                for (var i = 0; i < n; i++)
                {
                    var p = parts[i];
                    var k = "d" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".";
                    r.Add(k + "from", p.Damager?.Name ?? "unknown");
                    if (p.Damager != null) r.AddGuid(k + "fromGuid", p.Damager.Guid.Full);
                    r.Add(k + "spellId", p.SpellId).Add(k + "base", p.Base).Add(k + "resistMod", p.ResistMod).Add(k + "drrMod", p.DrrMod)
                     .Add(k + "dotResistMod", p.DotResistMod).Add(k + "netherMod", p.NetherMod).Add(k + "amount", p.Amount);
                }
                if (parts.Count > n)
                    r.Add("more", parts.Count - n);
            }
            r.Add("tick", total).Add("vital", "health").Add("before", before).Add("after", target.Health?.Current ?? 0)
             .Add("died", target.IsDead || (target.Health?.Current ?? 1) <= 0).Emit();
        }

        /// <summary>A healing kit use: the skill check, the heal range, the rating and motel scaling, and the result.</summary>
        public static void CombatHealKit(Healer kit, Player healer, Creature target, CreatureVital vital, bool success, int difficulty,
            uint healAmount, bool critical, uint staminaCost, uint before)
        {
            if (kit == null || healer == null || target == null)
                return;
            var healingSkill = healer.GetCreatureSkill(Skill.Healing);
            var trainedMod = healingSkill.AdvancementClass == SkillAdvancementClass.Specialized ? 1.5f : 1.1f;
            var combatMod = healer.CombatMode == CombatMode.NonCombat ? 1.0f : 1.1f;
            var effectiveSkill = (int)Math.Round((healingSkill.Current + kit.BoostValue) * trainedMod);
            var healBase = healingSkill.Current * (float)(kit.HealkitMod ?? 1.0);
            var petHealScale = 1.0f;
            if (target is CombatPet pet && pet.IsInMotelOrEncounter())
            {
                var healerHp = healer.Health?.MaxValue ?? 0;
                var petHp = pet.Health?.MaxValue ?? 0;
                if (healerHp > 0 && petHp > 0)
                    petHealScale = Math.Clamp((float)petHp / healerHp, 1.0f, 8.0f);
            }
            Begin("combat.heal", CombatSession(healer, target)).Add("kind", "kit").AddCreature("atk.", healer).AddCreature("def.", target)
                .Add("kit", kit.Name).Add("kitWcid", kit.WeenieClassId).Add("vital", vital?.Vital ?? PropertyAttribute2nd.Undef)
                .Add("checkRule", "success=skillChance(effectiveSkill,difficulty)>roll; effectiveSkill=round((healing+kitBoost)*trainedMod); difficulty=round(missing*2*combatMod)")
                .Add("healingSkill", healingSkill.Current).Add("kitBoost", kit.BoostValue).Add("trainedMod", trainedMod).Add("combatMod", combatMod)
                .Add("effectiveSkill", effectiveSkill).Add("difficulty", difficulty).Add("success", success)
                .Add("healRule", "amount=roll[healBase*0.2,healBase*0.5]*healingRatingMod*motelScale, x2 on crit (10%), capped at missing; healBase=healing*healkitMod; stamina=round(unscaled/5)")
                .Add("healkitMod", kit.HealkitMod ?? 1.0).Add("healBase", healBase).Add("healMin", healBase * 0.2f).Add("healMax", healBase * 0.5f)
                .Add("healingRatingMod", target.GetHealingRatingMod()).Add("motelScale", petHealScale).Add("crit", critical)
                .Add("staminaCost", staminaCost).Add("amount", healAmount).Add("before", before).Add("after", vital?.Current ?? 0)
                .Add("usesLeft", kit.UsesLeft ?? 0).Emit();
        }

        /// <summary>A creature's death: the killer and the damage history's top contributors.</summary>
        public static void CombatDeath(Creature victim, DamageHistoryInfo lastDamager, DamageType damageType, bool crit)
        {
            if (victim == null)
                return;
            var killer = lastDamager?.TryGetAttacker();
            var r = Begin("combat.death", CombatSession(victim, killer)).AddCreature("def.", victim)
                .Add("dmgType", damageType).Add("crit", crit).Add("maxHp", victim.Health?.MaxValue ?? 0);
            if (lastDamager != null)
                r.Add("killer", lastDamager.Name).AddGuid("killerGuid", lastDamager.Guid.Full).Add("killerKind", Kind(killer)).Add("killerIsPlayer", lastDamager.IsPlayer);
            else
                r.Add("killer", "none");
            if (killer is CombatPet petKiller && petKiller.P_PetOwner != null)
                r.Add("killerOwner", petKiller.P_PetOwner.Name);

            var history = victim.DamageHistory;
            if (history != null)
            {
                var total = history.TotalHealth;
                r.Add("historyTotal", total);
                var contributors = new List<DamageHistoryInfo>(history.TotalDamage.Values);
                contributors.Sort((x, y) => y.TotalDamage.CompareTo(x.TotalDamage));
                var n = Math.Min(contributors.Count, 5);
                for (var i = 0; i < n; i++)
                {
                    var c = contributors[i];
                    var k = "c" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".";
                    r.Add(k + "name", c.Name).AddGuid(k + "guid", c.Guid.Full).Add(k + "dmg", c.TotalDamage).Add(k + "share", total > 0 ? c.TotalDamage / total : 0.0f);
                }
                if (contributors.Count > n)
                    r.Add("more", contributors.Count - n);
            }
            r.Emit();
        }
    }
}
