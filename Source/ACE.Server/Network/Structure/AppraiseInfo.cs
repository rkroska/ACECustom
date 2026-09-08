using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;
using ZoneStatResolver = ACE.Server.Managers.ZoneControl.ZoneStatResolver;

namespace ACE.Server.Network.Structure
{
    /// <summary>
    /// Handles calculating and sending all object appraisal info
    /// </summary>
    public partial class AppraiseInfo
    {
        private static readonly uint EnchantmentMask = 0x80000000;

        public IdentifyResponseFlags Flags;

        public bool Success;    // assessment successful?

        public Dictionary<PropertyInt, int> PropertiesInt;
        public Dictionary<PropertyInt64, long> PropertiesInt64;
        public Dictionary<PropertyBool, bool> PropertiesBool;
        public Dictionary<PropertyFloat, double> PropertiesFloat;
        public Dictionary<PropertyString, string> PropertiesString;
        public Dictionary<PropertyDataId, uint> PropertiesDID;
        public Dictionary<PropertyInstanceId, uint> PropertiesIID;

        public List<uint> SpellBook;

        public ArmorProfile ArmorProfile;
        public CreatureProfile CreatureProfile;
        public WeaponProfile WeaponProfile;
        public HookProfile HookProfile;

        public ArmorMask ArmorHighlight;
        public ArmorMask ArmorColor;
        public WeaponMask WeaponHighlight;
        public WeaponMask WeaponColor;
        public ResistMask ResistHighlight;
        public ResistMask ResistColor;

        public ArmorLevel ArmorLevels;

        // This helps ensure the item will identify properly. Some "items" are technically "Creatures".
        private bool NPCLooksLikeObject;


        [GeneratedRegex("(\\B[A-Z])")]
        private static partial Regex CreatureNameRegex();

        public AppraiseInfo()
        {
            Flags = IdentifyResponseFlags.None;
            Success = false;
        }

        /// <summary>
        /// Construct all of the info required for appraising any WorldObject
        /// </summary>
        public AppraiseInfo(WorldObject wo, Player examiner, bool success = true)
        {
            BuildProfile(wo, examiner, success);
        }

        public void BuildProfile(WorldObject wo, Player examiner, bool success = true)
        {
            //Console.WriteLine("Appraise: " + wo.Guid);
            Success = success;

            BuildProperties(wo, examiner);
            BuildSpells(wo);

            // Live Stat Resolution (owner 2026-08-22, plan 3b): a piece with a ZcModifiers record is
            // resolved from its grades against the LIVE ladder for the RESPONSE COPY only - wo is
            // never written, never marked dirty (equip is the only re-stamp site: ApplyIfStale).
            var zcResolved = ZoneStatResolver.Compute(wo);
            if (zcResolved != null)
                SubstituteZoneResolvedInts(wo, zcResolved);

            // Help us make sure the item identify properly
            NPCLooksLikeObject = wo.GetProperty(PropertyBool.NpcLooksLikeObject) ?? false;

            if (PropertiesIID.ContainsKey(PropertyInstanceId.AllowedWielder) && !PropertiesBool.ContainsKey(PropertyBool.AppraisalHasAllowedWielder))
                PropertiesBool.Add(PropertyBool.AppraisalHasAllowedWielder, true);

            if (PropertiesIID.ContainsKey(PropertyInstanceId.AllowedActivator) && !PropertiesBool.ContainsKey(PropertyBool.AppraisalHasAllowedActivator))
                PropertiesBool.Add(PropertyBool.AppraisalHasAllowedActivator, true);

            if (PropertiesString.ContainsKey(PropertyString.ScribeAccount) && !examiner.IsAdmin && !examiner.IsSentinel && !examiner.IsEnvoy && !examiner.IsArch && !examiner.IsPsr)
                PropertiesString.Remove(PropertyString.ScribeAccount);

            if (PropertiesString.ContainsKey(PropertyString.HouseOwnerAccount) && !examiner.IsAdmin && !examiner.IsSentinel && !examiner.IsEnvoy && !examiner.IsArch && !examiner.IsPsr)
                PropertiesString.Remove(PropertyString.HouseOwnerAccount);

            if (PropertiesInt.ContainsKey(PropertyInt.Lifespan))
                PropertiesInt[PropertyInt.RemainingLifespan] = wo.GetRemainingLifespan();

            if (PropertiesInt.TryGetValue(PropertyInt.Faction1Bits, out var faction1Bits))
            {
                // hide any non-default factions, prevent client from displaying ???
                // this is only needed for non-standard faction creatures that use templates, to hide the ??? in the client
                var sendBits = faction1Bits & (int)FactionBits.ValidFactions;
                if (sendBits != faction1Bits)
                {
                    if (sendBits != 0)
                        PropertiesInt[PropertyInt.Faction1Bits] = sendBits;
                    else
                        PropertiesInt.Remove(PropertyInt.Faction1Bits);
                }
            }

            // armor / clothing / shield
            if (wo is Clothing || wo.IsShield)
                BuildArmor(wo);

            if (wo is Creature creature)
                BuildCreature(creature);

            if (wo.Damage != null && !(wo is Clothing) || wo is MeleeWeapon || wo is Missile || wo is MissileLauncher || wo is Ammunition || wo is Caster)
                BuildWeapon(wo, examiner);

            // Owner 2026-08-22: Zone Cantrip lines belong in the Property Details section, not
            // adrift in the description block. Runs here so downstream LongDesc edits see the same
            // shape they always have (a block already pinned to the top).
            PromoteZoneModifierLines(zcResolved, wo, examiner);

            // TODO: Resolve this issue a better way?
            // Because of the way ACE handles default base values in recipe system (or rather the lack thereof)
            // we need to check the following weapon properties to see if they're below expected minimum and adjust accordingly
            // The issue is that the recipe system likely added 0.005 to 0 instead of 1, which is what *should* have happened.
            if (wo.WeaponMagicDefense.HasValue && wo.WeaponMagicDefense.Value > 0 && wo.WeaponMagicDefense.Value < 1 && ((wo.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 1) != 0)
                PropertiesFloat[PropertyFloat.WeaponMagicDefense] += 1;
            if (wo.WeaponMissileDefense.HasValue && wo.WeaponMissileDefense.Value > 0 && wo.WeaponMissileDefense.Value < 1 && ((wo.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 1) != 0)
                PropertiesFloat[PropertyFloat.WeaponMissileDefense] += 1;

            // Mask real value of AbsorbMagicDamage and/or Add AbsorbMagicDamage for ImbuedEffectType.IgnoreSomeMagicProjectileDamage
            if (PropertiesFloat.ContainsKey(PropertyFloat.AbsorbMagicDamage) || wo.HasImbuedEffect(ImbuedEffectType.IgnoreSomeMagicProjectileDamage))
                PropertiesFloat[PropertyFloat.AbsorbMagicDamage] = 1;

            if (wo is Door || wo is Chest)
            {
                // If wo is not locked, do not send ResistLockpick value. If ResistLockpick is sent for unlocked objects, id panel shows bonus to Lockpick skill
                if (!wo.IsLocked && PropertiesInt.ContainsKey(PropertyInt.ResistLockpick))
                    PropertiesInt.Remove(PropertyInt.ResistLockpick);

                // If wo is locked, append skill check percent, as int, to properties for id panel display on chances of success
                if (wo.IsLocked)
                {
                    var resistLockpick = LockHelper.GetResistLockpick(wo);

                    if (resistLockpick != null)
                    {
                        PropertiesInt[PropertyInt.ResistLockpick] = (int)resistLockpick;

                        var pickSkill = examiner.Skills[Skill.Lockpick].Current;

                        var successChance = SkillCheck.GetSkillChance((int)pickSkill, (int)resistLockpick) * 100;

                        if (!PropertiesInt.ContainsKey(PropertyInt.AppraisalLockpickSuccessPercent))
                            PropertiesInt.Add(PropertyInt.AppraisalLockpickSuccessPercent, (int)successChance);
                    }
                }
                // if wo has DefaultLocked property and is unlocked, add that state to the property buckets
                else if (PropertiesBool.ContainsKey(PropertyBool.DefaultLocked))
                    PropertiesBool[PropertyBool.Locked] = false;
            }

            if (wo is Corpse)
            {
                var timeToRot = wo.TimeToRot;
                if (timeToRot.HasValue && timeToRot.Value != -1)
                {
                    TimeSpan tsSinceLastUpdate = DateTime.UtcNow - (wo.LastTimeToRotUpdate ?? DateTime.UtcNow);
                    TimeSpan tsToRot = TimeSpan.FromSeconds(wo.TimeToRot.Value) - tsSinceLastUpdate;
                    var msg = tsToRot.TotalSeconds < 5 ? $"Corpse is about to decay." : $"Corpse will decay in {tsToRot.GetFriendlyString()}.";
                    if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                        PropertiesString[PropertyString.LongDesc] += $"\n\n{msg}";
                    else
                        PropertiesString[PropertyString.LongDesc] = msg;
                }

                PropertiesBool.Clear();
                PropertiesDID.Clear();
                PropertiesFloat.Clear();
                PropertiesInt64.Clear();

                var discardInts = PropertiesInt.Where(x => x.Key != PropertyInt.EncumbranceVal && x.Key != PropertyInt.Value).Select(x => x.Key).ToList();
                foreach (var key in discardInts)
                    PropertiesInt.Remove(key);
                var discardString = PropertiesString.Where(x => x.Key != PropertyString.LongDesc).Select(x => x.Key).ToList();
                foreach (var key in discardString)
                    PropertiesString.Remove(key);

                PropertiesInt[PropertyInt.Value] = 0;
            }

            if (wo is Portal portal)
            {
                var useCount = portal.PortalUseCount;
                if (useCount.HasValue)
                {
                    var msg = $"Remaining Uses: {useCount.Value}";
                    if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                        PropertiesString[PropertyString.LongDesc] += $"\n\n{msg}";
                    else
                        PropertiesString[PropertyString.LongDesc] = msg;
                }

                var reqs = new (PortalRequirement type, int? value, int? max)[]
                {
                    (portal.PortalReqType, portal.PortalReqValue, portal.PortalReqMaxValue),
                    (portal.PortalReqType2, portal.PortalReqValue2, portal.PortalReqMaxValue2)
                };

                foreach (var req in reqs)
                {
                    if (req.type == PortalRequirement.None) continue;
                    if (req.value.GetValueOrDefault() <= 0 && (req.max.GetValueOrDefault() <= 0 || req.max.GetValueOrDefault() == 999)) continue;

                    string typeName;
                    bool isMultiplier = false;

                    switch (req.type)
                    {
                        case PortalRequirement.CreatureAug: typeName = "Creature Augmentations"; break;
                        case PortalRequirement.ItemAug: typeName = "Item Augmentations"; break;
                        case PortalRequirement.LifeAug: typeName = "Life Augmentations"; break;
                        case PortalRequirement.Enlighten: typeName = "Enlightenment"; break;
                        case PortalRequirement.QuestBonus: typeName = "Quest Bonus"; break;
                        case PortalRequirement.XPMultiplier: typeName = "XP Multiplier"; isMultiplier = true; break;
                        default: continue;
                    }

                    var prefix = isMultiplier ? "x" : "";
                    string msg;

                    if (req.max.GetValueOrDefault() > 0 && req.max.GetValueOrDefault() != 999 && req.value.GetValueOrDefault() > 0)
                        msg = $"Restricted to characters of {typeName} between {prefix}{req.value.Value} and {prefix}{req.max.Value}.";
                    else if (req.value.GetValueOrDefault() > 0)
                        msg = $"Restricted to characters of {typeName} {prefix}{req.value.Value} or greater.";
                    else
                        msg = $"Restricted to characters of {typeName} {prefix}{req.max.Value} or lower.";

                    if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                        PropertiesString[PropertyString.LongDesc] += $"\n\n{msg}";
                    else
                        PropertiesString[PropertyString.LongDesc] = msg;
                }

                var timeToRot = portal.TimeToRot;
                if (timeToRot.HasValue && timeToRot.Value != -1) // -1 is a special value meaning "Never"
                {
                    TimeSpan tsSinceLastUpdate = DateTime.UtcNow - (portal.LastTimeToRotUpdate ?? DateTime.UtcNow);
                    TimeSpan tsToRot = TimeSpan.FromSeconds(portal.TimeToRot.Value) - tsSinceLastUpdate;
                    var msg = tsToRot.TotalSeconds < 5 ? "Portal is about to fade." : $"Portal will fade in {tsToRot.GetFriendlyString()}.";
                    if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                        PropertiesString[PropertyString.LongDesc] += $"\n\n{msg}";
                    else
                        PropertiesString[PropertyString.LongDesc] = msg;
                }

                PropertiesInt.Remove(PropertyInt.EncumbranceVal);
            }

            // Weapon aug-scaling: the EXAMINER-projected value, so a drop can be evaluated without
            // equipping it (owner 2026-08-01). Computed from the VIEWER's current item augs — the
            // same resolve combat uses — so re-examining after buying augs refreshes it. Appraisal
            // output only, never persisted; only shows while the system is enabled and the item is
            // stamped (GetFlatBonus returns 0 otherwise). While WIELDED the green damage line
            // already includes the term (WeaponProfile), so the projection is for the unwielded case.
            if (examiner != null && wo.Wielder == null)
            {
                var projected = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetFlatBonus(wo, examiner);
                var floor = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetFloorBonus(wo);
                if (projected > floor)
                {
                    // The unwielded damage line already shows the wield-FLOOR value (an honest
                    // minimum for any hands), so this line is the examiner's bonus ABOVE that
                    // floor — hidden entirely for a player sitting exactly at the wield req.
                    var msg = $"Bonus Damage to weapon while equipped by you: +{(int)(projected - floor):N0}";

                    // Provenance ("Dropped by:" / "Location:") stays the very last block (owner
                    // 2026-08-01) - insert the per-viewer projection ABOVE it, not after. Legacy
                    // single-line formats ("Dropped by X in...", "Dropped in the...") still match.
                    if (PropertiesString.TryGetValue(PropertyString.LongDesc, out var ldCur) && !string.IsNullOrEmpty(ldCur))
                    {
                        var provIdx = ldCur.IndexOf("Dropped by", StringComparison.Ordinal);
                        if (provIdx < 0)
                            provIdx = ldCur.IndexOf("Dropped in ", StringComparison.Ordinal);
                        if (provIdx < 0)
                            provIdx = ldCur.IndexOf("Created by", StringComparison.Ordinal);   // forged test items
                        if (provIdx < 0)
                            provIdx = ldCur.IndexOf("Location:", StringComparison.Ordinal);
                        if (provIdx >= 0)
                            PropertiesString[PropertyString.LongDesc] = ldCur.Insert(provIdx, $"{msg}\n\n");
                        else
                            PropertiesString[PropertyString.LongDesc] = ldCur + $"\n\n{msg}";
                    }
                    else
                        PropertiesString[PropertyString.LongDesc] = msg;
                }
            }

            // Pet Bonding System - display bond info on PetDevice appraisal
            // This is intentionally done in appraisal output (not persisted to wo.LongDesc).
            if (wo is ACE.Server.WorldObjects.PetDevice petDevice)
            {
                if (petDevice.IsCombatPetDevice())
                {
                    // Show only when the pet device is attuned OR has any bond fields set.
                    var isAttuned = petDevice.IsPetBondAttuned;
                    var bondLevel = petDevice.PetBondLevel;
                    var bondXp = petDevice.PetBondXp;

                    if (isAttuned || bondLevel.HasValue || bondXp.HasValue)
                    {
                        var level = bondLevel.GetValueOrDefault();
                        var xp = bondXp.GetValueOrDefault();

                        // Keep wording player-friendly; avoid implementation details.
                        var msg = isAttuned
                            ? $"Bond Level: {level:N0}\nBond XP: {xp:N0}"
                            : $"Bond (unattuned)\nBond Level: {level:N0}\nBond XP: {xp:N0}";

                        if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                            PropertiesString[PropertyString.LongDesc] += $"\n\n{msg}";
                        else
                            PropertiesString[PropertyString.LongDesc] = msg;
                    }

                    var summonDur = petDevice.BuildCombatPetSummonDurationAppraisal(examiner);
                    if (!string.IsNullOrWhiteSpace(summonDur))
                    {
                        if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                            PropertiesString[PropertyString.LongDesc] += $"\n\n{summonDur}";
                        else
                            PropertiesString[PropertyString.LongDesc] = summonDur;
                    }

                    var potencyMsg = ACE.Server.Entity.PetPotency.BuildPotencyAppraisalBlock(petDevice);
                    if (!string.IsNullOrWhiteSpace(potencyMsg))
                    {
                        if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                            PropertiesString[PropertyString.LongDesc] += $"\n\n{potencyMsg}";
                        else
                            PropertiesString[PropertyString.LongDesc] = potencyMsg;
                    }
                }

                var minLumAugSummon = petDevice.GetProperty(PropertyInt.PetDeviceMinLumAugSummonCount);
                if (minLumAugSummon.HasValue && minLumAugSummon.Value > 0 && examiner != null)
                {
                    var req = minLumAugSummon.Value;
                    var current = examiner.GetProperty(PropertyInt64.LumAugSummonCount) ?? 0;
                    var suffix = $"\n\nLuminance Summoning inheritance: {current} (requires at least {req} to use this device).";
                    if (PropertiesString.TryGetValue(PropertyString.LongDesc, out var ld) && !string.IsNullOrEmpty(ld))
                        PropertiesString[PropertyString.LongDesc] = ld + suffix;
                    else
                        PropertiesString[PropertyString.LongDesc] = suffix.TrimStart('\r', '\n');
                }
            }

            // Growth charms + gems carry UnlimitedUse only so Gem.ActOnUse does not self-consume
            // them (gems are consumed by the confirm handler). It is a mechanical flag, not a use
            // counter - hide it so the panel shows no "Number of uses remaining" line.
            if ((wo.WeenieClassId >= 777700030 && wo.WeenieClassId <= 777700034) ||
                (wo.WeenieClassId >= 777700051 && wo.WeenieClassId <= 777700070))
                PropertiesBool.Remove(PropertyBool.UnlimitedUse);

            // Charm of the Triune Weave - progress lives on the examiner's character, so a
            // replacement charm always shows the true totals. Appraisal output only, never persisted.
            if (wo.WeenieClassId == 777700030 && examiner != null)
            {
                var weave = examiner.GetProperty(PropertyInt64.TriuneWeaveCount) ?? 0;
                var msg = weave > 0
                    ? $"The weave has been empowered {weave:N0} times:\n   +{weave:N0} Creature Augmentations\n   +{weave:N0} Item Augmentations\n   +{weave:N0} Life Augmentations\n\nFeed it Gems of the Triune Weave to empower it further."
                    : "The weave is dormant. Feed it Gems of the Triune Weave to empower it, granting +1 Creature, Item, and Life augmentation per point.";

                if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                    PropertiesString[PropertyString.LongDesc] += $"\n\n{msg}";
                else
                    PropertiesString[PropertyString.LongDesc] = msg;
            }

            // School charms (War/Void/Melee/Missile) - counters accumulate toward future ability
            // unlocks; like the Triune Weave, progress lives on the examiner's character.
            PropertyInt64? schoolCharmProp =
                wo.WeenieClassId == 777700051 ? PropertyInt64.BattlemagesWrathCharmCount :
                wo.WeenieClassId == 777700056 ? PropertyInt64.NetherVeilCharmCount :
                wo.WeenieClassId == 777700061 ? PropertyInt64.CrashingSteelCharmCount :
                wo.WeenieClassId == 777700066 ? PropertyInt64.TrueShotCharmCount :
                (PropertyInt64?)null;

            if (schoolCharmProp != null && examiner != null)
            {
                var count = examiner.GetProperty(schoolCharmProp.Value) ?? 0;
                var msg = count > 0
                    ? $"The charm has been empowered {count:N0} times. Its deeper powers remain sealed."
                    : "The charm is dormant. Feed it Gems to empower it.";

                if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                    PropertiesString[PropertyString.LongDesc] += $"\n\n{msg}";
                else
                    PropertiesString[PropertyString.LongDesc] = msg;
            }

            if (wo is SlumLord slumLord)
            {                
                PropertiesBool.Clear();
                PropertiesDID.Clear();
                PropertiesFloat.Clear();
                PropertiesIID.Clear();
                //PropertiesInt.Clear();
                PropertiesInt64.Clear();
                PropertiesString.Clear();

                var longDesc = "";
                if (slumLord.House == null)
                {
                    longDesc = $"This house is not configured correctly, please contact your Server Admin.\n";

                    PropertiesInt.Clear();
                }

                else
                {
                    if (slumLord.HouseOwner.HasValue && slumLord.HouseOwner.Value > 0)
                    {
                        longDesc = $"The current maintenance has {(slumLord.IsRentPaid() || !ServerConfig.house_rent_enabled.Value ? "" : "not ")}been paid.\n";

                        PropertiesInt.Clear();
                    }
                    else
                    {
                        //longDesc = $"This house is {(slumLord.HouseStatus == HouseStatus.Disabled ? "not " : "")}available for purchase.\n"; // this was the retail msg.
                        longDesc = $"This {(slumLord.House.HouseType == HouseType.Undef ? "house" : slumLord.House.HouseType.ToString().ToLower())} is {(slumLord.House.HouseStatus == HouseStatus.Disabled ? "not " : "")}available for purchase.\n";

                        var discardInts = PropertiesInt.Where(x => x.Key != PropertyInt.HouseStatus && x.Key != PropertyInt.HouseType && x.Key != PropertyInt.MinLevel && x.Key != PropertyInt.MaxLevel && x.Key != PropertyInt.AllegianceMinLevel && x.Key != PropertyInt.AllegianceMaxLevel).Select(x => x.Key).ToList();
                        foreach (var key in discardInts)
                            PropertiesInt.Remove(key);
                    }

                    if (slumLord.HouseRequiresMonarch)
                        longDesc += "You must be a monarch to purchase and maintain this dwelling.\n";

                    if (slumLord.AllegianceMinLevel.HasValue)
                    {
                        var allegianceMinLevel = ServerConfig.mansion_min_rank.Value;
                        if (allegianceMinLevel == -1)
                            allegianceMinLevel = slumLord.AllegianceMinLevel.Value;

                        longDesc += $"Restricted to characters of allegiance rank {allegianceMinLevel} or greater.\n";
                    }
                }

                PropertiesString.Add(PropertyString.LongDesc, longDesc);
            }

            if (wo is Container)
            {
                if (PropertiesInt.ContainsKey(PropertyInt.Value))
                {
                    var cachedWeenie = DatabaseManager.World.GetCachedWeenie(wo.WeenieClassId);
                    PropertiesInt[PropertyInt.Value] = cachedWeenie?.GetValue() ?? 0; // Value is masked to base value of Weenie
                }
            }

            if (wo is Storage)
            {
                var longDesc = "";

                if (wo.HouseOwner.HasValue && wo.HouseOwner.Value > 0)
                    longDesc = $"Owned by {wo.ParentLink.HouseOwnerName}\n";

                var discardString = PropertiesString.Where(x => x.Key != PropertyString.Use).Select(x => x.Key).ToList();
                foreach (var key in discardString)
                    PropertiesString.Remove(key);

                PropertiesString.Add(PropertyString.LongDesc, longDesc);
            }

            if (wo is Hook)
            {
                // If the hook has any inventory, we need to send THOSE properties instead.
                var hook = wo as Container;

                string baseDescString = "";
                if (wo.ParentLink.HouseOwner != null)
                {
                    // This is for backwards compatibility. This value was not set/saved in earlier versions.
                    // It will get the player's name and save that to the HouseOwnerName property of the house. This is now done when a player purchases a house.
                    if (wo.ParentLink.HouseOwnerName == null)
                    {
                        var houseOwnerPlayer = PlayerManager.FindByGuid((uint)wo.ParentLink.HouseOwner);
                        if (houseOwnerPlayer != null)
                        {
                            wo.ParentLink.HouseOwnerName = houseOwnerPlayer.Name;
                            wo.ParentLink.SaveBiotaToDatabase();
                        }
                    }
                    baseDescString = "This hook is owned by " + wo.ParentLink.HouseOwnerName + ". "; //if house is owned, display this text
                }

                var containsString = "";
                if (hook.Inventory.Count == 1)
                {
                    WorldObject hookedItem = hook.Inventory.First().Value;

                    // Hooked items have a custom "description", containing the desc of the sub item and who the owner of the house is (if any)
                    BuildProfile(hookedItem, examiner, success);

                    containsString = "It contains: \n";

                    if (!string.IsNullOrWhiteSpace(hookedItem.LongDesc))
                    {
                        containsString += hookedItem.LongDesc;
                    }
                    //else if (PropertiesString.ContainsKey(PropertyString.ShortDesc) && PropertiesString[PropertyString.ShortDesc] != null)
                    //{
                    //    containsString += PropertiesString[PropertyString.ShortDesc];
                    //}
                    else
                    {
                        containsString += hookedItem.Name;
                    }

                    BuildHookProfile(hookedItem);
                }

                //if (PropertiesString.ContainsKey(PropertyString.LongDesc) && PropertiesString[PropertyString.LongDesc] != null)
                //    PropertiesString[PropertyString.LongDesc] = baseDescString + containsString;
                ////else if (PropertiesString.ContainsKey(PropertyString.ShortDesc) && PropertiesString[PropertyString.ShortDesc] != null)
                ////    PropertiesString[PropertyString.LongDesc] = baseDescString + containsString;
                //else
                //    PropertiesString[PropertyString.LongDesc] = baseDescString + containsString;

                PropertiesString[PropertyString.LongDesc] = baseDescString + containsString;

                PropertiesInt.Remove(PropertyInt.Structure);

                // retail should have removed this property and then server side built the same result for the hook longdesc replacement but didn't and ends up with some odd looking appraisals as seen on video/pcaps
                //PropertiesInt.Remove(PropertyInt.AppraisalLongDescDecoration);
            }

            if (wo is ManaStone)
            {
                var useMessage = "";

                if (wo.ItemCurMana.HasValue)
                    useMessage = "Use on a magic item to give the stone's stored Mana to that item.";
                else
                    useMessage = "Use on a magic item to destroy that item and drain its Mana.";

                PropertiesString[PropertyString.Use] = useMessage;
            }

            if (wo.GetProperty(PropertyBool.IsCharm) == true)
            {
                string charmHeader;
                if (wo.GetProperty(PropertyBool.IsAbilityCharm) == true)
                {
                    var tier    = wo.GetProperty(PropertyInt.CharmLevel) ?? 1;
                    var maxTier = wo.GetProperty(PropertyInt.CharmMaxLevel);
                    var tierStr = maxTier.HasValue ? $"{tier}/{maxTier}" : $"{tier}";
                    var isOn    = wo.GetProperty(PropertyBool.IsCharmActivated) == true;
                    var status  = isOn ? "Status: ON" : "Status: OFF";
                    charmHeader = $"Charm [Tier {tierStr}]\n\n{status}\n\nDouble click to toggle ability on / off.";
                }
                else
                {
                    charmHeader = "Charm: Holding this item grants its magical effects.";
                }

                if (PropertiesString.TryGetValue(PropertyString.Use, out var existingUse))
                    PropertiesString[PropertyString.Use] = $"{charmHeader}\n\n{existingUse.TrimStart('\r', '\n')}";
                else
                    PropertiesString[PropertyString.Use] = charmHeader;
            }

            if (wo is CraftTool && (wo.ItemType == ItemType.TinkeringMaterial || wo.WeenieClassId >= 36619 && wo.WeenieClassId <= 36628 || wo.WeenieClassId >= 36634 && wo.WeenieClassId <= 36636))
            {
                PropertiesInt.Remove(PropertyInt.Structure);
            }

            if (!Success)
            {
                // todo: what specifically to keep/what to clear

                //PropertiesBool.Clear();
                //PropertiesDID.Clear();
                //PropertiesFloat.Clear();
                //PropertiesIID.Clear();
                //PropertiesInt.Clear();
                //PropertiesInt64.Clear();
                //PropertiesString.Clear();
            }

            // CR-33: Gem-socket LongDesc population is independent of item expiry.
            // Run this block for all items that have the AppendGemInfo flag set, regardless of whether
            // they also have an ItemExpirationTimestamp. Previously this was entangled inside the
            // expiry block, which would silently skip gem-info for socketable items with no expiry.
            if (PropertiesInt.TryGetValue(PropertyInt.AppraisalLongDescDecoration, out var dec))
            {
                if ((dec & 4) != 0) // AppraisalLongDescDecorations.AppendGemInfo
                {
                    // Gem-socket "set with N <gem>" label suppressed globally (owner 2026-07-14): don't
                    // append it server-side, but still clear the flag so the client doesn't append it either.
                    dec &= ~4;
                    if (dec == 0)
                        PropertiesInt.Remove(PropertyInt.AppraisalLongDescDecoration);
                    else
                        PropertiesInt[PropertyInt.AppraisalLongDescDecoration] = dec;
                }
            }

            if (wo.ItemExpirationTimestamp.HasValue)
            {
                var absoluteExpiration = ACE.Common.Time.GetDateTimeFromTimestamp(wo.ItemExpirationTimestamp.Value);
                var timeToExpiration = absoluteExpiration - DateTime.UtcNow;
                string msg;
                if (timeToExpiration.TotalSeconds < 5)
                {
                    msg = "This item is temporary and is about to crumble to dust.";
                }
                else
                {
                    msg = $"This item is temporary and will crumble to dust in {timeToExpiration.GetFriendlyString()}.";
                }

                if (PropertiesString.ContainsKey(PropertyString.LongDesc))
                    PropertiesString[PropertyString.LongDesc] += $"\n\n{msg}";
                else
                    PropertiesString[PropertyString.LongDesc] = msg;
            }

            BuildFlags();
        }

        private void BuildProperties(WorldObject wo, Player examiner = null)
        {
            PropertiesInt = wo.GetAllPropertyInt().Where(x => AssessmentProperties.PropertiesInt.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            PropertiesInt64 = wo.GetAllPropertyInt64().Where(x => AssessmentProperties.PropertiesInt64.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            PropertiesBool = wo.GetAllPropertyBools().Where(x => AssessmentProperties.PropertiesBool.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            PropertiesFloat = wo.GetAllPropertyFloat().Where(x => AssessmentProperties.PropertiesDouble.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            PropertiesString = wo.GetAllPropertyString().Where(x => AssessmentProperties.PropertiesString.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            PropertiesDID = wo.GetAllPropertyDataId().Where(x => AssessmentProperties.PropertiesDataId.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            PropertiesIID = wo.GetAllPropertyInstanceId().Where(x => AssessmentProperties.PropertiesInstanceId.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);

            // Int64Stat is a server-side extension the client has no requirement text for — it
            // renders the wield-requirement rows as a blank block (between the native Melee Defense
            // and Mana Conversion sections on casters). Enforcement reads the biota, not appraisal;
            // the authored "Wield requires: N Item Augmentations" line carries the requirement.
            if (PropertiesInt.TryGetValue(PropertyInt.WieldRequirements, out var wieldReqType) &&
                wieldReqType == (int)WieldRequirement.Int64Stat)
            {
                PropertiesInt.Remove(PropertyInt.WieldRequirements);
                PropertiesInt.Remove(PropertyInt.WieldSkillType);
                PropertiesInt.Remove(PropertyInt.WieldDifficulty);
            }

            // Paragon-stamped items (recipe + the ZoneLootMutator pre-Paragon card) carry
            // ItemMaxLevel/ItemBaseXp/ItemTotalXp but no ItemXpStyle, and the client can't compose
            // its item-level section without the style — it renders a blank block instead (between
            // the native Melee Defense and Mana Conversion sections on casters). Real leveling
            // items (cloaks, aetheria) always set ItemXpStyle and keep their display.
            if (PropertiesInt.ContainsKey(PropertyInt.ItemMaxLevel) &&
                !PropertiesInt.ContainsKey(PropertyInt.ItemXpStyle))
            {
                PropertiesInt.Remove(PropertyInt.ItemMaxLevel);
                PropertiesInt64.Remove(PropertyInt64.ItemBaseXp);
                PropertiesInt64.Remove(PropertyInt64.ItemTotalXp);
            }

            if (wo is Player player)
            {
                // handle character options
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourDateOfBirth))
                    PropertiesString.Remove(PropertyString.DateOfBirth);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourAge))
                    PropertiesInt.Remove(PropertyInt.Age);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourChessRank))
                    PropertiesInt.Remove(PropertyInt.ChessRank);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourFishingSkill))
                    PropertiesInt.Remove(PropertyInt.FakeFishingSkill);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourNumberOfDeaths))
                    PropertiesInt.Remove(PropertyInt.NumDeaths);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourNumberOfTitles))
                    PropertiesInt.Remove(PropertyInt.NumCharacterTitles);

                // handle dynamic properties for appraisal
                if (player.Allegiance != null && player.AllegianceNode != null)
                {
                    if (player.Allegiance.AllegianceName != null)
                        PropertiesString[PropertyString.AllegianceName] = player.Allegiance.AllegianceName;

                    if (player.AllegianceNode.IsMonarch)
                    {
                        PropertiesInt[PropertyInt.AllegianceFollowers] = player.AllegianceNode.TotalFollowers;
                    }
                    else
                    {
                        var monarch = player.Allegiance.Monarch;
                        var patron = player.AllegianceNode.Patron;

                        PropertiesString[PropertyString.MonarchsTitle] = AllegianceTitle.GetTitle((HeritageGroup)(monarch.Player.Heritage ?? 0), (Gender)(monarch.Player.Gender ?? 0), monarch.Rank) + " " + monarch.Player.Name;
                        PropertiesString[PropertyString.PatronsTitle] = AllegianceTitle.GetTitle((HeritageGroup)(patron.Player.Heritage ?? 0), (Gender)(patron.Player.Gender ?? 0), patron.Rank) + " " + patron.Player.Name;
                    }
                }

                if (player.Fellowship != null)
                    PropertiesString[PropertyString.Fellowship] = player.Fellowship.FellowshipName;
            }

            AddPropertyEnchantments(wo, examiner);
        }

        private void AddPropertyEnchantments(WorldObject wo, Player examiner = null)
        {
            if (wo == null) return;

            if (PropertiesInt.ContainsKey(PropertyInt.ArmorLevel))
                PropertiesInt[PropertyInt.ArmorLevel] += wo.EnchantmentManager.GetArmorMod();

            if (wo.ItemSkillLimit != null)
                PropertiesInt[PropertyInt.AppraisalItemSkill] = (int)wo.ItemSkillLimit;
            else
                PropertiesInt.Remove(PropertyInt.AppraisalItemSkill);

            if (PropertiesFloat.ContainsKey(PropertyFloat.WeaponDefense) && !(wo is Ammunition))
            {
                var defenseMod = wo.EnchantmentManager.GetDefenseMod();
                var auraDefenseMod = wo.Wielder != null && wo.IsEnchantable ? wo.Wielder.EnchantmentManager.GetDefenseMod() : 0.0f;

                PropertiesFloat[PropertyFloat.WeaponDefense] += defenseMod + auraDefenseMod;
            }

            if (PropertiesFloat.TryGetValue(PropertyFloat.ManaConversionMod, out var manaConvMod))
            {
                if (manaConvMod != 0)
                {
                    // hermetic link/void
                    var enchantmentMod = ResistMaskHelper.GetManaConversionMod(wo);

                    if (enchantmentMod != 1.0f)
                    {
                        PropertiesFloat[PropertyFloat.ManaConversionMod] *= enchantmentMod;

                        ResistHighlight = ResistMaskHelper.GetHighlightMask(wo);
                        ResistColor = ResistMaskHelper.GetColorMask(wo);
                    }
                }
                else if (!ServerConfig.show_mana_conv_bonus_0.Value)
                {
                    PropertiesFloat.Remove(PropertyFloat.ManaConversionMod);
                }
            }

            if (PropertiesFloat.ContainsKey(PropertyFloat.ElementalDamageMod))
            {
                // Weapon aug-scaling: show the RESOLVED elemental modifier, not the authored one —
                // the same resolver combat uses, with the same replace semantics. WIELDED reads
                // the wielder's augs; UNWIELDED reads the examiner's, so a drop in a pack reads as
                // what it would do in YOUR hands. Without this the panel would show every tier of
                // caster the same number, which is exactly the confusion the launcher lane hit on
                // 2026-08-06 (two different-tier bows appraising identically, reasonably read as
                // "these weapons are identical").
                var casterHolder = (wo.Wielder as Player) ?? examiner;
                var enchantmentBonus = ResistMaskHelper.GetElementalDamageBonus(wo);

                if (ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.TryGetCasterElementalMod(wo, casterHolder, out var gradedElemMod))
                    // the same composition combat uses: the graded mod MULTIPLIES the aura, it does not add to it
                    PropertiesFloat[PropertyFloat.ElementalDamageMod] = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.ComposeCasterModifier(
                        gradedElemMod, enchantmentBonus, ACE.Server.Managers.WeaponScaling.WeaponScalingManager.Current.CasterAuraRescale);
                else if (enchantmentBonus != 0)
                    PropertiesFloat[PropertyFloat.ElementalDamageMod] += enchantmentBonus;

                if (enchantmentBonus != 0)
                {

                    ResistHighlight = ResistMaskHelper.GetHighlightMask(wo);
                    ResistColor = ResistMaskHelper.GetColorMask(wo);
                }
            }

            var appraisalLongDescDecoration = AppraisalLongDescDecorations.None;

            if (wo.ItemWorkmanship > 0)
                appraisalLongDescDecoration |= AppraisalLongDescDecorations.PrependWorkmanship;
            if (wo.MaterialType > 0)
                appraisalLongDescDecoration |= AppraisalLongDescDecorations.PrependMaterial;
            if (wo.GemType > 0 && wo.GemCount > 0)
                appraisalLongDescDecoration |= AppraisalLongDescDecorations.AppendGemInfo;

            if (appraisalLongDescDecoration > 0 && wo.LongDesc != null && wo.LongDesc.StartsWith(wo.Name))
                PropertiesInt[PropertyInt.AppraisalLongDescDecoration] = (int)appraisalLongDescDecoration;
            else
                PropertiesInt.Remove(PropertyInt.AppraisalLongDescDecoration);
        }

        private void BuildSpells(WorldObject wo)
        {
            SpellBook = new List<uint>();

            if (wo is Creature)
                return;

            if (wo.SpellDID.HasValue)
                SpellBook.Add(wo.SpellDID.Value);

            // Zone Control "Cast on Strike" withholds its spell id from the client (owner 2026-08-27).
            // The client renders a SpellBook entry's name from its own DAT, so handing it the id would
            // print "Nether Arc I" beside our own Property Details line that deliberately says "Nether
            // Arc" - the level marker reads as a weak spell, which the card is not.
            //
            // GATED ON PROP 9058, NOT ON THE SPELL ID. A spell-id test would also swallow the ~11,700
            // live items carrying player-crafted Ring Glyph procs of the very same spells, rewriting
            // appraisal on gear we never touched. 9058 is stamped by this card and by nothing else.
            var zcArcDmg = wo.GetProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcArcDamagePropId);
            var zcHasProcCard = zcArcDmg.HasValue && zcArcDmg.Value > 0;

            if (wo.ProcSpell.HasValue && !zcHasProcCard)
                SpellBook.Add(wo.ProcSpell.Value);

            var woSpellDID = wo.SpellDID;   // prevent recursive lock
            var woProcSpell = wo.ProcSpell;

            foreach (var spellId in wo.Biota.GetKnownSpellsIdsWhere(i => i != woSpellDID && i != woProcSpell, wo.BiotaDatabaseLock))
                SpellBook.Add((uint)spellId);
        }

        private void AddEnchantments(WorldObject wo)
        {
            if (wo == null) return;

            // get all currently active item enchantments on the item
            var woEnchantments = wo.EnchantmentManager.GetEnchantments(MagicSchool.ItemEnchantment);

            foreach (var enchantment in woEnchantments)
                SpellBook.Add((uint)enchantment.SpellId | EnchantmentMask);

            // show auras from wielder, if applicable

            // this technically wasn't a feature in retail

            if (wo.Wielder != null && wo.IsEnchantable && wo.WeenieType != WeenieType.Clothing && !wo.IsShield && ServerConfig.show_aura_buff.Value)
            {
                // get all currently active item enchantment auras on the player
                var wielderEnchantments = wo.Wielder.EnchantmentManager.GetEnchantments(MagicSchool.ItemEnchantment);

                // Only show reflected Auras from player appropriate for wielded weapons
                foreach (var enchantment in wielderEnchantments)
                {
                    if (wo is Caster)
                    {
                        // Caster weapon only item Auras
                        if ((enchantment.SpellCategory == SpellCategory.DefenseModRaising)
                            || (enchantment.SpellCategory == SpellCategory.DefenseModRaisingRare)
                            || (enchantment.SpellCategory == SpellCategory.ManaConversionModRaising)
                            || (enchantment.SpellCategory == SpellCategory.SpellDamageRaising))
                        {
                            SpellBook.Add((uint)enchantment.SpellId | EnchantmentMask);
                        }
                    }
                    else if (wo is Missile || wo is Ammunition)
                    {
                        if ((enchantment.SpellCategory == SpellCategory.DamageRaising)
                            || (enchantment.SpellCategory == SpellCategory.DamageRaisingRare))
                        {
                            SpellBook.Add((uint)enchantment.SpellId | EnchantmentMask);
                        }
                    }
                    else
                    {
                        // Other weapon type Auras
                        if ((enchantment.SpellCategory == SpellCategory.AttackModRaising)
                            || (enchantment.SpellCategory == SpellCategory.AttackModRaisingRare)
                            || (enchantment.SpellCategory == SpellCategory.DamageRaising)
                            || (enchantment.SpellCategory == SpellCategory.DamageRaisingRare)
                            || (enchantment.SpellCategory == SpellCategory.DefenseModRaising)
                            || (enchantment.SpellCategory == SpellCategory.DefenseModRaisingRare)
                            || (enchantment.SpellCategory == SpellCategory.WeaponTimeRaising)
                            || (enchantment.SpellCategory == SpellCategory.WeaponTimeRaisingRare))
                        {
                            SpellBook.Add((uint)enchantment.SpellId | EnchantmentMask);
                        }
                    }
                }
            }
        }

        // The stored per-line marker. The VALUE is frozen plumbing - NEVER change the string: it is
        // baked into live items' LongDesc and four readers key on the exact string (this promoter,
        // the T11 whitelist, ladder diag/migrate). It is stripped before display, so players never
        // see it. Only the const NAME followed the cantrip -> modifier rename (2026-08-28).
        private const string LegacyModifierMarker = "Zone Cantrip:";
        // The section header players DO see. "Cantrips:" -> "Modifiers:" (owner 2026-08-28: the
        // lines are flat stat bonuses, not cantrips - the retail word was a misnomer).
        private const string ModifierSectionHeader = "Modifiers:";
        private const string WeaponDetailsHeader = "Property Details:";

        /// <summary>
        /// Cantrip lines are baked into wo.LongDesc at stamp time (ZoneModifiers.Stamp), so on
        /// armor / clothing / shields / jewelry they render as raw description text - none of those
        /// build a details section, only BuildWeapon does. Owner 2026-08-22: they get their own
        /// "Cantrips:" section. Creature Augmentation used to be lifted in here as a special case
        /// beside them; the fixed gear base that stamped that line was deleted the same day, so
        /// Creature Augs arrives as an ordinary "Zone Cantrip:" line (key 35) like the other augs.
        ///
        /// Lifts them out of the APPRAISAL copy only - wo.LongDesc, the stamped source of truth,
        /// is never touched, so nothing is re-rolled and existing drops re-render correctly on the
        /// next appraise. Bullets stay in STAMP ORDER (owner: no sorting - two identical pieces
        /// must read identically). The "Zone Cantrip:" prefix is a MARKER, not decoration -
        /// FinalizeT11LongDesc's whitelist deletes any line that lacks it - so it stays in the
        /// stored text and is dropped from the render only.
        ///
        /// Weapons carry their own "Property Details:" block pinned to the top; the cantrip group
        /// becomes a SEPARATE section right after it, never folded in. Armor has no other details
        /// entries, so it gets "Cantrips:" alone rather than an empty outer header (owner ruling).
        /// </summary>
        /// <summary>
        /// Live Stat Resolution (plan 3b, READ-ONLY): swap the response copy's int props for the values
        /// the record resolves to against the live ladder. Only keys the client is sent anyway
        /// (AssessmentProperties - the retail Gear* ratings) are substituted; the 502xx custom block is
        /// not an assessment property today and stays off the wire exactly as before. Armor Level is
        /// carried to the client as PropertyInt.ArmorLevel (ArmorProfile holds only the per-damage-type
        /// mods); BuildProperties already added the enchantment armor mod on top of the stamped base,
        /// so the same mod goes on top of the resolved base. wo is never touched.
        /// </summary>
        private void SubstituteZoneResolvedInts(WorldObject wo, ZoneStatResolver.Resolved r)
        {
            // Core four -> Ratings (resolved). A Gear* prop that a CANTRIP LINE produced is REMOVED from the
            // Ratings copy instead (owner 2026-08-23: it showed twice - once in the client's Ratings, once
            // under Cantrips). Display only: the prop stays on the item, combat and character totals read it
            // as before. Gated on the record, so pre-T11 gear never enters this path.
            var lineProps = new HashSet<PropertyInt>();
            foreach (var line in r.Lines)
                if (line.Def?.Ints != null)
                    foreach (var (propId, _) in line.Def.Ints)
                        lineProps.Add((PropertyInt)propId);

            foreach (var kv in r.Ints)
            {
                if (!AssessmentProperties.PropertiesInt.Contains(kv.Key))
                    continue;
                if (lineProps.Contains(kv.Key))
                    PropertiesInt.Remove(kv.Key);
                else
                    PropertiesInt[kv.Key] = kv.Value;
            }

            if (r.ArmorLevel.HasValue && AssessmentProperties.PropertiesInt.Contains(PropertyInt.ArmorLevel))
                PropertiesInt[PropertyInt.ArmorLevel] = r.ArmorLevel.Value + wo.EnchantmentManager.GetArmorMod();
        }

        private const string ReinforcedLineName = "Reinforced";

        /// <param name="resolved">Live Stat Resolution record (null = legacy piece / weapon): when present
        /// the bullets come from the record (record order, specials and Armor Level included, the core
        /// four excluded - they are ratings, not cantrip lines) plus any baked Reinforced text line
        /// (earned + frozen, never in the record). The baked "Zone Cantrip:" text is stripped either way.</param>
        private void PromoteZoneModifierLines(ZoneStatResolver.Resolved resolved = null, WorldObject wo = null, Player examiner = null)
        {
            if (!PropertiesString.TryGetValue(PropertyString.LongDesc, out var ld) || string.IsNullOrEmpty(ld))
                return;
            if (ld.IndexOf(LegacyModifierMarker, StringComparison.Ordinal) < 0)
                return;

            var cantrips = new List<string>();
            var reinforced = new List<string>();
            var rest = new List<string>();
            foreach (var raw in ld.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith(LegacyModifierMarker, StringComparison.Ordinal))
                {
                    var name = line.Substring(LegacyModifierMarker.Length).Trim();
                    if (name.Length == 0)
                        continue;
                    if (resolved == null)
                        cantrips.Add("- " + name);
                    else if (name.StartsWith(ReinforcedLineName, StringComparison.Ordinal))
                        reinforced.Add("- " + name);
                }
                else
                    rest.Add(raw);
            }

            if (resolved != null)
            {
                foreach (var line in resolved.Lines)
                {
                    if (ZoneStatResolver.IsCoreKey(line.Record.Key))
                        continue;
                    cantrips.Add("- " + line.Text);
                }
                cantrips.AddRange(reinforced);
            }

            if (cantrips.Count == 0)
                return;

            // Armor zone lock (owner 2026-08-30, wording approved): same rule as the weapon
            // panel - the lines keep showing full power (items are compared in town), and this
            // pinned line reconciles that with the dormant contribution while the examiner
            // stands outside every authored area.
            if (ACE.Server.Managers.ZoneControl.ZoneControlManager.IsZcGear(wo)
                && ACE.Server.Managers.ZoneControl.ZoneControlManager.WornPowerSuppressed(examiner))
                cantrips.Insert(0, ACE.Server.Managers.ZoneControl.ZoneControlManager.ZoneLockedAppraisalLine);

            // Collapse the blank runs the pulled lines leave behind: the slot-special stamp joins
            // with a blank line, so removing one can leave three consecutive newlines.
            var body = string.Join("\n", rest);
            while (body.Contains("\n\n\n"))
                body = body.Replace("\n\n\n", "\n\n");
            body = body.Trim('\n');

            var block = ModifierSectionHeader + "\n" + string.Join("\n", cantrips);

            if (body.StartsWith(WeaponDetailsHeader, StringComparison.Ordinal))
            {
                var split = body.IndexOf("\n\n", StringComparison.Ordinal);
                PropertiesString[PropertyString.LongDesc] = split >= 0
                    ? body.Substring(0, split) + "\n\n" + block + body.Substring(split)
                    : body + "\n\n" + block;
                return;
            }

            // Non-weapon: the block goes in the USE string, not LongDesc. That is the position
            // lever (owner 2026-08-22 - the section read too low): the client draws Use ABOVE its
            // native sections and LongDesc BELOW them, which is why the original implementation
            // (Ruggan, PR #327) used Use and why moving it to LongDesc pushed it under Spell
            // Descriptions. The wand problem that forced that move does not apply here - armor,
            // clothing, shields and jewelry have no native caster sections to sit on top of.
            PropertiesString[PropertyString.LongDesc] = body;
            if (string.IsNullOrEmpty(body))
                PropertiesString.Remove(PropertyString.LongDesc);

            var useParts = new List<string> { block };
            if (PropertiesString.TryGetValue(PropertyString.Use, out var existingUse) && !string.IsNullOrWhiteSpace(existingUse))
                useParts.Add(existingUse);

            var use = string.Join("\n\n", useParts);

            // Keep a break between the block and whatever the client draws next (same guard the
            // original carried - without it the section runs straight into the following text).
            if (PropertiesInt.ContainsKey(PropertyInt.ItemMaxLevel) ||
                PropertiesInt.ContainsKey(PropertyInt.ItemSpellcraft) ||
                PropertiesInt.ContainsKey(PropertyInt.WieldRequirements))
                use += "\n";

            PropertiesString[PropertyString.Use] = use;
        }

        private void BuildArmor(WorldObject wo)
        {
            if (!Success)
                return;

            ArmorProfile = new ArmorProfile(wo);
            ArmorHighlight = ArmorMaskHelper.GetHighlightMask(wo);
            ArmorColor = ArmorMaskHelper.GetColorMask(wo);

            AddEnchantments(wo);
        }

        private void BuildCreature(Creature creature)
        {
            CreatureProfile = new CreatureProfile(creature, Success);

            // only creatures?
            ResistHighlight = ResistMaskHelper.GetHighlightMask(creature);
            ResistColor = ResistMaskHelper.GetColorMask(creature);

            if (Success && (creature is Player || !creature.Attackable))
                ArmorLevels = new ArmorLevel(creature);

            AddRatings(creature);

            if (NPCLooksLikeObject)
            {
                var weenie = creature.Weenie ?? DatabaseManager.World.GetCachedWeenie(creature.WeenieClassId);

                if (!weenie.GetProperty(PropertyInt.EncumbranceVal).HasValue)
                    PropertiesInt.Remove(PropertyInt.EncumbranceVal);
            }
            else
                PropertiesInt.Remove(PropertyInt.EncumbranceVal);

            // see notes in CombatPet.Init()
            if (creature is CombatPet && PropertiesInt.ContainsKey(PropertyInt.Faction1Bits))
                PropertiesInt.Remove(PropertyInt.Faction1Bits);
        }

        private void AddRatings(Creature creature)
        {
            if (!Success)
                return;

            var damageRating = creature.GetDamageRating();

            // include heritage / weapon type rating?
            var weapon = creature.GetEquippedWeapon() ?? creature.GetEquippedWand();
            if (creature.GetHeritageBonus(weapon))
                damageRating += 5;

            // factor in weakness here?

            var damageResistRating = creature.GetDamageResistRating();

            // factor in nether dot damage here?

            var critRating = creature.GetCritRating();
            var critDamageRating = creature.GetCritDamageRating();

            var critResistRating = creature.GetCritResistRating();
            var critDamageResistRating = creature.GetCritDamageResistRating();

            var healingBoostRating = creature.GetHealingBoostRating();
            var dotResistRating = creature.GetDotResistanceRating();
            var netherResistRating = creature.GetNetherResistRating();

            var lifeResistRating = creature.GetLifeResistRating();  // drain / harm resistance
            var gearMaxHealth = creature.GetGearMaxHealth();

            var pkDamageRating = creature.GetPKDamageRating();
            var pkDamageResistRating = creature.GetPKDamageResistRating();

            if (damageRating != 0)
                PropertiesInt[PropertyInt.DamageRating] = damageRating;
            if (damageResistRating != 0)
                PropertiesInt[PropertyInt.DamageResistRating] = damageResistRating;

            if (critRating != 0)
                PropertiesInt[PropertyInt.CritRating] = critRating;
            if (critDamageRating != 0)
                PropertiesInt[PropertyInt.CritDamageRating] = critDamageRating;

            if (critResistRating != 0)
                PropertiesInt[PropertyInt.CritResistRating] = critResistRating;
            if (critDamageResistRating != 0)
                PropertiesInt[PropertyInt.CritDamageResistRating] = critDamageResistRating;

            if (healingBoostRating != 0)
                PropertiesInt[PropertyInt.HealingBoostRating] = healingBoostRating;
            if (netherResistRating != 0)
                PropertiesInt[PropertyInt.NetherResistRating] = netherResistRating;
            if (dotResistRating != 0)
                PropertiesInt[PropertyInt.DotResistRating] = dotResistRating;

            if (lifeResistRating != 0)
                PropertiesInt[PropertyInt.LifeResistRating] = lifeResistRating;
            if (gearMaxHealth != 0)
                PropertiesInt[PropertyInt.GearMaxHealth] = gearMaxHealth;

            if (pkDamageRating != 0)
                PropertiesInt[PropertyInt.PKDamageRating] = pkDamageRating;
            if (pkDamageResistRating != 0)
                PropertiesInt[PropertyInt.PKDamageResistRating] = pkDamageResistRating;

            // add ratings from equipped items?
        }

        private void BuildWeapon(WorldObject weapon, Player examiner)
        {
            if (!Success)
                return;

            var weaponProfile = new WeaponProfile(weapon, examiner);

            //WeaponHighlight = WeaponMaskHelper.GetHighlightMask(weapon, wielder);
            //WeaponColor = WeaponMaskHelper.GetColorMask(weapon, wielder);
            WeaponHighlight = WeaponMaskHelper.GetHighlightMask(weaponProfile);
            WeaponColor = WeaponMaskHelper.GetColorMask(weaponProfile);

            if (!(weapon is Caster))
                WeaponProfile = weaponProfile;

            // Add split arrow properties to appraisal
            var hasSplitArrows = weapon.GetProperty(PropertyBool.SplitArrows);
            if (hasSplitArrows == true)
            {
                PropertiesBool[PropertyBool.SplitArrows] = true;

                var splitCount = weapon.GetProperty(PropertyInt.SplitArrowCount) ?? Creature.DEFAULT_SPLIT_ARROW_COUNT;
                PropertiesInt[PropertyInt.SplitArrowCount] = splitCount;

                var splitRange = weapon.GetProperty(PropertyFloat.SplitArrowRange) ?? Creature.DEFAULT_SPLIT_ARROW_RANGE;
                PropertiesFloat[PropertyFloat.SplitArrowRange] = splitRange;

                var damageMultiplier = weapon.GetProperty(PropertyFloat.SplitArrowDamageMultiplier) ?? Creature.DEFAULT_SPLIT_ARROW_DAMAGE_MULTIPLIER;
                PropertiesFloat[PropertyFloat.SplitArrowDamageMultiplier] = damageMultiplier;
            }

            // Add all the descriptive enhancements
            var effectDescriptions = new List<string>();

            // Determine skill: explicit WeaponSkill, or fallback for Casters (Wands)
            var checkSkill = weapon.WeaponSkill;
            if (checkSkill == Skill.None && weapon is Caster)
            {
                uint warBase = examiner.GetCreatureSkill(Skill.WarMagic).Base;
                uint voidBase = examiner.GetCreatureSkill(Skill.VoidMagic).Base;
                checkSkill = (voidBase > warBase) ? Skill.VoidMagic : Skill.WarMagic;
            }

            CreatureSkill skill = examiner.GetCreatureSkill(checkSkill);

            // Slayer. The forge-only all-creatures flag (PropertyBool 50052) has no species to name.
            var slayerAll = weapon.GetProperty(PropertyBool.SlayerAllCreatures) == true;
            if (weapon.SlayerCreatureType.HasValue || slayerAll)
            {
                var bonus = weapon.SlayerDamageBonus ?? 1.0;
                var niceName = slayerAll
                    ? "All Creatures"
                    : CreatureNameRegex().Replace(weapon.SlayerCreatureType.ToString(), " $1");
                effectDescriptions.Add($"- {niceName} Slayer: {bonus:0.##}x Damage");
            }

            // Biting Strike
            if (weapon.CriticalFrequency.HasValue)
            {
                var val = weapon.CriticalFrequency.Value;
                effectDescriptions.Add($"- Biting Strike: +{val:P0} Crit Chance");
            }

            // Cleaving (multi-target): the raw prop stores TOTAL targets (extra + 1), so raw-prop
            // readouts look one higher than authored — this line shows the true extra-target count.
            if (weapon.IsCleaving)
                effectDescriptions.Add($"- Cleaving: +{weapon.CleaveTargets} Targets");

            // Resistance Cleaving (Fixed Resistance Modifier)
            if (weapon.ResistanceModifier.HasValue && weapon.ResistanceModifierType.HasValue)
            {
                var typeName = weapon.ResistanceModifierType.Value.DisplayName();
                var val = weapon.ResistanceModifier.Value;
                effectDescriptions.Add($"- {typeName} Cleaving: +{val:P0} Dmg (Vuln)");
            }

            // Armor Cleaving 
            if (weapon.GetProperty(PropertyFloat.IgnoreArmor).HasValue)
            {
                var mod = weapon.GetArmorCleavingMod();
                var reduction = 1.0f - mod;
                effectDescriptions.Add($"- Armor Cleaving: {reduction:P0} Armor Ignored");
            }

            // Split Arrow
            if (weapon.GetProperty(PropertyBool.SplitArrows) == true)
            {
                var count = weapon.GetProperty(PropertyInt.SplitArrowCount) ?? Creature.DEFAULT_SPLIT_ARROW_COUNT;
                var val = weapon.GetProperty(PropertyFloat.SplitArrowDamageMultiplier) ?? Creature.DEFAULT_SPLIT_ARROW_DAMAGE_MULTIPLIER;
                effectDescriptions.Add($"- Split Arrow: +{count} Targets, {val:P0} Dmg");
            }

            // Crushing Blow: engine crit damage = 1 + CriticalMultiplier, so the true multiplier the
            // player actually deals is prop + 1 (a stored 1.0 = normal 2x crit).
            if (weapon.GetProperty(PropertyFloat.CriticalMultiplier) > 1.0f)
            {
                var val = weapon.GetProperty(PropertyFloat.CriticalMultiplier).Value + 1.0;
                effectDescriptions.Add($"- Crushing Blow: {val:0.##}x Crit Dmg");
            }

            // Crippling Blow - hidden for players since 2026-08-25: WorldObject.CritImbuesSuppressed
            // makes it inert on a player's weapon, and an item panel that advertises an effect doing
            // exactly zero is worse for trust than an absent line. This panel is only ever built for
            // a player examining something, so the const alone is the right test here.
            if (weapon.HasImbuedEffect(ImbuedEffectType.CripplingBlow)
                && !WorldObject.CritImbuesSuppressedForPlayers)
            {
                var mod = WorldObject.GetCripplingBlowMod(skill);
                effectDescriptions.Add($"- {ImbuedEffectType.CripplingBlow.DisplayName()}: {mod:0.##}x Crit Dmg");
            }

            // Armor Rending: fraction of the target's armor IGNORED. Zone Control loot carries a
            // per-weapon override (ArmorRendOverridePropId) = the configured fraction directly, which
            // DamageEvent uses at hit time; retail weapons use the skill formula, which returns armor
            // REMAINING, so the ignored fraction is 1 - that. (Old code showed the skill 'remaining'
            // value mislabeled as "Ignored" and never reflected the Zone override.)
            if (weapon.HasImbuedEffect(ImbuedEffectType.ArmorRending))
            {
                var rendOverride = weapon.GetProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ArmorRendOverridePropId);
                var ignored = rendOverride.HasValue
                    ? Math.Clamp(rendOverride.Value, 0.0, 1.0)
                    : 1.0 - WorldObject.GetArmorRendingMod(skill);
                effectDescriptions.Add($"- {ImbuedEffectType.ArmorRending.DisplayName()}: {ignored:P1} Ignored");
            }

            // Critical Strike - hidden for players, same reason as Crippling Blow above.
            if (weapon.HasImbuedEffect(ImbuedEffectType.CriticalStrike)
                && !WorldObject.CritImbuesSuppressedForPlayers)
            {
                var mod = WorldObject.GetCriticalStrikeMod(skill);
                effectDescriptions.Add($"- {ImbuedEffectType.CriticalStrike.DisplayName()}: +{mod:P1} Crit Chance");
            }

            // Elemental Rendings
            var rendings = new ImbuedEffectType[]
            {
                ImbuedEffectType.SlashRending,
                ImbuedEffectType.PierceRending,
                ImbuedEffectType.BludgeonRending,
                ImbuedEffectType.AcidRending,
                ImbuedEffectType.ColdRending,
                ImbuedEffectType.ElectricRending,
                ImbuedEffectType.FireRending,
                ImbuedEffectType.NetherRending
            };

            foreach (var type in rendings)
            {
                if (weapon.HasImbuedEffect(type))
                {
                    var mod = WorldObject.GetRendingMod(skill);

                    // Zone Control loot: the rend power override substitutes for the skill formula
                    // (rendingMod = 1 + override), mirroring GetWeaponResistanceModifier, so the tooltip
                    // shows exactly the configured strength (e.g. wire 7.0 -> +700% Dmg).
                    var rendOverride = weapon.GetProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.RendingModOverridePropId);
                    if (rendOverride.HasValue && rendOverride.Value > 0)
                        mod = 1.0f + (float)rendOverride.Value;

                    // NO "(vuln)" SUFFIX (owner 2026-08-27: "Rend is not a vuln, text should not be
                    // there"). It shared a slot with the vuln multiplier internally - they are MAX'd,
                    // never stacked - but that is an implementation detail of the resist chain and has
                    // no business on a player-facing line. A rend is a rend.
                    var bonusPct = (mod - 1.0);
                    effectDescriptions.Add($"- {type.DisplayName()}: +{bonusPct:P0} Dmg");
                }
            }

            // Shield Cleaving: fraction of the target's shield AL the weapon ignores. Stored directly on
            // the weapon (PropertyFloat.IgnoreShield); GetIgnoreShieldMod reads it at hit time.
            if (weapon.IgnoreShield.HasValue && weapon.IgnoreShield.Value > 0)
                effectDescriptions.Add($"- Shield Cleaving: {Math.Clamp(weapon.IgnoreShield.Value, 0.0, 1.0):P0} Shield Ignored");

            // Phantom (hollow): the weapon bypasses the target's protective magic - Impen/Banes on armor
            // and Life prots. RETAIL ONLY as of 2026-08-25: our loot card was deleted, so every weapon
            // reaching this line is a retail hollow weapon (~830 of them) or one a GM made by hand. Some
            // set only one of the two properties, but it is the same category, so one line covers both.
            if (weapon.IgnoreMagicArmor || weapon.IgnoreMagicResist)
                effectDescriptions.Add("- Phantom: Ignores Magic Protections");

            // Calculate Effective Melee Defense
            var meleeSkill = examiner.GetCreatureSkill(Skill.MeleeDefense).Current;
            var wepMeleeDef = (float)(weapon.WeaponDefense ?? 1.0f);
            if (weapon.WeaponDefense > 0 && weapon.WeaponDefense < 1 && ((weapon.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 4) != 0)
                wepMeleeDef += 1;

            var meleeMod = wepMeleeDef + weapon.EnchantmentManager.GetDefenseMod();
            if (weapon.IsEnchantable)
                meleeMod += examiner.EnchantmentManager.GetDefenseMod();

            var meleeImbues = examiner.GetDefenseImbues(ImbuedEffectType.MeleeDefense);
            var meleeLum = examiner.LuminanceAugmentMeleeDefenseCount ?? 0;
            var burdenMod = examiner.GetBurdenMod();

            // Option B: stanceMod = 1.0f, not exhausted
            uint emdVal = (uint)Math.Round(meleeSkill * meleeMod * burdenMod * 1.0f + meleeImbues + meleeLum);

            effectDescriptions.Sort();

            // Weapon Grade pinned ABOVE the sorted list; wield gate pinned to the very BOTTOM
            // (owner 2026-08-01). Weapons carry these here instead of in the description block
            // (armor/jewelry have no Property Details section and keep the LongDesc line).
            var wsQuality = weapon.GetProperty(PropertyInt.WeaponAugScaleQuality);
            if (wsQuality != null)
            {
                // Sub-grade label (owner 2026-08-03): k and variance both resolve per SUB-grade
                // now, so the label has to carry the same granularity — "B+" is a different
                // weapon from "B-", and showing both as "B" would hide a real damage step.
                var wsGrade = ACE.Server.Managers.WeaponScaling.WeaponScalingManager.GetQualitySubGrade(wsQuality.Value);

                // The percent is DAMAGE relative to a perfect roll, not the quality percentile
                // (owner 2026-08-06). quality/10 was wrong twice over: an F- read "0 pct of max"
                // while dealing 41.7 pct of an S weapon's damage, and two mechanically identical
                // B+ weapons read 86 pct and 88 pct. Resolved off the SAME config the combat path
                // reads, so the number cannot drift from what the weapon actually hits for.
                var wsFamily = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetFamilyKey(weapon);
                var wsCfg = ACE.Server.Managers.WeaponScaling.WeaponScalingManager.Current;
                if (wsFamily != null && wsCfg.Scripts.TryGetValue(wsFamily, out var wsScript))
                {
                    var wsPct = ACE.Server.Managers.WeaponScaling.WeaponScalingManager
                        .RelativeDamagePercent(wsScript, wsCfg.TightenStrength, wsQuality.Value);
                    // "of max DAMAGE" (owner 2026-08-30): the percent measures dealt damage vs a
                    // perfect roll, NOT the quality percentile, and family ladders have different
                    // spreads - a bow F- honestly deals 72% of a perfect bow. Without the word
                    // "damage" that read as a display bug ("how is the worst grade 72%?").
                    effectDescriptions.Insert(0, $"- Weapon Grade: {wsGrade} ({wsPct}% of max damage)");
                }
                else
                    effectDescriptions.Insert(0, $"- Weapon Grade: {wsGrade}");
            }

            // Zone lock (owner 2026-08-30, "add the dormant line"): when the lock is ON and THIS
            // examiner is standing outside every authored area, say so at the very top - the
            // panel keeps showing the item's full power on purpose (players compare and trade in
            // town, where gated numbers would make every drop read as junk), so this line is
            // what reconciles the big numbers with the small hits. Absent when the lock is off,
            // when the item is not ZC-stamped, or inside an authored area.
            if (ACE.Server.Managers.ZoneControl.ZoneControlManager.WeaponPowerSuppressed(weapon, examiner))
                effectDescriptions.Insert(0, ACE.Server.Managers.ZoneControl.ZoneControlManager.ZoneLockedAppraisalLine);

            // Cast on Strike (owner 2026-08-27: "Appraisal line should show Force Arc (13% proc chance)").
            // One line PER SLOT - the arc and the ring are separate entities with separate rates, so a
            // single merged line would hide which one a given rate belongs to. Names come from our own
            // table, never from Spell.Name: the whole point is to drop the level marker.
            var zcArcB = weapon.GetProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcArcDamagePropId);
            var zcRingB = weapon.GetProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcRingDamagePropId);
            if ((zcArcB ?? 0) > 0 || (zcRingB ?? 0) > 0)
            {
                if (weapon.ProcSpell.HasValue &&
                    ACE.Server.Managers.ZoneControl.ZoneLootMutator.TryGetProcDisplayName(weapon.ProcSpell.Value, out var arcName))
                    effectDescriptions.Add($"- Cast on Strike: {arcName} ({(weapon.ProcSpellRate ?? 0f) * 100f:0.#}% proc chance)");

                if (weapon.ProcSpell2.HasValue &&
                    ACE.Server.Managers.ZoneControl.ZoneLootMutator.TryGetProcDisplayName(weapon.ProcSpell2.Value, out var ringName))
                    effectDescriptions.Add($"- Cast on Strike: {ringName} ({(weapon.ProcSpellRate2 ?? 0f) * 100f:0.#}% proc chance)");
            }

            effectDescriptions.Add($"- Effective Melee Defense: {emdVal}");

            if (weapon.WieldRequirements == WieldRequirement.Int64Stat &&
                weapon.WieldSkillType == (int)PropertyInt64.LumAugItemCount)
                effectDescriptions.Add($"- Wield requires: {weapon.WieldDifficulty ?? 0:N0} Item Augmentations");

            // T16+ charm wield gates in slots 3/4 (owner 2026-08-15)
            if (weapon.WieldRequirements3 == WieldRequirement.Int64Stat && weapon.WieldSkillType3 != null)
                effectDescriptions.Add($"- Wield requires: {weapon.WieldDifficulty3 ?? 0:N0} {CharmCounterName((PropertyInt64)weapon.WieldSkillType3.Value)}");
            if (weapon.WieldRequirements4 == WieldRequirement.Int64Stat && weapon.WieldSkillType4 != null)
                effectDescriptions.Add($"- Wield requires: {weapon.WieldDifficulty4 ?? 0:N0} {CharmCounterName((PropertyInt64)weapon.WieldSkillType4.Value)}");

            // Property Details renders via the LONG DESCRIPTION, not the Use string (owner
            // 2026-08-01): the client draws its native caster sections (Mana Conversion, "Damage
            // bonus for X spells") AFTER the Use text, so a Use-carried block sat ABOVE them on
            // wands. Prepending to LongDesc puts the block below every native section and above
            // the per-viewer bonus line + provenance (which were inserted earlier in the build).
            var detailsBlock = $"Property Details:\n{string.Join("\n", effectDescriptions)}";
            if (PropertiesString.TryGetValue(PropertyString.LongDesc, out var ldExisting) && !string.IsNullOrEmpty(ldExisting))
                PropertiesString[PropertyString.LongDesc] = $"{detailsBlock}\n\n{ldExisting}";
            else
                PropertiesString[PropertyString.LongDesc] = detailsBlock;

            // item enchantments can also be on wielder currently
            AddEnchantments(weapon);
        }

        /// <summary>Display name for a growth-charm counter used as a T16+ wield gate.</summary>
        private static string CharmCounterName(PropertyInt64 prop)
        {
            switch (prop)
            {
                case PropertyInt64.TriuneWeaveCount: return "Triune Weave";
                case PropertyInt64.BattlemagesWrathCharmCount: return "Battlemage's Wrath";
                case PropertyInt64.NetherVeilCharmCount: return "Nether Veil";
                case PropertyInt64.CrashingSteelCharmCount: return "Crashing Steel";
                case PropertyInt64.TrueShotCharmCount: return "True Shot";
                default: return prop.ToString();
            }
        }

        private void BuildHookProfile(WorldObject hookedItem)
        {
            HookProfile = new HookProfile();
            if (hookedItem.Inscribable)
                HookProfile.Flags |= HookFlags.Inscribable;
            if (hookedItem is Healer)
                HookProfile.Flags |= HookFlags.IsHealer;
            if (hookedItem is Food)
                HookProfile.Flags |= HookFlags.IsFood;
            if (hookedItem is Lockpick)
                HookProfile.Flags |= HookFlags.IsLockpick;
            if (hookedItem.ValidLocations != null)
                HookProfile.ValidLocations = hookedItem.ValidLocations.Value;
            if (hookedItem.AmmoType != null)
                HookProfile.AmmoType = hookedItem.AmmoType.Value;
        }

        /// <summary>
        /// Constructs the bitflags for appraising a WorldObject
        /// </summary>
        private void BuildFlags()
        {
            if (PropertiesInt.Count > 0)
                Flags |= IdentifyResponseFlags.IntStatsTable;
            if (PropertiesInt64.Count > 0)
                Flags |= IdentifyResponseFlags.Int64StatsTable;         				
			if (PropertiesBool.Count > 0)
                Flags |= IdentifyResponseFlags.BoolStatsTable;
            if (PropertiesFloat.Count > 0)
                Flags |= IdentifyResponseFlags.FloatStatsTable;
            if (PropertiesString.Count > 0)
                Flags |= IdentifyResponseFlags.StringStatsTable;
            if (PropertiesDID.Count > 0)
                Flags |= IdentifyResponseFlags.DidStatsTable;
            if (SpellBook.Count > 0)
                Flags |= IdentifyResponseFlags.SpellBook;

            if (ResistHighlight != 0)
                Flags |= IdentifyResponseFlags.ResistEnchantmentBitfield;
            if (ArmorProfile != null)
                Flags |= IdentifyResponseFlags.ArmorProfile;
            if (CreatureProfile != null && !NPCLooksLikeObject)
                Flags |= IdentifyResponseFlags.CreatureProfile;
            if (WeaponProfile != null)
                Flags |= IdentifyResponseFlags.WeaponProfile;
            if (HookProfile != null)
                Flags |= IdentifyResponseFlags.HookProfile;
            if (ArmorHighlight != 0)
                Flags |= IdentifyResponseFlags.ArmorEnchantmentBitfield;
            if (WeaponHighlight != 0)
                Flags |= IdentifyResponseFlags.WeaponEnchantmentBitfield;
            if (ArmorLevels != null)
                Flags |= IdentifyResponseFlags.ArmorLevels;
        }
    }

    public static class AppraiseInfoExtensions
    {
        /// <summary>
        /// Writes the AppraiseInfo to the network stream
        /// </summary>
        public static void Write(this BinaryWriter writer, AppraiseInfo info)
        {
            writer.Write((uint)info.Flags);
            writer.Write(Convert.ToUInt32(info.Success));
            if (info.Flags.HasFlag(IdentifyResponseFlags.IntStatsTable))
                writer.Write(info.PropertiesInt);
            if (info.Flags.HasFlag(IdentifyResponseFlags.Int64StatsTable))
                writer.Write(info.PropertiesInt64);
            if (info.Flags.HasFlag(IdentifyResponseFlags.BoolStatsTable))
                writer.Write(info.PropertiesBool);
            if (info.Flags.HasFlag(IdentifyResponseFlags.FloatStatsTable))
                writer.Write(info.PropertiesFloat);
            if (info.Flags.HasFlag(IdentifyResponseFlags.StringStatsTable))
                writer.Write(info.PropertiesString);
            if (info.Flags.HasFlag(IdentifyResponseFlags.DidStatsTable))
                writer.Write(info.PropertiesDID);
            if (info.Flags.HasFlag(IdentifyResponseFlags.SpellBook))
                writer.Write(info.SpellBook);
            if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorProfile))
                writer.Write(info.ArmorProfile);
            if (info.Flags.HasFlag(IdentifyResponseFlags.CreatureProfile))
                writer.Write(info.CreatureProfile);
            if (info.Flags.HasFlag(IdentifyResponseFlags.WeaponProfile))
                writer.Write(info.WeaponProfile);
            if (info.Flags.HasFlag(IdentifyResponseFlags.HookProfile))
                writer.Write(info.HookProfile);
            if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorEnchantmentBitfield))
            {
                writer.Write((ushort)info.ArmorHighlight);
                writer.Write((ushort)info.ArmorColor);
            }
            if (info.Flags.HasFlag(IdentifyResponseFlags.WeaponEnchantmentBitfield))
            {
                writer.Write((ushort)info.WeaponHighlight);
                writer.Write((ushort)info.WeaponColor);
            }
            if (info.Flags.HasFlag(IdentifyResponseFlags.ResistEnchantmentBitfield))
            {
                writer.Write((ushort)info.ResistHighlight);
                writer.Write((ushort)info.ResistColor);
            }
            if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorLevels))
                writer.Write(info.ArmorLevels);
        }

        private static readonly PropertyIntComparer PropertyIntComparer = new PropertyIntComparer(16);
        private static readonly PropertyInt64Comparer PropertyInt64Comparer = new PropertyInt64Comparer(8);
        private static readonly PropertyBoolComparer PropertyBoolComparer = new PropertyBoolComparer(8);
        private static readonly PropertyFloatComparer PropertyFloatComparer = new PropertyFloatComparer(8);
        private static readonly PropertyStringComparer PropertyStringComparer = new PropertyStringComparer(8);
        private static readonly PropertyDataIdComparer PropertyDataIdComparer = new PropertyDataIdComparer(8);

        // TODO: generics
        public static void Write(this BinaryWriter writer, Dictionary<PropertyInt, int> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyIntComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyInt, int>(_properties, PropertyIntComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyInt64, long> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyInt64Comparer.NumBuckets);

            var properties = new SortedDictionary<PropertyInt64, long>(_properties, PropertyInt64Comparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyBool, bool> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyBoolComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyBool, bool>(_properties, PropertyBoolComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(Convert.ToUInt32(kvp.Value));
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyFloat, double> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyFloatComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyFloat, double>(_properties, PropertyFloatComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyString, string> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyStringComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyString, string>(_properties, PropertyStringComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.WriteString16L(kvp.Value);
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyDataId, uint> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyDataIdComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyDataId, uint>(_properties, PropertyDataIdComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(kvp.Value);
            }
        }
    }
}
