using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Managers;
using ACE.Server.Web.Controllers;
using ACE.Server.WorldObjects;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Controllers
{
    /// <summary>
    /// Public data for the player pet guide (#/pets). The pages hardcode no numbers: every value comes from
    /// the live server - ServerConfig (effective values, so shard overrides apply), the rule constants the game
    /// code itself reads, and the world database (item names and icons, vendor stock and prices, NPC spawns,
    /// quest timers, NPC scripts). Only what a player can see or needs to plan is exposed.
    /// </summary>
    [ApiController]
    [Route("api/pet-guide")]
    public class PetGuideController : BaseController
    {
        // Items and NPCs the guide talks about. These are identities, not tuning: names, icons, prices and
        // locations are all read from the world database.
        private static readonly (string Key, uint Wcid)[] GuideItems =
        {
            ("flawedLens", 78780001), ("pristineLens", 78780002), ("perfectLens", 78780003),
            ("resonanceLens", 78780010), ("shimmeringEcho", 78780011), ("asheronsLens", 78780012),
            ("siphonedEssence", PetPotency.SiphonedEssenceWcid), ("hollowEssence", PetPotency.HollowEssenceWcid),
            ("monsterDex", MonsterDexWcid),
            ("savageEcho", PetPotency.EssenceResidueWcid), ("essenceResonator", PetPotency.EssenceResonatorWcid),
            ("refillCharm", 78780030), ("universalCharm", 78780031),
            ("sealedOrder", SealedOrderWcid), ("sealedLensOrder", 696900225),
            ("campRugganGem", 19853089), ("masteryCertificate", MasteryCertificateWcid), ("mmd", MmdWcid),
        };

        // Must be declared before GuideNpcs: static fields initialise in declaration order, and GuideNpcs
        // reads this one.
        private static readonly uint[] MasteryStatueWcids = { 49516, 49515, 49514 }; // Primalist, Necromancer, Naturalist

        private static readonly (string Key, uint[] Wcids)[] GuideNpcs =
        {
            ("mrsRuggan", new uint[] { MrsRugganWcid }),
            ("schneebs", new uint[] { SchneebsWcid, 696900227 }),
            ("profRuggan", new uint[] { 694201298 }),
            ("echoWeaver", new uint[] { EchoWeaverWcid }),
            ("crystallineResonator", new uint[] { 78780023 }),
            ("lensCollector", new uint[] { LensCollectorWcid }),
            ("banderling", new uint[] { BanderlingWcid }),
            ("bao", new uint[] { 19853063 }),
            ("elmer", new uint[] { 19853048 }),
            ("gunther", new uint[] { 19853047 }),
            ("tamantha", new uint[] { 98760107 }),
            ("masteryStatues", MasteryStatueWcids),
            ("motelPortal", new uint[] { 98760388 }),
            ("fenwick", new uint[] { 78780200 }),
            ("ivo", new uint[] { IvoWcid }),
        };

        private const uint MonsterDexWcid = 78780005;
        private const uint SealedOrderWcid = 696900223;
        private const uint MasteryCertificateWcid = 46422;
        private const uint MmdWcid = 20630;
        private const uint MrsRugganWcid = 696900224;
        private const uint SchneebsWcid = 696900226;
        private const uint EchoWeaverWcid = 78780020;
        private const uint LensCollectorWcid = 78780022;
        private const uint BanderlingWcid = 19853088;
        private const uint IvoWcid = 78780201;

        private const string ResonanceLensQuest = "resonance_lens_daily";
        private const string ShimmeringEchoQuest = "shimmeringecho_pickup_daily";
        private const string MasteryChangeQuest = "UsedSummoningMasteryReset";

        // World data changes only with a content push; cache it so the public page is cheap.
        private static readonly TimeSpan WorldCacheLifetime = TimeSpan.FromMinutes(5);
        private static readonly object worldCacheLock = new();
        private static object worldCache;
        private static DateTime worldCacheBuiltUtc;

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Get()
        {
            return Ok(new
            {
                features = new
                {
                    siphonLensDrops = ServerConfig.siphon_lens_enabled.Value,
                    bond = ServerConfig.pet_bond_enabled.Value,
                    potency = ServerConfig.pet_potency_enabled.Value,
                    savageEchoDrops = ServerConfig.pet_potency_enabled.Value && ServerConfig.pet_residue_drops_enabled.Value,
                    essenceSalvage = ServerConfig.pet_potency_enabled.Value && ServerConfig.pet_residue_salvage_enabled.Value,
                    bredEssenceSalvage = ServerConfig.pet_potency_enabled.Value && ServerConfig.pet_residue_salvage_enabled.Value && ServerConfig.pet_bred_essence_salvage_enabled.Value,
                    bondStrain = ServerConfig.pet_potency_enabled.Value && ServerConfig.pet_strain_enabled.Value,
                    breeding = ServerConfig.pet_breeding_enabled.Value,
                    breedingSpirit = ServerConfig.pet_breeding_guardian_enabled.Value,
                    maturity = ServerConfig.pet_maturity_enabled.Value,
                    refillCharm = ServerConfig.pet_device_pyreal_auto_refill_enabled.Value && CharmSettingsManager.EssenceRefill.Enabled,
                    universalCharm = ServerConfig.pet_charm_universal_summoning_mastery_enabled.Value && CharmSettingsManager.UniversalSummoning.Enabled,
                    captureDamageType = ServerConfig.pet_apply_capture_source_damage_type.Value,
                },
                capture = new
                {
                    rangeMeters = MonsterCapture.CaptureRangeM,
                    maxHealthFraction = MonsterCapture.CaptureMaxHealthFraction,
                    maxHealthPoints = MonsterCapture.CaptureMaxHealthPoints,
                    minChance = MonsterCapture.CaptureMinRate,
                    lenses = new[]
                    {
                        new { key = "flawedLens", baseChance = MonsterCapture.FlawedLensBaseRate, maxChance = MonsterCapture.FlawedLensRateCap },
                        new { key = "pristineLens", baseChance = MonsterCapture.PristineLensBaseRate, maxChance = MonsterCapture.PristineLensRateCap },
                        new { key = "perfectLens", baseChance = MonsterCapture.PerfectLensBaseRate, maxChance = MonsterCapture.PerfectLensRateCap },
                    },
                    assessSkillBonusMax = MonsterCapture.AssessSkillBonusMax,
                    // The Assess Creature skill that reaches the full bonus.
                    assessSkillForMaxBonus = MonsterCapture.AssessSkillBonusMax * MonsterCapture.AssessSkillBonusDivisor,
                    assessSpecializedBonus = MonsterCapture.AssessSpecializedBonus,
                    lowHealthBonusMax = MonsterCapture.LowHealthBonusMax,
                    levelPenaltyMax = MonsterCapture.LevelPenaltyMax,
                    // Levels the creature must have over the player for the full penalty.
                    levelsForMaxPenalty = MonsterCapture.LevelPenaltyMax * MonsterCapture.LevelPenaltyDivisor,
                    resonance = new
                    {
                        bonus = MonsterCapture.ResonanceLensBonus,
                        // What a player can actually reach: the Pristine cap plus the bonus, under the lens cap.
                        maxChance = Math.Min(MonsterCapture.PristineLensRateCap + MonsterCapture.ResonanceLensBonus, MonsterCapture.ResonanceLensRateCap),
                    },
                    enrage = new
                    {
                        damageMultiplier = MonsterCapture.EnrageDamageMultiplier,
                        damageReduction = MonsterCapture.EnrageDamageReduction,
                    },
                    lensDrops = new
                    {
                        flawedChance = ServerConfig.siphon_lens_flawed_rate.Value,
                        pristineChance = ServerConfig.siphon_lens_pristine_rate.Value,
                        perfectChance = ServerConfig.siphon_lens_perfect_rate.Value,
                        pristineMinCreatureLevel = LootGenerationFactory.SiphonLensPristineMinCreatureLevel,
                        perfectMinCreatureLevel = LootGenerationFactory.SiphonLensPerfectMinCreatureLevel,
                        rampLevels = LootGenerationFactory.SiphonLensRampLevels,
                        levelBonusDivisor = LootGenerationFactory.SiphonLensLevelBonusDivisor,
                    },
                },
                registry = new
                {
                    milestones = new[] { PetRegistryManager.RegistryMilestoneFirst, PetRegistryManager.RegistryMilestoneSecond, PetRegistryManager.RegistryMilestoneThird },
                    milestoneInterval = PetRegistryManager.RegistryMilestoneInterval,
                },
                charms = new
                {
                    refillCostPerCharge = ServerConfig.pet_device_pyreal_auto_refill_cost_per_charge.Value,
                    refillDiscountByTier = new[] { CharmSettingsManager.EssenceRefill.T1, CharmSettingsManager.EssenceRefill.T2, CharmSettingsManager.EssenceRefill.T3 },
                },
                bond = new
                {
                    levelCap = ServerConfig.pet_bond_level_cap.Value,
                },
                potency = new
                {
                    damagePerLevel = ServerConfig.pet_potency_damage_per_level.Value,
                    bondDivisor = ServerConfig.pet_potency_bond_divisor.Value,
                    activeCap = ServerConfig.pet_potency_active_cap.Value,
                    maxStored = ServerConfig.pet_potency_max_stored.Value,
                    echoDropRequiresBond = ServerConfig.pet_residue_require_bond_attuned.Value,
                    echoPerDrop = new
                    {
                        standard = ServerConfig.pet_residue_drop_default.Value,
                        tier9 = ServerConfig.pet_residue_drop_t9.Value,
                        tier10 = ServerConfig.pet_residue_drop_t10.Value,
                        shinyMultiplier = ServerConfig.pet_residue_shiny_mult.Value,
                    },
                    salvage = new
                    {
                        captured = ServerConfig.pet_residue_salvage_base.Value,
                        shinyMultiplier = ServerConfig.pet_residue_salvage_shiny_mult.Value,
                        bred = ServerConfig.pet_bred_essence_salvage_yield.Value,
                    },
                    strain = new
                    {
                        threshold = ServerConfig.pet_strain_potency_threshold.Value,
                        perLevel = ServerConfig.pet_strain_per_potency_level.Value,
                        max = ServerConfig.pet_strain_max_rating.Value,
                    },
                },
                breeding = new
                {
                    minTier = ServerConfig.pet_breeding_min_parent_level.Value,
                    minBond = ServerConfig.pet_breeding_min_bond.Value,
                    maleCharges = ServerConfig.pet_breeding_male_max_charges.Value,
                    maleChargeResetHours = ServerConfig.pet_breeding_male_charge_reset_hours.Value,
                    femaleRestHours = ServerConfig.pet_breeding_cooldown_hours.Value,
                    danceWindowSeconds = ServerConfig.pet_breeding_dance_sync_seconds.Value,
                    shinyCanBreed = ServerConfig.pet_breeding_allow_shiny.Value,
                    betterParentChance = PetDevice.BreedingMath.HigherParentChance,
                    mutationChance = ServerConfig.pet_breeding_base_mutation_chance.Value,
                    potencyMutationChance = ServerConfig.pet_breeding_potency_mutation_chance.Value,
                    mutationSteps = new
                    {
                        damageRating = ServerConfig.pet_breeding_damage_mutation_step.Value,
                        damageResistRating = ServerConfig.pet_breeding_dr_mutation_step.Value,
                        critRating = ServerConfig.pet_breeding_crit_mutation_step.Value,
                        vitality = ServerConfig.pet_breeding_vitality_mutation_step.Value,
                        potency = ServerConfig.pet_breeding_potency_mutation_step.Value,
                    },
                    spiritSeconds = ServerConfig.pet_breeding_guardian_timeout_seconds.Value,
                    maturity = new
                    {
                        killsRequired = PetDevice.MaturityKillsRequired,
                        minDamageShare = ServerConfig.pet_maturity_min_damage_share.Value,
                        imprintOnSummon = ServerConfig.pet_maturity_imprint_on_summon.Value,
                        stages = Enumerable.Range(1, PetDevice.MaturityStages).Select(stage => new
                        {
                            name = PetDevice.GetMaturityStageName(stage),
                            strength = PetDevice.BreedingMath.MaturityMultiplier(stage, PetDevice.MaturityStages, ServerConfig.pet_maturity_juvenile_strength.Value),
                            size = PetDevice.BreedingMath.MaturityMultiplier(stage, PetDevice.MaturityStages, ServerConfig.pet_maturity_juvenile_scale.Value),
                        }).ToList(),
                    },
                },
                world = GetWorldData(),
            });
        }

        /// <summary>
        /// Potency planner: active potency, damage bonus and Savage Echo costs for a bond / stored potency,
        /// computed by the same math the game uses so the page never carries its own copy of the formulas.
        /// </summary>
        [HttpGet("potency")]
        [AllowAnonymous]
        public IActionResult Potency([FromQuery] int bond = 1, [FromQuery] int stored = 0, [FromQuery] int target = 0)
        {
            var bondCap = (int)ServerConfig.pet_bond_level_cap.Value;
            bond = Math.Clamp(bond, 1, bondCap > 0 ? bondCap : 100000);
            var maxStored = (int)ServerConfig.pet_potency_max_stored.Value;
            var storedLimit = maxStored > 0 ? maxStored : 100000;
            stored = Math.Clamp(stored, 0, storedLimit);
            target = Math.Clamp(target, stored, storedLimit);

            var costBase = ServerConfig.pet_potency_cost_base.Value;
            var costExponent = ServerConfig.pet_potency_cost_exponent.Value;

            var active = PetPotencyMath.GetActivePotency(
                stored,
                bond,
                potencyEnabled: true,
                bondDivisor: (int)ServerConfig.pet_potency_bond_divisor.Value,
                minActiveWhenStored: (int)ServerConfig.pet_potency_bond_offense_min_active.Value,
                activeCap: PetPotency.GetActiveCapFromConfig());

            var bondCapForActive = PetPotencyMath.GetBondOffenseCap(
                bond,
                (int)ServerConfig.pet_potency_bond_divisor.Value,
                (int)ServerConfig.pet_potency_bond_offense_min_active.Value,
                hasStoredPotency: stored > 0,
                activeCap: PetPotency.GetActiveCapFromConfig());

            // Echo to go from stored to target, one level at a time, as the game charges it.
            long costToTarget = 0;
            for (var level = stored; level < target; level++)
                costToTarget += PetPotencyMath.GetUpgradeCost(level, costBase, costExponent);

            var strain = PetPotencyMath.GetBondStrainRating(
                active,
                strainEnabled: ServerConfig.pet_strain_enabled.Value,
                strainThreshold: (int)ServerConfig.pet_strain_potency_threshold.Value,
                strainPerPotencyLevel: ServerConfig.pet_strain_per_potency_level.Value,
                strainMaxRating: (int)ServerConfig.pet_strain_max_rating.Value);

            return Ok(new
            {
                bond,
                stored,
                target,
                active,
                dormant = PetPotencyMath.GetDormantPotency(stored, active),
                activeLimitFromBond = bondCapForActive,
                damageBonus = PetPotencyMath.GetBodyPartDamageMult(active, ServerConfig.pet_potency_damage_per_level.Value * 100.0) - 1.0,
                nextLevelCost = stored < storedLimit ? PetPotencyMath.GetUpgradeCost(stored, costBase, costExponent) : 0,
                costToTarget,
                strain,
            });
        }

        private static object GetWorldData()
        {
            lock (worldCacheLock)
            {
                if (worldCache != null && DateTime.UtcNow - worldCacheBuiltUtc < WorldCacheLifetime)
                    return worldCache;

                worldCache = BuildWorldData();
                worldCacheBuiltUtc = DateTime.UtcNow;
                return worldCache;
            }
        }

        private static object BuildWorldData()
        {
            var items = new Dictionary<string, object>();
            foreach (var (key, wcid) in GuideItems)
            {
                var dto = ItemDto(wcid);
                if (dto != null)
                    items[key] = dto;
            }

            var allNpcWcids = GuideNpcs.SelectMany(n => n.Wcids).Distinct().ToList();
            List<LandblockInstance> spawns;
            using (var context = new WorldDbContext())
            {
                spawns = context.LandblockInstance.AsNoTracking()
                    .Where(i => allNpcWcids.Contains(i.WeenieClassId))
                    .ToList();
            }

            var npcs = new Dictionary<string, object>();
            foreach (var (key, wcids) in GuideNpcs)
            {
                npcs[key] = wcids.Select(wcid => new
                {
                    wcid,
                    name = DatabaseManager.World.GetCachedWeenie(wcid)?.GetProperty(PropertyString.Name),
                    spots = spawns.Where(s => s.WeenieClassId == wcid).Select(SpotDto).ToList(),
                }).ToList();
            }

            return new
            {
                items,
                npcs,
                starterLensCount = GiveStackSize(MrsRugganWcid, 78780001),
                asheronsLensFlawedCost = LensCollectorFlawedCost(),
                resonanceLensCooldownSeconds = DatabaseManager.World.GetCachedQuest(ResonanceLensQuest)?.MinDelta,
                shimmeringEchoCooldownSeconds = DatabaseManager.World.GetCachedQuest(ShimmeringEchoQuest)?.MinDelta,
                mastery = MasteryData(),
                essences = EssenceData(),
                shops = new
                {
                    schneebs = ShopDto(SchneebsWcid),
                    banderling = ShopDto(BanderlingWcid),
                    ivo = ShopDto(IvoWcid),
                },
            };
        }

        private static object ItemDto(uint wcid)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null)
                return null;

            return new
            {
                wcid,
                name = weenie.GetProperty(PropertyString.Name),
                icon = IconUrl(weenie),
                // Lets the page show pyreal prices in MMD notes as well, from the note's real value.
                value = weenie.GetValue(),
            };
        }

        /// <summary>The portal's icon endpoint composes underlay, overlay and effects the way the client does.</summary>
        private static string IconUrl(ACE.Entity.Models.Weenie weenie)
        {
            var icon = weenie.GetProperty(PropertyDataId.Icon);
            if (!icon.HasValue || icon.Value == 0)
                return null;

            var query = new List<string>();
            var underlay = weenie.GetProperty(PropertyDataId.IconUnderlay);
            if (underlay.HasValue && underlay.Value != 0) query.Add($"underlay={underlay.Value}");
            var overlay = weenie.GetProperty(PropertyDataId.IconOverlay);
            if (overlay.HasValue && overlay.Value != 0) query.Add($"overlay={overlay.Value}");
            var uiEffects = weenie.GetProperty(PropertyInt.UiEffects);
            if (uiEffects.HasValue && uiEffects.Value != 0) query.Add($"uiEffects={uiEffects.Value}");

            return $"/api/icon/{icon.Value}" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        }

        private static object SpotDto(LandblockInstance spawn)
        {
            var indoors = (spawn.ObjCellId & 0xFFFF) >= 0x100;
            var cell = spawn.ObjCellId;

            // A town building's rooms use the landblock's own coordinates, so the outdoor cell of the same
            // landblock gives the building's map position. A dungeon (the Seedy Motel) has none: the page
            // names the place instead.
            if (indoors)
            {
                if (!IsBuildingLandblock(cell >> 16))
                    return new { coords = (string)null, indoors };
                cell = (cell & 0xFFFF0000) | 0x0001;
            }

            var position = new Position(cell, spawn.OriginX, spawn.OriginY, spawn.OriginZ,
                spawn.AnglesX, spawn.AnglesY, spawn.AnglesZ, spawn.AnglesW, false, spawn.VariationId);

            return new { coords = position.GetMapCoordStr(), indoors };
        }

        /// <summary>True when the landblock has buildings on the overworld, the same test Landblock.IsDungeon uses.</summary>
        private static bool IsBuildingLandblock(uint landblock)
        {
            var info = DatManager.CellDat.ReadFromDat<LandblockInfo>(landblock << 16 | 0xFFFE);
            return info?.Buildings != null && info.Buildings.Count > 0;
        }

        private static object ShopDto(uint vendorWcid)
        {
            var vendor = DatabaseManager.World.GetCachedWeenie(vendorWcid);
            if (vendor?.PropertiesCreateList == null)
                return null;

            var sellPrice = vendor.GetProperty(PropertyFloat.SellPrice);

            // A vendor with an AlternateCurrency charges its prices in that item (Schneebs: MMD notes,
            // the Banderling: Savage Echo), exactly as Vendor.BuyItems_ValidateTransaction counts it.
            // Null = pyreals.
            var currencyWcid = vendor.GetProperty(PropertyDataId.AlternateCurrency);
            var currency = currencyWcid.HasValue && currencyWcid.Value != 0 ? ItemDto(currencyWcid.Value) : null;

            var stock = vendor.PropertiesCreateList
                .Where(c => c.DestinationType == DestinationType.Shop)
                .Select(c => DatabaseManager.World.GetCachedWeenie(c.WeenieClassId))
                .Where(w => w != null)
                .Select(w => new
                {
                    wcid = w.WeenieClassId,
                    name = w.GetProperty(PropertyString.Name),
                    icon = IconUrl(w),
                    description = w.GetProperty(PropertyString.Use) ?? w.GetProperty(PropertyString.ShortDesc),
                    price = Vendor.SellCostFor(sellPrice, w.GetValue(), w.GetItemType()),
                })
                .ToList();

            return new { currency, stock };
        }

        /// <summary>How many of an item an NPC's script hands over (e.g. Mrs. Ruggan's starter lenses).</summary>
        private static int? GiveStackSize(uint npcWcid, uint itemWcid)
        {
            var emotes = DatabaseManager.World.GetCachedWeenie(npcWcid)?.PropertiesEmote;
            var give = emotes?.SelectMany(e => e.PropertiesEmoteAction)
                .FirstOrDefault(a => a.Type == (uint)EmoteType.Give && a.WeenieClassId == itemWcid);
            return give == null ? null : give.StackSize ?? 1;
        }

        /// <summary>
        /// Flawed lenses the Arcanum Lens Collector wants for an Asheron's Lens. The player hands him one lens
        /// (the Give is accepted, so that lens is already gone) and his script then requires and takes the rest
        /// from the pack, so the total is the owned-items check plus the one handed over.
        /// </summary>
        private static int? LensCollectorFlawedCost()
        {
            var emotes = DatabaseManager.World.GetCachedWeenie(LensCollectorWcid)?.PropertiesEmote;
            var check = emotes?
                .Where(e => e.Category == EmoteCategory.Give && e.WeenieClassId == 78780001)
                .SelectMany(e => e.PropertiesEmoteAction)
                .FirstOrDefault(a => a.Type == (uint)EmoteType.InqOwnsItems && a.WeenieClassId == 78780001);
            return check?.StackSize == null ? null : check.StackSize.Value + 1;
        }

        /// <summary>
        /// Mastery statue rules, read from the statue script: the level gates, and each paid change's MMD and
        /// luminance price (the TakeItems and SpendLuminance pair in the same emote).
        /// </summary>
        private static object MasteryData()
        {
            var statue = DatabaseManager.World.GetCachedWeenie(MasteryStatueWcids[0]);
            var emotes = statue?.PropertiesEmote;
            if (emotes == null)
                return null;

            var levelGates = emotes.SelectMany(e => e.PropertiesEmoteAction)
                .Where(a => a.Type == (uint)EmoteType.InqIntStat && a.Stat == (int)PropertyInt.Level && a.Min.HasValue)
                .Select(a => a.Min.Value)
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            var paidChanges = emotes
                .Select(e => new
                {
                    mmd = e.PropertiesEmoteAction.FirstOrDefault(a => a.Type == (uint)EmoteType.TakeItems && a.WeenieClassId == MmdWcid)?.StackSize,
                    luminance = e.PropertiesEmoteAction.FirstOrDefault(a => a.Type == (uint)EmoteType.SpendLuminance)?.Amount64,
                })
                .Where(c => c.mmd.HasValue && c.luminance.HasValue)
                .Distinct()
                .OrderBy(c => c.luminance)
                .ToList();

            return new
            {
                firstChangeMinLevel = levelGates.Count > 0 ? levelGates[0] : (int?)null,
                paidChangeMinLevel = levelGates.Count > 1 ? levelGates[1] : (int?)null,
                paidChangeCooldownSeconds = DatabaseManager.World.GetCachedQuest(MasteryChangeQuest)?.MinDelta,
                paidChanges,
            };
        }

        /// <summary>
        /// The summoning essences each mastery can use, and what each tier asks of the summoner, read from the
        /// essence weenies (mastery, level, Summoning skill and luminance summon augmentations).
        /// </summary>
        private static object EssenceData()
        {
            var tiers = PetDeviceWcids.GetTierLevels();
            var masteries = new[] { SummoningMastery.Primalist, SummoningMastery.Necromancer, SummoningMastery.Naturalist };

            var tierRequirements = new List<object>();
            var sample = PetDeviceWcids.GetFamilies(SummoningMastery.Necromancer).FirstOrDefault();
            for (var i = 0; sample != null && i < tiers.Count && i < sample.Count; i++)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie((uint)sample[i]);
                tierRequirements.Add(new
                {
                    tier = tiers[i],
                    level = weenie?.GetProperty(PropertyInt.UseRequiresLevel),
                    summoningSkill = weenie?.GetProperty(PropertyInt.UseRequiresSkillLevel),
                    // WorldObject.CheckUseRequirements refuses these unless the skill is specialized.
                    summoningSpecialized = weenie?.GetProperty(PropertyInt.UseRequiresSkillSpec) != null,
                    summonAugs = weenie?.GetProperty(PropertyInt.PetDeviceMinLumAugSummonCount),
                });
            }

            return new
            {
                tiers,
                tierRequirements,
                masteries = masteries.Select(m => new
                {
                    mastery = m.ToString(),
                    // Each family's lowest-tier essence stands in for the family on the page.
                    families = PetDeviceWcids.GetFamilies(m)
                        .Where(f => f.Count > 0)
                        .Select(f => ItemDto((uint)f[0]))
                        .Where(d => d != null)
                        .ToList(),
                }).ToList(),
            };
        }
    }
}
