using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Common;
using System;
using System.Diagnostics;
using System.Numerics;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// Return to home if target distance exceeds this range
        /// </summary>
        public static readonly float MaxChaseRange = 96.0f;
        public static readonly float MaxChaseRangeSq = MaxChaseRange * MaxChaseRange;

        /// <summary>
        /// Cache for physics calculations to reduce expensive operations
        /// </summary>
        private float _cachedDistanceToTarget = -1.0f;
        private double _lastDistanceCacheTime = 0.0;
        private const double DISTANCE_CACHE_DURATION = 0.25; // Cache for 0.25 seconds

        // Network Throttling
        private Vector3 _lastNetworkUpdatePos;
        private double _lastNetworkUpdateTime;
        private const double NetworkUpdateInterval = 0.1; // 10Hz limit
        private const float NetworkUpdateMinDistanceSq = 0.05f * 0.05f; // Threshold squared
        private const double NetworkUpdateForceInterval = 0.5; // Heartbeat

        /// <summary>
        /// Invalidate distance cache when target changes
        /// </summary>
        public void InvalidateDistanceCache()
        {
            _cachedDistanceToTarget = -1.0f;
            _lastDistanceCacheTime = 0.0;
        }

        /// <summary>
        /// Cached distance calculation to reduce expensive physics operations
        /// </summary>
        public float GetCachedDistanceToTarget()
        {
            var currentTime = Timers.RunningTime;
            
            // Check if cache is still valid
            if (currentTime - _lastDistanceCacheTime < DISTANCE_CACHE_DURATION && _cachedDistanceToTarget >= 0)
            {
                return _cachedDistanceToTarget;
            }
            
            // Cache expired or invalid, calculate new distance
            var myPhysics = PhysicsObj;
            var target = AttackTarget;
            if (target == null)
            {
                _cachedDistanceToTarget = float.MaxValue;
            }
            else
            {
                var targetPhysics = target.PhysicsObj;
                if (myPhysics == null || targetPhysics == null)
                {
                    _cachedDistanceToTarget = float.MaxValue;
                }
                else
                {
                    _cachedDistanceToTarget = (float)myPhysics.get_distance_to_object(targetPhysics, true);
                }
            }
            
            _lastDistanceCacheTime = currentTime;
            return _cachedDistanceToTarget;
        }

        /// <summary>
        /// Determines if a monster is within melee range of target
        /// </summary>
        //public static readonly float MaxMeleeRange = 1.5f;
        public static readonly float MaxMeleeRange = 0.75f;
        //public static readonly float MaxMeleeRange = 1.5f + 0.6f + 0.1f;    // max melee range + distance from + buffer

        /// <summary>
        /// The maximum range for a monster missile attack
        /// </summary>
        //public static readonly float MaxMissileRange = 80.0f;
        //public static readonly float MaxMissileRange = 40.0f;   // for testing

        /// <summary>
        /// The distance per second from running animation
        /// </summary>
        public float MoveSpeed;

        /// <summary>
        /// The run skill via MovementSystem GetRunRate()
        /// </summary>
        public float RunRate;

        /// <summary>
        /// Flag indicates monster is turning towards target
        /// </summary>
        public bool IsTurning = false;

        /// <summary>
        /// Flag indicates monster is moving towards target
        /// </summary>
        public bool IsMoving = false;

        /// <summary>
        /// The last time a movement tick was processed
        /// </summary>
        public double LastMoveTime;

        public bool DebugMove;

        public double NextMoveTime;
        public double NextCancelTime;

        /// <summary>
        /// Starts the process of monster turning towards target
        /// </summary>
        public void StartTurn()
        {
            //if (Timers.RunningTime < NextMoveTime)
            //return;
            if (!MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - StartTurn, ranged={IsRanged}");

            if (MoveSpeed == 0.0f)
                GetMovementSpeed();

            //Console.WriteLine($"[{Timers.RunningTime}] - {Name} ({Guid}) - starting turn");

            IsTurning = true;

            // send network actions
            var targetDist = GetCachedDistanceToTarget();
            var turnTo = IsRanged || (CurrentAttack == CombatType.Magic && targetDist <= GetSpellMaxRange()) || AiImmobile;
            if (turnTo)
                TurnTo(AttackTarget);
            else
                MoveTo(AttackTarget, RunRate);

            // need turning listener?
            IsTurning = false;
            IsMoving = true;
            LastMoveTime = Timers.RunningTime;
            NextCancelTime = LastMoveTime + ThreadSafeRandom.Next(2, 4);
            moveBit = false;

            var mvp = GetMovementParameters(targetDist);
            if (turnTo)
                PhysicsObj.TurnToObject(AttackTarget.PhysicsObj, mvp);
            else
                PhysicsObj.MoveToObject(AttackTarget.PhysicsObj, mvp);

            // prevent initial snap
            PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;
        }


        /// <summary>
        /// Called when the MoveTo process has completed
        /// </summary>
        public override void OnMoveComplete(WeenieError status)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - OnMoveComplete({status})");

            if (status != WeenieError.None) {
                IsMoving = false;
                return;
            }

            if (AiImmobile && CurrentAttack == CombatType.Melee)
            {
                var targetDist = GetCachedDistanceToTarget();
                if (targetDist > MaxRange)
                    ResetAttack();
            }

            if (MonsterState == State.Return) Sleep();
            PhysicsObj.CachedVelocity = Vector3.Zero;
            IsMoving = false;
        }


        /// <summary>
        /// Estimates the time it will take the monster to turn towards target
        /// </summary>
        public float EstimateTurnTo()
        {
            return GetRotateDelay(AttackTarget);
        }

        /// <summary>
        /// Returns TRUE if monster is within target melee range
        /// </summary>
        public bool IsMeleeRange()
        {
            return GetCachedDistanceToTarget() <= MaxMeleeRange;
        }

        /// <summary>
        /// Returns TRUE if monster in range for current attack type
        /// </summary>
        public bool IsAttackRange()
        {
            return GetCachedDistanceToTarget() <= MaxRange;
        }

        /// <summary>
        /// Gets the distance to target, with radius excluded
        /// </summary>
        public float GetDistanceToTarget()
        {
            if (AttackTarget == null)
                return float.MaxValue;

            var myPhysics = PhysicsObj;
            var targetPhysics = AttackTarget.PhysicsObj;
            if (myPhysics == null || targetPhysics == null)
                return float.MaxValue;

            //var matchIndoors = Location.Indoors == AttackTarget.Location.Indoors;
            //var targetPos = matchIndoors ? AttackTarget.Location.ToGlobal() : AttackTarget.Location.Pos;
            //var sourcePos = matchIndoors ? Location.ToGlobal() : Location.Pos;

            //var dist = (targetPos - sourcePos).Length();
            //var radialDist = dist - (AttackTarget.PhysicsObj.GetRadius() + PhysicsObj.GetRadius());

            // always use spheres?
            var cylDist = (float)Physics.Common.Position.CylinderDistance(myPhysics.GetRadius(), myPhysics.GetHeight(), myPhysics.Position,
                targetPhysics.GetRadius(), targetPhysics.GetHeight(), targetPhysics.Position);

            if (DebugMove)
                Console.WriteLine($"{Name}.DistanceToTarget: {cylDist}");

            //return radialDist;
            return cylDist;
        }


        /// <summary>
        /// Primary movement handler, determines if target in range
        /// </summary>
        public void Movement()
        {
            if (IsDead || PhysicsObj == null || Teleporting)
                return;

            //if (!IsRanged)
                UpdatePosition();

            if (MonsterState == State.Awake && GetDistanceToTarget() >= MaxChaseRange)
            {
                CancelMoveTo();
                FindNextTarget();
                return;
            }

            CheckForStuck();
            CheckDistressCalls();
        }

        /// <summary>
        /// Basic stuck detection system
        /// </summary>
        public void CheckForStuck()
        {
            if (!IsMoving || AiImmobile) return;

            MoveToManager manager = PhysicsObj?.MovementManager?.MoveToManager;
            if (manager == null) return;

            // If the manager is active and we haven't had motion in long enough, we are stuck.
            if (!manager.IsStuck(/*stuckThresholdSeconds=*/ 5.0f)) return;

            CancelMoveTo();
            switch (MonsterState)
            {
                // Stuck active monsters try to find a new target
                // This can trigger in cases like where a monster can't reach its target.
                case State.Awake:
                    FindNextTarget();
                    break;

                // Stuck monsters trying to return home teleport there.
                // This can trigger in cases like where a monster is stuck on a wall running home.
                case State.Return:
                    if (Home != null) DoTeleport(Home);
                    break;

                // Stuck Idle monsters should just stop moving. Likely a race condition.
                case State.Idle:
                    break;

                // Unexpected case, log error.
                default:
                    log.Error($"Monster stuck - unhandled state {Enum.GetName(MonsterState)}");
                    break;
            }

            manager.ResetStuck();
        }

        /// <summary>
        /// Make returning monsters respond to distress calls
        /// </summary>
        public void CheckDistressCalls()
        {
            if (MonsterState == State.Return)
            {
                var objMaint = PhysicsObj?.ObjMaint;
                if (objMaint == null)
                    return;

                // Check for nearby monsters in distress
                var nearbyCreatures = objMaint.GetVisibleTargetsValuesOfTypeCreature();
                
                foreach (var creature in nearbyCreatures)
                {
                    if (creature == null || creature.IsDead || creature == this)
                        continue;

                    // If a nearby monster is in combat, consider responding to distress
                    if (creature.MonsterState == State.Awake && creature.AttackTarget != null)
                    {
                        // Check if the distressed monster is of the same type or faction
                        if (CreatureType != null && CreatureType == creature.CreatureType ||
                            FriendType != null && FriendType == creature.CreatureType ||
                            SameFaction(creature))
                        {
                            var creaturePhysics = creature.PhysicsObj;
                            if (PhysicsObj == null || creaturePhysics == null)
                                continue;

                            var distSq = PhysicsObj.get_distance_sq_to_object(creaturePhysics, true);
                            if (distSq <= AuralAwarenessRangeSq)
                            {
                                // Respond to distress call
                                AttackTarget = creature.AttackTarget;
                                MonsterState = State.Awake;
                                WakeUp(false); // Don't alert others to avoid chain reaction
                                return;
                            }
                        }
                    }
                }
            }
        }

        public void UpdatePosition(bool netsend = true)
        {
            if (IsDead || PhysicsObj == null)
                return;

            stopwatch.Restart();
            PhysicsObj.update_object();
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Monster_Navigation_UpdatePosition_PUO, stopwatch.Elapsed.TotalSeconds);
            UpdatePosition_SyncLocation();

            if (netsend)
            {
                var currentTime = Timers.RunningTime;
                var timeDelta = currentTime - _lastNetworkUpdateTime;

                // Throttling Logic
                if (timeDelta >= NetworkUpdateInterval)
                {
                    bool shouldSend = false;

                    // Force update if it's been a while (keepalive/sync)
                    if (timeDelta >= NetworkUpdateForceInterval)
                    {
                        shouldSend = true;
                    }
                    else
                    {
                        // Check distance moved since last send
                        if (PhysicsObj != null)
                        {
                            var currentPos = PhysicsObj.Position.Frame.Origin;
                            if (Vector3.DistanceSquared(currentPos, _lastNetworkUpdatePos) > NetworkUpdateMinDistanceSq)
                            {
                                shouldSend = true;
                            }
                        }
                    }

                    if (shouldSend)
                    {
                        SendUpdatePosition();
                        _lastNetworkUpdateTime = currentTime;
                        if (PhysicsObj != null)
                            _lastNetworkUpdatePos = PhysicsObj.Position.Frame.Origin;
                    }
                }
            }

            if (DebugMove)
                //Console.WriteLine($"{Name} ({Guid}) - UpdatePosition (velocity: {PhysicsObj.CachedVelocity.Length()})");
                Console.WriteLine($"{Name} ({Guid}) - UpdatePosition: {Location.ToLOCString()}");

            if (PhysicsObj?.MovementManager?.MoveToManager != null)
            {
                if (MonsterState == State.Awake && IsMoving && PhysicsObj.MovementManager.MoveToManager.PendingActions.Count == 0)
                    IsMoving = false;
            }

            if (stopwatch.Elapsed.TotalSeconds > 1)
            {
                log.Error($"Timing: UpdatePosition longer than 1 second {Name}, {Guid}, {this.CurrentLandblock?.Id}, {this.CurrentLandblock.VariationId}");
                log.Error(new StackTrace());
            }
        }

        /// <summary>
        /// Synchronizes the WorldObject Location with the Physics Location
        /// </summary>
        public void UpdatePosition_SyncLocation()
        {
            if (PhysicsObj == null)
                return;

            // was the position successfully moved to?
            // use the physics position as the source-of-truth?
            var newPos = PhysicsObj.Position;

            if (Location.LandblockId.Raw != newPos.ObjCellID)
            {
                var prevBlock = Location.LandblockId.Raw >> 16;
                var newBlock = newPos.ObjCellID >> 16;

                Location.LandblockId = new LandblockId(newPos.ObjCellID);

                if (prevBlock != newBlock)
                {
                    LandblockManager.RelocateObjectForPhysics(this, true);
                }
            }

            // skip ObjCellID check when updating from physics
            // TODO: update to newer version of ACE.Entity.Position
            Location.PositionX = newPos.Frame.Origin.X;
            Location.PositionY = newPos.Frame.Origin.Y;
            Location.PositionZ = newPos.Frame.Origin.Z;

            Location.Rotation = newPos.Frame.Orientation;
        }

        public void GetMovementSpeed()
        {
            var moveSpeed = MotionTable.GetRunSpeed(MotionTableId);
            if (moveSpeed == 0) moveSpeed = 2.5f;
            var scale = ObjScale ?? 1.0f;

            RunRate = GetRunRate();

            // Calculate raw speed (with scale logic)
            MoveSpeed = moveSpeed * RunRate;

            // Cap: never faster than 800 run at 1.0 scale
            var maxMoveSpeed = moveSpeed * (18.0f / 4.0f); // 4.5 is the capped RunRate
            if (MoveSpeed > maxMoveSpeed)
                MoveSpeed = maxMoveSpeed;
        }

        /// <summary>
        /// Returns the RunRate that is sent to the client as myRunRate
        /// </summary>
        public float GetRunRate()
        {
            var burden = 0.0f;

            // assuming burden only applies to players...
            if (this is Player player)
            {
                var strength = Strength.Current;

                var capacity = EncumbranceSystem.EncumbranceCapacity((int)strength, player.AugmentationIncreasedCarryingCapacity);
                burden = EncumbranceSystem.GetBurden(capacity, EncumbranceVal ?? 0);

                // TODO: find this exact formula in client
                // technically this would be based on when the player releases / presses the movement key after stamina > 0
                if (player.IsExhausted)
                    burden = 3.0f;
            }

            var runSkill = GetCreatureSkill(Skill.Run).Current;
            var runRate = MovementSystem.GetRunRate(burden, (int)runSkill, 1.0f);

            return (float)runRate;
        }



        /// <summary>
        /// Returns TRUE if monster is facing towards the target
        /// </summary>
        public bool IsFacing(WorldObject target)
        {
            if (target?.Location == null) return false;

            var angle = GetAngle(target);
            var dist = target == AttackTarget
                ? Math.Max(0, GetCachedDistanceToTarget())
                : (PhysicsObj != null && target?.PhysicsObj != null
                    ? (float)PhysicsObj.get_distance_to_object(target.PhysicsObj, true)
                    : float.MaxValue);

            // rotation accuracy?
            var threshold = 5.0f;

            var minDist = 10.0f;

            if (dist < minDist)
                threshold += (minDist - dist) * 1.5f;

            if (DebugMove)
                Console.WriteLine($"{Name}.IsFacing({target.Name}): Angle={angle}, Dist={dist}, Threshold={threshold}, {angle < threshold}");

            return angle < threshold;
        }

        public MovementParameters GetMovementParameters(float? targetDistance = null)
        {
            var mvp = new MovementParameters();

            // set non-default params for monster movement
            mvp.Flags &= ~MovementParamFlags.CanWalk;

            var distance = targetDistance ?? GetCachedDistanceToTarget();
            var turnTo = IsRanged || (CurrentAttack == CombatType.Magic && distance <= GetSpellMaxRange()) || AiImmobile;

            if (!turnTo)
                mvp.Flags |= MovementParamFlags.FailWalk | MovementParamFlags.UseFinalHeading | MovementParamFlags.Sticky | MovementParamFlags.MoveAway;

            return mvp;
        }

        /// <summary>
        /// The maximum distance a monster can travel outside of its home position
        /// </summary>
        public double? HomeRadius
        {
            get => GetProperty(PropertyFloat.HomeRadius);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.HomeRadius); else SetProperty(PropertyFloat.HomeRadius, value.Value); }
        }

        private static readonly float DefaultHomeRadius = 192.0f;
        //public static float DefaultHomeRadiusSq = DefaultHomeRadius * DefaultHomeRadius;

        private float? homeRadiusSq;

        public float HomeRadiusSq
        {
            get
            {
                if (homeRadiusSq == null)
                {
                    var homeRadius = HomeRadius ?? DefaultHomeRadius;
                    homeRadiusSq = (float)(homeRadius * homeRadius);
                }
                return homeRadiusSq.Value;
            }
        }

        public void CheckMissHome()
        {
            if (Home == null) return;
            if (MonsterState == State.Return) return;
            var homeDistSq = Vector3.DistanceSquared(Home.ToGlobal(), Location.ToGlobal());
            if (homeDistSq > HomeRadiusSq) MoveToHome();
        }

        /// <summary>
        /// Initiates the Return Home behavior for a monster.
        /// Turns on State.Return, clears target, and moves towards Home position.
        /// </summary>
        public void MoveToHome()
        {
            if (DebugMove) Console.WriteLine($"{Name}.MoveToHome()");

            if (Home == null) return;
            if (Location.Equals(Home))
            {
                Sleep();
                return;
            }

            var prevAttackTarget = AttackTarget;
            MonsterState = State.Return;
            AttackTarget = null;
            NextCancelTime = Timers.RunningTime + 5.0f;
            MoveTo(Home, RunRate, false, 1.0f);
            var homePos = new Physics.Common.Position(Home);
            var mvp = GetMovementParameters();
            mvp.DistanceToObject = 0.6f;
            mvp.DesiredHeading = homePos.Frame.get_heading();
            PhysicsObj.MoveToPosition(homePos, mvp);
            IsMoving = true;

            EmoteManager.OnHomeSick(prevAttackTarget);
        }

        /// <summary>
        /// Cancels current movement.
        /// If returning home, forces a teleport to home.
        /// Otherwise slows/stops the creature and resets attack/target state.
        /// </summary>
        public void CancelMoveTo()
        {
            if (IsDead || PhysicsObj == null)
            {
                IsMoving = false;
                NextMoveTime = Timers.RunningTime + 1.0f;
                return;
            }
            
            PhysicsObj?.MovementManager?.MoveToManager?.CancelMoveTo(WeenieError.ActionCancelled);

            if (MonsterState == State.Return)
            {
                if (Home != null) DoTeleport(Home);
                return;
            }

            EnqueueBroadcastMotion(new Motion(CurrentMotionState.Stance, MotionCommand.Ready));

            IsMoving = false;
            NextMoveTime = Timers.RunningTime + 1.0f;

            ResetAttack();
        }

        /// <summary>
        /// Executes the full Monster teleport sequence (Leash/Recall).
        /// Plays fade-out effects, waits, teleports, then plays fade-in effects and sleeps.
        /// </summary>
        public void DoTeleport(ACE.Entity.Position destination)
        {
            if (IsDead || PhysicsObj == null || Teleporting) return;

            // Even though Teleport() also sets this to true, we set
            // it early to block effects during our animation.
            Teleporting = true;

            // Fade out, then enqueue the other things.
            PlayParticleEffect(PlayScript.Destroy, Guid);
            PlayParticleEffect(PlayScript.DispelLife, Guid);

            // Create an ActionChain to enqueue the rest of the steps.
            // 1. Wait for creature to become invisible from PlayScript.Destroy.
            // 2. Teleport the creature to its destination.
            // 3. Wait for the creature and its physics object to move.
            // 4. Spawn (PlayScript.Create) and reset. 
            new ActionChain()
                .AddDelaySeconds(3.0f)
                .AddAction(this, ActionType.CreatureLocation_TeleportToPosition, () => 
                {
                    if (IsDead || PhysicsObj == null) return;
                    Teleport(destination); 
                })
                .AddDelaySeconds(1.5f)
                .AddAction(this, ActionType.CreatureLocation_TeleportDone, () =>
                {
                    OnTeleportComplete();
                    if (!IsDead && PhysicsObj != null)
                    {
                        PlayParticleEffect(PlayScript.Create, Guid);
                        EnqueueBroadcastMotion(new Motion(MotionStance.NonCombat, MotionCommand.Ready));
                        ResetAttack();
                        Sleep();
                    }
                })
                .EnqueueChain();
        }
    }
}
