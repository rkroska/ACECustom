using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The per-variation Default layer (2026-07-30): resolution is
    /// <c>VariationDefault -&gt; zone -&gt; wcid</c>, merged PER STAT, never wholesale.
    ///
    /// These cover the merge primitives directly — they are pure static functions with no DB or DAT
    /// dependency. The merge runs once at snapshot-build time (ZoneControlManager.BuildZoneRef), so a bug
    /// here silently mis-resolves every governed monster's stats with no other symptom.
    /// </summary>
    [TestClass]
    public class ZoneDefaultLayerTests
    {
        private static ZoneVariantProfile Profile(params (string stat, double val)[] stats)
        {
            var p = new ZoneVariantProfile();
            foreach (var (stat, val) in stats)
                p.Stats[stat] = new StatCurve { Base = val };
            return p;
        }

        private static double Stat(ZoneVariantProfile p, string key) => p.Stats[key].Base;

        // ── stat layering ──

        [TestMethod]
        public void Merge_NoLayers_IsEmpty()
        {
            Assert.IsTrue(ZoneVariantProfile.Merge().IsEmpty);
            Assert.IsTrue(ZoneVariantProfile.Merge(null, null).IsEmpty);
        }

        [TestMethod]
        public void Merge_ZoneOverridesDefault_PerStat()
        {
            var def = Profile(("max_health", 1100), ("melee_defense", 1100));
            var zone = Profile(("max_health", 5000));

            var merged = ZoneVariantProfile.Merge(def, zone);

            Assert.AreEqual(5000, Stat(merged, "max_health"), "zone wins where it authors");
            Assert.AreEqual(1100, Stat(merged, "melee_defense"), "unauthored falls through to the Default");
        }

        [TestMethod]
        public void Merge_WcidIsMostSpecific()
        {
            var def = Profile(("max_health", 1100), ("melee_defense", 1100), ("attack_damage", 1100));
            var zone = Profile(("max_health", 5000), ("melee_defense", 2000));
            var wcid = Profile(("max_health", 9999));

            var merged = ZoneVariantProfile.Merge(def, zone, wcid);

            Assert.AreEqual(9999, Stat(merged, "max_health"), "wcid beats zone and default");
            Assert.AreEqual(2000, Stat(merged, "melee_defense"), "zone beats default");
            Assert.AreEqual(1100, Stat(merged, "attack_damage"), "default survives when nothing overrides");
        }

        [TestMethod]
        public void Merge_WcidSettingOneStat_DoesNotWipeTheRest()
        {
            // THE regression this layer exists to fix: a per-WCID bucket used to REPLACE the whole
            // profile, so a boss with one authored stat lost every other zone/default value.
            var def = Profile(("max_health", 1100), ("melee_defense", 1100), ("armor_level", 1100));
            var zone = Profile(("max_health", 5000));
            var wcid = Profile(("attack_damage", 7777));

            var merged = ZoneVariantProfile.Merge(def, zone, wcid);

            Assert.AreEqual(7777, Stat(merged, "attack_damage"));
            Assert.AreEqual(5000, Stat(merged, "max_health"));
            Assert.AreEqual(1100, Stat(merged, "melee_defense"));
            Assert.AreEqual(1100, Stat(merged, "armor_level"));
        }

        [TestMethod]
        public void Merge_NullLayersAreSkipped()
        {
            var zone = Profile(("max_health", 5000));

            var merged = ZoneVariantProfile.Merge(null, zone, null);

            Assert.AreEqual(5000, Stat(merged, "max_health"));
            Assert.AreEqual(1, merged.Stats.Count);
        }

        [TestMethod]
        public void Merge_DoesNotMutateItsInputs()
        {
            // The layers handed in are the LIVE admin-mutable objects behind the lock; mutating them
            // would corrupt the store and leak edits across zones.
            var def = Profile(("max_health", 1100));
            var zone = Profile(("max_health", 5000));

            ZoneVariantProfile.Merge(def, zone);

            Assert.AreEqual(1100, Stat(def, "max_health"));
            Assert.AreEqual(5000, Stat(zone, "max_health"));
            Assert.AreEqual(1, def.Stats.Count);
            Assert.AreEqual(1, zone.Stats.Count);
        }

        [TestMethod]
        public void Merge_Props_LayerPerKey()
        {
            var def = new ZoneVariantProfile();
            def.PropInts[1234] = 10;
            def.PropFloats[99] = 1.5;
            def.PropBools[7] = true;

            var zone = new ZoneVariantProfile();
            zone.PropInts[1234] = 20;

            var merged = ZoneVariantProfile.Merge(def, zone);

            Assert.AreEqual(20, merged.PropInts[1234]);
            Assert.AreEqual(1.5, merged.PropFloats[99]);
            Assert.IsTrue(merged.PropBools[7]);
        }

        // ── list-valued fields: UNION, most specific wins on collision ──

        [TestMethod]
        public void Merge_Modifiers_UnionAndDeduped()
        {
            var def = new ZoneVariantProfile();
            def.CustomModifiers.AddRange(new[] { 1, 2 });
            var zone = new ZoneVariantProfile();
            zone.CustomModifiers.AddRange(new[] { 2, 3 });

            var merged = ZoneVariantProfile.Merge(def, zone);

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, merged.CustomModifiers);
        }

        [TestMethod]
        public void Merge_CurrencyDrops_UnionKeyedByWcid()
        {
            var def = new ZoneVariantProfile();
            def.CurrencyDrops.Add(new ZoneCurrencyDrop { Wcid = 100, Amount = 1 });
            def.CurrencyDrops.Add(new ZoneCurrencyDrop { Wcid = 200, Amount = 5 });

            var zone = new ZoneVariantProfile();
            zone.CurrencyDrops.Add(new ZoneCurrencyDrop { Wcid = 100, Amount = 50 });   // collision
            zone.CurrencyDrops.Add(new ZoneCurrencyDrop { Wcid = 300, Amount = 7 });    // addition

            var merged = ZoneVariantProfile.Merge(def, zone);

            Assert.AreEqual(3, merged.CurrencyDrops.Count, "a zone adds a drop without restating the Default's");
            Assert.AreEqual(50, merged.CurrencyDrops.Find(d => d.Wcid == 100).Amount, "zone wins the collision");
            Assert.AreEqual(5, merged.CurrencyDrops.Find(d => d.Wcid == 200).Amount);
            Assert.AreEqual(7, merged.CurrencyDrops.Find(d => d.Wcid == 300).Amount);
        }

        [TestMethod]
        public void Merge_CurrencyDrops_AreClonedNotShared()
        {
            var def = new ZoneVariantProfile();
            def.CurrencyDrops.Add(new ZoneCurrencyDrop { Wcid = 100, Amount = 1 });

            var merged = ZoneVariantProfile.Merge(def);
            merged.CurrencyDrops[0].Amount = 999;

            Assert.AreEqual(1, def.CurrencyDrops[0].Amount, "merging must not alias the live entry");
        }

        [TestMethod]
        public void Merge_SpellRules_UnionKeyedBySpellId()
        {
            var def = new ZoneVariantProfile();
            def.SpellRules.Add(new ZoneSpellRule { SpellId = 10, Chance = 2.0 });

            var zone = new ZoneVariantProfile();
            zone.SpellRules.Add(new ZoneSpellRule { SpellId = 10, Chance = 25.0 });
            zone.SpellRules.Add(new ZoneSpellRule { SpellId = 20, Disabled = true });

            var merged = ZoneVariantProfile.Merge(def, zone);

            Assert.AreEqual(2, merged.SpellRules.Count);
            Assert.AreEqual(25.0, merged.SpellRules.Find(r => r.SpellId == 10).Chance);
            Assert.IsTrue(merged.SpellRules.Find(r => r.SpellId == 20).Disabled);
        }

        // ── body parts: per part, then per field within the part ──

        [TestMethod]
        public void Merge_BodyParts_LayerPerField()
        {
            var def = new ZoneVariantProfile();
            def.BodyParts[16] = new ZoneBodyPart { Armor = 500, Damage = 100 };

            var zone = new ZoneVariantProfile();
            zone.BodyParts[16] = new ZoneBodyPart { Damage = 250 };   // armor unset -> inherits

            var merged = ZoneVariantProfile.Merge(def, zone);

            Assert.AreEqual(500, merged.BodyParts[16].Armor, "unset field falls through");
            Assert.AreEqual(250, merged.BodyParts[16].Damage, "set field wins");
        }

        [TestMethod]
        public void BodyPart_Merge_HandlesNulls()
        {
            var only = new ZoneBodyPart { Armor = 1 };
            Assert.AreEqual(1, ZoneBodyPart.Merge(null, only).Armor);
            Assert.AreEqual(1, ZoneBodyPart.Merge(only, null).Armor);
            Assert.IsNull(ZoneBodyPart.Merge(null, null));
        }

        // ── effects: nullable so they can layer ──

        [TestMethod]
        public void Effects_MergePerField()
        {
            var def = new ZoneEffects { DotEnabled = true, DotDamage = 5, DotDamageType = 0x10, DotIntervalSeconds = 3 };
            var zone = new ZoneEffects { DotDamage = 12 };   // only the number, inherits the rest

            var merged = ZoneEffects.Merge(def, zone);

            Assert.IsTrue(merged.EffectiveDotEnabled);
            Assert.AreEqual(12, merged.EffectiveDotDamage);
            Assert.AreEqual(0x10, merged.EffectiveDotDamageType);
            Assert.AreEqual(3, merged.EffectiveDotIntervalSeconds);
        }

        [TestMethod]
        public void Effects_UnauthoredDefaultsMatchThePreNullableBehaviour()
        {
            // The field initializers that used to live on the class (interval 5s, Fire) must survive
            // as the Effective* fallbacks, or every zone silently changes DoT type/cadence.
            var empty = new ZoneEffects();

            Assert.IsTrue(empty.IsEmpty);
            Assert.IsFalse(empty.EffectiveDotEnabled);
            Assert.AreEqual(0.0, empty.EffectiveDotDamage);
            Assert.AreEqual(5.0, empty.EffectiveDotIntervalSeconds);
            Assert.AreEqual(0x10, empty.EffectiveDotDamageType);
            Assert.IsFalse(empty.AnyActive);
        }

        [TestMethod]
        public void Effects_AnyActive_OnlyWhenExplicitlyEnabled()
        {
            Assert.IsFalse(new ZoneEffects { DotEnabled = null }.AnyActive);
            Assert.IsFalse(new ZoneEffects { DotEnabled = false }.AnyActive);
            Assert.IsTrue(new ZoneEffects { DotEnabled = true }.AnyActive);
        }

        // ── the "zone with nothing authored IS its variation's Default" property ──

        [TestMethod]
        public void ZoneAuthoringNothing_ResolvesToTheDefaultExactly()
        {
            var def = Profile(("max_health", 1100), ("melee_defense", 1100), ("attack_damage", 1100));

            var merged = ZoneVariantProfile.Merge(def, new ZoneVariantProfile());

            Assert.AreEqual(3, merged.Stats.Count);
            Assert.AreEqual(1100, Stat(merged, "max_health"));
            Assert.AreEqual(1100, Stat(merged, "melee_defense"));
            Assert.AreEqual(1100, Stat(merged, "attack_damage"));
        }
    }
}
