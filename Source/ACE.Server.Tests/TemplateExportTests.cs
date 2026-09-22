using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The pure half of @export-template: argument parsing, wcid allocation, class-name derivation, property
    /// filtering, the npc overlay, the narrow DELETE and the client-facing text. The live-world path (target
    /// selection, thread hand-off, file write) is not covered here.
    /// </summary>
    [TestClass]
    public class TemplateExportTests
    {
        private const uint BlockStart = 78790000;
        private const uint BlockEnd = 78799999;

        // ------------------------------------------------------------------------------------------------
        // Arguments
        // ------------------------------------------------------------------------------------------------

        [TestMethod]
        public void ParseArguments_ZeroArguments_EverythingDefaults()
        {
            var args = TemplateExport.ParseArguments(Array.Empty<string>());

            Assert.IsNull(args.ExplicitWcid);
            Assert.IsNull(args.Flavour);
            Assert.IsFalse(args.Overwrite);
            Assert.IsNull(args.NameOverride);
        }

        [TestMethod]
        public void ParseArguments_IsOrderInsensitive()
        {
            var a = TemplateExport.ParseArguments(new[] { "78790300", "npc", "Gene's", "Handler" });
            var b = TemplateExport.ParseArguments(new[] { "Gene's", "Handler", "npc", "78790300" });
            var c = TemplateExport.ParseArguments(new[] { "npc", "Gene's", "78790300", "Handler" });

            foreach (var args in new[] { a, b, c })
            {
                Assert.AreEqual(78790300u, args.ExplicitWcid);
                Assert.AreEqual(TemplateExport.Flavour.Npc, args.Flavour);
                Assert.AreEqual("Gene's Handler", args.NameOverride);
            }
        }

        [TestMethod]
        public void ParseArguments_Keywords_HexWcid_AndTemplateWordIgnored()
        {
            var args = TemplateExport.ParseArguments(new[] { "template", "0x4B3A", "overwrite", "MONSTER", "Drudge", "2" });

            Assert.AreEqual(0x4B3Au, args.ExplicitWcid);
            Assert.AreEqual(TemplateExport.Flavour.Monster, args.Flavour);
            Assert.IsTrue(args.Overwrite);
            Assert.AreEqual("Drudge 2", args.NameOverride, "a second bare number belongs to the name");
        }

        [TestMethod]
        public void StripForceKeyword_RemovesEveryForceToken()
        {
            var kept = TemplateExport.StripForceKeyword(new[] { "78790001", "FORCE", "weenie", "force" }, out var force);

            Assert.IsTrue(force);
            CollectionAssert.AreEqual(new[] { "78790001", "weenie" }, kept);

            kept = TemplateExport.StripForceKeyword(new[] { "78790001" }, out force);
            Assert.IsFalse(force);
            Assert.AreEqual(1, kept.Length);
        }

        // ------------------------------------------------------------------------------------------------
        // WCID allocation
        // ------------------------------------------------------------------------------------------------

        [TestMethod]
        public void ChooseWcid_Auto_PicksLowestFreeIdInBlock_SkippingExisting()
        {
            var existing = new Dictionary<uint, string> { { BlockStart, "taken" }, { BlockStart + 1, "taken" } };

            var choice = TemplateExport.ChooseWcid(null, false, BlockStart, BlockEnd, 0, existing);

            Assert.IsFalse(choice.Refused);
            Assert.IsFalse(choice.Explicit);
            Assert.AreEqual(BlockStart + 2, choice.Wcid);
            Assert.AreEqual((long)BlockStart + 3, choice.NextHighWaterMark);
        }

        [TestMethod]
        public void ChooseWcid_Auto_NeverGoesBelowHighWaterMark_EvenWhenDatabaseShowsLowerIdsFree()
        {
            // two exports before either file is loaded: the database still shows both ids free
            var existing = new Dictionary<uint, string>();

            var first = TemplateExport.ChooseWcid(null, false, BlockStart, BlockEnd, 0, existing);
            var second = TemplateExport.ChooseWcid(null, false, BlockStart, BlockEnd, first.NextHighWaterMark, existing);

            Assert.AreEqual(BlockStart, first.Wcid);
            Assert.AreEqual(BlockStart + 1, second.Wcid);
            Assert.AreNotEqual(first.Wcid, second.Wcid);
        }

        [TestMethod]
        public void ChooseWcid_Auto_HighWaterMarkOnTakenId_WalksForward()
        {
            var existing = new Dictionary<uint, string> { { BlockStart + 5, "hand loaded" } };

            var choice = TemplateExport.ChooseWcid(null, false, BlockStart, BlockEnd, BlockStart + 5, existing);

            Assert.AreEqual(BlockStart + 6, choice.Wcid);
        }

        [TestMethod]
        public void ChooseWcid_Auto_BlockExhausted_RefusesAndNamesTheProperty_NoWrap()
        {
            var choice = TemplateExport.ChooseWcid(null, false, BlockStart, BlockStart + 1, BlockStart + 2, new Dictionary<uint, string>());

            Assert.IsTrue(choice.Refused);
            StringAssert.Contains(choice.RefusalReason, "content_template_export_wcid_end");
            StringAssert.Contains(choice.RefusalReason, "never wraps");
        }

        [TestMethod]
        public void ChooseWcid_Auto_WarnsPastEightyPercent()
        {
            var choice = TemplateExport.ChooseWcid(null, false, 100, 109, 109, new Dictionary<uint, string>());

            Assert.AreEqual(109u, choice.Wcid);
            Assert.IsTrue(choice.BlockUsedFraction > TemplateExport.BlockUsageWarningFraction);
        }

        [TestMethod]
        public void ChooseWcid_Explicit_ExistingId_RefusedWithName_UnlessOverwrite()
        {
            var existing = new Dictionary<uint, string> { { 78780201, "Ivo, Ruggan's Quartermaster" } };

            var refused = TemplateExport.ChooseWcid(78780201, false, BlockStart, BlockEnd, 0, existing);
            Assert.IsTrue(refused.Refused);
            StringAssert.Contains(refused.RefusalReason, "Ivo, Ruggan's Quartermaster");
            StringAssert.Contains(refused.RefusalReason, "overwrite");

            var allowed = TemplateExport.ChooseWcid(78780201, true, BlockStart, BlockEnd, 0, existing);
            Assert.IsFalse(allowed.Refused);
            Assert.IsTrue(allowed.ReplacesExisting);
            Assert.IsTrue(allowed.OutsideBlock);
            Assert.AreEqual("Ivo, Ruggan's Quartermaster", allowed.ExistingName);
        }

        [TestMethod]
        public void ChooseWcid_Explicit_InsideBlock_AdvancesHighWaterMarkPastIt()
        {
            var choice = TemplateExport.ChooseWcid(BlockStart + 10, false, BlockStart, BlockEnd, BlockStart + 3, new Dictionary<uint, string>());

            Assert.IsFalse(choice.Refused);
            Assert.IsFalse(choice.OutsideBlock);
            Assert.AreEqual((long)BlockStart + 11, choice.NextHighWaterMark);
        }

        [TestMethod]
        public void ChooseWcid_Explicit_OutsideBlock_DoesNotTouchHighWaterMark()
        {
            var choice = TemplateExport.ChooseWcid(78780300, false, BlockStart, BlockEnd, BlockStart + 3, new Dictionary<uint, string>());

            Assert.IsFalse(choice.Refused);
            Assert.IsTrue(choice.OutsideBlock);
            Assert.AreEqual((long)BlockStart + 3, choice.NextHighWaterMark);
        }

        [TestMethod]
        public void ChooseWcid_InvertedBlock_Refused()
        {
            var choice = TemplateExport.ChooseWcid(null, false, BlockEnd, BlockStart, 0, new Dictionary<uint, string>());

            Assert.IsTrue(choice.Refused);
        }

        [TestMethod]
        public void IsInExportBlock_And_ImportRefusal()
        {
            Assert.IsTrue(TemplateExport.IsInExportBlock(BlockStart, BlockStart, BlockEnd));
            Assert.IsTrue(TemplateExport.IsInExportBlock(BlockEnd, BlockStart, BlockEnd));
            Assert.IsFalse(TemplateExport.IsInExportBlock(BlockStart - 1, BlockStart, BlockEnd));
            Assert.IsFalse(TemplateExport.IsInExportBlock(78780201, BlockStart, BlockEnd));

            var text = TemplateExport.ImportRefusal(78790005, BlockStart, BlockEnd);
            StringAssert.Contains(text, "temporary export staging block");
            StringAssert.Contains(text, "force");
            StringAssert.Contains(text, "Renumber");
            AssertAscii(text);
        }

        // ------------------------------------------------------------------------------------------------
        // Names
        // ------------------------------------------------------------------------------------------------

        [TestMethod]
        public void ChooseName_OverrideWinsElseLiveName()
        {
            Assert.AreEqual("Gene's Handler", TemplateExport.ChooseName("Drudge Slinker", "Gene's Handler"));
            Assert.AreEqual("Drudge Slinker", TemplateExport.ChooseName("Drudge Slinker", null));
            Assert.AreEqual("Drudge Slinker", TemplateExport.ChooseName("Drudge Slinker", "   "));
            Assert.AreEqual("Exported Object", TemplateExport.ChooseName(null, null));
        }

        [TestMethod]
        public void BuildClassName_IsSlugWithPrefix_ApostropheSafe()
        {
            var className = TemplateExport.BuildClassName(78790000, "Gene's Handler");

            Assert.AreEqual("tmpl78790000_gene_s_handler", className);
            Assert.IsFalse(className.Contains("'"));
        }

        [TestMethod]
        public void BuildClassName_LongName_TruncatedToColumn_PrefixIntact()
        {
            var longName = string.Join(" ", Enumerable.Repeat("Sir Reginald Fitzwilliam of the Northern Marches", 5));

            var className = TemplateExport.BuildClassName(78799999, longName);

            Assert.IsTrue(className.Length <= TemplateExport.MaxClassNameLength, className.Length.ToString());
            StringAssert.StartsWith(className, "tmpl78799999_");
            Assert.IsFalse(className.EndsWith("_"));
        }

        [TestMethod]
        public void BuildClassName_NoUsableCharacters_FallsBackToObject()
        {
            Assert.AreEqual("tmpl5_object", TemplateExport.BuildClassName(5, "!!! ???"));
        }

        // ------------------------------------------------------------------------------------------------
        // Filtering
        // ------------------------------------------------------------------------------------------------

        [TestMethod]
        public void Filters_DropInstanceAndPetBookkeeping_KeepAppearance()
        {
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.PetBondLevel, false));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.PetMutCritRating, false));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.PetMaturityKills, false));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.PetPotencyStored, false));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.CapturedCreatureWCID, false));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.VisualOverridePaletteTemplate, false));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.PlacementPosition, false));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.ItemExpirationTimestamp, false));
            Assert.IsTrue(TemplateExport.KeepInt(PropertyInt.CreatureVariant, false));
            Assert.IsTrue(TemplateExport.KeepInt(PropertyInt.Level, false));
            Assert.IsTrue(TemplateExport.KeepInt(PropertyInt.PaletteTemplate, false));

            Assert.IsFalse(TemplateExport.KeepBool(PropertyBool.PetIsMaleOverride, false));
            Assert.IsFalse(TemplateExport.KeepBool(PropertyBool.PetIsJuvenile, false));
            Assert.IsTrue(TemplateExport.KeepBool(PropertyBool.Attackable, false));

            Assert.IsFalse(TemplateExport.KeepFloat(PropertyFloat.CreationTimestamp, false));
            Assert.IsFalse(TemplateExport.KeepFloat(PropertyFloat.PetNextBreedingTime, false));
            Assert.IsFalse(TemplateExport.KeepFloat(PropertyFloat.VisualOverrideShade, false));
            Assert.IsTrue(TemplateExport.KeepFloat(PropertyFloat.Shade, false));
            Assert.IsTrue(TemplateExport.KeepFloat(PropertyFloat.DefaultScale, false));

            Assert.IsFalse(TemplateExport.KeepDid(PropertyDataId.VisualOverrideSetup, false));
            Assert.IsFalse(TemplateExport.KeepDid(PropertyDataId.CapturedClothingBase, false));
            Assert.IsFalse(TemplateExport.KeepDid(PropertyDataId.CreatedByAccountId, false));
            Assert.IsTrue(TemplateExport.KeepDid(PropertyDataId.Setup, false));
            Assert.IsTrue(TemplateExport.KeepDid(PropertyDataId.ClothingBase, false));

            Assert.IsFalse(TemplateExport.KeepString(PropertyString.CapturedObjDescAnimParts, false));
            Assert.IsFalse(TemplateExport.KeepString(PropertyString.Name, false), "Name is always replaced by the chosen name");

            Assert.IsFalse(TemplateExport.KeepInt64(PropertyInt64.PetBondXp, false));

            Assert.IsFalse(TemplateExport.KeepPosition(PositionType.Location, false));
            Assert.IsFalse(TemplateExport.KeepPosition(PositionType.Home, false));
            Assert.IsTrue(TemplateExport.KeepPosition(PositionType.Destination, false));
        }

        [TestMethod]
        public void Filters_PlayerSource_IsAWhitelist()
        {
            Assert.IsTrue(TemplateExport.KeepInt(PropertyInt.Level, true));
            Assert.IsTrue(TemplateExport.KeepInt(PropertyInt.HeritageGroup, true));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.PlayerKillerStatus, true));
            Assert.IsFalse(TemplateExport.KeepInt(PropertyInt.AvailableSkillCredits, true));
            Assert.IsFalse(TemplateExport.KeepInt64(PropertyInt64.AvailableExperience, true));
            Assert.IsFalse(TemplateExport.KeepBool(PropertyBool.Attackable, true));
            Assert.IsFalse(TemplateExport.KeepString(PropertyString.Title, true));
            Assert.IsTrue(TemplateExport.KeepDid(PropertyDataId.Setup, true));
            Assert.IsFalse(TemplateExport.KeepDid(PropertyDataId.Icon + 1000, true));
            Assert.IsFalse(TemplateExport.KeepPosition(PositionType.Destination, true));
        }

        [TestMethod]
        public void MapWeenieType_PetsAndPlayersBecomeCreatures_OthersUnchanged()
        {
            Assert.AreEqual(WeenieType.Creature, TemplateExport.MapWeenieType(WeenieType.CombatPet, false));
            Assert.AreEqual(WeenieType.Creature, TemplateExport.MapWeenieType(WeenieType.Pet, false));
            Assert.AreEqual(WeenieType.Creature, TemplateExport.MapWeenieType(WeenieType.Creature, true));
            Assert.AreEqual(WeenieType.Chest, TemplateExport.MapWeenieType(WeenieType.Chest, false));
            Assert.AreEqual(WeenieType.Portal, TemplateExport.MapWeenieType(WeenieType.Portal, false));
        }

        // ------------------------------------------------------------------------------------------------
        // Building the weenie
        // ------------------------------------------------------------------------------------------------

        private static Biota PetBiota()
        {
            return new Biota
            {
                Id = 0x80001234,
                WeenieClassId = 787802001,
                WeenieType = WeenieType.CombatPet,
                PropertiesInt = new Dictionary<PropertyInt, int>
                {
                    { PropertyInt.ItemType, (int)ItemType.Creature },
                    { PropertyInt.CreatureType, (int)CreatureType.Drudge },
                    { PropertyInt.Level, 120 },
                    { PropertyInt.PaletteTemplate, 12 },
                    { PropertyInt.CreatureVariant, 1 },
                    { PropertyInt.Tolerance, 4 },
                    { PropertyInt.TargetingTactic, 2 },
                    { PropertyInt.XpOverride, 50000 },
                    { PropertyInt.PetBondLevel, 7 },
                    { PropertyInt.PetMutCritRating, 3 },
                    { PropertyInt.CapturedCreatureWCID, 5595 },
                },
                PropertiesInt64 = new Dictionary<PropertyInt64, long> { { PropertyInt64.PetBondXp, 999 } },
                PropertiesBool = new Dictionary<PropertyBool, bool>
                {
                    { PropertyBool.Attackable, true },
                    { PropertyBool.PetIsJuvenile, true },
                    { PropertyBool.PetIsMaleOverride, true },
                },
                PropertiesFloat = new Dictionary<PropertyFloat, double>
                {
                    { PropertyFloat.Shade, 0.42 },
                    { PropertyFloat.DefaultScale, 1.2 },
                    { PropertyFloat.CreationTimestamp, 123456.0 },
                    { PropertyFloat.PetNextBreedingTime, 5.0 },
                },
                PropertiesString = new Dictionary<PropertyString, string>
                {
                    { PropertyString.Name, "Bitey" },
                    { PropertyString.CapturedCreatureName, "Drudge Slinker" },
                },
                PropertiesDID = new Dictionary<PropertyDataId, uint>
                {
                    { PropertyDataId.Setup, 0x02000926 },
                    { PropertyDataId.MotionTable, 0x09000001 },
                    { PropertyDataId.SoundTable, 0x20000001 },
                    { PropertyDataId.CombatTable, 0x30000001 },
                    { PropertyDataId.PhysicsEffectTable, 0x34000001 },
                    { PropertyDataId.ClothingBase, 0x100004F7 },
                    { PropertyDataId.PaletteBase, 0x0400007E },
                    { PropertyDataId.Icon, 0x06001234 },
                    { PropertyDataId.DeathTreasureType, 42 },
                    { PropertyDataId.VisualOverrideSetup, 0x02000926 },
                },
                PropertiesIID = new Dictionary<PropertyInstanceId, uint>
                {
                    { PropertyInstanceId.PetOwner, 0x50000001 },
                    { PropertyInstanceId.PetDevice, 0x80000002 },
                },
                PropertiesPosition = new Dictionary<PositionType, PropertiesPosition>
                {
                    { PositionType.Location, Pos(0x013A02AE) },
                },
                PropertiesAttribute = new Dictionary<PropertyAttribute, PropertiesAttribute>
                {
                    { PropertyAttribute.Strength, new PropertiesAttribute { InitLevel = 200, LevelFromCP = 10, CPSpent = 5 } },
                },
                PropertiesAttribute2nd = new Dictionary<PropertyAttribute2nd, PropertiesAttribute2nd>
                {
                    { PropertyAttribute2nd.MaxHealth, new PropertiesAttribute2nd { InitLevel = 500, LevelFromCP = 0, CurrentLevel = 137 } },
                },
                PropertiesSkill = new Dictionary<Skill, PropertiesSkill>
                {
                    { Skill.MeleeDefense, new PropertiesSkill { InitLevel = 300, LevelFromPP = 20, SAC = SkillAdvancementClass.Trained, LastUsedTime = 99 } },
                },
                PropertiesBodyPart = new Dictionary<CombatBodyPart, PropertiesBodyPart>
                {
                    { CombatBodyPart.Head, BodyPart(DamageType.Slash, 10, 100) },
                    { CombatBodyPart.Chest, BodyPart(DamageType.Bludgeon, 12, 120) },
                },
                PropertiesSpellBook = new Dictionary<int, float> { { 1234, 1.0f } },
                PropertiesCreateList = new List<PropertiesCreateList>
                {
                    CreateRow(DestinationType.Treasure, 273),
                    CreateRow(DestinationType.Wield, 300),
                    CreateRow(DestinationType.Contain, 301),
                },
                PropertiesGenerator = new List<PropertiesGenerator>
                {
                    new PropertiesGenerator { Probability = 1, WeenieClassId = 1, InitCreate = 1, MaxCreate = 1, WhenCreate = RegenerationType.Destruction, WhereCreate = RegenLocationType.Scatter },
                },
            };
        }

        private static PropertiesPosition Pos(uint cell)
        {
            return new PropertiesPosition { ObjCellId = cell, PositionX = 0, PositionY = 0, PositionZ = 0, RotationW = 1, RotationX = 0, RotationY = 0, RotationZ = 0 };
        }

        private static PropertiesBodyPart BodyPart(DamageType type, int dval, int armor)
        {
            return new PropertiesBodyPart
            {
                DType = type, DVal = dval, DVar = 0.5f, BaseArmor = armor,
                ArmorVsSlash = armor, ArmorVsPierce = armor, ArmorVsBludgeon = armor, ArmorVsCold = armor, ArmorVsFire = armor, ArmorVsAcid = armor, ArmorVsElectric = armor, ArmorVsNether = armor,
                BH = 1, HLF = 1, MLF = 1, LLF = 1, HRF = 1, MRF = 1, LRF = 1, HLB = 1, MLB = 1, LLB = 1, HRB = 1, MRB = 1, LRB = 1,
            };
        }

        private static PropertiesCreateList CreateRow(DestinationType destination, uint wcid)
        {
            return new PropertiesCreateList { DestinationType = destination, WeenieClassId = wcid, StackSize = 1, Palette = 0, Shade = 0, TryToBond = false };
        }

        private static TemplateExport.Snapshot PetSnapshot(bool withWielded = true)
        {
            return new TemplateExport.Snapshot
            {
                Biota = PetBiota(),
                LiveName = "Bitey",
                IsPet = true,
                IsCreature = true,
                ObjDescPaletteId = 0x0400007E,
                AnimParts = new List<PropertiesAnimPart> { new PropertiesAnimPart { Index = 0, AnimationId = 0x01000ABC }, new PropertiesAnimPart { Index = 3, AnimationId = 0x01000DEF } },
                SubPalettes = new List<PropertiesPalette> { new PropertiesPalette { SubPaletteId = 0x04001234, Offset = 0, Length = 24 } },
                TextureChanges = new List<PropertiesTextureMap> { new PropertiesTextureMap { PartIndex = 3, OldTexture = 0x05000001, NewTexture = 0x05000002 } },
                Wielded = withWielded
                    ? new List<TemplateExport.WieldedItem> { new TemplateExport.WieldedItem(45000, "Skin Axe", 3, 0.5f) }
                    : new List<TemplateExport.WieldedItem>(),
            };
        }

        [TestMethod]
        public void BuildWeenie_Monster_CapturesLookAndStats_DropsBookkeeping()
        {
            var w = TemplateExport.BuildWeenie(PetSnapshot(), 78790000, "tmpl78790000_bitey", "Bitey", TemplateExport.Flavour.Monster, DateTime.UtcNow, out var summary);

            Assert.AreEqual(78790000u, w.ClassId);
            Assert.AreEqual("tmpl78790000_bitey", w.ClassName);
            Assert.AreEqual((int)WeenieType.Creature, w.Type, "a pet exports as a plain creature");

            // appearance as rendered
            Assert.AreEqual(2, w.WeeniePropertiesAnimPart.Count);
            Assert.AreEqual(1, w.WeeniePropertiesPalette.Count);
            Assert.AreEqual(1, w.WeeniePropertiesTextureMap.Count);
            Assert.AreEqual(0x0400007Eu, TemplateExport.GetDid(w, PropertyDataId.PaletteBase));
            Assert.AreEqual(0x02000926u, TemplateExport.GetDid(w, PropertyDataId.Setup));
            Assert.AreEqual(0x100004F7u, TemplateExport.GetDid(w, PropertyDataId.ClothingBase));
            Assert.AreEqual(12, TemplateExport.GetInt(w, PropertyInt.PaletteTemplate));
            Assert.AreEqual(1, TemplateExport.GetInt(w, PropertyInt.CreatureVariant));
            Assert.AreEqual(0.42, TemplateExport.GetFloat(w, PropertyFloat.Shade));
            Assert.AreEqual("Bitey", TemplateExport.GetString(w, PropertyString.Name));

            // faithful combat
            Assert.AreEqual(true, TemplateExport.GetBool(w, PropertyBool.Attackable));
            Assert.AreEqual(4, TemplateExport.GetInt(w, PropertyInt.Tolerance));
            Assert.AreEqual(42u, TemplateExport.GetDid(w, PropertyDataId.DeathTreasureType));
            Assert.AreEqual(2, w.WeeniePropertiesBodyPart.Count);
            Assert.AreEqual(1, w.WeeniePropertiesSpellBook.Count);

            // raised levels folded, runtime fields zeroed
            var str = w.WeeniePropertiesAttribute.Single();
            Assert.AreEqual(210u, str.InitLevel);
            Assert.AreEqual(0u, str.LevelFromCP);
            var health = w.WeeniePropertiesAttribute2nd.Single();
            Assert.AreEqual(0u, health.CurrentLevel);
            var skill = w.WeeniePropertiesSkill.Single();
            Assert.AreEqual(320u, skill.InitLevel);
            Assert.AreEqual(0.0, skill.LastUsedTime);

            // bookkeeping and instance state gone
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.PetBondLevel));
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.PetMutCritRating));
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.CapturedCreatureWCID));
            Assert.IsNull(TemplateExport.GetBool(w, PropertyBool.PetIsJuvenile));
            Assert.IsNull(TemplateExport.GetBool(w, PropertyBool.PetIsMaleOverride));
            Assert.IsNull(TemplateExport.GetFloat(w, PropertyFloat.CreationTimestamp));
            Assert.IsNull(TemplateExport.GetFloat(w, PropertyFloat.PetNextBreedingTime));
            Assert.IsNull(TemplateExport.GetDid(w, PropertyDataId.VisualOverrideSetup));
            Assert.IsNull(TemplateExport.GetString(w, PropertyString.CapturedCreatureName));
            Assert.AreEqual(0, w.WeeniePropertiesInt64.Count);
            Assert.AreEqual(0, w.WeeniePropertiesIID.Count);
            Assert.AreEqual(0, w.WeeniePropertiesPosition.Count);
            Assert.AreEqual(0, w.WeeniePropertiesGenerator.Count);

            // monster flavour of a pet: held capture-skin weapons are not written, biota wield rows stay
            Assert.IsTrue(summary.WieldSkippedForPet);
            Assert.AreEqual(0, summary.WieldedItems);
            Assert.AreEqual(3, w.WeeniePropertiesCreateList.Count);

            StringAssert.Contains(summary.ToLine(), "setup 0x02000926");
            StringAssert.Contains(summary.ToLine(), "2 anim parts");
            StringAssert.Contains(summary.ToLine(), "2 body parts");
            AssertAscii(summary.ToLine());
        }

        [TestMethod]
        public void BuildWeenie_Npc_MirrorsIvoBlock_AndDropsLootXpCorpse_WithHeldItems()
        {
            var w = TemplateExport.BuildWeenie(PetSnapshot(), 78790001, "tmpl78790001_bitey", "Bitey", TemplateExport.Flavour.Npc, DateTime.UtcNow, out var summary);

            // Ivo block ints / bools
            Assert.AreEqual((int)Usable.Remote, TemplateExport.GetInt(w, PropertyInt.ItemUseable));
            Assert.AreEqual((int)RadarColor.NPC, TemplateExport.GetInt(w, PropertyInt.RadarBlipColor));
            Assert.AreEqual((int)RadarBehavior.ShowAlways, TemplateExport.GetInt(w, PropertyInt.ShowableOnRadar));
            Assert.AreEqual((int)PlayerKillerStatus.RubberGlue, TemplateExport.GetInt(w, PropertyInt.PlayerKillerStatus));
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.Tolerance));
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.TargetingTactic));
            Assert.AreEqual(true, TemplateExport.GetBool(w, PropertyBool.Stuck));
            Assert.AreEqual(false, TemplateExport.GetBool(w, PropertyBool.Attackable));
            Assert.AreEqual(true, TemplateExport.GetBool(w, PropertyBool.Invincible));

            // no loot / XP / corpse
            Assert.AreEqual(true, TemplateExport.GetBool(w, PropertyBool.NoCorpse));
            Assert.AreEqual(0, TemplateExport.GetInt(w, PropertyInt.XpOverride));
            Assert.IsNull(TemplateExport.GetDid(w, PropertyDataId.DeathTreasureType));
            Assert.IsFalse(w.WeeniePropertiesCreateList.Any(r => (r.DestinationType & (sbyte)DestinationType.Treasure) != 0));

            // held items: live set replaces the biota wield rows, contain row survives
            Assert.AreEqual(1, summary.WieldedItems);
            Assert.IsFalse(summary.WieldSkippedForPet);
            var wield = w.WeeniePropertiesCreateList.Where(r => r.DestinationType == (sbyte)DestinationType.Wield).ToList();
            Assert.AreEqual(1, wield.Count);
            Assert.AreEqual(45000u, wield[0].WeenieClassId);
            Assert.AreEqual(3, wield[0].Palette);
            Assert.AreEqual(0.5f, wield[0].Shade);
            Assert.AreEqual(1, wield[0].StackSize);
            Assert.IsFalse(wield[0].TryToBond);
            Assert.IsTrue(w.WeeniePropertiesCreateList.Any(r => r.DestinationType == (sbyte)DestinationType.Contain));

            // the look is identical between flavours
            Assert.AreEqual(2, w.WeeniePropertiesAnimPart.Count);
            Assert.AreEqual(1, w.WeeniePropertiesTextureMap.Count);
        }

        [TestMethod]
        public void BuildWeenie_Player_WhitelistOnly_DefaultsToNpc_ForcedCreature()
        {
            var biota = PetBiota();
            biota.WeenieType = WeenieType.Creature;
            biota.PropertiesInt[PropertyInt.AvailableSkillCredits] = 12;
            biota.PropertiesInt64[PropertyInt64.AvailableExperience] = 123456789;
            biota.PropertiesString[PropertyString.Title] = "Knight";
            biota.PropertiesSkill[Skill.Alchemy] = new PropertiesSkill { SAC = SkillAdvancementClass.Untrained };

            var snapshot = new TemplateExport.Snapshot
            {
                Biota = biota,
                LiveName = "Gene",
                IsPlayer = true,
                IsCreature = true,
                ObjDescPaletteId = 0x0400007E,
                AnimParts = new List<PropertiesAnimPart> { new PropertiesAnimPart { Index = 1, AnimationId = 0x01000111 } },
                Wielded = new List<TemplateExport.WieldedItem> { new TemplateExport.WieldedItem(12345, "Sword", 0, 0f), new TemplateExport.WieldedItem(12346, "Shield", 0, 0f) },
            };

            Assert.AreEqual(TemplateExport.Flavour.Npc, TemplateExport.DefaultFlavour(snapshot));

            var w = TemplateExport.BuildWeenie(snapshot, 78790002, "tmpl78790002_gene", "Gene", TemplateExport.Flavour.Npc, DateTime.UtcNow, out var summary);

            Assert.AreEqual((int)WeenieType.Creature, w.Type);
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.AvailableSkillCredits));
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.Tolerance));
            Assert.AreEqual(0, w.WeeniePropertiesInt64.Count);
            Assert.IsNull(TemplateExport.GetString(w, PropertyString.Title));
            Assert.AreEqual("Gene", TemplateExport.GetString(w, PropertyString.Name));
            Assert.AreEqual(120, TemplateExport.GetInt(w, PropertyInt.Level));
            Assert.AreEqual(0x02000926u, TemplateExport.GetDid(w, PropertyDataId.Setup));
            Assert.IsNull(TemplateExport.GetDid(w, PropertyDataId.DeathTreasureType));
            Assert.AreEqual(0, w.WeeniePropertiesSpellBook.Count, "a player's spell book is not copied");
            Assert.AreEqual(1, w.WeeniePropertiesSkill.Count, "untrained rows dropped");
            Assert.AreEqual(1, w.WeeniePropertiesAnimPart.Count);
            Assert.AreEqual(false, TemplateExport.GetBool(w, PropertyBool.Attackable));
            Assert.AreEqual(2, summary.WieldedItems);
            Assert.AreEqual(2, w.WeeniePropertiesCreateList.Count, "held items only, no biota create list for a player");

            // explicit monster on a player: attackable, still a creature, still no player state
            w = TemplateExport.BuildWeenie(snapshot, 78790003, "tmpl78790003_gene", "Gene", TemplateExport.Flavour.Monster, DateTime.UtcNow, out summary);
            Assert.AreEqual(true, TemplateExport.GetBool(w, PropertyBool.Attackable));
            Assert.AreEqual(2, summary.WieldedItems);
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.AvailableSkillCredits));
        }

        [TestMethod]
        public void BuildWeenie_NonCreature_KeepsTypeAndLook_IgnoresFlavour()
        {
            var biota = new Biota
            {
                Id = 1,
                WeenieClassId = 21,
                WeenieType = WeenieType.Chest,
                PropertiesInt = new Dictionary<PropertyInt, int> { { PropertyInt.ItemType, (int)ItemType.Container }, { PropertyInt.PlacementPosition, 3 } },
                PropertiesString = new Dictionary<PropertyString, string> { { PropertyString.Name, "Chest" } },
                PropertiesDID = new Dictionary<PropertyDataId, uint> { { PropertyDataId.Setup, 0x02000123 } },
                PropertiesPosition = new Dictionary<PositionType, PropertiesPosition> { { PositionType.Location, Pos(0x00010100) } },
            };
            var snapshot = new TemplateExport.Snapshot { Biota = biota, LiveName = "Chest", IsCreature = false, ObjDescPaletteId = 0 };

            var w = TemplateExport.BuildWeenie(snapshot, 78790004, "tmpl78790004_chest", "Chest", TemplateExport.Flavour.Npc, DateTime.UtcNow, out var summary);

            Assert.AreEqual((int)WeenieType.Chest, w.Type);
            Assert.IsTrue(summary.FlavourIgnored);
            Assert.IsNull(TemplateExport.GetBool(w, PropertyBool.Invincible), "no combat properties fabricated");
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.PlacementPosition));
            Assert.AreEqual(0, w.WeeniePropertiesAttribute.Count);
            Assert.AreEqual(0, w.WeeniePropertiesBodyPart.Count);
            Assert.AreEqual(0, w.WeeniePropertiesPosition.Count);
            Assert.AreEqual(0x02000123u, TemplateExport.GetDid(w, PropertyDataId.Setup));
        }

        // ------------------------------------------------------------------------------------------------
        // SQL text and chat text
        // ------------------------------------------------------------------------------------------------

        [TestMethod]
        public void BuildDeleteStatement_IsNarrowOnIdAndClassName()
        {
            var sql = TemplateExport.BuildDeleteStatement(78790000, "tmpl78790000_bitey");

            StringAssert.Contains(sql, "DELETE FROM `weenie` WHERE `class_Id` = 78790000 AND `class_Name` = 'tmpl78790000_bitey';");
            StringAssert.Contains(sql, "-- ");
            Assert.IsFalse(sql.Contains("\r"));
            AssertAscii(sql);
        }

        [TestMethod]
        public void BuildHeader_NamesSourceExporterFlavourAndDependencies_Ascii()
        {
            var header = TemplateExport.BuildHeader(new TemplateExport.HeaderInfo
            {
                NewWcid = 78790000, ClassName = "tmpl78790000_gene_s_handler", Name = "Gene's Handler",
                SourceWcid = 787802001, SourceName = "Bitey", SourceGuid = 0x80001234, SourceType = WeenieType.CombatPet,
                CapturedFromWcid = 5595, Exporter = "Schneebly", ExportedUtc = new DateTime(2026, 9, 19, 14, 2, 11, DateTimeKind.Utc),
                Flavour = TemplateExport.Flavour.Npc, Summary = new TemplateExport.CaptureSummary { AnimParts = 2 },
                Wielded = new List<TemplateExport.WieldedItem> { new TemplateExport.WieldedItem(45000, "Skin Axe", 3, 0.5f) },
                BlockStart = BlockStart, BlockEnd = BlockEnd,
            });

            StringAssert.Contains(header, "wcid 787802001");
            StringAssert.Contains(header, "captured from wcid 5595");
            StringAssert.Contains(header, "Schneebly");
            StringAssert.Contains(header, "2026-09-19 14:02:11 UTC");
            StringAssert.Contains(header, "flavour: npc");
            StringAssert.Contains(header, "rendered ObjDesc");
            StringAssert.Contains(header, "45000  Skin Axe");
            StringAssert.Contains(header, "DAT art");
            Assert.IsFalse(header.Contains("\r"));
            AssertAscii(header);
        }

        [TestMethod]
        public void BuildChatReply_UnderEightLines_Ascii_EndsWithRenumberStep()
        {
            var reply = TemplateExport.BuildChatReply(new TemplateExport.ReplyInfo
            {
                Name = "Gene's Handler", Wcid = 78790000, ClassName = "tmpl78790000_gene_s_handler", Flavour = TemplateExport.Flavour.Monster,
                SourceName = "Bitey", SourceWcid = 787802001, CapturedFromWcid = 5595,
                FilePath = @"C:\ACE\Content\sql\weenies\78790000 Gene's Handler.sql", SummaryLine = "setup 0x02000926, 2 anim parts",
                BlockStart = BlockStart, BlockEnd = BlockEnd, BlockUsedFraction = 0.9,
            });

            var lines = reply.Split('\n');
            Assert.IsTrue(lines.Length <= 8, lines.Length.ToString());
            Assert.IsFalse(reply.Contains("\r"));
            AssertAscii(reply);
            StringAssert.Contains(reply, "verified free at export time");
            StringAssert.Contains(reply, "reserved for exports");
            StringAssert.Contains(reply, "90% used");
            // Not auto-loaded (switch off): the manual step must name the exact command, force included,
            // or the import is refused inside the staging block.
            StringAssert.Contains(reply, "@import-sql 78790000 force");
            StringAssert.Contains(reply, "@ci 78790000");
            StringAssert.Contains(lines[lines.Length - 1], "renumber");
        }

        // ---- pet summon state is not part of the template ---------------------------------------

        private static TemplateExport.Snapshot SummonedPetSnapshot(ACE.Entity.Models.Weenie template)
        {
            var s = PetSnapshot(withWielded: false);
            var b = s.Biota;
            // What CombatPet.Init writes onto a live pet (values from the reported Tumerok Priest export).
            b.PropertiesInt[PropertyInt.Lifespan] = 543;
            b.PropertiesInt[PropertyInt.Faction1Bits] = 1;
            b.PropertiesInt[PropertyInt.DamageRating] = 190;
            b.PropertiesInt[PropertyInt.DamageResistRating] = 233;
            b.PropertiesInt[PropertyInt.CritRating] = 5;
            b.PropertiesInt[PropertyInt.PhysicsState] = 1036;          // Ethereal | ReportCollisions | Gravity
            b.PropertiesFloat[PropertyFloat.TimeToRot] = 600;
            b.PropertiesFloat[PropertyFloat.UseRadius] = 0.5;
            b.PropertiesBool[PropertyBool.Ethereal] = true;
            b.PropertiesInt64[PropertyInt64.LumAugVoidCount] = 1850;
            b.PropertiesAttribute2nd = new Dictionary<PropertyAttribute2nd, PropertiesAttribute2nd>
            {
                { PropertyAttribute2nd.MaxHealth, new PropertiesAttribute2nd { InitLevel = 4320 } },
            };
            s.PetTemplate = template;
            return s;
        }

        [TestMethod]
        public void PetExport_DropsTheDespawnTimer_EvenWhenTheTemplateHasOne()
        {
            // The pet template itself ships Lifespan 43: restoring it would still delete every copy.
            var template = new ACE.Entity.Models.Weenie { WeenieClassId = 49149, ClassName = "tumerokpriestpet", WeenieType = WeenieType.CombatPet, PropertiesInt = new Dictionary<PropertyInt, int> { { PropertyInt.Lifespan, 43 } } };
            var w = TemplateExport.BuildWeenie(SummonedPetSnapshot(template), 78790000, "tmpl78790000_x", "X", TemplateExport.Flavour.Npc, DateTime.UtcNow, out var summary);

            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.Lifespan));
            Assert.IsNull(TemplateExport.GetFloat(w, PropertyFloat.TimeToRot));
            Assert.IsTrue(summary.PetSummonStateReverted);
            StringAssert.Contains(summary.ToLine(), "despawn timer removed");
        }

        [TestMethod]
        public void PetExport_RatingsAndVitalsComeFromTheTemplate()
        {
            var template = new ACE.Entity.Models.Weenie
            {
                WeenieClassId = 49149, ClassName = "tumerokpriestpet", WeenieType = WeenieType.CombatPet, 
                PropertiesInt = new Dictionary<PropertyInt, int> { { PropertyInt.DamageRating, 10 } },
                PropertiesAttribute2nd = new Dictionary<PropertyAttribute2nd, PropertiesAttribute2nd>
                {
                    { PropertyAttribute2nd.MaxHealth, new PropertiesAttribute2nd { InitLevel = 1250 } },
                },
            };
            var w = TemplateExport.BuildWeenie(SummonedPetSnapshot(template), 78790000, "tmpl78790000_x", "X", TemplateExport.Flavour.Monster, DateTime.UtcNow, out _);

            Assert.AreEqual(10, TemplateExport.GetInt(w, PropertyInt.DamageRating), "template value, not the pet's 190");
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.DamageResistRating), "template has none, so none");
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.CritRating));
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.Faction1Bits), "the owner's faction must not carry over");
            Assert.AreEqual(0, w.WeeniePropertiesInt64.Count, "the owner's luminance counts must not carry over");
            Assert.AreEqual(1250u, w.WeeniePropertiesAttribute2nd.Single().InitLevel, "base health, not the bonded 4320");
        }

        [TestMethod]
        public void PetExport_IsSolid()
        {
            var w = TemplateExport.BuildWeenie(SummonedPetSnapshot(null), 78790000, "tmpl78790000_x", "X", TemplateExport.Flavour.Monster, DateTime.UtcNow, out _);

            Assert.AreEqual(false, TemplateExport.GetBool(w, PropertyBool.Ethereal));
            Assert.AreEqual(1036 & ~(int)PhysicsState.Ethereal, TemplateExport.GetInt(w, PropertyInt.PhysicsState));
        }

        [TestMethod]
        public void PetExport_WithoutTemplate_StillStripsSummonState()
        {
            var w = TemplateExport.BuildWeenie(SummonedPetSnapshot(null), 78790000, "tmpl78790000_x", "X", TemplateExport.Flavour.Monster, DateTime.UtcNow, out _);

            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.Lifespan));
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.DamageRating));
            Assert.IsNull(TemplateExport.GetInt(w, PropertyInt.Faction1Bits));
            Assert.AreEqual(4320u, w.WeeniePropertiesAttribute2nd.Single().InitLevel, "no template: nothing better than the live value");
        }

        [TestMethod]
        public void NpcExport_IsClickableFromANormalDistance()
        {
            var w = TemplateExport.BuildWeenie(SummonedPetSnapshot(null), 78790000, "tmpl78790000_x", "X", TemplateExport.Flavour.Npc, DateTime.UtcNow, out _);
            Assert.AreEqual(TemplateExport.NpcMinUseRadius, TemplateExport.GetFloat(w, PropertyFloat.UseRadius));

            var m = TemplateExport.BuildWeenie(SummonedPetSnapshot(null), 78790001, "tmpl78790001_x", "X", TemplateExport.Flavour.Monster, DateTime.UtcNow, out _);
            Assert.AreEqual(0.5, TemplateExport.GetFloat(m, PropertyFloat.UseRadius), "monster flavour is left as captured");
        }

        [TestMethod]
        public void NonPetExport_KeepsItsRatingsAndTimers()
        {
            var s = PetSnapshot(withWielded: false);
            s.IsPet = false;
            s.Biota.PropertiesInt[PropertyInt.DamageRating] = 40;
            s.Biota.PropertiesInt[PropertyInt.Lifespan] = 3600;
            var w = TemplateExport.BuildWeenie(s, 78790000, "tmpl78790000_x", "X", TemplateExport.Flavour.Monster, DateTime.UtcNow, out var summary);

            Assert.AreEqual(40, TemplateExport.GetInt(w, PropertyInt.DamageRating));
            Assert.AreEqual(3600, TemplateExport.GetInt(w, PropertyInt.Lifespan), "only pet summon state is reverted");
            Assert.IsFalse(summary.PetSummonStateReverted);
        }

        private static readonly string[] Stages = { "Newborn", "Whelp", "Juvenile", "Adolescent", "Young Adult" };

        [TestMethod]
        public void PetBaseName_DropsOwnerAndStage()
        {
            Assert.AreEqual("Tumerok Priest", TemplateExport.PetBaseName("+Schneebly's Tumerok Priest", Stages));
            Assert.AreEqual("Tumerok Priest", TemplateExport.PetBaseName("+Schneebly's Whelp Tumerok Priest", Stages));
            Assert.AreEqual("Flaw's Dingleberry", TemplateExport.PetBaseName("+Schneebly's Flaw's Dingleberry", Stages));
            Assert.AreEqual("Browerk", TemplateExport.PetBaseName("Browerk", Stages));
        }

        [TestMethod]
        public void BuildChatReply_ImportStarted_SaysItIsLoading()
        {
            var reply = TemplateExport.BuildChatReply(new TemplateExport.ReplyInfo
            {
                Name = "X", Wcid = 78790000, ClassName = "tmpl78790000_x", Flavour = TemplateExport.Flavour.Npc,
                SourceName = "Y", SourceWcid = 1, FilePath = "f.sql", SummaryLine = "s", BlockStart = BlockStart, BlockEnd = BlockEnd,
                ImportStarted = true, SentToDiscord = true,
            });

            StringAssert.Contains(reply, "loading it into ace_world now");
            StringAssert.Contains(reply, "@ci 78790000");
            StringAssert.Contains(reply, "(also sent to Discord)");
            Assert.IsFalse(reply.Contains("@import-sql"), "no manual import step when it is already being loaded");
            AssertAscii(reply);
        }

        [TestMethod]
        public void BuildChatReply_OutsideBlock_ManualImportWithoutForce()
        {
            // Outside the block the import commands do not refuse, so force would be wrong advice; and the
            // command never auto-loads there, so the reply must ask for a reviewed manual import.
            var reply = TemplateExport.BuildChatReply(new TemplateExport.ReplyInfo
            {
                Name = "X", Wcid = 78780300, ClassName = "tmpl78780300_x", Flavour = TemplateExport.Flavour.Npc, OutsideBlock = true,
                SourceName = "Y", SourceWcid = 1, FilePath = "f.sql", SummaryLine = "s", BlockStart = BlockStart, BlockEnd = BlockEnd,
            });

            StringAssert.Contains(reply, "review the file, then @import-sql 78780300,");
            Assert.IsFalse(reply.Contains("force"));
        }

        [TestMethod]
        public void BuildChatReply_OutsideBlock_Warns()
        {
            var reply = TemplateExport.BuildChatReply(new TemplateExport.ReplyInfo
            {
                Name = "X", Wcid = 78780300, ClassName = "tmpl78780300_x", Flavour = TemplateExport.Flavour.Npc, OutsideBlock = true,
                SourceName = "Y", SourceWcid = 1, FilePath = "f.sql", SummaryLine = "s", BlockStart = BlockStart, BlockEnd = BlockEnd,
            });

            StringAssert.Contains(reply, "outside the export block");
            StringAssert.Contains(reply, "WCID_ALLOCATION_7878.md");
        }

        // ------------------------------------------------------------------------------------------------
        // Layered ObjDesc (a dressed player)
        // ------------------------------------------------------------------------------------------------

        private static TemplateExport.Snapshot LayeredSnapshot()
        {
            var s = PetSnapshot(withWielded: false);
            s.IsPet = false;
            // base body, then a shirt, then armour over both slots
            s.AnimParts = new List<PropertiesAnimPart>
            {
                new PropertiesAnimPart { Index = 1, AnimationId = 0x01000001 },
                new PropertiesAnimPart { Index = 2, AnimationId = 0x01000002 },
                new PropertiesAnimPart { Index = 1, AnimationId = 0x01000011 },
                new PropertiesAnimPart { Index = 1, AnimationId = 0x01000021 },
            };
            s.TextureChanges = new List<PropertiesTextureMap>
            {
                new PropertiesTextureMap { PartIndex = 0, OldTexture = 0x05000001, NewTexture = 0x05000001 },
                new PropertiesTextureMap { PartIndex = 1, OldTexture = 0x05000002, NewTexture = 0x05000003 },
                new PropertiesTextureMap { PartIndex = 0, OldTexture = 0x05000001, NewTexture = 0x05000009 },
            };
            s.SubPalettes = new List<PropertiesPalette>
            {
                new PropertiesPalette { SubPaletteId = 0x0100, Offset = 0, Length = 24 },  // skin
                new PropertiesPalette { SubPaletteId = 0x0200, Offset = 10, Length = 4 },  // shirt
                new PropertiesPalette { SubPaletteId = 0x0300, Offset = 12, Length = 16 }, // armour over the shirt
                new PropertiesPalette { SubPaletteId = 0x0100, Offset = 26, Length = 2 },  // lower id, drawn last over the armour
            };
            return s;
        }

        [TestMethod]
        public void BuildWeenie_LayeredLook_OneRowPerUniqueKey_TopLayerWins()
        {
            var w = TemplateExport.BuildWeenie(LayeredSnapshot(), 78790000, "tmpl78790000_x", "X", TemplateExport.Flavour.Npc, DateTime.UtcNow, out _);

            // the ace_world unique keys
            Assert.AreEqual(w.WeeniePropertiesAnimPart.Count, w.WeeniePropertiesAnimPart.Select(r => r.Index).Distinct().Count());
            Assert.AreEqual(w.WeeniePropertiesTextureMap.Count, w.WeeniePropertiesTextureMap.Select(r => (r.Index, r.OldId)).Distinct().Count());
            Assert.AreEqual(w.WeeniePropertiesPalette.Count, w.WeeniePropertiesPalette.Select(r => (r.SubPaletteId, r.Offset, r.Length)).Distinct().Count());

            Assert.AreEqual(0x01000021u, w.WeeniePropertiesAnimPart.Single(r => r.Index == 1).AnimationId);
            Assert.AreEqual(0x01000002u, w.WeeniePropertiesAnimPart.Single(r => r.Index == 2).AnimationId);
            Assert.AreEqual(0x05000009u, w.WeeniePropertiesTextureMap.Single(r => r.Index == 0).NewId);
            Assert.AreEqual(2, w.WeeniePropertiesTextureMap.Count);
        }

        [TestMethod]
        public void FlattenSubPalettes_ResolvesOverlapsInListOrder_IntoDisjointRuns()
        {
            var runs = TemplateExport.FlattenSubPalettes(LayeredSnapshot().SubPalettes);

            // owner of each block, as the client would paint it
            var owner = new Dictionary<int, uint>();
            foreach (var r in runs)
                for (var i = r.Offset; i < r.Offset + r.Length; i++)
                {
                    Assert.IsFalse(owner.ContainsKey(i), $"block {i} emitted twice");
                    owner[i] = r.SubPaletteId;
                }

            Assert.AreEqual(0x0100u, owner[0]);
            Assert.AreEqual(0x0200u, owner[10]);
            Assert.AreEqual(0x0200u, owner[11]);
            Assert.AreEqual(0x0300u, owner[12]);
            Assert.AreEqual(0x0300u, owner[25]);
            Assert.AreEqual(0x0100u, owner[26]);
            Assert.AreEqual(0x0100u, owner[27]);
            Assert.AreEqual(28, owner.Count);
        }

        [TestMethod]
        public void FlattenSubPalettes_FullPaletteOverride_SplitsAt255()
        {
            var runs = TemplateExport.FlattenSubPalettes(new List<PropertiesPalette>
            {
                new PropertiesPalette { SubPaletteId = 0x1234, Offset = 0, Length = 255 },
                new PropertiesPalette { SubPaletteId = 0x1234, Offset = 255, Length = 1 },
            });

            Assert.AreEqual(2, runs.Count);
            Assert.AreEqual(255, runs[0].Length);
            Assert.AreEqual(255, runs[1].Offset);
            Assert.AreEqual(1, runs[1].Length);
        }

        private static void AssertAscii(string text)
        {
            foreach (var c in text)
                Assert.IsTrue(c == '\n' || (c >= 0x20 && c <= 0x7E), $"non-ASCII char 0x{(int)c:X4} in: {text}");
        }
    }
}
