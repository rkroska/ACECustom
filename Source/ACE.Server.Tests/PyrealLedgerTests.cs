using ACE.Server.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Pyreal ledger rules that decide who gets flagged. A wrong answer here accuses a player or hides a dupe, so each
    /// startup classification and the "what did that save include" bookkeeping is pinned down.
    /// </summary>
    [TestClass]
    public class PyrealLedgerTests
    {
        // ---- startup classification ----------------------------------------------------------------------------

        [TestMethod]
        public void LoadedEqualsLive_IsClean()
        {
            Assert.AreEqual(PyrealLedger.Classification.Clean, PyrealLedger.Classify(loaded: 500, live: 500, saved: 300, pendingNotes: ""));
        }

        [TestMethod]
        public void LoadedEqualsLiveAndSaved_IsClean()
        {
            Assert.AreEqual(PyrealLedger.Classification.Clean, PyrealLedger.Classify(500, 500, 500, ""));
        }

        [TestMethod]
        public void RolledBackSpend_IsRollbackGain()
        {
            // bought something for 200 after the last save, then the server crashed: they kept the 200
            Assert.AreEqual(PyrealLedger.Classification.RollbackGain, PyrealLedger.Classify(loaded: 1000, live: 800, saved: 1000, pendingNotes: "VendorBuy:Hugh:-200"));
        }

        [TestMethod]
        public void RolledBackOfflineTransfer_IsLikelyDupe()
        {
            // sent 1M to an offline alt (saved at once), crash before the sender saved: both have the 1M
            Assert.AreEqual(PyrealLedger.Classification.LikelyDupe, PyrealLedger.Classify(loaded: 1_000_000, live: 0, saved: 1_000_000, pendingNotes: "!Transfer:Alt:-1000000"));
        }

        [TestMethod]
        public void DupeMarkerAnywhereInTheLostChanges_IsLikelyDupe()
        {
            Assert.AreEqual(PyrealLedger.Classification.LikelyDupe, PyrealLedger.Classify(5000, 1000, 5000, "VendorBuy:Hugh:-1000|!Withdraw::-3000"));
        }

        [TestMethod]
        public void RolledBackCredit_IsRollbackLoss()
        {
            Assert.AreEqual(PyrealLedger.Classification.RollbackLoss, PyrealLedger.Classify(loaded: 100, live: 900, saved: 100, pendingNotes: "VendorSell:Merchant:800"));
        }

        [TestMethod]
        public void LoadedMatchesNeither_IsUnexplained()
        {
            Assert.AreEqual(PyrealLedger.Classification.Unexplained, PyrealLedger.Classify(loaded: 9_000_000, live: 100, saved: 100, pendingNotes: ""));
        }

        [TestMethod]
        public void NewCharacterRowMissing_DefaultsCatchAnEditedBalance()
        {
            // Initialize treats a character with no state row as live = saved = 0
            Assert.AreEqual(PyrealLedger.Classification.Clean, PyrealLedger.Classify(0, 0, 0, ""));
            Assert.AreEqual(PyrealLedger.Classification.Unexplained, PyrealLedger.Classify(50_000, 0, 0, ""));
        }

        [TestMethod]
        public void NullNotes_DoNotThrow()
        {
            Assert.AreEqual(PyrealLedger.Classification.RollbackGain, PyrealLedger.Classify(10, 5, 10, null));
        }

        // ---- what a save included --------------------------------------------------------------------------------

        private static PyrealLedger.CharState StateWith(long saved, params (string text, long after)[] notes)
        {
            var state = new PyrealLedger.CharState { Saved = saved, Live = notes.Length > 0 ? notes[^1].after : saved };
            foreach (var (text, after) in notes)
                state.PendingNotes.Add(new PyrealLedger.PendingNote(text, after));
            return state;
        }

        [TestMethod]
        public void SaveOfTheLiveBalance_ClearsEveryNote()
        {
            var state = StateWith(100, ("VendorSell:A:50", 150), ("!Transfer:B:-100", 50));
            state.ApplySave(50);

            Assert.AreEqual(0, state.PendingNotes.Count);
            Assert.AreEqual(50, state.Saved);
        }

        [TestMethod]
        public void SaveInTheMiddle_KeepsOnlyLaterNotes()
        {
            // the save captured 150 (after the sale); the transfer after it is still unsaved
            var state = StateWith(100, ("VendorSell:A:50", 150), ("!Transfer:B:-100", 50));
            state.ApplySave(150);

            Assert.AreEqual(1, state.PendingNotes.Count);
            Assert.AreEqual("!Transfer:B:-100", state.PendingNotes[0].Text);
        }

        [TestMethod]
        public void SaveBeforeAnyNote_KeepsThemAll()
        {
            var state = StateWith(100, ("VendorSell:A:50", 150), ("VendorBuy:C:-20", 130));
            state.ApplySave(100);

            Assert.AreEqual(2, state.PendingNotes.Count);
        }

        [TestMethod]
        public void SavedTransferThenCrash_IsNotADupe()
        {
            // the transfer was saved, a later purchase was not: losing a purchase is a RollbackGain, not a LikelyDupe
            var state = StateWith(1000, ("!Transfer:Alt:-500", 500), ("VendorBuy:Hugh:-100", 400));
            state.ApplySave(500);

            Assert.AreEqual(PyrealLedger.Classification.RollbackGain,
                PyrealLedger.Classify(loaded: state.Saved, live: state.Live, saved: state.Saved, pendingNotes: state.NotesText));
        }

        [TestMethod]
        public void UnsavedTransferThenCrash_IsADupe()
        {
            var state = StateWith(1000, ("!Transfer:Alt:-500", 500));

            Assert.AreEqual(PyrealLedger.Classification.LikelyDupe,
                PyrealLedger.Classify(loaded: state.Saved, live: state.Live, saved: state.Saved, pendingNotes: state.NotesText));
        }

        // ---- source scopes ---------------------------------------------------------------------------------------

        [TestMethod]
        public void Scopes_NestAndRestore()
        {
            Assert.IsNull(PyrealLedger.CurrentSource);

            using (PyrealLedger.Begin(PyrealLedger.SrcCommand))
            {
                Assert.AreEqual(PyrealLedger.SrcCommand, PyrealLedger.CurrentSource);

                using (PyrealLedger.Begin(PyrealLedger.SrcDeposit))
                    Assert.AreEqual(PyrealLedger.SrcDeposit, PyrealLedger.CurrentSource);

                Assert.AreEqual(PyrealLedger.SrcCommand, PyrealLedger.CurrentSource);
            }

            Assert.IsNull(PyrealLedger.CurrentSource);
        }

        [TestMethod]
        public void BeginIfNone_DoesNotOverrideAnOpenScope()
        {
            using (PyrealLedger.Begin(PyrealLedger.SrcVendorSell))
            {
                using (PyrealLedger.BeginIfNone(PyrealLedger.SrcCommand))
                    Assert.AreEqual(PyrealLedger.SrcVendorSell, PyrealLedger.CurrentSource);

                Assert.AreEqual(PyrealLedger.SrcVendorSell, PyrealLedger.CurrentSource);
            }

            using (PyrealLedger.BeginIfNone(PyrealLedger.SrcCommand))
                Assert.AreEqual(PyrealLedger.SrcCommand, PyrealLedger.CurrentSource);

            Assert.IsNull(PyrealLedger.CurrentSource);
        }

        [TestMethod]
        public void CurrencyWcids_AreCoinsNotesAndPeas()
        {
            Assert.IsTrue(PyrealLedger.IsCurrency(273));
            Assert.IsTrue(PyrealLedger.IsCurrency(20630));
            Assert.IsTrue(PyrealLedger.IsCurrency(8330));
            Assert.IsFalse(PyrealLedger.IsCurrency(300004)); // Enlightened Coin is its own currency
        }
    }
}
