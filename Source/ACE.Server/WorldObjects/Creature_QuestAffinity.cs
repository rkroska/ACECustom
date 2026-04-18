namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Mid-combat quest-affinity reconciliation: when a player gains (or loses) the quest stamp
    /// matching this creature's FriendlyQuestString, the creature drops or restores aggression.
    /// </summary>
    public partial class Creature
    {
        /// <summary>
        /// Clears <see cref="AttackTarget"/> when it is <paramref name="player"/>.
        /// Called from reconciliation paths when a player becomes an ally.
        /// </summary>
        private void ClearAttackTargetForQuestAffinityReconcile(Player player)
        {
            if (player == null || AttackTarget != player)
                return;

            AttackTarget = null;
        }

        /// <summary>
        /// Per-tick safety check: if the current <see cref="AttackTarget"/> is a player who became a
        /// friendly-quest ally, drop combat immediately before <see cref="HandleFindTarget"/> can
        /// re-select them on the same tick.
        /// </summary>
        /// <returns>True when a break-off occurred and the tick should return early.</returns>
        public bool TryBreakOffAttackIfFriendlyQuestAlly()
        {
            if (AttackTarget is not Player p)
                return false;

            if (!UsesFriendlyQuestTargeting || !IsFriendlyQuestAlly(p))
                return false;

            ClearAttackTargetForQuestAffinityReconcile(p);
            InvalidateTargetCaches();

            if (HasRetaliateTarget(p))
                RemoveRetaliateTarget(p);

            if (MonsterState != State.Idle)
                FindNextTarget();

            return true;
        }

        /// <summary>
        /// Called by <see cref="Player.NotifyQuestAffinityChanged"/> when a player's quest registry
        /// changes for a quest that matches this creature's FriendlyQuestString.
        /// <para>
        /// If the player <em>is</em> now an ally: clears AttackTarget/retaliate and finds a new target.<br/>
        /// If the player is <em>no longer</em> an ally: may re-alert the monster so it resumes hostility.
        /// </para>
        /// </summary>
        public void ReconcileFriendlyQuestAffinityWithPlayer(Player player)
        {
            if (player == null || !UsesFriendlyQuestTargeting)
                return;
            if (!IsMonster || this is CombatPet)
                return;

            var isAlly = IsFriendlyQuestAlly(player);

            // Unconditionally invalidate the visible-target caches. The player's quest stamp just changed,
            // meaning their eligibility as a target has changed. We must ensure subsequent evaluations
            // (like FindNextTarget or AlertMonster) do not reuse cached data built under the old affinity.
            InvalidateTargetCaches();

            if (isAlly)
            {
                // Only act if we are actually targeting or have retaliated against this player.
                if (AttackTarget != player && !HasRetaliateTarget(player))
                    return;

                ClearAttackTargetForQuestAffinityReconcile(player);

                if (HasRetaliateTarget(player))
                    RemoveRetaliateTarget(player);

                if (MonsterState != State.Idle)
                    FindNextTarget();

                return;
            }

            // --- Player is no longer a quest ally; may resume hostility ---

            // Already attacking someone else — do nothing.
            if (AttackTarget != null && AttackTarget != player)
                return;

            // Already targeting this player — nothing to change.
            if (AttackTarget == player)
                return;

            // Had retaliate entry — find a new target (may pick this player again).
            if (HasRetaliateTarget(player))
            {
                FindNextTarget();
                return;
            }

            // Idle: alerting via proximity is the natural path.
            if (MonsterState == State.Idle)
            {
                // Guard: only alert if the player is actually within visual range and is a valid target.
                // AlertMonster assumes the caller has already done proximity/visibility filtering;
                // without this check, erasing a quest stamp could wake mobs far outside awareness range.
                if (PhysicsObj == null || player.PhysicsObj == null)
                    return;

                if (!player.Attackable || player.Teleporting || player.CloakStatus == ACE.Entity.Enum.CloakStatus.Creature || (player.Hidden ?? false))
                    return;

                if (PhysicsObj.get_distance_sq_to_object(player.PhysicsObj, true) > VisualAwarenessRangeSq)
                    return;

                player.AlertMonster(this);
            }
            else
                FindNextTarget();
        }
    }
}
