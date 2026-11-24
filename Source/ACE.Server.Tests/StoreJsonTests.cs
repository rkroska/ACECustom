using System;
using System.Linq;

using ACE.Server.Managers;
using ACE.Server.Managers.WeaponScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ACE.Server.Tests
{
    /// <summary>
    /// 2026-11-23 live regression: a mod (CustomClothingBase) set JsonConvert.DefaultSettings to a
    /// camelCase resolver. The weapon scaling store's Clone/Save went through JsonConvert, so the
    /// grade keys "S","A+",... were written as "s","a+",...; Normalize matched them case-sensitively,
    /// found none and set every ladder to null, and casters fell back to the low KMin/KMax lerp.
    /// </summary>
    [TestClass]
    public class StoreJsonTests
    {
        private static T WithCamelCaseDefaults<T>(Func<T> body)
        {
            var saved = JsonConvert.DefaultSettings;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };
            try { return body(); }
            finally { JsonConvert.DefaultSettings = saved; }
        }

        private static WeaponScalingScript FirstLadder(WeaponScalingConfig cfg) =>
            cfg.Scripts.Values.First(s => s.HasLadder);

        [TestMethod]
        public void CamelCaseDefaults_DoLowercaseGradeKeys_ViaJsonConvert()
        {
            // proves the trap is real, so the next test means something
            var json = WithCamelCaseDefaults(() => JsonConvert.SerializeObject(WeaponScalingManager.BuildDefaults()));
            StringAssert.Contains(json, "\"a+\"");
            Assert.IsFalse(json.Contains("\"A+\""));
        }

        [TestMethod]
        public void StoreJson_IgnoresDefaultSettings_AndKeepsEveryLadder()
        {
            var cfg = WeaponScalingManager.Normalize(WeaponScalingManager.BuildDefaults());
            var before = cfg.Scripts.Where(kv => kv.Value.HasLadder).ToDictionary(kv => kv.Key, kv => kv.Value.Grades);
            Assert.IsTrue(before.Count > 0, "defaults must seed at least one ladder");

            var json = WithCamelCaseDefaults(() => StoreJson.Serialize(cfg));
            StringAssert.Contains(json, "\"Grades\"");
            StringAssert.Contains(json, "\"A+\"");
            Assert.IsFalse(json.Contains("\n"), "store JSON stays compact, not the mod's Indented");

            var back = WithCamelCaseDefaults(() => WeaponScalingManager.Normalize(StoreJson.Deserialize<WeaponScalingConfig>(json)));
            foreach (var kv in before)
            {
                Assert.IsTrue(back.Scripts[kv.Key].HasLadder, $"{kv.Key} lost its ladder");
                foreach (var g in kv.Value)
                    Assert.AreEqual(g.Value, back.Scripts[kv.Key].Grades[g.Key], 1e-9, $"{kv.Key} {g.Key}");
            }
        }

        [TestMethod]
        public void Normalize_RecoversALowercasedLadder()
        {
            // the shape a camelCase save left behind (before Normalize nulled it)
            var json = WithCamelCaseDefaults(() => JsonConvert.SerializeObject(WeaponScalingManager.BuildDefaults()));
            var expected = FirstLadder(WeaponScalingManager.Normalize(WeaponScalingManager.BuildDefaults()));

            var cfg = WeaponScalingManager.Normalize(StoreJson.Deserialize<WeaponScalingConfig>(json));
            var s = FirstLadder(cfg);
            Assert.AreEqual(expected.Grades.Count, s.Grades.Count);
            Assert.AreEqual(expected.Grades["S"], s.Grades["S"], 1e-9);
            Assert.AreEqual(expected.Grades["A+"], s.Grades["A+"], 1e-9);
            Assert.AreEqual(expected.Grades["F-"], s.Grades["F-"], 1e-9);
        }

        [TestMethod]
        public void Normalize_ExactGradeKeyWins_AndGradeWeightsComeBackUppercase()
        {
            var cfg = WeaponScalingManager.Normalize(WeaponScalingManager.BuildDefaults());
            var s = FirstLadder(cfg);
            var ladder = new System.Collections.Generic.Dictionary<string, double>(s.Grades); // case-sensitive, like a fresh deserialize
            ladder["s"] = 99; // a hand-edited duplicate: the exact "S" must win
            s.Grades = ladder;
            var exactS = ladder["S"];

            cfg.GradeWeights = new System.Collections.Generic.Dictionary<string, double> { ["s"] = 0.1, ["a"] = 1.0 };

            var back = WeaponScalingManager.Normalize(cfg);
            Assert.AreEqual(exactS, FirstLadder(back).Grades["S"], 1e-9);
            CollectionAssert.AreEquivalent(new[] { "S", "A" }, back.GradeWeights.Keys.ToArray());
        }

        [TestMethod]
        public void StoreJson_EmptyOrNull_ReadsAsNull()
        {
            Assert.IsNull(StoreJson.Deserialize<WeaponScalingConfig>(null));
            Assert.IsNull(StoreJson.Deserialize<WeaponScalingConfig>(""));
        }
    }
}
