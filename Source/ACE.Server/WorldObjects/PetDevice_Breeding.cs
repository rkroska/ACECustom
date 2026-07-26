using System;
using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    public partial class PetDevice : WorldObject
    {
        public void TryBreedPets(Player player, PetDevice targetDevice)
        {
            if (player == null || targetDevice == null)
                return;

            if (!ServerConfig.pet_breeding_enabled.Value)
            {
                player.SendTransientError("Pet breeding is not enabled on this server.");
                return;
            }

            if (player.IsBusy)
            {
                player.SendTransientError("You are too busy to do that.");
                return;
            }

            // Guard against race conditions by locking the player's busy state
            player.IsBusy = true;

            try
            {
                // Self-breeding check
                if (Guid == targetDevice.Guid)
                {
                    player.SendTransientError("A pet cannot breed with itself.");
                    return;
                }

                // Inventory validation
                var inInventory1 = player.FindObject(Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
                var inInventory2 = player.FindObject(targetDevice.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
                if (!inInventory1 || !inInventory2)
                {
                    player.SendTransientError("Both pets must be in your inventory or equipped.");
                    return;
                }

                // Combat pet check
                if (!IsCombatPetDevice() || !targetDevice.IsCombatPetDevice())
                {
                    player.SendTransientError("Only combat pets can be bred.");
                    return;
                }

                // Attunement check
                if (!IsPetBondAttuned || !targetDevice.IsPetBondAttuned ||
                    PetBondAttunedCharacterId != (long)player.Character.Id ||
                    targetDevice.PetBondAttunedCharacterId != (long)player.Character.Id)
                {
                    player.SendTransientError("Both parent pets must be attuned and bonded to you.");
                    return;
                }

                // Location validation
                var allowedLandblock = (uint)ServerConfig.pet_breeding_allowed_landblock.Value;
                var allowedVariant = (int)ServerConfig.pet_breeding_allowed_variant.Value;
                if (allowedLandblock > 0)
                {
                    var currentLandblock = player.Location.Landblock;
                    var currentVariant = player.CurrentLandblock?.VariationId ?? -1;
                    if (currentLandblock != allowedLandblock || (allowedVariant != -1 && currentVariant != allowedVariant))
                    {
                        player.SendTransientError("You must be in the Seedy Motel to breed your pets.");
                        return;
                    }
                }

                // Parent level/tier checks
                var lvl1 = ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.GetPetLevel(WeenieClassId);
                var lvl2 = ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.GetPetLevel(targetDevice.WeenieClassId);
                if (!lvl1.HasValue || !lvl2.HasValue)
                {
                    player.SendTransientError("Failed to determine parent pet tiers.");
                    return;
                }

                var minParentLevel = (int)ServerConfig.pet_breeding_min_parent_level.Value;
                if (lvl1.Value < minParentLevel || lvl2.Value < minParentLevel)
                {
                    player.SendTransientError($"Parent pets must be at least tier {minParentLevel} to breed.");
                    return;
                }

                // Parent bond checks
                var minBond = (int)ServerConfig.pet_breeding_min_bond.Value;
                var bond1 = PetBondLevel ?? 1;
                var bond2 = targetDevice.PetBondLevel ?? 1;
                if (bond1 < minBond || bond2 < minBond)
                {
                    player.SendTransientError($"Parent pets must have a bond level of at least {minBond} to breed.");
                    return;
                }

                // Cooldown check
                var now = Time.GetUnixTime();
                var next1 = GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                var next2 = targetDevice.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                if (now < next1)
                {
                    var remaining = TimeSpan.FromSeconds(next1 - now);
                    player.SendTransientError($"{Name} is not ready to breed. Cooldown remaining: {remaining.Hours}h {remaining.Minutes}m.");
                    return;
                }
                if (now < next2)
                {
                    var remaining = TimeSpan.FromSeconds(next2 - now);
                    player.SendTransientError($"{targetDevice.Name} is not ready to breed. Cooldown remaining: {remaining.Hours}h {remaining.Minutes}m.");
                    return;
                }

                // Calculate baby stats
                var targetLevel = (lvl1.Value + lvl2.Value) / 2;
                var babyLevel = GetFlooredPetLevel(targetLevel);
                var masteryRoll = (global::ACE.Entity.Enum.SummoningMastery)ThreadSafeRandom.Next(1, 4); // Primalist=1, Necromancer=2, Naturalist=3
                var babyWcid = (uint)global::ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.RollBaby(masteryRoll, babyLevel);

                // Potency calculations: randomize between average of parents and highest of parents (inclusive)
                var pot1 = PetPotencyStored ?? 0;
                var pot2 = targetDevice.PetPotencyStored ?? 0;
                var minPot = (pot1 + pot2) / 2;
                var maxPot = Math.Max(pot1, pot2);
                var babyPot = minPot == maxPot ? minPot : ThreadSafeRandom.Next(minPot, maxPot + 1);

                // Set parent breeding cooldowns
                var cooldownSeconds = ServerConfig.pet_breeding_cooldown_hours.Value * 3600.0;
                SetProperty(PropertyFloat.PetNextBreedingTime, now + cooldownSeconds);
                targetDevice.SetProperty(PropertyFloat.PetNextBreedingTime, now + cooldownSeconds);

                ChangesDetected = true;
                targetDevice.ChangesDetected = true;

                SaveBiotaToDatabase();
                targetDevice.SaveBiotaToDatabase();

                SyncPetProgressPropertiesToOwner(player, broadcast: true);
                targetDevice.SyncPetProgressPropertiesToOwner(player, broadcast: true);

                // Instantiate the baby
                var baby = WorldObjectFactory.CreateNewWorldObject(babyWcid) as PetDevice;
                if (baby == null)
                {
                    player.SendTransientError("Failed to spawn the baby pet device.");
                    return;
                }

                baby.PetBondAttuned = true;
                baby.PetBondAttunedCharacterId = (long)player.Character.Id;
                baby.PetBondLevel = 1; // Baby starts at Bond Level 1 per exploit review feedback
                baby.PetPotencyStored = babyPot;
                baby.Attuned = AttunedStatus.Attuned;
                baby.Bonded = BondedStatus.Bonded;

                if (player.TryCreateInInventoryWithNetworking(baby))
                {
                    player.SendMessage($"Congratulations! A baby pet has been born: {baby.Name}!");
                    player.PlayParticleEffect(PlayScript.VisionUpWhite, player.Guid);
                }
                else
                {
                    player.SendTransientError("Your inventory is full! The baby pet essence fell on the ground.");
                    baby.Location = new ACE.Entity.Position(player.Location);
                    baby.EnterWorld();
                }

                baby.SaveBiotaToDatabase();
            }
            finally
            {
                player.IsBusy = false;
            }
        }

        public static int GetFlooredPetLevel(int target)
        {
            var levels = new[] { 50, 80, 100, 125, 150, 180, 200, 250, 300 };
            int result = levels[0];
            foreach (var lvl in levels)
            {
                if (lvl <= target)
                    result = lvl;
            }
            return result;
        }

        public string BuildBreedingAppraisalBlock()
        {
            if (!IsCombatPetDevice())
                return null;

            if (!ServerConfig.pet_breeding_enabled.Value)
                return null;

            var now = Time.GetUnixTime();
            var nextBreeding = GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
            if (now < nextBreeding)
            {
                var remaining = TimeSpan.FromSeconds(nextBreeding - now);
                return $"Breeding Cooldown: {remaining.Hours}h {remaining.Minutes}m remaining";
            }
            return "Breeding: Ready to breed";
        }
    }
}
