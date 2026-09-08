using System.Numerics;

using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Zone Control BOUNDARY enforcement — a fully standalone system owned by Zone Control.
    /// When any BOUNDED zone exists at the player's variation — enabled or not (owner 2026-07-21:
    /// the boundary is independent of the zone's stat controls) — the union of bounded-zone
    /// landblocks there is the allowlist (<see cref="ZoneControlManager.IsLandblockAllowed"/>).
    /// Stepping outside it: center-screen warning + void FX + 5%-max-HP drain per 2s + a "Guide"
    /// wisp leading back to safety. Near an outside edge: proximity warning + wisp.
    /// Independent of every other system — it consults only ZoneControlManager.
    /// </summary>
    partial class Player
    {
        private double _lastZoneBoundaryCheck;
        private double _lastZoneForbiddenLog;
        private double _lastZoneForbiddenPunishTime;
        private double _zoneOutOfBoundsEntryTime;
        private double _zoneLastDangerTime;
        private Creature _zoneGuideWisp;

        private void CheckZoneBoundary()
        {
            // Throttle to every 2 seconds
            if (Time.GetUnixTime() - _lastZoneBoundaryCheck < 2.0)
                return;

            _lastZoneBoundaryCheck = Time.GetUnixTime();

            // Dungeons are exempt (owner 2026-07-26): the dungeon IS the boundary. Indoor cells
            // use dungeon-local coordinates (map-edge math is meaningless there) and have no
            // walkable path onto neighboring landblocks. Stand down and clear any lingering wisp.
            if (Location.Indoors)
            {
                _zoneOutOfBoundsEntryTime = 0;
                _lastZoneForbiddenPunishTime = 0;
                if (_zoneGuideWisp != null)
                    CleanupZoneBoundaryEffects();
                return;
            }

            var variation = Location.Variation;
            var currentLBVal = (ushort)(Location.Cell >> 16);

            // Gate purely on Zone Control state: no bounded zone at this variation = free roam, zero cost.
            if (!ZoneControlManager.HasBoundedZonesAt(variation) || currentLBVal == 0)
            {
                if (_zoneGuideWisp != null)
                    CleanupZoneBoundaryEffects();   // a boundary that just went inactive must not leave its wisp behind
                return;
            }

            if (!ZoneControlManager.IsLandblockAllowed(variation, currentLBVal))
            {
                // PUNISHMENT ZONE
                var nowOob = Time.GetUnixTime();
                if (_zoneOutOfBoundsEntryTime == 0) _zoneOutOfBoundsEntryTime = nowOob;
                _zoneLastDangerTime = nowOob;

                // Enqueue everything to ensure thread safety (AddWorldObjectInternal must be main thread)
                WorldManager.ActionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_CreateWorldObjects, () =>
                {
                    if (Session == null || Session.State != SessionState.WorldConnected || Health == null)
                        return;
                    var cell = Location?.Cell ?? 0;
                    var lbVal = (ushort)(cell >> 16);
                    if (lbVal != currentLBVal || Location?.Variation != variation)
                        return;
                    if (ZoneControlManager.IsLandblockAllowed(variation, lbVal))
                        return;

                    var nowExec = Time.GetUnixTime();
                    const double forbiddenPunishCooldown = 2.0;
                    if (nowExec - _zoneOutOfBoundsEntryTime <= forbiddenPunishCooldown)
                        return;
                    if (_lastZoneForbiddenPunishTime > 0 && nowExec - _lastZoneForbiddenPunishTime < forbiddenPunishCooldown)
                        return;

                    _lastZoneForbiddenPunishTime = nowExec;

                    if (nowExec - _lastZoneForbiddenLog >= 10.0)
                    {
                        log.Info($"[ZoneControl] Player {Name} ({Guid}) is outside the zone boundary in landblock {currentLBVal:X4} (Var: {variation})");
                        _lastZoneForbiddenLog = nowExec;
                    }

                    // 1. Visuals: Void Particles (no fog — avoids fog/teleport interaction with the boundary)
                    Session.Network.EnqueueSend(new GameMessageScript(Guid, PlayScript.HealthDownVoid));

                    // 2. Text: Center Screen (Yellow) ONLY
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(
                        Session,
                        "!!! YOU ARE LEAVING THE ZONE! RETURN IMMEDIATELY! !!!"));

                    // Lifestone protection spares the boundary drain too (mirrors TakeZoneEffectDamage):
                    // the player still saw the warning + gets the guide wisp above, but takes no HP loss
                    // and cannot be killed by the boundary while protected.
                    if (UnderLifestoneProtection)
                    {
                        HandleLifestoneProtection();
                        UpdateZoneGuideWisp(true, variation);
                        return;
                    }

                    // 3. Damage: Force update vital (Direct HP reduction)
                    // Zone Control Cheat Death window (ZcDamageImmune): warning + wisp still fire, no HP loss.
                    var dmg = (int)(Health.MaxValue * 0.05f);
                    if (dmg < 10) dmg = 10;
                    if (ZcDamageImmune) dmg = 0;

                    UpdateVitalDelta(Health, -dmg);
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(this, Health));

                    UpdateZoneGuideWisp(true, variation);

                    if (Health.Current <= 0 && !IsInDeathProcess)
                    {
                        var selfInfo = new DamageHistoryInfo(this);
                        OnDeath(selfInfo, DamageType.Nether, false);
                        Die(selfInfo, selfInfo);   // explicit attribution: the parameterless Die() would read an empty damage history
                        CleanupZoneBoundaryEffects();
                    }
                }));
            }
            else
            {
                _zoneOutOfBoundsEntryTime = 0;
                _lastZoneForbiddenPunishTime = 0;

                // Edge proximity and/or forbidden current landblock (re-checked live in wisp spawn lambda)
                var danger = IsZoneBoundaryDangerState(variation);

                if (danger)
                {
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(
                        Session, "!!! ZONE BOUNDARY NEARBY !!!"));

                    // Track when we last had danger (for wisp persistence)
                    _zoneLastDangerTime = Time.GetUnixTime();
                }

                // Wisp with 10s persistence after reaching safety — full danger (edge + forbidden re-checked in spawn lambda)
                UpdateZoneGuideWisp(danger, variation);

                // Note: Boundary perimeter lanterns are spawned by the Landblock at load time,
                // so no per-player marker logic is needed here.
            }
        }

        /// <summary>
        /// True when in a landblock outside the Zone Control boundary or within 20m of a map edge that
        /// borders an outside neighbor. Used for guide wisp spawn checks so proximity danger still spawns
        /// when the current landblock is allowed.
        /// </summary>
        private bool IsZoneBoundaryDangerState(int? variation)
        {
            // Dungeon exemption (owner 2026-07-26) - also guards the wisp-spawn lambda's re-check
            // when a player slips indoors between enqueue and execution.
            if (Location.Indoors)
                return false;

            var lbId = new ACE.Entity.LandblockId(Location.Cell);
            var pos = Location.Pos;

            if (pos.X > 182.0f && lbId.LandblockX < 255 && !ZoneControlManager.IsLandblockAllowed(variation, lbId.East.Landblock))
                return true;
            if (pos.X < 10.0f && lbId.LandblockX > 0 && !ZoneControlManager.IsLandblockAllowed(variation, lbId.West.Landblock))
                return true;
            if (pos.Y > 182.0f && lbId.LandblockY < 255 && !ZoneControlManager.IsLandblockAllowed(variation, lbId.North.Landblock))
                return true;
            if (pos.Y < 10.0f && lbId.LandblockY > 0 && !ZoneControlManager.IsLandblockAllowed(variation, lbId.South.Landblock))
                return true;

            return !ZoneControlManager.IsLandblockAllowed(variation, lbId.Landblock);
        }

        /// <summary>
        /// Cleans up player-specific Zone Control boundary effects (guide wisp).
        /// Perimeter lanterns are landblock-owned and don't need per-player cleanup.
        /// </summary>
        public void CleanupZoneBoundaryEffects()
        {
            var capturedWisp = _zoneGuideWisp;
            if (capturedWisp == null)
                return;

            WorldManager.ActionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_CreateWorldObjects, () =>
            {
                capturedWisp.Destroy();
                if (_zoneGuideWisp == capturedWisp)
                    _zoneGuideWisp = null;
            }));
        }

        private void UpdateZoneGuideWisp(bool danger, int? variation)
        {
            if (danger)
            {
                if (_zoneGuideWisp == null)
                {
                    // Spawn Wisp
                    var weenie = DatabaseManager.World.GetCachedWeenie((uint)ACE.Entity.Enum.WeenieClassName.W_WISPETHEREAL_CLASS);
                    if (weenie != null)
                    {
                        // Capture state for thread safety
                        var playerPos = Location.Pos; // Exact player position
                        var cell = Location.Cell;
                        var lbId = new ACE.Entity.LandblockId(cell);

                        WorldManager.ActionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_CreateWorldObjects, () =>
                        {
                            if (Session == null || Session.State != SessionState.WorldConnected)
                                return;

                            // Double check inside queue
                            if (_zoneGuideWisp != null)
                                return;

                            var liveCell = Location?.Cell ?? 0;
                            if (liveCell != cell || Location?.Variation != variation)
                                return;

                            if (!IsZoneBoundaryDangerState(variation))
                                return;

                            var wisp = Factories.WorldObjectFactory.CreateNewWorldObject(weenie) as Creature;
                            if (wisp != null)
                            {
                                wisp.Name = "Guide";
                                wisp.SuppressShardPersistence = true; // ephemeral escort — never save to shard
                                wisp.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Invincible, true);
                                wisp.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.IgnoreCollisions, true);
                                wisp.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Attackable, false);
                                wisp.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.NeverAttack, true);

                                var spawnPos = playerPos + (Vector3.UnitZ * 0.2f);
                                wisp.Location = new ACE.Entity.Position(cell, spawnPos.X, spawnPos.Y, spawnPos.Z, 0, 0, 0, 0, false, variation);

                                if (!wisp.EnterWorld() || wisp.CurrentLandblock == null)
                                {
                                    log.Warn($"[ZoneControl] Failed to enter world for zone boundary guide wisp (player {Name} 0x{Guid.Full:X8}).");
                                    wisp.Destroy();
                                    return;
                                }

                                _zoneGuideWisp = wisp;

                                // Calculate "Smart" Safe Spot
                                uint targetCell = cell;
                                float targetX = 96.0f;
                                float targetY = 96.0f;
                                string safeDir = "None";

                                // Check cardinal neighbors for safety
                                bool eAllowed = lbId.LandblockX < 255 && ZoneControlManager.IsLandblockAllowed(variation, lbId.East.Landblock);
                                bool wAllowed = lbId.LandblockX > 0 && ZoneControlManager.IsLandblockAllowed(variation, lbId.West.Landblock);
                                bool nAllowed = lbId.LandblockY < 255 && ZoneControlManager.IsLandblockAllowed(variation, lbId.North.Landblock);
                                bool sAllowed = lbId.LandblockY > 0 && ZoneControlManager.IsLandblockAllowed(variation, lbId.South.Landblock);

                                if (eAllowed)
                                {
                                    targetCell = lbId.East.Raw;
                                    targetX = 5.0f;
                                    targetY = playerPos.Y;
                                    safeDir = "East";
                                }
                                else if (wAllowed)
                                {
                                    targetCell = lbId.West.Raw;
                                    targetX = 187.0f;
                                    targetY = playerPos.Y;
                                    safeDir = "West";
                                }
                                else if (nAllowed)
                                {
                                    targetCell = lbId.North.Raw;
                                    targetX = playerPos.X;
                                    targetY = 5.0f;
                                    safeDir = "North";
                                }
                                else if (sAllowed)
                                {
                                    targetCell = lbId.South.Raw;
                                    targetX = playerPos.X;
                                    targetY = 187.0f;
                                    safeDir = "South";
                                }
                                else
                                {
                                    safeDir = "Fallback(Center)";
                                }

                                var safePos = new ACE.Entity.Position(targetCell, targetX, targetY, spawnPos.Z, 0, 0, 0, 0, false, variation);

                                log.Debug($"[ZoneControl] Wisp: Spawned at {lbId}. Safe direction: {safeDir}. Move to {safePos} (cell {targetCell:X8}).");

                                wisp.MoveToPosition(safePos);

                                Session.Network.EnqueueSend(new GameMessageSystemChat(
                                    "Follow the Wisp to safety!", ChatMessageType.Broadcast));
                            }
                        }));
                    }
                }
            }
            else
            {
                // Only destroy wisp if we've been safe for 10+ seconds
                if (_zoneGuideWisp != null)
                {
                    var timeSinceDanger = Time.GetUnixTime() - _zoneLastDangerTime;

                    if (timeSinceDanger >= 10.0)
                    {
                        var wispAtEnqueue = _zoneGuideWisp;
                        WorldManager.ActionQueue.EnqueueAction(new ActionEventDelegate(ActionType.Landblock_CreateWorldObjects, () =>
                        {
                            if (Session == null || Session.State != SessionState.WorldConnected)
                                return;

                            if (Time.GetUnixTime() - _zoneLastDangerTime < 10.0)
                                return;

                            if (wispAtEnqueue == null || _zoneGuideWisp != wispAtEnqueue)
                                return;

                            var msg = new GameMessageHearSpeech("Do try to stay on the path next time...", wispAtEnqueue.Name, wispAtEnqueue.Guid.Full, ChatMessageType.Speech);
                            Session.Network.EnqueueSend(msg);

                            wispAtEnqueue.Destroy();
                            _zoneGuideWisp = null;
                        }));
                    }
                }
            }
        }
    }
}
