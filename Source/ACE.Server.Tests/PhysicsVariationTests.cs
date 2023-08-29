using ACE.Common;
using ACE.DatLoader;
using ACE.Server.Physics.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Tests
{
    [TestClass]
    public class PhysicsVariationTests
    {
        [TestMethod]
        public void GetCellTest()
        {
            ConfigManager.Initialize(@"D:\Code\ACE\Source\ACE.Server\bin\x64\Debug\net6.0\Config.js");
            DatManager.Initialize(ConfigManager.Config.Server.DatFilesDirectory, false, true, true);
            var cell = DBObj.GetEnvCell(3880648731, null);
            Assert.AreNotEqual<uint>(0, cell.CellStructureID);
        }
    }
}
