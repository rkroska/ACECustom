using ACE.Server.Network.GameAction;
using ACE.Server.Network.GameMessages;
using System;

namespace ACE.Server.Entity.Actions
{
    public enum ActionType : int
    {
        AdminCommands_HandleActionQueryHouse,
        AdvocateFane_Bestow,
        AdvocateFane_Reset,
        Aetheria_ActivateSigil,
        AllegianceManager_DoHandlePlayerDelete,
        AllegianceManager_DoPassXP,
        Bindstone_EnqueueBroadcastMotion,
        Bindstone_SetSanctuary,
        Chest_Reset,
        ConfirmationManager_EnqueueAbort,
        Container_FinishClose,
        Container_Reset,
        Container_SortBiotasIntoInventory,
        Corpse_DiscoverGeneratedRare,
        CreatureDeath_MakeCorpse,
        CreatureDeath_SaveInParallelCallback,
        CreatureEquipment_TryActivateItemSpellsOnWield,
        CreatureMissile_EnqueueBroadcast,
        CreatureMissile_EnsureAmmoVisible,
        CreatureNavigation_AddMoveToTick,
        CreatureNavigation_Rotate,
        CreatureNavigation_TurnToPosition,
        CreatureNavigation_TurnToTarget,
        CreatureNetworking_DoWorldBroadcast,
        Door_FinalizeClose,
        Door_Reset,
        Door_SetNotBusy,
        EmoteManager_CastSpell,
        EmoteManager_DebugDelay,
        EmoteManager_DoEnqueue,
        EmoteManager_ExecuteMotion,
        EmoteManager_Give,
        EmoteManager_OnDamage,
        EmoteManager_ReduceNested,
        Enlightenment_DoEnlighten,
        Game_GameOverCleanup,
        Game_GameOverStats,
        GamePiece_ApplyDeadVisuals,
        GamePiece_Destroy,
        GamePiece_EnqueueDeadMotion,
        Healer_DoHealing,
        Healer_SendMotion,
        Hotspot_ActionLoop,
        HouseManager_HandlePlayerDelete,
        Landblock_ClearFlagsAfterSave,
        Landblock_CreateWorldObjects,
        Landblock_Init,
        Landblock_SpawnDynamicShardObjects,
        Landblock_SpawnEncounters,
        Landblock_TeleportPlayerAfterFailureToAdd,
        Lifestone_EnqueueSound,
        Lifestone_Use,
        Lock_UseUnlocker,
        MonsterAwareness_CheckTargetsInner,
        MonsterCombat_DeleteObjectAfterDelay,
        MonsterMagic_CastSpell,
        MonsterMelee_DoAttack,
        MonsterMissile_EnqueueBroadcast,
        MonsterMissile_LaunchMissile,
        MonsterMissile_LaunchMissileInner,
        MonsterMissile_SetStance,
        MonsterMissile_SwitchToMeleeAttack,
        MonsterMissile_SwitchToMeleeAttackInner,
        MonsterMissile_SwitchToMeleeAttackInnerInner,
        MonsterNavigation_Sleep,
        PetDevice_Refill,
        PhysicsObj_TrackObject,
        PhysicsObj_TrackObjects,
        PKModifier_EnqueueDissonanceAndReset,
        PKModifier_TogglePKStauts,
        Player_FinalizeLogout,
        Player_ForceLogOff,
        Player_PKLiteSetState,
        Player_PKLiteStartTransition,
        Player_SendNonCombatStance,
        Player_SetNonBusy,
        PlayerAllegiance_HandleLogin,
        PlayerDatabase_SaveBiotasInParallelCallback,
        PlayerCombat_ChangeCombatMode,
        PlayerCombat_ChangeCombatModeCallback,
        PlayerCombat_SetActionType,
        PlayerCombat_SetCombatMode,
        PlayerDeath_Broadcast,
        PlayerDeath_CreateCorpseAndTeleport,
        PlayerDeath_EnqueueTeleport,
        PlayerDeath_HandleSuicide,
        PlayerDeath_Teleport,
        PlayerHouse_HandleActionQueryHouse,
        PlayerHouse_HandleEvictionOnLogin,
        PlayerHouse_NotificationsOnLogin,
        PlayerHouse_SetHouseDataOnOwnerChange,
        PlayerLocation_ClearFogColor,
        PlayerLocation_DoPreTeleportHide,
        PlayerLocation_OnTeleportComplete,
        PlayerLocation_TeleportToAllegianceHometown,
        PlayerLocation_TeleportToAllegianceMansion,
        PlayerLocation_TeleportToHouse,
        PlayerLocation_TeleportToLifestone,
        PlayerLocation_TeleportToMarketplace,
        PlayerLocation_TeleportToPKArena,
        PlayerLocation_TeleportToPKLArena,
        PlayerLocation_TeleToPosition,
        PlayerInventory_AddObjectToLandblock,
        PlayerInventory_DoPickup,
        PlayerInventory_DropItem,
        PlayerInventory_GetAndWieldInventory,
        PlayerInventory_GiveObjectToPlayer,
        PlayerInventory_RemoveTrackedObject,
        PlayerInventory_SetPickupDone,
        PlayerInventory_StackableMerge,
        PlayerInventory_StackableSplitToContainer,
        PlayerInventory_StackableSplitToLandblock,
        PlayerInventory_StackableSplitToWield,
        PlayerMagic_DoCastSpell,
        PlayerMagic_DoCastSpellOnMotionDone,
        PlayerMagic_FastTick,
        PlayerMagic_FinishCast,
        PlayerMagic_OnMoveComplete,
        PlayerMagic_RecordCast,
        PlayerMagic_ReturnToReadyStance,
        PlayerMagic_StartCastingGesture,
        PlayerManager_HandleDeletePlayer,
        PlayerManager_ProcessDeletedPlayer,
        PlayerMelee_Attack,
        PlayerMelee_AttackInner,
        PlayerMelee_HandleTargetedAttack,
        PlayerMelee_PowerbarRefill,
        PlayerMissile_LaunchMissile,
        PlayerMissile_LaunchProjectile,
        PlayerMissile_OutOfAmmo,
        PlayerMissile_PlaceNewAmmo,
        PlayerMissile_PowerbarRefill,
        PlayerMissile_SetAttacking,
        PlayerMotion_SendMotionAsCommands,
        PlayerMove_MoveToChain,
        PlayerMove_MoveToTick,
        PlayerMove_SetLastMoveAndCallCallback,
        PlayerNetworking_EnqueueSend,
        PlayerNetworking_SendShutdownMessage,
        PlayerSkills_HandleActionRaiseSkill,
        PlayerSkills_SendFreeAttributeResetRenewedMessage,
        PlayerSkills_SendFreeResetRenewedMessage,
        PlayerSkills_SendInstallDirtyFightingPatchesMessage,
        PlayerSkills_SendInstallSummoningPatchesMessage,
        PlayerSkills_SendInstallVoidMagicPatchesMessage,
        PlayerSkills_SendMasteriesResetRenewedMessage,
        PlayerSkills_SendSkillTemplesResetMessage,
        PlayerSkills_SendSpecializedSkillResetMessage,
        PlayerSkills_SendTrainedSkillResetMessage,
        PlayerSpells_HandleMaxVitalUpdate,
        PlayerTick_RemoveSpellsOnItemManaDepleted,
        PlayerTracking_CloakStep1,
        PlayerTracking_CloakStep2,
        PlayerTracking_CloakStep3,
        PlayerTracking_CloakStep4,
        PlayerTracking_DeCloakStep1,
        PlayerTracking_DeCloakStep2,
        PlayerTracking_DeCloakStep3,
        PlayerTracking_DeCloakStep4,
        PlayerTrade_EnqueueSendAddToTrade,
        PlayerTrade_FinalizeTrade,
        PlayerUse_ApplyConsumableAction,
        PlayerUse_SendUseDoneEvent,
        PlayerUse_SetNonBusy,
        PlayerXp_HandleMissingXp,
        PlayerXp_ItemIncreasedInPower,
        PlayerXp_RemoveVitae,
        PlayerXp_UpdateXpAndLevel,
        PlayerXp_FlushBatchedUpdate,
        Portal_Teleport,
        RecipeManager_FinishRecipe,
        RecipeManager_HandleRecipe,
        RecipeManager_ShowDialogue,
        Scroll_Read,
        SpellProjectile_Destroy,
        Switch_BaseOnActivate,
        Tailoring_DoTailoring,
        Vendor_ApplyService,
        Vendor_Approach,
        Vendor_CheckClose,
        Vendor_LoadInventory,
        Vendor_SetNotBusy,
        WorldManager_DisconnectAllSessions,
        WorldManager_DoPlayerEnterWorld,
        WorldManager_LogOffAllPlayers,
        WorldManager_PlayerEnterWorld,
        WorldManager_ThreadSafeTeleport,
        WorldObject_Destroy,
        WorldObjectDecay_Destroy,
        WorldObjectMagic_AdjustDungeonAndTeleportPlayer,
        WorldObjectMagic_TryProcCloakSpell,
        WorldObjectNetworking_BroadcastOther,
        WorldObjectNetworking_BroadcastSelf,
        WorldObjectNetworking_EnqueueMotion,
        WorldObjectNetworking_EnqueueMotionForce,
        WorldObjectNetworking_EnqueueMotionMagic,
        WorldObjectNetworking_EnqueueMotionMagicAction,
        WorldObjectNetworking_EnqueueMotionMagicPersist,
        WorldObjectNetworking_EnqueueMotionMissile,
        WorldObjectNetworking_EnqueueMotionMissilePersist,

        // GameMessage values map 1:1 with networking OpTypes.
        GameMessage_Unknown,
        GameMessage_None,
        GameMessage_InventoryRemoveObject,
        GameMessage_SetStackSize,
        GameMessage_PlayerKilled,
        GameMessage_EmoteText,
        GameMessage_SoulEmote,
        GameMessage_HearSpeech,
        GameMessage_HearRangedSpeech,
        GameMessage_PrivateUpdatePropertyInt,
        GameMessage_PublicUpdatePropertyInt,
        GameMessage_PrivateUpdatePropertyInt64,
        GameMessage_PublicUpdatePropertyInt64,
        GameMessage_PrivateUpdatePropertyBool,
        GameMessage_PublicUpdatePropertyBool,
        GameMessage_PrivateUpdatePropertyFloat,
        GameMessage_PublicUpdatePropertyFloat,
        GameMessage_PrivateUpdatePropertyString,
        GameMessage_PublicUpdatePropertyString,
        GameMessage_PrivateUpdatePropertyDataID,
        GameMessage_PublicUpdatePropertyDataID,
        GameMessage_PrivateUpdatePropertyInstanceID,
        GameMessage_PublicUpdateInstanceId,
        GameMessage_PrivateUpdatePosition,
        GameMessage_PublicUpdatePosition,
        GameMessage_PrivateUpdateSkill,
        GameMessage_PublicUpdateSkill,
        GameMessage_PrivateUpdateSkillLevel,
        GameMessage_PublicUpdateSkillLevel,
        GameMessage_PrivateUpdateAttribute,
        GameMessage_PublicUpdateAttribute,
        GameMessage_PrivateUpdateVital,
        GameMessage_PublicUpdateVital,
        GameMessage_PrivateUpdateAttribute2ndLevel,
        GameMessage_AdminEnvirons,
        GameMessage_PositionAndMovement,
        GameMessage_ObjDescEvent,
        GameMessage_CharacterCreateOrRestoreResponse,
        GameMessage_CharacterLogOff,
        GameMessage_CharacterDelete,
        GameMessage_CharacterCreate,
        GameMessage_CharacterEnterWorld,
        GameMessage_CharacterList,
        GameMessage_CharacterError,
        GameMessage_ForceObjectDescSend,
        GameMessage_ObjectCreate,
        GameMessage_PlayerCreate,
        GameMessage_ObjectDelete,
        GameMessage_UpdatePosition,
        GameMessage_ParentEvent,
        GameMessage_PickupEvent,
        GameMessage_SetState,
        GameMessage_MovementEvent,
        GameMessage_VectorUpdate,
        GameMessage_Sound,
        GameMessage_PlayerTeleport,
        GameMessage_AutonomousPosition,
        GameMessage_PlayScriptId,
        GameMessage_PlayEffect,
        GameMessage_GameEvent,
        GameMessage_GameAction,
        GameMessage_AccountBanned,
        GameMessage_CharacterEnterWorldRequest,
        GameMessage_GetServerVersion,
        GameMessage_FriendsOld,
        GameMessage_CharacterRestore,
        GameMessage_AccountBoot,
        GameMessage_UpdateObject,
        GameMessage_TurbineChat,
        GameMessage_CharacterEnterWorldServerReady,
        GameMessage_ServerMessage,
        GameMessage_ServerName,
        GameMessage_DDD_DataMessage,
        GameMessage_DDD_RequestDataMessage,
        GameMessage_DDD_ErrorMessage,
        GameMessage_DDD_Interrogation,
        GameMessage_DDD_InterrogationResponse,
        GameMessage_DDD_BeginDDD,
        GameMessage_DDD_BeginPullDDD,
        GameMessage_DDD_IterationData,
        GameMessage_DDD_EndDDD,

        // GameAction values map 1:1 with networking GameActionTypes.
        GameAction_SetSingleCharacterOption,
        GameAction_TargetedMeleeAttack,
        GameAction_TargetedMissileAttack,
        GameAction_SetAfkMode,
        GameAction_SetAfkMessage,
        GameAction_Talk,
        GameAction_RemoveFriend,
        GameAction_AddFriend,
        GameAction_PutItemInContainer,
        GameAction_GetAndWieldItem,
        GameAction_DropItem,
        GameAction_SwearAllegiance,
        GameAction_BreakAllegiance,
        GameAction_AllegianceUpdateRequest,
        GameAction_RemoveAllFriends,
        GameAction_TeleToPklArena,
        GameAction_TeleToPkArena,
        GameAction_TitleSet,
        GameAction_QueryAllegianceName,
        GameAction_ClearAllegianceName,
        GameAction_TalkDirect,
        GameAction_SetAllegianceName,
        GameAction_UseWithTarget,
        GameAction_Use,
        GameAction_SetAllegianceOfficer,
        GameAction_SetAllegianceOfficerTitle,
        GameAction_ListAllegianceOfficerTitles,
        GameAction_ClearAllegianceOfficerTitles,
        GameAction_DoAllegianceLockAction,
        GameAction_SetAllegianceApprovedVassal,
        GameAction_AllegianceChatGag,
        GameAction_DoAllegianceHouseAction,
        GameAction_RaiseVital,
        GameAction_RaiseAttribute,
        GameAction_RaiseSkill,
        GameAction_TrainSkill,
        GameAction_CastUntargetedSpell,
        GameAction_CastTargetedSpell,
        GameAction_ChangeCombatMode,
        GameAction_StackableMerge,
        GameAction_StackableSplitToContainer,
        GameAction_StackableSplitTo3D,
        GameAction_ModifyCharacterSquelch,
        GameAction_ModifyAccountSquelch,
        GameAction_ModifyGlobalSquelch,
        GameAction_Tell,
        GameAction_Buy,
        GameAction_Sell,
        GameAction_TeleToLifestone,
        GameAction_LoginComplete,
        GameAction_FellowshipCreate,
        GameAction_FellowshipQuit,
        GameAction_FellowshipDismiss,
        GameAction_FellowshipRecruit,
        GameAction_FellowshipUpdateRequest,
        GameAction_BookData,
        GameAction_BookModifyPage,
        GameAction_BookAddPage,
        GameAction_BookDeletePage,
        GameAction_BookPageData,
        GameAction_SetInscription,
        GameAction_IdentifyObject,
        GameAction_GiveObjectRequest,
        GameAction_AdvocateTeleport,
        GameAction_AbuseLogRequest,
        GameAction_AddChannel,
        GameAction_RemoveChannel,
        GameAction_ChatChannel,
        GameAction_ListChannels,
        GameAction_IndexChannels,
        GameAction_NoLongerViewingContents,
        GameAction_StackableSplitToWield,
        GameAction_AddShortCut,
        GameAction_RemoveShortCut,
        GameAction_SetCharacterOptions,
        GameAction_RemoveSpellC2S,
        GameAction_CancelAttack,
        GameAction_QueryHealth,
        GameAction_QueryAge,
        GameAction_QueryBirth,
        GameAction_Emote,
        GameAction_SoulEmote,
        GameAction_AddSpellFavorite,
        GameAction_RemoveSpellFavorite,
        GameAction_PingRequest,
        GameAction_OpenTradeNegotiations,
        GameAction_CloseTradeNegotiations,
        GameAction_AddToTrade,
        GameAction_AcceptTrade,
        GameAction_DeclineTrade,
        GameAction_ResetTrade,
        GameAction_ClearPlayerConsentList,
        GameAction_DisplayPlayerConsentList,
        GameAction_RemoveFromPlayerConsentList,
        GameAction_AddPlayerPermission,
        GameAction_RemovePlayerPermission,
        GameAction_BuyHouse,
        GameAction_HouseQuery,
        GameAction_AbandonHouse,
        GameAction_RentHouse,
        GameAction_SetDesiredComponentLevel,
        GameAction_AddPermanentGuest,
        GameAction_RemovePermanentGuest,
        GameAction_SetOpenHouseStatus,
        GameAction_ChangeStoragePermission,
        GameAction_BootSpecificHouseGuest,
        GameAction_RemoveAllStoragePermission,
        GameAction_RequestFullGuestList,
        GameAction_SetMotd,
        GameAction_QueryMotd,
        GameAction_ClearMotd,
        GameAction_QueryLord,
        GameAction_AddAllStoragePermission,
        GameAction_RemoveAllPermanentGuests,
        GameAction_BootEveryone,
        GameAction_TeleToHouse,
        GameAction_QueryItemMana,
        GameAction_SetHooksVisibility,
        GameAction_ModifyAllegianceGuestPermission,
        GameAction_ModifyAllegianceStoragePermission,
        GameAction_ChessJoin,
        GameAction_ChessQuit,
        GameAction_ChessMove,
        GameAction_ChessMovePass,
        GameAction_ChessStalemate,
        GameAction_ListAvailableHouses,
        GameAction_ConfirmationResponse,
        GameAction_BreakAllegianceBoot,
        GameAction_TeleToMansion,
        GameAction_Suicide,
        GameAction_AllegianceInfoRequest,
        GameAction_CreateTinkeringTool,
        GameAction_SpellbookFilter,
        GameAction_TeleToMarketPlace,
        GameAction_EnterPkLite,
        GameAction_FellowshipAssignNewLeader,
        GameAction_FellowshipChangeOpenness,
        GameAction_AllegianceChatBoot,
        GameAction_AddAllegianceBan,
        GameAction_RemoveAllegianceBan,
        GameAction_ListAllegianceBans,
        GameAction_RemoveAllegianceOfficer,
        GameAction_ListAllegianceOfficers,
        GameAction_ClearAllegianceOfficers,
        GameAction_RecallAllegianceHometown,
        GameAction_QueryPluginListResponse,
        GameAction_QueryPluginResponse,
        GameAction_FinishBarber,
        GameAction_AbandonContract,
        GameAction_Jump,
        GameAction_MoveToState,
        GameAction_DoMovementCommand,
        GameAction_TurnTo,
        GameAction_StopMovementCommand,
        GameAction_ForceObjectDescSend,
        GameAction_ObjectCreate,
        GameAction_ObjectDelete,
        GameAction_MovementEvent,
        GameAction_ApplySoundEffect,
        GameAction_AutonomyLevel,
        GameAction_AutonomousPosition,
        GameAction_ApplyVisualEffect,
        GameAction_JumpNonAutonomous,

        // These purely exist for control flow / classes.
        ControlFlowLoop,
        ControlFlowConditional,
        ControlFlowDelay,
    }
    public static class ActionTypeConverter
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static ActionType FromInboundGameMessageOpCode(InboundGameMessageOpcode opcode)
        {
            switch (opcode)
            {
                case InboundGameMessageOpcode.None: return ActionType.GameMessage_None;
                case InboundGameMessageOpcode.ForceObjectDescSend: return ActionType.GameMessage_ForceObjectDescSend;
                case InboundGameMessageOpcode.GameAction: return ActionType.GameMessage_GameAction;
                case InboundGameMessageOpcode.GetServerVersion: return ActionType.GameMessage_GetServerVersion;
                case InboundGameMessageOpcode.FriendsOld: return ActionType.GameMessage_FriendsOld;
                case InboundGameMessageOpcode.TurbineChat: return ActionType.GameMessage_TurbineChat;
                case InboundGameMessageOpcode.CharacterLogOff: return ActionType.GameMessage_CharacterLogOff;
                case InboundGameMessageOpcode.CharacterDelete: return ActionType.GameMessage_CharacterDelete;
                case InboundGameMessageOpcode.CharacterCreate: return ActionType.GameMessage_CharacterCreate;
                case InboundGameMessageOpcode.CharacterEnterWorld: return ActionType.GameMessage_CharacterEnterWorld;
                case InboundGameMessageOpcode.CharacterEnterWorldRequest: return ActionType.GameMessage_CharacterEnterWorldRequest;
                case InboundGameMessageOpcode.CharacterRestore: return ActionType.GameMessage_CharacterRestore;
                case InboundGameMessageOpcode.DDD_InterrogationResponse: return ActionType.GameMessage_DDD_InterrogationResponse;
                case InboundGameMessageOpcode.DDD_RequestDataMessage: return ActionType.GameMessage_DDD_RequestDataMessage;
                case InboundGameMessageOpcode.DDD_EndDDD: return ActionType.GameMessage_DDD_EndDDD;
            }
            log.Warn($"Unknown InboundGameMessageOpCode detected: 0x{((int)opcode):X4}:{opcode}");
            return ActionType.GameMessage_Unknown;
        }

        public static ActionType FromGameActionType(GameActionType gameActionType)
        {
            switch (gameActionType)
            {
                case GameActionType.SetSingleCharacterOption: return ActionType.GameAction_SetSingleCharacterOption;
                case GameActionType.TargetedMeleeAttack: return ActionType.GameAction_TargetedMeleeAttack;
                case GameActionType.TargetedMissileAttack: return ActionType.GameAction_TargetedMissileAttack;
                case GameActionType.SetAfkMode: return ActionType.GameAction_SetAfkMode;
                case GameActionType.SetAfkMessage: return ActionType.GameAction_SetAfkMessage;
                case GameActionType.Talk: return ActionType.GameAction_Talk;
                case GameActionType.RemoveFriend: return ActionType.GameAction_RemoveFriend;
                case GameActionType.AddFriend: return ActionType.GameAction_AddFriend;
                case GameActionType.PutItemInContainer: return ActionType.GameAction_PutItemInContainer;
                case GameActionType.GetAndWieldItem: return ActionType.GameAction_GetAndWieldItem;
                case GameActionType.DropItem: return ActionType.GameAction_DropItem;
                case GameActionType.SwearAllegiance: return ActionType.GameAction_SwearAllegiance;
                case GameActionType.BreakAllegiance: return ActionType.GameAction_BreakAllegiance;
                case GameActionType.AllegianceUpdateRequest: return ActionType.GameAction_AllegianceUpdateRequest;
                case GameActionType.RemoveAllFriends: return ActionType.GameAction_RemoveAllFriends;
                case GameActionType.TeleToPklArena: return ActionType.GameAction_TeleToPklArena;
                case GameActionType.TeleToPkArena: return ActionType.GameAction_TeleToPkArena;
                case GameActionType.TitleSet: return ActionType.GameAction_TitleSet;
                case GameActionType.QueryAllegianceName: return ActionType.GameAction_QueryAllegianceName;
                case GameActionType.ClearAllegianceName: return ActionType.GameAction_ClearAllegianceName;
                case GameActionType.TalkDirect: return ActionType.GameAction_TalkDirect;
                case GameActionType.SetAllegianceName: return ActionType.GameAction_SetAllegianceName;
                case GameActionType.UseWithTarget: return ActionType.GameAction_UseWithTarget;
                case GameActionType.Use: return ActionType.GameAction_Use;
                case GameActionType.SetAllegianceOfficer: return ActionType.GameAction_SetAllegianceOfficer;
                case GameActionType.SetAllegianceOfficerTitle: return ActionType.GameAction_SetAllegianceOfficerTitle;
                case GameActionType.ListAllegianceOfficerTitles: return ActionType.GameAction_ListAllegianceOfficerTitles;
                case GameActionType.ClearAllegianceOfficerTitles: return ActionType.GameAction_ClearAllegianceOfficerTitles;
                case GameActionType.DoAllegianceLockAction: return ActionType.GameAction_DoAllegianceLockAction;
                case GameActionType.SetAllegianceApprovedVassal: return ActionType.GameAction_SetAllegianceApprovedVassal;
                case GameActionType.AllegianceChatGag: return ActionType.GameAction_AllegianceChatGag;
                case GameActionType.DoAllegianceHouseAction: return ActionType.GameAction_DoAllegianceHouseAction;
                case GameActionType.RaiseVital: return ActionType.GameAction_RaiseVital;
                case GameActionType.RaiseAttribute: return ActionType.GameAction_RaiseAttribute;
                case GameActionType.RaiseSkill: return ActionType.GameAction_RaiseSkill;
                case GameActionType.TrainSkill: return ActionType.GameAction_TrainSkill;
                case GameActionType.CastUntargetedSpell: return ActionType.GameAction_CastUntargetedSpell;
                case GameActionType.CastTargetedSpell: return ActionType.GameAction_CastTargetedSpell;
                case GameActionType.ChangeCombatMode: return ActionType.GameAction_ChangeCombatMode;
                case GameActionType.StackableMerge: return ActionType.GameAction_StackableMerge;
                case GameActionType.StackableSplitToContainer: return ActionType.GameAction_StackableSplitToContainer;
                case GameActionType.StackableSplitTo3D: return ActionType.GameAction_StackableSplitTo3D;
                case GameActionType.ModifyCharacterSquelch: return ActionType.GameAction_ModifyCharacterSquelch;
                case GameActionType.ModifyAccountSquelch: return ActionType.GameAction_ModifyAccountSquelch;
                case GameActionType.ModifyGlobalSquelch: return ActionType.GameAction_ModifyGlobalSquelch;
                case GameActionType.Tell: return ActionType.GameAction_Tell;
                case GameActionType.Buy: return ActionType.GameAction_Buy;
                case GameActionType.Sell: return ActionType.GameAction_Sell;
                case GameActionType.TeleToLifestone: return ActionType.GameAction_TeleToLifestone;
                case GameActionType.LoginComplete: return ActionType.GameAction_LoginComplete;
                case GameActionType.FellowshipCreate: return ActionType.GameAction_FellowshipCreate;
                case GameActionType.FellowshipQuit: return ActionType.GameAction_FellowshipQuit;
                case GameActionType.FellowshipDismiss: return ActionType.GameAction_FellowshipDismiss;
                case GameActionType.FellowshipRecruit: return ActionType.GameAction_FellowshipRecruit;
                case GameActionType.FellowshipUpdateRequest: return ActionType.GameAction_FellowshipUpdateRequest;
                case GameActionType.BookData: return ActionType.GameAction_BookData;
                case GameActionType.BookModifyPage: return ActionType.GameAction_BookModifyPage;
                case GameActionType.BookAddPage: return ActionType.GameAction_BookAddPage;
                case GameActionType.BookDeletePage: return ActionType.GameAction_BookDeletePage;
                case GameActionType.BookPageData: return ActionType.GameAction_BookPageData;
                case GameActionType.SetInscription: return ActionType.GameAction_SetInscription;
                case GameActionType.IdentifyObject: return ActionType.GameAction_IdentifyObject;
                case GameActionType.GiveObjectRequest: return ActionType.GameAction_GiveObjectRequest;
                case GameActionType.AdvocateTeleport: return ActionType.GameAction_AdvocateTeleport;
                case GameActionType.AbuseLogRequest: return ActionType.GameAction_AbuseLogRequest;
                case GameActionType.AddChannel: return ActionType.GameAction_AddChannel;
                case GameActionType.RemoveChannel: return ActionType.GameAction_RemoveChannel;
                case GameActionType.ChatChannel: return ActionType.GameAction_ChatChannel;
                case GameActionType.ListChannels: return ActionType.GameAction_ListChannels;
                case GameActionType.IndexChannels: return ActionType.GameAction_IndexChannels;
                case GameActionType.NoLongerViewingContents: return ActionType.GameAction_NoLongerViewingContents;
                case GameActionType.StackableSplitToWield: return ActionType.GameAction_StackableSplitToWield;
                case GameActionType.AddShortCut: return ActionType.GameAction_AddShortCut;
                case GameActionType.RemoveShortCut: return ActionType.GameAction_RemoveShortCut;
                case GameActionType.SetCharacterOptions: return ActionType.GameAction_SetCharacterOptions;
                case GameActionType.RemoveSpellC2S: return ActionType.GameAction_RemoveSpellC2S;
                case GameActionType.CancelAttack: return ActionType.GameAction_CancelAttack;
                case GameActionType.QueryHealth: return ActionType.GameAction_QueryHealth;
                case GameActionType.QueryAge: return ActionType.GameAction_QueryAge;
                case GameActionType.QueryBirth: return ActionType.GameAction_QueryBirth;
                case GameActionType.Emote: return ActionType.GameAction_Emote;
                case GameActionType.SoulEmote: return ActionType.GameAction_SoulEmote;
                case GameActionType.AddSpellFavorite: return ActionType.GameAction_AddSpellFavorite;
                case GameActionType.RemoveSpellFavorite: return ActionType.GameAction_RemoveSpellFavorite;
                case GameActionType.PingRequest: return ActionType.GameAction_PingRequest;
                case GameActionType.OpenTradeNegotiations: return ActionType.GameAction_OpenTradeNegotiations;
                case GameActionType.CloseTradeNegotiations: return ActionType.GameAction_CloseTradeNegotiations;
                case GameActionType.AddToTrade: return ActionType.GameAction_AddToTrade;
                case GameActionType.AcceptTrade: return ActionType.GameAction_AcceptTrade;
                case GameActionType.DeclineTrade: return ActionType.GameAction_DeclineTrade;
                case GameActionType.ResetTrade: return ActionType.GameAction_ResetTrade;
                case GameActionType.ClearPlayerConsentList: return ActionType.GameAction_ClearPlayerConsentList;
                case GameActionType.DisplayPlayerConsentList: return ActionType.GameAction_DisplayPlayerConsentList;
                case GameActionType.RemoveFromPlayerConsentList: return ActionType.GameAction_RemoveFromPlayerConsentList;
                case GameActionType.AddPlayerPermission: return ActionType.GameAction_AddPlayerPermission;
                case GameActionType.RemovePlayerPermission: return ActionType.GameAction_RemovePlayerPermission;
                case GameActionType.BuyHouse: return ActionType.GameAction_BuyHouse;
                case GameActionType.HouseQuery: return ActionType.GameAction_HouseQuery;
                case GameActionType.AbandonHouse: return ActionType.GameAction_AbandonHouse;
                case GameActionType.RentHouse: return ActionType.GameAction_RentHouse;
                case GameActionType.SetDesiredComponentLevel: return ActionType.GameAction_SetDesiredComponentLevel;
                case GameActionType.AddPermanentGuest: return ActionType.GameAction_AddPermanentGuest;
                case GameActionType.RemovePermanentGuest: return ActionType.GameAction_RemovePermanentGuest;
                case GameActionType.SetOpenHouseStatus: return ActionType.GameAction_SetOpenHouseStatus;
                case GameActionType.ChangeStoragePermission: return ActionType.GameAction_ChangeStoragePermission;
                case GameActionType.BootSpecificHouseGuest: return ActionType.GameAction_BootSpecificHouseGuest;
                case GameActionType.RemoveAllStoragePermission: return ActionType.GameAction_RemoveAllStoragePermission;
                case GameActionType.RequestFullGuestList: return ActionType.GameAction_RequestFullGuestList;
                case GameActionType.SetMotd: return ActionType.GameAction_SetMotd;
                case GameActionType.QueryMotd: return ActionType.GameAction_QueryMotd;
                case GameActionType.ClearMotd: return ActionType.GameAction_ClearMotd;
                case GameActionType.QueryLord: return ActionType.GameAction_QueryLord;
                case GameActionType.AddAllStoragePermission: return ActionType.GameAction_AddAllStoragePermission;
                case GameActionType.RemoveAllPermanentGuests: return ActionType.GameAction_RemoveAllPermanentGuests;
                case GameActionType.BootEveryone: return ActionType.GameAction_BootEveryone;
                case GameActionType.TeleToHouse: return ActionType.GameAction_TeleToHouse;
                case GameActionType.QueryItemMana: return ActionType.GameAction_QueryItemMana;
                case GameActionType.SetHooksVisibility: return ActionType.GameAction_SetHooksVisibility;
                case GameActionType.ModifyAllegianceGuestPermission: return ActionType.GameAction_ModifyAllegianceGuestPermission;
                case GameActionType.ModifyAllegianceStoragePermission: return ActionType.GameAction_ModifyAllegianceStoragePermission;
                case GameActionType.ChessJoin: return ActionType.GameAction_ChessJoin;
                case GameActionType.ChessQuit: return ActionType.GameAction_ChessQuit;
                case GameActionType.ChessMove: return ActionType.GameAction_ChessMove;
                case GameActionType.ChessMovePass: return ActionType.GameAction_ChessMovePass;
                case GameActionType.ChessStalemate: return ActionType.GameAction_ChessStalemate;
                case GameActionType.ListAvailableHouses: return ActionType.GameAction_ListAvailableHouses;
                case GameActionType.ConfirmationResponse: return ActionType.GameAction_ConfirmationResponse;
                case GameActionType.BreakAllegianceBoot: return ActionType.GameAction_BreakAllegianceBoot;
                case GameActionType.TeleToMansion: return ActionType.GameAction_TeleToMansion;
                case GameActionType.Suicide: return ActionType.GameAction_Suicide;
                case GameActionType.AllegianceInfoRequest: return ActionType.GameAction_AllegianceInfoRequest;
                case GameActionType.CreateTinkeringTool: return ActionType.GameAction_CreateTinkeringTool;
                case GameActionType.SpellbookFilter: return ActionType.GameAction_SpellbookFilter;
                case GameActionType.TeleToMarketPlace: return ActionType.GameAction_TeleToMarketPlace;
                case GameActionType.EnterPkLite: return ActionType.GameAction_EnterPkLite;
                case GameActionType.FellowshipAssignNewLeader: return ActionType.GameAction_FellowshipAssignNewLeader;
                case GameActionType.FellowshipChangeOpenness: return ActionType.GameAction_FellowshipChangeOpenness;
                case GameActionType.AllegianceChatBoot: return ActionType.GameAction_AllegianceChatBoot;
                case GameActionType.AddAllegianceBan: return ActionType.GameAction_AddAllegianceBan;
                case GameActionType.RemoveAllegianceBan: return ActionType.GameAction_RemoveAllegianceBan;
                case GameActionType.ListAllegianceBans: return ActionType.GameAction_ListAllegianceBans;
                case GameActionType.RemoveAllegianceOfficer: return ActionType.GameAction_RemoveAllegianceOfficer;
                case GameActionType.ListAllegianceOfficers: return ActionType.GameAction_ListAllegianceOfficers;
                case GameActionType.ClearAllegianceOfficers: return ActionType.GameAction_ClearAllegianceOfficers;
                case GameActionType.RecallAllegianceHometown: return ActionType.GameAction_RecallAllegianceHometown;
                case GameActionType.QueryPluginListResponse: return ActionType.GameAction_QueryPluginListResponse;
                case GameActionType.QueryPluginResponse: return ActionType.GameAction_QueryPluginResponse;
                case GameActionType.FinishBarber: return ActionType.GameAction_FinishBarber;
                case GameActionType.AbandonContract: return ActionType.GameAction_AbandonContract;
                case GameActionType.Jump: return ActionType.GameAction_Jump;
                case GameActionType.MoveToState: return ActionType.GameAction_MoveToState;
                case GameActionType.DoMovementCommand: return ActionType.GameAction_DoMovementCommand;
                case GameActionType.TurnTo: return ActionType.GameAction_TurnTo;
                case GameActionType.StopMovementCommand: return ActionType.GameAction_StopMovementCommand;
                case GameActionType.ForceObjectDescSend: return ActionType.GameAction_ForceObjectDescSend;
                case GameActionType.ObjectCreate: return ActionType.GameAction_ObjectCreate;
                case GameActionType.ObjectDelete: return ActionType.GameAction_ObjectDelete;
                case GameActionType.MovementEvent: return ActionType.GameAction_MovementEvent;
                case GameActionType.ApplySoundEffect: return ActionType.GameAction_ApplySoundEffect;
                case GameActionType.AutonomyLevel: return ActionType.GameAction_AutonomyLevel;
                case GameActionType.AutonomousPosition: return ActionType.GameAction_AutonomousPosition;
                case GameActionType.ApplyVisualEffect: return ActionType.GameAction_ApplyVisualEffect;
                case GameActionType.JumpNonAutonomous: return ActionType.GameAction_JumpNonAutonomous;
            }
            log.Warn($"Unknown GameActionType detected: 0x{((int)gameActionType):X4}:{gameActionType}");
            return ActionType.GameMessage_Unknown;
        }
    }

    public enum ActionPriority
    {
        High,
        Normal,
        Low
    }

    public interface IAction
    {
        ActionType Type { get; }
        ActionPriority Priority { get; }

        Tuple<IActor, IAction> Act();

        void RunOnFinish(IActor actor, IAction action);
    }
}
