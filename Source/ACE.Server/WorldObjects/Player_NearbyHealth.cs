using System;
using System.Collections.Generic;

using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// The player's side of nearby health updates (see <see cref="NearbyHealth"/>): which creatures this player is told
    /// about. Not every creature in view - the nearest nearby_health_max_creatures of them, chosen again once a second -
    /// so a crowd of players round a crowd of monsters stays a bounded amount of traffic.
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// The creatures this player gets health readings for. Null = none (not opted in, or not chosen yet). Published
        /// whole and never written in place: creatures read it from their own landblock threads.
        /// </summary>
        internal volatile HashSet<uint> NearbyHealthSet;

        private DateTime nearbyHealthChosenAt = DateTime.MinValue;

        /// <summary>From the player's tick: chooses the set again once a second while the player is opted in.</summary>
        internal void NearbyHealthTick()
        {
            // off for this player, or switched off on the server (review 2026-10-20: the server switch has to stop players
            // who already opted in too - they keep their opt-in for when it comes back on)
            if (!NearbyHealth.IsOn(this) || !ServerConfig.nearby_health_updates.Value)
            {
                if (NearbyHealthSet != null)
                    NearbyHealthSet = null;
                return;
            }
            ChooseNearbyHealthSetIfDue();
        }

        /// <summary>Chooses the set again when a second has passed since the last choice - from the tick, and from the
        /// command, so /nearbyhealth on / off spam never costs more than one choice a second (review 2026-10-20).</summary>
        internal void ChooseNearbyHealthSetIfDue()
        {
            var now = DateTime.UtcNow;
            if ((now - nearbyHealthChosenAt).TotalMilliseconds < 1000)
                return;
            nearbyHealthChosenAt = now;
            ChooseNearbyHealthSet();
        }

        /// <summary>
        /// Chooses the nearest attackable creatures in view (never players), up to the server's limit, and sends a first
        /// reading for every one that was not in the set before - so a monster that comes into range shows its health at
        /// once instead of at its next health change.
        /// </summary>
        internal void ChooseNearbyHealthSet()
        {
            var visible = PhysicsObj?.ObjMaint?.GetVisibleObjectsValuesOfTypeCreature();
            var here = Location;
            if (visible == null || here == null || Session == null)
                return;

            var candidates = new List<(uint Id, float Distance)>(visible.Count);
            var byId = new Dictionary<uint, Creature>(visible.Count);
            foreach (var creature in visible)
            {
                if (creature == null || creature is Player || creature.IsDead || !creature.Attackable)
                    continue;
                // never a creature the client is not told about (server-side Visibility: its health and guid would leak),
                // nor one without max health (QueryHealth would send NaN) - review 2026-10-20
                if (creature.Visibility || creature.Health.MaxValue == 0)
                    continue;
                var at = creature.Location;
                if (at == null)
                    continue;
                candidates.Add((creature.Guid.Full, here.DistanceTo(at)));
                byId[creature.Guid.Full] = creature;
            }

            var chosen = NearbyHealth.Nearest(candidates, NearbyHealth.MaxCreatures);
            var before = NearbyHealthSet;
            // published BEFORE the first readings go out: a creature that dies meanwhile on its own thread finds itself in
            // the set and its zero is sent (review 2026-10-20)
            NearbyHealthSet = new HashSet<uint>(chosen);
            foreach (var id in chosen)
            {
                if (before != null && before.Contains(id))
                    continue;
                if (byId.TryGetValue(id, out var creature))
                    creature.QueryHealth(Session);
            }
        }
    }
}
