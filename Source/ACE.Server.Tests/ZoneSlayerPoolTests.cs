using System;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>T11+ rares on the monster's weakest element, and normal Slayers from a zone pool (2026-10-30).</summary>
    [TestClass]
    public class ZoneSlayerPoolTests
    {
        private const DamageType SlashPierce = DamageType.Slash | DamageType.Pierce;

        private static uint nextGuid = 0xF0005850;
        private static int nextZone;

        private static string NewZone() => $"Test Zone {++nextZone}|11";

        // -- the pool ------------------------------------------------------------------------------------

        [TestMethod]
        public void Pick_IsWeightedBySpawnCount()
        {
            var zone = NewZone();
            for (var i = 0; i < 9; i++)
                ZoneSlayerPool.Add(zone, CreatureType.Ursuin);
            ZoneSlayerPool.Add(zone, CreatureType.Moarsman);

            var ursuin = 0;
            const int steps = 1000;
            for (var i = 0; i < steps; i++)
                if (ZoneSlayerPool.Pick(zone, (i + 0.5) / steps) == CreatureType.Ursuin)
                    ursuin++;

            Assert.AreEqual(900, ursuin, 2);
        }

        [TestMethod]
        public void Pick_EmptyPool_ReturnsNull_SoTheKilledSpeciesIsUsed()
        {
            Assert.IsNull(ZoneSlayerPool.Pick("No Such Zone|11", 0.5));
        }

        [TestMethod]
        public void Pick_StaysInItsOwnZone()
        {
            var a = NewZone();
            var b = NewZone();
            ZoneSlayerPool.Add(a, CreatureType.Sleech);
            ZoneSlayerPool.Add(b, CreatureType.Golem);
            Assert.AreEqual(CreatureType.Sleech, ZoneSlayerPool.Pick(a, 0.5));
            Assert.AreEqual(CreatureType.Golem, ZoneSlayerPool.Pick(b, 0.5));
        }

        [TestMethod]
        public void UsesPool_FollowsTheShare_AndDrawsNothingAtTheEnds()
        {
            var draws = 0;
            double Roll(double value) { draws++; return value; }

            Assert.IsFalse(ZoneSlayerPool.UsesPool(0.0, () => Roll(0.0)));
            Assert.IsFalse(ZoneSlayerPool.UsesPool(-1.0, () => Roll(0.0)));
            Assert.IsFalse(ZoneSlayerPool.UsesPool(double.NaN, () => Roll(0.0)));
            Assert.IsTrue(ZoneSlayerPool.UsesPool(1.0, () => Roll(0.99)));
            Assert.AreEqual(0, draws, "a share of 0 or 1 needs no roll");

            Assert.IsTrue(ZoneSlayerPool.UsesPool(0.5, () => Roll(0.49)));
            Assert.IsFalse(ZoneSlayerPool.UsesPool(0.5, () => Roll(0.5)));
            Assert.AreEqual(2, draws);
        }

        [TestMethod]
        public void Counts_OnlyRealMonsters()
        {
            Assert.IsTrue(ZoneSlayerPool.Counts(false, true, CreatureType.Golem, false));
            Assert.IsFalse(ZoneSlayerPool.Counts(true, false, CreatureType.Human, false), "town NPC");
            Assert.IsFalse(ZoneSlayerPool.Counts(false, false, CreatureType.Human, false), "not attackable");
            Assert.IsFalse(ZoneSlayerPool.Counts(false, true, null, false), "no species");
            Assert.IsFalse(ZoneSlayerPool.Counts(false, true, CreatureType.Invalid, false), "invalid species");
            Assert.IsFalse(ZoneSlayerPool.Counts(false, true, CreatureType.Golem, true), "pet-only monster");
        }

        [TestMethod]
        public void Admit_StopsASourceAtItsCap_SoRespawnsCannotDriftTheMix()
        {
            var zone = NewZone();
            Assert.IsTrue(ZoneSlayerPool.Admit(zone, 1, 100, 2));
            Assert.IsTrue(ZoneSlayerPool.Admit(zone, 1, 100, 2));
            for (var respawn = 0; respawn < 5; respawn++)
                Assert.IsFalse(ZoneSlayerPool.Admit(zone, 1, 100, 2));

            Assert.IsTrue(ZoneSlayerPool.Admit(zone, 2, 100, 1), "another source counts on its own");
            Assert.IsTrue(ZoneSlayerPool.Admit(zone, 1, 101, 1), "and another WCID from the same source");
            Assert.IsTrue(ZoneSlayerPool.Admit(NewZone(), 1, 100, 1), "and the same source in another zone");
        }

        // -- weakest elements ----------------------------------------------------------------------------

        private static double[] Flat(double value) => Enumerable.Repeat(value, ZoneSlayerPool.ElementOrder.Count).ToArray();

        private static double[] ResistWith(params (DamageType Type, double Value)[] set)
        {
            var values = Flat(0.25);
            foreach (var (type, value) in set)
                values[ZoneSlayerPool.ElementOrder.ToList().IndexOf(type)] = value;
            return values;
        }

        [TestMethod]
        public void Weakest_IsTheOneElementAtFullDamage()
        {
            var r = ResistWith((DamageType.Electric, 1.0));
            Assert.AreEqual(DamageType.Electric, ZoneSlayerPool.WeakestElements(r, DamageType.Undef, 2.0, Flat(1.0), 500));
        }

        [TestMethod]
        public void Weakest_AllEqual_IsUndef()
        {
            Assert.AreEqual(DamageType.Undef, ZoneSlayerPool.WeakestElements(Flat(1.0), DamageType.Undef, 2.0, Flat(1.0), 500));
        }

        [TestMethod]
        public void Weakest_ATie_IsAMask()
        {
            var r = ResistWith((DamageType.Fire, 1.0), (DamageType.Acid, 1.0));
            Assert.AreEqual(DamageType.Fire | DamageType.Acid, ZoneSlayerPool.WeakestElements(r, DamageType.Undef, 2.0, Flat(1.0), 500));
        }

        [TestMethod]
        public void Weakest_CountsTheWeaknessMask()
        {
            Assert.AreEqual(DamageType.Cold, ZoneSlayerPool.WeakestElements(Flat(1.0), DamageType.Cold, 2.0, Flat(1.0), 500));
        }

        [TestMethod]
        public void Weakest_CountsArmorVsType()
        {
            var armor = Flat(1.0);
            armor[ZoneSlayerPool.ElementOrder.ToList().IndexOf(DamageType.Bludgeon)] = 0.5;
            Assert.AreEqual(DamageType.Bludgeon, ZoneSlayerPool.WeakestElements(Flat(1.0), DamageType.Undef, 2.0, armor, 500));
        }

        // -- the rare's element --------------------------------------------------------------------------

        [TestMethod]
        public void RareElement_MovesToTheWeakest()
        {
            Assert.AreEqual(DamageType.Acid, ZoneSlayerPool.ChooseRareElement(DamageType.Fire, DamageType.Acid));
            Assert.AreEqual(DamageType.Cold, ZoneSlayerPool.ChooseRareElement(DamageType.Slash, DamageType.Cold));
        }

        [TestMethod]
        public void RareElement_LeavesARightWeaponAlone()
        {
            Assert.IsNull(ZoneSlayerPool.ChooseRareElement(DamageType.Fire, DamageType.Fire));
            Assert.IsNull(ZoneSlayerPool.ChooseRareElement(DamageType.Fire, DamageType.Fire | DamageType.Acid), "in a tie");
        }

        [TestMethod]
        public void RareElement_SlashPierce_EndsOnOneType()
        {
            Assert.AreEqual(DamageType.Pierce, ZoneSlayerPool.ChooseRareElement(SlashPierce, DamageType.Pierce));
            Assert.AreEqual(DamageType.Slash, ZoneSlayerPool.ChooseRareElement(SlashPierce, DamageType.Slash));
            Assert.AreEqual(DamageType.Fire, ZoneSlayerPool.ChooseRareElement(SlashPierce, DamageType.Fire));
            Assert.AreEqual(DamageType.Slash, ZoneSlayerPool.ChooseRareElement(SlashPierce, SlashPierce), "a tie takes the lowest");
        }

        [TestMethod]
        public void RareElement_ATie_TakesTheLowestBit()
        {
            Assert.AreEqual(DamageType.Fire, ZoneSlayerPool.ChooseRareElement(DamageType.Slash, DamageType.Fire | DamageType.Acid));
        }

        [TestMethod]
        public void RareElement_SkipsVoid_PlainBows_AndNoWeakness()
        {
            Assert.IsNull(ZoneSlayerPool.ChooseRareElement(DamageType.Nether, DamageType.Fire), "Void stays Void");
            Assert.IsNull(ZoneSlayerPool.ChooseRareElement(DamageType.Undef, DamageType.Fire), "a plain bow's ammo decides");
            Assert.IsNull(ZoneSlayerPool.ChooseRareElement(DamageType.Base, DamageType.Fire));
            Assert.IsNull(ZoneSlayerPool.ChooseRareElement(DamageType.Fire, DamageType.Undef), "no weakness: keep the roll");
        }

        [TestMethod]
        public void ApplyWeakestElement_WithNoKill_ChangesNothing()
        {
            var sword = Melee(DamageType.Fire, "Flaming Ono");
            ZoneRare.ApplyWeakestElement(sword, null);
            Assert.AreEqual(DamageType.Fire, sword.W_DamageType, "the dev mint has no kill");
        }

        // -- retyping a drop -----------------------------------------------------------------------------

        private static MeleeWeapon Melee(DamageType type, string name, ImbuedEffectType imbue = ImbuedEffectType.Undef)
        {
            var weenie = new Weenie { WeenieClassId = 458500, ClassName = "TestSlayerPoolSword", WeenieType = WeenieType.MeleeWeapon };
            var w = new MeleeWeapon(weenie, new ObjectGuid(nextGuid++));
            w.W_DamageType = type;
            w.ImbuedEffect = imbue;
            w.Name = name;
            return w;
        }

        [TestMethod]
        public void RetypeDrop_MovesTheTypeAndAnyRend()
        {
            var sword = Melee(DamageType.Fire, "Flaming Ono", ImbuedEffectType.FireRending);
            GearEssences.RetypeDrop(sword, DamageType.Cold);

            Assert.AreEqual(DamageType.Cold, sword.W_DamageType);
            Assert.AreEqual(ImbuedEffectType.ColdRending, sword.ImbuedEffect);
            Assert.AreEqual("Frost Ono", sword.Name);
            CollectionAssert.Contains(ZoneLootMutator.GetMatchingRends(sword.W_DamageType), ImbuedEffectType.ColdRending);
        }

        [TestMethod]
        public void RetypeDrop_KeepsOtherImbues()
        {
            var sword = Melee(DamageType.Fire, "Flaming Ono", ImbuedEffectType.FireRending | ImbuedEffectType.ArmorRending);
            GearEssences.RetypeDrop(sword, DamageType.Acid);
            Assert.AreEqual(ImbuedEffectType.AcidRending | ImbuedEffectType.ArmorRending, sword.ImbuedEffect);
        }

        [TestMethod]
        public void DropRename_FollowsTheElement()
        {
            const GearEssences.NameStyle melee = GearEssences.NameStyle.Melee;
            Assert.AreEqual("Flaming Ono", GearEssences.DropRename("Ono", DamageType.Fire, melee), "physical going elemental gains its word");
            Assert.AreEqual("Ono", GearEssences.DropRename("Frost Ono", DamageType.Slash, melee), "elemental going physical loses it");
            Assert.AreEqual("Acid Ono", GearEssences.DropRename("Frost Ono", DamageType.Acid, melee));
            Assert.IsNull(GearEssences.DropRename("Ono", DamageType.Slash, melee), "physical stays nameless");
            Assert.IsNull(GearEssences.DropRename("Frost Ono", DamageType.Cold, melee), "already right");
            Assert.IsNull(GearEssences.DropRename("Atlan Axe of Black Fire", DamageType.Cold, melee), "states an element later on");
        }

        // -- the setting and the wiring ------------------------------------------------------------------

        [TestMethod]
        public void Share_IsAppended_AndIsNotAWeaponCard()
        {
            Assert.AreEqual(ZoneStat.LootWeightVoid, ZoneStat.All[Array.IndexOf(ZoneStat.All, ZoneStat.SlayerPoolShare) - 1], "appended after the previous tail");
            Assert.IsFalse(ZoneStat.WeaponCardChances.Contains(ZoneStat.SlayerPoolShare), "a weapon_*_chance name would make it a card");
        }

        private static string Source(params string[] parts) => TestSource.Read(parts);

        [TestMethod]
        public void RareElement_RunsBeforeAnythingReadsTheDamageType()
        {
            var src = Source("ACE.Server", "WorldObjects", "Creature_Death.cs");
            var start = src.IndexOf("internal static void ApplyZoneDropSweep", StringComparison.Ordinal);
            var element = src.IndexOf("ZoneRare.ApplyWeakestElement(wo, killed)", start, StringComparison.Ordinal);
            var gear = src.IndexOf("LootGenerationFactory.ApplyZoneGearStats(", start, StringComparison.Ordinal);
            var mutate = src.IndexOf("ZoneLootMutator.MutateLootItem(", start, StringComparison.Ordinal);
            Assert.IsTrue(element > 0 && element < gear && gear < mutate, "ApplyZoneSpells and the card rolls read W_DamageType");
        }

        [TestMethod]
        public void Record_IsCalledOnlyFromTheWorldSpawnPaths()
        {
            StringAssert.Contains(Source("ACE.Server", "Entity", "GeneratorProfile.cs"), "ZoneSlayerPool.RecordGenerated(");
            StringAssert.Contains(Source("ACE.Server", "Factories", "WorldObjectFactory.cs"), "ZoneSlayerPool.RecordPlaced(");
            Assert.IsFalse(Source("ACE.Server", "Managers", "ZoneControl", "ZoneSpawnScaler.cs").Contains("ZoneSlayerPool.Record"),
                "ApplyToSpawn is also called by admin spawns and the Zone Control preview");
        }

        [TestMethod]
        public void GeneratorRecord_ComesAfterTheSpawn()
        {
            var src = Source("ACE.Server", "Entity", "GeneratorProfile.cs");
            Assert.IsTrue(src.IndexOf("success = Spawn_Default(obj)", StringComparison.Ordinal) < src.IndexOf("ZoneSlayerPool.RecordGenerated(", StringComparison.Ordinal),
                "FindZoneRef walks the generator link for exempt generators, and the link is not set until the spawn");
        }


        [TestMethod]
        public void SlayerStamp_IsNotMatchedToTheWeapon_AndRaresKeepTheKill()
        {
            // owner ruling 2026-10-30: only rares are matched. A normal drop's species must not look at the element or rend.
            var src = Source("ACE.Server", "Managers", "ZoneControl", "ZoneLootMutator.cs");
            StringAssert.Contains(src, "ZoneSlayerPool.PickFor(killed, p, lootTier)");
            StringAssert.Contains(src, "wo.SlayerCreatureType = isRare");
        }
    }
}
