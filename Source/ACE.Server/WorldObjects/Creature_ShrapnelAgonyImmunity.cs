using System;
using System.Collections.Concurrent;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// SHRAPNEL / AGONY IMMUNITY (owner 2026-10-03): Rocky Shrapnel and Ring of Unspeakable Agony - the two fast-casting
    /// bludgeon rings the Shrapnel / Agony charms give players - are far too strong, but players keep them. Instead a
    /// monster can be made immune: its damage from those two spells is 0. Two switches, either one is enough:
    ///   - the WEENIE / creature bool PropertyBool.ZcImmuneShrapnelAgony (50059) - any monster, anywhere in the world;
    ///   - the Zone Control stat immune_shrapnel_agony = 1 - zone-wide, Tier Default, per rank or per wcid (Bestiary Defense).
    /// Checked in both damage paths: Player.ApplyRingSpellAreaDamage (a player's rings, the normal path) and
    /// SpellProjectile.OnCollideObject (Classic-mode rings and anything else that fires these spells as projectiles).
    /// </summary>
    partial class Creature
    {
        public const uint SpellIdRockyShrapnel = 6152u;
        public const uint SpellIdRingOfUnspeakableAgony = 2673u;

        public static bool IsShrapnelOrAgony(uint spellId) => spellId == SpellIdRockyShrapnel || spellId == SpellIdRingOfUnspeakableAgony;

        /// <summary>True when this creature takes no damage from <paramref name="spellId"/> (Rocky Shrapnel / Ring of
        /// Unspeakable Agony only). Players are never immune - the switch is for monsters.</summary>
        public bool IsImmuneToShrapnelAgony(uint spellId)
        {
            if (!IsShrapnelOrAgony(spellId) || this is Player)
                return false;
            if (GetProperty(PropertyBool.ZcImmuneShrapnelAgony) == true)
                return true;
            var zone = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveCombatProfile(this);
            return zone != null && zone.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.ImmuneShrapnelAgony)
                && zone.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.ImmuneShrapnelAgony) >= 0.5;
        }
    }

    partial class Player
    {
        // monster wcid -> when this player was last told it is immune (a ring hits many targets several times a second).
        // Concurrent (review 2026-10-03): written from the player's ring path AND from a Classic-mode projectile, which can
        // run on another landblock group's thread.
        private readonly ConcurrentDictionary<uint, DateTime> shrapnelAgonyImmuneTold = new ConcurrentDictionary<uint, DateTime>();
        private static readonly TimeSpan ShrapnelAgonyImmuneTellEvery = TimeSpan.FromSeconds(10);

        /// <summary>"Mushy Marv is immune to Rocky Shrapnel." - at most once per monster kind every 10 s.</summary>
        public void NotifyShrapnelAgonyImmune(Creature target, string spellName)
        {
            if (target == null || Session == null) return;
            var now = DateTime.UtcNow;
            if (shrapnelAgonyImmuneTold.TryGetValue(target.WeenieClassId, out var last) && now - last < ShrapnelAgonyImmuneTellEvery)
                return;
            shrapnelAgonyImmuneTold[target.WeenieClassId] = now;
            Session.Network.EnqueueSend(new GameMessageSystemChat($"{target.Name} is immune to {spellName}.", ChatMessageType.Magic));
        }
    }
}
