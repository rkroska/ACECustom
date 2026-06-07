using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.Structure;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public bool SpellIsKnown(uint spellId)
        {
            return Biota.SpellIsKnown((int)spellId, BiotaDatabaseLock);
        }

        /// <summary>
        /// Will return true if the spell was added, or false if the spell already exists.
        /// </summary>
        public bool AddKnownSpell(uint spellId)
        {
            Biota.GetOrAddKnownSpell((int)spellId, BiotaDatabaseLock, out var spellAdded);

            if (spellAdded)
                ChangesDetected = true;

            return spellAdded;
        }

        /// <summary>
        /// Removes a known spell from the player's spellbook
        /// </summary>
        public bool RemoveKnownSpell(uint spellId)
        {
            return Biota.TryRemoveKnownSpell((int)spellId, BiotaDatabaseLock);
        }

        public void LearnSpellWithNetworking(uint spellId, bool uiOutput = true)
        {
            var spells = DatManager.PortalDat.SpellTable;

            if (!spells.Spells.ContainsKey(spellId))
            {
                GameMessageSystemChat errorMessage = new GameMessageSystemChat("SpellID not found in Spell Table", ChatMessageType.Broadcast);
                Session.Network.EnqueueSend(errorMessage);
                return;
            }

            if (!AddKnownSpell(spellId))
            {
                if (uiOutput)
                {
                    GameMessageSystemChat errorMessage = new GameMessageSystemChat("You already know that spell!", ChatMessageType.Broadcast);
                    Session.Network.EnqueueSend(errorMessage);
                }
                return;
            }

            GameEventMagicUpdateSpell updateSpellEvent = new GameEventMagicUpdateSpell(Session, (ushort)spellId);
            Session.Network.EnqueueSend(updateSpellEvent);

            // Check to see if we echo output to the client text area and do playscript animation
            if (uiOutput)
            {
                // Always seems to be this SkillUpPurple effect
                ApplyVisualEffects(PlayScript.SkillUpPurple);

                string message = $"You learn the {spells.Spells[spellId].Name} spell.\n";
                GameMessageSystemChat learnMessage = new GameMessageSystemChat(message, ChatMessageType.Broadcast);
                Session.Network.EnqueueSend(learnMessage);
            }
            else
            {
                Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, "You have learned a new spell."));
            }
        }

        /// <summary>
        ///  Learns spells in bulk, without notification, filtered by school and level
        /// </summary>
        public void LearnSpellsInBulk(MagicSchool school, uint spellLevel, bool withNetworking = true)
        {
            var spellTable = DatManager.PortalDat.SpellTable;

            foreach (var spellID in PlayerSpellTable)
            {
                if (!spellTable.Spells.ContainsKey(spellID))
                {
                    Console.WriteLine($"Unknown spell ID in PlayerSpellID table: {spellID}");
                    continue;
                }
                var spell = new Spell(spellID, false);
                if (spell.School == school && spell.Formula.Level == spellLevel)
                {
                    if (withNetworking)
                        LearnSpellWithNetworking(spell.Id, false);
                    else
                        AddKnownSpell(spell.Id);
                }
            }
        }

        public void HandleActionMagicRemoveSpellId(uint spellId)
        {
            if (!Biota.TryRemoveKnownSpell((int)spellId, BiotaDatabaseLock))
            {
                log.Error("Invalid spellId passed to Player.RemoveSpellFromSpellBook");
                return;
            }

            ChangesDetected = true;

            GameEventMagicRemoveSpell removeSpellEvent = new GameEventMagicRemoveSpell(Session, (ushort)spellId);
            Session.Network.EnqueueSend(removeSpellEvent);
        }

        public void EquipItemFromSet(WorldObject item)
        {
            if (!item.HasItemSet) return;

            var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

            var spells = GetSpellSet(setItems);

            // get the spells from before / without this item
            setItems.Remove(item);
            var prevSpells = GetSpellSet(setItems);

            EquipDequipItemFromSet(item, spells, prevSpells);
        }

        public void EquipDequipItemFromSet(WorldObject item, List<Spell> spells, List<Spell> prevSpells, WorldObject surrogateItem = null)
        {
            // compare these 2 spell sets -
            // see which spells are being added, and which are being removed
            var addSpells = spells.Except(prevSpells);
            var removeSpells = prevSpells.Except(spells);

            // set spells are not affected by mana
            // if it's equipped, it's active.

            foreach (var spell in removeSpells)
                EnchantmentManager.Dispel(EnchantmentManager.GetEnchantment(spell.Id, item.EquipmentSetId.Value));

            var addItem = surrogateItem ?? item;

            foreach (var spell in addSpells)
                CreateItemSpell(addItem, spell.Id);
        }

        public void DequipItemFromSet(WorldObject item)
        {
            if (!item.HasItemSet) return;

            var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

            // for better bookkeeping, and to avoid a rarish error with AuditItemSpells detecting -1 duration item enchantments where
            // the CasterGuid is no longer in the player's possession
            var surrogateItem = setItems.LastOrDefault();

            var spells = GetSpellSet(setItems);

            // get the spells from before / with this item
            setItems.Add(item);
            var prevSpells = GetSpellSet(setItems);

            if (surrogateItem == null)
            {
                var addSpells = spells.Except(prevSpells);

                if (addSpells.Count() != 0)
                    log.Error($"{Name}.DequipItemFromSet({item.Name}) -- last item in set dequipped, but addSpells still contains {string.Join(", ", addSpells.Select(i => i.Name))} -- this shouldn't happen!");
            }

            EquipDequipItemFromSet(item, spells, prevSpells, surrogateItem);
        }

        public void OnItemLevelUp(WorldObject item, int prevItemLevel)
        {
            if (!item.HasItemSet) //general item level up
            {
                if (item.ItemType == ItemType.Armor)
                {
                    item.ArmorLevel += 5;
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} gained +5 Armor Level!", ChatMessageType.Advancement));
                    if (prevItemLevel % 5 == 0)
                    {
                        item.ArmorModVsAcid += 0.5;
                        item.ArmorModVsBludgeon += 0.5;
                        item.ArmorModVsCold += 0.5;
                        item.ArmorModVsElectric += 0.5;
                        item.ArmorModVsFire += 0.5;
                        item.ArmorModVsNether += 0.5;
                        item.ArmorModVsPierce += 0.5;
                        item.ArmorModVsSlash += 0.5;
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} gained +5 Defenses!", ChatMessageType.Advancement));
                    }

                }
                if (item.ItemType == ItemType.Clothing || item.ItemType == ItemType.Jewelry)
                {
                    if (item.ArmorLevel.HasValue)
                    {
                        item.ArmorLevel += 5;
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} gained +5 Armor Level!", ChatMessageType.Advancement));
                    }
                    else
                    {
                        if (item.ItemMaxMana.HasValue)
                        {
                            item.ItemMaxMana += 50;
                            Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} gained +50 Max Mana!", ChatMessageType.Advancement));
                        }                                                
                    }
                    
                }
                if (item.ItemType == ItemType.MeleeWeapon || item.ItemType == ItemType.MissileWeapon)
                {
                    if (prevItemLevel % 2 == 0)
                    {
                        item.Damage += 1;
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} gained +1 Damage!", ChatMessageType.Advancement));
                    }
                    else
                    {
                        item.DamageVariance -= 0.04;
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} improved +4% Variance!", ChatMessageType.Advancement));
                    }

                    if (prevItemLevel % 5 == 0)
                    {
                        item.WeaponDefense += 0.01;
                        item.WeaponMissileDefense += 0.01;
                        item.WeaponMagicDefense += 0.01;
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} gained +2% Defenses!", ChatMessageType.Advancement));
                    }
                }
                if (item.ItemType == ItemType.Caster)
                {
                    if (item.WeaponDefense == null)
                    {
                        item.WeaponDefense = 1.01;                      
                    }
                    else
                    {
                        item.WeaponDefense += 0.01;
                    }

                    if (item.ManaConversionMod == null)
                    {
                        item.ManaConversionMod = 1.01;
                    }
                    else
                    {
                        item.ManaConversionMod += 0.01;
                    }
                    if (prevItemLevel % 3 == 0)
                    {
                        if (item.WeaponMagicDefense == null)
                        {
                            item.WeaponMagicDefense = 1.01;
                        }
                        else
                        {
                            item.WeaponMagicDefense += 0.01;
                        }
                    }

                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} gained 1% Defense Mod!", ChatMessageType.Advancement));
                    if (item.ElementalDamageMod > 0)
                    {
                        item.ElementalDamageMod += 0.01;
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} gained 1% Elemental Damage Mod!", ChatMessageType.Advancement));
                    }
                }
            }
            else
            {
                var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

                var levelDiff = prevItemLevel - (item.ItemLevel ?? 0);

                var prevSpells = GetSpellSet(setItems, levelDiff);

                var spells = GetSpellSet(setItems);

                EquipDequipItemFromSet(item, spells, prevSpells);
            }

        }

        public void CreateSentinelBuffPlayers(IEnumerable<Player> players, bool self = false, ulong maxLevel = 8)
        {
            if (!(Session.AccessLevel >= AccessLevel.Sentinel)) return;

            var SelfOrOther = self ? "Self" : "Other";

            // ensure level 8s are installed
            var maxSpellLevel = Math.Clamp(maxLevel, 1, 8);
            if (maxSpellLevel == 8 && DatabaseManager.World.GetCachedSpell((uint)SpellId.ArmorOther8) == null)
                maxSpellLevel = 7;

            var tySpell = typeof(SpellId);
            List<BuffMessage> buffMessages = new List<BuffMessage>();
            // prepare messages
            List<string> buffsNotImplementedYet = new List<string>();
            foreach (var spell in Buffs)
            {
                var spellNamPrefix = spell;
                bool isBane = false;
                if (spellNamPrefix.StartsWith("@"))
                {
                    isBane = true;
                    spellNamPrefix = spellNamPrefix.Substring(1);
                }
                string fullSpellEnumName = spellNamPrefix + ((isBane) ? string.Empty : SelfOrOther) + maxSpellLevel;
                string fullSpellEnumNameAlt = spellNamPrefix + ((isBane) ? string.Empty : ((SelfOrOther == "Self") ? "Other" : "Self")) + maxSpellLevel;
                uint spellID = (uint)Enum.Parse(tySpell, fullSpellEnumName);
                var buffMsg = BuildBuffMessage(spellID);

                if (buffMsg == null)
                {
                    spellID = (uint)Enum.Parse(tySpell, fullSpellEnumNameAlt);
                    buffMsg = BuildBuffMessage(spellID);
                }

                if (buffMsg != null)
                {
                    buffMsg.Bane = isBane;
                    buffMessages.Add(buffMsg);
                }
                else
                {
                    buffsNotImplementedYet.Add(fullSpellEnumName);
                }
            }
            // buff each player
            players.ToList().ForEach(targetPlayer =>
            {
                if (buffMessages.Any(k => !k.Bane))
                {
                    // bake player into the messages
                    buffMessages.Where(k => !k.Bane).ToList().ForEach(k => k.SetTargetPlayer(targetPlayer));
                    // update client-side enchantments
                    targetPlayer.Session.Network.EnqueueSend(buffMessages.Where(k => !k.Bane).Select(k => k.SessionMessage).ToArray());
                    // run client-side effect scripts, omitting duplicates
                    targetPlayer.EnqueueBroadcast(buffMessages.Where(k => !k.Bane).ToList().GroupBy(m => m.Spell.TargetEffect).Select(a => a.First().LandblockMessage).ToArray());
                    // update server-side enchantments

                    var buffsForPlayer = buffMessages.Where(k => !k.Bane).ToList().Select(k => k.Enchantment);

                    var lifeBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.LifeMagic).ToList();
                    var critterBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.CreatureEnchantment).ToList();
                    var itemBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.ItemEnchantment).ToList();

                    lifeBuffsForPlayer.ForEach(spl =>
                    {
                        CreateEnchantmentSilent(spl.Spell, targetPlayer);
                    });
                    critterBuffsForPlayer.ForEach(spl =>
                    {
                        CreateEnchantmentSilent(spl.Spell, targetPlayer);
                    });
                    itemBuffsForPlayer.ForEach(spl =>
                    {
                        CreateEnchantmentSilent(spl.Spell, targetPlayer);
                    });
                }
                if (buffMessages.Any(k => k.Bane))
                {
                    // Impen/bane
                    var items = targetPlayer.EquippedObjects.Values.ToList();
                    var itembuffs = buffMessages.Where(k => k.Bane).ToList();
                    foreach (var itemBuff in itembuffs)
                    {
                        foreach (var item in items)
                        {
                            if ((item.WeenieType == WeenieType.Clothing || item.IsShield) && item.IsEnchantable)
                                CreateEnchantmentSilent(itemBuff.Spell, item);
                        }
                    }
                }
            });
        }

        public void ApplyUltimateBlessings()
        {
            if (!CharmSettingsManager.AutoRebuff.Enabled)
                return;

            var currentTime = ACE.Common.Time.GetUnixTime();
            var dispelLockoutActive = currentTime - LastDispelTimestamp < 180.0;
            if (dispelLockoutActive)
            {
                var remainingSeconds = (int)Math.Ceiling(180.0 - (currentTime - LastDispelTimestamp));
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"You cannot use the Auto-Rebuff Charm while under a dispel lockout. Try again in {remainingSeconds}s.", ChatMessageType.Broadcast));
                return;
            }

            var maxSpellLevel = 8;
            // Make sure level 8s are installed in the world DB (fallback to 7 if missing)
            if (DatabaseManager.World.GetCachedSpell((uint)SpellId.ArmorOther8) == null)
                maxSpellLevel = 7;

            var tySpell = typeof(SpellId);
            List<BuffMessage> buffMessages = new List<BuffMessage>();

            foreach (var spell in Buffs)
            {
                var spellNamPrefix = spell;
                bool isBane = false;
                if (spellNamPrefix.StartsWith("@"))
                {
                    isBane = true;
                    spellNamPrefix = spellNamPrefix.Substring(1);
                }

                // Gems always target "Self" for buffs (Attributes, Vitals, Protections, Masteries)
                string fullSpellEnumName = spellNamPrefix + ((isBane) ? string.Empty : "Self") + maxSpellLevel;
                string fullSpellEnumNameAlt = spellNamPrefix + ((isBane) ? string.Empty : "Other") + maxSpellLevel;

                uint spellID = 0;
                uint spellIDAlt = 0;

                if (Enum.TryParse(tySpell, fullSpellEnumName, out object parsedId))
                {
                    spellID = (uint)parsedId;
                }

                if (Enum.TryParse(tySpell, fullSpellEnumNameAlt, out object parsedIdAlt))
                {
                    if (spellID == 0)
                        spellID = (uint)parsedIdAlt;
                    else if (spellID != (uint)parsedIdAlt)
                        spellIDAlt = (uint)parsedIdAlt;
                }

                if (spellID == 0)
                    continue;

                var buffMsg = BuildBuffMessage(spellID);
                if (buffMsg != null)
                {
                    buffMsg.Bane = isBane;

                    // Enforce Option B: Check magic requirements to cast this spell
                    var spellObj = buffMsg.Spell;
                    var school = spellObj.School;

                    // 1. Skill check: Magic School must be trained or specialized
                    var skill = GetCreatureSkill(school);
                    if (skill == null || skill.AdvancementClass < SkillAdvancementClass.Trained)
                        continue;

                    // 2. Foci / Augment check: Player must have focus or augment
                    if (FociWCIDs.ContainsKey(school) && !HasFoci(school))
                        continue;

                    // 3. Spellbook check: Player must have learned the specific spell (or the Other variant)
                    if (!SpellIsKnown(spellID) && (spellIDAlt == 0 || !SpellIsKnown(spellIDAlt)))
                        continue;

                    buffMessages.Add(buffMsg);
                }
            }

            // 1. Buff Player
            var playerBuffs = buffMessages.Where(k => !k.Bane).ToList();
            if (playerBuffs.Count > 0)
            {
                playerBuffs.ForEach(k => k.SetTargetPlayer(this));
                // update client-side enchantments
                Session?.Network?.EnqueueSend(playerBuffs.Select(k => k.SessionMessage).ToArray());

                // Queue client-side effect scripts to stagger them sequentially
                PendingStaggeredEvents.Clear();

                var lifePrefixes = new[] { "Fire Protection", "Acid Protection", "Cold Protection", "Lightning Protection", "Bludgeon", "Blade Protection", "Piercing Protection", "Magic Resistance" };

                var lifeVisuals = new List<GameMessageScript>();

                foreach (var prefix in lifePrefixes)
                {
                    var buff = playerBuffs.FirstOrDefault(b => b.Spell.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase));
                    if (buff != null) lifeVisuals.Add(buff.LandblockMessage);
                }

                // Only enqueue slots for visuals that actually matched — no empty-slot churn
                for (int i = 0; i < lifeVisuals.Count; i++)
                {
                    var evt = new StaggeredVisualEvent { BroadcastTimeOffset = i * 1.0 };
                    evt.Visuals.Add(lifeVisuals[i]);
                    PendingStaggeredEvents.Enqueue(evt);
                }

                StaggeredCascadeStartTime = ACE.Common.Time.GetUnixTime();
                
                // update server-side enchantments
                var buffsForPlayer = playerBuffs.Select(k => k.Enchantment).ToList();
                var lifeBuffs = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.LifeMagic).ToList();
                var critterBuffs = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.CreatureEnchantment).ToList();
                var itemBuffs = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.ItemEnchantment).ToList();

                lifeBuffs.ForEach(spl => CreateEnchantmentSilent(spl.Spell, this));
                critterBuffs.ForEach(spl => CreateEnchantmentSilent(spl.Spell, this));
                itemBuffs.ForEach(spl => CreateEnchantmentSilent(spl.Spell, this));
            }

            // 2. Cast Banes and Impen on all gear (both equipped and in all bags recursively)
            var itemBuffsList = buffMessages.Where(k => k.Bane).ToList();
            if (itemBuffsList.Count > 0)
            {
                var allGear = GetAllPossessionsDeep()
                    .Where(item => (item.WeenieType == WeenieType.Clothing || item.IsShield) && item.IsEnchantable)
                    .ToList();

                foreach (var itemBuff in itemBuffsList)
                {
                    foreach (var item in allGear)
                    {
                        CreateEnchantmentSilent(itemBuff.Spell, item);
                    }
                }
            }

            // Send chat confirmation immediately
            bool appliedAny = playerBuffs.Count > 0 || itemBuffsList.Count > 0;
            if (appliedAny)
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat("You feel a surge of ultimate blessings flow through you and all your gear!", ChatMessageType.Broadcast));
            }
            else
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat("The Auto-Rebuff Charm is active, but you do not meet the magic requirements (trained school, focus/augment, learned spell) for any of its buffs.", ChatMessageType.Broadcast));
            }
        }

        private void CreateEnchantmentSilent(Spell spell, WorldObject target)
        {
            var addResult = target.EnchantmentManager.Add(spell, this, null);

            if (target is Player targetPlayer)
            {
                // Use safe navigation: player may disconnect mid-loop (e.g., during multi-item bane application)
                targetPlayer.Session?.Network?.EnqueueSend(new GameEventMagicUpdateEnchantment(targetPlayer.Session, new Enchantment(targetPlayer, addResult.Enchantment)));

                targetPlayer.HandleSpellHooks(spell);
            }
        }

        // TODO: switch this over to SpellProgressionTables
        private static string[] Buffs = new string[] {
#region spells
            // @ indicates impenetrability or a bane
            "Strength",
            "Invulnerability",
            "FireProtection",
            "Armor",
            "Rejuvenation",
            "Regeneration",
            "ManaRenewal",
            "Impregnability",
            "MagicResistance",
            //"AxeMastery",    // light weapons
            "LightWeaponsMastery",
            //"DaggerMastery", // finesse weapons
            "FinesseWeaponsMastery",
            //"MaceMastery",
            //"SpearMastery",
            //"StaffMastery",
            //"SwordMastery",  // heavy weapons
            "HeavyWeaponsMastery",
            //"UnarmedCombatMastery",
            //"BowMastery",    // missile weapons
            "MissileWeaponsMastery",
            //"CrossbowMastery",
            //"ThrownWeaponMastery",
            "AcidProtection",
            "CreatureEnchantmentMastery",
            "ItemEnchantmentMastery",
            "LifeMagicMastery",
            "WarMagicMastery",
            "ManaMastery",
            "ArcaneEnlightenment",
            "ArcanumSalvaging",
            "ArmorExpertise",
            "ItemExpertise",
            "MagicItemExpertise",
            "WeaponExpertise",
            "MonsterAttunement",
            "PersonAttunement",
            "DeceptionMastery",
            "HealingMastery",
            "LeadershipMastery",
            "LockpickMastery",
            "Fealty",
            "JumpingMastery",
            "Sprint",
            "BludgeonProtection",
            "ColdProtection",
            "LightningProtection",
            "BladeProtection",
            "PiercingProtection",
            "Endurance",
            "Coordination",
            "Quickness",
            "Focus",
            "Willpower",
            "CookingMastery",
            "FletchingMastery",
            "AlchemyMastery",
            "VoidMagicMastery",
            "SummoningMastery",
            "SwiftKiller",
            "Defender",
            "BloodDrinker",
            "HeartSeeker",
            "HermeticLink",
            "SpiritDrinker",
            "ShieldMastery",
            "DualWieldMastery",
            "TwoHandedMastery",
            "DirtyFightingMastery",
            "RecklessnessMastery",
            "SneakAttackMastery",
            "@Impenetrability",
            "@PiercingBane",
            "@BludgeonBane",
            "@BladeBane",
            "@AcidBane",
            "@FlameBane",
            "@FrostBane",
            "@LightningBane",
#endregion
            };

        private class BuffMessage
        {
            public bool Bane { get; set; } = false;
            public GameEventMagicUpdateEnchantment SessionMessage { get; set; } = null;
            public GameMessageScript LandblockMessage { get; set; } = null;
            public Spell Spell { get; set; } = null;
            public Enchantment Enchantment { get; set; } = null;
            public void SetTargetPlayer(Player p)
            {
                Enchantment.Target = p;
                SessionMessage = new GameEventMagicUpdateEnchantment(p.Session, Enchantment);
                SetLandblockMessage(p.Guid);
            }
            public void SetLandblockMessage(ObjectGuid target)
            {
                LandblockMessage = new GameMessageScript(target, Spell.TargetEffect, 1f);
            }
        }

        private static BuffMessage BuildBuffMessage(uint spellID)
        {
            BuffMessage buff = new BuffMessage();
            buff.Spell = new Spell(spellID);
            if (buff.Spell.NotFound) return null;
            buff.Enchantment = new Enchantment(null, 0, spellID, 1, (EnchantmentMask)buff.Spell.StatModType, buff.Spell.StatModVal);
            return buff;
        }

        public void HandleSpellbookFilters(SpellBookFilterOptions filters)
        {
            Character.SpellbookFilters = (uint)filters;
        }

        public void HandleSetDesiredComponentLevel(uint component_wcid, uint amount)
        {
            // ensure wcid is spell component
            if (!SpellComponent.IsValid(component_wcid))
            {
                log.Warn($"{Name}.HandleSetDesiredComponentLevel({component_wcid}, {amount}): invalid spell component wcid");
                return;
            }
            if (amount > 0)
            {
                var existing = Character.GetFillComponent(component_wcid, CharacterDatabaseLock);

                if (existing == null)
                    Character.AddFillComponent(component_wcid, amount, CharacterDatabaseLock, out bool exists);
                else
                    existing.QuantityToRebuy = (int)amount;
            }
            else
                Character.TryRemoveFillComponent(component_wcid, out var _, CharacterDatabaseLock);

            CharacterChangesDetected = true;
        }

        public static Dictionary<MagicSchool, uint> FociWCIDs = new Dictionary<MagicSchool, uint>()
        {
            { MagicSchool.CreatureEnchantment, 15268 },   // Foci of Enchantment
            { MagicSchool.ItemEnchantment,     15269 },   // Foci of Artifice
            { MagicSchool.LifeMagic,           15270 },   // Foci of Verdancy
            { MagicSchool.WarMagic,            15271 },   // Foci of Strife
            { MagicSchool.VoidMagic,           43173 },   // Foci of Shadow
        };

        public bool HasFoci(MagicSchool school)
        {
            switch (school)
            {
                case MagicSchool.CreatureEnchantment:
                    if (AugmentationInfusedCreatureMagic > 0)
                        return true;
                    break;
                case MagicSchool.ItemEnchantment:
                    if (AugmentationInfusedItemMagic > 0)
                        return true;
                    break;
                case MagicSchool.LifeMagic:
                    if (AugmentationInfusedLifeMagic > 0)
                        return true;
                    break;
                case MagicSchool.VoidMagic:
                    if (AugmentationInfusedVoidMagic > 0)
                        return true;
                    break;
                case MagicSchool.WarMagic:
                    if (AugmentationInfusedWarMagic > 0)
                        return true;
                    break;
            }

            // Use GetAllPossessionsDeep() so Focus Stones inside nested bags are detected.
            // Inventory.Values only covers the top-level bag — a foci in a bag-in-a-bag would be invisible.
            var wcid = FociWCIDs[school];
            return GetAllPossessionsDeep().FirstOrDefault(i => i.WeenieClassId == wcid) != null;
        }

        public void HandleSpellHooks(Spell spell)
        {
            HandleMaxVitalUpdate(spell);

            // unsure if spell hook was here in retail,
            // but this has the potential to take the client out of autorun mode
            // which causes them to stop if they hit a turn key afterwards
            if (ServerConfig.runrate_add_hooks.Value)
                HandleRunRateUpdate(spell);
        }

        /// <summary>
        /// Called when an enchantment is added or removed,
        /// checks if the spell affects the max vitals,
        /// and if so, updates the client immediately
        /// </summary>
        public void HandleMaxVitalUpdate(Spell spell)
        {
            var maxVitals = spell.UpdatesMaxVitals;

            if (maxVitals.Count == 0)
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(1.0f);      // client needs time for primary attribute updates
            actionChain.AddAction(this, ActionType.PlayerSpells_HandleMaxVitalUpdate, () =>
            {
                foreach (var maxVital in maxVitals)
                {
                    var playerVital = Vitals[maxVital];

                    Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute2ndLevel(this, playerVital.ToEnum(), playerVital.Current));
                }
            });
            actionChain.EnqueueChain();
        }

        public bool HandleRunRateUpdate(Spell spell)
        {
            if (!spell.UpdatesRunRate)
                return false;

            return HandleRunRateUpdate();
        }

        public void AuditItemSpells()
        {
            // cleans up bugged chars with dangling item set spells
            // from previous bugs

            var allPossessions = new Dictionary<ObjectGuid, WorldObject>();
            foreach (var item in GetAllPossessions())
                allPossessions[item.Guid] = item;

            // this is a legacy method, but is still a decent failsafe to catch any existing issues

            // get active item enchantments
            var enchantments = Biota.PropertiesEnchantmentRegistry.Clone(BiotaDatabaseLock).Where(i => i.Duration == -1 && i.SpellId != (int)SpellId.Vitae).ToList();

            foreach (var enchantment in enchantments)
            {
                var isCharm = false;
                if (allPossessions.TryGetValue(new ObjectGuid(enchantment.CasterObjectId), out var casterItem))
                    isCharm = casterItem.GetProperty(PropertyBool.IsCharm) ?? false;

                var table = (enchantment.HasSpellSetId || isCharm) ? allPossessions : EquippedObjects;

                // if this item is not present, remove enchantment
                if (!table.TryGetValue(new ObjectGuid(enchantment.CasterObjectId), out var item))
                {
                    var spell = new Spell(enchantment.SpellId, false);
                    log.Error($"{Name}.AuditItemSpells(): removing spell {spell.Name} from {(enchantment.HasSpellSetId ? "non-possessed" : "non-equipped")} item");

                    EnchantmentManager.Dispel(enchantment);
                    continue;
                }

                // is this item part of a set?
                if (!item.HasItemSet)
                    continue;

                // get all of the equipped items in this set
                // TODO: if we want sets to be compatible with charms, we need to check EquippedObjects AND charms in allPossessions.
                // This isn't something we want to support now, but it could be added later.
                var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

                // get all of the spells currently active from this set
                var currentSpells = GetSpellSet(setItems);

                // get all of the spells possible for this item set
                var possibleSpells = GetSpellSetAll((EquipmentSet)item.EquipmentSetId);

                // get the difference between them
                var inactiveSpells = possibleSpells.Except(currentSpells).ToList();

                // remove any item set spells that shouldn't be active
                foreach (var inactiveSpell in inactiveSpells)
                {
                    var removeSpells = enchantments.Where(i => i.SpellSetId == item.EquipmentSetId && i.SpellId == inactiveSpell.Id).ToList();

                    foreach (var removeSpell in removeSpells)
                    {
                        log.Error($"{Name}.AuditItemSpells(): removing spell {inactiveSpell.Name} from {item.EquipmentSetId}");

                        EnchantmentManager.Dispel(removeSpell);
                    }
                }
            }
        }

        // ── Auto-Rebuff Charm Support ──────────────────────────────────────────
        public bool HasAutoRebuffCharm { get; set; }
        public double LastDispelTimestamp { get; set; }
        public double LastAutoRebuffCheckTime { get; set; }
        public bool IsDispelMessageTriggered { get; set; }

        public class StaggeredVisualEvent
        {
            public double BroadcastTimeOffset { get; set; }
            public List<GameMessageScript> Visuals { get; } = new();
        }

        public Queue<StaggeredVisualEvent> PendingStaggeredEvents { get; } = new();
        public double StaggeredCascadeStartTime { get; set; }

        /// <summary>
        /// Returns true if any buff the player qualifies for (per Option B: trained skill, foci,
        /// spellbook) is either completely missing from their enchantment registry, or has less
        /// than 60 minutes (3600s) remaining. Banes are excluded — they are re-applied
        /// automatically whenever the player buff scan triggers a full rebuff.
        /// Returns false only when every qualifying buff is present and has >60 min remaining.
        /// </summary>
        public bool NeedsRebuff()
        {
            var maxSpellLevel = 8;
            if (DatabaseManager.World.GetCachedSpell((uint)SpellId.ArmorOther8) == null)
                maxSpellLevel = 7;

            var tySpell = typeof(SpellId);

            foreach (var spellPrefix in Buffs)
            {
                // Skip banes — they target gear items, not the player.
                // Banes are re-applied in the same ApplyUltimateBlessings() call when needed.
                if (spellPrefix.StartsWith("@"))
                    continue;

                // Resolve the spell ID the same way ApplyUltimateBlessings does
                string fullEnumName = spellPrefix + "Self"  + maxSpellLevel;
                string altEnumName  = spellPrefix + "Other" + maxSpellLevel;

                uint spellID = 0;
                uint spellIDAlt = 0;

                if (Enum.TryParse(tySpell, fullEnumName, out object parsed))
                {
                    spellID = (uint)parsed;
                }

                if (Enum.TryParse(tySpell, altEnumName, out object parsedAlt))
                {
                    if (spellID == 0)
                        spellID = (uint)parsedAlt;
                    else if (spellID != (uint)parsedAlt)
                        spellIDAlt = (uint)parsedAlt;
                }

                if (spellID == 0)
                    continue;

                // Resolve the spell object to check school
                var spell = new Spell(spellID);
                if (spell.NotFound) continue;

                var school = spell.School;

                // Option B: only consider spells the player is qualified to receive
                var skill = GetCreatureSkill(school);
                if (skill == null || skill.AdvancementClass < SkillAdvancementClass.Trained)
                    continue;

                if (FociWCIDs.ContainsKey(school) && !HasFoci(school))
                    continue;

                if (!SpellIsKnown(spellID) && (spellIDAlt == 0 || !SpellIsKnown(spellIDAlt)))
                    continue;

                // Check if the buff is missing entirely or expiring within 60 minutes
                var entry = EnchantmentManager.GetEnchantment(spellID);
                if (entry == null)
                    return true; // Missing — needs rebuff

                // Only evaluate expiration for spells with a positive duration.
                // Infinite or permanent enchantments (Duration <= 0, e.g. -1.0) never expire.
                if (entry.Duration > 0.0)
                {
                    // In ACE, StartTime counts downwards from 0 to -Duration, so remaining time is entry.Duration + entry.StartTime.
                    var remaining = entry.Duration + entry.StartTime;
                    if (remaining <= 3600.0 && remaining < entry.Duration - 60.0)
                        return true; // Expiring within 60 minutes — needs rebuff
                }
            }

            // If no qualifying buffs exist at all (fully gated by Option B), never auto-rebuff
            return false;
        }
    }
}
