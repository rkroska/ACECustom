using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// T11+ rares, the item half (ZoneRareItems.cs): which cards and lines a Pristine item carries, which drops can become
    /// one, the name prefix and the flag's property id. The pure rules only - no DB, no world. The item paths themselves
    /// are covered in game with `/zcrare pristine`.
    /// </summary>
    [TestClass]
    public class ZoneRareItemTests
    {
        private const int Uncapped = int.MaxValue;

        private static bool[] All(bool value) => Enumerable.Repeat(value, ZoneRare.CardCount).ToArray();

        private static int Count(bool[] cards) => cards.Count(c => c);

        /// <summary>A deterministic stand-in for the random picker: walks the pool by a fixed stride.</summary>
        private static Func<int, int> Stride(int seed)
        {
            var i = seed;
            return n => (i++ * 7 + 3) % n;
        }

        private static bool[] Melee(bool rend = true, bool slayer = true, bool procs = true)
            => ZoneRare.WeaponCardEligibility(true, false, false, rend, slayer, procs);

        private static bool[] Missile(bool rend = true, bool slayer = true, bool procs = true)
            => ZoneRare.WeaponCardEligibility(false, true, false, rend, slayer, procs);

        private static bool[] Caster(bool rend = true, bool slayer = true, bool procs = true)
            => ZoneRare.WeaponCardEligibility(false, false, true, rend, slayer, procs);

        // -- weapon card eligibility --

        [TestMethod]
        public void Eligibility_TenCards_InTheMutatorsOrder()
        {
            // ZoneLootMutator.TrySpecialRolls writes its won / chance arrays positionally in this order
            Assert.AreEqual(10, ZoneRare.CardCount);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, new[]
            {
                ZoneRare.CardRend, ZoneRare.CardSlayer, ZoneRare.CardBite, ZoneRare.CardCrush, ZoneRare.CardArmorRend,
                ZoneRare.CardShieldCleave, ZoneRare.CardCleave, ZoneRare.CardSplit, ZoneRare.CardProcArc, ZoneRare.CardProcRing,
            });
        }

        [TestMethod]
        public void Eligibility_MeleeMissileCaster()
        {
            var melee = Melee();
            Assert.IsTrue(melee[ZoneRare.CardArmorRend] && melee[ZoneRare.CardShieldCleave] && melee[ZoneRare.CardCleave]);
            Assert.IsFalse(melee[ZoneRare.CardSplit], "a melee weapon has no arrows to split");

            var missile = Missile();
            Assert.IsTrue(missile[ZoneRare.CardArmorRend] && missile[ZoneRare.CardShieldCleave] && missile[ZoneRare.CardSplit]);
            Assert.IsFalse(missile[ZoneRare.CardCleave]);

            var caster = Caster();
            Assert.IsFalse(caster[ZoneRare.CardArmorRend], "armor rend does nothing for magic");
            Assert.IsFalse(caster[ZoneRare.CardShieldCleave], "shields do not block magic");
            Assert.IsFalse(caster[ZoneRare.CardCleave] || caster[ZoneRare.CardSplit]);
            Assert.IsTrue(caster[ZoneRare.CardRend] && caster[ZoneRare.CardSlayer] && caster[ZoneRare.CardBite] && caster[ZoneRare.CardCrush]);
            Assert.IsTrue(caster[ZoneRare.CardProcArc] && caster[ZoneRare.CardProcRing]);
        }

        [TestMethod]
        public void Eligibility_NoElementNoSlayerTypeNoProcSlot()
        {
            var bow = Missile(rend: false, slayer: false, procs: false);   // a plain bow: its element comes from the ammo
            Assert.IsFalse(bow[ZoneRare.CardRend] || bow[ZoneRare.CardSlayer] || bow[ZoneRare.CardProcArc] || bow[ZoneRare.CardProcRing]);
            Assert.IsTrue(bow[ZoneRare.CardArmorRend], "armor rend needs no element");

            Assert.AreEqual(0, Count(ZoneRare.WeaponCardEligibility(false, false, false, true, true, true)), "not a weapon = no cards");
        }

        // -- weapon card selection --

        [TestMethod]
        public void Cards_CoreIsForced_EvenWhenNoCardCanDropAndTheCapIsBelowIt()
        {
            // nothing enabled, cap 0: the core still lands in full and nothing else does
            var cards = ZoneRare.PickWeaponCards(Melee(), All(false), 0, Stride(1));
            Assert.IsTrue(cards[ZoneRare.CardRend] && cards[ZoneRare.CardSlayer] && cards[ZoneRare.CardArmorRend]);
            Assert.AreEqual(3, Count(cards));
        }

        [TestMethod]
        public void Cards_CoreSkipsWhatTheWeaponCannotCarry()
        {
            var noSlayer = ZoneRare.PickWeaponCards(Melee(slayer: false), All(false), 0, Stride(1));
            Assert.IsTrue(noSlayer[ZoneRare.CardRend] && noSlayer[ZoneRare.CardArmorRend]);
            Assert.IsFalse(noSlayer[ZoneRare.CardSlayer]);

            var noElement = ZoneRare.PickWeaponCards(Missile(rend: false, procs: false), All(false), 0, Stride(1));
            Assert.IsFalse(noElement[ZoneRare.CardRend], "no element = no rend, forced or not");
            Assert.IsTrue(noElement[ZoneRare.CardSlayer] && noElement[ZoneRare.CardArmorRend]);
        }

        [TestMethod]
        public void Cards_T11Melee_CapThree_IsExactlyTheCore()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var cards = ZoneRare.PickWeaponCards(Melee(), All(true), 3, Stride(seed));
                Assert.AreEqual(3, Count(cards));
                Assert.IsTrue(cards[ZoneRare.CardRend] && cards[ZoneRare.CardSlayer] && cards[ZoneRare.CardArmorRend]);
            }
        }

        [TestMethod]
        public void Cards_CasterNeverGetsArmorRendOrShieldCleave_AndFillsFromItsProcs()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var cards = ZoneRare.PickWeaponCards(Caster(), All(true), 3, Stride(seed));
                Assert.IsFalse(cards[ZoneRare.CardArmorRend]);
                Assert.IsFalse(cards[ZoneRare.CardShieldCleave]);
                Assert.IsTrue(cards[ZoneRare.CardRend] && cards[ZoneRare.CardSlayer]);
                Assert.AreEqual(3, Count(cards), "two core cards + one extra");
                Assert.IsTrue(cards[ZoneRare.CardProcArc] ^ cards[ZoneRare.CardProcRing], "the one extra is one of the two Cast on Strike slots");
            }
        }

        [TestMethod]
        public void Cards_BiteAndCrushAreNeverChosen_WhileAnyOtherCardIsLeft()
        {
            // every cap from the core count up to "one short of every other card": Bite and Crush stay off
            foreach (var eligible in new[] { Melee(), Missile(), Caster() })
            {
                var others = Count(eligible) - 2;   // everything the weapon can carry besides Bite and Crush
                for (var cap = 0; cap <= others; cap++)
                    for (var seed = 0; seed < 30; seed++)
                    {
                        var cards = ZoneRare.PickWeaponCards(eligible, All(true), cap, Stride(seed));
                        Assert.IsFalse(cards[ZoneRare.CardBite], $"cap {cap}: Biting Strike before the other cards ran out");
                        Assert.IsFalse(cards[ZoneRare.CardCrush], $"cap {cap}: Crushing Blow before the other cards ran out");
                    }
            }
        }

        [TestMethod]
        public void Cards_T11Melee_CapFive_NeverGetsBiteOrCrush()
        {
            // live tuning: weapon_modifier_cap 5 at T11. Core 3 + two of Shield Cleave / Cleave / Arc / Ring - four are on offer
            for (var seed = 0; seed < 100; seed++)
            {
                var cards = ZoneRare.PickWeaponCards(Melee(), All(true), 5, Stride(seed));
                Assert.AreEqual(5, Count(cards));
                Assert.IsFalse(cards[ZoneRare.CardBite] || cards[ZoneRare.CardCrush]);
            }
        }

        [TestMethod]
        public void Cards_T14Caster_CapSix_FillsWithCrushThenBite()
        {
            // the owner's wand: a caster can only carry Rending, Slayer and the two procs besides the fillers
            for (var seed = 0; seed < 50; seed++)
            {
                var cards = ZoneRare.PickWeaponCards(Caster(), All(true), 6, Stride(seed));
                Assert.AreEqual(6, Count(cards), "6 of 6");
                foreach (var card in new[] { ZoneRare.CardRend, ZoneRare.CardSlayer, ZoneRare.CardProcArc, ZoneRare.CardProcRing, ZoneRare.CardCrush, ZoneRare.CardBite })
                    Assert.IsTrue(cards[card]);
            }

            // one slot left after the four: Crushing Blow goes first
            var five = ZoneRare.PickWeaponCards(Caster(), All(true), 5, Stride(0));
            Assert.IsTrue(five[ZoneRare.CardCrush]);
            Assert.IsFalse(five[ZoneRare.CardBite]);
            Assert.AreEqual(5, Count(five));

            // no creature type for the Slayer (the dev mint without a target): the fillers still close the gap
            var noSlayer = ZoneRare.PickWeaponCards(Caster(slayer: false), All(true), 6, Stride(0));
            Assert.AreEqual(5, Count(noSlayer), "Rend, Arc, Ring, Crush, Bite - all a wand without a Slayer can hold");
        }

        [TestMethod]
        public void Cards_Fillers_RespectEligibilityTheToggleAndTheCap()
        {
            // switched off at this tier = not a filler either
            var enabled = All(true);
            enabled[ZoneRare.CardCrush] = false;
            var noCrush = ZoneRare.PickWeaponCards(Caster(), enabled, Uncapped, Stride(0));
            Assert.IsFalse(noCrush[ZoneRare.CardCrush]);
            Assert.IsTrue(noCrush[ZoneRare.CardBite], "the other filler still fills");
            Assert.AreEqual(5, Count(noCrush));

            // nothing enabled: the forced core only, as before
            Assert.AreEqual(2, Count(ZoneRare.PickWeaponCards(Caster(), All(false), Uncapped, Stride(0))));

            // uncapped melee: every other card first, then both fillers
            var melee = ZoneRare.PickWeaponCards(Melee(), All(true), Uncapped, Stride(0));
            Assert.AreEqual(9, Count(melee));
            Assert.IsFalse(melee[ZoneRare.CardSplit]);

            // a cap already met by the core leaves no room for a filler
            var capped = ZoneRare.PickWeaponCards(Caster(procs: false), All(true), 2, Stride(0));
            Assert.AreEqual(2, Count(capped));
            Assert.IsFalse(capped[ZoneRare.CardCrush] || capped[ZoneRare.CardBite]);
        }

        [TestMethod]
        public void WeaponRatingTop_IsTheBandTop_UnderTheT25Ceiling()
        {
            Assert.AreEqual(69, ZoneRare.WeaponRatingTop(14, 69, 138));
            Assert.AreEqual(84, ZoneRare.WeaponRatingTop(17, 84, 138), "T14");
            Assert.AreEqual(138, ZoneRare.WeaponRatingTop(28, 138, 138), "T25");
            Assert.AreEqual(138, ZoneRare.WeaponRatingTop(28, 900, 138), "a mistyped band stops at the line's T25 ceiling");
            Assert.AreEqual(69, ZoneRare.WeaponRatingTop(69, 14, 138), "a reversed band still gives its top");
            Assert.AreEqual(0, ZoneRare.WeaponRatingTop(0, 0, 138), "no band = nothing to add");
        }

        [TestMethod]
        public void Cards_Uncapped_TakesEveryEligibleEnabledExtra()
        {
            var melee = ZoneRare.PickWeaponCards(Melee(), All(true), Uncapped, Stride(3));
            // core 3 + Shield Cleave, Cleave, both procs, then the two last-resort fillers; never Split (missile)
            Assert.AreEqual(9, Count(melee));
            Assert.IsFalse(melee[ZoneRare.CardSplit]);

            var missile = ZoneRare.PickWeaponCards(Missile(), All(true), Uncapped, Stride(3));
            Assert.AreEqual(9, Count(missile));
            Assert.IsTrue(missile[ZoneRare.CardSplit]);
            Assert.IsFalse(missile[ZoneRare.CardCleave]);
        }

        [TestMethod]
        public void Cards_CapIsRespected_AtEveryCount()
        {
            for (var cap = 3; cap <= 9; cap++)
                for (var seed = 0; seed < 30; seed++)
                    Assert.AreEqual(cap, Count(ZoneRare.PickWeaponCards(Melee(), All(true), cap, Stride(seed))), $"cap {cap}");

            // a cap above what the weapon can carry stops at what it can carry (7 cards + the two fillers), not at the cap
            Assert.AreEqual(9, Count(ZoneRare.PickWeaponCards(Melee(), All(true), 10, Stride(0))));
        }

        [TestMethod]
        public void Cards_ToggledOffCardIsNeverAnExtra()
        {
            // Split Arrows is off in the live config: enabled[] false for it, whatever the weapon
            var enabled = All(true);
            enabled[ZoneRare.CardSplit] = false;
            for (var seed = 0; seed < 50; seed++)
            {
                var cards = ZoneRare.PickWeaponCards(Missile(), enabled, Uncapped, Stride(seed));
                Assert.IsFalse(cards[ZoneRare.CardSplit]);
                Assert.AreEqual(8, Count(cards));   // core 3 + Shield Cleave + both procs, then Crush and Bite - never Split
            }

            // null = nothing is known to be enabled: the core only
            Assert.AreEqual(3, Count(ZoneRare.PickWeaponCards(Melee(), null, Uncapped, Stride(0))));
        }

        [TestMethod]
        public void Cards_ExtrasAreRandom_EveryEligibleExtraIsReachable()
        {
            var seen = new HashSet<int>();
            for (var seed = 0; seed < 200; seed++)
            {
                var cards = ZoneRare.PickWeaponCards(Melee(), All(true), 4, Stride(seed));
                for (var i = 0; i < cards.Length; i++)
                    if (cards[i]) seen.Add(i);
            }
            CollectionAssert.AreEquivalent(new[]
            {
                ZoneRare.CardRend, ZoneRare.CardSlayer, ZoneRare.CardArmorRend,
                ZoneRare.CardShieldCleave, ZoneRare.CardCleave, ZoneRare.CardProcArc, ZoneRare.CardProcRing,
            }, seen.ToList());
        }

        [TestMethod]
        public void Cards_SmallPoolAndBadPickers_NeverLoopOrThrow()
        {
            // one extra available, a huge cap: ends when the pool is empty
            var enabled = All(false);
            enabled[ZoneRare.CardCleave] = true;
            var cards = ZoneRare.PickWeaponCards(Melee(), enabled, Uncapped, Stride(0));
            Assert.AreEqual(4, Count(cards));
            Assert.IsTrue(cards[ZoneRare.CardCleave]);

            // pickers that answer out of range, and no picker at all, still terminate with a legal set
            foreach (var picker in new Func<int, int>[] { n => -1, n => n, n => int.MaxValue, null })
            {
                var odd = ZoneRare.PickWeaponCards(Melee(), All(true), 5, picker);
                Assert.AreEqual(5, Count(odd));
                Assert.IsFalse(odd[ZoneRare.CardBite] || odd[ZoneRare.CardCrush] || odd[ZoneRare.CardSplit]);
            }

            // malformed input never throws
            Assert.AreEqual(0, Count(ZoneRare.PickWeaponCards(null, All(true), 3, Stride(0))));
            Assert.AreEqual(0, Count(ZoneRare.PickWeaponCards(new bool[3], All(true), 3, Stride(0))));
            Assert.AreEqual(3, Count(ZoneRare.PickWeaponCards(Melee(), new bool[2], Uncapped, Stride(0))), "a short enabled[] enables nothing past its end");
        }

        // -- armour / jewelry extra lines --

        /// <summary>A repeatable stream of draws in [0, 1).</summary>
        private static Func<double> Draws(int seed)
        {
            var rng = new Random(seed);
            return () => rng.NextDouble();
        }

        [TestMethod]
        public void Lines_ExactlyTheCap_WhenEnoughLinesAreAvailable()
        {
            var weights = new[] { 0.3, 0.3, 0.3, 0.05, 0.05, 0.0000228, 0.0000228 };
            for (var cap = 1; cap <= 6; cap++)
                for (var seed = 0; seed < 100; seed++)
                {
                    var picked = ZoneRare.PickExtraLines(weights, cap, Draws(seed));
                    Assert.AreEqual(cap, picked.Count, $"cap {cap}: a Pristine piece carries exactly the tier's maximum, never one short");
                    Assert.AreEqual(cap, picked.Distinct().Count(), "each line at most once");
                    Assert.IsTrue(picked.All(i => i >= 0 && i < weights.Length), "only lines that were offered");
                }
        }

        [TestMethod]
        public void Lines_FewerThanTheCap_OnlyWhenThePieceHasFewerLines()
        {
            var picked = ZoneRare.PickExtraLines(new[] { 0.3, 0.0, 0.1 }, 5, Draws(1));
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, picked);
        }

        [TestMethod]
        public void Lines_ZeroChanceLineIsNeverChosen()
        {
            // index 1 is switched off / unauthored (weight 0); NaN, negative and infinite weights count as off too
            var weights = new[] { 0.3, 0.0, 0.2, double.NaN, -1.0, double.PositiveInfinity, 0.0000228 };
            for (var seed = 0; seed < 300; seed++)
            {
                var picked = ZoneRare.PickExtraLines(weights, 3, Draws(seed));
                Assert.AreEqual(3, picked.Count);
                CollectionAssert.IsSubsetOf(picked, new[] { 0, 2, 6 });
            }
        }

        [TestMethod]
        public void Lines_WeightsAreRespected_HeavyLinesWinFarMoreOften()
        {
            // one slot, a 0.3 filler against a 0.0000228 chase line: the chase line stays rare
            var weights = new[] { 0.3, 0.0000228 };
            var draw = Draws(20261008);   // one stream for the whole test, so the 2000 draws are independent
            var chase = 0;
            for (var n = 0; n < 2000; n++)
                if (ZoneRare.PickExtraLines(weights, 1, draw)[0] == 1)
                    chase++;
            Assert.IsTrue(chase <= 5, $"chase line picked {chase} times in 2000 - it must stay rare");

            // equal weights: both are picked, and often
            var first = 0;
            for (var n = 0; n < 2000; n++)
                if (ZoneRare.PickExtraLines(new[] { 0.5, 0.5 }, 1, draw)[0] == 0)
                    first++;
            Assert.IsTrue(first > 800 && first < 1200, $"equal weights split {first} / {2000 - first}");
        }

        [TestMethod]
        public void Lines_EdgeDrawsAndBadInput()
        {
            var weights = new[] { 0.3, 0.3, 0.3 };
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, ZoneRare.PickExtraLines(weights, 3, () => 0.0), "a draw of 0 takes the first line left");
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, ZoneRare.PickExtraLines(weights, 3, () => 1.0), "a draw of 1 (out of range) takes the last, never throws");
            Assert.AreEqual(3, ZoneRare.PickExtraLines(weights, 3, null).Count);

            Assert.AreEqual(0, ZoneRare.PickExtraLines(weights, 0, Draws(1)).Count);
            Assert.AreEqual(0, ZoneRare.PickExtraLines(weights, -2, Draws(1)).Count);
            Assert.AreEqual(0, ZoneRare.PickExtraLines(null, 3, Draws(1)).Count);
            Assert.AreEqual(0, ZoneRare.PickExtraLines(new double[0], 3, Draws(1)).Count);
            Assert.AreEqual(0, ZoneRare.PickExtraLines(new[] { 0.0, 0.0 }, 3, Draws(1)).Count, "nothing can drop = nothing is forced on");
        }

        // -- built-in Reinforced, item spells (owner 2026-10-10) --

        [TestMethod]
        public void Reinforced_IsBuiltInOnARare_AndUsesNoSlotThere()
        {
            Assert.IsTrue(ZoneRare.IsBuiltInOnRare(ZoneModifiers.ReinforcedKey));
            foreach (var key in new[] { 19, 25, 28, 29, 31, 32, 33, 43, 47, 48, 50, 51, 52, 53, 54 })
                Assert.IsFalse(ZoneRare.IsBuiltInOnRare(key), $"line {key} is still a drawn line");

            Assert.IsTrue(ZoneRare.ReinforcedUsesASlot(ZoneRareTier.None), "an ordinary piece counts Reinforced, as before");
            Assert.IsFalse(ZoneRare.ReinforcedUsesASlot(ZoneRareTier.Pristine));
            Assert.IsFalse(ZoneRare.ReinforcedUsesASlot(ZoneRareTier.Ascendant));
        }

        [TestMethod]
        public void Lines_WithReinforcedTakenOut_TheCapIsStillFilledWithOtherLines()
        {
            // the live armor pool: seven common lines, Reinforced (weight 0 on a rare) and two chase lines
            var keys = new[] { 19, 25, 28, 29, 31, 32, 43, 49, 33, 47 };
            var weights = new[] { 0.2, 0.3, 0.2, 0.2, 0.2, 0.3, 0.2, 0.0, 0.0000228, 0.0000228 };
            foreach (var cap in new[] { 3, 4, 8 })
                for (var seed = 0; seed < 100; seed++)
                {
                    var picked = ZoneRare.PickExtraLines(weights, cap, Draws(seed)).Select(i => keys[i]).ToList();
                    Assert.AreEqual(cap, picked.Count, $"cap {cap}: exactly the cap, without Reinforced");
                    CollectionAssert.DoesNotContain(picked, ZoneModifiers.ReinforcedKey);
                }

            // T25, cap 8: all seven common lines and one of the two chase lines
            var top = ZoneRare.PickExtraLines(weights, 8, Draws(7)).Select(i => keys[i]).ToList();
            foreach (var common in new[] { 19, 25, 28, 29, 31, 32, 43 })
                CollectionAssert.Contains(top, common);
            Assert.AreEqual(1, top.Count(k => k == 33 || k == 47));
        }

        [TestMethod]
        public void ItemSpells_ARareTakesTheMaximumCountAndLevel_WithoutADraw()
        {
            Func<int, int, int> mustNotRoll = (lo, hi) => throw new InvalidOperationException("a rare makes no draw here");
            Assert.AreEqual(3, ACE.Server.Factories.LootGenerationFactory.ZoneSpellCount(1, 3, true, mustNotRoll));
            Assert.AreEqual(4, ACE.Server.Factories.LootGenerationFactory.ZoneSpellLevel(2, 4, true, mustNotRoll));
            Assert.AreEqual(0, ACE.Server.Factories.LootGenerationFactory.ZoneSpellCount(0, 0, true, mustNotRoll), "a tier that gives no spells gives a rare none");

            // an ordinary drop rolls between the two, as before
            Assert.AreEqual(2, ACE.Server.Factories.LootGenerationFactory.ZoneSpellCount(1, 3, false, (lo, hi) => { Assert.AreEqual(1, lo); Assert.AreEqual(3, hi); return 2; }));
            Assert.AreEqual(3, ACE.Server.Factories.LootGenerationFactory.ZoneSpellLevel(2, 4, false, (lo, hi) => 3));
        }

        // -- which drops can be a rare --

        [TestMethod]
        public void Eligible_ZoneGearOnly()
        {
            // weapons: melee, launcher, caster (the caller passes isWeapon for those three classes)
            Assert.IsTrue(ZoneRare.IsEligibleKind(true, ItemType.MeleeWeapon, WeenieType.MeleeWeapon, true, 0));
            Assert.IsTrue(ZoneRare.IsEligibleKind(true, ItemType.MissileWeapon, WeenieType.MissileLauncher, true, 0));
            Assert.IsTrue(ZoneRare.IsEligibleKind(true, ItemType.Caster, WeenieType.Caster, true, 0));

            // armor (shields are ItemType Armor), clothing (cloaks are ItemType Clothing), jewelry
            Assert.IsTrue(ZoneRare.IsEligibleKind(false, ItemType.Armor, WeenieType.Clothing, true, 0));
            Assert.IsTrue(ZoneRare.IsEligibleKind(false, ItemType.Armor, WeenieType.Generic, true, 0));
            Assert.IsTrue(ZoneRare.IsEligibleKind(false, ItemType.Clothing, WeenieType.Clothing, true, 0));
            Assert.IsTrue(ZoneRare.IsEligibleKind(false, ItemType.Jewelry, WeenieType.Generic, true, 0));
            Assert.IsTrue(ZoneRare.IsEligibleKind(false, ItemType.Jewelry, WeenieType.Generic, true, 1), "a max stack of 1 is not a stack");
        }

        [TestMethod]
        public void Eligible_NeverCoinsStacksOrUnwearables()
        {
            Assert.IsFalse(ZoneRare.IsEligibleKind(false, ItemType.Money, WeenieType.Coin, false, 25000), "coins");
            Assert.IsFalse(ZoneRare.IsEligibleKind(true, ItemType.Money, WeenieType.Coin, true, 0), "a coin is never gear, whatever else is claimed");
            Assert.IsFalse(ZoneRare.IsEligibleKind(false, ItemType.MissileWeapon, WeenieType.Ammunition, true, 250), "arrows");
            Assert.IsFalse(ZoneRare.IsEligibleKind(false, ItemType.TinkeringMaterial, WeenieType.CraftTool, false, 0), "salvage bags");
            Assert.IsFalse(ZoneRare.IsEligibleKind(false, ItemType.Gem, WeenieType.Gem, false, 0), "gems");
            Assert.IsFalse(ZoneRare.IsEligibleKind(false, ItemType.Jewelry, WeenieType.Generic, false, 0), "jewelry that cannot be worn");
            Assert.IsFalse(ZoneRare.IsEligibleKind(false, ItemType.Jewelry, WeenieType.Generic, true, 10), "anything that stacks");
            Assert.IsFalse(ZoneRare.IsEligibleKind(false, ItemType.Misc, WeenieType.Generic, true, 0), "wearable but not armor / clothing / jewelry");
        }

        // -- name + appraisal + flag --

        [TestMethod]
        public void Name_PrefixedOnce_NeverTwice()
        {
            Assert.AreEqual("Pristine Katar", ZoneRare.PrefixName("Katar", ZoneRareTier.Pristine));
            Assert.AreEqual("Pristine Katar", ZoneRare.PrefixName(ZoneRare.PrefixName("Katar", ZoneRareTier.Pristine), ZoneRareTier.Pristine));
            Assert.AreEqual("pristine katar", ZoneRare.PrefixName("pristine katar", ZoneRareTier.Pristine), "already prefixed, whatever the case");
            Assert.AreEqual("Pristine", ZoneRare.PrefixName("Pristine", ZoneRareTier.Pristine));
            Assert.AreEqual("Pristine Pristineblade", ZoneRare.PrefixName("Pristineblade", ZoneRareTier.Pristine), "a word that merely starts the same is not the prefix");
            Assert.AreEqual("Pristine Katar", ZoneRare.PrefixName("  Katar ", ZoneRareTier.Pristine));
            Assert.AreEqual("Ascendant Katar", ZoneRare.PrefixName("Katar", ZoneRareTier.Ascendant));
        }

        [TestMethod]
        public void Name_Ascendant_PrefixedOnce_NeverTwice()
        {
            Assert.AreEqual("Ascendant Yumi", ZoneRare.PrefixName(ZoneRare.PrefixName("Yumi", ZoneRareTier.Ascendant), ZoneRareTier.Ascendant));
            Assert.AreEqual("ascendant yumi", ZoneRare.PrefixName("ascendant yumi", ZoneRareTier.Ascendant));
            Assert.AreEqual("Ascendant", ZoneRare.PrefixName("Ascendant", ZoneRareTier.Ascendant));
            Assert.AreEqual("Ascendant Ascendantbow", ZoneRare.PrefixName("Ascendantbow", ZoneRareTier.Ascendant));
            // an item is one tier or the other; the prefix of one is just a name to the other
            Assert.AreEqual("Ascendant Pristine Yumi", ZoneRare.PrefixName("Pristine Yumi", ZoneRareTier.Ascendant));
        }

        [TestMethod]
        public void Appraisal_Ascendant_ShowsItemTierAndTheTierItWasFoundIn()
        {
            Assert.AreEqual("- Ascendant: every roll is at the maximum for Tier 14 (found in Tier 11)", ZoneRare.AppraisalText(ZoneRareTier.Ascendant, 14, 11));
            Assert.AreEqual("- Ascendant: every roll is at the maximum for Tier 25 (found in Tier 22)", ZoneRare.AppraisalText(ZoneRareTier.Ascendant, 25, 22));
            Assert.AreEqual("- Ascendant: every roll is at the maximum for Tier 25 (found in Tier 25)", ZoneRare.AppraisalText(ZoneRareTier.Ascendant, 25, 25));
            Assert.AreEqual("- Ascendant: every roll is at the maximum for Tier 14", ZoneRare.AppraisalText(ZoneRareTier.Ascendant, 14), "no stored drop tier = no claim about it");
            Assert.AreEqual("- Ascendant: every roll is at the maximum for Tier 14", ZoneRare.AppraisalText(ZoneRareTier.Ascendant, 14, 7), "a drop tier off the ladder is not shown");

            // the Pristine line is unchanged, and never grows a "found in" tail
            Assert.AreEqual("- Pristine: every roll is at the maximum for Tier 11", ZoneRare.AppraisalText(ZoneRareTier.Pristine, 11));
            Assert.AreEqual("- Pristine: every roll is at the maximum for Tier 11", ZoneRare.AppraisalText(ZoneRareTier.Pristine, 11, 11));
        }

        [TestMethod]
        public void Refusals_NameTheItemsOwnTier()
        {
            Assert.AreEqual("Your Yumi is Pristine. Bags do not work on it.", ZoneRare.BagRefusalText("Yumi", ZoneRareTier.Pristine));
            Assert.AreEqual("Your Yumi is Ascendant. Bags do not work on it.", ZoneRare.BagRefusalText("Yumi", ZoneRareTier.Ascendant));
            Assert.AreEqual("This item is Pristine and cannot be imbued. Ordinary tinkering still works.", ZoneRare.ImbueRefusalText(ZoneRareTier.Pristine));
            Assert.AreEqual("This item is Ascendant and cannot be imbued. Ordinary tinkering still works.", ZoneRare.ImbueRefusalText(ZoneRareTier.Ascendant));

            foreach (var text in new[]
            {
                ZoneRare.BagRefusalText("Yumi", ZoneRareTier.Ascendant), ZoneRare.ImbueRefusalText(ZoneRareTier.Ascendant),
                ZoneRare.BagRefusalText("Yumi", ZoneRareTier.None), ZoneRare.ImbueRefusalText(ZoneRareTier.None),
                ZoneRare.AppraisalText(ZoneRareTier.Ascendant, 14, 11),
            })
                Assert.IsTrue(text.All(c => c >= 0x20 && c < 0x7F), $"not plain ASCII: {text}");
        }

        // -- the launcher / caster aug floor of an item wielded below its tier --
        //
        // THE RULE (WeaponScalingCombat.WieldFloorAugs). A launcher's or caster's tier steps are counted from
        // max(floor, holder's item augs), and the floor is "what the wield gate guarantees". For an ordinary weapon that
        // is its own tier's MinWieldAugs. An Ascendant weapon is wielded with the requirements of the tier it DROPPED in,
        // so its floor is THAT tier's MinWieldAugs - never its own tier's. The steps are still counted against its own
        // tier row (its cap), so the holder gets exactly the steps their real augs unlock: no more than a weapon of the
        // drop tier while they are a drop-tier character, and more only as their own augs grow into the higher cap.

        private static readonly WeaponScalingConfig Cfg = WeaponScalingManager.BuildDefaults();

        private static WeaponScalingTier Row(int tier) => Cfg.Tiers.Single(t => t.Tier == tier);

        [TestMethod]
        public void Floor_OrdinaryWeapon_IsItsOwnTiersMinimum_Unchanged()
        {
            for (var tier = 11; tier <= 25; tier++)
            {
                var row = Row(tier);
                Assert.AreEqual(row.MinWieldAugs, WeaponScalingCombat.WieldFloorAugs(row, null), $"T{tier}");
                // and so every step count is what LauncherTierSteps on the old floored count gave
                foreach (var augs in new long[] { 0, 2000, 2500, 3500, 4000, 9999 })
                    Assert.AreEqual(
                        WeaponScalingCombat.LauncherTierSteps(Cfg, row, System.Math.Max(row.MinWieldAugs, augs)),
                        WeaponScalingCombat.ScaledModTierSteps(Cfg, row, null, augs), $"T{tier} at {augs}");
            }
        }

        [TestMethod]
        public void Floor_AscendantWeapon_IsTheDropTiersMinimum()
        {
            Assert.AreEqual(Row(11).MinWieldAugs, WeaponScalingCombat.WieldFloorAugs(Row(14), Row(11)));
            Assert.AreEqual(Row(15).MinWieldAugs, WeaponScalingCombat.WieldFloorAugs(Row(18), Row(15)));
            Assert.IsTrue(WeaponScalingCombat.WieldFloorAugs(Row(14), Row(11)) < Row(14).MinWieldAugs, "the point of the rule");

            // never above the weapon's own minimum, whatever rows are handed in
            Assert.AreEqual(Row(11).MinWieldAugs, WeaponScalingCombat.WieldFloorAugs(Row(11), Row(14)));
            // gated by a lower tier whose row is gone: nothing is guaranteed, the holder's real count rules
            Assert.AreEqual(0, WeaponScalingCombat.WieldFloorAugs(Row(14), null, gateRowMissing: true));
        }

        [TestMethod]
        public void Floor_T14BowFoundInT11_GivesAT11CharacterNoFreeTierSteps()
        {
            var t11Augs = Row(11).MinWieldAugs;   // a character who only just meets the T11 gate

            // the bug this closes: floored at its OWN tier minimum the T14 bow would count steps nobody earned
            Assert.IsTrue(WeaponScalingCombat.LauncherTierSteps(Cfg, Row(14), Row(14).MinWieldAugs) > 0);

            // with the drop tier's floor: exactly what an ordinary T11 bow gives that character - the baseline, no steps
            Assert.AreEqual(0, WeaponScalingCombat.ScaledModTierSteps(Cfg, Row(11), null, t11Augs));
            Assert.AreEqual(0, WeaponScalingCombat.ScaledModTierSteps(Cfg, Row(14), Row(11), t11Augs));

            // and the same on an unwielded examine (no holder = 0 augs): the honest minimum for any hands that can wield it
            Assert.AreEqual(0, WeaponScalingCombat.ScaledModTierSteps(Cfg, Row(14), Row(11), 0));
        }

        [TestMethod]
        public void Floor_AscendantWeapon_NeverBeatsTheBestOrdinaryWeaponTheHolderCouldFill()
        {
            // at every aug count, a T14-found-in-T11 bow gives the steps the holder's own augs unlock: never more than an
            // ordinary T14 bow in the same hands, never fewer than an ordinary T11 bow, and equal to T14 once they are T14
            var asc = Row(14);
            var gate = Row(11);
            foreach (var augs in new long[] { 0, 1000, 2000, 2499, 2500, 2999, 3000, 3499, 3500, 3999, 4000, 6000 })
            {
                var steps = WeaponScalingCombat.ScaledModTierSteps(Cfg, asc, gate, augs);
                Assert.IsTrue(steps <= WeaponScalingCombat.ScaledModTierSteps(Cfg, asc, null, augs), $"{augs}: above an ordinary T14 bow");
                Assert.IsTrue(steps >= WeaponScalingCombat.ScaledModTierSteps(Cfg, gate, null, augs), $"{augs}: below an ordinary T11 bow");
                Assert.AreEqual(WeaponScalingCombat.LauncherTierSteps(Cfg, asc, System.Math.Max(gate.MinWieldAugs, augs)), steps, $"{augs}");
                if (augs >= asc.MinWieldAugs)
                    Assert.AreEqual(WeaponScalingCombat.ScaledModTierSteps(Cfg, asc, null, augs), steps, $"{augs}: a real T14 character loses nothing");
            }

            // the steps grow with the holder, one tier's cap at a time
            Assert.AreEqual(1, WeaponScalingCombat.ScaledModTierSteps(Cfg, asc, gate, Row(12).Cap));
            Assert.AreEqual(2, WeaponScalingCombat.ScaledModTierSteps(Cfg, asc, gate, Row(13).Cap));
            Assert.AreEqual(3, WeaponScalingCombat.ScaledModTierSteps(Cfg, asc, gate, Row(14).Cap));
            Assert.AreEqual(3, WeaponScalingCombat.ScaledModTierSteps(Cfg, asc, gate, 99999), "and stop at the weapon's own cap");
        }

        [TestMethod]
        public void Name_NoneAndEmpty_AreLeftAlone()
        {
            Assert.AreEqual("Katar", ZoneRare.PrefixName("Katar", ZoneRareTier.None));
            Assert.IsNull(ZoneRare.PrefixName(null, ZoneRareTier.Pristine));
            Assert.AreEqual("", ZoneRare.PrefixName("", ZoneRareTier.Pristine));
            Assert.IsNull(ZoneRare.TierName(ZoneRareTier.None));
        }

        [TestMethod]
        public void ClientText_IsPlainAscii()
        {
            // the AC client draws anything outside 7-bit ASCII as garbage (CLAUDE.md hard rule)
            var texts = new[]
            {
                ZoneRare.AppraisalText(ZoneRareTier.Pristine, 11),
                ZoneRare.AppraisalText(ZoneRareTier.Ascendant, 25),
                ZoneRare.AppraisalText(ZoneRareTier.Pristine, 0),
                ZoneRare.PrefixName("Katar", ZoneRareTier.Pristine),
                ZoneRare.BroadcastText("Some Player", "Gold Pristine Ring"),
            };
            foreach (var text in texts)
            {
                Assert.IsFalse(string.IsNullOrEmpty(text));
                Assert.IsTrue(text.All(c => c >= 0x20 && c < 0x7F), $"not plain ASCII: {text}");
            }

            Assert.AreEqual("- Pristine: every roll is at the maximum for Tier 11", ZoneRare.AppraisalText(ZoneRareTier.Pristine, 11));
            Assert.AreEqual("Some Player has found the Gold Pristine Ring!", ZoneRare.BroadcastText("Some Player", "Gold Pristine Ring"));
            Assert.IsNull(ZoneRare.AppraisalText(ZoneRareTier.None, 11));
        }

        [TestMethod]
        public void ZcRare_PropertyId_IsFreeAndOutsideTheSummedBlock()
        {
            // parsed by name, so the id is read at run time (two compile-time constants trip analyzer MSTEST0025)
            var rareId = Convert.ToInt64(Enum.Parse(typeof(PropertyInt), nameof(PropertyInt.ZcRare)));
            Assert.AreEqual(51100L, rareId);

            // 50200-50399 is the Zone Control cantrip block, summed across worn pieces on equip - a flag there would be added up
            Assert.IsFalse(rareId >= ZoneModifiers.PropMin && rareId <= ZoneModifiers.PropMax);

            // no other PropertyInt shares the id (EnumCollisionTests covers every custom id; this one names the flag)
            var sameId = Enum.GetNames(typeof(PropertyInt)).Where(n => Convert.ToInt64(Enum.Parse(typeof(PropertyInt), n)) == 51100).ToList();
            CollectionAssert.AreEqual(new[] { nameof(PropertyInt.ZcRare) }, sameId);

            // the Ascendant drop tier sits beside it, under the same two rules
            var gateId = Convert.ToInt64(Enum.Parse(typeof(PropertyInt), nameof(PropertyInt.ZcRareGateTier)));
            Assert.AreEqual(51101L, gateId);
            Assert.IsFalse(gateId >= ZoneModifiers.PropMin && gateId <= ZoneModifiers.PropMax);
            var sameGateId = Enum.GetNames(typeof(PropertyInt)).Where(n => Convert.ToInt64(Enum.Parse(typeof(PropertyInt), n)) == 51101).ToList();
            CollectionAssert.AreEqual(new[] { nameof(PropertyInt.ZcRareGateTier) }, sameGateId);

            // the stored value is the tier enum, and 0 must mean "not a rare"
            Assert.AreEqual(0, (int)ZoneRareTier.None);
            Assert.AreEqual(1, (int)ZoneRareTier.Pristine);
            Assert.AreEqual(2, (int)ZoneRareTier.Ascendant);
        }

        [TestMethod]
        public void IconUnderlay_EachTierHasItsOwn()
        {
            Assert.AreEqual(ZoneRare.PristineIconUnderlay, ZoneRare.IconUnderlayFor(ZoneRareTier.Pristine));
            Assert.AreEqual(ZoneRare.AscendantIconUnderlay, ZoneRare.IconUnderlayFor(ZoneRareTier.Ascendant));
            Assert.IsNull(ZoneRare.IconUnderlayFor(ZoneRareTier.None), "an ordinary item keeps its own background");
            Assert.AreNotEqual(ZoneRare.IconUnderlayFor(ZoneRareTier.Pristine), ZoneRare.IconUnderlayFor(ZoneRareTier.Ascendant));
        }
    }
}
