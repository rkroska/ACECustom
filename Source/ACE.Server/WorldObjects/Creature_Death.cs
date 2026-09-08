using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public TreasureDeath DeathTreasure { get => DeathTreasureType.HasValue ? DatabaseManager.World.GetCachedDeathTreasure(DeathTreasureType.Value) : null; }

        private bool onDeathEntered = false;

        /// <summary>
        /// Called when a monster or player dies, in conjunction with Die()
        /// </summary>
        /// <param name="lastDamager">The last damager that landed the death blow</param>
        /// <param name="damageType">The damage type for the death message</param>
        /// <param name="criticalHit">True if the death blow was a critical hit, generates a critical death message</param>
        public virtual DeathMessage OnDeath(DamageHistoryInfo lastDamager, DamageType damageType, bool criticalHit = false)
        {
            if (onDeathEntered)
            {
                if (lastDamager == null || lastDamager.Guid == Guid || lastDamager.TryGetAttacker() is Hotspot)
                    return Strings.General[1];

                var deathMessage = Strings.GetDeathMessage(damageType, criticalHit);
                if (criticalHit && this is Player)
                    deathMessage = Strings.PKCritical[0];
                return deathMessage;
            }

            onDeathEntered = true;

            IsTurning = false;
            IsMoving = false;

            grappleLoopCTS?.Cancel();
            hotspotLoopCTS?.Cancel();

            // Reset fog to Clear upon death only if the creature was enraged
            if (IsEnraged && CurrentLandblock != null)
            {
                var fogResetType = EnvironChangeType.Clear;
                CurrentLandblock.SendEnvironChange(fogResetType);
                //Console.WriteLine("[DEBUG] EnvironChange reset to Clear upon mob death (Enraged state detected).");
            }
            else if (IsEnraged)
            {
                //Console.WriteLine("[ERROR] CurrentLandblock is null. Unable to reset fog upon mob death.");
            }

            //QuestManager.OnDeath(lastDamager?.TryGetAttacker());

            // Greater Rifts: progress + guardian-death detection. Fast-bails for the whole normal world
            // (rift instances live exclusively at negative variations).
            ACE.Server.Managers.Rifts.RiftManager.OnCreatureDeath(this, lastDamager);

            if (KillQuest != null)
                OnDeath_HandleKillTask(KillQuest);
            if (KillQuest2 != null)
                OnDeath_HandleKillTask(KillQuest2);
            if (KillQuest3 != null)
                OnDeath_HandleKillTask(KillQuest3);

            if (!IsOnNoDeathXPLandblock)
                OnDeath_GrantXP();

            return GetDeathMessage(lastDamager, damageType, criticalHit);
        }


        public DeathMessage GetDeathMessage(DamageHistoryInfo lastDamagerInfo, DamageType damageType, bool criticalHit = false)
        {
            var lastDamager = lastDamagerInfo?.TryGetAttacker();

            if (lastDamagerInfo == null || lastDamagerInfo.Guid == Guid || lastDamager is Hotspot)
                return Strings.General[1];


            var deathMessage = Strings.GetDeathMessage(damageType, criticalHit);

            // ILT: always clear split arrow tracking — killer may not be a player
            var lastHitWasSplitArrow = GetProperty(PropertyBool.LastHitWasSplitArrow) is true;
            if (lastHitWasSplitArrow)
            {
                RemoveProperty(PropertyBool.LastHitWasSplitArrow);
                RemoveProperty(PropertyInstanceId.LastSplitArrowProjectile);
                RemoveProperty(PropertyInstanceId.LastSplitArrowShooter);
            }

            // if killed by a player, send them a message
            if (lastDamagerInfo.IsPlayer)
            {
                if (criticalHit && this is Player)
                    deathMessage = Strings.PKCritical[0];

                var killerMsg = string.Format(deathMessage.Killer, Name);

                if (lastDamager is Player playerKiller && playerKiller.Session != null)
                {
                    // ILT: build overkill suffix — applied AFTER split arrow text transformation
                    // inside GameEventKillerNotification, so [Overkill: N] is always last.
                    var overkillSuffix = (playerKiller.ShowOverkill && lastDamagerInfo.OverkillAmount > 0)
                        ? $" [Overkill: {Creature.FormatDamage(lastDamagerInfo.OverkillAmount, playerKiller.DamageNumberFormat)}]"
                        : "";

                    playerKiller.Session.Network.EnqueueSend(
                        new GameEventKillerNotification(playerKiller.Session, killerMsg, lastHitWasSplitArrow, overkillSuffix));
                }
            }
            return deathMessage;
        }


        /// <summary>
        /// Kills a player/creature and performs the full death sequence
        /// </summary>
        public void Die()
        {
            Die(DamageHistory.LastDamager, DamageHistory.TopDamager);
        }

        private bool dieEntered = false;

        /// <summary>
        /// Performs the full death sequence for non-Player creatures
        /// </summary>
        protected virtual void Die(DamageHistoryInfo lastDamager, DamageHistoryInfo topDamager)
        {
            if (dieEntered) return;

            dieEntered = true;

            UpdateVital(Health, 0);

            if (topDamager != null)
            {
                KillerId = topDamager.Guid.Full;

                if (topDamager.IsPlayer)
                {
                    var topDamagerPlayer = topDamager.TryGetAttacker();
                    if (topDamagerPlayer is Player playerKiller)
                    {
                        if (playerKiller.Session != null && playerKiller.Session.AccessLevel >= AccessLevel.Admin)
                            PlayerManager.BroadcastToAuditChannel(playerKiller, $"Admin {playerKiller.Name} killed {Name} (0x{Guid.Full:X8}) at {Location?.ToString() ?? "Unknown Location"}.");

                        playerKiller.CreatureKills = (playerKiller.CreatureKills ?? 0) + 1;
                    }
                }
            }

            CurrentMotionState = new Motion(MotionStance.NonCombat, MotionCommand.Ready);
            //IsMonster = false;
            if (PhysicsObj != null)
            {
                PhysicsObj.StopCompletely(true);
            }

            // broadcast death animation
            var motionDeath = new Motion(MotionStance.NonCombat, MotionCommand.Dead);
            var deathAnimLength = ExecuteMotion(motionDeath);

            // Try to generate Siphon Lens before death emotes (which might destroy the creature)
            if (!(this is Pet))
                GenerateSiphonLens(topDamager);

            if (EmoteManager != null)
            {
                EmoteManager.OnDeath(lastDamager);
            }            

            var dieChain = new ActionChain();

            // wait for death animation to finish
            //var deathAnimLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.Dead);
            dieChain.AddDelaySeconds(deathAnimLength);

            dieChain.AddAction(this, ActionType.CreatureDeath_MakeCorpse, () =>
            {
                CreateCorpse(topDamager);
                Destroy();
            });

            dieChain.EnqueueChain();
        }

        /// <summary>
        /// Called when an admin player uses the /smite command
        /// to instantly kill a creature
        /// </summary>
        public void Smite(WorldObject smiter, bool useTakeDamage = false)
        {
            if (useTakeDamage)
            {
                // deal remaining damage
                TakeDamage(smiter, DamageType.Bludgeon, Health.Current);
            }
            else
            {
                var smiterInfo = new DamageHistoryInfo(smiter);
                OnDeath(smiterInfo, DamageType.Undef);
                Die(smiterInfo, smiterInfo);
            }
        }

        public void OnDeath()
        {
            OnDeath(null, DamageType.Undef);
        }

        /// <summary>
        /// Grants XP to players in damage history
        /// </summary>
        public void OnDeath_GrantXP()
        {
            if (this is Player && PlayerKillerStatus == PlayerKillerStatus.PKLite)
                return;

            var totalHealth = DamageHistory.TotalHealth;

            if (totalHealth == 0)
                return;

            var monsterTier = PrestigeManager.GetKillScalingMonsterTier(this);

            var baseXp = (long)(XpOverride ?? 0);
            long? luminanceAward = LuminanceAward;

            // Owner ruling 2026-08-23: T11+ kill rewards are authored per zone by rank; weenie XpOverride/LuminanceAward are ignored when the zone sets them.
            var killProfile = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(this);
            if (killProfile != null)
            {
                // ONE key since 2026-09-02 (owner D3): xp_kill. The profile is already the mob's RANK
                // chain, so a Leader reads the Leader row's xp_kill and an unranked mob the Default
                // row's - the 08-29 / 08-31 branch logic (minion master key, unranked pays minion)
                // now lives in the resolver's layer order instead of here.
                if (killProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.XpKill))
                    baseXp = (long)Math.Round(killProfile.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.XpKill));
                if (killProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.LumAward))
                {
                    var zoneLum = (long)Math.Round(killProfile.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.LumAward));
                    luminanceAward = zoneLum > 0 ? zoneLum : null;   // zone-provided 0 = grant nothing
                }
            }

            // One EarnXP / EarnLuminance per player: combine direct hits + all of that player's combat pets.
            // Avoids duplicate fellowship splits and matches "your kill bonuses apply to the full credit you earned on the mob."
            var xpCreditByPlayer = new Dictionary<uint, float>();
            foreach (var kvp in DamageHistory.TotalDamage)
            {
                var info = kvp.Value;
                if (info.TotalDamage <= 0)
                    continue;

                var damager = info.TryGetAttacker();
                Player creditPlayer = damager as Player;
                if (creditPlayer == null && info.PetOwner != null)
                    creditPlayer = info.TryGetPetOwner();

                if (creditPlayer == null)
                    continue;

                var id = creditPlayer.Guid.Full;
                if (xpCreditByPlayer.TryGetValue(id, out var acc))
                    xpCreditByPlayer[id] = acc + info.TotalDamage;
                else
                    xpCreditByPlayer[id] = info.TotalDamage;
            }

            foreach (var kv in xpCreditByPlayer)
            {
                var player = PlayerManager.GetOnlinePlayer(new ObjectGuid(kv.Key));
                if (player == null)
                    continue;

                var damagePercent = kv.Value / totalHealth;
                if (damagePercent <= 0)
                    continue;

                if (baseXp > 0)
                {
                    var totalXP = baseXp * damagePercent;
                    player.EarnXP((long)Math.Round(totalXP), XpType.Kill, ShareType.All, monsterTier);
                }

                if (luminanceAward != null)
                {
                    var totalLuminance = luminanceAward.Value * damagePercent;
                    player.EarnLuminance((long)Math.Round(totalLuminance), XpType.Kill, ShareType.All, monsterTier);
                }

                // Launch-day diagnostic (2026-08-23): the client filters XP/lum chat, so the log is the readout.
                // One line per player per governed kill - switchable via zc_killxp_diag (review 2026-09-04).
                if (killProfile != null && ServerConfig.zc_killxp_diag.Value)
                {
                    var (zcRank, rankSrc) = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveRankForCreature(this);
                    var rank = ACE.Server.Managers.ZoneScaling.ZoneRank.Key(zcRank) + "/" + rankSrc;
                    log.Info($"[KILLXP] {Name} ({WeenieClassId}, {rank}) -> {player.Name}: share {damagePercent:P0}, xp {(long)Math.Round(baseXp * damagePercent):N0} of {baseXp:N0}, lum {(luminanceAward.HasValue ? ((long)Math.Round(luminanceAward.Value * damagePercent)).ToString("N0") : "none")}, zone-authored xp={killProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.XpKill)} lum={killProfile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.LumAward)}");
                }
            }

            // Pet Bonding System - bond XP scales with pet damage share and the owner's kill XP modifier stack (same profile as EarnXP).
            // Accumulate per summoning device so TryAwardBondXp (save + network) runs once per device per kill.
            if (ServerConfig.pet_bond_enabled.Value && baseXp > 0)
            {
                var bondXpByDevice = new Dictionary<uint, (Player Owner, long Xp)>();

                foreach (var kvp in DamageHistory.TotalDamage)
                {
                    var info = kvp.Value;
                    if (info.TotalDamage <= 0)
                        continue;

                    var damager = info.TryGetAttacker();
                    if (damager is not CombatPet combatPet || info.PetOwner == null)
                        continue;

                    var playerDamager = info.TryGetPetOwner();
                    if (playerDamager == null)
                        continue;

                    var damagePercent = info.TotalDamage / totalHealth;
                    if (damagePercent <= 0)
                        continue;

                    var bondXp = (long)Math.Round(baseXp * damagePercent * ServerConfig.pet_bond_xp_multiplier.Value * playerDamager.GetKillXpModifierProduct());
                    var minAward = ServerConfig.pet_bond_xp_min_award.Value;
                    if (bondXp < minAward) bondXp = minAward;

                    PetDevice device = combatPet.TryGetSummoningDevice();
                    if (device == null)
                    {
                        var devGuid = combatPet.SummoningDeviceGuid;
                        if (devGuid != ObjectGuid.Invalid)
                        {
                            device = playerDamager.FindObject(devGuid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) as PetDevice;
                        }
                    }

                    if (device == null)
                        continue;

                    var key = device.Guid.Full;
                    if (bondXpByDevice.TryGetValue(key, out var acc))
                        bondXpByDevice[key] = (playerDamager, acc.Xp + bondXp);
                    else
                        bondXpByDevice[key] = (playerDamager, bondXp);
                }

                foreach (var kv in bondXpByDevice)
                {
                    var (owner, xp) = kv.Value;
                    var device = owner.FindObject(kv.Key, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) as PetDevice;
                    if (device == null)
                        continue;

                    var awarded = device.TryAwardBondXp(owner, xp, out var leveledUp);
                    if (awarded && leveledUp)
                        owner.SendMessage($"Your bond with {device.GetBondMessageDisplayName()} deepens. (Bond Level {device.PetBondLevel:N0})");
                }
            }

            PetPotency.TryAwardResidueOnKill(this);
        }

        /// <summary>
        /// Handles the KillTask for a killed creature
        /// </summary>
        public void OnDeath_HandleKillTask(string killQuest)
        {
            /*var receivers = KillTask_GetEligibleReceivers(killQuest);

            foreach (var receiver in receivers)
            {
                var damager = receiver.Value.TryGetAttacker();

                var player = damager as Player;

                if (player == null && receiver.Value.PetOwner != null)
                    player = receiver.Value.TryGetPetOwner();

                if (player != null)
                    player.QuestManager.HandleKillTask(killQuest, this);
            }*/

            // new method

            // with full fellowship support and new config option for capping,
            // building a pre-flattened structure is no longer really necessary,
            // and we can do this more iteratively.

            // one caveat to do this, we need to keep track of player and summoning caps separately
            // this is to prevent ordering bugs, such as a player being processed after a summon,
            // and already being at the 1 cap for players

            var summon_credit_cap = (int)ServerConfig.summoning_killtask_multicredit_cap.Value - 1;

            var playerCredits = new Dictionary<ObjectGuid, int>();
            var summonCredits = new Dictionary<ObjectGuid, int>();

            // this option isn't really needed anymore, but keeping it around for compatibility
            // it is now synonymous with summoning_killtask_multicredit_cap <= 1
            if (!ServerConfig.allow_summoning_killtask_multicredit.Value)
                summon_credit_cap = 0;

            foreach (var kvp in DamageHistory.TotalDamage)
            {
                if (kvp.Value.TotalDamage <= 0)
                    continue;

                var damager = kvp.Value.TryGetAttacker();

                var combatPet = false;

                var playerDamager = damager as Player;

                if (playerDamager == null && kvp.Value.PetOwner != null)
                {
                    playerDamager = kvp.Value.TryGetPetOwner();
                    combatPet = true;
                }

                if (playerDamager == null)
                    continue;

                var killTaskCredits = combatPet ? summonCredits : playerCredits;

                var cap = combatPet ? summon_credit_cap : 1;

                if (cap <= 0)
                {
                    // handle special case: use playerCredits
                    killTaskCredits = playerCredits;
                    cap = 1;
                }

                if (playerDamager.QuestManager.HasQuest(killQuest))
                {
                    TryHandleKillTask(playerDamager, killQuest, killTaskCredits, cap);
                }
                // check option that requires killer to have killtask to pass to fellows
                else if (!ServerConfig.fellow_kt_killer.Value)   
                {
                    continue;
                }

                if (playerDamager.Fellowship == null)
                    continue;

                // share with fellows in kill task range
                var fellows = playerDamager.Fellowship.WithinRange(playerDamager);

                foreach (var fellow in fellows)
                {
                    if (fellow.QuestManager.HasQuest(killQuest))
                        TryHandleKillTask(fellow, killQuest, killTaskCredits, cap);
                }
            }
        }

        public bool TryHandleKillTask(Player player, string killTask, Dictionary<ObjectGuid, int> killTaskCredits, int cap)
        {
            if (killTaskCredits.TryGetValue(player.Guid, out var currentCredits))
            {
                if (currentCredits >= cap)
                    return false;

                killTaskCredits[player.Guid]++;
            }
            else
                killTaskCredits[player.Guid] = 1;

            player.QuestManager.HandleKillTask(killTask, this);

            return true;
        }

        /// <summary>
        /// Returns a flattened structure of eligible Players, Fellows, and CombatPets
        /// </summary>
        public Dictionary<ObjectGuid, DamageHistoryInfo> KillTask_GetEligibleReceivers(string killQuest)
        {
            // http://acpedia.org/wiki/Announcements_-_2012/12_-_A_Growing_Twilight#Release_Notes

            var questName = QuestManager.GetQuestName(killQuest);

            // we are using DamageHistoryInfo here, instead of Creature or WorldObjectInfo
            // WeakReference<CombatPet> may be null for expired CombatPets, but we still need the WeakReference<PetOwner> references

            var receivers = new Dictionary<ObjectGuid, DamageHistoryInfo>();

            foreach (var kvp in DamageHistory.TotalDamage)
            {
                if (kvp.Value.TotalDamage <= 0)
                    continue;

                var damager = kvp.Value.TryGetAttacker();

                var playerDamager = damager as Player;

                if (playerDamager == null && kvp.Value.PetOwner != null)
                {
                    // handle combat pets
                    playerDamager = kvp.Value.TryGetPetOwner();

                    if (playerDamager != null && playerDamager.QuestManager.HasQuest(questName))
                    {
                        // only add combat pet to eligible receivers if player has quest, and allow_summoning_killtask_multicredit = true (default, retail)
                        if (DamageHistory.HasDamager(playerDamager, true) && ServerConfig.allow_summoning_killtask_multicredit.Value)
                            receivers[kvp.Value.Guid] = kvp.Value;  // add CombatPet
                        else
                            receivers[playerDamager.Guid] = new DamageHistoryInfo(playerDamager);   // add dummy profile for PetOwner
                    }

                    // regardless if combat pet is eligible, we still want to continue traversing to the pet owner, and possibly fellows

                    // in a scenario where combat pet does 100% damage:

                    // - regardless if allow_summoning_killtask_multicredit is enabled/disabled, it should continue traversing into pet owner and possibly their fellows

                    // - if pet owner doesn't have kill task, and fellow_kt_killer=false, any fellows with the task should still receive 1 credit
                }

                if (playerDamager == null)
                    continue;

                // factors:
                // - has quest
                // - is killer (last damager, top damager, or any damager? in current context, considering it to be any damager)
                // - has fellowship
                // - server option: fellow_kt_killer
                // - server option: fellow_kt_landblock

                if (playerDamager.QuestManager.HasQuest(questName))
                {
                    // just add a fake DamageHistoryInfo for reference
                    receivers[playerDamager.Guid] = new DamageHistoryInfo(playerDamager);
                }
                else if (ServerConfig.fellow_kt_killer.Value)
                {
                    // if this option is enabled (retail default), the killer is required to have kill task
                    // for it to share with fellowship
                    continue;
                }

                // we want to add fellowship members in a flattened structure
                // in this inner loop, instead of the outer loop

                // scenarios:

                // i am a summoner in a fellowship with 1 other player
                // we both have a killtask

                // - my combatpet does 100% damage to the monster
                // result: i get 1 killtask credit, and my fellow gets 1 killtask credit

                // - my combatpet does 50% damage to monster, and i do 50% damage
                // result: i get 2 killtask credits (1 if allow_summoning_killtask_multicredit server option is disabled), and my fellow gets 1 killtask credit
                // after update should be 2/2, instead of 2/1

                // - my combatpet does 33% damage to monster, i do 33% damage, and fellow does 33% damage
                // result: same as previous scenario
                // after update should be 2/2, instead of 2/1 again

                // 2 players not in a fellowship both have a killtask
                // they each do 50% damage to monster

                // result: both players receive killtask credit

                if (playerDamager.Fellowship == null)
                    continue;

                // share with fellows in kill task range
                var fellows = playerDamager.Fellowship.WithinRange(playerDamager);

                foreach (var fellow in fellows)
                {
                    if (fellow.QuestManager.HasQuest(questName))
                        receivers[fellow.Guid] = new DamageHistoryInfo(fellow);
                }
            }
            return receivers;
        }

        /// <summary>
        /// Create a corpse for both creatures and players currently
        /// </summary>
        protected void CreateCorpse(DamageHistoryInfo killer, bool hadVitae = false)
        {
            if (this is Player decedent && (decedent.IsAdmin || (decedent.Session != null && decedent.Session.AccessLevel >= AccessLevel.Admin)))
            {
                PlayerManager.BroadcastToAuditChannel(decedent, $"Admin {decedent.Name} has died. (Admin Death - No Corpse Created)");
                return;
            }

            if (NoCorpse)
            {
                if (killer != null && killer.IsOlthoiPlayer) return;

                // PetDevice summons (Pet / CombatPet): no death treasure or ground drops (DeathTreasure, createlist, siphon lens)
                if (this is Pet)
                    return;

                var loot = GenerateTreasure(killer, null);

                foreach(var item in loot)
                {
                    if (!string.IsNullOrEmpty(item.Quest)) // if the item has a Quest string, make the creature a "generator" of the item so that the pickup action applies the quest. 
                        item.GeneratorId = Guid.Full; 
                    item.Location = new Position(Location);
                    LandblockManager.AddObject(item);
                }
                return;
            }

            var cachedWeenie = DatabaseManager.World.GetCachedWeenie("corpse");

            var corpse = WorldObjectFactory.CreateNewWorldObject(cachedWeenie) as Corpse;

            var prefix = "Corpse";

            if (TreasureCorpse)
            {
                // Hardcoded values from PCAPs of Treasure Pile Corpses, everything else lines up exactly with existing corpse weenie
                corpse.SetupTableId  = 0x02000EC4;
                corpse.MotionTableId = 0x0900019B;
                corpse.SoundTableId  = 0x200000C2;
                corpse.ObjScale      = 0.4f;

                prefix = "Treasure";
            }
            else
            {
                corpse.SetupTableId = SetupTableId;
                corpse.MotionTableId = MotionTableId;
                //corpse.SoundTableId = SoundTableId; // Do not change sound table for corpses
                corpse.PaletteBaseDID = PaletteBaseDID;
                corpse.ClothingBase = ClothingBase;
                corpse.PhysicsTableId = PhysicsTableId;

                if (ObjScale.HasValue)
                    corpse.ObjScale = ObjScale;
                if (PaletteTemplate.HasValue)
                    corpse.PaletteTemplate = PaletteTemplate;
                if (Shade.HasValue)
                    corpse.Shade = Shade;
                //if (Translucency.HasValue) // Shadows have Translucency but their corpses do not, videographic evidence can be found on YouTube.
                //corpse.Translucency = Translucency;


                // Pull and save objdesc for correct corpse apperance at time of death
                var objDesc = CalculateObjDesc();

                corpse.Biota.PropertiesAnimPart = objDesc.AnimPartChanges.Clone(corpse.BiotaDatabaseLock);

                corpse.Biota.PropertiesPalette = objDesc.SubPalettes.Clone(corpse.BiotaDatabaseLock);

                corpse.Biota.PropertiesTextureMap = objDesc.TextureChanges.Clone(corpse.BiotaDatabaseLock);
            }

            // use the physics location for accuracy,
            // especially while jumping
            corpse.Location = PhysicsObj.Position.ACEPosition();
            if (!corpse.Location.Variation.HasValue && Location.Variation.HasValue)
                corpse.Location.Variation = Location.Variation;

            corpse.VictimId = Guid.Full;
            corpse.Name = $"{prefix} of {Name}";

            // set 'killed by' for looting rights
            var killerName = "misadventure";
            if (killer != null)
            {
                if (!(Generator != null && Generator.Guid == killer.Guid) && Guid != killer.Guid)
                {
                    if (!string.IsNullOrWhiteSpace(killer.Name))
                        killerName = killer.Name.TrimStart('+');  // vtank requires + to be stripped for regex matching.

                    corpse.KillerId = killer.Guid.Full;

                    if (killer.TryGetPetOwner() is Player petLootPlayer)
                        corpse.KillerId = petLootPlayer.Guid.Full;
                    else if (killer.TryGetAttacker() is CombatPet killerPet && killerPet.P_PetOwner != null)
                        corpse.KillerId = killerPet.P_PetOwner.Guid.Full;
                }
            }

            corpse.LongDesc = $"Killed by {killerName}.";

            bool saveCorpse = false;

            var player = this as Player;

            if (player != null)
            {
                corpse.SetPosition(PositionType.Location, corpse.Location);

                var killerIsOlthoiPlayer = killer != null && killer.IsOlthoiPlayer;
                var killerIsPkPlayer = killer != null && killer.IsPlayer && killer.Guid != Guid;

                //var dropped = killer != null && killer.IsOlthoiPlayer ? player.CalculateDeathItems_Olthoi(corpse, hadVitae) : player.CalculateDeathItems(corpse);

                if (killerIsOlthoiPlayer || player.IsOlthoiPlayer)
                {
                    var dropped = player.CalculateDeathItems_Olthoi(corpse, hadVitae, killerIsOlthoiPlayer, killerIsPkPlayer);

                    foreach (var wo in dropped)
                        DoModifierLogging(killer, wo);

                    corpse.RecalculateDecayTime(player);

                    if (dropped.Count > 0)
                        saveCorpse = true;

                    corpse.PkLevel = PKLevel.PK;
                }
                else
                {
                    // If player is cloaked as creature, handle creature death mechanics
                    if (player.CloakStatus == CloakStatus.Creature)
                    {
                        // Mark corpse as monster for creature rot timer
                        corpse.IsMonster = true;

                        // Try to get the creature weenie from WeenieClassId (if morphed)
                        var creatureWeenie = DatabaseManager.World.GetCachedWeenie(player.WeenieClassId);
                        if (creatureWeenie != null)
                        {
                            // Check if this is a creature weenie
                            var isCreatureWeenie = creatureWeenie.WeenieType == WeenieType.Creature ||
                                                  creatureWeenie.WeenieType == WeenieType.Cow ||
                                                  creatureWeenie.WeenieType == WeenieType.Pet ||
                                                  creatureWeenie.WeenieType == WeenieType.CombatPet;

                            if (isCreatureWeenie)
                            {
                                // Set TimeToRot from creature weenie (use creature rot timer, not player rot timer)
                                var creatureTimeToRot = creatureWeenie.GetProperty(PropertyFloat.TimeToRot);
                                if (creatureTimeToRot.HasValue)
                                    corpse.TimeToRot = creatureTimeToRot.Value;
                                else
                                    // Default creature rot time if not specified (5 minutes)
                                    corpse.TimeToRot = 300;

                                // Get DeathTreasureType from the creature weenie
                                var deathTreasureType = creatureWeenie.GetProperty(PropertyDataId.DeathTreasureType);
                                if (deathTreasureType.HasValue)
                                {
                                    var deathTreasure = DatabaseManager.World.GetCachedDeathTreasure(deathTreasureType.Value);
                                    if (deathTreasure != null)
                                    {
                                        // Generate creature loot
                                        var lootItems = LootGenerationFactory.CreateRandomLootObjects(deathTreasure);
                                        foreach (var item in lootItems)
                                        {
                                            corpse.TryAddToInventory(item);
                                            DoModifierLogging(killer, item);
                                        }
                                    }
                                }
                                // Even if no loot, corpse is still created (empty)
                            }
                        }
                    }
                    else
                    {
                        // Normal player death handling
                        var dropped = player.CalculateDeathItems(corpse);

                        corpse.RecalculateDecayTime(player);

                        if (dropped.Count > 0)
                            saveCorpse = true;

                        if ((player.Location.Cell & 0xFFFF) < 0x100)
                        {
                            player.SetPosition(PositionType.LastOutsideDeath, new Position(corpse.Location));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePosition(player, PositionType.LastOutsideDeath, corpse.Location));

                            if (dropped.Count > 0)
                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your corpse is located at ({corpse.Location.GetMapCoordStr()}).", ChatMessageType.Broadcast));
                        }
                        else
                        {
                            if (dropped.Count > 0)
                            {
                                var dungeonName = DungeonNameResolver.Resolve(corpse.Location.Landblock, corpse.Location.Variation ?? 0);
                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your corpse is located in {dungeonName}.", ChatMessageType.Broadcast));
                            }
                        }

                        var isPKdeath = player.IsPKDeath(killer);
                        var isPKLdeath = player.IsPKLiteDeath(killer);

                        if (isPKdeath)
                            corpse.PkLevel = PKLevel.PK;

                        if (!isPKdeath && !isPKLdeath)
                        {
                            var miserAug = player.AugmentationLessDeathItemLoss * 5;
                            if (miserAug > 0)
                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your augmentation has reduced the number of items you can lose by {miserAug}!", ChatMessageType.Broadcast));
                        }

                        if (dropped.Count == 0 && !isPKLdeath)
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have retained all your items. You do not need to recover your corpse!", ChatMessageType.Broadcast));
                    }
                }
            }
            else
            {
                corpse.IsMonster = true;

                // Copy TimeToRot from Monster to Corpse
                corpse.TimeToRot = TimeToRot;

                if (killer == null || !killer.IsOlthoiPlayer)
                    GenerateTreasure(killer, corpse);
                else
                    GenerateTreasure_Olthoi(killer, corpse);

                if (killer != null && killer.IsPlayer && !killer.IsOlthoiPlayer)
                {
                    if (Level >= 100)
                    {
                        CanGenerateRare = true;
                    }
                    else
                    {
                        var killerPlayer = killer.TryGetAttacker();
                        if (killerPlayer != null && Level > killerPlayer.Level)
                            CanGenerateRare = true;
                    }
                }
                else
                    CanGenerateRare = false;
            }

            corpse.RemoveProperty(PropertyInt.Value);

            if (CanGenerateRare && killer != null)
                corpse.TryGenerateRare(killer);

            corpse.InitPhysicsObj(Location.Variation);

            // persist the original creature velocity (only used for falling) to corpse
            corpse.PhysicsObj.Velocity = PhysicsObj.Velocity;

            corpse.EnterWorld();

            if (player != null)
            {
                if (corpse.PhysicsObj == null || corpse.PhysicsObj.Position == null)
                    log.Debug($"[CORPSE] {Name}'s corpse (0x{corpse.Guid}) failed to spawn! Tried at {player.Location}");
                else
                    log.Debug($"[CORPSE] {Name}'s corpse (0x{corpse.Guid}) is located at {corpse.PhysicsObj.Position}");
            }

            if (saveCorpse)
            {
                var biotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();
                var savedObjects = new List<WorldObject>();

                // Save corpse
                corpse.SaveBiotaToDatabase(false);
                biotas.Add((corpse.Biota, corpse.BiotaDatabaseLock));
                savedObjects.Add(corpse);

                // Save all items in corpse
                foreach (var item in corpse.Inventory.Values)
                {
                    item.SaveBiotaToDatabase(false);
                    biotas.Add((item.Biota, item.BiotaDatabaseLock));
                    savedObjects.Add(item);
                }

                // Bulk save with callback to clear SaveInProgress flags
                DatabaseManager.Shard.SaveBiotasInParallel(
                    biotas,
                    result =>
                    {
                        var clearFlagsAction = new ACE.Server.Entity.Actions.ActionChain();
                        clearFlagsAction.AddAction(WorldManager.ActionQueue, ActionType.CreatureDeath_SaveInParallelCallback, () =>
                        {
                            foreach (var wo in savedObjects)
                            {
                                if (!wo.IsDestroyed)
                                {
                                    wo.SaveInProgress = false;
                                    wo.SaveStartTime = DateTime.MinValue; // Reset for next save
                                }
                            }

                            if (!result)
                            {
                                log.Warn($"[CORPSE SAVE] Bulk save for corpse {corpse.Guid} returned false; SaveInProgress flags cleared to avoid stuck state.");
                            }
                        });
                        clearFlagsAction.EnqueueChain();
                    },
                    $"CorpseSave:{corpse.Guid}");
            }
        }

        public bool CanGenerateRare
        {
            get => GetProperty(PropertyBool.CanGenerateRare) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.CanGenerateRare); else SetProperty(PropertyBool.CanGenerateRare, value); }
        }

        /// <summary>
        /// Transfers generated treasure from creature to corpse
        /// </summary>
        private List<WorldObject> GenerateTreasure(DamageHistoryInfo killer, Corpse corpse)
        {
            var droppedItems = new List<WorldObject>();
            var tier = PrestigeManager.GetKillScalingMonsterTier(this);

            // Zone Scaler loot: an authored profile can bump the loot tier/quality/quantity and inject bonus
            // currency for this mob (null for players/exempt/non-endgame/no-match -> normal loot).
            var zoneLoot = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(this);

            // Zone loot FLOOR (owner 2026-08-23): anything that dies inside a governed variant zone (v11+)
            // drops that variation's set - a mis-tiered T8/T9/T10 WCID, a retail mob with its own table,
            // or a retail mob with NO table (it gets the zone fallback profile). Exempt creatures never
            // reach here (zoneLoot is null for them), T11+ profiles are untouched (max, not add).
            var zoneFloorTier = zoneLoot != null ? (Location?.Variation ?? 0) : 0;
            if (zoneFloorTier < ACE.Server.Managers.ZoneControl.ZoneControlManager.MinBoundedVariation) zoneFloorTier = 0;
            var deathTreasure = DeathTreasure;
            if (deathTreasure == null && zoneFloorTier > 0)
                deathTreasure = DatabaseManager.World.GetCachedDeathTreasure(LootGenerationFactory.ZoneLootFallbackProfile);

            // create death treasure from loot generation factory
            if (deathTreasure != null)
            {
                // Zone Control loot tier = the zone floor: max(own tier, variation). (loot_tier_bonus,
                // loot_quantity_mult, loot_quality_mult removed 2026-08-23.)
                var effectiveTreasure = deathTreasure;
                if (zoneFloorTier > 0 && effectiveTreasure.Tier < zoneFloorTier)
                {
                    effectiveTreasure = CloneTreasureDeath(effectiveTreasure);
                    effectiveTreasure.Tier = zoneFloorTier;

                    // ZONE SET ONLY (owner 2026-08-30). Raising the tier alone left the mob's OWN
                    // retail chances intact, so a sub-tier-11 profile dying inside a v11+ zone rolled
                    // its full retail payload AT THE RAISED TIER and then got the zone set on top -
                    // e.g. tier-10 profile 3007 is 100/100/100 with 10 magic items, so ~11.5 extra
                    // items per corpse. That is exactly the case this floor exists to serve (its own
                    // comment names "a mis-tiered T8/T9/T10 WCID"), so it was a live double-drop on
                    // the design's intended path - it just was not reachable yet, because no 3007 mob
                    // is placed or generator-spawned in any authored zone landblock today.
                    // The floor's contract is that such a mob drops THAT VARIATION'S SET, not its own
                    // loot as well, so the three retail roll groups are zeroed here. Profiles already
                    // at tier 11+ never enter this branch and are untouched; a mob with NO table of
                    // its own already gets ZoneLootFallbackProfile (73001), which is all zeros too -
                    // so after this every zone drop comes from one place.
                    effectiveTreasure.ItemChance = 0;
                    effectiveTreasure.MagicItemChance = 0;
                    effectiveTreasure.MundaneItemChance = 0;
                }

                List<WorldObject> items = LootGenerationFactory.CreateRandomLootObjects(effectiveTreasure);

                // Structured loot set: blank weapons + per-slot gear
                // (items join the normal per-item mutation/corpse pipeline below).
                //
                // This is the DEFAULT for tier-11+ profiles and does not depend on Zone Control --
                // those profiles carry zero item chances of their own, so without this they drop
                // nothing. Every slot has its own count (default 1 at tier 11+, 0 below); a zone
                // profile overrides individual slots via the loot_slot_* stats. There is no
                // separate enable flag: a slot at 0 is off, and a zone can turn any slot on below
                // tier 11 by giving it a count.
                var slotCounts = LootGenerationFactory.ZoneLootSetCounts.TierDefault(effectiveTreasure.Tier);

                if (zoneLoot != null)
                {
                    // Per-slot count. The loot_slot_<slot> stat is the MIN; the optional
                    // loot_slot_<slot>_max turns it into a RANGE rolled uniform-inclusive, per slot,
                    // per kill (owner 2026-08-24: "1-2 Weapons, 3-5 Chest", independent per slot).
                    // Max undefined - the default and the pre-2026-08-24 behaviour - is an exact count.
                    // Reversed pairs auto-swap, matching every other min/max pair in the profile.
                    int Slot(string stat, int tierDefault)
                    {
                        var lo = (int)Math.Round(zoneLoot.Get(stat, tierDefault));
                        var maxStat = stat + "_max";
                        if (!zoneLoot.Has(maxStat))
                            return lo;
                        var hi = (int)Math.Round(zoneLoot.Get(maxStat, lo));
                        if (hi < lo)
                            (lo, hi) = (hi, lo);
                        return hi > lo ? ACE.Common.ThreadSafeRandom.Next(lo, hi) : lo;   // inclusive both ends
                    }

                    slotCounts.Weapons = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotWeapons, slotCounts.Weapons);
                    slotCounts.Helm = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotHelm, slotCounts.Helm);
                    slotCounts.Chest = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotChest, slotCounts.Chest);
                    slotCounts.Shoulder = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotShoulder, slotCounts.Shoulder);
                    slotCounts.Bracer = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotBracer, slotCounts.Bracer);
                    slotCounts.Glove = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotGlove, slotCounts.Glove);
                    slotCounts.Girth = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotGirth, slotCounts.Girth);
                    slotCounts.UpperLeg = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotUpperLeg, slotCounts.UpperLeg);
                    slotCounts.LowerLeg = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotLowerLeg, slotCounts.LowerLeg);
                    slotCounts.Boot = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotBoot, slotCounts.Boot);
                    slotCounts.Shield = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotShield, slotCounts.Shield);
                    slotCounts.Amulet = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotAmulet, slotCounts.Amulet);
                    slotCounts.Ring = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotRing, slotCounts.Ring);
                    slotCounts.Bracelet = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotBracelet, slotCounts.Bracelet);
                    slotCounts.Trinket = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotTrinket, slotCounts.Trinket);
                    slotCounts.Cloak = Slot(ACE.Server.Managers.ZoneScaling.ZoneStat.LootSlotCloak, slotCounts.Cloak);
                }
                // LEGACY per-slot mode has no budget: its aggregate must respect the corpse cap too (the budget path
                // clamps below). Weapons is a multiplier over the nine families.
                if (zoneLoot != null && !zoneLoot.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.LootDropsMin))
                {
                    var perSlotTotal = slotCounts.Weapons * 9 + slotCounts.Helm + slotCounts.Chest + slotCounts.Shoulder + slotCounts.Bracer
                        + slotCounts.Glove + slotCounts.Girth + slotCounts.UpperLeg + slotCounts.LowerLeg + slotCounts.Boot + slotCounts.Shield
                        + slotCounts.Amulet + slotCounts.Ring + slotCounts.Bracelet + slotCounts.Trinket + slotCounts.Cloak;
                    if (perSlotTotal > ZoneCorpseItemCap)
                    {
                        log.Warn($"[ZoneLoot] {Name} (0x{Guid}): per-slot counts total {perSlotTotal}, above the corpse cap {ZoneCorpseItemCap}; rolling a capped budget instead.");
                        slotCounts = LootGenerationFactory.RollBudgetedCounts(slotCounts, ZoneCorpseItemCap, 1.0, 1.0, 1.0, 1.0);
                    }
                }

                // BUDGET MODE (owner 2026-08-24): defining loot_max_drops switches from "every slot
                // drops its own count" to "roll this many ITEMS total, distributed by category weight".
                // The loot_slot_* values then mean WEIGHT WITHIN CATEGORY rather than count, and the
                // budget is a CEILING - armor coverage credit can land the corpse under it. Slot
                // specials below are deliberately OUTSIDE the budget. Unset = legacy, unchanged.
                if (zoneLoot != null && zoneLoot.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.LootDropsMin))
                {
                    var budgetLo = (int)Math.Round(zoneLoot.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.LootDropsMin, 0.0));
                    var budgetHi = budgetLo;
                    if (zoneLoot.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.LootDropsMax))
                        budgetHi = (int)Math.Round(zoneLoot.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.LootDropsMax, budgetLo));
                    if (budgetHi < budgetLo)
                        (budgetLo, budgetHi) = (budgetHi, budgetLo);
                    var budget = budgetHi > budgetLo ? ACE.Common.ThreadSafeRandom.Next(budgetLo, budgetHi) : budgetLo;

                    // A corpse holds 120 items (Corpse.cs:60-61) and TryAddToInventory's result is
                    // DISCARDED at the fill sites below - so anything past 120 is silently destroyed,
                    // not dropped to the ground and not logged. Clamp here rather than trust authoring.
                    budget = Math.Min(budget, ZoneCorpseItemCap);

                    slotCounts = LootGenerationFactory.RollBudgetedCounts(
                        slotCounts, budget,
                        zoneLoot.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.LootWeightWeapon, 1.0),
                        zoneLoot.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.LootWeightArmor, 1.0),
                        zoneLoot.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.LootWeightJewelry, 1.0),
                        zoneLoot.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.LootWeightCloak, 1.0));
                }

                if (slotCounts.Any)
                    items.AddRange(LootGenerationFactory.CreateZoneLootSet(effectiveTreasure, slotCounts));

                // Armor v2 slot special (owner 2026-08-21): ONE roll per KILL, retail-rare model
                // (1 in special_odds; IsZcBoss divides by special_boss_mult, IsZcLeader by
                // special_leader_mult). On a hit pick one launch special at random and stamp the
                // dropped piece of its slot (spawn one if the set has none). That piece becomes a
                // PERFECT piece: core four + every line at band MAX (forceMax below). The flag is a
                // LOCAL, never a prop - a 50200+ marker would be summed into the worn cache.
                WorldObject specialPiece = null;
                ACE.Server.Managers.ZoneControl.ZoneModifiers.Def specialDef = null;
                // Zone Control off: no slot special is rolled at all (owner 2026-08-23) - the fallback
                // is T10 max-rolled gear, which has no such thing.
                if (zoneLoot != null && ServerConfig.zonecontrol_enabled.Value
                    && effectiveTreasure.Tier >= LootGenerationFactory.ZoneLootSetMinTier)
                {
                    // special_odds is ABSOLUTE per rank since 2026-09-02 (owner D4): the profile is the
                    // mob's rank chain, so a Boss reads the Boss row's own denominator. The old
                    // special_boss_mult / special_leader_mult divisors were folded into those rows
                    // by the migration SQL (151,200 / 3 and / 2 at T11).
                    var odds = zoneLoot.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.SpecialOdds, 750000.0);
                    var denom = Math.Max(1, (int)Math.Round(odds));

                    if (ACE.Common.ThreadSafeRandom.Next(1, denom) == 1)
                    {
                        var specials = ACE.Server.Managers.ZoneControl.ZoneModifiers.SlotSpecials();
                        // per-special on/off (owner 2026-08-23): a special turned off at this scope never rolls;
                        // the odds are unchanged, the remaining specials share the hit
                        specials.RemoveAll(d => !zoneLoot.SpecialEnabled(d.Key));
                        if (specials.Count > 0)
                        {
                            specialDef = specials[ACE.Common.ThreadSafeRandom.Next(0, specials.Count - 1)];
                            // the special's home slot: the zone / Default override when authored (`cantrip <scope>
                            // slots <key> helm|...|cloak`, owner 2026-08-22), else the catalog's SpecialSlot
                            var slotId = ACE.Server.Managers.ZoneControl.ZoneModifiers.EffectiveSpecialSlot(specialDef, zoneLoot.ModifierSlots);
                            specialPiece = items.FirstOrDefault(i => ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialPieceMatches(i, slotId));
                            if (specialPiece == null)
                            {
                                // the set rolled no piece for that slot (zone turned it off) - spawn exactly one
                                var one = new LootGenerationFactory.ZoneLootSetCounts();
                                switch (slotId)
                                {
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Helm: one.Helm = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Chest: one.Chest = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Shoulders: one.Shoulder = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Bracers: one.Bracer = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Gauntlets: one.Glove = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Girth: one.Girth = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Tassets: one.UpperLeg = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Greaves: one.LowerLeg = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Boots: one.Boot = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Shield: one.Shield = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Neck: one.Amulet = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Trinket: one.Trinket = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Ring: one.Ring = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Bracelet: one.Bracelet = 1; break;
                                    case ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialSlotId.Cloak: one.Cloak = 1; break;
                                    default: one.Chest = 1; break;
                                }
                                var spawned = LootGenerationFactory.CreateZoneLootSet(effectiveTreasure, one);
                                specialPiece = spawned.FirstOrDefault(i => ACE.Server.Managers.ZoneControl.ZoneModifiers.SpecialPieceMatches(i, slotId));
                                items.AddRange(spawned);
                            }

                            if (specialPiece != null)
                                log.Info($"[ZONELOOT] SLOT SPECIAL: {killer?.Name ?? "(unknown)"} killed {Name} ({WeenieClassId}) -> {specialDef.Name} (key {specialDef.Key}, {slotId}) on {specialPiece.Name}, odds 1 in {denom}");
                            else
                            {
                                log.Warn($"[ZONELOOT] SLOT SPECIAL won by {killer?.Name ?? "(unknown)"} on {Name} ({WeenieClassId}) but no {slotId} piece could be found or spawned");
                                specialDef = null;
                            }
                        }
                    }
                }

                // Corpse display order (owner 2026-07-20): casters, missiles, UA, sword, other
                // melee, then armor/shields/jewelry/cloaks. The client's loot window shows items
                // in REVERSE insertion order, so insert in exact reverse of the desired display
                // (stable sort + Reverse keeps within-group generation order correct on screen).
                // Quest/create-list items are added AFTER treasure (below) so they display first.
                if (effectiveTreasure.Tier >= LootGenerationFactory.ZoneLootSetMinTier)
                    items = items.OrderBy(LootGenerationFactory.GetZoneLootDisplayOrder).Reverse().ToList();

                foreach (WorldObject wo in items)
                {
                    if (tier > 0)
                        PrestigeManager.ApplyLootScaling(wo, tier);

                    // T11+ deterministic per-slot gear budget (fixed base; cantrips carry the
                    // variance). BEFORE MutateLootItem so the cantrip
                    // stamps layer ON TOP of it rather than being clobbered.
                    var isSpecial = specialPiece != null && ReferenceEquals(wo, specialPiece);
                    if (effectiveTreasure.Tier >= LootGenerationFactory.ZoneLootSetMinTier)
                        LootGenerationFactory.ApplyT11GearStats(wo, effectiveTreasure.Tier, forceMax: isSpecial, p: zoneLoot);

                    // Zone Control loot: post-roll per-item mutations (weapon stats, AL, workmanship, coins,
                    // value, and the low-chance special-property rolls)
                    ACE.Server.Managers.ZoneControl.ZoneLootMutator.MutateLootItem(wo, zoneLoot, this, effectiveTreasure.Tier, forceMax: isSpecial);

                    // the slot special itself (Armor v2): rolled in ITS band (zone override wins), stamped
                    // after the lines so it reads last among the "Zone Cantrip:" lines
                    if (isSpecial && specialDef != null)
                    {
                        var (sMin, sMax) = zoneLoot.ModifierBands.TryGetValue(specialDef.Key, out var sBand)
                            ? (sBand.Min, sBand.Max) : ACE.Server.Managers.ZoneControl.ZoneModifiers.CatalogBandAt(specialDef, effectiveTreasure.Tier);
                        if (sMin > sMax) (sMin, sMax) = (sMax, sMin);
                        // specials join the grade model (owner 2026-08-22): graded roll, recorded in ZcModifiers
                        var sGrade = ACE.Server.Managers.ZoneControl.ZoneStatResolver.RollGrade(effectiveTreasure.Tier, false);
                        ACE.Server.Managers.ZoneControl.ZoneModifiers.StampGraded(wo, specialDef, sGrade, (sMin, sMax));
                    }

                    // Tier 11+ presentation sweep. Runs LAST so it also covers values that came
                    // from the base weenie or any mutation above.
                    if (effectiveTreasure.Tier >= LootGenerationFactory.ZoneLootSetMinTier)
                    {
                        // ALL inherited wield reqs removed, replaced by the per-tier item-aug gate
                        LootGenerationFactory.StripWieldRequirements(wo);
                        LootGenerationFactory.ApplyT11WieldRequirement(wo, effectiveTreasure.Tier);

                        // Weapon aug-scaling identity: quality roll + tier (weapons/casters only)
                        LootGenerationFactory.ApplyWeaponAugScaleStamp(wo, effectiveTreasure.Tier);

                        // one uniform resist value across all eight elements. Pass the tier AND the
                        // zone profile: without the profile the armor_prot_equalize switch resolves
                        // from the tier Default only, so a ZONE-level override would be silently
                        // ignored on this path while working everywhere else.
                        LootGenerationFactory.EqualizeT11ArmorResists(wo, effectiveTreasure.Tier, zoneLoot);

                        // description cleanup LAST: drop inherited weenie flavor text, keep our
                        // lines in order, provenance ("Dropped by") to the very bottom
                        LootGenerationFactory.FinalizeT11LongDesc(wo);

                        // Live stat resolution self-check: the record must resolve to exactly what
                        // was stamped (grades are the truth, props the cache). Cheap, once per piece.
                        VerifyLiveStatCache(wo);

                        // NOTE (owner 2026-07-21): the server-composed info block / full panel
                        // takeover was REVERTED -- the client renders its stock examine panel.
                        // A future pass will APPEND extra lines to the bottom (LongDesc renders
                        // last) without touching the default layout.

                        // "T11 - [base name]" (material cleared -- the client would prefix it)
                        LootGenerationFactory.ApplyT11NamePrefix(wo);

                        // name tinted by damage element (trial 2026-07-20, may revert)
                        LootGenerationFactory.ApplyT11ElementTint(wo);
                    }

                    if (corpse != null)
                        corpse.TryAddToInventory(wo);
                    else
                        droppedItems.Add(wo);

                    DoModifierLogging(killer, wo);
                }
            }

            // move wielded treasure over, which also should include Wielded objects not marked for destroy on death.
            // allow server operators to configure this behavior due to errors in createlist post 16py data
            var dropFlags = ServerConfig.creatures_drop_createlist_wield.Value ? DestinationType.WieldTreasure : DestinationType.Treasure;

            // Build list of items to move (optimized from Concat + Where + ToList)
            var itemsToMove = new List<WorldObject>();
            foreach (var item in Inventory.Values)
            {
                if ((item.DestinationType & dropFlags) != 0)
                    itemsToMove.Add(item);
            }
            foreach (var item in EquippedObjects.Values)
            {
                if ((item.DestinationType & dropFlags) != 0)
                    itemsToMove.Add(item);
            }

            // Now safe to modify collections during this iteration
            foreach (var item in itemsToMove)
            {
                if (item.Bonded == BondedStatus.Destroy)
                    continue;

                if (TryDequipObjectWithBroadcasting(item.Guid, out var wo, out var wieldedLocation))
                    EnqueueBroadcast(new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Wielder, ObjectGuid.Invalid));

                if (corpse != null)
                {
                    corpse.TryAddToInventory(item);
                    EnqueueBroadcast(new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, corpse.Guid), new GameMessagePickupEvent(item));
                }
                else
                    droppedItems.Add(item);
            }

            // contain and non-wielded treasure create (create-list: quest drops etc.)
            // Runs LAST: the client's loot window displays in reverse insertion order, so the
            // last-inserted quest/special items show FIRST (owner loot-order decision 2026-07-20).
            //
            // ZONE LOOT FLOOR SUPPRESSES CARRIED INVENTORY, OPT-IN PER MONSTER (owner 2026-09-01).
            // Inside a governed v11+ zone a monster drops THAT VARIATION'S SET and nothing else. The
            // three retail roll groups are already zeroed where the floor is applied above, but that
            // only covers ROLLED treasure - create-list Contain items never pass through
            // CreateRandomLootObjects at all, so they kept landing on the corpse. Found on retail wcid
            // 4124 (Lich Overseer), which carries three Focusing Stones and tipped all three into a T12
            // corpse alongside the zone set.
            //
            // NOT a blanket rule, because quest mobs deliver their quest item through exactly this
            // channel (owner: none do today, but they will). drop_carried_inventory authored above 0 -
            // normally on that ONE monster's per-WCID bucket - lets it hand over what it carries. So the
            // default is "drop the zone set only" and a quest drop is one authored stat away, instead of
            // the quest silently delivering nothing on the day it is built.
            //
            // Scope is deliberately narrow. This block takes Contain, and Treasure-without-Wield; the
            // WIELDED gear above is a separate path with its own creatures_drop_createlist_wield config
            // and is untouched - which matters because every custom T11 monster authors its kit as
            // DestinationType.Wield (its look), and NONE of them use Contain.
            var carriedAllowed = zoneFloorTier <= 0
                || (zoneLoot != null && zoneLoot.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.DropCarriedInventory, 0.0) > 0.0);

            if (Biota.PropertiesCreateList != null && carriedAllowed)
            {
                var createList = Biota.PropertiesCreateList.Where(i => (i.DestinationType & DestinationType.Contain) != 0 ||
                                (i.DestinationType & DestinationType.Treasure) != 0 && (i.DestinationType & DestinationType.Wield) == 0).ToList();

                var selected = CreateListSelect(createList);

                foreach (var item in selected)
                {
                    var wo = WorldObjectFactory.CreateNewWorldObject(item);

                    if (wo != null)
                    {
                        if (tier > 0)
                            PrestigeManager.ApplyLootScaling(wo, tier);

                        if (corpse != null)
                            corpse.TryAddToInventory(wo);
                        else
                            droppedItems.Add(wo);
                    }
                }
            }

            // Zone Scaler: inject the custom bonus-currency token (independent of the loot table).
            InjectZoneBonusCurrency(zoneLoot, killer, corpse, droppedItems);

            return droppedItems;
        }

        /// <summary>
        /// Shallow field-copy of a TreasureDeath profile so per-kill scaling (zone / QB) can mutate a clone
        /// without touching the shared cached row. Keep the field list in sync with TreasureDeath's columns.
        /// </summary>
        /// <summary>Hard ceiling on a budgeted drop set: a Corpse declares ItemCapacity 120
        /// (Corpse.cs:61), and every corpse.TryAddToInventory call here ignores its return value, so
        /// an item that will not fit is silently lost. Kept below the real cap so quest tokens,
        /// currency and slot specials - all added OUTSIDE the budget - still have room.</summary>
        private const int ZoneCorpseItemCap = 100;

        private static ACE.Database.Models.World.TreasureDeath CloneTreasureDeath(ACE.Database.Models.World.TreasureDeath src)
        {
            return new ACE.Database.Models.World.TreasureDeath
            {
                Id = src.Id,
                TreasureType = src.TreasureType,
                Tier = src.Tier,
                LootQualityMod = src.LootQualityMod,
                UnknownChances = src.UnknownChances,
                ItemChance = src.ItemChance,
                ItemMinAmount = src.ItemMinAmount,
                ItemMaxAmount = src.ItemMaxAmount,
                ItemTreasureTypeSelectionChances = src.ItemTreasureTypeSelectionChances,
                MagicItemChance = src.MagicItemChance,
                MagicItemMinAmount = src.MagicItemMinAmount,
                MagicItemMaxAmount = src.MagicItemMaxAmount,
                MagicItemTreasureTypeSelectionChances = src.MagicItemTreasureTypeSelectionChances,
                MundaneItemChance = src.MundaneItemChance,
                MundaneItemMinAmount = src.MundaneItemMinAmount,
                MundaneItemMaxAmount = src.MundaneItemMaxAmount,
                MundaneItemTypeSelectionChances = src.MundaneItemTypeSelectionChances,
                LastModified = src.LastModified,
            };
        }

        /// <summary>
        /// Zone Scaler: injects bonus currency onto the corpse/drop list. Two independent sources, both
        /// loot-table independent: the legacy single-token bonus_currency stat (server-wide token wcid from
        /// zonescale_bonus_currency_wcid) and the zone's per-entry currency drop table (each entry = its own
        /// item wcid + stack amount + per-kill chance).
        /// </summary>
        private void InjectZoneBonusCurrency(ACE.Server.Managers.ZoneScaling.EvaluatedProfile profile, DamageHistoryInfo killer, Corpse corpse, List<WorldObject> dropped)
        {
            if (profile == null)
                return;

            if (profile.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.BonusCurrency))
            {
                var amount = (int)Math.Round(profile.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.BonusCurrency));
                var wcid = (uint)ServerConfig.zonescale_bonus_currency_wcid.Value;
                if (amount > 0 && wcid != 0)
                    SpawnZoneCurrency(wcid, amount, corpse, dropped);
            }

            if (profile.CurrencyDrops != null)
            {
                foreach (var drop in profile.CurrencyDrops)
                {
                    if (drop == null || drop.Wcid == 0 || drop.Amount <= 0)
                        continue;
                    if (drop.Chance < 1.0 && ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f) >= drop.Chance)
                        continue;

                    if (drop.Direct && TryGiveZoneCurrencyToKiller(killer, drop.Wcid, drop.Amount))
                        continue;

                    SpawnZoneCurrency(drop.Wcid, drop.Amount, corpse, dropped);
                }
            }
        }

        private static WorldObject CreateZoneCurrencyToken(uint wcid, int amount)
        {
            var token = WorldObjectFactory.CreateNewWorldObject(wcid);
            if (token == null)
                return null;

            if (token.MaxStackSize.HasValue && amount > 1)
                token.SetStackSize(Math.Min(amount, token.MaxStackSize.Value));

            return token;
        }

        private static void SpawnZoneCurrency(uint wcid, int amount, Corpse corpse, List<WorldObject> dropped)
        {
            // an authored amount above the token's MaxStackSize spawns as several full stacks - never truncated silently
            var remaining = Math.Max(amount, 0);
            var guard = 0;
            while (remaining > 0 && guard++ < 64)
            {
                var token = CreateZoneCurrencyToken(wcid, remaining);
                if (token == null)
                    return;
                var stack = Math.Max(token.StackSize ?? 1, 1);
                if (corpse != null)
                    corpse.TryAddToInventory(token);
                else
                    dropped.Add(token);
                remaining -= stack;
            }
            if (remaining > 0)
                log.Warn($"[ZoneLoot] currency {wcid}: {remaining} of {amount} could not be spawned within 64 stacks - the authored amount is too large.");
        }

        /// <summary>Direct-delivery currency drop: straight into the killing player's inventory with a chat
        /// message. Returns false (caller falls back to the corpse) when the killer isn't a player, the
        /// token can't be created, or their inventory is full.</summary>
        private static bool TryGiveZoneCurrencyToKiller(DamageHistoryInfo killer, uint wcid, int amount)
        {
            var player = killer?.TryGetPetOwnerOrAttacker() as Player;
            if (player == null || player.Session == null)
                return false;

            var token = CreateZoneCurrencyToken(wcid, amount);
            if (token == null)
                return false;

            if (!player.TryCreateInInventoryWithNetworking(token))
            {
                token.Destroy();
                return false;
            }

            var qty = token.StackSize ?? 1;
            var name = qty > 1 ? token.GetPluralName() : token.Name;
            player.Session.Network.EnqueueSend(
                new GameMessageSystemChat($"You receive {qty:N0} {name} from the kill!", ChatMessageType.Broadcast));
            return true;
        }

        /// <summary>
        /// Generates random amounts of slag on a corpse
        /// when an OlthoiPlayer is the killer
        /// </summary>
        private void GenerateTreasure_Olthoi(DamageHistoryInfo killer, Corpse corpse)
        {
            if (DeathTreasure == null) return;

            var slag = LootGenerationFactory.RollSlag(DeathTreasure);

            if (slag == null) return;

            corpse.TryAddToInventory(slag);
        }

        /// <summary>
        /// Debug assertion for live stat resolution (2026-08-22): ZoneStatResolver.Compute(wo) must equal the
        /// props the producers stamped. Any difference means a producer bypassed the record, a zone override
        /// (core anchor) diverged from the tier Default layer the resolver
        /// reads, or a later mutation touched a ZC-owned prop. Warn only - never alters the drop.
        /// </summary>
        private static void VerifyLiveStatCache(WorldObject wo)
        {
            try
            {
                var r = ACE.Server.Managers.ZoneControl.ZoneStatResolver.Compute(wo);
                if (r == null)
                    return;
                var diffs = new List<string>();
                foreach (var kv in r.Ints)
                {
                    var cur = wo.GetProperty(kv.Key);
                    if (cur != kv.Value)
                        diffs.Add($"{kv.Key} item={(cur.HasValue ? cur.Value.ToString() : "null")} resolved={kv.Value}");
                }
                // WEAPON half (2026-08-25): every continuous weapon card is a PropertyFloat, so the
                // same assertion has to walk r.Floats or the whole weapon lane would go unchecked.
                //
                // EPSILON, unlike the ints above: these are doubles produced by band interpolation, and
                // the drop path and the resolve path reach the number by slightly different routes (the
                // drop path clamps a freshly interpolated value; the resolve path re-interpolates from
                // the stored grade). Bit-exact equality would make this warn on last-place noise and
                // train everyone to ignore it. 1e-9 is far below the second decimal that any of these
                // cards is designed in, so a REAL divergence - a producer bypassing the record, a
                // Crushing Blow "- 1.0" applied twice, a zone band diverging from the tier Default the
                // resolver reads - is still caught loudly.
                foreach (var kv in r.Floats)
                {
                    var cur = wo.GetProperty(kv.Key);
                    if (!cur.HasValue || Math.Abs(cur.Value - kv.Value) > 1e-9)
                        diffs.Add($"{kv.Key} item={(cur.HasValue ? cur.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")} resolved={kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }
                if (r.ArmorLevel.HasValue && wo.ArmorLevel != r.ArmorLevel.Value)
                    diffs.Add($"ArmorLevel item={(wo.ArmorLevel.HasValue ? wo.ArmorLevel.Value.ToString() : "null")} resolved={r.ArmorLevel.Value}");
                if (diffs.Count > 0)
                    log.Warn($"[ZONELOOT] LIVESTAT MISMATCH on {wo.Name} ({wo.WeenieClassId}, tier {r.Tier}, record \"{wo.GetProperty(PropertyString.ZcModifiers)}\"): {string.Join(", ", diffs)}");
            }
            catch (Exception ex)
            {
                log.Warn($"[ZONELOOT] LIVESTAT self-check threw on {wo?.Name}: {ex.Message}");
            }
        }

        public void DoModifierLogging(DamageHistoryInfo killer, WorldObject wo)
        {
            var epicCantrips = wo.EpicCantrips;
            var legendaryCantrips = wo.LegendaryCantrips;

            if (epicCantrips.Count > 0 && ServerConfig.log_loot_cantrip_debug.Value)
                log.Debug($"[LOOT][EPIC] {Name} ({Guid}) generated item with {epicCantrips.Count} epic{(epicCantrips.Count > 1 ? "s" : "")} - {wo.Name} ({wo.Guid}) - {GetSpellList(epicCantrips)} - killed by {killer?.Name} ({killer?.Guid})");

            if (legendaryCantrips.Count > 0 && ServerConfig.log_loot_cantrip_debug.Value)
                log.Debug($"[LOOT][LEGENDARY] {Name} ({Guid}) generated item with {legendaryCantrips.Count} legendar{(legendaryCantrips.Count > 1 ? "ies" : "y")} - {wo.Name} ({wo.Guid}) - {GetSpellList(legendaryCantrips)} - killed by {killer?.Name} ({killer?.Guid})");
        }

        public static string GetSpellList(Dictionary<int, float> spellTable)
        {
            var spells = new List<Server.Entity.Spell>();

            foreach (var kvp in spellTable)
                spells.Add(new Server.Entity.Spell(kvp.Key, false));

            return string.Join(", ", spells.Select(i => i.Name));
        }

        /// <summary>
        /// Generates a Siphon Lens and attempts to give it to the killer, or drops it to the ground.
        /// This is separate from normal treasure generation to ensure it happens even if the creature
        /// is destroyed by a death emote (DeleteSelf) before a corpse is created.
        /// </summary>
        private void GenerateSiphonLens(DamageHistoryInfo killer)
        {
            if (killer == null) return;

            var lens = LootGenerationFactory.TryRollSiphonLensForCreature((uint)(Level ?? 1));
            if (lens == null) return;

            bool lensDelivered = false;
            var killerPlayer = killer?.TryGetPetOwnerOrAttacker() as Player;
            
            if (killerPlayer != null)
            {
                if (killerPlayer.TryCreateInInventoryWithNetworking(lens))
                {
                    killerPlayer.Session.Network.EnqueueSend(
                        new GameMessageSystemChat($"You find a {lens.Name} on the creature!", ChatMessageType.Broadcast));
                    lensDelivered = true;
                }
                else
                {
                    killerPlayer.Session.Network.EnqueueSend(
                        new GameMessageSystemChat($"You found a {lens.Name}, but your inventory is full! It fell to the ground.", ChatMessageType.Broadcast));
                }
            }
            
            // Fallback: killer is null, not a player, or inventory full - drop to ground
            if (!lensDelivered)
            {
                lens.Location = new Position(Location);
                LandblockManager.AddObject(lens);
            }
        }

        public bool IsOnNoDeathXPLandblock => Location != null ? NoDeathXP_Landblocks.Contains(Location.LandblockId.Landblock) : false;

        /// <summary>
        /// A list of landblocks the player gains no xp from creature kills
        /// </summary>
        private static HashSet<ushort> NoDeathXP_Landblocks = new HashSet<ushort>()
        {
            0x00B0,     // Colosseum Arena One
            0x00B1,     // Colosseum Arena Two
            0x00B2,     // Colosseum Arena Three
            0x00B3,     // Colosseum Arena Four
            0x00B4,     // Colosseum Arena Five
            0x5960,     // Gauntlet Arena One (Celestial Hand)
            0x5961,     // Gauntlet Arena Two (Celestial Hand)
            0x5962,     // Gauntlet Arena One (Eldritch Web)
            0x5963,     // Gauntlet Arena Two (Eldritch Web)
            0x5964,     // Gauntlet Arena One (Radiant Blood)
            0x5965,     // Gauntlet Arena Two (Radiant Blood)
            0x596B,     // Gauntlet Staging Area (All Societies)
        };

        public bool IsInMarketplace => Location != null ? Marketplace_Landblocks.Contains(Location.LandblockId.Landblock) : false;

        /// <summary>
        /// landblock required for using /clap command
        /// </summary>
        private static HashSet<ushort> Marketplace_Landblocks = new HashSet<ushort>()
        {
            0x016C,     // Marketplace
        };
    }
}
