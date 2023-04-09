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
    }
}
