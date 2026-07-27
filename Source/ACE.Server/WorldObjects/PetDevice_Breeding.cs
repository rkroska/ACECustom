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

                // Check if players are in the same cell (room)
                if (player1.Location.Cell != otherPlayer.Location.Cell)
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

                // Check if pets are in the same cell
                if (pet1.Location.Cell != otherPet.Location.Cell)
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

                if (device1.GetProperty(PropertyBool.PetNeutered) == true)
                {
                    player1.SendTransientError("Your pet is spayed/neutered and cannot breed.");
                    return;
                }

                if (device2.GetProperty(PropertyBool.PetNeutered) == true)
                {
                    player1.SendTransientError($"{partner.Name}'s pet is spayed/neutered and cannot breed.");
                    partner.SendTransientError("Your pet is spayed/neutered and cannot breed.");
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

                // Color inheritance
                var parentPalette1 = device1.VisualOverridePaletteTemplate ?? device1.PaletteTemplate ?? 0;
                var parentPalette2 = device2.VisualOverridePaletteTemplate ?? device2.PaletteTemplate ?? 0;

                var parentShade1 = device1.VisualOverrideShade ?? device1.Shade ?? 0.0;
                var parentShade2 = device2.VisualOverrideShade ?? device2.Shade ?? 0.0;

                // Base inheritance (50/50 roll for palette, average for shade)
                var babyPalette = ThreadSafeRandom.Next(0, 2) == 0 ? parentPalette1 : parentPalette2;
                var babyShade = (parentShade1 + parentShade2) / 2.0;

                // Add minor random drift to shade (±0.05)
                babyShade += ThreadSafeRandom.Next(-0.05f, 0.05f);
                babyShade = Math.Clamp(babyShade, 0.0, 1.0);

                // Potency calculations: randomize between average of parents and highest of parents (inclusive)
                var pot1 = device1.PetPotencyStored ?? 0;
                var pot2 = device2.PetPotencyStored ?? 0;
                var minPot = (pot1 + pot2) / 2;
                var maxPot = Math.Max(pot1, pot2);
                var babyPot = minPot == maxPot ? minPot : ThreadSafeRandom.Next(minPot, maxPot + 1);

                // Mutation check (20% if hits max potency, or forced by config)
                var isMutated = false;
                if (ServerConfig.pet_breeding_force_mutation.Value || (babyPot == maxPot && ThreadSafeRandom.Next(0.0f, 1.0f) < 0.20))
                {
                    babyPot = maxPot + 2;
                    isMutated = true;

                    // Color Mutation triggers on stat mutation!
                    if (ThreadSafeRandom.Next(0.0f, 1.0f) < 0.5)
                    {
                        var rarePalettes = new[] {
                            (int)global::ACE.Entity.Enum.PaletteTemplate.Gold,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.Silver,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.Copper,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.Black,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.AquaBlue,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.Purple,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.Red,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.BluePurple,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.Rose,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.SnowyWhite,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.DyeWinterBlue,
                            (int)global::ACE.Entity.Enum.PaletteTemplate.DyeWinterGreen
                        };
                        babyPalette = rarePalettes[ThreadSafeRandom.Next(0, rarePalettes.Length)];
                    }
                    else
                    {
                        babyShade = ThreadSafeRandom.Next(0.0f, 1.0f);
                    }
                }

                // Stored parent pedigree mutations
                var parentMutations1 = device1.GetProperty(PropertyInt.PetMutationCount) ?? 0;
                var parentMutations2 = device2.GetProperty(PropertyInt.PetMutationCount) ?? 0;
                var babyMutations = parentMutations1 + parentMutations2 + (isMutated ? 1 : 0);

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

                baby.PetBondAttuned = false;
                baby.PetBondAttunedCharacterId = 0;
                baby.PetBondLevel = 1; // Baby starts at Bond Level 1
                baby.PetPotencyStored = babyPot;
                baby.Attuned = AttunedStatus.Normal;
                baby.Bonded = BondedStatus.Normal;

                if (babyPalette > 0)
                    baby.VisualOverridePaletteTemplate = babyPalette;
                if (babyShade > 0.0)
                    baby.VisualOverrideShade = babyShade;

                if (babyMutations > 0)
                    baby.SetProperty(PropertyInt.PetMutationCount, babyMutations);

                var successMsg = $"Congratulations! A baby pet has been born: {baby.Name}! It was placed in {winner.Name}'s inventory.";
                if (isMutated)
                    successMsg += " A genetic mutation has occurred! The baby gained a potency boost and unique colors!";

                if (winner.TryCreateInInventoryWithNetworking(baby))
                {
                    player1.SendMessage(successMsg);
                    partner.SendMessage(successMsg);
                    player1.PlayParticleEffect(PlayScript.VisionUpWhite, player1.Guid);
                    partner.PlayParticleEffect(PlayScript.VisionUpWhite, partner.Guid);
                    pet1.PlayParticleEffect(PlayScript.WeddingBliss, pet1.Guid);
                    pet2.PlayParticleEffect(PlayScript.WeddingBliss, pet2.Guid);
                    pet1.EnqueueBroadcastMotion(new global::ACE.Server.Entity.Motion(MotionStance.NonCombat, MotionCommand.Twitch1));
                    pet2.EnqueueBroadcastMotion(new global::ACE.Server.Entity.Motion(MotionStance.NonCombat, MotionCommand.Twitch1));
                }
                else
                {
                    var fullMsg = $"A baby pet has been born: {baby.Name}! {winner.Name}'s inventory was full, so the baby fell on the ground.";
                    if (isMutated)
                        fullMsg += " A genetic mutation has occurred! The baby gained a potency boost and unique colors!";
                    player1.SendMessage(fullMsg);
                    partner.SendMessage(fullMsg);
                    baby.Location = new Position(winner.Location);
                    baby.EnterWorld();
                    player1.PlayParticleEffect(PlayScript.VisionUpWhite, player1.Guid);
                    partner.PlayParticleEffect(PlayScript.VisionUpWhite, partner.Guid);
                    pet1.PlayParticleEffect(PlayScript.WeddingBliss, pet1.Guid);
                    pet2.PlayParticleEffect(PlayScript.WeddingBliss, pet2.Guid);
                    pet1.EnqueueBroadcastMotion(new global::ACE.Server.Entity.Motion(MotionStance.NonCombat, MotionCommand.Twitch1));
                    pet2.EnqueueBroadcastMotion(new global::ACE.Server.Entity.Motion(MotionStance.NonCombat, MotionCommand.Twitch1));
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

            var sb = new System.Text.StringBuilder();

            var isNeutered = GetProperty(PropertyBool.PetNeutered) ?? false;
            if (isNeutered)
            {
                sb.AppendLine("Breeding: [Neutered]");
            }
            else
            {
                var now = Time.GetUnixTime();
                var nextBreeding = GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                if (now < nextBreeding)
                {
                    var remaining = TimeSpan.FromSeconds(nextBreeding - now);
                    sb.AppendLine($"Breeding Cooldown: {remaining.Hours}h {remaining.Minutes}m remaining");
                }
                else
                {
                    sb.AppendLine("Breeding: Ready to breed");
                }
            }

            var mutationCount = GetProperty(PropertyInt.PetMutationCount) ?? 0;
            if (mutationCount > 0)
            {
                sb.Append($"Mutations: {mutationCount}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
