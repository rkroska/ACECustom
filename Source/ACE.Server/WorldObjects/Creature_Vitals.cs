using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public readonly Dictionary<PropertyAttribute2nd, CreatureVital> Vitals = new Dictionary<PropertyAttribute2nd, CreatureVital>();

        public CreatureVital Health => Vitals[PropertyAttribute2nd.MaxHealth];
        public CreatureVital Stamina => Vitals[PropertyAttribute2nd.MaxStamina];
        public CreatureVital Mana => Vitals[PropertyAttribute2nd.MaxMana];

        public virtual void SetMaxVitals()
        {
            var missingHealth = Health.Missing;

            Health.Current = Health.MaxValue;
            Stamina.Current = Stamina.MaxValue;
            Mana.Current = Mana.MaxValue;

            DamageHistory.OnHeal(missingHealth);
        }

        public CreatureVital GetCreatureVital(PropertyAttribute2nd vital)
        {
            switch (vital)
            {
                case PropertyAttribute2nd.Health:
                    return Health;
                case PropertyAttribute2nd.Stamina:
                    return Stamina;
                case PropertyAttribute2nd.Mana:
                    return Mana;
                default:
                    log.Error($"{Name}.GetCreatureVital({vital}): unexpected vital");
                    return null;
            }
        }

        /// <summary>
        /// Sets the current vital to a new value
        /// </summary>
        /// <returns>The actual change in the vital, after clamping between 0 and MaxVital</returns>
        public virtual int UpdateVital(CreatureVital vital, int newVal)
        {
            var before = vital.Current;
            vital.Current = (uint)Math.Clamp(newVal, 0, vital.MaxValue);
            var delta = (int)(vital.Current - before);

            // Keep the WoundedTaunt phase tracker fresh on any health gain (heal/regen) so banded
            // bosses re-arm correctly if healed above a threshold and then re-damaged through it.
            if (delta > 0 && vital == Health)
                EmoteManager?.OnHealthRaised();

            return delta;
        }

        public virtual int UpdateVital(CreatureVital vital, uint newVal)
        {
            return UpdateVital(vital, (int)newVal);
        }

        /// <summary>
        /// Updates a vital relative to current value
        /// </summary>
        public int UpdateVitalDelta(CreatureVital vital, int delta)
        {
            var newVital = (int)vital.Current + delta;

            return UpdateVital(vital, newVital);
        }

        public int UpdateVitalDelta(CreatureVital vital, uint delta)
        {
            return UpdateVitalDelta(vital, (int)delta);
        }

        /// <summary>
        /// Called every ~5 secs to regenerate vitals
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool VitalHeartBeat()
        {
            if (IsDead)
                return false;

            var vitalUpdate = false;

            vitalUpdate |= VitalHeartBeat(Health);

            vitalUpdate |= VitalHeartBeat(Stamina);

            vitalUpdate |= VitalHeartBeat(Mana);

            return vitalUpdate;
        }

        /// <summary>
        /// Updates a particular vital according to regeneration rate
        /// </summary>
        /// <param name="vital">The vital stat to update (health/stamina/mana)</param>
        /// <returns>TRUE if vital has changed</returns>
        public bool VitalHeartBeat(CreatureVital vital)
        {
            // Current and MaxValue are properties and include overhead in getting their values. We cache them so we only hit the overhead once.
            var vitalCurrent = vital.Current;
            var vitalMax = vital.MaxValue;

            if (vitalCurrent == vitalMax && vital.RegenRate > 0)
                return false;

            if (vitalCurrent > vitalMax)
            {
                UpdateVital(vital, vitalMax);
                return true;
            }

            if (vital.RegenRate == 0.0) return false;

            // take attributes into consideration (strength, endurance)
            var attributeMod = GetAttributeMod(vital);

            // take stance into consideration (combat, crouch, sitting, sleeping)
            var stanceMod = GetStanceMod(vital);

            // take enchantments into consideration:
            // (regeneration / rejuvenation / mana renewal / etc.)
            var enchantmentMod = EnchantmentManager.GetRegenerationMod(vital);

            var augMod = 1.0f;
            var zoneRegenMult = 1.0f;
            var zoneProdigalBlocked = false;
            var zcFlatRegenPct = 0;
            if (this is Player player)
            {
                if (player.AugmentationFasterRegen > 0)
                    augMod += player.AugmentationFasterRegen;

                // Zone Control Regen slot special (key 46, bracers; prop 50231): FLAT pct of the vital's MAX
                // added to every positive natural tick, MAX-wins across worn pieces (owner 2026-08-23: a
                // multiplier compounded the shard's x900 buff stack into god-mode; flat reads as "+300 on a
                // 1,700 tick" and can never compound). Applied after the tuner below (see zcFlatRegen).
                zcFlatRegenPct = player.GetZoneModifierMax(ACE.Server.Managers.ZoneControl.ZoneModifiers.RegenSpecialMult);

                // Zone Control Suppression: recompute the enchantment mod without the Prodigal regen line
                // (uncached path — the cached mod can't know about zone borders), and pick up the regen tuner.
                try
                {
                    var zfx = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveEffectsForPlayer(player);
                    if (zfx != null && zfx.EffectiveSuppressEnabled)
                    {
                        if (zfx.EffectiveSuppressProdigal)
                        {
                            enchantmentMod = EnchantmentManager.GetRegenerationMod(vital,
                                ACE.Server.Managers.ZoneControl.ZoneEffectManager.ProdigalRegenSpells);
                            zoneProdigalBlocked = true;
                        }
                        zoneRegenMult = (float)zfx.EffectiveSuppressRegenMult;
                    }
                }
                catch (Exception ex)
                {
                    // never let zone resolution break vital ticks - but do not lose the reason either
                    if (log.IsDebugEnabled)
                        log.Debug($"[ZoneControl] regen effect resolve failed for {player.Name}: {ex.Message}");
                }
            }

            // cap rate?
            var currentTick = vital.RegenRate * attributeMod * stanceMod * enchantmentMod * augMod;

            // Suppression regen tuner: shrink POSITIVE regen only (a degen tick is never softened).
            if (zoneRegenMult != 1.0f && currentTick > 0)
                currentTick *= zoneRegenMult;

            // Bracers Regeneration special: flat pct of max, only while the natural tick is regenerating
            if (zcFlatRegenPct > 0 && currentTick >= 0)
                currentTick += vitalMax * zcFlatRegenPct / 100.0f;

            if (ACE.Server.Managers.ServerConfig.regen_diag_verbose.Value && this is Player)
                log.Warn($"[RegenDiag] {Name} {vital.Vital}: {vitalCurrent}/{vitalMax} rate={vital.RegenRate} " +
                         $"attr={attributeMod:F3} stance={stanceMod:F3} ench={enchantmentMod:F3}" +
                         $"{(zoneProdigalBlocked ? " PRODIGAL-BLOCKED" : "")} aug={augMod:F3} bracersFlat={zcFlatRegenPct}pct " +
                         $"zoneRegenMult={zoneRegenMult:F2} tick={currentTick:F3}");

            // add in partially accumulated / rounded vitals from previous tick(s)
            var totalTick = currentTick + vital.PartialRegen;

            // accumulate partial vital rates between ticks
            var intTick = (int)totalTick;
            vital.PartialRegen = totalTick - intTick;

            if (intTick != 0)
            {
                //if (this is Player)
                    //Console.WriteLine($"VitalTick({vital.Vital.ToSentence()}): attributeMod={attributeMod}, stanceMod={stanceMod}, enchantmentMod={enchantmentMod}, regenRate={vital.RegenRate}, currentTick={currentTick}, totalTick={totalTick}, accumulated={vital.PartialRegen}");

                UpdateVitalDelta(vital, intTick);
                if (vital.Vital == PropertyAttribute2nd.MaxHealth)
                {
                    if (intTick > 0)
                        DamageHistory.OnHeal((uint)intTick);
                    else
                    {
                        DamageHistory.Add(this, DamageType.Health, (uint)Math.Abs(intTick));

                        if (Health.Current <= 0)
                        {
                            OnDeath(DamageHistory.LastDamager, DamageType.Health);
                            Die();
                        }
                    }

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the vital regeneration modifier based on attributes
        /// (strength, endurance for health, stamina)
        /// </summary>
        public float GetAttributeMod(CreatureVital vital)
        {
            // only applies to players
            if (!(this is Player)) return 1.0f;

            // only applies for health?
            if (vital.Vital != PropertyAttribute2nd.MaxHealth) return 1.0f;

            // The combination of strength and endurance (with endurance being more important) allows one to regenerate hit points 
            // at a faster rate the higher one's endurance is. This bonus is in addition to any regeneration spells one may have placed upon themselves.
            // This regeneration bonus caps at around 110%.

            var strength = (int)Strength.Base;
            var endurance = (int)Endurance.Base;

            var strAndEnd = strength + (endurance * 2);

            //var modifier = 1.0 + (0.0494 * Math.Pow(strAndEnd, 1.179) / 100.0f);    // formula deduced from values present in the client pdb
            //var attributeMod = Math.Clamp(modifier, 1.0, 2.1);      // cap between + 0-110%

            if (strAndEnd <= 200)
                return 1.0f;

            var modifier = 1.0f + (float)(strAndEnd - 200) / 600;
            var attributeMod = Math.Clamp(modifier, 1.0f, 2.1f);

            return attributeMod;
        }

        /// <summary>
        /// Returns the vital regeneration modifier based on player stance
        /// (combat, crouch, sitting, sleeping)
        /// </summary>
        public float GetStanceMod(CreatureVital vital)
        {
            // only applies to players
            if ((this as Player) == null) return 1.0f;

            // does not apply for mana?
            if (vital.Vital == PropertyAttribute2nd.MaxMana) return 1.0f;

            var forwardCommand = CurrentMovementData.MovementType == MovementType.Invalid && CurrentMovementData.Invalid != null ? CurrentMovementData.Invalid.State.ForwardCommand : MotionCommand.Invalid;

            // combat mode / running
            if (CombatMode != CombatMode.NonCombat || forwardCommand == MotionCommand.RunForward)
                return 0.5f;

            switch (forwardCommand)
            {
                // TODO: verify multipliers
                default:
                    return 1.0f;
                case MotionCommand.Crouch:
                    return 2.0f;
                case MotionCommand.Sitting:
                    return 2.5f;
                case MotionCommand.Sleeping:
                    return 3.0f;
            }
        }
    }
}
