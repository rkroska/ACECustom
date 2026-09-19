using System;
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
    }
}
