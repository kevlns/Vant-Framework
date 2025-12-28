using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace Vant.Utils
{
    /// <summary>
    /// 非MonoBehaviour的单例基类
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public class Singleton<T> where T : class, new()
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isDisposed = false;

        public virtual void Init(MonoBehaviour monoBehaviour)
        {
        }

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(typeof(T).Name, "Instance has been disposed.");
                }

                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                        }
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 释放单例实例
        /// </summary>
        public static void Dispose()
        {
            lock (_lock)
            {
                if (_instance != null)
                {
                    if (_instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }

                    _instance = null;
                    _isDisposed = true;
                }
            }
        }
    }
}