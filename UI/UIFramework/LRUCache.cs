using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vant.UI.UIFramework
{
    /// <summary>
    /// 简单的 LRU (Least Recently Used) 缓存实现
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public class LRUCache<TKey, TValue>
    {
        private readonly uint _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;
        private readonly LinkedList<CacheItem> _lruList;
        private readonly Action<TValue> _onRemove;

        private struct CacheItem
        {
            public TKey Key;
            public TValue Value;
        }

        public LRUCache(uint capacity, Action<TValue> onRemove)
        {
            _capacity = capacity;
            _onRemove = onRemove;
            _cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem>>((int)capacity);
            _lruList = new LinkedList<CacheItem>();
        }

        public TValue Get(TKey key)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                // 命中缓存，移动到链表头部 (表示最近使用)
                TValue value = node.Value.Value;
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return value;
            }
            return default;
        }

        public void Put(TKey key, TValue value)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                // 更新值，移动到头部
                node.Value = new CacheItem { Key = key, Value = value };
                _lruList.Remove(node);
                _lruList.AddFirst(node);
            }
            else
            {
                // 新增
                if (_cacheMap.Count >= _capacity)
                {
                    RemoveLeastUsed();
                }

                var newNode = new LinkedListNode<CacheItem>(new CacheItem { Key = key, Value = value });
                _lruList.AddFirst(newNode);
                _cacheMap.Add(key, newNode);
            }
        }

        public bool Contains(TKey key)
        {
            return _cacheMap.ContainsKey(key);
        }

        public void Remove(TKey key)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cacheMap.Remove(key);
                _onRemove?.Invoke(node.Value.Value);
            }
        }

        private void RemoveLeastUsed()
        {
            var lastNode = _lruList.Last;
            if (lastNode != null)
            {
                _lruList.RemoveLast();
                _cacheMap.Remove(lastNode.Value.Key);
                _onRemove?.Invoke(lastNode.Value.Value);
            }
        }

        public void Clear()
        {
            foreach (var node in _lruList)
            {
                _onRemove?.Invoke(node.Value);
            }
            _lruList.Clear();
            _cacheMap.Clear();
        }
    }
}
