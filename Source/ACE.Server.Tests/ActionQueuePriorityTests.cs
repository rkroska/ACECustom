using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACE.Server.Entity.Actions;

namespace ACE.Server.Tests
{
    [TestClass]
    public class ActionQueuePriorityTests
    {
        [TestMethod]
        public void TestPriorityOrder()
        {
            var queue = new ActionQueue();
            var resultHelper = "";

            // Enqueue Low priority actions
            queue.EnqueueAction(new ActionEventDelegate(ActionType.Container_Reset, () => resultHelper += "L1", ActionPriority.Low));
            queue.EnqueueAction(new ActionEventDelegate(ActionType.Container_Reset, () => resultHelper += "L2", ActionPriority.Low));

            // Enqueue High priority actions (after Low, but should run first)
            queue.EnqueueAction(new ActionEventDelegate(ActionType.Container_Reset, () => resultHelper += "H1", ActionPriority.High));
            queue.EnqueueAction(new ActionEventDelegate(ActionType.Container_Reset, () => resultHelper += "H2", ActionPriority.High));

            // Enqueue Normal priority actions
            queue.EnqueueAction(new ActionEventDelegate(ActionType.Container_Reset, () => resultHelper += "N1", ActionPriority.Normal));

            // Process
            queue.RunActions();

            // Expected: Highs, then Normals, then Lows
            // H1, H2, N1, L1, L2
            Assert.AreEqual("H1H2N1L1L2", resultHelper);
        }

        [TestMethod]
        public void TestExceptionHandling()
        {
            var queue = new ActionQueue();
            var resultHelper = "";

            // Action 1: Throws exception
            queue.EnqueueAction(new ActionEventDelegate(ActionType.Container_Reset, () => 
            { 
                throw new Exception("Boom"); 
            }, ActionPriority.High));

            // Action 2: Should still run
            queue.EnqueueAction(new ActionEventDelegate(ActionType.Container_Reset, () => resultHelper += "Success", ActionPriority.High));

            // Run
            try
            {
                queue.RunActions();
            }
            catch (Exception)
            {
                Assert.Fail("RunActions should not propagate exception");
            }

            Assert.AreEqual("Success", resultHelper);
        }
    }
}
