using System.Collections.Generic;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// DoT SPELL LINES (owner 2026-10-26: "build a full on Damage Meter ... So this is easier to visually see" - to check that
    /// Corrosion and Corruption deal about the same and Destructive Curse the rest). A void DoT tick from a player names nothing:
    /// the retail heartbeat sends one "periodic nether damage" line for all of a caster's DoTs together (only with the server's
    /// show_dot_messages on - off by default, as in retail), and the faster DoT ticker (Zone Control monsters) sends no line at
    /// all. These lines do NOT follow show_dot_messages: the player asked for them. A player who turned them on (/ilt dotlines, owner 2026-11-02: a player
    /// option, every 1-10 s) gets their DoT damage on a monster added up per spell and sent once per their interval (and when
    /// the monster dies), one line per spell:
    ///   "Your Incantation of Corrosion deals 123456 points of periodic nether damage to Shallows Moarsman (5 ticks, 1 critical)."
    /// (the ticks and crits it covers: owner 10-26 "show DPS and damage per tick")
    /// The combined line is then left out for that player (the same damage twice would be counted twice). Off = unchanged.
    /// Only on this creature's own thread, like the ticks that feed it. Keyed by the caster's guid, never the Player object
    /// (a monster must not keep a logged-out player alive; a relog is looked up fresh when the line goes out).
    ///
    /// The window: each caster has one due time per monster. Their lines go out at it (on a tick, or on the one delayed flush
    /// queued when their first line of the window starts), the next due time is one interval after the PLANNED one (no drift,
    /// never faster than the interval), and a caster back after their planned time has passed starts a fresh window.
    /// </summary>
    partial class Creature
    {
        private sealed class DotSpellLine { public double Amount; public int Ticks, Crits; }   // double: a window can pass 16.7M
        private Dictionary<(uint Caster, uint SpellId), DotSpellLine> dotSpellLinePending;
        private Dictionary<uint, double> dotSpellLineNextAt;

        /// <summary>A line due within this much of its time goes out now (a tick runs a little after its planned time, and the
        /// heartbeat's clock is not the live one - an exact compare would slip a whole tick).</summary>
        private const double DotSpellLineSlack = 0.25;

        /// <summary>This player gets per-spell DoT lines (their /ilt dotlines is on).</summary>
        public static bool DotSpellLinesFor(Player player) => player != null && player.DotLineSeconds > 0;

        /// <summary>One DoT tick's share from one spell of one player.</summary>
        public void DotSpellLineAdd(Player caster, uint spellId, float amount, bool crit = false)
        {
            if (caster == null || !(amount > 0)) return;   // NaN too
            var guid = caster.Guid.Full;
            dotSpellLinePending ??= new Dictionary<(uint, uint), DotSpellLine>();
            var key = (guid, spellId);
            if (!dotSpellLinePending.TryGetValue(key, out var line))
            {
                var firstOfCaster = !HasDotSpellLinePending(guid);
                dotSpellLinePending[key] = line = new DotSpellLine();
                if (firstOfCaster)
                {
                    var seconds = System.Math.Max(1, caster.DotLineSeconds);
                    var now = Time.GetUnixTime();
                    dotSpellLineNextAt ??= new Dictionary<uint, double>();
                    // the planned time stands while it is ahead, or only just past (the tick that arrives a moment after it must
                    // still find the line due); a caster back after it has passed starts a fresh window, so a line is never
                    // sent sooner than one interval after the last
                    if (!dotSpellLineNextAt.TryGetValue(guid, out var at) || now - at > DotSpellLineSlack)
                        dotSpellLineNextAt[guid] = at = now + seconds;

                    // the lines go out at that time even if no tick comes after it (DoTs ran out, dispelled, the monster went
                    // home): one delayed flush on this creature's own thread per caster window
                    var chain = new ActionChain();
                    chain.AddDelaySeconds(System.Math.Max(0.0, at - now) + DotSpellLineSlack);
                    chain.AddAction(this, ActionType.ControlFlowDelay, () => { if (!IsDestroyed) DotSpellLineFlushSafe(false); });
                    chain.EnqueueChain();
                }
            }
            line.Amount += amount; line.Ticks++;
            if (crit) line.Crits++;
        }

        /// <summary>
        /// A monster that goes without dying - a despawn, a reset, a landblock unload - sends the lines it still holds first
        /// (CodeRabbit #590: the delayed flush is dropped with its action queue, and only OnDeath forced one). Nothing pending =
        /// nothing sent (players, pets and a monster already flushed by its death).
        /// </summary>
        public override void Destroy(bool raiseNotifyOfDestructionEvent = true, bool fromLandblockUnload = false, bool enqueueDatabaseRemove = true)
        {
            DotSpellLineFlushSafe(force: true);
            base.Destroy(raiseNotifyOfDestructionEvent, fromLandblockUnload, enqueueDatabaseRemove);
        }

        /// <summary>The flush from a damage tick or a death: a chat line must never stop either - a failure is logged, not thrown.</summary>
        public void DotSpellLineFlushSafe(bool force)
        {
            try { DotSpellLineFlush(force); }
            catch (System.Exception ex) { log.Warn($"[DOTLINES] flush on {Name} (0x{Guid}) failed: {ex}"); }
        }

        /// <summary>After a tick: each caster's lines go out once per their interval, or at once when this tick killed.</summary>
        public void DotSpellLineFlush(bool force)
        {
            if (dotSpellLinePending == null || dotSpellLinePending.Count == 0) return;
            var now = Time.GetUnixTime();

            List<(uint Caster, uint SpellId)> sent = null;
            foreach (var kvp in dotSpellLinePending)
            {
                var (guid, spellId) = kvp.Key;
                var due = dotSpellLineNextAt == null || !dotSpellLineNextAt.TryGetValue(guid, out var at) || now >= at - DotSpellLineSlack;
                if (!force && !due) continue;
                (sent ??= new List<(uint Caster, uint SpellId)>()).Add(kvp.Key);

                var caster = PlayerManager.GetOnlinePlayer(guid);
                var line = kvp.Value;
                var amount = (ulong)System.Math.Round(line.Amount);
                if (amount == 0 || caster?.Session == null || caster.DotLineSeconds <= 0) continue;   // turned off since: off means off
                var spell = new Spell(spellId);
                var name = spell.NotFound || string.IsNullOrEmpty(spell.Name) ? $"spell {spellId}" : spell.Name;
                var crits = line.Crits > 0 ? $", {line.Crits} critical" : "";
                caster.SendMessage($"Your {name} deals {FormatDamage(amount, caster.DamageNumberFormat)} points of periodic nether damage to {Name} ({line.Ticks} tick{(line.Ticks == 1 ? "" : "s")}{crits}).", ChatMessageType.Magic);
            }
            if (sent == null) return;

            HashSet<uint> casters = null;
            foreach (var key in sent)
            {
                dotSpellLinePending.Remove(key);
                (casters ??= new HashSet<uint>()).Add(key.Caster);
            }
            foreach (var guid in casters)
            {
                // the next line from this caster: one interval after the PLANNED time (no drift, never faster than the interval;
                // after a forced or very late send, one interval from now). Logged out / turned off = forgotten here.
                var seconds = PlayerManager.GetOnlinePlayer(guid)?.DotLineSeconds ?? 0;
                if (seconds <= 0 || dotSpellLineNextAt == null) { dotSpellLineNextAt?.Remove(guid); continue; }
                var planned = dotSpellLineNextAt.TryGetValue(guid, out var at) ? at + seconds : now + seconds;
                dotSpellLineNextAt[guid] = planned > now ? planned : now + seconds;
            }

            // casters long gone from here (idle more than 10 s past their time, nothing pending) are forgotten
            if (dotSpellLineNextAt != null && dotSpellLineNextAt.Count > 0)
            {
                List<uint> stale = null;
                foreach (var kvp in dotSpellLineNextAt)
                    if (now - kvp.Value > 10 && !HasDotSpellLinePending(kvp.Key))
                        (stale ??= new List<uint>()).Add(kvp.Key);
                if (stale != null)
                    foreach (var g in stale) dotSpellLineNextAt.Remove(g);
            }
        }

        private bool HasDotSpellLinePending(uint guid)
        {
            if (dotSpellLinePending == null) return false;
            foreach (var key in dotSpellLinePending.Keys)
                if (key.Caster == guid) return true;
            return false;
        }
    }
}
