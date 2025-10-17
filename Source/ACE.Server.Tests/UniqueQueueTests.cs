using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACE.Database;
namespace ACE.Database.Tests
{
    [TestClass]
    public class UniqueQueueTests
    {
        [TestMethod]
        public void Enqueue_WithCustomObjects_ShouldMaintainUniquenessByKey()
        {
            // Arrange
            using var queue = new UniqueQueue<QueueItem, int>(item => item.Id);

            // Act
            queue.Enqueue(new QueueItem { Id = 1, Data = "First" });
            queue.Enqueue(new QueueItem { Id = 2, Data = "Second" });
            queue.Enqueue(new QueueItem { Id = 1, Data = "Updated First" }); // Should replace existing

            // Assert
            Assert.AreEqual(2, queue.Count);
            var items = queue.DequeueBatch(queue.Count).ToList();
            Assert.AreEqual(2, items[0].Id);
            Assert.AreEqual("Second", items[0].Data);
            Assert.AreEqual(1, items[1].Id);
            Assert.AreEqual("Updated First", items[1].Data);
        }

        [TestMethod]
        public void Enqueue_WithStrings_ShouldMaintainUniquenessByValue()
        {
            // Arrange
            using var queue = new UniqueQueue<string, string>(item => item);

            // Act
            queue.Enqueue("apple");
            queue.Enqueue("banana");
            queue.Enqueue("apple"); // Should move apple to end

            // Assert
            Assert.AreEqual(2, queue.Count);
            var items = queue.DequeueBatch(queue.Count).ToList();
            Assert.AreEqual("banana", items[0]);
            Assert.AreEqual("apple", items[1]);
        }

        [TestMethod]
        public void Remove_WithValidId_ShouldRemoveItem()
        {
            // Arrange
            using var queue = new UniqueQueue<QueueItem, int>(item => item.Id);
            queue.Enqueue(new QueueItem { Id = 1, Data = "First" });
            queue.Enqueue(new QueueItem { Id = 2, Data = "Second" });

            // Act
            bool removed = queue.Remove(2);

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(1, queue.Count);
            var remainingItem = queue.Dequeue();
            Assert.AreEqual(1, remainingItem.Id);
            Assert.AreEqual("First", remainingItem.Data);
        }

        [TestMethod]
        public void Dequeue_ShouldReturnItemsInCorrectOrder()
        {
            // Arrange
            using var queue = new UniqueQueue<QueueItem, int>(item => item.Id);
            queue.Enqueue(new QueueItem { Id = 1, Data = "First" });
            queue.Enqueue(new QueueItem { Id = 2, Data = "Second" });
            queue.Enqueue(new QueueItem { Id = 1, Data = "Updated First" });

            // Act & Assert
            var first = queue.Dequeue();
            Assert.AreEqual(2, first.Id);
            Assert.AreEqual("Second", first.Data);

            var second = queue.Dequeue();
            Assert.AreEqual(1, second.Id);
            Assert.AreEqual("Updated First", second.Data);

            Assert.AreEqual(0, queue.Count);
        }
    }

    internal class QueueItem
    {
        public int Id { get; internal set; }
        public string Data { get; internal set; }
    }
}
