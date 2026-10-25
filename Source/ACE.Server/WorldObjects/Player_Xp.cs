using System;
using System.Linq;

using ACE.Common.Extensions;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public const long levelXpMult = 58;
        public const long xp275 = 191226310247;
        public const long xp274to275delta = 3390451400;
        public const double levelRatio = 0.014234603;
        public const double questToBonusRation = 0.005;
        public const double enlightenmentToBonusRatio = 0.1;

        // XP Batching fields
        private long pendingXpUpdate = 0;
        private ActionChain xpBatchChain = null;
        private readonly object xpBatchLock = new object();

        // the "unassigned experience is full" notice went out this login (AddAvailableXp)
        private bool overflowNoticeSent;

        // Item XP Batching fields
        private readonly object itemXpBatchLock = new object();
        private readonly System.Collections.Generic.Dictionary<uint, long> pendingItemXpUpdates = new System.Collections.Generic.Dictionary<uint, long>();
        private ActionChain itemXpBatchChain = null;

        /// <summary>
        /// A player earns XP through natural progression, ie. kills and quests completed
        /// </summary>
        /// <param name="amount">The amount of XP being added</param>
        /// <param name="xpType">The source of XP being added</param>
        /// <param name="shareable">True if this XP can be shared with Fellowship</param>
        /// <param name="killTier">Tier rewards: the T11+ Zone Control tier of the kill, 0 for any other XP (see TierRewardScale.ScaleXp)</param>
        public void EarnXP(long amount, XpType xpType, ShareType shareType = ShareType.All, int monsterTier = 0, int killTier = 0)
        {
            //Console.WriteLine($"{Name}.EarnXP({amount}, {sharable}, {fixedAmount})");
            if (IsMule)
            {
                return;
            }
            // apply xp modifiers.  Quest XP is multiplicative with general XP modification
            var questModifier = ServerConfig.quest_xp_modifier.Value;
            var modifier = ServerConfig.xp_modifier.Value;
            if (xpType == XpType.Quest)
                modifier *= questModifier;

            // should this be passed upstream to fellowship / allegiance?
            var enchantment = GetXPAndLuminanceModifier(xpType);

            var quest = GetQuestCountXPBonus();

            var enlightenment = GetEnglightenmentXPBonus();

            var hardCoreMult = 1.0 + ServerConfig.hardcore_xp_multiplier.Value;

            long m_amount = 0;

            if (IsVPHardcore)
            {
                m_amount = (long)Math.Round(amount * enchantment * modifier * quest * enlightenment * hardCoreMult);
            }
            else
            {
                m_amount = (long)Math.Round(amount * enchantment * modifier * quest * enlightenment);
            }

            if (m_amount < 0)
            {
                log.Warn($"{Name}.EarnXP({amount}, {shareType})");
                log.Warn($"modifier: {modifier}, enchantment: {enchantment}, m_amount: {m_amount}");
                return;
            }

            GrantXP(m_amount, xpType, shareType, monsterTier, killTier);
        }

        /// <summary>
        /// Directly grants XP to the player, without the XP modifier
        /// </summary>
        /// <param name="amount">The amount of XP to grant to the player</param>
        /// <param name="xpType">The source of the XP being granted</param>
        /// <param name="shareable">If TRUE, this XP can be shared with fellowship members</param>
        public void GrantXP(long amount, XpType xpType, ShareType shareType = ShareType.All, int monsterTier = 0, int killTier = 0)
        {
            if (IsOlthoiPlayer || IsMule)
            {
                if (HasVitae)
                    UpdateXpVitae(amount);

                return;
            }

            // Zone Share (owner 2026-09-23): a kill inside a Zone Share area is shared with everyone standing in it, as one
            // fellowship, in place of the player's own fellowship. Quest XP is not - it falls through to the rules below.
            if (xpType == XpType.Kill && shareType.HasFlag(ShareType.Fellowship))
            {
                var zoneMembers = ZoneShareManager.MembersFor(this);
                if (zoneMembers != null)
                {
                    ACE.Server.Entity.Fellowship.ShareXpAmong(zoneMembers, (ulong)amount, xpType, shareType & ~ShareType.Fellowship, this, monsterTier, killTier);
                    return;
                }
            }

            if (Fellowship != null && Fellowship.ShareXP && shareType.HasFlag(ShareType.Fellowship))
            {
                // this will divy up the XP, and re-call this function
                // with ShareType.Fellowship removed
                Fellowship.SplitXp((ulong)amount, xpType, shareType, this, monsterTier, killTier);
                return;
            }

            var isKillWithPrestigeMonster = xpType == XpType.Kill && monsterTier > 0;

            long rewardAdjusted = amount;
            if (isKillWithPrestigeMonster)
                rewardAdjusted = (long)Math.Round(amount * PrestigeManager.GetXPRewardModifier(monsterTier));

            var finalAmount = rewardAdjusted;
            if (isKillWithPrestigeMonster)
            {
                var playerTier = GetProperty(PropertyInt.PrestigeLevel) ?? 0;
                finalAmount = (long)Math.Round(rewardAdjusted * PrestigeManager.GetXPPenaltyMultiplier(playerTier, monsterTier));
            }

            // tier dip (off unless zc_tier_dip_xp_enabled): this player's own share of a T11+ Zone Control kill
            if (xpType == XpType.Kill)
                finalAmount = ACE.Server.Managers.ZoneControl.TierRewardScale.ScaleXp(finalAmount, this, killTier);

            // Make sure UpdateXpAndLevel is done on this players thread
            EnqueueAction(new ActionEventDelegate(ActionType.PlayerXp_UpdateXpAndLevel, () => UpdateXpAndLevel(finalAmount, xpType)));

            // for passing XP up the allegiance chain,
            // this function is only called at the very beginning, to start the process.
            if (shareType.HasFlag(ShareType.Allegiance))
                UpdateXpAllegiance(finalAmount);

            // only certain types of XP are granted to items
            if (xpType == XpType.Kill || xpType == XpType.Quest)
                GrantItemXP(finalAmount);
        }

        /// <summary>
        /// Adds XP to a player's total XP, handles triggers (vitae, level up)
        /// </summary>
        private void UpdateXpAndLevel(long amount, XpType xpType)
        {
            // until we are max level we must make sure that we send
            var xpTable = DatManager.PortalDat.XpTable;

            var maxLevel = GetMaxLevel();

            if (Level != maxLevel)
            {
                var addAmount = amount;
                
                TotalExperience = AddXpClamped(TotalExperience, addAmount);

                if (!TotalExperienceDouble.HasValue || TotalExperienceDouble == 0)
                {
                    TotalExperienceDouble = TotalExperience;
                }
                else
                {
                    TotalExperienceDouble += addAmount;
                }
                AddAvailableXp(addAmount);

                // Batch XP updates to reduce network traffic
                BatchXpUpdate(addAmount, xpType);
                
            }

            if (xpType == XpType.Quest)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} experience.", ChatMessageType.Broadcast));

            if (HasVitae && xpType != XpType.Allegiance)
                UpdateXpVitae(amount);
        }

        /// <summary>
        /// Batches XP updates to reduce network packet spam during rapid XP gains
        /// </summary>
        private void BatchXpUpdate(long amount, XpType xpType)
        {
            lock (xpBatchLock)
            {
                pendingXpUpdate = AddXpClamped(pendingXpUpdate, amount);
                EnsureXpBatchChain();
            }
        }

        /// <summary>Call with xpBatchLock held. Starts the batch window if none is running.</summary>
        private void EnsureXpBatchChain()
        {
            if (xpBatchChain != null)
                return;

            xpBatchChain = new ActionChain();
            xpBatchChain.AddDelaySeconds(ServerConfig.xp_batch_window_seconds.Value);
            xpBatchChain.AddAction(this, ActionType.PlayerXp_FlushBatchedUpdate, () =>
            {
                FlushBatchedXpUpdate();
            });
            xpBatchChain.EnqueueChain();
        }

        /// <summary>
        /// The bank's luminance changed (owner 2026-09-23): send it to the client with the XP batch. It rides a batch that is
        /// already running - no timer of its own - and starts the same batch when none is (a transfer, a card table, a
        /// character that no longer gains XP). Only the luminance value is sent, never the rest of the bank, and at most once
        /// per batch window however fast it changes. Called from the BankedLuminance setter (Player_Bank.cs).
        /// </summary>
        private void QueueBankedLuminanceUpdate()
        {
            if (Session == null)
                return;

            lock (xpBatchLock)
            {
                bankedLumChanged = true;
                EnsureXpBatchChain();
            }
        }

        private bool bankedLumChanged;

        /// <summary>
        /// Sends the accumulated XP update to the client, and the banked luminance when it changed in the same window.
        /// </summary>
        private void FlushBatchedXpUpdate()
        {
            lock (xpBatchLock)
            {
                if (pendingXpUpdate > 0)
                {
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.TotalExperience, TotalExperience ?? 0));
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0));
                    pendingXpUpdate = 0;
                    CheckForLevelup();
                }
                if (bankedLumChanged)
                {
                    Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, BankedLuminance ?? 0));
                    bankedLumChanged = false;
                }
                xpBatchChain = null;
            }
        }

        /// <summary>
        /// Optionally passes XP up the Allegiance tree
        /// </summary>
        private void UpdateXpAllegiance(long amount)
        {
            if (!HasAllegiance) return;

            AllegianceManager.PassXP(AllegianceNode, (ulong)amount, true);
        }



        /// <summary>
        /// Handles updating the vitae penalty through earned XP
        /// </summary>
        /// <param name="amount">The amount of XP to apply to the vitae penalty</param>
        private void UpdateXpVitae(long amount)
        {
            var vitae = EnchantmentManager.GetVitae();

            if (vitae == null)
            {
                log.Error($"{Name}.UpdateXpVitae({amount}) vitae null, likely due to cross-thread operation or corrupt EnchantmentManager cache. Please report this.");
                log.Error(System.Environment.StackTrace);
                return;
            }

            var vitaePenalty = vitae.StatModValue;
            var startPenalty = vitaePenalty;

            var maxPool = (int)VitaeCPPoolThreshold(vitaePenalty, DeathLevel.Value);
            var curPool = VitaeCpPool + amount;
            while (curPool >= maxPool)
            {
                curPool -= maxPool;
                vitaePenalty = EnchantmentManager.ReduceVitae();
                if (vitaePenalty == 1.0f)
                    break;
                maxPool = (int)VitaeCPPoolThreshold(vitaePenalty, DeathLevel.Value);
            }
            VitaeCpPool = (int)curPool;

            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.VitaeCpPool, VitaeCpPool.Value));

            if (vitaePenalty != startPenalty)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your experience has reduced your Vitae penalty!", ChatMessageType.Magic));
                EnchantmentManager.SendUpdateVitae();
            }

            if (vitaePenalty.EpsilonEquals(1.0f) || vitaePenalty > 1.0f)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(2.0f);
                actionChain.AddAction(this, ActionType.PlayerXp_RemoveVitae, () =>
                {
                    var vitae = EnchantmentManager.GetVitae();
                    if (vitae != null)
                    {
                        var curPenalty = vitae.StatModValue;
                        if (curPenalty.EpsilonEquals(1.0f) || curPenalty > 1.0f)
                            EnchantmentManager.RemoveVitae();
                    }
                });
                actionChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Returns the maximum possible character level
        /// </summary>
        public static int GetMaxLevel()
        {
            return int.MaxValue; //(uint)DatManager.PortalDat.XpTable.CharacterLevelXPList.Count - 1;
        }

        /// <summary>
        /// Returns TRUE if player >= MaxLevel
        /// </summary>
        public bool IsMaxLevel => Level >= GetMaxLevel();

        /// <summary>
        /// Returns the remaining XP required to reach a level
        /// </summary>
        public long? GetRemainingXP(int level)
        {
            var maxLevel = GetMaxLevel();
            if (level < 1)
                return null;
            if (level == int.MaxValue)
                return long.MaxValue;
            
            if (level > 275)
            {
                return (long)(GenerateDynamicLevelPostMax(level) - TotalExperienceDouble ?? 0);
            }
            else
            {
                var levelTotalXP = (int)DatManager.PortalDat.XpTable.CharacterLevelXPList[(int)level];
                return (long)levelTotalXP - TotalExperience ?? 0;
            }
             

            
        }

        /// <summary>
        /// Returns the remaining XP required to the next level
        /// </summary>
        public ulong GetRemainingXP()
        {
            if (Level >= 275)
            {
                return (ulong)(GenerateDynamicLevelPostMax(Level + 1) - (ulong)TotalExperienceDouble.Value);
            }
            var nextLevelTotalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[Level.Value + 1];
            return nextLevelTotalXP - (ulong)TotalExperience.Value;
        }

        /// <summary>
        /// Returns the total amount of XP required for a player reach max level
        /// </summary>
        public static long MaxLevelXP
        {
            get
            {
                var xpTable = DatManager.PortalDat.XpTable.CharacterLevelXPList;

                return (long)xpTable[xpTable.Count - 1];
            }
        }

        /// <summary>
        /// Returns the XP required to go from level A to level B
        /// </summary>
        public ulong GetXPBetweenLevels(int levelA, int levelB)
        {
            // special case for max level
            var maxLevel = (int)GetMaxLevel();

            levelA = Math.Clamp(levelA, 1, maxLevel - 1);
            levelB = Math.Clamp(levelB, 1, maxLevel);
            double levelA_totalXP = 0;
            double levelB_totalXP = 0;

            if (Level > 274)
            {
                levelA_totalXP = GenerateDynamicLevelPostMax(levelA);
                levelB_totalXP = GenerateDynamicLevelPostMax(levelB);
            }
            else
            {
                levelA_totalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[levelA];
                levelB_totalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[levelB];
            }

            return (ulong)(levelB_totalXP - levelA_totalXP);
        }

        public ulong GetXPToNextLevel(int level)
        {
            return GetXPBetweenLevels(level, level + 1);
        }

        /// <summary>
        /// Determines if the player has advanced a level
        /// </summary>
        private void CheckForLevelup()
        {
            var xpTable = DatManager.PortalDat.XpTable;

            var maxLevel = GetMaxLevel();

            if (Level >= maxLevel) return;

            var startingLevel = Level;
            bool creditEarned = false;

            // increases until the correct level is found
            while
                (
                    (Level < 275 && (ulong)(TotalExperience ?? 0) >= xpTable.CharacterLevelXPList[(Level ?? 0) + 1]) //level < 275
                    || (Level >= 275 && TotalExperienceDouble > GenerateDynamicLevelPostMax(Level + 1))// level >= 275
                )
            {
                Level++;

                // increase the skill credits if the chart allows this level to grant a credit
                if (Level <= 274)
                {
                    if (xpTable.CharacterLevelSkillCreditList[Level ?? 0] > 0)
                    {
                        AvailableSkillCredits += (int)xpTable.CharacterLevelSkillCreditList[Level ?? 0];
                        TotalSkillCredits += (int)xpTable.CharacterLevelSkillCreditList[Level ?? 0];
                        creditEarned = true;
                    }
                }
                else
                {
                    if (Level % 5 == 0) //skill credit every 5th
                    {
                        AvailableSkillCredits += 1;
                        TotalSkillCredits += 1;
                        creditEarned = true;
                    }
                }

                // break if we reach max
                if (Level == maxLevel)
                {
                    PlayParticleEffect(PlayScript.WeddingBliss, Guid);
                    break;
                }
            }

            if (Level > startingLevel)
            {
                var message = (Level == maxLevel) ? $"You have reached the maximum level of {Level}!" : $"You are now level {Level}!";

                message += (AvailableSkillCredits > 0) ? $"\nYou have {AvailableExperience:#,###0} experience points and {AvailableSkillCredits} skill credits available to raise skills and attributes." : $"\nYou have {AvailableExperience:#,###0} experience points available to raise skills and attributes.";

                var levelUp = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Level, Level ?? 1);
                var currentCredits = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0);

                if (Level != maxLevel && !creditEarned)
                {
                    var nextLevelWithCredits = 0;
                    
                    for (int i = (Level ?? 0) + 1; i <= maxLevel; i++)
                    {
                        if (Level <= 275 && xpTable.CharacterLevelSkillCreditList[i] > 0)
                        {
                            nextLevelWithCredits = i;
                            break;
                        }
                        else if (Level > 275 && i % 5 == 0)
                        {
                            nextLevelWithCredits = i;
                            break;
                        }
                    }
                    message += $"\nYou will earn another skill credit at level {nextLevelWithCredits}.";
                }

                if (Fellowship != null)
                    Fellowship.OnFellowLevelUp(this);

                if (AllegianceNode != null)
                    AllegianceNode.OnLevelUp();

                Session.Network.EnqueueSend(levelUp);

                SetMaxVitals();

                // play level up effect
                PlayParticleEffect(PlayScript.LevelUp, Guid);

                Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Advancement), currentCredits);
            }
        }

        /// <summary>
        /// Finds the total XP needed for next level after maximum of 275 in the character level tables
        /// </summary>
        /// <param name="startingLevel"></param>
        /// <returns></returns>
        public static double GenerateDynamicLevelPostMax(int? startingLevel)
        {

            double nextXpDelta = (xp274to275delta + (xp274to275delta * levelRatio));
            double prevXpDelta = xp274to275delta;
            double nextXpCost = xp275;
            for (int i = 275; i < startingLevel; i++)
            {
                nextXpDelta = (prevXpDelta + (prevXpDelta * levelRatio));
                nextXpCost += nextXpDelta;
                prevXpDelta = nextXpDelta;
            }            

            return nextXpCost;
        }
            

        /// <summary>
        /// Spends the amount of XP specified, deducting it from available experience
        /// </summary>
        public bool SpendXP(ulong amount, bool sendNetworkUpdate = true)
        {
            // a cost past long.MaxValue is a wrapped ulong (e.g. a vital cost asked for a LOWER rank):
            // cast to long it is negative, and "spending" it would ADD XP
            if (amount > long.MaxValue)
            {
                log.Warn($"{Name}.SpendXP({amount}) - cost past long.MaxValue refused");
                return false;
            }

            RefillAvailableXpFromOverflow(sendNetworkUpdate);   // tells the client itself when it moves XP

            // ?? 0: a missing balance (/setproperty ... null) compares false against anything - every raise would be free
            if ((long)amount > (AvailableExperience ?? 0))
                return false;

            AvailableExperience = (AvailableExperience ?? 0) - (long)amount;

            RefillAvailableXpFromOverflow(false);   // the update below carries the result

            if (sendNetworkUpdate)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0));

            return true;
        }

       

        /// <summary>
        /// Tries to spend all of the players Xp into Attributes, Vitals and Skills
        /// </summary>
        public void SpendAllXp(bool sendNetworkUpdate = true)
        {
            SpendAllAvailableAttributeXp(Strength, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Endurance, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Coordination, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Quickness, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Focus, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Self, sendNetworkUpdate);

            SpendAllAvailableVitalXp(Health, sendNetworkUpdate);
            SpendAllAvailableVitalXp(Stamina, sendNetworkUpdate);
            SpendAllAvailableVitalXp(Mana, sendNetworkUpdate);

            foreach (var skill in Skills)
            {
                if (skill.Value.AdvancementClass >= SkillAdvancementClass.Trained)
                    SpendAllAvailableSkillXp(skill.Value, sendNetworkUpdate);
            }
        }

        /// <summary>
        /// Gives available XP of the amount specified, without increasing total XP
        /// </summary>
        public void RefundXP(long amount)
        {
            AddAvailableXp(amount);

            var xpUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0);
            Session.Network.EnqueueSend(xpUpdate);
        }

        /// <summary>
        /// Adds amount to an XP value, stopping at long.MaxValue / long.MinValue instead of wrapping around
        /// </summary>
        /// <param name="current">The current XP value, null is treated as 0</param>
        /// <param name="amount">The XP to add, can be negative</param>
        /// <returns>current + amount, clamped to the range of a long</returns>
        internal static long AddXpClamped(long? current, long amount)
        {
            var value = current ?? 0;

            if (amount > 0 && value > long.MaxValue - amount)
                return long.MaxValue;

            if (amount < 0 && value < long.MinValue - amount)
                return long.MinValue;

            return value + amount;
        }

        /// <summary>
        /// Adds earned or refunded XP to AvailableExperience. The part that would go past long.MaxValue is kept in
        /// OverflowExperience instead of being lost, and moves back as XP is spent (RefillAvailableXpFromOverflow).
        /// </summary>
        /// <param name="amount">The XP to add, can be negative</param>
        private void AddAvailableXp(long amount)
        {
            if (amount <= 0)
            {
                AvailableExperience = AddXpClamped(AvailableExperience, amount);
                RefillAvailableXpFromOverflow();   // a lower balance: top it up from the bank
                return;
            }

            var bankBefore = OverflowExperience ?? 0;
            var (available, bank) = ApplyXpGain(AvailableExperience ?? 0, bankBefore, amount);

            AvailableExperience = available;

            if (bank != bankBefore)
            {
                OverflowExperience = bank;

                // once per login: a player at the cap who spends and earns would empty and refill the bank over and over
                if (!overflowNoticeSent)
                {
                    overflowNoticeSent = true;
                    Session?.Network.EnqueueSend(new GameMessageSystemChat("Your unassigned experience is full. Extra experience is now saved for you and refills it automatically as you spend. Type /bank to see how much is saved.", ChatMessageType.Advancement));
                }
            }
        }

        /// <summary>
        /// Moves saved XP from OverflowExperience back into AvailableExperience, as much as fits under long.MaxValue
        /// </summary>
        /// <param name="sendNetworkUpdate">Tell the client the new AvailableExperience when XP moved</param>
        /// <returns>True if any XP was moved</returns>
        public bool RefillAvailableXpFromOverflow(bool sendNetworkUpdate = true)
        {
            // a hand-edited 0 or negative bank (/setproperty) reads as 0 - clear the row instead of keeping it for good
            var raw = GetProperty(PropertyInt64.OverflowExperience);
            if (raw.HasValue && raw.Value <= 0)
                RemoveProperty(PropertyInt64.OverflowExperience);

            var overflow = OverflowExperience ?? 0;
            if (overflow <= 0)
                return false;

            // god mode: /ungod puts the saved AvailableExperience back, so XP moved out of the bank now would be lost
            // (a GodState left behind by a failed /ungod keeps the bank frozen until it is cleared)
            if (!string.IsNullOrEmpty(GodState))
                return false;

            var (moved, remaining) = TakeFromOverflow(AvailableExperience ?? 0, overflow);
            if (moved == 0)
                return false;

            AvailableExperience = (AvailableExperience ?? 0) + moved;
            OverflowExperience = remaining;

            // the client shows AvailableExperience: tell it, even when the action that asked is then refused
            if (sendNetworkUpdate)
                Session?.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0));

            return true;
        }

        /// <summary>
        /// How much XP still fits in AvailableExperience under long.MaxValue
        /// </summary>
        /// <param name="available">The current AvailableExperience</param>
        /// <returns>The room left; a negative balance (an old wrap) has room for any long, and long.MaxValue - available would overflow</returns>
        internal static long RoomBelowCap(long available) => available < 0 ? long.MaxValue : long.MaxValue - available;

        /// <summary>
        /// An XP gain applied to the balance and the bank: what fits goes to AvailableExperience, the rest to OverflowExperience
        /// (which stops at long.MaxValue itself - together they hold 2 x 9.22 quintillion, past that the rest is not kept)
        /// </summary>
        /// <param name="available">The current AvailableExperience</param>
        /// <param name="bank">The current OverflowExperience</param>
        /// <param name="amount">The XP being added</param>
        /// <returns>The new AvailableExperience and OverflowExperience</returns>
        internal static (long available, long bank) ApplyXpGain(long available, long bank, long amount)
        {
            var (toAvailable, toOverflow) = SplitXpAtCap(available, amount);

            return (available + toAvailable, toOverflow > 0 ? AddXpClamped(Math.Max(0, bank), toOverflow) : bank);
        }

        /// <summary>
        /// Splits an XP gain into the part that fits in AvailableExperience and the part past long.MaxValue
        /// </summary>
        /// <param name="available">The current AvailableExperience</param>
        /// <param name="amount">The XP being added; 0 or negative is returned whole, with nothing to keep</param>
        /// <returns>The XP to add to AvailableExperience, and the XP to keep in OverflowExperience</returns>
        internal static (long toAvailable, long toOverflow) SplitXpAtCap(long available, long amount)
        {
            if (amount <= 0)
                return (amount, 0);

            var toAvailable = Math.Min(amount, RoomBelowCap(available));

            return (toAvailable, amount - toAvailable);
        }

        /// <summary>
        /// Works out how much saved XP fits back into AvailableExperience
        /// </summary>
        /// <param name="available">The current AvailableExperience</param>
        /// <param name="overflow">The current OverflowExperience</param>
        /// <returns>The XP to move into AvailableExperience, and what is left in OverflowExperience (moved + remaining = overflow)</returns>
        internal static (long moved, long remaining) TakeFromOverflow(long available, long overflow)
        {
            if (overflow <= 0)
                return (0, 0);

            var moved = Math.Min(overflow, RoomBelowCap(available));

            return (moved, overflow - moved);
        }

        public void HandleMissingXp()
        {
            var verifyXp = GetProperty(PropertyInt64.VerifyXp) ?? 0;
            if (verifyXp == 0) return;
            if (verifyXp == long.MinValue) verifyXp = long.MinValue + 1;   // Math.Abs / negating long.MinValue overflows

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, ActionType.PlayerXp_HandleMissingXp, () =>
            {
                var xpType = verifyXp > 0 ? "unassigned experience" : "experience points";

                var msg = $"This character was missing some {xpType} --\nYou have gained an additional {Math.Abs(verifyXp).ToString("N0")} {xpType}!";

                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

                if (verifyXp < 0)
                {
                    // add to character's total XP (clamped: a capped character would wrap negative)
                    TotalExperience = AddXpClamped(TotalExperience, -verifyXp);

                    CheckForLevelup();
                }

                RemoveProperty(PropertyInt64.VerifyXp);
            });

            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Returns the total amount of XP required to go from vitae to vitae + 0.01
        /// </summary>
        /// <param name="vitae">The current player life force, ie. 0.95f vitae = 5% penalty</param>
        /// <param name="level">The player DeathLevel, their level on last death</param>
        private double VitaeCPPoolThreshold(float vitae, int level)
        {
            return ((Math.Pow(level, 2.5) * 2.5 + 20.0) * Math.Pow(vitae, 5.0) + 0.5) * (1 + Enlightenment/5);
        }

        /// <summary>
        /// Raise the available XP by a percentage of the current level XP or a maximum
        /// </summary>
        public void GrantLevelProportionalXp(double percent, long min, long max)
        {
            var nextLevelXP = GetXPBetweenLevels(Level.Value, Level.Value + 1);

            var scaledXP = (long)Math.Round(nextLevelXP * percent);

            if (max > 0)
                scaledXP = Math.Min(scaledXP, max);

            if (min > 0)
                scaledXP = Math.Max(scaledXP, min);

            // apply xp modifiers?
            EarnXP(scaledXP, XpType.Quest, ShareType.Allegiance);
        }

        /// <summary>
        /// The player earns XP for items that can be leveled up
        /// by killing creatures and completing quests,
        /// while those items are equipped.
        /// </summary>
        public void GrantItemXP(long amount)
        {
            foreach (var item in EquippedObjects.Values.Where(i => i.HasItemLevel))
            {
                if (item != null && amount > 0)
                { 
                    GrantItemXP(item, amount);
                }
            }
        }

        public void GrantItemXP(WorldObject item, long amount)
        {
            var prevItemLevel = item.ItemLevel.Value;
            var addItemXP = item.AddItemXP(amount);

            if (addItemXP > 0)
                BatchItemXpUpdate(item, addItemXP);

            // handle item leveling up
            var newItemLevel = item.ItemLevel.Value;
            if (newItemLevel > prevItemLevel)
            {
                for (int i = 0; i < newItemLevel - prevItemLevel; i++)
                {
                    OnItemLevelUp(item, prevItemLevel + i);
                }

                var actionChain = new ActionChain();
                actionChain.AddAction(this, ActionType.PlayerXp_ItemIncreasedInPower, () =>
                {
                    var msg = $"Your {item.Name} has increased in power to level {newItemLevel}!";
                    Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.AetheriaLevelUp));
                });
                actionChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Batches item XP updates to reduce network packet spam during rapid XP gains
        /// </summary>
        private void BatchItemXpUpdate(WorldObject item, long amount)
        {
            var batchWindow = ServerConfig.xp_batch_window_seconds.Value;

            lock (itemXpBatchLock)
            {
                if (!pendingItemXpUpdates.ContainsKey(item.Guid.Full))
                    pendingItemXpUpdates[item.Guid.Full] = 0;

                pendingItemXpUpdates[item.Guid.Full] += amount;

                // If no batch chain exists, create one
                if (itemXpBatchChain == null)
                {
                    itemXpBatchChain = new ActionChain();
                    itemXpBatchChain.AddDelaySeconds(batchWindow);
                    itemXpBatchChain.AddAction(this, ActionType.PlayerXp_FlushBatchedUpdate, () =>
                    {
                        FlushBatchedItemXpUpdate();
                    });
                    itemXpBatchChain.EnqueueChain();
                }
            }
        }

        /// <summary>
        /// Sends the accumulated item XP updates to the client
        /// </summary>
        private void FlushBatchedItemXpUpdate()
        {
            lock (itemXpBatchLock)
            {
                foreach (var kvp in pendingItemXpUpdates)
                {
                    var itemGuid = kvp.Key;
                    var item = GetInventoryItem(new ACE.Entity.ObjectGuid(itemGuid)) ?? GetEquippedItem(new ACE.Entity.ObjectGuid(itemGuid));
                    
                    if (item != null && item.ItemTotalXp.HasValue)
                    {
                        Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(item, PropertyInt64.ItemTotalXp, item.ItemTotalXp.Value));
                    }
                }

                pendingItemXpUpdates.Clear();
                itemXpBatchChain = null;
            }
        }

        /// <summary>
        /// Returns the multiplier to XP and Luminance from Trinkets and Augmentations
        /// </summary>
        public float GetXPAndLuminanceModifier(XpType xpType)
        {
            var enchantmentBonus = EnchantmentManager.GetXPBonus();

            var augBonus = 0.0f;
            if (xpType == XpType.Kill && AugmentationBonusXp > 0)
                augBonus = AugmentationBonusXp * 0.05f;

            var modifier = 1.0f + enchantmentBonus + augBonus;
            //Console.WriteLine($"XPAndLuminanceModifier: {modifier}");

            return modifier;
        }

        /// <summary>
        /// Reads from the quest completion count property to get the running XP bonus
        /// </summary>
        /// <returns></returns>
        public double GetQuestCountXPBonus()
        {
            return 1 + (this.QuestCompletionCount ?? 1) * questToBonusRation;
        }

        public double GetEnglightenmentXPBonus()
        {
            return 1 + (this.Enlightenment * enlightenmentToBonusRatio);
        }

        public double GetTotalXPBonusMultiplier()
        {
            return GetQuestCountXPBonus() * GetXPAndLuminanceModifier(XpType.Kill) * GetEnglightenmentXPBonus();
        }

        /// <summary>
        /// Product of modifiers applied to kill XP in <see cref="EarnXP"/> before prestige / GrantXP
        /// (server xp_modifier, equipment/aug kill bonus, quest count bonus, enlightenment, and hardcore when applicable).
        /// </summary>
        public double GetKillXpModifierProduct()
        {
            var modifier = ServerConfig.xp_modifier.Value;
            var enchantment = GetXPAndLuminanceModifier(XpType.Kill);
            var quest = GetQuestCountXPBonus();
            var enlightenment = GetEnglightenmentXPBonus();
            var product = modifier * enchantment * quest * enlightenment;
            if (IsVPHardcore)
                product *= 1.0 + ServerConfig.hardcore_xp_multiplier.Value;
            return product;
        }
    }
}
