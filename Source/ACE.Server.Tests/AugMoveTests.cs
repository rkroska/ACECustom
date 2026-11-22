using System.Collections.Generic;

using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The Triune / school charm retire (owner 2026-11-22): the login move of the old counters onto the augs and the admin
    /// undo. Pure planning only - no player, no DB.
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
        public void Undo_Right_After_The_Move_Gives_Back_Exactly_The_Old_Counters()
        {
            var before = Gom();
            var add = AugMove.Plan(From(before));
            var after = new Dictionary<PropertyInt64, long>();
            foreach (var (prop, _) in AugMove.Schools)
                after[prop] = before[prop] + (add.TryGetValue(prop, out var a) ? a : 0);
            var record = new AugMove.Record();
            foreach (var kv in add) record.Added[(ushort)kv.Key] = kv.Value;
            foreach (var (old, _) in AugMove.Moves)
                if (before.TryGetValue(old, out var v)) record.Old[(ushort)old] = v;

            var undo = AugMove.PlanUndo(From(after), record);
            Assert.IsNotNull(undo);
            foreach (var (prop, _) in AugMove.Schools)
                Assert.AreEqual(before[prop], undo.Value.Counts[prop], prop.ToString());
            Assert.AreEqual(141, undo.Value.OldCounters[PropertyInt64.TriuneWeaveCount]);
            Assert.AreEqual(227, undo.Value.OldCounters[PropertyInt64.BattlemagesWrathCharmCount]);
            Assert.AreEqual(221, undo.Value.OldCounters[PropertyInt64.TrueShotCharmCount]);
        }

        [TestMethod]
        public void Undo_Turns_Augs_Earned_Since_Past_The_Caps_Back_Into_Triune_And_Charms()
        {
            // an empty record (PlanUndo only - RunMove writes a record only for a character that held a counter) and since earned Creature 6100 / Item 6000+... past the caps
            var now = new Dictionary<PropertyInt64, long>
            {
                [PropertyInt64.LumAugCreatureCount] = 6120, [PropertyInt64.LumAugItemCount] = 4090, [PropertyInt64.LumAugLifeCount] = 4100,
                [PropertyInt64.LumAugWarCount] = 4030, [PropertyInt64.LumAugVoidCount] = 3000,
            };
            var undo = AugMove.PlanUndo(From(now), new AugMove.Record());
            Assert.IsNotNull(undo);
            Assert.AreEqual(90, undo.Value.OldCounters[PropertyInt64.TriuneWeaveCount], "the part ALL three hold past the caps");
            Assert.AreEqual(6030, undo.Value.Counts[PropertyInt64.LumAugCreatureCount], "the rest stays (the old build allows it)");
            Assert.AreEqual(4000, undo.Value.Counts[PropertyInt64.LumAugItemCount]);
            Assert.AreEqual(4010, undo.Value.Counts[PropertyInt64.LumAugLifeCount]);
            Assert.AreEqual(30, undo.Value.OldCounters[PropertyInt64.BattlemagesWrathCharmCount]);
            Assert.AreEqual(4000, undo.Value.Counts[PropertyInt64.LumAugWarCount]);
            Assert.AreEqual(3000, undo.Value.Counts[PropertyInt64.LumAugVoidCount], "under 4,000: untouched");
            Assert.IsFalse(undo.Value.OldCounters.ContainsKey(PropertyInt64.NetherVeilCharmCount));
        }

        [TestMethod]
        public void Undo_Folds_Drip_Counters_Into_The_School_Before_Converting()
        {
            // moved War 3990 + 20 drip -> 4010; the undo must not leave War above 4,000 on the old build
            var record = new AugMove.Record();
            record.Added[(ushort)PropertyInt64.LumAugWarCount] = 20;
            record.Old[(ushort)PropertyInt64.AugDripWarCount] = 20;
            var undo = AugMove.PlanUndo(From(new() { [PropertyInt64.LumAugWarCount] = 4010 }), record);
            Assert.IsNotNull(undo);
            Assert.AreEqual(4000, undo.Value.Counts[PropertyInt64.LumAugWarCount]);
            Assert.AreEqual(10, undo.Value.OldCounters[PropertyInt64.BattlemagesWrathCharmCount]);
            Assert.IsFalse(undo.Value.OldCounters.ContainsKey(PropertyInt64.AugDripWarCount), "the old build never had the drip");
        }

        [TestMethod]
        public void Undo_Keeps_A_Retired_Counter_Set_After_The_Move()
        {
            // moved Triune 100; then (old build in between, or staff) Triune 30 and a 7 War charm were set again: the undo must
            // give them back on top of the record, never delete them
            var record = new AugMove.Record();
            foreach (var p in new[] { PropertyInt64.LumAugCreatureCount, PropertyInt64.LumAugItemCount, PropertyInt64.LumAugLifeCount })
                record.Added[(ushort)p] = 100;
            record.Old[(ushort)PropertyInt64.TriuneWeaveCount] = 100;
            var undo = AugMove.PlanUndo(From(new()
            {
                [PropertyInt64.LumAugCreatureCount] = 5100, [PropertyInt64.LumAugItemCount] = 3100, [PropertyInt64.LumAugLifeCount] = 3100,
                [PropertyInt64.TriuneWeaveCount] = 30, [PropertyInt64.BattlemagesWrathCharmCount] = 7,
            }), record);
            Assert.IsNotNull(undo);
            Assert.AreEqual(5000, undo.Value.Counts[PropertyInt64.LumAugCreatureCount]);
            Assert.AreEqual(130, undo.Value.OldCounters[PropertyInt64.TriuneWeaveCount]);
            Assert.AreEqual(7, undo.Value.OldCounters[PropertyInt64.BattlemagesWrathCharmCount]);

            // a drip counter held now is not in this build's power: it is not given back (it would add power on the old build)
            var dripped = AugMove.PlanUndo(From(new()
            {
                [PropertyInt64.LumAugCreatureCount] = 5100, [PropertyInt64.LumAugItemCount] = 3100, [PropertyInt64.LumAugLifeCount] = 3100,
                [PropertyInt64.LumAugWarCount] = 3000, [PropertyInt64.AugDripWarCount] = 50,
            }), record);
            Assert.AreEqual(3000, dripped.Value.Counts[PropertyInt64.LumAugWarCount]);
            Assert.IsFalse(dripped.Value.OldCounters.ContainsKey(PropertyInt64.AugDripWarCount));
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
        public void Undo_Gives_Back_A_Counter_Held_At_Zero()
        {
            // CodeRabbit 2026-11-22: a stray 0 counter is recorded at the move and comes back as 0, not "never set"
            var record = new AugMove.Record();
            record.Old[(ushort)PropertyInt64.NetherVeilCharmCount] = 0;
            var read = Gom();
            foreach (var (old, _) in AugMove.Moves)
                read.Remove(old); // after the move: the move took every retired counter off
            var plan = AugMove.PlanUndo(From(read), record);
            Assert.IsNotNull(plan);
            Assert.IsTrue(plan.Value.OldCounters.TryGetValue(PropertyInt64.NetherVeilCharmCount, out var v) && v == 0);
            Assert.AreEqual(4000, plan.Value.Counts[PropertyInt64.LumAugVoidCount], "nothing was added: the count stays");
        }

        [TestMethod]
        public void Undo_Of_A_Never_Moved_Player_Changes_Nothing()
        {
            Assert.IsNull(AugMove.PlanUndo(From(new() { [PropertyInt64.LumAugCreatureCount] = 5000, [PropertyInt64.LumAugItemCount] = 4000 }), null));
            // owner 2026-11-22: only moved characters are undone - one past the caps with no move record (an admin-set test
            // character) is left exactly as it is
            Assert.IsNull(AugMove.PlanUndo(From(new()
            {
                [PropertyInt64.LumAugCreatureCount] = 10000, [PropertyInt64.LumAugItemCount] = 10000, [PropertyInt64.LumAugLifeCount] = 10000,
                [PropertyInt64.LumAugMeleeCount] = 31500,
            }), null));
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
        public void ReadUndoRecord_Takes_What_The_Move_Writes_And_Refuses_The_Rest()
        {
            // the shape the move writes (as stored on TEST), and a zero-only one: usable
            Assert.IsNull(AugMove.ReadUndoRecord("{\n  \"v\": 1,\n  \"at\": 1795330260,\n  \"added\": {\n    \"9007\": 141\n  },\n  \"old\": {\n    \"50000\": 141\n  }\n}", out var real));
            Assert.AreEqual(141, real.Added[(ushort)PropertyInt64.LumAugCreatureCount]);
            Assert.IsNull(AugMove.ReadUndoRecord("{\"v\":1,\"at\":1795330260,\"added\":{},\"old\":{\"50000\":0}}", out var zero));
            Assert.IsNotNull(zero);

            // hand-edited ones: FAILED, never "never moved" and never deleted without putting anything back
            foreach (var bad in new[] { "", "null", "not json {", "{}", "{\"v\":1}", "{\"v\":2,\"at\":1795330260}", "{\"v\":1,\"at\":0}" })
            {
                Assert.IsNotNull(AugMove.ReadUndoRecord(bad, out var r), bad);
                Assert.IsNull(r, bad);
            }
        }
    }
}
