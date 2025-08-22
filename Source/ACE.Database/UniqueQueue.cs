using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Database
{
    public class UniqueQueue<T>
    {
        private readonly LinkedList<T> _items = new LinkedList<T>();
        private readonly Dictionary<object, LinkedListNode<T>> _lookup = new Dictionary<object, LinkedListNode<T>>();
        private readonly Func<T, object> _keySelector;

        public UniqueQueue(Func<T, object> keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        }

        public bool Enqueue(T item)
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

        public T Dequeue()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            var node = _items.First;
            var item = node.Value;
            var key = _keySelector(item);

            _items.RemoveFirst();
            _lookup.Remove(key);

            return item;
        }

        public bool Remove(object uniqueId)
        {
            if (_lookup.TryGetValue(uniqueId, out var node))
            {
                _items.Remove(node);
                _lookup.Remove(uniqueId);
                return true;
            }
            return false;
        }

        public bool TryPeek(out T item)
        {
            if (_items.Count > 0)
            {
                item = _items.First.Value;
                return true;
            }
            item = default(T);
            return false;
        }

        public bool Contains(object uniqueId)
        {
            return _lookup.ContainsKey(uniqueId);
        }

        public bool TryGetItem(object uniqueId, out T item)
        {
            if (_lookup.TryGetValue(uniqueId, out var node))
            {
                item = node.Value;
                return true;
            }
            item = default(T);
            return false;
        }

        public int Count => _items.Count;

        public void Clear()
        {
            _items.Clear();
            _lookup.Clear();
        }

        // For enumeration (maintains queue order)
        public IEnumerable<T> Items => _items;
    }

}
