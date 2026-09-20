using System;
using System.Linq;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Services;
using ACE.Server.WorldObjects;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Coverage for the shared mutation-palette write in <see cref="PetMutationService"/>, which
    /// breeding, @mutate_pet and the Mutagenic Serum all rely on. These run without a world or DAT:
    /// a device with no VisualOverrideSetup takes the "no native base" path, so the base is left
    /// alone and only the template / captured-palette rule is exercised.
    /// </summary>
    [TestClass]
    public class PetMutationServiceTests
    {
        private const uint OldTemplate = 0x04000001;
        private const uint NewPalette = 0x0400001D;

        private static PetDevice CreateDevice()
        {
            var weenie = new Weenie
            {
                WeenieClassId = 787801001,
                ClassName = "TestPetDeviceForSerum",
                WeenieType = WeenieType.PetDevice,
            };
            return new PetDevice(weenie, new ObjectGuid(0xF0005257));
        }

        [TestMethod]
        public void ApplyMutationPalette_WritesTemplateAndClearsCapturedPalettes()
        {
            var device = CreateDevice();
            device.VisualOverridePaletteTemplate = (int)OldTemplate;
            device.SetProperty(PropertyString.CapturedObjDescPalettes, "0x0400000A:0:256");

            var r = PetMutationService.ApplyMutationPalette(device, null, NewPalette);

            Assert.AreEqual((int)NewPalette, device.VisualOverridePaletteTemplate);
            Assert.IsNull(device.GetProperty(PropertyString.CapturedObjDescPalettes), "captured palette rows block the recolour and must be removed");
            Assert.IsNull(device.VisualOverrideSetup, "a null setup must not write VisualOverrideSetup");
            Assert.IsNull(device.VisualOverridePaletteBase, "no setup means no native base, so the base is left alone");

            Assert.AreEqual(NewPalette, r.PaletteId);
            Assert.AreEqual(0u, r.SetupId);
            Assert.AreEqual((int)OldTemplate, r.OldPaletteTemplate);
            Assert.AreEqual((int)NewPalette, r.NewPaletteTemplate);
            Assert.AreEqual(0u, r.OldPaletteBase);
            Assert.AreEqual(0u, r.NewPaletteBase);
            Assert.IsFalse(r.NativeBaseApplied);
            Assert.IsTrue(r.CapturedPalettesCleared);
        }

        [TestMethod]
        public void ApplyMutationPalette_ReportsNothingClearedWhenNoCapturedPalettes()
        {
            var device = CreateDevice();

            var r = PetMutationService.ApplyMutationPalette(device, null, NewPalette);

            Assert.IsNull(r.OldPaletteTemplate);
            Assert.IsFalse(r.CapturedPalettesCleared);
            Assert.AreEqual((int)NewPalette, device.VisualOverridePaletteTemplate);
        }

        [TestMethod]
        public void ApplyMutationPalette_IsColourOnly()
        {
            var device = CreateDevice();
            device.SetProperty(PropertyInt.PetMutationCount, 3);
            device.SetProperty(PropertyBool.PetIsJuvenile, true);
            device.VisualOverrideScale = 1.25f;
            device.VisualOverrideShade = 0.4f;
            device.SetProperty(PropertyString.CapturedObjDescAnimParts, "anim");
            device.SetProperty(PropertyString.CapturedObjDescTextures, "tex");

            PetMutationService.ApplyMutationPalette(device, null, NewPalette);

            Assert.AreEqual(3, device.GetProperty(PropertyInt.PetMutationCount));
            Assert.AreEqual(true, device.GetProperty(PropertyBool.PetIsJuvenile));
            Assert.AreEqual(1.25f, device.VisualOverrideScale);
            Assert.AreEqual(0.4f, device.VisualOverrideShade);
            Assert.AreEqual("anim", device.GetProperty(PropertyString.CapturedObjDescAnimParts), "anim-part rows are not touched");
            Assert.AreEqual("tex", device.GetProperty(PropertyString.CapturedObjDescTextures), "texture rows are not touched");
        }

        [TestMethod]
        public void ApplyMutationPalette_RejectsZeroPalette()
        {
            var device = CreateDevice();
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PetMutationService.ApplyMutationPalette(device, null, 0));
            Assert.IsNull(device.VisualOverridePaletteTemplate);
        }

        [TestMethod]
        public void MutagenicSerumWcid_SkipsTheReservedReroller()
        {
            // 78780256 is the reserved, unbuilt Ancestral Gene Re-roller (docs/WCID_ALLOCATION_7878.md).
            Assert.AreEqual(78780257u, PetMutationService.MutagenicSerumWcid);
            Assert.AreNotEqual(78780256u, PetMutationService.MutagenicSerumWcid);
        }

        // =============================================================================
        // Colour-change visibility.
        //
        // A texture replacement hides the palette for the ONE part it lands on. The old rule
        // refused any essence that carried a texture at all, which blocked the Mutagenic Serum
        // on 784 of the live shard's essences - 44 of them wrongly, including a Sawato Bandit
        // with three replacements across 34 parts that recolours perfectly well.
        // =============================================================================

        private const string SawatoBanditTextures =
            "16:83886668:83890487,16:83886837:83890522,16:83886684:83890642";

        /// <summary>The 34 anim parts from the live @petdesc dump of the reported pet.</summary>
        private const string SawatoBanditAnimParts =
            "16:16795640,0:16789975,1:16789977,2:16789980,5:16789978,6:16789979,9:16789970," +
            "10:16789972,11:16789974,13:16789971,14:16789973,3:16789983,7:16789982,4:16789981," +
            "8:16789987,12:16777304,15:16777307,17:16777708,18:16777708,19:16777708,20:16777708," +
            "21:16777708,22:16777708,23:16777708,24:16777708,25:16777708,26:16777708,27:16777708," +
            "28:16777708,29:16777708,30:16777708,31:16777708,32:16777708,33:16777708";

        [TestMethod]
        public void NoCapturedTextures_ColourIsVisible()
        {
            var v = PetMutationService.GetColourChangeVisibility(null, null);
            Assert.AreEqual(PetMutationService.ColourCoverage.Visible, v.Coverage);
            Assert.IsFalse(v.BlocksColour);
            Assert.AreEqual(0, v.TextureCount);
        }

        [TestMethod]
        public void ReportedSawatoBandit_ColourIsVisible()
        {
            // The pet from the bug report: 3 textures, all on part 16, out of 34 parts.
            var v = PetMutationService.GetColourChangeVisibility(SawatoBanditTextures, SawatoBanditAnimParts);

            Assert.AreEqual(PetMutationService.ColourCoverage.Visible, v.Coverage);
            Assert.IsFalse(v.BlocksColour, "the serum must not refuse a pet that @mutate_pet recolours correctly");
            Assert.AreEqual(3, v.TextureCount);
            Assert.AreEqual(1, v.TexturedParts, "all three replacements land on part 16");
            Assert.AreEqual(34, v.TotalParts);
        }

        [TestMethod]
        public void EveryPartRetextured_ColourIsHidden()
        {
            var textures = "0:1:2,1:1:2,2:1:2,3:1:2";
            var animParts = "0:10,1:11,2:12,3:13";

            var v = PetMutationService.GetColourChangeVisibility(textures, animParts);
            Assert.AreEqual(PetMutationService.ColourCoverage.Hidden, v.Coverage);
            Assert.IsTrue(v.BlocksColour);
            Assert.AreEqual(4, v.TexturedParts);
            Assert.AreEqual(4, v.TotalParts);
        }

        [TestMethod]
        public void NinetyPercentRetextured_ColourIsHidden()
        {
            // 9 of 10 parts covered: at the threshold, so still refused.
            var textures = string.Join(",", System.Linq.Enumerable.Range(0, 9).Select(i => i + ":1:2"));
            var animParts = string.Join(",", System.Linq.Enumerable.Range(0, 10).Select(i => i + ":10"));

            var v = PetMutationService.GetColourChangeVisibility(textures, animParts);
            Assert.AreEqual(PetMutationService.ColourCoverage.Hidden, v.Coverage);
        }

        [TestMethod]
        public void HalfRetextured_ColourIsVisible()
        {
            // 5 of 10 parts: half the body still tints, so the serum is allowed.
            var textures = string.Join(",", System.Linq.Enumerable.Range(0, 5).Select(i => i + ":1:2"));
            var animParts = string.Join(",", System.Linq.Enumerable.Range(0, 10).Select(i => i + ":10"));

            var v = PetMutationService.GetColourChangeVisibility(textures, animParts);
            Assert.AreEqual(PetMutationService.ColourCoverage.Visible, v.Coverage);
            Assert.IsFalse(v.BlocksColour);
        }

        [TestMethod]
        public void TexturesButNoPartList_CoverageIsUnknown()
        {
            // 101 live shard devices look like this. Unknown must not be treated as Hidden:
            // the caller warns and proceeds rather than blocking the item outright.
            var v = PetMutationService.GetColourChangeVisibility(SawatoBanditTextures, null);

            Assert.AreEqual(PetMutationService.ColourCoverage.Unknown, v.Coverage);
            Assert.IsFalse(v.BlocksColour, "undecidable must not block the serum");
            Assert.AreEqual(3, v.TextureCount);
            Assert.AreEqual(0, v.TotalParts);
        }

        [TestMethod]
        public void RepeatedTexturesOnOnePart_CountAsOnePart()
        {
            // Three replacements on the same part hide that one part, not three.
            var v = PetMutationService.GetColourChangeVisibility("7:1:2,7:3:4,7:5:6", "7:10,8:11,9:12,10:13");

            Assert.AreEqual(3, v.TextureCount);
            Assert.AreEqual(1, v.TexturedParts);
            Assert.AreEqual(PetMutationService.ColourCoverage.Visible, v.Coverage);
        }

        [TestMethod]
        public void TexturesOnPartsTheCaptureDoesNotDefine_AreIgnored()
        {
            // A texture on a part with no anim-part row cannot hide anything that renders.
            var v = PetMutationService.GetColourChangeVisibility("99:1:2,0:1:2", "0:10,1:11,2:12,3:13");

            Assert.AreEqual(1, v.TexturedParts, "part 99 is not in the capture's part list");
            Assert.AreEqual(4, v.TotalParts);
            Assert.AreEqual(PetMutationService.ColourCoverage.Visible, v.Coverage);
        }

        [TestMethod]
        public void MalformedEntries_AreSkippedWithoutThrowing()
        {
            // Junk entries are counted but contribute no part index, so they cannot hide anything.
            // "5" has no colon, ":::" has no number, "notanumber" parses to nothing.
            var v = PetMutationService.GetColourChangeVisibility(",,notanumber,5,:::,6:1:2,", "6:10,7:11");

            Assert.AreEqual(4, v.TextureCount, "four non-empty entries");
            Assert.AreEqual(1, v.TexturedParts, "only 6:1:2 yields a part in the capture's list");
            Assert.AreEqual(2, v.TotalParts);
            Assert.AreEqual(PetMutationService.ColourCoverage.Visible, v.Coverage);
        }
    }
}
