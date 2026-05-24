using System;
using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using AttackTypes = ACE.DatLoader.FileTypes.CombatManeuverTable.AttackTypes;
using ACE.Entity.Enum;
using MotionTableAnim = ACE.Server.Physics.Animation.MotionTable;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// Expected melee damage events per second (each attack frame rolls damage once), averaged over attack heights
        /// and maneuver variants the CMT can select for this creature's weapon and stance. Uses the same motion-table
        /// resolution rules as <see cref="Monster_Melee.GetCombatManeuver"/> and mean post-swing delay (half of max PowerupTime).
        /// </summary>
        public float EstimateMeleeDamageEventsPerSecond(uint motionTableId, float meanMeleeDelaySeconds)
        {
            var stance = CurrentMotionState?.Stance ?? MotionStance.NonCombat;
            return EstimateMeleeDamageEventsPerSecond(motionTableId, meanMeleeDelaySeconds, stance);
        }

        /// <summary>
        /// Same as <see cref="EstimateMeleeDamageEventsPerSecond(uint,float)"/> but uses an explicit combat stance
        /// (for capture-skin compatibility probes before <see cref="CurrentMotionState"/> is finalized).
        /// </summary>
        public float EstimateMeleeDamageEventsPerSecond(uint motionTableId, float meanMeleeDelaySeconds, MotionStance stance)
        {
            if (CombatTable == null)
                GetCombatTable();
            if (CombatTable == null || meanMeleeDelaySeconds < 0)
                return 0f;

            var motionTable = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.MotionTable>(motionTableId);
            if (motionTable == null)
                return 0f;

            if (!CombatTable.Stances.TryGetValue(stance, out var stanceManeuvers))
                return 0f;

            var stanceKey = (uint)stance << 16 | ((uint)MotionCommand.Ready & 0xFFFFFF);
            if (!motionTable.Links.TryGetValue(stanceKey, out var motions) || motions == null)
                return 0f;

            var baseSpeed = GetAnimSpeed();
            var animSpeedMod = IsDualWieldAttack ? 1.2f : 1.0f;
            var animSpeed = baseSpeed * animSpeedMod;

            var startHeight = stanceManeuvers.Table.Count == 3 ? 1 : 2;
            double sum = 0;
            var count = 0;

            for (var h = startHeight; h <= 3; h++)
            {
                var atkHeight = (ACE.Entity.Enum.AttackHeight)h;
                if (!stanceManeuvers.Table.TryGetValue(atkHeight, out var attackTypes))
                    continue;

                var offhand = false;
                var weapon = GetEquippedMeleeWeapon();

                AttackType attackType;
                if (weapon != null)
                    attackType = weapon.GetAttackType(stance, 0.5f, offhand);
                else if (atkHeight != ACE.Entity.Enum.AttackHeight.Low)
                    attackType = AttackType.Punch;
                else
                    attackType = AttackType.Kick;

                if (!TryGetManeuverListForMotionEstimate(attackTypes, ref attackType, atkHeight, out var maneuverList) || maneuverList.Count == 0)
                    continue;

                foreach (var motionCommand in maneuverList)
                {
                    var resolved = ResolveMonsterMotionCommandForEstimate(motionCommand, motions);
                    if (resolved == null)
                        continue;

                    var animLength = MotionTableAnim.GetAnimationLength(motionTableId, stance, resolved.Value, animSpeed);
                    var frames = MotionTableAnim.GetAttackFrames(motionTableId, stance, resolved.Value);
                    var strikes = frames.Count == 0 ? 1 : frames.Count;
                    var period = animLength + meanMeleeDelaySeconds;
                    if (period <= 0)
                        continue;

                    sum += strikes / period;
                    count++;
                }
            }

            return count > 0 ? (float)(sum / count) : 0f;
        }

        /// <summary>
        /// Finds a combat stance that has at least one resolvable unarmed melee maneuver for the current
        /// <see cref="CombatTable"/> and the given motion table. Prefers <see cref="MotionStance.HandCombat"/> when tied.
        /// </summary>
        public bool TryFindValidUnarmedMeleeStance(uint motionTableId, float meanMeleeDelaySeconds, out MotionStance stance)
        {
            stance = MotionStance.HandCombat;

            if (CombatTable == null)
                GetCombatTable();
            if (CombatTable == null || CombatTable.Stances.Count == 0)
                return false;

            MotionStance? bestStance = null;
            var bestRate = 0f;

            foreach (var candidate in CombatTable.Stances.Keys.OrderBy(s => s == MotionStance.HandCombat ? 0 : 1))
            {
                var rate = EstimateMeleeDamageEventsPerSecond(motionTableId, meanMeleeDelaySeconds, candidate);
                if (rate <= bestRate)
                    continue;

                bestRate = rate;
                bestStance = candidate;
            }

            if (!bestStance.HasValue || bestRate <= float.Epsilon)
                return false;

            stance = bestStance.Value;
            return true;
        }

        private static MotionCommand? ResolveMonsterMotionCommandForEstimate(MotionCommand motionCommand, Dictionary<uint, MotionData> motions)
        {
            if (motions.ContainsKey((uint)motionCommand))
                return motionCommand;

            if (motionCommand.IsMultiStrike())
            {
                var singleStrike = motionCommand.ReduceMultiStrike();
                if (motions.ContainsKey((uint)singleStrike))
                    return singleStrike;
            }
            else if (motionCommand.IsSubsequent())
            {
                var firstCommand = motionCommand.ReduceSubsequent();
                if (motions.ContainsKey((uint)firstCommand))
                    return firstCommand;
            }

            return null;
        }

        private bool TryGetManeuverListForMotionEstimate(AttackTypes attackTypes, ref AttackType attackType, ACE.Entity.Enum.AttackHeight attackHeight, out List<MotionCommand> maneuverList)
        {
            maneuverList = null;

            if (!attackTypes.Table.TryGetValue(attackType, out var maneuvers) || maneuvers.Count == 0)
            {
                if (attackType == AttackType.Punch && attackHeight == ACE.Entity.Enum.AttackHeight.Low || attackType == AttackType.Kick)
                {
                    attackType = attackType == AttackType.Punch ? AttackType.Kick : AttackType.Punch;
                    if (!attackTypes.Table.TryGetValue(attackType, out maneuvers) || maneuvers.Count == 0)
                        return false;
                }
                else if (attackType.IsMultiStrike())
                {
                    var reduced = attackType.ReduceMultiStrike();
                    if (!attackTypes.Table.TryGetValue(reduced, out maneuvers) || maneuvers.Count == 0)
                        return false;
                }
                else
                    return false;
            }

            maneuverList = maneuvers;
            return true;
        }
    }
}
