using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers.ZoneControl;

namespace ACE.Server.Tests
{
    /// <summary>Salvage Bags (2026-10-02, code name GearEssences): the pure helpers. The item paths are covered on a test server by the bench.</summary>
    [TestClass]
    public class GearEssencesTests
    {
        [TestMethod]
        public void WcidRange_IsExactlyTheSixteenEssences()
        {
            Assert.IsFalse(GearEssences.IsGearEssence(78780309));
            Assert.IsTrue(GearEssences.IsGearEssence(GearEssences.LossWcid));
            Assert.IsTrue(GearEssences.IsGearEssence(GearEssences.TransmutationWcid));
            Assert.IsTrue(GearEssences.IsGearEssence(GearEssences.TradeWcid));
            // the crafting-step bags (2026-11-26) took the retired single-type ids 320-322 and 325
            foreach (var bag in new uint[] { 78780320, 78780321, 78780322, 78780325 })
                Assert.IsTrue(GearEssences.IsGearEssence(bag));
            foreach (var free in new uint[] { 78780326, 78780327, 78780328, 78780329 })
                Assert.IsFalse(GearEssences.IsGearEssence(free));
            var count = 0;
            for (var wcid = 78780300u; wcid < 78780340u; wcid++)
                if (GearEssences.IsGearEssence(wcid)) count++;
            Assert.AreEqual(16, count);
        }

        [TestMethod]
        public void KindOf_MapsEveryEssenceAndRejectsOthers()
        {
            var kinds = new HashSet<GearEssences.EssenceKind>();
            for (var wcid = GearEssences.LossWcid; wcid <= GearEssences.ReforgingWcid; wcid++)
                if (GearEssences.IsGearEssence(wcid))
                    kinds.Add(GearEssences.KindOf(wcid));
            Assert.AreEqual(16, kinds.Count);   // one kind per essence
            Assert.AreEqual(GearEssences.EssenceKind.Genesis, GearEssences.KindOf(GearEssences.GenesisWcid));
            Assert.AreEqual(GearEssences.EssenceKind.Augmentation, GearEssences.KindOf(GearEssences.AugmentationWcid));
            Assert.AreEqual(GearEssences.EssenceKind.Alteration, GearEssences.KindOf(GearEssences.AlterationWcid));
            Assert.AreEqual(GearEssences.EssenceKind.Reforging, GearEssences.KindOf(GearEssences.ReforgingWcid));
            Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => GearEssences.KindOf(78780326));
        }

        [TestMethod]
        public void CountRefusal_GenesisNeedsAnEmptyItem()
        {
            Assert.IsNull(GearEssences.CountRefusal(GearEssences.EssenceKind.Genesis, 0, "Ring"));
            StringAssert.Contains(GearEssences.CountRefusal(GearEssences.EssenceKind.Genesis, 1, "Ring"), "has 1 property.");
            StringAssert.Contains(GearEssences.CountRefusal(GearEssences.EssenceKind.Genesis, 3, "Ring"), "has 3 properties.");
        }

        [TestMethod]
        public void CountRefusal_AugmentationNeedsExactlyOne()
        {
            Assert.IsNotNull(GearEssences.CountRefusal(GearEssences.EssenceKind.Augmentation, 0, "Ring"));
            Assert.IsNull(GearEssences.CountRefusal(GearEssences.EssenceKind.Augmentation, 1, "Ring"));
            Assert.IsNotNull(GearEssences.CountRefusal(GearEssences.EssenceKind.Augmentation, 2, "Ring"));
        }

        [TestMethod]
        public void CountRefusal_AlterationNeedsOneOrTwo()
        {
            Assert.IsNotNull(GearEssences.CountRefusal(GearEssences.EssenceKind.Alteration, 0, "Ring"));
            Assert.IsNull(GearEssences.CountRefusal(GearEssences.EssenceKind.Alteration, 1, "Ring"));
            Assert.IsNull(GearEssences.CountRefusal(GearEssences.EssenceKind.Alteration, 2, "Ring"));
            Assert.IsNotNull(GearEssences.CountRefusal(GearEssences.EssenceKind.Alteration, 3, "Ring"));
        }

        [TestMethod]
        public void CountRefusal_OtherBagsHaveNoCountRule()
        {
            foreach (var kind in new[] { GearEssences.EssenceKind.Reforging, GearEssences.EssenceKind.Gain, GearEssences.EssenceKind.Trade, GearEssences.EssenceKind.Emptiness })
                for (var count = 0; count <= 6; count++)
                    Assert.IsNull(GearEssences.CountRefusal(kind, count, "Ring"), $"{kind} at {count}");
        }

        [TestMethod]
        public void ReplacementCount_AlterationLeavesOneReforgingKeepsTheCount()
        {
            const int noCap = int.MaxValue;
            Assert.AreEqual(1, GearEssences.ReplacementCount(GearEssences.EssenceKind.Alteration, 1, 0, 4));
            Assert.AreEqual(1, GearEssences.ReplacementCount(GearEssences.EssenceKind.Alteration, 2, 0, 4));   // the second is forfeit
            Assert.AreEqual(1, GearEssences.ReplacementCount(GearEssences.EssenceKind.Alteration, 1, 1, 4));   // Reinforced + 1 stays at 2
            for (var removed = 1; removed <= 6; removed++)
                Assert.AreEqual(removed, GearEssences.ReplacementCount(GearEssences.EssenceKind.Reforging, removed, 0, noCap));
        }

        [TestMethod]
        public void ReplacementCount_NeverPastTheCap()
        {
            // a legacy necklace at 5 of 4: Reforging brings it down to the cap
            Assert.AreEqual(4, GearEssences.ReplacementCount(GearEssences.EssenceKind.Reforging, 5, 0, 4));
            // a locked line and Reinforced stay (kept 2) on a 4-cap piece with 3 changeable: room for 2
            Assert.AreEqual(2, GearEssences.ReplacementCount(GearEssences.EssenceKind.Reforging, 3, 2, 4));
            // nothing fits when what stays already fills the cap
            Assert.AreEqual(0, GearEssences.ReplacementCount(GearEssences.EssenceKind.Reforging, 1, 4, 4));
            Assert.AreEqual(4, GearEssences.ReplacementCount(GearEssences.EssenceKind.Reforging, 4, 0, 4));   // at the cap: unchanged
        }

        [TestMethod]
        public void OnlyChaseLeft_TrueOnlyWhenNoOrdinaryLineRemains()
        {
            var chase = ZoneModifiers.Catalog.Values.Where(d => d.Class == ZoneModifiers.ModifierClass.Chase).ToList();
            var ordinary = ZoneModifiers.Catalog.Values.First(d => d.Class != ZoneModifiers.ModifierClass.Chase && d.Class != ZoneModifiers.ModifierClass.None);
            Assert.IsTrue(chase.Count > 0);
            Assert.IsTrue(GearEssences.OnlyChaseLeft(chase));
            Assert.IsFalse(GearEssences.OnlyChaseLeft(chase.Append(ordinary)));
            Assert.IsFalse(GearEssences.OnlyChaseLeft(new[] { ordinary }));
        }

        [TestMethod]
        public void CanFillWithoutChase_CountsOnlyOrdinaryLines()
        {
            var chase = ZoneModifiers.Catalog.Values.Where(d => d.Class == ZoneModifiers.ModifierClass.Chase).ToList();
            var ordinary = ZoneModifiers.Catalog.Values.Where(d => d.Class != ZoneModifiers.ModifierClass.Chase && d.Class != ZoneModifiers.ModifierClass.None).Take(3).ToList();
            var pool = ordinary.Concat(chase).ToList();
            Assert.IsTrue(GearEssences.CanFillWithoutChase(pool, 3));
            Assert.IsFalse(GearEssences.CanFillWithoutChase(pool, 4));   // the 4th slot would need a Chase line: refuse up front
            Assert.IsFalse(GearEssences.CanFillWithoutChase(chase, 1));
            Assert.IsTrue(GearEssences.CanFillWithoutChase(pool, 0));
        }

        [TestMethod]
        public void IsReplaceKind_OnlyAlterationAndReforging()
        {
            foreach (GearEssences.EssenceKind kind in System.Enum.GetValues(typeof(GearEssences.EssenceKind)))
                Assert.AreEqual(kind == GearEssences.EssenceKind.Alteration || kind == GearEssences.EssenceKind.Reforging, GearEssences.IsReplaceKind(kind), kind.ToString());
        }

        [TestMethod]
        public void SlayerPhrase_UsesAnBeforeAVowel()
        {
            Assert.AreEqual("an Olthoi Slayer", GearEssences.SlayerPhrase(CreatureType.Olthoi));
            Assert.AreEqual("an Undead Slayer", GearEssences.SlayerPhrase(CreatureType.Undead));
            Assert.AreEqual("a Drudge Slayer", GearEssences.SlayerPhrase(CreatureType.Drudge));
        }

        [TestMethod]
        public void ElementRename_SwapsOnlyALeadingElementWord()
        {
            Assert.AreEqual("Flaming Ono", GearEssences.ElementRename("Frost Ono", DamageType.Fire));
            Assert.AreEqual("Frost Simi", GearEssences.ElementRename("Acid Simi", DamageType.Cold));
            Assert.AreEqual("Lightning Dagger", GearEssences.ElementRename("Corrupted Dagger", DamageType.Electric));
            Assert.AreEqual("Acid Greataxe", GearEssences.ElementRename("Lightning Greataxe", DamageType.Acid));
            Assert.IsNull(GearEssences.ElementRename("Epee", DamageType.Fire));                 // no element word
            Assert.IsNull(GearEssences.ElementRename("Frostbite Blade", DamageType.Fire));      // a word that only starts the same
            Assert.IsNull(GearEssences.ElementRename("Flaming Ono", DamageType.Fire));          // already right
            Assert.IsNull(GearEssences.ElementRename(null, DamageType.Fire));
            Assert.AreEqual("Ono", GearEssences.ElementRename("Frost Ono", DamageType.Slash));        // physical: the word goes
            Assert.AreEqual("Assagai", GearEssences.ElementRename("Corrupted Assagai", DamageType.Pierce));
            Assert.IsNull(GearEssences.ElementRename("Ono", DamageType.Bludgeon));                  // nothing to remove
            // bows and casters keep their own words (retail: "Piercing Bow", "Electric Sceptre", "Nether Staff")
            Assert.AreEqual("Blunt Bow", GearEssences.ElementRename("Piercing Bow", DamageType.Bludgeon, GearEssences.NameStyle.Launcher));
            Assert.AreEqual("Fire Yumi", GearEssences.ElementRename("Electric Yumi", DamageType.Fire, GearEssences.NameStyle.Launcher));
            Assert.AreEqual("Slashing Staff", GearEssences.ElementRename("Nether Staff", DamageType.Slash, GearEssences.NameStyle.Caster));
            Assert.AreEqual("Nether Sceptre", GearEssences.ElementRename("Acid Sceptre", DamageType.Nether, GearEssences.NameStyle.Caster));
            Assert.AreEqual("Flaming Spear", GearEssences.ElementRename("Fire Spear", DamageType.Fire));   // melee says Flaming
            Assert.IsNull(GearEssences.ElementRename("Blade of Ruin", DamageType.Fire));                 // "Blade" is not a type word
            Assert.AreEqual("Corrupted Mace", GearEssences.ElementRename("Bludgeoning Mace", DamageType.Nether));
            Assert.AreEqual("Fire Wand", GearEssences.ElementRename("Void Wand", DamageType.Fire, GearEssences.NameStyle.Caster));
            // drops made before 2026-10-04 keep their "T11 - " prefix; the type word after it follows
            Assert.AreEqual("T11 - Flaming Ono", GearEssences.ElementRename("T11 - Frost Ono", DamageType.Fire));
            Assert.AreEqual("T11 - Ono", GearEssences.ElementRename("T11 - Frost Ono", DamageType.Slash));
            Assert.IsNull(GearEssences.ElementRename("T11 - Ono", DamageType.Fire));
        }

        [TestMethod]
        public void UsesASlot_OnlyLinesThatCountTowardTheLimit()
        {
            Assert.IsTrue(GearEssences.UsesASlot(new ZoneModifiers.Def { Class = ZoneModifiers.ModifierClass.Mid }));
            Assert.IsTrue(GearEssences.UsesASlot(new ZoneModifiers.Def { Class = ZoneModifiers.ModifierClass.Mid, SetsProtection = true }));   // Reinforced counts
            Assert.IsFalse(GearEssences.UsesASlot(new ZoneModifiers.Def { Class = ZoneModifiers.ModifierClass.Always }));   // Built-in
            Assert.IsFalse(GearEssences.UsesASlot(new ZoneModifiers.Def { Class = ZoneModifiers.ModifierClass.None }));     // retired keys / non-roll lines
            Assert.IsFalse(GearEssences.UsesASlot(new ZoneModifiers.Def { Class = ZoneModifiers.ModifierClass.Mid, SlotSpecial = true }));
            Assert.IsFalse(GearEssences.UsesASlot(null));
        }

        [TestMethod]
        public void OddsToInt_NeverOverflowsAndTreatsBadValuesAsOff()
        {
            Assert.AreEqual(0, ZoneStatResolver.OddsToInt(0));
            Assert.AreEqual(0, ZoneStatResolver.OddsToInt(0.5));
            Assert.AreEqual(0, ZoneStatResolver.OddsToInt(double.NaN));
            Assert.AreEqual(0, ZoneStatResolver.OddsToInt(double.PositiveInfinity));
            Assert.AreEqual(500, ZoneStatResolver.OddsToInt(500));
            Assert.AreEqual(int.MaxValue - 1, ZoneStatResolver.OddsToInt(1e12));   // Next(1, int.MaxValue) would throw
            Assert.AreEqual(int.MaxValue - 1, ZoneStatResolver.OddsToInt(int.MaxValue));   // special_odds 2147483647 (review 2026-10-04)
            ACE.Common.ThreadSafeRandom.Next(1, ZoneStatResolver.OddsToInt(int.MaxValue));   // must not throw
        }

        [TestMethod]
        public void EndgameGate_NeverBelowV11()
        {
            Assert.AreEqual(11, ACE.Server.Managers.VariationManager.EndgameGate(0));   // a typo can never reach retail v0-v2
            Assert.AreEqual(11, ACE.Server.Managers.VariationManager.EndgameGate(2));
            Assert.AreEqual(11, ACE.Server.Managers.VariationManager.EndgameGate(11));
            Assert.AreEqual(14, ACE.Server.Managers.VariationManager.EndgameGate(14));
        }

        [TestMethod]
        public void TransmutationChoices_AlwaysANewType()
        {
            Assert.AreEqual(8, GearEssences.TransmutationChoices(DamageType.Undef).Count);           // no type: all eight
            var fire = GearEssences.TransmutationChoices(DamageType.Fire);
            Assert.AreEqual(7, fire.Count);
            Assert.IsFalse(fire.Contains(DamageType.Fire));
            Assert.IsTrue(fire.Contains(DamageType.Nether) && fire.Contains(DamageType.Slash));
            var sword = GearEssences.TransmutationChoices(DamageType.Slash | DamageType.Pierce);   // multi-type: the other six
            Assert.AreEqual(6, sword.Count);
            Assert.IsFalse(sword.Contains(DamageType.Slash) || sword.Contains(DamageType.Pierce));
        }

        [TestMethod]
        public void WeightedIndex_NeverPicksAZeroWeight()
        {
            var weights = new List<double> { 0, 0, 1, 0 };
            for (var i = 0; i < 500; i++)
                Assert.AreEqual(2, GearEssences.WeightedIndex(weights));

            var mixed = new List<double> { 1, 0, 3, 0 };
            var picks = Enumerable.Range(0, 2000).Select(_ => GearEssences.WeightedIndex(mixed)).ToList();
            Assert.IsFalse(picks.Contains(1));
            Assert.IsFalse(picks.Contains(3));
            Assert.IsTrue(picks.Count(p => p == 2) > picks.Count(p => p == 0));   // 3:1 weights

            Assert.AreEqual(-1, GearEssences.WeightedIndex(new List<double> { 0, 0 }));   // callers check the total first
        }
    }
}
