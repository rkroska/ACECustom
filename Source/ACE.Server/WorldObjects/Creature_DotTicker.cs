using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// DoT TICKER (owner 2026-10-04: "can we change the dot tick rate. Would prefer every second, not every 5"). Retail DoTs tick
    /// inside the creature's 5 s heartbeat. With dot_tick_seconds below 5, a T11+ Zone Control monster (ZcDebuffCompressed - never
    /// a player, a pet, or anything below variation 11; owner 2026-10-04: "gate it all to T11+") carrying a damage DoT gets its own
    /// ticker at that interval instead (EnchantmentManager.HeartBeat then skips the damage ticks for it), each tick scaled by
    /// interval / heartbeat so the damage per second is unchanged. Heals over time stay on the heartbeat. Everything else keeps the
    /// retail heartbeat tick.
    ///
    /// The heartbeat (re)starts the ticker whenever it skips a DoT, so a DoT that was never Added through BuildEntry still ticks:
    /// loaded with the creature, refreshed by a recast, or already on it when the setting changed. The ticker stops itself when
    /// the last DoT is gone, the creature dies or leaves the world, or it stops qualifying - the heartbeat takes the damage back.
    /// </summary>
    partial class Creature
    {
        private bool dotTickerRunning;
        private int dotTickerGeneration;
        private double dotTickerScheduledAt;
        private double dotEffectsNextAt;

        /// <summary>True when a DoT tick on this creature should play the hit sound and splatter: always on the retail
        /// heartbeat, and at most once per heartbeat interval while it is on the faster ticker (owner 2026-10-04: "can the sound
        /// only tick every 5 seconds like normal"). Touched only on this creature's own thread, like the ticker.</summary>
        public bool DotEffectsDue()
        {
            if (!UsesDotTicker)
                return true;
            var now = Time.GetUnixTime();
            if (now < dotEffectsNextAt)
                return false;
            var heartbeat = HeartbeatInterval ?? DefaultHeartbeatSeconds;
            dotEffectsNextAt = now + (heartbeat > 0 ? heartbeat : DefaultHeartbeatSeconds);
            return true;
        }

        /// <summary>The fastest tick allowed: every tick is a damage update to everyone nearby, so a tiny dot_tick_seconds
        /// would flood the network (review 2026-10-04).</summary>
        private const double MinDotTickSeconds = 0.5;

        /// <summary>The heartbeat interval the DoTs were tuned for (and the fallback when a creature has none).</summary>
        private const double DefaultHeartbeatSeconds = 5.0;

        public static bool DotTickerEnabled
        {
            get
            {
                var secs = ServerConfig.dot_tick_seconds.Value;
                return double.IsFinite(secs) && secs > 0 && secs < DefaultHeartbeatSeconds;
            }
        }

        /// <summary>dot_tick_seconds, never below MinDotTickSeconds (the heartbeat interval if it is not a finite number).</summary>
        public static double DotTickSeconds
        {
            get
            {
                var secs = ServerConfig.dot_tick_seconds.Value;
                return double.IsFinite(secs) ? Math.Max(MinDotTickSeconds, secs) : DefaultHeartbeatSeconds;
            }
        }

        /// <summary>This creature's damage DoTs run on the ticker, not the heartbeat.</summary>
        public bool UsesDotTicker => DotTickerEnabled && ZcDebuffCompressed;

        public static bool HasDamageDot(IEnumerable<PropertiesEnchantmentRegistry> top) =>
            top != null && top.Any(e => e.StatModKey == (int)PropertyInt.DamageOverTime || e.StatModKey == (int)PropertyInt.NetherOverTime);

        /// <summary>Start the ticker if it is not running - from ANY thread. A cast lands on the CASTER's thread
        /// (EnchantmentManager.BuildEntry), so the start is queued onto this creature's own action queue: the ticker's
        /// fields are only ever touched by this creature's landblock thread (review 2026-10-04: a cross-group cast raced the
        /// generation counter and could double-tick).</summary>
        public void EnsureDotTicker()
        {
            if (!UsesDotTicker) return;
            EnqueueAction(new ActionEventDelegate(ActionType.ControlFlowDelay, EnsureDotTickerOnOwnThread));
        }

        /// <summary>EnsureDotTicker for a caller already on this creature's landblock thread (its heartbeat).</summary>
        public void EnsureDotTickerOnOwnThread()
        {
            if (!UsesDotTicker) return;

            // a running ticker whose next tick is well overdue was dropped (an action queued while the creature was detached);
            // the generation bump makes any late arrival of the old chain a no-op, so a restart never double-ticks
            if (dotTickerRunning && Time.GetUnixTime() - dotTickerScheduledAt < DotTickSeconds + DefaultHeartbeatSeconds)
                return;

            dotTickerRunning = true;
            dotTickerGeneration++;
            ScheduleDotTick(dotTickerGeneration);
        }

        private void ScheduleDotTick(int generation)
        {
            dotTickerScheduledAt = Time.GetUnixTime();
            var chain = new ActionChain();
            chain.AddDelaySeconds(DotTickSeconds);
            chain.AddAction(this, ActionType.ControlFlowDelay, () => DotTick(generation));
            chain.EnqueueChain();
        }

        private void DotTick(int generation)
        {
            if (generation != dotTickerGeneration) return;

            if (!UsesDotTicker || IsDestroyed || IsDead || CurrentLandblock == null)
            {
                dotTickerRunning = false;
                return;
            }

            var top = PropertiesEnchantmentRegistryExtensions.GetEnchantmentsTopLayer(
                Biota.PropertiesEnchantmentRegistry, BiotaDatabaseLock, SpellSet.SetSpells);
            if (!HasDamageDot(top))
            {
                dotTickerRunning = false;
                return;
            }

            var heartbeat = HeartbeatInterval ?? DefaultHeartbeatSeconds;
            var scale = (float)(DotTickSeconds / (heartbeat > 0 ? heartbeat : DefaultHeartbeatSeconds));
            EnchantmentManager.HeartBeat_DamageOverTime(top, includeDamage: true, includeHeals: false, tickScale: scale);

            ScheduleDotTick(generation);
        }
    }
}
