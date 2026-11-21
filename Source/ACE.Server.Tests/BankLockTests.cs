using System;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Command.Handlers;
using ACE.Server.Entity;
using ACE.Server.Managers;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Bank lock (2026-11-20, /banklock): which properties are the bank, which NPC emote chains deal with an item through the
    /// bank (a locked player keeps the item instead of handing it over for nothing) and which emote sets stop for a locked
    /// player, the write rule and the staff write that still reaches a locked bank, the property id, and /bankwipe confirm
    /// only wiping the character its staff member was just shown.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class BankLockTests
    {
        private static readonly PropertyInt64[] Balances =
        {
            PropertyInt64.BankedPyreals,
            PropertyInt64.BankedLuminance,
            PropertyInt64.BankedLegendaryKeys,
            PropertyInt64.BankedMythicalKeys,
            PropertyInt64.BankedEnlightenedCoins,
            PropertyInt64.BankedWeaklyEnlightenedCoins,
            PropertyInt64.BankedPrestigeCoins,
            PropertyInt64.BankedEnlightenmentTokens,
        };

        [TestMethod]
        public void IsBankProperty_TheEightBalances()
        {
            foreach (var balance in Balances)
                Assert.IsTrue(BankLock.IsBankProperty(balance), balance.ToString());
        }

        /// <summary>Nothing else is frozen: XP, available luminance and the saved XP overflow keep working on a locked character.</summary>
        [TestMethod]
        public void IsBankProperty_NothingElse()
        {
            var others = Enum.GetValues(typeof(PropertyInt64)).Cast<PropertyInt64>().Where(p => !Balances.Contains(p)).ToList();

            Assert.IsTrue(others.Contains(PropertyInt64.AvailableExperience));
            Assert.IsTrue(others.Contains(PropertyInt64.AvailableLuminance));
            Assert.IsTrue(others.Contains(PropertyInt64.OverflowExperience));

            foreach (var other in others)
                Assert.IsFalse(BankLock.IsBankProperty(other), other.ToString());
        }

        /// <summary>The list staff commands show and wipe: the eight balances, once each, in /b order.</summary>
        [TestMethod]
        public void Currencies_AreTheEightBalancesInBankOrder()
        {
            CollectionAssert.AreEqual(Balances, BankLock.Currencies.Select(c => c.Property).ToArray());
            Assert.AreEqual("Pyreals", BankLock.Currencies[0].Name);
            Assert.AreEqual("Enlightenment Tokens", BankLock.Currencies[7].Name);
        }

        /// <summary>Luminance is banked, so a hand-in whose only reward is luminance would go for nothing. A quest stamp is not a reward.</summary>
        [TestMethod]
        public void UsesBank_LuminanceAsTheOnlyReward()
        {
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance)), null));
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.StampQuest), Step(EmoteType.AwardLuminance)), null));
        }

        /// <summary>With another reward of its own the hand-in goes ahead, and only the luminance is not paid.</summary>
        [TestMethod]
        public void UsesBank_LuminanceBesideAnotherReward()
        {
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.Give)), null));
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.AwardXP), Step(EmoteType.AwardLuminance)), null));
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.CastSpell)), null));
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.TeleportTarget)), null));
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.IncrementInt64Stat, PropertyInt64.AvailableExperience)), null));
        }

        /// <summary>A test outcome with no set of its own runs nothing, so luminance beside a test that may go nowhere can be all a run pays.</summary>
        [TestMethod]
        public void UsesBank_LuminanceBesideATestWithNoSets()
        {
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.InqQuest, message: "q")), null));
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.Goto, message: "g")), null));
        }

        /// <summary>The sets a test continues in are followed: one outcome that pays only luminance keeps the item back.</summary>
        [TestMethod]
        public void UsesBank_FollowsTestBranches()
        {
            var root = Set(Step(EmoteType.InqQuest, message: "Daily"));
            var success = Branch(EmoteCategory.QuestSuccess, "Daily", Step(EmoteType.Tell));
            var lumOnly = Branch(EmoteCategory.QuestFailure, "daily", Step(EmoteType.AwardLuminance), Step(EmoteType.StampQuest, message: "Daily"));
            var withXp = Branch(EmoteCategory.QuestFailure, "Daily", Step(EmoteType.AwardXP), Step(EmoteType.AwardLuminance));

            Assert.IsTrue(BankLock.UsesBank(root, new[] { root, success, lumOnly }));    // the quest name matches in any case
            Assert.IsFalse(BankLock.UsesBank(root, new[] { root, success, withXp }));
            Assert.IsFalse(BankLock.UsesBank(root, new[] { root, success }));
        }

        /// <summary>
        /// Goto is followed too, and so are the custom tests EmoteManager.EmoteIsBranchingType does not list, into the
        /// categories EmoteManager.ExecuteEmote sends them to: InqPetRegistryCount to QuestSuccess / QuestFailure,
        /// QuestCompletionCount to TestSuccess / TestFailure / TestNoQuality.
        /// </summary>
        [TestMethod]
        public void UsesBank_FollowsGotoAndCustomInqSteps()
        {
            var viaGoto = Set(Step(EmoteType.Tell), Step(EmoteType.Goto, message: "Pay"));
            var pay = Branch(EmoteCategory.GotoSet, "Pay", Step(EmoteType.IncrementInt64Stat, PropertyInt64.BankedLuminance));
            Assert.IsTrue(BankLock.UsesBank(viaGoto, new[] { viaGoto, pay }));

            var viaPetTest = Set(Step(EmoteType.InqPetRegistryCount, message: "Pets"));
            var pets = Branch(EmoteCategory.QuestSuccess, "Pets", Step(EmoteType.AwardLuminance));
            Assert.IsTrue(BankLock.UsesBank(viaPetTest, new[] { viaPetTest, pets }));

            var notWherePetTestsGo = Branch(EmoteCategory.TestSuccess, "Pets", Step(EmoteType.IncrementInt64Stat, PropertyInt64.BankedLuminance));
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.Give), Step(EmoteType.InqPetRegistryCount, message: "Pets")), new[] { notWherePetTestsGo }));

            var viaCompletions = Set(Step(EmoteType.QuestCompletionCount, message: "Count"));
            var counted = Branch(EmoteCategory.TestNoQuality, "Count", Step(EmoteType.DecrementInt64Stat, PropertyInt64.BankedPyreals));
            Assert.IsTrue(BankLock.UsesBank(viaCompletions, new[] { viaCompletions, counted }));
        }

        /// <summary>Goto continues only in GotoSet sets: a quest outcome set with the same name is not where it goes.</summary>
        [TestMethod]
        public void UsesBank_GotoFollowsOnlyGotoSets()
        {
            var root = Set(Step(EmoteType.Give), Step(EmoteType.Goto, message: "Reward"));
            var questOutcome = Branch(EmoteCategory.QuestSuccess, "Reward", Step(EmoteType.IncrementInt64Stat, PropertyInt64.BankedLuminance));
            Assert.IsFalse(BankLock.UsesBank(root, new[] { root, questOutcome }));

            // a Goto with no Message continues in any GotoSet set (EmoteManager.GetEmoteSet filters on the quest only when given one)
            var anyName = Set(Step(EmoteType.Give), Step(EmoteType.Goto, message: null));
            var someGotoSet = Branch(EmoteCategory.GotoSet, "Anything", Step(EmoteType.IncrementInt64Stat, PropertyInt64.BankedLuminance));
            Assert.IsTrue(BankLock.UsesBank(anyName, new[] { anyName, someGotoSet }));
        }

        /// <summary>
        /// A reward a continuation always pays counts: luminance with a Goto to a sure XP set is a mixed reward. Not when the
        /// set may not be picked (a Probability under 1) or does not exist - then the run may pay luminance alone.
        /// </summary>
        [TestMethod]
        public void UsesBank_CountsRewardsAContinuationAlwaysPays()
        {
            var root = Set(Step(EmoteType.AwardLuminance), Step(EmoteType.Goto, message: "Xp"));
            var sureXp = Branch(EmoteCategory.GotoSet, "Xp", Step(EmoteType.AwardXP));
            Assert.IsFalse(BankLock.UsesBank(root, new[] { root, sureXp }));

            var maybeXp = Chance(Branch(EmoteCategory.GotoSet, "Xp", Step(EmoteType.AwardXP)), 0.5f);
            Assert.IsTrue(BankLock.UsesBank(root, new[] { root, maybeXp }));

            Assert.IsTrue(BankLock.UsesBank(root, new[] { root }));
        }

        /// <summary>An AwardXP step with a negative amount takes XP (ExecuteEmote: SpendXP), so it is no reward beside luminance.</summary>
        [TestMethod]
        public void UsesBank_XpTakenIsNoReward()
        {
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.AwardXP, amount: -1000)), null));
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.AwardNoShareXP, amount: 0)), null));
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.AwardLuminance), Step(EmoteType.AwardXP, amount: 1000)), null));
        }

        /// <summary>A chain deeper than the emote engine goes (it aborts past 100 nested sets) is assumed to reach the bank.</summary>
        [TestMethod]
        public void UsesBank_AChainPastTheEngineLimitIsAssumedToReachTheBank()
        {
            var sets = Enumerable.Range(0, 110)
                .Select(i => Branch(EmoteCategory.GotoSet, $"Step{i}", Step(EmoteType.Goto, message: $"Step{i + 1}")))
                .ToList();
            var root = Set(Step(EmoteType.Give), Step(EmoteType.Goto, message: "Step0"));
            sets.Add(root);

            Assert.IsTrue(BankLock.UsesBank(root, sets));
            Assert.IsTrue(BankLock.ReachesBankStep(root, 0, sets));
        }

        /// <summary>Only the categories a branch continues in count, and a chain that loops back on itself ends.</summary>
        [TestMethod]
        public void UsesBank_IgnoresOtherCategoriesAndEndsLoops()
        {
            var root = Set(Step(EmoteType.Give), Step(EmoteType.Goto, message: "Loop"));
            var loop = Branch(EmoteCategory.GotoSet, "Loop", Step(EmoteType.Goto, message: "Loop"));
            var notABranch = Branch(EmoteCategory.Use, "Loop", Step(EmoteType.IncrementInt64Stat, PropertyInt64.BankedPyreals));

            Assert.IsFalse(BankLock.UsesBank(root, new[] { root, loop, notABranch }));
        }

        /// <summary>The lum token NPCs pay with IncrementInt64Stat on BankedLuminance; any Int64 stat step on a balance counts, whatever else the set gives.</summary>
        [TestMethod]
        public void UsesBank_Int64StatStepsOnABalance()
        {
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.IncrementInt64Stat, PropertyInt64.BankedLuminance)), null));
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.DecrementInt64Stat, PropertyInt64.BankedPrestigeCoins)), null));
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.SetInt64Stat, PropertyInt64.BankedPyreals)), null));
            Assert.IsTrue(BankLock.UsesBank(Set(Step(EmoteType.Give), Step(EmoteType.IncrementInt64Stat, PropertyInt64.BankedLuminance)), null));
        }

        [TestMethod]
        public void UsesBank_OtherStepsDoNot()
        {
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.Give), Step(EmoteType.IncrementInt64Stat, PropertyInt64.AvailableExperience)), null));
            Assert.IsFalse(BankLock.UsesBank(Set(Step(EmoteType.Tell), Step(EmoteType.StampQuest)), null));
            Assert.IsFalse(BankLock.UsesBank(Set(), null));
            Assert.IsFalse(BankLock.UsesBank(null, null));
        }

        /// <summary>An emote set stops for a locked player before its next step while a bank step is still to come.</summary>
        [TestMethod]
        public void ReachesBankStep_FromTheGivenStepOn()
        {
            var set = Set(Step(EmoteType.Give), Step(EmoteType.DecrementInt64Stat, PropertyInt64.BankedPyreals), Step(EmoteType.Tell));

            Assert.IsTrue(BankLock.ReachesBankStep(set, 0, null));
            Assert.IsTrue(BankLock.ReachesBankStep(set, 1, null));
            Assert.IsFalse(BankLock.ReachesBankStep(set, 2, null));
            Assert.IsFalse(BankLock.ReachesBankStep(Set(Step(EmoteType.IncrementInt64Stat, PropertyInt64.AvailableLuminance)), 0, null));
            Assert.IsFalse(BankLock.ReachesBankStep(null, 0, null));
        }

        /// <summary>
        /// A set the chain continues in counts: "Goto Pay, then Give" queues the Pay set and hands over the item first, so the
        /// parent must stop when Pay holds a bank step - not just the Pay set.
        /// </summary>
        [TestMethod]
        public void ReachesBankStep_ThroughASetTheChainContinuesIn()
        {
            var use = Branch(EmoteCategory.Use, "", Step(EmoteType.Goto, message: "Pay"), Step(EmoteType.Give));
            var pay = Branch(EmoteCategory.GotoSet, "Pay", Step(EmoteType.DecrementInt64Stat, PropertyInt64.BankedLuminance));

            Assert.IsTrue(BankLock.ReachesBankStep(use, 0, new[] { use, pay }));
            Assert.IsFalse(BankLock.ReachesBankStep(use, 0, new[] { use }));
        }

        /// <summary>The lock read off a character, and the setters' rule: a write is refused only for a balance of a locked bank, outside a staff write.</summary>
        [TestMethod]
        public void RefusesWrite_ALockedCharactersBalances()
        {
            var locked = StubCharacter.Create(true);
            var unlocked = StubCharacter.Create(false);

            Assert.IsTrue(BankLock.IsLocked(locked));
            Assert.IsFalse(BankLock.IsLocked(unlocked));
            Assert.IsFalse(BankLock.IsLocked(null));

            Assert.IsTrue(BankLock.RefusesWrite(locked, PropertyInt64.BankedLuminance));
            Assert.IsFalse(BankLock.RefusesWrite(locked, PropertyInt64.AvailableLuminance));
            Assert.IsFalse(BankLock.RefusesWrite(unlocked, PropertyInt64.BankedLuminance));

            using (BankLock.AdminWrite())
                Assert.IsFalse(BankLock.RefusesWrite(locked, PropertyInt64.BankedLuminance));

            Assert.IsTrue(BankLock.RefusesWrite(locked, PropertyInt64.BankedLuminance));   // closed again
        }

        /// <summary>A staff write is open until its last scope is closed, and closing one twice does not close another.</summary>
        [TestMethod]
        public void AdminWrite_NestsAndUnwinds()
        {
            Assert.IsFalse(BankLock.AdminWriteOpen);

            var outer = BankLock.AdminWrite();
            var inner = BankLock.AdminWrite();
            try
            {
                Assert.IsTrue(BankLock.AdminWriteOpen);

                inner.Dispose();
                inner.Dispose();
                Assert.IsTrue(BankLock.AdminWriteOpen);

                outer.Dispose();
                Assert.IsFalse(BankLock.AdminWriteOpen);
            }
            finally
            {
                // a failed assert must not leave this thread's staff write open for the next test
                inner.Dispose();
                outer.Dispose();
            }
        }

        /// <summary>PropertyBool.BankLocked is 56300, and 56300 is free in every other property enum.</summary>
        [TestMethod]
        public void BankLocked_IdIsFreeInEveryOtherPropertyEnum()
        {
            Assert.AreEqual(56300, (int)PropertyBool.BankLocked);

            foreach (var type in new[] { typeof(PropertyInt), typeof(PropertyInt64), typeof(PropertyFloat), typeof(PropertyString), typeof(PropertyDataId), typeof(PropertyInstanceId) })
            {
                var used = Enum.GetValues(type).Cast<object>().Any(v => Convert.ToInt64(v) == 56300);
                Assert.IsFalse(used, $"56300 is used in {type.Name}");
            }
        }

        /// <summary>/bankwipe confirm: only the character the same staff member was shown, once, and only while the preview is good.</summary>
        [TestMethod]
        public void BankWipePreviews_ConfirmOnlyThePreviewedCharacterOnce()
        {
            var now = new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);
            BankWipePreviews.Remember(0x50000011, 0x50000100, now);

            Assert.IsFalse(BankWipePreviews.TryConsume(0x50000011, 0x50000101, now));   // a mistyped id: the next character
            Assert.IsFalse(BankWipePreviews.TryConsume(0x50000012, 0x50000100, now));   // another staff member
            Assert.IsTrue(BankWipePreviews.TryConsume(0x50000011, 0x50000100, now.AddMinutes(1)));
            Assert.IsFalse(BankWipePreviews.TryConsume(0x50000011, 0x50000100, now.AddMinutes(1)));   // used up
        }

        [TestMethod]
        public void BankWipePreviews_Expire()
        {
            var now = new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);
            BankWipePreviews.Remember(0x50000021, 0x50000200, now);

            Assert.IsFalse(BankWipePreviews.TryConsume(0x50000021, 0x50000200, now + BankWipePreviews.Lifetime + TimeSpan.FromSeconds(1)));
        }

        /// <summary>A new preview replaces the last one: only the character shown most recently can be wiped.</summary>
        [TestMethod]
        public void BankWipePreviews_NewPreviewReplacesTheLast()
        {
            var now = new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);
            BankWipePreviews.Remember(0x50000031, 0x50000300, now);
            BankWipePreviews.Remember(0x50000031, 0x50000301, now);

            Assert.IsFalse(BankWipePreviews.TryConsume(0x50000031, 0x50000300, now));
            Assert.IsTrue(BankWipePreviews.TryConsume(0x50000031, 0x50000301, now));
        }

        /// <summary>
        /// A character with nothing but a name, an id and the bank lock flag: an IPlayer made at run time (DispatchProxy, so no
        /// mocking library). Every other member returns its type's default.
        /// </summary>
        public class StubCharacter : DispatchProxy
        {
            private bool locked;

            public static IPlayer Create(bool locked)
            {
                var character = DispatchProxy.Create<IPlayer, StubCharacter>();
                ((StubCharacter)(object)character).locked = locked;
                return character;
            }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == "GetProperty" && args?.Length == 1 && args[0] is PropertyBool property)
                    return property == PropertyBool.BankLocked && locked ? true : (bool?)null;
                if (targetMethod.Name == "get_Name")
                    return "Stub";
                if (targetMethod.Name == "get_Guid")
                    return new ObjectGuid(0x50000001);

                var type = targetMethod.ReturnType;
                return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
            }
        }

        /// <summary>A Give set, as an NPC hand-in runs.</summary>
        private static PropertiesEmote Set(params PropertiesEmoteAction[] steps) => Branch(EmoteCategory.Give, "", steps);

        /// <summary>A set of the given category and quest - where a test or a Goto with that quest as its Message continues.</summary>
        private static PropertiesEmote Branch(EmoteCategory category, string quest, params PropertiesEmoteAction[] steps)
        {
            var set = new PropertiesEmote { Category = category, Probability = 1, Quest = quest };
            foreach (var step in steps)
                set.PropertiesEmoteAction.Add(step);
            return set;
        }

        /// <summary>The same set with another Probability (EmoteManager.GetEmoteSet picks by it).</summary>
        private static PropertiesEmote Chance(PropertiesEmote set, float probability)
        {
            set.Probability = probability;
            return set;
        }

        private static PropertiesEmoteAction Step(EmoteType type, PropertyInt64? stat = null, string message = "", long amount = 1)
        {
            return new PropertiesEmoteAction
            {
                Type = (uint)type,
                Delay = 0,
                Extent = 0,
                Message = message,
                TestString = "",
                Stat = stat.HasValue ? (int?)(int)stat.Value : null,
                Amount64 = amount,
            };
        }
    }
}
