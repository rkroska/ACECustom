using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using System.Threading.Tasks;
using ACE.Server.Factories;
using ACE.Server.Entity.Actions;
using ACE.Entity;
using System.Numerics;
using ACE.Server.Physics;
using ACE.Server.Command;
using ACE.Server.Command.Handlers;
using ACE.Server.Physics.Combat;
using System.Threading;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Monster combat general functions
    /// </summary>
    partial class Creature
    {
        public float EnrageHealthPercentage { get; set; } = 0.2f; // Default to 20% health
        public bool IsEnraged { get; private set; } = false;
        private Player lastGrappleTarget;
        private Player lastHotspotTarget;

        /// <summary>
        /// The current attack target for the monster
        /// </summary>
        public WorldObject AttackTarget;

        /// <summary>
        /// A monster chooses 1 attack height
        /// </summary>
        public AttackHeight? AttackHeight;

        /// <summary>
        /// The next type of attack (melee/range/magic)
        /// </summary>
        public CombatType? CurrentAttack;

        /// <summary>
        /// The maximum distance for the next attack
        /// </summary>
        public float MaxRange;

        /// <summary>
        ///  The time when monster started its last attack
        /// </summary>
        public double PrevAttackTime { get; set; }

        /// <summary>
        /// The time when monster can perform its next attack
        /// </summary>
        public double NextAttackTime { get; set; }

        /// <summary>
        /// The time when monster can perform its next magic attack
        /// </summary>
        public double NextMagicAttackTime
        {
            get
            {
                // defaults to most common value found in py16 db
                var magicDelay = AiUseMagicDelay ?? 3.0f;

                return PrevAttackTime + magicDelay;
            }
        }

        /// <summary>
        /// Returns true if monster is dead
        /// </summary>
        public bool IsDead => Health.Current <= 0;

        /// <summary>
        /// A list of possible attack heights for this monster,
        /// as determined by the combat maneuvers table
        /// </summary>
        private List<AttackHeight> _attackHeights;

        public List<AttackHeight> AttackHeights
        {
            get
            {
                if (CombatTable == null) return null;

                if (_attackHeights == null)
                    _attackHeights = CombatTable.CMT.Select(m => m.AttackHeight).Distinct().ToList();

                return _attackHeights;
            }
        }

        /// <summary>
        /// Selects a random attack height for the next attack
        /// </summary>
        public AttackHeight ChooseAttackHeight()
        {
            var rng = ThreadSafeRandom.Next(0, AttackHeights.Count - 1);
            return AttackHeights[rng];
        }

        public CombatType GetNextAttackType()
        {
            if (CombatTable == null)
                GetCombatTable();

            // if caster, roll for spellcasting chance
            if (HasKnownSpells && TryRollSpell())
                return CombatType.Magic;

            if (IsRanged)
                return CombatType.Missile;
            else
                return CombatType.Melee;
        }

        /// <summary>
        /// Reads the combat maneuvers table from the DAT file
        /// </summary>
        public void GetCombatTable()
        {
            if (CombatTableDID != null)
                CombatTable = DatManager.PortalDat.ReadFromDat<CombatManeuverTable>(CombatTableDID.Value);
        }

        /// <summary>
        /// Switch to attack stance
        /// </summary>
        public void DoAttackStance()
        {
            var combatMode = IsRanged ? CombatMode.Missile : CombatMode.Melee;

            var stanceTime = SetCombatMode(combatMode);

            var nextTime = Timers.RunningTime + stanceTime;

            if (NextMoveTime > Timers.RunningTime)
                NextMoveTime += stanceTime;
            else
                NextMoveTime = nextTime;

            if (NextAttackTime > Timers.RunningTime)
                NextAttackTime += stanceTime;
            else
                NextAttackTime = nextTime;

            if (IsRanged)
            {
                PrevAttackTime = NextAttackTime + MissileDelay - (AiUseMagicDelay ?? 3.0f);

                NextAttackTime += MissileDelay;
            }

            if (NeverAttack)
            {
                PrevAttackTime = NextAttackTime = double.MaxValue - (AiUseMagicDelay ?? 3.0f);
            }

            if (DebugMove)
                Console.WriteLine($"[{Timers.RunningTime}] - {Name} ({Guid}) - DoAttackStance - stanceTime: {stanceTime}, isAnimating: {IsAnimating}");

            PhysicsObj.StartTimer();
        }

        public float GetMaxRange()
        {
            // FIXME
            var it = 0;
            bool? isVisible = null;

            while (CurrentAttack == CombatType.Magic)
            {
                // select a magic spell
                //CurrentSpell = GetRandomSpell();
                if (CurrentSpell.IsProjectile)
                {
                    if (isVisible == null)
                        isVisible = IsDirectVisible(AttackTarget);

                    // ensure direct los
                    if (!isVisible.Value)
                    {
                        // reroll attack type
                        CurrentAttack = GetNextAttackType();
                        it++;

                        // max iterations to melee?
                        if (it >= 10)
                        {
                            //log.Warn($"{Name} ({Guid}) reached max iterations");
                            CurrentAttack = CombatType.Melee;

                            var powerupTime = (float)(PowerupTime ?? 1.0f);
                            var failDelay = ThreadSafeRandom.Next(0.0f, powerupTime);

                            NextMoveTime = Timers.RunningTime + failDelay;
                        }
                        continue;
                    }
                }
                return GetSpellMaxRange();
            }

            if (CurrentAttack == CombatType.Missile)
            {
                /*var weapon = GetEquippedWeapon();
                if (weapon == null) return MaxMissileRange;

                var maxRange = weapon.GetProperty(PropertyInt.WeaponRange) ?? MaxMissileRange;
                return Math.Min(maxRange, MaxMissileRange);     // in-game cap @ 80 yds.*/
                return GetMaxMissileRange();
            }
            else
                return MaxMeleeRange;   // distance_to_target?
        }

        public bool MoveReady()
        {
            if (Timers.RunningTime < NextMoveTime)
                return false;

            PhysicsObj.update_object();
            UpdatePosition_SyncLocation();

            return !PhysicsObj.IsAnimating;
        }

        /// <summary>
        /// Returns TRUE if creature can perform its next attack
        /// </summary>
        /// <returns></returns>
        public bool AttackReady()
        {
            var nextAttackTime = CurrentAttack == CombatType.Magic ? NextMagicAttackTime : NextAttackTime;

            if (Timers.RunningTime < nextAttackTime || !IsAttackRange())
                return false;

            PhysicsObj.update_object();
            UpdatePosition_SyncLocation();

            return !PhysicsObj.IsAnimating;
        }

        /// <summary>
        /// Performs the current attack on the target
        /// </summary>
        public void Attack()
        {
            if (DebugMove)
                Console.WriteLine($"[{Timers.RunningTime}] - {Name} ({Guid}) - Attack");

            switch (CurrentAttack)
            {
                case CombatType.Melee:
                    MeleeAttack();
                    break;
                case CombatType.Missile:
                    RangeAttack();
                    break;
                case CombatType.Magic:
                    MagicAttack();
                    break;
            }

            EmoteManager.OnAttack(AttackTarget);

            ResetAttack();
        }

        /// <summary>
        /// Called after attack has completed
        /// </summary>
        public void ResetAttack()
        {
            // wait for missile to strike
            //if (CurrentAttack == CombatType.Missile)
                //return;

            IsTurning = false;
            IsMoving = false;

            CurrentAttack = null;
            MaxRange = 0.0f;
        }

        public DamageType GetDamageType(PropertiesBodyPart attackPart, CombatType? combatType = null)
        {
            var weapon = GetEquippedWeapon();

            if (weapon != null)
                return GetDamageType(false, combatType);
            else
            {
                var damageType = attackPart.DType;

                if (damageType.IsMultiDamage())
                    damageType = damageType.SelectDamageType();

                return damageType;
            }
        }

        /// <summary>
        /// Simplified monster take damage over time function, only called for DoTs currently
        /// </summary>
        public virtual void TakeDamageOverTime(float amount, DamageType damageType)
        {
            if (IsDead) return;

            TakeDamage(null, damageType, amount);

            // splatter effects
            var hitSound = new GameMessageSound(Guid, Sound.HitFlesh1, 0.5f);
            //var splatter = (PlayScript)Enum.Parse(typeof(PlayScript), "Splatter" + playerSource.GetSplatterHeight() + playerSource.GetSplatterDir(this));
            var splatter = new GameMessageScript(Guid, damageType == DamageType.Nether ? PlayScript.HealthDownVoid : PlayScript.DirtyFightingDamageOverTime);
            EnqueueBroadcast(hitSound, splatter);

            if (Health.Current <= 0) return;

            if (amount >= Health.MaxValue * 0.25f)
            {
                var painSound = (Sound)Enum.Parse(typeof(Sound), "Wound" + ThreadSafeRandom.Next(1, 3), true);
                EnqueueBroadcast(new GameMessageSound(Guid, painSound, 1.0f));
            }
        }

        /// <summary>
        /// Notifies the damage over time (DoT) source player of the tick damage amount
        /// </summary>
        public void TakeDamageOverTime_NotifySource(Player source, DamageType damageType, float amount, bool aetheria = false)
        {
            if (!PropertyManager.GetBool("show_dot_messages").Item)
                return;

            var iAmount = (uint)Math.Round(amount);

            var notifyType = damageType == DamageType.Undef ? DamageType.Health : damageType;

            string verb = null, plural = null;
            var percent = amount / Health.MaxValue;
            Strings.GetAttackVerb(notifyType, percent, ref verb, ref plural);

            string msg = null;

            var type = ChatMessageType.CombatSelf;

            if (damageType == DamageType.Nether)
            {
                msg = $"You {verb} {Name} for {iAmount} points of periodic nether damage!";
                type = ChatMessageType.Magic;
            }
            else if (aetheria)
            {
                msg = $"With Surge of Affliction you {verb} {iAmount} points of health from {Name}!";
                type = ChatMessageType.Magic;
            }
            else
            {
                /*var skill = source.GetCreatureSkill(Skill.DirtyFighting);
                var attack = skill.AdvancementClass == SkillAdvancementClass.Specialized ? "Bleeding Assault" : "Bleeding Blow";
                msg = $"With {attack} you {verb} {iAmount} points of health from {Name}!";*/

                msg = $"You bleed {Name} for {iAmount} points of periodic health damage!";
                type = ChatMessageType.CombatSelf;
            }
            source.SendMessage(msg, type);
        }

        public void Enrage()
        {
            if (IsEnraged) return; // Avoid multiple enrages
            IsEnraged = true;

           // Console.WriteLine($"{Name} has enraged!");

            // Set default values for damage multiplier and reduction if not configured
            EnrageDamageMultiplier = (float)(GetProperty(PropertyFloat.EnrageDamageMultiplier) ?? 2.0f);
            EnrageDamageReduction = (float)(GetProperty(PropertyFloat.EnrageDamageReduction) ?? 0.2f);

          //  Console.WriteLine($"{Name} has enraged! Damage Multiplier: {EnrageDamageMultiplier}, Damage Reduction: {EnrageDamageReduction * 100}%");

            EnsureVisibility();

            ApplyEnrageEffects();

            // **Delay Before Starting the Grapple Loop**
            if (CanGrapple)
            {
                StartGrappleLoopWithDelay();
            }

            // **Delay First AOE Attack by 5 seconds**
            if (CanAOE)
            {
                StartHotspotSpawnLoopWithDelay();
            }

            // **Broadcast an enrage message**
            BroadcastMessage($"{Name} becomes enraged and starts attacking fiercely!", 250.0f);
        }

        private void EnsureVisibility()
        {
            if (PhysicsObj != null)
            {
                PhysicsObj.State &= ~PhysicsState.NoDraw; // Disable NoDraw
              //  Console.WriteLine("[DEBUG] NoDraw disabled to ensure mob is visible.");
            }
            else
            {
               // Console.WriteLine("[ERROR] PhysicsObj is null, cannot modify NoDraw state.");
            }
        }

        private void ApplyEnrageEffects()
        {
            ApplyVisualEffect();
            ApplyFogEffect();
            ApplySoundEffect();
        }

        private void ApplyVisualEffect()
        {
            var visualEffectId = (int?)GetProperty(PropertyInt.EnrageVisualEffect);
            if (visualEffectId != null)
            {
                var scriptMessage = new GameMessageScript(this.Guid, (PlayScript)visualEffectId);
                EnqueueBroadcast(scriptMessage);
              //  Console.WriteLine($"[DEBUG] Enrage applied visual effect: {(PlayScript)visualEffectId}.");
            }
        }

        private void ApplyFogEffect()
        {
            var fogId = (int?)GetProperty(PropertyInt.EnrageFogColor);
            if (fogId != null)
            {
                var fogEffect = (EnvironChangeType)fogId;
                CurrentLandblock.SendEnvironChange(fogEffect);
               // Console.WriteLine($"[DEBUG] Enrage applied fog effect: {fogEffect}.");
            }
        }

        private void ApplySoundEffect()
        {
            var soundId = (int?)GetProperty(PropertyInt.EnrageSound);
            if (soundId != null)
            {
                var soundEffect = (EnvironChangeType)soundId;
                CurrentLandblock.SendEnvironChange(soundEffect);
               // Console.WriteLine($"[DEBUG] Enrage applied sound effect: {soundEffect}.");
            }
        }

        private CancellationTokenSource hotspotLoopCTS;
        private Task hotspotLoopTask;

        public void StartHotspotSpawnLoopWithDelay()
        {
            hotspotLoopCTS?.Cancel();
            hotspotLoopCTS = new CancellationTokenSource();

            hotspotLoopTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), hotspotLoopCTS.Token);
                    await StartHotspotSpawnLoopAsync(hotspotLoopCTS.Token);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    Console.WriteLine($"{Name}: hotspot loop terminated unexpectedly.");
                }
            });
        }



        private CancellationTokenSource grappleLoopCTS;
        private Task grappleLoopTask;

        public void StartGrappleLoopWithDelay()
        {
            grappleLoopCTS?.Cancel();
            grappleLoopCTS = new CancellationTokenSource();

            grappleLoopTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), grappleLoopCTS.Token);
                    await StartGrappleLoopAsync(grappleLoopCTS.Token);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    Console.WriteLine($"{Name}: grapple loop terminated unexpectedly.");
                }
            });
        }


        private async Task StartGrappleLoopAsync(CancellationToken ct)
        {
            var random = new Random();

            try
            {
                while (IsEnraged && IsAlive && !ct.IsCancellationRequested)
                {
                    var playersInRange = GetPlayersInRange(250.0f);
                    if (playersInRange.Count > 1)
                    {
                        var validTargets = new List<Player>();
                        foreach (var p in playersInRange)
                        {
                            if (p != lastHotspotTarget)
                                validTargets.Add(p);
                        }
                        if (validTargets.Count > 0)
                        {
                            var targetPlayer = validTargets[random.Next(validTargets.Count)];
                            lastGrappleTarget = targetPlayer;

                            BroadcastMessage($"{Name} Lashes out, attempting to drag his next victim closer!", 250.0f);

                            await Task.Delay(2500, ct);
                            if (targetPlayer != null && this != null && !ct.IsCancellationRequested)
                                await MoveTargetToMeAsync(targetPlayer, ct);

                            await Task.Delay(random.Next(8000, 12000), ct);
                        }
                    }
                    else if (playersInRange.Count == 1)
                    {
                        lastGrappleTarget = playersInRange.First();
                        if (!ct.IsCancellationRequested)
                            await MoveTargetToMeAsync(lastGrappleTarget, ct);
                    }

                    await Task.Delay(30000, ct);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown or despawn
            }
        }


        private async Task StartHotspotSpawnLoopAsync(CancellationToken ct)
        {
            var random = new Random();

            try
            {
                while (IsEnraged && IsAlive && CanAOE && !ct.IsCancellationRequested)
                {
                    var playersInRange = GetPlayersInRange(250.0f);
                    if (playersInRange.Count > 0)
                    {
                        var targetPlayer = playersInRange[random.Next(playersInRange.Count)];

                        if (targetPlayer == lastGrappleTarget && playersInRange.Count > 1)
                        {
                            targetPlayer = playersInRange.FirstOrDefault(p => p != lastGrappleTarget) ?? targetPlayer;
                        }

                        lastHotspotTarget = targetPlayer;
                        //Console.WriteLine($"[DEBUG] Targeted {targetPlayer.Name} for hotspot spawn.");
                        SpawnObjectAtPlayer(targetPlayer);
                    }

                    int nextHotspotTime = random.Next(10000, 15000);
                    await Task.Delay(nextHotspotTime, ct);
                }
            }
            catch (TaskCanceledException)
            {
                // Clean cancel; no logging necessary
            }
        }

        private async Task MoveTargetToMeAsync(Player targetPlayer, CancellationToken ct = default)
        {
            if (targetPlayer == null || ct.IsCancellationRequested)
                return;

            BroadcastMessage($"Get Over Here {targetPlayer.Name}!", 250.0f);

            var destination = new Position(this.Location);
            destination.Rotation = targetPlayer.Location.Rotation;

            WorldManager.ThreadSafeTeleport(targetPlayer, destination);

            // Wait before switching target
            await Task.Delay(2500, ct);
            if (ct.IsCancellationRequested) return;

            this.AttackTarget = targetPlayer;
            this.WakeUp();

            // Delay further AI switching
            await Task.Delay(6000, ct);
        }


        /// <summary>
        /// Retrieves all players within a specified range of the current creature.
        /// </summary>
        /// <param name="range">The range to search for players.</param>
        /// <returns>A list of players within the specified range.</returns>
        private List<Player> GetPlayersInRange(double range)
        {
            var playersInRange = new List<Player>();
            var mobPosition = new Position(Location); // Get mob's position

            foreach (var player in PlayerManager.GetAllOnline())
            {
                if (player == null || !player.IsAlive)
                    continue;

                var playerPosition = new Position(player.Location);
                if (mobPosition.SquaredDistanceTo(playerPosition) <= range * range) // Using squared distance
                {
                    playersInRange.Add(player);
                }
            }

            return playersInRange;
        }

        private void SpawnObjectAtPlayer(Player targetPlayer)
        {
            if (targetPlayer == null)
            {
                //Console.WriteLine("[DEBUG] No valid player for hotspot spawn.");
                return;
            }

           // Console.WriteLine($"[DEBUG] Spawning objects at {targetPlayer.Name}'s location.");

            // Define items and their respective messages
            var spawnOptions = new List<(int? DamageObjectId, int VisualObjectId, string Message)>
    {
        (90000411, 91000411, "A searing pool of acid forms at their location, better run away!"),
        (90000412, 91000412, "Fire bursts at their feet, move quickly!"),
        (90000413, 91000413, "A chilling frost gathers, flee to avoid being frozen!"),
        (90000414, 91000414, "A shocking vortex opens, get out of its pull!"),
        (90000415, 91000415, "A Sandstorm stirs up, move now or be torn to shreds!"),
        (90000416, 91000416, "A void emerges from the ground, quickly escape or wither away!"),
        (null, 90000417, "An iron cage falls from the sky, dodge it or be trapped!")
    };

            // Select a random object set
            var random = new Random();
            var (damageObjectId, visualObjectId, message) = spawnOptions[random.Next(0, spawnOptions.Count)];

            WorldObject damageObj = null;

            // **Create Damage Object if it's not null**
            if (damageObjectId.HasValue)
            {
                var damageWeenie = DatabaseManager.World.GetCachedWeenie((uint)damageObjectId.Value);
                if (damageWeenie != null)
                {
                    damageObj = WorldObjectFactory.CreateNewWorldObject(damageWeenie);
                    if (damageObj != null)
                    {
                        damageObj.Location = targetPlayer.Location.InFrontOf(0.01f, true);
                        damageObj.Location.LandblockId = new LandblockId(damageObj.Location.GetCell());
                        damageObj.EnterWorld();
                    }
                }
            }

            // **Create Visual Effect Object**
            var visualWeenie = DatabaseManager.World.GetCachedWeenie((uint)visualObjectId);
            if (visualWeenie == null) return;

            var visualObj = WorldObjectFactory.CreateNewWorldObject(visualWeenie);
            if (visualObj == null) return;

            visualObj.Location = targetPlayer.Location.InFrontOf(0.01f, true);
            visualObj.Location.LandblockId = new LandblockId(visualObj.Location.GetCell());
            visualObj.EnterWorld();

            //Console.WriteLine($"[DEBUG] Spawned {(damageObj != null ? "damage" : "visual-only")} object(s) at {targetPlayer.Name}'s location.");

            // Broadcast warning
            BroadcastMessage($"The enraged mob targets {targetPlayer.Name}! {message}", 250.0f);

            // Remove objects after 20 seconds
            RemoveObjectsAfterDelay(damageObj, visualObj, 20);
        }

        private void RemoveObjectsAfterDelay(WorldObject damageObj, WorldObject visualObj, int delaySeconds)
        {
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(delaySeconds);
            if (damageObj != null)
            {
                actionChain.AddAction(new ActionChain.ChainElement(
                    damageObj, new ActionEventDelegate(() => damageObj.DeleteObject())
                ));
            }
            actionChain.AddAction(new ActionChain.ChainElement(
                visualObj, new ActionEventDelegate(() => visualObj.DeleteObject())
            ));
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Broadcasts a message to all players within a certain range of the mob.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <param name="range">The range to broadcast within.</param>
        private void BroadcastMessage(string message, float range)
        {
            var playersInRange = GetPlayersInRange(range);
            foreach (var player in playersInRange)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
            }

           // Console.WriteLine($"[DEBUG] Broadcasted message: \"{message}\" to players within {range} range.");
        }

        /// <summary>
        /// Applies some amount of damage to this monster from source
        /// </summary>
        /// <param name="source">The attacker / source of damage</param>
        /// <param name="amount">The amount of damage rounded</param>
        public virtual uint TakeDamage(WorldObject source, DamageType damageType, float amount, bool crit = false)
        {
            var tryDamage = (int)Math.Round(amount);
            var damage = -UpdateVitalDelta(Health, -tryDamage);

            // TODO: update monster stamina?

            // source should only be null for combined DoT ticks from multiple sources
            if (source != null)
            {
                if (damage >= 0)
                    DamageHistory.Add(source, damageType, (uint)damage);
                else
                    DamageHistory.OnHeal((uint)-damage);
            }

           // Console.WriteLine($"[DEBUG] Current Health: {Health.Current}, Max Health: {Health.MaxValue}, Enrage Threshold: {EnrageHealthPercentage * 100}%");

            // Enrage logic: Check if the monster can enrage and meets the health threshold
            if (CanEnrage && !IsEnraged && Health.Current > 0)
            {
                float healthPercentage = (float)Health.Current / Health.MaxValue;
                // Use the dynamic threshold if set, otherwise default to EnrageHealthPercentage
                float enrageThreshold = (float)(GetProperty(PropertyFloat.EnrageThreshold) ?? EnrageHealthPercentage);
               // Console.WriteLine($"[DEBUG] {Name} Health Percentage: {healthPercentage * 100}%");

                if (healthPercentage <= enrageThreshold)
                {
                    Enrage();
                }
            }

            // Handle death

            if (Health.Current <= 0)
            {
                OnDeath(DamageHistory.LastDamager, damageType, crit);

                Die();
            }
            return (uint)Math.Max(0, damage);
        }

        public void EmitSplatter(Creature target, float damage)
        {
            if (target.IsDead) return;

            target.EnqueueBroadcast(new GameMessageSound(target.Guid, Sound.HitFlesh1, 0.5f));
            if (damage >= target.Health.MaxValue * 0.25f)
            {
                var painSound = (Sound)Enum.Parse(typeof(Sound), "Wound" + ThreadSafeRandom.Next(1, 3), true);
                target.EnqueueBroadcast(new GameMessageSound(target.Guid, painSound, 1.0f));
            }
            var splatter = (PlayScript)Enum.Parse(typeof(PlayScript), "Splatter" + GetSplatterHeight() + GetSplatterDir(target));
            target.EnqueueBroadcast(new GameMessageScript(target.Guid, splatter));
        }

        public CombatStyle AiAllowedCombatStyle
        {
            get => (CombatStyle)(GetProperty(PropertyInt.AiAllowedCombatStyle) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.AiAllowedCombatStyle); else SetProperty(PropertyInt.AiAllowedCombatStyle, (int)value); }
        }

        private static readonly ConcurrentDictionary<uint, BodyPartTable> BPTableCache = new ConcurrentDictionary<uint, BodyPartTable>();

        public static BodyPartTable GetBodyParts(uint wcid)
        {
            if (!BPTableCache.TryGetValue(wcid, out var bpTable))
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);

                bpTable = new BodyPartTable(weenie);
                BPTableCache[wcid] = bpTable;
            }
            return bpTable;
        }

        /// <summary>
        /// Flag indicates if a monster will aggro, but not attack
        /// </summary>
        public bool NeverAttack
        {
            get => GetProperty(PropertyBool.NeverAttack) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.NeverAttack); else SetProperty(PropertyBool.NeverAttack, value); }
        }
    }
}
