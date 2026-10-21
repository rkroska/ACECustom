using System;
using System.Collections.Concurrent;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// DESTRUCTIVE CURSE DAMAGE CUT (owner 2026-10-13: "Destructive Curse now also makes enemies deal 50% less damage", then
    /// "a flat 25% damage reduction, that we can tune", "Works on everything. But we need a way to make certain enemies
    /// immune, similar to how we did agony and shrapnel").
    ///
    /// While a PLAYER's Destructive Curse (any level) is on a Zone Control monster, everything that monster deals to a player is
    /// multiplied by 1 - cut: swings with their True Damage, spells, harm, drain and its DoT ticks - every path that goes
    /// through ZoneControlManager.MonsterDamageMultFor. On or off: two Curses do not stack, the level does not matter.
    ///   - the cut: zone stat curse_damage_cut (zone / Tier Default / rank / wcid). Unset = 0.25. 0 = no cut. Never above 0.9.
    ///   - immune: zone stat immune_curse_cut = 1, or the weenie bool PropertyBool.ZcImmuneCurseCut (51200). The monster still
    ///     takes the Curse's own damage; the caster is told it is immune.
    ///   - switch: server property zc_curse_damage_cut. Retail monsters, players and pets are never touched.
    /// </summary>
    partial class Creature
    {
        public const double ZcCurseCutDefault = 0.25;
        public const double ZcCurseCutMax = 0.90;

        /// <summary>True when a Destructive Curse does not cut this creature's damage. Players are never "immune" - the cut
        /// is for monsters only.</summary>
        public bool IsImmuneToCurseCut()
        {
            if (this is Player || !ServerConfig.zonecontrol_enabled.Value)
                return false;
            if (GetProperty(PropertyBool.ZcImmuneCurseCut) == true)
                return true;
            var zone = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveCombatProfile(this);
            return zone != null && zone.Has(ZoneStat.ImmuneCurseCut) && zone.Get(ZoneStat.ImmuneCurseCut) >= 0.5;
        }

        /// <summary>The factor on what this monster deals to a player while a player's Destructive Curse is on it: 1 - cut,
        /// or 1.0 when there is none, the monster is immune, or the switch is off. <paramref name="profile"/> is the
        /// monster's own resolved combat profile (the caller has it already).</summary>
        public float GetZcCurseDamageDealtMod(EvaluatedProfile profile)
        {
            if (this is Player || profile == null || !ZcDebuffCompressed || !ServerConfig.zc_curse_damage_cut.Value)
                return 1.0f;

            var cut = ZcCurseCutDefault;
            if (profile.Has(ZoneStat.CurseDamageCut))
            {
                var v = profile.Get(ZoneStat.CurseDamageCut);
                if (double.IsFinite(v))
                    cut = v;
            }
            cut = Math.Clamp(cut, 0.0, ZcCurseCutMax);
            if (cut <= 0.0)
                return 1.0f;

            // only now the enchantment list: this runs on every monster hit on a player
            var cursed = false;
            foreach (var e in EnchantmentManager.GetEnchantments(SpellCategory.NetherDamageOverTimeRaising))
            {
                if (IsDestructiveCurseSpell((uint)e.SpellId) && ObjectGuid.IsPlayer(e.CasterObjectId))
                {
                    cursed = true;
                    break;
                }
            }
            if (!cursed)
                return 1.0f;

            if (GetProperty(PropertyBool.ZcImmuneCurseCut) == true)
                return 1.0f;
            if (profile.Has(ZoneStat.ImmuneCurseCut) && profile.Get(ZoneStat.ImmuneCurseCut) >= 0.5)
                return 1.0f;

            return (float)(1.0 - cut);
        }
    }

    partial class Player
    {
        // monster wcid -> when this player was last told its damage is not cut (a re-cast every 30 s, several monsters)
        private readonly ConcurrentDictionary<uint, DateTime> curseCutImmuneTold = new ConcurrentDictionary<uint, DateTime>();
        private static readonly TimeSpan CurseCutImmuneTellEvery = TimeSpan.FromSeconds(10);

        /// <summary>"Big Knuckles is immune to the weakening of Destructive Curse." - at most once per monster kind every 10 s.</summary>
        public void NotifyCurseCutImmune(Creature target)
        {
            if (target == null || Session == null) return;
            var now = DateTime.UtcNow;
            if (curseCutImmuneTold.TryGetValue(target.WeenieClassId, out var last) && now - last < CurseCutImmuneTellEvery)
                return;
            curseCutImmuneTold[target.WeenieClassId] = now;
            Session.Network.EnqueueSend(new GameMessageSystemChat($"{target.Name} is immune to the weakening of Destructive Curse.", ChatMessageType.Magic));
        }
    }
}
