using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Monsters the levelled charms leave alone (the Shrapnel / Agony and Curse-cut pattern): either switch is enough -
    ///   - per monster: the weenie bool (PropertyBool.ZcImmunePandemic 51205 / ZcImmuneChain 51206), any monster anywhere;
    ///   - per zone / Tier Default / rank / wcid: the zone stat immune_pandemic / immune_chain = 1.
    /// Immune means the charm never puts its effect ONTO the monster: Pandemic does not spread a DoT to it, Chain does not
    /// jump to it. A spell cast AT it works as always. Players and pets are never targets of either charm at all.
    /// </summary>
    partial class Creature
    {
        public bool IsImmuneToPandemic() => IsImmuneByZoneStat(PropertyBool.ZcImmunePandemic, ZoneStat.ImmunePandemic);

        public bool IsImmuneToChain() => IsImmuneByZoneStat(PropertyBool.ZcImmuneChain, ZoneStat.ImmuneChain);

        private bool IsImmuneByZoneStat(PropertyBool weenieBool, string zoneStat)
        {
            if (this is Player)
                return false;
            if (GetProperty(weenieBool) == true)
                return true;
            if (!ServerConfig.zonecontrol_enabled.Value)
                return false;
            var zone = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveCombatProfile(this);
            return zone != null && zone.Has(zoneStat) && zone.Get(zoneStat) >= 0.5;
        }
    }
}
