using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Store round-trip compatibility for the 2026-07-30 Default-layer change.
    ///
    /// The zone store is a single JSON blob in shard config (`zonecontrol_data`). Two things in that change
    /// could silently destroy authored data on load rather than failing loudly:
    ///   * the `Boss` slot was DELETED — stored profiles still carry a "Boss" key
    ///   * the zone layer is still serialized under the legacy key "Minion"
    /// If either mis-binds, every zone loads with no stats and the only symptom is monsters quietly
    /// reverting to weenie baselines. Hence a test rather than an assumption.
    ///
    /// The JSON below is shaped exactly like the live store read on 2026-07-30.
    /// </summary>
    [TestClass]
    public class ZoneStoreCompatTests
    {
        private const string LegacyAreaJson = @"{
            ""Name"": ""Tou Tou"",
            ""Landblocks"": [62809, 62810],
            ""Variation"": 11,
            ""Enabled"": true,
            ""Bounded"": true,
            ""Notes"": null,
            ""TerrainOverrides"": { ""62809"": ""obsidian"" },
            ""Profile"": {
                ""ScopeType"": 0,
                ""Landblock"": null,
                ""Variation"": null,
                ""ZoneName"": null,
                ""Enabled"": true,
                ""Notes"": null,
                ""Minion"": {
                    ""Stats"": {
                        ""percent_hp_base"": { ""Base"": 10.0, ""Growth"": 1.0, ""Additive"": false, ""Overrides"": null },
                        ""max_health"": { ""Base"": 5000.0, ""Growth"": 1.0, ""Additive"": false, ""Overrides"": null }
                    },
                    ""BodyParts"": {},
                    ""CustomCantrips"": [],
                    ""CurrencyDrops"": [],
                    ""SpellRules"": [],
                    ""PropInts"": {},
                    ""PropInt64s"": {},
                    ""PropFloats"": {},
                    ""PropBools"": {}
                },
                ""Boss"": {
                    ""Stats"": { ""max_health"": { ""Base"": 999999.0, ""Growth"": 1.0, ""Additive"": false, ""Overrides"": null } },
                    ""BodyParts"": {}
                },
                ""WcidOverrides"": {
                    ""730000116"": {
                        ""Stats"": { ""attack_damage"": { ""Base"": 1234.0, ""Growth"": 1.0, ""Additive"": false, ""Overrides"": null } },
                        ""BodyParts"": {}
                    }
                }
            },
            ""Effects"": { ""DotEnabled"": true, ""DotDamage"": 5.0, ""DotPercent"": false, ""DotIntervalSeconds"": 3.0, ""DotDamageType"": 16 },
            ""AppearanceDefault"": {},
            ""AppearanceByWcid"": {}
        }";

        [TestMethod]
        public void LegacyStore_ZoneStatsStillLoad()
        {
            var area = JsonConvert.DeserializeObject<ControlledArea>(LegacyAreaJson);

            Assert.IsNotNull(area);
            Assert.AreEqual("Tou Tou", area.Name);
            Assert.AreEqual(11, area.Variation);
            Assert.IsTrue(area.Bounded);
            Assert.AreEqual(2, area.Landblocks.Count);
            Assert.AreEqual("obsidian", area.TerrainOverrides[62809]);

            // the zone layer still binds from the legacy "Minion" key
            Assert.AreEqual(2, area.Profile.Minion.Stats.Count);
            Assert.AreEqual(5000.0, area.Profile.Minion.Stats["max_health"].Base);
            Assert.AreEqual(10.0, area.Profile.Minion.Stats["percent_hp_base"].Base);
        }

        [TestMethod]
        public void LegacyStore_DeletedBossSlotIsIgnoredNotFatal()
        {
            // Newtonsoft ignores unknown members by default, so a stored "Boss" key simply drops.
            var area = JsonConvert.DeserializeObject<ControlledArea>(LegacyAreaJson);

            Assert.IsNotNull(area);
            Assert.AreEqual(5000.0, area.Profile.Minion.Stats["max_health"].Base,
                "the Boss block must not bleed into the zone layer");
        }

        [TestMethod]
        public void LegacyStore_WcidOverridesStillLoad()
        {
            var area = JsonConvert.DeserializeObject<ControlledArea>(LegacyAreaJson);

            Assert.AreEqual(1, area.Profile.WcidOverrides.Count);
            var bucket = area.Profile.VariantForWcid(730000116);
            Assert.IsNotNull(bucket);
            Assert.AreEqual(1234.0, bucket.Stats["attack_damage"].Base);
        }

        [TestMethod]
        public void VariantForWcid_ReturnsNullForAnUnauthoredMonster()
        {
            // Changed 2026-07-30: it used to fall back to the zone profile, which is wrong now that
            // resolution LAYERS — the bucket accessor reports only what this monster authors.
            var area = JsonConvert.DeserializeObject<ControlledArea>(LegacyAreaJson);

            Assert.IsNull(area.Profile.VariantForWcid(999999));
            Assert.IsNotNull(area.Profile.VariantForWcid(999999, create: true));
        }

        [TestMethod]
        public void VariantForWcid_SurvivesNullMapAndNullBucket()
        {
            // persisted JSON can carry an explicit null map, or a null bucket inside the map; neither may throw,
            // and both read as "absent"
            var nullMap = JsonConvert.DeserializeObject<ZoneScalingProfile>("{\"WcidOverrides\": null}");
            Assert.IsNull(nullMap.VariantForWcid(12345));

            var nullBucket = JsonConvert.DeserializeObject<ZoneScalingProfile>("{\"WcidOverrides\": {\"5\": null}}");
            Assert.IsNull(nullBucket.VariantForWcid(5));
        }

        [TestMethod]
        public void VariantForWcid_CreatesAndStoresFromNullMapAndNullBucket()
        {
            // Fresh instances: the create call must be the FIRST touch, so the null map is what it
            // normalizes - a prior non-creating lookup would have done that already and hidden the path.
            var nullMap = JsonConvert.DeserializeObject<ZoneScalingProfile>("{\"WcidOverrides\": null}");
            var created = nullMap.VariantForWcid(12345, create: true);
            Assert.IsNotNull(created);
            Assert.IsNotNull(nullMap.WcidOverrides, "create must materialize the map");
            Assert.AreSame(created, nullMap.VariantForWcid(12345), "a later non-creating lookup must return the stored bucket");
            Assert.AreEqual(1, nullMap.WcidOverrides.Count);

            var nullBucket = JsonConvert.DeserializeObject<ZoneScalingProfile>("{\"WcidOverrides\": {\"5\": null}}");
            var replaced = nullBucket.VariantForWcid(5, create: true);
            Assert.IsNotNull(replaced);
            Assert.AreSame(replaced, nullBucket.VariantForWcid(5), "the null bucket must be replaced in the map, not just returned");
            Assert.AreSame(replaced, nullBucket.WcidOverrides[5]);
        }

        [TestMethod]
        public void LegacyStore_EffectsBindToNullableFields()
        {
            var area = JsonConvert.DeserializeObject<ControlledArea>(LegacyAreaJson);

            Assert.IsTrue(area.Effects.EffectiveDotEnabled);
            Assert.AreEqual(5.0, area.Effects.EffectiveDotDamage);
            Assert.AreEqual(3.0, area.Effects.EffectiveDotIntervalSeconds);
            Assert.AreEqual(16, area.Effects.EffectiveDotDamageType);
            Assert.IsFalse(area.Effects.EffectiveDotPercent);

            // unauthored reserved fields stay null so they can inherit
            Assert.IsNull(area.Effects.SlowEnabled);
            Assert.IsNull(area.Effects.CharmEnabled);
        }

        [TestMethod]
        public void RoundTrip_SurvivesSerializeAndReload()
        {
            var area = JsonConvert.DeserializeObject<ControlledArea>(LegacyAreaJson);
            var again = JsonConvert.DeserializeObject<ControlledArea>(JsonConvert.SerializeObject(area));

            Assert.AreEqual(5000.0, again.Profile.Minion.Stats["max_health"].Base);
            Assert.AreEqual(1234.0, again.Profile.WcidOverrides[730000116].Stats["attack_damage"].Base);
            Assert.IsTrue(again.Effects.EffectiveDotEnabled);
            Assert.AreEqual(3.0, again.Effects.EffectiveDotIntervalSeconds);
            Assert.IsTrue(again.Bounded);
            Assert.AreEqual("obsidian", again.TerrainOverrides[62809]);
        }

        [TestMethod]
        public void NewStore_VariationDefaultRoundTrips()
        {
            var def = new VariationDefault();
            def.Profile.Stats["max_health"] = new StatCurve { Base = 1100 };
            def.Effects.DotEnabled = true;
            def.Effects.DotDamage = 4;

            var back = JsonConvert.DeserializeObject<VariationDefault>(JsonConvert.SerializeObject(def));

            Assert.AreEqual(1100.0, back.Profile.Stats["max_health"].Base);
            Assert.IsTrue(back.Effects.EffectiveDotEnabled);
            Assert.AreEqual(4.0, back.Effects.EffectiveDotDamage);
            Assert.IsFalse(back.IsEmpty);
        }
    }
}
