using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;
using ACE.Server.Physics.Extensions;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public float ReloadMissileAmmo(ActionChain actionChain = null)
        {
            var weapon = GetEquippedMissileWeapon();
            var ammo = GetEquippedAmmo();

            if (weapon == null || ammo == null) return 0.0f;

            var newChain = actionChain == null;
            if (newChain)
                actionChain = new ActionChain();

            var animLength = 0.0f;
            if (weapon.IsAmmoLauncher)
            {
                var animSpeed = GetAnimSpeed();
                //Console.WriteLine($"AnimSpeed: {animSpeed}");

                animLength = EnqueueMotionPersist(actionChain, MotionCommand.Reload, animSpeed);   // start pulling out next arrow
                EnqueueMotionPersist(actionChain, MotionCommand.Ready);    // finish reloading
            }

            // ensure ammo visibility for players
            actionChain.AddAction(this, () =>
            {
                if (CombatMode != CombatMode.Missile)
                    return;

                EnqueueActionBroadcast(p => p.TrackEquippedObject(this, ammo));

                var delayChain = new ActionChain();
                delayChain.AddDelaySeconds(0.001f);     // ensuring this message gets sent after player broadcasts above...
                delayChain.AddAction(this, () =>
                {
                    EnqueueBroadcast(new GameMessageParentEvent(this, ammo, ACE.Entity.Enum.ParentLocation.RightHand, ACE.Entity.Enum.Placement.RightHandCombat));
                });
                delayChain.EnqueueChain();
            });

            if (newChain)
                actionChain.EnqueueChain();

            var animLength2 = Physics.Animation.MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, MotionCommand.Reload, MotionCommand.Ready);
            //Console.WriteLine($"AnimLength: {animLength} + {animLength2}");

            return animLength + animLength2;
        }

        // Split arrow constants
        private const int DEFAULT_SPLIT_ARROW_COUNT = 3;
        private const float DEFAULT_SPLIT_ARROW_RANGE = 8f;
        private const float DEFAULT_SPLIT_ARROW_DAMAGE_MULTIPLIER = 0.6f;
        
        // Split arrow validation constants
        private const int SPLIT_ARROW_COUNT_MIN = 1;
        private const int SPLIT_ARROW_COUNT_MAX = 10;
        private const float SPLIT_ARROW_RANGE_MIN = 0f;
        private const float SPLIT_ARROW_RANGE_MAX = 50f;
        private const float SPLIT_ARROW_DAMAGE_MULTIPLIER_MIN = 0f;
        private const float SPLIT_ARROW_DAMAGE_MULTIPLIER_MAX = 1f;

        /// <summary>
        /// Launches a projectile from player to target
        /// </summary>
        public WorldObject LaunchProjectile(WorldObject weapon, WorldObject ammo, WorldObject target, Vector3 origin, Quaternion orientation, Vector3 velocity)
        {
            if (log.IsDebugEnabled)
                log.Debug($"LaunchProjectile called - Weapon: {weapon?.Guid}, Ammo: {ammo?.Guid}, Target: {target?.Guid}");
            
            var player = this as Player;

            if (!velocity.IsValid())
            {
                if (player != null)
                    player.SendWeenieError(WeenieError.YourAttackMisfired);

                return null;
            }

            var proj = WorldObjectFactory.CreateNewWorldObject(ammo.WeenieClassId);

            proj.ProjectileSource = this;
            proj.ProjectileTarget = target;

            proj.ProjectileLauncher = weapon;
            proj.ProjectileAmmo = ammo;

            // Check for split arrows capability (will be handled after main projectile launch)

            proj.Location = new Position(Location);
            proj.Location.Pos = origin;
            proj.Location.Rotation = orientation;

            SetProjectilePhysicsState(proj, target, velocity);

            var success = LandblockManager.AddObject(proj);

            if (!success || proj.PhysicsObj == null)
            {
                if (!proj.HitMsg)
                {
                    if (player != null)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("Your missile attack hit the environment.", ChatMessageType.Broadcast));
                }

                proj.Destroy();
                return null;
            }

            if (!IsProjectileVisible(proj))
            {
                proj.OnCollideEnvironment();

                proj.Destroy();
                return null;
            }

            var pkStatus = player?.PlayerKillerStatus ?? PlayerKillerStatus.Creature;

            proj.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(proj, PropertyInt.PlayerKillerStatus, (int)pkStatus));
            proj.EnqueueBroadcast(new GameMessageScript(proj.Guid, PlayScript.Launch, 0f));

            // Create split arrows if weapon has split property
            if (weapon != null)
            {
                var hasSplitArrows = weapon.GetProperty(PropertyBool.SplitArrows);
                log.Debug($"Checking for split arrows - Weapon: {weapon.Guid}, SplitArrows: {hasSplitArrows}");
                
                if (hasSplitArrows == true)
                {
                    log.Debug("Calling CreateSplitArrows");
                    CreateSplitArrows(weapon, ammo, target, origin, orientation);
                }
            }
            else
            {
                log.Debug("Weapon is null, skipping split arrows");
            }

            // detonate point-blank projectiles immediately
            /*var radsum = target.PhysicsObj.GetRadius() + proj.PhysicsObj.GetRadius();
            var dist = Vector3.Distance(origin, dest);
            if (dist < radsum)
            {
                Console.WriteLine($"Point blank");
                proj.OnCollideObject(target);
            }*/

            return proj;
        }

        public static readonly float ProjSpawnHeight = 0.8454f;

        /// <summary>
        /// Returns the origin to spawn the projectile in the attacker local space
        /// </summary>
        public Vector3 GetProjectileSpawnOrigin(uint projectileWcid, MotionCommand motion)
        {
            var attackerRadius = PhysicsObj.GetPhysicsRadius();
            var projectileRadius = GetProjectileRadius(projectileWcid);

            //Console.WriteLine($"{Name} radius: {attackerRadius}");
            //Console.WriteLine($"Projectile {projectileWcid} radius: {projectileRadius}");

            var radsum = attackerRadius * 2.0f + projectileRadius * 2.0f + PhysicsGlobals.EPSILON;

            var origin = new Vector3(0, radsum, 0);

            // rotate by aim angle
            var angle = motion.GetAimAngle().ToRadians();
            var zRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle);

            origin = Vector3.Transform(origin, zRotation);

            origin.Z += Height * ProjSpawnHeight;

            return origin;
        }

        /// <summary>
        /// Returns the cached physics radius for a projectile wcid
        /// </summary>
        private static float GetProjectileRadius(uint projectileWcid)
        {
            if (ProjectileRadiusCache.TryGetValue(projectileWcid, out var radius))
                return radius;

            var weenie = DatabaseManager.World.GetCachedWeenie(projectileWcid);

            if (weenie == null)
            {
                log.Error($"Creature_Missile.GetProjectileRadius(): couldn't find projectile weenie {projectileWcid}");
                return 0.0f;
            }

            if (!weenie.PropertiesDID.TryGetValue(PropertyDataId.Setup, out var setupId))
            {
                log.Error($"Creature_Missile.GetProjectileRadius(): couldn't find SetupId for {weenie.WeenieClassId} - {weenie.ClassName}");
                return 0.0f;
            }

            var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(setupId);

            if (!weenie.PropertiesFloat.TryGetValue(PropertyFloat.DefaultScale, out var scale))
                scale = 1.0f;

            var result = (float)(setup.Spheres[0].Radius * scale);

            ProjectileRadiusCache.TryAdd(projectileWcid, result);

            return result;
        }

        // lowest value found in data / for starter bows
        public static readonly float DefaultProjectileSpeed = 20.0f;

        public float GetProjectileSpeed()
        {
            var missileLauncher = GetEquippedMissileWeapon();

            var maxVelocity = missileLauncher?.MaximumVelocity ?? DefaultProjectileSpeed;

            if (maxVelocity == 0.0f)
            {
                log.Warn($"{Name}.GetMissileSpeed() - {missileLauncher.Name} ({missileLauncher.Guid}) has speed 0");

                maxVelocity = DefaultProjectileSpeed;
            }

            if (this is Player player && player.GetCharacterOption(CharacterOption.UseFastMissiles))
            {
                maxVelocity *= PropertyManager.GetDouble("fast_missile_modifier").Item;
            }

            // hard cap in physics engine
            maxVelocity = Math.Min(maxVelocity, PhysicsGlobals.MaxVelocity);

            //Console.WriteLine($"MaxVelocity: {maxVelocity}");

            return (float)maxVelocity;
        }

        public Vector3 GetAimVelocity(WorldObject target, float projectileSpeed)
        {
            var crossLandblock = Location.Landblock != target.Location.Landblock;

            // eye level -> target point
            var origin = crossLandblock ? Location.ToGlobal(false) : Location.Pos;
            origin.Z += Height * ProjSpawnHeight;

            var dest = crossLandblock ? target.Location.ToGlobal(false) : target.Location.Pos;
            dest.Z += target.Height / GetAimHeight(target);

            var dir = Vector3.Normalize(dest - origin);

            var velocity = GetProjectileVelocity(target, origin, dir, dest, projectileSpeed, out float time);

            return velocity;
        }

        public Vector3 CalculateProjectileVelocity(Vector3 localOrigin, WorldObject target, float projectileSpeed, out Vector3 origin, out Quaternion rotation)
        {
            var sourceLoc = PhysicsObj.Position.ACEPosition();
            var targetLoc = target.PhysicsObj.Position.ACEPosition();

            var crossLandblock = sourceLoc.Landblock != targetLoc.Landblock;

            var startPos = crossLandblock ? sourceLoc.ToGlobal(false) : sourceLoc.Pos;
            var endPos = crossLandblock ? targetLoc.ToGlobal(false) : targetLoc.Pos;

            var dir = Vector3.Normalize(endPos - startPos);

            var angle = Math.Atan2(-dir.X, dir.Y);

            rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)angle);

            origin = sourceLoc.Pos + Vector3.Transform(localOrigin, rotation);

            startPos += Vector3.Transform(localOrigin, rotation);
            endPos.Z += target.Height / GetAimHeight(target);

            var velocity = GetProjectileVelocity(target, startPos, dir, endPos, projectileSpeed, out float time);

            return velocity;
        }

        /// <summary>
        /// Updates the ammo count or destroys the ammo after launching the projectile.
        /// </summary>
        /// <param name="ammo">The equipped missile ammo object</param>
        public virtual void UpdateAmmoAfterLaunch(WorldObject ammo)
        {
            // hide previously held ammo
            EnqueueBroadcast(new GameMessagePickupEvent(ammo));

            // monsters have infinite ammo?

            /*if (ammo.StackSize == null || ammo.StackSize <= 1)
            {
                TryUnwieldObjectWithBroadcasting(ammo.Guid, out _, out _);
                ammo.Destroy();
            }
            else
            {
                ammo.SetStackSize(ammo.StackSize - 1);
                EnqueueBroadcast(new GameMessageSetStackSize(ammo));
            }*/
        }

        /// <summary>
        /// Calculates the velocity to launch the projectile from origin to dest
        /// </summary>
        public Vector3 GetProjectileVelocity(WorldObject target, Vector3 origin, Vector3 dir, Vector3 dest, float speed, out float time, bool useGravity = true)
        {
            time = 0.0f;
            Vector3 s0;
            float t0;

            var gravity = useGravity ? -PhysicsGlobals.Gravity : 0.00001f;

            var targetVelocity = target.PhysicsObj.CachedVelocity;

            if (!targetVelocity.Equals(Vector3.Zero))
            {
                if (this is Player player && !player.GetCharacterOption(CharacterOption.LeadMissileTargets))
                {
                    // fall through
                }
                else
                {
                    // use movement quartic solver
                    if (!PropertyManager.GetBool("trajectory_alt_solver").Item)
                    {
                        var numSolutions = Trajectory.solve_ballistic_arc(origin, speed, dest, targetVelocity, gravity, out s0, out _, out time);

                        if (numSolutions > 0)
                            return s0;
                    }
                    else
                        return Trajectory2.CalculateTrajectory(origin, dest, targetVelocity, speed, useGravity);
                }
            }

            // use stationary solver
            if (!PropertyManager.GetBool("trajectory_alt_solver").Item)
            {
                Trajectory.solve_ballistic_arc(origin, speed, dest, gravity, out s0, out _, out t0, out _);

                time = t0;
                return s0;
            }
            else
                return Trajectory2.CalculateTrajectory(origin, dest, Vector3.Zero, speed, useGravity);
        }

        /// <summary>
        /// Sets the physics state for a launched projectile
        /// </summary>
        public void SetProjectilePhysicsState(WorldObject obj, WorldObject target, Vector3 velocity)
        {
            obj.InitPhysicsObj(Location.Variation);

            obj.ReportCollisions = true;
            obj.Missile = true;
            obj.AlignPath = true;
            obj.PathClipped = true;
            obj.Ethereal = false;
            obj.IgnoreCollisions = false;

            var pos = obj.Location.Pos;
            var rotation = obj.Location.Rotation;
            obj.PhysicsObj.Position.Frame.Origin = pos;
            obj.PhysicsObj.Position.Frame.Orientation = rotation;

            if (obj.HasMissileFlightPlacement)
                obj.Placement = ACE.Entity.Enum.Placement.MissileFlight;
            else
                obj.Placement = null;

            obj.CurrentMotionState = null;

            obj.PhysicsObj.Velocity = velocity;
            obj.PhysicsObj.ProjectileTarget = target.PhysicsObj;

            // Projectiles with RotationSpeed get omega values and "align path" turned off which
            // creates the nice swirling animation
            if ((obj.RotationSpeed ?? 0) != 0)
            {
                obj.AlignPath = false;
                obj.PhysicsObj.Omega = new Vector3((float)(Math.PI * 2 * obj.RotationSpeed), 0, 0);
            }

            obj.PhysicsObj.set_active(true);
        }

        public static Sound GetLaunchMissileSound(WorldObject weapon)
        {
            switch (weapon.DefaultCombatStyle)
            {
                case CombatStyle.Bow:
                    return Sound.BowRelease;
                case CombatStyle.Crossbow:
                    return Sound.CrossbowRelease;
                default:
                    return Sound.ThrownWeaponRelease1;
            }
        }

        public static readonly float MetersToYards = 1.094f;    // 1.09361
        public static readonly float MissileRangeCap = 85.0f / MetersToYards;   // 85 yards = ~77.697 meters w/ ac formula
        public static readonly float DefaultMaxVelocity = 20.0f;    // ?

        public float GetMaxMissileRange()
        {
            var weapon = GetEquippedMissileWeapon();
            var maxVelocity = weapon?.MaximumVelocity ?? DefaultMaxVelocity;

            var missileRange = (float)Math.Pow(maxVelocity, 2.0f) * 0.1020408163265306f;
            //var missileRange = (float)Math.Pow(maxVelocity, 2.0f) * 0.0682547266398198f;

            //var strengthMod = SkillFormula.GetAttributeMod((int)Strength.Current);
            //var maxRange = Math.Min(missileRange * strengthMod, MissileRangeCap);
            var maxRange = Math.Min(missileRange, MissileRangeCap);

            // any kind of other caps for monsters specifically?
            // throwing lugian rocks @ 85 yards seems a bit far...

            //Console.WriteLine($"{Name}.GetMaxMissileRange(): maxVelocity={maxVelocity}, strengthMod={strengthMod}, maxRange={maxRange}");

            // for client display
            /*var maxRangeYards = maxRange * MetersToYards;
            if (maxRangeYards >= 10.0f)
                maxRangeYards -= maxRangeYards % 5.0f;
            else
                maxRangeYards = (float)Math.Ceiling(maxRangeYards);

            Console.WriteLine($"Max range: {maxRange} ({maxRangeYards} yds.)");*/

            return maxRange;
        }

        public static MotionCommand GetAimLevel(Vector3 velocity)
        {
            // get z-angle?
            var zAngle = Vector3.Normalize(velocity).Z * 90.0f;

            var aimLevel = MotionCommand.AimLevel;

            if (zAngle >= 82.5f)
                aimLevel = MotionCommand.AimHigh90;
            else if (zAngle >= 67.5f)
                aimLevel = MotionCommand.AimHigh75;
            else if (zAngle >= 52.5f)
                aimLevel = MotionCommand.AimHigh60;
            else if (zAngle >= 37.5f)
                aimLevel = MotionCommand.AimHigh45;
            else if (zAngle >= 22.5f)
                aimLevel = MotionCommand.AimHigh30;
            else if (zAngle >= 7.5f)
                aimLevel = MotionCommand.AimHigh15;
            else if (zAngle > -7.5f)
                aimLevel = MotionCommand.AimLevel;
            else if (zAngle > -22.5f)
                aimLevel = MotionCommand.AimLow15;
            else if (zAngle > -37.5f)
                aimLevel = MotionCommand.AimLow30;
            else if (zAngle > -52.5f)
                aimLevel = MotionCommand.AimLow45;
            else if (zAngle > -67.5f)
                aimLevel = MotionCommand.AimLow60;
            else if (zAngle > -82.5f)
                aimLevel = MotionCommand.AimLow75;
            else
                aimLevel = MotionCommand.AimLow90;

            //Console.WriteLine($"Z Angle: {aimLevel.GetAimAngle()}");

            return aimLevel;
        }

        /// <summary>
        /// Creates additional projectiles for split arrow effect
        /// </summary>
        /// <param name="weapon">The weapon that has split arrows capability</param>
        /// <param name="ammo">The ammunition to use for split arrows</param>
        /// <param name="target">The primary target</param>
        /// <param name="origin">Origin position for split arrows</param>
        /// <param name="orientation">Orientation for split arrows</param>
        private void CreateSplitArrows(WorldObject weapon, WorldObject ammo, WorldObject target, Vector3 origin, Quaternion orientation)
        {
            try
            {
                // Validate inputs
                if (weapon == null || ammo == null || target == null)
                {
                    log.Warn("CreateSplitArrows called with null parameters");
                    return;
                }

                log.Debug($"CreateSplitArrows called for weapon {weapon.Guid}");
                
                var splitCount = weapon.GetProperty(PropertyInt.SplitArrowCount) ?? DEFAULT_SPLIT_ARROW_COUNT;
                var splitRange = (float)(weapon.GetProperty(PropertyFloat.SplitArrowRange) ?? DEFAULT_SPLIT_ARROW_RANGE);
                
                // Apply safety clamps to prevent invalid values
                splitCount = Math.Clamp(splitCount, SPLIT_ARROW_COUNT_MIN, SPLIT_ARROW_COUNT_MAX);
                splitRange = Math.Clamp(splitRange, SPLIT_ARROW_RANGE_MIN, SPLIT_ARROW_RANGE_MAX);
                
                log.Debug($"Split arrows - Count: {splitCount}, Range: {splitRange}");
                
                var additionalArrowCount = splitCount - 1; // We already have the main arrow
                
                var validTargets = FindValidSplitTargets(origin, target, splitRange, additionalArrowCount);
                
                log.Debug($"Found {validTargets.Count} valid targets for split arrows");
                
                if (validTargets.Count == 0)
                {
                    log.Debug("No valid targets found, skipping split arrows");
                    return;
                }
                
                var arrowsCreated = 0;
                
                foreach (var splitTarget in validTargets)
                {
                    if (arrowsCreated >= additionalArrowCount)
                        break;
                        
                    log.Debug($"Creating split arrow {arrowsCreated + 1} for target: {splitTarget.Name} (ID: {splitTarget.WeenieClassId})");
                    
                    // Create new projectile
                    var splitProj = WorldObjectFactory.CreateNewWorldObject(ammo.WeenieClassId);
                    
                    if (splitProj == null)
                    {
                        log.Error($"Failed to create split projectile for ammo {ammo.WeenieClassId}");
                        continue;
                    }
                    
                    // Set necessary properties
                    splitProj.ProjectileSource = this;
                    splitProj.ProjectileTarget = splitTarget;
                    splitProj.ProjectileLauncher = weapon;
                    splitProj.ProjectileAmmo = ammo;
                    
                    // Reduce damage for split arrows using weapon's damage multiplier property
                    var damageValue = splitProj.GetProperty(PropertyInt.Damage);
                    if (damageValue.HasValue)
                    {
                        var damageMultiplier = (float)(weapon.GetProperty(PropertyFloat.SplitArrowDamageMultiplier) ?? DEFAULT_SPLIT_ARROW_DAMAGE_MULTIPLIER);
                        // Apply safety clamp to damage multiplier
                        damageMultiplier = Math.Clamp(damageMultiplier, SPLIT_ARROW_DAMAGE_MULTIPLIER_MIN, SPLIT_ARROW_DAMAGE_MULTIPLIER_MAX);
                        var reducedDamage = (int)(damageValue.Value * damageMultiplier);
                        splitProj.SetProperty(PropertyInt.Damage, reducedDamage);
                        log.Debug($"Set split arrow damage to {reducedDamage} (original: {damageValue.Value}, multiplier: {damageMultiplier})");
                    }
                    
                    // Position at same origin
                    splitProj.Location = new Position(Location);
                    splitProj.Location.Pos = origin;
                    splitProj.Location.Rotation = orientation;
                    
                    // Calculate velocity to new target - use EXACT same physics as main arrow
                    var splitVelocity = GetAimVelocity(splitTarget, GetProjectileSpeed());
                    
                    log.Debug($"Split arrow velocity: {splitVelocity}");
                    
                    // Set physics state
                    SetProjectilePhysicsState(splitProj, splitTarget, splitVelocity);
                    
                    // Add to world
                    var success = LandblockManager.AddObject(splitProj);
                    if (success && splitProj.PhysicsObj != null)
                    {
                        splitProj.PhysicsObj.set_active(true);
                        splitProj.ReportCollisions = true;
                        
                        // Send launch broadcasts like the main projectile
                        var pkStatus = (this as Player)?.PlayerKillerStatus ?? PlayerKillerStatus.Creature;
                        splitProj.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(splitProj, PropertyInt.PlayerKillerStatus, (int)pkStatus));
                        splitProj.EnqueueBroadcast(new GameMessageScript(splitProj.Guid, PlayScript.Launch, 0f));
                        
                        log.Debug($"Successfully added split arrow {arrowsCreated + 1} to world");
                        arrowsCreated++;
                    }
                    else
                    {
                        log.Error($"Failed to add split arrow {arrowsCreated + 1} to world");
                    }
                }
                
                log.Debug($"Created {arrowsCreated} split arrows successfully");
            }
            catch (Exception ex)
            {
                log.Error($"Exception in CreateSplitArrows: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Finds valid targets for split arrows with optimized search
        /// </summary>
        /// <param name="origin">Origin position for the split arrows</param>
        /// <param name="primaryTarget">The primary target that was hit</param>
        /// <param name="splitRange">Maximum range to search for additional targets</param>
        /// <param name="maxTargets">Maximum number of additional targets to find</param>
        /// <returns>List of valid targets for split arrows</returns>
        private List<WorldObject> FindValidSplitTargets(Vector3 origin, WorldObject primaryTarget, float splitRange, int maxTargets)
        {
            log.Debug($"FindValidSplitTargets called - Range: {splitRange}, MaxTargets: {maxTargets}");
            var potentialTargets = new List<WorldObject>();
            var landblock = CurrentLandblock;
            
            if (landblock == null)
            {
                log.Warn("No landblock found for split arrow target search");
                return potentialTargets;
            }

            // Use a more targeted search instead of GetAllWorldObjectsForDiagnostics
            // Create a snapshot to avoid thread safety issues with ConcurrentDictionary iteration
            var nearbyObjects = landblock.GetWorldObjectsForPhysicsHandling().ToList();
            log.Debug($"Total nearby objects: {nearbyObjects.Count}");
            
            foreach (var obj in nearbyObjects.OrderBy(o => Vector3.Distance(primaryTarget.Location.Pos, o.Location.Pos)))
            {
                if (potentialTargets.Count >= maxTargets)
                    break;

                if (!(obj is Creature creature) || !creature.IsAlive)
                    continue;

                if (obj == primaryTarget || obj == this)
                    continue;

                if (!CanDamage(creature))
                    continue;

                // Calculate distance from PRIMARY TARGET to this potential target
                var distanceFromPrimaryTarget = Vector3.Distance(primaryTarget.Location.Pos, obj.Location.Pos);
                if (distanceFromPrimaryTarget <= splitRange)
                {
                    // Basic angle validation: Check if target is roughly in front of the player
                    if (IsTargetInValidAngle(primaryTarget, obj, origin))
                    {
                        potentialTargets.Add(obj);
                    }
                }
            }

            return potentialTargets;
        }
        
        /// <summary>
        /// Validates if a target is within a reasonable firing angle for split arrows
        /// </summary>
        /// <param name="primaryTarget">The primary target that was hit</param>
        /// <param name="potentialTarget">The potential target to validate</param>
        /// <param name="origin">Origin position for the split arrows</param>
        /// <returns>True if the target is within a valid firing angle</returns>
        private bool IsTargetInValidAngle(WorldObject primaryTarget, WorldObject potentialTarget, Vector3 origin)
        {
            try
            {
                // Calculate direction from primary target to potential target
                var directionToTarget = Vector3.Normalize(potentialTarget.Location.Pos - primaryTarget.Location.Pos);
                
                // Calculate direction from player to primary target (player's forward direction)
                var playerForward = Vector3.Normalize(primaryTarget.Location.Pos - origin);
                
                // Calculate dot product between player forward direction and direction to target
                var dot = Vector3.Dot(playerForward, directionToTarget);
                // <= 90° iff dot >= 0
                return dot >= 0f;
            }
            catch (Exception ex)
            {
                log.Error($"Exception in IsTargetInValidAngle: {ex.Message}", ex);
                return false; // Fail safe - don't create split arrow if validation fails
            }
        }
    }
}
