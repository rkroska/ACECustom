using System.Collections.Generic;

using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The Triune / school charm retire (owner 2026-11-22): the login move of the old counters onto the augs.
    /// Pure planning only - no player, no DB.
    /// </summary>
    [TestClass]
    public class AugMoveTests
    {
        private static System.Func<PropertyInt64, long> From(Dictionary<PropertyInt64, long> d) => p => d.TryGetValue(p, out var v) ? v : 0;

        private static Dictionary<PropertyInt64, long> Gom() => new()
        {
            // GOM on the 11-09 copy: Triune 141, the four charms
            [PropertyInt64.LumAugCreatureCount] = 6000, [PropertyInt64.LumAugItemCount] = 4000, [PropertyInt64.LumAugLifeCount] = 4000,
            [PropertyInt64.LumAugWarCount] = 4000, [PropertyInt64.LumAugVoidCount] = 4000, [PropertyInt64.LumAugMeleeCount] = 4000, [PropertyInt64.LumAugMissileCount] = 4000,
            [PropertyInt64.TriuneWeaveCount] = 141,
            [PropertyInt64.BattlemagesWrathCharmCount] = 227, [PropertyInt64.NetherVeilCharmCount] = 218,
            [PropertyInt64.CrashingSteelCharmCount] = 222, [PropertyInt64.TrueShotCharmCount] = 221,
        };

        [TestMethod]
        public void Plan_Triune_Lands_On_All_Three_And_Each_Charm_On_Its_School()
        {
            var add = AugMove.Plan(From(Gom()));
            Assert.AreEqual(141, add[PropertyInt64.LumAugCreatureCount]);
            Assert.AreEqual(141, add[PropertyInt64.LumAugItemCount]);
            Assert.AreEqual(141, add[PropertyInt64.LumAugLifeCount]);
            Assert.AreEqual(227, add[PropertyInt64.LumAugWarCount]);
            Assert.AreEqual(218, add[PropertyInt64.LumAugVoidCount]);
            Assert.AreEqual(222, add[PropertyInt64.LumAugMeleeCount]);
            Assert.AreEqual(221, add[PropertyInt64.LumAugMissileCount]);
            Assert.AreEqual("Creature +141, Item +141, Life +141, War +227, Void +218, Melee +222, Missile +221", AugMove.AddedText(add));
        }

        [TestMethod]
        public void Plan_Drip_Counters_Add_Onto_Their_School_Too()
        {
            var add = AugMove.Plan(From(new Dictionary<PropertyInt64, long>
            {
                [PropertyInt64.TriuneWeaveCount] = 10, [PropertyInt64.AugDripLifeCount] = 5, [PropertyInt64.AugDripWarCount] = 3,
                [PropertyInt64.NetherVeilCharmCount] = -4,
            }));
            Assert.AreEqual(10, add[PropertyInt64.LumAugCreatureCount]);
            Assert.AreEqual(15, add[PropertyInt64.LumAugLifeCount]);
            Assert.AreEqual(3, add[PropertyInt64.LumAugWarCount]);
            Assert.IsFalse(add.ContainsKey(PropertyInt64.LumAugVoidCount), "a negative counter adds nothing");
            Assert.AreEqual(0, AugMove.Plan(From(new())).Count, "nothing to move");
        }

        [TestMethod]
        public void MovedOld_Reads_What_Was_Held_And_Null_For_What_Never_Was()
        {
            var record = new AugMove.Record();
            record.Old[(ushort)PropertyInt64.TriuneWeaveCount] = 141;
            record.Old[(ushort)PropertyInt64.NetherVeilCharmCount] = 0;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(record);
            Assert.AreEqual(141L, AugMove.MovedOld(json, PropertyInt64.TriuneWeaveCount));
            Assert.AreEqual(0L, AugMove.MovedOld(json, PropertyInt64.NetherVeilCharmCount), "held at 0: reads 0");
            Assert.IsNull(AugMove.MovedOld(json, PropertyInt64.TrueShotCharmCount), "never set: null, so TestNoQuality still fires");
            Assert.IsNull(AugMove.MovedOld((string)null, PropertyInt64.TriuneWeaveCount), "no record");
            Assert.AreEqual(0L, AugMove.MovedOld("{not json", PropertyInt64.TriuneWeaveCount), "unreadable record");
        }

        [TestMethod]
        public void A_Hand_Edited_Null_Record_Reads_As_Empty()
        {
            var r = Newtonsoft.Json.JsonConvert.DeserializeObject<AugMove.Record>("{\"Added\":null,\"Old\":null}");
            Assert.IsNotNull(r.Added);
            Assert.IsNotNull(r.Old);
            Assert.AreEqual(0, r.Added.Count + r.Old.Count);
            Assert.IsNull(AugMove.MovedOld("{\"Added\":null,\"Old\":null}", PropertyInt64.TriuneWeaveCount));
        }

        [TestMethod]
        public void A_Record_From_Before_The_Seed_Flag_Reads_Not_Seeded()
        {
            var old = Newtonsoft.Json.JsonConvert.DeserializeObject<AugMove.Record>("{\"V\":1,\"At\":1,\"Added\":{},\"Old\":{\"150\":141}}");
            Assert.IsFalse(old.Seeded, "an older record seeds once more at login (rolls only go up)");
            var seeded = new AugMove.Record { Seeded = true };
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<AugMove.Record>(Newtonsoft.Json.JsonConvert.SerializeObject(seeded));
            Assert.IsTrue(back.Seeded, "the flag survives the save");
        }

        [TestMethod]
        public void CarriedFraction_Is_Three_Rewards_Per_Triune_Point_Kept_Below_One()
        {
            Assert.AreEqual(0.9, AugMove.CarriedFraction(0, 0.3), 1e-9, "0.3 of a Triune point = 0.9 of a reward");
            Assert.AreEqual(0.999, AugMove.CarriedFraction(0, 0.801), 1e-9, "2.4 rewards: kept below a whole one (owner: the small loss is fine)");
            Assert.AreEqual(0.999, AugMove.CarriedFraction(0.5, 0.2), 1e-9, "on top of a part-way reward, still below one");
            Assert.AreEqual(0.4, AugMove.CarriedFraction(0.1, 0.1), 1e-9);
            Assert.AreEqual(0.3, AugMove.CarriedFraction(-1, 0.1), 1e-9, "a bad stored part-way reward reads as 0");
        }
    }
}
