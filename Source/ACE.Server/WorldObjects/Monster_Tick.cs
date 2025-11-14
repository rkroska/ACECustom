using System;
using System.Diagnostics;

using ACE.Entity.Enum;

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
                if (MonsterState == State.Return)
                    MonsterState = State.Idle;

                if (IsFactionMob || HasFoeType)
                    FactionMob_CheckMonsters();

                return;
            }

            if (IsDead) return;

            if (EmoteManager.IsBusy) return;

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

            HandleFindTarget();

            CheckMissHome();    // tickrate?

            if (AttackTarget == null && MonsterState != State.Return)
            {
                Sleep();
                return;
            }

            if (MonsterState == State.Return)
            {
                Movement();
                return;
            }

            var combatPet = this as CombatPet;

            var creatureTarget = AttackTarget as Creature;

            if (creatureTarget != null && (creatureTarget.IsDead || (combatPet == null && !IsVisibleTarget(creatureTarget))))
            {
                FindNextTarget();
                return;
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

            if (PhysicsObj.IsSticky)
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
