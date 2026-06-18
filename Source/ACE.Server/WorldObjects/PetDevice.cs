using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using log4net;

using ACE.Database;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Pet Devices are the essences used to summon creatures
    /// </summary>
    public partial class PetDevice : WorldObject
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public int? PetClass
        {
            get => GetProperty(PropertyInt.PetClass);
            set { if (value.HasValue) SetProperty(PropertyInt.PetClass, value.Value); else RemoveProperty(PropertyInt.PetClass); }
        }

        // Pet Bonding System - Stored on the device (not the spawned pet)
        public bool? PetBondAttuned
        {
            get => GetProperty(PropertyBool.PetBondAttuned);
            set { if (!value.HasValue) RemoveProperty(PropertyBool.PetBondAttuned); else SetProperty(PropertyBool.PetBondAttuned, value.Value); }
        }

        public int? PetBondLevel
        {
            get => GetProperty(PropertyInt.PetBondLevel);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.PetBondLevel); else SetProperty(PropertyInt.PetBondLevel, value.Value); }
        }

        public long? PetBondXp
        {
            get => GetProperty(PropertyInt64.PetBondXp);
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.PetBondXp); else SetProperty(PropertyInt64.PetBondXp, value.Value); }
        }

        public long? PetBondXpTotal
        {
            get => GetProperty(PropertyInt64.PetBondXpTotal);
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.PetBondXpTotal); else SetProperty(PropertyInt64.PetBondXpTotal, value.Value); }
        }

        public long? PetBondAttunedCharacterId
        {
            get => GetProperty(PropertyInt64.PetBondAttunedCharacterId);
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.PetBondAttunedCharacterId); else SetProperty(PropertyInt64.PetBondAttunedCharacterId, value.Value); }
        }

        /// <summary>Stored potency levels (Essence Residue spent on this essence). PropertyInt 9056.</summary>
        public int? PetPotencyStored
        {
            get => GetProperty(PropertyInt.PetPotencyStored);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.PetPotencyStored); else SetProperty(PropertyInt.PetPotencyStored, value.Value); }
        }

        public bool IsPetBondAttuned => PetBondAttuned ?? false;

        /// <summary>
        /// Returns true if this device summons a CombatPet (WeenieType.CombatPet).
        /// Used to scope Pet Bonding to combat pet devices only.
        /// </summary>
        public bool IsCombatPetDevice()
        {
            if (!PetClass.HasValue || PetClass.Value <= 0)
                return false;

            // Use cached weenie data; PetClass points at the summon weenie.
            var weenie = DatabaseManager.World.GetCachedWeenie((uint)PetClass.Value);
            if (weenie == null)
                return false;

            return weenie.WeenieType == WeenieType.CombatPet;
        }

        /// <summary>
        /// Bond-derived combat bonuses for a summoned CombatPet (additive on top of gem gear ratings).
        /// DR uses (level+2)/3; CDR/CD ramp linearly 0→cap over bond level cap (if cap is unlimited, ramp uses 1000 levels so bonuses reach their configured caps at finite bond); D uses the agreed low-level table then +1 per 3 levels from 15+.
        /// Vitality bonus is bondLevel × pet_bond_vitality_per_level (applied as flat max health in CombatPet.Init).
        /// </summary>
        public static void GetBondCombatStatBonuses(int bondLevel, int levelCap, out int damageResistRating, out int critDamageResistRating, out int damageRating, out int critDamageRating, out int vitalityToMaxHealth)
        {
            damageResistRating = 0;
            critDamageResistRating = 0;
            damageRating = 0;
            critDamageRating = 0;
            vitalityToMaxHealth = 0;

            if (bondLevel < 0)
                bondLevel = 0;

            if (levelCap < 1)
                levelCap = 1;

            if (bondLevel > levelCap)
                bondLevel = levelCap;

            damageResistRating = (bondLevel + 2) / 3;

            var cdrCap = (int)ServerConfig.pet_bond_cdr_cap.Value;
            var cdCap = (int)ServerConfig.pet_bond_cd_cap.Value;
            if (cdrCap < 0)
                cdrCap = 0;
            if (cdCap < 0)
                cdCap = 0;

            // Unlimited bond level uses int.MaxValue as cap; do not divide by that or CDR/CD stay ~0 until enormous bond.
            var linearRampLevels = levelCap == int.MaxValue ? 1000 : levelCap;

            critDamageResistRating = (int)Math.Min(cdrCap, (long)bondLevel * cdrCap / linearRampLevels);
            critDamageRating = (int)Math.Min(cdCap, (long)bondLevel * cdCap / linearRampLevels);

            damageRating = GetBondDamageRatingBonus(bondLevel);

            var vitPer = (int)ServerConfig.pet_bond_vitality_per_level.Value;
            if (vitPer < 0)
                vitPer = 0;
            vitalityToMaxHealth = bondLevel * vitPer;
        }

        private static int GetBondDamageRatingBonus(int L)
        {
            if (L < 3)
                return 0;
            if (L <= 6)
                return 1;
            if (L <= 8)
                return 2;
            if (L <= 11)
                return 3;
            if (L <= 14)
                return 4;
            return 5 + (L - 15) / 3;
        }

        public static long GetBondXpToNextLevel(int currentLevel)
        {
            // Match the player's level XP curve (retail table through 275, dynamic progression beyond).
            var lvl = Math.Max(1, currentLevel);

            if (lvl >= 275)
            {
                var a = Player.GenerateDynamicLevelPostMax(lvl);
                var b = Player.GenerateDynamicLevelPostMax(lvl + 1);
                var delta = b - a;
                if (delta < 1) delta = 1;
                if (delta > long.MaxValue) delta = long.MaxValue;
                return (long)Math.Round(delta);
            }

            var xpTable = DatManager.PortalDat.XpTable.CharacterLevelXPList;
            var maxIndex = xpTable.Count - 1;
            if (maxIndex <= 1)
                return 1;

            var aIdx = Math.Clamp(lvl, 1, maxIndex - 1);
            var bIdx = aIdx + 1;

            var deltaTable = (long)xpTable[bIdx] - (long)xpTable[aIdx];
            return Math.Max(1, deltaTable);
        }

        /// <summary>
        /// Pushes bond + potency fields to the owning client via <see cref="Player.UpdateProperty"/> (and includes them on identify
        /// when marked <see cref="AssessmentPropertyAttribute"/>). Property IDs: PetBondAttuned 9047, PetBondXp 9050,
        /// PetBondXpTotal 9051, PetBondAttunedCharacterId 9052, PetBondLevel 9053, PetPotencyStored 9056.
        /// </summary>
        public void SyncPetBondPropertiesToOwner(Player owner, bool broadcast = false)
        {
            SyncPetProgressPropertiesToOwner(owner, broadcast);
        }

        public void SyncPetProgressPropertiesToOwner(Player owner, bool broadcast = false)
        {
            if (owner == null || !IsCombatPetDevice())
                return;

            if (ServerConfig.pet_bond_enabled.Value && IsPetBondAttuned)
            {
                owner.UpdateProperty(this, PropertyBool.PetBondAttuned, PetBondAttuned, broadcast);
                owner.UpdateProperty(this, PropertyInt.PetBondLevel, PetBondLevel ?? 1, broadcast);
                owner.UpdateProperty(this, PropertyInt64.PetBondXp, PetBondXp ?? 0, broadcast);
                owner.UpdateProperty(this, PropertyInt64.PetBondXpTotal, PetBondXpTotal ?? 0, broadcast);
                owner.UpdateProperty(this, PropertyInt64.PetBondAttunedCharacterId, PetBondAttunedCharacterId, broadcast);
            }

            if (ServerConfig.pet_potency_enabled.Value)
                owner.UpdateProperty(this, PropertyInt.PetPotencyStored, PetPotencyStored ?? 0, broadcast);
        }

        public bool TryAwardBondXp(Player owner, long amount, out bool leveledUp)
        {
            leveledUp = false;

            if (amount <= 0)
                return false;

            if (!ServerConfig.pet_bond_enabled.Value)
                return false;

            if (!IsCombatPetDevice() || !IsPetBondAttuned)
                return false;

            var bondedCharacterId = PetBondAttunedCharacterId;
            if (bondedCharacterId.HasValue && owner != null && bondedCharacterId.Value != (long)owner.Character.Id)
                return false;

            var levelCapRaw = (int)ServerConfig.pet_bond_level_cap.Value;
            var levelCap = levelCapRaw <= 0 ? int.MaxValue : Math.Max(1, levelCapRaw);

            var level = PetBondLevel.GetValueOrDefault(1);
            if (level < 1) level = 1;

            var xp = PetBondXp.GetValueOrDefault(0);
            var total = PetBondXpTotal.GetValueOrDefault(0);

            xp += amount;
            total += amount;

            while (level < levelCap)
            {
                var xpToNext = GetBondXpToNextLevel(level);
                if (xp < xpToNext)
                    break;

                xp -= xpToNext;
                level++;
                leveledUp = true;
            }

            PetBondLevel = level;
            PetBondXp = xp;
            PetBondXpTotal = total;

            SaveBiotaToDatabase();

            if (owner != null)
                SyncPetBondPropertiesToOwner(owner, broadcast: true);

            // Same level-up play script as players (Player_Xp), on the spawned pet so it shows in-world for nearby clients.
            if (leveledUp && owner != null
                && owner.CurrentActivePet is CombatPet bondPet
                && bondPet.SummoningDeviceGuid == Guid
                && bondPet.PhysicsObj != null)
            {
                bondPet.PlayParticleEffect(PlayScript.LevelUp, bondPet.Guid);
            }

            return true;
        }

        // Monster Capture System - Visual Override Properties
        public uint? VisualOverrideSetup
        {
            get => GetProperty(PropertyDataId.VisualOverrideSetup);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.VisualOverrideSetup); else SetProperty(PropertyDataId.VisualOverrideSetup, value.Value); }
        }

        public uint? VisualOverrideMotionTable
        {
            get => GetProperty(PropertyDataId.VisualOverrideMotionTable);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.VisualOverrideMotionTable); else SetProperty(PropertyDataId.VisualOverrideMotionTable, value.Value); }
        }

        public uint? VisualOverrideCombatTable
        {
            get => GetProperty(PropertyDataId.VisualOverrideCombatTable);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.VisualOverrideCombatTable); else SetProperty(PropertyDataId.VisualOverrideCombatTable, value.Value); }
        }

        public uint? VisualOverrideSoundTable
        {
            get => GetProperty(PropertyDataId.VisualOverrideSoundTable);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.VisualOverrideSoundTable); else SetProperty(PropertyDataId.VisualOverrideSoundTable, value.Value); }
        }

        public uint? VisualOverridePaletteBase
        {
            get => GetProperty(PropertyDataId.VisualOverridePaletteBase);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.VisualOverridePaletteBase); else SetProperty(PropertyDataId.VisualOverridePaletteBase, value.Value); }
        }

        public uint? VisualOverrideClothingBase
        {
            get => GetProperty(PropertyDataId.VisualOverrideClothingBase);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.VisualOverrideClothingBase); else SetProperty(PropertyDataId.VisualOverrideClothingBase, value.Value); }
        }

        public uint? VisualOverrideIcon
        {
            get => GetProperty(PropertyDataId.VisualOverrideIcon);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.VisualOverrideIcon); else SetProperty(PropertyDataId.VisualOverrideIcon, value.Value); }
        }

        public int? VisualOverridePaletteTemplate
        {
            get => GetProperty(PropertyInt.VisualOverridePaletteTemplate);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.VisualOverridePaletteTemplate); else SetProperty(PropertyInt.VisualOverridePaletteTemplate, value.Value); }
        }

        public double? VisualOverrideShade
        {
            get => GetProperty(PropertyFloat.VisualOverrideShade);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.VisualOverrideShade); else SetProperty(PropertyFloat.VisualOverrideShade, value.Value); }
        }

        public double? VisualOverrideScale
        {
            get => GetProperty(PropertyFloat.VisualOverrideScale);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.VisualOverrideScale); else SetProperty(PropertyFloat.VisualOverrideScale, value.Value); }
        }

        public string VisualOverrideName
        {
            get => GetProperty(PropertyString.CapturedCreatureName);
            set { if (value == null) RemoveProperty(PropertyString.CapturedCreatureName); else SetProperty(PropertyString.CapturedCreatureName, value); }
        }

        public string VisualOverrideCapturedItems
        {
            get => GetProperty(PropertyString.CapturedItems);
            set { if (value == null) RemoveProperty(PropertyString.CapturedItems); else SetProperty(PropertyString.CapturedItems, value); }
        }

        public int? VisualOverrideCreatureType
        {
            get => GetProperty(PropertyInt.CapturedCreatureType);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.CapturedCreatureType); else SetProperty(PropertyInt.CapturedCreatureType, value.Value); }
        }

        public int? VisualOverrideCreatureVariant
        {
            get => GetProperty(PropertyInt.CapturedCreatureVariant);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.CapturedCreatureVariant); else SetProperty(PropertyInt.CapturedCreatureVariant, value.Value); }
        }

        /// <summary>WCID of the creature this combat essence skin was siphoned from (<see cref="PropertyInt.CapturedCreatureWCID"/>).</summary>
        public int? CaptureSkinCreatureWcid
        {
            get => GetProperty(PropertyInt.CapturedCreatureWCID);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.CapturedCreatureWCID); else SetProperty(PropertyInt.CapturedCreatureWCID, value.Value); }
        }

        // Serialized ObjDesc data for humanoid appearance
        public string CapturedObjDescAnimParts => GetProperty(PropertyString.CapturedObjDescAnimParts);
        public string CapturedObjDescPalettes => GetProperty(PropertyString.CapturedObjDescPalettes);
        public string CapturedObjDescTextures => GetProperty(PropertyString.CapturedObjDescTextures);
        
        /// <summary>
        /// Returns true if any captured ObjDesc data exists (AnimParts, Palettes, or Textures).
        /// </summary>
        public bool HasCapturedObjDesc => 
            !string.IsNullOrEmpty(CapturedObjDescAnimParts) ||
            !string.IsNullOrEmpty(CapturedObjDescPalettes) ||
            !string.IsNullOrEmpty(CapturedObjDescTextures);

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public PetDevice(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();

            // todo: remove me when the data is fixed
            Structure = MaxStructure;
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public PetDevice(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        /// <summary>
        /// Base <see cref="WorldObject.OnActivate"/> starts item cooldown before <see cref="ActOnUse"/>, which charged
        /// cooldown on failed spawns, stow-only activations, and retail two-click passive flow. Pet devices defer
        /// cooldown until <see cref="SummonCreature"/> completes with success. When
        /// <see cref="ServerConfig.pet_summon_cooldown_on_pet_death_only"/> is enabled for combat pets, cooldown is
        /// applied from <see cref="CombatPet.Die"/> only — not on summon.
        /// When <see cref="ServerConfig.pet_combat_summon_skips_shared_cooldown"/> is enabled for combat pets, no
        /// SharedCooldown is started on summon; combat pings and death handle timing instead.
        /// </summary>
        protected override bool ShouldApplyActivationCooldown(Player player) => false;

        /// <summary>
        /// Upper bound (seconds) for this essence's SharedCooldown timers from combat refresh or death.
        /// Uses <see cref="PropertyFloat.CooldownDuration"/> when positive; otherwise 45s default.
        /// </summary>
        public static float GetEssenceSharedCooldownCapSeconds(PetDevice device)
        {
            if (device == null)
                return 0f;

            var d = device.CooldownDuration;
            if (d.HasValue && d.Value > 0)
                return (float)d.Value;

            return 45f;
        }

        private const string SummonDurationSeparateFromCooldownFooter = "\n(Separate from essence cooldown.)";

        /// <summary>
        /// Appraisal-only summary for how long a summoned combat pet stays before decay/lifespan despawns it,
        /// mirroring <see cref="CombatPet.Init"/> (server unlimited flag; luminance summon/duration aug bonuses applied to both
        /// <see cref="PropertyFloat.TimeToRot"/> and <see cref="PropertyInt.Lifespan"/> when present). Distinct from essence reuse cooldown.
        /// </summary>
        public string BuildCombatPetSummonDurationAppraisal(Player examiner)
        {
            if (!IsCombatPetDevice())
                return null;

            if (!PetClass.HasValue || PetClass.Value <= 0)
                return null;

            if (ServerConfig.pet_combat_unlimited_lifespan.Value)
            {
                return "Summon duration: unlimited decay while alive (server setting). Pet still despawns if killed or exceeds owner follow distance.\n(This is separate from \"Cooldown When Used\" on this essence.)";
            }

            var petWeenie = DatabaseManager.World.GetCachedWeenie((uint)PetClass.Value);
            if (petWeenie == null)
                return null;

            var (rotSeconds, lifeSeconds) = GetCombatPetSummonRotAndLifespanSeconds(petWeenie, examiner);

            if (!rotSeconds.HasValue && !lifeSeconds.HasValue)
            {
                return "Summon duration: not fixed by creature decay/lifespan on this pet template (pet may still despawn from kill or owner follow distance)." + SummonDurationSeparateFromCooldownFooter;
            }

            return FormatCombatPetSummonDurationCore(rotSeconds, lifeSeconds) + SummonDurationSeparateFromCooldownFooter;
        }

        /// <summary>Mirrors <see cref="CombatPet.Init"/> luminance aug math applied to creature TimeToRot and Lifespan.</summary>
        private static (int? RotSeconds, int? LifeSeconds) GetCombatPetSummonRotAndLifespanSeconds(Weenie petWeenie, Player examiner)
        {
            var tr = petWeenie.GetProperty(PropertyFloat.TimeToRot);
            var life = petWeenie.GetProperty(PropertyInt.Lifespan);

            var summonAugCount = examiner?.LuminanceAugmentSummonCount ?? 0;
            var durationAugEffective = CombatPet.GetLifespanBonusEffectiveDurationAugCount(examiner, summonAugCount);

            var extraSeconds = 0.0;
            var perAug = ServerConfig.pet_summon_lifespan_seconds_per_aug.Value;
            var perDur = ServerConfig.pet_combat_lifespan_seconds_per_duration_aug.Value;
            if (summonAugCount > 0 && perAug > 0)
                extraSeconds += summonAugCount * perAug;
            if (durationAugEffective > 0 && perDur > 0)
                extraSeconds += durationAugEffective * perDur;

            var bonusRounded = extraSeconds > 0 ? (int)Math.Round(extraSeconds) : 0;

            int? rotSeconds = null;
            if (tr.HasValue && tr.Value > 0)
                rotSeconds = bonusRounded > 0 ? (int)Math.Round(tr.Value + bonusRounded) : (int)Math.Round(tr.Value);

            int? lifeSeconds = null;
            if (life.HasValue && life.Value > 0)
                lifeSeconds = bonusRounded > 0 ? life.Value + bonusRounded : life.Value;

            return (rotSeconds, lifeSeconds);
        }

        private static string FormatCombatPetSummonDurationCore(int? rotSeconds, int? lifeSeconds)
        {
            if (rotSeconds.HasValue && lifeSeconds.HasValue)
            {
                if (rotSeconds.Value == lifeSeconds.Value)
                    return $"Summon duration (for you): ~{rotSeconds.Value}s (decay timer matches innate lifespan).";
                return $"Summon duration (for you): whichever expires first — decay ~{rotSeconds.Value}s (includes luminance summon/duration aug bonuses when configured); innate lifespan ~{lifeSeconds.Value}s.";
            }

            if (rotSeconds.HasValue)
                return $"Summon duration (for you): ~{rotSeconds.Value}s until decay (includes luminance summon/duration aug bonuses when configured).";

            return $"Summon duration (for you): ~{lifeSeconds.Value}s from creature lifespan timer (includes luminance summon/duration aug bonuses when configured).";
        }

        private void TryApplySummonActivationCooldown(Player player)
        {
            if (player?.EnchantmentManager == null || CooldownId == null)
                return;

            if (ServerConfig.pet_summon_cooldown_on_pet_death_only.Value && IsCombatPetDevice())
                return;

            if (ServerConfig.pet_combat_summon_skips_shared_cooldown.Value && IsCombatPetDevice())
                return;

            // Optional global override: let combat pet essences have a short shared cooldown on summon
            // without editing each device's CooldownDuration in the DB. Death cooldown is handled elsewhere.
            if (IsCombatPetDevice())
            {
                var overrideSeconds = (float)ServerConfig.pet_combat_summon_initial_shared_cooldown_seconds.Value;
                if (overrideSeconds > 0)
                {
                    var cap = GetEssenceSharedCooldownCapSeconds(this);
                    var duration = cap > 0 ? Math.Min(overrideSeconds, cap) : overrideSeconds;
                    player.EnchantmentManager.StartOrRefreshItemCooldown(Guid.Full, CooldownId.Value, duration);
                    return;
                }
            }
            player.EnchantmentManager.StartCooldown(this);
        }

        /// <summary>
        /// Strips chained "Owner's " style prefixes from a stored capture name (same rules as summon naming).
        /// </summary>
        public static string StripCapturedCreatureNamePrefixes(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var baseName = name;
            int apostropheIdx;
            while ((apostropheIdx = baseName.IndexOf("'s ")) > 0)
                baseName = baseName.Substring(apostropheIdx + 3);

            return baseName.Trim();
        }

        /// <summary>
        /// Rebuilds pet device inventory name after applying a new capture skin: keeps the prefix before the
        /// creature token and the " Essence..." suffix (e.g. "Lightning Maiden Essence" → "Lightning Floeshark Essence").
        /// When a previous <see cref="VisualOverrideName"/> exists, it is stripped from the head for a reliable prefix;
        /// otherwise the last word of the head is treated as the template creature token (works for "Lightning Maiden").
        /// </summary>
        public static string BuildDisplayNameAfterCaptureApply(string currentDeviceName, string previousCapturedCreatureName, string newCapturedCreatureName)
        {
            if (string.IsNullOrEmpty(currentDeviceName))
                return currentDeviceName;

            var newMid = StripCapturedCreatureNamePrefixes(newCapturedCreatureName);
            if (string.IsNullOrEmpty(newMid))
                return currentDeviceName;

            var idx = currentDeviceName.LastIndexOf(" Essence", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return currentDeviceName;

            var tail = currentDeviceName.Substring(idx);
            var head = currentDeviceName.Substring(0, idx);
            var oldMid = StripCapturedCreatureNamePrefixes(previousCapturedCreatureName ?? "");

            string prefix;
            if (!string.IsNullOrEmpty(oldMid) && head.EndsWith(oldMid, StringComparison.OrdinalIgnoreCase))
                prefix = head.Substring(0, head.Length - oldMid.Length).TrimEnd();
            else
            {
                var parts = head.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    prefix = string.Join(" ", parts, 0, parts.Length - 1);
                else
                    prefix = "";
            }

            if (string.IsNullOrEmpty(prefix))
                return newMid + tail;

            return $"{prefix} {newMid}{tail}";
        }

        /// <summary>
        /// Display name for bond/progress messages: uses capture rename (VisualOverrideName) with the device tier suffix (e.g. " Essence (80)").
        /// </summary>
        public string GetBondMessageDisplayName()
        {
            if (string.IsNullOrEmpty(VisualOverrideName))
                return Name;

            var essenceIdx = Name.LastIndexOf(" Essence", StringComparison.OrdinalIgnoreCase);
            var tail = essenceIdx >= 0 ? Name.Substring(essenceIdx) : "";

            var baseName = StripCapturedCreatureNamePrefixes(VisualOverrideName);

            return string.IsNullOrEmpty(tail) ? baseName : baseName + tail;
        }

        // Monster Capture System - Handle captured appearance application
        public override void HandleActionUseOnTarget(Player player, WorldObject target)
        {
            if (MonsterCapture.IsCapturedAppearance(target))
            {
                if (player.IsBusy)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You are too busy to do that.", ChatMessageType.System));
                    return;
                }

                if (player.CombatMode != CombatMode.NonCombat)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You are in combat mode!", ChatMessageType.System));
                    return;
                }

                MonsterCapture.ApplyAppearanceToCrate(player, this, target);
                return;
            }

            base.HandleActionUseOnTarget(player, target);
        }

        public override void ActOnUse(WorldObject activator)
        {
            if (!(activator is Player player))
                return;

            // Pet Bonding System - character-bound combat pet devices
            if (ServerConfig.pet_bond_enabled.Value && IsCombatPetDevice() && IsPetBondAttuned)
            {
                var bondedCharacterId = PetBondAttunedCharacterId;
                if (bondedCharacterId.HasValue && bondedCharacterId.Value != (long)player.Character.Id)
                {
                    player.SendTransientError("This pet device is bonded to another character.");
                    return;
                }

                // Ensure legacy/admin-attuned devices get the standard "no trade/drop" flags.
                if (Attuned != AttunedStatus.Attuned || Bonded != BondedStatus.Bonded)
                {
                    Attuned = AttunedStatus.Attuned;
                    Bonded = BondedStatus.Bonded;
                    SaveBiotaToDatabase();
                }
            }

            // Good PCAP example of using a PetDevice to summon a pet:
            // Asherons-Call-packets-includes-3-towers\pkt_2017-1-30_1485823896_log.pcap lines 27837 - 27843

            if (PetClass == null)
            {
                log.Error($"{activator.Name}.ActOnUse({Name}) - PetClass is null for PetDevice {WeenieClassId}");
                return;
            }

            if (Structure == 0)
            {
                if (!TryApplyPyrealAutoRefillBeforeSummon(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("Your summoning device does not have enough charges to function!", ChatMessageType.Broadcast));
                    return;
                }
            }

            var wcid = (uint)PetClass;

            var result = SummonCreature(player, wcid);

            // Only consume a charge on a successful summon. (SummonCreature returns null when Init aborts; treating null as success wrongly decremented structure.)
            if (result == true)
            {
                // CombatPet devices should always have structure
                if (Structure != null)
                {
                    // decrease remaining uses
                    Structure--;

                    player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.Structure, Structure.Value));
                }

                TryApplySummonActivationCooldown(player);
            }
            else
            {
                // this would be a good place to send a friendly reminder to install the latest summoning updates from ACE-World-Patch
            }
        }

        public override ActivationResult CheckUseRequirements(WorldObject activator)
        {
            if (!(activator is Player player))
                return new ActivationResult(false);

            var baseRequirements = base.CheckUseRequirements(activator);
            if (!baseRequirements.Success)
                return baseRequirements;

            var minLumAugSummon = GetProperty(PropertyInt.PetDeviceMinLumAugSummonCount);
            if (minLumAugSummon.HasValue && minLumAugSummon.Value > 0)
            {
                var current = player.GetProperty(PropertyInt64.LumAugSummonCount) ?? 0;
                if (current < minLumAugSummon.Value)
                {
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session,
                        $"Your Luminance Summoning inheritance is {current}. This item requires at least {minLumAugSummon.Value}."));
                    return new ActivationResult(false);
                }
            }

            // Mastery: essence/player PropertyInt 362; universal charm = bool 50038 (see docs/ADMIN_PET_SUMMON_CHARMS.md).
            // global:: required — PetDevice.SummoningMastery property shadows ACE.Entity.Enum.SummoningMastery.
            if (SummoningMastery != null && SummoningMastery != global::ACE.Entity.Enum.SummoningMastery.Undef
                && player.SummoningMastery != SummoningMastery
                && !(ServerConfig.pet_charm_universal_summoning_mastery_enabled.Value && player.HasUniversalSummoningMastery && CharmSettingsManager.UniversalSummoning.Enabled))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must be a {SummoningMastery} to use the {Name}", ChatMessageType.Broadcast));
                return new ActivationResult(false);
            }

            // While a CombatPet is active, summoning another creature goes through Pet.Init -> HandleCurrentActivePet
            // (replace/stow rules). Do not block unrelated PetDevice uses (e.g. inventory crates misclassified as PetDevice).
            return new ActivationResult(true);
        }

        /// <summary>
        /// Sends <see cref="GameMessagePublicUpdatePropertyString"/> for <see cref="PropertyString.Name"/> to the
        /// summoning player (non-broadcast). Helps inventory display refresh without relog; client may still cache
        /// some string properties (see <see cref="Player.UpdateProperty(WorldObject, PropertyString, string, bool)"/>).
        /// </summary>
        public void TryNotifySummonerNameProperty(Player summoner)
        {
            if (summoner?.Session == null)
                return;

            summoner.UpdateProperty(this, PropertyString.Name, Name ?? "");
        }

        /// <summary>First word of matrix / naturalist essence names that denotes damage flavor (elemental or physical).</summary>
        private static readonly HashSet<string> EssenceNameDamageLeadWords = BuildEssenceNameDamageLeadWords();

        private static HashSet<string> BuildEssenceNameDamageLeadWords()
        {
            var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dt in Enum.GetValues(typeof(DamageType)).Cast<DamageType>().OrderBy(d => (int)d))
            {
                if (dt == DamageType.Undef || dt == DamageType.Base || dt.IsMultiDamage())
                    continue;
                if (dt == DamageType.Health || dt == DamageType.Stamina || dt == DamageType.Mana)
                    continue;
                hs.Add(dt.ToString());
                hs.Add(dt.DisplayName());
            }

            hs.Add("Frost");
            // Common matrix / capstone name lead-ins (not enum spellings): see PetDeviceToPetMapping comments.
            foreach (var extra in new[]
                     {
                         "Caustic", "Blistering", "Scorched", "Arctic", "Excited", "Volcanic", "Electrified", "Galvanic",
                         "Glacial", "Incendiary", "Frigid", "Charred", "Shocked", "Corrosion", "Corrosive", "Voltaic", "Freezing",
                         "Blizzard"
                     })
                hs.Add(extra);

            return hs;
        }

        /// <summary>Legacy suffix from older clients: <c> [Slash]</c>, etc.</summary>
        private static readonly Regex WeaponDamageDisplaySuffix = new(@" \[(?<tag>\w+)\]\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Bracket suffix tags may use enum names or <see cref="DamageTypeExtensions.DisplayName"/> (e.g. Lightning for Electric).</summary>
        private static bool TryParseWeaponDamageSuffixTag(string tag, out DamageType parsed)
        {
            if (Enum.TryParse(tag, ignoreCase: true, out parsed))
                return true;

            foreach (DamageType dt in Enum.GetValues(typeof(DamageType)))
            {
                if (dt == DamageType.Undef || dt == DamageType.Base || dt.IsMultiDamage())
                    continue;
                if (tag.Equals(dt.ToString(), StringComparison.OrdinalIgnoreCase)
                    || tag.Equals(dt.DisplayName(), StringComparison.OrdinalIgnoreCase))
                {
                    parsed = dt;
                    return true;
                }
            }

            parsed = DamageType.Undef;
            return false;
        }

        private static string GetDisplayNameWithoutWeaponDamageSuffix(string name)
        {
            var n = name ?? "";
            var m = WeaponDamageDisplaySuffix.Match(n);
            if (!m.Success)
                return n;

            var tag = m.Groups["tag"].Value;
            if (!TryParseWeaponDamageSuffixTag(tag, out var parsed))
                return n;
            if (parsed == DamageType.Undef || parsed == DamageType.Base || parsed.IsMultiDamage())
                return n;

            return n[..m.Index].TrimEnd();
        }

        private static DamageType? PickPrimaryPhysicalDamageType(DamageType flags)
        {
            if (flags == DamageType.Undef || flags == 0)
                return null;

            foreach (DamageType damageType in Enum.GetValues(typeof(DamageType)))
            {
                if ((flags & damageType) != 0 && !damageType.IsMultiDamage())
                    return damageType;
            }

            return null;
        }

        /// <summary>
        /// Damage type from creature equipped weapons only (no body innate). Used when siphoning for
        /// <see cref="PropertyInt.CapturedSourceDamageType"/> so unarmed captures do not lock creature innate damage.
        /// </summary>
        public static DamageType? TryGetDamageTypeFromCreatureEquippedWeaponsOnly(Creature creature)
        {
            if (creature == null)
                return null;

            var melee = creature.GetEquippedMeleeWeapon();
            var launcher = creature.GetEquippedMissileWeapon();
            var ammo = creature.GetMissileAmmo();

            if (melee != null)
                return PickPrimaryPhysicalDamageType(melee.W_DamageType);
            if (launcher != null && ammo != null)
                return PickPrimaryPhysicalDamageType(ammo.W_DamageType);
            if (launcher != null)
                return PickPrimaryPhysicalDamageType(launcher.W_DamageType);
            var wand = creature.GetEquippedWand();
            if (wand != null)
                return PickPrimaryPhysicalDamageType(wand.W_DamageType);
            return null;
        }

        /// <summary>
        /// If <paramref name="leadWord"/> matches a known matrix / essence damage lead-in, returns the corresponding
        /// <see cref="DamageType"/> (single-type only). Used for template weenie names and for validating replace-only naming.
        /// </summary>
        public static DamageType? TryMatchEssenceDamageLeadWordToDamageType(string leadWord)
        {
            if (string.IsNullOrWhiteSpace(leadWord))
                return null;

            foreach (DamageType dt in Enum.GetValues(typeof(DamageType)))
            {
                if (dt == DamageType.Undef || dt == DamageType.Base || dt.IsMultiDamage())
                    continue;
                if (dt == DamageType.Health || dt == DamageType.Stamina || dt == DamageType.Mana)
                    continue;
                if (leadWord.Equals(dt.ToString(), StringComparison.OrdinalIgnoreCase)
                    || leadWord.Equals(dt.DisplayName(), StringComparison.OrdinalIgnoreCase))
                    return dt;
            }

            // Matrix-style words that are not enum spellings (subset of <see cref="EssenceNameDamageLeadWords"/>).
            if (leadWord.Equals("Frost", StringComparison.OrdinalIgnoreCase))
                return DamageType.Cold;

            if (MatrixExtraEssenceDamageLeadWords.TryGetValue(leadWord, out var mapped))
                return mapped;

            return null;
        }

        /// <summary>Maps non-enum matrix name lead-ins to a single <see cref="DamageType"/> for template parsing.</summary>
        private static readonly Dictionary<string, DamageType> MatrixExtraEssenceDamageLeadWords = BuildMatrixExtraEssenceDamageLeadWords();

        private static Dictionary<string, DamageType> BuildMatrixExtraEssenceDamageLeadWords()
        {
            static void add(Dictionary<string, DamageType> d, string w, DamageType dt) => d[w] = dt;

            var d = new Dictionary<string, DamageType>(StringComparer.OrdinalIgnoreCase);
            foreach (var extra in new[]
                     {
                         "Caustic", "Blistering", "Blistered", "Corrosion", "Corrosive"
                     })
                add(d, extra, DamageType.Acid);

            foreach (var extra in new[] { "Scorched", "Incendiary", "Charred", "Volcanic" })
                add(d, extra, DamageType.Fire);

            foreach (var extra in new[] { "Arctic", "Glacial", "Frigid", "Freezing", "Blizzard" })
                add(d, extra, DamageType.Cold);

            foreach (var extra in new[] { "Electrified", "Galvanic", "Shocked", "Excited", "Voltaic" })
                add(d, extra, DamageType.Electric);

            return d;
        }

        /// <summary>
        /// Parses the first word of a combat-pet <see cref="PetDevice"/> template weenie <see cref="PropertyString.Name"/>
        /// (e.g. "Acid Matron Essence (250)") for a matrix elemental / physical lead-in.
        /// </summary>
        public static DamageType? TryGetTemplateEssenceDamageTypeFromWeenie(uint deviceWeenieClassId)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(deviceWeenieClassId);
            var n = weenie?.GetProperty(PropertyString.Name);
            if (string.IsNullOrWhiteSpace(n))
                return null;

            n = n.TrimStart();
            var sp = n.IndexOf(' ');
            var lead = sp > 0 ? n[..sp] : n;
            return TryMatchEssenceDamageLeadWordToDamageType(lead);
        }

        /// <summary>
        /// Resolves <see cref="PropertyInt.CapturedSourceDamageType"/> on the device, or the matrix template default from
        /// <see cref="WorldObject.WeenieClassId"/> when unset (e.g. after reskin to an unarmed appearance).
        /// </summary>
        public static DamageType? TryResolveCapturedSourceDamageTypeForCombatPet(PetDevice device)
        {
            if (device == null || !device.IsCombatPetDevice())
                return null;

            var capDt = device.GetProperty(PropertyInt.CapturedSourceDamageType);
            if (capDt.HasValue && capDt.Value != 0 && Enum.IsDefined(typeof(DamageType), capDt.Value))
                return (DamageType)capDt.Value;

            return TryGetTemplateEssenceDamageTypeFromWeenie(device.WeenieClassId);
        }

        /// <summary>
        /// Primary physical/elemental damage type from the creature's wielded weapon(s), for essence naming.
        /// Mirrors <see cref="CombatPet.Init"/> capture-source override for melee vs missile ammo rules.
        /// When unarmed and <c>pet_apply_capture_source_damage_type</c> is on, falls back to stored/template damage on the device.
        /// </summary>
        public static DamageType? TryGetPrimaryWeaponDamageTypeForDisplay(Creature creature, PetDevice device)
        {
            if (creature == null)
                return null;

            var melee = creature.GetEquippedMeleeWeapon();
            var launcher = creature.GetEquippedMissileWeapon();
            var ammo = creature.GetMissileAmmo();

            DamageType? raw = null;

            if (melee != null)
                raw = PickPrimaryPhysicalDamageType(melee.W_DamageType);
            else if (launcher != null && ammo != null)
                raw = PickPrimaryPhysicalDamageType(ammo.W_DamageType);
            else if (launcher != null)
                raw = PickPrimaryPhysicalDamageType(launcher.W_DamageType);
            else
            {
                var wand = creature.GetEquippedWand();
                if (wand != null)
                    raw = PickPrimaryPhysicalDamageType(wand.W_DamageType);
            }

            if (raw.HasValue)
            {
                if (device != null && ServerConfig.pet_apply_capture_source_damage_type.Value)
                {
                    var capDt = device.GetProperty(PropertyInt.CapturedSourceDamageType);
                    if (capDt.HasValue && capDt.Value != 0 && Enum.IsDefined(typeof(DamageType), capDt.Value))
                    {
                        var forced = (DamageType)capDt.Value;
                        if (melee != null || launcher == null)
                            return forced;
                    }
                }

                return raw;
            }

            if (device != null && device.IsCombatPetDevice() && ServerConfig.pet_apply_capture_source_damage_type.Value)
                return TryResolveCapturedSourceDamageTypeForCombatPet(device);

            return null;
        }

        /// <summary>
        /// If the name begins with a known damage-type label (<see cref="EssenceNameDamageLeadWords"/> or
        /// <see cref="TryMatchEssenceDamageLeadWordToDamageType"/>), replace that word with
        /// <paramref name="weaponDt"/>'s display label. If the first word is not a known damage lead-in,
        /// returns <paramref name="name"/> unchanged (no insertion).
        /// </summary>
        private static string ReplaceLeadingEssenceDamageLabel(string name, DamageType weaponDt)
        {
            var n = name.TrimStart();
            var sp = n.IndexOf(' ');
            if (sp <= 0)
                return name;

            var lead = n[..sp];
            if (!EssenceNameDamageLeadWords.Contains(lead)
                && !TryMatchEssenceDamageLeadWordToDamageType(lead).HasValue)
                return name;

            var tail = n[sp..];
            return weaponDt.DisplayName() + tail;
        }

        /// <summary>
        /// Updates this combat pet essence's <see cref="WorldObject.Name"/> from the summoned pet's weapons: strips
        /// any legacy <c> [Slash]</c> suffix, then replaces a leading elemental/physical word (Acid, Fire, …) with the
        /// weapon's damage label (Slash, Bludgeon, Lightning, …).
        /// </summary>
        private void RefreshCombatPetEssenceDisplayNameForSummonedPet(CombatPet pet, Player owner)
        {
            if (!IsCombatPetDevice() || pet == null || owner?.Session == null)
                return;

            var stripped = GetDisplayNameWithoutWeaponDamageSuffix(Name ?? "");
            var weaponDt = TryGetPrimaryWeaponDamageTypeForDisplay(pet, this);
            if (!weaponDt.HasValue)
            {
                if (stripped != Name)
                    Name = stripped;
                TryNotifySummonerNameProperty(owner);
                return;
            }

            var rebuilt = ReplaceLeadingEssenceDamageLabel(stripped, weaponDt.Value);
            if (stripped != rebuilt)
                Name = rebuilt;
            else if (stripped != Name)
                Name = stripped;

            TryNotifySummonerNameProperty(owner);
        }

        /// <summary>
        /// After applying a siphoned appearance, re-sync the leading damage word on the combat essence name from
        /// <see cref="TryResolveCapturedSourceDamageTypeForCombatPet"/> (weapon capture or matrix template when unarmed).
        /// </summary>
        public void RefreshCombatPetEssenceDisplayNameAfterSkinApply(Player player)
        {
            if (!IsCombatPetDevice() || player?.Session == null || !ServerConfig.pet_apply_capture_source_damage_type.Value)
                return;

            var stripped = GetDisplayNameWithoutWeaponDamageSuffix(Name ?? "");
            var weaponDt = TryResolveCapturedSourceDamageTypeForCombatPet(this);
            if (!weaponDt.HasValue)
            {
                if (stripped != Name)
                    Name = stripped;
                TryNotifySummonerNameProperty(player);
                return;
            }

            var rebuilt = ReplaceLeadingEssenceDamageLabel(stripped, weaponDt.Value);
            if (stripped != rebuilt)
                Name = rebuilt;
            else if (stripped != Name)
                Name = stripped;

            TryNotifySummonerNameProperty(player);
        }

        public bool? SummonCreature(Player player, uint wcid)
        {
            var wo = WorldObjectFactory.CreateNewWorldObject(wcid);

            if (wo == null)
            {
                log.Error($"{player.Name}.SummonCreature({wcid}) - couldn't find wcid for PetDevice {WeenieClassId} - {WeenieClassName}");
                return false;
            }

            var pet = wo as Pet;

            if (pet == null)
            {
                log.Error($"{player.Name}.SummonCreature({wcid}) - PetDevice {WeenieClassId} - {WeenieClassName} tried to summon {wo.WeenieClassId} - {wo.WeenieClassName} of unknown type {wo.WeenieType}");
                return false;
            }

            // Monster Capture System - Apply visual overrides if set
            if (VisualOverrideSetup.HasValue)
            {
                // Force-clear ALL existing visual properties to prevent base weenie appearance leaking through
                pet.RemoveProperty(PropertyDataId.ClothingBase);
                pet.RemoveProperty(PropertyDataId.PaletteBase);
                pet.RemoveProperty(PropertyInt.PaletteTemplate);
                pet.RemoveProperty(PropertyFloat.Shade);
                
                // Clear existing Biota visual data only if we have captured ObjDesc to apply
                // This prevents visual emptiness if the captured data is missing
                if (HasCapturedObjDesc)
                {
                    pet.Biota.PropertiesAnimPart?.Clear();
                    pet.Biota.PropertiesPalette?.Clear();
                    pet.Biota.PropertiesTextureMap?.Clear();
                }
                
                // Remove ALL equipped items that affect visual appearance (weapons, armor, clothing)
                // This prevents the base pet weenie's equipment from conflicting with captured appearance
                var equipmentToRemove = pet.EquippedObjects.Values
                    .Where(x => x.ItemType == ACE.Entity.Enum.ItemType.Armor || 
                                x.ItemType == ACE.Entity.Enum.ItemType.Clothing ||
                                x.ItemType == ACE.Entity.Enum.ItemType.MeleeWeapon ||
                                x.ItemType == ACE.Entity.Enum.ItemType.MissileWeapon ||
                                x.ItemType == ACE.Entity.Enum.ItemType.Caster ||
                                x.ItemType == ACE.Entity.Enum.ItemType.Jewelry)  // Jewelry can have visual effects
                    .ToList();
                
                foreach (var item in equipmentToRemove)
                {
                    // Directly remove from dictionaries and destroy
                    pet.EquippedObjects.Remove(item.Guid);
                    pet.Inventory.Remove(item.Guid);
                    item.Destroy();
                }
                
                pet.SetupTableId = VisualOverrideSetup.Value;

                if (VisualOverrideMotionTable.HasValue)
                    pet.MotionTableId = VisualOverrideMotionTable.Value;

                if (VisualOverrideCombatTable.HasValue)
                {
                    if (VisualOverrideCombatTable.Value > 0)
                    {
                        pet.CombatTableDID = VisualOverrideCombatTable.Value;
                        pet.GetCombatTable();
                    }
                    else
                        log.Warn($"{nameof(SummonCreature)}: {nameof(VisualOverrideCombatTable)} is 0 for device {Name} ({Guid}) — skipping combat table override.");
                }

                if (VisualOverrideSoundTable.HasValue)
                    pet.SoundTableId = VisualOverrideSoundTable.Value;

                if (VisualOverridePaletteBase.HasValue)
                    pet.PaletteBaseId = VisualOverridePaletteBase.Value;

                if (VisualOverrideClothingBase.HasValue)
                {
                    var clothingBaseId = VisualOverrideClothingBase.Value;
                    if (DatLoader.DatDatabase.IsClothingBaseId(clothingBaseId))
                        pet.ClothingBase = clothingBaseId;
                    else
                        log.Warn($"{nameof(SummonCreature)}: {nameof(VisualOverrideClothingBase)} {clothingBaseId:X8} on device {Name} ({Guid}) is not a clothing DID (0x10xxxxxx); skipping.");
                }
                else
                    pet.RemoveProperty(PropertyDataId.ClothingBase); // Remove inherited ClothingBase if original had none

                if (VisualOverrideIcon.HasValue)
                    pet.IconId = VisualOverrideIcon.Value;

                if (VisualOverridePaletteTemplate.HasValue)
                    pet.PaletteTemplate = VisualOverridePaletteTemplate.Value;

                if (VisualOverrideShade.HasValue)
                    pet.Shade = (float)VisualOverrideShade.Value;

                if (VisualOverrideScale.HasValue)
                    pet.ObjScale = (float)VisualOverrideScale.Value;
                
                // Apply creature name override
                if (!string.IsNullOrEmpty(VisualOverrideName))
                {
                    // Strip ALL existing "Player's" prefixes to get base creature name
                    var baseName = VisualOverrideName;
                    int apostropheIdx;
                    while ((apostropheIdx = baseName.IndexOf("'s ")) > 0)
                    {
                        baseName = baseName.Substring(apostropheIdx + 3);
                    }
                    
                    // Now add only the current owner's name
                    var ownerName = player.Name;
                    //pet.Name = $"{ownerName}'s {baseName}";
                    pet.Name = $"{baseName}";
                }
                
                // Apply creature type (species) override
                if (VisualOverrideCreatureType.HasValue)
                {
                    pet.CreatureType = (ACE.Entity.Enum.CreatureType)VisualOverrideCreatureType.Value;
                }

                // Apply creature variant override (e.g. shiny)
                if (VisualOverrideCreatureVariant.HasValue)
                {
                    pet.CreatureVariant = (ACE.Server.Entity.CreatureVariant)VisualOverrideCreatureVariant.Value;
                }

                // Apply captured ObjDesc (AnimParts, Palettes, Textures) for full humanoid appearance
                // This restores the exact visual appearance captured from the original creature
                if (HasCapturedObjDesc)
                {
                    ApplyCapturedObjDesc(pet);
                }

                // Equip Captured Items (Armor, Weapons, Shield)
                if (!string.IsNullOrEmpty(VisualOverrideCapturedItems))
                {
                    // Calculate scale ratio: how much the pet was shrunk/grown
                    var petScaleRatio = pet.ObjScale ?? 1.0f;
                    
                    var itemEntries = VisualOverrideCapturedItems.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var entry in itemEntries)
                    {
                        // Format: WCID;Scale;Palette;Shade
                        var parts = entry.Split(';');
                        if (uint.TryParse(parts[0], out var itemWcid))
                        {
                            var item = WorldObjectFactory.CreateNewWorldObject(itemWcid);
                            if (item != null)
                            {
                                CombatPet.StripVisualWeaponDamageStats(item);

                                // Apply Visual Properties - scale items by pet's scale ratio
                                if (parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var itemScale))
                                {
                                    // Multiply original item scale by pet scale ratio
                                    var adjustedScale = itemScale * petScaleRatio;
                                    if (System.Math.Abs(adjustedScale - 1.0f) > 0.001f)
                                        item.ObjScale = adjustedScale;
                                }
                                else
                                {
                                    // No original scale stored, just use pet's ratio
                                    if (System.Math.Abs(petScaleRatio - 1.0f) > 0.001f)
                                        item.ObjScale = petScaleRatio;
                                }

                                if (parts.Length > 2 && int.TryParse(parts[2], out var itemPalette) && itemPalette != 0)
                                {
                                    item.PaletteTemplate = itemPalette;
                                }

                                if (parts.Length > 3 && float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var itemShade) && itemShade != 0.0f)
                                {
                                    item.Shade = itemShade;
                                }

                                // Add to pet's inventory first
                                if (pet.TryAddToInventory(item))
                                {
                                    // Make item effectively worthless/bonded so it's not exploited
                                    item.Value = 0; 
                                    
                                    // Try to wield/equip it
                                    // We use TryWieldObject which handles slot logic
                                    pet.TryWieldObject(item, (EquipMask)(item.ValidLocations ?? 0));
                                }
                                else
                                {
                                    item.Destroy();
                                }
                            }
                        }
                    }
                }

            }

            var success = pet.Init(player, this);

            if (success == true)
            {
                if (pet is CombatPet combatPet)
                    RefreshCombatPetEssenceDisplayNameForSummonedPet(combatPet, player);
                else
                    TryNotifySummonerNameProperty(player);
            }
            else
                wo.Destroy();

            return success;
        }

        /// <summary>
        /// Applies captured ObjDesc data (AnimParts, Palettes, Textures) to the pet's Biota.
        /// This restores the exact visual appearance of humanoid creatures including clothing/armor.
        /// </summary>
        private void ApplyCapturedObjDesc(Creature pet)
        {
            // Parse and apply AnimPartChanges: Index:AnimationId,Index:AnimationId,...
            var animPartsStr = CapturedObjDescAnimParts;
            if (!string.IsNullOrEmpty(animPartsStr))
            {
                var animParts = new System.Collections.Generic.List<ACE.Entity.Models.PropertiesAnimPart>();
                foreach (var entry in animPartsStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = entry.Split(':');
                    if (parts.Length >= 2 && 
                        byte.TryParse(parts[0], out var index) && 
                        uint.TryParse(parts[1], out var animId))
                    {
                        animParts.Add(new ACE.Entity.Models.PropertiesAnimPart { Index = index, AnimationId = animId });
                    }
                }
                if (animParts.Count > 0)
                {
                    pet.Biota.PropertiesAnimPart = new System.Collections.Generic.List<ACE.Entity.Models.PropertiesAnimPart>(animParts);
                }
            }

            // Parse and apply SubPalettes: SubPaletteId:Offset:Length,SubPaletteId:Offset:Length,...
            var palettesStr = CapturedObjDescPalettes;
            if (!string.IsNullOrEmpty(palettesStr))
            {
                var palettes = new System.Collections.Generic.List<ACE.Entity.Models.PropertiesPalette>();
                foreach (var entry in palettesStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = entry.Split(':');
                    if (parts.Length >= 3 && 
                        uint.TryParse(parts[0], out var subPaletteId) && 
                        ushort.TryParse(parts[1], out var offset) && 
                        ushort.TryParse(parts[2], out var length))
                    {
                        palettes.Add(new ACE.Entity.Models.PropertiesPalette { SubPaletteId = subPaletteId, Offset = offset, Length = length });
                    }
                }
                if (palettes.Count > 0)
                {
                    pet.Biota.PropertiesPalette = new System.Collections.Generic.List<ACE.Entity.Models.PropertiesPalette>(palettes);
                }
            }

            // Parse and apply TextureChanges: PartIndex:OldTexture:NewTexture,PartIndex:OldTexture:NewTexture,...
            var texturesStr = CapturedObjDescTextures;
            if (!string.IsNullOrEmpty(texturesStr))
            {
                var textures = new System.Collections.Generic.List<ACE.Entity.Models.PropertiesTextureMap>();
                foreach (var entry in texturesStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = entry.Split(':');
                    if (parts.Length >= 3 && 
                        byte.TryParse(parts[0], out var partIndex) && 
                        uint.TryParse(parts[1], out var oldTexture) && 
                        uint.TryParse(parts[2], out var newTexture))
                    {
                        textures.Add(new ACE.Entity.Models.PropertiesTextureMap { PartIndex = partIndex, OldTexture = oldTexture, NewTexture = newTexture });
                    }
                }
                if (textures.Count > 0)
                {
                    pet.Biota.PropertiesTextureMap = new System.Collections.Generic.List<ACE.Entity.Models.PropertiesTextureMap>(textures);
                }
            }
        }

        /// <summary>
        /// Maps an Essence to a WCID to be spawned
        /// </summary>
        public static Dictionary<uint, Tuple<uint, DamageType>> PetDeviceToPetMapping = new Dictionary<uint, Tuple<uint, DamageType>>()
        {
            // ============================ Geomancer ============================

            // ============ Golems ============

            { 48886, new Tuple<uint, DamageType>(48887, DamageType.Bludgeon) }, // mud golem (15)
            { 48890, new Tuple<uint, DamageType>(48891, DamageType.Bludgeon) }, // sandstone golem (30)
            { 48878, new Tuple<uint, DamageType>(48879, DamageType.Bludgeon) }, // copper golem (50)
            { 48888, new Tuple<uint, DamageType>(48889, DamageType.Bludgeon) }, // oak golem (80)
            { 48882, new Tuple<uint, DamageType>(48883, DamageType.Bludgeon) }, // gold golem (100)
            { 48880, new Tuple<uint, DamageType>(48881, DamageType.Bludgeon) }, // coral golem (125)
            { 48884, new Tuple<uint, DamageType>(48885, DamageType.Bludgeon) }, // iron golem (150)

            // ============================ Naturalist ============================

            // ============ Grievvers ============

            { 49366, new Tuple<uint, DamageType>(49052, DamageType.Acid) }, // acid grievver (50)
            { 49367, new Tuple<uint, DamageType>(49053, DamageType.Acid) }, // acid grievver (80)
            { 49368, new Tuple<uint, DamageType>(49054, DamageType.Acid) }, // acid grievver (100)
            { 49369, new Tuple<uint, DamageType>(49055, DamageType.Acid) }, // acid grievver (125)
            { 49370, new Tuple<uint, DamageType>(49056, DamageType.Acid) }, // acid grievver (150)
            { 49371, new Tuple<uint, DamageType>(49057, DamageType.Acid) }, // acid grievver (180)
            { 49372, new Tuple<uint, DamageType>(49058, DamageType.Acid) }, // caustic grievver (200)

            { 49380, new Tuple<uint, DamageType>(49066, DamageType.Fire) }, // fire grievver (50)
            { 49381, new Tuple<uint, DamageType>(49067, DamageType.Fire) }, // fire grievver (80)
            { 49382, new Tuple<uint, DamageType>(49068, DamageType.Fire) }, // fire grievver (100)
            { 49383, new Tuple<uint, DamageType>(49069, DamageType.Fire) }, // fire grievver (125)
            { 49384, new Tuple<uint, DamageType>(49070, DamageType.Fire) }, // fire grievver (150)
            { 49385, new Tuple<uint, DamageType>(49071, DamageType.Fire) }, // fire grievver (180)
            { 49386, new Tuple<uint, DamageType>(49072, DamageType.Fire) }, // scorched grievver (200)

            { 49387, new Tuple<uint, DamageType>(49073, DamageType.Cold) }, // frost grievver (50)
            { 49388, new Tuple<uint, DamageType>(49074, DamageType.Cold) }, // frost grievver (80)
            { 49389, new Tuple<uint, DamageType>(49075, DamageType.Cold) }, // frost grievver (100)
            { 49390, new Tuple<uint, DamageType>(49076, DamageType.Cold) }, // frost grievver (125)
            { 49391, new Tuple<uint, DamageType>(49077, DamageType.Cold) }, // frost grievver (150)
            { 49392, new Tuple<uint, DamageType>(49078, DamageType.Cold) }, // frost grievver (180)
            { 49365, new Tuple<uint, DamageType>(49051, DamageType.Cold) }, // arctic grievver (200)
            //{ -1, -1 }, // glacial grievver (200) ?

            { 49373, new Tuple<uint, DamageType>(49059, DamageType.Electric) }, // lightning grievver (50)
            { 49374, new Tuple<uint, DamageType>(49060, DamageType.Electric) }, // lightning grievver (80)
            { 49375, new Tuple<uint, DamageType>(49061, DamageType.Electric) }, // lightning grievver (100)
            { 49376, new Tuple<uint, DamageType>(49062, DamageType.Electric) }, // lightning grievver (125)
            { 49377, new Tuple<uint, DamageType>(49063, DamageType.Electric) }, // lightning grievver (150)
            { 49378, new Tuple<uint, DamageType>(49064, DamageType.Electric) }, // lightning grievver (180)
            { 49379, new Tuple<uint, DamageType>(49065, DamageType.Electric) }, // excited grievver (200)

            // ============ Moars ============

            { 49338, new Tuple<uint, DamageType>(49108, DamageType.Acid) }, // acid moar (50)
            { 49339, new Tuple<uint, DamageType>(49109, DamageType.Acid) }, // acid moar (80)
            { 49340, new Tuple<uint, DamageType>(49110, DamageType.Acid) }, // acid moar (100)
            { 49341, new Tuple<uint, DamageType>(49111, DamageType.Acid) }, // acid moar (125)
            { 49342, new Tuple<uint, DamageType>(49112, DamageType.Acid) }, // acid moar (150)
            { 49343, new Tuple<uint, DamageType>(49113, DamageType.Acid) }, // acid moar (180)
            { 49344, new Tuple<uint, DamageType>(49114, DamageType.Acid) }, // blistering moar (200)
            //{ 49344, 49114 },   // blistering moar (200)

            { 49352, new Tuple<uint, DamageType>(49122, DamageType.Fire) }, // fire moar (50)
            { 49353, new Tuple<uint, DamageType>(49123, DamageType.Fire) }, // fire moar (80)
            { 49354, new Tuple<uint, DamageType>(49124, DamageType.Fire) }, // fire moar (100)
            { 49355, new Tuple<uint, DamageType>(49125, DamageType.Fire) }, // fire moar (125)
            { 49356, new Tuple<uint, DamageType>(49126, DamageType.Fire) }, // fire moar (150)
            { 49357, new Tuple<uint, DamageType>(49127, DamageType.Fire) }, // fire moar (180)
            { 49358, new Tuple<uint, DamageType>(49128, DamageType.Fire) }, // volcanic moar (200)

            { 49359, new Tuple<uint, DamageType>(49129, DamageType.Cold) }, // frost moar (50)
            { 49360, new Tuple<uint, DamageType>(49130, DamageType.Cold) }, // frost moar (80)
            { 49361, new Tuple<uint, DamageType>(49131, DamageType.Cold) }, // frost moar (100)
            { 49362, new Tuple<uint, DamageType>(49132, DamageType.Cold) }, // frost moar (125)
            { 49363, new Tuple<uint, DamageType>(49133, DamageType.Cold) }, // frost moar (150)
            { 49364, new Tuple<uint, DamageType>(49134, DamageType.Cold) }, // frost moar (180)
            { 49337, new Tuple<uint, DamageType>(49107, DamageType.Cold) }, // freezing moar (200)

            { 49345, new Tuple<uint, DamageType>(49115, DamageType.Electric) }, // lightning moar (50)
            { 49346, new Tuple<uint, DamageType>(49116, DamageType.Electric) }, // lightning moar (80)
            { 49347, new Tuple<uint, DamageType>(49117, DamageType.Electric) }, // lightning moar (100)
            { 49348, new Tuple<uint, DamageType>(49118, DamageType.Electric) }, // lightning moar (125)
            { 49349, new Tuple<uint, DamageType>(49119, DamageType.Electric) }, // lightning moar (150)
            { 49350, new Tuple<uint, DamageType>(49120, DamageType.Electric) }, // lightning moar (180)
            { 49351, new Tuple<uint, DamageType>(49121, DamageType.Electric) }, // electrified moar (200)

            // ============ Phyntos Wasps ============

            { 49524, new Tuple<uint, DamageType>(49136, DamageType.Acid) }, // acid phyntos wasp (50)
            { 49525, new Tuple<uint, DamageType>(49137, DamageType.Acid) }, // acid phyntos wasp (80)
            { 49526, new Tuple<uint, DamageType>(49138, DamageType.Acid) }, // acid phyntos wasp (100)
            { 49527, new Tuple<uint, DamageType>(49139, DamageType.Acid) }, // acid phyntos wasp (125)
            { 49528, new Tuple<uint, DamageType>(49140, DamageType.Acid) }, // acid phyntos wasp (150)
            { 49529, new Tuple<uint, DamageType>(49141, DamageType.Acid) }, // acid phyntos wasp (180)
            { 49530, new Tuple<uint, DamageType>(49142, DamageType.Acid) }, // acid phyntos swarm (200)

            { 49531, new Tuple<uint, DamageType>(49143, DamageType.Fire) }, // fire phyntos wasp (50)
            { 49532, new Tuple<uint, DamageType>(49144, DamageType.Fire) }, // fire phyntos wasp (80)
            { 49533, new Tuple<uint, DamageType>(49145, DamageType.Fire) }, // fire phyntos wasp (100)
            { 49534, new Tuple<uint, DamageType>(49146, DamageType.Fire) }, // fire phyntos wasp (125)
            { 49535, new Tuple<uint, DamageType>(49147, DamageType.Fire) }, // fire phyntos wasp (150)
            { 49536, new Tuple<uint, DamageType>(49148, DamageType.Fire) }, // fire phyntos wasp (180)
            { 49537, new Tuple<uint, DamageType>(49149, DamageType.Fire) }, // fire phyntos swarm (200)

            { 49538, new Tuple<uint, DamageType>(49150, DamageType.Cold) }, // frost phyntos wasp (50)
            { 49539, new Tuple<uint, DamageType>(49151, DamageType.Cold) }, // frost phyntos wasp (80)
            { 49540, new Tuple<uint, DamageType>(49152, DamageType.Cold) }, // frost phyntos wasp (100)
            { 49541, new Tuple<uint, DamageType>(49153, DamageType.Cold) }, // frost phyntos wasp (125)
            { 49542, new Tuple<uint, DamageType>(49154, DamageType.Cold) }, // frost phyntos wasp (150)
            { 49543, new Tuple<uint, DamageType>(49155, DamageType.Cold) }, // frost phyntos wasp (180)
            { 49544, new Tuple<uint, DamageType>(49156, DamageType.Cold) }, // frost phyntos swarm (200)

            { 49545, new Tuple<uint, DamageType>(49157, DamageType.Electric) }, // lightning phyntos wasp (50)
            { 49546, new Tuple<uint, DamageType>(49158, DamageType.Electric) }, // lightning phyntos wasp (80)
            { 49547, new Tuple<uint, DamageType>(49159, DamageType.Electric) }, // lightning phyntos wasp (100)
            { 49548, new Tuple<uint, DamageType>(49160, DamageType.Electric) }, // lightning phyntos wasp (125)
            { 49549, new Tuple<uint, DamageType>(49161, DamageType.Electric) }, // lightning phyntos wasp (150)
            { 49550, new Tuple<uint, DamageType>(49162, DamageType.Electric) }, // lightning phyntos wasp (180)
            { 49551, new Tuple<uint, DamageType>(49135, DamageType.Electric) }, // lightning phyntos swarm (200)

            // ============================ Necromancer ============================

            // ============ Skeletons ============

            { 49213, new Tuple<uint, DamageType>(49164, DamageType.Acid) }, // acid skeleton minion (50)
            { 49214, new Tuple<uint, DamageType>(49165, DamageType.Acid) }, // acid skeleton minion (80)
            { 49215, new Tuple<uint, DamageType>(49166, DamageType.Acid) }, // acid skeleton minion (100)
            { 49216, new Tuple<uint, DamageType>(49167, DamageType.Acid) }, // acid skeleton bushi (125)
            { 49217, new Tuple<uint, DamageType>(49168, DamageType.Acid) }, // acid skeleton bushi (150)
            { 49218, new Tuple<uint, DamageType>(49169, DamageType.Acid) }, // acid skeleton bushi (180)
            { 49219, new Tuple<uint, DamageType>(49163, DamageType.Acid) }, // acid skeleton samurai (200)

            { 48942, new Tuple<uint, DamageType>(48943, DamageType.Fire) }, // fire skeleton minion (50)
            { 48944, new Tuple<uint, DamageType>(48950, DamageType.Fire) }, // fire skeleton minion (80)
            { 48945, new Tuple<uint, DamageType>(48951, DamageType.Fire) }, // fire skeleton minion (100)
            { 48946, new Tuple<uint, DamageType>(48952, DamageType.Fire) }, // fire skeleton bushi (125)
            { 48947, new Tuple<uint, DamageType>(48953, DamageType.Fire) }, // fire skeleton bushi (150)
            { 48948, new Tuple<uint, DamageType>(48949, DamageType.Fire) }, // fire skeleton bushi (180)
            { 48956, new Tuple<uint, DamageType>(48955, DamageType.Fire) }, // fire skeleton samurai (200)

            { 49227, new Tuple<uint, DamageType>(49178, DamageType.Cold) }, // frost skeleton minion (50)
            { 49228, new Tuple<uint, DamageType>(49179, DamageType.Cold) }, // frost skeleton minion (80)
            { 49229, new Tuple<uint, DamageType>(49180, DamageType.Cold) }, // frost skeleton minion (100)
            { 49230, new Tuple<uint, DamageType>(49181, DamageType.Cold) }, // frost skeleton bushi (125)
            { 49231, new Tuple<uint, DamageType>(49182, DamageType.Cold) }, // frost skeleton bushi (150)
            { 49232, new Tuple<uint, DamageType>(49183, DamageType.Cold) }, // frost skeleton bushi (180)
            { 49212, new Tuple<uint, DamageType>(49177, DamageType.Cold) }, // frost skeleton samurai (200)

            { 49220, new Tuple<uint, DamageType>(49171, DamageType.Electric) }, // lightning skeleton minion (50)
            { 49221, new Tuple<uint, DamageType>(49172, DamageType.Electric) }, // lightning skeleton minion (80)
            { 49222, new Tuple<uint, DamageType>(49173, DamageType.Electric) }, // lightning skeleton minion (100)
            { 49223, new Tuple<uint, DamageType>(49174, DamageType.Electric) }, // lightning skeleton bushi (125)
            { 49224, new Tuple<uint, DamageType>(49175, DamageType.Electric) }, // lightning skeleton bushi (150)
            { 49225, new Tuple<uint, DamageType>(49176, DamageType.Electric) }, // lightning skeleton bushi (180)
            { 49226, new Tuple<uint, DamageType>(49170, DamageType.Electric) }, // lightning skeleton samurai (200)

            // ============ Spectres ============

            { 49421, new Tuple<uint, DamageType>(49394, DamageType.Acid) }, // acid spectre (50)
            { 49422, new Tuple<uint, DamageType>(49395, DamageType.Acid) }, // acid spectre (80)
            { 49423, new Tuple<uint, DamageType>(49396, DamageType.Acid) }, // acid spectre (100)
            { 49424, new Tuple<uint, DamageType>(49397, DamageType.Acid) }, // acid spectre (125)
            { 49425, new Tuple<uint, DamageType>(49398, DamageType.Acid) }, // acid spectre (150)
            { 49426, new Tuple<uint, DamageType>(49399, DamageType.Acid) }, // acid spectre (180)
            { 49427, new Tuple<uint, DamageType>(49393, DamageType.Acid) }, // acid maiden (200)

            { 49435, new Tuple<uint, DamageType>(49408, DamageType.Fire) }, // fire spectre (50)
            { 49436, new Tuple<uint, DamageType>(49409, DamageType.Fire) }, // fire spectre (80)
            { 49437, new Tuple<uint, DamageType>(49410, DamageType.Fire) }, // fire spectre (100)
            { 49438, new Tuple<uint, DamageType>(49411, DamageType.Fire) }, // fire spectre (125)
            { 49439, new Tuple<uint, DamageType>(49412, DamageType.Fire) }, // fire spectre (150)
            { 49440, new Tuple<uint, DamageType>(49413, DamageType.Fire) }, // fire spectre (180)
            { 49441, new Tuple<uint, DamageType>(49407, DamageType.Fire) }, // fire maiden (200)

            { 49442, new Tuple<uint, DamageType>(49415, DamageType.Cold) }, // frost spectre (50)
            { 49443, new Tuple<uint, DamageType>(49416, DamageType.Cold) }, // frost spectre (80)
            { 49444, new Tuple<uint, DamageType>(49417, DamageType.Cold) }, // frost spectre (100)
            { 49445, new Tuple<uint, DamageType>(49418, DamageType.Cold) }, // frost spectre (125)
            { 49446, new Tuple<uint, DamageType>(49419, DamageType.Cold) }, // frost spectre (150)
            { 49447, new Tuple<uint, DamageType>(49420, DamageType.Cold) }, // frost spectre (180)
            { 49448, new Tuple<uint, DamageType>(49414, DamageType.Cold) }, // frost maiden (200)

            { 49428, new Tuple<uint, DamageType>(49401, DamageType.Electric) }, // lightning spectre (50)
            { 49429, new Tuple<uint, DamageType>(49402, DamageType.Electric) }, // lightning spectre (80)
            { 49430, new Tuple<uint, DamageType>(49403, DamageType.Electric) }, // lightning spectre (100)
            { 49431, new Tuple<uint, DamageType>(49404, DamageType.Electric) }, // lightning spectre (125)
            { 49432, new Tuple<uint, DamageType>(49405, DamageType.Electric) }, // lightning spectre (150)
            { 49433, new Tuple<uint, DamageType>(49406, DamageType.Electric) }, // lightning spectre (180)
            { 49434, new Tuple<uint, DamageType>(49400, DamageType.Electric) }, // lightning maiden (200)

            // ============ Zombies ============

            { 48972, new Tuple<uint, DamageType>(49000, DamageType.Acid) }, // acid zombie (50)
            { 49234, new Tuple<uint, DamageType>(49003, DamageType.Acid) }, // acid zombie (80)
            { 49235, new Tuple<uint, DamageType>(49004, DamageType.Acid) }, // acid zombie (100)
            { 49236, new Tuple<uint, DamageType>(49005, DamageType.Acid) }, // acid zombie (125)
            { 49237, new Tuple<uint, DamageType>(49006, DamageType.Acid) }, // acid zombie (150)
            { 49238, new Tuple<uint, DamageType>(49007, DamageType.Acid) }, // acid zombie (180)
            { 49239, new Tuple<uint, DamageType>(49008, DamageType.Acid) }, // blistered zombie (200)

            { 49247, new Tuple<uint, DamageType>(49016, DamageType.Fire) }, // fire zombie (50)
            { 49248, new Tuple<uint, DamageType>(49017, DamageType.Fire) }, // fire zombie (80)
            { 49249, new Tuple<uint, DamageType>(49018, DamageType.Fire) }, // fire zombie (100)
            { 49250, new Tuple<uint, DamageType>(49019, DamageType.Fire) }, // fire zombie (125)
            { 49251, new Tuple<uint, DamageType>(49020, DamageType.Fire) }, // fire zombie (150)
            { 49252, new Tuple<uint, DamageType>(49021, DamageType.Fire) }, // fire zombie (180)
            { 49253, new Tuple<uint, DamageType>(49022, DamageType.Fire) }, // charred zombie (200)

            { 49254, new Tuple<uint, DamageType>(49023, DamageType.Cold) }, // frost zombie (50)
            { 49255, new Tuple<uint, DamageType>(49024, DamageType.Cold) }, // frost zombie (80)
            { 49256, new Tuple<uint, DamageType>(49025, DamageType.Cold) }, // frost zombie (100)
            { 49257, new Tuple<uint, DamageType>(49026, DamageType.Cold) }, // frost zombie (125)
            { 49258, new Tuple<uint, DamageType>(49027, DamageType.Cold) }, // frost zombie (150)
            { 49259, new Tuple<uint, DamageType>(49028, DamageType.Cold) }, // frost zombie (180)
            { 49233, new Tuple<uint, DamageType>(49029, DamageType.Cold) }, // frigid zombie (200)

            { 49240, new Tuple<uint, DamageType>(49009, DamageType.Electric) }, // lightning zombie (50)
            { 49241, new Tuple<uint, DamageType>(49010, DamageType.Electric) }, // lightning zombie (80)
            { 49242, new Tuple<uint, DamageType>(49011, DamageType.Electric) }, // lightning zombie (100)
            { 49243, new Tuple<uint, DamageType>(49012, DamageType.Electric) }, // lightning zombie (125)
            { 49244, new Tuple<uint, DamageType>(49013, DamageType.Electric) }, // lightning zombie (150)
            { 49245, new Tuple<uint, DamageType>(49014, DamageType.Electric) }, // lightning zombie (180)
            { 49246, new Tuple<uint, DamageType>(49015, DamageType.Electric) }, // shocked zombie (200)

            // // ============================ Primalist ============================

            // ============ Elementals ============

            { 49261, new Tuple<uint, DamageType>(49031, DamageType.Acid) }, // acid elemental (50)
            { 49262, new Tuple<uint, DamageType>(49032, DamageType.Acid) }, // acid elemental (80)
            { 49263, new Tuple<uint, DamageType>(49033, DamageType.Acid) }, // acid elemental (100)
            { 49264, new Tuple<uint, DamageType>(49034, DamageType.Acid) }, // acid child (125)
            { 49265, new Tuple<uint, DamageType>(49035, DamageType.Acid) }, // acid child (150)
            { 49266, new Tuple<uint, DamageType>(49036, DamageType.Acid) }, // acid child (180)
            { 49267, new Tuple<uint, DamageType>(49030, DamageType.Acid) }, // caustic knight (200)

            { 48959, new Tuple<uint, DamageType>(48960, DamageType.Fire) }, // fire elemental (50)
            { 48961, new Tuple<uint, DamageType>(48962, DamageType.Fire) }, // fire elemental (80)
            { 48963, new Tuple<uint, DamageType>(48964, DamageType.Fire) }, // fire elemental (100)
            { 48965, new Tuple<uint, DamageType>(48966, DamageType.Fire) }, // fire child (125)
            { 48967, new Tuple<uint, DamageType>(48968, DamageType.Fire) }, // fire child (150)
            { 48969, new Tuple<uint, DamageType>(48970, DamageType.Fire) }, // fire child (180)
            { 48957, new Tuple<uint, DamageType>(48958, DamageType.Fire) }, // incendiary knight (200)
            //{ 0, new Tuple<uint, DamageType>(0, DamageType.Fire) }, // scorched knight (200) ?

            { 49275, new Tuple<uint, DamageType>(49045, DamageType.Cold) }, // frost elemental (50)
            { 49276, new Tuple<uint, DamageType>(49046, DamageType.Cold) }, // frost elemental (80)
            { 49277, new Tuple<uint, DamageType>(49047, DamageType.Cold) }, // frost elemental (100)
            { 49278, new Tuple<uint, DamageType>(49048, DamageType.Cold) }, // frost child (125)
            { 49279, new Tuple<uint, DamageType>(49049, DamageType.Cold) }, // frost child (150)
            { 49280, new Tuple<uint, DamageType>(49050, DamageType.Cold) }, // frost child (180)
            { 49260, new Tuple<uint, DamageType>(49044, DamageType.Cold) }, // glacial knight (200)

            { 49268, new Tuple<uint, DamageType>(49038, DamageType.Electric) }, // lightning elemental (50)
            { 49269, new Tuple<uint, DamageType>(49039, DamageType.Electric) }, // lightning elemental (80)
            { 49270, new Tuple<uint, DamageType>(49040, DamageType.Electric) }, // lightning elemental (100)
            { 49271, new Tuple<uint, DamageType>(49041, DamageType.Electric) }, // lightning child (125)
            { 49272, new Tuple<uint, DamageType>(49042, DamageType.Electric) }, // lightning child (150)
            { 49273, new Tuple<uint, DamageType>(49043, DamageType.Electric) }, // lightning child (180)
            { 49274, new Tuple<uint, DamageType>(49037, DamageType.Electric) }, // galvanic knight (200)

            // ============ K'naths ============

            { 49282, new Tuple<uint, DamageType>(49080, DamageType.Acid) }, // acid k'nath (50)
            { 49283, new Tuple<uint, DamageType>(49081, DamageType.Acid) }, // acid k'nath (80)
            { 49284, new Tuple<uint, DamageType>(49082, DamageType.Acid) }, // acid k'nath (100)
            { 49285, new Tuple<uint, DamageType>(49083, DamageType.Acid) }, // acid k'nath (125)
            { 49286, new Tuple<uint, DamageType>(49084, DamageType.Acid) }, // acid k'nath (150)
            { 49287, new Tuple<uint, DamageType>(49085, DamageType.Acid) }, // acid k'nath (180)
            { 49288, new Tuple<uint, DamageType>(49086, DamageType.Acid) }, // k'nath y'nda (200)

            { 49296, new Tuple<uint, DamageType>(49094, DamageType.Fire) }, // fire k'nath (50)
            { 49297, new Tuple<uint, DamageType>(49095, DamageType.Fire) }, // fire k'nath (80)
            { 49298, new Tuple<uint, DamageType>(49096, DamageType.Fire) }, // fire k'nath (100)
            { 49299, new Tuple<uint, DamageType>(49097, DamageType.Fire) }, // fire k'nath (125)
            { 49300, new Tuple<uint, DamageType>(49098, DamageType.Fire) }, // fire k'nath (150)
            { 49301, new Tuple<uint, DamageType>(49099, DamageType.Fire) }, // fire k'nath (180)
            { 49302, new Tuple<uint, DamageType>(49100, DamageType.Fire) }, // k'nath b'orret (200)

            { 49303, new Tuple<uint, DamageType>(49101, DamageType.Cold) }, // frost k'nath (50)
            { 49304, new Tuple<uint, DamageType>(49102, DamageType.Cold) }, // frost k'nath (80)
            { 49305, new Tuple<uint, DamageType>(49103, DamageType.Cold) }, // frost k'nath (100)
            { 49306, new Tuple<uint, DamageType>(49104, DamageType.Cold) }, // frost k'nath (125)
            { 49307, new Tuple<uint, DamageType>(49105, DamageType.Cold) }, // frost k'nath (150)
            { 49308, new Tuple<uint, DamageType>(49106, DamageType.Cold) }, // frost k'nath (180)
            { 49281, new Tuple<uint, DamageType>(49079, DamageType.Cold) }, // k'nath r'ajed (200)

            { 49289, new Tuple<uint, DamageType>(49087, DamageType.Electric) }, // lightning k'nath (50)
            { 49290, new Tuple<uint, DamageType>(49088, DamageType.Electric) }, // lightning k'nath (80)
            { 49291, new Tuple<uint, DamageType>(49089, DamageType.Electric) }, // lightning k'nath (100)
            { 49292, new Tuple<uint, DamageType>(49090, DamageType.Electric) }, // lightning k'nath (125)
            { 49293, new Tuple<uint, DamageType>(49091, DamageType.Electric) }, // lightning k'nath (150)
            { 49294, new Tuple<uint, DamageType>(49092, DamageType.Electric) }, // lightning k'nath (180)
            { 49295, new Tuple<uint, DamageType>(49093, DamageType.Electric) }, // k'nath t'soct (200)

            // ============ Wisps ============

            { 49310, new Tuple<uint, DamageType>(49185, DamageType.Acid) }, // acid wisp (50)
            { 49311, new Tuple<uint, DamageType>(49186, DamageType.Acid) }, // acid wisp (80)
            { 49312, new Tuple<uint, DamageType>(49187, DamageType.Acid) }, // acid wisp (100)
            { 49313, new Tuple<uint, DamageType>(49188, DamageType.Acid) }, // acid wisp (125)
            { 49314, new Tuple<uint, DamageType>(49189, DamageType.Acid) }, // acid wisp (150)
            { 49315, new Tuple<uint, DamageType>(49190, DamageType.Acid) }, // acid wisp (180)
            { 49316, new Tuple<uint, DamageType>(49191, DamageType.Acid) }, // corrosion wisp (200)

            { 49324, new Tuple<uint, DamageType>(49199, DamageType.Fire) }, // fire wisp (50)
            { 49325, new Tuple<uint, DamageType>(49200, DamageType.Fire) }, // fire wisp (80)
            { 49326, new Tuple<uint, DamageType>(49201, DamageType.Fire) }, // fire wisp (100)
            { 49327, new Tuple<uint, DamageType>(49202, DamageType.Fire) }, // fire wisp (125)
            { 49328, new Tuple<uint, DamageType>(49203, DamageType.Fire) }, // fire wisp (150)
            { 49329, new Tuple<uint, DamageType>(49204, DamageType.Fire) }, // fire wisp (180)
            { 49330, new Tuple<uint, DamageType>(49205, DamageType.Fire) }, // incendiary wisp (200)

            { 49331, new Tuple<uint, DamageType>(49206, DamageType.Cold) }, // frost wisp (50)
            { 49332, new Tuple<uint, DamageType>(49207, DamageType.Cold) }, // frost wisp (80)
            { 49333, new Tuple<uint, DamageType>(49208, DamageType.Cold) }, // frost wisp (100)
            { 49334, new Tuple<uint, DamageType>(49209, DamageType.Cold) }, // frost wisp (125)
            { 49335, new Tuple<uint, DamageType>(49210, DamageType.Cold) }, // frost wisp (150)
            { 49336, new Tuple<uint, DamageType>(49211, DamageType.Cold) }, // frost wisp (180)
            { 49309, new Tuple<uint, DamageType>(49184, DamageType.Cold) }, // blizzard wisp (200)

            { 49317, new Tuple<uint, DamageType>(49192, DamageType.Electric) }, // lightning wisp (50)
            { 49318, new Tuple<uint, DamageType>(49193, DamageType.Electric) }, // lightning wisp (80)
            { 49319, new Tuple<uint, DamageType>(49194, DamageType.Electric) }, // lightning wisp (100)
            { 49320, new Tuple<uint, DamageType>(49195, DamageType.Electric) }, // lightning wisp (125)
            { 49321, new Tuple<uint, DamageType>(49196, DamageType.Electric) }, // lightning wisp (150)
            { 49322, new Tuple<uint, DamageType>(49197, DamageType.Electric) }, // lightning wisp (180)
            { 49323, new Tuple<uint, DamageType>(49198, DamageType.Electric) }, // voltaic wisp (200)

            { 787801001, new Tuple<uint, DamageType>(787802001, DamageType.Fire) }, // fireskeletonsamuraiessence (250)
            { 787801037, new Tuple<uint, DamageType>(787802037, DamageType.Fire) }, // fireskeletonsamuraiessence (300)
            { 787801002, new Tuple<uint, DamageType>(787802002, DamageType.Acid) }, // acidskeletonsamuraiessence (250)
            { 787801038, new Tuple<uint, DamageType>(787802038, DamageType.Acid) }, // acidskeletonsamuraiessence (300)
            { 787801003, new Tuple<uint, DamageType>(787802003, DamageType.Electric) }, // lightningskeletonsamuraiessence (250)
            { 787801039, new Tuple<uint, DamageType>(787802039, DamageType.Electric) }, // lightningskeletonsamuraiessence (300)
            { 787801004, new Tuple<uint, DamageType>(787802004, DamageType.Cold) }, // frostskeletonsamuraiessence (250)
            { 787801040, new Tuple<uint, DamageType>(787802040, DamageType.Cold) }, // frostskeletonsamuraiessence (300)
            { 787801005, new Tuple<uint, DamageType>(787802005, DamageType.Acid) }, // blisteredzombieessence (250)
            { 787801041, new Tuple<uint, DamageType>(787802041, DamageType.Acid) }, // blisteredzombieessence (300)
            { 787801006, new Tuple<uint, DamageType>(787802006, DamageType.Electric) }, // shockedzombieessence (250)
            { 787801042, new Tuple<uint, DamageType>(787802042, DamageType.Electric) }, // shockedzombieessence (300)
            { 787801007, new Tuple<uint, DamageType>(787802007, DamageType.Fire) }, // charredzombieessence (250)
            { 787801043, new Tuple<uint, DamageType>(787802043, DamageType.Fire) }, // charredzombieessence (300)
            { 787801008, new Tuple<uint, DamageType>(787802008, DamageType.Cold) }, // frigidzombieessence (250)
            { 787801044, new Tuple<uint, DamageType>(787802044, DamageType.Cold) }, // frigidzombieessence (300)
            { 787801009, new Tuple<uint, DamageType>(787802009, DamageType.Acid) }, // acidmaidenessence (250)
            { 787801045, new Tuple<uint, DamageType>(787802045, DamageType.Acid) }, // acidmaidenessence (300)
            { 787801010, new Tuple<uint, DamageType>(787802010, DamageType.Electric) }, // lightningmaidenessence (250)
            { 787801046, new Tuple<uint, DamageType>(787802046, DamageType.Electric) }, // lightningmaidenessence (300)
            { 787801011, new Tuple<uint, DamageType>(787802011, DamageType.Fire) }, // firemaidenessence (250)
            { 787801047, new Tuple<uint, DamageType>(787802047, DamageType.Fire) }, // firemaidenessence (300)
            { 787801012, new Tuple<uint, DamageType>(787802012, DamageType.Cold) }, // frostmaidenessence (250)
            { 787801048, new Tuple<uint, DamageType>(787802048, DamageType.Cold) }, // frostmaidenessence (300)
            { 787801013, new Tuple<uint, DamageType>(787802013, DamageType.Fire) }, // incendiaryknightessence (250)
            { 787801049, new Tuple<uint, DamageType>(787802049, DamageType.Fire) }, // incendiaryknightessence (300)
            { 787801014, new Tuple<uint, DamageType>(787802014, DamageType.Acid) }, // causticknightessence (250)
            { 787801050, new Tuple<uint, DamageType>(787802050, DamageType.Acid) }, // causticknightessence (300)
            { 787801015, new Tuple<uint, DamageType>(787802015, DamageType.Electric) }, // galvanicknightessence (250)
            { 787801051, new Tuple<uint, DamageType>(787802051, DamageType.Electric) }, // galvanicknightessence (300)
            { 787801016, new Tuple<uint, DamageType>(787802016, DamageType.Cold) }, // glacialknightessence (250)
            { 787801052, new Tuple<uint, DamageType>(787802052, DamageType.Cold) }, // glacialknightessence (300)
            { 787801017, new Tuple<uint, DamageType>(787802017, DamageType.Cold) }, // knathrajedessence (250)
            { 787801053, new Tuple<uint, DamageType>(787802053, DamageType.Cold) }, // knathrajedessence (300)
            { 787801018, new Tuple<uint, DamageType>(787802018, DamageType.Acid) }, // knathyndaessence (250)
            { 787801054, new Tuple<uint, DamageType>(787802054, DamageType.Acid) }, // knathyndaessence (300)
            { 787801019, new Tuple<uint, DamageType>(787802019, DamageType.Electric) }, // knathtsoctessence (250)
            { 787801055, new Tuple<uint, DamageType>(787802055, DamageType.Electric) }, // knathtsoctessence (300)
            { 787801020, new Tuple<uint, DamageType>(787802020, DamageType.Fire) }, // knathborretessence (250)
            { 787801056, new Tuple<uint, DamageType>(787802056, DamageType.Fire) }, // knathborretessence (300)
            { 787801021, new Tuple<uint, DamageType>(787802021, DamageType.Acid) }, // corrosionwispessence (250)
            { 787801057, new Tuple<uint, DamageType>(787802057, DamageType.Acid) }, // corrosionwispessence (300)
            { 787801022, new Tuple<uint, DamageType>(787802022, DamageType.Electric) }, // voltiacwispessence (250)
            { 787801058, new Tuple<uint, DamageType>(787802058, DamageType.Electric) }, // voltiacwispessence (300)
            { 787801023, new Tuple<uint, DamageType>(787802023, DamageType.Fire) }, // incendiarywispessence (250)
            { 787801059, new Tuple<uint, DamageType>(787802059, DamageType.Fire) }, // incendiarywispessence (300)
            { 787801024, new Tuple<uint, DamageType>(787802024, DamageType.Cold) }, // blizzardwispessence (250)
            { 787801060, new Tuple<uint, DamageType>(787802060, DamageType.Cold) }, // blizzardwispessence (300)
            { 787801025, new Tuple<uint, DamageType>(787802025, DamageType.Acid) }, // blisteringmoaressence (250)
            { 787801061, new Tuple<uint, DamageType>(787802061, DamageType.Acid) }, // blisteringmoaressence (300)
            { 787801026, new Tuple<uint, DamageType>(787802026, DamageType.Electric) }, // electrifiedmoaressence (250)
            { 787801062, new Tuple<uint, DamageType>(787802062, DamageType.Electric) }, // electrifiedmoaressence (300)
            { 787801027, new Tuple<uint, DamageType>(787802027, DamageType.Fire) }, // volcanicmoaressence (250)
            { 787801063, new Tuple<uint, DamageType>(787802063, DamageType.Fire) }, // volcanicmoaressence (300)
            { 787801028, new Tuple<uint, DamageType>(787802028, DamageType.Cold) }, // freezingmoaressence (250)
            { 787801064, new Tuple<uint, DamageType>(787802064, DamageType.Cold) }, // freezingmoaressence (300)
            { 787801029, new Tuple<uint, DamageType>(787802029, DamageType.Acid) }, // causticgrievveressence (250)
            { 787801065, new Tuple<uint, DamageType>(787802065, DamageType.Acid) }, // causticgrievveressence (300)
            { 787801030, new Tuple<uint, DamageType>(787802030, DamageType.Electric) }, // excitedgrievveressence (250)
            { 787801066, new Tuple<uint, DamageType>(787802066, DamageType.Electric) }, // excitedgrievveressence (300)
            { 787801031, new Tuple<uint, DamageType>(787802031, DamageType.Fire) }, // scorchedgrievveressence (250)
            { 787801067, new Tuple<uint, DamageType>(787802067, DamageType.Fire) }, // scorchedgrievveressence (300)
            { 787801032, new Tuple<uint, DamageType>(787802032, DamageType.Cold) }, // arcticgrievveressence (250)
            { 787801068, new Tuple<uint, DamageType>(787802068, DamageType.Cold) }, // arcticgrievveressence (300)
            { 787801033, new Tuple<uint, DamageType>(787802033, DamageType.Acid) }, // acidphyntosswarmessence (250)
            { 787801069, new Tuple<uint, DamageType>(787802069, DamageType.Acid) }, // acidphyntosswarmessence (300)
            { 787801034, new Tuple<uint, DamageType>(787802034, DamageType.Fire) }, // firephyntosswarmessence (250)
            { 787801070, new Tuple<uint, DamageType>(787802070, DamageType.Fire) }, // firephyntosswarmessence (300)
            { 787801035, new Tuple<uint, DamageType>(787802035, DamageType.Cold) }, // frostphyntosswarmessence (250)
            { 787801071, new Tuple<uint, DamageType>(787802071, DamageType.Cold) }, // frostphyntosswarmessence (300)
            { 787801036, new Tuple<uint, DamageType>(787802036, DamageType.Electric) }, // lightningphyntosswarmessence (250)
            { 787801072, new Tuple<uint, DamageType>(787802072, DamageType.Electric) }, // lightningphyntosswarmessence (300)
        };

        /// <summary>
        /// Returns TRUE if wo is Encapsulated Spirit
        /// </summary>
        public static bool IsEncapsulatedSpirit(WorldObject wo)
        {
            return wo.WeenieClassId == 49485;
        }

        /// <summary>
        /// When <see cref="PropertyInt.Structure"/> is 0, optionally restores one charge if the server allows pyreal auto-refill,
        /// the player has enrolled (<see cref="PropertyBool.PetDevicePyrealAutoRefillEnrolled"/>), and the configured cost is not negative
        /// (0 = free, &gt; 0 = must be able to pay).
        /// Returns true if the device now has at least one charge.
        /// </summary>
        private bool TryApplyPyrealAutoRefillBeforeSummon(Player player)
        {
            if (Structure != 0)
                return true;

            if (!ServerConfig.pet_device_pyreal_auto_refill_enabled.Value || !player.PetDevicePyrealAutoRefillEnrolled)
                return false;

            var cost = ServerConfig.pet_device_pyreal_auto_refill_cost_per_charge.Value;
            if (cost < 0)
                return false;

            // Calculate per-tier discount
            var discount = 0.0f;
            if (CharmSettingsManager.EssenceRefill.Enabled
                && player.ActiveCharmLevels.TryGetValue(CharmAbilityRegistry.PetDevicePyrealAutoRefillAbilityId, out var tier))
            {
                discount = tier switch
                {
                    1 => CharmSettingsManager.EssenceRefill.T1,
                    2 => CharmSettingsManager.EssenceRefill.T2,
                    3 => CharmSettingsManager.EssenceRefill.T3,
                    _ => 0.0f
                };
            }
            discount = Math.Clamp(discount, 0.0f, 1.0f);
            var finalCost = (int)Math.Max(0.0f, cost * (1.0f - discount));

            if (finalCost == 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "You restore one charge on your summoning essence at no cost.",
                    ChatMessageType.Broadcast));
            }
            else if (!player.TrySpendPyreals(finalCost))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"You need {finalCost:N0} pyreals to auto-replenish your summoning essence. You do not have enough pyreals.",
                    ChatMessageType.Broadcast));
                return false;
            }

            Structure = 1;
            player.UpdateProperty(this, PropertyInt.Structure, Structure.Value);

            if (finalCost > 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"You spend {finalCost:N0} pyreals to restore one charge on your summoning essence.",
                    ChatMessageType.Broadcast));
            }

            return true;
        }

        /// <summary>
        /// Applies an encapsulated spirit to a PetDevice
        /// </summary>
        public void Refill(Player player, CraftTool spirit)
        {
            // TODO: this should be moved to recipe system
            if (!IsEncapsulatedSpirit(spirit))
            {
                player.SendUseDoneEvent();
                return;
            }

            if (player.IsBusy)
            {
                player.SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            // verify use requirements
            var useError = VerifyUseRequirements(player, spirit, this);
            if (useError != WeenieError.None)
            {
                player.SendUseDoneEvent(useError);
                return;
            }

            player.IsBusy = true;

            var animTime = 0.0f;

            var actionChain = new ActionChain();

            // handle switching to peace mode
            if (player.CombatMode != CombatMode.NonCombat)
            {
                var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                actionChain.AddDelaySeconds(stanceTime);

                animTime += stanceTime;
            }

            // perform clapping motion
            animTime += player.EnqueueMotion(actionChain, MotionCommand.ClapHands);

            actionChain.AddAction(player, ActionType.PetDevice_Refill, () =>
            {
                // re-verify
                var useError = VerifyUseRequirements(player, spirit, this);
                if (useError != WeenieError.None)
                {
                    player.SendUseDoneEvent(useError);
                    player.IsBusy = false;
                    return;
                }

                if (Structure == MaxStructure)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("This essence is already full.", ChatMessageType.Broadcast));
                    player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                    player.IsBusy = false;
                    return;
                }

                player.UpdateProperty(this, PropertyInt.Structure, MaxStructure);

                player.TryConsumeFromInventoryWithNetworking(spirit, 1);

                player.Session.Network.EnqueueSend(new GameMessageSystemChat("You add the spirit to the essence.", ChatMessageType.Broadcast));

                player.SendUseDoneEvent();

                player.IsBusy = false;
            });

            player.EnqueueMotion(actionChain, MotionCommand.Ready);

            actionChain.EnqueueChain();

            player.NextUseTime = DateTime.UtcNow.AddSeconds(animTime);
        }

        public static WeenieError VerifyUseRequirements(Player player, WorldObject source, WorldObject target)
        {
            // ensure target is summoning essence? source.TargetType is Misc

            // ensure both source and target are in player's inventory
            if (player.FindObject(source.Guid.Full, Player.SearchLocations.MyInventory) == null)
                return WeenieError.YouDoNotPassCraftingRequirements;

            if (player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) == null)
                return WeenieError.YouDoNotPassCraftingRequirements;

            return WeenieError.None;
        }
    }
}
