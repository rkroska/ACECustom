using System;

using ACE.Entity;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class DamageHistoryInfo
    {
        public readonly WeakReference<WorldObject> Attacker;

        public readonly ObjectGuid Guid;
        public readonly string Name;

        public float TotalDamage;

        public readonly WeakReference<Player> PetOwner;

        public bool IsPlayer => Guid.IsPlayer();

        public readonly bool IsOlthoiPlayer;

        /// <summary>
        /// Excess damage beyond the target's remaining HP on the killing blow.
        /// Set on the killing blow's damager entry only — zero on all other hits.
        /// Populated by Monster_Combat.TakeDamage (melee/missile) and SpellProjectile.DamageTarget (spells).
        /// </summary>
        public uint OverkillAmount { get; set; }

        public DamageHistoryInfo(WorldObject attacker, float totalDamage = 0.0f)
        {
            Attacker = new WeakReference<WorldObject>(attacker);

            Guid = attacker.Guid;
            Name = attacker.Name;

            IsOlthoiPlayer = attacker is Player player && player.IsOlthoiPlayer;

            TotalDamage = totalDamage;

            if (attacker is CombatPet combatPet && combatPet.P_PetOwner != null)
                PetOwner = new WeakReference<Player>(combatPet.P_PetOwner);
        }

        public WorldObject TryGetAttacker()
        {
            Attacker.TryGetTarget(out var attacker);

            return attacker;
        }

        public Player TryGetPetOwner()
        {
            PetOwner.TryGetTarget(out var petOwner);

            return petOwner;
        }

        public WorldObject TryGetPetOwnerOrAttacker()
        {
            if (PetOwner != null)
                return TryGetPetOwner();
            else
                return TryGetAttacker();
        }
    }
}
