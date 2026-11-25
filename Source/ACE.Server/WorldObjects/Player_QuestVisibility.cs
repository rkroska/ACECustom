using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Quest-gated objects (2026-11-25): a world object with PropertyString.VisibleWithQuest is sent only to the players
    /// who hold that quest stamp with its timer still running (for a quest with min_Delta 0: who hold the stamp at all).
    /// Everyone else's client never gets a CreateObject for it, so it neither draws nor blocks for them. First use: the
    /// Courier's Gauntlet runner's bells, which appear for 15 minutes after talking to Postmaster Hara instead of
    /// cluttering Shoushi for everyone.
    ///
    /// The server side is untouched: the object stays in every nearby player's ObjMaint (known/visible lists) as usual,
    /// only the client messages are held back, the same way PropertyBool.Visibility (server-only objects) works.
    /// A quest stamp or erase re-checks at once (QuestManager); the heartbeat catches timers that run out.
    /// </summary>
    partial class Player
    {
        /// <summary>Guids of quest-gated objects this player's client was sent a CreateObject for and has not been told
        /// to delete. Touched from landblock threads (tracking) and the player's own (quests, heartbeat), hence the lock.</summary>
        private readonly HashSet<uint> questGatedShown = new();

        /// <summary>TRUE when this player may be sent the object: it carries no quest gate, admin vision is on, or the
        /// player holds the quest with its timer running (min_Delta 0: holds it at all).</summary>
        public bool CanSeeQuestGated(WorldObject wo)
        {
            var quest = wo?.VisibleWithQuest;
            if (string.IsNullOrEmpty(quest) || Adminvision)
                return true;

            return HasRunningQuestStamp(quest);
        }

        private bool HasRunningQuestStamp(string quest)
        {
            var qm = QuestManager;
            if (qm == null || !qm.HasQuest(quest))
                return false;

            var worldQuest = DatabaseManager.World.GetCachedQuest(QuestManager.GetQuestName(quest));
            return QuestGateOpen(true, worldQuest, worldQuest == null ? TimeSpan.MaxValue : qm.GetNextSolveTime(quest));
        }

        /// <summary>
        /// The rule, pure: held = the player has the stamp; quest = its world row (null = none); left = what
        /// QuestManager.GetNextSolveTime says (MinValue = the timer ran out, MaxValue = max solves reached).
        /// Open while the timer runs; for a quest with min_Delta 0, open while the stamp is held; never without a row.
        /// </summary>
        public static bool QuestGateOpen(bool held, ACE.Database.Models.World.Quest quest, TimeSpan left)
        {
            if (!held || quest == null)
                return false;

            if (quest.MinDelta == 0)
                return true;

            return left != TimeSpan.MinValue && left != TimeSpan.MaxValue;
        }

        /// <summary>Called from TrackObject just before a CreateObject goes out. FALSE = hold it back.</summary>
        private bool QuestGateAllowsCreate(WorldObject wo)
        {
            if (string.IsNullOrEmpty(wo.VisibleWithQuest))
                return true;

            if (!CanSeeQuestGated(wo))
                return false;

            lock (questGatedShown)
                questGatedShown.Add(wo.Guid.Full);
            return true;
        }

        /// <summary>Called from RemoveTrackedObject before a DeleteObject goes out. FALSE = the client never had it.</summary>
        private bool QuestGateAllowsDelete(WorldObject wo)
        {
            if (string.IsNullOrEmpty(wo.VisibleWithQuest))
                return true;

            lock (questGatedShown)
                return questGatedShown.Remove(wo.Guid.Full);
        }

        /// <summary>
        /// Shows or hides the quest-gated objects this player knows about, to match the quests now held. With a quest
        /// name, only objects gated on that quest are looked at (a stamp or erase); without, all of them (the heartbeat).
        /// </summary>
        public void RefreshQuestGatedObjects(string questName = null)
        {
            if (Session == null || PhysicsObj?.ObjMaint == null)
                return;

            var gated = PhysicsObj.ObjMaint.GetKnownObjectsValuesWhere(o =>
            {
                var q = o?.WeenieObj?.WorldObject?.VisibleWithQuest;
                return !string.IsNullOrEmpty(q) && (questName == null
                    || QuestManager.GetQuestName(q).Equals(questName, StringComparison.OrdinalIgnoreCase));
            }).Select(o => o.WeenieObj.WorldObject).ToList();

            foreach (var wo in gated)
            {
                bool shown;
                lock (questGatedShown)
                    shown = questGatedShown.Contains(wo.Guid.Full);

                var see = CanSeeQuestGated(wo);
                if (see && !shown)
                    TrackObject(wo, createObjectPath: "QuestGate");
                else if (!see && shown)
                {
                    lock (questGatedShown)
                        questGatedShown.Remove(wo.Guid.Full);
                    Session.Network.EnqueueSend(new GameMessageDeleteObject(wo));
                }
            }
        }

        /// <summary>Heartbeat: hides gated objects whose quest timer ran out. Costs nothing while none are shown.</summary>
        private void QuestGatedObjectsTick()
        {
            int count;
            lock (questGatedShown)
                count = questGatedShown.Count;

            if (count > 0)
                RefreshQuestGatedObjects();
        }
    }
}
