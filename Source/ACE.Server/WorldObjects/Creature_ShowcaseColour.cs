using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Services;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// Showcase NPCs (e.g. the Ruggan's Annex Ward pets): seconds between colour changes. Unset = off.
        /// </summary>
        public double? ShowcaseColourCycleSeconds
        {
            get => GetProperty(PropertyFloat.ShowcaseColourCycleSeconds);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ShowcaseColourCycleSeconds); else SetProperty(PropertyFloat.ShowcaseColourCycleSeconds, value.Value); }
        }

        private double nextShowcaseColourTime;

        /// <summary>
        /// Called from the creature heartbeat (~5 s). Rolls a vibrant mutation palette - the same pool the
        /// Chromatic Catalyst uses - into PaletteTemplate and redraws for nearby players. Nothing is saved:
        /// a placed NPC's biota never persists, so the colour resets on respawn.
        /// </summary>
        private void ShowcaseColourHeartbeat(double currentUnixTime)
        {
            var interval = ShowcaseColourCycleSeconds;
            if (interval == null || interval.Value <= 0 || this is Player)
                return;

            if (nextShowcaseColourTime == 0)
            {
                // First tick after spawn: start at a random point in the cycle, so a room of these
                // never changes in unison.
                nextShowcaseColourTime = currentUnixTime + ThreadSafeRandom.Next(0.0f, (float)interval.Value);
                return;
            }

            if (currentUnixTime < nextShowcaseColourTime)
                return;

            nextShowcaseColourTime = currentUnixTime + interval.Value;

            // Nobody watching: skip the roll and the broadcast.
            if (PhysicsObj == null || !PlayersInRange())
                return;

            var pool = PetMutationService.GetVibrantPalettePool();
            if (pool == null || pool.Count == 0)
                return;

            var paletteId = pool[ThreadSafeRandom.Next(0, pool.Count - 1)].PaletteId; // Next(min, max) includes max
            if (paletteId == 0)
                return;

            // A 0x04 template is a full palette override; CalculateObjDesc applies it on both of its paths.
            PaletteTemplate = (int)paletteId;

            EnqueueBroadcast(new GameMessageObjDescEvent(this));
            EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.EnchantUpPurple));
        }
    }
}
