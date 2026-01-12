using System;
using System.Collections.Generic;
using UnityEngine;
using Vant.Core;

namespace Vant.System
{
    /// <summary>
    /// 事件通知器
    /// 核心功能：
    /// 1. 提供基于 Enum 的事件注册、移除和分发机制
    /// 2. 支持泛型参数传递 (0-3个参数)
    /// 3. 自动处理 Unity 对象销毁后的回调清理
    /// 4. 线程不安全，仅限主线程使用
    /// </summary>
    public class Notifier
    {
        private class Listener
        {
            public Delegate OriginalDelegate { get; }
            public Action<object[]> Invoker { get; }
            // 缓存 Target，避免频繁访问 Delegate.Target 属性
            public object Target { get; }

            public Listener(Delegate original, Action<object[]> invoker)
            {
                OriginalDelegate = original;
                Invoker = invoker;
                Target = original.Target;
            }
        }

        // 使用 Enum 作为 Key，Unity 2018+ 后 Dictionary<Enum, T> 不再产生额外 GC
        private readonly Dictionary<Enum, List<Listener>> _eventMap = new Dictionary<Enum, List<Listener>>();

        /// <summary>
        /// 安全获取参数：当参数数组为 null 或长度不足时，返回 default(T)。
        /// 这样可以允许调用方少传参数，未传入的参数会自动视为 null。
        /// </summary>
        private static T GetArg<T>(object[] args, int index)
        {
            if (args == null || index < 0 || index >= args.Length)
            {
                return default;
            }

            object value = args[index];
            if (value == null)
            {
                return default;
            }

            return (T)value;
        }

        #region Listen (注册)

        public void AddListener(Enum eventType, Action callback)
        {
            AddListener(eventType, callback, _ => callback());
        }

        /// <summary>
        /// 通用版本：直接接收 object[] 参数，由调用方自行拆解
        /// 适合配合 ArgsHelper 使用，避免频繁声明泛型重载
        /// </summary>
        public void AddListener(Enum eventType, Action<object[]> callback)
        {
            if (callback == null) return;
            AddListener(eventType, callback, callback);
        }

        /// <summary>
        /// 接收单个 object 参数的通用接口
        /// </summary>
        public void AddListener(Enum eventType, Action<object> callback)
        {
            AddListener(eventType, callback, args => callback(GetArg<object>(args, 0)));
        }

        public void AddListener<T>(Enum eventType, Action<T> callback)
        {
            AddListener(eventType, callback, args => callback(GetArg<T>(args, 0)));
        }

        public void AddListener<T1, T2>(Enum eventType, Action<T1, T2> callback)
        {
            AddListener(eventType, callback, args => callback(
                GetArg<T1>(args, 0),
                GetArg<T2>(args, 1)));
        }

        public void AddListener<T1, T2, T3>(Enum eventType, Action<T1, T2, T3> callback)
        {
            AddListener(eventType, callback, args => callback(
                GetArg<T1>(args, 0),
                GetArg<T2>(args, 1),
                GetArg<T3>(args, 2)));
        }

        private void AddListener(Enum eventType, Delegate callback, Action<object[]> invoker)
        {
            if (!_eventMap.TryGetValue(eventType, out var list))
            {
                list = new List<Listener>();
                _eventMap[eventType] = list;
            }

            // 防止重复添加
            // 注意：这里只检查 Target 和 Method，不检查闭包上下文的细微差异
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].OriginalDelegate == callback)
                {
                    Debug.LogWarning($"[Notifier] 重复注册事件: {eventType}, Target: {callback.Target}");
                    return;
                }
            }

            list.Add(new Listener(callback, invoker));
        }

        #endregion

        #region RemoveListener (移除)

        public void RemoveListener(Enum eventType, Action callback) => RemoveListenerInternal(eventType, callback);
        public void RemoveListener(Enum eventType, Action<object[]> callback) => RemoveListenerInternal(eventType, callback);
        public void RemoveListener(Enum eventType, Action<object> callback) => RemoveListenerInternal(eventType, callback);
        public void RemoveListener<T>(Enum eventType, Action<T> callback) => RemoveListenerInternal(eventType, callback);
        public void RemoveListener<T1, T2>(Enum eventType, Action<T1, T2> callback) => RemoveListenerInternal(eventType, callback);
        public void RemoveListener<T1, T2, T3>(Enum eventType, Action<T1, T2, T3> callback) => RemoveListenerInternal(eventType, callback);

        private void RemoveListenerInternal(Enum eventType, Delegate callback)
        {
            if (_eventMap.TryGetValue(eventType, out var list))
            {
                // 从后往前遍历，虽然 RemoveAll 更快，但我们需要精确匹配 Delegate
                // Delegate.Equals 会比较 Target 和 Method
                int count = list.RemoveAll(l => l.OriginalDelegate == callback || l.OriginalDelegate.Equals(callback));

                if (list.Count == 0)
                {
                    _eventMap.Remove(eventType);
                }
            }
        }

        /// <summary>
        /// 移除指定对象上的所有事件监听
        /// 通常在对象销毁时调用
        /// </summary>
        public void RemoveAllListeners(object target)
        {
            if (target == null) return;

            var keysToRemove = new List<Enum>();

            foreach (var kvp in _eventMap)
            {
                var list = kvp.Value;
                list.RemoveAll(l => l.Target == target);

                if (list.Count == 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _eventMap.Remove(key);
            }
        }

        public void Clear()
        {
            _eventMap.Clear();
        }

        #endregion

        #region Dispatch (发送/触发)

        public void Dispatch(Enum eventType)
        {
            DispatchInternal(eventType, null);
        }

        public void Dispatch<T>(Enum eventType, T arg1)
        {
            DispatchInternal(eventType, new object[] { arg1 });
        }

        public void Dispatch<T1, T2>(Enum eventType, T1 arg1, T2 arg2)
        {
            DispatchInternal(eventType, new object[] { arg1, arg2 });
        }

        public void Dispatch<T1, T2, T3>(Enum eventType, T1 arg1, T2 arg2, T3 arg3)
        {
            DispatchInternal(eventType, new object[] { arg1, arg2, arg3 });
        }

        private void DispatchInternal(Enum eventType, object[] args)
        {
            if (!_eventMap.TryGetValue(eventType, out var list)) return;

            // 倒序遍历，支持在回调中移除监听，以及处理失效对象
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var listener = list[i];
                var target = listener.Target;

                // 1. 智能清理：检查 Unity 对象是否已销毁
                // 这里的 check 是针对 UnityEngine.Object 的重载 == null
                if (target is UnityEngine.Object unityObj && unityObj == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                try
                {
                    // 2. 执行回调
                    // 如果参数不匹配，(T)args[0] 强转会抛出 InvalidCastException
                    // 如果参数数量不对，数组越界会抛出 IndexOutOfRangeException
                    listener.Invoker(args);
                }
                catch (Exception ex)
                {
                    // 3. 异常处理
                    Debug.LogError($"[Notifier] 执行事件 {eventType} 回调失败: {ex}");

                    // 如果是因为目标对象被销毁导致的空引用（非 Unity Object 的情况），也可以考虑移除
                    // 但为了安全起见，只对明确的 Unity Object 销毁做自动移除
                }
            }
        }

        #endregion
    }
}
