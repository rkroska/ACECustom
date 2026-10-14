using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// PANDEMIC charm (owner 2026-10-13: "Pandemic - Void only. Each time a void dot ticks (not the initial hit), it spreads to
    /// nearby creatures within X range", and "Yes can spread again, infinitely if mobs are in range, just not on the same mob.
    /// Lasts the source").
    ///
    /// A player's hand-cast void DoT (Corrosion, Corruption, Destructive Curse) on a Zone Control monster is MARKED at the cast
    /// when the caster's Pandemic charm is active: the charm's level and the moment the DoT ends are written onto the DoT's
    /// snapshot (Creature_DotHit.cs). Every later tick of a marked DoT - never the landing tick - looks for monsters near the
    /// ticking one that do not carry that DoT from this caster, and puts a copy on them:
    ///   - the copy is a real DoT entry from the same caster (kill credit, the DoT bonus, the Curse's damage cut all follow);
    ///   - it lasts only until the ORIGINAL cast would have ended (an absolute time carried from copy to copy), so a whole
    ///     infection is bounded by one cast's clock however far it travels, and it cannot keep itself alive by ping-pong;
    ///   - the copy is marked too (one generation further), so it spreads in turn;
    ///   - a monster that carries the DoT from this caster is never given it again (an Add would refresh it to full length).
    /// Monsters only (never players or pets), Zone Control monsters only (only they tick by the new math), each with the tier
    /// hit gate, optional line of sight and resist roll, and the immunity (Creature_CharmImmunity.cs).
    ///
    /// THREADS: the tick runs on the ticking monster's landblock thread. A neighbour can belong to another thread, so the
    /// infection itself is queued onto the NEIGHBOUR's own action queue (as EnsureDotTicker does); only read-only checks are
    /// made from here.
    /// </summary>
    partial class Creature
    {
        /// <summary>Marks a freshly cast (or re-cast) void DoT as one that spreads. Called from the cast path only - a weapon
        /// proc's DoT, a bench Add and a spread copy do not come through here.</summary>
        public void ZcPandemicArm(Player caster, Spell spell, PropertiesEnchantmentRegistry entry)
        {
            if (caster == null || spell == null || entry == null || !IsVoidDotSpell(spell.Id))
                return;
            var snap = ZcDotSnapshotFor(entry);
            if (snap == null)
                return;   // old math or not a Zone Control monster: nothing to mark

            snap.PandemicLevel = 0;
            var cs = CharmSettingsManager.Pandemic;
            if (!cs.Enabled || !caster.HasPandemicCharm)
                return;
            // "full T11 for now": cast with a T11+ wand whose power is live here
            if (cs.On("t11only"))
            {
                var wand = caster.GetEquippedWand();
                if (wand == null || ZoneCraftGate.TierOf(wand) < 11 || ZoneControlManager.WeaponPowerSuppressed(wand, caster))
                    return;
            }
            var level = CharmAbilityRegistry.LevelOf(caster, CharmAbilityRegistry.PandemicAbilityId);
            if (level < 1)
                return;

            snap.Generation = 0;
            snap.PandemicEndsAt = Time.GetUnixTime() + Math.Max(0.0, entry.Duration + entry.StartTime);
            snap.PandemicLevel = level;
        }

        /// <summary>One tick (not the landing tick) of a marked DoT has just been dealt to this monster: spread it.</summary>
        public void ZcPandemicOnTick(PropertiesEnchantmentRegistry entry, Player caster, ZcDotSnapshot snap)
        {
            var cs = CharmSettingsManager.Pandemic;
            if (entry == null || caster == null || snap == null || snap.PandemicLevel < 1 || !cs.Enabled)
                return;

            var level = snap.PandemicLevel;
            var spellId = (uint)entry.SpellId;
            var which = IsCorrosionSpell(spellId) ? "corrosion" : IsCorruptionSpell(spellId) ? "corruption" : IsDestructiveCurseSpell(spellId) ? "curse" : null;
            if (which == null || !cs.On(which))
                return;

            // cheap gates first: the clock, the generation, the chance, the caster's cap - only then the scan
            var now = Time.GetUnixTime();
            var left = snap.PandemicEndsAt - now;
            if (left < 1.0)
                return;
            var generations = cs.CountAt("generations", level);
            if (generations > 0 && snap.Generation >= generations)
                return;
            var maxTargets = cs.CountAt("targets", level);
            var range = cs.NumberAt("range", level);
            if (maxTargets <= 0 || range <= 0f)
                return;
            var chance = cs.At("chance", level);
            if (chance < 1.0 && ThreadSafeRandom.Next(0.0f, 1.0f) >= chance)
                return;
            var cap = cs.CountAt("maxinfected", level);
            var room = cap > 0 ? cap - caster.PandemicCarried(now) : int.MaxValue;
            if (room <= 0)
                return;
            if (Location == null || CurrentLandblock == null)
                return;

            var here = Location.ToGlobal(false);
            var allObjects = new List<WorldObject>();
            var lb = CurrentLandblock;
            allObjects.AddRange(lb.GetWorldObjectsForPhysicsHandling());
            if (lb.Adjacents != null)
            {
                foreach (var adj in lb.Adjacents)
                    if (adj != null) allObjects.AddRange(adj.GetWorldObjectsForPhysicsHandling());
            }

            var casterGuid = caster.Guid.Full;
            var category = entry.SpellCategory;
            var candidates = new List<(Creature c, float dist)>();
            var seen = new HashSet<ObjectGuid>();
            foreach (var obj in allObjects)
            {
                if (!seen.Add(obj.Guid)) continue;
                if (obj is not Creature c || c == this || c is Player || c is CombatPet) continue;
                if (!c.IsAlive || c.Location == null || c.PhysicsObj == null || !c.ZcDebuffCompressed) continue;
                var dist = Vector3.Distance(here, c.Location.ToGlobal(false));
                if (dist > range) continue;
                if (!caster.CanDamage(c) || !c.CanBeDamagedBy(caster)) continue;
                if (c.IsImmuneToPandemic()) continue;
                // "just not on the same mob": a monster that carries this DoT from this caster is left alone
                if (c.ZcCarriesDotFrom(category, casterGuid)) continue;
                candidates.Add((c, dist));
            }
            if (candidates.Count == 0)
                return;
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            var los = cs.On("los");
            var limit = Math.Min(maxTargets, room);
            var sent = 0;
            ACE.Server.Physics.PhysicsObj sightObj = null;
            try
            {
                foreach ((Creature neighbour, float _) in candidates)
                {
                    if (sent >= limit)
                        break;
                    if (los && PhysicsObj != null)
                    {
                        if (sightObj == null)
                        {
                            sightObj = Player.CreateRingSightObject();
                            sightObj.SightIgnoresTerrain = false;
                        }
                        if (!Player.IsRingTargetInSight(this, neighbour, sightObj))
                            continue;
                    }

                    sent++;
                    var endsAt = snap.PandemicEndsAt;
                    var generation = snap.Generation + 1;
                    neighbour.EnqueueAction(new ActionEventDelegate(ActionType.ControlFlowDelay,
                        () => neighbour.ZcPandemicReceive(caster, spellId, level, generation, endsAt)));
                }
            }
            finally
            {
                sightObj?.DestroyObject();
            }
        }

        /// <summary>True when this creature has a DoT of that family from that caster on it.</summary>
        public bool ZcCarriesDotFrom(SpellCategory category, uint casterGuid)
        {
            foreach (var e in EnchantmentManager.GetEnchantments(category))
                if (e.CasterObjectId == casterGuid) return true;
            return false;
        }

        /// <summary>On THIS monster's own thread: catch a spread copy of the caster's DoT.</summary>
        private void ZcPandemicReceive(Player caster, uint spellId, int level, int generation, double endsAt)
        {
            var cs = CharmSettingsManager.Pandemic;
            if (IsDestroyed || IsDead || CurrentLandblock == null || !ZcDebuffCompressed || !cs.Enabled)
                return;
            if (caster == null || caster.IsDestroyed || caster.CurrentLandblock == null)
                return;
            var left = endsAt - Time.GetUnixTime();
            if (left < 1.0)
                return;

            var spell = new Spell(spellId);
            if (spell.NotFound || ZcCarriesDotFrom(spell.Category, caster.Guid.Full) || IsImmuneToPandemic())
                return;

            // the cap again, now that the copy lands: two monsters ticking at the same moment both saw room for one more
            // (measured 2026-10-14: cap 2 let 3 carry at once)
            var cap = cs.CountAt("maxinfected", level);
            if (cap > 0 && caster.PandemicCarried(Time.GetUnixTime()) >= cap)
                return;

            // the tier hit gate always holds (an under-augmented caster's spells never land on a T11+ monster); the plain
            // magic-defense roll only when the setting asks for it. Quiet: no "resists your spell" for a spread.
            if (!TierHitGate.CanHit(caster, this, out _))
                return;
            if (cs.On("resist"))
            {
                var skill = caster.GetCreatureSkill(spell.School)?.Current ?? 0;
                if (MagicDefenseCheck(skill, GetEffectiveMagicDefense(), out _))
                    return;
            }

            // a real DoT entry from the caster: fresh numbers against THIS monster from the wand in the caster's hand now
            ACE.Server.WorldObjects.Managers.EnchantmentManager.ZcHoldLandingTick = !cs.On("landing");
            AddEnchantmentResult result;
            try
            {
                result = EnchantmentManager.Add(spell, caster, caster.GetEquippedWand());
            }
            finally
            {
                ACE.Server.WorldObjects.Managers.EnchantmentManager.ZcHoldLandingTick = false;
            }
            var copy = result?.Enchantment;
            if (copy == null)
                return;

            // "Lasts the source": it ends when the cast it came from ends
            copy.StartTime = 0;
            copy.Duration = left;
            ChangesDetected = true;

            var snap = ZcDotSnapshotFor(copy);
            if (snap != null)
            {
                var share = cs.NumberAt("damage", level);
                snap.NormalPre *= share;
                snap.CritPre *= share;
                snap.Generation = generation;
                snap.PandemicEndsAt = endsAt;
                snap.PandemicLevel = level;   // copies spread too
            }

            if (cs.On("aggro"))
                caster.OnAttackMonster(this);

            // what players see: the spell's own effect on the newly infected monster
            if (spell.TargetEffect != 0)
                EnqueueBroadcast(new GameMessageScript(Guid, spell.TargetEffect, spell.Formula.Scale));
            else
                EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.HealthDownVoid));

            caster.PandemicSpreadTo(this, spell, endsAt, cs.On("message"));
        }
    }

    partial class Player
    {
        // monster guid -> the monster and when the spread copy it caught ends; the charm's cap counts the ones still carrying it
        private readonly ConcurrentDictionary<uint, (Creature Monster, double EndsAt)> pandemicCarried = new ConcurrentDictionary<uint, (Creature, double)>();
        private int pandemicUntold;
        private double pandemicToldAt;
        private const double PandemicTellEvery = 5.0;

        /// <summary>How many monsters carry a spread copy of this player's DoTs right now: alive, in the world, the copy's time
        /// not over, and a void DoT of this player still on them (found 2026-10-14: counting by end time alone kept dead and
        /// despawned monsters in the count for the DoT's whole length - hours with duration augs - and the charm stopped).</summary>
        public int PandemicCarried(double now)
        {
            var n = 0;
            var me = Guid.Full;
            foreach (var kvp in pandemicCarried)
            {
                var m = kvp.Value.Monster;
                var carrying = kvp.Value.EndsAt > now && m != null && !m.IsDestroyed && !m.IsDead && m.CurrentLandblock != null
                    && (m.ZcCarriesDotFrom(SpellCategory.NetherDamageOverTimeRaising, me)
                        || m.ZcCarriesDotFrom(SpellCategory.NetherDamageOverTimeRaising2, me)
                        || m.ZcCarriesDotFrom(SpellCategory.NetherDamageOverTimeRaising3, me));
                if (carrying) n++;
                else pandemicCarried.TryRemove(kvp.Key, out _);
            }
            return n;
        }

        /// <summary>A DoT of this player's has spread to a monster: count it for the cap, and tell the player - at most one
        /// line every 5 s, with how many monsters since the last line.</summary>
        public void PandemicSpreadTo(Creature monster, Spell spell, double endsAt, bool tell)
        {
            if (monster == null) return;
            pandemicCarried[monster.Guid.Full] = (monster, endsAt);
            if (!tell || Session == null) return;

            var count = System.Threading.Interlocked.Increment(ref pandemicUntold);
            var now = Time.GetUnixTime();
            if (now - pandemicToldAt < PandemicTellEvery) return;
            pandemicToldAt = now;
            System.Threading.Interlocked.Exchange(ref pandemicUntold, 0);
            var text = count <= 1 ? $"Your {spell.Name} spreads to {monster.Name}." : $"Your void magic spreads to {count} more creatures.";
            Session.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Magic));
        }
    }
}
