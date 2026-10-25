using System;
using System.Collections.Generic;
using System.Numerics;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// CHAIN charm (owner 2026-10-13: "Chain - War Spells only. Arcs, Bolts, Streaks", and "all that you said, but also with
    /// an option to allow hitting the same target twice, just not twice in a row ... default off").
    ///
    /// A hand-cast war arc, bolt or streak that hits a monster jumps on: a new projectile of the same spell leaves the
    /// monster just hit and flies to the nearest other monster in range, and so on - one monster after another, each jump
    /// from the last monster hit. A jump is a real hit (its own resist, crit and weapon cards), times the chain's damage
    /// share. The chain's numbers are read ONCE, when it starts, from the charm's settings at the charm's level, and travel
    /// with the projectiles - a setting changed mid-flight never changes a chain already on its way.
    ///
    /// What can never loop: a chain starts only from a hand-cast spell that is not itself part of a chain; every jump
    /// lowers JumpsLeft by one and nothing raises it; a chained projectile never forks.
    /// </summary>
    public sealed class SpellChain
    {
        /// <summary>Jumps still to make after the projectile carrying this has hit.</summary>
        public int JumpsLeft;
        /// <summary>1 for the first jump, 2 for the second ...</summary>
        public int JumpIndex;
        /// <summary>The share of a normal hit the projectile carrying this deals.</summary>
        public float Mult;
        /// <summary>Each further jump deals this share of the jump before it.</summary>
        public float Falloff;
        /// <summary>The lowest share of a normal hit a jump can fall to through the falloff (0 = no floor; owner 2026-10-17).</summary>
        public float Floor;
        public float Range;
        public bool Revisit, Los, OnResist, Procs;
        /// <summary>Chance a chain that would end (no fresh monster in reach) jumps back to a monster it hit instead (2026-10-17).</summary>
        public float RevisitChance;
        /// <summary>Every monster this chain has hit (the first one included), by guid.</summary>
        public HashSet<uint> Visited;

        /// <summary>Why chains ended, by reason - a cheap counter for tests (the bench's /bench charms counts reads it).</summary>
        public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Stops = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

        public static void Stop(string why) => Stops.AddOrUpdate(why, 1, (_, n) => n + 1);
    }

    partial class SpellProjectile
    {
        /// <summary>Set on a projectile that is a JUMP of a chain (never on the spell that started it).</summary>
        public SpellChain Chain { get; set; }

        /// <summary>One of the Split Cast charm's extra projectiles: it never starts a chain - only the main projectile does,
        /// so the two charms do not multiply (owner 2026-10-20: "Chain starts only from the main projectile").</summary>
        public bool IsSplitCastExtra { get; set; }

        /// <summary>True for a spell the Chain charm works on: war school, a plain projectile flying as an arc, bolt or
        /// streak (per the charm's three switches). Rings, blasts, volleys, walls, life and void spells never chain.</summary>
        private bool IsChainShape() => IsChainShape(Spell, SpellType);

        /// <summary>The same test for a spell before it flies (Player.LeveledCharmReplacesForkAndSplit): Chain replaces Fork and
        /// Split Cast only for the shapes it actually chains.</summary>
        internal static bool IsChainShape(Spell spell, ProjectileSpellType shape)
        {
            if (spell == null || spell.School != MagicSchool.WarMagic || spell.MetaSpellType != ACE.Entity.Enum.SpellType.Projectile)
                return false;
            var cs = CharmSettingsManager.Chain;
            return shape == ProjectileSpellType.Arc && cs.On("arcs")
                || shape == ProjectileSpellType.Bolt && cs.On("bolts")
                || shape == ProjectileSpellType.Streak && cs.On("streaks");
        }

        /// <summary>
        /// Called when this projectile has struck a creature it may damage (after the damage, whatever it was).
        /// <paramref name="landed"/> = the hit was not resisted. Starts a chain from a hand-cast spell, or carries this
        /// projectile's own chain one jump further.
        /// </summary>
        private void TryChain(Player player, Creature struck, bool landed)
        {
            if (player == null || struck == null || IsForkProjectile)
                return;

            SpellChain next;

            if (Chain != null)
            {
                // a jump has hit: remember who it really hit (it can strike a monster in its way), then go on
                Chain.Visited.Add(struck.Guid.Full);
                if (Chain.JumpsLeft <= 0) { SpellChain.Stop("all jumps made"); return; }
                if (!landed && !Chain.OnResist) { SpellChain.Stop("jump resisted"); return; }
                next = new SpellChain
                {
                    JumpsLeft = Chain.JumpsLeft - 1,
                    JumpIndex = Chain.JumpIndex + 1,
                    Mult = Math.Max(Chain.Mult * Chain.Falloff, Chain.Floor),
                    Falloff = Chain.Falloff,
                    Floor = Chain.Floor,
                    Range = Chain.Range,
                    Revisit = Chain.Revisit,
                    RevisitChance = Chain.RevisitChance,
                    Los = Chain.Los,
                    OnResist = Chain.OnResist,
                    Procs = Chain.Procs,
                    Visited = Chain.Visited,
                };
            }
            else
            {
                // a chain starts only from a HAND-CAST spell: never a weapon proc, never a wand's built-in spell, never a Split
                // Cast extra projectile (only the main one)
                if (FromProc || IsWeaponSpell || IsSplitCastExtra || !player.HasChainCharm)
                    return;
                var cs = CharmSettingsManager.Chain;
                if (!cs.Enabled || !IsChainShape() || (!landed && !cs.On("onresist")))
                    return;
                // "full T11 for now": cast with a T11+ wand whose power is live here
                if (cs.On("t11only"))
                {
                    var wand = ProjectileLauncher;
                    if (!(wand is Caster) || ZoneCraftGate.TierOf(wand) < 11 || ZoneControlManager.WeaponPowerSuppressed(wand, player))
                        return;
                }
                var level = CharmAbilityRegistry.LevelOf(player, CharmAbilityRegistry.ChainAbilityId);
                var jumps = cs.CountAt("jumps", level);
                if (level < 1 || jumps <= 0)
                    return;
                var chance = cs.At("chance", level);
                if (chance < 1.0 && ThreadSafeRandom.Next(0.0f, 1.0f) >= chance)
                    return;
                next = new SpellChain
                {
                    JumpsLeft = jumps - 1,
                    JumpIndex = 1,
                    Mult = cs.NumberAt("damage", level),
                    Falloff = cs.NumberAt("falloff", level),
                    Floor = cs.NumberAt("floor", level),
                    Range = cs.NumberAt("range", level),
                    Revisit = cs.On("revisit"),
                    RevisitChance = Math.Clamp(cs.NumberAt("revisitchance", level), 0f, 1f),
                    Los = cs.On("los"),
                    OnResist = cs.On("onresist"),
                    Procs = cs.On("procs"),
                    Visited = new HashSet<uint> { struck.Guid.Full },
                };
            }

            if (next.Mult <= 0f || next.Range <= 0f)
                return;

            LaunchChainJump(player, struck, next);
        }

        /// <summary>Finds the nearest monster the chain may jump to from <paramref name="from"/> and launches the jump.</summary>
        private void LaunchChainJump(Player player, Creature from, SpellChain chain)
        {
            // the narrow window where the landblock is already removing the struck creature (as the Fork charm guards)
            if (from.Location == null || from.CurrentLandblock == null || from.PhysicsObj == null)
            {
                SpellChain.Stop("monster just hit is gone");
                return;
            }

            var fromGlobal = from.Location.ToGlobal(false);

            var allObjects = new List<WorldObject>();
            var lb = from.CurrentLandblock;
            allObjects.AddRange(lb.GetWorldObjectsForPhysicsHandling());
            if (lb.Adjacents != null)
            {
                foreach (var adj in lb.Adjacents)
                    if (adj != null) allObjects.AddRange(adj.GetWorldObjectsForPhysicsHandling());
            }

            // JUMP BACK (owner 2026-10-17): "5% chance [per milestone] to jump back to a monster it has hit (just never the same
            // monster twice in a row)" - rolled ONLY when no fresh monster can be reached, i.e. when the chain would end.
            List<(Creature c, float dist)> Gather(bool back)
            {
                var list = new List<(Creature c, float dist)>();
                var seen = new HashSet<ObjectGuid>();
                foreach (var obj in allObjects)
                {
                    if (!seen.Add(obj.Guid)) continue;
                    if (obj is not Creature c || c == player || c == from || c is Player || c is CombatPet) continue;
                    if (!c.IsAlive || c.Location == null || c.PhysicsObj == null) continue;
                    // fresh pass: every monster once - or, with "revisit" on, any monster but the one just hit;
                    // jump-back pass: only monsters this chain already hit (never the one just hit - excluded above)
                    var hit = chain.Visited.Contains(c.Guid.Full);
                    if (back ? !hit : !chain.Revisit && hit) continue;
                    var dist = Vector3.Distance(fromGlobal, c.Location.ToGlobal(false));
                    if (dist > chain.Range) continue;
                    if (!player.CanDamage(c) || !c.CanBeDamagedBy(player)) continue;
                    if (player.CheckPKStatusVsTarget(c, Spell) != null) continue;
                    if (c.IsImmuneToChain()) continue;
                    list.Add((c, dist));
                }
                list.Sort((a, b) => a.dist.CompareTo(b.dist));
                return list;
            }

            ACE.Server.Physics.PhysicsObj sightObj = null;
            var why = "no monster in range";

            bool TryLaunch(List<(Creature c, float dist)> candidates, string label)
            {
                foreach ((Creature target, float _) in candidates)
                {
                    // a clear line from the monster just hit to the next one (the ring's sight trace; the ground blocks)
                    if (chain.Los)
                    {
                        if (sightObj == null)
                        {
                            sightObj = Player.CreateRingSightObject();
                            sightObj.SightIgnoresTerrain = false;
                        }
                        if (!Player.IsRingTargetInSight(from, target, sightObj))
                        {
                            why = from.IsDead ? "no sight from a dying monster" : "no monster in sight";
                            continue;
                        }
                    }

                    var origins = player.CalculateProjectileOrigins(Spell, SpellType, target, from);
                    if (origins == null || origins.Count == 0)
                    {
                        why = "no launch point";
                        continue;
                    }
                    var velocity = player.CalculateProjectileVelocity(Spell, target, SpellType, origins[0], from);

                    // a jump is a hand-cast hit like the spell that started it (FromProc stays false: full crit rules);
                    // the chain rides on the projectile and is set before it enters the world (it can collide on entry)
                    player.LaunchSpellProjectiles(
                        Spell, target, SpellType,
                        ProjectileLauncher, IsWeaponSpell, FromProc,
                        origins, velocity, LifeProjectileDamage,
                        originOverride: from, directionOverride: from,
                        chain: chain);
                    SpellChain.Stop(label + (from.IsDead ? " (from a dying monster)" : ""));
                    return true;   // one jump at a time
                }
                return false;
            }

            try
            {
                if (TryLaunch(Gather(false), "jumped"))
                    return;
                if (!chain.Revisit && chain.RevisitChance > 0f)
                {
                    if (ThreadSafeRandom.Next(0.0f, 1.0f) < chain.RevisitChance)
                    {
                        why = "no monster to jump back to";
                        if (TryLaunch(Gather(true), "jumped back"))
                            return;
                    }
                    else
                        why += " (jump back not rolled)";
                }
                SpellChain.Stop(why);
            }
            finally
            {
                sightObj?.DestroyObject();
            }
        }
    }
}
