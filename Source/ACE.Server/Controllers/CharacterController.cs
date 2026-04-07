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
         *  ARCHITECTURAL NOTE: THE SNAPSHOT PATTERN
         *  ----------------------------------------
         *  To ensure thread-safety and high performance for the Web Portal, we use a 
         *  "Captured Snapshot" pattern for character data retrieval.
         *  
         *  1. GetBiotaSnapshot(player): Enters the character's BiotaDatabaseLock once,
         *     performs a deep clone of the Biota properties, and exits.
         *  2. JSON Generation: The rest of the request works on this private 'clone'.
         *  3. Lock-Free Logic: Because the clone is isolated, we use the lock-free 
         *     IWeenieExtensions for all lookups, avoiding complex lock recursion 
         *     and maximizing concurrency.
         */

        private class StatValue
        {
            public uint Innate { get; set; } = 0;
            public uint Ranks { get; set; } = 0;
            public uint Total { get; set; } = 0;
        }
        private class SkillValue
        {
            public string Name { get; set; } = "";
            public string Sac { get; set; } = "";
            public bool isUsable { get; set; } = true;
            public uint Base { get; set; } = 0;
            public uint Total { get; set; } = 0;
        };

        [HttpGet("list")]
        public IActionResult GetList()
        {
            var accountId = CurrentAccountId;
            if (accountId == null)
                return Unauthorized();

            var account = DatabaseManager.Authentication.GetAccountById(accountId.Value);
            var isAdmin = account?.AccessLevel > 0;

            var characters = DatabaseManager.Shard.BaseDatabase.GetCharacters(accountId.Value, false);
            var result = characters.Select(c => {
                var iPlayer = PlayerManager.FindByGuid(c.Id);
                var charName = isAdmin && !c.Name.StartsWith("+") ? $"+{c.Name}" : c.Name;

                return new
                {
                    guid = c.Id,
                    name = charName,
                    level = iPlayer?.Level ?? 0,
                    enlightenment = iPlayer?.GetProperty(PropertyInt.Enlightenment) ?? 0,
                    isOnline = PlayerManager.GetOnlinePlayer(c.Id) != null,
                    isAdmin = isAdmin
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
                    level = p.Level ?? 0,
                    enlightenment = p.GetProperty(PropertyInt.Enlightenment) ?? 0,
                    isAdmin = isAdmin
                };
            });

            return Ok(result.OrderBy(r => r.name));
        }

        [HttpGet("search-offline/{name}")]
        public IActionResult SearchOffline(string name)
        {
            if (!IsAdmin)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(name))
                return Ok(new List<object>());

            // Search only characters who are not currently online
            var stubs = DatabaseManager.Shard.BaseDatabase.GetCharacterStubsByPartialName(name)
                .Where(s => PlayerManager.GetOnlinePlayer(s.Id) == null)
                .Select(s => {
                    var iPlayer = PlayerManager.FindByGuid(s.Id);
                    var accessLevel = 0;
                    if (iPlayer is Player online) accessLevel = (int)online.Account.AccessLevel;
                    else if (iPlayer is OfflinePlayer offline) accessLevel = (int)offline.Account.AccessLevel;

                    var isAdminChar = accessLevel > 0;
                    var charName = isAdminChar && !s.Name.StartsWith("+") ? $"+{s.Name}" : s.Name;

                    return new
                    {
                        guid = s.Id,
                        name = charName,
                        level = iPlayer?.Level ?? 0,
                        enlightenment = iPlayer?.GetProperty(PropertyInt.Enlightenment) ?? 0,
                        isOnline = false,
                        isAdmin = isAdminChar
                    };
                });

            return Ok(stubs);
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
                var isOnline = PlayerManager.GetOnlinePlayer(s.Id) != null;
                var player = PlayerManager.FindByGuid(s.Id);
                var accessLevel = 0;
                if (player is Player online) accessLevel = (int)online.Account.AccessLevel;
                else if (player is OfflinePlayer offline) accessLevel = (int)offline.Account.AccessLevel;

                var isAdminChar = accessLevel > 0;
                var charName = isAdminChar && !s.Name.StartsWith("+") ? $"+{s.Name}" : s.Name;

                return new
                {
                    guid = s.Id,
                    name = charName,
                    level = player?.Level ?? 0,
                    enlightenment = player?.GetProperty(PropertyInt.Enlightenment) ?? 0,
                    isOnline,
                    isAdmin = isAdminChar
                };
            });

            return Ok(result);
        }

        [HttpGet("detail/{guid}")]
        public IActionResult GetDetail(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out var player))
                return Unauthorized();

            var snapshot = GetBiotaSnapshot(player);
            var level = snapshot.GetProperty(PropertyInt.Level) ?? 0;
            var enlightenment = snapshot.GetProperty(PropertyInt.Enlightenment) ?? 0;
            
            var accessLevel = 0;
            if (player is Player pOnline) accessLevel = (int)pOnline.Account.AccessLevel;
            else if (player is OfflinePlayer pOffline) accessLevel = (int)pOffline.Account.AccessLevel;
            var isAdminChar = accessLevel > 0;
            var charName = isAdminChar && !player.Name.StartsWith("+") ? $"+{player.Name}" : player.Name;

            return Ok(new
            {
                guid,
                name = charName,
                level,
                enlightenment,
                augmentations = GetAugmentations(snapshot),
                isOnline = PlayerManager.GetOnlinePlayer(guid) != null,
                isAdmin = isAdminChar
            });
        }

        [HttpGet("stats/{guid}")]
        public IActionResult GetStats(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out IPlayer player))
                return Unauthorized();

            var snapshot = GetBiotaSnapshot(player);
            Player online = player as Player;

            return Ok(new
            {
                level = snapshot.GetProperty(PropertyInt.Level) ?? 0,
                enlightenment = snapshot.GetProperty(PropertyInt.Enlightenment) ?? 0,
                attributes = new
                {
                    strength = GetAttribute(player, snapshot, PropertyAttribute.Strength),
                    endurance = GetAttribute(player, snapshot, PropertyAttribute.Endurance),
                    quickness = GetAttribute(player, snapshot, PropertyAttribute.Quickness),
                    coordination = GetAttribute(player, snapshot, PropertyAttribute.Coordination),
                    focus = GetAttribute(player, snapshot, PropertyAttribute.Focus),
                    self = GetAttribute(player, snapshot, PropertyAttribute.Self)
                },
                vitals = new
                {
                    health = GetVital(player, snapshot, PropertyAttribute2nd.MaxHealth),
                    stamina = GetVital(player, snapshot, PropertyAttribute2nd.MaxStamina),
                    mana = GetVital(player, snapshot, PropertyAttribute2nd.MaxMana)
                },
                ratings = new
                {
                    emd = online != null ? online.GetEffectiveDefenseSkill(CombatType.Melee) : (uint?)null,
                    damage = online != null ? online.GetDamageRating() : (int?)null,
                    critDamage = online != null ? online.GetCritDamageRating() : (int?)null,
                    dr = online != null ? online.GetDamageResistRating() : (int?)null,
                    cdr = online != null ? online.GetCritDamageResistRating() : (int?)null,
                },
                augmentations = GetAugmentations(snapshot),
                bank = GetBankedProperties(snapshot)
            });
        }

        [HttpGet("skills/{guid}")]
        public IActionResult GetSkills(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out var player))
                return Unauthorized();

            var snapshot = GetBiotaSnapshot(player);
            var validSkills = SkillHelper.ValidSkills.OrderBy(s => s.ToString());

            var result = new List<object>();
            foreach (Skill skill in validSkills) result.Add(GetSkill(player, snapshot, skill));
            return Ok(result);
        }

        [HttpGet("inventory/{guid}")]
        public IActionResult GetInventory(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out var player))
                return Unauthorized();

            var inventoryItems = new List<Biota>();
            var onlinePlayer = PlayerManager.GetOnlinePlayer(guid);

            HashSet<uint> equippedIds;

            if (onlinePlayer != null)
            {
                // Memory-First: Get live inventory
                onlinePlayer.BiotaDatabaseLock.EnterReadLock();
                try
                {
                    CollectInventoryRecursive(onlinePlayer, inventoryItems);
                }
                finally { onlinePlayer.BiotaDatabaseLock.ExitReadLock(); }

                equippedIds = new HashSet<uint>(onlinePlayer.EquippedObjects.Keys.Select(g => g.Full));
            }
            else
            {
                // Offline: Fallback to Shard database
                var possessedBiotas = DatabaseManager.Shard.BaseDatabase.GetPossessedBiotasInParallel(guid);
                foreach (var dbBiom in possessedBiotas.Inventory.Concat(possessedBiotas.WieldedItems))
                    inventoryItems.Add(BiotaConverter.ConvertToEntityBiota(dbBiom));

                equippedIds = new HashSet<uint>(possessedBiotas.WieldedItems.Select(w => w.Id));
            }

            var result = inventoryItems.Select(biom => new
            {
                guid = biom.Id,
                containerGuid = biom.GetProperty(PropertyInstanceId.Container) ?? 0,
                weenieType = (uint)biom.WeenieType,
                requiresBackpackSlot = biom.RequiresBackpackSlotOrIsContainer(),
                isContainer = biom.WeenieType == WeenieType.Container,
                name = biom.GetProperty(PropertyString.Name) ?? "Unknown Item",
                wcid = biom.WeenieClassId,
                stackSize = biom.GetProperty(PropertyInt.StackSize) ?? 1,
                isEquipped = equippedIds.Contains(biom.Id),
                iconId = biom.GetProperty(PropertyDataId.Icon) ?? 0,
                iconUnderlayId = biom.GetProperty(PropertyDataId.IconUnderlay) ?? 0,
                iconOverlayId = biom.GetProperty(PropertyDataId.IconOverlay) ?? 0,
                iconOverlaySecondaryId = biom.GetProperty(PropertyDataId.IconOverlaySecondary) ?? 0,
                itemType = (uint)biom.GetItemType(),
                uiEffects = biom.GetProperty(PropertyInt.UiEffects) ?? 0
            });

            return Ok(result.OrderBy(i => i.name));
        }

        private void CollectInventoryRecursive(Container container, List<Biota> items)
        {
            // Add items in this container
            foreach (var wo in container.Inventory.Values)
            {
                items.Add(wo.Biota.Clone()); // Isolated clone for snapshot

                if (wo is Container subContainer)
                    CollectInventoryRecursive(subContainer, items);
            }

            // Also add equipped items if this is a creature/player
            if (container is Creature creature)
            {
                foreach (var eq in creature.EquippedObjects.Values)
                    items.Add(eq.Biota.Clone());
            }
        }
        
        [HttpGet("stamps/{guid}")]
        public IActionResult GetStamps(uint guid)
        {
            if (!IsAuthorizedForCharacter(guid, out var player))
                return Unauthorized();

            uint accountId = 0;
            if (player is Player online)
                accountId = online.Session.AccountId;
            else if (player is OfflinePlayer offline)
                accountId = offline.Account.AccountId;

            var stamps = DatabaseManager.Authentication.GetAccountQuests(accountId)
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

            // Check if this character belongs to the account
            uint playerAccountId = 0;
            if (p is Player online)
                playerAccountId = online.Session.AccountId;
            else if (p is OfflinePlayer offline)
                playerAccountId = offline.Account.AccountId;

            if (!IsAuthorizedForAccount(playerAccountId)) return false;

            player = p;
            return true;
        }

        private Dictionary<string, long> GetBankedProperties(Biota snapshot)
        {
            return new Dictionary<string, long>
            {
                ["Pyreals"] = snapshot.GetProperty(PropertyInt64.BankedPyreals) ?? 0,
                ["Luminance"] = snapshot.GetProperty(PropertyInt64.BankedLuminance) ?? 0,
                ["EnlightenedCoins"] = snapshot.GetProperty(PropertyInt64.BankedEnlightenedCoins) ?? 0,
                ["WeaklyEnlightenedCoins"] = snapshot.GetProperty(PropertyInt64.BankedWeaklyEnlightenedCoins) ?? 0,
                ["LegendaryKeys"] = snapshot.GetProperty(PropertyInt64.BankedLegendaryKeys) ?? 0,
                ["MythicalKeys"] = snapshot.GetProperty(PropertyInt64.BankedMythicalKeys) ?? 0
            };
        }

        private Dictionary<string, long> GetAugmentations(Biota snapshot)
        {
            return new Dictionary<string, long>
            {
                ["Creature"] = snapshot.GetProperty(PropertyInt64.LumAugCreatureCount) ?? 0,
                ["Item"] = snapshot.GetProperty(PropertyInt64.LumAugItemCount) ?? 0,
                ["Life"] = snapshot.GetProperty(PropertyInt64.LumAugLifeCount) ?? 0,
                ["War"] = snapshot.GetProperty(PropertyInt64.LumAugWarCount) ?? 0,
                ["Void"] = snapshot.GetProperty(PropertyInt64.LumAugVoidCount) ?? 0,
                ["Duration"] = snapshot.GetProperty(PropertyInt64.LumAugDurationCount) ?? 0,
                ["Specialize"] = snapshot.GetProperty(PropertyInt64.LumAugSpecializeCount) ?? 0,
                ["Summon"] = snapshot.GetProperty(PropertyInt64.LumAugSummonCount) ?? 0,
                ["Melee"] = snapshot.GetProperty(PropertyInt64.LumAugMeleeCount) ?? 0,
                ["Missile"] = snapshot.GetProperty(PropertyInt64.LumAugMissileCount) ?? 0
            };
        }

        private Biota GetBiotaSnapshot(IPlayer player)
        {
            if (player is Player online)
                return online.Biota.Clone(online.BiotaDatabaseLock);
            
            if (player is OfflinePlayer offline)
                return offline.Biota.Clone(offline.BiotaDatabaseLock);

            throw new InvalidOperationException("Unknown player type");
        }

        private static StatValue GetAttribute(IPlayer player, Biota snapshot, PropertyAttribute attr)
        {
            if (player is Player online) return GetOnlineAttribute(online, attr);
            return GetOfflineAttribute(snapshot, attr);
        }

        private static StatValue GetOnlineAttribute(Player player, PropertyAttribute attr)
        {
            if (!player.Attributes.TryGetValue(attr, out CreatureAttribute creatureAttribute)) return new();
            return new StatValue
            {
                Innate = creatureAttribute.StartingValue,
                Ranks = creatureAttribute.Ranks,
                Total = creatureAttribute.Current
            };
        }

        private static StatValue GetOfflineAttribute(Biota snapshot, PropertyAttribute attr)
        {
            if (snapshot.PropertiesAttribute == null) return new();
            if (!snapshot.PropertiesAttribute.TryGetValue(attr, out PropertiesAttribute prop)) return new();
            return new StatValue
            {
                Innate = prop.InitLevel,
                Ranks = prop.LevelFromCP,
                Total = prop.InitLevel + prop.LevelFromCP
            };
        }

        private static StatValue GetVital(IPlayer player, Biota snapshot, PropertyAttribute2nd vital)
        {
            if (player is Player online) return GetOnlineVital(online, vital);
            return GetOfflineVital(snapshot, vital);
        }
        private static StatValue GetOnlineVital(Player player, PropertyAttribute2nd vital)
        {
            if (!player.Vitals.TryGetValue(vital, out CreatureVital creatureVital)) return new();
            return new StatValue
            {
                // Base includes Ranks (which is its own field) and GearBonus (not innate)
                Innate = creatureVital.Base - creatureVital.Ranks - creatureVital.GearBonus,
                Ranks = creatureVital.Ranks,
                Total = creatureVital.MaxValue
            };
        }
        private static StatValue GetOfflineVital(Biota snapshot, PropertyAttribute2nd vital)
        {
            StatValue statValue = new();
            if (snapshot.PropertiesAttribute2nd == null) return statValue;
            if (!snapshot.PropertiesAttribute2nd.TryGetValue(vital, out PropertiesAttribute2nd prop)) return statValue;

            // Innate
            statValue.Innate =
                prop.InitLevel
                + AttributeFormula.GetFormula(snapshot, vital)
                + ((vital == PropertyAttribute2nd.MaxHealth) ? ((uint)(snapshot.GetProperty(PropertyInt.Enlightenment) ?? 0) * 2) : 0);

            // Ranks
            statValue.Ranks = prop.LevelFromCP;

            // Total
            statValue.Total =
                statValue.Innate
                + statValue.Ranks
                + ((vital == PropertyAttribute2nd.MaxHealth) ? ((uint)(snapshot.GetProperty(PropertyInt.GearMaxHealth) ?? 0)) : 0);

            return statValue;
        }

        private static SkillValue GetSkill(IPlayer player, Biota snapshot, Skill skill)
        {
            if (player is Player online) return GetOnlineSkill(online, skill);
            return GetOfflineSkill(snapshot, skill);
        }
        private static SkillValue GetOnlineSkill(Player player, Skill skill)
        {
            if (!player.Skills.TryGetValue(skill, out CreatureSkill creatureSkill)) return new();
            return new SkillValue
            {
                Name = skill.ToSentence(),
                Sac = creatureSkill.AdvancementClass.ToString(),
                isUsable = creatureSkill.IsUsable,
                Base = creatureSkill.Base,
                Total = creatureSkill.Current
            };
        }
        private static SkillValue GetOfflineSkill(Biota snapshot, Skill skill)
        {
            SkillValue skillValue = new()
            {
                Name = skill.ToSentence(),
                Sac = SkillAdvancementClass.Untrained.ToString(),
            };
            if (snapshot.PropertiesSkill == null) return skillValue;
            if (!snapshot.PropertiesSkill.TryGetValue(skill, out PropertiesSkill prop)) return skillValue;

            skillValue.Sac = prop.SAC.ToString();
            skillValue.isUsable = true;

            if (prop.SAC == SkillAdvancementClass.Untrained)
            {
                DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skill, out SkillBase skillTableRecord);
                if (skillTableRecord?.MinLevel != 1) skillValue.isUsable = false;
            }

            uint fromAttributes = skillValue.isUsable ? AttributeFormula.GetFormula(snapshot, skill) : 0;
            uint enlBonus = (prop.SAC >= SkillAdvancementClass.Trained ? (uint)(snapshot.GetProperty(PropertyInt.Enlightenment) ?? 0) : 0);
            uint augBaseBonus =
                (uint)(snapshot.GetProperty(PropertyInt.LumAugAllSkills) ?? 0)
                + (Player.MeleeSkills.Contains(skill) ? (uint)(10 * (snapshot.GetProperty(PropertyInt.AugmentationSkilledMelee) ?? 0)) : 0)
                + (Player.MissileSkills.Contains(skill) ? (uint)(10 * (snapshot.GetProperty(PropertyInt.AugmentationSkilledMissile) ?? 0)) : 0)
                + (Player.MagicSkills.Contains(skill) ? (uint)(10 * (snapshot.GetProperty(PropertyInt.AugmentationSkilledMagic) ?? 0)) : 0);
            uint augTotalBonus =
                (uint)(5 * (snapshot.GetProperty(PropertyInt.AugmentationJackOfAllTrades) ?? 0))
                + (prop.SAC >= SkillAdvancementClass.Specialized ? (uint)(2 * (snapshot.GetProperty(PropertyInt.LumAugSkilledSpec) ?? 0)) : 0);

            skillValue.Base =
                prop.InitLevel +
                + fromAttributes
                + prop.LevelFromPP
                + augBaseBonus
                + enlBonus;

            skillValue.Total =
                skillValue.Base
                + augTotalBonus;

            return skillValue;
        }
    }
}
