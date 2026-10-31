using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>@item-name (owner 2026-10-30): which names and items qualify, rares keep their tier word, and a player-chosen
    /// name survives an element change.</summary>
    [TestClass]
    public class ItemRenameTests
    {
        private static uint nextGuid = 0xF0005800;

        private static MeleeWeapon Weapon(string name)
        {
            var weenie = new Weenie { WeenieClassId = 458000, ClassName = "TestRenameWeapon", WeenieType = WeenieType.MeleeWeapon };
            var w = new MeleeWeapon(weenie, new ObjectGuid(nextGuid++));
            w.Name = name;
            w.ItemType = ItemType.MeleeWeapon;
            return w;
        }

        // ---- names ---------------------------------------------------------------------------------

        [TestMethod]
        public void PlainNames_AreAccepted()
        {
            Assert.IsNull(ItemRename.ValidateName("Frost Fang"));
            Assert.IsNull(ItemRename.ValidateName("Bob's Big-Stick 2"));
            Assert.IsNull(ItemRename.ValidateName("Axe"));
            Assert.IsNull(ItemRename.ValidateName(new string('a', ItemRename.MaxNameLength)));
        }

        [TestMethod]
        public void BadLengthsAndCharacters_AreRefused()
        {
            Assert.IsNotNull(ItemRename.ValidateName("Ax"));
            Assert.IsNotNull(ItemRename.ValidateName(new string('a', ItemRename.MaxNameLength + 1)));
            Assert.IsNotNull(ItemRename.ValidateName("Sword—of Doom"));   // em dash: the client cannot draw it
            Assert.IsNotNull(ItemRename.ValidateName("Sword_of_Doom"));
            Assert.IsNotNull(ItemRename.ValidateName("Big  Gap"));
            Assert.IsNotNull(ItemRename.ValidateName(""));
            Assert.IsNotNull(ItemRename.ValidateName(null));
        }

        [TestMethod]
        public void RarePrefixWords_AreRefused_AsWholeWordsOnly()
        {
            Assert.IsNotNull(ItemRename.ValidateName("Pristine Fang"));
            Assert.IsNotNull(ItemRename.ValidateName("my ASCENDANT blade"));
            Assert.IsNull(ItemRename.ValidateName("Pristinely Sharp"));
        }

        // ---- items ---------------------------------------------------------------------------------

        [TestMethod]
        public void WeaponsAndCasters_AreRenamable_StacksAndArmorAreNot()
        {
            Assert.IsTrue(ItemRename.IsRenamable((int)ItemType.MeleeWeapon, (int)WeenieType.MeleeWeapon, null));
            Assert.IsTrue(ItemRename.IsRenamable((int)ItemType.MissileWeapon, (int)WeenieType.MissileLauncher, 1));
            Assert.IsTrue(ItemRename.IsRenamable((int)ItemType.Caster, (int)WeenieType.Caster, null));

            Assert.IsFalse(ItemRename.IsRenamable((int)ItemType.Armor, (int)WeenieType.Clothing, null));
            Assert.IsFalse(ItemRename.IsRenamable((int)ItemType.MissileWeapon, (int)WeenieType.Ammunition, 250));
            Assert.IsFalse(ItemRename.IsRenamable((int)ItemType.MissileWeapon, (int)WeenieType.Missile, 1));
            Assert.IsFalse(ItemRename.IsRenamable((int)ItemType.MeleeWeapon, (int)WeenieType.MeleeWeapon, 20));
            Assert.IsFalse(ItemRename.IsRenamable(null, (int)WeenieType.MeleeWeapon, null));
        }

        // ---- rares ---------------------------------------------------------------------------------

        [TestMethod]
        public void RealRares_KeepTheirTierWord()
        {
            Assert.AreEqual("Frost Fang", ItemRename.FinalName("Frost Fang", null));
            Assert.AreEqual("Frost Fang", ItemRename.FinalName("Frost Fang", 0));
            Assert.AreEqual("Pristine Frost Fang", ItemRename.FinalName("Frost Fang", (int)ZoneRareTier.Pristine));
            Assert.AreEqual("Ascendant Frost Fang", ItemRename.FinalName("Frost Fang", (int)ZoneRareTier.Ascendant));
            Assert.AreEqual("Frost Fang", ItemRename.FinalName("Frost Fang", 99));
        }

        // ---- denial reasons ---------------------------------------------------------------------

        [TestMethod]
        public void DenialReason_IsAsciiWithoutTrailingPeriods()
        {
            Assert.AreEqual("bad name", ItemRename.ReasonForPlayer("bad name"));
            Assert.AreEqual("bad name", ItemRename.ReasonForPlayer("  bad name... "));
            Assert.AreEqual("too long - try again", ItemRename.ReasonForPlayer("too long — try again"));
            Assert.AreEqual("line one line two", ItemRename.ReasonForPlayer("line one\r\nline two"));
            Assert.IsNull(ItemRename.ReasonForPlayer("..."));
            Assert.IsNull(ItemRename.ReasonForPlayer(null));
        }

        // ---- appraisal and element changes ---------------------------------------------------------

        [TestMethod]
        public void OriginalNameLine_ShowsOnlyForRenamedItems()
        {
            var plain = Weapon("Frost Ono");
            Assert.IsNull(ItemRename.OriginalNameLine(plain));

            var renamed = Weapon("Frost Fang");
            renamed.SetProperty(PropertyString.OriginalItemName, "Frost Ono");
            Assert.AreEqual("- Originally: Frost Ono", ItemRename.OriginalNameLine(renamed));

            var nonAscii = Weapon("Frost Fang");
            nonAscii.SetProperty(PropertyString.OriginalItemName, "Ono™");
            Assert.IsNull(ItemRename.OriginalNameLine(nonAscii));
        }

        [TestMethod]
        public void ElementChange_RenamesStockNames_ButNotPlayerChosenOnes()
        {
            var stock = Weapon("Frost Ono");
            GearEssences.RenameForElement(stock, DamageType.Fire);
            Assert.AreEqual("Flaming Ono", stock.Name);

            var custom = Weapon("Frost Fang");
            custom.SetProperty(PropertyString.OriginalItemName, "Frost Ono");
            GearEssences.RenameForElement(custom, DamageType.Fire);
            Assert.AreEqual("Frost Fang", custom.Name);
        }
    }
}
