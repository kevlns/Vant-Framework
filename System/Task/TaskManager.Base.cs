using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vant.Core;

namespace Vant.System
{
    public partial class TaskManager
    {
        private AppCore _appCore;
        private readonly object _gate = new object();
        public static TaskManager Instance { get; private set; }
        public event Action ResetEvent;

        public TaskManager(AppCore appCore)
        {
            Instance = this;
            _appCore = appCore;
            _defaultChain = new TaskChain(this, needFuse: false);
        }

        /// <summary>
        /// Reset：取消并释放上一轮 CTS，然后创建新一轮 CTS
        /// </summary>
        public void Reset()
        {
            CancellationTokenSource old;
            lock (_gate)
            {
                old = _cts;
                _cts = new CancellationTokenSource();
            }

            // Cancel 会触发旧 token 的所有注册回调
            try { old?.Cancel(); }
            finally
            {
                old?.Dispose();
                _defaultChain.ResetCTS();
                ResetEvent?.Invoke();
            }
        }
    }
}