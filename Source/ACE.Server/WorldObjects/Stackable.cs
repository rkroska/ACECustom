using System;

using ACE.Entity;
using ACE.Entity.Models;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    public class Stackable : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Stackable(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Stackable(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            if (!StackSize.HasValue)
                StackSize = 1;

            if (!MaxStackSize.HasValue)
                MaxStackSize = 1;

            if (!Value.HasValue)
                Value = 0;

            if (!EncumbranceVal.HasValue)
                EncumbranceVal = 0;

            // Fix for "poisoned" stackables: If unit values are missing/zero but totals exist,
            // compute unit values from totals. This prevents crashes in bank/trade operations.
            // CRITICAL: We preserve the original Value and EncumbranceVal to avoid truncation
            // from integer division (e.g., 101/3=33, 33*3=99 loses 2 value).
            // Only repair unit values; never recompute totals from repaired units.
            if (!StackUnitEncumbrance.HasValue || (StackUnitEncumbrance == 0 && EncumbranceVal > 0))
            {
                if (StackSize > 1)
                    StackUnitEncumbrance = EncumbranceVal / StackSize;
                else
                    StackUnitEncumbrance = EncumbranceVal;
            }

            if (!StackUnitValue.HasValue || (StackUnitValue == 0 && Value > 0))
            {
                if (StackSize > 1)
                    StackUnitValue = Value / StackSize;
                else
                    StackUnitValue = Value;
            }
        }

        public override void ActOnUse(WorldObject wo)
        {
            // Do nothing
        }
    }
}
