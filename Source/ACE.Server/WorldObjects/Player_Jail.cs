using ACE.Common.Extensions;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Database;
using ACE.Common;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Enum;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ACE.Server.Factories;

namespace ACE.Server.WorldObjects
{
    public partial class Player
    {
        /// <summary>
        /// Determines whether the player is currently serving a jail sentence.
        /// Uses database-backed PropertyFloat.JailedUntil so sentences survive server restarts/crashes.
        /// </summary>
        public bool IsInJail()
        {
            var jailedUntil = GetProperty(PropertyFloat.JailedUntil);
            return jailedUntil.HasValue && Time.GetUnixTime() < jailedUntil.Value;
        }

        /// <summary>
        /// Helper function to immediately apply the jail punishment to a player. 
        /// Applies tracking properties, ephemeral combat state overrides, and teleports them to the jail boundary.
        /// If the player is already in jail, their sentence is extended to whichever release time is later.
        /// </summary>
        /// <param name="overrideDuration">Sentence length to serve instead of ucm_jail_duration_seconds. Used by the advanced math UCM check, which carries a much shorter sentence.</param>
        /// <param name="countsTowardTotal">
        /// False leaves no permanent record: no TimesJailed increment and no jail quest stamp. The
        /// advanced math check is a joke that most players are meant to fail, and TimesJailed feeds
        /// the /top jails leaderboard - letting comedy sentences onto it would make the board
        /// measure participation in a gag rather than actual discipline. The sentence itself is
        /// served exactly the same either way.
        /// </param>
        public void SendToJail(TimeSpan? overrideDuration = null, bool countsTowardTotal = true)
        {
            TimeSpan jailTime = overrideDuration ?? TimeSpan.FromSeconds(ServerConfig.ucm_jail_duration_seconds.Value);
            double releaseTime = Time.GetUnixTime() + jailTime.TotalSeconds;

            // When already serving, keep whichever sentence runs longer, rather than overwriting:
            // a second offence still resets the clock, because a fresh full-length sentence always
            // ends later than what is left of the old one - but a SHORTER sentence can no longer
            // cut a longer one short. Without this, a player two minutes into the full
            // ucm_jail_duration_seconds who then failed an advanced math check would have their
            // sentence replaced by that check's 60 seconds and walk out early, i.e. failing a
            // second check would be a way to reduce the punishment.
            if (IsInJail())
            {
                var current = GetProperty(PropertyFloat.JailedUntil) ?? 0;
                if (releaseTime > current)
                    SetProperty(PropertyFloat.JailedUntil, releaseTime);
                return;
            }

            SetProperty(PropertyFloat.JailedUntil, releaseTime);

            // Apply jail effects (newly jailed).
            if (countsTowardTotal)
            {
                var jailCount = GetProperty(PropertyInt.TimesJailed) ?? 0;
                if (jailCount < int.MaxValue)
                    SetProperty(PropertyInt.TimesJailed, jailCount + 1);

                QuestManager.Stamp("jail_fresh_meat");
            }
            RedrawPlayerWithUpdates();
            Teleport(GetJailTeleportLocationSafe());
            Session?.Network?.EnqueueSend(new GameMessageSystemChat($"You are being punished. You are now in jail for {jailTime.GetFriendlyLongString()} and are attackable by other players.", ChatMessageType.Broadcast));
        }

        public bool JailEscapeActive { get; set; }
        public DateTime? JailEscapeStartTimeUtc { get; set; }
        public DateTime? JailEscapeEndTimeUtc { get; set; }
        public bool JailEscapeMazeInitialized { get; set; }
        public DateTime? JailEscapeMazeInitializedTimeUtc { get; set; }

        public enum EscapeLightState { None, Green, Yellow, Red }
        public EscapeLightState JailEscapeLightState { get; set; } = EscapeLightState.None;
        public DateTime? JailEscapeNextLightChangeUtc { get; set; }
        public DateTime? JailEscapeStateTransitionTimeUtc { get; set; }
        public DateTime? JailEscapeNextLightEffectUtc { get; set; }
        public List<ObjectGuid> JailEscapeDynamicObjectGuids { get; } = new List<ObjectGuid>();
        /// <summary>Where the player stood when the current red light's grace ended; null until then.</summary>
        public Position JailEscapeFreezePoint { get; set; }
        /// <summary>Set once the player is caught in the current red light, so one red is punished once.</summary>
        public bool JailEscapeCaughtThisRed { get; set; }
        /// <summary>Red-light catches in the current run, for the Flawless tier.</summary>
        public int JailEscapeCatches { get; set; }

        /// <summary>The trial chambers: landblock 0x00C0, variations 2-10.</summary>
        public const int JailEscapeFirstVariation = 2, JailEscapeLastVariation = 10;

        public static bool IsJailEscapeChamber(Position pos) =>
            pos != null && pos.Landblock == 0x00C0 && pos.Variation >= JailEscapeFirstVariation && pos.Variation <= JailEscapeLastVariation;

        private static readonly object EscapeReservationLock = new object();
        private static readonly ConcurrentDictionary<int, uint> ActiveEscapeReservations = new ConcurrentDictionary<int, uint>();

        public static bool TryReserveEscapeVariation(uint playerGuid, int minVariant, int maxVariant, out int assignedVariant)
        {
            lock (EscapeReservationLock)
            {
                assignedVariant = -1;
                // Check if player already has an active reservation to prevent duplicate holdings
                foreach (var kvp in ActiveEscapeReservations)
                {
                    if (kvp.Value == playerGuid)
                    {
                        assignedVariant = kvp.Key;
                        return true; // Re-use existing reservation
                    }
                }
                for (int v = minVariant; v <= maxVariant; v++)
                {
                    if (ActiveEscapeReservations.TryAdd(v, playerGuid))
                    {
                        assignedVariant = v;
                        return true;
                    }
                }
                return false;
            }
        }

        public static void ReleaseEscapeReservation(uint playerGuid)
        {
            lock (EscapeReservationLock)
            {
                foreach (var kvp in ActiveEscapeReservations.ToList())
                {
                    if (kvp.Value == playerGuid)
                    {
                        ActiveEscapeReservations.TryRemove(kvp.Key, out _);
                    }
                }
            }
        }

        public void StartJailEscape()
        {
            if (JailEscapeActive)
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat("You are already attempting to escape!", ChatMessageType.Broadcast));
                return;
            }

            // Only prisoners may try (staff may, for testing). Without this any free player could walk into the
            // jail, which is open, and farm the trial's stamps and leaderboard.
            if (Session?.AccessLevel == AccessLevel.Player && !IsInJail())
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat("Only prisoners can attempt the escape.", ChatMessageType.Broadcast));
                return;
            }

            if (Session?.AccessLevel == AccessLevel.Player && !QuestManager.CanSolve("jail_escape_cooldown"))
            {
                var remaining = QuestManager.GetNextSolveTime("jail_escape_cooldown");
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"You must wait {remaining.GetFriendlyLongString()} before trying to escape again.", ChatMessageType.Broadcast));
                return;
            }

            // Dismiss active pet/summons
            if (CurrentActivePet != null)
            {
                CurrentActivePet.Destroy();
            }

            // Reserve an instance from the variation pool (2 to 10)
            if (!TryReserveEscapeVariation(Guid.Full, JailEscapeFirstVariation, JailEscapeLastVariation, out int variationId))
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat("All jail escape trial chambers are currently occupied. Please try again shortly.", ChatMessageType.Broadcast));
                return;
            }

            // Set flags
            JailEscapeActive = true;
            var timeLimit = TimeSpan.FromMinutes(Math.Max(1.0, ServerConfig.ucm_jail_escape_time_limit_minutes.Value));
            JailEscapeStartTimeUtc = DateTime.UtcNow;
            JailEscapeCatches = 0;
            JailEscapeEndTimeUtc = JailEscapeStartTimeUtc.Value.Add(timeLimit);
            JailEscapeMazeInitialized = false;
            JailEscapeMazeInitializedTimeUtc = null;

            // Reset and initialize the dynamic maze layout
            ResetJailMaze(variationId);

            // Retrieve configuration starting location and parse it
            Position startLoc = null;
            var configLoc = ServerConfig.ucm_jail_escape_start_location.Value;
            if (!string.IsNullOrEmpty(configLoc) && Position.TryParse(configLoc, out var parsedLoc))
            {
                startLoc = new Position(parsedLoc);
                startLoc.Variation = variationId;
            }
            else
            {
                // Fallback to default starting coordinates
                startLoc = new Position(0x00C00242u, 80.0f, -10.0f, 6.015f, 0.0f, 0.0f, 0.0f, 1.0f, false, variationId);
            }

            log.Info($"[Jail Debug] Player {Name} (0x{Guid.Full:X8}) starting escape. Variation={variationId}, Target={startLoc}");
            JailTeleport(startLoc);

            Session?.Network?.EnqueueSend(new GameMessageSystemChat($"The Jail Escape Trial has begun! You have {timeLimit.GetFriendlyLongString()} to find the exit portal. Be swift!", ChatMessageType.Broadcast));
        }

        public void FailJailEscape(string reason)
        {
            FailJailEscapeNoTeleport(reason);

            if (IsInJail())
            {
                // still serving: back to the jail spawn
                JailTeleport(GetJailTeleportLocationSafe());
            }
            else
            {
                // the sentence ran out during the trial (or staff released them): released, and put where every
                // release ends, inside the jail and free to walk out
                if (GetProperty(PropertyFloat.JailedUntil).HasValue)
                    ReleaseFromJail(teleportOut: false);
                JailTeleport(GetJailReleaseLocation());
            }
        }

        public void FailJailEscapeNoTeleport(string reason)
        {
            if (!JailEscapeActive) return;

            int reservedVariation = -1;
            foreach (var kvp in ActiveEscapeReservations.ToList())
            {
                if (kvp.Value == Guid.Full)
                {
                    reservedVariation = kvp.Key;
                    break;
                }
            }

            JailEscapeActive = false;
            JailEscapeEndTimeUtc = null;

            // Destroy the light cloud, if one is up
            foreach (var guid in JailEscapeDynamicObjectGuids.ToList())
            {
                var o = CurrentLandblock?.GetObject(guid);
                o?.Destroy();
            }
            JailEscapeDynamicObjectGuids.Clear();
            JailEscapeLightState = EscapeLightState.None;

            // Stamp persistent cooldown
            QuestManager.Stamp("jail_escape_cooldown");

            // the chamber stays reserved until its delayed reset has run, so no one is put into it mid-reset
            if (reservedVariation != -1)
                ResetJailMaze(reservedVariation, Guid.Full);
            else
                ReleaseEscapeReservation(Guid.Full);

            if (!string.IsNullOrEmpty(reason))
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat($"Escape failed: {reason}", ChatMessageType.Broadcast));
            }
        }

        public void SucceedJailEscape()
        {
            if (!JailEscapeActive) return;

            int reservedVariation = -1;
            foreach (var kvp in ActiveEscapeReservations.ToList())
            {
                if (kvp.Value == Guid.Full)
                {
                    reservedVariation = kvp.Key;
                    break;
                }
            }

            int secondsTaken = -1;
            if (JailEscapeEndTimeUtc.HasValue)
            {
                var timeTaken = DateTime.UtcNow - (JailEscapeStartTimeUtc ?? DateTime.UtcNow);
                secondsTaken = (int)timeTaken.TotalSeconds;
                if (secondsTaken < 1) secondsTaken = 1;
            }

            JailEscapeActive = false;
            JailEscapeEndTimeUtc = null;

            // Destroy the light cloud, if one is up
            foreach (var guid in JailEscapeDynamicObjectGuids.ToList())
            {
                var o = CurrentLandblock?.GetObject(guid);
                o?.Destroy();
            }
            JailEscapeDynamicObjectGuids.Clear();
            JailEscapeLightState = EscapeLightState.None;

            // the chamber stays reserved until its delayed reset has run, so no one is put into it mid-reset
            if (reservedVariation != -1)
                ResetJailMaze(reservedVariation, Guid.Full);
            else
                ReleaseEscapeReservation(Guid.Full);

            // Release from sentence. The player is taken back to the jail below, not to their lifestone.
            ReleaseFromJail(teleportOut: false);

            // The cooldown starts on success too, so an escapee jailed again soon serves that sentence.
            QuestManager.Stamp("jail_escape_cooldown");

            // Quest stamps, one per tier, so each counts toward QB on its own:
            //   jail_escape_completed - escaped within the time limit
            //   jail_escape_flawless  - escaped without a single red-light catch (care, not speed)
            //   jail_escape_swift     - escaped in under ucm_jail_escape_swift_minutes
            // The admin test character (13 m/s, perfect corners) took 3m 22s; a buffed player steering by hand is
            // expected near 5 minutes, so Swift (6) is reachable without being free. Size and buffs change speed.
            QuestManager.Stamp("jail_escape_completed");
            var tiers = new List<string>();
            if (JailEscapeCatches == 0)
            {
                QuestManager.Stamp("jail_escape_flawless");
                tiers.Add("Flawless (no red light catches)");
            }
            if (secondsTaken != -1 && secondsTaken < ServerConfig.ucm_jail_escape_swift_minutes.Value * 60)
            {
                QuestManager.Stamp("jail_escape_swift");
                tiers.Add($"Swift (under {TimeSpan.FromMinutes(ServerConfig.ucm_jail_escape_swift_minutes.Value).GetFriendlyLongString()})");
            }
            if (tiers.Count > 0)
                Session?.Network?.EnqueueSend(new GameMessageSystemChat("Escape honours earned: " + string.Join(", ", tiers) + ".", ChatMessageType.Broadcast));

            if (secondsTaken != -1)
            {
                RecordJailEscapeTime(secondsTaken);
            }

            // Back to the jail, released, free to walk out
            JailTeleport(GetJailReleaseLocation());

            Session?.Network?.EnqueueSend(new GameMessageSystemChat("Congratulations! You have escaped! You are free to walk out of the jail.", ChatMessageType.Broadcast));
        }

        /// <param name="releaseOwner">When set, that player's reservation of the chamber is released once the reset has
        /// run (or at once if the chamber is not loaded), unless they have started a new trial in the meantime.</param>
        public void ResetJailMaze(int? variationId, uint? releaseOwner = null)
        {
            log.Info($"[Jail Debug] ResetJailMaze called for variation {variationId}. Scheduling reset with a 3-second delay...");

            var landblockId = new LandblockId(0x00C0FFFF); // Jail escape landblock
            var landblock = LandblockManager.GetLandblock(landblockId, false, variationId);
            if (landblock != null)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(3.0f);
                // Bind action actor to the long-lived landblock object itself to prevent aborts on logout/disconnect
                actionChain.AddAction(landblock, ActionType.Landblock_Init, () =>
                {
                    log.Info($"[Jail Debug] ActionChain executing: resetting landblock variation {variationId}...");
                    landblock.DestroyAllNonPlayerObjects();
                    DatabaseManager.World.ClearCachedInstancesByLandblock(0x00C0, variationId);
                    landblock.Init(variationId, true);
                    if (releaseOwner.HasValue)
                        ReleaseEscapeReservationAfterReset(releaseOwner.Value);
                });
                actionChain.EnqueueChain();
            }
            else
            {
                log.Warn($"[Jail Debug] Landblock 0x00C0FFFF variation {variationId} was null. Clearing cached instances only.");
                DatabaseManager.World.ClearCachedInstancesByLandblock(0x00C0, variationId);
                if (releaseOwner.HasValue)
                    ReleaseEscapeReservationAfterReset(releaseOwner.Value);
            }
        }

        /// <summary>
        /// Frees a chamber after its reset, unless the same player has already started a new trial (a reservation is
        /// reused by its owner, so it would otherwise be taken from under the running trial).
        /// </summary>
        private static void ReleaseEscapeReservationAfterReset(uint owner)
        {
            if (PlayerManager.GetOnlinePlayer(owner)?.JailEscapeActive == true)
                return;
            ReleaseEscapeReservation(owner);
        }

        public void InitializeDynamicJailMaze(int? variationId)
        {
            log.Info($"[Jail Debug] InitializeDynamicJailMaze entered for variation {variationId}.");
            JailEscapeDynamicObjectGuids.Clear();
        }

        public void TransitionEscapeLightState(EscapeLightState newState)
        {
            log.Info($"[Jail Debug] TransitionEscapeLightState called: player {Name} going from {JailEscapeLightState} to {newState}.");
            JailEscapeLightState = newState;
            
            // 1. Delete the previous light's cloud
            foreach (var guid in JailEscapeDynamicObjectGuids.ToList())
            {
                var o = CurrentLandblock?.GetObject(guid);
                o?.Destroy();
            }
            JailEscapeDynamicObjectGuids.Clear();

            // 2. Select new WCID, sound, and message
            uint newWcid = 0;
            Sound changeSound = Sound.Invalid;
            string alertMsg = "";
            double durationSeconds = 0;

            JailEscapeStateTransitionTimeUtc = DateTime.UtcNow;
            JailEscapeFreezePoint = null;
            JailEscapeCaughtThisRed = false;

            switch (newState)
            {
                case EscapeLightState.Green:
                    newWcid = (uint)ServerConfig.ucm_jail_escape_green_wcid.Value;
                    changeSound = Sound.UI_EnterPortal;
                    alertMsg = "GREEN LIGHT - GO!";
                    var greenMin = ServerConfig.ucm_jail_escape_green_light_min_seconds.Value;
                    var greenMax = ServerConfig.ucm_jail_escape_green_light_max_seconds.Value;
                    durationSeconds = ThreadSafeRandom.Next((float)greenMin, (float)greenMax);
                    break;
                case EscapeLightState.Yellow:
                    newWcid = (uint)ServerConfig.ucm_jail_escape_yellow_wcid.Value;
                    changeSound = Sound.Launch; // warning launch sound
                    alertMsg = "YELLOW LIGHT - STOP NOW!";
                    durationSeconds = ServerConfig.ucm_jail_escape_yellow_light_seconds.Value;
                    break;
                case EscapeLightState.Red:
                    newWcid = (uint)ServerConfig.ucm_jail_escape_red_wcid.Value;
                    changeSound = Sound.UI_Bell; // alarm gong
                    alertMsg = "RED LIGHT - FREEZE!";
                    var redMin = ServerConfig.ucm_jail_escape_red_light_min_seconds.Value;
                    var redMax = ServerConfig.ucm_jail_escape_red_light_max_seconds.Value;
                    durationSeconds = ThreadSafeRandom.Next((float)redMin, (float)redMax);
                    break;
            }

            JailEscapeNextLightChangeUtc = DateTime.UtcNow.AddSeconds(durationSeconds);
            log.Info($"[Jail Debug] Light state {newState} duration: {durationSeconds}s. Next change scheduled at: {JailEscapeNextLightChangeUtc.Value}");

            // 3. Spawn one cloud of the new colour where the player stands. The course is a corridor that
            // turns every 10 m, so fixed clouds along it are mostly out of sight; a cloud on the player is
            // always in view. It stays put until the next change, so on green it falls behind as they walk.
            // A WCID of 0 means no cloud for that light (the chat line and sound still announce it).
            if (newWcid != 0 && CurrentLandblock != null)
            {
                var mist = WorldObjectFactory.CreateNewWorldObject(newWcid);
                if (mist != null)
                {
                    mist.Location = new Position(Location);
                    mist.Ethereal = true;
                    mist.IgnoreCollisions = true;
                    CurrentLandblock.AddWorldObject(mist, Location.Variation);
                    JailEscapeDynamicObjectGuids.Add(mist.Guid);
                }
                else
                {
                    log.Warn($"[Jail Debug] Failed to create light cloud for WCID {newWcid}!");
                }
            }

            // 4. Colour effect on the player. It is attached to them, so unlike a cloud it moves with them;
            // TickJail replays it so it stays visible for the whole light.
            PlayJailEscapeLightEffect();

            // 5. Alert player: text at the top of the screen and a sound, never a chat line. A chat line is what a
            // VTank meta's chat rule reacts to, so it made the trial trivial to automate. (The top-of-screen text
            // has no colour field; the colour is carried by the effect on the player.)
            if (!string.IsNullOrEmpty(alertMsg))
            {
                if (Session != null)
                    SendTransientError(alertMsg);
                if (changeSound != Sound.Invalid)
                    PlaySoundEffect(changeSound, Guid);
            }
        }

        /// <summary>
        /// True while the player's last movement report has a walk, run or strafe key down. Turning on the spot is not
        /// movement. The client sends this the moment a key changes, so it is current to within the network delay.
        /// </summary>
        private bool IsHoldingMovementKey()
        {
            var raw = LastMoveToState?.RawMotionState;
            if (raw == null)
                return false;

            var forward = (raw.Flags & RawMotionFlags.ForwardCommand) != 0 &&
                (raw.ForwardCommand == MotionCommand.WalkForward || raw.ForwardCommand == MotionCommand.WalkBackwards || raw.ForwardCommand == MotionCommand.RunForward);
            var sidestep = (raw.Flags & RawMotionFlags.SideStepCommand) != 0 &&
                (raw.SidestepCommand == MotionCommand.SideStepRight || raw.SidestepCommand == MotionCommand.SideStepLeft);
            return forward || sidestep;
        }

        /// <summary>
        /// Plays the current light's colour effect on the player and schedules the next replay.
        /// </summary>
        private void PlayJailEscapeLightEffect()
        {
            long effect = JailEscapeLightState switch
            {
                EscapeLightState.Green => ServerConfig.ucm_jail_escape_green_effect.Value,
                EscapeLightState.Yellow => ServerConfig.ucm_jail_escape_yellow_effect.Value,
                EscapeLightState.Red => ServerConfig.ucm_jail_escape_red_effect.Value,
                _ => 0
            };

            if (effect > 0)
                PlayParticleEffect((PlayScript)effect, Guid);

            var repeat = Math.Max(0.5, ServerConfig.ucm_jail_escape_effect_repeat_seconds.Value);
            JailEscapeNextLightEffectUtc = DateTime.UtcNow.AddSeconds(repeat);
        }

        /// <summary>
        /// Ends a player's jail sentence and restores their original combat state and visual properties.
        /// Also clears them from the database properties and informs them that they are free.
        /// </summary>
        /// <param name="teleportOut">False leaves a player who is in the escape dungeon where they are; the caller moves them.</param>
        public void ReleaseFromJail(bool teleportOut = true)
        {
            // Any recorded sentence, running or run out. Gating on IsInJail() made an expired sentence impossible
            // to release: it read as "not in jail", so JailedUntil was never removed and the player stayed red.
            if (!GetProperty(PropertyFloat.JailedUntil).HasValue) return;

            RemoveProperty(PropertyFloat.JailedUntil);
            
            // Clean up escape attempt if active
            if (JailEscapeActive)
            {
                FailJailEscapeNoTeleport("Released from jail by authority.");
            }

            RedrawPlayerWithUpdates();

            // Every release ends the same way: inside the jail, free to walk out. Only someone in the jail (or in a
            // trial copy) is moved; staff releasing a player elsewhere leaves them where they are.
            if (teleportOut && (IsJailEscapeChamber(Location) || IsAtJail()))
                JailTeleport(GetJailReleaseLocation());

            Session?.Network?.EnqueueSend(new GameMessageSystemChat("Your punishment has concluded. You may now resume your adventures.", ChatMessageType.Broadcast));
        }

        public void OnDeathInJail(Player killingPlayer)
        {
            if (killingPlayer == null || killingPlayer == this || killingPlayer.IsInJail()) return;
            int totalKills = killingPlayer.QuestManager.Stamp("jail_vigilante_justice");
            if (totalKills >= 5) killingPlayer.QuestManager.StampFirst("jail_its_me_the_warden");
        }

        /// <summary>
        /// Broadcasts an update to other players to see the new player.
        /// Aborts if player is logging out to prevent double-spawning "ghost" visual glitches in the cell.
        /// </summary>
        private void RedrawPlayerWithUpdates()
        {
            if (IsLoggingOut || Session == null) return;

            EnqueueBroadcast(false, new GameMessageDeleteObject(this));
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(.25);
            actionChain.AddAction(this, ActionType.PlayerTracking_DeCloakStep3, () =>
            {
                if (IsLoggingOut || Session == null) return;
                EnqueueBroadcast(false, new GameMessageCreateObject(this));
            });
            actionChain.EnqueueChain();
        }

        public Position GetJailTeleportLocationSafe()
        {
            if (Position.TryParse(ServerConfig.ucm_check_fail_teleport_location.Value, out Position failTeleLoc))
                return failTeleLoc;
            if (Position.TryParse(ServerConfig.ucm_check_jail_center_location.Value, out Position jailCenterLoc))
                return jailCenterLoc;
                
            var fallback = Sanctuary ?? Instantiation;
            if (fallback == null || fallback.Landblock == 0x00C0)
            {
                // Safe town fallback: Holtburg, where the Holtburg point of interest drops players (world DB,
                // weenie 42820's destination), so this can never point back into the jail or a trial copy.
                return new Position(0xA9B40019u, 84.0f, 7.1f, 94.0f, 0.0f, 0.0f, -0.078459f, 0.996917f);
            }
            return fallback;
        }

        /// <summary>
        /// Where every release ends: inside the jail, past the UCM Shamers that pen in the jail spawn (only a ~1.7 m
        /// gap), free to walk out. ucm_jail_escape_exit_location; blank falls back to the jail spawn.
        /// </summary>
        public Position GetJailReleaseLocation()
        {
            return Position.TryParse(ServerConfig.ucm_jail_escape_exit_location.Value, out var exitLoc) ? exitLoc : GetJailTeleportLocationSafe();
        }

        /// <summary>True when the player stands inside the jail's bounding box (ucm_jail_size around the jail centre).</summary>
        private bool IsAtJail()
        {
            var center = GetJailCenterLocation();
            if (center == null || Location.Landblock != center.Landblock || Location.Variation != center.Variation) return false;
            var offset = Location.GetOffset(center);
            var half = ServerConfig.ucm_jail_size.Value / 2.0;
            return Math.Abs(offset.X) <= half && Math.Abs(offset.Y) <= half;
        }

        /// <summary>
        /// A teleport into or out of a trial copy. Location.Variation is NOT set first: Teleport() decides the
        /// variation-change cleanup (ghost-object sweep) by comparing it with the destination (see Portal.cs).
        /// </summary>
        private void JailTeleport(Position destination)
        {
            WorldManager.ThreadSafeTeleport(this, destination, new ActionEventDelegate(ActionType.Portal_Teleport, () =>
            {
                SendWeenieError(WeenieError.ITeleported);
                OnTeleportComplete();
            }), true);
        }

        private Position GetJailCenterLocation()
        {
            if (Position.TryParse(ServerConfig.ucm_check_jail_center_location.Value, out Position jailCenterLoc))
                return jailCenterLoc;
            return GetJailTeleportLocationSafe();
        }

        /// <summary>
        /// Handles random starts of checks and timing out of active checks. For use by Player.Tick().
        /// </summary>
        public void TickJail()
        {
            if (JailEscapeActive)
            {
                if (JailEscapeEndTimeUtc.HasValue && DateTime.UtcNow > JailEscapeEndTimeUtc.Value)
                {
                    log.Info($"[Jail Debug] Jail Escape Time Trial expired for player {Name}. Calling FailJailEscape.");
                    FailJailEscape("Your time expired!");
                    return;
                }

                // Boundary check: If they leave the escape dungeon landblock, fail the escape attempt immediately (blocks portal gems/teleport item exploits)
                if (Location.Landblock != 0x00C0 && !Teleporting)
                {
                    log.Warn($"[Jail Debug] Player {Name} left the landblock boundary (Landblock: 0x{Location.Landblock:X4}). Failing escape attempt.");
                    FailJailEscape("You left the escape chamber boundary!");
                    return;
                }

                // Lazy initialize the dynamic maze layout once static scenery objects have loaded on the background thread
                if (!JailEscapeMazeInitialized)
                {
                    if (!Teleporting && CurrentLandblock != null && CurrentLandblock.CreateWorldObjectsCompleted)
                    {
                        log.Info($"[Jail Debug] TickJail detected CreateWorldObjectsCompleted = true for variation {Location.Variation}. Initializing maze layout...");
                        InitializeDynamicJailMaze(Location.Variation);
                        JailEscapeMazeInitialized = true;
                        JailEscapeMazeInitializedTimeUtc = DateTime.UtcNow;
                    }
                    else
                    {
                        // Wait for landblock objects to finish loading and teleport to complete
                        return;
                    }
                }

                // The Red Light Green Light rules apply throughout the escape dungeon for the whole trial.
                {
                    if (JailEscapeLightState == EscapeLightState.None)
                    {
                        var timeSinceInit = JailEscapeMazeInitializedTimeUtc.HasValue ? (DateTime.UtcNow - JailEscapeMazeInitializedTimeUtc.Value).TotalSeconds : 0.0;
                        if (timeSinceInit >= 1.5)
                        {
                            log.Info($"[Jail Debug] Player {Name} entered Mist Zone. Starting Green Light...");
                            TransitionEscapeLightState(EscapeLightState.Green);
                        }
                    }
                    else if (JailEscapeNextLightChangeUtc.HasValue && DateTime.UtcNow >= JailEscapeNextLightChangeUtc.Value)
                    {
                        var nextState = (JailEscapeLightState == EscapeLightState.Green) ? EscapeLightState.Yellow :
                                        (JailEscapeLightState == EscapeLightState.Yellow) ? EscapeLightState.Red :
                                        EscapeLightState.Green;
                        log.Info($"[Jail Debug] Light state timer expired. Transitioning from {JailEscapeLightState} to {nextState}...");
                        TransitionEscapeLightState(nextState);
                    }
                    else if (JailEscapeLightState != EscapeLightState.None && JailEscapeNextLightEffectUtc.HasValue && DateTime.UtcNow >= JailEscapeNextLightEffectUtc.Value)
                    {
                        PlayJailEscapeLightEffect();
                    }

                    // Red light: the player must already be stopped. Yellow is the warning to stop; the grace
                    // (ucm_jail_escape_red_light_grace_seconds) only covers the network delay of the key release.
                    // The test is the movement keys: the client reports a key going down or up at once, while its
                    // position only reaches the server about once a second, so positions alone caught players who
                    // had stopped 1 s before red (tested 2026-10-16). Drifting more than
                    // ucm_jail_escape_red_light_tolerance_meters from where they stood when the grace ended is the
                    // backstop, for movement without keys (click to move, speed hacks). One catch per red, and
                    // nothing is checked while the catch's own teleport is under way.
                    if (JailEscapeLightState == EscapeLightState.Red && !JailEscapeCaughtThisRed && !Teleporting)
                    {
                        var graceSeconds = ServerConfig.ucm_jail_escape_red_light_grace_seconds.Value;
                        var timeSinceTransition = JailEscapeStateTransitionTimeUtc.HasValue ? (DateTime.UtcNow - JailEscapeStateTransitionTimeUtc.Value).TotalSeconds : 0.0;

                        if (timeSinceTransition >= graceSeconds)
                        {
                            if (JailEscapeFreezePoint == null)
                                JailEscapeFreezePoint = new Position(Location);

                            var dist = Location.Distance2D(JailEscapeFreezePoint);
                            var holding = IsHoldingMovementKey();
                            if (holding || dist > ServerConfig.ucm_jail_escape_red_light_tolerance_meters.Value)
                            {
                                JailEscapeCaughtThisRed = true;
                                JailEscapeCatches++;
                                log.Info($"[Jail Debug] RED LIGHT MOTION DETECTED! Player: {Name}, {(holding ? "holding a movement key" : "no key held")}, {dist:F2} m from the freeze point. Penalty and back to the start.");
                                PlayParticleEffect(PlayScript.Explode, Guid);
                                // Hurts but never kills: a jailed player may already be low (they are attackable),
                                // and a death here also ran the death teleport on top of the one below.
                                var damage = Math.Min(Health.MaxValue * 0.15f, (float)Health.Current - 1);
                                if (damage > 0)
                                    TakeDamage(null, DamageType.Bludgeon, damage);
                                UpdateVital(Stamina, (uint)(Stamina.Current * 0.50f));
                                if (!JailEscapeActive)
                                    return;

                                // Back to the trial's start, in the player's own trial copy
                                Position startLoc;
                                if (Position.TryParse(ServerConfig.ucm_jail_escape_start_location.Value, out var parsedStart))
                                    startLoc = new Position(parsedStart) { Variation = Location.Variation };
                                else
                                    startLoc = new Position(0x00C00242u, 80.0f, -10.0f, 6.015f, 0.0f, 0.0f, 0.0f, 1.0f, false, Location.Variation);
                                WorldManager.ThreadSafeTeleport(this, startLoc, new ActionEventDelegate(ActionType.Portal_Teleport, () => { }), true);

                                Session?.Network?.EnqueueSend(new GameMessageSystemChat("You moved on a RED LIGHT! Teleported back to the start!", ChatMessageType.Broadcast));
                            }
                        }
                    }
                }

                return;
            }

            var jailedUntil = GetProperty(PropertyFloat.JailedUntil);
            if (!jailedUntil.HasValue) return;

            // Player has waited out their sentence and can be released.
            if (Time.GetUnixTime() > jailedUntil.Value)
            {
                ReleaseFromJail();
                return;
            }

            // Player is still serving their sentence, so enforce the jail boundaries.
            var center = GetJailCenterLocation();
            var size = ServerConfig.ucm_jail_size.Value;
            var offset = Location.GetOffset(center);
            if (Math.Abs(offset.X) > size / 2.0 || Math.Abs(offset.Y) > size / 2.0)
            {
                var teleLoc = GetJailTeleportLocationSafe();
                if (Location.Distance2D(teleLoc) < 1.0) return;
                QuestManager.StampFirst("jail_magic_bars");
                Session?.Network?.EnqueueSend(new GameMessageSystemChat("You cannot leave the jail area until your punishment is complete!", ChatMessageType.Broadcast));
                Teleport(teleLoc);
            }
            return;
        }

        public void RecordJailEscapeTime(int secondsTaken)
        {
            var bestTimeVal = GetProperty(PropertyInt.JailbreakBestTime);
            bool isNewRecord = false;
            int? oldTime = null;

            if (bestTimeVal.HasValue && bestTimeVal.Value > 0)
            {
                oldTime = bestTimeVal.Value;
                if (secondsTaken < bestTimeVal.Value)
                {
                    isNewRecord = true;
                    SetProperty(PropertyInt.JailbreakBestTime, secondsTaken);
                }
            }
            else
            {
                isNewRecord = true;
                SetProperty(PropertyInt.JailbreakBestTime, secondsTaken);
            }

            // Tell the player achievement details
            var timeStr = FormatTime(secondsTaken);
            if (isNewRecord)
            {
                if (oldTime.HasValue)
                {
                    Session?.Network?.EnqueueSend(new GameMessageSystemChat(
                        $"NEW PERSONAL RECORD! You escaped the jail in {timeStr} (previous best: {FormatTime(oldTime.Value)}).", 
                        ChatMessageType.System));
                }
                else
                {
                    Session?.Network?.EnqueueSend(new GameMessageSystemChat(
                        $"NEW PERSONAL RECORD! You escaped the jail in {timeStr}.", 
                        ChatMessageType.System));
                }
            }
            else
            {
                Session?.Network?.EnqueueSend(new GameMessageSystemChat(
                    $"You escaped the jail in {timeStr} (personal best: {FormatTime(oldTime.Value)}).", 
                    ChatMessageType.System));
            }
        }

        private string FormatTime(int seconds)
        {
            var minutes = seconds / 60;
            var secs = seconds % 60;
            if (minutes > 0)
                return $"{minutes}m {secs}s";
            return $"{secs}s";
        }
    }
}
