using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using System;
using static Google.Protobuf.Compiler.CodeGeneratorResponse.Types;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public double? ResistSlash
        {
            get => GetProperty(PropertyFloat.ResistSlash);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistSlash); else SetProperty(PropertyFloat.ResistSlash, value.Value); }
        }

        public double? ResistPierce
        {
            get => GetProperty(PropertyFloat.ResistPierce);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistPierce); else SetProperty(PropertyFloat.ResistPierce, value.Value); }
        }

        public double? ResistBludgeon
        {
            get => GetProperty(PropertyFloat.ResistBludgeon);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistBludgeon); else SetProperty(PropertyFloat.ResistBludgeon, value.Value); }
        }

        public double? ResistFire
        {
            get => GetProperty(PropertyFloat.ResistFire);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistFire); else SetProperty(PropertyFloat.ResistFire, value.Value); }
        }

        public double? ResistCold
        {
            get => GetProperty(PropertyFloat.ResistCold);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistCold); else SetProperty(PropertyFloat.ResistCold, value.Value); }
        }

        public double? ResistAcid
        {
            get => GetProperty(PropertyFloat.ResistAcid);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistAcid); else SetProperty(PropertyFloat.ResistAcid, value.Value); }
        }

        public double? ResistElectric
        {
            get => GetProperty(PropertyFloat.ResistElectric);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistElectric); else SetProperty(PropertyFloat.ResistElectric, value.Value); }
        }

        public double? ResistHealthDrain
        {
            get => GetProperty(PropertyFloat.ResistHealthDrain);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistHealthDrain); else SetProperty(PropertyFloat.ResistHealthDrain, value.Value); }
        }

        public double? ResistHealthBoost
        {
            get => GetProperty(PropertyFloat.ResistHealthBoost);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistHealthBoost); else SetProperty(PropertyFloat.ResistHealthBoost, value.Value); }
        }

        public double? ResistStaminaDrain
        {
            get => GetProperty(PropertyFloat.ResistStaminaDrain);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistStaminaDrain); else SetProperty(PropertyFloat.ResistStaminaDrain, value.Value); }
        }

        public double? ResistStaminaBoost
        {
            get => GetProperty(PropertyFloat.ResistStaminaBoost);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistStaminaBoost); else SetProperty(PropertyFloat.ResistStaminaBoost, value.Value); }
        }

        public double? ResistManaDrain
        {
            get => GetProperty(PropertyFloat.ResistManaDrain);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistManaDrain); else SetProperty(PropertyFloat.ResistManaDrain, value.Value); }
        }

        public double? ResistManaBoost
        {
            get => GetProperty(PropertyFloat.ResistManaBoost);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistManaBoost); else SetProperty(PropertyFloat.ResistManaBoost, value.Value); }
        }

        public double? ResistNether
        {
            get => GetProperty(PropertyFloat.ResistNether);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistNether); else SetProperty(PropertyFloat.ResistNether, value.Value); }
        }

        public bool NonProjectileMagicImmune
        {
            get => GetProperty(PropertyBool.NonProjectileMagicImmune) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.NonProjectileMagicImmune); else SetProperty(PropertyBool.NonProjectileMagicImmune, value); }
        }

        public virtual float GetResistanceMod(DamageType damageType, WorldObject attacker, WorldObject weapon, float weaponResistanceMod = 1.0f)
        {
            var ignoreMagicResist = (weapon?.IgnoreMagicResist ?? false) || (attacker?.IgnoreMagicResist ?? false);

            // hollow weapons also ignore player natural resistances
            if (ignoreMagicResist)
            {
                if (!(attacker is Player) || !(this is Player) || ServerConfig.ignore_magic_resist_pvp_scalar.Value == 1.0)
                    return weaponResistanceMod;
            }

            // TODO(Ruggan): When rolled out, use the new curve exclusively.
            float newCurvePct = (float)Math.Clamp(GetProperty(PropertyFloat.LifeAugNewCurveAmount) ?? ServerConfig.new_life_aug_curve_pct.Value, 0.0, 1.0);
            float protModOld = EnchantmentManager.GetProtectionResistanceMod(damageType);
            float protModNew = EnchantmentManager.GetProtectionResistanceModNew(damageType);
            float protMod = protModOld + ((protModNew - protModOld) * newCurvePct);

            var vulnMod = EnchantmentManager.GetVulnerabilityResistanceMod(damageType);

            // Zone Scaler: resolve the winning zone profile for this monster once (null for players/exempt/non-endgame/
            // no-match). Consumers below prefer a profile-defined stat over the global v11_* knob. Global profile is
            // seeded DISABLED, so with no authored scope this is null and behavior is unchanged.
            var zoneProfile = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(this as Creature);

            // v11+ monster vuln-defense: compress the vuln (Imperil) multiplier so stacked vulns can't produce
            // absurd damage against endgame mobs. Only the vuln enchantment bonus is touched here (before the
            // weaponResistanceMod max below), so base damage, offensive augs, and weapon rending are unaffected.
            // Player defenders never hit this: Player overrides GetResistanceMod. Gate is the monster's instance
            // Variation (same convention as the v11+ percent-HP offense system) -> auto-applies to all v11+ mobs.
            if (vulnMod > 1.0f && ServerConfig.v11_vuln_enabled.Value && !(this is Player)
                && ((zoneProfile != null && zoneProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.VulnCap))
                    || (ACE.Server.Managers.PrestigeManager.SystemsEnabled
                        && ACE.Server.Managers.VariationManager.GetEffectiveEndgameVariation(this) >= ServerConfig.v11_vuln_min_variation.Value)))
            {
                var vulnEff = GetProperty(PropertyFloat.VulnEffectivenessOverride) ?? ServerConfig.v11_vuln_effectiveness.Value;
                var vulnCap = GetProperty(PropertyFloat.VulnCapOverride) ?? ServerConfig.v11_vuln_cap.Value;
                if (zoneProfile != null && zoneProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.VulnCap))
                    vulnCap = zoneProfile.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.VulnCap);

                // diminishing curve: only a fraction of the vuln bonus lands, then clamp to the cap
                vulnMod = 1.0f + (vulnMod - 1.0f) * (float)vulnEff;
                if (vulnMod > vulnCap)
                    vulnMod = (float)vulnCap;
                if (vulnMod < 1.0f)
                    vulnMod = 1.0f;
            }

            var naturalResistMod = GetNaturalResistance(damageType);

            // protection mod becomes either life protection or natural resistance,
            // whichever is more powerful (more powerful = lower value here)
            if (protMod > naturalResistMod)
                protMod = naturalResistMod;

            // does this stack with natural resistance?
            if (this is Player player)
            {
                var resistAug = player.GetAugmentationResistance(damageType);
                if (resistAug > 0)
                {
                    var augFactor = Math.Min(1.0f, resistAug * 0.1f);
                    protMod *= 1.0f - augFactor;
                }
            }

            // vulnerability mod becomes either life vuln or weapon resistance mod,
            // whichever is more powerful
            if (vulnMod < weaponResistanceMod)
                vulnMod = weaponResistanceMod;

            if (ignoreMagicResist)
            {
                // convert to additive space
                var addProt = -ModToRating(protMod);
                var addVuln = ModToRating(vulnMod);

                // scale
                addProt = IgnoreMagicResistScaled(addProt);
                addVuln = IgnoreMagicResistScaled(addVuln);

                protMod = GetNegativeRatingMod(addProt);
                vulnMod = GetPositiveRatingMod(addVuln);
            }

            var resistMod = protMod * vulnMod;

            // v11+ monster damage-taken mitigation: scale ALL incoming damage against endgame mobs by a flat factor so they are
            // hard to kill via mitigation (not evasion). Applied after armor/resist, so it is rending-proof. Bosses (IsEmpowerSource)
            // take even less. Player defenders never hit this (Player overrides GetResistanceMod). Prestige-gated -> dormant while
            // prestige is off. The zone damage_taken_mult stat that used to override this was REMOVED 2026-08-03 (owner):
            // redundant with damage_resist_rating.
            if (ServerConfig.v11_mob_dmg_taken_enabled.Value && !(this is Player)
                && ACE.Server.Managers.PrestigeManager.SystemsEnabled
                && ACE.Server.Managers.VariationManager.GetEffectiveEndgameVariation(this) >= ServerConfig.v11_mob_dmg_taken_min_variation.Value)
            {
                var dmgMult = GetProperty(PropertyFloat.MobDmgTakenOverride) ?? ServerConfig.v11_mob_dmg_taken_mult.Value;

                if (GetProperty(PropertyBool.IsEmpowerSource) == true)
                    dmgMult *= ServerConfig.v11_mob_dmg_taken_boss_mult.Value;

                dmgMult = Math.Clamp(dmgMult, ServerConfig.v11_mob_dmg_taken_floor.Value, 1.0);

                resistMod *= (float)dmgMult;
            }

            // Per-monster ELEMENTAL WEAKNESS: if the incoming damage type is in the mob's weakness mask, multiply the
            // (post-mitigation) resistance mod so the right element does proportionally more. Independent of zone
            // scaling; a relative reward that stays meaningful even against heavily-mitigated endgame mobs. Unset
            // mask => no-op, so this changes nothing for existing mobs. Players never carry it.
            if (!(this is Player))
            {
                var weakMask = GetProperty(PropertyInt.ElementalWeaknessMask);
                if (weakMask.HasValue && weakMask.Value != 0 && ((DamageType)weakMask.Value & damageType) != 0)
                {
                    var weakFactor = GetProperty(PropertyFloat.ElementalWeaknessFactor) ?? ServerConfig.elemental_weakness_default_factor.Value;
                    if (weakFactor > 0)
                        resistMod *= (float)weakFactor;
                }
            }

            return resistMod;
        }

        public virtual float GetNaturalResistance(DamageType damageType)
        {
            // overridden for players
            return 1.0f;
        }

        /// <summary>Zone Control: a governed monster's zone profile can REPLACE a creature-level multiplier
        /// (resists / armor-vs-type). Returns the fallback when unprofiled — players and exempt mobs always
        /// resolve null, so this is a no-op for them.</summary>
        private double ZoneStatOr(string statKey, double fallback)
        {
            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(this as Creature);
            return zp != null && zp.Has(statKey) ? zp.Get(statKey) : fallback;
        }

        public double GetArmorVsType(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Slash:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ArmorVsSlash, GetProperty(PropertyFloat.ArmorModVsSlash) ?? 1.0f);
                case DamageType.Pierce:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ArmorVsPierce, GetProperty(PropertyFloat.ArmorModVsPierce) ?? 1.0f);
                case DamageType.Bludgeon:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ArmorVsBludgeon, GetProperty(PropertyFloat.ArmorModVsBludgeon) ?? 1.0f);
                case DamageType.Fire:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ArmorVsFire, GetProperty(PropertyFloat.ArmorModVsFire) ?? 1.0f);
                case DamageType.Cold:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ArmorVsCold, GetProperty(PropertyFloat.ArmorModVsCold) ?? 1.0f);
                case DamageType.Acid:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ArmorVsAcid, GetProperty(PropertyFloat.ArmorModVsAcid) ?? 1.0f);
                case DamageType.Electric:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ArmorVsElectric, GetProperty(PropertyFloat.ArmorModVsElectric) ?? 1.0f);
                case DamageType.Nether:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ArmorVsNether, GetProperty(PropertyFloat.ArmorModVsNether) ?? 1.0f);
                default:
                    return 1.0f;
            }
        }

        public double GetResistanceMod(ResistanceType resistance, WorldObject attacker = null, WorldObject weapon = null, float weaponResistanceMod = 1.0f)
        {
            switch (resistance)
            {
                case ResistanceType.Slash:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ResistSlash, ResistSlash ?? 1.0) * GetResistanceMod(DamageType.Slash, attacker, weapon, weaponResistanceMod);
                case ResistanceType.Pierce:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ResistPierce, ResistPierce ?? 1.0) * GetResistanceMod(DamageType.Pierce, attacker, weapon, weaponResistanceMod);
                case ResistanceType.Bludgeon:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ResistBludgeon, ResistBludgeon ?? 1.0) * GetResistanceMod(DamageType.Bludgeon, attacker, weapon, weaponResistanceMod);
                case ResistanceType.Fire:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ResistFire, ResistFire ?? 1.0) * GetResistanceMod(DamageType.Fire, attacker, weapon, weaponResistanceMod);
                case ResistanceType.Cold:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ResistCold, ResistCold ?? 1.0) * GetResistanceMod(DamageType.Cold, attacker, weapon, weaponResistanceMod);
                case ResistanceType.Acid:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ResistAcid, ResistAcid ?? 1.0) * GetResistanceMod(DamageType.Acid, attacker, weapon, weaponResistanceMod);
                case ResistanceType.Electric:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ResistElectric, ResistElectric ?? 1.0) * GetResistanceMod(DamageType.Electric, attacker, weapon, weaponResistanceMod);
                case ResistanceType.Nether:
                    return ZoneStatOr(ACE.Server.Managers.ZoneScaling.ZoneStat.ResistNether, ResistNether ?? 1.0) * GetResistanceMod(DamageType.Nether, attacker, weapon, weaponResistanceMod) * GetNetherResistRatingMod();
                case ResistanceType.HealthBoost:
                    return (ResistHealthBoost ?? 1.0) * GetHealingRatingMod();
                case ResistanceType.HealthDrain:
                    return (ResistHealthDrain ?? 1.0) * GetNaturalResistance(DamageType.Health) * GetLifeResistRatingMod();
                case ResistanceType.StaminaBoost:
                    return (ResistStaminaBoost ?? 1.0) * GetHealingRatingMod();     // does healing rating affect these?
                case ResistanceType.StaminaDrain:
                    return (ResistStaminaDrain ?? 1.0) * GetNaturalResistance(DamageType.Stamina);
                case ResistanceType.ManaBoost:
                    return (ResistManaBoost ?? 1.0) * GetHealingRatingMod();
                case ResistanceType.ManaDrain:
                    return (ResistManaDrain ?? 1.0) * GetNaturalResistance(DamageType.Mana);
                default:
                    return 1.0;
            }
        }

        public double? HealthRate
        {
            get => GetProperty(PropertyFloat.HealthRate);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.HealthRate); else SetProperty(PropertyFloat.HealthRate, value.Value); }
        }

        public double? StaminaRate
        {
            get => GetProperty(PropertyFloat.StaminaRate);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.StaminaRate); else SetProperty(PropertyFloat.StaminaRate, value.Value); }
        }

        public double ResistSlashMod => (ResistSlash ?? 1.0) * EnchantmentManager.GetResistanceMod(DamageType.Slash);
        public double ResistPierceMod => (ResistPierce ?? 1.0) * EnchantmentManager.GetResistanceMod(DamageType.Pierce);
        public double ResistBludgeonMod => (ResistBludgeon ?? 1.0) * EnchantmentManager.GetResistanceMod(DamageType.Bludgeon);
        public double ResistFireMod => (ResistFire ?? 1.0) * EnchantmentManager.GetResistanceMod(DamageType.Fire);
        public double ResistColdMod => (ResistCold ?? 1.0) * EnchantmentManager.GetResistanceMod(DamageType.Cold);
        public double ResistAcidMod => (ResistAcid ?? 1.0) * EnchantmentManager.GetResistanceMod(DamageType.Acid);
        public double ResistElectricMod => (ResistElectric ?? 1.0) * EnchantmentManager.GetResistanceMod(DamageType.Electric);
        public double ResistNetherMod => (ResistNether ?? 1.0) * EnchantmentManager.GetResistanceMod(DamageType.Nether) * GetNetherResistRatingMod();

        public bool NoCorpse
        {
            get => GetProperty(PropertyBool.NoCorpse) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.NoCorpse); else SetProperty(PropertyBool.NoCorpse, value); }
        }

        public bool TreasureCorpse
        {
            get => GetProperty(PropertyBool.TreasureCorpse) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.TreasureCorpse); else SetProperty(PropertyBool.TreasureCorpse, value); }
        }

        public uint? DeathTreasureType
        {
            get => GetProperty(PropertyDataId.DeathTreasureType);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.DeathTreasureType); else SetProperty(PropertyDataId.DeathTreasureType, value.Value); }
        }

        public int? LuminanceAward
        {
            get => GetProperty(PropertyInt.LuminanceAward);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.LuminanceAward); else SetProperty(PropertyInt.LuminanceAward, value.Value); }
        }

        public bool AiImmobile
        {
            get => GetProperty(PropertyBool.AiImmobile) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.AiImmobile); else SetProperty(PropertyBool.AiImmobile, value); }
        }

        public int? Overpower
        {
            get => GetProperty(PropertyInt.Overpower);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.Overpower); else SetProperty(PropertyInt.Overpower, value.Value); }
        }

        public int? OverpowerResist
        {
            get => GetProperty(PropertyInt.OverpowerResist);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.OverpowerResist); else SetProperty(PropertyInt.OverpowerResist, value.Value); }
        }

        public string KillQuest
        {
            get => GetProperty(PropertyString.KillQuest);
            set { if (value == null) RemoveProperty(PropertyString.KillQuest); else SetProperty(PropertyString.KillQuest, value); }
        }

        public string KillQuest2
        {
            get => GetProperty(PropertyString.KillQuest2);
            set { if (value == null) RemoveProperty(PropertyString.KillQuest2); else SetProperty(PropertyString.KillQuest2, value); }
        }

        public string KillQuest3
        {
            get => GetProperty(PropertyString.KillQuest3);
            set { if (value == null) RemoveProperty(PropertyString.KillQuest3); else SetProperty(PropertyString.KillQuest3, value); }
        }

        public FactionBits? Faction1Bits
        {
            get => (FactionBits?)GetProperty(PropertyInt.Faction1Bits);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.Faction1Bits); else SetProperty(PropertyInt.Faction1Bits, (int)value); }
        }

        public int? Faction2Bits
        {
            get => GetProperty(PropertyInt.Faction2Bits);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.Faction2Bits); else SetProperty(PropertyInt.Faction2Bits, value.Value); }
        }

        public int? Faction3Bits
        {
            get => GetProperty(PropertyInt.Faction3Bits);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.Faction3Bits); else SetProperty(PropertyInt.Faction3Bits, value.Value); }
        }

        public int? Hatred1Bits
        {
            get => GetProperty(PropertyInt.Hatred1Bits);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.Hatred1Bits); else SetProperty(PropertyInt.Hatred1Bits, value.Value); }
        }

        public int? Hatred2Bits
        {
            get => GetProperty(PropertyInt.Hatred2Bits);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.Hatred2Bits); else SetProperty(PropertyInt.Hatred2Bits, value.Value); }
        }

        public int? Hatred3Bits
        {
            get => GetProperty(PropertyInt.Hatred3Bits);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.Hatred3Bits); else SetProperty(PropertyInt.Hatred3Bits, value.Value); }
        }

        public int? SocietyRankCelhan
        {
            get => GetProperty(PropertyInt.SocietyRankCelhan);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.SocietyRankCelhan); else SetProperty(PropertyInt.SocietyRankCelhan, value.Value); }
        }

        public int? SocietyRankEldweb
        {
            get => GetProperty(PropertyInt.SocietyRankEldweb);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.SocietyRankEldweb); else SetProperty(PropertyInt.SocietyRankEldweb, value.Value); }
        }

        public int? SocietyRankRadblo
        {
            get => GetProperty(PropertyInt.SocietyRankRadblo);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.SocietyRankRadblo); else SetProperty(PropertyInt.SocietyRankRadblo, value.Value); }
        }

        public long? LuminanceAugmentCreatureCount
        {
            get => GetProperty(PropertyInt64.LumAugCreatureCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugCreatureCount); else SetProperty(PropertyInt64.LumAugCreatureCount, value.Value); }
        }

        public long? LuminanceAugmentItemCount
        {
            get => GetProperty(PropertyInt64.LumAugItemCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugItemCount); else SetProperty(PropertyInt64.LumAugItemCount, value.Value); }
        }

        public long? LuminanceAugmentLifeCount
        {
            get => GetProperty(PropertyInt64.LumAugLifeCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugLifeCount); else SetProperty(PropertyInt64.LumAugLifeCount, value.Value); }
        }

        public long? TriuneWeaveCount
        {
            get => GetProperty(PropertyInt64.TriuneWeaveCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.TriuneWeaveCount); else SetProperty(PropertyInt64.TriuneWeaveCount, value.Value); }
        }

        // ── Effective augmentation counts: purchased gems PLUS the growth charms ────────────────
        //
        // Passing the 6,000 and 4,000 caps is the POINT of the charms - the caps bound what
        // luminance can buy, and a charm is a separate, uncapped path bought with Prestige Coins.
        //
        // Use these at bonus-application sites ONLY. Gem purchases, the caps themselves, portal
        // gates, aggro totals and the raw /aug lines all read the underlying LumAug counts, so a
        // charm can never eat a player's purchase headroom or unlock content gated on bought augs.
        //
        // The Triune Weave feeds three schools at once; each school charm feeds only its own.
        public long EffectiveCreatureAugCount => (LuminanceAugmentCreatureCount ?? 0) + (TriuneWeaveCount ?? 0);

        public long EffectiveItemAugCount => (LuminanceAugmentItemCount ?? 0) + (TriuneWeaveCount ?? 0);

        public long EffectiveLifeAugCount => (LuminanceAugmentLifeCount ?? 0) + (TriuneWeaveCount ?? 0);

        public long EffectiveWarAugCount =>
            (LuminanceAugmentWarCount ?? 0) + (GetProperty(PropertyInt64.BattlemagesWrathCharmCount) ?? 0);

        public long EffectiveVoidAugCount =>
            (LuminanceAugmentVoidCount ?? 0) + (GetProperty(PropertyInt64.NetherVeilCharmCount) ?? 0);

        public long EffectiveMeleeAugCount =>
            (LuminanceAugmentMeleeCount ?? 0) + (GetProperty(PropertyInt64.CrashingSteelCharmCount) ?? 0);

        public long EffectiveMissileAugCount =>
            (LuminanceAugmentMissileCount ?? 0) + (GetProperty(PropertyInt64.TrueShotCharmCount) ?? 0);

        public long? LuminanceAugmentVoidCount
        {
            get => GetProperty(PropertyInt64.LumAugVoidCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugVoidCount); else SetProperty(PropertyInt64.LumAugVoidCount, value.Value); }
        }

        public long? LuminanceAugmentWarCount
        {
            get => GetProperty(PropertyInt64.LumAugWarCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugWarCount); else SetProperty(PropertyInt64.LumAugWarCount, value.Value); }
        }

        public long? LuminanceAugmentMeleeCount
        {
            get => GetProperty(PropertyInt64.LumAugMeleeCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugMeleeCount); else SetProperty(PropertyInt64.LumAugMeleeCount, value.Value); }
        }

        public long? LuminanceAugmentMissileCount
        {
            get => GetProperty(PropertyInt64.LumAugMissileCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugMissileCount); else SetProperty(PropertyInt64.LumAugMissileCount, value.Value); }
        }

        public long? LuminanceAugmentSummonCount
        {
            get => GetProperty(PropertyInt64.LumAugSummonCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugSummonCount); else SetProperty(PropertyInt64.LumAugSummonCount, value.Value); }
        }

        public long? LuminanceAugmentMeleeDefenseCount
        {
            get => GetProperty(PropertyInt64.LumAugMeleeDefenseCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugMeleeDefenseCount); else SetProperty(PropertyInt64.LumAugMeleeDefenseCount, value.Value); }
        }

        public long? LuminanceAugmentMissileDefenseCount
        {
            get => GetProperty(PropertyInt64.LumAugMissileDefenseCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugMissileDefenseCount); else SetProperty(PropertyInt64.LumAugMissileDefenseCount, value.Value); }
        }

        public long? LuminanceAugmentMagicDefenseCount
        {
            get => GetProperty(PropertyInt64.LumAugMagicDefenseCount) ?? 0;
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.LumAugMagicDefenseCount); else SetProperty(PropertyInt64.LumAugMagicDefenseCount, value.Value); }
        }

        public bool CanEnrage
        {
            get => GetProperty(PropertyBool.CanEnrage) ?? false; // Default to false
            set { if (!value) RemoveProperty(PropertyBool.CanEnrage); else SetProperty(PropertyBool.CanEnrage, value); }
        }

        public bool CanGrapple
        {
            get => GetProperty(PropertyBool.CanGrapple) ?? false; // Default to false
            set { if (!value) RemoveProperty(PropertyBool.CanGrapple); else SetProperty(PropertyBool.CanGrapple, value); }
        }

        public bool CanAOE
        {
            get => GetProperty(PropertyBool.CanAOE) ?? false; // Default to false
            set { if (!value) RemoveProperty(PropertyBool.CanAOE); else SetProperty(PropertyBool.CanAOE, value); }
        }

        public FactionBits Society => Faction1Bits ?? FactionBits.None;
    }
}
