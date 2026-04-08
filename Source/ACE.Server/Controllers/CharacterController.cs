using ACE.Database;
using ACE.Database.Adapter;
using ACE.Database.Models.Shard;
using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Web.Controllers;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Biota = ACE.Entity.Models.Biota;

namespace ACE.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CharacterController : BaseController
    {
        /*
         *  ARCHITECTURAL NOTE: AUTHORITATIVE ATOMIC PLUCKING
         *  -------------------------------------------------
         *  To ensure thread-safety and high performance without the overhead of deep clones,
         *  we use an "Authoritative Atomic Plucking" pattern for character data retrieval.
         *  
         *  1. Discrete Locking: We enter the character's BiotaDatabaseLock briefly to pluck 
         *     raw property values or internal dictionary references.
         *  2. Pluck and Release: The lock is released immediately after extraction.
         *  3. Authoritative Engines: Calculations (Attributes, Skills, Vitals) that call 
         *     into authoritative server engines (and acquire their own locks) are performed 
         *     OUTSIDE the controller-level lock to prevent LockRecursionException.
         */

        private class StatValueDto
        {
            public uint Innate { get; set; } = 0;
            public uint Ranks { get; set; } = 0;
            public uint Total { get; set; } = 0;
        }

        private class SkillValueDto
        {
            public string Name { get; set; } = "";
            public string Sac { get; set; } = "";
            public bool isUsable { get; set; } = true;
            public uint Base { get; set; } = 0;
            public uint Total { get; set; } = 0;
        }

        private class CharacterStatsDto
        {
            public int Level { get; set; }
            public int Enlightenment { get; set; }
            public StatAttributesDto Attributes { get; set; } = new();
            public StatVitalsDto Vitals { get; set; } = new();
            public StatRatingsDto Ratings { get; set; } = new();
            public Dictionary<string, long> Augmentations { get; set; } = new();
            public Dictionary<string, long> Bank { get; set; } = new();
        }

        private class StatAttributesDto
        {
            public StatValueDto Strength { get; set; } = new();
            public StatValueDto Endurance { get; set; } = new();
            public StatValueDto Quickness { get; set; } = new();
            public StatValueDto Coordination { get; set; } = new();
            public StatValueDto Focus { get; set; } = new();
            public StatValueDto Self { get; set; } = new();
        }

        private class StatVitalsDto
        {
            public StatValueDto Health { get; set; } = new();
            public StatValueDto Stamina { get; set; } = new();
            public StatValueDto Mana { get; set; } = new();
        }

        private class StatRatingsDto
        {
            public uint? Emd { get; set; }
            public int? Damage { get; set; }
            public int? CritDamage { get; set; }
            public int? Dr { get; set; }
            public int? Cdr { get; set; }
        }

        private class InventoryItemDto
        {
            public uint Guid { get; set; }
            public uint ContainerGuid { get; set; }
            public uint WeenieType { get; set; }
            public bool RequiresBackpackSlot { get; set; }
            public bool IsContainer { get; set; }
            public string Name { get; set; }
            public int Wcid { get; set; }
            public int StackSize { get; set; }
            public bool IsEquipped { get; set; }
            public uint IconId { get; set; }
            public uint IconUnderlayId { get; set; }
            public uint IconOverlayId { get; set; }
            public uint IconOverlaySecondaryId { get; set; }
            public uint ItemType { get; set; }
            public uint UiEffects { get; set; }
        }

        [HttpGet("list")]
        public IActionResult GetList()
        {
            var accountId = CurrentAccountId;
            if (accountId == null) return Unauthorized();

            var characters = DatabaseManager.Shard.BaseDatabase.GetCharacters(accountId.Value, false);
            var result = characters.Select(c => {
                var isAdmin = c.IsPlussed;
                var charName = isAdmin && !c.Name.StartsWith("+") ? $"+{c.Name}" : c.Name;
                return new
                {
                    guid = c.Id,
                    name = charName,
                    isOnline = PlayerManager.GetOnlinePlayer(c.Id) != null,
                    isAdmin
                };
            });

            return Ok(result);
        }

        [HttpGet("all-online")]
        public IActionResult GetAllOnline()
        {
            if (!IsAdmin)
                return Unauthorized();

            var online = PlayerManager.GetAllOnline();
            var result = online.Select(p => {
                var isAdmin = p.Account.AccessLevel > 0;
                var charName = isAdmin && !p.Name.StartsWith("+") ? $"+{p.Name}" : p.Name;
                return new
                {
                    guid = p.Guid.Full,
                    name = charName,
                    isOnline = true,
                    isAdmin
                };
            });

            return Ok(result.OrderBy(r => r.name));
        }


        [HttpGet("search-all/{name}")]
        public IActionResult SearchAll(string name)
        {
            if (!IsAdmin)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(name))
                return Ok(new List<object>());

            // SQL-level search with ShardDatabase
            var stubs = DatabaseManager.Shard.BaseDatabase.GetCharacterStubsByPartialName(name, 100);
            
            var result = stubs.Select(s =>
            {
                var player = PlayerManager.FindByGuid(s.Id);
                var isAdmin = player.Account.AccessLevel > 0;
                var charName = isAdmin && !s.Name.StartsWith("+") ? $"+{s.Name}" : s.Name;

                return new
                {
                    guid = s.Id,
                    name = charName,
                    isOnline = PlayerManager.GetOnlinePlayer(s.Id) != null,
                    isAdmin
                };
            });

            return Ok(result);
        }

        [HttpGet("detail/{guid}")]
        public IActionResult GetDetail(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out IPlayer player)) return Unauthorized();

            var isAdmin = player.Account.AccessLevel > 0;
            var charName = isAdmin && !player.Name.StartsWith("+") ? $"+{player.Name}" : player.Name;

            return Ok(new
            {
                guid,
                name = charName,
                isOnline = PlayerManager.GetOnlinePlayer(guid) != null,
                isAdmin
            });
        }

        [HttpGet("stats/{guid}")]
        public IActionResult GetStats(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out IPlayer player)) return Unauthorized();

            if (player is Player online)
                return Ok(GetOnlineStats(online));
            else
                return Ok(GetOfflineStats(RetrieveBiota(player)));
        }

        [HttpGet("skills/{guid}")]
        public IActionResult GetSkills(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out IPlayer player)) return Unauthorized();

            if (player is Player online)
                return Ok(GetOnlineSkills(online));
            else
                return Ok(GetOfflineSkills(RetrieveBiota(player)));
        }

        [HttpGet("inventory/{guid}")]
        public IActionResult GetInventory(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out IPlayer player)) return Unauthorized();

            if (player is Player online)
                return Ok(GetOnlineInventory(online));
            else
                return Ok(GetOfflineInventory(guid));
        }
        
        [HttpGet("stamps/{guid}")]
        public IActionResult GetStamps(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out IPlayer player)) return Unauthorized();

            var stamps = DatabaseManager.Authentication.GetAccountQuests(player.Account.AccountId)
                .Where(q => q.NumTimesCompleted >= 1)
                .Select(q => q.Quest)
                .Distinct()
                .OrderBy(q => q)
                .ToList();

            return Ok(stamps);
        }

        [HttpPost("logout/{guid}")]
        public async Task<IActionResult> ForceLogout(uint guid)
        {
            if (!IsAdmin)
                return Unauthorized();

            var player = PlayerManager.GetOnlinePlayer(guid);
            if (player == null) return NotFound(new { message = "Character is not online" });

            player.Session.LogOffPlayer(true);
            player.Session.Network.EnqueueSend(new GameMessageBootAccount(" because the character was forcefully logged out."));
            return Ok(new { message = "Administrative logout signal sent" });
        }

        private bool IsAuthorizedForCharacter(uint guid, out IPlayer player)
        {
            player = null;
            if (CurrentAccountId == null) return false;
            IPlayer p = PlayerManager.FindByGuid(guid);
            if (p == null) return false;
            if (!IsAuthorizedForAccount(p.Account.AccountId)) return false;
            player = p;
            return true;
        }

        private static Dictionary<string, long> GetBankedProperties(Biota biota)
        {
            return new Dictionary<string, long>
            {
                ["Pyreals"] = biota.GetProperty(PropertyInt64.BankedPyreals) ?? 0,
                ["Luminance"] = biota.GetProperty(PropertyInt64.BankedLuminance) ?? 0,
                ["EnlightenedCoins"] = biota.GetProperty(PropertyInt64.BankedEnlightenedCoins) ?? 0,
                ["WeaklyEnlightenedCoins"] = biota.GetProperty(PropertyInt64.BankedWeaklyEnlightenedCoins) ?? 0,
                ["LegendaryKeys"] = biota.GetProperty(PropertyInt64.BankedLegendaryKeys) ?? 0,
                ["MythicalKeys"] = biota.GetProperty(PropertyInt64.BankedMythicalKeys) ?? 0
            };
        }

        private static Dictionary<string, long> GetAugmentations(Biota biota)
        {
            return new Dictionary<string, long>
            {
                ["Creature"] = biota.GetProperty(PropertyInt64.LumAugCreatureCount) ?? 0,
                ["Item"] = biota.GetProperty(PropertyInt64.LumAugItemCount) ?? 0,
                ["Life"] = biota.GetProperty(PropertyInt64.LumAugLifeCount) ?? 0,
                ["War"] = biota.GetProperty(PropertyInt64.LumAugWarCount) ?? 0,
                ["Void"] = biota.GetProperty(PropertyInt64.LumAugVoidCount) ?? 0,
                ["Duration"] = biota.GetProperty(PropertyInt64.LumAugDurationCount) ?? 0,
                ["Specialize"] = biota.GetProperty(PropertyInt64.LumAugSpecializeCount) ?? 0,
                ["Summon"] = biota.GetProperty(PropertyInt64.LumAugSummonCount) ?? 0,
                ["Melee"] = biota.GetProperty(PropertyInt64.LumAugMeleeCount) ?? 0,
                ["Missile"] = biota.GetProperty(PropertyInt64.LumAugMissileCount) ?? 0
            };
        }

        private static Biota RetrieveBiota(IPlayer player)
        {
            if (player is Player online)
                return online.Biota.Clone(online.BiotaDatabaseLock);
            
            if (player is OfflinePlayer offline)
                return offline.Biota.Clone(offline.BiotaDatabaseLock);

            throw new InvalidOperationException("Unknown player type");
        }

        private static StatValueDto GetOnlineAttribute(Player player, PropertyAttribute attr)
        {
            if (!player.Attributes.TryGetValue(attr, out CreatureAttribute creatureAttribute)) return new();
            return new StatValueDto
            {
                Innate = creatureAttribute.StartingValue,
                Ranks = creatureAttribute.Ranks,
                Total = creatureAttribute.Current
            };
        }

        private static StatValueDto GetOfflineAttribute(Biota biota, PropertyAttribute attr)
        {
            if (biota.PropertiesAttribute == null) return new();
            if (!biota.PropertiesAttribute.TryGetValue(attr, out PropertiesAttribute prop)) return new();
            return new StatValueDto
            {
                Innate = prop.InitLevel,
                Ranks = prop.LevelFromCP,
                Total = prop.InitLevel + prop.LevelFromCP
            };
        }

        private static StatValueDto GetOnlineVital(Player player, PropertyAttribute2nd vital)
        {
            if (!player.Vitals.TryGetValue(vital, out CreatureVital creatureVital)) return new();
            uint removeFromBase = creatureVital.Ranks + creatureVital.GearBonus;
            return new StatValueDto
            {
                // Base includes Ranks (which is its own field) and GearBonus (not innate)
                Innate = removeFromBase > creatureVital.Base ? 0 : creatureVital.Base - removeFromBase,
                Ranks = creatureVital.Ranks,
                Total = creatureVital.MaxValue
            };
        }

        private static StatValueDto GetOfflineVital(Biota biota, PropertyAttribute2nd vital)
        {
            StatValueDto statValue = new();
            if (biota.PropertiesAttribute2nd == null) return statValue;
            if (!biota.PropertiesAttribute2nd.TryGetValue(vital, out PropertiesAttribute2nd prop)) return statValue;

            // Innate
            statValue.Innate =
                prop.InitLevel
                + AttributeFormula.GetFormula(biota, vital)
                + ((vital == PropertyAttribute2nd.MaxHealth) ? ((uint)(biota.GetProperty(PropertyInt.Enlightenment) ?? 0) * 2) : 0);

            // Ranks
            statValue.Ranks = prop.LevelFromCP;

            // Total
            statValue.Total =
                statValue.Innate
                + statValue.Ranks
                + ((vital == PropertyAttribute2nd.MaxHealth) ? ((uint)(biota.GetProperty(PropertyInt.GearMaxHealth) ?? 0)) : 0);

            return statValue;
        }

        private static CharacterStatsDto GetOnlineStats(Player online)
        {
            var stats = new CharacterStatsDto();

            // DISCRETE PLUCKING:
            // We use a brief lock for raw property extraction, but release it before
            // calling authoritative engine methods (Attributes, Vitals, Ratings) 
            // to avoid LockRecursionException in ACE's non-recursive locks.
            online.BiotaDatabaseLock.EnterReadLock();
            try
            {
                stats.Level = online.Level ?? 0;
                stats.Enlightenment = online.GetProperty(PropertyInt.Enlightenment) ?? 0;
                stats.Bank = GetBankedProperties(online.Biota);
                stats.Augmentations = GetAugmentations(online.Biota);
            }
            finally { online.BiotaDatabaseLock.ExitReadLock(); }

            // Authoritative engine calls - MUST be outside the lock
            stats.Attributes.Strength = GetOnlineAttribute(online, PropertyAttribute.Strength);
            stats.Attributes.Endurance = GetOnlineAttribute(online, PropertyAttribute.Endurance);
            stats.Attributes.Quickness = GetOnlineAttribute(online, PropertyAttribute.Quickness);
            stats.Attributes.Coordination = GetOnlineAttribute(online, PropertyAttribute.Coordination);
            stats.Attributes.Focus = GetOnlineAttribute(online, PropertyAttribute.Focus);
            stats.Attributes.Self = GetOnlineAttribute(online, PropertyAttribute.Self);
            
            stats.Vitals.Health = GetOnlineVital(online, PropertyAttribute2nd.MaxHealth);
            stats.Vitals.Stamina = GetOnlineVital(online, PropertyAttribute2nd.MaxStamina);
            stats.Vitals.Mana = GetOnlineVital(online, PropertyAttribute2nd.MaxMana);

            stats.Ratings.Emd = online.GetEffectiveDefenseSkill(CombatType.Melee);
            stats.Ratings.Damage = online.GetDamageRating();
            stats.Ratings.CritDamage = online.GetCritDamageRating();
            stats.Ratings.Dr = online.GetDamageResistRating();
            stats.Ratings.Cdr = online.GetCritDamageResistRating();

            return stats;
        }

        private static CharacterStatsDto GetOfflineStats(Biota biota)
        {
            var stats = new CharacterStatsDto();
            stats.Level = biota.GetProperty(PropertyInt.Level) ?? 0;
            stats.Enlightenment = biota.GetProperty(PropertyInt.Enlightenment) ?? 0;
            stats.Attributes.Strength = GetOfflineAttribute(biota, PropertyAttribute.Strength);
            stats.Attributes.Endurance = GetOfflineAttribute(biota, PropertyAttribute.Endurance);
            stats.Attributes.Quickness = GetOfflineAttribute(biota, PropertyAttribute.Quickness);
            stats.Attributes.Coordination = GetOfflineAttribute(biota, PropertyAttribute.Coordination);
            stats.Attributes.Focus = GetOfflineAttribute(biota, PropertyAttribute.Focus);
            stats.Attributes.Self = GetOfflineAttribute(biota, PropertyAttribute.Self);
            
            stats.Vitals.Health = GetOfflineVital(biota, PropertyAttribute2nd.MaxHealth);
            stats.Vitals.Stamina = GetOfflineVital(biota, PropertyAttribute2nd.MaxStamina);
            stats.Vitals.Mana = GetOfflineVital(biota, PropertyAttribute2nd.MaxMana);

            stats.Bank = GetBankedProperties(biota);
            stats.Augmentations = GetAugmentations(biota);

            return stats;
        }

        private static List<SkillValueDto> GetOnlineSkills(Player online)
        {
            var result = new List<SkillValueDto>();
            var pluckedSkills = new List<CreatureSkill>();

            // DISCRETE PLUCKING:
            // We use a brief lock for dictionary safety (to get references to the skill objects), 
            // but we MUST release the lock before calling .Base or .Current.
            // Those properties call into the EnchantmentManager, which attempts to acquire
            // the same BiotaDatabaseLock. Since ACE locks are non-recursive, holding the lock
            // here would cause a LockRecursionException and crash the thread.
            online.BiotaDatabaseLock.EnterReadLock();
            try
            {
                foreach (var skill in SkillHelper.ValidSkills)
                {
                    if (online.Skills.TryGetValue(skill, out var creatureSkill))
                        pluckedSkills.Add(creatureSkill);
                }
            }
            finally { online.BiotaDatabaseLock.ExitReadLock(); }

            foreach (var creatureSkill in pluckedSkills)
            {
                result.Add(new SkillValueDto
                {
                    Name = creatureSkill.Skill.ToSentence(),
                    Sac = creatureSkill.AdvancementClass.ToString(),
                    isUsable = creatureSkill.IsUsable,
                    Base = creatureSkill.Base,
                    Total = creatureSkill.Current
                });
            }
            return result;
        }

        private static List<SkillValueDto> GetOfflineSkills(Biota biota)
        {
            var result = new List<SkillValueDto>();
            if (biota.PropertiesSkill == null) return result;

            foreach (var skill in SkillHelper.ValidSkills)
            {
                if (!biota.PropertiesSkill.TryGetValue(skill, out PropertiesSkill prop))
                    continue;

                SkillValueDto skillValue = new()
                {
                    Name = skill.ToSentence(),
                    Sac = prop.SAC.ToString(),
                    isUsable = true,
                };

                if (prop.SAC == SkillAdvancementClass.Untrained)
                {
                    DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skill, out SkillBase skillTableRecord);
                    if (skillTableRecord?.MinLevel != 1) skillValue.isUsable = false;
                }

                uint fromAttributes = skillValue.isUsable ? AttributeFormula.GetFormula(biota, skill) : 0;
                uint enlBonus = (prop.SAC >= SkillAdvancementClass.Trained ? (uint)(biota.GetProperty(PropertyInt.Enlightenment) ?? 0) : 0);
                uint augBaseBonus =
                    (uint)(biota.GetProperty(PropertyInt.LumAugAllSkills) ?? 0)
                    + (Player.MeleeSkills.Contains(skill) ? (uint)(10 * (biota.GetProperty(PropertyInt.AugmentationSkilledMelee) ?? 0)) : 0)
                    + (Player.MissileSkills.Contains(skill) ? (uint)(10 * (biota.GetProperty(PropertyInt.AugmentationSkilledMissile) ?? 0)) : 0)
                    + (Player.MagicSkills.Contains(skill) ? (uint)(10 * (biota.GetProperty(PropertyInt.AugmentationSkilledMagic) ?? 0)) : 0);
                uint augTotalBonus =
                    (uint)(5 * (biota.GetProperty(PropertyInt.AugmentationJackOfAllTrades) ?? 0))
                    + (prop.SAC >= SkillAdvancementClass.Specialized ? (uint)(2 * (biota.GetProperty(PropertyInt.LumAugSkilledSpec) ?? 0)) : 0);

                skillValue.Base = prop.InitLevel + fromAttributes + prop.LevelFromPP + augBaseBonus + enlBonus;
                skillValue.Total = skillValue.Base + augTotalBonus;

                result.Add(skillValue);
            }

            return result;
        }

        private static List<InventoryItemDto> GetOnlineInventory(Player online)
        {
            var result = new List<InventoryItemDto>();
            online.BiotaDatabaseLock.EnterReadLock();
            try
            {
                var equippedIds = new HashSet<uint>(online.EquippedObjects.Keys.Select(g => g.Full));
                CollectInventoryRecursive(online, result, equippedIds);
            }
            finally { online.BiotaDatabaseLock.ExitReadLock(); }

            return result.OrderBy(i => i.Name).ToList();
        }

        private static List<InventoryItemDto> GetOfflineInventory(uint guid)
        {
            var result = new List<InventoryItemDto>();
            var possessedBiotas = DatabaseManager.Shard.BaseDatabase.GetPossessedBiotasInParallel(guid);
            var equippedIds = new HashSet<uint>(possessedBiotas.WieldedItems.Select(w => w.Id));

            foreach (var dbBiota in possessedBiotas.Inventory.Concat(possessedBiotas.WieldedItems))
            {
                var biota = BiotaConverter.ConvertToEntityBiota(dbBiota);
                result.Add(MapToDto(biota, equippedIds.Contains(biota.Id)));
            }

            return result.OrderBy(i => i.Name).ToList();
        }

        private static InventoryItemDto MapToDto(Biota biota, bool isEquipped, uint? containerGuid = null)
        {
            return new InventoryItemDto
            {
                Guid = biota.Id,
                ContainerGuid = containerGuid ?? biota.GetProperty(PropertyInstanceId.Container) ?? 0,
                WeenieType = (uint)biota.WeenieType,
                RequiresBackpackSlot = biota.RequiresBackpackSlotOrIsContainer(),
                IsContainer = biota.WeenieType == WeenieType.Container,
                Name = biota.GetProperty(PropertyString.Name) ?? "Unknown Item",
                Wcid = (int)biota.WeenieClassId,
                StackSize = biota.GetProperty(PropertyInt.StackSize) ?? 1,
                IsEquipped = isEquipped,
                IconId = biota.GetProperty(PropertyDataId.Icon) ?? 0,
                IconUnderlayId = biota.GetProperty(PropertyDataId.IconUnderlay) ?? 0,
                IconOverlayId = biota.GetProperty(PropertyDataId.IconOverlay) ?? 0,
                IconOverlaySecondaryId = biota.GetProperty(PropertyDataId.IconOverlaySecondary) ?? 0,
                ItemType = (uint)biota.GetItemType(),
                UiEffects = (uint)(biota.GetProperty(PropertyInt.UiEffects) ?? 0)
            };
        }

        private static void CollectInventoryRecursive(Container container, List<InventoryItemDto> items, HashSet<uint> equippedIds)
        {
            foreach (var wo in container.Inventory.Values)
            {
                items.Add(MapToDto(wo.Biota, equippedIds.Contains(wo.Guid.Full), (uint)container.Guid.Full));

                if (wo is Container subContainer)
                    CollectInventoryRecursive(subContainer, items, equippedIds);
            }

            if (container is Creature creature)
            {
                foreach (var eq in creature.EquippedObjects.Values)
                    items.Add(MapToDto(eq.Biota, true, (uint)container.Guid.Full));
            }
        }
    }
}
