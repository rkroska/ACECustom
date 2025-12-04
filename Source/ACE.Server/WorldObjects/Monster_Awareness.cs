using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Determines when a monster wakes up from idle state
    /// </summary>
    partial class Creature
    {
        /// <summary>
        /// Monsters wake up when players are in visual range
        /// </summary>
        public bool IsAwake = false;

        /// <summary>
        /// Cache for visible targets to reduce expensive lookups
        /// </summary>
        private List<Creature> _cachedVisibleTargets = new List<Creature>();
        private double _lastTargetCacheTime = 0.0;
        private const double TARGET_CACHE_DURATION = 0.5; // Cache for 0.5 seconds

        // Multi-target distance cache for BuildTargetDistance to avoid repeated physics calculations
        // Thread-safe: Monster_Tick runs in single-threaded landblock groups, no concurrent access
        // Cache lifetime: Cleared in InvalidateTargetCaches() on target change, bounded by active target count (~5-20 entries)
        // Cache key includes distSq flag to prevent mixing squared and linear distances
        private Dictionary<(uint TargetId, bool DistSq), float> _multiTargetDistanceCache = new Dictionary<(uint, bool), float>();
        private double _lastMultiTargetDistanceCacheTime = 0.0;
        private const double MULTI_TARGET_DISTANCE_CACHE_DURATION = 0.3; // Cache for 0.3 seconds (1 tick)
        
        // Pre-filter multiplier for fast distance check - filters out obviously out-of-range creatures
        // Applied to squared distance: 2.25 = (1.5)^2 to provide 1.5x linear safety margin
        // This accounts for height differences and collision radii while still filtering 90%+ of creatures
        private const float QUICK_DISTANCE_CHECK_MULTIPLIER_SQ = 2.25f;

        // Cache performance counters
        private static long _cacheHits = 0;
        private static long _cacheMisses = 0;

        /// <summary>
        /// Invalidates both target and distance caches
        /// </summary>
        private void InvalidateTargetCaches()
        {
            _cachedVisibleTargets.Clear();
            _lastTargetCacheTime = 0.0;
            _multiTargetDistanceCache.Clear();
            _lastMultiTargetDistanceCacheTime = 0.0;
            InvalidateDistanceCache();
        }

        /// <summary>
        /// Sets the attack target and invalidates all caches in one operation.
        /// Ensures consistency across all code paths.
        /// </summary>
        private void SetAttackTargetAndInvalidate(Creature target)
        {
            AttackTarget = target;
            InvalidateTargetCaches();
        }

        /// <summary>
        /// Gets cache performance statistics for monitoring
        /// </summary>
        public static (long hits, long misses, double hitRate) GetCacheStats()
        {
            var hits = Interlocked.Read(ref _cacheHits);
            var misses = Interlocked.Read(ref _cacheMisses);
            var total = hits + misses;
            var hitRate = total > 0 ? (double)hits / total : 0.0;
            return (hits, misses, hitRate);
        }

        /// <summary>
        /// Transitions a monster from idle to awake state
        /// </summary>
        public void WakeUp(bool alertNearby = true)
        {
            MonsterState = State.Awake;
            IsAwake = true;
            //DoAttackStance();
            EmoteManager.OnWakeUp(AttackTarget as Creature);
            EmoteManager.OnNewEnemy(AttackTarget as Creature);
            //SelectTargetingTactic();

            if (alertNearby)
                AlertFriendly();
        }

        /// <summary>
        /// Transitions a monster from awake to idle state
        /// </summary>
        public virtual void Sleep()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).Sleep()");

            SetCombatMode(CombatMode.NonCombat);

            CurrentAttack = null;
            firstUpdate = true;
            AttackTarget = null;
            IsAwake = false;
            IsMoving = false;
            MonsterState = State.Idle;

            // Clear both target and distance caches consistently
            InvalidateTargetCaches();

            PhysicsObj.CachedVelocity = Vector3.Zero;

            ClearRetaliateTargets();
        }

        public Tolerance Tolerance
        {
            get => (Tolerance)(GetProperty(PropertyInt.Tolerance) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.Tolerance); else SetProperty(PropertyInt.Tolerance, (int)value); }
        }

        /// <summary>
        /// This list of possible targeting tactics for this monster
        /// </summary>
        public TargetingTactic TargetingTactic
        {
            get => (TargetingTactic)(GetProperty(PropertyInt.TargetingTactic) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.TargetingTactic); else SetProperty(PropertyInt.TargetingTactic, (int)TargetingTactic); }
        }

        /// <summary>
        /// The current targeting tactic for this monster
        /// </summary>
        public TargetingTactic CurrentTargetingTactic;

        public void SelectTargetingTactic()
        {
            // monsters have multiple targeting tactics, ex. Focused | Random

            // when should this function be called?
            // when a monster spawns in, does it choose 1 TargetingTactic?

            // or do they randomly select a TargetingTactic from their list of possible tactics,
            // each time they go to find a new target?

            //Console.WriteLine($"{Name}.TargetingTactics: {TargetingTactic}");

            // if targeting tactic is none,
            // use the most common targeting tactic
            // TODO: ensure all monsters in the db have a targeting tactic
            var targetingTactic = TargetingTactic;
            if (targetingTactic == TargetingTactic.None)
                targetingTactic = TargetingTactic.Random | TargetingTactic.TopDamager;

            var possibleTactics = EnumHelper.GetFlags(targetingTactic);
            var rng = ThreadSafeRandom.Next(1, possibleTactics.Count - 1);

            if (targetingTactic == 0)
                rng = 0;

            CurrentTargetingTactic = (TargetingTactic)possibleTactics[rng];

            //Console.WriteLine($"{Name}.TargetingTactic: {CurrentTargetingTactic}");
        }

        public double NextFindTarget;

        public virtual void HandleFindTarget()
        {
            if (Timers.RunningTime < NextFindTarget)
                return;

            FindNextTarget();
        }

        public void SetNextTargetTime()
        {
            // use rng?

            //var rng = ThreadSafeRandom.Next(5.0f, 10.0f);
            var rng = 5.0f;

            NextFindTarget = Timers.RunningTime + rng;
        }

        public virtual bool FindNextTarget()
        {
            stopwatch.Restart();

            try
            {
                SelectTargetingTactic();
                SetNextTargetTime();

                // Don't use cached targets for critical target finding decisions
                var visibleTargets = GetAttackTargetsUncached();
                if (visibleTargets.Count == 0)
                {
                    if (MonsterState != State.Return)
                        MoveToHome();

                    return false;
                }

                // Generally, a creature chooses whom to attack based on:
                //  - who it was last attacking,
                //  - who attacked it last,
                //  - or who caused it damage last.

                // When players first enter the creature's detection radius, however, none of these things are useful yet,
                // so the creature chooses a target randomly, weighted by distance.

                // Players within the creature's detection sphere are weighted by how close they are to the creature --
                // the closer you are, the more chance you have to be selected to be attacked.

                var prevAttackTarget = AttackTarget;

                switch (CurrentTargetingTactic)
                {
                    case TargetingTactic.None:

                        Console.WriteLine($"{Name}.FindNextTarget(): TargetingTactic.None");
                        break; // same as focused?

                    case TargetingTactic.Random:

                        // this is a very common tactic with monsters,
                        // although it is not truly random, it is weighted by distance
                        var targetDistances = BuildTargetDistance(visibleTargets);
                        SetAttackTargetAndInvalidate(SelectWeightedDistance(targetDistances));
                        break;

                    case TargetingTactic.Focused:

                        break; // always stick with original target?

                    case TargetingTactic.LastDamager:

                        var lastDamager = DamageHistory.LastDamager?.TryGetAttacker() as Creature;
                        if (lastDamager != null)
                        {
                            SetAttackTargetAndInvalidate(lastDamager);
                        }
                        break;

                    case TargetingTactic.TopDamager:

                        var topDamager = DamageHistory.TopDamager?.TryGetAttacker() as Creature;
                        if (topDamager != null)
                        {
                            SetAttackTargetAndInvalidate(topDamager);
                        }
                        break;

                    // these below don't seem to be used in PY16 yet...

                    case TargetingTactic.Weakest:

                        // should probably shuffle the list beforehand,
                        // in case a bunch of levels of same level are in a group,
                        // so the same player isn't always selected
                        var lowestLevel = visibleTargets.OrderBy(p => p.Level).FirstOrDefault();
                        SetAttackTargetAndInvalidate(lowestLevel);
                        break;

                    case TargetingTactic.Strongest:

                        var highestLevel = visibleTargets.OrderByDescending(p => p.Level).FirstOrDefault();
                        SetAttackTargetAndInvalidate(highestLevel);
                        break;

                    case TargetingTactic.Nearest:

                        var nearest = BuildTargetDistance(visibleTargets);
                        SetAttackTargetAndInvalidate(nearest[0].Target);
                        break;
                }

                //Console.WriteLine($"{Name}.FindNextTarget = {AttackTarget.Name}");

                if (AttackTarget != null && AttackTarget != prevAttackTarget)
                    EmoteManager.OnNewEnemy(AttackTarget);

                return AttackTarget != null;
            }
            finally
            {
                ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Monster_Awareness_FindNextTarget, stopwatch.Elapsed.TotalSeconds);
            }
        }

        /// <summary>
        /// Returns a list of attackable targets currently visible to this monster
        /// Uses caching to reduce expensive lookups
        /// </summary>
        public List<Creature> GetAttackTargets()
        {
            var currentTime = Timers.RunningTime;
            var last = Volatile.Read(ref _lastTargetCacheTime);
            
            // Check if cache is still valid
            if (last > 0.0 && (currentTime - last) < TARGET_CACHE_DURATION)
            {
                Interlocked.Increment(ref _cacheHits);
                return new List<Creature>(_cachedVisibleTargets);
            }
            
            // Cache expired, refresh it
            Interlocked.Increment(ref _cacheMisses);
            var visibleTargets = GetAttackTargetsUncached();
            
            // Update cache efficiently by clearing and adding items
            _cachedVisibleTargets.Clear();
            _cachedVisibleTargets.AddRange(visibleTargets);
            Volatile.Write(ref _lastTargetCacheTime, currentTime);

            return visibleTargets;
        }

        /// <summary>
        /// Returns a list of attackable targets currently visible to this monster
        /// Always performs fresh calculation (no caching)
        /// </summary>
        public List<Creature> GetAttackTargetsUncached()
        {
            var visibleTargets = new List<Creature>();
            var listOfCreatures = PhysicsObj.ObjMaint.GetVisibleTargetsValuesOfTypeCreature();
            
            // Pre-calculate max range for early bailout
            var maxRangeSq = Math.Max(MaxChaseRangeSq, VisualAwarenessRangeSq);
            
            foreach (var creature in listOfCreatures)
            {
                // exclude dead creatures
                if (creature.IsDead)
                    continue;
                    
                // ensure attackable
                if (!creature.Attackable)
                    continue;
                    
                // hidden players shouldn't be valid visible targets
                if (creature is Player p && (p.Hidden ?? false))
                    continue;
                    
                // Only apply TargetingTactic-based skip to non-players (players are excluded above)
                if (creature.TargetingTactic == TargetingTactic.None && !(creature is Player))
                    continue;
                    
                if (creature.Teleporting)
                    continue;

                if (creature.PhysicsObj == null)
                    continue;

                // Optimization: Fast approximate range check before expensive physics calculation
                // Use simple position distance check first to filter out obviously out-of-range creatures
                // This is MUCH faster than PhysicsObj.get_distance_sq_to_object (no collision checks)
                var quickDistSq = Location.SquaredDistanceTo(creature.Location);
                if (quickDistSq > maxRangeSq * QUICK_DISTANCE_CHECK_MULTIPLIER_SQ)
                    continue;

                // ensure within 'detection radius' ?
                var chaseDistSq = creature == AttackTarget ? MaxChaseRangeSq : VisualAwarenessRangeSq;

                // Now do expensive physics-based distance check only for creatures that passed quick check
                var physicsDistSq = PhysicsObj.get_distance_sq_to_object(creature.PhysicsObj, true);
                if (physicsDistSq > chaseDistSq)
                    continue;

                // if this monster belongs to a faction,
                // ensure target does not belong to the same faction
                if (SameFaction(creature))
                {
                    // unless they have been provoked
                    if (!PhysicsObj.ObjMaint.RetaliateTargetsContainsKey(creature.Guid.Full))
                        continue;
                }

                // cannot switch AttackTargets with Tolerance.Target
                if (Tolerance.HasFlag(Tolerance.Target) && creature != AttackTarget)
                    continue;

                // can only target other monsters with Tolerance.Monster -- cannot target players or combat pets
                if (Tolerance.HasFlag(Tolerance.Monster) && (creature is Player || creature is CombatPet))
                    continue;

                visibleTargets.Add(creature);
            }
            return visibleTargets;
        }

        /// <summary>
        /// Returns the list of potential attack targets, sorted by closest distance 
        /// Uses caching to avoid repeated physics calculations
        /// </summary>
        public List<TargetDistance> BuildTargetDistance(List<Creature> targets, bool distSq = false)
        {
            var currentTime = Timers.RunningTime;
            var targetDistance = new List<TargetDistance>();
            var cacheValid = (currentTime - _lastMultiTargetDistanceCacheTime) < MULTI_TARGET_DISTANCE_CACHE_DURATION;

            foreach (var target in targets)
            {
                var cacheKey = (target.Guid.Full, distSq);
                float distance;
                
                // Try to use cached distance
                if (cacheValid && _multiTargetDistanceCache.TryGetValue(cacheKey, out distance))
                {
                    targetDistance.Add(new TargetDistance(target, distance));
                }
                else
                {
                    // Defensive: Skip if target was destroyed between filtering and distance calculation
                    // Extremely rare edge case, but prevents NullReferenceException
                    if (PhysicsObj == null || target.PhysicsObj == null)
                        continue;
                    
                    // Calculate and cache distance
                    distance = distSq ? (float)PhysicsObj.get_distance_sq_to_object(target.PhysicsObj, true) : (float)PhysicsObj.get_distance_to_object(target.PhysicsObj, true);
                    targetDistance.Add(new TargetDistance(target, distance));
                    _multiTargetDistanceCache[cacheKey] = distance;
                }
            }
            
            // Update cache timestamp after successful build
            _lastMultiTargetDistanceCacheTime = currentTime;

            return targetDistance.OrderBy(i => i.Distance).ToList();
        }

        /// <summary>
        /// Uses weighted RNG selection by distance to select a target
        /// </summary>
        public Creature SelectWeightedDistance(List<TargetDistance> targetDistances)
        {
            if (targetDistances.Count == 1)
                return targetDistances[0].Target;

            // http://asheron.wikia.com/wiki/Wi_Flag

            var distSum = targetDistances.Select(i => i.Distance).Sum();

            // get the sum of the inverted ratios
            var invRatioSum = targetDistances.Count - 1;

            // roll between 0 - invRatioSum here,
            // instead of 0-1 (the source of the original wi bug)
            var rng = ThreadSafeRandom.Next(0.0f, invRatioSum);

            // walk the list
            var invRatio = 0.0f;
            foreach (var targetDistance in targetDistances)
            {
                invRatio += 1.0f - (targetDistance.Distance / distSum);

                if (rng < invRatio)
                    return targetDistance.Target;
            }
            // precision error?
            Console.WriteLine($"{Name}.SelectWeightedDistance: couldn't find target: {string.Join(",", targetDistances.Select(i => i.Distance))}");
            return targetDistances[0].Target;
        }

        /// <summary>
        /// If one of these fields is set, monster scanning for targets when it first spawns in
        /// is terminated immediately
        /// </summary>
        private static readonly Tolerance ExcludeSpawnScan = Tolerance.NoAttack | Tolerance.Appraise | Tolerance.Provoke | Tolerance.Retaliate;

        /// <summary>
        /// Called when a monster is first spawning in
        /// </summary>
        public void CheckTargets()
        {
            if (!Attackable && TargetingTactic == TargetingTactic.None || (Tolerance & ExcludeSpawnScan) != 0)
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(0.75f);
            actionChain.AddAction(this, ActionType.MonsterAwareness_CheckTargetsInner, CheckTargets_Inner);
            actionChain.EnqueueChain();
        }

        public void CheckTargets_Inner()
        {
            Creature closestTarget = null;
            var closestDistSq = float.MaxValue;
            var listOfCreature = PhysicsObj.ObjMaint.GetVisibleTargetsValuesOfTypeCreature();
            foreach (var creature in listOfCreature) //has variant filter
            {
                if (creature is Player player && (!player.Attackable || player.Teleporting || (player.Hidden ?? false)))
                    continue;

                if (Tolerance.HasFlag(Tolerance.Monster) && (creature is Player || creature is CombatPet))
                    continue;

                //var distSq = Location.SquaredDistanceTo(creature.Location);
                var distSq = PhysicsObj.get_distance_sq_to_object(creature.PhysicsObj, true);
                if (distSq < closestDistSq)
                {
                    closestDistSq = (float)distSq;
                    closestTarget = creature;
                }
            }
            if (closestTarget == null || closestDistSq > VisualAwarenessRangeSq)
                return;

            closestTarget.AlertMonster(this);
        }

        /// <summary>
        /// The most common value from retail
        /// Some other common values are in the range of 12-25
        /// </summary>
        public static readonly float VisualAwarenessRange_Default = 18.0f;

        /// <summary>
        /// The highest value found in the current database
        /// </summary>
        public static readonly float VisualAwarenessRange_Highest = 75.0f;

        public double? VisualAwarenessRange
        {
            get => GetProperty(PropertyFloat.VisualAwarenessRange);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.VisualAwarenessRange); else SetProperty(PropertyFloat.VisualAwarenessRange, value.Value); }
        }

        public double? AuralAwarenessRange
        {
            get => GetProperty(PropertyFloat.AuralAwarenessRange);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.AuralAwarenessRange); else SetProperty(PropertyFloat.AuralAwarenessRange, value.Value); }
        }

        private float? _visualAwarenessRangeSq;

        public float VisualAwarenessRangeSq
        {
            get
            {
                if (_visualAwarenessRangeSq == null)
                {
                    var visualAwarenessRange = (float)((VisualAwarenessRange ?? VisualAwarenessRange_Default) * PropertyManager.GetDouble("mob_awareness_range"));

                    _visualAwarenessRangeSq = visualAwarenessRange * visualAwarenessRange;
                }

                return _visualAwarenessRangeSq.Value;
            }
        }

        private float? _auralAwarenessRangeSq;

        public float AuralAwarenessRangeSq
        {
            get
            {
                if (_auralAwarenessRangeSq == null)
                {
                    var auralAwarenessRange = (float)((AuralAwarenessRange ?? VisualAwarenessRange ?? VisualAwarenessRange_Default) * PropertyManager.GetDouble("mob_awareness_range"));

                    _auralAwarenessRangeSq = auralAwarenessRange * auralAwarenessRange;
                }

                return _auralAwarenessRangeSq.Value;
            }
        }

        /// <summary>
        /// A monster can only alert friendly mobs to the presence of each attack target
        /// once every AlertThreshold
        /// </summary>
        private static readonly TimeSpan AlertThreshold = TimeSpan.FromMinutes(2);

        /// <summary>
        /// AttackTarget => last alerted time
        /// </summary>
        private Dictionary<uint, DateTime> Alerted;

        public void AlertFriendly()
        {
            // if current attacker has already alerted this monster recently,
            // don't re-alert friendlies
            if (Alerted != null && Alerted.TryGetValue(AttackTarget.Guid.Full, out var lastAlertTime) && DateTime.UtcNow - lastAlertTime < AlertThreshold)
                return;

            var visibleObjs = PhysicsObj.ObjMaint.GetVisibleObjects(PhysicsObj.CurCell);

            var targetCreature = AttackTarget as Creature;

            var alerted = false;

            foreach (var obj in visibleObjs)
            {
                var nearbyCreature = obj.WeenieObj.WorldObject as Creature;
                if (nearbyCreature == null || nearbyCreature.IsAwake || !nearbyCreature.Attackable && nearbyCreature.TargetingTactic == TargetingTactic.None)
                    continue;

                if ((nearbyCreature.Tolerance & AlertExclude) != 0)
                    continue;

                if (CreatureType != null && CreatureType == nearbyCreature.CreatureType ||
                      FriendType != null && FriendType == nearbyCreature.CreatureType)
                {
                    //var distSq = Location.SquaredDistanceTo(nearbyCreature.Location);
                    var distSq = PhysicsObj.get_distance_sq_to_object(nearbyCreature.PhysicsObj, true);
                    if (distSq > nearbyCreature.AuralAwarenessRangeSq)
                        continue;

                    // scenario: spawn a faction mob, and then spawn a non-faction mob next to it, of the same CreatureType
                    // the spawning mob will become alerted by the faction mob, and will then go to alert its friendly types
                    // the faction mob happens to be a friendly type, so it in effect becomes alerted to itself
                    // this is to prevent the faction mob from adding itself to its retaliate targets / visible targets,
                    // and setting itself to its AttackTarget
                    if (nearbyCreature == AttackTarget)
                        continue;

                    if (nearbyCreature.SameFaction(targetCreature))
                        nearbyCreature.AddRetaliateTarget(AttackTarget);

                    if (PotentialFoe(targetCreature))
                    {
                        if (nearbyCreature.PotentialFoe(targetCreature))
                            nearbyCreature.AddRetaliateTarget(AttackTarget);
                        else
                            continue;
                    }

                    alerted = true;

                    nearbyCreature.AttackTarget = AttackTarget;
                    nearbyCreature.WakeUp(false);
                }
            }
            // only set alerted if monsters were actually alerted
            if (alerted)
            {
                if (Alerted == null)
                    Alerted = new Dictionary<uint, DateTime>();

                Alerted[AttackTarget.Guid.Full] = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Wakes up a faction monster from any non-faction monsters wandering within range
        /// </summary>
        public void FactionMob_CheckMonsters()
        {
            if (MonsterState != State.Idle) return;

            var creatures = PhysicsObj.ObjMaint.GetVisibleTargetsValuesOfTypeCreature();

            foreach (var creature in creatures)
            {
                // ensure type isn't already handled elsewhere
                if (creature is Player || creature is CombatPet)
                    continue;

                // ensure valid/attackable
                if (creature.IsDead || creature.Teleporting)
                    continue;
                if (!creature.Attackable)
                    continue;
                // Don't skip players based on TargetingTactic - that's for monster behavior, not target validity
                if (creature.TargetingTactic == TargetingTactic.None && !(creature is Player))
                    continue;

                // ensure another faction
                if (SameFaction(creature) && !PotentialFoe(creature))
                    continue;

                // ensure within detection range
                if (PhysicsObj.get_distance_sq_to_object(creature.PhysicsObj, true) > VisualAwarenessRangeSq)
                    continue;

                creature.AlertMonster(this);
                break;
            }
        }
    }
}
