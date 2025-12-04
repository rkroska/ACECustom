using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages;
using Org.BouncyCastle.Tls;
using System;
using System.Reflection.Emit;
using System.Security.Policy;

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

        // These purely exist for control flow / classes.
        ControlFlowLoop,
        ControlFlowConditional,
        ControlFlowDelay,
    }
    public static class ActionTypeConverter
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static ActionType FromGameMessageOpCode(GameMessageOpcode opcode)
        {
            switch (opcode)
            {
                case GameMessageOpcode.None: return ActionType.GameMessage_None;
                case GameMessageOpcode.InventoryRemoveObject: return ActionType.GameMessage_InventoryRemoveObject;
                case GameMessageOpcode.SetStackSize: return ActionType.GameMessage_SetStackSize;
                case GameMessageOpcode.PlayerKilled: return ActionType.GameMessage_PlayerKilled;
                case GameMessageOpcode.EmoteText: return ActionType.GameMessage_EmoteText;
                case GameMessageOpcode.SoulEmote: return ActionType.GameMessage_SoulEmote;
                case GameMessageOpcode.HearSpeech: return ActionType.GameMessage_HearSpeech;
                case GameMessageOpcode.HearRangedSpeech: return ActionType.GameMessage_HearRangedSpeech;
                case GameMessageOpcode.PrivateUpdatePropertyInt: return ActionType.GameMessage_PrivateUpdatePropertyInt;
                case GameMessageOpcode.PublicUpdatePropertyInt: return ActionType.GameMessage_PublicUpdatePropertyInt;
                case GameMessageOpcode.PrivateUpdatePropertyInt64: return ActionType.GameMessage_PrivateUpdatePropertyInt64;
                case GameMessageOpcode.PublicUpdatePropertyInt64: return ActionType.GameMessage_PublicUpdatePropertyInt64;
                case GameMessageOpcode.PrivateUpdatePropertyBool: return ActionType.GameMessage_PrivateUpdatePropertyBool;
                case GameMessageOpcode.PublicUpdatePropertyBool: return ActionType.GameMessage_PublicUpdatePropertyBool;
                case GameMessageOpcode.PrivateUpdatePropertyFloat: return ActionType.GameMessage_PrivateUpdatePropertyFloat;
                case GameMessageOpcode.PublicUpdatePropertyFloat: return ActionType.GameMessage_PublicUpdatePropertyFloat;
                case GameMessageOpcode.PrivateUpdatePropertyString: return ActionType.GameMessage_PrivateUpdatePropertyString;
                case GameMessageOpcode.PublicUpdatePropertyString: return ActionType.GameMessage_PublicUpdatePropertyString;
                case GameMessageOpcode.PrivateUpdatePropertyDataID: return ActionType.GameMessage_PrivateUpdatePropertyDataID;
                case GameMessageOpcode.PublicUpdatePropertyDataID: return ActionType.GameMessage_PublicUpdatePropertyDataID;
                case GameMessageOpcode.PrivateUpdatePropertyInstanceID: return ActionType.GameMessage_PrivateUpdatePropertyInstanceID;
                case GameMessageOpcode.PublicUpdateInstanceId: return ActionType.GameMessage_PublicUpdateInstanceId;
                case GameMessageOpcode.PrivateUpdatePosition: return ActionType.GameMessage_PrivateUpdatePosition;
                case GameMessageOpcode.PublicUpdatePosition: return ActionType.GameMessage_PublicUpdatePosition;
                case GameMessageOpcode.PrivateUpdateSkill: return ActionType.GameMessage_PrivateUpdateSkill;
                case GameMessageOpcode.PublicUpdateSkill: return ActionType.GameMessage_PublicUpdateSkill;
                case GameMessageOpcode.PrivateUpdateSkillLevel: return ActionType.GameMessage_PrivateUpdateSkillLevel;
                case GameMessageOpcode.PublicUpdateSkillLevel: return ActionType.GameMessage_PublicUpdateSkillLevel;
                case GameMessageOpcode.PrivateUpdateAttribute: return ActionType.GameMessage_PrivateUpdateAttribute;
                case GameMessageOpcode.PublicUpdateAttribute: return ActionType.GameMessage_PublicUpdateAttribute;
                case GameMessageOpcode.PrivateUpdateVital: return ActionType.GameMessage_PrivateUpdateVital;
                case GameMessageOpcode.PublicUpdateVital: return ActionType.GameMessage_PublicUpdateVital;
                case GameMessageOpcode.PrivateUpdateAttribute2ndLevel: return ActionType.GameMessage_PrivateUpdateAttribute2ndLevel;
                case GameMessageOpcode.AdminEnvirons: return ActionType.GameMessage_AdminEnvirons;
                case GameMessageOpcode.PositionAndMovement: return ActionType.GameMessage_PositionAndMovement;
                case GameMessageOpcode.ObjDescEvent: return ActionType.GameMessage_ObjDescEvent;
                case GameMessageOpcode.CharacterCreateResponse: return ActionType.GameMessage_CharacterCreateOrRestoreResponse;
                case GameMessageOpcode.CharacterLogOff: return ActionType.GameMessage_CharacterLogOff;
                case GameMessageOpcode.CharacterDelete: return ActionType.GameMessage_CharacterDelete;
                case GameMessageOpcode.CharacterCreate: return ActionType.GameMessage_CharacterCreate;
                case GameMessageOpcode.CharacterEnterWorld: return ActionType.GameMessage_CharacterEnterWorld;
                case GameMessageOpcode.CharacterList: return ActionType.GameMessage_CharacterList;
                case GameMessageOpcode.CharacterError: return ActionType.GameMessage_CharacterError;
                case GameMessageOpcode.ForceObjectDescSend: return ActionType.GameMessage_ForceObjectDescSend;
                case GameMessageOpcode.ObjectCreate: return ActionType.GameMessage_ObjectCreate;
                case GameMessageOpcode.PlayerCreate: return ActionType.GameMessage_PlayerCreate;
                case GameMessageOpcode.ObjectDelete: return ActionType.GameMessage_ObjectDelete;
                case GameMessageOpcode.UpdatePosition: return ActionType.GameMessage_UpdatePosition;
                case GameMessageOpcode.ParentEvent: return ActionType.GameMessage_ParentEvent;
                case GameMessageOpcode.PickupEvent: return ActionType.GameMessage_PickupEvent;
                case GameMessageOpcode.SetState: return ActionType.GameMessage_SetState;
                case GameMessageOpcode.MovementEvent: return ActionType.GameMessage_MovementEvent;
                case GameMessageOpcode.VectorUpdate: return ActionType.GameMessage_VectorUpdate;
                case GameMessageOpcode.Sound: return ActionType.GameMessage_Sound;
                case GameMessageOpcode.PlayerTeleport: return ActionType.GameMessage_PlayerTeleport;
                case GameMessageOpcode.AutonomousPosition: return ActionType.GameMessage_AutonomousPosition;
                case GameMessageOpcode.PlayScriptId: return ActionType.GameMessage_PlayScriptId;
                case GameMessageOpcode.PlayEffect: return ActionType.GameMessage_PlayEffect;
                case GameMessageOpcode.GameEvent: return ActionType.GameMessage_GameEvent;
                case GameMessageOpcode.GameAction: return ActionType.GameMessage_GameAction;
                case GameMessageOpcode.AccountBanned: return ActionType.GameMessage_AccountBanned;
                case GameMessageOpcode.CharacterEnterWorldRequest: return ActionType.GameMessage_CharacterEnterWorldRequest;
                case GameMessageOpcode.GetServerVersion: return ActionType.GameMessage_GetServerVersion;
                case GameMessageOpcode.FriendsOld: return ActionType.GameMessage_FriendsOld;
                case GameMessageOpcode.CharacterRestore: return ActionType.GameMessage_CharacterRestore;
                case GameMessageOpcode.AccountBoot: return ActionType.GameMessage_AccountBoot;
                case GameMessageOpcode.UpdateObject: return ActionType.GameMessage_UpdateObject;
                case GameMessageOpcode.TurbineChat: return ActionType.GameMessage_TurbineChat;
                case GameMessageOpcode.CharacterEnterWorldServerReady: return ActionType.GameMessage_CharacterEnterWorldServerReady;
                case GameMessageOpcode.ServerMessage: return ActionType.GameMessage_ServerMessage;
                case GameMessageOpcode.ServerName: return ActionType.GameMessage_ServerName;
                case GameMessageOpcode.DDD_DataMessage: return ActionType.GameMessage_DDD_DataMessage;
                case GameMessageOpcode.DDD_RequestDataMessage: return ActionType.GameMessage_DDD_RequestDataMessage;
                case GameMessageOpcode.DDD_ErrorMessage: return ActionType.GameMessage_DDD_ErrorMessage;
                case GameMessageOpcode.DDD_Interrogation: return ActionType.GameMessage_DDD_Interrogation;
                case GameMessageOpcode.DDD_InterrogationResponse: return ActionType.GameMessage_DDD_InterrogationResponse;
                case GameMessageOpcode.DDD_BeginDDD: return ActionType.GameMessage_DDD_BeginDDD;
                case GameMessageOpcode.DDD_BeginPullDDD: return ActionType.GameMessage_DDD_BeginPullDDD;
                case GameMessageOpcode.DDD_IterationData: return ActionType.GameMessage_DDD_IterationData;
                case GameMessageOpcode.DDD_EndDDD: return ActionType.GameMessage_DDD_EndDDD;
            }
            log.Warn($"Unknown GameMessageOpCode detected: 0x{((int)opcode):X4}:{opcode}");
            return ActionType.GameMessage_Unknown;
        }
    }

    public interface IAction
    {
        ActionType Type { get; }

        Tuple<IActor, IAction> Act();

        void RunOnFinish(IActor actor, IAction action);
    }
}
