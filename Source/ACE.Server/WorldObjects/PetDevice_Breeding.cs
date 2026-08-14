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

                // Stat Inheritance Package Deal (55/45 Rule: Couples Stat Value & Mutation Count together)
                (int val, int muts) InheritStat(PropertyInt propVal, PropertyInt propMuts, int defaultVal)
                {
                    var val1 = device1.GetProperty(propVal) ?? defaultVal;
                    var val2 = device2.GetProperty(propVal) ?? defaultVal;
                    var muts1 = device1.GetProperty(propMuts) ?? 0;
                    var muts2 = device2.GetProperty(propMuts) ?? 0;

                    var isHigh1 = val1 >= val2;
                    var chosenIs1 = ThreadSafeRandom.Next(0.0f, 1.0f) < 0.55f ? isHigh1 : !isHigh1;

                    var chosenVal = chosenIs1 ? val1 : val2;
                    var chosenMuts = chosenIs1 ? muts1 : muts2;
                    return (chosenVal, chosenMuts);
                }

                var potRes = InheritStat(PropertyInt.PetPotencyStored, PropertyInt.PetMutPotency, 150);
                var dmgRes = InheritStat(PropertyInt.DamageRating, PropertyInt.PetMutDamageRating, 0);
                var drRes = InheritStat(PropertyInt.DamageResistRating, PropertyInt.PetMutDamageResistRating, 0);
                var critRes = InheritStat(PropertyInt.CritRating, PropertyInt.PetMutCritRating, 0);
                var vitRes = InheritStat(PropertyInt.Vitality, PropertyInt.PetMutVitality, 0);

                var babyPotency = potRes.val;
                var babyDmg = dmgRes.val;
                var babyDR = drRes.val;
                var babyCrit = critRes.val;
                var babyVitality = vitRes.val;

                var babyDmgMuts = dmgRes.muts;
                var babyDrMuts = drRes.muts;
                var babyCritMuts = critRes.muts;
                var babyVitMuts = vitRes.muts;
                var babyPotMuts = potRes.muts;

                var babyCritDmg = (int)Math.Round(babyDmg * 0.8);
                var babyCritResist = (int)Math.Round(babyDR * 0.8);
                var babyCritDmgResist = (int)Math.Round(babyDR * 0.6);

                var totalParentStatMuts = babyDmgMuts + babyDrMuts + babyCritMuts + babyVitMuts;

                // Roll 1: Normal Stat Mutation (15% base decaying odds per stat line, max 20 muts per stat)
                var mutChance = Math.Max(0.015, 0.15 / (1.0 + 0.75 * totalParentStatMuts));
                var isMutated = ServerConfig.pet_breeding_force_mutation.Value || ThreadSafeRandom.Next(0.0f, 1.0f) < mutChance;

                // Roll 2: Independent Potency Mutation Roll (2.0% Fixed Rare Chance, Uncapped / Soft-Capped at 1000)
                var isPotencyMutated = ThreadSafeRandom.Next(0.0f, 1.0f) < 0.02f;

                string mutatedStatName = null;
                int mutatedStatBoost = 0;

                if (isPotencyMutated)
                {
                    int potStep = 20; // Fixed +20 Potency Step
                    if (babyPotency >= 1000 && babyPotency < 2000)
                        potStep = 5;  // Soft-cap diminishing step
                    
                    if (babyPotency < 2000)
                    {
                        babyPotency += potStep;
                        babyPotMuts += 1;
                        mutatedStatName = "Potency";
                        mutatedStatBoost = potStep;
                    }
                }

                uint? babyPaletteBase = null;

                if (isMutated)
                {
                    var eligibleStats = new System.Collections.Generic.List<string>();
                    if (babyDmgMuts < 20) eligibleStats.Add("DamageRating");
                    if (babyDrMuts < 20) eligibleStats.Add("DamageResistRating");
                    if (babyCritMuts < 20) eligibleStats.Add("CritRating");
                    if (babyVitMuts < 20) eligibleStats.Add("Vitality");

                    if (eligibleStats.Count > 0)
                    {
                        var chosenStat = eligibleStats[ThreadSafeRandom.Next(0, eligibleStats.Count)];
                        if (chosenStat == "DamageRating")
                        {
                            mutatedStatName = "Damage Rating";
                            mutatedStatBoost = 3; // Fixed +3 Step
                            babyDmg += 3;
                            babyDmgMuts += 1;
                        }
                        else if (chosenStat == "DamageResistRating")
                        {
                            mutatedStatName = "Damage Resist Rating";
                            mutatedStatBoost = 3; // Fixed +3 Step
                            babyDR += 3;
                            babyDrMuts += 1;
                        }
                        else if (chosenStat == "DamageResistRating")
                        {
                            mutatedStatName = "Crit Rating";
                            mutatedStatBoost = 2; // Fixed +2 Step
                            babyCrit += 2;
                            babyCritMuts += 1;
                        }
                        else if (chosenStat == "Vitality")
                        {
                            mutatedStatName = "Vitality";
                            mutatedStatBoost = 200; // Fixed +200 HP Step
                            babyVitality += 200;
                            babyVitMuts += 1;
                        }
                    }
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

                if (isMutated)
                {
                    // Pick a random 0x04... DAT Palette ID from 3D Showroom DAT pool for this species
                    try
                    {
                        var pool = ACE.Server.Services.VisualizerService.GetSmartPalettePool(babyWcid, "all");
                        if (pool != null && pool.Count > 0)
                        {
                            babyPaletteBase = pool[ThreadSafeRandom.Next(0, pool.Count)].PaletteId;
                        }
                    }
                    catch { }
                }

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

                // Write dedicated PetMut properties
                if (babyDmgMuts > 0) baby.SetProperty(PropertyInt.PetMutDamageRating, babyDmgMuts * 3);
                if (babyDrMuts > 0) baby.SetProperty(PropertyInt.PetMutDamageResistRating, babyDrMuts * 3);
                if (babyCritMuts > 0) baby.SetProperty(PropertyInt.PetMutCritRating, babyCritMuts * 2);
                if (babyVitMuts > 0) baby.SetProperty(PropertyInt.PetMutVitality, babyVitMuts * 200);
                if (babyPotMuts > 0) baby.SetProperty(PropertyInt.PetMutPotency, babyPotMuts * 20);

                var totalMutations = babyDmgMuts + babyDrMuts + babyCritMuts + babyVitMuts + babyPotMuts;
                if (totalMutations > 0)
                    baby.SetProperty(PropertyInt.PetMutationCount, totalMutations);

                if (babyPaletteBase.HasValue && babyPaletteBase.Value != 0)
                {
                    baby.SetProperty(PropertyDataId.PaletteBase, babyPaletteBase.Value);
                    baby.PaletteBaseId = babyPaletteBase.Value;
                }

                var successMsg = $"Congratulations! A baby pet has been born: {baby.Name}! Placed in {winner.Name}'s inventory.";
                if (isMutated || isPotencyMutated)
                    successMsg += $" 🌟 GENETIC MUTATION! Gained +{mutatedStatBoost} {mutatedStatName} & Rare DAT Palette unlocked!";

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
                    if (isMutated || isPotencyMutated)
                        fullMsg += $" 🌟 GENETIC MUTATION! Gained +{mutatedStatBoost} {mutatedStatName} & Rare DAT Palette unlocked!";

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
