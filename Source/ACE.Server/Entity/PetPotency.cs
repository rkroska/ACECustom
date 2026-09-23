using System;
using System.Collections.Concurrent;

using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Essence Residue currency and pet potency gameplay (see docs/PET_POTENCY_AND_STRAIN.md).
    /// </summary>
    public static class PetPotency
    {
        private struct CachedStrain
        {
            public int Rating;
            public double ExpiryTime;
        }

        private static readonly ConcurrentDictionary<uint, CachedStrain> _strainCache = new();

        public const uint EssenceResidueWcid = 78780013;
        public const uint EssenceResonatorWcid = 78780014;
        /// <summary>Player-facing stack name (weenie string type 1). Code alias: Essence Residue.</summary>
        public const string CurrencyDisplayName = "Savage Echo";
        public const uint SiphonedEssenceWcid = 78780004;
        public const uint HollowEssenceWcid = 78780006;

        public static bool IsPotencyUseOnTargetTool(uint wcid)
        {
            return wcid == EssenceResidueWcid || wcid == EssenceResonatorWcid;
        }

        public static bool IsSalvageableCapturedEssence(WorldObject target)
        {
            if (target == null)
                return false;

            // A bred pet is the owner's own essence rather than a siphoned skin, so it never carries
            // IsCapturedAppearance; it is recognised by having been born juvenile instead.
            if (IsSalvageableBredEssence(target))
                return true;

            if (!MonsterCapture.IsCapturedAppearance(target))
                return false;

            return target.WeenieClassId == SiphonedEssenceWcid || target.WeenieClassId == HollowEssenceWcid;
        }

        /// <summary>
        /// A bred pet essence its owner is finished with. The pet's quality is deliberately NOT considered:
        /// the yield is the flat <c>pet_bred_essence_salvage_yield</c>, because breeding COPIES potency and
        /// mutation counts into the baby rather than moving them out of the parents. A yield that scaled with
        /// either would let a breeder mint Savage Echo from nothing, and even a flat one is kept a token since
        /// each female breeds again every few hours. Behind its own switch so the bin can be closed without a
        /// build.
        /// </summary>
        public static bool IsSalvageableBredEssence(WorldObject target)
        {
            return ServerConfig.pet_bred_essence_salvage_enabled.Value
                && target is PetDevice device
                && device.IsCombatPetDevice()
                && device.WasBredJuvenile;
        }

        /// <summary>
        /// Salvaging is allowed whatever the pet is worth - this is a bin, not a trade-in - but it cannot be
        /// undone, and "salvage" and "summon" are both a use on the same essence. A bred pet carrying
        /// mutations or bought potency asks first; a plain one goes straight through.
        /// </summary>
        private static bool RequiresSalvageConfirmation(WorldObject essence, out string message)
        {
            message = null;

            if (essence is not PetDevice device || !device.WasBredJuvenile)
                return false;

            // The per-line counts are what summon reads; the PetMutationCount total is only written when
            // non-zero, so it is not trusted here.
            var mutations = (device.GetProperty(PropertyInt.PetMutDamageCount) ?? 0)
                          + (device.GetProperty(PropertyInt.PetMutDamageResistCount) ?? 0)
                          + (device.GetProperty(PropertyInt.PetMutCritCount) ?? 0)
                          + (device.GetProperty(PropertyInt.PetMutVitalityCount) ?? 0)
                          + (device.GetProperty(PropertyInt.PetMutPotencyCount) ?? 0);
            var potency = device.PetPotencyStored ?? 0;
            if (mutations <= 0 && potency <= 0)
                return false;

            var mutationText = $"{mutations} mutation{(mutations == 1 ? "" : "s")}";
            var detail = mutations > 0 && potency > 0 ? $"{mutationText} and {potency} potency"
                       : mutations > 0 ? mutationText
                       : $"{potency} potency";

            // Client text: 7-bit ASCII, explicit \n line breaks (CLAUDE.md).
            message = $"Salvage {device.Name}?\n\nIt has {detail}.\nThis cannot be undone.";
            return true;
        }

        public static int GetActiveCapFromConfig()
        {
            var cap = (int)ServerConfig.pet_potency_active_cap.Value;
            return cap > 0 ? cap : int.MaxValue;
        }

        public static int GetActivePotency(PetDevice device)
        {
            if (device == null || !ServerConfig.pet_potency_enabled.Value)
                return 0;

            return PetPotencyMath.GetActivePotency(
                device.PetPotencyStored ?? 0,
                device.PetBondLevel ?? 0,
                potencyEnabled: true,
                bondDivisor: (int)ServerConfig.pet_potency_bond_divisor.Value,
                minActiveWhenStored: (int)ServerConfig.pet_potency_bond_offense_min_active.Value,
                activeCap: GetActiveCapFromConfig());
        }

        public static int GetDormantPotency(PetDevice device)
        {
            var stored = device?.PetPotencyStored ?? 0;
            return PetPotencyMath.GetDormantPotency(stored, GetActivePotency(device));
        }

        public static long GetUpgradeCost(int currentStored)
        {
            return PetPotencyMath.GetUpgradeCost(
                currentStored,
                ServerConfig.pet_potency_cost_base.Value,
                ServerConfig.pet_potency_cost_exponent.Value);
        }

        public static float GetBodyPartDamageMult(int activePotency)
        {
            return PetPotencyMath.GetBodyPartDamageMult(
                activePotency,
                ServerConfig.pet_potency_damage_per_level.Value * 100.0);
        }

        public static int GetBondStrainRating(Player player)
        {
            if (player == null || !ServerConfig.pet_potency_enabled.Value || !ServerConfig.pet_strain_enabled.Value)
                return 0;

            if (player.IsDead && !ServerConfig.pet_strain_while_player_dead.Value)
                return 0;

            if (ServerConfig.pet_strain_combat_pet_only.Value && player.CurrentActivePet is not CombatPet)
                return 0;

            var guid = player.Guid.Full;
            var now = ACE.Common.Time.GetUnixTime();

            if (_strainCache.TryGetValue(guid, out var cached) && now < cached.ExpiryTime)
                return cached.Rating;

            var rating = CalculateBondStrainRating(player);

            var newCached = new CachedStrain
            {
                Rating = rating,
                ExpiryTime = now + 1.0 // 1 second TTL
            };
            _strainCache[guid] = newCached;

            return rating;
        }

        private static int CalculateBondStrainRating(Player player)
        {
            var device = TryGetStrainPetDevice(player);
            if (device == null)
                return 0;

            return PetPotencyMath.GetBondStrainRating(
                GetActivePotency(device),
                strainEnabled: true,
                strainThreshold: (int)ServerConfig.pet_strain_potency_threshold.Value,
                strainPerPotencyLevel: ServerConfig.pet_strain_per_potency_level.Value,
                strainMaxRating: (int)ServerConfig.pet_strain_max_rating.Value);
        }

        private static PetDevice TryGetStrainPetDevice(Player player)
        {
            if (player.CurrentActivePet is not CombatPet combatPet)
                return null;

            var device = combatPet.TryGetSummoningDevice();
            if (device != null)
                return device;

            var devGuid = combatPet.SummoningDeviceGuid;
            if (devGuid == ObjectGuid.Invalid)
                return null;

            return player.FindObject(devGuid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) as PetDevice;
        }

        /// <summary>
        /// Use Essence Residue stack on attuned combat essence: consume residue, +1 stored potency.
        /// Cost is computed server-side (not NPC pricing): cost_base × (stored+1)^exponent.
        /// </summary>
        public static bool TrySpendResidueOnEssence(Player player, Stackable residue, PetDevice essence)
        {
            if (player == null || residue == null || essence == null)
                return false;

            if (!ServerConfig.pet_potency_enabled.Value)
            {
                player.SendTransientError("Potency training is not enabled.");
                return false;
            }

            if (residue.WeenieClassId != EssenceResidueWcid)
                return false;

            if (!essence.IsCombatPetDevice())
            {
                player.SendTransientError("You can only apply Essence Residue to a combat pet essence.");
                return false;
            }

            if (!essence.IsPetBondAttuned)
            {
                player.SendTransientError("This essence must be bond-attuned before you can train potency.");
                return false;
            }

            var bondedCharacterId = essence.PetBondAttunedCharacterId;
            if (bondedCharacterId.HasValue && bondedCharacterId.Value != (long)player.Character.Id)
            {
                player.SendTransientError("You can only train potency on essences attuned to you.");
                return false;
            }

            var maxStored = (int)ServerConfig.pet_potency_max_stored.Value;
            var stored = essence.PetPotencyStored ?? 0;
            if (maxStored > 0 && stored >= maxStored)
            {
                player.SendTransientError("This essence has reached the maximum stored potency.");
                return false;
            }

            var cost = GetUpgradeCost(stored);
            var available = player.GetNumInventoryItemsOfWCID(EssenceResidueWcid);

            if (cost <= 0)
            {
                player.SendTransientError("Potency upgrade cost is invalid.");
                return false;
            }

            if (cost > int.MaxValue)
            {
                player.SendTransientError("Potency upgrade cost is too large to process.");
                return false;
            }

            if (available < cost)
            {
                player.SendTransientError($"You need {cost:N0} {CurrencyDisplayName} to increase potency (you have {available:N0}).");
                return false;
            }

            try
            {
                var previousActive = GetActivePotency(essence);

                if (!player.TryConsumeFromInventoryWithNetworking(EssenceResidueWcid, (int)cost))
                {
                    player.SendTransientError("Could not consume Savage Echo.");
                    return false;
                }

                essence.PetPotencyStored = stored + 1;
                essence.SaveBiotaToDatabase();
                essence.SyncPetProgressPropertiesToOwner(player, broadcast: true);

                var newActive = GetActivePotency(essence);
                var dormant = GetDormantPotency(essence);
                var pct = (int)Math.Round((GetBodyPartDamageMult(newActive) - 1.0f) * 100.0);

                player.SendMessage($"Potency increased on {essence.GetBondMessageDisplayName()}: {essence.PetPotencyStored:N0} stored ({newActive:N0} active, {dormant:N0} dormant). Body training: +{pct}% damage from potency.");

                if (ServerConfig.pet_potency_debug_chat.Value)
                    player.SendMessage($"[Potency] Spent {cost:N0} residue. Active {previousActive} -> {newActive}.");

                if (ServerConfig.pet_potency_debug_log.Value)
                    log.Info($"[Potency] {player.Name} spent {cost} residue on {essence.Name} -> stored {essence.PetPotencyStored}, active {newActive}.");

                return true;
            }
            catch (Exception ex)
            {
                log.Error("[Potency] TrySpendResidueOnEssence failed after consume", ex);
                player.SendTransientError("Potency training failed (server error).");
                return false;
            }
        }

        /// <summary>
        /// Use Essence Resonator on a spare Siphoned/Hollow captured essence, or on a bred pet its owner is
        /// finished with: destroy it, award Savage Echo. A bred pet with mutations or potency confirms first.
        /// </summary>
        public static bool TrySalvageCapturedEssence(Player player, WorldObject tool, WorldObject essence, bool confirmed = false)
        {
            if (player == null || tool == null || essence == null)
                return false;

            if (!ServerConfig.pet_potency_enabled.Value)
            {
                player.SendTransientError("Savage Echo salvage is not enabled.");
                return false;
            }

            if (!ServerConfig.pet_residue_salvage_enabled.Value)
            {
                player.SendTransientError("Essence salvage is not enabled.");
                return false;
            }

            if (tool.WeenieClassId != EssenceResonatorWcid)
                return false;

            if (!IsSalvageableCapturedEssence(essence))
            {
                var bredButClosed = !ServerConfig.pet_bred_essence_salvage_enabled.Value
                    && essence is PetDevice closedDevice && closedDevice.WasBredJuvenile;

                player.SendTransientError(bredButClosed
                    ? "Bred pet salvage is not enabled."
                    : "You can only salvage spare captured essences, or a bred pet.");
                return false;
            }

            if (player.IsTrading && essence.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                player.SendWeenieError(WeenieError.YouCannotSalvageItemsInTrading);
                return false;
            }

            // Re-checked on every pass, so a confirmed salvage cannot pay out for an essence that left the
            // pack while the prompt was open (residue is awarded before the essence is consumed).
            if (player.FindObject(essence.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) == null)
            {
                player.SendTransientError("You no longer have that essence.");
                return false;
            }

            if (player.FindObject(tool.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) == null)
            {
                player.SendTransientError("You no longer have the Essence Resonator.");
                return false;
            }

            // A bred pet can be out, unlike a captured skin. Destroying the essence under it would leave the
            // pet fighting with its potency applied and nothing to credit kills or residue to. Same rule as
            // tailoring (PetTailoring.IsSummoned).
            if (player.CurrentActivePet is CombatPet activePet && !activePet.IsDestroyed && activePet.SummoningDeviceGuid == essence.Guid)
            {
                player.SendTransientError($"Dismiss {essence.Name}'s pet before salvaging it.");
                return false;
            }

            if (!confirmed && RequiresSalvageConfirmation(essence, out var confirmMessage))
            {
                void onResponse(bool response, bool _)
                {
                    if (response)
                        TrySalvageCapturedEssence(player, tool, essence, true);
                }

                if (!player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, onResponse), confirmMessage))
                    player.SendWeenieError(WeenieError.ConfirmationInProgress);

                return true;
            }

            // Snapshot all essence properties before any destructive operation.
            var isHollow = essence.WeenieClassId == HollowEssenceWcid;
            var isShiny = essence.GetProperty(PropertyInt.CapturedCreatureVariant) == (int)CreatureVariant.Shiny;
            var creatureName = essence.GetProperty(PropertyString.CapturedCreatureName) ?? essence.Name;
            var creatureOverride = GetCapturedCreatureSalvageOverride(essence);

            // A bred baby inherits CapturedCreatureWCID and CapturedCreatureVariant, so the capture formula
            // would hand it the creature override and the shiny multiplier. Breeding COPIES the parents'
            // traits rather than moving them, so either would mint Savage Echo from nothing: the bred yield
            // is its own flat setting in code, not flat by luck of the current settings and data.
            var isBred = IsSalvageableBredEssence(essence);

            // TODO: pass isHollow to GetSalvageExpectedAmount once hollow_mult tuning is finalised.
            // Hollow and siphoned currently yield the same amount by design (pet_residue_hollow_mult reserved).
            var expectedAmount = isBred
                ? Math.Max(0, (double)ServerConfig.pet_bred_essence_salvage_yield.Value)
                : PetPotencyMath.GetSalvageExpectedAmount(
                    isShiny,
                    ServerConfig.pet_residue_salvage_base.Value,
                    ServerConfig.pet_residue_salvage_shiny_mult.Value,
                    creatureOverride);

            // A bred pet is still taken at a yield of 0 - the point of the bin is getting rid of it.
            var amount = PetPotencyMath.RoundResidueDropAmount(expectedAmount);
            if (amount <= 0 && !isBred)
            {
                player.SendTransientError("This essence has too little resonance to salvage.");
                return false;
            }

            // Award residue BEFORE consuming the essence so that a full-inventory failure
            // leaves the essence intact rather than silently destroying it.
            var awarded = 0;
            if (amount > 0 && (!TryAwardResidueToPlayer(player, amount, out awarded) || awarded <= 0))
            {
                player.SendTransientError($"You do not have enough pack space for the {CurrencyDisplayName}.");
                return false;
            }

            if (!player.TryConsumeFromInventoryWithNetworking(essence))
            {
                // Extremely unlikely — items were already awarded. Log it but don't take back the echoes.
                log.Error($"[Potency] Salvage consume failed for {player.Name} on {creatureName} after awarding {awarded} Savage Echo. Items kept by player.");
                player.SendTransientError(awarded > 0
                    ? "Salvage failed (server error); your Savage Echo was kept."
                    : "Salvage failed (server error).");
                return false;
            }

            var hollowLabel = isHollow ? " (hollow)" : string.Empty;
            player.SendMessage(awarded > 0
                ? $"You salvage {creatureName}{hollowLabel} into {awarded:N0} {CurrencyDisplayName}."
                : $"You salvage {creatureName}{hollowLabel}. It leaves no {CurrencyDisplayName} behind.");

            if (ServerConfig.pet_potency_debug_chat.Value)
                player.SendMessage($"[Potency] Salvage expected {expectedAmount:F2}, awarded {awarded:N0} (bred={isBred}, hollow={isHollow}, shiny={isShiny}, override={creatureOverride}).");

            if (ServerConfig.pet_potency_debug_log.Value)
                log.Info($"[Potency] {player.Name} salvaged {creatureName} -> {awarded} Savage Echo (expected {expectedAmount:F2}, override={creatureOverride}).");

            return true;
        }

        /// <summary>
        /// Returns the per-creature salvage yield override (PropertyInt.EssenceSalvageYield) from
        /// the source creature's weenie. Returns 0 if absent, meaning use the formula default.
        /// Admins set this directly on a creature weenie in the DB to fix its salvage yield.
        /// </summary>
        private static int GetCapturedCreatureSalvageOverride(WorldObject essence)
        {
            var creatureWcid = essence.GetProperty(PropertyInt.CapturedCreatureWCID);
            if (!creatureWcid.HasValue)
                return 0;

            var weenie = DatabaseManager.World.GetCachedWeenie((uint)creatureWcid.Value);
            if (weenie == null)
            {
                log.Warn($"[Potency] GetCapturedCreatureSalvageOverride: weenie {creatureWcid.Value} not found in DB for essence '{essence.Name}' ({essence.WeenieClassId}). Using formula default.");
                return 0;
            }

            return weenie.GetProperty(PropertyInt.EssenceSalvageYield) ?? 0;
        }

        public static void ApplyBodyPartPotencyScaling(CombatPet pet, PetDevice device)
        {
            if (pet == null || device == null || !ServerConfig.pet_potency_enabled.Value)
                return;

            if (pet.PotencyApplied)
                return;

            var active = GetActivePotency(device);
            if (active <= 0)
                return;

            var mult = GetBodyPartDamageMult(active);
            if (mult <= 1.0f)
                return;

            var scaleDvar = ServerConfig.pet_potency_scale_dvar.Value;
            if (pet.Biota.PropertiesBodyPart == null || pet.Biota.PropertiesBodyPart.Count == 0)
                return;

            foreach (var kvp in pet.Biota.PropertiesBodyPart)
            {
                var part = kvp.Value;
                if (part.DVal > 0)
                    part.DVal = (int)Math.Round(part.DVal * mult, MidpointRounding.AwayFromZero);
                if (scaleDvar && part.DVar > 0)
                    part.DVar *= mult;
            }

            pet.PotencyApplied = true;
        }

        public static void TryAwardResidueOnKill(Creature creature)
        {
            if (creature == null || !ServerConfig.pet_potency_enabled.Value || !ServerConfig.pet_residue_drops_enabled.Value)
                return;

            var totalHealth = creature.DamageHistory.TotalHealth;
            if (totalHealth <= 0)
                return;

            var tierAmount = GetResidueDropAmountForCreature(creature);
            if (tierAmount <= 0)
                return;

            var residueByOwner = new System.Collections.Generic.Dictionary<uint, (Player Owner, int Amount, double ExpectedAmount)>();

            foreach (var kvp in creature.DamageHistory.TotalDamage)
            {
                var info = kvp.Value;
                if (info.TotalDamage <= 0)
                    continue;

                if (info.TryGetAttacker() is not CombatPet combatPet || info.PetOwner == null)
                    continue;

                var owner = info.TryGetPetOwner();
                if (owner == null)
                    continue;

                var petShare = (float)(info.TotalDamage / totalHealth);
                petShare = Math.Clamp(petShare, 0f, 1f);

                var minShare = (float)ServerConfig.pet_residue_drop_min_pet_share.Value;
                if (petShare < minShare)
                    continue;

                var maxShare = (float)ServerConfig.pet_residue_drop_max_pet_share.Value;
                if (maxShare > 0 && petShare > maxShare)
                    petShare = maxShare;

                var dropChance = petShare * ServerConfig.pet_residue_drop_chance_mult.Value;
                if (dropChance <= 0 || Random.Shared.NextDouble() >= dropChance)
                    continue;

                PetDevice device = combatPet.TryGetSummoningDevice();
                if (device == null && combatPet.SummoningDeviceGuid != ObjectGuid.Invalid)
                    device = owner.FindObject(combatPet.SummoningDeviceGuid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) as PetDevice;

                if (device == null)
                    continue;

                if (ServerConfig.pet_residue_require_bond_attuned.Value && (!device.IsCombatPetDevice() || !device.IsPetBondAttuned))
                    continue;

                var expectedAmount = tierAmount;
                if (creature.CreatureVariant == CreatureVariant.Shiny)
                    expectedAmount *= ServerConfig.pet_residue_shiny_mult.Value;

                var globalMult = ServerConfig.pet_residue_global_mult.Value;
                if (globalMult > 0)
                    expectedAmount *= globalMult;

                var amount = PetPotencyMath.RoundResidueDropAmount(expectedAmount);
                if (amount <= 0)
                    continue;

                var key = owner.Guid.Full;
                if (residueByOwner.TryGetValue(key, out var acc))
                    residueByOwner[key] = (owner, acc.Amount + amount, acc.ExpectedAmount + expectedAmount);
                else
                    residueByOwner[key] = (owner, amount, expectedAmount);
            }

            foreach (var kv in residueByOwner)
            {
                var (owner, amount, expectedAmount) = kv.Value;
                var creatureName = creature.Name;

                // Thread audit: the award creates items in the owner's inventory and messages them from the dying creature's
                // thread - the owner may be ticked by another group (portaled or logged elsewhere), so RunOnThreadFor hands it to
                // the world queue unless this thread owns the owner.
                ACE.Server.Managers.LandblockManager.RunOnThreadFor(owner, ACE.Server.Entity.Actions.ActionType.PetPotency_ResidueAward, () =>
                {
                    if (TryAwardResidueToPlayer(owner, amount, out var awarded) && awarded > 0)
                    {
                        // Per-character opt-in: off by default to avoid spam during long hunts.
                        // Players toggle via @echo-notify command.
                        if (owner.GetProperty(PropertyBool.ShowPetEchoDrops) ?? false)
                            owner.SendMessage($"Your pet earns you {awarded:N0} {CurrencyDisplayName}.");

                        if (ServerConfig.pet_potency_debug_chat.Value)
                            owner.SendMessage($"[Potency] You receive {awarded:N0} {CurrencyDisplayName} (expected {expectedAmount:F2}).");

                        if (ServerConfig.pet_potency_debug_log.Value)
                            log.Info($"[Potency] {owner.Name} awarded {awarded} Savage Echo (expected {expectedAmount:F2}) from {creatureName} kill.");
                    }
                });
            }
        }

        private static double GetResidueDropAmountForCreature(Creature creature)
        {
            var tier = creature.DeathTreasure?.Tier ?? 0;
            if (tier >= 10)
                return ServerConfig.pet_residue_drop_t10.Value;
            if (tier >= 9)
                return ServerConfig.pet_residue_drop_t9.Value;
            return ServerConfig.pet_residue_drop_default.Value;
        }

        public static bool TryAwardResidueToPlayer(Player player, int amount, out int awarded)
        {
            awarded = 0;
            if (player == null || amount <= 0)
                return false;

            var remaining = amount;
            while (remaining > 0)
            {
                // Chunk at 10,000 to match the weenie MaxStackSize, minimising the number of
                // inventory slots consumed and reducing the chance of a partial-award on a
                // nearly-full inventory.
                var stackSize = Math.Min(remaining, 10000);
                var item = WorldObjectFactory.CreateNewWorldObject(EssenceResidueWcid);
                if (item == null)
                    return awarded > 0;

                item.SetStackSize(stackSize);
                if (!player.TryCreateInInventoryWithNetworking(item))
                {
                    item.Destroy();
                    return awarded > 0;
                }

                awarded += stackSize;
                remaining -= stackSize;
            }

            return awarded > 0;
        }

        public static string BuildPotencyAppraisalBlock(PetDevice device)
        {
            if (device == null || !device.IsCombatPetDevice())
                return null;

            if (!ServerConfig.pet_potency_enabled.Value)
                return null;

            var stored = device.PetPotencyStored ?? 0;
            if (stored <= 0)
                return "Potency: 0 (use Savage Echo on this essence to raise its damage).";

            var active = GetActivePotency(device);
            var dormant = GetDormantPotency(device);
            var pct = (int)Math.Round((GetBodyPartDamageMult(active) - 1.0f) * 100.0);

            var msg = $"Potency: {stored:N0} stored ({active:N0} active, {dormant:N0} dormant)\nPotency Bonus: +{pct}% damage (from active potency)";

            if (ServerConfig.pet_strain_enabled.Value && active > ServerConfig.pet_strain_potency_threshold.Value)
            {
                var strain = PetPotencyMath.GetBondStrainRating(
                    active,
                    strainEnabled: true,
                    strainThreshold: (int)ServerConfig.pet_strain_potency_threshold.Value,
                    strainPerPotencyLevel: ServerConfig.pet_strain_per_potency_level.Value,
                    strainMaxRating: (int)ServerConfig.pet_strain_max_rating.Value);
                if (strain > 0)
                    msg += $"\nBond Strain: -{strain:N0} damage rating while combat pet summoned";
            }

            return msg;
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
