using System;

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
            if (target == null || !MonsterCapture.IsCapturedAppearance(target))
                return false;

            return target.WeenieClassId == SiphonedEssenceWcid || target.WeenieClassId == HollowEssenceWcid;
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

            if (available < cost)
            {
                player.SendTransientError($"You need {cost:N0} {CurrencyDisplayName} to increase potency (you have {available:N0}).");
                return false;
            }

            if (!player.TryConsumeFromInventoryWithNetworking(EssenceResidueWcid, (int)cost))
            {
                player.SendTransientError("Could not consume Savage Echo.");
                return false;
            }

            try
            {
                var previousActive = GetActivePotency(essence);
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
        /// Use Essence Resonator on spare Siphoned/Hollow captured essence: destroy gem, award Savage Echo.
        /// </summary>
        public static bool TrySalvageCapturedEssence(Player player, WorldObject tool, WorldObject essence)
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
                player.SendTransientError("You can only salvage spare Siphoned or Hollow captured essences.");
                return false;
            }

            if (player.IsTrading && essence.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                player.SendWeenieError(WeenieError.YouCannotSalvageItemsInTrading);
                return false;
            }

            var creatureLevel = GetCapturedCreatureLevel(essence);
            var isHollow = essence.WeenieClassId == HollowEssenceWcid;
            var isShiny = essence.GetProperty(PropertyInt.CapturedCreatureVariant) == (int)CreatureVariant.Shiny;

            var expectedAmount = PetPotencyMath.GetSalvageExpectedAmount(
                creatureLevel,
                isHollow,
                isShiny,
                ServerConfig.pet_residue_salvage_base.Value,
                ServerConfig.pet_residue_salvage_per_creature_level.Value,
                ServerConfig.pet_residue_salvage_mult.Value,
                ServerConfig.pet_residue_hollow_mult.Value,
                ServerConfig.pet_residue_salvage_shiny_mult.Value);

            var amount = PetPotencyMath.RoundResidueDropAmount(expectedAmount);
            if (amount <= 0)
            {
                player.SendTransientError("This essence has too little resonance to salvage.");
                return false;
            }

            if (!player.TryConsumeFromInventoryWithNetworking(essence))
            {
                player.SendTransientError("Could not consume the captured essence.");
                return false;
            }

            if (!TryAwardResidueToPlayer(player, amount, out var awarded) || awarded <= 0)
            {
                log.Error($"[Potency] Salvage award failed for {player.Name} after consuming {essence.Name} (expected {expectedAmount:F2}, amount {amount}).");
                player.SendTransientError("You do not have enough pack space for the Savage Echo.");
                return false;
            }

            var creatureName = essence.GetProperty(PropertyString.CapturedCreatureName) ?? essence.Name;
            player.SendMessage($"You salvage {creatureName} into {awarded:N0} {CurrencyDisplayName}.");

            if (ServerConfig.pet_potency_debug_chat.Value)
                player.SendMessage($"[Potency] Salvage expected {expectedAmount:F2}, awarded {awarded:N0} (level {creatureLevel}, hollow={isHollow}, shiny={isShiny}).");

            if (ServerConfig.pet_potency_debug_log.Value)
                log.Info($"[Potency] {player.Name} salvaged {essence.Name} -> {awarded} Savage Echo (expected {expectedAmount:F2}, level {creatureLevel}).");

            return true;
        }

        private static int GetCapturedCreatureLevel(WorldObject essence)
        {
            var creatureWcid = essence.GetProperty(PropertyInt.CapturedCreatureWCID);
            if (!creatureWcid.HasValue)
                return 1;

            var weenie = DatabaseManager.World.GetCachedWeenie((uint)creatureWcid.Value);
            if (weenie == null)
                return 1;

            return weenie.GetProperty(PropertyInt.Level) ?? 1;
        }

        public static void ApplyBodyPartPotencyScaling(CombatPet pet, PetDevice device)
        {
            if (pet == null || device == null || !ServerConfig.pet_potency_enabled.Value)
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
                if (TryAwardResidueToPlayer(owner, amount, out var awarded) && awarded > 0)
                {
                    if (ServerConfig.pet_potency_debug_chat.Value)
                        owner.SendMessage($"You receive {awarded:N0} {CurrencyDisplayName} (expected {expectedAmount:F2}).");

                    if (ServerConfig.pet_potency_debug_log.Value)
                        log.Info($"[Potency] {owner.Name} awarded {awarded} Savage Echo (expected {expectedAmount:F2}) from {creature.Name} kill.");
                }
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
                var stackSize = Math.Min(remaining, 1000);
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
                return "Potency: 0 (use Savage Echo on this essence to train body damage).";

            var active = GetActivePotency(device);
            var dormant = GetDormantPotency(device);
            var pct = (int)Math.Round((GetBodyPartDamageMult(active) - 1.0f) * 100.0);

            var msg = $"Potency: {stored:N0} stored ({active:N0} active, {dormant:N0} dormant)\nBody Training: +{pct}% damage from potency (active)";

            if (ServerConfig.pet_strain_enabled.Value && active > ServerConfig.pet_strain_potency_threshold.Value)
            {
                var strain = PetPotencyMath.GetBondStrainRating(
                    active,
                    strainEnabled: true,
                    strainThreshold: (int)ServerConfig.pet_strain_potency_threshold.Value,
                    strainPerPotencyLevel: ServerConfig.pet_strain_per_potency_level.Value,
                    strainMaxRating: (int)ServerConfig.pet_strain_max_rating.Value);
                if (strain > 0)
                    msg += $"\nBond Strain: −{strain:N0} damage rating while combat pet summoned";
            }

            return msg;
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
