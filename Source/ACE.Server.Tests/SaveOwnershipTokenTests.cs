using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Regression cover for the DeepSave save-flag ownership rule (PR #514, 2026-09-09).
    ///
    /// DeepSave's callback clears SaveInProgress on objects it never raised the flag on, so it must not
    /// clear a flag a NEWER save raised after DeepSave enqueued. The first cut compared DateTime.UtcNow
    /// stamps; CodeRabbit pointed out UtcNow can repeat within its resolution (two stamps in the same
    /// instant read as "not newer") and can step backwards under NTP. Ownership is now a monotonic
    /// token from one process-wide counter, and this pins the comparison the callback relies on.
    ///
    /// The predicate is tested directly rather than through a live WorldObject: constructing one drags in
    /// the biota/physics stack, and the rule under test is the comparison, not the plumbing around it.
    /// </summary>
    [TestClass]
    public class SaveOwnershipTokenTests
    {
        [TestMethod]
        public void FlagStampedBeforeCutoff_IsOursToClear()
        {
            // A save that raised its flag before the callback captured the cutoff is ahead of that
            // callback in the single-threaded queue, or was orphaned - either way the callback clears it.
            Assert.IsTrue(WorldObject.SaveFlagOwnedAtOrBefore(token: 41, cutoff: 42));
        }

        [TestMethod]
        public void FlagStampedAtCutoff_IsOursToClear()
        {
            // The "same instant" case the timestamp version got wrong: a flag stamped by the very last
            // save before the cutoff was read carries the cutoff value itself, and it is older, not newer.
            Assert.IsTrue(WorldObject.SaveFlagOwnedAtOrBefore(token: 42, cutoff: 42));
        }

        [TestMethod]
        public void FlagStampedAfterCutoff_BelongsToNewerSave()
        {
            // Raised after the enqueue: a newer save is in flight and its own callback clears it.
            Assert.IsFalse(WorldObject.SaveFlagOwnedAtOrBefore(token: 43, cutoff: 42));
        }

        [TestMethod]
        public void NeverStamped_IsOursToClear()
        {
            // An object that has never been through SaveBiotaToDatabase carries token 0 - the stranded
            // flag rescue DeepSave exists for must still reach it.
            Assert.IsTrue(WorldObject.SaveFlagOwnedAtOrBefore(token: 0, cutoff: 42));
        }

        [TestMethod]
        public void CurrentSaveToken_IsMonotonic()
        {
            // The cutoff a callback captures can only move forward, whatever the wall clock does.
            var a = WorldObject.CurrentSaveToken;
            var b = WorldObject.CurrentSaveToken;
            Assert.IsTrue(b >= a);
        }
    }
}
