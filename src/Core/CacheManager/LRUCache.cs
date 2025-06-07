using System;
using System.Collections.Generic;
using System.Linq;

namespace GoLani.SPTModTranslator.Core.CacheManager
{
    public class LRUCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, CacheNode<TKey, TValue>> _cache;
        private readonly CacheNode<TKey, TValue> _head;
        private readonly CacheNode<TKey, TValue> _tail;
        private int _maxCapacity;

        public int Count => _cache.Count;
        public int MaxCapacity 
        { 
            get => _maxCapacity;
            set 
            {
                _maxCapacity = value;
                EnforceCapacity();
            }
        }

        public LRUCache(int maxCapacity)
        {
            if (maxCapacity <= 0)
                throw new ArgumentException("최대 용량은 0보다 커야 합니다", nameof(maxCapacity));

            _maxCapacity = maxCapacity;
            _cache = new Dictionary<TKey, CacheNode<TKey, TValue>>();
            
            _head = new CacheNode<TKey, TValue>();
            _tail = new CacheNode<TKey, TValue>();
            
            _head.Next = _tail;
            _tail.Previous = _head;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);
            
            if (_cache.TryGetValue(key, out var node))
            {
                MoveToHead(node);
                value = node.Value;
                return true;
            }
            
            return false;
        }

        public void Set(TKey key, TValue value)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                existingNode.Value = value;
                MoveToHead(existingNode);
            }
            else
            {
                var newNode = new CacheNode<TKey, TValue>
                {
                    Key = key,
                    Value = value
                };
                
                _cache[key] = newNode;
                AddToHead(newNode);
                
                if (_cache.Count > _maxCapacity)
                {
                    var lastNode = RemoveTail();
                    _cache.Remove(lastNode.Key);
                }
            }
        }

        public bool Remove(TKey key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                RemoveNode(node);
                _cache.Remove(key);
                return true;
            }
            
            return false;
        }

        public void Clear()
        {
            _cache.Clear();
            _head.Next = _tail;
            _tail.Previous = _head;
        }

        public Dictionary<TKey, TValue> GetAll()
        {
            var result = new Dictionary<TKey, TValue>();
            
            var current = _head.Next;
            while (current != _tail)
            {
                result[current.Key] = current.Value;
                current = current.Next;
            }
            
            return result;
        }

        public void RemoveOldestItems(int count)
        {
            if (count <= 0)
                return;

            var itemsRemoved = 0;
            var current = _tail.Previous;
            
            while (current != _head && itemsRemoved < count)
            {
                var nodeToRemove = current;
                current = current.Previous;
                
                RemoveNode(nodeToRemove);
                _cache.Remove(nodeToRemove.Key);
                itemsRemoved++;
            }
        }

        public List<TKey> GetKeysByAccessOrder()
        {
            var keys = new List<TKey>();
            var current = _head.Next;
            
            while (current != _tail)
            {
                keys.Add(current.Key);
                current = current.Next;
            }
            
            return keys;
        }

        public List<TKey> GetOldestKeys(int count)
        {
            var keys = new List<TKey>();
            var current = _tail.Previous;
            var itemsCollected = 0;
            
            while (current != _head && itemsCollected < count)
            {
                keys.Add(current.Key);
                current = current.Previous;
                itemsCollected++;
            }
            
            return keys;
        }

        public bool ContainsKey(TKey key)
        {
            return _cache.ContainsKey(key);
        }

        private void AddToHead(CacheNode<TKey, TValue> node)
        {
            node.Previous = _head;
            node.Next = _head.Next;
            
            _head.Next.Previous = node;
            _head.Next = node;
        }

        private void RemoveNode(CacheNode<TKey, TValue> node)
        {
            node.Previous.Next = node.Next;
            node.Next.Previous = node.Previous;
        }

        private void MoveToHead(CacheNode<TKey, TValue> node)
        {
            RemoveNode(node);
            AddToHead(node);
        }

        private CacheNode<TKey, TValue> RemoveTail()
        {
            var lastNode = _tail.Previous;
            RemoveNode(lastNode);
            return lastNode;
        }

        private void EnforceCapacity()
        {
            while (_cache.Count > _maxCapacity)
            {
                var lastNode = RemoveTail();
                _cache.Remove(lastNode.Key);
            }
        }
    }

    internal class CacheNode<TKey, TValue>
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }
        public CacheNode<TKey, TValue> Previous { get; set; }
        public CacheNode<TKey, TValue> Next { get; set; }
    }
}