using System;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// VOID LOCKOUTS (owner 2026-10-14), modelled on retail's war / void rule (Player_Magic.GetCastingPreCheckStatus: a war
    /// cast within 3-5 s of a void one fails, and the other way round):
    ///   - "If you cast a void spell, melee and missile attacks should be locked out for 10 seconds" - zc_void_weapon_lockout_seconds;
    ///   - "increase the lockout time for void to war also, 10 seconds, same as melee and bow for t11+" - zc_void_war_lockout_seconds.
    /// Zone Control areas only (owner: "Zone Control areas only"; the place the player stands when they attack / cast). One
    /// way: a weapon attack never locks void. The clock is the last VOID cast's own, so a life or creature spell cast in
    /// between does not end it (it does end retail's war / void window, which keys on the last school cast).
    /// A blocked attack is refused before it starts (no swing, no stamina) with a chat line; a blocked war cast fizzles.
    /// </summary>
    partial class Player
    {
        /// <summary>When this player last cast a void spell of their own (CreatePlayerSpell; procs never count). 0 = never.</summary>
        public double LastVoidCast_Time;

        private const double VoidLockoutMaxSeconds = 600;

        /// <summary>Seconds left of a void lockout this long, where the player stands now; 0 = none (off, over, or not in a
        /// Zone Control area).</summary>
        public double ZcVoidLockoutLeft(double seconds)
        {
            if (!(seconds > 0) || LastVoidCast_Time <= 0)
                return 0;
            var left = LastVoidCast_Time + Math.Min(seconds, VoidLockoutMaxSeconds) - Time.GetUnixTime();
            if (left <= 0)
                return 0;
            // only while a lockout runs: is this spot a Zone Control area (never a retail layer)?
            var loc = Location;
            if (loc == null || !ServerConfig.zonecontrol_enabled.Value)
                return 0;
            var variation = ZoneControlManager.GetEffectiveVariation(this);
            if (variation < VariationManager.EndgameMinVariation)
                return 0;
            return ZoneControlManager.ResolveWinnerForLocation(loc.LandblockId.Landblock, variation) != null ? left : 0;
        }

        /// <summary>Melee / missile attack start: true (and the attack refused, with the line) inside the void lockout.</summary>
        public bool ZcRefuseAttackAfterVoid()
        {
            var left = ZcVoidLockoutLeft(ServerConfig.zc_void_weapon_lockout_seconds.Value);
            if (left <= 0)
                return false;
            Session?.Network.EnqueueSend(new GameMessageSystemChat(VoidWeaponLockoutText(left), ChatMessageType.Combat));
            OnAttackDone();
            return true;
        }

        public static string VoidWeaponLockoutText(double left) =>
            $"The Nether energies permeating your blood stay your weapon hand. You can attack again in {Seconds(left)}.";

        public static string VoidWarLockoutText(double left) =>
            $"The Nether energies permeating your blood cause this War magic to fail. You can cast War magic again in {Seconds(left)}.";

        private static string Seconds(double left)
        {
            var s = (int)Math.Ceiling(left);
            return s == 1 ? "1 second" : $"{s} seconds";
        }
    }
}
