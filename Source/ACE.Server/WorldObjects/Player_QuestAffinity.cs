using System.Collections.Generic;

using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    public partial class Player
    {
        /// <summary>
        /// Called after this player's quest registry changes (stamp gained, erased, or completions set).
        /// Scans monsters on the current landblock (and adjacents) plus known physics objects for any
        /// creature whose <c>FriendlyQuestString</c> matches <paramref name="questName"/>, then asks
        /// each matching creature to reconcile its combat state against this player.
        /// </summary>
        /// <remarks>
        /// We scan both the physics known-object list and the landblock because an actively attacking
        /// mob may have been removed from (or not yet added to) the known-object list.
        /// </remarks>
        public void NotifyQuestAffinityChanged(string questName)
        {
            if (PhysicsObj == null || string.IsNullOrEmpty(questName))
                return;

            var normalizedChanged = QuestManager.GetQuestName(questName);
            var processed = new HashSet<uint>();

            void Consider(Creature creature)
            {
                if (creature == null || !creature.IsMonster || creature is CombatPet)
                    return;
                if (string.IsNullOrEmpty(creature.FriendlyQuestString))
                    return;

                // Normalize mob-side quest name the same way QuestManager does.
                var mobNormalized = QuestManager.GetQuestName(creature.FriendlyQuestString);
                if (!string.Equals(normalizedChanged, mobNormalized, System.StringComparison.OrdinalIgnoreCase))
                    return;

                // Deduplicate — creature may appear in both known-objects and landblock scan.
                if (!processed.Add(creature.Guid.Full))
                    return;

                creature.ReconcileFriendlyQuestAffinityWithPlayer(this);
            }

            // 1. Physics known-object list (fastest; covers most attacking mobs).
            foreach (var c in PhysicsObj.ObjMaint.GetKnownObjectsValuesAsCreature())
                Consider(c);

            // 2. Current landblock + adjacents (catches mobs not yet in known-objects).
            void ScanLandblock(Landblock lb)
            {
                if (lb == null) return;
                foreach (var wo in lb.GetWorldObjectsForPhysicsHandling())
                {
                    if (wo is Creature c)
                        Consider(c);
                }
            }

            ScanLandblock(CurrentLandblock);
            if (CurrentLandblock != null)
            {
                foreach (var adj in CurrentLandblock.Adjacents)
                    ScanLandblock(adj);
            }
        }
    }
}
