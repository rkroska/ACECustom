using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Character panel display of the T11+ gear lines (owner 2026-10-19) - see <see cref="ZcGearDisplay"/> for the why.
    /// The server's own numbers never read anything here: this only keeps the CLIENT's copy in step with them.
    /// </summary>
    partial class Player
    {
        /// <summary>What the client holds right now, by layer. Cleared when the client's enchantment list is rebuilt
        /// (login, a re-sent player description), so the next sync sends everything again.</summary>
        private readonly Dictionary<ushort, ZcGearDisplay.Entry> zcGearDisplaySent = new();

        /// <summary>
        /// Sends the client every display entry that differs from what it holds, and quietly removes the ones that went
        /// neutral. Cheap when nothing changed (a few cache reads, no packets), so it is safe on the heartbeat.
        /// <paramref name="clientListReset"/>: the client's enchantment list was just rebuilt, nothing we sent is there.
        /// Returns true when anything was sent.
        /// </summary>
        public bool SyncZcGearDisplay(bool clientListReset = false)
        {
            if (Session == null || !ZcGearDisplay.CheckSpellIdFree())
                return false;

            if (clientListReset)
                zcGearDisplaySent.Clear();

            var wanted = ZcGearDisplay.Compute(GetZoneModifierBonus, GetZoneModifierMax, ZoneModifierSkillPropIds())
                .ToDictionary(e => e.Layer);

            var changed = false;

            foreach (var entry in wanted.Values)
            {
                if (zcGearDisplaySent.TryGetValue(entry.Layer, out var sent) && sent == entry)
                    continue;
                Session.Network.EnqueueSend(new GameEventMagicUpdateEnchantment(Session, ZcGearDisplay.ToEnchantment(entry, Guid.Full)));
                zcGearDisplaySent[entry.Layer] = entry;
                changed = true;
            }

            foreach (var layer in zcGearDisplaySent.Keys.Where(l => !wanted.ContainsKey(l)).ToList())
            {
                // the silent remove (no "spell has expired" line in chat)
                Session.Network.EnqueueSend(new GameEventMagicDispelEnchantment(Session, ZcGearDisplay.SpellId, layer));
                zcGearDisplaySent.Remove(layer);
                changed = true;
            }

            // The client caps the CURRENT vitals it holds at the max it works out itself (CACQualities::BoundsCheck), so a
            // current value that arrived before a higher max was known reads low until the next vital update. Re-send all three
            // now that the client's max is right.
            if (changed)
                SendCurrentVitals();

            return changed;
        }

        /// <summary>Caps all three vitals at their (possibly lower) max and tells the client. For an unequip: the regen tick
        /// would cap them too, but only up to ~5 s later, which left a window to carry a removed piece's extra health.</summary>
        public void ClampVitalsToMax()
        {
            foreach (var vital in new[] { Health, Stamina, Mana })
            {
                var max = vital.MaxValue;
                if (vital.Current > max)
                    UpdateVital(vital, (int)max);
            }
            SendCurrentVitals();
        }

        /// <summary>Re-sends current health, stamina and mana, so the client checks them against the max it now works out.</summary>
        private void SendCurrentVitals()
        {
            if (Session == null)
                return;
            Session.Network.EnqueueSend(
                new GameMessagePrivateUpdateAttribute2ndLevel(this, Vital.Health, Health.Current),
                new GameMessagePrivateUpdateAttribute2ndLevel(this, Vital.Stamina, Stamina.Current),
                new GameMessagePrivateUpdateAttribute2ndLevel(this, Vital.Mana, Mana.Current));
        }

        /// <summary>What the server holds as sent, for /zcpanel show.</summary>
        public IReadOnlyCollection<ZcGearDisplay.Entry> ZcGearDisplaySent => zcGearDisplaySent.Values;
    }
}
