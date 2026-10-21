using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>Crits on this creature are compressed (<see cref="CritCompression"/>): a monster at variation 11 and up -
        /// never a player or a combat pet, which keep the retail Crit Damage Resist - while Zone Control, the endgame combat
        /// rules and zc_crit_compress are on. Same scope as ZcDebuffCompressed (variation, not an authored-zone lookup). Only a
        /// PLAYER's crit is compressed (ZcCritResistMod checks the attacker; owner 2026-10-21: pets keep today's crit).</summary>
        public bool ZcCritCompressed =>
            !(this is Player) && !(this is CombatPet)
            && ServerConfig.zonecontrol_enabled.Value && ServerConfig.zc_combat_rules_enabled.Value && ServerConfig.zc_crit_compress.Value
            && VariationManager.GetEffectiveEndgameVariation(this) >= VariationManager.EndgameMinVariation;

        /// <summary>The damage resist modifier for a crit on this creature: <paramref name="critResistMod"/> as before, or the
        /// compressed one (<see cref="CritCompression.ResistMod"/>). Every crit site that adds Crit Damage Resist calls this
        /// right after it (melee / missile, spell projectiles, ring casts).</summary>
        /// <param name="attacker">the creature that landed the crit - only a player's crit is compressed</param>
        /// <param name="raw">the crit's multiple of a max normal hit before the defender's ratings; 0 or less = unknown,
        /// left as before</param>
        public float ZcCritResistMod(WorldObject attacker, float raw, float normalResistMod, float critResistMod)
        {
            if (raw <= 0 || attacker is not Player || !ZcCritCompressed)
                return critResistMod;
            return CritCompression.ResistMod(raw, normalResistMod, critResistMod,
                ServerConfig.zc_crit_target_ratio.Value, ServerConfig.zc_crit_floor_ratio.Value);
        }
    }
}
