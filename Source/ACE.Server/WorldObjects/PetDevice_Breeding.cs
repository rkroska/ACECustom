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

            // 2. Check if player1 is in the Seedy Motel (unless Admin bypass)
            var isAdminBypass = player1.IsAdmin;
            var allowedLandblock = (uint)ServerConfig.pet_breeding_allowed_landblock.Value;
            var allowedVariant = (int)ServerConfig.pet_breeding_allowed_variant.Value;
            if (allowedLandblock > 0 && !isAdminBypass)
            {
                var currentLandblock = player1.Location.Landblock;
                var currentVariant = player1.CurrentLandblock?.VariationId ?? -1;
                if (currentLandblock != allowedLandblock || (allowedVariant != -1 && currentVariant != allowedVariant))
                    return;
            }

            // 3. Scan for a dancing partner in the same landblock
            var nowUtc = DateTime.UtcNow;
            var players = player1.CurrentLandblock?.players;
            if (players == null)
                return;

            Player partner = null;
            CombatPet pet2 = null;

            var playersSnapshot = players.ToList();
            foreach (var otherPlayer in playersSnapshot)
            {
                if (otherPlayer.Guid == player1.Guid)
                    continue;

                if (otherPlayer.IsTrading)
                    continue;

                if (player1.Location.Cell != otherPlayer.Location.Cell && !isAdminBypass)
                    continue;

                if (player1.GetDistance(otherPlayer) > 10.0f && !isAdminBypass)
                    continue;

                var isOtherDancing = (otherPlayer.LastSoulEmote == MotionCommand.DrudgeDance || otherPlayer.LastSoulEmote == MotionCommand.DrudgeDanceState) && nowUtc < otherPlayer.LastSoulEmoteEndTime;
                if (!isOtherDancing && !isAdminBypass)
                    continue;

                if (otherPlayer.CurrentActivePet is not CombatPet otherPet)
                    continue;

                if (pet1.Location.Cell != otherPet.Location.Cell && !isAdminBypass)
                    continue;

                if (pet1.GetDistance(otherPet) > 5.0f && !isAdminBypass)
                    continue;

                partner = otherPlayer;
                pet2 = otherPet;
                break;
            }

            if (partner == null || pet2 == null)
                return;

            if (player1.IsBusy || partner.IsBusy)
                return;

            player1.IsBusy = true;
            partner.IsBusy = true;

            try
            {
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

                var inInv1 = player1.FindObject(device1.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
                var inInv2 = partner.FindObject(device2.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
                if (!inInv1 || !inInv2)
                {
                    player1.SendTransientError("Summoning devices must remain in inventory to breed.");
                    partner.SendTransientError("Summoning devices must remain in inventory to breed.");
                    return;
                }

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

                // Check Alpha / Non-Alpha Role Pairing
                var isAlpha1 = device1.GetProperty(PropertyBool.IsAlphaPet) ?? false;
                var isAlpha2 = device2.GetProperty(PropertyBool.IsAlphaPet) ?? false;

                if (isAlpha1 && isAlpha2 && !isAdminBypass)
                {
                    player1.SendTransientError("Breeding cancelled: Two Alpha Studs cannot breed together! One pet must be a Non-Alpha Donor.");
                    partner.SendTransientError("Breeding cancelled: Two Alpha Studs cannot breed together! One pet must be a Non-Alpha Donor.");
                    return;
                }

                // Identify Alpha Stud and Non-Alpha Donor
                PetDevice alphaDevice = isAlpha1 ? device1 : (isAlpha2 ? device2 : device1); // default device1 as Alpha if neither set
                PetDevice donorDevice = alphaDevice == device1 ? device2 : device1;

                var nowUnix = Time.GetUnixTime();

                // Check Alpha Charges (10 daily charges)
                var alphaCharges = alphaDevice.GetProperty(PropertyInt.PetAlphaStamina) ?? 10;
                if (alphaCharges <= 0 && !isAdminBypass)
                {
                    var alphaOwner = alphaDevice == device1 ? player1 : partner;
                    alphaOwner.SendTransientError($"{alphaDevice.Name} has exhausted its 10 daily Alpha Breeding Charges. Rest for 24h or use an Alpha Stamina Tonic.");
                    return;
                }

                // Check Non-Alpha Cooldown (4-hour cooldown)
                var nextDonor = donorDevice.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                if (nowUnix < nextDonor && !isAdminBypass)
                {
                    var remaining = TimeSpan.FromSeconds(nextDonor - nowUnix);
                    player1.SendTransientError($"{donorDevice.Name} is a Non-Alpha Donor on cooldown. Remaining: {remaining.Hours}h {remaining.Minutes}m.");
                    partner.SendTransientError("Breeding cancelled: Non-Alpha Donor is on cooldown.");
                    return;
                }

                // Stat Inheritance (55/45 Rule)
                int InheritRating(PropertyInt prop)
                {
                    var val1 = device1.GetProperty(prop) ?? 0;
                    var val2 = device2.GetProperty(prop) ?? 0;
                    var highVal = Math.Max(val1, val2);
                    var lowVal = Math.Min(val1, val2);
                    var chosen = ThreadSafeRandom.Next(0.0f, 1.0f) < 0.55f ? highVal : lowVal;
                    var variance = ThreadSafeRandom.Next(-1, 2); // +-1 variance
                    return Math.Max(0, chosen + variance);
                }

                var babyPotency = InheritRating(PropertyInt.PetPotencyStored);
                var babyDmg = InheritRating(PropertyInt.DamageRating);
                var babyDR = InheritRating(PropertyInt.DamageResistRating);
                var babyCrit = InheritRating(PropertyInt.CritRating);
                var babyCritDmg = InheritRating(PropertyInt.CritDamageRating);
                var babyCritResist = InheritRating(PropertyInt.CritResistRating);
                var babyCritDmgResist = InheritRating(PropertyInt.CritDamageResistRating);
                var babyVitality = InheritRating(PropertyInt.Vitality);

                // Parent mutations count
                var parentMutations1 = device1.GetProperty(PropertyInt.PetMutationCount) ?? 0;
                var parentMutations2 = device2.GetProperty(PropertyInt.PetMutationCount) ?? 0;
                var totalParentMuts = parentMutations1 + parentMutations2;

                // Diminishing returns mutation chance formula: max(0.015, 0.15 / (1 + 0.75 * totalParentMuts))
                var mutChance = Math.Max(0.015, 0.15 / (1.0 + 0.75 * totalParentMuts));
                var isMutated = ServerConfig.pet_breeding_force_mutation.Value || ThreadSafeRandom.Next(0.0f, 1.0f) < mutChance;

                string mutatedStatName = null;
                int mutatedStatBoost = 0;

                var parentPalette1 = device1.VisualOverridePaletteTemplate ?? device1.PaletteTemplate ?? 0;
                var parentPalette2 = device2.VisualOverridePaletteTemplate ?? device2.PaletteTemplate ?? 0;
                var parentShade1 = device1.VisualOverrideShade ?? device1.Shade ?? 0.0;
                var parentShade2 = device2.VisualOverrideShade ?? device2.Shade ?? 0.0;

                var babyPalette = ThreadSafeRandom.Next(0, 2) == 0 ? parentPalette1 : parentPalette2;
                var babyShade = (parentShade1 + parentShade2) / 2.0;

                if (isMutated)
                {
                    // Select 1 random stat target out of 8 ratings for POSITIVE boost
                    var statChoice = ThreadSafeRandom.Next(0, 8);
                    switch (statChoice)
                    {
                        case 0:
                            mutatedStatName = "Potency";
                            mutatedStatBoost = ThreadSafeRandom.Next(2, 6); // +2 to +5
                            babyPotency += mutatedStatBoost;
                            break;
                        case 1:
                            mutatedStatName = "Damage Rating";
                            mutatedStatBoost = ThreadSafeRandom.Next(3, 6); // +3 to +5
                            babyDmg += mutatedStatBoost;
                            break;
                        case 2:
                            mutatedStatName = "Damage Resist Rating";
                            mutatedStatBoost = ThreadSafeRandom.Next(3, 6);
                            babyDR += mutatedStatBoost;
                            break;
                        case 3:
                            mutatedStatName = "Crit Rating";
                            mutatedStatBoost = ThreadSafeRandom.Next(2, 5);
                            babyCrit += mutatedStatBoost;
                            break;
                        case 4:
                            mutatedStatName = "Crit Damage Rating";
                            mutatedStatBoost = ThreadSafeRandom.Next(3, 6);
                            babyCritDmg += mutatedStatBoost;
                            break;
                        case 5:
                            mutatedStatName = "Crit Resist Rating";
                            mutatedStatBoost = ThreadSafeRandom.Next(3, 6);
                            babyCritResist += mutatedStatBoost;
                            break;
                        case 6:
                            mutatedStatName = "Crit Damage Resist Rating";
                            mutatedStatBoost = ThreadSafeRandom.Next(3, 6);
                            babyCritDmgResist += mutatedStatBoost;
                            break;
                        case 7:
                            mutatedStatName = "Vitality";
                            mutatedStatBoost = ThreadSafeRandom.Next(200, 501); // +200 to +500 HP
                            babyVitality += mutatedStatBoost;
                            break;
                    }

                    // 100% Guaranteed Rare Color Palette Override on Mutation
                    var rarePalettes = new[] {
                        (int)global::ACE.Entity.Enum.PaletteTemplate.Gold,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.Silver,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.Copper,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.Black,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.AquaBlue,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.Purple,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.Red,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.Rose,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.SnowyWhite,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.DyeWinterBlue,
                        (int)global::ACE.Entity.Enum.PaletteTemplate.DyeWinterGreen
                    };
                    babyPalette = rarePalettes[ThreadSafeRandom.Next(0, rarePalettes.Length)];
                }

                // Update charges & cooldowns
                if (!isAdminBypass)
                {
                    alphaDevice.SetProperty(PropertyInt.PetAlphaStamina, Math.Max(0, alphaCharges - 1));
                    donorDevice.SetProperty(PropertyFloat.PetNextBreedingTime, nowUnix + 14400.0); // 4-hour cooldown
                }

                alphaDevice.ChangesDetected = true;
                donorDevice.ChangesDetected = true;
                alphaDevice.SaveBiotaToDatabase();
                donorDevice.SaveBiotaToDatabase();

                // Roll 50/50 for baby level and species donor
                var babyLevel = ThreadSafeRandom.Next(0, 2) == 0 ? lvl1.Value : lvl2.Value;
                var masteryRoll = (global::ACE.Entity.Enum.SummoningMastery)ThreadSafeRandom.Next(1, 4);
                var babyWcid = (uint)global::ACE.Server.Factories.Tables.Wcids.PetDeviceWcids.RollBaby(masteryRoll, babyLevel);

                var donor = ThreadSafeRandom.Next(0, 2) == 0 ? device1 : device2;
                var winner = ThreadSafeRandom.Next(0, 2) == 0 ? player1 : partner;

                var baby = WorldObjectFactory.CreateNewWorldObject(babyWcid) as PetDevice;
                if (baby == null)
                {
                    player1.SendTransientError("Failed to spawn the baby pet device.");
                    partner.SendTransientError("Failed to spawn the baby pet device.");
                    return;
                }

                // Copy visual overrides
                baby.VisualOverrideSetup = donor.VisualOverrideSetup;
                baby.VisualOverrideMotionTable = donor.VisualOverrideMotionTable;
                baby.VisualOverrideCombatTable = donor.VisualOverrideCombatTable;
                baby.VisualOverrideSoundTable = donor.VisualOverrideSoundTable;
                baby.VisualOverridePaletteBase = donor.VisualOverridePaletteBase;
                baby.VisualOverrideClothingBase = donor.VisualOverrideClothingBase;
                baby.VisualOverrideScale = donor.VisualOverrideScale;
                baby.VisualOverrideName = donor.VisualOverrideName;
                baby.VisualOverrideCreatureVariant = donor.VisualOverrideCreatureVariant;
                baby.VisualOverrideCreatureType = donor.VisualOverrideCreatureType;

                // Write baby ratings & properties
                baby.PetBondAttuned = false;
                baby.PetBondAttunedCharacterId = 0;
                baby.PetBondLevel = 1;
                baby.PetPotencyStored = babyPotency;
                baby.SetProperty(PropertyInt.DamageRating, babyDmg);
                baby.SetProperty(PropertyInt.DamageResistRating, babyDR);
                baby.SetProperty(PropertyInt.CritRating, babyCrit);
                baby.SetProperty(PropertyInt.CritDamageRating, babyCritDmg);
                baby.SetProperty(PropertyInt.CritResistRating, babyCritResist);
                baby.SetProperty(PropertyInt.CritDamageResistRating, babyCritDmgResist);
                baby.SetProperty(PropertyInt.Vitality, babyVitality);

                var babyMutations = totalParentMuts + (isMutated ? 1 : 0);
                if (babyMutations > 0)
                    baby.SetProperty(PropertyInt.PetMutationCount, babyMutations);

                if (babyPalette > 0)
                    baby.VisualOverridePaletteTemplate = babyPalette;
                if (babyShade > 0.0)
                    baby.VisualOverrideShade = babyShade;

                var successMsg = $"Congratulations! A baby pet has been born: {baby.Name}! Placed in {winner.Name}'s inventory.";
                if (isMutated)
                    successMsg += $" 🌟 GENETIC MUTATION! Gained +{mutatedStatBoost} {mutatedStatName} & Rare Essence Palette unlocked!";

                if (winner.TryCreateInInventoryWithNetworking(baby))
                {
                    player1.SendMessage(successMsg);
                    partner.SendMessage(successMsg);
                    player1.PlayParticleEffect(PlayScript.VisionUpWhite, player1.Guid);
                    partner.PlayParticleEffect(PlayScript.VisionUpWhite, partner.Guid);
                    pet1.PlayParticleEffect(PlayScript.WeddingBliss, pet1.Guid);
                    pet2.PlayParticleEffect(PlayScript.WeddingBliss, pet2.Guid);
                }
                else
                {
                    baby.PetBondAttuned = true;
                    baby.PetBondAttunedCharacterId = winner.Guid.Full;
                    baby.Attuned = AttunedStatus.Attuned;

                    var fullMsg = $"A baby pet has been born: {baby.Name}! {winner.Name}'s inventory was full, so the baby fell on the ground (attuned to {winner.Name}).";
                    if (isMutated)
                        fullMsg += $" 🌟 GENETIC MUTATION! Gained +{mutatedStatBoost} {mutatedStatName} & Rare Essence Palette unlocked!";

                    player1.SendMessage(fullMsg);
                    partner.SendMessage(fullMsg);
                    baby.Location = new Position(winner.Location);
                    baby.EnterWorld();
                }

                baby.SaveBiotaToDatabase();
            }
            finally
            {
                player1.IsBusy = false;
                partner.IsBusy = false;
            }
        }

        public string BuildBreedingAppraisalBlock()
        {
            if (!IsCombatPetDevice())
                return null;

            if (!ServerConfig.pet_breeding_enabled.Value)
                return null;

            var sb = new System.Text.StringBuilder();

            var isAlpha = GetProperty(PropertyBool.IsAlphaPet) ?? false;
            if (isAlpha)
                sb.AppendLine($"Role: [Alpha Stud] ({GetProperty(PropertyInt.PetAlphaStamina) ?? 10} / 10 Daily Charges)");
            else
            {
                var now = Time.GetUnixTime();
                var nextBreeding = GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                if (now < nextBreeding)
                {
                    var remaining = TimeSpan.FromSeconds(nextBreeding - now);
                    sb.AppendLine($"Role: [Non-Alpha Donor] (Cooldown: {remaining.Hours}h {remaining.Minutes}m remaining)");
                }
                else
                    sb.AppendLine("Role: [Non-Alpha Donor] (Ready to breed)");
            }

            var mutationCount = GetProperty(PropertyInt.PetMutationCount) ?? 0;
            if (mutationCount > 0)
                sb.AppendLine($"Mutations: {mutationCount}");

            var dmg = GetProperty(PropertyInt.DamageRating) ?? 0;
            var dr = GetProperty(PropertyInt.DamageResistRating) ?? 0;
            var crit = GetProperty(PropertyInt.CritRating) ?? 0;
            if (dmg > 0 || dr > 0 || crit > 0)
                sb.AppendLine($"Ratings: Damage +{dmg} | DR +{dr} | Crit +{crit}");

            sb.AppendLine("Ritual: Perform /dance in Seedy Motel");
            return sb.ToString().TrimEnd();
        }
    }
}
