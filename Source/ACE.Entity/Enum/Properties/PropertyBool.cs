namespace ACE.Entity.Enum.Properties
{
    public enum PropertyBool : ushort
    {
        // No properties are sent to the client unless they featured an attribute.
        // SendOnLogin gets sent to players in the PlayerDescription event
        // AssessmentProperty gets sent in successful appraisal

        Undef = 0,
        [Ephemeral]
        Stuck                            = 1,
        [AssessmentProperty]
        [Ephemeral]
        Open                             = 2,
        [AssessmentProperty]
        Locked                           = 3,
        RotProof                         = 4,
        AllegianceUpdateRequest          = 5,
        AiUsesMana                       = 6,
        AiUseHumanMagicAnimations        = 7,
        AllowGive                        = 8,
        CurrentlyAttacking               = 9,
        AttackerAi                       = 10,
        IgnoreCollisions                 = 11,
        ReportCollisions                 = 12,
        Ethereal                         = 13,
        GravityStatus                    = 14,
        LightsStatus                     = 15,
        ScriptedCollision                = 16,
        Inelastic                        = 17,
        [Ephemeral]
        Visibility                       = 18,
        Attackable                       = 19,
        SafeSpellComponents              = 20,
        [SendOnLogin]
        AdvocateState                    = 21,
        Inscribable                      = 22,
        DestroyOnSell                    = 23,
        UiHidden                         = 24,
        IgnoreHouseBarriers              = 25,
        HiddenAdmin                      = 26,
        PkWounder                        = 27,
        PkKiller                         = 28,
        NoCorpse                         = 29,
        UnderLifestoneProtection         = 30,
        ItemManaUpdatePending            = 31,
        [Ephemeral]
        GeneratorStatus                  = 32,
        [Ephemeral]
        ResetMessagePending              = 33,
        DefaultOpen                      = 34,
        DefaultLocked                    = 35,
        DefaultOn                        = 36,
        OpenForBusiness                  = 37,
        IsFrozen                         = 38,
        DealMagicalItems                 = 39,
        LogoffImDead                     = 40,
        ReportCollisionsAsEnvironment    = 41,
        AllowEdgeSlide                   = 42,
        AdvocateQuest                    = 43,
        [Ephemeral][SendOnLogin]
        IsAdmin                          = 44,
        [Ephemeral][SendOnLogin]
        IsArch                           = 45,
        [Ephemeral][SendOnLogin]
        IsSentinel                       = 46,
        [SendOnLogin]
        IsAdvocate                       = 47,
        CurrentlyPoweringUp              = 48,
        [Ephemeral]
        GeneratorEnteredWorld            = 49,
        NeverFailCasting                 = 50,
        VendorService                    = 51,
        AiImmobile                       = 52,
        DamagedByCollisions              = 53,
        IsDynamic                        = 54,
        IsHot                            = 55,
        IsAffecting                      = 56,
        AffectsAis                       = 57,
        SpellQueueActive                 = 58,
        [Ephemeral]
        GeneratorDisabled                = 59,
        IsAcceptingTells                 = 60,
        LoggingChannel                   = 61,
        OpensAnyLock                     = 62,
        [AssessmentProperty]
        UnlimitedUse                     = 63,
        GeneratedTreasureItem            = 64,
        IgnoreMagicResist                = 65,
        IgnoreMagicArmor                 = 66,
        AiAllowTrade                     = 67,
        [SendOnLogin]
        SpellComponentsRequired          = 68,
        [AssessmentProperty]
        IsSellable                       = 69,
        IgnoreShieldsBySkill             = 70,
        NoDraw                           = 71,
        ActivationUntargeted             = 72,
        HouseHasGottenPriorityBootPos    = 73,
        [Ephemeral]
        GeneratorAutomaticDestruction    = 74,
        HouseHooksVisible                = 75,
        HouseRequiresMonarch             = 76,
        HouseHooksEnabled                = 77,
        HouseNotifiedHudOfHookCount      = 78,
        AiAcceptEverything               = 79,
        IgnorePortalRestrictions         = 80,
        RequiresBackpackSlot             = 81,
        DontTurnOrMoveWhenGiving         = 82,
        NpcLooksLikeObject               = 83,
        IgnoreCloIcons                   = 84,
        AppraisalHasAllowedWielder       = 85,
        ChestRegenOnClose                = 86,
        LogoffInMinigame                 = 87,
        PortalShowDestination            = 88,
        PortalIgnoresPkAttackTimer       = 89,
        NpcInteractsSilently             = 90,
        [AssessmentProperty]
        Retained                         = 91,
        IgnoreAuthor                     = 92,
        Limbo                            = 93,
        [AssessmentProperty]
        AppraisalHasAllowedActivator     = 94,
        ExistedBeforeAllegianceXpChanges = 95,
        IsDeaf                           = 96,
        [Ephemeral][SendOnLogin]
        IsPsr                            = 97,
        Invincible                       = 98,
        [AssessmentProperty]
        Ivoryable                        = 99,
        [AssessmentProperty]
        Dyable                           = 100,
        CanGenerateRare                  = 101,
        CorpseGeneratedRare              = 102,
        NonProjectileMagicImmune         = 103,
        [SendOnLogin]
        ActdReceivedItems                = 104,
        Unknown105                       = 105,
        [Ephemeral]
        FirstEnterWorldDone              = 106,
        RecallsDisabled                  = 107,
        [AssessmentProperty]
        RareUsesTimer                    = 108,
        ActdPreorderReceivedItems        = 109,
        [Ephemeral]
        Afk                              = 110,
        IsGagged                         = 111,
        ProcSpellSelfTargeted            = 112,
        IsAllegianceGagged               = 113,
        EquipmentSetTriggerPiece         = 114,
        Uninscribe                       = 115,
        WieldOnUse                       = 116,
        ChestClearedWhenClosed           = 117,
        NeverAttack                      = 118,
        SuppressGenerateEffect           = 119,
        TreasureCorpse                   = 120,
        EquipmentSetAddLevel             = 121,
        BarberActive                     = 122,
        TopLayerPriority                 = 123,
        [SendOnLogin]
        NoHeldItemShown                  = 124,
        [SendOnLogin]
        LoginAtLifestone                 = 125,
        OlthoiPk                         = 126,
        [SendOnLogin]
        Account15Days                    = 127,
        HadNoVitae                       = 128,
        NoOlthoiTalk                     = 129,
        [AssessmentProperty]
        AutowieldLeft                    = 130,
        IsMule                           = 131,
        RandomizeSpawnTime               = 132,

        /* custom */
        LinkedPortalOneSummon            = 9001,
        LinkedPortalTwoSummon            = 9002,
        HouseEvicted                     = 9003,
        UntrainedSkills                  = 9004,
        [Ephemeral]
        IsEnvoy                          = 9005,
        UnspecializedSkills              = 9006,
        FreeSkillResetRenewed            = 9007,
        FreeAttributeResetRenewed        = 9008,
        SkillTemplesTimerReset           = 9009,
        FreeMasteryResetRenewed          = 9010,
        ExcludeFromLeaderboards          = 9011,
        IsVPHardcore                     = 9012,
        DisableCreate                    = 9013,
        CanEnrage                        = 9014,
        CanGrapple                       = 9015,
        CanAOE                           = 9016,
        EnragedHotspot                   = 9017,
        [AssessmentProperty]
        SplitArrows                      = 9030,
        IsSplitArrow                     = 9031,
        LastHitWasSplitArrow             = 9032,
        IsSplitArrowKill                 = 9033,
        [AssessmentProperty]
        IsCharm                          = 9040,
        AllowFriendlyPlayerDamage       = 9043,
        BreakPeaceOnHostileAction       = 9044,
        /// <summary>
        /// If TRUE on a weapon, allows multi-strike hits to each roll a proc with decay
        /// </summary>
        WeaponAllowMultiStrikeProcs      = 9035,
        /// <summary>
        /// If TRUE on a weapon, its cast-on-strike can proc on cleaved targets
        /// </summary>
        WeaponProcOnCleaveTargets        = 9036,

        // Monster Capture System - POC
        IsCaptureCrystal                 = 9037,
        IsCapturedAppearance             = 9038,
        /// <summary>
        /// PURPOSE:
        /// This property is designed for scenarios where precise damage control is needed.
        /// This is useful for retail quests such as Gerraine or capturing monsters below 20% health
        /// 
        /// WHAT IT DOES:
        /// - Only works for melee attacks
        /// - When enabled, this weapon is unaffected by damage calculations, modifiers, crits, and buffs
        /// - The damage range is fixed to the visible damage on the item
        /// - For example, a weapon with damage range 1-500 will always deal between 1-500 
        ///     damage, regardless of player stats, buffs, or target defenses.
        /// 
        /// WHEN TO USE:
        /// - On weapons specifically designed for monster capture/retail quests. Use sparingly.
        /// </summary>
        [AssessmentProperty]
        UseDamageCap                      = 9039,

        // Resonance Lens System (Second-Chance Shiny Capture)
        /// <summary>
        /// If TRUE, this lens is a Resonance Lens that grants a second-chance capture attempt
        /// after a failed shiny capture. Only usable once per failed shiny encounter.
        /// </summary>
        IsResonanceLens                   = 9041,

        /// <summary>
        /// If TRUE, this lens is Asheron's Lens which guarantees 100% capture success.
        /// Single-use, extremely rare item obtained by exchanging 5000 Flawed Siphon Lenses.
        /// </summary>
        IsGuaranteedCaptureLens           = 9042,
        /// <summary>If TRUE, item has unlimited uses and will never be consumed.</summary>
        [AssessmentProperty]
        InfiniteCharges                   = 9045,
        /// <summary>
        /// If TRUE, this world object suppresses configured spell schools even when not awake/aggro.
        /// </summary>
        IsPassiveSpellSuppressor          = 9046,

        /// <summary>Combat pet essence: bond attunement enabled for this device.</summary>
        [AssessmentProperty]
        PetBondAttuned                   = 9047,
        /// <summary>Combat pet uses captured creature weapon appearance/skin.</summary>
        CombatPetCaptureSkinWeapon       = 9048,
        /// <summary>While active (Summon Essence Refill charm WCID 78780030), opt-in for per-charge pyreal refill on summon. See docs/ADMIN_PET_SUMMON_CHARMS.md.</summary>
        PetDevicePyrealAutoRefillEnrolled = 9049,

        // -- ILT Ability Charm System (50000-50099) ---------------------------------
        /// <summary>On an ability charm item: marks it as an ability-granting charm.</summary>
        IsAbilityCharm   = 50000,
        /// <summary>On an ability charm item: true when the charm has been activated/consumed.</summary>
        IsCharmActivated = 50001,
        /// <summary>On an ability charm item: true for limited trial charms that expire.</summary>
        IsTestCharm      = 50002,

        // -- ILT Ability Flags - players (50010+) ------------------------------------
        /// <summary>Player has the Mana Barrier ability active (absorbs damage using mana).</summary>
        HasManaBarrier   = 50010,

        /// <summary>ILT Skill #2: Melee attacks cost stamina for bonus damage.</summary>
        HasHeavySwing = 50011,

        /// <summary>ILT Skill #3: Missile/bow attacks cost stamina for bonus damage.</summary>
        HasHeavyDraw = 50012,

        /// <summary>ILT Skill #4: Magic attacks cost mana for bonus damage.</summary>
        HasFocusedCasting = 50013,
        HasChaining       = 50014,
        HasRepeater       = 50015,
        HasBloodLetting   = 50017,

        /// <summary>ILT Skill #16: Spells are cast without consuming components while charm is in inventory.</summary>
        HasInfiniteCasting = 50028,

        /// <summary>ILT Player Pref: Show [Overkill] suffix on kill/death messages. Default ON.</summary>
        ShowOverkill = 50029,

        /// <summary>Player has Asheron's Favor active — permanently maintains Asheron's Lesser Benediction (+Health) and Blackmoor's Favor (+Natural Armor).</summary>
        HasAsheronsFavor = 50030,

        /// <summary>ILT Skill #18: Imbue success chance is increased while charm is in inventory.</summary>
        HasArtisanCharm  = 50031,

        /// <summary>ILT Skill #19: Tectonic Rifts I/II are redirected to Rocky Shrapnel while charm is active (requires Rocky Shrapnel learned).</summary>
        HasShrapnelCharm = 50032,

        /// <summary>ILT Skill #20: Tectonic Rifts I is redirected to Ring of Unspeakable Agony while charm is active (requires Agony learned). Rocky Shrapnel takes priority if both charms are active.</summary>
        HasAgonyCharm    = 50033,

        /// <summary>ILT Player Pref: Use classic physics-collision ring spell behavior (can multi-hit through positioning). Default OFF = new guaranteed AOE system.</summary>
        ClassicRingAoe   = 50034,

        /// <summary>Player has Penta Cast active — Streak, Arc, and Bolt spells hit up to 5 distinct targets in alternating proximity order.</summary>
        HasPentaCast     = 50035,

        /// <summary>ILT Skill #21: When an arrow hits an enemy, casts a damage-type-matched ring spell centered on the target.</summary>
        HasExplosiveArrowCharm = 50036,

        /// <summary>ILT Skill #22: Melee attacks scan target resistances, override damage to weakest element, and dynamically match weapon rends.</summary>
        HasPrismaticStrike = 50037,

        /// <summary>While active (Universal Summoning Mastery charm WCID 78780031), bypass PetDevice vs player PropertyInt 362 check. Does not change player mastery 362. See docs/ADMIN_PET_SUMMON_CHARMS.md.</summary>
        HasUniversalSummoningMastery = 50038,

        /// <summary>Player has the Fork Charm active — Streak, Arc, and Bolt projectiles fork to nearby enemies on hit.</summary>
        HasForkCharm = 50039,

        /// <summary>Player has the Far Shot Charm active — increases missile weapon attack range and final damage.</summary>
        HasFarShotCharm = 50040,

        // -- ILT Player UI Preferences -> see PropertyInt.DamageNumberFormat (50101) --
    }
}

