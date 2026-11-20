using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;
using ACE.Server.WorldObjects;

using log4net;

namespace ACE.Server.Entity
{
    public class Fellowship
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The maximum # of fellowship members: the live server setting fellowship_max_size, 0 = no limit (owner 2026-11-05: "No cap
        /// everywhere", then "Add a way for us to change fellowship max size live"). It was a constant 29 (upstream's 2023-07-06
        /// release, retail 9). The retail client was shown a 100-member fellowship on TEST and listed it fine; shares past 28
        /// members stay at the table's 12.5% each (GetMemberSharePercent), "Treat 29 to 100 the same as 29".
        /// </summary>
        public static int MaxFellows => ServerConfig.fellowship_max_size.Value <= 0 ? int.MaxValue : (int)Math.Min(int.MaxValue, ServerConfig.fellowship_max_size.Value);

        public string FellowshipName;
        public uint FellowshipLeaderGuid;

        public bool ShareXP;   // determined by the leader's 'ShareFellowshipExpAndLuminance' client option when fellowship is created
        public bool ShareLoot; // determined by the leader's 'ShareFellowshipLoot' client option when fellowship is created

        public bool Open;      // indicates if non-leaders can invite new fellowship members
        public bool IsLocked;  // only set through emotes. if a fellowship is locked, new fellowship members cannot be added

        /// <summary>Concurrent since 2026-11-05: a zone fellowship (ZoneFellowshipManager) gains and loses members from every
        /// landblock thread its zone spans while kills on other threads read it - a plain Dictionary throws on that.</summary>
        public ConcurrentDictionary<uint, WeakReference<Player>> FellowshipMembers;

        /// <summary>Not null: a zone fellowship the server holds for an area (ZoneFellowshipManager, owner 2026-11-05). Members
        /// join and leave by walking in and out (/autofellow); nobody can disband, dismiss, lock, open or recruit into it.</summary>
        public string ZoneKey;

        public Dictionary<uint, int> DepartedMembers;

        public Dictionary<string, FellowshipLockData> FellowshipLocks;

        public QuestManager QuestManager;

        /// <summary>
        /// Called when a player first creates a Fellowship
        /// </summary>
        public Fellowship(Player leader, string fellowshipName, bool shareXP)
        {
            ShareXP = shareXP;

            // get loot sharing from leader's character options
            ShareLoot = leader.GetCharacterOption(CharacterOption.ShareFellowshipLoot);

            FellowshipLeaderGuid = leader.Guid.Full;
            FellowshipName = fellowshipName;

            FellowshipMembers = new ConcurrentDictionary<uint, WeakReference<Player>>();
            FellowshipMembers[leader.Guid.Full] = new WeakReference<Player>(leader);

            Open = false;

            QuestManager = new QuestManager(this);
            IsLocked = false;
            DepartedMembers = new Dictionary<uint, int>();
            FellowshipLocks = new Dictionary<string, FellowshipLockData>();
        }

        /// <summary>
        /// Called when a player clicks the 'add fellow' button
        /// </summary>
        public void AddFellowshipMember(Player inviter, Player newMember)
        {
            if (inviter == null || newMember == null)
                return;

            if (IsLocked)
            {

                if (!DepartedMembers.TryGetValue(newMember.Guid.Full, out var timeDeparted))
                {
                    inviter.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(inviter.Session, WeenieErrorWithString.LockedFellowshipCannotRecruit_, newMember.Name));
                    //newMember.SendWeenieError(WeenieError.LockedFellowshipCannotRecruitYou);
                    return;
                }
                else
                {
                    var timeLimit = Time.GetDateTimeFromTimestamp(timeDeparted).AddSeconds(600);
                    if (DateTime.UtcNow > timeLimit)
                    {
                        inviter.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(inviter.Session, WeenieErrorWithString.LockedFellowshipCannotRecruit_, newMember.Name));
                        //newMember.SendWeenieError(WeenieError.LockedFellowshipCannotRecruitYou);
                        return;
                    }
                }
            }

            if (FellowshipMembers.Count >= MaxFellows)
            {
                inviter.Session.Network.EnqueueSend(new GameEventWeenieError(inviter.Session, WeenieError.YourFellowshipIsFull));
                return;
            }

            if (newMember.Fellowship != null || FellowshipMembers.ContainsKey(newMember.Guid.Full))
            {
                inviter.Session.Network.EnqueueSend(new GameMessageSystemChat($"{newMember.Name} is already a member of a Fellowship.", ChatMessageType.Broadcast));
            }
            else
            {
                if (newMember.IsLoggingOut)
                {
                    inviter.Session.Network.EnqueueSend(new GameMessageSystemChat($"{newMember.Name} is not available.", ChatMessageType.Broadcast));
                    return;
                }

                if (ServerConfig.fellow_busy_no_recruit.Value && newMember.IsBusy)
                {
                    inviter.Session.Network.EnqueueSend(new GameMessageSystemChat($"{newMember.Name} is busy.", ChatMessageType.Broadcast));
                    return;
                }

                if (newMember.GetCharacterOption(CharacterOption.AutomaticallyAcceptFellowshipRequests))
                {
                    AddConfirmedMember(inviter, newMember, true);
                }
                else
                {
                    if (!newMember.ConfirmationManager.EnqueueSend(new Confirmation_Fellowship(inviter.Guid, newMember.Guid), inviter.Name))
                    {
                        inviter.Session.Network.EnqueueSend(new GameMessageSystemChat($"{newMember.Name} is busy.", ChatMessageType.Broadcast));
                    }
                }
            }
        }

        /// <summary>
        /// Finalizes the process of adding a player to the fellowship
        /// If the player doesn't have the 'automatically accept fellowship requests' option set,
        /// this would be after they responded to the popup window
        /// </summary>
        public void AddConfirmedMember(Player inviter, Player player, bool response)
        {
            if (inviter == null || inviter.Session == null || inviter.Session.Player == null || player == null) return;

            // Do not add a player who is in the middle of logging out.
            // This closes the race where FellowshipQuit fires during logout, then an auto-accept
            // or a previously-queued confirmation dialog re-adds the departing player.
            if (player.IsLoggingOut) return;

            if (!response)
            {
                // player clicked 'no' on the fellowship popup
                inviter.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} declines your invite", ChatMessageType.Fellowship));
                inviter.Session.Network.EnqueueSend(new GameEventWeenieError(inviter.Session, WeenieError.FellowshipDeclined));
                return;
            }

            if (FellowshipMembers.Count >= MaxFellows)
            {
                inviter.Session.Network.EnqueueSend(new GameEventWeenieError(inviter.Session, WeenieError.YourFellowshipIsFull));
                return;
            }

            // Auto Fellow test suite 2026-11-05 (X1): a yes lands in the inviter's fellowship OF NOW - an invite sent before the
            // inviter walked into an Auto Fellow area put the invited player (outside the area, even blacklisted from it) into
            // the area's fellowship. An area fellowship never takes anyone by invitation.
            if (ZoneKey != null)
            {
                player.Session?.Network.EnqueueSend(new GameMessageSystemChat($"That offer of fellowship is no longer good: {inviter.Name} is in an area fellowship now.", ChatMessageType.Broadcast));
                return;
            }

            // (X2) a yes to an old invite while already in a fellowship made the player a member of two at once
            if (player.Fellowship != null && player.Fellowship != this)
            {
                player.Session?.Network.EnqueueSend(new GameMessageSystemChat("You are already in a fellowship - leave it first.", ChatMessageType.Broadcast));
                return;
            }
            if (FellowshipMembers.ContainsKey(player.Guid.Full))
                return;

            FellowshipMembers.TryAdd(player.Guid.Full, new WeakReference<Player>(player));
            player.Fellowship = inviter.Fellowship;

            if (player.Session.AccessLevel >= AccessLevel.Admin)
            {
                var leaderName = PlayerManager.GetOnlinePlayer(FellowshipLeaderGuid)?.Name ?? "Unknown";
                PlayerManager.BroadcastToAuditChannel(player, $"Admin {player.Name} joined fellowship {FellowshipName} (Leader: {leaderName}).");
            }

            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
            {
                if (member.Guid == player.Guid) continue;
                member.Session.Network.EnqueueSend(new GameEventFellowshipUpdateFellow(member.Session, player, ShareXP));
            }

            if (ShareLoot)
            {
                foreach (var member in fellowshipMembers.Values)
                {
                    if (member.Guid == player.Guid) continue;
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} has given you permission to loot his or her kills.", ChatMessageType.Broadcast));
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} may now loot your kills.", ChatMessageType.Broadcast));

                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{member.Name} has given you permission to loot his or her kills.", ChatMessageType.Broadcast));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{member.Name} may now loot your kills.", ChatMessageType.Broadcast));
                }
            }

            UpdateAllMembers();

            if (inviter.CurrentMotionState.Stance == MotionStance.NonCombat) // only do this motion if inviter is at peace, other times motion is skipped. 
                inviter.SendMotionAsCommands(MotionCommand.BowDeep, MotionStance.NonCombat);
        }

        /// <summary>
        /// A zone fellowship takes a player in (ZoneFellowshipManager, owner 2026-11-05): no invite, no popup, no bow. The others
        /// get the one new row (FellowshipUpdateFellow), the newcomer the whole list - never the whole list to everyone, which
        /// with a hundred members is a hundred lists of a hundred on every arrival.
        /// </summary>
        public void AddZoneMember(Player player)
        {
            if (player == null || player.Session == null || player.IsLoggingOut) return;

            // the fellowship first, then the member list: GetFellowshipMembers drops an entry whose player has no fellowship, and a
            // kill split on another landblock thread between the two lines lost the joiner for good (review 2026-11-06)
            player.Fellowship = this;
            FellowshipMembers[player.Guid.Full] = new WeakReference<Player>(player);

            foreach (var member in GetFellowshipMembers().Values)
            {
                if (member.Guid == player.Guid) continue;
                member.Session.Network.EnqueueSend(new GameEventFellowshipUpdateFellow(member.Session, player, ShareXP));
            }

            player.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(player.Session));
            player.Session.Network.EnqueueSend(new GameEventFellowshipFellowUpdateDone(player.Session));
        }

        public void RemoveFellowshipMember(Player player, Player leader)
        {
            if (player == null) return;

            var fellowshipMembers = GetFellowshipMembers();

            if (!fellowshipMembers.ContainsKey(player.Guid.Full))
            {
                log.Warn($"{leader.Name} tried to dismiss {player.Name} from the fellowship, but {player.Name} was not found in the fellowship");

                var done = true;

                if (player.Fellowship != null)
                {
                    if (player.Fellowship == this)
                    {
                        log.Warn($"{player.Name} still has a reference to this fellowship somehow. This shouldn't happen");
                        done = false;
                    }
                    else
                        log.Warn($"{player.Name} has a reference to a different fellowship. {leader.Name} is possibly sending crafted data!");
                }

                if (done) return;
            }

            foreach (var member in fellowshipMembers.Values)
            {
                member.Session.Network.EnqueueSend(new GameEventFellowshipDismiss(member.Session, player));
                member.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} dismissed from fellowship", ChatMessageType.Fellowship));
            }

            FellowshipMembers.TryRemove(player.Guid.Full, out _);
            player.Fellowship = null;

            if (player.Session.AccessLevel >= AccessLevel.Admin)
                PlayerManager.BroadcastToAuditChannel(player, $"Admin {player.Name} was dismissed from fellowship {FellowshipName}.");

            UpdateAllMembers();
        }

        private void UpdateAllMembers()
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
                if (member.Fellowship == this) member.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(member.Session));   // not one who just left
        }

        private void SendBroadcastAndUpdate(string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
            {
                member.Session.Network.EnqueueSend(new GameEventChannelBroadcast(member.Session, Channel.FellowBroadcast, "", message));

                if (member.Fellowship == this) member.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(member.Session));   // not one who just left
            }
        }

        public void BroadcastToFellow(string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
                member.Session.Network.EnqueueSend(new GameEventChannelBroadcast(member.Session, Channel.FellowBroadcast, "", message));
        }

        public void TellFellow(WorldObject sender, string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
                member.Session.Network.EnqueueSend(new GameEventChannelBroadcast(member.Session, Channel.Fellow, sender.Name, message));
        }

        private void SendWeenieErrorWithStringAndUpdate(WeenieErrorWithString error, string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
            {
                member.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(member.Session, error, message));

                if (member.Fellowship == this) member.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(member.Session));   // not one who just left
            }
        }

        public void QuitFellowship(Player player, bool disband)
        {
            if (player == null) return;

            // a zone fellowship is never disbanded by a member, its leader included: "Disband" leaves it like "Quit" (2026-11-05)
            if (ZoneKey != null) disband = false;

            if (player.Session.AccessLevel >= AccessLevel.Admin)
                PlayerManager.BroadcastToAuditChannel(player, $"Admin {player.Name} left fellowship {FellowshipName} (Disbanded: {disband}).");

            if (player.Guid.Full == FellowshipLeaderGuid)
            {
                if (disband)
                {
                    var fellowshipMembers = GetFellowshipMembers();

                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameEventFellowshipDisband(member.Session));

                        if (ShareLoot)
                        {
                            member.Session.Network.EnqueueSend(new GameMessageSystemChat("You no longer have permission to loot anyone else's kills.", ChatMessageType.Broadcast));

                            // you would expect this occur, but it did not in retail pcaps
                            //foreach (var fellow in fellowshipMembers.Values)
                            //    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"{fellow.Name} does not have permission to loot your kills.", ChatMessageType.Broadcast));
                        }

                        member.Fellowship = null;
                    }
                }
                else
                {
                    FellowshipMembers.TryRemove(player.Guid.Full, out _);

                    if (IsLocked)
                    {
                        var timestamp = (int)Time.GetUnixTime();
                        if (!DepartedMembers.TryAdd(player.Guid.Full, timestamp))
                            DepartedMembers[player.Guid.Full] = timestamp;
                    }

                    player.Fellowship = null;

                    player.Session.Network.EnqueueSend(new GameEventFellowshipQuit(player.Session, player.Guid.Full));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("You no longer have permission to loot anyone else's kills.", ChatMessageType.Broadcast));

                    var fellowshipMembers = GetFellowshipMembers();

                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameEventFellowshipQuit(member.Session, player.Guid.Full));

                        if (ShareLoot)
                        {
                            member.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have lost permission to loot the kills of {player.Name}.", ChatMessageType.Broadcast));
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{member.Name} does not have permission to loot your kills.", ChatMessageType.Broadcast));
                        }
                    }
                    AssignNewLeader(null, null);
                }
            }
            else if (!disband)
            {
                FellowshipMembers.TryRemove(player.Guid.Full, out _);

                if (IsLocked)
                {
                    var timestamp = (int)Time.GetUnixTime();

                    if (!DepartedMembers.TryAdd(player.Guid.Full, timestamp))
                        DepartedMembers[player.Guid.Full] = timestamp;
                }

                player.Session.Network.EnqueueSend(new GameEventFellowshipQuit(player.Session, player.Guid.Full));

                var fellowshipMembers = GetFellowshipMembers();

                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameEventFellowshipQuit(member.Session, player.Guid.Full));

                    if (ShareLoot)
                    {
                        member.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have lost permission to loot the kills of {player.Name}.", ChatMessageType.Broadcast));
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{member.Name} does not have permission to loot your kills.", ChatMessageType.Broadcast));
                    }
                }

                player.Fellowship = null;
            }
        }

        public void AssignNewLeader(Player oldLeader, Player newLeader)
        {
            if (newLeader != null)
            {
                FellowshipLeaderGuid = newLeader.Guid.Full;

                if (oldLeader != null)
                    oldLeader.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(oldLeader.Session, WeenieErrorWithString.YouHavePassedFellowshipLeadershipTo_, newLeader.Name));

                SendWeenieErrorWithStringAndUpdate(WeenieErrorWithString._IsNowLeaderOfFellowship, newLeader.Name);
            }
            else
            {
                // leader has dropped, assign new random leader
                var fellowshipMembers = GetFellowshipMembers();

                if (fellowshipMembers.Count == 0) return;

                var rng = ThreadSafeRandom.Next(0, fellowshipMembers.Count - 1);

                var fellowGuids = fellowshipMembers.Keys.ToList();

                FellowshipLeaderGuid = fellowGuids[rng];

                var newLeaderName = fellowshipMembers[FellowshipLeaderGuid].Name;

                if (oldLeader != null)
                    oldLeader.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(oldLeader.Session, WeenieErrorWithString.YouHavePassedFellowshipLeadershipTo_, newLeaderName));

                SendWeenieErrorWithStringAndUpdate(WeenieErrorWithString._IsNowLeaderOfFellowship, newLeaderName);
            }
        }

        public void UpdateOpenness(bool isOpen)
        {
            Open = isOpen;
            var openness = Open ? WeenieErrorWithString._IsNowOpenFellowship : WeenieErrorWithString._IsNowClosedFellowship;
            SendWeenieErrorWithStringAndUpdate(openness, FellowshipName);
        }

        public void UpdateLock(bool isLocked, string lockName)
        {
            // Unlocking a fellowship is not possible without disbanding in retail worlds, so in all likelihood, this is only firing for fellowships being locked by emotemanager

            IsLocked = isLocked;

            if (string.IsNullOrWhiteSpace(lockName))
                lockName = "Undefined";

            if (isLocked)
            {
                Open = false;

                DepartedMembers.Clear();

                var timestamp = Time.GetUnixTime();
                if (!FellowshipLocks.TryAdd(lockName, new FellowshipLockData(timestamp)))
                    FellowshipLocks[lockName].UpdateTimestamp(timestamp);

                SendBroadcastAndUpdate("Your fellowship is now locked.  You may not recruit new members.  If you leave the fellowship, you have 15 minutes to be recruited back into the fellowship.");
            }
            else
            {
                // Unlocking a fellowship is not possible without disbanding in retail worlds, so in all likelihood, this never occurs

                DepartedMembers.Clear();

                FellowshipLocks.Remove(lockName);

                SendBroadcastAndUpdate("Your fellowship is now unlocked.");
            }
        }

        /// <summary>
        /// Audit 2026-09-13 (C5): a member elsewhere (quest shares have no distance limit, kill shares reach 600 units) may be
        /// ticked by another group, and the split itself can run on a thread that owns neither the earner nor the member.
        /// Grant directly only when the EXECUTING thread owns the member's landblock, otherwise on the world queue (RunOnThreadFor).
        /// </summary>
        private static void GrantOnMemberThread(Player member, Action grant)
        {
            LandblockManager.RunOnThreadFor(member, ActionType.Fellowship_GrantShare, grant);
        }

        /// <summary>
        /// Splits XP amongst fellowship members, depending on XP type and fellow settings
        /// </summary>
        /// <param name="amount">The input amount of XP</param>
        /// <param name="xpType">The type of XP (quest XP is handled differently)</param>
        /// <param name="player">The fellowship member who originated the XP</param>
        public void SplitXp(ulong amount, XpType xpType, ShareType shareType, Player player, int monsterTier = 0, int killTier = 0)
        {
            // https://asheron.fandom.com/wiki/Announcements_-_2002/02_-_Fever_Dreams#Letter_to_the_Players_1

            var fellowshipMembers = GetFellowshipMembers();

            shareType &= ~ShareType.Fellowship;

            // an area fellowship (Auto Fellow) never shares quest XP (owner 2026-11-06, "Quester keeps it all"): with no size cap an even
            // split left the one who turned in 1% of their own reward. Kill XP and luminance share as usual (GetDistanceScalar)
            if (ZoneKey != null && xpType == XpType.Quest)
            {
                GrantOnMemberThread(player, () => player.GrantXP((long)amount, XpType.Quest, shareType));
                return;
            }

            // quest turn-ins: flat share (retail default), only among fellows on the same prestige/visibility layer
            if (xpType == XpType.Quest && !ServerConfig.fellow_quest_bonus.Value)
            {
                var earnerVar = VariationManager.GetEffectiveVariationForVisibility(player);
                var sameLayer = new List<Player>();
                foreach (var member in fellowshipMembers.Values)
                {
                    if (VariationManager.SameVariationForVisibility(earnerVar, VariationManager.GetEffectiveVariationForVisibility(member)))
                        sameLayer.Add(member);
                }

                if (sameLayer.Count == 0)
                {
                    GrantOnMemberThread(player, () => player.GrantXP((long)amount, XpType.Quest, shareType));
                    return;
                }

                var perAmount = (long)amount / sameLayer.Count;

                foreach (var member in sameLayer)
                {
                    long shareAmount = perAmount;
                    if (xpType == XpType.Kill && monsterTier > 0)
                    {
                        var memberTier = member.GetProperty(PropertyInt.PrestigeLevel) ?? 0;
                        shareAmount = (long)Math.Round(shareAmount * PrestigeManager.GetXPRewardModifier(monsterTier));
                        shareAmount = (long)Math.Round(shareAmount * PrestigeManager.GetXPPenaltyMultiplier(memberTier, monsterTier));
                    }

                    var fellowXpType = player == member ? XpType.Quest : XpType.Fellowship;
                    var questShare = (member.HasVitae && member.IsVPHardcore) ? 0 : shareAmount;
                    var questMember = member;
                    GrantOnMemberThread(member, () => questMember.GrantXP(questShare, fellowXpType, shareType));
                }
            }

            // divides XP evenly to all the sharable fellows within level range,
            // but with a significant boost to the amount of xp, based on # of fellowship members
            else
            {
                var eligibleMembers = new List<(Player Member, double Scalar)>(fellowshipMembers.Count);

                foreach (var member in fellowshipMembers.Values)
                {
                    var scalar = GetDistanceScalar(player, member, xpType);
                    if (scalar > 0)
                        eligibleMembers.Add((member, scalar));
                }

                ZoneFellowshipManager.DropLeeches(this, player, eligibleMembers);   // anti-leech (2026-11-05): an area fellowship's rule
                ShareXpAmong(eligibleMembers, amount, xpType, shareType, player, monsterTier, killTier);
            }
        }

        /// <summary>
        /// Divides non-quest XP among an explicit list of sharers - the fellowship's eligible fellows, or everyone standing in
        /// a Zone Share area (ZoneShareManager, 2026-09-23), so both share by the same numbers. shareType must already have
        /// ShareType.Fellowship removed, which is what stops each member's GrantXP from splitting again.
        /// </summary>
        internal static void ShareXpAmong(List<(Player Member, double Scalar)> eligibleMembers, ulong amount, XpType xpType, ShareType shareType, Player player, int monsterTier, int killTier = 0)
        {
            var totalAmount = (ulong)Math.Round(amount * GetMemberSharePercent(eligibleMembers.Count));

            foreach (var (member, scalar) in eligibleMembers)
            {
                if (member == player && ServerConfig.fellowship_additive.Value)
                {
                    var additiveMember = member;
                    GrantOnMemberThread(member, () => additiveMember.GrantXP((long)amount, xpType, shareType, monsterTier, killTier));
                    continue;
                }
                var shareAmount = (double)totalAmount * scalar;

                if (xpType == XpType.Kill && monsterTier > 0)
                {
                    var memberTier = member.GetProperty(PropertyInt.PrestigeLevel) ?? 0;
                    shareAmount *= PrestigeManager.GetXPRewardModifier(monsterTier);
                    shareAmount *= PrestigeManager.GetXPPenaltyMultiplier(memberTier, monsterTier);
                }

                var fellowXpType = player == member ? xpType : XpType.Fellowship;
                var killShare = (member.HasVitae && member.IsVPHardcore) ? 0 : (long)Math.Round(shareAmount);
                var killMember = member;
                // tier dip (off unless zc_tier_dip_xp_enabled): each member's own credit, read on the member's own thread
                var dipTier = xpType == XpType.Kill ? killTier : 0;
                GrantOnMemberThread(member, () => killMember.GrantXP(ACE.Server.Managers.ZoneControl.TierRewardScale.ScaleXp(killShare, killMember, dipTier), fellowXpType, shareType));
            }
        }

        /// <summary>
        /// Splits luminance amongst fellowship members, depending on XP type and fellow settings
        /// </summary>
        /// <param name="amount">The input amount of luminance</param>
        /// <param name="xpType">The type of lumaniance (quest luminance is handled differently)</param>
        /// <param name="player">The fellowship member who originated the luminance</param>
        public void SplitLuminance(ulong amount, XpType xpType, ShareType shareType, Player player, int monsterTier = 0, int killTier = 0)
        {
            // https://asheron.fandom.com/wiki/Announcements_-_2002/02_-_Fever_Dreams#Letter_to_the_Players_1

            shareType &= ~ShareType.Fellowship;

            if (xpType == XpType.Quest)
            {
                // quest luminance is not shared
                GrantOnMemberThread(player, () => player.GrantLuminance((long)amount, XpType.Quest, shareType, monsterTier));
            }
            else
            {
                // pre-filter: evenly divide between luminance-eligible fellows
                var fellowshipMembers = GetFellowshipMembers();

                if (fellowshipMembers.Count == 0)
                {
                    GrantOnMemberThread(player, () => player.GrantLuminance((long)amount, xpType, shareType, monsterTier, killTier));
                    return;
                }

                var eligibleMembers = new List<(Player Member, double Scalar)>(fellowshipMembers.Count);

                foreach (var member in fellowshipMembers.Values)
                {
                    var scalar = GetDistanceScalar(player, member, xpType);
                    if (scalar > 0)
                        eligibleMembers.Add((member, scalar));
                }

                ZoneFellowshipManager.DropLeeches(this, player, eligibleMembers);   // anti-leech (2026-11-05): an area fellowship's rule
                ShareLuminanceAmong(eligibleMembers, amount, xpType, shareType, player, monsterTier, killTier);
            }
        }

        /// <summary>
        /// The luminance twin of <see cref="ShareXpAmong"/>: the fellowship's eligible fellows, or everyone in a Zone Share area.
        /// shareType must already have ShareType.Fellowship removed.
        /// </summary>
        internal static void ShareLuminanceAmong(List<(Player Member, double Scalar)> eligibleMembers, ulong amount, XpType xpType, ShareType shareType, Player player, int monsterTier, int killTier = 0)
        {
            var totalAmount = amount * GetMemberSharePercent(eligibleMembers.Count);

            // Iterate fellowship members directly without .ToList() allocation
            foreach (var (member, scalar) in eligibleMembers)
            {
                var fellowXpType = player == member ? xpType : XpType.Fellowship;
                if (member == player && ServerConfig.fellowship_additive.Value)
                {
                    var additiveLumType = fellowXpType;
                    GrantOnMemberThread(player, () => player.GrantLuminance((long)amount, additiveLumType, shareType, monsterTier, killTier));
                    continue;
                }

                var playerTotal = totalAmount * scalar;

                if (xpType == XpType.Kill && monsterTier > 0)
                {
                    var memberTier = member.GetProperty(PropertyInt.PrestigeLevel) ?? 0;
                    playerTotal *= PrestigeManager.GetXPRewardModifier(monsterTier);
                    playerTotal *= PrestigeManager.GetXPPenaltyMultiplier(memberTier, monsterTier);
                }

                var lumShare = (long)Math.Round(playerTotal);
                var lumMember = member;
                // tier dip (off unless zc_tier_dip_xp_enabled): each member's own credit, read on the member's own thread
                var dipTier = xpType == XpType.Kill ? killTier : 0;
                GrantOnMemberThread(member, () => lumMember.GrantLuminance(ACE.Server.Managers.ZoneControl.TierRewardScale.ScaleXp(lumShare, lumMember, dipTier), fellowXpType, shareType));
            }
        }

        internal static double GetMemberSharePercent(int memberCount)
        {

            switch (memberCount)
            {
                case 1:
                    return 1.0;
                case 2:
                    return .75;
                case 3:
                    return .6;
                case 4:
                    return .55;
                case 5:
                    return .5;
                case 6:
                    return .45;
                case 7:
                    return .4;
                case 8:
                    return .35;
                case 9:
                    return .32;
                case 10:
                    return .29;
                case 11:
                    return .268;
                case 12:
                    return .248;
                case 13:
                    return .232;
                case 14:
                    return .218;
                case 15:
                    return .206;
                case 16:
                    return .196;
                case 17:
                    return .186;
                case 18:
                    return .178;
                case 19:
                    return .171;
                case 20:
                    return .164;
                case 21:
                    return .158;
                case 22:
                    return .152;
                case 23:
                    return .147;
                case 24:
                    return .143;
                case 25:
                    return .139;
                case 26:
                    return .135;
                case 27:
                    return .131;
                case 28:
                    return .128;
                default:
                    return .125;
                    // TODO: handle fellowship mods with > 9 players?
            }
            //return 1.0;
        }

        public static readonly int MaxDistance = 600;

        // Debug tracking for distance calculations
        private static long _distanceCalcCount = 0;
        private static long _sameLandblockSkips = 0;
        private static long _indoorOutdoorMismatches = 0;
        private static long _differentLandblockIndoor = 0;
        private static DateTime _lastLogTime = DateTime.UtcNow;

        /// <summary>
        /// Logs distance calculation statistics every 10 seconds
        /// </summary>
        private static void LogDistanceStats()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastLogTime).TotalSeconds >= 10.0)
            {
                var distCalcs = System.Threading.Interlocked.Exchange(ref _distanceCalcCount, 0);
                var sameLB = System.Threading.Interlocked.Exchange(ref _sameLandblockSkips, 0);
                var indoorOutdoor = System.Threading.Interlocked.Exchange(ref _indoorOutdoorMismatches, 0);
                var diffLBIndoor = System.Threading.Interlocked.Exchange(ref _differentLandblockIndoor, 0);

                var elapsed = (now - _lastLogTime).TotalSeconds;
                var total = distCalcs + sameLB + indoorOutdoor + diffLBIndoor;

                if (total > 0 && ServerConfig.fellowship_xp_debug_logging.Value)
                {
                    var optimizedChecks = sameLB + indoorOutdoor + diffLBIndoor;
                    var optimizationPct = (optimizedChecks * 100.0 / total);

                    log.Info($"[FELLOWSHIP XP] Stats for last {elapsed:F1}s:");
                    log.Info($"  Total checks: {total} ({total / elapsed:F1}/sec)");
                    log.Info($"  - Same LB (optimized): {sameLB} ({sameLB * 100.0 / total:F1}%)");
                    log.Info($"  - Distance calcs: {distCalcs} ({distCalcs * 100.0 / total:F1}%)");
                    log.Info($"  - Indoor/outdoor mismatch: {indoorOutdoor} ({indoorOutdoor * 100.0 / total:F1}%)");
                    log.Info($"  - Diff LB indoor: {diffLBIndoor} ({diffLBIndoor * 100.0 / total:F1}%)");
                    log.Info($"  Optimization effectiveness: {optimizationPct:F1}% of checks skipped expensive Distance2D()");
                }

                _lastLogTime = now;
            }
        }

        /// <summary>
        /// Returns the amount to scale the XP for a fellow
        /// based on distance from the earner
        /// </summary>
        public static double GetDistanceScalar(Player earner, Player fellow, XpType xpType)
        {
            if (earner == null || fellow == null)
                return 0.0f;

            // An area fellowship shares ONLY between two members who both stand in its area (owner 2026-11-05: "in that 2 minute
            // window, they should not be earning lum / xp"): a member in the 2-minute grace outside it gets nothing from the
            // others, and a kill made outside it feeds nobody else - however close (the ordinary 600 m rule) and for quest XP too.
            // The earner's own share is untouched.
            var zoneKey = earner.Fellowship?.ZoneKey;
            if (zoneKey != null && fellow != earner && (earner.AutoFellowAreaKey != zoneKey || fellow.AutoFellowAreaKey != zoneKey))
                return 0.0f;

            // Auto fellowship areas (owner 2026-11-05: "landblock sharing + tier sharing together", like T10 and VoD): two fellows
            // in the same area share in full - any tier (layer), any landblock, indoors or out. ZoneFellowshipManager keeps the key.
            // Only inside the area's own fellowship (review 2026-11-06: two players in a fellowship of their own - /autofellow off -
            // got the full cross-layer share in an area too)
            if (zoneKey != null && earner.AutoFellowAreaKey == zoneKey && fellow.AutoFellowAreaKey == zoneKey)
                return 1.0f;

            if (!VariationManager.SameVariationForVisibility(
                    VariationManager.GetEffectiveVariationForVisibility(earner),
                    VariationManager.GetEffectiveVariationForVisibility(fellow)))
                return 0.0f;

            if (xpType == XpType.Quest)
                return 1.0f;

            // Thaelaryn Island - full share if both players are in a thaelaryn island landblock
            if ((earner.Location.Variation ?? 0) == (fellow.Location.Variation ?? 0))
            {
                if (LandblockCollections.ThaelarynIslandLandblocks.Contains((ushort)earner.Location.Landblock) && LandblockCollections.ThaelarynIslandLandblocks.Contains((ushort)fellow.Location.Landblock))
                    return 1.0f;
            }

            // Valley of Death - full share if both players are in a valley of death landblock
            if ((earner.Location.Variation ?? 0) == (fellow.Location.Variation ?? 0))
            {
                if (LandblockCollections.ValleyOfDeathLandblocks.Contains((ushort)earner.Location.Landblock) && LandblockCollections.ValleyOfDeathLandblocks.Contains((ushort)fellow.Location.Landblock))
                    return 1.0f;
            }

            // https://asheron.fandom.com/wiki/Announcements_-_2004/01_-_Mirror,_Mirror#Rollout_Article

            // OPTIMIZATION: Check if both players are in the same landblock
            // Landblock max distance (corner to corner) is ~271 units, well under the 600 unit XP sharing range
            // This skips expensive Distance2D calculations for the majority of fellowship grinding
            if (earner.Location.Landblock == fellow.Location.Landblock
                && (earner.Location.Variation ?? 0) == (fellow.Location.Variation ?? 0))
            {
                // Same landblock but one is indoor and one is outdoor - can't share
                if (earner.Location.Indoors != fellow.Location.Indoors)
                {
                    System.Threading.Interlocked.Increment(ref _indoorOutdoorMismatches);
                    LogDistanceStats();
                    return 0.0f;
                }

                // Same landblock and both indoor or both outdoor - guaranteed in range
                System.Threading.Interlocked.Increment(ref _sameLandblockSkips);
                LogDistanceStats();
                return 1.0f;
            }

            // DIFFERENT LANDBLOCKS

            // If either player is indoors, they can't share XP across landblocks
            if (earner.Location.Indoors || fellow.Location.Indoors)
            {
                System.Threading.Interlocked.Increment(ref _differentLandblockIndoor);
                LogDistanceStats();
                return 0.0f;
            }

            // Both players are outdoor in different landblocks - need distance check
            // This handles cases like players at adjacent landblock boundaries.

            // Track distance calculation rate (debug logging)
            System.Threading.Interlocked.Increment(ref _distanceCalcCount);
            LogDistanceStats();

            var dist = earner.Location.Distance2D(fellow.Location);

            if (dist >= MaxDistance * 2.0f)
                return 0.0f;

            if (dist <= MaxDistance)
                return 1.0f;

            var scalar = 1.0f - (dist - MaxDistance) / MaxDistance;

            return Math.Max(0.0f, scalar);
        }

        /// <summary>
        /// Returns fellows within radar range (75 units outdoors, 25 units indoors)
        /// </summary>
        public List<Player> WithinRange(Player player, bool includeSelf = false)
        {
            var fellows = GetFellowshipMembers();

            var landblockRange = ServerConfig.fellow_kt_landblock.Value;

            var results = new List<Player>();

            foreach (var fellow in fellows.Values)
            {
                if (player == fellow && !includeSelf)
                    continue;

                // an area fellowship (owner 2026-11-06, "Inside the area only"): kill-task / tier credit passes only between two members
                // both inside its area - as XP and luminance (GetDistanceScalar)
                if (ZoneKey != null && player != fellow && (player.AutoFellowAreaKey != ZoneKey || fellow.AutoFellowAreaKey != ZoneKey))
                    continue;

                var shareable = player == fellow || landblockRange ?
                    player.CurrentLandblock == fellow.CurrentLandblock || player.Location.DistanceTo(fellow.Location) <= 192.0f :
                    player.Location.Distance2D(fellow.Location) <= player.CurrentRadarRange && player.ObjMaint.VisibleObjectsContainsKey(fellow.Guid.Full);      // 2d visible distance / radar range?

                if (shareable)
                    results.Add(fellow);
            }
            return results;
        }

        /// <summary>
        /// Called when someone in the fellowship levels up
        /// </summary>
        public void OnFellowLevelUp(Player player)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var fellow in fellowshipMembers.Values)
            {
                if (fellow == player)
                    continue;

                fellow.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} is now level {player.Level}!", ChatMessageType.Broadcast));
            }
        }

        /// <summary>Sends this member's vitals to the fellows; false when an area fellowship's once-a-second limit held it back (the
        /// caller keeps the update pending, so the last change is never lost - review 2026-11-06).</summary>
        public bool OnVitalUpdate(Player player)
        {
            // cap max update interval?
            // an area fellowship (Auto Fellow, any size): one vitals update a second per member (review 2026-11-06: every vital change
            // of every member went to every member - 100 x 99 packets a round of combat)
            if (ZoneKey != null)
            {
                var now = Time.GetUnixTime();
                if (now - player.ZoneFellowVitalsAt < 1.0)
                    return false;
                player.ZoneFellowVitalsAt = now;
            }

            var fellowshipMembers = GetFellowshipMembers();

            foreach (var fellow in fellowshipMembers.Values)
            {
                if (fellow.FellowshipPanelOpen)
                    fellow.Session.Network.EnqueueSend(new GameEventFellowshipUpdateFellow(fellow.Session, player, ShareLoot, FellowUpdateType.Vitals));
            }
            return true;
        }

        /// <summary>
        /// Who a fellowship spell lands on (owner 2026-11-06, "Caster's landblock + layer"): in an area fellowship only the members inside
        /// its area on the anchor's landblock and layer (the anchor = the caster, or the player an NPC / item cast on), and the anchor
        /// itself - an NPC's fellow portal sending moved every stranger in the area. Any other fellowship: every member, as retail.
        /// </summary>
        public List<Player> SpellTargets(Player anchor)
        {
            var members = GetFellowshipMembers().Values;
            var at = anchor?.Location;
            if (ZoneKey == null || at == null)
                return members.ToList();
            var lb = at.Landblock;
            var layer = at.Variation ?? 0;
            return members.Where(m => m == anchor
                || (m.AutoFellowAreaKey == ZoneKey && m.Location != null && m.Location.Landblock == lb && (m.Location.Variation ?? 0) == layer)).ToList();
        }

        public void OnDeath(Player player)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var fellow in fellowshipMembers.Values)
            {
                if (fellow != player)
                    fellow.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your fellow {player.Name} has died!", ChatMessageType.Broadcast));
            }
        }

        public Dictionary<uint, Player> GetFellowshipMembers()
        {
            var results = new Dictionary<uint, Player>();
            var dropped = new HashSet<uint>();

            foreach (var kvp in FellowshipMembers)
            {
                var playerGuid = kvp.Key;
                var playerRef = kvp.Value;

                playerRef.TryGetTarget(out var player);

                if (player != null && player.Session != null && player.Session.Player != null && player.Fellowship != null)
                    results.Add(playerGuid, player);
                else
                    dropped.Add(playerGuid);
            }

            // TODO: process dropped list
            if (dropped.Count > 0)
                ProcessDropList(FellowshipMembers, dropped);

            return results;
        }

        public void ProcessDropList(ConcurrentDictionary<uint, WeakReference<Player>> fellowshipMembers, HashSet<uint> fellowGuids)
        {
            foreach (var fellowGuid in fellowGuids)
            {
                var offlinePlayer = PlayerManager.FindByGuid(fellowGuid);
                var offlineName = offlinePlayer != null ? offlinePlayer.Name : "NULL";

                log.Warn($"Dropped fellow: {offlineName}");
                fellowshipMembers.TryRemove(fellowGuid, out _);
            }
            if (fellowGuids.Contains(FellowshipLeaderGuid))
                AssignNewLeader(null, null);

            UpdateAllMembers();
        }
    }

    public static class FellowshipExtensions
    {
        private static readonly HashComparer hashComparer = new HashComparer(32);

        public static void Write(this BinaryWriter writer, Dictionary<uint, int> departedFellows)
        {
            PackableHashTable.WriteHeader(writer, departedFellows.Count, hashComparer.NumBuckets);

            var sorted = new SortedDictionary<uint, int>(departedFellows, hashComparer);

            foreach (var departed in sorted)
            {
                writer.Write(departed.Key);
                writer.Write(departed.Value);
            }
        }
    }
}
