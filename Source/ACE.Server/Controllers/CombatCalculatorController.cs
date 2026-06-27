using System;
using System.Collections.Generic;
using System.Linq;
using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Web.Controllers;
using ACE.Server.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Controllers
{
    [ApiController]
    [Route("api/combat")]
    public class CombatCalculatorController : BaseController
    {
        private static readonly int[] MagicCastSkillIds = { 34, 33, 43, 32, 31, 29 };
        private static readonly int[] MeleeAttackSkillIds = { 45, 44, 46, 2 };

        private static readonly Dictionary<int, string> SkillNames = new()
        {
            { 2, "Unarmed" },
            { 5, "Bow" },
            { 6, "MeleeDefense" },
            { 7, "MissileDefense" },
            { 15, "MagicDefense" },
            { 24, "Run" },
            { 33, "LifeMagic" },
            { 34, "WarMagic" },
            { 43, "VoidMagic" },
            { 44, "HeavyWeapons" },
            { 45, "LightWeapons" },
            { 46, "FinesseWeapons" },
            { 47, "MissileWeapons" },
        };

        private static int DefSkillIdForMode(string mode) => mode switch
        {
            "melee" => 6,
            "magic" => 15,
            _ => 7,
        };

        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            if (!HasPortalAccess(PortalPages.CombatCalculator)) return Forbid();

            return Ok(new CombatConfigDto
            {
                Melee = ScalingMode("melee"),
                Missile = ScalingMode("missile"),
                Magic = ScalingMode("magic"),
            });
        }

        [HttpGet("weenie/search")]
        public IActionResult SearchWeenies([FromQuery] string q, [FromQuery] int limit = 50)
        {
            if (!HasPortalAccess(PortalPages.CombatCalculator)) return Forbid();
            if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<WeenieSearchResultDto>());

            q = q.Trim();
            limit = Math.Clamp(limit, 1, 100);
            var results = new List<WeenieSearchResultDto>();

            if (uint.TryParse(q, out var wcid))
            {
                var byId = DatabaseManager.World.GetCachedWeenie(wcid);
                if (byId != null)
                {
                    results.Add(ToSearchResult(byId));
                }
            }

            using var context = new WorldDbContext();
            var nameQuery = context.Weenie
                .Where(w => w.Type == (int)WeenieType.Creature)
                .Join(
                    context.WeeniePropertiesString.Where(s => s.Type == (ushort)PropertyString.Name && s.Value.Contains(q)),
                    w => w.ClassId,
                    s => s.ObjectId,
                    (w, s) => new { w.ClassId, s.Value })
                .Take(limit)
                .AsNoTracking()
                .ToList();

            foreach (var row in nameQuery)
            {
                if (results.Any(r => r.Wcid == row.ClassId)) continue;
                var weenie = DatabaseManager.World.GetCachedWeenie(row.ClassId);
                if (weenie != null) results.Add(ToSearchResult(weenie));
            }

            return Ok(ACE.Server.Web.WeenieSearchOrdering.SortByRelevance(
                results,
                q,
                r => r.Name,
                r => r.ClassName,
                r => r.Wcid,
                limit));
        }

        [HttpGet("weenie/{wcid}")]
        public IActionResult GetWeenie(uint wcid)
        {
            if (!HasPortalAccess(PortalPages.CombatCalculator)) return Forbid();

            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null) return NotFound();

            return Ok(BuildWeenieCombatDto(weenie));
        }

        [HttpPost("preview")]
        public IActionResult Preview([FromBody] CombatPreviewRequest request)
        {
            if (!HasPortalAccess(PortalPages.CombatCalculator)) return Forbid();
            if (request == null) return BadRequest();

            var mode = NormalizeMode(request.Mode);
            var direction = request.Direction == "monsterAttacksPlayer" ? "monsterAttacksPlayer" : "playerAttacksMonster";
            var playerAttacks = direction == "playerAttacksMonster";
            var cfg = ScalingMode(mode);
            var testAgg = request.TestAggression ?? cfg.DefaultAggression;

            var skillSource = "manual";
            var playerAccuracyMod = request.PlayerAccuracyMod ?? 1.0;
            var playerOffenseMod = request.PlayerOffenseMod ?? 1.0;
            var playerDefenseMod = request.PlayerDefenseMod ?? 1.0;
            var playerDefenseFlat = request.PlayerDefenseFlat ?? 0;
            var monsterOffenseMod = request.MonsterOffenseMod ?? 1.0;

            int playerAttackBase = 0;
            int playerDefenseBase = 0;
            int monsterAttackBase = 0;
            int monsterDefenseBase = 0;

            Player onlinePlayer = null;
            if (request.PlayerGuid.HasValue && request.PlayerGuid.Value > 0)
            {
                if (!IsAuthorizedForPlayerGuid(request.PlayerGuid.Value))
                    return Forbid();

                var resolved = ResolvePlayerSkills(
                    request.PlayerGuid.Value,
                    mode,
                    request.PlayerAccuracyMod,
                    request.PlayerOffenseMod,
                    request.PlayerDefenseMod,
                    out playerAccuracyMod,
                    out playerOffenseMod,
                    out playerDefenseMod);
                if (resolved == null) return NotFound(new { message = "Character not found." });
                skillSource = resolved.Value.Source;
                playerAttackBase = resolved.Value.PlayerAttackBase;
                playerDefenseBase = resolved.Value.PlayerDefense;
                onlinePlayer = resolved.Value.OnlinePlayer;

                if (onlinePlayer != null && !request.PlayerDefenseFlat.HasValue)
                    playerDefenseFlat = GetPlayerDefenseFlatBonus(onlinePlayer, mode);
            }

            if (request.MonsterWcid.HasValue && request.MonsterWcid.Value > 0)
            {
                var mob = BuildWeenieCombatDto(DatabaseManager.World.GetCachedWeenie(request.MonsterWcid.Value));
                if (mob == null) return NotFound(new { message = "Weenie not found." });
                monsterAttackBase = GetModeAttack(mob, mode);
                monsterDefenseBase = GetModeDefense(mob, mode);
            }

            if (request.OverridePlayerAttack.HasValue)
                playerAttackBase = request.OverridePlayerAttack.Value;
            if (request.OverridePlayerDefense.HasValue)
                playerDefenseBase = request.OverridePlayerDefense.Value;
            if (request.OverrideMonsterAttack.HasValue)
                monsterAttackBase = request.OverrideMonsterAttack.Value;
            if (request.OverrideMonsterDefense.HasValue)
                monsterDefenseBase = request.OverrideMonsterDefense.Value;

            var effectivePlayerAttack = (int)Math.Round(playerAttackBase * playerAccuracyMod * playerOffenseMod);
            var effectivePlayerDefense = onlinePlayer != null
                ? ComputeEffectivePlayerDefense(onlinePlayer, mode, playerDefenseMod, (uint)playerDefenseBase, playerDefenseFlat)
                : (int)Math.Round(playerDefenseBase * playerDefenseMod + playerDefenseFlat);
            var effectiveMonsterAttack = (int)Math.Round(monsterAttackBase * monsterOffenseMod);
            var effectiveMonsterDefense = monsterDefenseBase;

            var attackSkill = playerAttacks ? effectivePlayerAttack : effectiveMonsterAttack;
            var defenseSkill = playerAttacks ? effectiveMonsterDefense : effectivePlayerDefense;

            var triplet = BuildTriplet(mode, attackSkill, defenseSkill, testAgg, direction, cfg);

            List<RangeRowDto> rangeRows = null;
            if (request.RangeMin.HasValue && request.RangeMax.HasValue && request.RangeStep.HasValue)
            {
                var lo = Math.Min(request.RangeMin.Value, request.RangeMax.Value);
                var hi = Math.Max(request.RangeMin.Value, request.RangeMax.Value);
                rangeRows = BuildRangeRows(
                    mode, testAgg, lo, hi, request.RangeStep.Value, direction,
                    attackSkill, defenseSkill, cfg,
                    onlinePlayer, playerDefenseMod, playerDefenseFlat,
                    playerAccuracyMod, playerOffenseMod);
            }

            return Ok(new CombatPreviewResponse
            {
                Mode = mode,
                Direction = direction,
                SkillSource = skillSource,
                DefSkillId = DefSkillIdForMode(mode),
                ScalingEnabled = cfg.Enabled,
                AttackSkill = attackSkill,
                DefenseSkill = defenseSkill,
                PlayerAttackBase = playerAttackBase,
                PlayerDefenseBase = playerDefenseBase,
                MonsterAttackBase = monsterAttackBase,
                MonsterDefenseBase = monsterDefenseBase,
                EffectivePlayerAttack = effectivePlayerAttack,
                EffectivePlayerDefense = effectivePlayerDefense,
                EffectiveMonsterAttack = effectiveMonsterAttack,
                EffectiveMonsterDefense = effectiveMonsterDefense,
                PlayerAccuracyMod = playerAccuracyMod,
                PlayerOffenseMod = playerOffenseMod,
                PlayerDefenseMod = playerDefenseMod,
                PlayerDefenseFlat = playerDefenseFlat,
                MonsterOffenseMod = monsterOffenseMod,
                TestAggression = testAgg,
                Triplet = triplet,
                RangeRows = rangeRows,
            });
        }

        private bool IsAuthorizedForPlayerGuid(uint guid)
        {
            if (CurrentAccountId == null)
                return false;

            var p = PlayerManager.FindByGuid(guid);
            if (p?.Account == null)
                return false;

            if (p.Account.AccountId == CurrentAccountId.Value)
                return true;

            return HasPortalAccess(PortalPages.Players);
        }

        private static string NormalizeMode(string mode) =>
            mode?.ToLowerInvariant() switch
            {
                "melee" => "melee",
                "magic" => "magic",
                _ => "missile",
            };

        private static ScalingModeDto ScalingMode(string mode) => mode switch
        {
            "melee" => new ScalingModeDto
            {
                Enabled = ServerConfig.defense_scaling_melee_enabled.Value,
                DefaultAggression = ServerConfig.defense_scaling_melee_agg.Value,
            },
            "magic" => new ScalingModeDto
            {
                Enabled = ServerConfig.defense_scaling_magic_enabled.Value,
                DefaultAggression = ServerConfig.defense_scaling_magic_agg.Value,
            },
            _ => new ScalingModeDto
            {
                Enabled = ServerConfig.defense_scaling_missile_enabled.Value,
                DefaultAggression = ServerConfig.defense_scaling_missile_agg.Value,
            },
        };

        private static WeenieSearchResultDto ToSearchResult(ACE.Entity.Models.Weenie weenie)
        {
            var name = weenie.PropertiesString?.TryGetValue(PropertyString.Name, out var n) == true ? n : weenie.ClassName;
            return new WeenieSearchResultDto
            {
                Wcid = weenie.WeenieClassId,
                Name = name ?? $"WCID {weenie.WeenieClassId}",
                ClassName = weenie.ClassName,
                WeenieType = ((WeenieType)weenie.WeenieType).ToString(),
            };
        }

        private static WeenieCombatDto BuildWeenieCombatDto(ACE.Entity.Models.Weenie weenie)
        {
            if (weenie == null) return null;

            var skills = weenie.PropertiesSkill?
                .Select(kv => new WeenieSkillDto
                {
                    SkillId = (int)kv.Key,
                    InitLevel = (int)kv.Value.InitLevel,
                    Name = SkillName((int)kv.Key),
                })
                .OrderByDescending(s => s.InitLevel)
                .ToList() ?? new List<WeenieSkillDto>();

            var name = weenie.PropertiesString?.TryGetValue(PropertyString.Name, out var n) == true ? n : weenie.ClassName;

            return new WeenieCombatDto
            {
                Wcid = weenie.WeenieClassId,
                Name = name ?? $"WCID {weenie.WeenieClassId}",
                WeenieType = ((WeenieType)weenie.WeenieType).ToString(),
                Skills = skills,
                MeleeDefense = SkillLevel(skills, 6),
                MissileDefense = SkillLevel(skills, 7),
                MagicDefense = SkillLevel(skills, 15),
                MeleeAttack = BestAttack(skills, new[] { 45, 44, 46, 2 }),
                MissileAttack = BestAttack(skills, new[] { 47, 5, 24 }),
                MagicAttack = BestAttack(skills, MagicCastSkillIds),
            };
        }

        private static int SkillLevel(List<WeenieSkillDto> skills, int id) =>
            skills.FirstOrDefault(s => s.SkillId == id)?.InitLevel ?? 0;

        private static int BestAttack(List<WeenieSkillDto> skills, IEnumerable<int> ids)
        {
            return skills.Where(s => ids.Contains(s.SkillId)).Select(s => s.InitLevel).DefaultIfEmpty(0).Max();
        }

        private static int GetModeDefense(WeenieCombatDto mob, string mode) => mode switch
        {
            "melee" => mob.MeleeDefense,
            "magic" => mob.MagicDefense,
            _ => mob.MissileDefense,
        };

        private static int GetModeAttack(WeenieCombatDto mob, string mode) => mode switch
        {
            "melee" => mob.MeleeAttack,
            "magic" => mob.MagicAttack,
            _ => mob.MissileAttack,
        };

        private static string SkillName(int skillId) =>
            SkillNames.TryGetValue(skillId, out var name) ? name : $"Skill {skillId}";

        private struct ResolvedPlayerSkills
        {
            public string Source;
            public int PlayerAttackBase;
            public int PlayerDefense;
            public Player OnlinePlayer;
        }

        private static ResolvedPlayerSkills? ResolvePlayerSkills(
            uint guid,
            string mode,
            double? requestAccuracyMod,
            double? requestOffenseMod,
            double? requestDefenseMod,
            out double accuracyMod,
            out double offenseMod,
            out double defenseMod)
        {
            accuracyMod = requestAccuracyMod ?? 1.0;
            offenseMod = requestOffenseMod ?? 1.0;
            defenseMod = requestDefenseMod ?? 1.0;

            var online = PlayerManager.GetOnlinePlayer(guid);
            if (online != null)
            {
                var weapon = online.GetEquippedWeapon();
                var attackBase = (int)GetPlayerAttackBase(online, mode);
                if (!requestAccuracyMod.HasValue)
                    accuracyMod = online.GetAccuracyMod(weapon);
                if (!requestOffenseMod.HasValue)
                    offenseMod = WorldObject.GetWeaponOffenseModifier(online);
                if (!requestDefenseMod.HasValue)
                    defenseMod = GetPlayerDefenseMod(online, mode);
                return new ResolvedPlayerSkills
                {
                    Source = "online-exact",
                    PlayerAttackBase = attackBase,
                    PlayerDefense = (int)GetPlayerDefense(online, mode),
                    OnlinePlayer = online,
                };
            }

            var offline = PlayerManager.GetOfflinePlayer(guid);
            if (offline == null) return null;

            return new ResolvedPlayerSkills
            {
                Source = "offline-approximate",
                PlayerAttackBase = (int)GetOfflineAttackBest(offline, mode),
                PlayerDefense = (int)GetOfflineSkillTotal(offline, DefenseSkillForMode(mode)),
                OnlinePlayer = null,
            };
        }

        private static int GetPlayerDefenseFlatBonus(Player player, string mode)
        {
            if (mode == "magic")
            {
                return player.GetDefenseImbues(ImbuedEffectType.MagicDefense)
                    + (int)(player.LuminanceAugmentMagicDefenseCount ?? 0);
            }

            if (mode == "missile")
            {
                return player.GetDefenseImbues(ImbuedEffectType.MissileDefense)
                    + (int)(player.LuminanceAugmentMissileDefenseCount ?? 0);
            }

            return player.GetDefenseImbues(ImbuedEffectType.MeleeDefense)
                + (int)(player.LuminanceAugmentMeleeDefenseCount ?? 0);
        }

        private static float GetPlayerDefenseMod(Player player, string mode) => mode switch
        {
            "magic" => WorldObject.GetWeaponMagicDefenseModifierForPreview(player),
            "missile" => WorldObject.GetWeaponMissileDefenseModifierForPreview(player),
            _ => WorldObject.GetWeaponMeleeDefenseModifierForPreview(player),
        };

        /// <summary>
        /// Mirrors Creature.GetEffectiveDefenseSkill / GetEffectiveMagicDefense for preview math.
        /// defenseMod is the full multiplier (standing 1.0 + weapon bonus); loot rolls add +1.0 before storing WeaponDefense.
        /// </summary>
        private static int ComputeEffectivePlayerDefense(Player player, string mode, double defenseMod, uint skillBase, int flatBonus)
        {
            if (mode == "magic")
            {
                return (int)Math.Round(skillBase * defenseMod + flatBonus);
            }

            var burdenMod = player.GetBurdenMod();
            var stanceMod = player.GetDefenseStanceMod();

            var effectiveDefense = (int)Math.Round(
                skillBase * defenseMod * burdenMod * stanceMod + flatBonus);

            if (player.IsExhausted) effectiveDefense = 0;

            return effectiveDefense;
        }

        private static Skill DefenseSkillForMode(string mode) => mode switch
        {
            "melee" => Skill.MeleeDefense,
            "magic" => Skill.MagicDefense,
            _ => Skill.MissileDefense,
        };

        private static uint GetPlayerAttackBase(Player player, string mode)
        {
            if (mode == "missile")
                return player.GetCreatureSkill(Skill.MissileWeapons).Current;
            if (mode == "melee")
                return player.GetCreatureSkill(player.GetCurrentWeaponSkill()).Current;

            var magicSkill = player.GetCreatureSkill(Skill.WarMagic).Current;
            var voidSkill = player.GetCreatureSkill(Skill.VoidMagic).Current;
            return (uint)Math.Max(magicSkill, voidSkill);
        }

        private static uint GetOfflineAttackBest(OfflinePlayer player, string mode)
        {
            if (mode == "magic")
            {
                var war = GetOfflineSkillTotal(player, Skill.WarMagic);
                var voidMagic = GetOfflineSkillTotal(player, Skill.VoidMagic);
                return Math.Max(war, voidMagic);
            }

            if (mode == "missile")
                return GetOfflineSkillTotal(player, Skill.MissileWeapons);

            return MeleeAttackSkillIds
                .Select(id => GetOfflineSkillTotal(player, (Skill)id))
                .DefaultIfEmpty(0u)
                .Max();
        }

        private static uint GetPlayerDefense(Player player, string mode)
        {
            var defSkill = DefenseSkillForMode(mode);
            return player.GetCreatureSkill(defSkill).Current;
        }

        /// <summary>
        /// Offline skill total aligned with CharacterController.GetOfflineSkills / in-game Current
        /// (augments including Jack of All Trades; excludes live enchantments and vitae).
        /// </summary>
        private static uint GetOfflineSkillTotal(OfflinePlayer player, Skill skill)
        {
            if (player.Biota.PropertiesSkill == null || !player.Biota.PropertiesSkill.TryGetValue(skill, out var prop))
                return 0;

            player.BiotaDatabaseLock.EnterReadLock();
            try
            {
                return GetOfflineSkillTotalFromBiota(player.Biota, skill, prop);
            }
            finally
            {
                player.BiotaDatabaseLock.ExitReadLock();
            }
        }

        private static uint GetOfflineSkillTotalFromBiota(Biota biota, Skill skill, PropertiesSkill prop)
        {
            var isUsable = prop.SAC != SkillAdvancementClass.Untrained;
            if (!isUsable)
            {
                DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skill, out SkillBase skillTableRecord);
                isUsable = skillTableRecord?.MinLevel == 1;
            }

            var fromAttributes = isUsable ? AttributeFormula.GetFormula(biota, skill) : 0;
            var enlBonus = prop.SAC >= SkillAdvancementClass.Trained ? (uint)(biota.GetProperty(PropertyInt.Enlightenment) ?? 0) : 0;
            var augBaseBonus =
                (uint)(biota.GetProperty(PropertyInt.LumAugAllSkills) ?? 0)
                + (Player.MeleeSkills.Contains(skill) ? (uint)(10 * (biota.GetProperty(PropertyInt.AugmentationSkilledMelee) ?? 0)) : 0)
                + (Player.MissileSkills.Contains(skill) ? (uint)(10 * (biota.GetProperty(PropertyInt.AugmentationSkilledMissile) ?? 0)) : 0)
                + (Player.MagicSkills.Contains(skill) ? (uint)(10 * (biota.GetProperty(PropertyInt.AugmentationSkilledMagic) ?? 0)) : 0);
            var augCurrentBonus =
                (uint)(5 * (biota.GetProperty(PropertyInt.AugmentationJackOfAllTrades) ?? 0))
                + (prop.SAC >= SkillAdvancementClass.Specialized ? (uint)(2 * (biota.GetProperty(PropertyInt.LumAugSkilledSpec) ?? 0)) : 0);

            var baseTotal = prop.InitLevel + fromAttributes + prop.LevelFromPP + augBaseBonus + enlBonus;
            return baseTotal + augCurrentBonus;
        }

        private static TripletDto BuildTriplet(string mode, int atk, int def, double testAgg, string direction, ScalingModeDto cfg)
        {
            var playerAttacks = direction == "playerAttacksMonster";
            var evA = ToEvadePct(atk, def, mode, direction, false, cfg.DefaultAggression, cfg.DefaultAggression);
            var evB = ToEvadePct(atk, def, mode, direction, true, cfg.DefaultAggression, cfg.DefaultAggression);
            var evC = ToEvadePct(atk, def, mode, direction, true, cfg.DefaultAggression, testAgg);
            var hitA = Math.Round((100 - evA) * 10) / 10;
            var hitB = Math.Round((100 - evB) * 10) / 10;
            var hitC = Math.Round((100 - evC) * 10) / 10;

            return new TripletDto
            {
                PrimaryLabel = playerAttacks ? "You hit" : (mode == "magic" ? "You resist" : "You evade"),
                SecondaryLabel = playerAttacks ? "Monster evades" : (mode == "magic" ? "Spell lands" : "Monster hits you"),
                PrimaryBaseline = playerAttacks ? hitA : evA,
                PrimaryServerDefault = playerAttacks ? hitB : evB,
                PrimaryTest = playerAttacks ? hitC : evC,
                SecondaryBaseline = playerAttacks ? evA : hitA,
                SecondaryServerDefault = playerAttacks ? evB : hitB,
                SecondaryTest = playerAttacks ? evC : hitC,
            };
        }

        private static double ToEvadePct(int atk, int def, string mode, string direction, bool useScaling, double serverAgg, double aggression)
        {
            var playerAttacks = direction == "playerAttacksMonster";
            float factor;
            double hit;

            if (!useScaling)
            {
                factor = SkillCheck.GetUnscaledCombatFactor(mode, playerAttacks);
                hit = SkillCheck.GetSkillChance(atk, def, factor);
            }
            else
            {
                var scalingEnabled = ScalingMode(mode).Enabled;
                factor = SkillCheck.GetScaledCombatBaseFactor(mode);
                hit = SkillCheck.GetCombatSkillChancePreview(atk, def, factor, scalingEnabled, aggression);
            }

            return Math.Round(Math.Clamp(1.0 - hit, 0, 1) * 1000) / 10;
        }

        private static List<RangeRowDto> BuildRangeRows(
            string mode,
            double testAgg,
            int lo,
            int hi,
            int step,
            string direction,
            int fixedAtk,
            int fixedDef,
            ScalingModeDto cfg,
            Player onlinePlayer,
            double playerDefenseMod,
            int playerDefenseFlat,
            double playerAccuracyMod,
            double playerOffenseMod)
        {
            var rows = new List<RangeRowDto>();
            var sweepAttack = direction == "playerAttacksMonster";
            step = Math.Max(1, step);
            for (var sweep = lo; sweep <= hi && rows.Count < 120; sweep += step)
            {
                var atk = sweepAttack
                    ? (int)Math.Round(sweep * playerAccuracyMod * playerOffenseMod)
                    : fixedAtk;
                var def = sweepAttack ? fixedDef : sweep;
                var sweepLabel = sweep;
                var evA = ToEvadePct(atk, def, mode, direction, false, cfg.DefaultAggression, cfg.DefaultAggression);
                var evB = ToEvadePct(atk, def, mode, direction, true, cfg.DefaultAggression, cfg.DefaultAggression);
                var evC = ToEvadePct(atk, def, mode, direction, true, cfg.DefaultAggression, testAgg);
                var toPrimary = (double ev) => Math.Round((sweepAttack ? 100 - ev : ev) * 10) / 10;
                rows.Add(new RangeRowDto { Sweep = sweepLabel, A = toPrimary(evA), B = toPrimary(evB), C = toPrimary(evC) });
            }
            return rows;
        }

        public class CombatConfigDto
        {
            public ScalingModeDto Melee { get; set; }
            public ScalingModeDto Missile { get; set; }
            public ScalingModeDto Magic { get; set; }
        }

        public class ScalingModeDto
        {
            public bool Enabled { get; set; }
            public double DefaultAggression { get; set; }
        }

        public class WeenieSearchResultDto
        {
            public uint Wcid { get; set; }
            public string Name { get; set; }
            public string ClassName { get; set; }
            public string WeenieType { get; set; }
        }

        public class WeenieSkillDto
        {
            public int SkillId { get; set; }
            public int InitLevel { get; set; }
            public string Name { get; set; }
        }

        public class WeenieCombatDto
        {
            public uint Wcid { get; set; }
            public string Name { get; set; }
            public string WeenieType { get; set; }
            public List<WeenieSkillDto> Skills { get; set; }
            public int MeleeDefense { get; set; }
            public int MissileDefense { get; set; }
            public int MagicDefense { get; set; }
            public int MeleeAttack { get; set; }
            public int MissileAttack { get; set; }
            public int MagicAttack { get; set; }
        }

        public class CombatPreviewRequest
        {
            public uint? PlayerGuid { get; set; }
            public uint? MonsterWcid { get; set; }
            public string Mode { get; set; } = "missile";
            public string Direction { get; set; } = "playerAttacksMonster";
            public int? OverridePlayerAttack { get; set; }
            public int? OverridePlayerDefense { get; set; }
            public int? OverrideMonsterAttack { get; set; }
            public int? OverrideMonsterDefense { get; set; }
            public double? PlayerAccuracyMod { get; set; }
            public double? PlayerOffenseMod { get; set; }
            public double? PlayerDefenseMod { get; set; }
            public int? PlayerDefenseFlat { get; set; }
            public double? MonsterOffenseMod { get; set; }
            public double? TestAggression { get; set; }
            public int? RangeMin { get; set; }
            public int? RangeMax { get; set; }
            public int? RangeStep { get; set; }
        }

        public class TripletDto
        {
            public string PrimaryLabel { get; set; }
            public string SecondaryLabel { get; set; }
            public double PrimaryBaseline { get; set; }
            public double PrimaryServerDefault { get; set; }
            public double PrimaryTest { get; set; }
            public double SecondaryBaseline { get; set; }
            public double SecondaryServerDefault { get; set; }
            public double SecondaryTest { get; set; }
        }

        public class RangeRowDto
        {
            public int Sweep { get; set; }
            public double A { get; set; }
            public double B { get; set; }
            public double C { get; set; }
        }

        public class CombatPreviewResponse
        {
            public string Mode { get; set; }
            public string Direction { get; set; }
            public string SkillSource { get; set; }
            public int DefSkillId { get; set; }
            public bool ScalingEnabled { get; set; }
            public int AttackSkill { get; set; }
            public int DefenseSkill { get; set; }
            public int PlayerAttackBase { get; set; }
            public int PlayerDefenseBase { get; set; }
            public int MonsterAttackBase { get; set; }
            public int MonsterDefenseBase { get; set; }
            public int EffectivePlayerAttack { get; set; }
            public int EffectivePlayerDefense { get; set; }
            public int EffectiveMonsterAttack { get; set; }
            public int EffectiveMonsterDefense { get; set; }
            public double PlayerAccuracyMod { get; set; }
            public double PlayerOffenseMod { get; set; }
            public double PlayerDefenseMod { get; set; }
            public int PlayerDefenseFlat { get; set; }
            public double MonsterOffenseMod { get; set; }
            public double TestAggression { get; set; }
            public TripletDto Triplet { get; set; }
            public List<RangeRowDto> RangeRows { get; set; }
        }
    }
}
