using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Database
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    public class UniqueQueue<TItem, TKey> : IDisposable where TKey : notnull
    {
        private readonly LinkedList<TItem> _items = new LinkedList<TItem>();
        private readonly Dictionary<TKey, LinkedListNode<TItem>> _lookup = new Dictionary<TKey, LinkedListNode<TItem>>();
        private readonly Func<TItem, TKey> _keySelector;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public UniqueQueue(Func<TItem, TKey> keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        }

        private void EnqueueLocked(TItem item)
        {
            var key = _keySelector(item);

            if (_lookup.TryGetValue(key, out LinkedListNode<TItem> existingNode))
            {
                // An item with this key exists, so update the value and move the existing node to the end
                existingNode.Value = item;
                _items.Remove(existingNode);
                _items.AddLast(existingNode);
            }
            else
            {
                // An item with this key doesn't exist, so add it.
                var newNode = _items.AddLast(item);
                _lookup[key] = newNode;
            }
        }

        public bool Enqueue(TItem item)
        {
            _lock.EnterWriteLock();
            try
            {
                EnqueueLocked(item);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryDequeue(out TItem item)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_items.Count == 0)
                {
                    item = default(TItem);
                    return false;
                }

                var node = _items.First;
                item = node.Value;
                var key = _keySelector(item);

                _items.RemoveFirst();
                _lookup.Remove(key);

                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public TItem Dequeue()
        {
            if (TryDequeue(out TItem item))
                return item;

            throw new InvalidOperationException("Queue is empty");
        }

        public bool Remove(TKey uniqueId)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_lookup.TryGetValue(uniqueId, out var node))
                {
                    _items.Remove(node);
                    _lookup.Remove(uniqueId);
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryPeek(out TItem item)
        {
            _lock.EnterReadLock();
            try
            {
                if (_items.Count > 0)
                {
                    item = _items.First.Value;
                    return true;
                }
                item = default(TItem);
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool Contains(TKey uniqueId)
        {
            if (uniqueId is null) return false;
            _lock.EnterReadLock();
            try
            {
                return _lookup.ContainsKey(uniqueId);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryGetItem(TKey uniqueId, out TItem item)
        {
            if (uniqueId is null)
            {
                item = default;
                return false;
            }

            _lock.EnterReadLock();
            try
            {
                if (_lookup.TryGetValue(uniqueId, out var node))
                {
                    item = node.Value;
                    return true;
                }
                item = default(TItem);
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _items.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _items.Clear();
                _lookup.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        // Thread-safe enumeration - returns a snapshot
        public TItem[] ToArray()
        {
            _lock.EnterReadLock();
            try
            {
                return _items.ToArray();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // Get all unique IDs currently in queue
        public TKey[] GetAllIds()
        {
            _lock.EnterReadLock();
            try
            {
                return _lookup.Keys.ToArray();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // Batch operations for better performance when adding multiple items
        public void EnqueueBatch(IEnumerable<TItem> items)
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (TItem item in items)
                {
                    EnqueueLocked(item);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public List<TItem> DequeueBatch(int maxCount)
        {
            _lock.EnterWriteLock();
            try
            {
                var count = Math.Min(maxCount, _items.Count);
                var result = new List<TItem>(count);

                for (int i = 0; i < count; i++)
                {
                    var node = _items.First;
                    var item = node.Value;
                    var key = _keySelector(item);

                    _items.RemoveFirst();
                    _lookup.Remove(key);
                    result.Add(item);
                }

                return result;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        // Implement IDisposable to properly dispose of the lock
        public void Dispose()
        {
            _lock?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
