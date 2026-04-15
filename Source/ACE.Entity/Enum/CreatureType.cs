namespace ACE.Entity.Enum
{
    public enum CreatureType : uint
    {
        Invalid,
        Olthoi,
        Banderling,
        Drudge,
        Mosswart,
        Lugian,
        Tumerok,
        Mite,
        Tusker,
        PhyntosWasp,
        Rat,
        Auroch,
        Cow,
        Golem,
        Undead,
        Gromnie,
        Reedshark,
        Armoredillo,
        Fae,
        Virindi,
        Wisp,
        Knathtead,
        Shadow,
        Mattekar,
        Mumiyah,
        Rabbit,
        Sclavus,
        ShallowsShark,
        Monouga,
        Zefir,
        Skeleton,
        Human,
        Shreth,
        Chittick,
        Moarsman,
        OlthoiLarvae,
        Slithis,
        Deru,
        FireElemental,
        Snowman,
        Unknown,
        Bunny,
        LightningElemental,
        Rockslide,
        Grievver,
        Niffis,
        Ursuin,
        Crystal,
        HollowMinion,
        Scarecrow,
        Idol,
        Empyrean,
        Hopeslayer,
        Doll,
        Marionette,
        Carenzi,
        Siraluun,
        AunTumerok,
        HeaTumerok,
        Simulacrum,
        AcidElemental,
        FrostElemental,
        Elemental,
        Statue,
        Wall,
        AlteredHuman,
        Device,
        Harbinger,
        DarkSarcophagus,
        Chicken,
        GotrokLugian,
        Margul,
        BleachedRabbit,
        NastyRabbit,
        GrimacingRabbit,
        Burun,
        Target,
        Ghost,
        Fiun,
        Eater,
        Penguin,
        Ruschk,
        Thrungus,
        ViamontianKnight,
        Remoran,
        Swarm,
        Moar,
        EnchantedArms,
        Sleech,
        Mukkir,
        Merwart,
        Food,
        ParadoxOlthoi,
        Harvest,
        Energy,
        Apparition,
        Aerbax,
        Touched,
        BlightedMoarsman,
        GearKnight,
        Gurog,
        Anekshay,
        /// <summary>
        /// Pseudo creature type used by custom targeting lists to represent players that match a quest stamp.
        /// </summary>
        /// <remarks>
        /// Intended usage is inside comma-separated <see cref="T:ACE.Entity.Enum.Properties.PropertyString.FriendTypeString"/> (9014)
        /// and/or <see cref="T:ACE.Entity.Enum.Properties.PropertyString.FoeTypeString"/> (9015) when
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.UseCustomTargetingLists"/> (9018) is enabled.
        ///
        /// Quest matching is driven by <see cref="T:ACE.Entity.Enum.Properties.PropertyString.FriendlyQuestString"/> (9016): players are treated as matching
        /// <c>QuestPlayer</c> when they currently have that quest stamp.
        ///
        /// Related behavior flags (not required for parsing the lists, but commonly used alongside them):
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.AllowFriendlyPlayerDamage"/> (9041) and
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.BreakPeaceOnHostileAction"/> (9042).
        /// </remarks>
        QuestPlayer = 996,
        /// <summary>
        /// Pseudo creature type used by custom targeting lists to represent all players (regardless of quest stamps).
        /// </summary>
        /// <remarks>
        /// Use inside comma-separated <see cref="T:ACE.Entity.Enum.Properties.PropertyString.FriendTypeString"/> (9014) and/or
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyString.FoeTypeString"/> (9015) when
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.UseCustomTargetingLists"/> (9018) is enabled.
        ///
        /// Unlike <see cref="QuestPlayer"/>, this does not require <see cref="T:ACE.Entity.Enum.Properties.PropertyString.FriendlyQuestString"/> (9016) to take effect.
        ///
        /// Related behavior flags (not required for parsing the lists, but commonly used alongside them):
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.AllowFriendlyPlayerDamage"/> (9041) and
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.BreakPeaceOnHostileAction"/> (9042).
        /// </remarks>
        Player = 997,
        AttackAll = 998,
        /// <summary>
        /// Pseudo creature type meaning “attack anything that is not the same concrete <see cref="CreatureType"/> as this creature”.
        /// Hostile to outsiders + all players, but not automatically hostile to other mobs of my same <see cref="CreatureType"/>.
        /// </summary>
        /// <remarks>
        /// In custom targeting mode, this is typically listed in comma-separated <see cref="T:ACE.Entity.Enum.Properties.PropertyString.FoeTypeString"/> (9015) while
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.UseCustomTargetingLists"/> (9018) is enabled, and expands into an internal “attack non-self” mode during cache parsing.
        ///
        /// Legacy databases may also represent this concept via <c>weenie_properties_int</c> <c>FoeType</c> (73) instead of string lists.
        ///
        /// Related behavior flags (not required for the pseudo-type itself, but commonly used alongside custom targeting):
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.AllowFriendlyPlayerDamage"/> (9041) and
        /// <see cref="T:ACE.Entity.Enum.Properties.PropertyBool.BreakPeaceOnHostileAction"/> (9042).
        /// </remarks>
        AttackNonSelf = 999
    }
}
