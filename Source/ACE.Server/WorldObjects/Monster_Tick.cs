using System;
using System.Diagnostics;

using ACE.Entity.Enum;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        protected const double monsterTickInterval = 0.3;

        public double NextMonsterTickTime;

        /// <summary>
        /// Fixed desync offset per creature to spread tick processing across time
        /// Set once at spawn, maintained forever to ensure consistent tick rate
        /// -1 indicates uninitialized, will be set to 0-0.6s on first tick
        /// </summary>
        private double monsterTickDesyncOffset = -1;

        private bool firstUpdate = true;

        /// <summary>
        /// Determines if an idle monster can skip full AI processing
        /// Returns true only if: no target, idle state, and hasn't been damaged
        /// Note: Only called after !IsAwake early return, so IsAwake is always true here
        /// Ensures monsters still respond to attacks and proximity aggro
        /// </summary>
        private bool ShouldSkipIdleMonsterTick()
        {
            return AttackTarget == null && 
                   MonsterState == State.Idle && 
                   DamageHistory.IsEmpty;
        }

        /// <summary>
        /// Primary dispatch for monster think
        /// </summary>
        public void Monster_Tick(double currentUnixTime)
        {
            if (IsChessPiece && this is GamePiece gamePiece)
            {
                // faster than vtable?
                gamePiece.Tick(currentUnixTime);
                return;
            }

            if (IsPassivePet && this is Pet pet)
            {
                pet.Tick(currentUnixTime);
                return;
            }

            // Initialize desync offset on first tick only to spread spawns across time
            // After first tick, use standard interval to maintain consistent tick rate
            if (monsterTickDesyncOffset < 0)
            {
                // First tick: Apply desync offset to stagger initial spawn timing
                monsterTickDesyncOffset = Common.ThreadSafeRandom.Next(0.0f, (float)(monsterTickInterval * 2));
                NextMonsterTickTime = currentUnixTime + monsterTickInterval + monsterTickDesyncOffset;
                monsterTickDesyncOffset = 0; // Mark as applied, use standard interval from now on
            }
            else
            {
                // Subsequent ticks: Use standard interval (offset is maintained via currentUnixTime)
                NextMonsterTickTime = currentUnixTime + monsterTickInterval;
            }

            if (!IsAwake)
            {
                if (IsFactionMob || HasFoeType)
                    FactionMob_CheckMonsters();

                // FactionMob_CheckMonsters ignores players; extended foe mobs still need idle proximity wake when the player is standing still
                if (HasFoeType && UsesExtendedFoeTargeting)
                    ExtendedFoeWakeFromProximity();

                return;
            }

            if (IsDead) return;

            // Engaged combat pets skip Pet.SlowTick; apply the same max owner distance despawn as idle pets (~1 Hz).
            // Include State.Return (pet walking home with no AttackTarget) so returning pets are not exempt from despawn.
            if (this is CombatPet engagedRangePet
                && (engagedRangePet.AttackTarget != null || engagedRangePet.MonsterState == State.Return)
                && engagedRangePet.TryDespawnIfOwnerBeyondMaxFollowThrottled(currentUnixTime))
                return;

            if (EmoteManager.IsBusy) return;

            // Owner leash while engaged: monster AI never runs Pet follow while AttackTarget is set; if the owner walks away, drop target so idle follow runs.
            if (this is CombatPet ownerLeashPet
                && ServerConfig.pet_combat_follow_owner_when_idle.Value
                && ServerConfig.pet_combat_owner_recall_distance_m.Value > 0
                && ownerLeashPet.AttackTarget != null
                && ownerLeashPet.P_PetOwner?.PhysicsObj != null
                && ownerLeashPet.GetCylinderDistance(ownerLeashPet.P_PetOwner) > (float)ServerConfig.pet_combat_owner_recall_distance_m.Value)
            {
                if (ownerLeashPet.IsOwnerFollowRecallBlocked())
                {
                    CombatPet.TraceRecallBlockStatic(ownerLeashPet, "Monster_Tick.owner_leash_suppressed",
                        $"ownerDist>{ServerConfig.pet_combat_owner_recall_distance_m.Value:F0}m recall_blocked_after_damage");
                }
                else
                {
                    ownerLeashPet.AttackTarget = null;
                    ownerLeashPet.ResetAttack();
                    ((Pet)ownerLeashPet).Tick(currentUnixTime);
                    HandleFindTarget();
                    return;
                }
            }

            // Hard leash radius (independent of idle-follow toggle): if configured, drop target when pet strays too far from owner.
            if (this is CombatPet hardLeashPet
                && ServerConfig.pet_combat_leash_radius_m.Value > 0
                && hardLeashPet.AttackTarget != null
                && hardLeashPet.P_PetOwner?.PhysicsObj != null
                && hardLeashPet.GetCylinderDistance(hardLeashPet.P_PetOwner) > (float)ServerConfig.pet_combat_leash_radius_m.Value)
            {
                hardLeashPet.AttackTarget = null;
                hardLeashPet.ResetAttack();
                ((Pet)hardLeashPet).Tick(currentUnixTime);
                return;
            }

            // Idle combat pets: use the same Pet.Tick path as passive pets (physics cadence + SlowTick recall),
            // then only run target search. Without this, idle undamaged pets hit ShouldSkipIdleMonsterTick and
            // never reach follow logic; physics also diverged from passive Pet.Tick.
            if (this is CombatPet combatPetRecall
                && ServerConfig.pet_combat_follow_owner_when_idle.Value
                && AttackTarget == null
                && MonsterState != State.Return)
            {
                ((Pet)combatPetRecall).Tick(currentUnixTime);
                HandleFindTarget();
                return;
            }

            // Optimization: Skip AI tick for idle monsters with no targets nearby
            // Reduces CPU load in high-density landblocks significantly
            // Impact: ~80% CPU reduction in landblocks with 400+ creatures (320 idle, 80 active)
            // Critical: Must check DamageHistory.IsEmpty to ensure damaged monsters respond to attacks
            // Player proximity aggro handled by Player.CheckMonsters() on movement (instant response)
            if (ShouldSkipIdleMonsterTick())
            {
                // Still check occasionally if we should wake up (proximity-based aggro)
                // HandleFindTarget() already checks NextFindTarget timer internally (every 5 seconds)
                HandleFindTarget();
                return;
            }

            // If the current attack target became a friendly-quest ally this tick (stamp was just gained),
            // drop combat before HandleFindTarget runs — otherwise it could immediately re-select the
            // same player as AttackTarget on this same tick.
            if (TryBreakOffAttackIfFriendlyQuestAlly())
                return;

            HandleFindTarget();

            CheckMissHome();

            if (AttackTarget == null && MonsterState != State.Return)
            {
                if (this is CombatPet homeCombatPet && ServerConfig.pet_combat_follow_owner_when_idle.Value)
                {
                    ((Pet)homeCombatPet).Tick(currentUnixTime);
                    HandleFindTarget();
                    return;
                }
                MoveToHome();
                return;
            }

            if (MonsterState == State.Return)
            {
                Movement();
                return;
            }

            var combatPet = this as CombatPet;

            var creatureTarget = AttackTarget as Creature;

            // Drop invalid target if it's a player with CloakStatus.Creature
            if (creatureTarget is Player playerTarget && playerTarget.CloakStatus == CloakStatus.Creature)
            {
                // Stop considering this player as a valid target
                AttackTarget = null;

                // Try to acquire a new target (or return home).
                FindNextTarget();

                return;
            }

            if (creatureTarget != null && creatureTarget.IsDead)
            {
                if (HasRetaliateTarget(creatureTarget))
                    RemoveRetaliateTarget(creatureTarget);
                AttackTarget = null;
                InvalidateTargetCaches();
                FindNextTarget();
                return;
            }

            // Custom mob-to-mob targeting can briefly lag visibility table updates after spawn/idle transitions.
            // If the current non-player foe is valid, pin it via retaliate so we don't bounce Awake<->Idle.
            if (creatureTarget != null && combatPet == null && !IsVisibleTarget(creatureTarget))
            {
                if (UsesExtendedFoeTargeting
                    && creatureTarget is not Player
                    && (PotentialFoe(creatureTarget) || AllowFactionCombat(creatureTarget)))
                    AddRetaliateTarget(creatureTarget);

                if (!IsVisibleTarget(creatureTarget))
                {
                    FindNextTarget();
                    return;
                }
            }

            if (firstUpdate)
            {
                if (CurrentMotionState == null)
                {
                    log.Warn($"[Monster_Tick] 0x{Guid} {Name} has a null CurrentMotionState setting to NonCombat");
                    CurrentMotionState = new ACE.Server.Entity.Motion(MotionStance.NonCombat, MotionCommand.Ready);
                }
                if (CurrentMotionState.Stance == MotionStance.NonCombat)
                    DoAttackStance();

                if (IsAnimating)
                {
                    //PhysicsObj.ShowPendingMotions();
                    PhysicsObj.update_object();
                    return;
                }

                firstUpdate = false;
            }

            // select a new weapon if missile launcher is out of ammo
            var weapon = GetEquippedWeapon();
            /*if (weapon != null && weapon.IsAmmoLauncher)
            {
                var ammo = GetEquippedAmmo();
                if (ammo == null)
                    SwitchToMeleeAttack();
            }*/

            if (weapon == null && CurrentAttack != null && CurrentAttack == CombatType.Missile)
            {
                EquipInventoryItems(true);
                DoAttackStance();
                CurrentAttack = null;
            }

            // decide current type of attack
            if (CurrentAttack == null)
            {
                CurrentAttack = GetNextAttackType();
                MaxRange = GetMaxRange();

                //if (CurrentAttack == AttackType.Magic)
                //MaxRange = MaxMeleeRange;   // FIXME: server position sync
            }

            if (PhysicsObj?.IsSticky == true)
                UpdatePosition(false);

            // get distance to target
            var targetDist = GetDistanceToTarget();
            //Console.WriteLine($"{Name} ({Guid}) - Dist: {targetDist}");

            if (CurrentAttack != CombatType.Missile)
            {
                if (targetDist > MaxRange || (!IsFacing(AttackTarget) && !IsSelfCast()))
                {
                    // turn / move towards
                    if (!IsTurning && !IsMoving)
                        StartTurn();
                    else
                        Movement();
                }
                else
                {
                    // perform attack
                    if (AttackReady())
                        Attack();
                }
            }
            else
            {
                if (IsTurning || IsMoving)
                {
                    Movement();
                    return;
                }

                if (!IsFacing(AttackTarget))
                {
                    StartTurn();
                }
                else if (targetDist <= MaxRange)
                {
                    // perform attack
                    if (AttackReady())
                        Attack();
                }
                else
                {
                    // monster switches to melee combat immediately,
                    // if target is beyond max range?

                    // should ranged mobs only get CurrentTargets within MaxRange?
                    //Console.WriteLine($"{Name}.MissileAttack({AttackTarget.Name}): targetDist={targetDist}, MaxRange={MaxRange}, switching to melee");
                    TrySwitchToMeleeAttack();
                }
            }

            // pets drawing aggro
            if (combatPet != null)
                combatPet.PetCheckMonsters();
        }
    }
}
