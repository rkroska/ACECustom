using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Quiver looks size every model to about QuiverLook.LookSizeM across, so it flies (and hits) like an arrow.
    /// The target matches the creature-model infinite arrows the admins sized by hand. Pure: no database, no dat.
    /// </summary>
    [TestClass]
    public class QuiverLookTests
    {
        /// <summary>A model is scaled to LookSizeM across its largest dimension.</summary>
        [TestMethod]
        public void ScaleForSize_MakesTheModelLookSizeAcross()
        {
            // a 1.5 m mosswart -> 0.5 m (the admins' Infinite Mosswarts: 1.5 m x 0.32 = 0.48 m)
            Assert.AreEqual(QuiverLook.LookSizeM / 1.5f, QuiverLook.ScaleForSize(1.5f), 1e-6f);
            Assert.AreEqual(QuiverLook.LookSizeM, QuiverLook.ScaleForSize(3.2f) * 3.2f, 1e-5f);
        }

        /// <summary>The stored scale is not clamped (no double clamping); the shot's scale is held to the limits.</summary>
        [TestMethod]
        public void ScaleForSize_IsNotClamped_ProjectileScaleIs()
        {
            Assert.AreEqual(25f, QuiverLook.ScaleForSize(0.02f), 1e-4f);      // a tiny model: 25x, stored as is
            Assert.AreEqual(0.001f, QuiverLook.ScaleForSize(500f), 1e-6f);    // a huge model: 0.001, stored as is
            Assert.AreEqual(QuiverLook.MaxScale, QuiverLook.ProjectileScale(25.0, QuiverLook.LookSizeM));    // can't balloon
            Assert.AreEqual(QuiverLook.MinScale, QuiverLook.ProjectileScale(0.001, QuiverLook.LookSizeM));   // can't vanish
            // a 50 m model at a 1.5 m look: 0.01 x 3 = 0.03, not the 0.06 a pre-clamped 0.02 would give
            Assert.AreEqual(0.03f, QuiverLook.ProjectileScale(QuiverLook.ScaleForSize(50f), 1.5), 1e-5f);
        }

        /// <summary>The projectile setting maps each AmmoType to a weenie; zero and junk entries are skipped.</summary>
        [TestMethod]
        public void ParseProjectiles_MapsAmmoTypesToWeenies()
        {
            var map = QuiverLook.ParseProjectiles("1:4395100, 2:4395101,4:4395102,8:0,x:5,16");
            Assert.AreEqual(3, map.Count);
            Assert.AreEqual(4395100u, map[1]);
            Assert.AreEqual(4395102u, map[4]);
            Assert.IsFalse(map.ContainsKey(8));
        }

        /// <summary>A stored scale is rescaled to the live size setting at shot time; bad input falls back.</summary>
        [TestMethod]
        public void ProjectileScale_RescalesToTheLiveSize()
        {
            // a look stored for 0.5 m, shot at 0.75 m, is 1.5x bigger; at 0.5 m it is unchanged
            Assert.AreEqual(0.48f, QuiverLook.ProjectileScale(0.32, 0.75), 1e-5f);
            Assert.AreEqual(0.32f, QuiverLook.ProjectileScale(0.32, QuiverLook.LookSizeM), 1e-5f);
            Assert.AreEqual(QuiverLook.MaxScale, QuiverLook.ProjectileScale(2.5, 10.0));
            Assert.AreEqual(0.3f, QuiverLook.ProjectileScale(null, 0.75));
            Assert.AreEqual(0.32f, QuiverLook.ProjectileScale(0.32, 0), 1e-5f);   // a bad setting keeps the stored size
        }

        /// <summary>The per-creature size overrides setting keeps good pairs and skips junk.</summary>
        [TestMethod]
        public void ParseSizeOverrides_ReadsPairsAndSkipsJunk()
        {
            var map = QuiverLook.ParseSizeOverrides(" 1234:1.5, 5678:0.7 ,bad,9:0,10:-2,11:x,12:2 ");
            Assert.AreEqual(3, map.Count);
            Assert.AreEqual(1.5, map[1234], 1e-9);
            Assert.AreEqual(0.7, map[5678], 1e-9);
            Assert.AreEqual(2.0, map[12], 1e-9);
            Assert.AreEqual(0, QuiverLook.ParseSizeOverrides(null).Count);
        }

        /// <summary>Arrow stones step in tenths between 50% and 200% and round-trip to exactly 1.0.</summary>
        [TestMethod]
        public void ArrowStones_StepInTenths_Between50And200Percent()
        {
            Assert.IsTrue(QuiverLook.TryStepArrow(1.0, true, out var up));
            Assert.AreEqual(1.1, up, 1e-9);
            Assert.IsTrue(QuiverLook.TryStepArrow(0.6, false, out var down));
            Assert.AreEqual(0.5, down, 1e-9);
            Assert.IsFalse(QuiverLook.TryStepArrow(0.5, false, out _));   // floor: the stone is not used
            Assert.IsFalse(QuiverLook.TryStepArrow(2.0, true, out _));    // ceiling

            // ten up and ten down land back on exactly 1.0
            var level = 1.0;
            for (var i = 0; i < 10; i++) QuiverLook.TryStepArrow(level, true, out level);
            for (var i = 0; i < 10; i++) QuiverLook.TryStepArrow(level, false, out level);
            Assert.AreEqual(1.0, level, 1e-12);
        }

        /// <summary>A size that can't be measured gives the fallback scale.</summary>
        [TestMethod]
        public void ScaleForSize_BadSizeFallsBack()
        {
            Assert.AreEqual(0.3f, QuiverLook.ScaleForSize(0f));
            Assert.AreEqual(0.3f, QuiverLook.ScaleForSize(float.NaN));
        }
    }
}
