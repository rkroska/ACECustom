using System;
using System.Collections.Generic;
using System.Numerics;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// A short-lived monster spawned by a mutation breed. It wears the mutated offspring's exact
    /// appearance (species donor + rolled palette) so the owners see what they are about to receive,
    /// and the two parent pets must kill it together to complete the birth.
    ///
    /// Rules:
    ///  - Only the two parent pets can damage it. Everything else (players, other pets, other
    ///    monsters, DoT ticks with no source) deals 0.
    ///  - It only ever targets the two parent pets, never the players.
    ///  - No corpse, no loot, no XP. It is a ritual, not a farm.
    ///  - It is never saved: it exists purely in memory for one fight.
    /// </summary>
    public class MatingGuardian : Creature
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>GUIDs of the two parent pets; the only objects allowed to damage or be targeted.</summary>
        private readonly HashSet<uint> allowedPetGuids = new HashSet<uint>();

        /// <summary>
        /// GUIDs of the two owners. A CombatPet resummoned mid-fight gets a new GUID, so owner
        /// identity is the fallback that keeps the fight winnable.
        /// </summary>
        private readonly HashSet<uint> allowedOwnerGuids = new HashSet<uint>();

        private Action<MatingGuardian> onSlain;
        private Action<MatingGuardian> onLost;
        private bool dieEnteredGuardian;
        private bool resolved;

        public double SpawnTime { get; private set; }

        public bool IsResolved => resolved;

        public MatingGuardian(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
        }

        /// <summary>
        /// Binds the guardian to its two parent pets and the callback to run when they kill it.
        /// Must be called before the guardian enters the world.
        /// </summary>
        public void Bind(CombatPet pet1, CombatPet pet2, Player owner1, Player owner2, Action<MatingGuardian> onSlainCallback, Action<MatingGuardian> onLostCallback)
        {
            allowedPetGuids.Add(pet1.Guid.Full);
            allowedPetGuids.Add(pet2.Guid.Full);
            allowedOwnerGuids.Add(owner1.Guid.Full);
            allowedOwnerGuids.Add(owner2.Guid.Full);
            onSlain = onSlainCallback;
            onLost = onLostCallback;
            SpawnTime = Timers.RunningTime;
        }

        /// <summary>True if the object is one of the parent pets, or a pet belonging to one of the two owners.</summary>
        public bool IsParentPet(WorldObject wo)
        {
            if (wo == null)
                return false;

            if (allowedPetGuids.Contains(wo.Guid.Full))
                return true;

            if (wo is CombatPet pet && pet.P_PetOwner != null && allowedOwnerGuids.Contains(pet.P_PetOwner.Guid.Full))
                return true;

            return false;
        }

        /// <summary>
        /// Resolves a damage source to the creature that actually caused it: projectiles and spell
        /// projectiles carry their launcher in ProjectileSource.
        /// </summary>
        private static WorldObject ResolveDamageSource(WorldObject source)
        {
            var current = source;
            for (var i = 0; i < 4 && current != null; i++)
            {
                if (current is Creature)
                    return current;
                current = current.ProjectileSource;
            }
            return source;
        }

        /// <summary>
        /// Only the parent pets can hurt the guardian. Returning 0 before base.TakeDamage means no
        /// vital change, no damage history entry, and therefore no XP credit or death attribution
        /// for anyone else.
        /// </summary>
        public override bool CanBeDamagedBy(WorldObject source) => IsParentPet(ResolveDamageSource(source));

        public override uint TakeDamage(WorldObject source, DamageType damageType, float amount, bool crit = false)
        {
            if (!CanBeDamagedBy(source))
                return 0;

            return base.TakeDamage(source, damageType, amount, crit);
        }

        /// <summary>
        /// Never sleeps. The stock monster loop puts an idle monster to sleep and the next player who
        /// walks past wakes it with themselves as the target; the guardian must keep scanning for pets.
        /// </summary>
        public override void Sleep()
        {
        }

        /// <summary>
        /// Runs every tick. Anything that is not a parent pet (a player set as target by AlertMonster
        /// or by attacking it) is dropped immediately, before the melee loop can swing at it.
        /// </summary>
        public override void HandleFindTarget()
        {
            if (AttackTarget != null && !IsParentPet(AttackTarget))
                AttackTarget = null;

            base.HandleFindTarget();
        }

        /// <summary>
        /// The guardian fights back against the parent pets only. It never targets a player, so the
        /// owners can stand next to the ritual safely.
        /// </summary>
        public override bool FindNextTarget()
        {
            SetNextTargetTime();

            WorldObject best = null;
            var bestDistSq = float.MaxValue;

            var candidates = PhysicsObj?.ObjMaint?.GetVisibleTargetsValuesOfTypeCreature();
            if (candidates != null)
            {
                foreach (var wo in candidates)
                {
                    if (wo == null || wo.IsDead || wo.IsDestroyed || wo.Location == null || !IsParentPet(wo))
                        continue;

                    var distSq = Vector3.DistanceSquared(Location.ToGlobal(), wo.Location.ToGlobal());
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = wo;
                    }
                }
            }

            AttackTarget = best;
            return best != null;
        }

        /// <summary>
        /// Death completes the ritual exactly once, then the normal death path runs (which, with
        /// NoCorpse and no treasure/XP, just plays the animation and removes the object).
        /// </summary>
        public override DeathMessage OnDeath(DamageHistoryInfo lastDamager, DamageType damageType, bool criticalHit = false)
        {
            var result = base.OnDeath(lastDamager, damageType, criticalHit);

            if (!resolved)
            {
                resolved = true;
                var fightSeconds = Timers.RunningTime - SpawnTime;
                log.Info($"[PetBreeding] Mating guardian {Name} (0x{Guid.Full:X8}) slain after {fightSeconds:F1}s by {lastDamager?.Name ?? "unknown"}.");

                try
                {
                    onSlain?.Invoke(this);
                }
                catch (Exception ex)
                {
                    log.Error($"[PetBreeding] Mating guardian completion callback threw: {ex}");
                }
            }

            return result;
        }

        /// <summary>
        /// Removal without a death (landblock unload, admin delete, anything else) still resolves the
        /// breed, otherwise the parents stay locked out and their paid breed is lost.
        /// </summary>
        public override void Destroy(bool raiseNotifyOfDestructionEvent = true, bool fromLandblockUnload = false)
        {
            if (!resolved)
            {
                resolved = true;
                log.Info($"[PetBreeding] Mating guardian {Name} (0x{Guid.Full:X8}) removed without dying (landblockUnload={fromLandblockUnload}); resolving breed as lost.");
                try
                {
                    onLost?.Invoke(this);
                }
                catch (Exception ex)
                {
                    log.Error($"[PetBreeding] Mating guardian lost callback threw: {ex}");
                }
            }

            base.Destroy(raiseNotifyOfDestructionEvent, fromLandblockUnload);
        }

        /// <summary>
        /// Minimal death: stop, play the death animation, vanish. Skips the stock path's Siphon Lens
        /// roll, death emotes, treasure and template item drops.
        /// </summary>
        protected override void Die(DamageHistoryInfo lastDamager, DamageHistoryInfo topDamager)
        {
            if (dieEnteredGuardian)
                return;
            dieEnteredGuardian = true;

            UpdateVital(Health, 0);
            CurrentMotionState = new Motion(MotionStance.NonCombat, MotionCommand.Ready);
            PhysicsObj?.StopCompletely(true);

            var deathAnimLength = ExecuteMotion(new Motion(MotionStance.NonCombat, MotionCommand.Dead));

            var dieChain = new ActionChain();
            dieChain.AddDelaySeconds(deathAnimLength);
            dieChain.AddAction(WorldManager.ActionQueue, ActionType.CreatureDeath_MakeCorpse, () =>
            {
                if (!IsDestroyed)
                    Destroy();
            });
            dieChain.EnqueueChain();
        }

        /// <summary>
        /// Marks the guardian resolved without running the slain callback (timeout / cancel path)
        /// and removes it from the world.
        /// </summary>
        public void Fade()
        {
            resolved = true;
            if (!IsDestroyed)
            {
                EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.DispelAll));
                Destroy();
            }
        }

        /// <summary>
        /// Strips everything from the template weenie that would turn the ritual into a farm or let it
        /// wander: loot, XP, corpse, faction, generator ties, kill tasks, leash. Called after the
        /// template has been dressed and statted.
        /// </summary>
        public void NeutraliseTemplate()
        {
            NoCorpse = true;
            XpOverride = 0;
            RemoveProperty(PropertyDataId.DeathTreasureType);
            RemoveProperty(PropertyInt.LuminanceAward);
            // Retail templates can carry wielded/contained items that drop on death; the ritual drops nothing.
            Biota.PropertiesCreateList?.Clear();
            RemoveProperty(PropertyInt.Faction1Bits);
            RemoveProperty(PropertyInt.GeneratorDestructionType);
            RemoveProperty(PropertyInt.GeneratorEndDestructionType);
            RemoveProperty(PropertyInstanceId.Generator);
            RemoveProperty(PropertyBool.NeverAttack);
            Attackable = true;
            RemoveProperty(PropertyString.KillQuest);
            RemoveProperty(PropertyString.KillQuest2);
            RemoveProperty(PropertyString.KillQuest3);
            // Keep it in place: home is where it spawned, and a short leash brings it straight back.
            SetProperty(PropertyFloat.HomeRadius, 20.0);

            // Items the template instantiated at construction would drop on death; get rid of them.
            foreach (var item in new List<WorldObject>(EquippedObjects.Values))
            {
                EquippedObjects.Remove(item.Guid);
                item.Destroy();
            }
            foreach (var item in new List<WorldObject>(Inventory.Values))
            {
                Inventory.Remove(item.Guid);
                item.Destroy();
            }

            // IsMonster / IsFactionMob were cached at construction from the template's properties;
            // recompute now that faction and attackability have been changed.
            SetMonsterState();
        }
    }
}
