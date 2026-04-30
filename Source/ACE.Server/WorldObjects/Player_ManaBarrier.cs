namespace ACE.Server.WorldObjects
{
    public partial class Player
    {
        /// <summary>
        /// Returns the mana-to-damage absorption ratio modifier for the player's active Mana Barrier tier.
        /// Level 1: 1:1   (1 Mana per 1 Damage)   -> ratio mod = 1.000
        /// Level 2: 1.5:1 (1 Mana per 1.5 Damage) -> ratio mod = 0.667
        /// Level 3: 2:1   (1 Mana per 2 Damage)   -> ratio mod = 0.500
        /// </summary>
        public override float GetManaBarrierRatioMod()
        {
            // Read level from the activated MB charm directly — ActiveCharmLevel is a single
            // global field and can be overwritten when a second charm (e.g. Infinite Casting)
            // is toggled on or off.
            var mbAbilityId = CharmAbilityRegistry.ManaBarrierAbilityId;
            var level = 1;
            foreach (var item in GetAllPossessions())
            {
                if (item.IsAbilityCharm && item.IsCharmActivated && item.CharmGrantsAbility == mbAbilityId)
                {
                    level = item.CharmLevel ?? 1;
                    break;
                }
            }
            return level switch
            {
                1 => 1.000f,
                2 => 0.667f,
                3 => 0.500f,
                _ => 1.000f
            };
        }

        /// <summary>
        /// Returns the Mana Barrier suffix shown to the attacking player.
        /// Format: " [Barrier: X / Y]" where X = current mana, Y = max mana.
        /// </summary>
        public string GetManaBarrierSuffix(ManaBarrierResult result)
        {
            if (result.AmountAbsorbed == 0) return "";
            var cur = Mana?.Current ?? 0;
            var max = Mana?.MaxValue ?? 0;
            return $" [Barrier: {FormatDamage((ulong)cur, DamageNumberFormat)} / {FormatDamage((ulong)max, DamageNumberFormat)}]";
        }
    }
}
