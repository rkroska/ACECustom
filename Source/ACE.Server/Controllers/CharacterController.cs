using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Adapter;
using ACE.Database.Entity;
using ACE.Database.Models.Auth;
using ACE.Database.Models.Shard;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Web.Controllers;
using ACE.Server.WorldObjects;
using Google.Protobuf.WellKnownTypes;
using Lifestoned.DataModel.Gdle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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

        private struct StatValue
        {
            public uint Innate { get; set; }
            public uint Ranks { get; set; }
            public uint Total { get; set; }
        }

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
            if (!IsAuthorizedForCharacter(guid, out var player))
                return Unauthorized();

            var snapshot = GetBiotaSnapshot(player);

            return Ok(new
            {
                level = snapshot.GetProperty(PropertyInt.Level) ?? 0,
                enlightenment = snapshot.GetProperty(PropertyInt.Enlightenment) ?? 0,
                attributes = new
                {
                    strength = GetAttribute(snapshot, PropertyAttribute.Strength),
                    endurance = GetAttribute(snapshot, PropertyAttribute.Endurance),
                    quickness = GetAttribute(snapshot, PropertyAttribute.Quickness),
                    coordination = GetAttribute(snapshot, PropertyAttribute.Coordination),
                    focus = GetAttribute(snapshot, PropertyAttribute.Focus),
                    self = GetAttribute(snapshot, PropertyAttribute.Self)
                },
                vitals = new
                {
                    health = GetVital(snapshot, PropertyAttribute2nd.MaxHealth),
                    stamina = GetVital(snapshot, PropertyAttribute2nd.MaxStamina),
                    mana = GetVital(snapshot, PropertyAttribute2nd.MaxMana)
                },
                ratings = new
                {
                    emd = GetEMD(player), // EMD still needs authoritative engine check for online
                    dr = snapshot.GetProperty(PropertyInt.DamageResistRating) ?? 0,
                    cdr = snapshot.GetProperty(PropertyInt.CritDamageResistRating) ?? 0,
                    damage = snapshot.GetProperty(PropertyInt.DamageRating) ?? 0,
                    critDamage = snapshot.GetProperty(PropertyInt.CritDamageRating) ?? 0
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
            var result = new List<object>();
            var validSkills = SkillHelper.ValidSkills.OrderBy(s => s.ToString());

            foreach (var skillEnum in validSkills)
            {
                var skillName = skillEnum.ToSentence();
                var sac = SkillAdvancementClass.Untrained;
                var baseVal = 0;
                int? total = 0;
                var isUsable = false;

                if (snapshot.PropertiesSkill != null && snapshot.PropertiesSkill.TryGetValue(skillEnum, out var skillProp))
                {
                    sac = skillProp.SAC;
                    baseVal = (int)(skillProp.InitLevel + skillProp.LevelFromPP);
                    
                    if (sac == SkillAdvancementClass.Trained || sac == SkillAdvancementClass.Specialized)
                        isUsable = true;
                }
                
                // Calculate skill formula base
                if (DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skillEnum, out var skillTableRecord))
                {
                    baseVal += (int)AttributeFormula.GetFormula(snapshot, skillTableRecord.Formula);

                    if (sac == SkillAdvancementClass.Untrained)
                        isUsable = skillTableRecord.MinLevel == 1;
                }

                // If online, we can show the authoritative 'Current' value
                if (player is Player online && online.Skills.TryGetValue(skillEnum, out var creatureSkill))
                    total = (int)creatureSkill.Current;
                else
                    total = null;

                result.Add(new
                {
                    name = skillName,
                    sac = sac.ToString(),
                    @base = baseVal,
                    total = total,
                    isUsable = isUsable
                });
            }

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

        private double? GetEMD(IPlayer player)
        {
            if (player is Player online)
            {
                // Use the server's native defense skill calculation for online players
                // This includes weapon bonuses, enchantments, and stance modifiers
                return online.GetEffectiveDefenseSkill(CombatType.Melee);
            }
            
            // Effective Melee Defense requires authoritative engine calculation (weapon/shield buffs etc)
            // so we return null for offline players
            return null;
        }

        private bool IsAuthorizedForCharacter(uint guid, out IPlayer player)
        {
            player = null;
            if (CurrentAccountId == null) return false;

            var p = PlayerManager.FindByGuid(guid);
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

        private StatValue GetAttribute(Biota snapshot, PropertyAttribute attr)
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

        private static StatValue GetVital(Biota snapshot, PropertyAttribute2nd vital)
        {
            if (snapshot.PropertiesAttribute2nd == null) return new();
            if (!snapshot.PropertiesAttribute2nd.TryGetValue(vital, out PropertiesAttribute2nd prop)) return new();
            return new StatValue
            {
                Innate = AttributeFormula.GetFormula(snapshot, vital),
                Ranks = prop.LevelFromCP,
                Total = prop.CurrentLevel
            };

        }
    }
}
