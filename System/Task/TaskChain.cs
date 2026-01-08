using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vant.Core;

namespace Vant.System
{
    public class TaskChain : IUniTaskAsyncDisposable
    {
        private TaskManager taskManager;
        private readonly object _gate = new();  // 原子操作锁
        private UniTask _tail = UniTask.CompletedTask;  // “任务链”最后一个节点
        private CancellationTokenSource _cts;  // 用于取消后续任务
        private volatile bool _isBroken = false;  // 链条是否已熔断
        private bool _needFuse = false;  // 是否需要熔断机制

        public TaskChain(TaskManager taskManager, bool needFuse = false)
        {
            this.taskManager = taskManager;
            _needFuse = needFuse;
            ResetCTS();
        }

        /// <summary>
        /// 把一个异步委托按提交顺序排队并返回可等待的 Task。
        /// </summary>
        public void EnqueueAsync(Func<UniTask> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            if (_isBroken)
            {
                Debug.LogWarning("TaskChain is broken, auto-resetting...");
                ResetBrokenChain();
            }

            // 捕获调用方上下文（如果有的话）——这是默认行为，无需显式操作
            UniTask newTail;

            lock (_gate)
            {
                // 把当前队尾记下来，再把 _tail 指向新节点
                try
                {
                    newTail = RunAfterAsync(_tail, work, _cts.Token);
                    _tail = newTail;
                }
                catch (Exception ex)
                {
                    Debug.LogError("TaskChain EnqueueAsync failed: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 依次等待 prev → 执行 work；保持调用方上下文，不用 ConfigureAwait(false)。
        /// </summary>
        private async UniTask RunAfterAsync(UniTask prev, Func<UniTask> work, CancellationToken token)
        {
            // 等待前一个任务完成
            try { await prev; }
            catch { }

            // 检查状态
            if (token.IsCancellationRequested || _isBroken) return;

            // 执行当前任务
            try { await work(); }
            catch (Exception ex)
            {
                _isBroken = _needFuse;
                Debug.LogError("TaskChain task failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 同步重置链条状态
        /// </summary>
        private void ResetBrokenChain()
        {
            lock (_gate)
            {
                ResetCTS();
                _tail = UniTask.CompletedTask;
                _isBroken = false;
            }
        }

        /// <summary>
        /// 优雅关闭：等待已排队的任务跑完，取消后续排队。
        /// </summary>
        public async UniTask DisposeAsync()
        {
            _cts.Cancel();
            try
            {
                await _tail;
            }
            finally
            {
                _cts.Dispose();
            }
        }

        public void ResetCTS()
        {
            lock (_gate)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = taskManager.CreateLinkedCTS();
            }
        }
    }
}