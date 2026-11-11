using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        ///  The fellowship that this player belongs to
        /// </summary>
        public Fellowship Fellowship;

        public bool FellowVitalUpdate;

        /// <summary>ZoneFellowshipManager's own clock and the moment this player left their zone fellowship's area (0 = inside).</summary>
        public double ZoneFellowNextTick;
        /// <summary>When this member's vitals last went to an area fellowship (at most once a second).</summary>
        public double ZoneFellowVitalsAt;
        /// <summary>When /autofellow was last switched (review 2026-11-06: an off / on macro was a leave + join of the whole fellowship each time).</summary>
        public System.DateTime ZoneFellowToggledAt;
        public double ZoneFellowOutsideSince;
        /// <summary>The zone fellowship this player quit by hand: not joined again until they leave its area (or relog).</summary>
        public string ZoneFellowSkipKey;
        /// <summary>The area whose fellowship this player stayed out of because their own is locked (told once per area).</summary>
        public string ZoneFellowLockedKey;
        /// <summary>The auto fellowship area this player stands in (ZoneFellowshipManager, refreshed every 2 s whatever their
        /// /autofellow setting), or null. Fellowship.GetDistanceScalar reads it: fellows in one area share in full, across tiers.</summary>
        public string AutoFellowAreaKey;

        /// <summary>Anti-leech (2026-11-05): this player's damage to monsters in 20 s steps (ZoneFellowshipManager.RecordDamage),
        /// the area they were last told they are barred from, and when they were last told they are under the leech line.</summary>
        public readonly object LeechLock = new object();
        public readonly long[] LeechBucketSlot = new long[ZoneFellowshipManager.Buckets];
        public readonly double[] LeechBucketDamage = new double[ZoneFellowshipManager.Buckets];
        public string LeechBucketKey;   // the area the buckets count for (CodeRabbit #596): damage in another area starts them over
        public double LeechWarnedAt;
        public string ZoneFellowBarredKey;
        public string ZoneFellowFullKey;   // the area fellowship this player was last told is full (fellowship_max_size)

        /// <summary>A zone fellowship cannot be changed by its members (2026-11-05): say so and refuse.</summary>
        private bool RefuseZoneFellowship(string what)
        {
            if (Fellowship?.ZoneKey == null) return false;
            Session.Network.EnqueueSend(new GameMessageSystemChat($"The {Fellowship.FellowshipName} fellowship {what}. Everyone here is in it while /autofellow is on.", ChatMessageType.Broadcast));
            return true;
        }

        // todo: Figure out if this is the best place to do this, and whether there are concurrency issues associated with it.
        public void FellowshipCreate(string fellowshipName, bool shareXP)
        {
            // An Olthoi player cannot create a fellowship
            if (IsOlthoiPlayer || IsMule)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OlthoiCannotJoinFellowship));
                return;
            }

            // Auto Fellow test suite 2026-11-05 (X6): the client hides Create while in a fellowship, but a crafted message got
            // through - the player led a new fellowship and stayed listed in the old one (an area fellowship included): two at once
            if (Fellowship != null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("You are already in a fellowship - leave it first.", ChatMessageType.Broadcast));
                return;
            }

            Fellowship = new Fellowship(this, fellowshipName, shareXP);

            if (Session.AccessLevel >= AccessLevel.Admin)
                PlayerManager.BroadcastToAuditChannel(this, $"Admin {Name} created fellowship {fellowshipName}.");
            Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(Session));
            Session.Network.EnqueueSend(new GameEventFellowshipFellowUpdateDone(Session));
        }

        public void HandleActionFellowshipChangeOpenness(bool openness)
        {
            if (RefuseZoneFellowship("is always open to everyone in the area")) return;

            if (Fellowship != null)
            {
                if (Guid.Full != Fellowship.FellowshipLeaderGuid)
                {
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouMustBeLeaderOfFellowship));
                    return;
                }

                if (!Fellowship.IsLocked)
                    Fellowship.UpdateOpenness(openness);
                else
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.FellowshipIsLocked));
            }
        }

        public void HandleActionFellowshipChangeLock(bool lockState, string lockName)
        {
            if (Fellowship?.ZoneKey != null) return;   // emotes lock fellowships for quests; a zone fellowship is never locked

            if (Fellowship != null)
                Fellowship.UpdateLock(lockState, lockName);
        }

        public void FellowshipQuit(bool disband)
        {
            if (Fellowship?.ZoneKey != null)
            {
                // quit by hand: stay out of it until leaving the area - not pulled back in 2 s later (ZoneFellowshipManager)
                ZoneFellowSkipKey = Fellowship.ZoneKey;
                var name = Fellowship.FellowshipName;
                Fellowship.QuitFellowship(this, false);
                Fellowship = null;
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You left the {name} fellowship. You join it again the next time you come here; /autofellow off stops that.", ChatMessageType.Broadcast));
                return;
            }

            if (Fellowship != null)
                Fellowship.QuitFellowship(this, disband);
        }

        public void FellowshipDismissPlayer(uint dismissGuid)
        {
            if (Fellowship == null) return;

            if (RefuseZoneFellowship("cannot dismiss anyone")) return;

            if (Guid.Full != Fellowship.FellowshipLeaderGuid)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouMustBeLeaderOfFellowship));
                return;
            }

            if (Guid.Full == dismissGuid)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("You can't dismiss yourself from the fellowship", ChatMessageType.Broadcast));
                return;
            }

            var fellowToDismiss = PlayerManager.GetOnlinePlayer(dismissGuid);

            if (fellowToDismiss == null)
                return;

            Fellowship.RemoveFellowshipMember(fellowToDismiss, this);
        }

        public void FellowshipRecruit(Player newPlayer)
        {
            if (newPlayer == null) return;

            // An Olthoi player cannot join a fellowship
            if (newPlayer.IsOlthoiPlayer || newPlayer.IsMule)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("The Olthoi's hunger for destruction is too great to understand a request for fellowship.", ChatMessageType.Broadcast));
                SendWeenieError(WeenieError.None);
                return;
            }

            if (newPlayer.GetCharacterOption(CharacterOption.IgnoreFellowshipRequests))
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{newPlayer.Name} is not accepting fellowship requests.", ChatMessageType.Fellowship));                
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.FellowshipIgnoringRequests));
            }
            else if (Fellowship != null)
            {
                if (RefuseZoneFellowship("takes nobody by invitation")) return;

                if (Guid.Full == Fellowship.FellowshipLeaderGuid || Fellowship.Open)
                    Fellowship.AddFellowshipMember(this, newPlayer);
                else
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouMustBeLeaderOfFellowship));
            }
        }

        public void FellowshipNewLeader(uint newLeaderGuid)
        {
            if (Fellowship == null || Guid.Full == newLeaderGuid)
                return;

            // an area fellowship is the server's: its lead is not passed by hand (review 2026-11-06: a crafted pass back and forth
            // sent every member's full list to every member, each time)
            if (Fellowship.ZoneKey != null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("The lead of an area fellowship cannot be passed.", ChatMessageType.Broadcast));
                return;
            }

            if (Guid.Full != Fellowship.FellowshipLeaderGuid)
            {
                log.Warn($"{Name} tried to assign new fellowship leader from {Fellowship.FellowshipLeaderGuid:X8} to {newLeaderGuid:X8}");
                return;
            }

            var newLeader = PlayerManager.GetOnlinePlayer(newLeaderGuid);

            if (newLeader == null)
                return;

            if (newLeader.Fellowship != Fellowship)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{newLeader.Name} is not a member of the fellowship!", ChatMessageType.Broadcast));
                return;
            }

            Fellowship.AssignNewLeader(this, newLeader);
        }

        public bool FellowshipPanelOpen { get; set; }

        /// <summary>
        /// Called when player opens / closes the fellowship panel
        /// </summary>
        public void HandleFellowshipUpdateRequest(bool panelOpen)
        {
            FellowshipPanelOpen = panelOpen;

            if (Fellowship != null && FellowshipPanelOpen)
                Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(Session));
        }
    }
}
