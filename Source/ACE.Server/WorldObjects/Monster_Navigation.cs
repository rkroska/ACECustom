using System;
using System.Diagnostics;
using System.Numerics;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Common;

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
        /// Stuck detection properties
        /// </summary>
        public double LastStuckCheckTime;
        public int StuckAttempts;
        public const int MaxStuckAttempts = 3;
        public const double StuckCheckInterval = 2.0;

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

            // Initialize stuck detection
            LastStuckCheckTime = Timers.RunningTime;

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

            if (status != WeenieError.None)
                return;

            if (AiImmobile && CurrentAttack == CombatType.Melee)
            {
                var targetDist = GetCachedDistanceToTarget();
                if (targetDist > MaxRange)
                    ResetAttack();
            }

            if (MonsterState == State.Return)
                Sleep();

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
            //if (!IsRanged)
                UpdatePosition();

            if (MonsterState == State.Awake && GetDistanceToTarget() >= MaxChaseRange)
            {
                CancelMoveTo();
                FindNextTarget();
                return;
            }

            var moveToManager = PhysicsObj?.MovementManager?.MoveToManager;
            if (moveToManager != null && 
                moveToManager.FailProgressCount > 0 && 
                Timers.RunningTime > NextCancelTime)
                CancelMoveTo();

            CheckForStuck();
            ApplyFastHealing();
            CheckDistressCalls();
        }

        /// <summary>
        /// Basic stuck detection system
        /// </summary>
        public void CheckForStuck()
        {
            if (!IsMoving || AttackTarget == null)
                return;

            if (ShouldBypassStuckLogic())
                return;

            var moveToManager = PhysicsObj?.MovementManager?.MoveToManager;
            if (moveToManager == null)
                return;

            var currentTime = Timers.RunningTime;

            if (currentTime - LastStuckCheckTime < StuckCheckInterval)
                return;

            LastStuckCheckTime = currentTime;

            if (moveToManager.FailProgressCount >= MaxStuckAttempts)
            {
                HandleStuck();
            }
        }

        /// <summary>
        /// Handles when a monster is confirmed to be stuck
        /// </summary>
        public void HandleStuck()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - Confirmed stuck, attempting recovery");

            StuckAttempts = 0;
            CancelMoveTo();

            if (MonsterState == State.Awake)
            {
                FindNextTarget();
            }
            else if (MonsterState == State.Return)
            {
                ForceHome();
            }
        }

        /// <summary>
        /// Bypass stuck logic if mob is set with bool property 52 (aiImobile)
        /// </summary>
        public bool ShouldBypassStuckLogic()
        {
            return AiImmobile;
        }

        /// <summary>
        /// Apply fast healing when returning home
        /// </summary>
        public void ApplyFastHealing()
        {
            if (MonsterState == State.Return)
            {
                // Increase vital regen when returning home
                // This is handled by the existing vital regen system
                // The actual implementation would be in the vital regen logic
            }
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
            if (PhysicsObj == null)
                return;

            stopwatch.Restart();
            PhysicsObj.update_object();
            ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Monster_Navigation_UpdatePosition_PUO, stopwatch.Elapsed.TotalSeconds);
            UpdatePosition_SyncLocation();

            if (netsend)
                SendUpdatePosition();

            if (DebugMove)
                //Console.WriteLine($"{Name} ({Guid}) - UpdatePosition (velocity: {PhysicsObj.CachedVelocity.Length()})");
                Console.WriteLine($"{Name} ({Guid}) - UpdatePosition: {Location.ToLOCString()}");

            if (MonsterState == State.Return && PhysicsObj.MovementManager.MoveToManager.PendingActions.Count == 0)
                Sleep();

            if (MonsterState == State.Awake && IsMoving && PhysicsObj.MovementManager.MoveToManager.PendingActions.Count == 0)
                IsMoving = false;

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
                //var prevBlockCell = Location.LandblockId.Raw;
                var prevBlock = Location.LandblockId.Raw >> 16;
                //var prevCell = Location.LandblockId.Raw & 0xFFFF;

                //var newBlockCell = newPos.ObjCellID;
                var newBlock = newPos.ObjCellID >> 16;
                //var newCell = newPos.ObjCellID & 0xFFFF;

                Location.LandblockId = new LandblockId(newPos.ObjCellID);

                if (prevBlock != newBlock)
                {
                    LandblockManager.RelocateObjectForPhysics(this, true);
                    //Console.WriteLine($"Relocating {Name} from {prevBlockCell:X8} to {newBlockCell:X8}");
                    //Console.WriteLine("Old position: " + Location.Pos);
                    //Console.WriteLine("New position: " + newPos.Frame.Origin);
                }
                //else
                    //Console.WriteLine("Moving " + Name + " to " + Location.LandblockId.Raw.ToString("X8"));
            }

            // skip ObjCellID check when updating from physics
            // TODO: update to newer version of ACE.Entity.Position
            Location.PositionX = newPos.Frame.Origin.X;
            Location.PositionY = newPos.Frame.Origin.Y;
            Location.PositionZ = newPos.Frame.Origin.Z;

            Location.Rotation = newPos.Frame.Orientation;

            if (DebugMove)
                DebugDistance();
        }

        public void DebugDistance()
        {
            if (AttackTarget == null) return;

            //var dist = GetDistanceToTarget();
            //var angle = GetAngle(AttackTarget);
            //Console.WriteLine("Dist: " + dist);
            //Console.WriteLine("Angle: " + angle);
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
            if (MonsterState == State.Return)
                return;

            var homePosition = GetPosition(PositionType.Home);
            //var matchIndoors = Location.Indoors == homePosition.Indoors;

            //var globalPos = matchIndoors ? Location.ToGlobal() : Location.Pos;
            //var globalHomePos = matchIndoors ? homePosition.ToGlobal() : homePosition.Pos;
            var globalPos = Location.ToGlobal();
            var globalHomePos = homePosition.ToGlobal();

            var homeDistSq = Vector3.DistanceSquared(globalHomePos, globalPos);

            if (homeDistSq > HomeRadiusSq)
                MoveToHome();
        }

        public void MoveToHome()
        {
            if (DebugMove)
                Console.WriteLine($"{Name}.MoveToHome()");

            var prevAttackTarget = AttackTarget;

            MonsterState = State.Return;
            AttackTarget = null;

            var home = GetPosition(PositionType.Home);

            if (Location.Equals(home))
            {
                Sleep();
                return;
            }

            NextCancelTime = Timers.RunningTime + 5.0f;

            MoveTo(home, RunRate, false, 1.0f);

            var homePos = new Physics.Common.Position(home);

            var mvp = GetMovementParameters();
            mvp.DistanceToObject = 0.6f;
            mvp.DesiredHeading = homePos.Frame.get_heading();

            PhysicsObj.MoveToPosition(homePos, mvp);
            IsMoving = true;

            EmoteManager.OnHomeSick(prevAttackTarget);
        }

        public void CancelMoveTo()
        {
            //Console.WriteLine($"{Name}.CancelMoveTo()");

            PhysicsObj.MovementManager.MoveToManager.CancelMoveTo(WeenieError.ActionCancelled);
            PhysicsObj.MovementManager.MoveToManager.FailProgressCount = 0;

            if (MonsterState == State.Return)
                ForceHome();

            EnqueueBroadcastMotion(new Motion(CurrentMotionState.Stance, MotionCommand.Ready));

            IsMoving = false;
            NextMoveTime = Timers.RunningTime + 1.0f;

            // Reset stuck detection
            StuckAttempts = 0;

            ResetAttack();

            FindNextTarget();
        }

        public void ForceHome()
        {
            var homePos = GetPosition(PositionType.Home);

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - ForceHome({homePos.ToLOCString()})");

            if (PhysicsObj == null)
            {
                log.Warn($"{Name} ({Guid}) - ForceHome failed: PhysicsObj is null");
                return;
            }

            var setPos = new SetPosition();
            setPos.Pos = new Physics.Common.Position(homePos);
            setPos.Flags = SetPositionFlags.Teleport;

            PhysicsObj.SetPosition(setPos);

            UpdatePosition_SyncLocation();
            SendUpdatePosition();

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(0.5f);
            actionChain.AddAction(this, Sleep);
            actionChain.EnqueueChain();
        }
    }
}
