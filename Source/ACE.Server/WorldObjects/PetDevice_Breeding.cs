using System;
using System.Linq;

using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    public partial class PetDevice : WorldObject
    {
        /// <summary>
        /// True when the player stands inside the configured breeding area. Accepts pet_breeding_allowed_landblock
        /// as either a 16-bit landblock (0x016C) or a full raw cell (0x016C0102), and honours the variant filter.
        /// A configured landblock of 0 means "anywhere".
        /// </summary>
        private static bool IsInBreedingArea(Player player)
        {
            if (player?.Location == null)
                return false;

            var allowedLandblock = (uint)ServerConfig.pet_breeding_allowed_landblock.Value;
            var allowedVariant = (int)ServerConfig.pet_breeding_allowed_variant.Value;

            var targetLb = allowedLandblock > 0xFFFF ? (allowedLandblock >> 16) : allowedLandblock;
            var currentVariant = player.CurrentLandblock?.VariationId ?? -1;

            var locValid = allowedLandblock == 0
                || player.Location.Landblock == targetLb
                || player.Location.Cell == allowedLandblock;

            var varValid = allowedVariant == -1 || currentVariant == allowedVariant;

            return locValid && varValid;
        }

        /// <summary>
        /// Everything decided about a breed BEFORE the baby object exists: participants, the species
        /// donor, the rolled palette, and every inherited/mutated stat. CompleteBirth consumes it.
        /// Splitting the decision from the birth lets the birth be deferred (e.g. until a mating
        /// guardian dies) without recomputing or re-rolling anything.
        /// </summary>
        private sealed class PendingBreed
        {
            public Player Player1, Partner, Winner;
            public PetDevice Donor;
            public CombatPet Pet1, Pet2;
            public uint BabyWcid;
            public uint? BabyPaletteBase;
            public System.Collections.Generic.List<string> MutationSummary;
            public int BabyPotency, BabyDmg, BabyDR, BabyCrit, BabyCritDmg, BabyCritResist, BabyCritDmgResist, BabyVitality;
            public int BabyDmgMuts, BabyDrMuts, BabyCritMuts, BabyVitMuts, BabyPotMuts;
            public int DmgStep, DrStep, CritStep, VitStep, PotStepConfig;

            // Phase 2 (mating guardian) bookkeeping.
            public uint GuardianGuid;
            public Position FallbackDropLocation;
        }

        /// <summary>
        /// Breeds waiting on a mating guardian, keyed by guardian GUID. In-memory only: a server
        /// restart drops them (the parents already paid; the guardian cannot survive a restart).
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, PendingBreed> pendingGuardianBreeds = new();

        private static PendingBreed FindPendingGuardianBreed(Player player)
        {
            if (player == null) return null;
            foreach (var p in pendingGuardianBreeds.Values)
            {
                if (p.Player1?.Guid.Full == player.Guid.Full || p.Partner?.Guid.Full == player.Guid.Full)
                    return p;
            }
            return null;
        }

        /// <param name="forced">Set by the admin @breed command: skips the mutual-dance requirement and
        /// tells both owners the ritual was forced rather than performed.</param>
        public static void CheckMultiplayerBreeding(Player player1, string triggerSource = "Manual", bool forced = false)
        {
            if (player1 == null)
                return;

            if (ServerConfig.pet_breeding_verbose_logging.Value)
                log.Info($"[PetBreeding] Breeding trigger received from {player1.Name} (Source: {triggerSource}, LB: 0x{player1.Location.Landblock:X4}, Cell: 0x{player1.Location.LandblockId.Raw:X8})");

            if (!ServerConfig.pet_breeding_enabled.Value)
            {
                if (ServerConfig.pet_breeding_verbose_logging.Value)
                    log.Warn($"[PetBreeding] Breeding aborted: ServerConfig.pet_breeding_enabled is false.");
                if (player1.IsAdmin) player1.SendMessage("[Breeding Debug] Breeding failed: ServerConfig.pet_breeding_enabled is FALSE.");
                return;
            }

            if (player1.IsTrading)
            {
                if (player1.IsAdmin) player1.SendMessage("[Breeding Debug] Breeding failed: Player is in trade.");
                return;
            }

            // 1. Check if player1 has an active summoned combat pet
            if (player1.CurrentActivePet is not CombatPet pet1)
            {
                if (player1.IsAdmin) player1.SendMessage("[Breeding Debug] Breeding failed: You do not have an active Combat Pet summoned.");
                return;
            }

            // 2. Check if player1 is in the allowed breeding area (unless Admin bypass)
            // Admin bypass covers LOCATION checks only (area, landblock, pet landcell) as a testing
            // convenience. It must NOT skip the economy - sex pairing, breeding charges, recovery
            // cooldown - or admins can never test those rules, and they silently never apply to admin
            // characters. Use @pet-reset-cooldown to reset charges/cooldown between test breeds.
            var isAdminBypass = player1.IsAdmin;
            var currentLandblock = player1.Location.Landblock;

            if (!IsInBreedingArea(player1))
            {
                if (ServerConfig.pet_breeding_verbose_logging.Value)
                    log.Info($"[PetBreeding] {player1.Name} location check failed: current LB=0x{currentLandblock:X4} (Cell=0x{player1.Location.Cell:X8}, Var={player1.CurrentLandblock?.VariationId ?? -1}).");
                if (isAdminBypass)
                {
                    player1.SendMessage($"[Breeding Debug] Location check would fail for non-admins (current LB 0x{currentLandblock:X4}), but bypassed for Admin.");
                }
                else
                {
                    player1.SendTransientError("You must be in the designated breeding area to perform the breeding ritual.");
                    return;
                }
            }

            // Both partners must have danced within the sync window. player1 just danced (this call is
            // driven by that emote); the partner is checked below.
            var danceWindow = TimeSpan.FromSeconds(ServerConfig.pet_breeding_dance_sync_seconds.Value);
            var nowUtc = DateTime.UtcNow;

            var device1 = pet1.TryGetSummoningDevice() ?? player1.FindObject(pet1.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
            if (device1 == null)
            {
                player1.SendTransientError("Failed to locate parent summoning device.");
                return;
            }

            // 3. Scan for a partner: same landblock, also inside the breeding area, who danced within
            //    the sync window, and whose pet shares a landcell with ours.
            var onlinePlayers = PlayerManager.GetAllOnline();
            var roomCandidates = new System.Collections.Generic.List<(Player Player, CombatPet Pet)>();

            foreach (var otherPlayer in onlinePlayers)
            {
                if (otherPlayer.Guid == player1.Guid)
                    continue;

                if (otherPlayer.IsTrading)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Currently in trade.");
                    continue;
                }

                var sameLandblock = otherPlayer.Location.Landblock == player1.Location.Landblock;
                var dist = player1.GetDistance(otherPlayer);

                if (!sameLandblock && !isAdminBypass)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Different landblock (0x{otherPlayer.Location.Landblock:X4}).");
                    continue;
                }

                if (!IsInBreedingArea(otherPlayer) && !isAdminBypass)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Not inside the designated breeding area.");
                    continue;
                }

                var sinceTheirDance = nowUtc - otherPlayer.LastDanceTime;
                if (sinceTheirDance > danceWindow && !forced)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Has not danced within {danceWindow.TotalSeconds:0.#}s (last dance {sinceTheirDance.TotalSeconds:0.#}s ago).");
                    continue;
                }

                if (otherPlayer.CurrentActivePet is not CombatPet otherPet)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Found nearby player {otherPlayer.Name} ({dist:F1}m away), but they have NO active combat pet summoned.");
                    continue;
                }

                if (pet1.Location == null || otherPet.Location == null)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: A pet has no world location yet (still spawning or teleporting).");
                    continue;
                }

                if (pet1.Location.Cell != otherPet.Location.Cell && !isAdminBypass)
                {
                    if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Skipping {otherPlayer.Name}: Their pet is in landcell 0x{otherPet.Location.Cell:X8}, yours is in 0x{pet1.Location.Cell:X8}. The pets must share a landcell.");
                    continue;
                }

                roomCandidates.Add((otherPlayer, otherPet));
            }

            if (roomCandidates.Count == 0)
            {
                if (player1.IsAdmin)
                    player1.SendMessage($"[Breeding Debug] No eligible partner found: needs to be in the breeding area, have danced within {danceWindow.TotalSeconds:0.#}s, and have a summoned pet sharing your pet's landcell. (Checked {onlinePlayers.Count} online players)");
                else if (!forced)
                    player1.SendMessage("[Breeding] Your pet performs the courtship dance, waiting for a partner... (Your partner must have their pet summoned in the same room and *dance* within 5 seconds).");
                return;
            }

            // In crowded rooms with multiple pairs dancing, prioritize candidates whose pets are mutually compatible
            // (opposite sex, not neutered, adult, charges/cooldowns ready) so bystanders do not block valid pairs.
            bool IsCandidateCompatible(Player p, CombatPet cp)
            {
                if (p.IsBusy) return false;
                var dev = cp.TryGetSummoningDevice() ?? p.FindObject(cp.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
                if (dev == null) return false;
                if (device1.IsMale == dev.IsMale) return false;
                if (device1.GetProperty(PropertyBool.PetNeutered) == true || dev.GetProperty(PropertyBool.PetNeutered) == true) return false;
                if (device1.IsJuvenile || dev.IsJuvenile) return false;
                if (!ServerConfig.pet_breeding_allow_shiny.Value && (device1.IsShiny || dev.IsShiny)) return false;

                if (!ServerConfig.pet_breeding_bypass_male_charges.Value)
                {
                    var maleDev = device1.IsMale ? device1 : dev;
                    var charges = maleDev.GetProperty(PropertyInt.PetMaleBreedingCharges) ?? (int)ServerConfig.pet_breeding_male_max_charges.Value;
                    if (charges <= 0) return false;
                }

                if (!ServerConfig.pet_breeding_bypass_female_cooldown.Value)
                {
                    var femaleDev = device1.IsMale ? dev : device1;
                    var nextBreed = femaleDev.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                    if (nextBreed > (double)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
                }

                return true;
            }

            var compatibleCandidates = roomCandidates.Where(c => IsCandidateCompatible(c.Player, c.Pet)).ToList();
            var chosen = compatibleCandidates.Count > 0
                ? compatibleCandidates.OrderBy(c => player1.GetDistance(c.Player)).First()
                : roomCandidates.OrderBy(c => player1.GetDistance(c.Player)).First();

            Player partner = chosen.Player;
            CombatPet pet2 = chosen.Pet;
            if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Matched partner {partner.Name} ({player1.GetDistance(partner):F1}m away) with summoned pet {pet2.Name} in landcell 0x{pet2.Location.Cell:X8}!");

            if (player1.IsBusy || partner.IsBusy)
            {
                if (player1.IsAdmin) player1.SendMessage($"[Breeding Debug] Breeding aborted: Player or partner is busy.");
                return;
            }

            var pendingFor = FindPendingGuardianBreed(player1) ?? FindPendingGuardianBreed(partner);
            if (pendingFor != null)
            {
                var pendingIsMine = pendingFor.Player1?.Guid.Full == player1.Guid.Full || pendingFor.Partner?.Guid.Full == player1.Guid.Full;
                player1.SendMessage(pendingIsMine
                    ? "[Breeding] You already have a mating guardian to defeat. Finish that ritual first."
                    : $"[Breeding] {partner.Name} already has a mating guardian to defeat. They must finish that ritual first.");
                partner.SendMessage("[Breeding] A mating guardian is still standing from a previous ritual. Finish that one first.");
                return;
            }

            player1.IsBusy = true;
            partner.IsBusy = true;

            try
            {
                var device2 = pet2.TryGetSummoningDevice() ?? partner.FindObject(pet2.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;

                if (device2 == null)
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

                // Shiny is a capture-only trait: it is never bred for and never inherited.
                if (!ServerConfig.pet_breeding_allow_shiny.Value)
                {
                    foreach (var (dev, owner) in new[] { (device1, player1), (device2, partner) })
                    {
                        if (!dev.IsShiny)
                            continue;
                        var other = owner == player1 ? partner : player1;
                        owner.SendTransientError($"{dev.Name} is shiny and cannot breed. Shiny is a capture-only trait.");
                        other.SendTransientError($"Breeding cancelled: {owner.Name}'s pet is shiny and cannot breed.");
                        return;
                    }
                }

                // Juveniles cannot breed until they have been raised to adulthood.
                foreach (var (dev, owner) in new[] { (device1, player1), (device2, partner) })
                {
                    if (!dev.IsJuvenile)
                        continue;
                    var other = owner == player1 ? partner : player1;
                    owner.SendTransientError($"{dev.Name} is still a {dev.MaturityStageName.ToLowerInvariant()} and cannot breed until it is an adult ({dev.MaturityKills}/{MaturityKillsRequired} kills).");
                    other.SendTransientError($"Breeding cancelled: {owner.Name}'s pet is not an adult yet.");
                    return;
                }

                // Breeding requires exactly one male and one female.
                var isMale1 = device1.IsMale;
                var isMale2 = device2.IsMale;

                if (isMale1 == isMale2)
                {
                    var sex = isMale1 ? "males" : "females";
                    var msgSex = $"Breeding cancelled: two {sex} cannot breed. You need one male and one female.";
                    player1.SendTransientError(msgSex);
                    partner.SendTransientError(msgSex);
                    return;
                }

                // The male sires (spends a breeding charge); the female takes the recovery cooldown.
                PetDevice maleDevice = isMale1 ? device1 : device2;
                PetDevice femaleDevice = isMale1 ? device2 : device1;
                var donorDevices = new System.Collections.Generic.List<PetDevice> { femaleDevice };

                var nowUnix = Time.GetUnixTime();

                // Check male breeding charges
                var maleMaxCharges = (int)ServerConfig.pet_breeding_male_max_charges.Value;
                var maleCharges = maleDevice?.GetAvailableMaleCharges() ?? maleMaxCharges;
                if (maleDevice != null && maleCharges <= 0 && !ServerConfig.pet_breeding_bypass_male_charges.Value)
                {
                    var restHours = ServerConfig.pet_breeding_male_charge_reset_hours.Value;
                    var maleOwner = maleDevice == device1 ? player1 : partner;
                    maleOwner.SendTransientError($"{maleDevice.Name} has exhausted its {maleMaxCharges} daily breeding charges. Rest for {restHours:0.#}h.");
                    return;
                }

                // Check female recovery cooldown
                foreach (var donorDevice in donorDevices)
                {
                    var nextDonor = donorDevice.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                    if (nowUnix < nextDonor && !ServerConfig.pet_breeding_bypass_female_cooldown.Value)
                    {
                        var remaining = TimeSpan.FromSeconds(nextDonor - nowUnix);
                        player1.SendTransientError($"{donorDevice.Name} is still recovering from her last litter. Ready in {remaining.Hours}h {remaining.Minutes}m.");
                        partner.SendTransientError("Breeding cancelled: the female is still recovering from her last litter.");
                        return;
                    }
                }

                // Every gate has passed - the breed will now go through.
                if (forced)
                {
                    var forcedMsg = "[Breeding] Breed was forced by an admin - dance ritual not active.";
                    player1.SendMessage(forcedMsg);
                    partner.SendMessage(forcedMsg);
                }

                // Breeding resolves in this same tick, so the particles play once on the birth path below
                // rather than twice in the same instant.
                var ritualMsg = $"The mating ritual has begun between {pet1.Name} and {pet2.Name}...";
                player1.SendMessage(ritualMsg);
                partner.SendMessage(ritualMsg);

                // Stat Mutation Steps
                var dmgStep = (int)ServerConfig.pet_breeding_damage_mutation_step.Value;
                var drStep = (int)ServerConfig.pet_breeding_dr_mutation_step.Value;
                var critStep = (int)ServerConfig.pet_breeding_crit_mutation_step.Value;
                var vitStep = (int)ServerConfig.pet_breeding_vitality_mutation_step.Value;
                var potStepConfig = (int)ServerConfig.pet_breeding_potency_mutation_step.Value;

                // Stat Inheritance Package Deal (55/45 Rule: Couples Stat Value & Mutation Count together)
                (int val, int count) InheritStat(PropertyInt propVal, PropertyInt propCount, PropertyInt propLegacyRating, int step, int defaultVal)
                {
                    var val1 = device1.GetProperty(propVal) ?? defaultVal;
                    var val2 = device2.GetProperty(propVal) ?? defaultVal;
                    var count1 = device1.GetProperty(propCount) ?? ((device1.GetProperty(propLegacyRating) ?? 0) / Math.Max(1, step));
                    var count2 = device2.GetProperty(propCount) ?? ((device2.GetProperty(propLegacyRating) ?? 0) / Math.Max(1, step));

                    var isHigh1 = val1 >= val2;
                    var chosenIs1 = ThreadSafeRandom.Next(0.0f, 1.0f) < 0.55f ? isHigh1 : !isHigh1;

                    var chosenVal = chosenIs1 ? val1 : val2;
                    var chosenCount = chosenIs1 ? count1 : count2;
                    return (chosenVal, chosenCount);
                }

                var potRes = InheritStat(PropertyInt.PetPotencyStored, PropertyInt.PetMutPotencyCount, PropertyInt.PetMutPotency, potStepConfig, 150);
                var dmgRes = InheritStat(PropertyInt.DamageRating, PropertyInt.PetMutDamageCount, PropertyInt.PetMutDamageRating, dmgStep, 0);
                var drRes = InheritStat(PropertyInt.DamageResistRating, PropertyInt.PetMutDamageResistCount, PropertyInt.PetMutDamageResistRating, drStep, 0);
                var critRes = InheritStat(PropertyInt.CritRating, PropertyInt.PetMutCritCount, PropertyInt.PetMutCritRating, critStep, 0);
                var vitRes = InheritStat(PropertyInt.Vitality, PropertyInt.PetMutVitalityCount, PropertyInt.PetMutVitality, vitStep, 0);

                var babyPotency = potRes.val;
                var babyDmg = dmgRes.val;
                var babyDR = drRes.val;
                var babyCrit = critRes.val;
                var babyVitality = vitRes.val;

                var babyDmgMuts = dmgRes.count;
                var babyDrMuts = drRes.count;
                var babyCritMuts = critRes.count;
                var babyVitMuts = vitRes.count;
                var babyPotMuts = potRes.count;

                var babyCritDmg = (int)Math.Round(babyDmg * 0.8);
                var babyCritResist = (int)Math.Round(babyDR * 0.8);
                var babyCritDmgResist = (int)Math.Round(babyDR * 0.6);

                var totalParentStatMuts = babyDmgMuts + babyDrMuts + babyCritMuts + babyVitMuts;

                var baseMutChance = ServerConfig.pet_breeding_base_mutation_chance.Value;
                var decayRate = ServerConfig.pet_breeding_mutation_decay_rate.Value;
                var minFloor = ServerConfig.pet_breeding_mutation_min_floor.Value;
                var potChance = ServerConfig.pet_breeding_potency_mutation_chance.Value;
                var potSoftCap = (int)ServerConfig.pet_breeding_potency_soft_cap.Value;
                var potHardCap = (int)ServerConfig.pet_breeding_potency_hard_cap.Value;
                var maxStatMuts = (int)ServerConfig.pet_breeding_max_stat_mutations.Value;

                // Roll 1: Normal Stat Mutation (decaying odds per stat line, max stat mutations per line)
                var mutChance = Math.Max(minFloor, baseMutChance / (1.0 + decayRate * totalParentStatMuts));
                var isMutated = ServerConfig.pet_breeding_force_mutation.Value || ThreadSafeRandom.Next(0.0f, 1.0f) < mutChance;

                // Roll 2: Independent Potency Mutation Roll
                var isPotencyMutated = ThreadSafeRandom.Next(0.0f, 1.0f) < potChance;

                var mutationSummary = new System.Collections.Generic.List<string>();

                if (isPotencyMutated)
                {
                    int potStep = potStepConfig;
                    if (potSoftCap > 0 && babyPotency >= potSoftCap)
                        potStep = Math.Max(1, potStepConfig / 4);
                    
                    // Clamp the step so the hard cap is never overshot
                    if (potHardCap > 0)
                        potStep = Math.Min(potStep, Math.Max(0, potHardCap - babyPotency));

                    if (potStep > 0)
                    {
                        babyPotency += potStep;
                        babyPotMuts += 1;
                        mutationSummary.Add($"+{potStep} Potency");
                    }
                }

                uint? babyPaletteBase = null;

                if (isMutated)
                {
                    var eligibleStats = new System.Collections.Generic.List<string>();
                    if (maxStatMuts <= 0 || babyDmgMuts < maxStatMuts) eligibleStats.Add("DamageRating");
                    if (maxStatMuts <= 0 || babyDrMuts < maxStatMuts) eligibleStats.Add("DamageResistRating");
                    if (maxStatMuts <= 0 || babyCritMuts < maxStatMuts) eligibleStats.Add("CritRating");
                    if (maxStatMuts <= 0 || babyVitMuts < maxStatMuts) eligibleStats.Add("Vitality");

                    if (eligibleStats.Count > 0)
                    {
                        var chosenStat = eligibleStats[ThreadSafeRandom.Next(0, eligibleStats.Count)];
                        if (chosenStat == "DamageRating")
                        {
                            mutationSummary.Add($"+{dmgStep} Damage Rating");
                            babyDmg += dmgStep;
                            babyDmgMuts += 1;
                        }
                        else if (chosenStat == "DamageResistRating")
                        {
                            mutationSummary.Add($"+{drStep} Damage Resist Rating");
                            babyDR += drStep;
                            babyDrMuts += 1;
                        }
                        else if (chosenStat == "CritRating")
                        {
                            mutationSummary.Add($"+{critStep} Crit Rating");
                            babyCrit += critStep;
                            babyCritMuts += 1;
                        }
                        else if (chosenStat == "Vitality")
                        {
                            mutationSummary.Add($"+{vitStep} Vitality");
                            babyVitality += vitStep;
                            babyVitMuts += 1;
                        }
                    }
                }

                // Update charges & cooldowns - for everyone, admins included. This used to be skipped for
                // admins, which meant charges never decremented and the recovery cooldown was never
                // written on admin characters, so neither rule ever appeared to work in testing.
                if (!ServerConfig.pet_breeding_bypass_male_charges.Value && maleDevice != null)
                {
                    var chargesLeft = Math.Max(0, maleCharges - 1);
                    maleDevice.SetProperty(PropertyInt.PetMaleBreedingCharges, chargesLeft);

                    var studOwner = maleDevice == device1 ? player1 : partner;
                    studOwner.SendMessage($"[Breeding] {maleDevice.Name} spent a breeding charge: {chargesLeft}/{maleMaxCharges} left today.");
                    log.Info($"[PetBreeding] Stud {maleDevice.Name} (0x{maleDevice.Guid.Full:X8}, {studOwner.Name}) charges {maleCharges} -> {chargesLeft}.");
                }

                if (!ServerConfig.pet_breeding_bypass_female_cooldown.Value)
                {
                    var donorCooldown = ServerConfig.pet_breeding_cooldown_hours.Value * 3600.0;
                    foreach (var donorDevice in donorDevices)
                        donorDevice.SetProperty(PropertyFloat.PetNextBreedingTime, nowUnix + donorCooldown);
                }

                foreach (var parentDevice in new[] { device1, device2 })
                {
                    parentDevice.ChangesDetected = true;
                    parentDevice.SaveBiotaToDatabase();
                }

                // Roll 50/50 for species donor parent and winner
                var donor = ThreadSafeRandom.Next(0, 2) == 0 ? device1 : device2;
                // The baby always goes to the female's owner. A coin flip meant the player who ate the
                // recovery cooldown could walk away with nothing, which reads as being robbed.
                var winner = femaleDevice == device1 ? player1 : partner;
                var babyWcid = donor.WeenieClassId;

                if (mutationSummary.Count > 0)
                {
                    // Fully random draw from the same master DAT pool the 3D showroom offers, so a
                    // previewed colour is always one breeding can actually roll. Deliberately unfiltered
                    // and not species-gated: the whole point is that the result is a lottery.
                    try
                    {
                        var pool = ACE.Server.Services.PetMutationService.GetMasterPalettePool();
                        if (pool != null && pool.Count > 0)
                            babyPaletteBase = pool[ThreadSafeRandom.Next(0, pool.Count)].PaletteId;
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"[PetBreeding] Failed to roll a mutation palette: {ex.Message}");
                    }
                }

                var pending = new PendingBreed
                {
                    Player1 = player1, Partner = partner, Winner = winner,
                    Donor = donor, Pet1 = pet1, Pet2 = pet2,
                    BabyWcid = babyWcid, BabyPaletteBase = babyPaletteBase, MutationSummary = mutationSummary,
                    BabyPotency = babyPotency, BabyDmg = babyDmg, BabyDR = babyDR, BabyCrit = babyCrit,
                    BabyCritDmg = babyCritDmg, BabyCritResist = babyCritResist, BabyCritDmgResist = babyCritDmgResist,
                    BabyVitality = babyVitality,
                    BabyDmgMuts = babyDmgMuts, BabyDrMuts = babyDrMuts, BabyCritMuts = babyCritMuts,
                    BabyVitMuts = babyVitMuts, BabyPotMuts = babyPotMuts,
                    DmgStep = dmgStep, DrStep = drStep, CritStep = critStep, VitStep = vitStep, PotStepConfig = potStepConfig,
                };

                // Mutation breeds can be gated behind a mating guardian: a monster wearing the
                // offspring's exact look that the two parent pets must kill together. If the guardian
                // cannot be spawned for any reason, the birth completes immediately instead - the
                // parents have already paid, so the breed must never be lost.
                if (ServerConfig.pet_breeding_guardian_enabled.Value && mutationSummary.Count > 0)
                {
                    if (TrySpawnMatingGuardian(pending))
                        return;
                }

                CompleteBirth(pending);
            }
            finally
            {
                player1.IsBusy = false;
                partner.IsBusy = false;
            }
        }

        /// <summary>
        /// Spawns the mating guardian for a decided mutation breed and defers the birth to its death
        /// (or to the timeout). Returns false if anything prevents the spawn so the caller can fall
        /// back to an immediate birth.
        /// </summary>
        private static bool TrySpawnMatingGuardian(PendingBreed p)
        {
            try
            {
                var pet1 = p.Pet1; var pet2 = p.Pet2;
                if (pet1 == null || pet2 == null || pet1.Location == null || pet1.IsDestroyed || pet2.IsDestroyed)
                {
                    log.Warn("[PetBreeding] Guardian skipped: a parent pet has no location or is gone. Completing birth immediately.");
                    return false;
                }

                var templateWcid = (uint)ServerConfig.pet_breeding_guardian_template_wcid.Value;
                var weenie = DatabaseManager.World.GetCachedWeenie(templateWcid);
                if (weenie == null)
                {
                    log.Warn($"[PetBreeding] Guardian skipped: template weenie {templateWcid} not found (pet_breeding_guardian_template_wcid). Completing birth immediately.");
                    return false;
                }

                var guardian = new MatingGuardian(weenie, GuidManager.NewDynamicGuid());

                // Look: exactly what the baby will look like. Same dressing path as a summon, then the
                // same base/template rule the baby uses (native base, mutation in the template), and
                // the captured palette rows cleared so CalculateObjDesc reaches the template branch.
                p.Donor.ApplyVisualOverridesTo(guardian);
                if (p.BabyPaletteBase.HasValue && p.BabyPaletteBase.Value != 0)
                {
                    var nativeBase = Creature.GetSetupDefaultPaletteId(p.Donor.VisualOverrideSetup ?? 0);
                    if (nativeBase != 0)
                        guardian.PaletteBaseId = nativeBase;
                    guardian.PaletteTemplate = (int)p.BabyPaletteBase.Value;
                    guardian.Biota.PropertiesPalette?.Clear();
                }
                guardian.Name = $"Spirit of {guardian.Name}";
                var translucency = (float)Math.Clamp(ServerConfig.pet_breeding_guardian_translucency.Value, 0.0, 0.95);
                if (translucency > 0.001f)
                    guardian.Translucency = translucency;

                // Stats: the offspring's ratings, health from both parents, damage scaled down.
                var hpMult = Math.Max(0.01, ServerConfig.pet_breeding_guardian_health_mult.Value);
                var dmgMult = Math.Max(0.0, ServerConfig.pet_breeding_guardian_damage_mult.Value);
                var dmgOffset = (int)Math.Round((dmgMult - 1.0) * 100.0);

                guardian.Level = Math.Max(pet1.Level ?? 1, pet2.Level ?? 1);
                guardian.SetProperty(PropertyInt.DamageRating, p.BabyDmg + dmgOffset);
                guardian.SetProperty(PropertyInt.DamageResistRating, p.BabyDR);
                guardian.SetProperty(PropertyInt.CritRating, p.BabyCrit);
                guardian.SetProperty(PropertyInt.CritDamageRating, p.BabyCritDmg);
                guardian.SetProperty(PropertyInt.CritResistRating, p.BabyCritResist);
                guardian.SetProperty(PropertyInt.CritDamageResistRating, p.BabyCritDmgResist);

                var combinedHealth = (double)pet1.Health.MaxValue + pet2.Health.MaxValue;
                guardian.Health.StartingValue = (uint)Math.Max(1, Math.Round(combinedHealth * hpMult));
                guardian.Health.Current = guardian.Health.MaxValue;

                guardian.NeutraliseTemplate();

                var spawnPos = FindGuardianSpawnPosition(pet1, pet2, guardian);
                guardian.Location = spawnPos;
                guardian.Home = new Position(spawnPos);
                p.FallbackDropLocation = new Position(spawnPos);

                guardian.Bind(pet1, pet2, p.Player1, p.Partner, OnGuardianSlain, OnGuardianLost);

                if (!guardian.EnterWorld())
                {
                    log.Warn("[PetBreeding] Guardian skipped: EnterWorld failed. Completing birth immediately.");
                    return false;
                }

                p.GuardianGuid = guardian.Guid.Full;
                pendingGuardianBreeds[guardian.Guid.Full] = p;

                // Physics placement may have nudged it; home must be where it actually stands or the
                // monster loop keeps walking it "home" every idle cycle.
                guardian.Home = new Position(guardian.Location);

                guardian.WakeUp(false);
                guardian.PlayParticleEffect(PlayScript.EnchantUpBlue, guardian.Guid);

                var timeout = Math.Max(5.0, ServerConfig.pet_breeding_guardian_timeout_seconds.Value);
                var stirMsg = $"The union stirs something... {guardian.Name} rises before {pet1.Name} and {pet2.Name}! " +
                              $"Only the two parents can harm it. They have {timeout:0}s to bring it down together.";
                p.Player1.SendMessage(stirMsg);
                p.Partner.SendMessage(stirMsg);

                log.Info($"[PetBreeding] Mating guardian {guardian.Name} (0x{guardian.Guid.Full:X8}) spawned for {p.Player1.Name} + {p.Partner.Name}: " +
                         $"template={templateWcid}, level={guardian.Level}, hp={guardian.Health.MaxValue}, dmgRating={p.BabyDmg + dmgOffset}, " +
                         $"drRating={p.BabyDR}, palette=0x{(p.BabyPaletteBase ?? 0):X8}, timeout={timeout:0}s");

                // On the world queue, not the guardian: an action queued on a creature is silently
                // dropped once that creature has no landblock, which is exactly the case we must handle.
                var timeoutChain = new ACE.Server.Entity.Actions.ActionChain();
                timeoutChain.AddDelaySeconds(timeout);
                timeoutChain.AddAction(WorldManager.ActionQueue, ACE.Server.Entity.Actions.ActionType.PetDevice_GuardianTimeout, () => OnGuardianTimeout(guardian));
                timeoutChain.EnqueueChain();

                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[PetBreeding] Guardian spawn threw; completing birth immediately. {ex}");
                return false;
            }
        }

        /// <summary>
        /// Physics radius of a creature from its live physics object, falling back to its setup's
        /// bounding sphere. Creatures do not push each other apart at placement, so spawn spacing
        /// has to be computed by hand.
        /// </summary>
        private static float GetCreatureRadius(Creature c)
        {
            try
            {
                if (c.PhysicsObj != null)
                {
                    var r = c.PhysicsObj.GetPhysicsRadius();
                    if (r > 0.01f) return r;
                }
            }
            catch { }

            try
            {
                var setup = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.SetupModel>(c.SetupTableId);
                if (setup?.Spheres != null && setup.Spheres.Count > 0)
                    return setup.Spheres[0].Radius * (c.ObjScale ?? 1.0f);
            }
            catch { }

            return 1.0f;
        }

        /// <summary>
        /// Picks a spawn spot for the guardian that does not overlap either parent pet: in front of
        /// pet 1, then behind it, then in front of / behind pet 2, at radius + radius + a gap. The
        /// first candidate clear of both pets wins; if the room is that cramped, the last candidate
        /// is used and physics placement slides it off any wall on entry.
        /// </summary>
        private static Position FindGuardianSpawnPosition(CombatPet pet1, CombatPet pet2, Creature guardian)
        {
            var guardianRadius = GetCreatureRadius(guardian);
            var r1 = GetCreatureRadius(pet1);
            var r2 = GetCreatureRadius(pet2);
            const float gap = 1.5f;

            var candidates = new System.Collections.Generic.List<Position>
            {
                pet1.Location.InFrontOf(r1 + guardianRadius + gap, false),
                pet1.Location.InFrontOf(r1 + guardianRadius + gap, true),
            };
            if (pet2.Location != null)
            {
                candidates.Add(pet2.Location.InFrontOf(r2 + guardianRadius + gap, false));
                candidates.Add(pet2.Location.InFrontOf(r2 + guardianRadius + gap, true));
            }

            Position chosen = null;
            foreach (var c in candidates)
            {
                c.LandblockId = new LandblockId(c.GetCell());
                var clear1 = c.Distance2D(pet1.Location) >= r1 + guardianRadius + 0.5f;
                var clear2 = pet2.Location == null || c.Distance2D(pet2.Location) >= r2 + guardianRadius + 0.5f;
                if (clear1 && clear2)
                {
                    chosen = c;
                    break;
                }
            }

            return chosen ?? candidates[candidates.Count - 1];
        }

        /// <summary>The parent pets killed the guardian: the birth completes.</summary>
        private static void OnGuardianSlain(MatingGuardian guardian)
        {
            if (!pendingGuardianBreeds.TryRemove(guardian.Guid.Full, out var p))
                return;

            var yieldMsg = $"{guardian.Name} yields to its parents and dissolves into light...";
            p.Player1.SendMessage(yieldMsg);
            p.Partner.SendMessage(yieldMsg);

            CompleteBirth(p);
        }

        /// <summary>
        /// The guardian was removed without dying (landblock unload, admin delete). The parents already
        /// paid, so the birth completes exactly as on a timeout.
        /// </summary>
        private static void OnGuardianLost(MatingGuardian guardian)
        {
            if (!pendingGuardianBreeds.TryRemove(guardian.Guid.Full, out var p))
                return;

            var lostMsg = $"{guardian.Name} has vanished. The birth proceeds regardless.";
            p.Player1.SendMessage(lostMsg);
            p.Partner.SendMessage(lostMsg);

            CompleteBirth(p);
        }

        /// <summary>
        /// The guardian outlived its window: it fades and the birth completes anyway. The ritual is
        /// a spectacle, never a way to lose a breed the parents already paid for.
        /// </summary>
        private static void OnGuardianTimeout(MatingGuardian guardian)
        {
            if (guardian.IsResolved)
                return;

            if (!pendingGuardianBreeds.TryRemove(guardian.Guid.Full, out var p))
            {
                guardian.Fade();
                return;
            }

            var fightSeconds = ACE.Server.Entity.Timers.RunningTime - guardian.SpawnTime;
            log.Info($"[PetBreeding] Mating guardian {guardian.Name} (0x{guardian.Guid.Full:X8}) timed out after {fightSeconds:F0}s with {guardian.Health.Current}/{guardian.Health.MaxValue} health left; completing birth.");

            guardian.Fade();

            var fadeMsg = $"{guardian.Name} fades before it can be bested. The birth proceeds regardless.";
            p.Player1.SendMessage(fadeMsg);
            p.Partner.SendMessage(fadeMsg);

            CompleteBirth(p);
        }

        /// <summary>
        /// Creates the baby device from a decided breed, writes its inheritance and mutation, places it
        /// with the winner, announces it, and dismisses the parents. Pure function of the record:
        /// safe to call immediately or later.
        /// </summary>
        private static void CompleteBirth(PendingBreed p)
        {
            // Local aliases keep the birth body identical to its pre-refactor form.
            var player1 = p.Player1; var partner = p.Partner; var winner = p.Winner;
            var donor = p.Donor; var pet1 = p.Pet1; var pet2 = p.Pet2;
            var babyWcid = p.BabyWcid; var babyPaletteBase = p.BabyPaletteBase; var mutationSummary = p.MutationSummary;
            var babyPotency = p.BabyPotency; var babyDmg = p.BabyDmg; var babyDR = p.BabyDR; var babyCrit = p.BabyCrit;
            var babyCritDmg = p.BabyCritDmg; var babyCritResist = p.BabyCritResist; var babyCritDmgResist = p.BabyCritDmgResist;
            var babyVitality = p.BabyVitality;
            var babyDmgMuts = p.BabyDmgMuts; var babyDrMuts = p.BabyDrMuts; var babyCritMuts = p.BabyCritMuts;
            var babyVitMuts = p.BabyVitMuts; var babyPotMuts = p.BabyPotMuts;
            var dmgStep = p.DmgStep; var drStep = p.DrStep; var critStep = p.CritStep; var vitStep = p.VitStep; var potStepConfig = p.PotStepConfig;

            var baby = WorldObjectFactory.CreateNewWorldObject(babyWcid) as PetDevice;
            if (baby == null)
            {
                player1.SendTransientError("Failed to spawn the baby pet device.");
                partner.SendTransientError("Failed to spawn the baby pet device.");
                return;
            }

            baby.Name = donor.Name;

            // Copy visual overrides
            baby.VisualOverrideSetup = donor.VisualOverrideSetup;
            baby.VisualOverrideMotionTable = donor.VisualOverrideMotionTable;
            baby.VisualOverrideCombatTable = donor.VisualOverrideCombatTable;
            baby.VisualOverrideSoundTable = donor.VisualOverrideSoundTable;
            baby.VisualOverridePaletteBase = donor.VisualOverridePaletteBase;
            baby.VisualOverrideClothingBase = donor.VisualOverrideClothingBase;
            baby.VisualOverrideScale = donor.VisualOverrideScale;
            baby.VisualOverrideName = donor.VisualOverrideName;
            // The shiny variant is never inherited, even if a shiny somehow reached this point.
            baby.VisualOverrideCreatureVariant = donor.IsShiny && !ServerConfig.pet_breeding_allow_shiny.Value
                ? null
                : donor.VisualOverrideCreatureVariant;
            baby.VisualOverrideCreatureType = donor.VisualOverrideCreatureType;
            baby.VisualOverrideShade = donor.VisualOverrideShade;
            baby.VisualOverridePaletteTemplate = donor.VisualOverridePaletteTemplate;
            baby.VisualOverrideCapturedItems = donor.VisualOverrideCapturedItems;

            // The ObjDesc recipe (part meshes, subpalette ranges, texture swaps) is what makes the
            // baby look like its parent. Without it a bred pet falls back to the bare weenie.
            foreach (var objDescProp in new[] { PropertyString.CapturedObjDescAnimParts, PropertyString.CapturedObjDescPalettes, PropertyString.CapturedObjDescTextures })
            {
                var val = donor.GetProperty(objDescProp);
                if (!string.IsNullOrEmpty(val))
                    baby.SetProperty(objDescProp, val);
            }

            if (donor.GetProperty(PropertyDataId.Icon) is uint icon && icon != 0)
                baby.SetProperty(PropertyDataId.Icon, icon);
            if (donor.GetProperty(PropertyDataId.IconOverlay) is uint iconOverlay && iconOverlay != 0)
                baby.SetProperty(PropertyDataId.IconOverlay, iconOverlay);

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

            // Write persistent Genetic Mutation Counts & Evaluated Ratings
            if (babyDmgMuts > 0)
            {
                baby.SetProperty(PropertyInt.PetMutDamageCount, babyDmgMuts);
                baby.SetProperty(PropertyInt.PetMutDamageRating, babyDmgMuts * dmgStep);
            }
            if (babyDrMuts > 0)
            {
                baby.SetProperty(PropertyInt.PetMutDamageResistCount, babyDrMuts);
                baby.SetProperty(PropertyInt.PetMutDamageResistRating, babyDrMuts * drStep);
            }
            if (babyCritMuts > 0)
            {
                baby.SetProperty(PropertyInt.PetMutCritCount, babyCritMuts);
                baby.SetProperty(PropertyInt.PetMutCritRating, babyCritMuts * critStep);
            }
            if (babyVitMuts > 0)
            {
                baby.SetProperty(PropertyInt.PetMutVitalityCount, babyVitMuts);
                baby.SetProperty(PropertyInt.PetMutVitality, babyVitMuts * vitStep);
            }
            if (babyPotMuts > 0)
            {
                baby.SetProperty(PropertyInt.PetMutPotencyCount, babyPotMuts);
                baby.SetProperty(PropertyInt.PetMutPotency, babyPotMuts * potStepConfig);
            }

            var totalMutations = babyDmgMuts + babyDrMuts + babyCritMuts + babyVitMuts + babyPotMuts;
            if (totalMutations > 0)
                baby.SetProperty(PropertyInt.PetMutationCount, totalMutations);

            // Creature recolour goes through PaletteTemplate, not PaletteBase. Creature.CalculateObjDesc
            // reads the creature's PaletteTemplate (line ~252) and expands a full 0x04 palette DID into
            // two subpalette ranges (0..255 and 255..1) covering all 2048 slots.
            //
            // CRITICAL: that branch is unreachable when the pet's biota carries any palette/animpart/
            // texture rows, because CalculateObjDesc returns early (line ~141). ApplyCapturedObjDesc
            // populates those rows from CapturedObjDescPalettes, so the captured string must be cleared
            // or the mutation colour is silently discarded.
            // The mutation goes in the TEMPLATE (overlay). The BASE must stay a palette the model
            // legitimately renders with - its native DefaultPaletteId, or the one it was captured
            // with. Every in-game case where the mutation was written into the base rendered
            // nothing; every case with a real base rendered. This is exactly what @create does.
            // Prefer the native base so a donor carrying a stale mutation base can't poison the
            // lineage; fall back to the inherited captured base for models with no native one.
            if (babyPaletteBase.HasValue && babyPaletteBase.Value != 0)
            {
                var nativeBase = Creature.GetSetupDefaultPaletteId(baby.VisualOverrideSetup ?? 0);
                if (nativeBase != 0)
                    baby.VisualOverridePaletteBase = nativeBase;

                baby.VisualOverridePaletteTemplate = (int)babyPaletteBase.Value;
                baby.RemoveProperty(PropertyString.CapturedObjDescPalettes);

                log.Info($"[PetBreeding] Mutation palette 0x{babyPaletteBase.Value:X8} applied to {baby.Name}: " +
                         $"VisualOverridePaletteTemplate={(int)babyPaletteBase.Value}, CapturedObjDescPalettes cleared " +
                         $"(otherwise CalculateObjDesc early-returns and the colour never reaches the client).");
            }

            var mutationSuffix = mutationSummary.Count > 0
                ? $" [GENETIC MUTATION] Gained {string.Join(" and ", mutationSummary)}" +
                  (babyPaletteBase.HasValue ? " & Rare DAT Palette unlocked!" : "!")
                : "";

            var successMsg = $"Congratulations! A baby pet has been born: {baby.Name}! Placed in {winner.Name}'s inventory.";
            successMsg += mutationSuffix;

            // A winner who logged out during a guardian fight cannot receive to inventory; the baby
            // drops attuned where the ritual happened instead of vanishing.
            var winnerOnline = winner.Session != null && !winner.IsDestroyed && winner.Location != null;

            if (winnerOnline && winner.TryCreateInInventoryWithNetworking(baby))
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

                var fullMsg = winnerOnline
                    ? $"A baby pet has been born: {baby.Name}! {winner.Name}'s inventory was full, so the baby fell on the ground (attuned to {winner.Name})."
                    : $"A baby pet has been born: {baby.Name}! {winner.Name} is not online, so the baby was left where the ritual took place (attuned to {winner.Name}).";
                fullMsg += mutationSuffix;

                player1.SendMessage(fullMsg);
                partner.SendMessage(fullMsg);
                var dropAt = winnerOnline ? winner.Location : (p.FallbackDropLocation ?? winner.Location);
                baby.Location = new Position(dropAt);
                baby.EnterWorld();
            }

            // Bred babies start life as juveniles: small, weak, and unable to breed until raised.
            baby.MarkBornJuvenile();

            baby.SaveBiotaToDatabase();

            // Breeding consumes the summon: dismiss both parents after a short delay so the birth
            // particles play out first. Re-breeding then requires a re-summon and its use cooldown,
            // which is the natural pacing between rituals. Destroy() clears the owner's
            // CurrentActivePet itself, and is guarded so a pet already gone is a no-op.
            if (ServerConfig.pet_breeding_dismiss_after_breed.Value)
            {
                // Queued on the world queue so a parent that died in the guardian fight cannot swallow
                // the action, and dismissed directly: this is a system dismissal, not an owner stow, so
                // the post-combat recall block does not apply.
                var dismissChain = new ACE.Server.Entity.Actions.ActionChain();
                dismissChain.AddDelaySeconds(2.0);
                dismissChain.AddAction(WorldManager.ActionQueue, ACE.Server.Entity.Actions.ActionType.PetDevice_DismissAfterBreed, () =>
                {
                    if (!pet1.IsDestroyed)
                        pet1.Destroy();
                    if (!pet2.IsDestroyed)
                        pet2.Destroy();
                });
                dismissChain.EnqueueChain();
            }
        }

        public string BuildBreedingAppraisalBlock()
        {
            if (!IsCombatPetDevice())
                return null;

            if (!ServerConfig.pet_breeding_enabled.Value)
                return null;

            var sb = new System.Text.StringBuilder();

            // One line: sex plus what it means for breeding right now. A male is always the stud and
            // a female always the dam, so a separate "Role" line only repeated the sex.
            var isMale = IsMale;
            if (GetProperty(PropertyBool.PetNeutered) == true)
            {
                sb.AppendLine($"Sex: {SexName} (neutered - cannot breed)");
            }
            else if (IsShiny && !ServerConfig.pet_breeding_allow_shiny.Value)
            {
                sb.AppendLine($"Sex: {SexName} (shiny - cannot breed)");
            }
            else if (IsJuvenile)
            {
                sb.AppendLine($"Sex: {SexName} ({MaturityStageName.ToLowerInvariant()} - cannot breed yet)");
            }
            else if (isMale)
            {
                var maleMax = (int)ServerConfig.pet_breeding_male_max_charges.Value;
                var maleAvailable = GetAvailableMaleCharges();
                var sexLine = $"Sex: Male ({maleAvailable}/{maleMax} breeding charges today)";

                var maleResetHours = ServerConfig.pet_breeding_male_charge_reset_hours.Value;
                if (maleAvailable < maleMax && maleResetHours > 0)
                {
                    var refillAt = (GetProperty(PropertyFloat.PetMaleChargesRefreshTime) ?? 0.0) + maleResetHours * 3600.0;
                    var untilRefill = refillAt - Time.GetUnixTime();
                    if (untilRefill > 0)
                    {
                        var ts = TimeSpan.FromSeconds(untilRefill);
                        sexLine += $" - refills in {(int)ts.TotalHours}h {ts.Minutes}m";
                    }
                }

                sb.AppendLine(sexLine);
            }
            else
            {
                var now = Time.GetUnixTime();
                var nextBreeding = GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                if (now < nextBreeding)
                {
                    var remaining = TimeSpan.FromSeconds(nextBreeding - now);
                    sb.AppendLine($"Sex: Female (recovering - ready to breed in {remaining.Hours}h {remaining.Minutes}m)");
                }
                else
                    sb.AppendLine("Sex: Female (ready to breed)");
            }

            var growthLine = BuildMaturityAppraisalLine();
            if (growthLine != null)
                sb.AppendLine(growthLine);

            var imprintLine = BuildImprintAppraisalLine();
            if (imprintLine != null)
                sb.AppendLine(imprintLine);

            var dmgStep = (int)ServerConfig.pet_breeding_damage_mutation_step.Value;
            var drStep = (int)ServerConfig.pet_breeding_dr_mutation_step.Value;
            var critStep = (int)ServerConfig.pet_breeding_crit_mutation_step.Value;
            var vitStep = (int)ServerConfig.pet_breeding_vitality_mutation_step.Value;
            var potStep = (int)ServerConfig.pet_breeding_potency_mutation_step.Value;

            var dmgMuts = GetProperty(PropertyInt.PetMutDamageCount) ?? ((GetProperty(PropertyInt.PetMutDamageRating) ?? 0) / Math.Max(1, dmgStep));
            var drMuts = GetProperty(PropertyInt.PetMutDamageResistCount) ?? ((GetProperty(PropertyInt.PetMutDamageResistRating) ?? 0) / Math.Max(1, drStep));
            var critMuts = GetProperty(PropertyInt.PetMutCritCount) ?? ((GetProperty(PropertyInt.PetMutCritRating) ?? 0) / Math.Max(1, critStep));
            var vitMuts = GetProperty(PropertyInt.PetMutVitalityCount) ?? ((GetProperty(PropertyInt.PetMutVitality) ?? 0) / Math.Max(1, vitStep));
            var potMuts = GetProperty(PropertyInt.PetMutPotencyCount) ?? ((GetProperty(PropertyInt.PetMutPotency) ?? 0) / Math.Max(1, potStep));

            var statMuts = dmgMuts + drMuts + critMuts + vitMuts;
            var totalMutations = statMuts + potMuts;

            var maxStatMuts = (int)ServerConfig.pet_breeding_max_stat_mutations.Value;
            sb.AppendLine($"Total Mutations: {totalMutations}");

            var dynDmgBonus = dmgMuts * dmgStep;
            var dynDrBonus = drMuts * drStep;
            var dynCritBonus = critMuts * critStep;
            var dynVitBonus = vitMuts * vitStep;
            var dynPotBonus = potMuts * potStep;

            if (totalMutations > 0)
            {
                sb.AppendLine("--- Genetic Mutations ---");
                var capSuffix = maxStatMuts > 0 ? $"/{maxStatMuts}" : "";
                if (dmgMuts > 0) sb.AppendLine($"* Damage:        +{dynDmgBonus} [{dmgMuts}{capSuffix} Muts]");
                if (drMuts > 0) sb.AppendLine($"* Damage Resist: +{dynDrBonus} [{drMuts}{capSuffix} Muts]");
                if (critMuts > 0) sb.AppendLine($"* Crit Rating:   +{dynCritBonus} [{critMuts}{capSuffix} Muts]");
                if (vitMuts > 0) sb.AppendLine($"* Vitality:      +{dynVitBonus} HP [{vitMuts}{capSuffix} Muts]");
                if (potMuts > 0) sb.AppendLine($"* Potency:       +{dynPotBonus} [{potMuts} Muts]");
            }

            var baseDmg = GearDamage ?? 0;
            var baseDr = GearDamageResist ?? 0;
            var baseCrit = GearCrit ?? 0;

            var totalDmg = baseDmg + dynDmgBonus;
            var totalDr = baseDr + dynDrBonus;
            var totalCrit = baseCrit + dynCritBonus;

            if (totalDmg > 0 || totalDr > 0 || totalCrit > 0)
            {
                sb.AppendLine("--- Combat Ratings ---");
                sb.AppendLine($"* Damage: {totalDmg} (Base: {baseDmg}, Mut: +{dynDmgBonus})");
                sb.AppendLine($"* Damage Resist: {totalDr} (Base: {baseDr}, Mut: +{dynDrBonus})");
                sb.AppendLine($"* Crit Rating: {totalCrit} (Base: {baseCrit}, Mut: +{dynCritBonus})");
            }

            // StringBuilder.AppendLine emits "\r\n" on Windows and the AC client draws the bare CR as a
            // music-note glyph. Every string bound for the client must use "\n" only.
            return sb.ToString().Replace("\r\n", "\n").TrimEnd();
        }

        /// <summary>
        /// Stud breeding charges available right now. Charges are refilled lazily: the first read
        /// after pet_breeding_male_charge_reset_hours has elapsed since the last refill tops the device
        /// back up to pet_breeding_male_max_charges. A reset period of 0 disables regeneration.
        /// </summary>
        public int GetAvailableMaleCharges(bool persist = true)
        {
            var maxCharges = Math.Max(0, (int)ServerConfig.pet_breeding_male_max_charges.Value);
            var charges = Math.Min(GetProperty(PropertyInt.PetMaleBreedingCharges) ?? maxCharges, maxCharges);

            var resetHours = ServerConfig.pet_breeding_male_charge_reset_hours.Value;
            if (resetHours <= 0)
                return charges;

            var now = Time.GetUnixTime();
            var lastRefill = GetProperty(PropertyFloat.PetMaleChargesRefreshTime) ?? 0.0;

            // Never stamped (pre-existing device): start the rest window now rather than granting a
            // free instant refill.
            if (lastRefill <= 0.0)
            {
                if (persist)
                {
                    SetProperty(PropertyFloat.PetMaleChargesRefreshTime, now);
                    ChangesDetected = true;
                }
                return charges;
            }

            if (now < lastRefill + resetHours * 3600.0)
                return charges;

            if (persist)
            {
                SetProperty(PropertyInt.PetMaleBreedingCharges, maxCharges);
                SetProperty(PropertyFloat.PetMaleChargesRefreshTime, now);
                ChangesDetected = true;
                SaveBiotaToDatabase();
            }

            return maxCharges;
        }

        public static void RunBreedingDiagnostics(Player player)
        {
            if (player == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== PET BREEDING DIAGNOSTICS ===");
            sb.AppendLine($"Breeding System Enabled: {ServerConfig.pet_breeding_enabled.Value}");

            var allowedLb = (uint)ServerConfig.pet_breeding_allowed_landblock.Value;
            var allowedVar = (int)ServerConfig.pet_breeding_allowed_variant.Value;
            var curLb = player.Location.Landblock;
            var curCell = player.Location.LandblockId.Raw;
            var curVar = player.CurrentLandblock?.VariationId ?? -1;
            var targetLb = allowedLb > 0xFFFF ? (allowedLb >> 16) : allowedLb;
            bool locMatch = (allowedLb == 0) || (curLb == targetLb) || (curCell == allowedLb);
            bool varMatch = (allowedVar == -1) || (curVar == allowedVar);

            sb.AppendLine($"Your Location: Landblock=0x{curLb:X4}, Cell=0x{curCell:X8}, Variant={curVar}");
            sb.AppendLine($"Allowed Target: Landblock=0x{allowedLb:X}, Variant={allowedVar} => Location Match: {(locMatch && varMatch ? "VALID (Room OK)" : "INVALID (Wrong Location)")}");

            if (player.CurrentActivePet is CombatPet myPet)
            {
                var myDevice = myPet.TryGetSummoningDevice() ?? player.FindObject(myPet.SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
                var isMale = myDevice?.IsMale ?? false;
                var maleMaxCharges = (int)ServerConfig.pet_breeding_male_max_charges.Value;
                var maleCharges = myDevice?.GetAvailableMaleCharges() ?? maleMaxCharges;
                var nextBreed = myDevice?.GetProperty(PropertyFloat.PetNextBreedingTime) ?? 0.0;
                var onCooldown = Time.GetUnixTime() < nextBreed;
                sb.AppendLine($"Your Active Pet: {myPet.Name} (WCID {myPet.WeenieClassId}) | Device: {(myDevice != null ? myDevice.Name : "Not Found")}");
                sb.AppendLine($"  Sex: {(myDevice != null ? myDevice.SexName : "?")} {(isMale ? $"({maleCharges}/{maleMaxCharges} charges)" : $"({(onCooldown ? "recovering" : "ready to breed")})")}");
            }
            else
            {
                sb.AppendLine("Your Active Pet: NONE SUMMONED! (Please summon your combat pet)");
            }

            var online = PlayerManager.GetAllOnline();
            sb.AppendLine($"--- Other Online Players ({online.Count} total) ---");
            int candidateCount = 0;
            foreach (var p in online)
            {
                if (p.Guid == player.Guid) continue;
                var dist = player.GetDistance(p);
                var sameLb = p.Location.Landblock == curLb;
                var hasPet = p.CurrentActivePet is CombatPet otherPet;
                sb.AppendLine($"* {p.Name}: Dist={dist:F1}m, LB=0x{p.Location.Landblock:X4}, Pet={(hasPet ? ((CombatPet)p.CurrentActivePet).Name : "None")}, Trade={p.IsTrading}");
                if (hasPet && (dist <= 30.0f || sameLb)) candidateCount++;
            }

            sb.AppendLine($"Valid Partner Candidates within 30m: {candidateCount}");
            sb.AppendLine("================================");

            player.SendMessage(sb.ToString().Replace("\r\n", "\n"));
        }
    }
}
