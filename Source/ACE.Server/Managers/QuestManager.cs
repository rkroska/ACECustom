using System;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Entity;
using MySqlX.XDevAPI;
using System.Drawing;
using ACE.Server.Entity.Actions;
using ACE.Database.Models.Auth;
using System.Reactive;
using ACE.Entity.Adapter;
using ACE.Entity.Enum.Properties;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Managers
{
    public class QuestManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// This is almost always a Player
        /// 
        /// however there are some rare cases of Creatures having quests
        /// such as 'chickencrossingroad'
        /// </summary>
        public Creature Creature { get; }

        public Fellowship Fellowship { get; }

        private ICollection<CharacterPropertiesQuestRegistry> runtimeQuests { get; set; } = new HashSet<CharacterPropertiesQuestRegistry>();

        public string Name
        {
            get
            {
                if (Creature != null)
                    return Creature.Name;
                else
                    return $"Fellowship({Fellowship.FellowshipName})";
            }

        }
        public uint IDtoUseForQuestRegistry
        {
            get
            {
                if (Creature != null)
                    return Creature.Guid.Full;
                else
                    return 1;
                //return Fellowship.FellowshipLeaderGuid;
            }
        }

        private static readonly bool Debug = false;

        /// <summary>
        /// Constructs a new QuestManager for a Player / Creature
        /// </summary>
        public QuestManager(Creature creature)
        {
            Creature = creature;
        }

        /// <summary>
        /// Constructs a new QuestManager for a Fellowship
        /// </summary>
        public QuestManager(Fellowship fellowship)
        {
            Fellowship = fellowship;
        }

        /// <summary>
        /// This will return a clone of the quests collection. You should not mutate the results.
        /// This is mostly used for information/debugging
        /// </summary>
        /// <returns></returns>
        public ICollection<CharacterPropertiesQuestRegistry> GetQuests()
        {
            if (Creature is Player player)
                return player.Character.GetQuests(player.CharacterDatabaseLock);

            // Not a player
            return runtimeQuests;
        }

        public ICollection<CharacterPropertiesQuestRegistry> GetDynamicQuests()
        {           
            return GetQuests().Where(q=>q.QuestName.StartsWith("Dynamic_")).ToList();
        }

        /// <summary>
        /// Returns TRUE if a player has started a particular quest
        /// </summary>
        public bool HasQuest(string questFormat)
        {
            var questName = GetQuestName(questFormat);
            var Quest = GetQuest(questName);
            var hasQuest = (Quest != null && Quest.NumTimesCompleted >= 0);

            if (Debug)
                Console.WriteLine($"{Name}.QuestManager.HasQuest({questFormat}): {hasQuest}");

            return hasQuest;
        }

        public bool HasQuestCompletes(string questName)
        {
            if (Debug) Console.WriteLine($"{Name}.QuestManager.HasQuestCompletes({questName})");

            if (!questName.Contains('@'))
                return HasQuest(questName);

            var pieces = questName.Split('@');
            if (pieces.Length != 2)
            {
                Console.WriteLine($"{Name}.QuestManager.HasQuestCompletes({questName}): error parsing quest name");
                return false;
            }
            var name = pieces[0];
            if (!Int32.TryParse(pieces[1], out var numCompletes))
            {
                Console.WriteLine($"{Name}.QuestManager.HasQuestCompletes({questName}): unknown quest format");
                return HasQuest(questName);
            }
            var quest = GetQuest(name);
            if (quest == null)
                return false;

            var success = quest.NumTimesCompleted == numCompletes;     // minimum or exact?
            if (Debug) Console.WriteLine(success);
            return success;
        }

        /// <summary>
        /// Returns an active or completed quest for this player
        /// </summary>
        public CharacterPropertiesQuestRegistry GetQuest(string questName)
        {
            if (Creature is Player player)
                return player.Character.GetQuest(questName, player.CharacterDatabaseLock);

            // Not a player
            return runtimeQuests.FirstOrDefault(q => q.QuestName.Equals(questName, StringComparison.OrdinalIgnoreCase));
        }

        private CharacterPropertiesQuestRegistry GetOrCreateQuest(string questName, out bool questRegistryWasCreated)
        {
            if (Creature is Player player)
            {
                CharacterPropertiesQuestRegistry r = player.Character.GetOrCreateQuest(questName, player.CharacterDatabaseLock, out questRegistryWasCreated);
                //var quest = DatabaseManager.World.GetCachedQuest(questName);
                return r;
            }


            // Not a player
            var existing = runtimeQuests.FirstOrDefault(q => q.QuestName.Equals(questName, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new CharacterPropertiesQuestRegistry
                {
                    QuestName = questName,
                };

                runtimeQuests.Add(existing);

                questRegistryWasCreated = true;

            }
            else
                questRegistryWasCreated = false;

            return existing;
        }

        /// <summary>
        /// Adds or updates a quest completion to the player's registry
        /// </summary>
        public void Update(string questFormat)
        {
            var questName = GetQuestName(questFormat);

            var quest = GetOrCreateQuest(questName, out var questRegistryWasCreated);

            if (questRegistryWasCreated)
            {
                quest.LastTimeCompleted = (uint)Time.GetUnixTime();
                quest.NumTimesCompleted = 1; // initial add / first solve

                quest.CharacterId = IDtoUseForQuestRegistry;

                if (Debug) Console.WriteLine($"{Name}.QuestManager.Update({quest}): added quest");

                if (Creature is Player player)
                {
                    player.CharacterChangesDetected = true;
                    bool isStamp = false; bool isCompletion = false;
                    UpdatePlayerQuestCompletions(player, questFormat, out isStamp, out isCompletion, (uint)quest.NumTimesCompleted);
                    if (isStamp)
                    {
                        player.SendMessage($"You've stamped {questName}!", ChatMessageType.Advancement);//quest name
                    }
                    if (isCompletion)
                    {
                        player.SendMessage($"You've stamped {questName} on first completion!", ChatMessageType.Advancement);//quest name
                    }

                    player.ContractManager.NotifyOfQuestUpdate(quest.QuestName);
                }
            }
            else
            {
                if (IsMaxSolves(questName))
                {
                    if (Debug) Console.WriteLine($"{Name}.QuestManager.Update({quest}): can not update existing quest. IsMaxSolves({questName}) is true.");
                    return;
                }

                // update existing quest
                quest.LastTimeCompleted = (uint)Time.GetUnixTime();
                quest.NumTimesCompleted++;

                if (Debug) Console.WriteLine($"{Name}.QuestManager.Update({quest}): updated quest ({quest.NumTimesCompleted})");

                if (Creature is Player player)
                {
                    player.CharacterChangesDetected = true;

                    if (quest.NumTimesCompleted == 1)
                    {
                        bool isStamp = false; bool isCompletion = false;
                        UpdatePlayerQuestCompletions(player, questFormat, out isStamp, out isCompletion, (uint)quest.NumTimesCompleted);
                        if (isStamp)
                        {
                            player.SendMessage($"You've stamped {questName} on completion!", ChatMessageType.Advancement);//quest name
                        }
                        if (isCompletion)
                        {
                            player.SendMessage($"You've stamped {questName} on first completion!", ChatMessageType.Advancement);//quest name
                        }
                    }

                    player.ContractManager.NotifyOfQuestUpdate(quest.QuestName);
                }
            }
        }

        /// <summary>
        /// Initialize a quest completion with the provided number to the player's registry
        /// </summary>
        public void SetQuestCompletions(string questFormat, int questCompletions = 0)
        {
            var questName = GetQuestName(questFormat);

            var maxSolves = GetMaxSolves(questName);

            var numTimesCompleted = maxSolves > -1 ? Math.Min(questCompletions, maxSolves) : Math.Abs(questCompletions);

            var quest = GetOrCreateQuest(questName, out var questRegistryWasCreated);

            if (questRegistryWasCreated)
            {
                quest.LastTimeCompleted = (uint)Time.GetUnixTime();
                quest.NumTimesCompleted = numTimesCompleted; // initialize the quest to the given completions

                quest.CharacterId = IDtoUseForQuestRegistry;

                if (Debug) Console.WriteLine($"{Name}.QuestManager.SetQuestCompletions({questFormat}): initialized quest to {quest.NumTimesCompleted}");

                if (Creature is Player player)
                {
                    player.CharacterChangesDetected = true;
                    bool isStamp = false; bool isCompletion = false;
                    UpdatePlayerQuestCompletions(player, questFormat, out isStamp, out isCompletion, (uint)numTimesCompleted);
                    if (isStamp)
                    {
                        player.SendMessage($"You've stamped {questName}!", ChatMessageType.Advancement);//quest name
                    }
                    if (isCompletion)
                    {
                        player.SendMessage($"You've stamped {questName} on first completion!", ChatMessageType.Advancement);//quest name
                    }
                    player.ContractManager.NotifyOfQuestUpdate(quest.QuestName);

                }

            }
            else
            {
                // update existing quest
                quest.LastTimeCompleted = (uint)Time.GetUnixTime();
                quest.NumTimesCompleted = numTimesCompleted;

                if (Debug) Console.WriteLine($"{Name}.QuestManager.SetQuestCompletions({questFormat}): initialized quest to {quest.NumTimesCompleted}");

                if (Creature is Player player)
                {
                    player.CharacterChangesDetected = true;
                    if (quest.NumTimesCompleted == 1)
                    {
                        bool isStamp = false; bool isCompletion = false;
                        UpdatePlayerQuestCompletions(player, questFormat, out isStamp, out isCompletion, (uint)numTimesCompleted);
                        if (isStamp)
                        {
                            player.SendMessage($"You've stamped {questName}!", ChatMessageType.Advancement);//quest name
                        }
                        if (isCompletion)
                        {
                            player.SendMessage($"You've stamped {questName} on first completion!", ChatMessageType.Advancement);//quest name
                        }
                    }
                    player.ContractManager.NotifyOfQuestUpdate(quest.QuestName);
                }
            }
        }

        /// <summary>
        /// Returns TRUE if player can solve this quest now
        /// </summary>
        public bool CanSolve(string questFormat)
        {
            var questName = GetQuestName(questFormat);

            // verify max solves / quest timer
            var nextSolveTime = GetNextSolveTime(questName);

            var canSolve = nextSolveTime == TimeSpan.MinValue;
            if (Debug) Console.WriteLine($"{Name}.QuestManager.CanSolve({questName}): {canSolve}");
            return canSolve;
        }

        /// <summary>
        /// Returns TRUE if player has reached the maximum # of solves for this quest
        /// </summary>
        public bool IsMaxSolves(string questName)
        {
            var quest = DatabaseManager.World.GetCachedQuest(questName);
            if (quest == null) return false;

            var playerQuest = GetQuest(questName);
            if (playerQuest == null) return false;  // player hasn't completed this quest yet

            // return TRUE if quest has solve limit, and it has been reached
            return quest.MaxSolves > -1 && playerQuest.NumTimesCompleted >= quest.MaxSolves;
        }

        /// <summary>
        /// Returns the maximum # of solves for this quest
        /// </summary>
        public static int GetMaxSolves(string questFormat)
        {
            var questName = GetQuestName(questFormat);

            var quest = DatabaseManager.World.GetCachedQuest(questName);
            if (quest == null) return 0;

            return quest.MaxSolves;
        }

        /// <summary>
        /// Returns the current # of solves for this quest
        /// </summary>
        public int GetCurrentSolves(string questFormat)
        {
            var questName = GetQuestName(questFormat);

            var quest = GetQuest(questName);
            if (quest == null) return 0;

            return quest.NumTimesCompleted;
        }

        /// <summary>
        /// Some quests we do not want to scale MinDelta if "quest_mindelta_rate" has been set.
        /// They may be things that are races against time, like Colo
        /// </summary>
        public static bool CanScaleQuestMinDelta(Database.Models.World.Quest quest)
        {
            if (quest.Name.StartsWith("ColoArena"))
                return false;

            return true;
        }

        /// <summary>
        /// Returns the time remaining until the player can solve this quest again
        /// </summary>
        public TimeSpan GetNextSolveTime(string questFormat)
        {
            var questName = GetQuestName(questFormat);

            var quest = DatabaseManager.World.GetCachedQuest(questName);
            if (quest == null)
                return TimeSpan.MaxValue;   // world quest not found - cannot solve it

            var playerQuest = GetQuest(questName);
            if (playerQuest == null)
                return TimeSpan.MinValue;   // player hasn't completed this quest yet - can solve immediately

            if (quest.MaxSolves > -1 && playerQuest.NumTimesCompleted >= quest.MaxSolves)
                return TimeSpan.MaxValue;   // cannot solve this quest again - max solves reached / exceeded

            var currentTime = (uint)Time.GetUnixTime();
            uint nextSolveTime;

            if (CanScaleQuestMinDelta(quest))
                nextSolveTime = playerQuest.LastTimeCompleted + (uint)(quest.MinDelta * PropertyManager.GetDouble("quest_mindelta_rate", 1));
            else
                nextSolveTime = playerQuest.LastTimeCompleted + quest.MinDelta;

            if (currentTime >= nextSolveTime)
                return TimeSpan.MinValue;   // can solve again now - next solve time expired

            // return the time remaining on the player's quest timer
            return TimeSpan.FromSeconds(nextSolveTime - currentTime);
        }

        /// <summary>
        /// Increment the number of times completed for a quest
        /// </summary>
        public void Increment(string questName, int amount = 1)
        {
            for (var i = 0; i < amount; i++)
                Update(questName);
        }

        /// <summary>
        /// Decrement the number of times completed for a quest
        /// </summary>
        public void Decrement(string quest, int amount = 1)
        {
            var questName = GetQuestName(quest);

            var existing = GetQuest(questName);

            if (existing != null)
            {
                //if (existing.NumTimesCompleted == 0)
                //{
                //    if (Debug) Console.WriteLine($"{Name}.QuestManager.Decrement({quest}): can not Decrement existing quest. {questName}.NumTimesCompleted is already 0.");
                //    return;
                //}

                // update existing quest
                existing.LastTimeCompleted = (uint)Time.GetUnixTime();
                existing.NumTimesCompleted -= amount;

                if (Debug) Console.WriteLine($"{Name}.QuestManager.Decrement({quest}): updated quest ({existing.NumTimesCompleted})");

                if (Creature is Player player)
                {
                    player.CharacterChangesDetected = true;
                    player.ContractManager.NotifyOfQuestUpdate(existing.QuestName);
                }
            }
        }

        /// <summary>
        /// Removes an existing quest from the Player's registry
        /// </summary>
        public void Erase(string questFormat)
        {
            if (Debug)
                Console.WriteLine($"{Name}.QuestManager.Erase({questFormat})");

            var questName = GetQuestName(questFormat);

            if (Creature is Player player)
            {
                if (player.Character.EraseQuest(questName, player.CharacterDatabaseLock))
                {
                    player.CharacterChangesDetected = true;

                    player.ContractManager.NotifyOfQuestUpdate(questName);
                }
            }
            else
            {
                // Not a player
                var quests = runtimeQuests.Where(q => q.QuestName.Equals(questName, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var quest in quests)
                    runtimeQuests.Remove(quest);
            }
        }

        /// <summary>
        /// Removes an all quests from registry
        /// </summary>
        public void EraseAll()
        {
            if (Debug)
                Console.WriteLine($"{Name}.QuestManager.EraseAll");

            if (Creature is Player player)
            {
                player.Character.EraseAllQuests(out var questNamesErased, player.CharacterDatabaseLock);

                if (questNamesErased.Count > 0)
                {
                    player.CharacterChangesDetected = true;

                    foreach (var questName in questNamesErased)
                        player.ContractManager.NotifyOfQuestUpdate(questName);
                }
            }
            else
            {
                // Not a player
                runtimeQuests.Clear();
            }
        }

        /// <summary>
        /// Shows the current quests in progress for a Player
        /// </summary>
        public void ShowQuests(Player player)
        {
            Console.WriteLine("ShowQuests");

            var quests = GetQuests();

            if (quests.Count == 0)
            {
                Console.WriteLine("No quests in progress for " + Name);
                return;
            }

            foreach (var quest in quests)
            {
                Console.WriteLine("Quest Name: " + quest.QuestName);
                Console.WriteLine("Times Completed: " + quest.NumTimesCompleted);
                Console.WriteLine("Last Time Completed: " + quest.LastTimeCompleted);
                Console.WriteLine("Player ID: " + quest.CharacterId.ToString("X8"));
                Console.WriteLine("----");
            }
        }

        public void Stamp(string questFormat)
        {
            var questName = GetQuestName(questFormat);
            Update(questName);  // ??
        }

        /// <summary>
        /// Returns the quest name without the @ comment
        /// </summary>
        /// <param name="questFormat">A quest name with an optional @comment on the end</param>
        public static string GetQuestName(string questFormat)
        {
            var idx = questFormat.IndexOf('@');     // strip comment
            if (idx == -1)
                return questFormat;

            var questName = questFormat.Substring(0, idx);
            return questName;
        }

        /// <summary>
        /// Returns TRUE if player has solved this quest between min-max times
        /// </summary>
        public bool HasQuestSolves(string questFormat, int? _min, int? _max)
        {
            var questName = GetQuestName(questFormat);    // strip optional @comment

            var quest = GetQuest(questName);
            var numSolves = quest != null ? quest.NumTimesCompleted : 0;

            int min = _min ?? int.MinValue;    // use defaults?
            int max = _max ?? int.MaxValue;

            var hasQuestSolves = numSolves >= min && numSolves <= max;    // verify: can either of these be -1?
            if (Debug)
                Console.WriteLine($"{Name}.QuestManager.HasQuestSolves({questFormat}, {_min}, {_max}): {hasQuestSolves}");

            return hasQuestSolves;
        }

        /// <summary>
        /// Called when a player hasn't started a quest yet
        /// </summary>
        public void HandleNoQuestError(WorldObject wo)
        {
            var player = Creature as Player;

            if (player == null) return;

            var error = new GameEventInventoryServerSaveFailed(player.Session, wo.Guid.Full, WeenieError.ItemRequiresQuestToBePickedUp);
            player.Session.Network.EnqueueSend(error);
        }

        public void HandlePortalQuestError(string questName)
        {
            var player = Creature as Player;

            if (player == null) return;

            if (!HasQuest(questName))
            {
                player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouMustCompleteQuestToUsePortal));
            }
            else if (CanSolve(questName))
            {
                var error = new GameEventWeenieError(player.Session, WeenieError.QuestSolvedTooLongAgo);
                var text = new GameMessageSystemChat("You completed the quest this portal requires too long ago!", ChatMessageType.Magic); // This msg wasn't sent in retail PCAP, leading to a completely silent fail when using the portal with an expired flag.
                player.Session.Network.EnqueueSend(text, error);
            }
        }

        /// <summary>
        /// Called when either the player has completed the quest too recently, or max solves has been reached.
        /// </summary>
        public void HandleSolveError(string questName)
        {
            var player = Creature as Player;
            if (player == null) return;

            if (IsMaxSolves(questName))
            {
                var error = new GameEventInventoryServerSaveFailed(player.Session, 0, WeenieError.YouHaveSolvedThisQuestTooManyTimes);
                var text = new GameMessageSystemChat("You have solved this quest too many times!", ChatMessageType.Broadcast);
                player.Session.Network.EnqueueSend(text, error);
            }
            else
            {
                var error = new GameEventInventoryServerSaveFailed(player.Session, 0, WeenieError.YouHaveSolvedThisQuestTooRecently);
                var text = new GameMessageSystemChat("You have solved this quest too recently!", ChatMessageType.Broadcast);

                var remainStr = GetNextSolveTime(questName).GetFriendlyString();
                var remain = new GameMessageSystemChat($"You may complete this quest again in {remainStr}.", ChatMessageType.Broadcast);
                player.Session.Network.EnqueueSend(text, remain, error);
            }
        }

        /// <summary>
        /// Increments the counter for a kill task for a player
        /// </summary>
        public void HandleKillTask(string killQuestName, WorldObject killedCreature)
        {
            var player = Creature as Player;
            if (player == null) return;

            // http://acpedia.org/wiki/Announcements_-_2012/12_-_A_Growing_Twilight#Release_Notes

            if (killedCreature == null)
            {
                log.Error($"{Name}.QuestManager.HandleKillTask({killQuestName}): input object is null!");
                return;
            }

            var questName = GetQuestName(killQuestName);
            var quest = DatabaseManager.World.GetCachedQuest(questName);

            if (quest == null)
            {
                log.Error($"{Name}.QuestManager.HandleKillTask({killQuestName}): couldn't find kill task {questName} in database");
                return;
            }

            if (!HasQuest(questName))
                return;

            Stamp(killQuestName);

            var playerQuest = GetQuest(questName);

            if (playerQuest == null)
            {
                // this should be impossible
                log.Error($"{Name}.QuestManager.HandleKillTask({killQuestName}): couldn't find kill task {questName} in player quests");
                return;
            }

            var msg = $"You have killed {playerQuest.NumTimesCompleted} {killedCreature.GetPluralName()}!";

            if (IsMaxSolves(questName))
                msg += $" Your task is complete!";
            else
                msg += $" You must kill {quest.MaxSolves} to complete your task.";

            player.Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz!@#$^&";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[ThreadSafeRandom.Next(0, s.Length - 1)]).ToArray());
        }

        private static bool WeenieHasDynamicQuest(Weenie npcTarget)
        {
            if (npcTarget == null || npcTarget.PropertiesEmote == null)
            {
                return false;
            }
            return npcTarget.PropertiesEmote.Any(x => x.Quest?.StartsWith("Dynamic") == true);
        }

        private static string DynamicQuestFlagName = "dynamicQuestFlag";

        public void ComputeDynamicQuest(string questName, Network.Session session, bool test)
        {
            if (string.IsNullOrEmpty(questName) || !questName.Contains("Dynamic"))
            {
                return;
            }
            if (session.Player != null)
            {
                session.Player.QuestManager.Erase(DynamicQuestFlagName);
            }

            bool created = false;
            var player = Creature as Player;
            int? Variation = player.Location.Variation;

            //create random note paper - start to fill it with the details of the quest. place it in inventory at the end
            //weenie class 365 - Parchment -- Create Parchment world object
            //find target NPC
            WorldDatabaseWithEntityCache world = new WorldDatabaseWithEntityCache();
            Weenie npcTarget = world.GetCachedWeenie(world.GetRandomNPCWeenieIDFromWhitelist());
          
            if (npcTarget == null)
            {
                return;
            }
            int i = 0;
            while (WeenieHasDynamicQuest(npcTarget))
            {
                i++;
                npcTarget = world.GetCachedWeenie(world.GetRandomNPCWeenieIDFromWhitelist());
                if (i > 50) // Don't try forever
                {
                    break;
                }
            }
            if (test)
            {
                npcTarget = world.GetCachedWeenie(6873);//Ulgrim
            }

            using (ACE.Database.Models.World.WorldDbContext context = new ACE.Database.Models.World.WorldDbContext())
            {
                //find an item for the target NPC to give player - create emote to give it to player
                Weenie itemTargetWeenie = world.GetRandomEquippableItem();

                // Exclude Rares
                while (itemTargetWeenie.PropertiesInt.Any(x => x.Key == PropertyInt.RareId))
                {
                    itemTargetWeenie = world.GetRandomEquippableItem();
                }
                var itemTargetWorldObject = WorldObjectFactory.CreateWorldObject(itemTargetWeenie, GuidManager.NewDynamicGuid());

                var npcTargetWorldObject = WorldObjectFactory.CreateWorldObject(npcTarget, GuidManager.NewDynamicGuid());

                //this forms a basic fetch from NPC quest fashion - check if it should be also a delivery
                bool chainDelivery = false;
                int randChance = ThreadSafeRandom.Next(0, 10);
                if (randChance > 2) // chain delivery on 80% of the time
                {
                    chainDelivery = true;
                }

                WorldObject woDeliveryTarget = null;
                Weenie npcDeliveryTarget = null;
                Database.Models.World.Weenie woWeenieDelivery = null;

                var noteMessage = $"Get {itemTargetWorldObject.Name} from {npcTargetWorldObject.Name}. Provide them this note as proof of your errand. ";
                if (chainDelivery)
                {
                    npcDeliveryTarget = world.GetCachedWeenie(world.GetRandomNPCWeenieIDFromWhitelist(npcTargetWorldObject.WeenieClassId));
                    if (npcDeliveryTarget == null)
                    {
                        chainDelivery = false;
                    }
                    else
                    {
                        i = 0;
                        while (WeenieHasDynamicQuest(npcDeliveryTarget))
                        {
                            i++;
                            npcTarget = world.GetCachedWeenie(world.GetRandomNPCWeenieIDFromWhitelist(npcTargetWorldObject.WeenieClassId));
                            if (i > 50) // Don't try forever
                            {
                                break;
                            }
                        }
                        if (test)
                        {
                            npcDeliveryTarget = world.GetCachedWeenie(28690); //Erik Festus, Ayan
                        }
                        woDeliveryTarget = WorldObjectFactory.CreateWorldObject(npcDeliveryTarget, GuidManager.NewDynamicGuid());
                        woWeenieDelivery = context.Weenie.Where(x => x.ClassId == npcDeliveryTarget.WeenieClassId).FirstOrDefault();
                        noteMessage += $"Once you've received the {itemTargetWorldObject.Name}, bring it to {woDeliveryTarget.Name}. You will be rewarded handsomely once this has been completed.";
                    }
                }

                var note = WorldObjectFactory.CreateNewWorldObject(365); //parchment
                Book b = (note as Book); int index = 0;
                b.AddPage(0, "Quest", "", true, noteMessage, out index);
                //note.SetProperty(ACE.Entity.Enum.Properties.PropertyString.Inscription, $"Get {it.Name} from {wo.Name}");

                if (!player.TryCreateInInventoryWithNetworking(note))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough pack space, free at least one inventory slot...", ChatMessageType.Tell));
                    return;
                }

                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You receive an interesting note with an errand...", ChatMessageType.Tell));
                Database.Models.World.Weenie databaseWeenie = context.Weenie.Where(x => x.ClassId == npcTarget.WeenieClassId).FirstOrDefault();
                if (databaseWeenie == null) { return; }

                int ranStrLen = ThreadSafeRandom.Next(5, 15);
                questName += "_" + RandomString(ranStrLen - 1);

                var quest = GetOrCreateQuest(questName, out created);
                if (quest == null) { return; }
                if (!created) { return; }

                // Refuse Item, check Generic Flag
                var responseEmote = NewStarterEmote(questName, databaseWeenie);

                // If Generic Flag is Set, No Reward
                var checkOtherQuestEmote = CheckOtherQuestEmote(databaseWeenie);

                // If Generic Flag is not Set, Continue
                var continueEmote = ContinueEmote(questName, databaseWeenie);

                //Basic ends with InqQuest - to respond to those already finished, create quest success as "Already done" emote.
                var completeEmote = NewAlreadyCompleteEmote(questName, databaseWeenie);

                //Basic ends with InqQuest - to add rewards - create emote with Quest Failure
                var finishedEmote = NewQuestFinishedEmote(questName, databaseWeenie, itemTargetWorldObject.WeenieClassId);

                if (chainDelivery)
                {
                    var deliveryEmote = NewStarterDyanmicEmote(questName + "_d", woWeenieDelivery, itemTargetWorldObject.WeenieClassId, true);

                    //Basic ends with InqQuest - to respond to those already finished, create quest success as "Already done" emote.
                    var deliverycompleteEmote = NewAlreadyCompleteEmote(questName + "_d", woWeenieDelivery);

                    //Basic ends with InqQuest - to add rewards - create emote with Quest Failure
                    var deliveryfinishedEmote = NewQuestFinishedEmote(questName + "_d", woWeenieDelivery, GetSpecialWeenieReward());

                    woWeenieDelivery.WeeniePropertiesEmote.Add(deliveryEmote);
                    woWeenieDelivery.WeeniePropertiesEmote.Add(deliverycompleteEmote);
                    woWeenieDelivery.WeeniePropertiesEmote.Add(deliveryfinishedEmote);

                    woDeliveryTarget.EmoteManager.AddEmote(deliveryEmote);
                    woDeliveryTarget.EmoteManager.AddEmote(deliverycompleteEmote);
                    woDeliveryTarget.EmoteManager.AddEmote(deliveryfinishedEmote);
                }

                databaseWeenie.WeeniePropertiesEmote.Add(responseEmote);
                databaseWeenie.WeeniePropertiesEmote.Add(checkOtherQuestEmote);
                databaseWeenie.WeeniePropertiesEmote.Add(continueEmote);
                databaseWeenie.WeeniePropertiesEmote.Add(completeEmote);
                databaseWeenie.WeeniePropertiesEmote.Add(finishedEmote);

                context.SaveChanges();

                //find the NPC - ? npcTargetWorldObject.Guid;
                //load the new emotes onto the biota
                //look at converters ACE.Entity/Adapter/WeenieConverter.cs

                npcTargetWorldObject.EmoteManager.AddEmote(responseEmote);
                npcTargetWorldObject.EmoteManager.AddEmote(checkOtherQuestEmote);
                npcTargetWorldObject.EmoteManager.AddEmote(continueEmote);
                npcTargetWorldObject.EmoteManager.AddEmote(completeEmote);
                npcTargetWorldObject.EmoteManager.AddEmote(finishedEmote);

                DatabaseManager.World.ClearCachedWeenie(npcTargetWorldObject.WeenieClassId);
                if (chainDelivery)
                {
                    DatabaseManager.World.ClearCachedWeenie(npcDeliveryTarget.WeenieClassId);
                }

                //DatabaseManager.Shard.SaveBiota(npcTargetWorldObject.Biota, new System.Threading.ReaderWriterLockSlim(), null);

                //DatabaseManager.Shard.SaveBiota(npcTargetWorldObject.Biota, new System.Threading.ReaderWriterLockSlim(), null);

                //npcTargetWorldObject.SaveBiotaToDatabase();

                //npcTargetWorldObject.Destroy();

                //npcTargetWorldObject = WorldObjectFactory.CreateWorldObject(npcTarget, GuidManager.NewDynamicGuid());
                //npcTargetWorldObject.EnterWorld();


                //reload landblock if it's already loaded
                //destroy all non - player server objects
                PropertiesPosition propPos = null;
                if (npcTarget != null && npcTarget.PropertiesPosition != null)
                {
                    foreach (var item in Enum.GetValues(typeof(PositionType)))
                    {
                        bool found = npcTarget.PropertiesPosition.TryGetValue((PositionType)item, out propPos);

                        if (found && propPos != null)
                        {
                            LandblockId p = new LandblockId(propPos.ObjCellId);
                          
                            Landblock L = LandblockManager.GetLandblock(p, false, Variation);
                            if (L != null)
                            {
                                L.DestroyAllNonPlayerObjects();

                                //DatabaseManager.Shard.SaveBiota //try this?
                                // clear landblock cache
                              
                                DatabaseManager.World.ClearCachedInstancesByLandblock((ushort)L.Id.Raw, Variation);

                                // reload landblock
                                L.Init(Variation, true);
                              
                                //var actionChain = new ActionChain();
                                //actionChain.AddDelayForOneTick();
                                //actionChain.AddAction(session.Player, () =>
                                //{
                                //    L.Init(true);
                                //});
                                //actionChain.EnqueueChain();
                            }
                            break;
                        }
                    }
                }

                if (chainDelivery && npcDeliveryTarget != null && npcDeliveryTarget.PropertiesPosition != null)
                {
                    LandblockId p2;
                    foreach (var item2 in Enum.GetValues(typeof(PositionType)))
                    {
                        PropertiesPosition propPos2 = null;
                        bool found2 = npcDeliveryTarget.PropertiesPosition.TryGetValue((PositionType)item2, out propPos2);

                        if (found2 && propPos2 != null)
                        {
                            if (propPos == null || propPos.ObjCellId != propPos2.ObjCellId)
                            {
                                p2 = new LandblockId(propPos2.ObjCellId);
                                Landblock L2 = LandblockManager.GetLandblock(p2, false, Variation);
                                if (L2 != null)
                                {
                                    L2.DestroyAllNonPlayerObjects();
                                    DatabaseManager.World.ClearCachedInstancesByLandblock((ushort)L2.Id.Raw, Variation);
                                    L2.Init(Variation, true);
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }

        private static Database.Models.World.WeeniePropertiesEmote CheckOtherQuestEmote(Database.Models.World.Weenie targetNPCWeenie)
        {
            Database.Models.World.WeeniePropertiesEmote responseCompletedOtherEmote = new Database.Models.World.WeeniePropertiesEmote
            {
                Object = targetNPCWeenie,
                Category = (uint)EmoteCategory.QuestSuccess,
                Probability = 1,
                WeenieClassId = null,
                Style = null,
                Substyle = null,
                Quest = DynamicQuestFlagName,
                VendorType = (int?)VendorType.Undef,
                MinHealth = 0,
                MaxHealth = null,
            };

            Database.Models.World.WeeniePropertiesEmoteAction responseCompletedOtherAction1 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseCompletedOtherEmote,
                Type = (uint)EmoteType.Tell,
                Order = 0,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = $"Hmm.. Looks like you've already completed one of these quests today, try again tomorrow.",
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            responseCompletedOtherEmote.WeeniePropertiesEmoteAction.Add(responseCompletedOtherAction1);
            return responseCompletedOtherEmote;
        }

        private static Database.Models.World.WeeniePropertiesEmote ContinueEmote(string questName, Database.Models.World.Weenie targetNPCWeenie)
        {
            Database.Models.World.WeeniePropertiesEmote responseContinueEmote = new Database.Models.World.WeeniePropertiesEmote
            {
                Object = targetNPCWeenie,
                Category = (uint)EmoteCategory.QuestFailure,
                Probability = 1,
                WeenieClassId = null,
                Style = null,
                Substyle = null,
                Quest = DynamicQuestFlagName,
                VendorType = (int?)VendorType.Undef,
                MinHealth = 0,
                MaxHealth = null,
            };

            Database.Models.World.WeeniePropertiesEmoteAction responseContinueAction1 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseContinueEmote,
                Type = (uint)EmoteType.InqQuestSolves,
                Order = 0,
                Delay = 0,
                Extent = 0,
                Motion = null,
                Message = questName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,
                Max = 1,
                Min = 1

            };

            responseContinueEmote.WeeniePropertiesEmoteAction.Add(responseContinueAction1);
            return responseContinueEmote;
        }

        private static Database.Models.World.WeeniePropertiesEmote NewQuestFinishedEmote(string questName, Database.Models.World.Weenie targetNPCWeenie, uint item_id)
        {
            Database.Models.World.WeeniePropertiesEmote responseEmote = new Database.Models.World.WeeniePropertiesEmote
            {
                Object = targetNPCWeenie,
                Category = (uint)EmoteCategory.QuestFailure,
                Probability = 1,
                WeenieClassId = null,
                Style = null,
                Substyle = null,
                Quest = questName,
                VendorType = (int?)VendorType.Undef,
                MinHealth = 0,
                MaxHealth = null,
            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction1 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.Tell,
                Order = 0,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = $"Good work, you've completed this errand with ease. Here's the item you've ventured all this way for.",
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction2 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.Give,
                Order = 1,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = $"Good work, you've completed this errand with ease. Here's an item for you.",
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = item_id,

            };

            var dynamicQuestMaxXP = PropertyManager.GetLong("dynamic_quest_max_xp");

            Database.Models.World.WeeniePropertiesEmoteAction responseAction3 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.AwardLevelProportionalXP,
                Order = 2,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = $"Good work, you've completed this errand with ease. Here's an item for you.",
                TestString = "",
                Amount = null,
                Percent = 0.15,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,
                Max64 = dynamicQuestMaxXP
            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction4 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.StampQuest,
                Order = 3,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = questName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction5 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.StampQuest,
                Order = 4,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = DynamicQuestFlagName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction1);
            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction2);
            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction3);
            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction4);
            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction5);


            return responseEmote;
        }

        private static Database.Models.World.WeeniePropertiesEmote NewAlreadyCompleteEmote(string questName, Database.Models.World.Weenie targetNPCWeenie)
        {
            Database.Models.World.WeeniePropertiesEmote responseEmote = new Database.Models.World.WeeniePropertiesEmote
            {
                Object = targetNPCWeenie,
                Category = (uint)EmoteCategory.QuestSuccess,
                Probability = 1,
                WeenieClassId = null,
                Style = null,
                Substyle = null,
                Quest = questName,
                VendorType = (int?)VendorType.Undef,
                MinHealth = 0,
                MaxHealth = null,
            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction1 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.Tell,
                Order = 0,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = $"Hmm.. Either you've already completed this quest, or you're not supposed to be here today. You won't be able to repeat or retry this errand for some time",
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction1);

            return responseEmote;
        }

        private static Database.Models.World.WeeniePropertiesEmote NewStarterEmote(string questName, Database.Models.World.Weenie targetNPCWeenie)
        {
            Database.Models.World.WeeniePropertiesEmote responseEmote = new Database.Models.World.WeeniePropertiesEmote
            {
                Object = targetNPCWeenie,
                Category = (int)EmoteCategory.Refuse, //refuse = examine
                Probability = 1,
                WeenieClassId = 365, //Parchment: the note, or the item from the delivery
                Style = (uint?)MotionStance.NonCombat,
                Substyle = (uint?)MotionCommand.Wave,
                Quest = null,
                VendorType = (int?)VendorType.Undef,
                MinHealth = 0,
                MaxHealth = null,
            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction1 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.TurnToTarget,
                Order = 0,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = questName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction2 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.Motion,
                Order = 1,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = questName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction3 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.InqQuest,
                Order = 2,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = DynamicQuestFlagName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,
            };

            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction1);
            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction2);
            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction3);

            return responseEmote;
        }

        private static Database.Models.World.WeeniePropertiesEmote NewStarterDyanmicEmote(string questName, Database.Models.World.Weenie targetNPCWeenie, uint? acceptedItemWeenieID = 365, bool takeItem = false)
        {
            uint takeCategory = 1;
            if (takeItem == true)
            {
                takeCategory = 6;
            }

            Database.Models.World.WeeniePropertiesEmote responseEmote = new Database.Models.World.WeeniePropertiesEmote
            {
                Object = targetNPCWeenie,
                Category = takeCategory, //refuse = examine
                Probability = 1,
                WeenieClassId = acceptedItemWeenieID, //Parchment: the note, or the item from the delivery
                Style = (uint?)MotionStance.NonCombat,
                Substyle = (uint?)MotionCommand.Wave,
                Quest = questName,
                VendorType = (int?)VendorType.Undef,
                MinHealth = 0,
                MaxHealth = null,
            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction1 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.TurnToTarget,
                Order = 0,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = questName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction2 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.Motion,
                Order = 1,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = questName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,

            };

            Database.Models.World.WeeniePropertiesEmoteAction responseAction3 = new Database.Models.World.WeeniePropertiesEmoteAction
            {
                Emote = responseEmote,
                Type = (uint)EmoteType.InqQuestSolves,
                Order = 2,
                Delay = 0,
                Extent = 0,
                Motion = (uint?)MotionCommand.Wave,
                Message = questName,
                TestString = "",
                Amount = null,
                Amount64 = null,
                HeroXP64 = null,
                WealthRating = null,
                TreasureClass = null,
                TreasureType = null,
                WeenieClassId = null,
                Max = 1,
                Min = 1,

            };

            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction1);
            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction2);
            responseEmote.WeeniePropertiesEmoteAction.Add(responseAction3);

            return responseEmote;
        }

        public static void AbandonDynamicQuests(Player player)
        {
            using (var db = new Database.Models.Shard.ShardDbContext())
            {
                var quests = db.CharacterPropertiesQuestRegistry.Where(x => x.CharacterId == player.Character.Id && x.QuestName.StartsWith("Dynamic"));
                foreach (var q in quests)
                { 
                    q.LastTimeCompleted = (uint)Time.GetUnixTime(DateTime.Now); 
                }
                db.SaveChanges();
            }
        }

        public static bool IsDynamicQuestEligible(Player player)
        {
            using (var db = new Database.Models.Shard.ShardDbContext())
            {
                db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                var dynamicQuestRepeatHours = PropertyManager.GetLong("dynamic_quest_repeat_hours");
                var quests = db.CharacterPropertiesQuestRegistry.Where(x => x.CharacterId == player.Character.Id && x.QuestName.StartsWith("Dynamic"));
                foreach (var q in quests)
                {
                    if (q.LastTimeCompleted != 0 && q.LastTimeCompleted > (uint)Time.GetUnixTime(DateTime.Now.AddHours(-dynamicQuestRepeatHours))) 
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public static int ClearDynamicQuestEmotes()
        {
            int stateItems = 0;
            using (var db = new Database.Models.World.WorldDbContext())
            {
                var emotes = db.WeeniePropertiesEmote.Where(e => e.Quest.StartsWith("Dynamic")).ToList();                
                foreach (var emote in emotes)
                {
                    db.WeeniePropertiesEmote.Remove(emote);
                }
                stateItems +=  db.SaveChanges();
            }
            using (var db = new Database.Models.Shard.ShardDbContext())
            {
                var emotes = db.BiotaPropertiesEmote.Where(e => e.Quest.StartsWith("Dynamic")).ToList();
                foreach (var emote in emotes)
                {
                    db.BiotaPropertiesEmote.Remove(emote);
                }
                stateItems += db.SaveChanges();
            }

            return stateItems;
        }

        public static int ClearDynamicQuestEmotesByQuest(string questName)
        {
            using (var db = new Database.Models.World.WorldDbContext())
            {
                var emotes = db.WeeniePropertiesEmote.Where(e => e.Quest == questName).ToList();
                foreach (var emote in emotes)
                {
                    db.WeeniePropertiesEmote.Remove(emote);
                }
                return db.SaveChanges();
            }
        }

        public bool HasQuestBits(string questFormat, int bits)
        {
            var questName = GetQuestName(questFormat);

            var quest = GetQuest(questName);
            if (quest == null) return false;

            var hasQuestBits = (quest.NumTimesCompleted & bits) == bits;

            if (Debug)
                Console.WriteLine($"{Name}.QuestManager.HasQuestBits({questFormat}, 0x{bits:X}): {hasQuestBits}");

            return hasQuestBits;
        }

        public bool HasNoQuestBits(string questFormat, int bits)
        {
            var questName = GetQuestName(questFormat);

            var quest = GetQuest(questName);
            if (quest == null) return true;

            var hasNoQuestBits = (quest.NumTimesCompleted & bits) == 0;

            if (Debug)
                Console.WriteLine($"{Name}.QuestManager.HasNoQuestBits({questFormat}, 0x{bits:X}): {hasNoQuestBits}");

            return hasNoQuestBits;
        }

        public void SetQuestBits(string questFormat, int bits, bool on = true)
        {
            var questName = GetQuestName(questFormat);

            var quest = GetQuest(questName);

            var questBits = 0;

            if (quest != null) questBits = quest.NumTimesCompleted;

            if (on)
                questBits |= bits;
            else
                questBits &= ~bits;

            if (Debug)
                Console.WriteLine($"{Name}.QuestManager.SetQuestBits({questFormat}, 0x{bits:X}): {on}");

            SetQuestCompletions(questFormat, questBits);
        }

        private static void UpdatePlayerQuestCompletions(Player player, string questName, out bool stampedNew, out bool stampedCompletion, uint solves = 0)
        {
            stampedNew = false;
            stampedCompletion = false;
            if (player.IsMule)
            {
                return;
            }

            var acctId = player.Account.AccountId;

            if (player.Account.HasQuestBonusAndCompletion(questName))
            {
                return; //no need to thrash.
            }

            using (Database.Models.Auth.AuthDbContext context = new Database.Models.Auth.AuthDbContext())
            {
                try
                {
                    var acctQuest = context.AccountQuest.Where(x => x.AccountId == acctId && x.Quest == questName).FirstOrDefault();
                    if (acctQuest != null)
                    {

                        if (solves == 1 && acctQuest.NumTimesCompleted < 1)
                        {
                            stampedNew = false;
                            stampedCompletion = true;
                            acctQuest.NumTimesCompleted = solves;
                            context.AccountQuest.Update(acctQuest);
                            player.Account.UpdateAccountQuestsCacheByQuestName(questName, solves);
                        }
                    }
                    else
                    {
                        context.AccountQuest.Add(new Database.Models.Auth.AccountQuest() { AccountId = acctId, Quest = questName, NumTimesCompleted = solves });
                        stampedNew = true;
                        player.Account.UpdateAccountQuestsCacheByQuestName(questName, solves);
                    }

                    context.SaveChangesFailed += (object sender, Microsoft.EntityFrameworkCore.SaveChangesFailedEventArgs e) =>
                    {
                        Console.WriteLine($"Failed to save quest {questName} for account {acctId}");
                    };

                    context.SaveChanges();
                }
                catch (Exception) { stampedNew = false; stampedCompletion = false; }
            }
            
            

            //player.QuestCompletionCount = player.Account.GetCharacterQuestCompletions();
            player.QuestCompletionCount = player.Account.CachedQuestBonusCount;
        }

        public static uint GetSpecialWeenieReward()
        {
            var list = GetSpecialWeenieRewardsList();
            if (list.Count == 0) return 0;
            if (list.Count == 1) return list[0];
            var index = ThreadSafeRandom.Next(0, list.Count -1);
            return list[index];
        }

        public static List<uint> GetSpecialWeenieRewardsList()
        {
            List<uint> list = new List<uint>();

            list.Add(8899); //Bandit Hilt
            list.Add(29295); //Blank aug gem
            list.Add(36867); //Dire champ token
            list.Add(34276); //Empyrean trinket
            list.Add(300004); //Mid enl coin

            return list;
        }
    }
}
