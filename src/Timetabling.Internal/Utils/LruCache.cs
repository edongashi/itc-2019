using System.Collections.Generic;

namespace Timetabling.Internal.Utils
{
    internal class LruCache<TKey, TValue>
    {
        private class Node
        {
            internal readonly TValue Data;
            internal readonly TKey Key;
            internal Node Previous;
            internal Node Next;

            internal Node(TValue data, TKey key)
            {
                Data = data;
                Key = key;
            }
        }

        private readonly int maxCapacity;
        private readonly Dictionary<TKey, Node> lruCache;
        private Node head;
        private Node tail;

        internal LruCache(int maxCapacity)
        {
            this.maxCapacity = maxCapacity;
            lruCache = new Dictionary<TKey, Node>();
        }

        internal void Clear()
        {
            lruCache.Clear();
            head = null;
            tail = null;
        }

        internal void Add(TKey key, TValue value)
        {
            if (lruCache.ContainsKey(key))
            {
                MakeMostRecentlyUsed(lruCache[key]);
            }

            if (lruCache.Count >= maxCapacity)
            {
                RemoveLeastRecentlyUsed();
            }

            var insertedNode = new Node(value, key);

            if (head == null)
            {
                head = insertedNode;
                tail = head;
            }
            else
            {
                MakeMostRecentlyUsed(insertedNode);
            }

            lruCache.Add(key, insertedNode);
        }

        internal bool TryGet(TKey key, out TValue value)
        {
            if (lruCache.TryGetValue(key, out var node))
            {
                MakeMostRecentlyUsed(node);
                value = node.Data;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        internal int Count => lruCache.Count;

        internal int Capacity => maxCapacity;

        internal string CacheFeed()
        {
            var headReference = head;

            var items = new List<string>();

            while (headReference != null)
            {
                items.Add($"[V: {headReference.Data}]");
                headReference = headReference.Next;
            }

            return string.Join(",", items);
        }

        private void RemoveLeastRecentlyUsed()
        {
            lruCache.Remove(tail.Key);
            tail.Previous.Next = null;
            tail = tail.Previous;
        }

        private void MakeMostRecentlyUsed(Node foundItem)
        {
            // Newly inserted item bring to the top
            if (foundItem.Next == null && foundItem.Previous == null)
            {
                foundItem.Next = head;
                head.Previous = foundItem;
                if (head.Next == null)
                {
                    tail = head;
                }

                head = foundItem;
            }
            // If it is the tail than bring it to the top
            else if (foundItem.Next == null && foundItem.Previous != null)
            {
                foundItem.Previous.Next = null;
                tail = foundItem.Previous;
                foundItem.Next = head;
                head.Previous = foundItem;
                head = foundItem;
            }
            // If it is an element in between than bring it to the top
            else if (foundItem.Next != null && foundItem.Previous != null)
            {
                foundItem.Previous.Next = foundItem.Next;
                foundItem.Next.Previous = foundItem.Previous;
                foundItem.Next = head;
                head.Previous = foundItem;
                head = foundItem;
            }
            // Last case would be to check if it is a head but if it is than there is no need to bring it to the top
        }
    }
}
