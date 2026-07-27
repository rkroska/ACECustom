using System;
using System.Linq;
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
        public static void CheckMultiplayerBreeding(Player player1)
        {
            if (player1 == null)
                return;

            if (!ServerConfig.pet_breeding_enabled.Value)
                return;

            if (player1.IsTrading)
                return;

            // 1. Check if player1 has an active summoned combat pet
            if (player1.CurrentActivePet is not CombatPet pet1)
                return;

            // 2. Check if player1 is in the Seedy Motel
            var allowedLandblock = (uint)ServerConfig.pet_breeding_allowed_landblock.Value;
            var allowedVariant = (int)ServerConfig.pet_breeding_allowed_variant.Value;
            if (allowedLandblock > 0)
            {
                var currentLandblock = player1.Location.Landblock;
                var currentVariant = player1.CurrentLandblock?.VariationId ?? -1;
                if (currentLandblock != allowedLandblock || (allowedVariant != -1 && currentVariant != allowedVariant))
                    return;
            }

            // 3. Scan for a dancing partner in the same landblock (which covers the entire Seedy Motel variation)
            var nowUtc = DateTime.UtcNow;
            var players = player1.CurrentLandblock?.players;
            if (players == null)
                return;

            Player partner = null;
            CombatPet pet2 = null;

            // Take a snapshot to avoid collection modification issues while iterating
            var playersSnapshot = players.ToList();
            foreach (var otherPlayer in playersSnapshot)
            {
                if (otherPlayer.Guid == player1.Guid)
                    continue;

                if (otherPlayer.IsTrading)
                    continue;

                // Check distance between players (within 10.0 meters)
                if (player1.GetDistance(otherPlayer) > 10.0f)
                    continue;

                // Check if otherPlayer is also dancing (generous window using LastSoulEmoteEndTime)
                var isOtherDancing = (otherPlayer.LastSoulEmote == MotionCommand.DrudgeDance || otherPlayer.LastSoulEmote == MotionCommand.DrudgeDanceState) && nowUtc < otherPlayer.LastSoulEmoteEndTime;
                if (!isOtherDancing)
                    continue;

                // Check if otherPlayer has a summoned combat pet
                if (otherPlayer.CurrentActivePet is not CombatPet otherPet)
                    continue;

                // Check distance between the two summoned pets (within 5.0 meters)
                if (pet1.GetDistance(otherPet) > 5.0f)
                    continue;

                // Found a valid partner
                partner = otherPlayer;
                pet2 = otherPet;
                break;
            }

            if (partner == null || pet2 == null)
                return;

            // Lock execution on both players using IsBusy to prevent race conditions / double-breeding
            if (player1.IsBusy || partner.IsBusy)
                return;

            player1.IsBusy = true;
            partner.IsBusy = true;

            try
            {
                // 4. Retrieve and validate summoning devices
                var device1 = pet1.TryGetSummoningDevice() ?? player1.FindObject(pet1.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
                var device2 = pet2.TryGetSummoningDevice() ?? partner.FindObject(pet2.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;

                if (device1 == null || device2 == null)
                {
                    player1.SendTransientError("Failed to locate parent summoning devices.");
                    partner.SendTransientError("Failed to locate parent summoning devices.");
                    return;
                }

                // Verify devices remain in the players' inventories (prevent trade/drop exploits)
                var inInv1 = player1.FindObject(device1.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
                var inInv2 = partner.FindObject(device2.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
                if (!inInv1 || !inInv2)
                {
                    player1.SendTransientError("Summoning devices must remain in inventory to breed.");
                    partner.SendTransientError("Summoning devices must remain in inventory to breed.");
                    return;
                }

                // Parent level/tier checks
                var lvl1 = global::ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.GetPetLevel(device1.WeenieClassId);
                var lvl2 = global::ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.GetPetLevel(device2.WeenieClassId);
                if (!lvl1.HasValue || !lvl2.HasValue)
                {
                    player1.SendTransientError("Failed to determine parent pet tiers.");
                    partner.SendTransientError("Failed to determine parent pet tiers.");
                    return;
                }

                var minParentLevel = (int)ServerConfig.pet_breeding_min_parent_level.Value;
                if (lvl1.Value < minParentLevel || lvl2.Value < minParentLevel)
                {
                    var msg = $"Parent pets must be at least tier {minParentLevel} to breed.";
                    player1.SendTransientError(msg);
                    partner.SendTransientError(msg);
                    return;
                }

                // Parent bond checks
                var minBond = (int)ServerConfig.pet_breeding_min_bond.Value;
                var bond1 = device1.PetBondLevel ?? 1;
                var bond2 = device2.PetBondLevel ?? 1;
                if (bond1 < minBond || bond2 < minBond)
                {
                    var msg = $"Parent pets must have a bond level of at least {minBond} to breed.";
                    player1.SendTransientError(msg);
                    partner.SendTransientError(msg);
                    return;
                }

                // Cooldown check
                var nowUnix = Time.GetUnixTime();
                var next1 = device1.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                var next2 = device2.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                if (nowUnix < next1)
                {
                    var remaining = TimeSpan.FromSeconds(next1 - nowUnix);
                    player1.SendTransientError($"{device1.Name} is not ready to breed. Cooldown remaining: {remaining.Hours}h {remaining.Minutes}m.");
                    partner.SendTransientError("Breeding cancelled: partner's pet is on cooldown.");
                    return;
                }
                if (nowUnix < next2)
                {
                    var remaining = TimeSpan.FromSeconds(next2 - nowUnix);
                    player1.SendTransientError("Breeding cancelled: partner's pet is on cooldown.");
                    partner.SendTransientError($"{device2.Name} is not ready to breed. Cooldown remaining: {remaining.Hours}h {remaining.Minutes}m.");
                    return;
                }

                // Calculate baby stats
                var targetLevel = (lvl1.Value + lvl2.Value) / 2;
                var babyLevel = GetFlooredPetLevel(targetLevel);
                var masteryRoll = (global::ACE.Entity.Enum.SummoningMastery)ThreadSafeRandom.Next(1, 4); // Primalist=1, Necromancer=2, Naturalist=3
                var babyWcid = (uint)global::ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.RollBaby(masteryRoll, babyLevel);

                // Potency calculations: randomize between average of parents and highest of parents (inclusive)
                var pot1 = device1.PetPotencyStored ?? 0;
                var pot2 = device2.PetPotencyStored ?? 0;
                var minPot = (pot1 + pot2) / 2;
                var maxPot = Math.Max(pot1, pot2);
                var babyPot = minPot == maxPot ? minPot : ThreadSafeRandom.Next(minPot, maxPot + 1);

                // Set parent breeding cooldowns
                var cooldownSeconds = ServerConfig.pet_breeding_cooldown_hours.Value * 3600.0;
                device1.SetProperty(PropertyFloat.PetNextBreedingTime, nowUnix + cooldownSeconds);
                device2.SetProperty(PropertyFloat.PetNextBreedingTime, nowUnix + cooldownSeconds);

                device1.ChangesDetected = true;
                device2.ChangesDetected = true;

                device1.SaveBiotaToDatabase();
                device2.SaveBiotaToDatabase();

                device1.SyncPetProgressPropertiesToOwner(player1, broadcast: true);
                device2.SyncPetProgressPropertiesToOwner(partner, broadcast: true);

                // Roll 50/50 to see who gets the baby (prevents ninja looting)
                var winner = ThreadSafeRandom.Next(0, 2) == 0 ? player1 : partner;

                // Instantiate the baby
                var baby = WorldObjectFactory.CreateNewWorldObject(babyWcid) as PetDevice;
                if (baby == null)
                {
                    player1.SendTransientError("Failed to spawn the baby pet device.");
                    partner.SendTransientError("Failed to spawn the baby pet device.");
                    return;
                }

                baby.PetBondAttuned = true;
                baby.PetBondAttunedCharacterId = (long)winner.Character.Id;
                baby.PetBondLevel = 1; // Baby starts at Bond Level 1
                baby.PetPotencyStored = babyPot;
                baby.Attuned = AttunedStatus.Attuned;
                baby.Bonded = BondedStatus.Bonded;

                if (winner.TryCreateInInventoryWithNetworking(baby))
                {
                    var msg = $"Congratulations! A baby pet has been born: {baby.Name}! It was placed in {winner.Name}'s inventory.";
                    player1.SendMessage(msg);
                    partner.SendMessage(msg);
                    player1.PlayParticleEffect(PlayScript.VisionUpWhite, player1.Guid);
                    partner.PlayParticleEffect(PlayScript.VisionUpWhite, partner.Guid);
                }
                else
                {
                    var msg = $"A baby pet has been born: {baby.Name}! {winner.Name}'s inventory was full, so the baby fell on the ground.";
                    player1.SendMessage(msg);
                    partner.SendMessage(msg);
                    baby.Location = new Position(winner.Location);
                    baby.EnterWorld();
                    player1.PlayParticleEffect(PlayScript.VisionUpWhite, player1.Guid);
                    partner.PlayParticleEffect(PlayScript.VisionUpWhite, partner.Guid);
                }

                baby.SaveBiotaToDatabase();
            }
            finally
            {
                player1.IsBusy = false;
                partner.IsBusy = false;
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
