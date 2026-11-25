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
    /// Everyone else's client never gets a CreateObject for it, so it doesn't draw for them and their client doesn't
    /// collide with it (server-driven movement still does, unless the weenie is ethereal). First use: the
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

        // Show and hide are each one step under the questGatedShown lock: the decision, the set change and the
        // CreateObject / DeleteObject enqueue, so a stamp (emote thread) and the heartbeat can't interleave them and leave
        // the set saying one thing and the client another. Safe to hold: EnqueueSend only takes the session's own bundle
        // locks and never calls back into player code (TrackEquippedObject / RemoveTrackedEquippedObject only enqueue), and QuestManager takes no locks. CanSeeQuestGated reads the quest
        // row from the world cache; callers check it once outside the lock first, so a cache miss (a database read) never
        // happens while the lock is held.

        /// <summary>From TrackObject: sends the CreateObject for a gated object and records it, if this player may still
        /// see it. FALSE = held back (nothing sent).</summary>
        private bool SendQuestGatedCreate(WorldObject wo)
        {
            lock (questGatedShown)
            {
                if (!CanSeeQuestGated(wo))
                    return false;

                questGatedShown.Add(wo.Guid.Full);
                Session.Network.EnqueueSend(new GameMessageCreateObject(wo, Adminvision, Adminvision));

                // its wielded items in the same step, so a hide can't slip between the creature and them
                if (wo is Creature creature)
                    foreach (var wieldedItem in creature.EquippedObjects.Values)
                        if (IsInChildLocation(wieldedItem))
                            TrackEquippedObject(creature, wieldedItem);
                return true;
            }
        }

        /// <summary>Sends the DeleteObject for a gated object the client was sent, and forgets it. onlyIfHidden: only when
        /// this player may no longer see it (a refresh; not for an object leaving range). FALSE = nothing sent.</summary>
        private bool SendQuestGatedDelete(WorldObject wo, bool onlyIfHidden)
        {
            lock (questGatedShown)
            {
                if (onlyIfHidden && CanSeeQuestGated(wo))
                    return false;

                if (!questGatedShown.Remove(wo.Guid.Full))
                    return false;

                Session.Network.EnqueueSend(new GameMessageDeleteObject(wo));

                // as RemoveTrackedObject does, in the same step: the client drops what the creature holds too
                if (wo is Creature creature)
                    foreach (var wieldedItem in creature.EquippedObjects.Values)
                        RemoveTrackedEquippedObject(creature, wieldedItem);
                return true;
            }
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

                var see = CanSeeQuestGated(wo);   // first check, outside the lock; the send re-checks under it
                if (see && !shown)
                    TrackObject(wo, createObjectPath: "QuestGate");
                else if (!see && shown)
                    SendQuestGatedDelete(wo, onlyIfHidden: true);
            }
        }

        /// <summary>
        /// Heartbeat: hides gated objects whose quest timer ran out. Costs nothing while none are shown.
        /// First drops guids this player no longer knows about at all (an object destroyed, or dropped from ObjMaint
        /// without a RemoveTrackedObject), so stale entries neither pile up nor keep this check running. Objects the
        /// client lost in a teleport are sent again by ReconcileVisibilityAfterArrival, through TrackObject and this gate.
        /// </summary>
        private void QuestGatedObjectsTick()
        {
            uint[] shown;
            lock (questGatedShown)
            {
                if (questGatedShown.Count == 0)
                    return;
                shown = questGatedShown.ToArray();
            }

            var objMaint = PhysicsObj?.ObjMaint;
            if (objMaint == null)
                return;

            var gone = shown.Where(g => !objMaint.KnownObjectsContainsKey(g)).ToList();
            if (gone.Count > 0)
            {
                lock (questGatedShown)
                    foreach (var g in gone)
                        questGatedShown.Remove(g);
            }

            if (gone.Count < shown.Length)
                RefreshQuestGatedObjects();
        }
    }
}
