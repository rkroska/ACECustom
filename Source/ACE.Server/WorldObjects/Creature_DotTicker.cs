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

        public static bool DotTickerEnabled
        {
            get
            {
                var secs = ServerConfig.dot_tick_seconds.Value;
                return secs > 0 && secs < 5.0;
            }
        }

        /// <summary>This creature's damage DoTs run on the ticker, not the heartbeat.</summary>
        public bool UsesDotTicker => DotTickerEnabled && ZcDebuffCompressed;

        public static bool HasDamageDot(IEnumerable<PropertiesEnchantmentRegistry> top) =>
            top != null && top.Any(e => e.StatModKey == (int)PropertyInt.DamageOverTime || e.StatModKey == (int)PropertyInt.NetherOverTime);

        public void EnsureDotTicker()
        {
            if (!UsesDotTicker) return;

            // a running ticker whose next tick is well overdue was dropped (an action queued while the creature was detached);
            // the generation bump makes any late arrival of the old chain a no-op, so a restart never double-ticks
            if (dotTickerRunning && Time.GetUnixTime() - dotTickerScheduledAt < ServerConfig.dot_tick_seconds.Value + 5.0)
                return;

            dotTickerRunning = true;
            dotTickerGeneration++;
            ScheduleDotTick(dotTickerGeneration);
        }

        private void ScheduleDotTick(int generation)
        {
            dotTickerScheduledAt = Time.GetUnixTime();
            var chain = new ActionChain();
            chain.AddDelaySeconds(ServerConfig.dot_tick_seconds.Value);
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

            var heartbeat = HeartbeatInterval ?? 5.0;
            var scale = (float)(ServerConfig.dot_tick_seconds.Value / (heartbeat > 0 ? heartbeat : 5.0));
            EnchantmentManager.HeartBeat_DamageOverTime(top, includeDamage: true, includeHeals: false, tickScale: scale);

            ScheduleDotTick(generation);
        }
    }
}
