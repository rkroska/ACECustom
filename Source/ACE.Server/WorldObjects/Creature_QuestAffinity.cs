namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Mid-combat quest-affinity reconciliation: when a player gains (or loses) the quest stamp
    /// matching this creature's FriendlyQuestString, the creature drops or restores aggression.
    /// </summary>
    public partial class Creature
    {
        /// <summary>
        /// Clears <see cref="AttackTarget"/> when it is <paramref name="player"/> and invalidates
        /// the target cache.  Called from reconciliation paths so all caches stay consistent.
        /// </summary>
        private void ClearAttackTargetForQuestAffinityReconcile(Player player)
        {
            if (player == null || AttackTarget != player)
                return;

            AttackTarget = null;
            // Invalidate the visible-target cache so the next FindNextTarget() call
            // re-evaluates the fresh list without the now-friendly player.
            InvalidateTargetCaches();
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
            if (player == null || string.IsNullOrEmpty(FriendlyQuestString))
                return;
            if (!IsMonster || this is CombatPet)
                return;

            var isAlly = IsFriendlyQuestAlly(player);

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
                player.AlertMonster(this);
            else
                FindNextTarget();
        }
    }
}
