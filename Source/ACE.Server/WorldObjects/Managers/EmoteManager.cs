using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Adapter;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;
using log4net;
using Position = ACE.Entity.Position;
using Spell = ACE.Server.Entity.Spell;

namespace ACE.Server.WorldObjects.Managers
{
    public class EmoteManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public WorldObject WorldObject => _proxy ?? _worldObject;

        private WorldObject _worldObject;
        private WorldObject _proxy;

        /// <summary>
        /// Returns TRUE if this WorldObject is currently busy processing other emotes
        /// </summary>
        public bool IsBusy { get; set; }
        public int Nested { get; set; }

        public bool Debug = false;

        public EmoteManager(WorldObject worldObject)
        {
            _worldObject = worldObject;
        }

        /// <summary>
        /// Executes an emote
        /// </summary>
        /// <param name="emoteSet">The parent set of this emote</param>
        /// <param name="emote">The emote to execute</param>
        /// <param name="targetObject">A target object, usually player</param>
        /// <param name="actionChain">Only used for passing to further sets</param>
        public float ExecuteEmote(PropertiesEmote emoteSet, PropertiesEmoteAction emote, WorldObject targetObject = null)
        {
            var player = targetObject as Player;
            var creature = WorldObject as Creature;
            var targetCreature = targetObject as Creature;

            var delay = 0.0f;
            var emoteType = (EmoteType)emote.Type;

            //if (Debug)
            //Console.WriteLine($"{WorldObject.Name}.ExecuteEmote({emoteType})");

            var text = emote.Message;

            switch ((EmoteType)emote.Type)
            {
                case EmoteType.Act:
                    // short for 'acting' text
                    var message = Replace(text, WorldObject, targetObject, emoteSet.Quest);
                    WorldObject.EnqueueBroadcast(new GameMessageSystemChat(message, ChatMessageType.Broadcast), 30.0f);
                    break;

                case EmoteType.Activate:

                    if (WorldObject.ActivationTarget > 0)
                    {
                        // ActOnUse delay?
                        var activationTarget = WorldObject.CurrentLandblock?.GetObject(WorldObject.ActivationTarget);
                        activationTarget?.OnActivate(WorldObject);
                    }
                    else if (WorldObject.GeneratorId.HasValue && WorldObject.GeneratorId > 0) // Fallback to linked generator
                    {
                        var linkedGenerator = WorldObject.CurrentLandblock?.GetObject(WorldObject.GeneratorId ?? 0);
                        linkedGenerator?.OnActivate(WorldObject);
                    }
                    break;

                case EmoteType.AddCharacterTitle:

                    // emoteAction.Stat == null for all EmoteType.AddCharacterTitle entries in current db?
                    if (player != null && emote.Amount != 0)
                        player.AddTitle((CharacterTitle)emote.Amount);
                    break;

                case EmoteType.AddContract:

                    // Contracts werent in emote table for 16py, guessing that Stat was used to hold id for contract.
                    if (player != null && emote.Stat.HasValue && emote.Stat.Value > 0)
                        player.ContractManager.Add(emote.Stat.Value);
                    break;

                case EmoteType.AdminSpam:

                    text = Replace(emote.Message, WorldObject, targetObject, emoteSet.Quest);

                    PlayerManager.BroadcastToChannelFromEmote(Channel.Admin, text);
                    break;

                case EmoteType.AwardLevelProportionalSkillXP:

                    var min = emote.Min64 ?? emote.Min ?? 0;
                    var max = emote.Max64 ?? emote.Max ?? 0;

                    if (player != null)
                        player.GrantLevelProportionalSkillXP((Skill)emote.Stat, emote.Percent ?? 0, min, max);
                    break;

                case EmoteType.AwardLevelProportionalXP:

                    min = emote.Min64 ?? emote.Min ?? 0;
                    max = emote.Max64 ?? emote.Max ?? 0;

                    if (player != null)
                        player.GrantLevelProportionalXp(emote.Percent ?? 0, min, max);
                    break;

                case EmoteType.AwardLuminance:

                    if (player != null)
                        player.EarnLuminance(emote.Amount64 ?? emote.HeroXP64 ?? 0, XpType.Quest, ShareType.None);

                    break;

                case EmoteType.AwardNoShareXP:

                    if (player != null)
                        player.EarnXP(emote.Amount64 ?? emote.Amount ?? 0, XpType.Quest, ShareType.None);

                    break;

                case EmoteType.AwardSkillPoints:

                    if (player != null)
                        player.AwardSkillPoints((Skill)emote.Stat, (uint)emote.Amount);
                    break;

                case EmoteType.AwardSkillXP:

                    if (player != null)
                    {
                        if (delay < 1) delay += 1; // because of how AwardSkillXP grants and then raises the skill, ensure delay is at least 1 to allow for processing correctly
                        player.AwardSkillXP((Skill)emote.Stat, (uint)emote.Amount, true);
                    }
                    break;

                case EmoteType.AwardTrainingCredits:

                    if (player != null)
                        player.AddSkillCredits(emote.Amount ?? 0);
                    break;

                case EmoteType.AwardXP:

                    if (player != null)
                    {
                        var amt = emote.Amount64 ?? emote.Amount ?? 0;
                        if (amt > 0)
                        {
                            player.EarnXP(amt, XpType.Quest, ShareType.All);
                        }
                        else if (amt < 0)
                        {
                            player.SpendXP((ulong)-amt);
                        }
                    }
                    break;

                case EmoteType.BLog:

                    text = Replace(emote.Message, WorldObject, targetObject, emoteSet.Quest);

                    log.Info($"0x{WorldObject.Guid}:{WorldObject.Name}({WorldObject.WeenieClassId}).EmoteManager.BLog - {text}");
                    break;

                case EmoteType.CastSpell:

                    if (WorldObject != null)
                    {
                        var spell = new Spell((uint)emote.SpellId);
                        if (spell.NotFound)
                        {
                            log.Error($"{WorldObject.Name} ({WorldObject.Guid}) EmoteManager.CastSpell - unknown spell {emote.SpellId}");
                            break;
                        }

                        creature.CheckForHumanPreCast(spell);

                        var spellTarget = GetSpellTarget(spell, targetObject);

                        var preCastTime = creature.PreCastMotion(spellTarget);

                        delay = preCastTime + creature.GetPostCastTime(spell);

                        var castChain = new ActionChain();
                        castChain.AddDelaySeconds(preCastTime);
                        castChain.AddAction(creature, () =>
                        {
                            creature.TryCastSpell_WithRedirects(spell, spellTarget, creature);
                            creature.PostCastMotion();
                        });
                        castChain.EnqueueChain();
                    }
                    break;

                case EmoteType.CastSpellInstant:

                    if (WorldObject != null)
                    {
                        var spell = new Spell((uint)emote.SpellId);

                        if (!spell.NotFound)
                        {
                            var spellTarget = GetSpellTarget(spell, targetObject);

                            WorldObject.TryCastSpell_WithRedirects(spell, spellTarget, WorldObject);
                        }
                    }
                    break;

                case EmoteType.CloseMe:

                    // animation delay?
                    if (WorldObject is Container container)
                        container.Close(null);
                    else if (WorldObject is Door closeDoor)
                        closeDoor.Close();

                    break;

                case EmoteType.CreateTreasure:

                    if (player != null)
                    {
                        var treasureTier = emote.WealthRating ?? 1;

                        var treasureType = (TreasureItemCategory?)emote.TreasureType ?? TreasureItemCategory.Undef;

                        var treasureClass = (TreasureItemType_Orig?)emote.TreasureClass ?? TreasureItemType_Orig.Undef;

                        // Create a dummy treasure profile for passing emote values
                        var profile = new Database.Models.World.TreasureDeath
                        {
                            Tier = treasureTier,
                            //TreasureType = (uint)treasureType,
                            LootQualityMod = 0,
                            ItemChance = 100,
                            ItemMinAmount = 1,
                            ItemMaxAmount = 1,
                            //ItemTreasureTypeSelectionChances = (int)treasureClass,
                            MagicItemChance = 100,
                            MagicItemMinAmount = 1,
                            MagicItemMaxAmount = 1,
                            //MagicItemTreasureTypeSelectionChances = (int)treasureClass,
                            MundaneItemChance = 100,
                            MundaneItemMinAmount = 1,
                            MundaneItemMaxAmount = 1,
                            //MundaneItemTypeSelectionChances = (int)treasureClass,
                            UnknownChances = 21
                        };

                        var treasure = LootGenerationFactory.CreateRandomLootObjects_New(profile, treasureType, treasureClass);
                        if (treasure != null)
                        {
                            player.TryCreateForGive(WorldObject, treasure);
                        }
                    }
                    break;

                /* decrements a PropertyInt stat by some amount */
                case EmoteType.DecrementIntStat:

                    // only used by 1 emote in 16PY - check for lower bounds?
                    if (targetObject != null && emote.Stat != null)
                    {
                        var intProperty = (PropertyInt)emote.Stat;
                        var current = targetObject.GetProperty(intProperty) ?? 0;
                        current -= emote.Amount ?? 1;
                        targetObject.SetProperty(intProperty, current);

                        if (player != null)
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, intProperty, current));
                    }
                    break;

                /* decrements a PropertyInt64 stat by some amount */
                case EmoteType.DecrementInt64Stat:

                    // only used by 1 emote in 16PY - check for lower bounds?
                    if (targetObject != null && emote.Stat != null)
                    {
                        var int64Property = (PropertyInt64)emote.Stat;
                        var current = targetObject.GetProperty(int64Property) ?? 0;
                        current -= emote.Amount64 ?? 1;
                        targetObject.SetProperty(int64Property, current);

                        if (player != null)
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, int64Property, current));
                    }
                    break;

                case EmoteType.DecrementMyQuest:
                case EmoteType.DecrementQuest:

                    var questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                        questTarget.QuestManager.Decrement(emote.Message, emote.Amount ?? 1);

                    break;

                case EmoteType.DeleteSelf:

                    if (player != null)
                    {
                        var wo = player.FindObject(WorldObject.Guid.Full, Player.SearchLocations.Everywhere, out _, out Container rootOwner, out bool wasEquipped);

                        WorldObject.DeleteObject(rootOwner);
                    }
                    else
                        WorldObject.DeleteObject();

                    break;

                case EmoteType.DirectBroadcast:

                    text = Replace(emote.Message, WorldObject, targetObject, emoteSet.Quest);

                    if (player != null)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));

                    break;

                case EmoteType.Enlightenment:

                    if (player != null)
                    {
                        Enlightenment.HandleEnlightenment(player);
                    }

                    break;

                case EmoteType.EraseMyQuest:
                case EmoteType.EraseQuest:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                        questTarget.QuestManager.Erase(emote.Message);

                    break;

                case EmoteType.FellowBroadcast:

                    if (player != null)
                    {
                        var fellowship = player.Fellowship;

                        if (fellowship != null)
                        {
                            text = Replace(emote.Message, WorldObject, player, emoteSet.Quest);

                            fellowship.BroadcastToFellow(text);
                        }
                    }
                    break;

                case EmoteType.Generate:

                    if (WorldObject.IsGenerator)
                        WorldObject.Generator_Generate();
                    break;

                case EmoteType.Give:

                    bool success = false;

                    var stackSize = emote.StackSize ?? 1;

                    if (player != null && emote.WeenieClassId != null)
                    {
                        var motionChain = new ActionChain();

                        if (!WorldObject.DontTurnOrMoveWhenGiving && creature != null && targetCreature != null)
                        {
                            delay = creature.Rotate(targetCreature);
                            motionChain.AddDelaySeconds(delay);
                        }
                        motionChain.AddAction(WorldObject, () => player.GiveFromEmote(WorldObject, emote.WeenieClassId ?? 0, stackSize > 0 ? stackSize : 1, emote.Palette ?? 0, emote.Shade ?? 0));
                        motionChain.EnqueueChain();
                    }

                    break;

                /* redirects to the GotoSet category for this action */
                case EmoteType.Goto:

                    // TODO: revisit if nested chains need to back-propagate timers
                    var gotoSet = GetEmoteSet(EmoteCategory.GotoSet, emote.Message);
                    ExecuteEmoteSet(gotoSet, targetObject, true);
                    break;

                /* increments a PropertyInt stat by some amount */
                case EmoteType.IncrementIntStat:

                    if (targetObject != null && emote.Stat != null)
                    {
                        var intProperty = (PropertyInt)emote.Stat;
                        var current = targetObject.GetProperty(intProperty) ?? 0;
                        current += emote.Amount ?? 1;
                        targetObject.SetProperty(intProperty, current);

                        if (player != null)
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, intProperty, current));
                    }
                    break;

                /* increments a PropertyInt64 stat by some amount */
                case EmoteType.IncrementInt64Stat:

                    if (targetObject != null && emote.Stat != null)
                    {
                        var int64Property = (PropertyInt64)emote.Stat;
                        var current = targetObject.GetProperty(int64Property) ?? 0;
                        current += emote.Amount64 ?? 1;
                        targetObject.SetProperty(int64Property, current);

                        if (player != null)
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, int64Property, current));
                    }
                    break;

                case EmoteType.IncrementMyQuest:
                case EmoteType.IncrementQuest:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                        questTarget.QuestManager.Increment(emote.Message, emote.Amount ?? 1);

                    break;

                case EmoteType.InflictVitaePenalty:
                    if (player != null)
                        player.InflictVitaePenalty(emote.Amount ?? 5);
                    break;

                case EmoteType.InqAttributeStat:

                    if (targetCreature != null)
                    {
                        var attr = targetCreature.Attributes[(PropertyAttribute)emote.Stat];

                        if (attr == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = attr != null && attr.Current >= (emote.Min ?? int.MinValue) && attr.Current <= (emote.Max ?? int.MaxValue);

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqBoolStat:

                    if (targetObject != null)
                    {
                        var stat = targetObject.GetProperty((PropertyBool)emote.Stat);

                        if (stat == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = stat ?? false;

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqContractsFull:

                    ExecuteEmoteSet(player != null && player.ContractManager.IsFull ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                    break;

                case EmoteType.InqEvent:

                    var started = EventManager.IsEventStarted(emote.Message, WorldObject, targetObject);
                    ExecuteEmoteSet(started ? EmoteCategory.EventSuccess : EmoteCategory.EventFailure, emote.Message, targetObject, true);
                    break;

                case EmoteType.InqFellowNum:

                    // unused in PY16 - ensure # of fellows between min-max?
                    var result = EmoteCategory.TestNoFellow;

                    if (player?.Fellowship != null)
                    {
                        var fellows = player.Fellowship.GetFellowshipMembers();

                        if (fellows.Count < (emote.Min ?? int.MinValue) || fellows.Count > (emote.Max ?? int.MaxValue))
                            result = EmoteCategory.NumFellowsFailure;
                        else
                            result = EmoteCategory.NumFellowsSuccess;
                    }
                    ExecuteEmoteSet(result, emote.Message, targetObject, true);
                    break;

                case EmoteType.InqFellowQuest:

                    if (player != null)
                    {
                        if (player.Fellowship != null)
                        {
                            var hasQuest = player.Fellowship.QuestManager.HasQuest(emote.Message);
                            var canSolve = player.Fellowship.QuestManager.CanSolve(emote.Message);

                            // verify: QuestSuccess = player has quest, and their last completed time + quest minDelta <= currentTime
                            success = hasQuest && !canSolve;

                            ExecuteEmoteSet(success ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                        }
                        else
                            ExecuteEmoteSet(EmoteCategory.QuestNoFellow, emote.Message, targetObject, true);
                    }
                    break;

                case EmoteType.InqFloatStat:

                    if (targetObject != null)
                    {
                        var stat = targetObject.GetProperty((PropertyFloat)emote.Stat);

                        if (stat == null && HasValidTestNoQuality(emote.Message))
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        else
                        {
                            stat ??= 0.0f;
                            success = stat >= (emote.MinDbl ?? double.MinValue) && stat <= (emote.MaxDbl ?? double.MaxValue);
                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqInt64Stat:

                    if (targetObject != null)
                    {
                        var stat = targetObject.GetProperty((PropertyInt64)emote.Stat);

                        if (stat == null && HasValidTestNoQuality(emote.Message))
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        else
                        {
                            stat ??= 0;
                            if (emote.Stat == 6) { stat += player.BankedLuminance; } //DB magic number for available luminance
                            success = stat >= (emote.Min64 ?? long.MinValue) && stat <= (emote.Max64 ?? long.MaxValue);
                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqIntStat:

                    if (targetObject != null)
                    {
                        var stat = targetObject.GetProperty((PropertyInt)emote.Stat);

                        if (stat == null && HasValidTestNoQuality(emote.Message))
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        else
                        {
                            stat ??= 0;
                            if (emote.Max == 999) //DB magic number
                            {
                                emote.Max = int.MaxValue;
                            }
                            success = stat >= (emote.Min ?? int.MinValue) && stat <= (emote.Max ?? int.MaxValue);
                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqNumCharacterTitles:

                    //if (player != null)
                    //InqCategory(player.NumCharacterTitles != 0 ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote);
                    break;

                case EmoteType.InqOwnsItems:

                    if (player != null)
					{
                        var numRequired = emote.StackSize ?? 1;

                        var items = player.GetInventoryItemsOfWCID(emote.WeenieClassId ?? 0);
                        items.AddRange(player.GetEquippedObjectsOfWCID(emote.WeenieClassId ?? 0));
                        var numItems = items.Sum(i => i.StackSize ?? 1);

                        success = numItems >= numRequired;

                        ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                    }
                    break;

                case EmoteType.InqPackSpace:

                    if (player != null)
                    {
                        var numRequired = emote.Amount ?? 1;

                        success = false;
                        if (numRequired > 10000) // Since emote was not in 16py and we have just the two fields to go on, I will assume you could "mask" the value to pick between free Item Capacity space or free Container Capacity space
                        {
                            var freeSpace = player.GetFreeContainerSlots();

                            success = freeSpace >= (numRequired - 10000);
                        }
                        else
                        {
                            var freeSpace = player.GetFreeInventorySlots(false); // assuming this was only for main pack. makes things easier at this point.

                            success = freeSpace >= numRequired;
                        }

                        ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                    }
                    break;

                case EmoteType.InqMyQuest:
                case EmoteType.InqQuest:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                    {
                        var hasQuest = questTarget.QuestManager.HasQuest(emote.Message);
                        var canSolve = questTarget.QuestManager.CanSolve(emote.Message);

                        //  verify: QuestSuccess = player has quest, but their quest timer is currently still on cooldown
                        success = hasQuest && !canSolve;

                        ExecuteEmoteSet(success ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                    }

                    break;

                case EmoteType.InqMyQuestBitsOff:
                case EmoteType.InqQuestBitsOff:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                    {
                        var hasNoQuestBits = questTarget.QuestManager.HasNoQuestBits(emote.Message, emote.Amount ?? 0);

                        ExecuteEmoteSet(hasNoQuestBits ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                    }

                    break;

                case EmoteType.InqMyQuestBitsOn:
                case EmoteType.InqQuestBitsOn:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                    {
                        var hasQuestBits = questTarget.QuestManager.HasQuestBits(emote.Message, emote.Amount ?? 0);

                        ExecuteEmoteSet(hasQuestBits ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                    }

                    break;

                case EmoteType.InqMyQuestSolves:
                case EmoteType.InqQuestSolves:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                    {
                        var questSolves = questTarget.QuestManager.HasQuestSolves(emote.Message, emote.Min, emote.Max);

                        ExecuteEmoteSet(questSolves ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                    }
                    break;

                case EmoteType.InqRawAttributeStat:

                    if (targetCreature != null)
                    {
                        var attr = targetCreature.Attributes[(PropertyAttribute)emote.Stat];

                        if (attr == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = attr != null && attr.Base >= (emote.Min ?? int.MinValue) && attr.Base <= (emote.Max ?? int.MaxValue);

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqRawSecondaryAttributeStat:

                    if (targetCreature != null)
                    {
                        var vital = targetCreature.Vitals[(PropertyAttribute2nd)emote.Stat];

                        if (vital == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = vital != null && vital.Base >= (emote.Min ?? int.MinValue) && vital.Base <= (emote.Max ?? int.MaxValue);

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqRawSkillStat:

                    if (targetCreature != null)
                    {
                        var skill = targetCreature.GetCreatureSkill((Skill)emote.Stat);

                        if (skill == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = skill != null && skill.Base >= (emote.Min ?? int.MinValue) && skill.Base <= (emote.Max ?? int.MaxValue);

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqSecondaryAttributeStat:

                    if (targetCreature != null)
                    {
                        var vital = targetCreature.Vitals[(PropertyAttribute2nd)emote.Stat];

                        if (vital == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = vital != null && vital.Current >= (emote.Min ?? int.MinValue) && vital.Current <= (emote.Max ?? int.MaxValue);

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqSkillSpecialized:

                    if (targetCreature != null)
                    {
                        var skill = targetCreature.GetCreatureSkill((Skill)emote.Stat);

                        if (skill == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = skill != null && skill.AdvancementClass == SkillAdvancementClass.Specialized;

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqSkillStat:

                    if (targetCreature != null)
                    {
                        var skill = targetCreature.GetCreatureSkill((Skill)emote.Stat);

                        if (skill == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = skill != null && skill.Current >= (emote.Min ?? int.MinValue) && skill.Current <= (emote.Max ?? int.MaxValue);

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqSkillTrained:

                    if (targetCreature != null)
                    {
                        var skill = targetCreature.GetCreatureSkill((Skill)emote.Stat);

                        if (skill == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            success = skill != null && skill.AdvancementClass >= SkillAdvancementClass.Trained;

                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqStringStat:

                    if (targetObject != null)
                    {
                        var stringStat = targetObject.GetProperty((PropertyString)emote.Stat);

                        if (stringStat == null && HasValidTestNoQuality(emote.Message))
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        else
                        {
                            success = stringStat != null && stringStat.Equals(emote.TestString);
                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
                    }
                    break;

                case EmoteType.InqYesNo:

                    if (player != null)
                    {
                        if (!player.ConfirmationManager.EnqueueSend(new Confirmation_YesNo(WorldObject.Guid, player.Guid, emote.Message), Replace(emote.TestString, WorldObject, targetObject, emoteSet.Quest)))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestFailure, emote.Message, player);
                        }
                    }
                    break;

                case EmoteType.Invalid:
                    break;

                case EmoteType.KillSelf:

                    if (creature != null)
                        creature.Smite(creature);
                    break;

                case EmoteType.LocalBroadcast:

                    message = Replace(emote.Message, WorldObject, targetObject, emoteSet.Quest);
                    WorldObject.EnqueueBroadcast(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
                    break;

                case EmoteType.LocalSignal:

                    if (WorldObject != null)
                    {
                        if (WorldObject.CurrentLandblock != null)
                            WorldObject.CurrentLandblock.EmitSignal(WorldObject, emote.Message);
                    }
                    break;

                case EmoteType.LockFellow:

                    if (player != null && player.Fellowship != null)
                        player.HandleActionFellowshipChangeLock(true, emoteSet.Quest);

                    break;

                /* plays an animation on the target object (usually the player) */
                case EmoteType.ForceMotion:

                    var motionCommand = MotionCommandHelper.GetMotion(emote.Motion.Value);
                    var motion = new Motion(targetObject, motionCommand, emote.Extent);
                    targetObject.EnqueueBroadcastMotion(motion);
                    break;

                /* plays an animation on the source object */
                case EmoteType.Motion:

                    var debugMotion = false;

                    if (emote.Motion == null)
                        break;

                    if (Debug)
                        Console.Write($".{(MotionCommand)emote.Motion}");

                    // If the landblock is dormant, there are no players in range
                    if (WorldObject.CurrentLandblock?.IsDormant ?? false)
                        break;

                    // are there players within emote range?
                    if (!WorldObject.PlayersInRange(ClientMaxAnimRange))
                        break;

                    if (WorldObject.PhysicsObj != null && WorldObject.PhysicsObj.IsMovingTo())
                        break;

                    if (WorldObject == null || WorldObject.CurrentMotionState == null) break;

                    // TODO: REFACTOR ME
                    if (emoteSet.Category != EmoteCategory.Vendor && emoteSet.Style != null)
                    {
                        var startingMotion = new Motion((MotionStance)emoteSet.Style, (MotionCommand)emoteSet.Substyle);
                        motion = new Motion((MotionStance)emoteSet.Style, (MotionCommand)emote.Motion, emote.Extent);

                        if (WorldObject.CurrentMotionState.Stance != startingMotion.Stance)
                        {
                            if (WorldObject.CurrentMotionState.Stance == MotionStance.Invalid)
                            {
                                if (debugMotion)
                                    Console.WriteLine($"{WorldObject.Name} running starting motion {(MotionStance)emoteSet.Style}, {(MotionCommand)emoteSet.Substyle}");

                                delay = WorldObject.ExecuteMotion(startingMotion);
                            }
                        }
                        else
                        {
                            if (WorldObject.CurrentMotionState.MotionState.ForwardCommand == startingMotion.MotionState.ForwardCommand
                                    && startingMotion.Stance == MotionStance.NonCombat)     // enforce non-combat here?
                            {
                                if (debugMotion)
                                    Console.WriteLine($"{WorldObject.Name} running motion {(MotionStance)emoteSet.Style}, {(MotionCommand)emote.Motion}");

                                float? maxRange = ClientMaxAnimRange;
                                if (MotionQueue.Contains((MotionCommand)emote.Motion))
                                    maxRange = null;

                                var motionTable = DatManager.PortalDat.ReadFromDat<DatLoader.FileTypes.MotionTable>(WorldObject.MotionTableId);
                                var animLength = motionTable.GetAnimationLength(WorldObject.CurrentMotionState.Stance, (MotionCommand)emote.Motion, MotionCommand.Ready);

                                delay = WorldObject.ExecuteMotion(motion, true, maxRange);

                                var motionChain = new ActionChain();
                                motionChain.AddDelaySeconds(animLength);
                                motionChain.AddAction(WorldObject, () =>
                                {
                                    // FIXME: better cycle handling
                                    var cmd = WorldObject.CurrentMotionState.MotionState.ForwardCommand;
                                    if (cmd != MotionCommand.Dead && cmd != MotionCommand.Sleeping && cmd != MotionCommand.Sitting && !cmd.ToString().EndsWith("State"))
                                    {
                                        if (debugMotion)
                                            Console.WriteLine($"{WorldObject.Name} running starting motion again {(MotionStance)emoteSet.Style}, {(MotionCommand)emoteSet.Substyle}");

                                        WorldObject.ExecuteMotion(startingMotion);
                                    }
                                });
                                motionChain.EnqueueChain();

                                if (debugMotion)
                                    Console.WriteLine($"{WorldObject.Name} appending time to existing chain: " + animLength);
                            }
                        }
                    }
                    else
                    {
                        // vendor / other motions
                        var startingMotion = new Motion(MotionStance.NonCombat, MotionCommand.Ready);
                        var motionTable = DatManager.PortalDat.ReadFromDat<DatLoader.FileTypes.MotionTable>(WorldObject.MotionTableId);
                        var animLength = motionTable.GetAnimationLength(WorldObject.CurrentMotionState.Stance, (MotionCommand)emote.Motion, MotionCommand.Ready);

                        motion = new Motion(MotionStance.NonCombat, (MotionCommand)emote.Motion, emote.Extent);

                        if (debugMotion)
                            Console.WriteLine($"{WorldObject.Name} running motion (block 2) {MotionStance.NonCombat}, {(MotionCommand)(emote.Motion ?? 0)}");

                        delay = WorldObject.ExecuteMotion(motion);

                        var motionChain = new ActionChain();
                        motionChain.AddDelaySeconds(animLength);
                        motionChain.AddAction(WorldObject, () => WorldObject.ExecuteMotion(startingMotion, false));

                        motionChain.EnqueueChain();
                    }

                    break;

                /* move to position relative to home */
                case EmoteType.Move:

                    if (creature != null)
                    {
                        // If the landblock is dormant, there are no players in range
                        if (WorldObject.CurrentLandblock?.IsDormant ?? false)
                            break;

                        // are there players within emote range?
                        if (!WorldObject.PlayersInRange(ClientMaxAnimRange))
                            break;

                        var newPos = new Position(creature.Home);
                        newPos.Pos += new Vector3(emote.OriginX ?? 0, emote.OriginY ?? 0, emote.OriginZ ?? 0);      // uses relative position

                        // ensure valid quaternion - all 0s for example can lock up physics engine
                        if (emote.AnglesX != null && emote.AnglesY != null && emote.AnglesZ != null && emote.AnglesW != null &&
                           (emote.AnglesX != 0    || emote.AnglesY != 0    || emote.AnglesZ != 0    || emote.AnglesW != 0) )
                        {
                            // also relative, or absolute?
                            newPos.Rotation *= new Quaternion(emote.AnglesX.Value, emote.AnglesY.Value, emote.AnglesZ.Value, emote.AnglesW.Value);  
                        }

                        if (Debug)
                            Console.WriteLine(newPos.ToLOCString());

                        // get new cell
                        newPos.LandblockId = new LandblockId(PositionExtensions.GetCell(newPos));

                        // TODO: handle delay for this?
                        creature.MoveTo(newPos, creature.GetRunRate(), true, null, emote.Extent);
                    }
                    break;

                case EmoteType.MoveHome:

                    // TODO: call MoveToManager on server, handle delay for this?
                    if (creature != null && creature.Home != null)
                    {
                        // are we already at home origin?
                        if (creature.Location.Pos.Equals(creature.Home.Pos))
                        {
                            // just turnto if required?
                            if (Debug)
                                Console.Write($" - already at home origin, checking rotation");

                            if (!creature.Location.Rotation.Equals(creature.Home.Rotation))
                            {
                                if (Debug)
                                    Console.Write($" - turning to");
                                delay = creature.TurnTo(creature.Home);
                            }
                            else if (Debug)
                                Console.Write($" - already at home rotation, doing nothing");
                        }
                        else
                        {
                            if (Debug)
                                Console.Write($" - {creature.Home.ToLOCString()}");

                            // how to get delay with this, callback required?
                            creature.MoveTo(creature.Home, creature.GetRunRate(), true, null, emote.Extent);
                        }
                    }
                    break;

                case EmoteType.MoveToPos:

                    if (creature != null)
                    {
                        var currentPos = creature.Location;

                        var newPos = new Position();
                        newPos.LandblockId = new LandblockId(emote.ObjCellId ?? currentPos.LandblockId.Raw);

                        newPos.Pos = new Vector3(emote.OriginX ?? currentPos.Pos.X, emote.OriginY ?? currentPos.Pos.Y, emote.OriginZ ?? currentPos.Pos.Z);

                        if (emote.AnglesX == null || emote.AnglesY == null || emote.AnglesZ == null || emote.AnglesW == null)
                            newPos.Rotation = new Quaternion(currentPos.Rotation.X, currentPos.Rotation.Y, currentPos.Rotation.Z, currentPos.Rotation.W);
                        else
                            newPos.Rotation = new Quaternion(emote.AnglesX ?? 0, emote.AnglesY ?? 0, emote.AnglesZ ?? 0, emote.AnglesW ?? 1);

                        //if (emote.ObjCellId != null)
                        //newPos.LandblockId = new LandblockId(emote.ObjCellId.Value);

                        newPos.LandblockId = new LandblockId(PositionExtensions.GetCell(newPos));

                        // TODO: handle delay for this?
                        creature.MoveTo(newPos, creature.GetRunRate(), true, null, emote.Extent);
                    }
                    break;

                case EmoteType.OpenMe:

                    if (WorldObject is Container openContainer)
                        openContainer.Open(null);
                    else if (WorldObject is Door openDoor)
                        openDoor.Open();

                    break;

                case EmoteType.PetCastSpellOnOwner:

                    if (creature is Pet passivePet && passivePet.P_PetOwner != null)
                    {
                        var spell = new Spell((uint)emote.SpellId);
                        passivePet.TryCastSpell(spell, passivePet.P_PetOwner);
                    }
                    break;

                case EmoteType.PhysScript:

                    WorldObject.PlayParticleEffect((PlayScript)emote.PScript, WorldObject.Guid, emote.Extent);
                    break;

                case EmoteType.PopUp:

                    if (player != null)
                        player.Session.Network.EnqueueSend(new GameEventPopupString(player.Session, emote.Message));
                    break;

                case EmoteType.RemoveContract:

                    if (player != null && emote.Stat.HasValue && emote.Stat.Value > 0)
                        player.HandleActionAbandonContract((uint)emote.Stat);
                    break;

                case EmoteType.RemoveVitaePenalty:

                    if (player != null)
                        player.EnchantmentManager.RemoveVitae();

                    break;

                case EmoteType.ResetHomePosition:

                    if (WorldObject.Location != null)
                        WorldObject.Home = new Position(WorldObject.Location);
                    break;

                case EmoteType.Say:

                    if (Debug)
                        Console.Write($" - {emote.Message}");

                    message = Replace(emote.Message, WorldObject, targetObject, emoteSet.Quest);

                    var name = WorldObject.CreatureType == CreatureType.Olthoi ? WorldObject.Name + "&" : WorldObject.Name;

                    if (emote.Extent > 0)
                        WorldObject.EnqueueBroadcast(new GameMessageHearRangedSpeech(message, name, WorldObject.Guid.Full, emote.Extent, ChatMessageType.Emote), WorldObject.LocalBroadcastRange);
                    else
                        WorldObject.EnqueueBroadcast(new GameMessageHearSpeech(message, name, WorldObject.Guid.Full, ChatMessageType.Emote), WorldObject.LocalBroadcastRange);
                    break;

                case EmoteType.SetAltRacialSkills:
                    break;

                case EmoteType.SetBoolStat:

                    if (player != null)
                    {
                        player.UpdateProperty(player, (PropertyBool)emote.Stat, emote.Amount == 0 ? false : true);
                        player.EnqueueBroadcast(false, new GameMessagePublicUpdatePropertyBool(player, (PropertyBool)emote.Stat, emote.Amount == 0 ? false : true));
                    }
                    break;

                case EmoteType.SetEyePalette:
                    //if (creature != null)
                    //    creature.EyesPaletteDID = (uint)emote.Display;
                    break;

                case EmoteType.SetEyeTexture:
                    //if (creature != null)
                    //    creature.EyesTextureDID = (uint)emote.Display;
                    break;

                case EmoteType.SetFloatStat:

                    if (player != null)
                    {
                        player.UpdateProperty(player, (PropertyFloat)emote.Stat, emote.Percent);
                        player.EnqueueBroadcast(false, new GameMessagePublicUpdatePropertyFloat(player, (PropertyFloat)emote.Stat, Convert.ToDouble(emote.Percent)));
                    }
                    break;

                case EmoteType.SetHeadObject:
                    //if (creature != null)
                    //    creature.HeadObjectDID = (uint)emote.Display;
                    break;

                case EmoteType.SetHeadPalette:
                    break;

                case EmoteType.SetInt64Stat:

                    if (player != null)
                    {
                        player.UpdateProperty(player, (PropertyInt64)emote.Stat, emote.Amount64);
                        player.EnqueueBroadcast(false, new GameMessagePublicUpdatePropertyInt64(player, (PropertyInt64)emote.Stat, Convert.ToInt64(emote.Amount64)));
                    }
                    break;

                case EmoteType.SetIntStat:

                    if (player != null)
                    {
                        player.UpdateProperty(player, (PropertyInt)emote.Stat, emote.Amount);
                        player.EnqueueBroadcast(false, new GameMessagePublicUpdatePropertyInt(player, (PropertyInt)emote.Stat, Convert.ToInt32(emote.Amount)));
                    }
                    break;

                case EmoteType.SetMouthPalette:
                    break;

                case EmoteType.SetMouthTexture:
                    //if (creature != null)
                    //    creature.MouthTextureDID = (uint)emote.Display;
                    break;

                case EmoteType.SetNosePalette:
                    break;

                case EmoteType.SetNoseTexture:
                    //if (creature != null)
                    //    creature.NoseTextureDID = (uint)emote.Display;
                    break;

                case EmoteType.SetMyQuestBitsOff:
                case EmoteType.SetQuestBitsOff:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null && emote.Message != null && emote.Amount != null)
                        questTarget.QuestManager.SetQuestBits(emote.Message, (int)emote.Amount, false);

                    break;

                case EmoteType.SetMyQuestBitsOn:
                case EmoteType.SetQuestBitsOn:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null && emote.Message != null && emote.Amount != null)
                        questTarget.QuestManager.SetQuestBits(emote.Message, (int)emote.Amount);

                    break;

                case EmoteType.SetMyQuestCompletions:
                case EmoteType.SetQuestCompletions:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null && emote.Amount != null)
                        questTarget.QuestManager.SetQuestCompletions(emote.Message, (int)emote.Amount);

                    break;

                case EmoteType.SetSanctuaryPosition:

                    if (player != null)
                        player.SetPosition(PositionType.Sanctuary, new Position(emote.ObjCellId.Value, emote.OriginX.Value, emote.OriginY.Value, emote.OriginZ.Value, emote.AnglesX.Value, emote.AnglesY.Value, emote.AnglesZ.Value, emote.AnglesW.Value));
                    break;

                case EmoteType.Sound:

                    WorldObject.EnqueueBroadcast(new GameMessageSound(WorldObject.Guid, (Sound)emote.Sound, 1.0f));
                    break;

                case EmoteType.SpendLuminance:

                    if (player != null)
                        if(!player.SpendLuminance(emote.Amount64 ?? emote.HeroXP64 ?? 0))
                        {
                            WorldObject.EnqueueBroadcast(new GameMessageHearSpeech("You do not have enough luminance to do that.", "System", WorldObject.Guid.Full, ChatMessageType.System));
                        }
                    break;

                case EmoteType.StampFellowQuest:

                    if (player != null)
                    {
                        if (player.Fellowship != null)
                        {
                            var questName = emote.Message;

                            player.Fellowship.QuestManager.Stamp(emote.Message);
                        }
                    }
                    break;

                case EmoteType.StampMyQuest:
                case EmoteType.StampQuest:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                    {
                        var questName = emote.Message;

                        if (questName.EndsWith("@#kt", StringComparison.Ordinal))
                            log.Warn($"0x{WorldObject.Guid}:{WorldObject.Name} ({WorldObject.WeenieClassId}).EmoteManager.ExecuteEmote: EmoteType.StampQuest({questName}) is a depreciated kill task method.");


                        questTarget.QuestManager.Stamp(emote.Message);
                    }
                    break;

                case EmoteType.StartBarber:

                    if (player != null)
                        player.StartBarber();
                    break;

                case EmoteType.StartEvent:

                    EventManager.StartEvent(emote.Message, WorldObject, targetObject);
                    break;

                case EmoteType.StopEvent:

                    EventManager.StopEvent(emote.Message, WorldObject, targetObject);
                    break;

                case EmoteType.TakeItems:

                    if (player != null)
                    {
                        var weenieItemToTake = emote.WeenieClassId ?? 0;
                        var amountToTake = emote.StackSize ?? 1;

                        if (weenieItemToTake == 0)
                        {
                            log.Warn($"EmoteManager.Execute: 0x{WorldObject.Guid} {WorldObject.Name} ({WorldObject.WeenieClassId}) EmoteType.TakeItems has invalid emote.WeenieClassId: {weenieItemToTake}");
                            break;
                        }

                        if (amountToTake < -1 || amountToTake == 0)
                        {
                            log.Warn($"EmoteManager.Execute: 0x{WorldObject.Guid} {WorldObject.Name} ({WorldObject.WeenieClassId}) EmoteType.TakeItems has invalid emote.StackSize: {amountToTake}");
                            break;
                        }

                        if ((player.GetNumInventoryItemsOfWCID(weenieItemToTake) > 0 && player.TryConsumeFromInventoryWithNetworking(weenieItemToTake, amountToTake == -1 ? int.MaxValue : amountToTake))
                            || (player.GetNumEquippedObjectsOfWCID(weenieItemToTake) > 0 && player.TryConsumeFromEquippedObjectsWithNetworking(weenieItemToTake, amountToTake == -1 ? int.MaxValue : amountToTake)))
                        {
                            var itemTaken = DatabaseManager.World.GetCachedWeenie(weenieItemToTake);
                            if (itemTaken != null)
                            {
                                var amount = amountToTake == -1 ? "all" : amountToTake.ToString();

                                var msg = $"You hand over {amount} of your {itemTaken.GetPluralName()}.";

                                player.Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
                            }
                        }
                    }
                    break;

                case EmoteType.TeachSpell:

                    if (player != null)
                        player.LearnSpellWithNetworking((uint)emote.SpellId, false);
                    break;

                case EmoteType.TeleportSelf:

                    //if (WorldObject is Player)
                    //(WorldObject as Player).Teleport(emote.Position);
                    break;

                case EmoteType.TeleportTarget:

                    if (player != null)
                    {
                        if (emote.ObjCellId.HasValue && emote.OriginX.HasValue && emote.OriginY.HasValue && emote.OriginZ.HasValue && emote.AnglesX.HasValue && emote.AnglesY.HasValue && emote.AnglesZ.HasValue && emote.AnglesW.HasValue)
                        {
                            if (emote.ObjCellId.Value > 0)
                            {
                                var variation = emote.Stat;
                                var destination = new Position(emote.ObjCellId.Value, emote.OriginX.Value, emote.OriginY.Value, emote.OriginZ.Value, emote.AnglesX.Value, emote.AnglesY.Value, emote.AnglesZ.Value, emote.AnglesW.Value, false, variation);

                                WorldObject.AdjustDungeon(destination);
                                WorldManager.ThreadSafeTeleport(player, destination);
                            }
                            else // position is relative to WorldObject's current location
                            {
                                var relativeDestination = new Position(WorldObject.Location);
                                relativeDestination.Pos += new Vector3(emote.OriginX.Value, emote.OriginY.Value, emote.OriginZ.Value);
                                relativeDestination.Rotation = new Quaternion(emote.AnglesX.Value, emote.AnglesY.Value, emote.AnglesZ.Value, emote.AnglesW.Value);
                                relativeDestination.LandblockId = new LandblockId(relativeDestination.GetCell());

                                WorldObject.AdjustDungeon(relativeDestination);
                                WorldManager.ThreadSafeTeleport(player, relativeDestination);
                            }
                        }
                    }
                    break;

                case EmoteType.Tell:

                    if (player != null)
                    {
                        message = Replace(emote.Message, WorldObject, player, emoteSet.Quest);
                        player.Session.Network.EnqueueSend(new GameEventTell(WorldObject, message, player, ChatMessageType.Tell));
                    }
                    break;

                case EmoteType.TellFellow:

                    if (player != null)
                    {
                        var fellowship = player.Fellowship;
                        if (fellowship != null)
                        {
                            text = Replace(emote.Message, WorldObject, player, emoteSet.Quest);

                            fellowship.TellFellow(WorldObject, text);
                        }
                    }
                    break;

                case EmoteType.TextDirect:

                    if (player != null)
                    {
                        message = Replace(emote.Message, WorldObject, player, emoteSet.Quest);
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
                    }
                    break;

                case EmoteType.Turn:

                    if (creature != null)
                    {
                        // turn to heading
                        var rotation = new Quaternion(emote.AnglesX ?? 0, emote.AnglesY ?? 0, emote.AnglesZ ?? 0, emote.AnglesW ?? 1);
                        var newPos = new Position(creature.Location);
                        newPos.Rotation = rotation;

                        var rotateTime = creature.TurnTo(newPos);
                        delay = rotateTime;
                    }
                    break;

                case EmoteType.TurnToTarget:

                    if (creature != null && targetCreature != null)
                        delay = creature.Rotate(targetCreature);

                    break;

                case EmoteType.UntrainSkill:

                    if (player != null)
                        player.ResetSkill((Skill)emote.Stat);
                    break;

                case EmoteType.UpdateFellowQuest:

                    if (player != null)
                    {
                        if (player.Fellowship != null)
                        {
                            var questName = emote.Message;

                            var hasQuest = player.Fellowship.QuestManager.HasQuest(questName);

                            if (!hasQuest)
                            {
                                // add new quest
                                player.Fellowship.QuestManager.Update(questName);
                                hasQuest = player.Fellowship.QuestManager.HasQuest(questName);
                                ExecuteEmoteSet(hasQuest ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                            }
                            else
                            {
                                // update existing quest
                                var canSolve = player.Fellowship.QuestManager.CanSolve(questName);
                                if (canSolve)
                                    player.Fellowship.QuestManager.Stamp(questName);
                                ExecuteEmoteSet(canSolve ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                            }
                        }
                        else
                            ExecuteEmoteSet(EmoteCategory.QuestNoFellow, emote.Message, targetObject, true);
                    }
                    break;

                case EmoteType.UpdateMyQuest:
                case EmoteType.UpdateQuest:

                    questTarget = GetQuestTarget((EmoteType)emote.Type, targetCreature, creature);

                    if (questTarget != null)
                    {
                        var questName = emote.Message;

                        var hasQuest = questTarget.QuestManager.HasQuest(questName);

                        if (!hasQuest)
                        {
                            // add new quest
                            questTarget.QuestManager.Update(questName);
                            hasQuest = questTarget.QuestManager.HasQuest(questName);
                            ExecuteEmoteSet(hasQuest ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                        }
                        else
                        {
                            // update existing quest
                            var canSolve = questTarget.QuestManager.CanSolve(questName);
                            if (canSolve)
                                questTarget.QuestManager.Stamp(questName);
                            ExecuteEmoteSet(canSolve ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, emote.Message, targetObject, true);
                        }
                    }

                    break;

                case EmoteType.WorldBroadcast:

                    message = Replace(text, WorldObject, targetObject, emoteSet.Quest);

                    PlayerManager.BroadcastToAll(new GameMessageSystemChat(message, ChatMessageType.WorldBroadcast));

                    DiscordChatManager.SendDiscordMessage("BROADCAST", message, ConfigManager.Config.Chat.GeneralChannelId);

                    PlayerManager.LogBroadcastChat(Channel.AllBroadcast, WorldObject, message);

                    break;
                case EmoteType.StartDynamicQuest:
                    if (player != null)
                    {
                        if (player.QuestManager.IsDynamicQuestEligible(player))
                        {
                            player.QuestManager.ComputeDynamicQuest("Dynamic_1", player.Session, false);
                        }
                        else
                        {
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The Town Crier looks at you, exhausted, clearly nursing a writers cramp. Perhaps you should come back tomorrow...", ChatMessageType.Tell));
                        }
                        
                    }

                    break;
                case EmoteType.StartDynamicBounty:
                    if (player != null)
                    {

                    }
                    break;
                case EmoteType.PromptAddAugment:
                    if (player != null)
                    {
                        switch (emote.Message)
                        {
                            case "Creature":
                                long creatureAugs = player.LuminanceAugmentCreatureCount ?? 0;
                                var curVal = emote.Amount + (creatureAugs * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentCreatureCount = creatureAugs + 1;
                                        player.TryConsumeFromInventoryWithNetworking(300005, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your {emote.Message} casting abilities by 1.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal:N0} luminance to add 1 point to all of your creature spell effects. Are you sure?");
                                }
                                break;
                            case "Item":
                                long itemAugs = player.LuminanceAugmentItemCount ?? 0;
                                var curVal2 = emote.Amount + (itemAugs * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal2)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal2:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal2)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal2:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal2))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentItemCount = itemAugs + 1;
                                        player.TryConsumeFromInventoryWithNetworking(300006, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your {emote.Message} casting abilities by 1.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal2:N0} luminance to add an equivalent point to all of your item spell effects. Are you sure?");
                                }
                                break;
                            case "Life":
                                long lifeAugs = player.LuminanceAugmentLifeCount ?? 0;
                                var curVal3 = emote.Amount + (lifeAugs * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal3)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal3:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal3)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal3:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal3))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentLifeCount = lifeAugs + 1;
                                        player.TryConsumeFromInventoryWithNetworking(300007, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your {emote.Message} casting abilities by 1.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal3:N0} luminance to add 1 point to all of your life spell effects. Are you sure?");
                                }
                                break;
                            case "War":
                                long warAugs = player.LuminanceAugmentWarCount ?? 0;
                                var curVal4 = emote.Amount + (warAugs * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal4)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal4:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal4)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal4:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal4))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentWarCount = warAugs + 1;
                                        player.TryConsumeFromInventoryWithNetworking(300008, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your {emote.Message} casting abilities by 1.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal4:N0} luminance to add 1 point to all of your war spell effects. Are you sure?");
                                }
                                break;
                            case "Void":
                                long voidAugs = player.LuminanceAugmentVoidCount ?? 0;
                                var curVal5 = emote.Amount + (voidAugs * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal5)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal5:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal5)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal5:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal5))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentVoidCount = voidAugs + 1;
                                        player.TryConsumeFromInventoryWithNetworking(300009, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your {emote.Message} casting abilities by 1.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal5:N0} luminance to add 1 point to all of your void spell effects. Are you sure?");

                                }
                                break;
                            case "Duration":
                                long durAugs = player.LuminanceAugmentSpellDurationCount ?? 0;
                                var curVal6 = emote.Amount + (durAugs * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal6)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal6:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal6)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal6:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal6))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentSpellDurationCount = durAugs + 1;
                                        player.TryConsumeFromInventoryWithNetworking(300016, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your spell {emote.Message} by 5%.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal6:N0} luminance to add 5% to the duration of all of your spell effects. Are you sure?");

                                }
                                break;
                            case "Specialize":
                                long specAugs = player.LuminanceAugmentSpecializeCount ?? 0;
                                var curVal7 = emote.Amount + (specAugs * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal7)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal7:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal7)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal7:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal7))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentSpecializeCount = specAugs + 1;
                                        player.TryConsumeFromInventoryWithNetworking(300021, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your Max Specialized Skill Credits by 1.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal7:N0} luminance to add 1 point to your max specialized skill credits. Are you sure?");
                                }
                                break;
                            case "Summon":
                                long summonAugs = player.LuminanceAugmentSummonCount ?? 0;
                                var curVal8 = emote.Amount + (summonAugs * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal8)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal8:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal8)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal8:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal8))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentSummonCount = summonAugs + 1;
                                        player.TryConsumeFromInventoryWithNetworking(2003001, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your summons damage resist rating by 3.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal8:N0} luminance to add 3 points to your summons damage resist rating. Are you sure?");
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case EmoteType.PromptAddAugment10:
                    if (player != null)
                    {
                        switch (emote.Message)
                        {
                            case "Creature10":
                                long creatureAugs10 = player.LuminanceAugmentCreatureCount ?? 0;
                                var curVal9 = emote.Amount + (creatureAugs10 * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (creatureAugs10 + 1) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (creatureAugs10 + 2) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (creatureAugs10 + 3) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (creatureAugs10 + 4) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (creatureAugs10 + 5) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (creatureAugs10 + 6) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (creatureAugs10 + 7) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (creatureAugs10 + 8) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (creatureAugs10 + 9) * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal9)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal9:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal9)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal9:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal9))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentCreatureCount = creatureAugs10 + 10;
                                        player.TryConsumeFromInventoryWithNetworking(81000125, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your Creature casting abilities by 10.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal9:N0} luminance to add 10 points to all of your creature spell effects. Are you sure?");
                                }
                                break;
                            case "Item10":
                                long itemAugs10 = player.LuminanceAugmentItemCount ?? 0;
                                var curVal10 = emote.Amount + (itemAugs10 * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (itemAugs10 + 1) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (itemAugs10 + 2) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (itemAugs10 + 3) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (itemAugs10 + 4) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (itemAugs10 + 5) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (itemAugs10 + 6) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (itemAugs10 + 7) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (itemAugs10 + 8) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (itemAugs10 + 9) * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal10)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal10:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal10)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal10:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal10))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentItemCount = itemAugs10 + 10;
                                        player.TryConsumeFromInventoryWithNetworking(81000126, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your Item casting abilities by 10.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal10:N0} luminance to add an equivalent points to all of your item spell effects. Are you sure?");
                                }
                                break;
                            case "Life10":
                                long lifeAugs10 = player.LuminanceAugmentLifeCount ?? 0;
                                var curVal11 = emote.Amount + (lifeAugs10 * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (lifeAugs10 + 1) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (lifeAugs10 + 2) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (lifeAugs10 + 3) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (lifeAugs10 + 4) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (lifeAugs10 + 5) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (lifeAugs10 + 6) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (lifeAugs10 + 7) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (lifeAugs10 + 8) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (lifeAugs10 + 9) * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal11)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal11:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal11)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal11:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal11))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentLifeCount = lifeAugs10 + 10;
                                        player.TryConsumeFromInventoryWithNetworking(81000127, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your Life casting abilities by 10.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal11:N0} luminance to add 10 points to all of your life spell effects. Are you sure?");
                                }
                                break;
                            case "War10":
                                long warAugs10 = player.LuminanceAugmentWarCount ?? 0;
                                var curVal12 = emote.Amount + (warAugs10 * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (warAugs10 + 1) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (warAugs10 + 2) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (warAugs10 + 3) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (warAugs10 + 4) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (warAugs10 + 5) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (warAugs10 + 6) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (warAugs10 + 7) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (warAugs10 + 8) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (warAugs10 + 9) * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal12)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal12:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal12)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal12:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal12))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentWarCount = warAugs10 + 10;
                                        player.TryConsumeFromInventoryWithNetworking(81000128, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your War casting abilities by 10.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal12:N0} luminance to add 10 points to all of your war spell effects. Are you sure?");
                                }
                                break;
                            case "Void10":
                                long voidAugs10 = player.LuminanceAugmentVoidCount ?? 0;
                                var curVal13 = emote.Amount + (voidAugs10 * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (voidAugs10 + 1) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (voidAugs10 + 2) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (voidAugs10 + 3) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (voidAugs10 + 4) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (voidAugs10 + 5) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (voidAugs10 + 6) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (voidAugs10 + 7) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (voidAugs10 + 8) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (voidAugs10 + 9) * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal13)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal13:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal13)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal13:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal13))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentVoidCount = voidAugs10 + 10;
                                        player.TryConsumeFromInventoryWithNetworking(81000129, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your Void casting abilities by 10.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal13:N0} luminance to add 10 points to all of your void spell effects. Are you sure?");

                                }
                                break;
                            case "Duration10":
                                long durAugs10 = player.LuminanceAugmentSpellDurationCount ?? 0;
                                var curVal14 = emote.Amount + (durAugs10 * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (durAugs10 + 1) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (durAugs10 + 2) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (durAugs10 + 3) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (durAugs10 + 4) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (durAugs10 + 5) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (durAugs10 + 6) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (durAugs10 + 7) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (durAugs10 + 8) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (durAugs10 + 9) * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal14)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal14:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal14)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal14:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal14))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentSpellDurationCount = durAugs10 + 10;
                                        player.TryConsumeFromInventoryWithNetworking(81000130, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your spell Duration by 50%.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal14:N0} luminance to add 50% to the duration of all of your spell effects. Are you sure?");

                                }
                                break;
                            case "Specialize10":
                                long specAugs10 = player.LuminanceAugmentSpecializeCount ?? 0;
                                var curVal15 = emote.Amount + (specAugs10 * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (specAugs10 + 1) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (specAugs10 + 2) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (specAugs10 + 3) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (specAugs10 + 4) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (specAugs10 + 5) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (specAugs10 + 6) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (specAugs10 + 7) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (specAugs10 + 8) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (specAugs10 + 9) * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal15)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal15:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal15)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal15:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal15))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentSpecializeCount = specAugs10 + 10;
                                        player.TryConsumeFromInventoryWithNetworking(81000131, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your Max Specialized Skill Credits by 10.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal15:N0} luminance to add 10 points to your max specialized skill credits. Are you sure?");
                                }
                                break;
                            case "Summon10":
                                long summonAugs10 = player.LuminanceAugmentSummonCount ?? 0;
                                var curVal16 = emote.Amount + (summonAugs10 * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (summonAugs10 + 1) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (summonAugs10 + 2) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (summonAugs10 + 3) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (summonAugs10 + 4) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (summonAugs10 + 5) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (summonAugs10 + 6) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (summonAugs10 + 7) * (emote.Amount * (1 + emote.Percent)))
                                    + (emote.Amount + (summonAugs10 + 8) * (emote.Amount * (1 + emote.Percent))) + (emote.Amount + (summonAugs10 + 9) * (emote.Amount * (1 + emote.Percent)));
                                if (player.BankedLuminance < curVal16)
                                {
                                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal16:N0} luminance to use.", ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                    {
                                        if (player.BankedLuminance < curVal16)
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {curVal16:N0} luminance to use.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        if (!player.SpendLuminance((long)curVal16))
                                        {
                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                            return;
                                        }
                                        player.LuminanceAugmentSummonCount = summonAugs10 + 10;
                                        player.TryConsumeFromInventoryWithNetworking(81000132, 1);
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have succesfully increased your summons damage resist rating by 30.", ChatMessageType.Broadcast));
                                    }), $"You are about to spend {curVal16:N0} luminance to add 30 points to your summons damage resist rating. Are you sure?");
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case EmoteType.SetAttributeStat:
                    if (player != null && emote.Amount != null)
                    {
                        switch (emote.Stat)
                        {
                            case 1:
                                player.Strength.Ranks = Player.CalcAttributeRank(Player.GetXPCostByRank((uint)emote.Amount));
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Strength));
                                break;
                            case 2:
                                player.Endurance.Ranks = Player.CalcAttributeRank(Player.GetXPCostByRank((uint)emote.Amount));
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Endurance));
                                break;
                            case 3:
                                player.Quickness.Ranks = Player.CalcAttributeRank(Player.GetXPCostByRank((uint)emote.Amount));
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Quickness));
                                break;
                            case 4:
                                player.Coordination.Ranks = Player.CalcAttributeRank(Player.GetXPCostByRank((uint)emote.Amount));
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Coordination));
                                break;
                            case 5:
                                player.Focus.Ranks = Player.CalcAttributeRank(Player.GetXPCostByRank((uint)emote.Amount));
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Focus));
                                break;
                            case 6:
                                player.Self.Ranks = Player.CalcAttributeRank(Player.GetXPCostByRank((uint)emote.Amount));
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Self));
                                break;
                            default:
                                break;
                        }

                        
                    }
                    break;
                case EmoteType.SetSecondaryAttributeStat:
                    if (player != null)
                    {

                    }
                    break;
                case EmoteType.SetEnvironment:
                    if (WorldObject == null || WorldObject.CurrentLandblock == null)
                    {
                        break;
                    }
                    else
                    {
                        EnvironChangeType changeType = EnvironChangeType.Clear;
                        switch (emote.Amount)
                        {
                            case 0:
                                changeType = EnvironChangeType.Clear;
                                break;
                            case 1:
                                changeType = EnvironChangeType.RedFog;
                                break;
                            case 2:
                                changeType = EnvironChangeType.BlueFog;
                                break;
                            case 3:
                                changeType = EnvironChangeType.WhiteFog;
                                break;
                            case 4:
                                changeType = EnvironChangeType.GreenFog;
                                break;
                            case 5:
                                changeType = EnvironChangeType.BlackFog;
                                break;
                            case 6:
                                changeType = EnvironChangeType.BlackFog2;
                                break;
                            case 101:
                                changeType = EnvironChangeType.RoarSound;
                                break;
                            case 102:
                                changeType = EnvironChangeType.BellSound;
                                break;
                            case 103:
                                changeType = EnvironChangeType.Chant1Sound;
                                break;
                            case 104:
                                changeType = EnvironChangeType.Chant2Sound;
                                break;
                            case 105:
                                changeType = EnvironChangeType.DarkWhispers1Sound;
                                break;
                            case 106:
                                changeType = EnvironChangeType.DarkWhispers2Sound;
                                break;
                            case 107:
                                changeType = EnvironChangeType.DarkLaughSound;
                                break;
                            case 108:
                                changeType = EnvironChangeType.DarkWindSound;
                                break;
                            case 109:
                                changeType = EnvironChangeType.DarkSpeechSound;
                                break;
                            case 110:
                                changeType = EnvironChangeType.DrumsSound;
                                break;
                            case 111:
                                changeType = EnvironChangeType.GhostSpeakSound;
                                break;
                            case 112:
                                changeType = EnvironChangeType.BreathingSound;
                                break;
                            case 113:
                                changeType = EnvironChangeType.HowlSound;
                                break;
                            case 114:
                                changeType = EnvironChangeType.LostSoulsSound;
                                break;
                            case 117:
                                changeType = EnvironChangeType.SquealSound;
                                break;
                            case 118:
                                changeType = EnvironChangeType.Thunder1Sound;
                                break;
                            case 119:
                                changeType = EnvironChangeType.Thunder2Sound;
                                break;
                            case 120:
                                changeType = EnvironChangeType.Thunder3Sound;
                                break;
                            case 121:
                                changeType = EnvironChangeType.Thunder4Sound;
                                break;
                            case 122:
                                changeType = EnvironChangeType.Thunder5Sound;
                                break;
                            case 123:
                                changeType = EnvironChangeType.Thunder6Sound;
                                break;


                        }
                        WorldObject.CurrentLandblock.SendEnvironChange(changeType);
                    }
                    break;
                default:
                    log.Debug($"EmoteManager.Execute - Encountered Unhandled EmoteType {(EmoteType)emote.Type} for {WorldObject.Name} ({WorldObject.WeenieClassId})");
                    break;
            }

            return delay;
        }

        /// <summary>
        /// Selects an emote set based on category, and optional: quest, vendor, rng
        /// </summary>
        public PropertiesEmote GetEmoteSet(EmoteCategory category, string questName = null, VendorType? vendorType = null, uint? wcid = null, bool useRNG = true)
        {
            //if (Debug) Console.WriteLine($"{WorldObject.Name}.EmoteManager.GetEmoteSet({category}, {questName}, {vendorType}, {wcid}, {useRNG})");

            if (_worldObject.Biota.PropertiesEmote == null)
                return null;
            
            // always pull emoteSet from _worldObject
            var emoteSet = _worldObject.Biota.PropertiesEmote.Where(e => e.Category == category);

            if (category == EmoteCategory.Refuse && questName != null)
            {
                emoteSet = emoteSet.Where(e => e.Quest != null && e.Quest.Equals(questName, StringComparison.OrdinalIgnoreCase));
                if (emoteSet.Count() == 0 && _worldObject.Biota.DynamicEmoteList != null) //pre-refresh dynamic quests
                {
                    emoteSet = _worldObject.Biota.DynamicEmoteList.Where(e => e.Quest == questName);
                }
            }

            // optional criteria
            if ((category == EmoteCategory.HearChat || category == EmoteCategory.ReceiveTalkDirect) && questName != null)
                emoteSet = emoteSet.Where(e => e.Quest != null && e.Quest.Equals(questName, StringComparison.OrdinalIgnoreCase) || e.Quest == null);
            else if (questName != null)
                emoteSet = emoteSet.Where(e => e.Quest != null && e.Quest.Equals(questName, StringComparison.OrdinalIgnoreCase));
            if (vendorType != null)
                emoteSet = emoteSet.Where(e => e.VendorType != null && e.VendorType.Value == vendorType);
            if (wcid != null)
                emoteSet = emoteSet.Where(e => e.WeenieClassId == wcid.Value);

            if (category == EmoteCategory.HeartBeat)
            {
                WorldObject.GetCurrentMotionState(out MotionStance currentStance, out MotionCommand currentMotion);

                emoteSet = emoteSet.Where(e => e.Style == null || e.Style == currentStance);
                emoteSet = emoteSet.Where(e => e.Substyle == null || e.Substyle == currentMotion);
            }

            if (category == EmoteCategory.WoundedTaunt)
            {
                if (_worldObject is Creature creature)
                    emoteSet = emoteSet.Where(e => creature.Health.Percent >= e.MinHealth && creature.Health.Percent <= e.MaxHealth);
            }

            if (useRNG)
            {
                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
                emoteSet = emoteSet.Where(e => e.Probability > rng).OrderBy(e => e.Probability);
                //emoteSet = emoteSet.Where(e => e.Probability >= rng);
            }

            return emoteSet.FirstOrDefault();
        }

        /// <summary>
        /// Convenience wrapper between GetEmoteSet and ExecututeEmoteSet
        /// </summary>
        public void ExecuteEmoteSet(EmoteCategory category, string quest = null, WorldObject targetObject = null, bool nested = false)
        {
            //if (Debug) Console.WriteLine($"{WorldObject.Name}.EmoteManager.ExecuteEmoteSet({category}, {quest}, {targetObject}, {nested})");

            var emoteSet = GetEmoteSet(category, quest);

            if (emoteSet == null) return;

            // TODO: revisit if nested chains need to propagate timers
            try
            {
                ExecuteEmoteSet(emoteSet, targetObject, nested);
            }
            catch (StackOverflowException)
            {
                log.Error($"Stack Overflow while ExecuteEmoteSet - Weenie: {WorldObject.WeenieClassId}, {category}, {quest}");
                return;
            }
            
        }

        /// <summary>
        /// Executes a set of emotes to run with delays
        /// </summary>
        /// <param name="emoteSet">A list of emotes to execute</param>
        /// <param name="targetObject">An optional target, usually player</param>
        /// <param name="actionChain">For adding delays between emotes</param>
        public bool ExecuteEmoteSet(PropertiesEmote emoteSet, WorldObject targetObject = null, bool nested = false)
        {
            //if (Debug) Console.WriteLine($"{WorldObject.Name}.EmoteManager.ExecuteEmoteSet({emoteSet}, {targetObject}, {nested})");

            // detect busy state
            // TODO: maybe eventually we should consider having categories that can be queued?
            // there are some categories that shouldn't be queued, like heartbeats...
            if (IsBusy && !nested) return false;

            // start action chain
            Nested++;
            Enqueue(emoteSet, targetObject);

            return true;
        }

        public void Enqueue(PropertiesEmote emoteSet, WorldObject targetObject, int emoteIdx = 0, float delay = 0.0f)
        {
            //if (Debug) Console.WriteLine($"{WorldObject.Name}.EmoteManager.Enqueue({emoteSet}, {targetObject}, {emoteIdx}, {delay})");

            if (emoteSet == null || emoteIdx >= emoteSet.PropertiesEmoteAction.Count)
            {
                Nested--;
                return;
            }
            if (Nested > 100)
            {
                log.Error($"[EMOTE] {WorldObject.Name}.EmoteManager.Enqueue(): Nested > 100 possible Infinite loop detected and aborted on 0x{WorldObject.Guid}:{WorldObject.WeenieClassId}");
                return;
            }

            IsBusy = true;

            var emote = emoteSet.PropertiesEmoteAction.ElementAt(emoteIdx);

            if (Nested > 75 && !string.IsNullOrEmpty(emoteSet.Quest) && emoteSet.Quest == emote.Message && EmoteIsBranchingType(emote))
            {
                var emoteStack = $"{emoteSet.Category}: {emoteSet.Quest}\n";
                foreach (var e in emoteSet.PropertiesEmoteAction)
                    emoteStack += $"       - {(EmoteType)emote.Type}{(string.IsNullOrEmpty(emote.Message) ? "" : $": {emote.Message}")}\n";

                log.Error($"[EMOTE] {WorldObject.Name}.EmoteManager.Enqueue(): Nested > 75, possible Infinite loop detected and aborted on 0x{WorldObject.Guid}:{WorldObject.WeenieClassId}\n-> {emoteStack}");

                Nested--;

                if (Nested == 0)
                    IsBusy = false;

                return;
            }

            

            if (delay + emote.Delay > 0)
            {
                var actionChain = new ActionChain();

                if (Debug)
                    actionChain.AddAction(WorldObject, () => Console.Write($"{emote.Delay} - "));

                // delay = post-delay from actual time of previous emote
                // emote.Delay = pre-delay for current emote
                actionChain.AddDelaySeconds(delay + emote.Delay);

                actionChain.AddAction(WorldObject, () => DoEnqueue(emoteSet, targetObject, emoteIdx, emote));
                actionChain.EnqueueChain();
            }
            else
            {
                DoEnqueue(emoteSet, targetObject, emoteIdx, emote);
            }
        }

        /// <summary>
        /// This should only be called by Enqueue
        /// </summary>
        private void DoEnqueue(PropertiesEmote emoteSet, WorldObject targetObject, int emoteIdx, PropertiesEmoteAction emote)
        {
            if (Debug)
                Console.Write($"{(EmoteType)emote.Type}");

            //if (!string.IsNullOrEmpty(emoteSet.Quest) && emoteSet.Quest == emote.Message && EmoteIsBranchingType(emote))
            //{
            //    log.Error($"[EMOTE] {WorldObject.Name}.EmoteManager.DoEnqueue(): Infinite loop detected on 0x{WorldObject.Guid}:{WorldObject.WeenieClassId}\n-> {emoteSet.Category}: {emoteSet.Quest} to {(EmoteType)emote.Type}: {emote.Message}");

            //    Nested--;

            //    if (Nested == 0)
            //        IsBusy = false;

            //    return;
            //}

            var nextDelay = ExecuteEmote(emoteSet, emote, targetObject);

            if (Debug)
                Console.WriteLine($" - { nextDelay}");

            if (emoteIdx < emoteSet.PropertiesEmoteAction.Count - 1)
                Enqueue(emoteSet, targetObject, emoteIdx + 1, nextDelay);
            else
            {
                if (nextDelay > 0)
                {
                    var delayChain = new ActionChain();
                    delayChain.AddDelaySeconds(nextDelay);
                    delayChain.AddAction(WorldObject, () =>
                    {
                        Nested--;

                        if (Nested == 0)
                            IsBusy = false;
                    });
                    delayChain.EnqueueChain();
                }
                else
                {
                    Nested--;

                    if (Nested == 0)
                        IsBusy = false;
                }
            }
        }

        private bool EmoteIsBranchingType(PropertiesEmoteAction emote)
        {
            if (emote == null)
                return false;

            var emoteType = (EmoteType)emote.Type;

            switch (emoteType)
            {
                case EmoteType.UpdateQuest:
                case EmoteType.InqQuest:
                case EmoteType.InqQuestSolves:
                case EmoteType.InqBoolStat:
                case EmoteType.InqIntStat:
                case EmoteType.InqFloatStat:
                case EmoteType.InqStringStat:
                case EmoteType.InqAttributeStat:
                case EmoteType.InqRawAttributeStat:
                case EmoteType.InqSecondaryAttributeStat:
                case EmoteType.InqRawSecondaryAttributeStat:
                case EmoteType.InqSkillStat:
                case EmoteType.InqRawSkillStat:
                case EmoteType.InqSkillTrained:
                case EmoteType.InqSkillSpecialized:
                case EmoteType.InqEvent:
                case EmoteType.InqFellowQuest:
                case EmoteType.InqFellowNum:
                case EmoteType.UpdateFellowQuest:
                case EmoteType.Goto:
                case EmoteType.InqNumCharacterTitles:
                case EmoteType.InqYesNo:
                case EmoteType.InqOwnsItems:
                case EmoteType.UpdateMyQuest:
                case EmoteType.InqMyQuest:
                case EmoteType.InqMyQuestSolves:
                case EmoteType.InqPackSpace:
                case EmoteType.InqQuestBitsOn:
                case EmoteType.InqQuestBitsOff:
                case EmoteType.InqMyQuestBitsOn:
                case EmoteType.InqMyQuestBitsOff:
                case EmoteType.InqInt64Stat:
                case EmoteType.InqContractsFull:
                    return true;
                default:
                    return false;
            }
        }

        public bool HasValidTestNoQuality(string testName) => GetEmoteSet(EmoteCategory.TestNoQuality, testName) != null;

        /// <summary>
        /// The maximum animation range of the client
        /// Motions broadcast outside of this range will be automatically queued by client
        /// </summary>
        public static float ClientMaxAnimRange = 96.0f;     // verify: same indoors?

        /// <summary>
        /// The client automatically queues animations that are broadcast outside of 96.0f range
        /// Normally we exclude these emotes from being broadcast outside this range,
        /// but for certain emotes (like monsters going to sleep) we want to always broadcast / enqueue
        /// </summary>
        public static HashSet<MotionCommand> MotionQueue = new HashSet<MotionCommand>()
        {
            MotionCommand.Sleeping
        };

        public void DoVendorEmote(VendorType vendorType, WorldObject target)
        {
            var vendorSet = GetEmoteSet(EmoteCategory.Vendor, null, vendorType);
            var heartbeatSet = GetEmoteSet(EmoteCategory.Vendor, null, VendorType.Heartbeat);

            ExecuteEmoteSet(vendorSet, target);
            ExecuteEmoteSet(heartbeatSet, target, true);
        }

        public IEnumerable<PropertiesEmote> Emotes(EmoteCategory emoteCategory)
        {
            return _worldObject.Biota.PropertiesEmote.Where(x => x.Category == emoteCategory);
        }

        public string Replace(string message, WorldObject source, WorldObject target, string quest)
        {
            var result = message;

            if (result == null)
            {
                log.Warn($"[EMOTE] {WorldObject.Name}.EmoteManager.Replace(message, {source.Name}:0x{source.Guid}:{source.WeenieClassId}, {target.Name}:0x{target.Guid}:{target.WeenieClassId}, {quest}): message was null!");
                return "";
            }

            var sourceName = source != null ? source.Name : "";
            var targetName = target != null ? target.Name : "";

            result = result.Replace("%n", sourceName);
            result = result.Replace("%mn", sourceName);
            result = result.Replace("%s", targetName);
            result = result.Replace("%tn", targetName);

            var sourceLevel = source != null ? $"{source.Level ?? 0}" : "";
            var targetLevel = target != null ? $"{target.Level ?? 0}" : "";
            result = result.Replace("%ml", sourceLevel);
            result = result.Replace("%tl", targetLevel);

            //var sourceTemplate = source != null ? source.GetProperty(PropertyString.Title) : "";
            //var targetTemplate = source != null ? target.GetProperty(PropertyString.Title) : "";
            var sourceTemplate = source != null ? source.GetProperty(PropertyString.Template) : "";
            var targetTemplate = target != null ? target.GetProperty(PropertyString.Template) : "";
            result = result.Replace("%mt", sourceTemplate);
            result = result.Replace("%tt", targetTemplate);

            var sourceHeritage = source != null ? source.HeritageGroupName : "";
            var targetHeritage = target != null ? target.HeritageGroupName : "";
            result = result.Replace("%mh", sourceHeritage);
            result = result.Replace("%th", targetHeritage);

            //result = result.Replace("%mf", $"{source.GetProperty(PropertyString.Fellowship)}");
            //result = result.Replace("%tf", $"{target.GetProperty(PropertyString.Fellowship)}");

            //result = result.Replace("%l", $"{???}"); // level?
            //result = result.Replace("%pk", $"{???}"); // pk status?
            //result = result.Replace("%a", $"{???}"); // allegiance?
            //result = result.Replace("%p", $"{???}"); // patron?

            // Find quest in standard or LSD custom usage for %tqt and %CDtime
            var embeddedQuestName = result.Contains("@") ? message.Split("@")[0] : null;
            var questName = !string.IsNullOrWhiteSpace(embeddedQuestName) ? embeddedQuestName : quest;

            // LSD custom tqt usage
            result = result.Replace($"{questName}@%tqt", "You may complete this quest again in %tqt.", StringComparison.OrdinalIgnoreCase);

            // LSD custom CDtime variable
            if (result.Contains("%CDtime"))
                result = result.Replace($"{questName}@", "", StringComparison.OrdinalIgnoreCase);

            if (target is Player targetPlayer)
            {
                result = result.Replace("%tqt", !string.IsNullOrWhiteSpace(quest) ? targetPlayer.QuestManager.GetNextSolveTime(questName).GetFriendlyString() : "");
                
                result = result.Replace("%CDtime", !string.IsNullOrWhiteSpace(quest) ? targetPlayer.QuestManager.GetNextSolveTime(questName).GetFriendlyString() : "");

                result = result.Replace("%tf", $"{(targetPlayer.Fellowship != null ? targetPlayer.Fellowship.FellowshipName : "")}");

                result = result.Replace("%fqt", !string.IsNullOrWhiteSpace(quest) && targetPlayer.Fellowship != null ? targetPlayer.Fellowship.QuestManager.GetNextSolveTime(questName).GetFriendlyString() : "");

                result = result.Replace("%tqm", !string.IsNullOrWhiteSpace(quest) ? targetPlayer.QuestManager.GetMaxSolves(questName).ToString() : "");

                result = result.Replace("%tqc", !string.IsNullOrWhiteSpace(quest) ? targetPlayer.QuestManager.GetCurrentSolves(questName).ToString() : "");
            }

            if (source is Creature sourceCreature)
            {
                result = result.Replace("%mqt", !string.IsNullOrWhiteSpace(quest) ? sourceCreature.QuestManager.GetNextSolveTime(questName).GetFriendlyString() : "");

                result = result.Replace("%mxqt", !string.IsNullOrWhiteSpace(quest) ? sourceCreature.QuestManager.GetNextSolveTime(questName).GetFriendlyLongString() : "");

                //result = result.Replace("%CDtime", !string.IsNullOrWhiteSpace(quest) ? sourceCreature.QuestManager.GetNextSolveTime(questName).GetFriendlyString() : "");

                result = result.Replace("%mqc", !string.IsNullOrWhiteSpace(quest) ? sourceCreature.QuestManager.GetCurrentSolves(questName).ToString() : "");
            }

            return result;
        }

        /// <summary>
        /// Returns the creature target for quest emotes
        /// </summary>
        public static Creature GetQuestTarget(EmoteType emote, Creature target, Creature self)
        {
            switch (emote)
            {
                // MyQuest always targets self
                case EmoteType.DecrementMyQuest:
                case EmoteType.EraseMyQuest:
                case EmoteType.IncrementMyQuest:
                case EmoteType.InqMyQuest:
                case EmoteType.InqMyQuestBitsOff:
                case EmoteType.InqMyQuestBitsOn:
                case EmoteType.InqMyQuestSolves:
                case EmoteType.SetMyQuestBitsOff:
                case EmoteType.SetMyQuestBitsOn:
                case EmoteType.SetMyQuestCompletions:
                case EmoteType.StampMyQuest:
                case EmoteType.UpdateMyQuest:

                    return self;

                default:

                    return target ?? self;
            }
        }

        private WorldObject GetSpellTarget(Spell spell, WorldObject target)
        {
            var targetSelf = spell.Flags.HasFlag(SpellFlags.SelfTargeted);
            var untargeted = spell.NonComponentTargetType == ItemType.None;

            var spellTarget = target;
            if (untargeted)
                spellTarget = null;
            else if (targetSelf)
                spellTarget = WorldObject;

            return spellTarget;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void HeartBeat()
        {
            // player didn't do idle emotes in retail?
            if (WorldObject is Player)
                return;

            if (WorldObject is Creature creature && creature.IsAwake)
                return;

            ExecuteEmoteSet(EmoteCategory.HeartBeat);
        }

        public void OnUse(Creature activator)
        {
            ExecuteEmoteSet(EmoteCategory.Use, null, activator);
        }

        public void OnPortal(Creature activator)
        {
            IsBusy = false;

            ExecuteEmoteSet(EmoteCategory.Portal, null, activator);
        }

        public void OnActivation(Creature activator)
        {
            ExecuteEmoteSet(EmoteCategory.Activation, null, activator);
        }

        public void OnGeneration()
        {
            ExecuteEmoteSet(EmoteCategory.Generation, null, null);
        }

        public void OnWield(Creature wielder)
        {
            ExecuteEmoteSet(EmoteCategory.Wield, null, wielder);
        }

        public void OnUnwield(Creature wielder)
        {
            ExecuteEmoteSet(EmoteCategory.UnWield, null, wielder);
        }

        public void OnPickup(Creature initiator)
        {
            ExecuteEmoteSet(EmoteCategory.PickUp, null, initiator);
        }

        public void OnDrop(Creature dropper)
        {
            ExecuteEmoteSet(EmoteCategory.Drop, null, dropper);
        }

        /// <summary>
        /// Called when an idle mob becomes alerted by a player
        /// and initially wakes up
        /// </summary>
        public void OnWakeUp(Creature target)
        {
            ExecuteEmoteSet(EmoteCategory.Scream, null, target);
        }

        /// <summary>
        /// Called when a monster switches targets
        /// </summary>
        public void OnNewEnemy(WorldObject newEnemy)
        {
            ExecuteEmoteSet(EmoteCategory.NewEnemy, null, newEnemy);
        }

        /// <summary>
        /// Called when a monster completes an attack
        /// </summary>
        public void OnAttack(WorldObject target)
        {
            ExecuteEmoteSet(EmoteCategory.Taunt, null, target);
        }

        public void OnDamage(Creature attacker)
        {
            ExecuteEmoteSet(EmoteCategory.WoundedTaunt, null, attacker);
        }

        public void OnReceiveCritical(Creature attacker)
        {
            ExecuteEmoteSet(EmoteCategory.ReceiveCritical, null, attacker);
        }

        public void OnResistSpell(Creature attacker)
        {
            ExecuteEmoteSet(EmoteCategory.ResistSpell, null, attacker);
        }

        public void OnDeath(DamageHistoryInfo lastDamagerInfo)
        {
            IsBusy = false;

            var lastDamager = lastDamagerInfo?.TryGetPetOwnerOrAttacker();

            ExecuteEmoteSet(EmoteCategory.Death, null, lastDamager);
        }

        /// <summary>
        /// Called when a monster kills a player
        /// </summary>
        public void OnKill(Player player)
        {
            ExecuteEmoteSet(EmoteCategory.KillTaunt, null, player);
        }

        /// <summary>
        /// Called when player interacts with item that has a Quest string
        /// </summary>
        public void OnQuest(Creature initiator)
        {
            var questName = WorldObject.Quest;

            var hasQuest = initiator.QuestManager.HasQuest(questName);

            if (!hasQuest)
            {
                // add new quest
                initiator.QuestManager.Update(questName);
                hasQuest = initiator.QuestManager.HasQuest(questName);
                ExecuteEmoteSet(hasQuest ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, questName, initiator);
            }
            else
            {
                // update existing quest
                var canSolve = initiator.QuestManager.CanSolve(questName);
                if (canSolve)
                    initiator.QuestManager.Stamp(questName);
                ExecuteEmoteSet(canSolve ? EmoteCategory.QuestSuccess : EmoteCategory.QuestFailure, questName, initiator);
            }
        }

        /// <summary>
        /// Called when this NPC receives a direct text message from a player
        /// </summary>
        public void OnTalkDirect(Player player, string message)
        {
            ExecuteEmoteSet(EmoteCategory.ReceiveTalkDirect, message, player);
        }

        /// <summary>
        /// Called when this NPC receives a local signal from a player
        /// </summary>
        public void OnLocalSignal(WorldObject emitter, string message)
        {
            ExecuteEmoteSet(EmoteCategory.ReceiveLocalSignal, message, emitter);
        }

        /// <summary>
        /// Called when monster exceeds the maximum distance from home position
        /// </summary>
        public void OnHomeSick(WorldObject attackTarget)
        {
            ExecuteEmoteSet(EmoteCategory.Homesick, null, attackTarget);
        }

        /// <summary>
        /// Called when this NPC hears local chat from a player
        /// </summary>
        public void OnHearChat(Player player, string message)
        {
            ExecuteEmoteSet(EmoteCategory.HearChat, message, player);
        }

        //public bool HasAntennas => WorldObject.Biota.BiotaPropertiesEmote.Count(x => x.Category == (int)EmoteCategory.ReceiveLocalSignal) > 0;

        /// <summary>
        /// Call this function when WorldObject is being used via a proxy object, e.g.: Hooker on a Hook
        /// </summary>
        public void SetProxy(WorldObject worldObject)
        {
            _proxy = worldObject;
        }

        /// <summary>
        /// Called when this object is removed from the proxy object (Hooker is picked up from Hook)
        /// </summary>
        public void ClearProxy()
        {
            _proxy = null;
        }


        /// <summary>
        /// Dynamically add an emote to the given worldObject for the purposes of responding to dynamic quests
        /// </summary>
        /// <param name="emote">The eMote to add</param>
        /// <returns></returns>
        public bool AddEmote(PropertiesEmote emote)
        {
            if(emote == null)
            {
                return false;
            }
            if (_worldObject.Biota.PropertiesEmote == null)
            {
                _worldObject.Biota.PropertiesEmote = new List<PropertiesEmote>();
            }
            _worldObject.Biota.PropertiesEmote.Add(emote); //preserved on refresh
            if (_worldObject.Biota.DynamicEmoteList == null)
            {
                _worldObject.Biota.DynamicEmoteList = new List<PropertiesEmote>();
            }
            _worldObject.Biota.DynamicEmoteList.Add(emote); //temporary pre-refresh list

            return true;
        }


        /// <summary>
        /// Dynamicall add an emote to the given worldObject for the purposes of responding to dynamic quests
        /// </summary>
        /// <param name="emote">World Database Emote</param>
        /// <returns></returns>
        public bool AddEmote(Database.Models.World.WeeniePropertiesEmote emote)
        {
            //map the database emote to the biota object emote
            ACE.Entity.Models.PropertiesEmote newEmote = new ACE.Entity.Models.PropertiesEmote();

            newEmote.DatabaseRecordId = 1;
            newEmote.Quest = emote.Quest;
            newEmote.MaxHealth = emote.MaxHealth;
            newEmote.MinHealth = emote.MinHealth;
            newEmote.Style = (MotionStance?)emote.Style;
            newEmote.Category = (EmoteCategory)emote.Category;
            newEmote.VendorType = (VendorType?)emote.VendorType;
            newEmote.Substyle = (MotionCommand?)emote.Substyle;
            newEmote.Object = ACE.Database.Adapter.WeenieConverter.ConvertToEntityWeenie(emote.Object);
            newEmote.Probability = emote.Probability;
            newEmote.WeenieClassId = emote.WeenieClassId;
            List<Database.Models.World.WeeniePropertiesEmoteAction> actions = emote.WeeniePropertiesEmoteAction.ToList();
            for (int i = 0; i < actions.Count; i++)
            {
                PropertiesEmoteAction newAction = new PropertiesEmoteAction();
                
                newAction.DatabaseRecordId = (uint)i+1;
                newAction.Amount = actions[i].Amount;
                newAction.Amount64 = actions[i].Amount64;
                newAction.AnglesW = actions[i].AnglesW;
                newAction.AnglesX = actions[i].AnglesX;
                newAction.AnglesY = actions[i].AnglesY;
                newAction.AnglesZ = actions[i].AnglesZ;
                newAction.Delay = actions[i].Delay;
                newAction.DestinationType = actions[i].DestinationType;
                newAction.Display = actions[i].Display;
                newAction.Extent = actions[i].Extent;
                newAction.HeroXP64 = actions[i].HeroXP64;
                newAction.Max = actions[i].Max;
                newAction.Max64 = actions[i].Max64;
                newAction.Min = actions[i].Min;
                newAction.MaxDbl = actions[i].MaxDbl;
                newAction.Message = actions[i].Message;
                newAction.Min64 = actions[i].Min64;
                newAction.MinDbl = actions[i].MinDbl;
                newAction.Motion = (MotionCommand?)actions[i].Motion;
                newAction.ObjCellId = actions[i].ObjCellId;
                newAction.OriginX = actions[i].OriginX;
                newAction.OriginY = actions[i].OriginY;
                newAction.OriginZ = actions[i].OriginZ;
                newAction.Palette = actions[i].Palette;
                newAction.Percent = actions[i].Percent;
                newAction.PScript = (PlayScript?)actions[i].PScript;
                newAction.Shade = actions[i].Shade;
                newAction.Sound = (Sound?)actions[i].Sound;
                newAction.SpellId = actions[i].SpellId;
                newAction.StackSize = actions[i].StackSize;
                newAction.TestString = actions[i].TestString;
                newAction.TreasureClass = actions[i].TreasureClass;
                newAction.TryToBond = actions[i].TryToBond;
                newAction.Type = actions[i].Type;
                newAction.WealthRating = actions[i].WealthRating;
                newAction.WeenieClassId = actions[i].WeenieClassId;

                newEmote.PropertiesEmoteAction.Add(newAction);

            }

            return AddEmote(newEmote);



        }
    }
}
