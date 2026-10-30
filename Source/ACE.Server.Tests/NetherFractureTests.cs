using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using ACE.Server.Managers.ZoneControl;
using ACE.Entity.Enum;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class NetherFractureTests
    {
        private const string Valid = "{\"SchemaVersion\":1,\"Enabled\":true,\"Zones\":{\"14:Tou Tou 14\":{\"Corrosion\":0.22564000000000003,\"Corruption\":0.5076900000000001,\"Curse\":0.5641,\"Streak\":0.5641,\"Fracture\":6,\"Radius\":5}}}";

        [TestMethod]
        public void BurstVisibilityUsesCapturedOriginAndDoesNotSkipCloseWalls()
        {
            var origin = new ACE.Entity.Position(0x12340001, 1f, 2f, 0f, 0f, 0f, 0f, 1f) { Variation = 14 };
            var victim = new ACE.Server.Physics.Common.Position(origin);
            victim.Frame.Origin.X = 3f;
            var captured = new ACE.Entity.Position(origin);
            // A solid divider at x=2 separates blast/victim. A caster north of the
            // divider could see both, which must not make the blast pass through it.
            bool WallTrace(ACE.Server.Physics.Common.Position start, ACE.Server.Physics.Common.Position end)
            {
                Assert.AreEqual(1f, start.Frame.Origin.X);
                Assert.AreEqual(2f, start.Frame.Origin.Y);
                Assert.AreEqual(2f * WorldObject.ProjHeight, start.Frame.Origin.Z);
                return !(start.Frame.Origin.X < 2f && end.Frame.Origin.X > 2f);
            }
            Assert.IsFalse(NetherFractureManager.TraceBurstSight(captured, 2f, victim, 3f, WallTrace));
            origin.PositionX = 4f; // A moving source must not move the captured blast.
            Assert.IsFalse(NetherFractureManager.TraceBurstSight(captured, 2f, victim, 3f, WallTrace));
            victim.Frame.Origin.X = 1.1f;
            Assert.IsTrue(NetherFractureManager.TraceBurstSight(captured, 2f, victim, 3f, WallTrace));
            Assert.AreEqual(0f, victim.Frame.Origin.Z, "Trace must not mutate target physics position.");
            victim.Frame.Origin.X = 1.01f;
            var traces = 0;
            Assert.IsFalse(NetherFractureManager.TraceBurstSight(captured, 2f, victim, 3f, (a, b) => { traces++; return false; }));
            Assert.AreEqual(1, traces, "Even point-blank targets require a wall trace.");
            victim.Variation = 15;
            Assert.IsFalse(NetherFractureManager.TraceBurstSight(captured, 2f, victim, 3f, (a, b) => throw new Exception("Wrong variation traced")));
        }

        [TestMethod]
        public void InvalidReloadRetainsExactLastValidProfile()
        {
            var store = new NetherFractureConfigurationStore(Valid);
            var old = store.Current;
            foreach (var bad in new[] { "", "null", "{}", "{", Valid.Replace("0.5641", "-1"), Valid.Replace("\"Radius\":5", "\"Radius\":9"), Valid.Replace("\"Streak\":0.5641,", ""), Valid.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2"), Valid.Replace("\"Enabled\":true", "\"Enabled\":true,\"Enabled\":false"), Valid.Replace("\"Enabled\":true", "\"Enabeld\":true"), Valid.Replace("0.22564000000000003", "1e9999") })
            {
                Assert.IsFalse(store.TryReload(bad, out var error), bad);
                Assert.IsFalse(string.IsNullOrEmpty(error));
                Assert.AreSame(old, store.Current);
                Assert.AreEqual(.22564000000000003, store.Current.Zones["14:Tou Tou 14"].Corrosion);
            }
        }

        [TestMethod]
        public void InvalidStartupThrowsInsteadOfNativeDamageFallback()
        {
            Assert.Throws<JsonException>(() => new NetherFractureConfigurationStore("{"));
            Assert.ThrowsExactly<InvalidDataException>(() => new NetherFractureConfigurationStore("null"));
        }

        [TestMethod]
        public void ValidToggleAndCoefficientReloadIsAtomic()
        {
            var s = new NetherFractureConfigurationStore(Valid);
            Assert.IsTrue(s.TryReload(Valid.Replace("\"Enabled\":true", "\"Enabled\":false"), out _));
            Assert.IsFalse(s.Current.Enabled);
            Assert.IsTrue(s.TryReload(Valid, out _));
            Assert.IsTrue(s.Current.Enabled);
            Assert.AreEqual(6d, s.Current.Zones["14:Tou Tou 14"].Fracture);
            Assert.AreEqual(5d, s.Current.Zones["14:Tou Tou 14"].Radius);
        }

        [TestMethod]
        public void SoloIsIdentityAndGroupsNeverExceedTwoStrongestExpectedContributions()
        {
            Assert.AreEqual(1d, NetherFractureMath.GroupFactor(1, 123456, 123456));
            Assert.AreEqual(1d, NetherFractureMath.GroupFactor(2, 200, 100));
            Assert.AreEqual(2d / 3, NetherFractureMath.GroupFactor(3, 300, 100), 1e-12);
            var random = new Random(314);
            for (var count = 2; count <= 40; count++)
                for (var sample = 0; sample < 100; sample++)
                {
                    var values = Enumerable.Range(0, count).Select(_ => random.NextDouble() * 1000000).ToArray();
                    var f = NetherFractureMath.GroupFactor(count, values.Sum(), values.Max());
                    Assert.IsTrue(f <= 1 && f >= 0);
                    Assert.IsTrue(values.Sum() * f <= 2 * values.Max() + .00001);
                }
        }

        [TestMethod]
        public void ActualHealthAllocationConservesDamageAcrossMixedCastersAndOverkill()
        {
            var random = new Random(2718);
            for (var count = 1; count <= 20; count++)
                for (var sample = 0; sample < 100; sample++)
                {
                    var entries = Enumerable.Range(0, count).Select(i => ((uint)(100 + i), (ulong)random.Next(1, 10000000))).ToArray();
                    var actual = (uint)random.Next(0, 10000000);
                    var allocation = NetherFractureMath.Allocate(actual, entries);
                    Assert.AreEqual((long)actual, allocation.Sum(x => (long)x));
                    if (count == 1) Assert.AreEqual(actual, allocation[0]);
                }
            CollectionAssert.AreEqual(new uint[] { 1, 0, 0 }, NetherFractureMath.Allocate(1, new[] { (1u, 1ul), (2u, 1ul), (3u, 1ul) }));
            CollectionAssert.AreEqual(new uint[] { 0, 0 }, NetherFractureMath.Allocate(0, new[] { (1u, 1ul), (2u, 1ul) }));
        }

        [TestMethod]
        public void MeasuredBudgetAndCooldownConstantsAreUnchanged()
        {
            Assert.AreEqual(.20, NetherFractureMath.BankShare);
            Assert.AreEqual(8d, NetherFractureMath.BankSeconds);
            Assert.AreEqual(8d, NetherFractureMath.CooldownSeconds);
            Assert.AreEqual(4, NetherFractureMath.TargetCap);
        }

        [TestMethod]
        public void BaseVoidCoefficientsApplyToNormalAndCriticalAmountsWithoutScalingOtherAttackKinds()
        {
            var z = NetherFractureConfiguration.Parse(Valid).Zones["14:Tou Tou 14"];
            foreach (var pair in new[] { (5394u, z.Corrosion), (5402u, z.Corruption), (5338u, z.Curse) })
            {
                Assert.AreEqual(100f * (float)pair.Item2, IndependentVoidDots.ApplySource(z, pair.Item1, ZoneControlManager.ZcAttackKind.Void, 100f));
                Assert.AreEqual(500f * (float)pair.Item2, IndependentVoidDots.ApplySource(z, pair.Item1, ZoneControlManager.ZcAttackKind.Void, 500f));
                foreach (var kind in Enum.GetValues<ZoneControlManager.ZcAttackKind>().Where(k => k != ZoneControlManager.ZcAttackKind.Void))
                    Assert.AreEqual(100f, IndependentVoidDots.ApplySource(z, pair.Item1, kind, 100f));
            }
            Assert.IsTrue(NetherFractureManager.IsHandCastVoidStreak(SpellCategory.NetherStreak, false, false, false));
            Assert.IsTrue(NetherFractureManager.IsHandCastVoidStreak(SpellCategory.NetherStreak, true, false, true));
            Assert.IsFalse(NetherFractureManager.IsHandCastVoidStreak(SpellCategory.NetherStreak, true, false, false));
            Assert.IsFalse(NetherFractureManager.IsHandCastVoidStreak(SpellCategory.NetherStreak, false, true, false));
        }

        [TestMethod]
        public void ThreeCastersReleaseOnlyTheirOwnBanksAndStreaksCannotRearm()
        {
            var states = new[] { new NetherFractureState(), new NetherFractureState(), new NetherFractureState() };
            for (var i = 0; i < states.Length; i++)
            {
                var s = states[i];
                Assert.IsTrue(s.Prime(100, 100));
                s.Credit((uint)(1000 * (i + 1)), 101);
                Assert.IsTrue(s.Release(100, 102, 6, out var budget));
                Assert.AreEqual(1200d * (i + 1), budget, .00001);
                Assert.IsFalse(s.Release(100, 103, 6, out _));
                Assert.IsFalse(s.Prime(100, 109.99));
                s.Credit(500, 110);
                Assert.IsFalse(s.Release(100, 110, 6, out _), "Filler streak alone must not prime again.");
                Assert.IsTrue(s.Prime(100, 110));
                Assert.IsTrue(s.Release(100, 110, 6, out budget));
                Assert.AreEqual(600d, budget, .00001);
            }
        }

        [TestMethod]
        public void StaleCreditsExpireAndOneCasterCannotDoubleSpendAcrossTargets()
        {
            var s = new NetherFractureState();
            s.Credit(1000000, 100);
            s.Credit(1000, 109);
            Assert.IsTrue(s.Prime(1, 109)); Assert.IsTrue(s.Prime(2, 109));
            Assert.IsTrue(s.Release(1, 109, 6, out var budget));
            Assert.AreEqual(1200d, budget, .00001);
            Assert.IsFalse(s.Release(2, 109, 6, out _));
            Assert.IsFalse(s.Release(2, 117, 6, out _), "A second armed target cannot reuse the spent bank.");
        }

        [TestMethod]
        [DataRow("014:Tou Tou 14")]
        [DataRow("+14:Tou Tou 14")]
        [DataRow(" 14:Tou Tou 14")]
        [DataRow("14: Tou Tou 14")]
        [DataRow("14:Tou Tou 14 ")]
        public void ZoneKeysMustMatchRuntimeCanonicalFormat(string key)
        {
            Assert.ThrowsExactly<InvalidDataException>(() => NetherFractureConfiguration.Parse(Valid.Replace("14:Tou Tou 14", key)));
        }

        [TestMethod]
        public void CaseInsensitiveZoneLookupPreservesSerializedKey()
        {
            var cfg = NetherFractureConfiguration.Parse(Valid.Replace("14:Tou Tou 14", "14:tou tou 14"));
            var key = NetherFractureManager.StoredZoneKey(cfg, "14:Tou Tou 14");
            Assert.AreEqual("14:tou tou 14", key);
            var node = JsonSerializer.SerializeToNode(cfg);
            node["Zones"][key]["Corrosion"] = .3;
            var updated = NetherFractureConfiguration.Parse(node.ToJsonString());
            Assert.AreEqual(1, updated.Zones.Count);
            Assert.AreEqual(.3, updated.Zones["14:Tou Tou 14"].Corrosion);
            Assert.IsNull(NetherFractureManager.StoredZoneKey(cfg, "15:Unknown"));
            Assert.IsNull(NetherFractureManager.StoredZoneKey(null, "14:Tou Tou 14"));
        }

        [TestMethod]
        public void TimestampPollSkipsUnchangedFileAndAcceptsValidChangedFile()
        {
            NetherFractureManager.ResetForTests();
            var path = Path.GetTempFileName();
            try
            {
                var json = Valid.Replace("\"Enabled\":true", "\"Enabled\":false");
                File.WriteAllText(path, json);
                NetherFractureManager.Initialize(path);
                var original = NetherFractureManager.CurrentConfiguration;
                var stamp = File.GetLastWriteTimeUtc(path);
                NetherFractureManager.PollForTests();
                Assert.AreSame(original, NetherFractureManager.CurrentConfiguration);
                File.WriteAllText(path, json.Replace("0.5641", "0.6641"));
                File.SetLastWriteTimeUtc(path, stamp.AddSeconds(2)); // Deterministic same-length update.
                NetherFractureManager.PollForTests();
                var changed = NetherFractureManager.CurrentConfiguration;
                Assert.AreNotSame(original, changed);
                Assert.AreEqual(.6641, changed.Zones["14:Tou Tou 14"].Streak);
                NetherFractureManager.PollForTests();
                Assert.AreSame(changed, NetherFractureManager.CurrentConfiguration);
            }
            finally { NetherFractureManager.ResetForTests(); File.Delete(path); }
        }

        [TestMethod]
        public void MissingAndMalformedRuntimeFilesRetainLastValidManagerSnapshot()
        {
            NetherFractureManager.ResetForTests();
            var path = Path.GetTempFileName();
            try
            {
                var disabled = Valid.Replace("\"Enabled\":true", "\"Enabled\":false");
                File.WriteAllText(path, disabled);
                NetherFractureManager.Initialize(path);
                var initial = NetherFractureManager.CurrentConfiguration;
                NetherFractureManager.PollForTests();
                Assert.AreSame(initial, NetherFractureManager.CurrentConfiguration, "An unchanged file must keep its existing snapshot.");
                foreach (var invalid in new[] { "{", Valid.Replace("\"Radius\":5", "\"Radius\":9"), Valid.Replace("\"Enabled\":true", "\"Enabled\":true,\"Enabled\":false") })
                {
                    File.WriteAllText(path, invalid);
                    NetherFractureManager.ForceReloadForTests();
                    Assert.AreSame(initial, NetherFractureManager.CurrentConfiguration);
                }
                File.WriteAllText(path, disabled.Replace("0.22564000000000003", "0.3"));
                NetherFractureManager.ForceReloadForTests();
                var updated = NetherFractureManager.CurrentConfiguration;
                Assert.AreNotSame(initial, updated, "A valid changed file must be accepted.");
                Assert.AreEqual(.3, updated.Zones["14:Tou Tou 14"].Corrosion);
                File.Delete(path);
                NetherFractureManager.ForceReloadForTests();
                Assert.AreSame(updated, NetherFractureManager.CurrentConfiguration);
                Assert.ThrowsExactly<FileNotFoundException>(() => NetherFractureManager.Initialize(path));
            }
            finally
            {
                NetherFractureManager.ResetForTests();
                if (File.Exists(path)) File.Delete(path);
            }
            Assert.IsNull(NetherFractureManager.CurrentConfiguration, "No manager/path state may escape this test.");
        }
    }
}
