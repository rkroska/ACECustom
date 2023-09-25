using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Tests
{
    [TestClass]
    public class XPTests
    {
        [TestMethod]
        public void XPIsAccurate276()
        {
           double result = Player.GenerateDynamicLevelPostMax(276);
           Assert.AreEqual(194665023376, (long)result);
        }

        [TestMethod]
        public void XPIsAccurate277()
        {
            double result = Player.GenerateDynamicLevelPostMax(277);
            Assert.AreEqual(198152685222, (long)result);
        }

        [TestMethod]
        public void EarnXP()
        {
            var amount = 1000;
            var enchantment = 1.0;
            var quest = 1 + (100) * 0.005;
            var enlightenment = 1.0;
            var modifier = 1.0;

            var m_amount = (long)Math.Round(amount * enchantment * modifier * quest * enlightenment);
        }

        //[TestMethod]
        //public void AttributeXP190()
        //{
        //    double result = Player.GetXPCostByRank(190);
        //    Assert.AreEqual(308765680, (long)result);
        //}
        //[TestMethod]
        //public void AttributeXP191()
        //{
        //    double result = Player.GetXPCostByRank(191);
        //    Assert.AreEqual(332540637, (long)result);
        //}

        [TestMethod]
        public void AttributeXP192()
        {
            double result = Player.GetXPCostByRank(192);
            Assert.AreEqual(357000000, (long)result);
        }
    }
}
