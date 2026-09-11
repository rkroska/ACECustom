using System;

using ACE.Common.Extensions;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;

namespace ACE.Server.WorldObjects.Entity
{
    public class CreatureVital
    {
        private readonly Creature creature;

        public readonly PropertyAttribute2nd Vital;

        // the underlying database record
        private readonly PropertiesAttribute2nd propertiesAttribute2nd;

        /// <summary>
        /// If the creature's biota does not contain this vital, a new record will be created.
        /// </summary>
        public CreatureVital(Creature creature, PropertyAttribute2nd vital)
        {
            this.creature = creature;
            Vital = vital;

            if (!creature.Biota.PropertiesAttribute2nd.TryGetValue(vital, out propertiesAttribute2nd))
            {
                propertiesAttribute2nd = new PropertiesAttribute2nd();
                creature.Biota.PropertiesAttribute2nd[vital] = propertiesAttribute2nd;
            }

            switch (Vital)
            {
                case PropertyAttribute2nd.MaxHealth:
                    RegenRate = creature.GetProperty(PropertyFloat.HealthRate) ?? 0;
                    break;
                case PropertyAttribute2nd.MaxStamina:
                    RegenRate = creature.GetProperty(PropertyFloat.StaminaRate) ?? 0;
                    break;
                case PropertyAttribute2nd.MaxMana:
                    RegenRate = creature.GetProperty(PropertyFloat.ManaRate) ?? 0;
                    break;
            }
        }

        public uint StartingValue
        {
            get => propertiesAttribute2nd.InitLevel;
            set => propertiesAttribute2nd.InitLevel = value;
        }

        /// <summary>
        /// Total Experience Spent on this vital
        /// </summary>
        public uint ExperienceSpent
        {
            get => propertiesAttribute2nd.CPSpent;
            set => propertiesAttribute2nd.CPSpent = value;
        }

        /// <summary>
        /// Returns the amount of vital experience remaining
        /// until max rank is reached
        /// </summary>
        public uint ExperienceLeft
        {
            get
            {
                var vitalXPTable = DatManager.PortalDat.XpTable.VitalXpList;

                return vitalXPTable[vitalXPTable.Count - 1] - ExperienceSpent;
            }
        }

        /// <summary>
        /// The number of levels a vital has been raised,
        /// derived from ExperienceSpent
        /// </summary>
        public uint Ranks
        {
            get => propertiesAttribute2nd.LevelFromCP;
            set => propertiesAttribute2nd.LevelFromCP = value;
        }

        /// <summary>
        /// Returns TRUE if this vital has been raised the maximum # of times
        /// </summary>
        public bool IsMaxRank
        {
            get
            {
                var vitalXPTable = DatManager.PortalDat.XpTable.VitalXpList;

                return Ranks == (vitalXPTable.Count - 1);
            }
        }

        /// <summary>
        /// Returns the adjusted Value depending on the base attribute formula
        /// </summary>
        public uint Base
        {
            get
            {
                var total = StartingValue
                    + AttributeFormula.GetFormula(creature, Vital, /*current*/false)
                    + EnlBonus
                    + Ranks
                    + GearBonus;

                // Fortify Vitals deliberately NOT applied here: Base feeds raw-stat consumers
                // (Innate reporting, RawSecondaryAttrib wield gates) that must not see gear
                // multipliers. The player-facing max rides GetMaxValue below.
                return (uint)total;
            }
        }

        /// <summary>
        /// Zone Control Fortify Vitals slot special (key 41, helm): pct points scaling the whole vital
        /// total. MAX-wins across worn pieces (2026-08-21 armor v2) -- the highest single piece counts,
        /// a second piece never stacks, so no clamp is needed (the band caps the roll). Applied in
        /// GetMaxValue only (not the rating cache) so it covers all 3 vitals -- MaxHealth rides
        /// GetGearMaxHealth and a rating-cache route would silently exclude it. Application point
        /// (before vitae/additives) LEFT where it is -- owner ruling 08-21.
        /// </summary>
        private double FortifyVitalsMod
        {
            get
            {
                if (creature == null)
                    return 1.0;

                var pts = Math.Max(0, creature.GetZoneModifierMax(ACE.Server.Managers.ZoneControl.ZoneModifiers.FortifyVitalsPct));

                // Pct Max Health (key 47, chase, 2026-08-22): percentage points SUMMED across worn pieces,
                // MaxHealth only, additive with the Fortify Vitals points (owner: they stack).
                if (Vital == PropertyAttribute2nd.MaxHealth)
                    pts += Math.Max(0, creature.GetZoneModifierBonus(ACE.Server.Managers.ZoneControl.ZoneModifiers.PctMaxHealthPct));

                return 1.0 + pts / 100.0;
            }
        }

        public uint EnlBonus
        {
            get {
                if (creature is Player player && Vital == PropertyAttribute2nd.MaxHealth)
                    return (uint)player.Enlightenment * 2;
                return 0;
            }
        }

        public uint GearBonus
        {
            get
            {
                if (creature is not Player player)
                    return 0;

                switch (Vital)
                {
                    case PropertyAttribute2nd.MaxHealth:
                        // retail gear prop (also carries the Zone Control health cantrip)
                        return (uint)player.GetGearMaxHealth();
                    default:
                        return 0;
                }
            }
        }

        public uint Current
        {
            get => propertiesAttribute2nd.CurrentLevel;
            set => propertiesAttribute2nd.CurrentLevel = value;
        }

        public uint MaxValue => GetMaxValue(true);

        public uint GetMaxValue(bool enchanted)
        {
            uint total =
                StartingValue
                + AttributeFormula.GetFormula(creature, Vital, /*current*/true)
                + EnlBonus
                + Ranks
                + GearBonus;

            // apply multiplicative enchantments first
            var multiplier = enchanted ? creature.EnchantmentManager.GetVitalMod_Multiplier(this) : 1.0f;
            var fTotal = total * multiplier;

            // Zone Control Fortify Vitals cantrip (whole-total multiplier, all 3 vitals)
            fTotal = (float)(fTotal * FortifyVitalsMod);

            var player = creature as Player;
            if (player != null)
            {
                var vitae = player.Vitae;

                if (vitae != 1.0f)
                    fTotal *= vitae;
            }

            // everything beyond this point does not get scaled by vitae
            var additives = enchanted ? creature.EnchantmentManager.GetVitalMod_Additives(this) : 0;
            var iTotal = (fTotal + additives).Round();

            // a creature cannot fall below 5 MaxVital from enchantments / vitae normally,
            // or 1 MaxVital for creatures with very low starting vitals
            var minVital = total >= 5 ? 5 : 1; 

            iTotal = Math.Max(minVital, iTotal);

            return (uint)iTotal;
        }

        public uint Missing => MaxValue - Current;

        public float Percent => (float)Current / MaxValue;

        public ModifierType ModifierType
        {
            get
            {
                var diff = (int)GetMaxValue(true) - (int)GetMaxValue(false);

                if (diff > 0)
                    return ModifierType.Buffed;
                else if (diff < 0)
                    return ModifierType.Debuffed;
                else
                    return ModifierType.None;
            }
        }


        public double RegenRate { set; get; }

        /// <summary>
        /// For tracking partial regeneration between ticks
        /// </summary>
        public double PartialRegen { get; set; }

        public Vital ToEnum()
        {
            switch (Vital)
            {
                case PropertyAttribute2nd.MaxHealth:    return ACE.Entity.Enum.Vital.Health;
                case PropertyAttribute2nd.MaxStamina:   return ACE.Entity.Enum.Vital.Stamina;
                case PropertyAttribute2nd.MaxMana:      return ACE.Entity.Enum.Vital.Mana;
            }
            return ACE.Entity.Enum.Vital.Undefined;
        }
    }
}
