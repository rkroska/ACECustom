using System;
using System.Collections.Generic;
using System.Numerics;

using log4net;

using ACE.Common;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Physics.Animation;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// A passive summonable creature
    /// </summary>
    public class Pet : Creature
    {
        public Player P_PetOwner;

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Pet(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Pet(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            // Solid collision so pets respect doors and world geometry like normal creatures (ethereal walked through doors).
            Ethereal = false;
            RadarBehavior = ACE.Entity.Enum.RadarBehavior.ShowNever;
            ItemUseable = Usable.No;

            SuppressGenerateEffect = true;
        }

        public virtual bool? Init(Player player, PetDevice petDevice)
        {
            var result = HandleCurrentActivePet(player);

            if (result == null || !result.Value)
                return result;

            // get physics radius of player and pet
            var playerRadius = player.PhysicsObj.GetPhysicsRadius();
            var petRadius = GetPetRadius();

            var spawnDist = playerRadius + petRadius + PetFollowMinDistance;

            if (IsPassivePet)
            {
                Location = player.Location.InFrontOf(spawnDist, true);

                TimeToRot = -1;
            }
            else
            {
                Location = player.Location.InFrontOf(spawnDist, false);
            }

            Location.LandblockId = new LandblockId(Location.GetCell());

            Name = player.Name + "'s " + Name;

            PetOwner = player.Guid.Full;
            P_PetOwner = player;

            // All pets don't leave corpses, this maybe should have been in data, but isn't so lets make sure its true.
            NoCorpse = true;

            var success = EnterWorld();

            if (!success)
            {
                player.SendTransientError($"Couldn't spawn {Name}");
                return false;
            }

            player.CurrentActivePet = this;

            // Used by Pet.Tick / SlowTick for owner follow (passive pets and combat pets when idle-follow is enabled).
            nextSlowTickTime = Time.GetUnixTime();

            return true;
        }

        public bool? HandleCurrentActivePet(Player player)
        {
            if (ServerConfig.pet_stow_replace.Value)
                return HandleCurrentActivePet_Replace(player);
            else
                return HandleCurrentActivePet_Retail(player);
        }

        public bool HandleCurrentActivePet_Replace(Player player)
        {
            // original ace logic
            if (player.CurrentActivePet == null)
                return true;

            if (player.CurrentActivePet is CombatPet combatPetReplace)
            {
                // Same creature: stow only (no resummon this activation), like passive pets. Different creature: despawn old and continue Init.
                if (CombatPet.TryDenyOwnerStowFromRecallBlock(player, combatPetReplace, "HandleCurrentActivePet_Replace"))
                    return false;

                var stowSameCombatPet = WeenieClassId == combatPetReplace.WeenieClassId;
                player.CurrentActivePet.Destroy();
                return !stowSameCombatPet;
            }

            var stowPet = WeenieClassId == player.CurrentActivePet.WeenieClassId;

            // despawn passive pet
            player.CurrentActivePet.Destroy();

            return !stowPet;
        }

        public bool? HandleCurrentActivePet_Retail(Player player)
        {
            if (player.CurrentActivePet == null)
                return true;

            if (IsPassivePet)
            {
                // using a passive pet device
                // stow currently active passive/combat pet, as per retail
                // spawning the new passive pet requires another double click
                if (player.CurrentActivePet is CombatPet combatPetPassiveStow
                    && CombatPet.TryDenyOwnerStowFromRecallBlock(player, combatPetPassiveStow, "HandleCurrentActivePet_Retail.passive_essence_stow"))
                    return false;

                player.CurrentActivePet.Destroy();
            }
            else
            {
                // using a combat pet device
                if (player.CurrentActivePet is CombatPet combatPetEssenceStow)
                {
                    // QoL: using a combat pet essence/device a second time should stow the active combat pet,
                    // similar to how passive pet devices behave on reuse.
                    if (CombatPet.TryDenyOwnerStowFromRecallBlock(player, combatPetEssenceStow, "HandleCurrentActivePet_Retail.combat_essence_stow"))
                        return false;

                    // Same creature template: stow only (no resummon this activation). Different template: despawn and continue Init so another essence can replace on one click.
                    var sameCreature = WeenieClassId == combatPetEssenceStow.WeenieClassId;
                    player.CurrentActivePet.Destroy();
                    return !sameCreature;
                }
                else
                {
                    // Stow passive pet so this summon can proceed on the same activation (retail often required a second click; same-click replace avoids bogus cooldown/structure when activation cooldown is enabled).
                    player.CurrentActivePet.Destroy();

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Called 5x per second for passive pets
        /// </summary>
        public void Tick(double currentUnixTime)
        {
            NextMonsterTickTime = currentUnixTime + monsterTickInterval;

            if (IsMoving)
            {
                PhysicsObj.update_object();

                UpdatePosition_SyncLocation();

                SendUpdatePosition();
            }

            if (currentUnixTime >= nextSlowTickTime)
                SlowTick(currentUnixTime);
        }

        private static readonly double slowTickSeconds = 1.0;
        private double nextSlowTickTime;

        /// <summary>
        /// Engaged combat pets skip <see cref="SlowTick"/>; max-follow despawn is evaluated here at the same ~1 Hz as idle pets.
        /// </summary>
        private double _nextEngagedOwnerFollowDespawnCheckUnix;

        /// <summary>
        /// Destroy this pet if the owner is missing/invalid or farther than <see cref="ServerConfig.pet_owner_max_follow_distance_m"/> (same rule as idle <see cref="SlowTick"/>).
        /// </summary>
        /// <returns>True if this object was destroyed.</returns>
        protected bool TryDestroyIfBeyondOwnerMaxFollowDistance()
        {
            if (P_PetOwner?.PhysicsObj == null)
            {
                log.Error($"{Name} ({Guid}).TryDestroyIfBeyondOwnerMaxFollowDistance() - P_PetOwner: {P_PetOwner}, P_PetOwner.PhysicsObj: {P_PetOwner?.PhysicsObj}");
                Destroy();
                return true;
            }

            var dist = GetCylinderDistance(P_PetOwner);

            if (dist > PetFollowMaxDistanceMeters)
            {
                Destroy();
                return true;
            }

            return false;
        }

        /// <summary>
        /// While <see cref="Creature.AttackTarget"/> is set, <see cref="Monster_Tick"/> does not run <see cref="SlowTick"/>;
        /// call this at ~1 Hz so max-distance despawn matches idle pets.
        /// </summary>
        internal bool TryDespawnIfOwnerBeyondMaxFollowThrottled(double currentUnixTime)
        {
            if (currentUnixTime < _nextEngagedOwnerFollowDespawnCheckUnix)
                return false;

            _nextEngagedOwnerFollowDespawnCheckUnix = currentUnixTime + slowTickSeconds;

            return TryDestroyIfBeyondOwnerMaxFollowDistance();
        }

        /// <summary>
        /// Called 1x per second
        /// </summary>
        public void SlowTick(double currentUnixTime)
        {
            //Console.WriteLine($"{Name}.HeartbeatStatic({currentUnixTime})");

            nextSlowTickTime += slowTickSeconds;

            if (TryDestroyIfBeyondOwnerMaxFollowDistance())
                return;

            var dist = GetCylinderDistance(P_PetOwner);

            if (!IsMoving && dist > PetFollowMinDistance)
            {
                if (this is CombatPet combatPet && combatPet.IsOwnerFollowRecallBlocked())
                {
                    CombatPet.TraceRecallBlockStatic(combatPet, "SlowTick.skip_StartFollowPetOwner", $"dist={dist:F1}m (>{PetFollowMinDistance})");
                    return;
                }

                StartFollowPetOwner();
            }
        }

        // if the passive pet is between min-max distance to owner,
        // it will turn and start running torwards its owner

        protected const float PetFollowMinDistance = 2.0f;

        /// <summary>
        /// Max distance from owner before auto-despawn; see <see cref="ServerConfig.pet_owner_max_follow_distance_m"/>. Values &lt;= 0 in config clamp to 1m.
        /// </summary>
        protected float PetFollowMaxDistanceMeters => (float)Math.Max(1.0, ServerConfig.pet_owner_max_follow_distance_m.Value);

        /// <summary>
        /// Client motion for following the owner. Passive pets always use this; combat pets use it for
        /// idle follow so animation matches physics (monster MoveToObject uses different MotionParams and skids).
        /// </summary>
        private void BroadcastFollowOwnerMotion(WorldObject target)
        {
            if (MoveSpeed == 0.0f)
                GetMovementSpeed();

            var motion = new Motion(this, target, MovementType.MoveToObject);

            motion.MoveToParameters.MovementParameters |= MovementParams.CanCharge;
            motion.MoveToParameters.DistanceToObject = PetFollowMinDistance;
            motion.MoveToParameters.WalkRunThreshold = 0.0f;

            motion.RunRate = RunRate;

            CurrentMotionState = motion;

            EnqueueBroadcastMotion(motion);
        }

        /// <summary>
        /// Begin running toward <see cref="P_PetOwner"/> (shared by passive SlowTick and combat-pet idle follow).
        /// </summary>
        protected void StartFollowPetOwner()
        {
            //Console.WriteLine($"{Name}.StartFollowPetOwner()");

            IsMoving = true;

            BroadcastFollowOwnerMotion(P_PetOwner);

            var mvp = new MovementParameters();
            mvp.DistanceToObject = PetFollowMinDistance;
            mvp.WalkRunThreshold = 0.0f;

            PhysicsObj.MoveToObject(P_PetOwner.PhysicsObj, mvp);

            // prevent snap forward
            PhysicsObj.UpdateTime = Physics.Common.PhysicsTimer.CurrentTime;
        }

        /// <summary>
        /// Broadcasts passive pet movement to clients
        /// </summary>
        public override void MoveTo(WorldObject target, float runRate = 1.0f)
        {
            if (!IsPassivePet)
            {
                base.MoveTo(target, runRate);
                return;
            }

            BroadcastFollowOwnerMotion(target);
        }

        /// <summary>
        /// Called when the MoveTo process has completed
        /// </summary>
        public override void OnMoveComplete(WeenieError status)
        {
            //Console.WriteLine($"{Name}.OnMoveComplete({status})");

            if (!IsPassivePet)
            {
                base.OnMoveComplete(status);
                return;
            }

            if (status != WeenieError.None)
                return;

            PhysicsObj.CachedVelocity = Vector3.Zero;
            IsMoving = false;
        }

        public static Dictionary<uint, float> PetRadiusCache = new Dictionary<uint, float>();

        private float GetPetRadius()
        {
            if (PetRadiusCache.TryGetValue(WeenieClassId, out var radius))
                return radius;

            var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(SetupTableId);

            var scale = ObjScale ?? 1.0f;

            return ProjectileRadiusCache[WeenieClassId] = setup.Spheres[0].Radius * scale;
        }
    }
}
