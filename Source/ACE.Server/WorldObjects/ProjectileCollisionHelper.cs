using System;
using log4net;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Helper class for arrows / bolts / thrown weapons
    /// outside of the WorldObject hierarchy
    /// </summary>
    public static class ProjectileCollisionHelper
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static void OnCollideObject(WorldObject worldObject, WorldObject target)
        {
            if (!worldObject.PhysicsObj.is_active()) return;

            //Console.WriteLine($"Projectile.OnCollideObject - {WorldObject.Name} ({WorldObject.Guid}) -> {target.Name} ({target.Guid})");

            // Check if this is a split arrow
            var isSplitArrow = worldObject.GetProperty(PropertyBool.IsSplitArrow) == true;
            
            if (isSplitArrow)
            {
                // Split arrows can hit their intended split target
                if (worldObject.ProjectileTarget != target)
                {
                    OnCollideEnvironment(worldObject);
                    return;
                }
            }
            else if (worldObject.ProjectileTarget == null || worldObject.ProjectileTarget != target)
            {
                //Console.WriteLine("Unintended projectile target! (should be " + ProjectileTarget.Guid.Full.ToString("X8") + " - " + ProjectileTarget.Name + ")");
                OnCollideEnvironment(worldObject);
                return;
            }

            // take damage
            var sourceCreature = worldObject.ProjectileSource as Creature;
            var sourcePlayer = worldObject.ProjectileSource as Player;
            var targetCreature = target as Creature;
            
            if (targetCreature != null && targetCreature.IsAlive)
            {
                DamageEvent damageEvent = null;

                if (sourcePlayer != null)
                {
                    // Track the last projectile that hit this creature for death message modification
                    try
                    {
                        var projectileIsSplitArrow = worldObject.GetProperty(PropertyBool.IsSplitArrow) == true;
                        
                        // Always track the last projectile hit, regardless of whether it's split or main
                        targetCreature.SetProperty(PropertyBool.IsSplitArrowKill, projectileIsSplitArrow);
                        targetCreature.SetProperty(PropertyInstanceId.LastSplitArrowProjectile, worldObject.Guid.Full);
                        targetCreature.SetProperty(PropertyInstanceId.LastSplitArrowShooter, sourcePlayer.Guid.Full);
                        
                        // Removed verbose projectile tracking logging
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error setting projectile tracking: {ex.Message}", ex);
                    }
                    
                    // player damage monster or player
                    damageEvent = sourcePlayer.DamageTarget(targetCreature, worldObject);

                    if (damageEvent != null && damageEvent.HasDamage)
                        worldObject.EnqueueBroadcast(new GameMessageSound(worldObject.Guid, Sound.Collision, 1.0f));
                }
                else if (sourceCreature != null && sourceCreature.AttackTarget != null)
                {
                    // todo: clean this up
                    var targetPlayer = sourceCreature.AttackTarget as Player;

                    damageEvent = DamageEvent.CalculateDamage(sourceCreature, targetCreature, worldObject);

                    if (targetPlayer != null)
                    {
                        // monster damage player
                        if (damageEvent.HasDamage)
                        {
                            targetPlayer.TakeDamage(sourceCreature, damageEvent);

                            // blood splatter?

                            if (damageEvent.ShieldMod != 1.0f)
                            {
                                var shieldSkill = targetPlayer.GetCreatureSkill(Skill.Shield);
                                Proficiency.OnSuccessUse(targetPlayer, shieldSkill, shieldSkill.Current);   // ??
                            }

                            // handle Dirty Fighting
                            if (sourceCreature.GetCreatureSkill(Skill.DirtyFighting).AdvancementClass >= SkillAdvancementClass.Trained)
                                sourceCreature.FightDirty(targetPlayer, damageEvent.Weapon);
                        }
                        else
                            targetPlayer.OnEvade(sourceCreature, CombatType.Missile);
                    }
                    else
                    {
                        // monster damage pet
                        if (damageEvent.HasDamage)
                        {
                            targetCreature.TakeDamage(sourceCreature, damageEvent.DamageType, damageEvent.Damage);

                            // blood splatter?

                            // handle Dirty Fighting
                            if (sourceCreature.GetCreatureSkill(Skill.DirtyFighting).AdvancementClass >= SkillAdvancementClass.Trained)
                                sourceCreature.FightDirty(targetCreature, damageEvent.Weapon);
                        }

                        if (!(targetCreature is CombatPet))
                        {
                            // faction mobs and foetype
                            sourceCreature.MonsterOnAttackMonster(targetCreature);
                        }
                    }
                }

                // handle target procs
                if (damageEvent != null && damageEvent.HasDamage)
                {
                    bool threadSafe = true;

                    if (LandblockManager.CurrentlyTickingLandblockGroupsMultiThreaded)
                    {
                        // Ok... if we got here, we're likely in the parallel landblock physics processing.
                        if (worldObject.CurrentLandblock == null || sourceCreature?.CurrentLandblock == null || targetCreature.CurrentLandblock == null || worldObject.CurrentLandblock.CurrentLandblockGroup != sourceCreature?.CurrentLandblock.CurrentLandblockGroup || sourceCreature?.CurrentLandblock.CurrentLandblockGroup != targetCreature.CurrentLandblock.CurrentLandblockGroup)
                            threadSafe = false;
                    }

                    if (threadSafe && sourceCreature != null)
                    {
                        var isSplitArrowProjectile = worldObject.GetProperty(PropertyBool.IsSplitArrow) == true;

                        if (isSplitArrowProjectile)
                        {
                            // Apply weapon-only, per-target procs for split arrows, governed by single toggle
                            // Divide chance by number of split arrows configured on the weapon
                            var splitCount = worldObject.ProjectileLauncher?.GetProperty(PropertyInt.SplitArrowCount) ?? 0;
                            sourceCreature.TryProcWeaponOnCleaveTarget(sourceCreature, targetCreature, worldObject.ProjectileLauncher, splitCount);
                        }
                        else
                        {
                            // Normal projectile: use standard proc handling
                            worldObject.TryProcEquippedItems(sourceCreature, targetCreature, false, worldObject.ProjectileLauncher);
                        }
                    }
                    else
                    {
                        // sourceCreature and creatureTarget are now in different landblock groups.
                        // What has likely happened is that sourceCreature sent a projectile toward creatureTarget. Before impact, sourceCreature was teleported away.
                        // To perform this fully thread safe, we would enqueue the work onto worldManager.
                        // WorldManager.EnqueueAction(new ActionEventDelegate(() => sourceCreature.TryProcEquippedItems(targetCreature, false)));
                        // But, to keep it simple, we will just ignore it and not bother with TryProcEquippedItems for this particular impact.
                    }
                }
            }

            worldObject.CurrentLandblock?.RemoveWorldObject(worldObject.Guid, showError: !worldObject.PhysicsObj.entering_world);
            worldObject.PhysicsObj.set_active(false);

            worldObject.HitMsg = true;
        }

        public static void OnCollideEnvironment(WorldObject worldObject)
        {
            if (!worldObject.PhysicsObj.is_active()) return;

            // do not send 'Your missile attack hit the environment' messages to player,
            // if projectile is still in the process of spawning into world.
            if (worldObject.PhysicsObj.entering_world)
                return;

            //Console.WriteLine($"Projectile.OnCollideEnvironment({WorldObject.Name} - {WorldObject.Guid})");

            worldObject.CurrentLandblock?.RemoveWorldObject(worldObject.Guid, showError: !worldObject.PhysicsObj.entering_world);
            worldObject.PhysicsObj.set_active(false);

            if (worldObject.ProjectileSource is Player player)
            {
                // Don't show environment hit messages for split arrows to reduce spam
                var isSplitArrow = worldObject.GetProperty(PropertyBool.IsSplitArrow) == true;
                if (!isSplitArrow)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("Your missile attack hit the environment.", ChatMessageType.Broadcast));
                }
            }
            else if (worldObject.ProjectileSource is Creature creature)
            {
                creature.MonsterProjectile_OnCollideEnvironment();
            }

            worldObject.HitMsg = true;
        }
    }
}

