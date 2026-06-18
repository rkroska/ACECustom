using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Command;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;
using ACE.Entity.Models;

namespace ACE.Server.Command.Handlers
{
    public static class TestCharacterCommands
    {
        [CommandHandler("testchar", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Boost character stats/gear/weapons to Tier 11 or Tier 10, or reset to Tier 0.",
            "Usage: /testchar <T11|T10|T0>  |  /testchar stats <T11|T10>  |  /testchar gear <tier>  |  /testchar weapons <tier> [style]  |  /testchar gems <tier>  |  /testchar charms")]
        public static void HandleTestChar(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player == null) return;

            if (parameters.Length == 1)
            {
                // Full booster package (stats + gear + weapons)
                var tier = parameters[0].ToUpper();
                if (tier == "T0" || tier == "0")
                {
                    ResetToTier0(player);
                    player.SendMessage("Character successfully reset to Tier 0 baseline! Please log out and back in to completely refresh your client spellbook.");
                    player.SaveBiotaToDatabase();
                    return;
                }

                bool isT10 = (tier == "T10" || tier == "10");
                if (!isT10 && tier != "T11" && tier != "11")
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Supported tiers: T11, T10, T0. Usage: /testchar T11 or /testchar T10 or /testchar T0", ChatMessageType.System));
                    return;
                }

                if (isT10)
                    ConfigureStatsAndSpellsT10(player);
                else
                    ConfigureStatsAndSpells(player);

                CreateCustomBow(player, DamageType.Cold, true);
                CreateCustomBow(player, DamageType.Fire, true);
                CreateCustomBow(player, DamageType.Electric, true);
                CreateCustomBow(player, DamageType.Acid, true);
                CreateCustomBow(player, DamageType.Slash, true);
                CreateCustomBow(player, DamageType.Pierce, true);
                CreateCustomBow(player, DamageType.Bludgeon, true);
                CreateCustomBow(player, DamageType.Nether, true);

                CreateCustomUA(player, DamageType.Cold);
                CreateCustomUA(player, DamageType.Fire);
                CreateCustomUA(player, DamageType.Electric);
                CreateCustomUA(player, DamageType.Acid);
                CreateCustomUA(player, DamageType.Slash);
                CreateCustomUA(player, DamageType.Pierce);
                CreateCustomUA(player, DamageType.Bludgeon);
                CreateCustomUA(player, DamageType.Nether);

                CreateCustomWand(player, DamageType.Cold);
                CreateCustomWand(player, DamageType.Fire);
                CreateCustomWand(player, DamageType.Electric);
                CreateCustomWand(player, DamageType.Acid);
                CreateCustomWand(player, DamageType.Slash);
                CreateCustomWand(player, DamageType.Pierce);
                CreateCustomWand(player, DamageType.Bludgeon);
                CreateCustomWand(player, DamageType.Nether);

                // Spawn booster packs 7 down to 3 empty
                for (int i = 7; i >= 3; i--)
                {
                    var packName = $"Booster Pack {i}";
                    if (HasItemNamed(player, packName)) continue;
                    var emptyBag = WorldObjectFactory.CreateNewWorldObject(310025) as Container;
                    if (emptyBag != null)
                    {
                        emptyBag.Name = packName;
                        emptyBag.SetProperty(PropertyString.Name, packName);
                        player.TryCreateInInventoryWithNetworking(emptyBag);
                    }
                }

                // Spawn Booster Pack 2 containing hilts
                var bag2 = player.GetAllPossessionsDeep().OfType<Container>().FirstOrDefault(c => c.Name == "Booster Pack 2");
                if (bag2 == null)
                {
                    bag2 = WorldObjectFactory.CreateNewWorldObject(310025) as Container;
                    if (bag2 != null)
                    {
                        bag2.Name = "Booster Pack 2";
                        bag2.SetProperty(PropertyString.Name, "Booster Pack 2");
                        SpawnHilts(player, bag2);
                        player.TryCreateInInventoryWithNetworking(bag2);
                    }
                }
                else
                {
                    SpawnHilts(player, bag2);
                }

                // Spawn Booster Pack 1 containing portal gems
                var bag1 = player.GetAllPossessionsDeep().OfType<Container>().FirstOrDefault(c => c.Name == "Booster Pack 1");
                if (bag1 == null)
                {
                    bag1 = WorldObjectFactory.CreateNewWorldObject(310025) as Container;
                    if (bag1 != null)
                    {
                        bag1.Name = "Booster Pack 1";
                        bag1.SetProperty(PropertyString.Name, "Booster Pack 1");
                        SpawnTeleportGems(player, bag1);
                        player.TryCreateInInventoryWithNetworking(bag1);
                    }
                }
                else
                {
                    SpawnTeleportGems(player, bag1);
                }

                var gearTier = isT10 ? "T10" : "T11";
                SpawnOlthoiShadowArmor(player, gearTier);
                SpawnCustomUndergarmentsAndCloak(player, gearTier);
                SpawnCustomJewelry(player, gearTier);

                // Add and auto-equip Infinite Deadly Prismatic Arrow
                bool hasArrow = player.GetAllPossessionsDeep().Any(i => i.WeenieClassId == 4395100);
                if (!hasArrow)
                {
                    var arrow = WorldObjectFactory.CreateNewWorldObject(4395100);
                    if (arrow != null)
                    {
                        AddItemToInventory(player, arrow);
                    }
                }

                // Add Aetherias
                SpawnAetherias(player);

                // Add all charms (T1) + Charm Catalysts
                SpawnCharms(player);

                var tierLabel = isT10 ? "Tier 10" : "Tier 11";
                player.SendMessage($"Character fully configured with {tierLabel} stats, elemental weapons, Olthoi Infused Shadow armor, custom undergarments, cloak, jewelry, Infinite Arrow, Aetherias, and all ability charms!");
                player.SaveBiotaToDatabase();
            }
            else if (parameters.Length >= 2)
            {
                var sub = parameters[0].ToLower();
                var tier = parameters[1].ToUpper();

                if (tier == "T0" || tier == "0")
                {
                    if (sub == "stats")
                    {
                        ResetToTier0(player);
                        player.SendMessage("Character stats, skills, augmentations, and spellbook reset to Tier 0 baseline! Please log out and back in to completely refresh your client spellbook.");
                        player.SaveBiotaToDatabase();
                    }
                    else if (sub == "gear")
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("No gear is defined for Tier 0.", ChatMessageType.System));
                    }
                    else if (sub == "weapons")
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("No weapons are defined for Tier 0.", ChatMessageType.System));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Unknown subcommand. Usage: /testchar <tier> | /testchar [stats|gear|weapons|gems] <tier> [style]", ChatMessageType.System));
                    }
                    return;
                }

                bool isT10 = (tier == "T10" || tier == "10");
                if (!isT10 && tier != "T11" && tier != "11")
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Supported tiers: T11, T10, T0. Usage: /testchar {sub} T11 or /testchar {sub} T10", ChatMessageType.System));
                    return;
                }

                if (sub == "stats")
                {
                    if (isT10)
                        ConfigureStatsAndSpellsT10(player);
                    else
                        ConfigureStatsAndSpells(player);
                    var statsTierLabel = isT10 ? "Tier 10" : "Tier 11";
                    player.SendMessage($"Character stats, skills, augmentations, and spells configured for {statsTierLabel}!");
                    player.SaveBiotaToDatabase();
                }
                else if (sub == "gear")
                {
                    var gearTier = isT10 ? "T10" : "T11";
                    SpawnArmor(player);
                    SpawnOlthoiShadowArmor(player, gearTier);
                    SpawnCustomUndergarmentsAndCloak(player, gearTier);
                    SpawnCustomJewelry(player, gearTier);

                    // Add and auto-equip Infinite Deadly Prismatic Arrow
                    bool hasArrow = player.GetAllPossessionsDeep().Any(i => i.WeenieClassId == 4395100);
                    if (!hasArrow)
                    {
                        var arrow = WorldObjectFactory.CreateNewWorldObject(4395100);
                        if (arrow != null)
                        {
                            AddItemToInventory(player, arrow);
                        }
                    }

                    // Add Aetherias
                    SpawnAetherias(player);

                    player.SendMessage("Prismatic GSA armor, Olthoi Infused Shadow armor (No Cloak), custom undergarments, custom cloak, custom jewelry, Infinite Arrow, and Aetherias generated and equipped.");
                    player.SaveBiotaToDatabase();
                }
                else if (sub == "weapons")
                {
                    string style = null;
                    if (parameters.Length >= 3)
                    {
                        var argStyle = parameters[2].ToUpper();
                        if (argStyle == "UA" || argStyle == "2H" || argStyle == "BOW" || argStyle == "WAND")
                        {
                            style = argStyle;
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Invalid weapon style. Supported styles: UA, 2H, Bow, Wand.", ChatMessageType.System));
                            return;
                        }
                    }

                    SpawnWeapons(player, style);
                    var styleLabel = style != null ? $"{style} " : "";
                    player.SendMessage($"Weapons generated in your inventory.");
                    player.SaveBiotaToDatabase();
                }
                else if (sub == "gems")
                {
                    SpawnTeleportGems(player);
                    player.SendMessage("Custom Teleport Gems generated in your inventory.");
                    player.SaveBiotaToDatabase();
                }
                else if (sub == "charms")
                {
                    SpawnCharms(player);
                    player.SendMessage("All ability charms (T1) and Charm Catalysts added to your inventory.");
                    player.SaveBiotaToDatabase();
                }
                else if (sub == "print")
                {
                    if (parameters.Length >= 2 && uint.TryParse(parameters[1], out var wcid))
                    {
                        var wo = WorldObjectFactory.CreateNewWorldObject(wcid);
                        if (wo != null)
                        {
                            player.SendMessage($"--- WorldObject Properties for Weenie {wcid} ({wo.Name}) ---");
                            foreach (var prop in wo.GetAllPropertyInt())
                                player.SendMessage($"  Int {prop.Key} ({(int)prop.Key}): {prop.Value}");
                            foreach (var prop in wo.GetAllPropertyBools())
                                player.SendMessage($"  Bool {prop.Key} ({(int)prop.Key}): {prop.Value}");
                            foreach (var prop in wo.GetAllPropertyFloat())
                                player.SendMessage($"  Float {prop.Key} ({(int)prop.Key}): {prop.Value}");
                            foreach (var prop in wo.GetAllPropertyDataId())
                                player.SendMessage($"  DataId {prop.Key} ({(int)prop.Key}): {prop.Value}");
                            foreach (var prop in wo.GetAllPropertyString())
                                player.SendMessage($"  String {prop.Key} ({(int)prop.Key}): {prop.Value}");
                            wo.Destroy();
                        }
                        else
                        {
                            player.SendMessage($"Weenie {wcid} could not be created.");
                        }
                    }
                    else
                    {
                        player.SendMessage("Usage: /testchar print <wcid>");
                    }
                }
                else
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Unknown subcommand. Usage: /testchar <tier> | /testchar [stats|gear|weapons|gems|print] <tier> [style]", ChatMessageType.System));
                }
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /testchar <tier> | /testchar [stats|gear|weapons|gems] <tier> [style]", ChatMessageType.System));
            }
        }

        private static void ResetToTier0(Player player)
        {
            // 1. Reset Base Attributes to 10 starting value and 0 raised ranks
            foreach (var attrType in player.Attributes.Keys)
            {
                if (!player.Attributes.TryGetValue(attrType, out var attr))
                    continue;

                attr.StartingValue = 10;
                player.SetAttributeRank(attr, 0);
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, attr));
            }

            // 2. Reset Secondary Vitals to 10 starting value and 0 raised ranks
            foreach (var vitalType in player.Vitals.Keys)
            {
                if (!player.Vitals.TryGetValue(vitalType, out var vital))
                    continue;

                vital.StartingValue = 10;
                vital.Ranks = 0;
                vital.ExperienceSpent = 0;
                vital.Current = vital.MaxValue;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, vital));
            }

            // 3. Reset Level to 1
            player.Level = 1;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Level, 1));

            // 4. Reset Experience and Luminance to 0
            player.AvailableExperience = 0;
            player.TotalExperience = 0;
            player.TotalExperienceDouble = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableExperience, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.TotalExperience, 0));

            player.AvailableLuminance = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableLuminance, 0));

            // 5. Reset Skill Credits to 46 (starting amount)
            player.AvailableSkillCredits = 46;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, 46));

            // 6. Reset all Skills to Untrained with 0 ranks and 0 spent XP
            foreach (var skill in player.Skills.Values)
            {
                skill.AdvancementClass = SkillAdvancementClass.Untrained;
                skill.InitLevel = 0;
                skill.Ranks = 0;
                skill.ExperienceSpent = 0;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, skill));
            }

            // 7. Reset Custom Augmentations (Luminance Augmentations) to 0
            player.LuminanceAugmentCreatureCount = 0;
            player.LuminanceAugmentItemCount = 0;
            player.LuminanceAugmentLifeCount = 0;
            player.LuminanceAugmentWarCount = 0;
            player.LuminanceAugmentVoidCount = 0;
            player.LuminanceAugmentSpellDurationCount = 0;
            player.LuminanceAugmentSpecializeCount = 0;
            player.LuminanceAugmentSummonCount = 0;
            player.LuminanceAugmentMeleeCount = 0;
            player.LuminanceAugmentMissileCount = 0;

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugCreatureCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugItemCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugLifeCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugWarCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugVoidCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugDurationCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugSpecializeCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugSummonCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugMeleeCount, 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugMissileCount, 0));

            // 8. Reset Retail Augmentations to 0
            foreach (var kvp in AugmentationDevice.MaxAugs)
            {
                var augProp = AugmentationDevice.AugProps[kvp.Key];
                player.SetProperty(augProp, 0);
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, augProp, 0));
            }

            // 9. Purge all known Spells from the spellbook
            var spellsToClear = player.Biota.GetKnownSpellsIds(player.BiotaDatabaseLock);
            foreach (var spellId in spellsToClear)
            {
                player.RemoveKnownSpell((uint)spellId);
            }

            // 10. Reset Enlightenment to 0
            player.Enlightenment = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Enlightenment, 0));

            // 11. Dequip and destroy all equipped objects first (before clearing inventory)
            var equippedGuids = new List<ObjectGuid>(player.EquippedObjects.Keys);
            foreach (var guid in equippedGuids)
            {
                player.TryDequipObjectWithNetworking(guid, out _, Player.DequipObjectAction.ConsumeItem);
            }

            // 12. Clear all remaining inventory items
            player.ClearInventory(false);
        }

        private static void ConfigureStatsAndSpells(Player player)
        {
            // 1. Set Base Attributes
            // Base Attributes: Strength 450, Endurance 450, Coordination 575, Quickness 450, Focus 450, Self 450
            var attributeTargets = new Dictionary<PropertyAttribute, uint>()
            {
                { PropertyAttribute.Strength, 450 },
                { PropertyAttribute.Endurance, 450 },
                { PropertyAttribute.Coordination, 575 },
                { PropertyAttribute.Quickness, 450 },
                { PropertyAttribute.Focus, 450 },
                { PropertyAttribute.Self, 450 },
            };

            foreach (var kvp in attributeTargets)
            {
                var attrType = kvp.Key;
                var targetValue = kvp.Value;
                if (!player.Attributes.TryGetValue(attrType, out var attr))
                    continue;

                // Set innate StartingValue to 100
                attr.StartingValue = 100;
                
                // Ranks = Target - 100
                uint ranks = targetValue > 100 ? targetValue - 100 : 0;
                player.SetAttributeRank(attr, ranks);
                
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, attr));
            }

            // 2. Set Secondary Vitals (Max)
            // Secondary Vitals: Max Health 700, Max Stamina 900, Max Mana 900
            var vitalTargets = new Dictionary<PropertyAttribute2nd, uint>()
            {
                { PropertyAttribute2nd.MaxHealth, 700 },
                { PropertyAttribute2nd.MaxStamina, 900 },
                { PropertyAttribute2nd.MaxMana, 900 },
            };

            foreach (var kvp in vitalTargets)
            {
                var vitalType = kvp.Key;
                var targetValue = kvp.Value;
                if (!player.Vitals.TryGetValue(vitalType, out var vital))
                    continue;

                // Clear CP ranks
                vital.Ranks = 0;
                vital.ExperienceSpent = 0;

                // Adjust starting value based on current base attribute formulas, etc.
                int baseFormula = (int)AttributeFormula.GetFormula(player, vitalType, true);
                int enlBonus = (int)vital.EnlBonus;
                int gearBonus = (int)vital.GearBonus;
                
                int startingValue = (int)targetValue - baseFormula - enlBonus - gearBonus;
                vital.StartingValue = (uint)Math.Max(1, startingValue);
                
                // Fully restore current vital value
                vital.Current = vital.MaxValue;
                
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, vital));
            }

            // 2.5 Set Level and Maximize All Skills
            player.Level = 1300;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Level, 1300));

            foreach (var skill in player.Skills.Values)
            {
                skill.AdvancementClass = SkillAdvancementClass.Specialized;
                skill.InitLevel = 10;

                var skillXPTable = Player.GetSkillXPTable(SkillAdvancementClass.Specialized);
                if (skillXPTable != null && skillXPTable.Count > 0)
                {
                    skill.Ranks = (ushort)(skillXPTable.Count - 1);
                    skill.ExperienceSpent = skillXPTable[skillXPTable.Count - 1];
                }

                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, skill));
            }

            // 3. Set Custom Augmentations (Luminance Augmentations)
            player.LuminanceAugmentCreatureCount = 5000;
            player.LuminanceAugmentItemCount = 3500;
            player.LuminanceAugmentLifeCount = 3500;
            player.LuminanceAugmentWarCount = 3500;
            player.LuminanceAugmentVoidCount = 3500;
            player.LuminanceAugmentSpellDurationCount = 3000;
            player.LuminanceAugmentSpecializeCount = 100;
            player.LuminanceAugmentSummonCount = 1100;
            player.LuminanceAugmentMeleeCount = 3500;
            player.LuminanceAugmentMissileCount = 3500;

            // Sync updated custom augs to the client
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugCreatureCount, player.LuminanceAugmentCreatureCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugItemCount, player.LuminanceAugmentItemCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugLifeCount, player.LuminanceAugmentLifeCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugWarCount, player.LuminanceAugmentWarCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugVoidCount, player.LuminanceAugmentVoidCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugDurationCount, player.LuminanceAugmentSpellDurationCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugSpecializeCount, player.LuminanceAugmentSpecializeCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugSummonCount, player.LuminanceAugmentSummonCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugMeleeCount, player.LuminanceAugmentMeleeCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugMissileCount, player.LuminanceAugmentMissileCount ?? 0));

            // 4. Set Retail Augmentations to max/acquired
            foreach (var kvp in AugmentationDevice.MaxAugs)
            {
                var type = kvp.Key;
                var maxVal = kvp.Value;
                var augProp = AugmentationDevice.AugProps[type];
                player.SetProperty(augProp, maxVal);
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, augProp, maxVal));
            }

            // 5. Learn All Spells Silently
            foreach (var spellID in Player.PlayerSpellTable)
            {
                if (player.AddKnownSpell(spellID))
                {
                    player.Session.Network.EnqueueSend(new GameEventMagicUpdateSpell(player.Session, (ushort)spellID));
                }
            }

            // 6. Enable Aetheria Slots
            player.UpdateProperty(player, PropertyInt.AetheriaBitfield, (int)AetheriaBitfield.All);

            // 7. Set Enlightenment to 325
            player.Enlightenment = 325;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Enlightenment, 325));
        }

        private static void ConfigureStatsAndSpellsT10(Player player)
        {
            // Same as T11 but with lower luminance augs and ENL 300
            // Reuse T11 to set all attributes, vitals, skills, retail augs, and spells
            ConfigureStatsAndSpells(player);

            // Override luminance augs for T10
            player.LuminanceAugmentCreatureCount = 4000;
            player.LuminanceAugmentItemCount = 2000;
            player.LuminanceAugmentLifeCount = 2000;
            player.LuminanceAugmentWarCount = 1750;
            player.LuminanceAugmentVoidCount = 1750;
            player.LuminanceAugmentSpellDurationCount = 1750;
            player.LuminanceAugmentSpecializeCount = 100;
            player.LuminanceAugmentSummonCount = 1100;
            player.LuminanceAugmentMeleeCount = 1750;
            player.LuminanceAugmentMissileCount = 1750;

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugCreatureCount, player.LuminanceAugmentCreatureCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugItemCount, player.LuminanceAugmentItemCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugLifeCount, player.LuminanceAugmentLifeCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugWarCount, player.LuminanceAugmentWarCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugVoidCount, player.LuminanceAugmentVoidCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugDurationCount, player.LuminanceAugmentSpellDurationCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugSpecializeCount, player.LuminanceAugmentSpecializeCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugSummonCount, player.LuminanceAugmentSummonCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugMeleeCount, player.LuminanceAugmentMeleeCount ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.LumAugMissileCount, player.LuminanceAugmentMissileCount ?? 0));

            // Override Enlightenment to 300
            player.Enlightenment = 300;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Enlightenment, 300));
        }

        private static void SpawnArmor(Player player)
        {
            var armorWcids = new List<uint>()
            {
                14594, // helmprismatic
                23777, // coatamulishadowbrilliant
                23785, // leggingsamulishadowbrilliant
                23793, // breastplateceldonshadowbrilliant
                23801, // girthceldonshadowbrilliant
                23809, // leggingsceldonshadowbrilliant
                23817, // sleevesceldonshadowbrilliant
                23825, // breastplatekoujiashadowbrilliant
                23833, // leggingskoujiashadowbrilliant
                23841, // sleeveskoujiashadowbrilliant
            };

            foreach (var wcid in armorWcids)
            {
                bool alreadyHas = player.GetAllPossessionsDeep().Any(i => i.WeenieClassId == wcid);
                if (alreadyHas) continue;
                var armorItem = WorldObjectFactory.CreateNewWorldObject(wcid);
                if (armorItem != null)
                    player.TryCreateInInventoryWithNetworking(armorItem);
            }
        }

        private static void SpawnOlthoiShadowArmor(Player player, string tier = "T11")
        {
            var armorNames = new Dictionary<uint, string>()
            {
                { 3110264, $"{tier} Helm (Test)" },
                { 3110266, $"{tier} Girth (Test)" },
                { 3110267, $"{tier} Tassets (Test)" },
                { 3110268, $"{tier} Greaves (Test)" },
                { 3110269, $"{tier} Pauldrons (Test)" },
                { 3110270, $"{tier} Sollerets (Test)" },
                { 3110271, $"{tier} Gloves (Test)" },
                { 3110272, $"{tier} Bracers (Test)" },
                { 3110308, $"{tier} Coat (No Cloak) (Test)" }
            };

            foreach (var kvp in armorNames)
            {
                if (HasItemNamed(player, kvp.Value)) continue;
                var item = WorldObjectFactory.CreateNewWorldObject(kvp.Key);
                if (item != null)
                {
                    item.Name = kvp.Value;
                    item.SetProperty(PropertyString.Name, kvp.Value);
                    item.SetProperty(PropertyInt.MaterialType, 0);
                    AddItemToInventory(player, item);
                }
            }
        }

        private static void SpawnCustomUndergarmentsAndCloak(Player player, string tier = "T11")
        {
            var shirtName = $"{tier} Shirt";
            if (!HasItemNamed(player, shirtName))
            {
            var shirt = WorldObjectFactory.CreateNewWorldObject(28607);
            if (shirt != null)
            {
                shirt.Name = shirtName;
                shirt.SetProperty(PropertyString.Name, shirtName);
                shirt.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                shirt.SetProperty(PropertyInt.Value, 11519);
                shirt.SetProperty(PropertyInt.Mass, 75);
                shirt.SetProperty(PropertyInt.EncumbranceVal, 75);
                shirt.SetProperty(PropertyInt.WieldRequirements, (int)WieldRequirement.RawSkill);
                shirt.SetProperty(PropertyInt.WieldSkillType, (int)Skill.MeleeDefense);
                shirt.SetProperty(PropertyInt.WieldDifficulty, 725);
                shirt.SetProperty(PropertyInt.ItemWorkmanship, 7);
                shirt.SetProperty(PropertyInt.ItemSpellcraft, 750);
                shirt.SetProperty(PropertyInt.ItemCurMana, 3240);
                shirt.SetProperty(PropertyInt.ItemMaxMana, 3500);
                shirt.SetProperty(PropertyInt.ItemDifficulty, 750);
                shirt.SetProperty(PropertyInt.ItemMaxLevel, 20);
                shirt.SetProperty(PropertyInt.GearDamage, 13);
                shirt.SetProperty(PropertyInt.GearDamageResist, 4);
                shirt.SetProperty(PropertyInt.GearCritDamage, 13);
                shirt.SetProperty(PropertyInt.GearCritDamageResist, 4);
                shirt.SetProperty(PropertyInt.GearNetherResist, 9);
                shirt.SetProperty(PropertyInt.GearMaxHealth, 175);
                
                // Spells
                shirt.Biota.ClearSpells(shirt.BiotaDatabaseLock);
                var shirtSpells = new List<uint>() { 2161, 3694, 4466, 4664, 4667, 5238 };
                foreach (var spellId in shirtSpells)
                    shirt.Biota.GetOrAddKnownSpell((int)spellId, shirt.BiotaDatabaseLock, out _);
                shirt.ChangesDetected = true;
                shirt.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, shirt);
            }
            }
 
            var pantsName = $"{tier} Pants";
            if (!HasItemNamed(player, pantsName))
            {
            var pants = WorldObjectFactory.CreateNewWorldObject(2599);
            if (pants != null)
            {
                pants.Name = pantsName;
                pants.SetProperty(PropertyString.Name, pantsName);
                pants.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                pants.SetProperty(PropertyInt.Value, 13948);
                pants.SetProperty(PropertyInt.Mass, 90);
                pants.SetProperty(PropertyInt.EncumbranceVal, 135);
                pants.SetProperty(PropertyInt.WieldRequirements, (int)WieldRequirement.RawSkill);
                pants.SetProperty(PropertyInt.WieldSkillType, (int)Skill.MeleeDefense);
                pants.SetProperty(PropertyInt.WieldDifficulty, 725);
                pants.SetProperty(PropertyInt.ItemWorkmanship, 8);
                pants.SetProperty(PropertyInt.ItemSpellcraft, 750);
                pants.SetProperty(PropertyInt.ItemCurMana, 3240);
                pants.SetProperty(PropertyInt.ItemMaxMana, 3500);
                pants.SetProperty(PropertyInt.ItemDifficulty, 750);
                pants.SetProperty(PropertyInt.ItemMaxLevel, 20);
                pants.SetProperty(PropertyInt.GearDamage, 14);
                pants.SetProperty(PropertyInt.GearDamageResist, 4);
                pants.SetProperty(PropertyInt.GearCritDamage, 13);
                pants.SetProperty(PropertyInt.GearCritDamageResist, 4);
                pants.SetProperty(PropertyInt.GearNetherResist, 8);
                pants.SetProperty(PropertyInt.GearMaxHealth, 175);
                
                // Spells
                pants.Biota.ClearSpells(pants.BiotaDatabaseLock);
                var pantsSpells = new List<uint>() { 2157, 3730, 4470, 4667, 4695, 5253 };
                foreach (var spellId in pantsSpells)
                    pants.Biota.GetOrAddKnownSpell((int)spellId, pants.BiotaDatabaseLock, out _);
                pants.ChangesDetected = true;
                pants.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, pants);
            }
            }
 
            var cloakName = $"{tier} Cloak";
            if (!HasItemNamed(player, cloakName))
            {
            var cloak = WorldObjectFactory.CreateNewWorldObject(227190032);
            if (cloak != null)
            {
                cloak.Name = cloakName;
                cloak.SetProperty(PropertyString.Name, cloakName);
                cloak.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                cloak.SetProperty(PropertyInt.Value, 2500);
                cloak.SetProperty(PropertyInt.Mass, 0);
                cloak.SetProperty(PropertyInt.EncumbranceVal, 75);
                cloak.SetProperty(PropertyInt.ItemWorkmanship, 10);
                cloak.SetProperty(PropertyInt.ItemSpellcraft, 2000);
                cloak.SetProperty(PropertyInt.ItemCurMana, 4791);
                cloak.SetProperty(PropertyInt.ItemMaxMana, 5000);
                cloak.SetProperty(PropertyInt.EquipmentSetId, 71);
                cloak.SetProperty(PropertyInt.ItemMaxLevel, 5);
                cloak.SetProperty(PropertyInt.ItemXpStyle, (int)ItemXpStyle.ScalesWithLevel);
                cloak.SetProperty(PropertyInt.GearDamageResist, 5);
                cloak.SetProperty(PropertyInt.GearCritDamageResist, 3);
                cloak.SetProperty(PropertyInt.GearNetherResist, 5);
                cloak.SetProperty(PropertyInt.GearMaxHealth, 10);
                
                // Spells
                cloak.Biota.ClearSpells(cloak.BiotaDatabaseLock);
                cloak.Biota.GetOrAddKnownSpell(5450, cloak.BiotaDatabaseLock, out _); // Towering Defense
                cloak.ChangesDetected = true;
                cloak.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, cloak);
            }
            }
        }
 
        private static void SpawnCustomJewelry(Player player, string tier = "T11")
        {
            var leftBraceletName = $"{tier} Bracelet 1";
            if (!HasItemNamed(player, leftBraceletName))
            {
            var leftBracelet = WorldObjectFactory.CreateNewWorldObject(21392);
            if (leftBracelet != null)
            {
                leftBracelet.Name = leftBraceletName;
                leftBracelet.SetProperty(PropertyString.Name, leftBraceletName);
                leftBracelet.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                leftBracelet.SetProperty(PropertyInt.Value, 150);
                leftBracelet.SetProperty(PropertyInt.Mass, 60);
                leftBracelet.SetProperty(PropertyInt.EncumbranceVal, 150);
                leftBracelet.SetProperty(PropertyInt.WieldRequirements, (int)WieldRequirement.RawSkill);
                leftBracelet.SetProperty(PropertyInt.WieldSkillType, (int)Skill.MeleeDefense);
                leftBracelet.SetProperty(PropertyInt.WieldDifficulty, 725);
                leftBracelet.SetProperty(PropertyInt.ItemWorkmanship, 6);
                leftBracelet.SetProperty(PropertyInt.ItemSpellcraft, 3870);
                leftBracelet.SetProperty(PropertyInt.ItemCurMana, 1196);
                leftBracelet.SetProperty(PropertyInt.ItemMaxMana, 1618);
                leftBracelet.SetProperty(PropertyInt.ItemDifficulty, 1698);
                leftBracelet.SetProperty(PropertyInt.GemCount, 2);
                leftBracelet.SetProperty(PropertyInt.GemType, 49);
                leftBracelet.SetProperty(PropertyInt.GearMaxHealth, 100);
                leftBracelet.ValidLocations = EquipMask.WristWearLeft;
 
                // Spells
                leftBracelet.Biota.ClearSpells(leftBracelet.BiotaDatabaseLock);
                var spells = new List<uint>() { 4291, 4470, 4693, 4712 };
                foreach (var spellId in spells)
                    leftBracelet.Biota.GetOrAddKnownSpell((int)spellId, leftBracelet.BiotaDatabaseLock, out _);
                leftBracelet.ChangesDetected = true;
                leftBracelet.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, leftBracelet);
            }
            }
 
            var rightBraceletName = $"{tier} Bracelet 2";
            if (!HasItemNamed(player, rightBraceletName))
            {
            var rightBracelet = WorldObjectFactory.CreateNewWorldObject(21392);
            if (rightBracelet != null)
            {
                rightBracelet.Name = rightBraceletName;
                rightBracelet.SetProperty(PropertyString.Name, rightBraceletName);
                rightBracelet.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                rightBracelet.SetProperty(PropertyInt.Value, 150);
                rightBracelet.SetProperty(PropertyInt.Mass, 60);
                rightBracelet.SetProperty(PropertyInt.EncumbranceVal, 150);
                rightBracelet.SetProperty(PropertyInt.WieldRequirements, (int)WieldRequirement.RawSkill);
                rightBracelet.SetProperty(PropertyInt.WieldSkillType, (int)Skill.MeleeDefense);
                rightBracelet.SetProperty(PropertyInt.WieldDifficulty, 725);
                rightBracelet.SetProperty(PropertyInt.ItemWorkmanship, 6);
                rightBracelet.SetProperty(PropertyInt.ItemSpellcraft, 3825);
                rightBracelet.SetProperty(PropertyInt.ItemCurMana, 1392);
                rightBracelet.SetProperty(PropertyInt.ItemMaxMana, 1743);
                rightBracelet.SetProperty(PropertyInt.ItemDifficulty, 1608);
                rightBracelet.SetProperty(PropertyInt.GemCount, 4);
                rightBracelet.SetProperty(PropertyInt.GemType, 33);
                rightBracelet.SetProperty(PropertyInt.GearMaxHealth, 100);
                rightBracelet.ValidLocations = EquipMask.WristWearRight;
 
                // Spells
                rightBracelet.Biota.ClearSpells(rightBracelet.BiotaDatabaseLock);
                var spells = new List<uint>() { 2059, 4693, 6067 };
                foreach (var spellId in spells)
                    rightBracelet.Biota.GetOrAddKnownSpell((int)spellId, rightBracelet.BiotaDatabaseLock, out _);
                rightBracelet.ChangesDetected = true;
                rightBracelet.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, rightBracelet);
            }
            }
 
            var leftRingName = $"{tier} Ring 1";
            if (!HasItemNamed(player, leftRingName))
            {
            var leftRing = WorldObjectFactory.CreateNewWorldObject(21394);
            if (leftRing != null)
            {
                leftRing.Name = leftRingName;
                leftRing.SetProperty(PropertyString.Name, leftRingName);
                leftRing.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                leftRing.SetProperty(PropertyInt.Value, 30);
                leftRing.SetProperty(PropertyInt.Mass, 20);
                leftRing.SetProperty(PropertyInt.EncumbranceVal, 30);
                leftRing.SetProperty(PropertyInt.WieldRequirements, (int)WieldRequirement.RawSkill);
                leftRing.SetProperty(PropertyInt.WieldSkillType, (int)Skill.MeleeDefense);
                leftRing.SetProperty(PropertyInt.WieldDifficulty, 725);
                leftRing.SetProperty(PropertyInt.ItemWorkmanship, 7);
                leftRing.SetProperty(PropertyInt.ItemSpellcraft, 4315);
                leftRing.SetProperty(PropertyInt.ItemCurMana, 1166);
                leftRing.SetProperty(PropertyInt.ItemMaxMana, 1517);
                leftRing.SetProperty(PropertyInt.ItemDifficulty, 1876);
                leftRing.SetProperty(PropertyInt.GemCount, 3);
                leftRing.SetProperty(PropertyInt.GemType, 49);
                leftRing.SetProperty(PropertyInt.GearMaxHealth, 100);
                leftRing.ValidLocations = EquipMask.FingerWearLeft;
 
                // Spells
                leftRing.Biota.ClearSpells(leftRing.BiotaDatabaseLock);
                var spells = new List<uint>() { 2197, 2251, 4686, 4708 };
                foreach (var spellId in spells)
                    leftRing.Biota.GetOrAddKnownSpell((int)spellId, leftRing.BiotaDatabaseLock, out _);
                leftRing.ChangesDetected = true;
                leftRing.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, leftRing);
            }
            }
 
            var rightRingName = $"{tier} Ring 2";
            if (!HasItemNamed(player, rightRingName))
            {
            var rightRing = WorldObjectFactory.CreateNewWorldObject(21394);
            if (rightRing != null)
            {
                rightRing.Name = rightRingName;
                rightRing.SetProperty(PropertyString.Name, rightRingName);
                rightRing.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                rightRing.SetProperty(PropertyInt.Value, 30);
                rightRing.SetProperty(PropertyInt.Mass, 20);
                rightRing.SetProperty(PropertyInt.EncumbranceVal, 30);
                rightRing.SetProperty(PropertyInt.WieldRequirements, (int)WieldRequirement.RawSkill);
                rightRing.SetProperty(PropertyInt.WieldSkillType, (int)Skill.MeleeDefense);
                rightRing.SetProperty(PropertyInt.WieldDifficulty, 725);
                rightRing.SetProperty(PropertyInt.ItemWorkmanship, 7);
                rightRing.SetProperty(PropertyInt.ItemSpellcraft, 4273);
                rightRing.SetProperty(PropertyInt.ItemCurMana, 1399);
                rightRing.SetProperty(PropertyInt.ItemMaxMana, 1751);
                rightRing.SetProperty(PropertyInt.ItemDifficulty, 1828);
                rightRing.SetProperty(PropertyInt.GemCount, 3);
                rightRing.SetProperty(PropertyInt.GemType, 39);
                rightRing.SetProperty(PropertyInt.GearMaxHealth, 100);
                rightRing.ValidLocations = EquipMask.FingerWearRight;
 
                // Spells
                rightRing.Biota.ClearSpells(rightRing.BiotaDatabaseLock);
                var spells = new List<uint>() { 279, 2153, 4701, 6041 };
                foreach (var spellId in spells)
                    rightRing.Biota.GetOrAddKnownSpell((int)spellId, rightRing.BiotaDatabaseLock, out _);
                rightRing.ChangesDetected = true;
                rightRing.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, rightRing);
            }
            }
 
            var necklaceName = $"{tier} Necklace";
            if (!HasItemNamed(player, necklaceName))
            {
            var necklace = WorldObjectFactory.CreateNewWorldObject(27445);
            if (necklace != null)
            {
                necklace.Name = necklaceName;
                necklace.SetProperty(PropertyString.Name, necklaceName);
                necklace.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                necklace.SetProperty(PropertyInt.Value, 90);
                necklace.SetProperty(PropertyInt.Mass, 60);
                necklace.SetProperty(PropertyInt.EncumbranceVal, 90);
                necklace.SetProperty(PropertyInt.WieldRequirements, (int)WieldRequirement.RawSkill);
                necklace.SetProperty(PropertyInt.WieldSkillType, (int)Skill.MeleeDefense);
                necklace.SetProperty(PropertyInt.WieldDifficulty, 725);
                necklace.SetProperty(PropertyInt.ItemWorkmanship, 8);
                necklace.SetProperty(PropertyInt.ItemSpellcraft, 4370);
                necklace.SetProperty(PropertyInt.ItemCurMana, 2138);
                necklace.SetProperty(PropertyInt.ItemMaxMana, 2560);
                necklace.SetProperty(PropertyInt.ItemDifficulty, 1935);
                necklace.SetProperty(PropertyInt.GemCount, 3);
                necklace.SetProperty(PropertyInt.GemType, 49);
                necklace.SetProperty(PropertyInt.GearMaxHealth, 100);
                necklace.ValidLocations = EquipMask.NeckWear;
 
                // Spells
                necklace.Biota.ClearSpells(necklace.BiotaDatabaseLock);
                var spells = new List<uint>() { 4462, 4466, 4703, 6079, 6085 };
                foreach (var spellId in spells)
                    necklace.Biota.GetOrAddKnownSpell((int)spellId, necklace.BiotaDatabaseLock, out _);
                necklace.ChangesDetected = true;
                necklace.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, necklace);
            }
            }
 
            var trinketName = $"{tier} Trinket";
            if (!HasItemNamed(player, trinketName))
            {
            var trinket = WorldObjectFactory.CreateNewWorldObject(41483);
            if (trinket != null)
            {
                trinket.Name = trinketName;
                trinket.SetProperty(PropertyString.Name, trinketName);
                trinket.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                trinket.SetProperty(PropertyInt.Value, 100);
                trinket.SetProperty(PropertyInt.Mass, 60);
                trinket.SetProperty(PropertyInt.EncumbranceVal, 100);
                trinket.SetProperty(PropertyInt.WieldRequirements, (int)WieldRequirement.RawSkill);
                trinket.SetProperty(PropertyInt.WieldSkillType, (int)Skill.MeleeDefense);
                trinket.SetProperty(PropertyInt.WieldDifficulty, 725);
                trinket.SetProperty(PropertyInt.ItemWorkmanship, 8);
                trinket.SetProperty(PropertyInt.ItemSpellcraft, 339);
                trinket.SetProperty(PropertyInt.ItemCurMana, 1266);
                trinket.SetProperty(PropertyInt.ItemMaxMana, 1618);
                trinket.SetProperty(PropertyInt.ItemDifficulty, 397);
                trinket.SetProperty(PropertyInt.GemCount, 4);
                trinket.SetProperty(PropertyInt.GemType, 38);
                trinket.SetProperty(PropertyInt.GearMaxHealth, 100);
                trinket.ValidLocations = EquipMask.TrinketOne;
 
                // Spells
                trinket.Biota.ClearSpells(trinket.BiotaDatabaseLock);
                var spells = new List<uint>() { 1450, 2281, 4698, 5137, 5139, 5141, 5449, 6081 };
                foreach (var spellId in spells)
                    trinket.Biota.GetOrAddKnownSpell((int)spellId, trinket.BiotaDatabaseLock, out _);
                trinket.ChangesDetected = true;
                trinket.UiEffects = UiEffects.Magical;
 
                AddItemToInventory(player, trinket);
            }
            }
        }

        private static void SpawnCharms(Player player)
        {
            var charmWcids = new List<uint>()
            {
                777700001,  // Mana Barrier Charm (T1)
                777700020,  // Asheron's Favor Charm (T1)
                777700021,  // Artisan's Charm (T1)
                777700022,  // Shrapnel Charm
                777700023,  // Agony Charm
                777700024,  // Split Cast Charm
                777700025,  // Explosive Arrow Charm (T1)
                777700026,  // Omni Strike Charm
                777700027,  // Fork Charm (T1)
                777700300,  // Charm of Auto Rebuffing
                777700019,  // Infinite Casting Stone
            };

            var playerPossessions = player.GetAllPossessionsDeep().ToList();
            var playerWcids = new HashSet<uint>(playerPossessions.Select(i => i.WeenieClassId));

            foreach (var wcid in charmWcids)
            {
                if (playerWcids.Contains(wcid)) continue;

                var charm = WorldObjectFactory.CreateNewWorldObject(wcid);
                if (charm != null)
                    player.TryCreateInInventoryWithNetworking(charm);
            }

            // 20x Charm Catalyst — skip if player already has any
            bool hasCatalyst = playerWcids.Contains(777700010);
            if (!hasCatalyst)
            {
                var catalyst = WorldObjectFactory.CreateNewWorldObject(777700010);
                if (catalyst != null)
                {
                    catalyst.SetProperty(PropertyInt.StackSize, 20);
                    catalyst.SetProperty(PropertyInt.EncumbranceVal, (catalyst.StackUnitEncumbrance ?? 5) * 20);
                    player.TryCreateInInventoryWithNetworking(catalyst);
                }
            }

            SpawnSpellcastingConsumables(player);
        }

        private static void SpawnSpellcastingConsumables(Player player)
        {
            var possessions = player.GetAllPossessionsDeep().ToList();

            // 1. Tapers (1000 Prismatic Tapers)
            var targetTapers = 1000;
            var currentTapers = possessions.Where(i => i.WeenieClassId == 20631).Sum(i => i.StackSize ?? 1);
            if (currentTapers < targetTapers)
            {
                var needed = targetTapers - currentTapers;
                while (needed > 0)
                {
                    var taper = WorldObjectFactory.CreateNewWorldObject(20631);
                    if (taper == null) break;
                    var maxStack = taper.MaxStackSize ?? 1000;
                    var toSpawn = Math.Min(needed, maxStack);
                    taper.SetStackSize(toSpawn);
                    player.TryCreateInInventoryWithNetworking(taper);
                    needed -= toSpawn;
                }
            }

            // 2. Scarabs (100 of each standard spellcasting scarab)
            var scarabWcids = new List<uint> { 691, 689, 686, 688, 687, 690, 8897, 37155 };
            var targetScarabs = 100;

            foreach (var wcid in scarabWcids)
            {
                var currentScarabs = possessions.Where(i => i.WeenieClassId == wcid).Sum(i => i.StackSize ?? 1);
                if (currentScarabs < targetScarabs)
                {
                    var needed = targetScarabs - currentScarabs;
                    while (needed > 0)
                    {
                        var scarab = WorldObjectFactory.CreateNewWorldObject(wcid);
                        if (scarab == null) break;
                        var maxStack = scarab.MaxStackSize ?? 100;
                        var toSpawn = Math.Min(needed, maxStack);
                        scarab.SetStackSize(toSpawn);
                        player.TryCreateInInventoryWithNetworking(scarab);
                        needed -= toSpawn;
                    }
                }
            }
        }

        private static void AddItemToInventory(Player player, WorldObject item)
        {
            player.TryCreateInInventoryWithNetworking(item);
        }

        private static bool HasItemNamed(Player player, string name)
        {
            return player.GetAllPossessionsDeep().Any(i => i.Name == name);
        }

        private static void SpawnAetherias(Player player)
        {
            // Enable Aetheria slots
            player.UpdateProperty(player, PropertyInt.AetheriaBitfield, (int)AetheriaBitfield.All);

            // 1. Blue Aetheria
            if (!HasItemNamed(player, "Blue Aetheria"))
            {
            var blueAetheria = WorldObjectFactory.CreateNewWorldObject(42635);
            if (blueAetheria != null)
            {
                blueAetheria.Name = "Blue Aetheria";
                blueAetheria.SetProperty(PropertyString.Name, "Blue Aetheria");
                blueAetheria.SetProperty(PropertyString.LongDesc, "This aetheria's sigil now shows on the surface.");
                blueAetheria.SetProperty(PropertyInt.EquipmentSetId, (int)EquipmentSet.AetheriaGrowth);
                blueAetheria.SetProperty(PropertyDataId.Icon, 100690944); // 0x06006C00 Blue Growth icon
                blueAetheria.SetProperty(PropertyDataId.ProcSpell, 5206); // Surge of Protection
                blueAetheria.SetProperty(PropertyBool.ProcSpellSelfTargeted, true);
                blueAetheria.SetProperty(PropertyInt.ValidLocations, (int)EquipMask.SigilOne);
                blueAetheria.SetProperty(PropertyInt.ItemMaxLevel, 10);
                blueAetheria.SetProperty(PropertyInt.ItemXpStyle, (int)ItemXpStyle.ScalesWithLevel);
                blueAetheria.SetProperty(PropertyInt64.ItemBaseXp, 1000000000L);
                blueAetheria.SetProperty(PropertyInt64.ItemTotalXp, 1023000000000L);
                blueAetheria.SetProperty(PropertyInt.GearCrit, 4);

                // Set Level 10 overlay & wield requirements matching Grumpy Old Man
                blueAetheria.IconOverlayId = LootGenerationFactory.IconOverlay_ItemMaxLevel[9];
                blueAetheria.WieldRequirements = WieldRequirement.RawSkill;
                blueAetheria.WieldSkillType = (int)Skill.MeleeDefense;
                blueAetheria.WieldDifficulty = 725;
                blueAetheria.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix

                AddItemToInventory(player, blueAetheria);
            }
            }

            // 2. Yellow Aetheria
            if (!HasItemNamed(player, "Yellow Aetheria"))
            {
            var yellowAetheria = WorldObjectFactory.CreateNewWorldObject(42637);
            if (yellowAetheria != null)
            {
                yellowAetheria.Name = "Yellow Aetheria";
                yellowAetheria.SetProperty(PropertyString.Name, "Yellow Aetheria");
                yellowAetheria.SetProperty(PropertyString.LongDesc, "This aetheria's sigil now shows on the surface.");
                yellowAetheria.SetProperty(PropertyInt.EquipmentSetId, (int)EquipmentSet.AetheriaFury);
                yellowAetheria.SetProperty(PropertyDataId.Icon, 100690931); // 0x06006BF3 Yellow Fury icon
                yellowAetheria.SetProperty(PropertyDataId.ProcSpell, 5208); // Surge of Regeneration
                yellowAetheria.SetProperty(PropertyBool.ProcSpellSelfTargeted, true);
                yellowAetheria.SetProperty(PropertyInt.ValidLocations, (int)EquipMask.SigilTwo);
                yellowAetheria.SetProperty(PropertyInt.ItemMaxLevel, 8);
                yellowAetheria.SetProperty(PropertyInt.ItemXpStyle, (int)ItemXpStyle.ScalesWithLevel);
                yellowAetheria.SetProperty(PropertyInt64.ItemBaseXp, 1000000000L);
                yellowAetheria.SetProperty(PropertyInt64.ItemTotalXp, 255000000000L);
                yellowAetheria.SetProperty(PropertyInt.GearCrit, 4);

                // Set Level 8 overlay & wield requirements matching Grumpy Old Man
                yellowAetheria.IconOverlayId = LootGenerationFactory.IconOverlay_ItemMaxLevel[7];
                yellowAetheria.WieldRequirements = WieldRequirement.RawSkill;
                yellowAetheria.WieldSkillType = (int)Skill.MeleeDefense;
                yellowAetheria.WieldDifficulty = 725;
                yellowAetheria.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix

                AddItemToInventory(player, yellowAetheria);
            }
            }

            // 3. Red Aetheria
            if (!HasItemNamed(player, "Red Aetheria"))
            {
            var redAetheria = WorldObjectFactory.CreateNewWorldObject(42636);
            if (redAetheria != null)
            {
                redAetheria.Name = "Red Aetheria";
                redAetheria.SetProperty(PropertyString.Name, "Red Aetheria");
                redAetheria.SetProperty(PropertyString.LongDesc, "This aetheria's sigil now shows on the surface.");
                redAetheria.SetProperty(PropertyInt.EquipmentSetId, (int)EquipmentSet.AetheriaFury);
                redAetheria.SetProperty(PropertyDataId.Icon, 100690948); // 0x06006C04 Red Fury icon
                redAetheria.SetProperty(PropertyDataId.ProcSpell, 5204); // Surge of Destruction
                redAetheria.SetProperty(PropertyBool.ProcSpellSelfTargeted, true);
                redAetheria.SetProperty(PropertyInt.ValidLocations, (int)EquipMask.SigilThree);
                redAetheria.SetProperty(PropertyInt.ItemMaxLevel, 8);
                redAetheria.SetProperty(PropertyInt.ItemXpStyle, (int)ItemXpStyle.ScalesWithLevel);
                redAetheria.SetProperty(PropertyInt64.ItemBaseXp, 1000000000L);
                redAetheria.SetProperty(PropertyInt64.ItemTotalXp, 255000000000L);
                redAetheria.SetProperty(PropertyInt.GearCrit, 4);

                // Set Level 8 overlay & wield requirements matching Grumpy Old Man
                redAetheria.IconOverlayId = LootGenerationFactory.IconOverlay_ItemMaxLevel[7];
                redAetheria.WieldRequirements = WieldRequirement.RawSkill;
                redAetheria.WieldSkillType = (int)Skill.MeleeDefense;
                redAetheria.WieldDifficulty = 725;
                redAetheria.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix

                AddItemToInventory(player, redAetheria);
            }
            }
        }

        private static void SpawnWeapons(Player player, string targetStyle)
        {
            var rucksackName = "Weapons Pack";
            if (HasItemNamed(player, rucksackName)) return;
            var rucksack = WorldObjectFactory.CreateNewWorldObject(310025) as Container;
            if (rucksack != null)
            {
                rucksack.Name = rucksackName;
                rucksack.SetProperty(PropertyString.Name, rucksackName);
                rucksack.SetProperty(PropertyInt.MaterialType, 0);
            }

            var weaponBases = new Dictionary<string, (uint wcid, string baseName)>()
            {
                { "UA", (29651, "Spiked Knuckles") },
                { "2H", (46105, "Atlan Two Handed Sword") },
                { "Bow", (29639, "Bow") },
                { "Wand", (29661, "Wand") }
            };

            var elements = new List<DamageType>()
            {
                DamageType.Slash,
                DamageType.Pierce,
                DamageType.Bludgeon,
                DamageType.Cold,
                DamageType.Fire,
                DamageType.Acid,
                DamageType.Electric,
                DamageType.Nether
            };

            foreach (var baseKvp in weaponBases)
            {
                var weaponType = baseKvp.Key;
                var baseWcid = baseKvp.Value.wcid;
                var baseName = baseKvp.Value.baseName;

                // If style filter is provided, skip non-matching styles
                if (targetStyle != null && !weaponType.Equals(targetStyle, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var element in elements)
                {
                    if (weaponType.Equals("Bow", StringComparison.OrdinalIgnoreCase))
                    {
                        CreateCustomBow(player, element, true, rucksack);
                    }
                    else if (weaponType.Equals("UA", StringComparison.OrdinalIgnoreCase))
                    {
                        CreateCustomUA(player, element, rucksack);
                    }
                    else if (weaponType.Equals("Wand", StringComparison.OrdinalIgnoreCase))
                    {
                        CreateCustomWand(player, element, rucksack);
                    }
                    else
                    {
                        var weapon = WorldObjectFactory.CreateNewWorldObject(baseWcid);
                        if (weapon != null)
                        {
                            weapon.SetProperty(PropertyInt.DamageType, (int)element);
                            var elementLabel = GetElementLabel(element);
                            string label = weaponType.Equals("2H", StringComparison.OrdinalIgnoreCase) ? "2H" : baseName;
                            weapon.Name = $"{elementLabel} {label} (Test)";
                            weapon.SetProperty(PropertyString.Name, $"{elementLabel} {label} (Test)");
                            weapon.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
                            if (rucksack != null)
                                rucksack.TryAddToInventory(weapon);
                            else
                                player.TryCreateInInventoryWithNetworking(weapon);
                        }
                    }
                }
            }

            if (targetStyle == null || targetStyle.Equals("UA", StringComparison.OrdinalIgnoreCase))
            {
                SpawnHilts(player, rucksack);
            }

            if (rucksack != null)
            {
                player.TryCreateInInventoryWithNetworking(rucksack);
            }
        }

        private static void CreateCustomBow(Player player, DamageType element, bool includeRend = true, Container destination = null)
        {
            uint baseWcid = 46139;
            string elementLabel = GetElementLabel(element);
            var expectedName = $"{elementLabel} Bow (Test)";
            if (HasItemNamed(player, expectedName)) return;

            var bow = WorldObjectFactory.CreateNewWorldObject(baseWcid);
            if (bow == null) return;
            bow.Name = $"{elementLabel} Bow (Test)";
            bow.SetProperty(PropertyString.Name, $"{elementLabel} Bow (Test)");
            bow.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix

            // Get matching rend background underlay
            ImbuedEffectType rendType = ImbuedEffectType.Undef;

            if (includeRend)
            {
                switch (element)
                {
                    case DamageType.Slash:
                        rendType = ImbuedEffectType.SlashRending;
                        break;
                    case DamageType.Pierce:
                        rendType = ImbuedEffectType.PierceRending;
                        break;
                    case DamageType.Bludgeon:
                        rendType = ImbuedEffectType.BludgeonRending;
                        break;
                    case DamageType.Cold:
                        rendType = ImbuedEffectType.ColdRending;
                        break;
                    case DamageType.Fire:
                        rendType = ImbuedEffectType.FireRending;
                        break;
                    case DamageType.Acid:
                        rendType = ImbuedEffectType.AcidRending;
                        break;
                    case DamageType.Electric:
                        rendType = ImbuedEffectType.ElectricRending;
                        break;
                    case DamageType.Nether:
                        rendType = ImbuedEffectType.NetherRending;
                        break;
                }
            }

            if (includeRend && rendType != ImbuedEffectType.Undef)
            {
                if (ACE.Server.Managers.RecipeManager.IconUnderlay.TryGetValue(rendType, out var underlayId))
                {
                    bow.IconUnderlayId = underlayId;
                }
                else if (rendType == ImbuedEffectType.NetherRending)
                {
                    bow.IconUnderlayId = 0x060067A1; // Nether Rending underlay icon
                }
            }

            if (includeRend)
            {
                bow.SetProperty(PropertyString.LongDesc, $"{elementLabel} Bow (Test) of Swift Killer, set with 1 Emerald");
            }
            else
            {
                bow.SetProperty(PropertyString.LongDesc, $"{elementLabel} Bow (Test) of Swift Killer");
            }

            // Int Properties
            bow.SetProperty(PropertyInt.DamageType, (int)element);
            bow.SetProperty(PropertyInt.WieldDifficulty, 725);
            bow.SetProperty(PropertyInt.GearCritDamage, 3);
            bow.SetProperty(PropertyInt.ElementalDamageBonus, 31);
            bow.SetProperty(PropertyInt.Cleaving, 3);
            if (includeRend && rendType != ImbuedEffectType.Undef)
            {
                bow.SetProperty(PropertyInt.ImbuedEffect, (int)rendType);
            }
            bow.SetProperty(PropertyInt.ImbuedEffect2, (int)ImbuedEffectType.MagicDefense); // 4096 is MagicDefense
            bow.SetProperty(PropertyInt.NumTimesTinkered, 10);

            // Float Properties
            bow.SetProperty(PropertyFloat.DamageMod, 3.39f);
            bow.SetProperty(PropertyFloat.WeaponDefense, 1.29f);
            bow.SetProperty(PropertyFloat.CriticalFrequency, 0.33f);
            bow.SetProperty(PropertyFloat.CriticalMultiplier, 2.25f);

            // Split Arrow custom properties
            bow.SetProperty(PropertyBool.SplitArrows, true);
            bow.SetProperty(PropertyInt.SplitArrowCount, 3);
            bow.SetProperty(PropertyFloat.SplitArrowRange, 8.0f);
            bow.SetProperty(PropertyFloat.SplitArrowDamageMultiplier, 0.95f);

            // Bow Spells
            bow.Biota.ClearSpells(bow.BiotaDatabaseLock);
            var bowSpells = new List<uint>()
            {
                6089, // Legendary Blood Thirst (CantripBloodThirst4)
            };

            foreach (var spellId in bowSpells)
            {
                bow.Biota.GetOrAddKnownSpell((int)spellId, bow.BiotaDatabaseLock, out _);
            }
            bow.ChangesDetected = true;
            bow.UiEffects = UiEffects.Magical;

            if (destination != null)
                destination.TryAddToInventory(bow);
            else
                player.TryCreateInInventoryWithNetworking(bow);
        }

        private static void CreateCustomUA(Player player, DamageType element, Container destination = null)
        {
            uint baseWcid = 6171;
            string elementLabel = GetElementLabel(element);
            string weaponName = $"{elementLabel} UA (Test)";
            if (HasItemNamed(player, weaponName)) return;

            var claw = WorldObjectFactory.CreateNewWorldObject(baseWcid);
            if (claw == null) return;

            claw.Name = weaponName;
            claw.SetProperty(PropertyString.Name, weaponName);
            claw.SetProperty(PropertyString.LongDesc, $"{weaponName} of Defender, set with 4 Rubies");

            // Get matching rend background underlay
            ImbuedEffectType rendType = ImbuedEffectType.Undef;
            switch (element)
            {
                case DamageType.Slash:
                    rendType = ImbuedEffectType.SlashRending;
                    break;
                case DamageType.Pierce:
                    rendType = ImbuedEffectType.PierceRending;
                    break;
                case DamageType.Bludgeon:
                    rendType = ImbuedEffectType.BludgeonRending;
                    break;
                case DamageType.Cold:
                    rendType = ImbuedEffectType.ColdRending;
                    break;
                case DamageType.Fire:
                    rendType = ImbuedEffectType.FireRending;
                    break;
                case DamageType.Acid:
                    rendType = ImbuedEffectType.AcidRending;
                    break;
                case DamageType.Electric:
                    rendType = ImbuedEffectType.ElectricRending;
                    break;
                case DamageType.Nether:
                    rendType = ImbuedEffectType.NetherRending;
                    break;
            }

            if (rendType != ImbuedEffectType.Undef)
            {
                if (ACE.Server.Managers.RecipeManager.IconUnderlay.TryGetValue(rendType, out var underlayId))
                {
                    claw.IconUnderlayId = underlayId;
                }
                else if (rendType == ImbuedEffectType.NetherRending)
                {
                    claw.IconUnderlayId = 0x060067A1; // Nether Rending underlay icon
                }
            }

            // Int Properties
            claw.SetProperty(PropertyInt.DamageType, (int)element);
            claw.SetProperty(PropertyInt.Damage, 130);
            claw.SetProperty(PropertyInt.WieldDifficulty, 800);
            claw.SetProperty(PropertyInt.WieldRequirements, 2);
            claw.SetProperty(PropertyInt.WieldSkillType, 46);
            claw.SetProperty(PropertyInt.GearCritDamage, 3);
            claw.SetProperty(PropertyInt.GemCount, 4);
            claw.SetProperty(PropertyInt.GemType, 38);
            claw.SetProperty(PropertyInt.Cleaving, 3); // Cleaving
            if (rendType != ImbuedEffectType.Undef)
            {
                claw.SetProperty(PropertyInt.ImbuedEffect, (int)rendType);
            }
            claw.SetProperty(PropertyInt.ItemCurMana, 1402);
            claw.SetProperty(PropertyInt.ItemMaxMana, 1814);
            claw.SetProperty(PropertyInt.ItemSpellcraft, 370);
            claw.SetProperty(PropertyInt.ItemWorkmanship, 9);
            claw.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
            claw.SetProperty(PropertyInt.NumTimesTinkered, 10);
            claw.SetProperty(PropertyInt.Value, 0); // Sets value to 0
            claw.SetProperty(PropertyInt.Bonded, 1);
            claw.SetProperty(PropertyInt.Attuned, 1);
            claw.SetProperty(PropertyInt.AttackType, 486); // Fine Bandit Hilt multi-strike attack style
            claw.SetProperty(PropertyInt.WieldRequirements2, 8);
            claw.SetProperty(PropertyInt.WieldSkillType2, 46);
            claw.SetProperty(PropertyInt.WieldDifficulty2, 3);
            claw.SetProperty(PropertyBool.Ivoryable, true);

            // Float Properties
            claw.SetProperty(PropertyFloat.DamageMod, 1.075f); // Base 1.0f + 0.075f from Recipe 527870096
            claw.SetProperty(PropertyFloat.DamageVariance, 0.53076923f);
            claw.SetProperty(PropertyFloat.ManaRate, -0.06666667f);
            claw.SetProperty(PropertyFloat.WeaponDefense, 1.29f);
            claw.SetProperty(PropertyFloat.WeaponMagicDefense, 1.04f);
            claw.SetProperty(PropertyFloat.CriticalFrequency, 0.58f); // Base 0.33f + 0.25f from Recipe 527870096
            claw.SetProperty(PropertyFloat.CriticalMultiplier, 2.425f); // Base 2.25f + 0.175f from Recipe 527870096
            claw.SetProperty(PropertyFloat.ManaStoneDestroyChance, 0.01f);

            // Claw Spells
            claw.Biota.ClearSpells(claw.BiotaDatabaseLock);
            var clawSpells = new List<uint>()
            {
                6089, // Legendary Blood Thirst (CantripBloodThirst4)
            };

            foreach (var spellId in clawSpells)
            {
                claw.Biota.GetOrAddKnownSpell((int)spellId, claw.BiotaDatabaseLock, out _);
            }
            claw.ChangesDetected = true;
            claw.UiEffects = UiEffects.Magical;

            if (destination != null)
                destination.TryAddToInventory(claw);
            else
                player.TryCreateInInventoryWithNetworking(claw);
        }

        private static void CreateCustomWand(Player player, DamageType element, Container destination = null)
        {
            uint baseWcid = 46122;
            string elementLabel = GetElementLabel(element);
            string weaponName = $"{elementLabel} Wand (Test)";
            if (HasItemNamed(player, weaponName)) return;

            var wand = WorldObjectFactory.CreateNewWorldObject(baseWcid);
            if (wand == null) return;

            wand.Name = weaponName;
            wand.SetProperty(PropertyString.Name, weaponName);
            wand.SetProperty(PropertyString.LongDesc, $"{weaponName} of Corrosion, set with 2 Peridots");

            // Underlays
            ImbuedEffectType rendType = element switch
            {
                DamageType.Slash => ImbuedEffectType.SlashRending,
                DamageType.Pierce => ImbuedEffectType.PierceRending,
                DamageType.Bludgeon => ImbuedEffectType.BludgeonRending,
                DamageType.Cold => ImbuedEffectType.ColdRending,
                DamageType.Fire => ImbuedEffectType.FireRending,
                DamageType.Acid => ImbuedEffectType.AcidRending,
                DamageType.Electric => ImbuedEffectType.ElectricRending,
                DamageType.Nether => ImbuedEffectType.NetherRending,
                _ => ImbuedEffectType.Undef
            };

            if (rendType != ImbuedEffectType.Undef)
            {
                if (ACE.Server.Managers.RecipeManager.IconUnderlay.TryGetValue(rendType, out var underlayId))
                {
                    wand.IconUnderlayId = underlayId;
                }
                else if (rendType == ImbuedEffectType.NetherRending)
                {
                    wand.IconUnderlayId = 0x060067A1; // Nether Rending underlay icon
                }
            }

            // Int Properties
            wand.SetProperty(PropertyInt.DamageType, (int)element);
            wand.SetProperty(PropertyInt.WieldDifficulty, 650);
            wand.SetProperty(PropertyInt.WieldRequirements, 2);
            wand.SetProperty(PropertyInt.WieldSkillType, element == DamageType.Nether ? (int)43 : (int)34); // Void Magic or Mana Conversion
            wand.SetProperty(PropertyInt.GearCritDamage, 3);
            wand.SetProperty(PropertyInt.GemCount, 2);
            wand.SetProperty(PropertyInt.GemType, 34); // Peridot
            if (rendType != ImbuedEffectType.Undef)
            {
                wand.SetProperty(PropertyInt.ImbuedEffect, (int)rendType);
            }
            wand.SetProperty(PropertyInt.ImbuedEffect2, (int)ImbuedEffectType.MagicDefense); // MagicDefense
            wand.SetProperty(PropertyInt.ItemCurMana, 4084);
            wand.SetProperty(PropertyInt.ItemMaxMana, 4084);
            wand.SetProperty(PropertyInt.ItemDifficulty, 400);
            wand.SetProperty(PropertyInt.ItemSpellcraft, 370);
            wand.SetProperty(PropertyInt.ItemWorkmanship, 7);
            wand.SetProperty(PropertyInt.MaterialType, 0); // Suppress material prefix
            wand.SetProperty(PropertyInt.NumTimesTinkered, 1);
            wand.Biota.TryRemoveProperty(PropertyInt.SlayerCreatureType, wand.BiotaDatabaseLock);
            wand.SetProperty(PropertyInt.Value, 31410);
            wand.SetProperty(PropertyInt.WeaponType, 0);
            wand.SetProperty(PropertyInt.EncumbranceVal, 50);

            // Float Properties
            wand.SetProperty(PropertyFloat.CriticalFrequency, 0.33f);
            wand.SetProperty(PropertyFloat.CriticalMultiplier, 2.25f);
            wand.SetProperty(PropertyFloat.ElementalDamageMod, 1.375f);
            wand.SetProperty(PropertyFloat.ManaConversionMod, 0.19f);
            wand.SetProperty(PropertyFloat.ManaRate, -0.06666667f);
            wand.SetProperty(PropertyFloat.WeaponDefense, 1.23f);

            // Wand Spells
            wand.Biota.ClearSpells(wand.BiotaDatabaseLock);
            var wandSpells = new List<uint>()
            {
                6098, // Legendary Spirit Thirst
            };

            foreach (var spellId in wandSpells)
            {
                wand.Biota.GetOrAddKnownSpell((int)spellId, wand.BiotaDatabaseLock, out _);
            }
            wand.ChangesDetected = true;
            wand.UiEffects = UiEffects.Magical;

            if (destination != null)
                destination.TryAddToInventory(wand);
            else
                player.TryCreateInInventoryWithNetworking(wand);
        }

        private static void SpawnHilts(Player player, Container destination = null)
        {
            var existingHiltCount = player.GetAllPossessionsDeep().Count(i => i.WeenieClassId == 719220045);
            if (existingHiltCount >= 10) return;

            int toSpawn = 10 - existingHiltCount;
            for (int i = 0; i < toSpawn; i++)
            {
                var hilt = WorldObjectFactory.CreateNewWorldObject(719220045);
                if (hilt != null)
                {
                    if (destination != null)
                        destination.TryAddToInventory(hilt);
                    else
                        player.TryCreateInInventoryWithNetworking(hilt);
                }
            }
        }

        private static string GetElementLabel(DamageType element)
        {
            return element switch
            {
                DamageType.Electric => "Lightning",
                DamageType.Nether => "Nether",
                _ => element.ToString()
            };
        }

        private static void SpawnTeleportGems(Player player, Container destination = null)
        {
            var gemWcids = new List<uint>()
            {
                // Portal-Sending / Summoning Gems
                86753051,   // Frozen Valley Everlasting Portal Gem
                644540104,  // Gaerlan's Library Portal Sending Gem
                64454045,   // Hoshino Tent Sending Gem
                290444450,  // Mhoire Castle Portal Sending Gem
                290444449,  // Timaru Portal Sending Gem
                64454046,   // Town Network Sending Gem
                290444451,  // Tusker King Sending Gem
                86753080,   // Lifestone Sending Gem
                694200120,  // Infinite Viridian Rise Deru Portal Sending Gem
                53450,      // Viridian Rise Deru Portal Sending Gem (Single Use)
                2005053,    // Infinite Town Network Portal Gem
                290500127,  // Unlimited Dark Island Portal Gem
                290500126,  // Unlimited Vissidal Island Portal Gem
                227190017,  // Restored Portal Gem
                694200501,  // Pet Shop Quest Portal gem
                694200509,  // Burun History Quest Portal gem
                694200181,  // Enlightened Facility Hub Portal Gem
                3110166,    // Valley of Death Encampment Gem
                71271,      // Inner Burial Chamber Portal Sending Gem
                694200385,  // Defense of Zaikhal portal gem
                694200389,  // Elysas Favor portal gem

                // Quest Explorer Portal Gems
                694200515,  // Quest Explorer Portal Gem Week2
                694200518,  // Quest Explorer Portal Gem Week3
                694200521,  // Quest Explorer Portal Gem Week4
                694200524,  // Quest Explorer Portal Gem Week5
                694200527,  // Quest Explorer Portal Gem Week6
                694200530,  // Quest Explorer Portal Gem Week7

                // Self-Teleport / Recall Gems
                86753075,   // Marketplace Recall Gem
                86753079,   // Lifestone Recall Gem
                86753076,   // Portal Recall Gem
                86753077,   // Primary Portal Recall Gem
                86753078,   // Secondary Portal Recall Gem
                3110009,    // Wicked Wares Gem
                98760170,   // Zerivax Recall Gem
                300101193,  // Thaelaryn Lassel Recall Gem
                867530155,  // Penthouse Penthouse Recall Gem
                867530100,  // Costco Recall Gem
                64454319,   // Igmo's Retreat Recall Gem
                98760065,   // Fraternity of QB Recall Gem
                19851000,   // Halls of Introduction Gem
                19860016,   // Prestige Palace Gem
                500008972,  // Plateau of Agility Gem
                300101097,  // Admin Bog Gem

                // Housing Residence Hall Recalls
                227190148,  // Alphus Court Recall Gem
                227190149,  // Gajin Dwellings Recall Gem
                227190154,  // Hasina Gardens Recall Gem
                227190150,  // Heartland Yard Recall Gem
                227190151,  // Ivory Gate Recall Gem
                227190152,  // Lakespur Gardens Recall Gem
                227190153,  // Mellas Court Recall Gem
                227190155,  // Valorya Gate Recall Gem
                227190156,  // Vesper Gate Recall Gem
                227190157   // Winthur Gate Recall Gem
            };

            var playerWcids = new HashSet<uint>(player.GetAllPossessionsDeep().Select(i => i.WeenieClassId));

            foreach (var wcid in gemWcids)
            {
                if (playerWcids.Contains(wcid)) continue;

                var gem = WorldObjectFactory.CreateNewWorldObject(wcid);
                if (gem != null)
                {
                    if (destination != null)
                        destination.TryAddToInventory(gem);
                    else
                        player.TryCreateInInventoryWithNetworking(gem);
                }
            }
        }
    }
}
