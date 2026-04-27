using System;
using System.Linq;
using ACE.Server.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Handles player->monster visibility checks
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// Wakes up any monsters within the applicable range
        /// </summary>
        public void CheckMonsters()
        {
            // If cloaked as creature, don't alert monsters
            if (CloakStatus == CloakStatus.Creature)
                return;

            if (!Attackable || Teleporting) return;

            var visibleObjs = PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature();

            foreach (var monster in visibleObjs)
            {
                if (monster is Player) continue;

                //if (Location.SquaredDistanceTo(monster.Location) <= monster.VisualAwarenessRangeSq)
                if (PhysicsObj.get_distance_sq_to_object(monster.PhysicsObj, true) <= monster.VisualAwarenessRangeSq)
                    AlertMonster(monster);
            }
        }

        /// <summary>
        /// Called when this player attacks a monster
        /// </summary>
        public void OnAttackMonster(Creature monster, bool hostileAction = true)
        {
            if (monster == null || !Attackable) return;

            /*Console.WriteLine($"{Name}.OnAttackMonster({monster.Name})");
            Console.WriteLine($"Attackable: {monster.Attackable}");
            Console.WriteLine($"Tolerance: {monster.Tolerance}");*/

            // Custom targeting system: only mobs using string-based friend/foe lists get the de-aggro
            // treatment here.  Legacy mobs that define friendship via the old Int.73 FoeType property
            // have IsUsingCustomTargetingLists = false and fall through to the standard retaliate path
            // below.  This asymmetry is intentional: broadening the guard to IsFriend() alone could
            // produce unexpected side-effects on existing content that predates the custom targeting system.
            if (monster.IsUsingCustomTargetingLists && monster.IsFriend(this))
            {
                var breakPeace = hostileAction && monster.BreakPeaceOnHostileAction.GetValueOrDefault(false);
                if (!breakPeace)
                    return;

                // BreakPeaceOnHostileAction intentionally overrides the Tolerance flag.
                // This is the "escort betrayal" scenario: a mob that is normally friendly but turns
                // hostile the moment a player attacks it.  That hostile intent is stronger than a
                // generic Tolerance.NoRetaliateAttack exclusion, so we skip the Tolerance check here
                // deliberately.  If you need a friendly mob that ignores being attacked, leave
                // BreakPeaceOnHostileAction unset (or false) instead.
                monster.AddRetaliateTarget(this);
                monster.AttackTarget = this;
                if (monster.MonsterState != State.Awake)
                    monster.WakeUp();

                return;
            }

            // faction mobs will retaliate against players belonging to the same faction
            if (SameFaction(monster))
                monster.AddRetaliateTarget(this);

            if (monster.MonsterState != State.Awake && (monster.Tolerance & PlayerCombatPet_RetaliateExclude) == 0)
            {
                monster.AttackTarget = this;
                monster.WakeUp();
            }
        }
    }
}
