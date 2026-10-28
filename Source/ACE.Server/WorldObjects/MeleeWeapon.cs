using System;

using ACE.Entity;
using ACE.Entity.Models;

namespace ACE.Server.WorldObjects
{
    public class MeleeWeapon : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public MeleeWeapon(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public MeleeWeapon(Biota biota) : base(biota)
        {
            SetEphemeralValues();

            // T11+ Slash Rending on a Slashing/Piercing weapon -> Slashing only (owner 2026-10-28). Retroactive:
            // the rewrite marks the biota changed, so it is saved with the owner's next save.
            ACE.Server.Managers.ZoneControl.ZoneSlashRendDamageType.Normalize(this, existingItem: true);
        }

        private void SetEphemeralValues()
        {
            CurrentMotionState = null;
        }
    }
}
