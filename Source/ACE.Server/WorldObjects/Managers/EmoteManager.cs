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

        private readonly WorldObject _worldObject;
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
                        if (creature == null)
                        {
                            log.Error($"{WorldObject.Name} ({WorldObject.Guid}) EmoteManager.CastSpell - creature is null");
                            break;
                        }

                        creature.CheckForHumanPreCast(spell);

                        var spellTarget = GetSpellTarget(spell, targetObject);

                        var preCastTime = creature.PreCastMotion(spellTarget);

                        delay = preCastTime + creature.GetPostCastTime(spell);

                        var castChain = new ActionChain();
                        castChain.AddDelaySeconds(preCastTime);
                        castChain.AddAction(creature, ActionType.EmoteManager_CastSpell, () =>
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

                case EmoteType.DropFellow:
                    if (player != null)
                    {
                        var fellowship = player.Fellowship;

                        if (fellowship != null)
                        {
                            fellowship.QuitFellowship(player, false);
                        }
                    }
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
                        motionChain.AddAction(WorldObject, ActionType.EmoteManager_Give, () => player.GiveFromEmote(WorldObject, emote.WeenieClassId ?? 0, stackSize > 0 ? stackSize : 1, emote.Palette ?? 0, emote.Shade ?? 0));
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

                /* inq questbonus amount */
                case EmoteType.QuestCompletionCount:

                    if (targetObject != null)
                    {
                        var QB = targetObject.GetProperty((PropertyInt64)emote.Stat);

                        if (QB == null && HasValidTestNoQuality(emote.Message))
                        {
                            ExecuteEmoteSet(EmoteCategory.TestNoQuality, emote.Message, targetObject, true);
                        }
                        else
                        {
                            QB ??= 0;
                            success = QB != null && QB >= (emote.Min64 ?? long.MinValue) && QB <= (emote.Max64 ?? long.MaxValue);
                            ExecuteEmoteSet(success ? EmoteCategory.TestSuccess : EmoteCategory.TestFailure, emote.Message, targetObject, true);
                        }
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
                    if (emoteSet.Category != EmoteCategory.Vendor && emoteSet.Style.HasValue)
                    {
                        if (!emoteSet.Substyle.HasValue)
                        {
                            log.Warn($"{WorldObject.Name} has an invalid motion emote with a missing Substyle value");
                            break;
                        }

                        var startingMotion = new Motion(emoteSet.Style.Value, emoteSet.Substyle.Value);
                        motion = new Motion(emoteSet.Style.Value, emote.Motion.Value, emote.Extent);

                        if (WorldObject.CurrentMotionState.Stance != startingMotion.Stance)
                        {
                            if (WorldObject.CurrentMotionState.Stance == MotionStance.Invalid)
                            {
                                if (debugMotion)
                                    Console.WriteLine($"{WorldObject.Name} running starting motion {emoteSet.Style.Value}, {emoteSet.Substyle.Value}");

                                delay = WorldObject.ExecuteMotion(startingMotion);
                            }
                        }
                        else
                        {
                            if (WorldObject.CurrentMotionState.MotionState.ForwardCommand == startingMotion.MotionState.ForwardCommand
                                    && startingMotion.Stance == MotionStance.NonCombat)     // enforce non-combat here?
                            {
                                if (WorldObject.MotionTableId == 0)
                                    break;

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
                                motionChain.AddAction(WorldObject, ActionType.EmoteManager_ExecuteMotion, () =>
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
                        if (WorldObject.MotionTableId == 0)
                            break;

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
                        motionChain.AddAction(WorldObject, ActionType.EmoteManager_ExecuteMotion, () => WorldObject.ExecuteMotion(startingMotion, false));

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
                           (emote.AnglesX != 0 || emote.AnglesY != 0 || emote.AnglesZ != 0 || emote.AnglesW != 0))
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
                        if (!player.SpendLuminance(emote.Amount64 ?? emote.HeroXP64 ?? 0))
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
                        if (QuestManager.IsDynamicQuestEligible(player))
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
                            case "Creature10":
                            case "Creature50":
                            case "Creature100":
                                {
                                    long creatureAugs = player.LuminanceAugmentCreatureCount ?? 0;

                                    // Determine the number of augmentations based on emote.Message
                                    int augCount = emote.Message == "Creature" ? 1 :
                                                   emote.Message == "Creature10" ? 10 :
                                                   emote.Message == "Creature50" ? 50 :
                                                   emote.Message == "Creature100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent; // Scaling factor per augmentation
                                    double totalCost = 0;

                                    // Calculate cumulative cost for the specified number of augmentations
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((creatureAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }

                                    // Apply cost multipliers based on the augmentation threshold
                                    double additionalMultiplier = 1.0;
                                    if (creatureAugs >= 4000)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (creatureAugs >= 2750)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 2750
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance to proceed
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentCreatureCount = creatureAugs + augCount;

                                            // Consume the correct augmentation item based on the number of augmentations
                                            int itemId = augCount == 1 ? 300005 :
                                                         augCount == 10 ? 81000125 :
                                                         augCount == 50 ? 81000135 :
                                                         augCount == 100 ? 81000145 : 0;

                                            if (itemId != 0)
                                            {
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);
                                            }

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your Creature casting abilities by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} points to your Creature spell effects. Are you sure?");
                                    }
                                }
                                break;
                            case "Item":
                            case "Item10":
                            case "Item50":
                            case "Item100":
                                {
                                    long itemAugs = player.LuminanceAugmentItemCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "Item" ? 1 :
                                                   emote.Message == "Item10" ? 10 :
                                                   emote.Message == "Item50" ? 50 :
                                                   emote.Message == "Item100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((itemAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }

                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (itemAugs >= 2000)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (itemAugs >= 1250)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 1250
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentItemCount = itemAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 300006 :
                                                         augCount == 10 ? 81000126 :
                                                         augCount == 50 ? 81000136 :
                                                         augCount == 100 ? 81000146 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your Item casting abilities by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} points to your Item spell effects. Are you sure?");
                                    }
                                }
                                break;

                            case "Life":
                            case "Life10":
                            case "Life50":
                            case "Life100":
                                {
                                    long lifeAugs = player.LuminanceAugmentLifeCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "Life" ? 1 :
                                                   emote.Message == "Life10" ? 10 :
                                                   emote.Message == "Life50" ? 50 :
                                                   emote.Message == "Life100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((lifeAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }

                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (lifeAugs >= 2000)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (lifeAugs >= 1000)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 1000
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentLifeCount = lifeAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 300007 :
                                                         augCount == 10 ? 81000127 :
                                                         augCount == 50 ? 81000137 :
                                                         augCount == 100 ? 81000147 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your Life casting abilities by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} points to your Life spell effects. Are you sure?");
                                    }
                                }
                                break;

                            case "War":
                            case "War10":
                            case "War50":
                            case "War100":
                                {
                                    long warAugs = player.LuminanceAugmentWarCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "War" ? 1 :
                                                   emote.Message == "War10" ? 10 :
                                                   emote.Message == "War50" ? 50 :
                                                   emote.Message == "War100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((warAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }

                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (warAugs >= 2500)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (warAugs >= 1750)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 1750
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentWarCount = warAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 300008 :
                                                         augCount == 10 ? 81000128 :
                                                         augCount == 50 ? 81000138 :
                                                         augCount == 100 ? 81000148 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your War casting abilities by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} points to your War spell effects. Are you sure?");
                                    }
                                }
                                break;
                            case "Void":
                            case "Void10":
                            case "Void50":
                            case "Void100":
                                {
                                    long voidAugs = player.LuminanceAugmentVoidCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "Void" ? 1 :
                                                   emote.Message == "Void10" ? 10 :
                                                   emote.Message == "Void50" ? 50 :
                                                   emote.Message == "Void100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((voidAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }
                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (voidAugs >= 2500)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (voidAugs >= 1750)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 1750
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentVoidCount = voidAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 300009 :
                                                         augCount == 10 ? 81000129 :
                                                         augCount == 50 ? 81000139 :
                                                         augCount == 100 ? 81000149 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your Void casting abilities by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} points to your Void spell effects. Are you sure?");
                                    }
                                }
                                break;
                            case "Melee":
                            case "Melee10":
                            case "Melee50":
                            case "Melee100":
                                {
                                    long meleeAugs = player.LuminanceAugmentMeleeCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "Melee" ? 1 :
                                                   emote.Message == "Melee10" ? 10 :
                                                   emote.Message == "Melee50" ? 50 :
                                                   emote.Message == "Melee100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((meleeAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }

                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (meleeAugs >= 2500)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (meleeAugs >= 1750)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 1750
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentMeleeCount = meleeAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 2003002 :
                                                         augCount == 10 ? 81000133 :
                                                         augCount == 50 ? 81000143 :
                                                         augCount == 100 ? 81000153 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your Melee casting abilities by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} points to your Melee spell effects. Are you sure?");
                                    }
                                }
                                break;
                            case "Missile":
                            case "Missile10":
                            case "Missile50":
                            case "Missile100":
                                {
                                    long missileAugs = player.LuminanceAugmentMissileCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "Missile" ? 1 :
                                                   emote.Message == "Missile10" ? 10 :
                                                   emote.Message == "Missile50" ? 50 :
                                                   emote.Message == "Missile100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((missileAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }

                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (missileAugs >= 2500)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (missileAugs >= 1750)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 1750
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentMissileCount = missileAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 2003003 :
                                                         augCount == 10 ? 81000134 :
                                                         augCount == 50 ? 81000144 :
                                                         augCount == 100 ? 81000154 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your Missile casting abilities by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} points to your Missile spell effects. Are you sure?");
                                    }
                                }
                                break;
                            case "Duration":
                            case "Duration10":
                            case "Duration50":
                            case "Duration100":
                                {
                                    long durationAugs = player.LuminanceAugmentSpellDurationCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "Duration" ? 1 :
                                                   emote.Message == "Duration10" ? 10 :
                                                   emote.Message == "Duration50" ? 50 :
                                                   emote.Message == "Duration100" ? 100 : 0;

                                    // Calculate total duration increase (e.g., 5% per augmentation)
                                    double durationIncreasePercent = augCount * 5.0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((durationAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }
                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (durationAugs >= 2000)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (durationAugs >= 1000)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 1000
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentSpellDurationCount = durationAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 300016 :
                                                         augCount == 10 ? 81000130 :
                                                         augCount == 50 ? 81000140 :
                                                         augCount == 100 ? 81000150 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your spell Duration by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {durationIncreasePercent}% to the duration of all of your spell effects. Are you sure?");
                                    }
                                }
                                break;
                            case "Specialize":
                            case "Specialize10":
                            case "Specialize50":
                            case "Specialize100":
                                {
                                    long specAugs = player.LuminanceAugmentSpecializeCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "Specialize" ? 1 :
                                                   emote.Message == "Specialize10" ? 10 :
                                                   emote.Message == "Specialize50" ? 50 :
                                                   emote.Message == "Specialize100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((specAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }

                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (specAugs >= 2000)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 2500
                                    }
                                    else if (specAugs >= 1750)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 1750
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentSpecializeCount = specAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 300021 :
                                                         augCount == 10 ? 81000131 :
                                                         augCount == 50 ? 81000141 :
                                                         augCount == 100 ? 81000151 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your Max Specialized Skill Credits by {augCount}.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} points to your max specialized skill credits. Are you sure?");
                                    }
                                }
                                break;
                            case "Summon":
                            case "Summon10":
                            case "Summon50":
                            case "Summon100":
                                {
                                    long summonAugs = player.LuminanceAugmentSummonCount ?? 0;

                                    // Define how many augmentations will be applied
                                    int augCount = emote.Message == "Summon" ? 1 :
                                                   emote.Message == "Summon10" ? 10 :
                                                   emote.Message == "Summon50" ? 50 :
                                                   emote.Message == "Summon100" ? 100 : 0;

                                    // Base cost per augmentation
                                    double baseCost = (double)emote.Amount;
                                    double percentIncrease = (double)emote.Percent;
                                    double totalCost = 0;

                                    // Calculate cumulative cost dynamically using a loop
                                    for (int i = 0; i < augCount; i++)
                                    {
                                        totalCost += baseCost + ((summonAugs + i) * (baseCost * (1 + percentIncrease)));
                                    }

                                    // Apply the cost multipliers based on the augment thresholds
                                    double additionalMultiplier = 1.0;
                                    if (summonAugs >= 4000)
                                    {
                                        additionalMultiplier = 8; // Apply 8x multiplier for augments >= 4000
                                    }
                                    else if (summonAugs >= 2750)
                                    {
                                        additionalMultiplier = 4; // Apply 4x multiplier for augments >= 2750
                                    }

                                    totalCost *= additionalMultiplier;

                                    // Check if the player has enough Luminance
                                    if (player.BankedLuminance < totalCost)
                                    {
                                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                    }
                                    else
                                    {
                                        player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
                                        {
                                            if (player.BankedLuminance < totalCost)
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to use this gem. This will require {totalCost:N0} luminance.", ChatMessageType.Broadcast));
                                                return;
                                            }
                                            if (!player.SpendLuminance((long)totalCost))
                                            {
                                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                                                return;
                                            }

                                            // Apply the augmentations
                                            player.LuminanceAugmentSummonCount = summonAugs + augCount;

                                            // Consume the correct item based on augCount
                                            int itemId = augCount == 1 ? 2003001 :
                                                         augCount == 10 ? 81000132 :
                                                         augCount == 50 ? 81000142 :
                                                         augCount == 100 ? 81000152 : 0;

                                            if (itemId != 0)
                                                player.TryConsumeFromInventoryWithNetworking((uint)itemId, 1);

                                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your summons attributes and skills by {augCount} point(s) each.", ChatMessageType.Broadcast));
                                        }), $"You are about to spend {totalCost:N0} luminance to add {augCount} point(s) to all of your summons attributes and skills. Are you sure?");
                                    }
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
                case EmoteType.GrantAttributeStat:
                    if (player != null && emote.Stat != null)
                    {
                        var amount = emote.Amount.HasValue && emote.Amount.Value > 0 ? (uint)emote.Amount.Value : 1u;
                        if (!Enum.IsDefined(typeof(PropertyAttribute), (ushort)emote.Stat.Value))
                        {
                            log.Warn($"GrantAttributeStat: Unknown attribute id {emote.Stat.Value} for {WorldObject?.Name ?? "unknown"}.");
                            break;
                        }

                        var attribute = (PropertyAttribute)emote.Stat.Value;
                        var grantSucceeded = player.GrantFreeAttributeRanks(attribute, amount);
                        var targetName = attribute.GetDescription();

                        if (grantSucceeded)
                            player.Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Your base {targetName} has been increased by {amount}.", ChatMessageType.Advancement));
                        else
                            player.Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Unable to increase {targetName}.", ChatMessageType.System));
                    }
                    break;
                case EmoteType.GrantVitalStat:
                    if (player != null && emote.Stat != null)
                    {
                        var amount = emote.Amount.HasValue && emote.Amount.Value > 0 ? (uint)emote.Amount.Value : 1u;
                        if (!Enum.IsDefined(typeof(PropertyAttribute2nd), (ushort)emote.Stat.Value))
                        {
                            log.Warn($"GrantVitalStat: Unknown vital id {emote.Stat.Value} for {WorldObject?.Name ?? "unknown"}.");
                            break;
                        }

                        var vital = (PropertyAttribute2nd)emote.Stat.Value;
                        if (vital != PropertyAttribute2nd.MaxHealth && vital != PropertyAttribute2nd.MaxStamina && vital != PropertyAttribute2nd.MaxMana)
                        {
                            log.Warn($"GrantVitalStat: Vital {vital} is not supported for innate grants.");
                            break;
                        }

                        var grantSucceeded = player.GrantFreeVitalRanks(vital, amount);
                        var targetName = vital.GetDescription();

                        if (grantSucceeded)
                            player.Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Your base {targetName} has been increased by {amount}.", ChatMessageType.Advancement));
                        else
                            player.Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Unable to increase {targetName}.", ChatMessageType.System));
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
                if (!emoteSet.Any() && _worldObject.Biota.DynamicEmoteList != null) //pre-refresh dynamic quests
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
                    actionChain.AddAction(WorldObject, ActionType.EmoteManager_DebugDelay, () => Console.Write($"{emote.Delay} - "));

                // delay = post-delay from actual time of previous emote
                // emote.Delay = pre-delay for current emote
                actionChain.AddDelaySeconds(delay + emote.Delay);

                actionChain.AddAction(WorldObject, ActionType.EmoteManager_DoEnqueue, () => DoEnqueue(emoteSet, targetObject, emoteIdx, emote));
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
                Console.WriteLine($" - {nextDelay}");

            if (emoteIdx < emoteSet.PropertiesEmoteAction.Count - 1)
                Enqueue(emoteSet, targetObject, emoteIdx + 1, nextDelay);
            else
            {
                if (nextDelay > 0)
                {
                    var delayChain = new ActionChain();
                    delayChain.AddDelaySeconds(nextDelay);
                    delayChain.AddAction(WorldObject, ActionType.EmoteManager_ReduceNested, () =>
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

        private static bool EmoteIsBranchingType(PropertiesEmoteAction emote)
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
            var embeddedQuestName = result.Contains('@') ? message.Split("@")[0] : null;
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

                result = result.Replace("%tqm", !string.IsNullOrWhiteSpace(quest) ? QuestManager.GetMaxSolves(questName).ToString() : "");

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
            if (emote == null)
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

                newAction.DatabaseRecordId = (uint)i + 1;
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
