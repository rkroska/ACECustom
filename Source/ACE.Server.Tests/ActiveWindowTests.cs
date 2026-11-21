using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Database.Models.Auth;
using ACE.Server.Managers;

namespace ACE.Server.Tests
{
    /// <summary>The "active in the last N days" filter on the leaderboards and the Economy page.</summary>
    [TestClass]
    public class ActiveWindowTests
    {
        private const string Marker = "/*active:";

        private static IEnumerable<(string name, FormattableString sql)> SelfPlacementQueries() =>
            typeof(LeaderboardInlineSql).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.StartsWith("SelfPlacement") && m.ReturnType == typeof(FormattableString))
                .Select(m =>
                {
                    var args = m.GetParameters().Select(p => p.ParameterType == typeof(ushort) ? (object)(ushort)25 : (object)7u).ToArray();
                    return (m.Name, (FormattableString)m.Invoke(null, args));
                });

        // ---- leaderboard SQL ----------------------------------------------------------------------------------------

        [TestMethod]
        public void EveryBoard_CanBeFiltered()
        {
            Assert.IsTrue(LeaderboardInlineSql.TopBoards.Count >= 22);
            foreach (var (id, sql) in LeaderboardInlineSql.TopBoards)
            {
                // every eligible-account join carries the marker, so no join escapes the filter
                var joins = sql.Split("INNER JOIN account ").Length - 1;
                Assert.IsTrue(joins >= 1, id);
                Assert.AreEqual(joins, LeaderboardInlineSql.ActiveMarkerCount(sql), id);
            }
        }

        [TestMethod]
        public void EverySelfPlacement_CanBeFiltered()
        {
            var queries = SelfPlacementQueries().ToList();
            Assert.IsTrue(queries.Count >= 18);
            foreach (var (name, sql) in queries)
            {
                var joins = sql.Format.Split("INNER JOIN account ").Length - 1;
                Assert.IsTrue(joins >= 1, name);
                Assert.AreEqual(joins, LeaderboardInlineSql.ActiveMarkerCount(sql.Format), name);
            }
        }

        [TestMethod]
        public void Window_AddsTheGameLoginCondition_PerJoinAlias()
        {
            var qb = LeaderboardInlineSql.ForWindow(LeaderboardInlineSql.TopBoards["qb"], 30);
            Assert.IsFalse(qb.Contains(Marker));
            StringAssert.Contains(qb, "act.account_Id = acc.accountId");
            StringAssert.Contains(qb, "act.last_Login_Timestamp >= UNIX_TIMESTAMP() - 2592000");
            StringAssert.Contains(qb, "act.is_Deleted = 0");

            var lum = LeaderboardInlineSql.ForWindow(LeaderboardInlineSql.TopBoards["lum"], 14);
            StringAssert.Contains(lum, "act.account_Id = a.accountId");
            StringAssert.Contains(lum, "UNIX_TIMESTAMP() - 1209600");
        }

        [TestMethod]
        public void WindowZero_FiltersNobody()
        {
            foreach (var (id, sql) in LeaderboardInlineSql.TopBoards)
            {
                var all = LeaderboardInlineSql.ForWindow(sql, 0);
                Assert.IsFalse(all.Contains(Marker), id);
                Assert.IsFalse(all.Contains("last_Login_Timestamp"), id);
            }

            // left unreplaced, the marker is only a comment
            Assert.AreEqual("", LeaderboardInlineSql.ActiveCondition("a", -5));
        }

        [TestMethod]
        public void SelfPlacementWindow_KeepsTheAccountArgument()
        {
            var sql = LeaderboardInlineSql.ForWindow(LeaderboardInlineSql.SelfPlacementLum(4242), 30);
            Assert.AreEqual(1, sql.ArgumentCount);
            Assert.AreEqual(4242u, sql.GetArgument(0));
            StringAssert.Contains(sql.Format, "UNIX_TIMESTAMP() - 2592000");
            StringAssert.Contains(sql.Format, "{0}");
        }

        // ---- window choices ----------------------------------------------------------------------------------------

        [TestMethod]
        public void Options_AreThePresetsPlusTheDefault_EveryoneLast()
        {
            CollectionAssert.AreEqual(new[] { 7, 14, 30, 60, 90, 0 }, ActiveWindow.Options(30));
            CollectionAssert.AreEqual(new[] { 7, 14, 21, 30, 60, 90, 0 }, ActiveWindow.Options(21));
            CollectionAssert.AreEqual(new[] { 7, 14, 30, 60, 90, 0 }, ActiveWindow.Options(0));
        }

        [TestMethod]
        public void TryResolve_AcceptsOfferedWindowsOnly()
        {
            Assert.IsTrue(ActiveWindow.TryResolve(null, 30, out var d)); Assert.AreEqual(30, d);
            Assert.IsTrue(ActiveWindow.TryResolve(14, 30, out d)); Assert.AreEqual(14, d);
            Assert.IsTrue(ActiveWindow.TryResolve(0, 30, out d)); Assert.AreEqual(0, d);
            Assert.IsTrue(ActiveWindow.TryResolve(21, 21, out d)); Assert.AreEqual(21, d);
            Assert.IsFalse(ActiveWindow.TryResolve(13, 30, out _));
            Assert.IsFalse(ActiveWindow.TryResolve(-1, 30, out _));
        }

        [TestMethod]
        public void FromSetting_Clamps()
        {
            Assert.AreEqual(0, ActiveWindow.FromSetting(-3));
            Assert.AreEqual(30, ActiveWindow.FromSetting(30));
            Assert.AreEqual(ActiveWindow.MaxDays, ActiveWindow.FromSetting(long.MaxValue));
        }

        [TestMethod]
        public void Describe_IsPlainAscii()
        {
            foreach (var d in new[] { 0, 1, 30 })
                Assert.IsTrue(ActiveWindow.Describe(d).All(c => c < 128), d.ToString());
        }

        // ---- economy holdings --------------------------------------------------------------------------------------

        private static readonly string[] Keys = { "luminance", "keys", "coins" };

        private static Dictionary<uint, long>[] Empty() => Keys.Select(_ => new Dictionary<uint, long>()).ToArray();

        [TestMethod]
        public void AccountHoldings_SumPerAccount_SkipStaffAndEmpty()
        {
            var banked = Empty();
            var items = Empty();

            // currency 0: account 10 has two characters (one with items), account 20 holds nothing positive, char 3 is staff
            banked[0][1] = 100;
            banked[0][2] = 50;
            items[0][2] = 7;
            banked[0][4] = 0;
            banked[0][3] = 999;
            items[0][5] = -4;

            var accountOf = new Dictionary<uint, uint> { [1] = 10, [2] = 10, [4] = 20, [5] = 20 };
            var idleOf = new Dictionary<uint, long> { [10] = 3600 };

            var result = EconomyLedger.HoldingsBuilder.Build(Keys, banked, items, accountOf, idleOf);

            Assert.AreEqual(1, result.Count, "staff, zero and negative balances are not holdings");
            var h = result.Single();
            Assert.AreEqual("luminance", h.Currency);
            Assert.AreEqual(10u, h.AccountId);
            Assert.AreEqual(150, h.Banked);
            Assert.AreEqual(7, h.Items);
            Assert.AreEqual(3600L, h.IdleSeconds);
        }

        [TestMethod]
        public void AccountHoldings_UnknownLogin_IsNull_AndCurrenciesStayApart()
        {
            var banked = Empty();
            var items = Empty();
            banked[2][9] = 5;
            items[1][9] = 2;

            var result = EconomyLedger.HoldingsBuilder.Build(Keys, banked, items, new Dictionary<uint, uint> { [9] = 90 }, new Dictionary<uint, long>());

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(r => r.AccountId == 90 && r.IdleSeconds == null));
            var coins = result.Single(r => r.Currency == "coins");
            Assert.AreEqual(5, coins.Banked);
            Assert.AreEqual(0, coins.Items);
            var keys = result.Single(r => r.Currency == "keys");
            Assert.AreEqual(0, keys.Banked);
            Assert.AreEqual(2, keys.Items);
        }
    }
}
