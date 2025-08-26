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

    public class UniqueQueue<T>
    {
        private readonly LinkedList<T> _items = new LinkedList<T>();
        private readonly Dictionary<object, LinkedListNode<T>> _lookup = new Dictionary<object, LinkedListNode<T>>();
        private readonly Func<T, object> _keySelector;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public UniqueQueue(Func<T, object> keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        }

        public bool Enqueue(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                var key = _keySelector(item);

                // If item with this key already exists, remove it first
                if (_lookup.TryGetValue(key, out var existingNode))
                {
                    _items.Remove(existingNode);
                    _lookup.Remove(key);
                }

                // Add the new item to the end
                var newNode = _items.AddLast(item);
                _lookup[key] = newNode;

                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryDequeue(out T item)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_items.Count == 0)
                {
                    item = default(T);
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

        public T Dequeue()
        {
            if (TryDequeue(out T item))
                return item;

            throw new InvalidOperationException("Queue is empty");
        }

        public bool Remove(object uniqueId)
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

        public bool TryPeek(out T item)
        {
            _lock.EnterReadLock();
            try
            {
                if (_items.Count > 0)
                {
                    item = _items.First.Value;
                    return true;
                }
                item = default(T);
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool Contains(object uniqueId)
        {
            if (uniqueId == null) return false;
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

        public bool TryGetItem(object uniqueId, out T item)
        {
            _lock.EnterReadLock();
            try
            {
                if (_lookup.TryGetValue(uniqueId, out var node))
                {
                    item = node.Value;
                    return true;
                }
                item = default(T);
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
        public T[] ToArray()
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
        public object[] GetAllIds()
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
        public void EnqueueBatch(IEnumerable<T> items)
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var item in items)
                {
                    var key = _keySelector(item);

                    // If item with this key already exists, remove it first
                    if (_lookup.TryGetValue(key, out var existingNode))
                    {
                        _items.Remove(existingNode);
                        _lookup.Remove(key);
                    }

                    // Add the new item to the end
                    var newNode = _items.AddLast(item);
                    _lookup[key] = newNode;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public List<T> DequeueBatch(int maxCount)
        {
            _lock.EnterWriteLock();
            try
            {
                var result = new List<T>();
                var count = Math.Min(maxCount, _items.Count);

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
        }
    }
}
