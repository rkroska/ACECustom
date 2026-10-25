using System;

using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// The creature's side of nearby health updates (see <see cref="NearbyHealth"/>): when its health changes, the players
    /// who know about it and have opted in get the reading too.
    /// </summary>
    partial class Creature
    {
        private DateTime nearbyHealthSentAt = DateTime.MinValue;
        // written by attackers on other landblock threads and by this creature's own tick (review 2026-10-20)
        private volatile bool nearbyHealthHeld;
        // set while a DoT tick is being worked out (its health reports come before its damage lands); a time, not a flag,
        // so that an exception mid-tick can never leave the readings held for good - it lapses after a second
        private DateTime nearbyHealthDotTickAt = DateTime.MinValue;

        private static DateTime nearbyHealthWarnedAt = DateTime.MinValue;

        /// <summary>A warning at most once a minute server-wide: this runs on the combat hot path.</summary>
        private void WarnNearbyHealth(string where, Exception ex)
        {
            var now = DateTime.UtcNow;
            if (now - nearbyHealthWarnedAt < TimeSpan.FromMinutes(1))
                return;
            nearbyHealthWarnedAt = now;
            log.Warn($"[NearbyHealth] {Name} ({Guid}) {where}: {ex}");
        }

        /// <summary>Called from OnHealthUpdate, after the players who have this creature selected were told. It runs
        /// inside the damage path before the death check, so nothing it does may throw into combat.</summary>
        private void NearbyHealthChanged()
        {
            try
            {
                if (!NearbyHealth.Any || this is Player || !ServerConfig.nearby_health_updates.Value)
                    return;
                // a creature the client is never told about (server-side Visibility), or one without max health (its
                // fraction would read as a death and skip the throttle): nothing to send (review 2026-10-20)
                if (Visibility || Health.MaxValue == 0)
                    return;
                // inside a DoT tick: this is the health from BEFORE the tick's damage - the report after it follows
                if (DateTime.UtcNow - nearbyHealthDotTickAt < TimeSpan.FromSeconds(1))
                    return;

                var now = DateTime.UtcNow;
                if (!NearbyHealth.SendNow((float)Health.Current / Health.MaxValue, nearbyHealthSentAt, now, NearbyHealth.MinGap))
                {
                    nearbyHealthHeld = true;   // the tick sends the latest value once the gap has passed
                    return;
                }
                // cleared BEFORE the health is read for the send, as in FlushNearbyHealth: a newer change that holds again
                // on another thread after this point is flushed later, never overwritten (review 2026-10-20)
                nearbyHealthHeld = false;
                SendNearbyHealth((float)Health.Current / Health.MaxValue, now);
            }
            catch (Exception ex)
            {
                WarnNearbyHealth("health change", ex);
            }
        }

        /// <summary>A DoT tick starts on this creature (EnchantmentManager.ApplyDamageTick): its health reports come before
        /// its damage, so they are held until <see cref="NearbyHealthAfterDamageOverTime"/>.</summary>
        internal void NearbyHealthDotTickBegin() => nearbyHealthDotTickAt = DateTime.UtcNow;

        /// <summary>A DoT tick's damage has landed: one health report now, with the new value - to the players who have this
        /// creature selected and (through OnHealthUpdate) to the nearby ones; a tick that kills sends its zero at once.
        /// Nothing when the tick changed nothing (review 2026-10-20).</summary>
        internal void NearbyHealthAfterDamageOverTime(bool changed)
        {
            nearbyHealthDotTickAt = DateTime.MinValue;
            if (!changed || this is Player)
                return;
            try
            {
                OnHealthUpdate();
            }
            catch (Exception ex)
            {
                WarnNearbyHealth("after a DoT tick", ex);
            }
        }

        /// <summary>Called from the creature's tick and heartbeat: a reading that was held back goes out when it may.</summary>
        private void FlushNearbyHealth()
        {
            if (!nearbyHealthHeld)
                return;
            if (!NearbyHealth.Any || !ServerConfig.nearby_health_updates.Value || Visibility || Health.MaxValue == 0)
            {
                nearbyHealthHeld = false;
                return;
            }
            var now = DateTime.UtcNow;
            if (now - nearbyHealthSentAt < NearbyHealth.MinGap)
                return;
            // cleared BEFORE the health is read: a newer change on another thread that holds again after this point is
            // flushed next time, never overwritten (review 2026-10-20)
            nearbyHealthHeld = false;
            try
            {
                SendNearbyHealth((float)Health.Current / Health.MaxValue, now);
            }
            catch (Exception ex)
            {
                WarnNearbyHealth("flush", ex);
            }
        }

        private void SendNearbyHealth(float fraction, DateTime now)
        {
            nearbyHealthSentAt = now;

            var known = PhysicsObj?.ObjMaint?.GetKnownPlayersValuesAsPlayer();
            if (known == null)
                return;

            foreach (var player in known)
            {
                if (player == null || !NearbyHealth.IsOn(player))
                    continue;
                // only the creatures that player is told about: their nearest few, chosen once a second
                var told = player.NearbyHealthSet;
                if (told == null || !told.Contains(Guid.Full))
                    continue;
                // a player who has this creature selected was told a moment ago by OnHealthUpdate
                if (selectedTargets.ContainsKey(player.Guid.Full))
                    continue;
                var session = player.Session;
                if (session == null)
                {
                    NearbyHealth.Remove(player);
                    continue;
                }
                session.Network.EnqueueSend(new GameEventUpdateHealth(session, Guid.Full, fraction));
            }
        }
    }
}
