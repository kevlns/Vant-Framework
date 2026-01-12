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
        private CancellationTokenSource _cts = new CancellationTokenSource();
        /// <summary>当前全局 Token</summary>

        public CancellationToken GlobalCancellationToken
        {
            get { lock (_gate) return _cts.Token; }
        }

        /// <summary>
        /// 创建一个关联到全局 Token 的 CancellationTokenSource
        /// </summary>
        public CancellationTokenSource CreateLinkedCTS()
        {
            lock (_gate)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(GlobalCancellationToken);
            }
        }

        /// <summary>
        /// 创建一个关联到全局 Token 和指定 Token 的 CancellationTokenSource
        /// </summary>
        public CancellationTokenSource CreateLinkedCTS(CancellationToken externalToken)
        {
            lock (_gate)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(GlobalCancellationToken, externalToken);
            }
        }

        #region Static Utils

        /// <summary>
        /// UniTask.WaitUntilCanceled 的语义是“token 被 cancel 时任务成功完成”，不会抛异常。
        /// </summary>
        public static UniTask WaitUntilCanceled(
            CancellationToken cancellationToken = default,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            bool completeImmediately = false)
        {
            if (Instance == null)
            {
                return UniTask.WaitUntilCanceled(cancellationToken, timing, completeImmediately);
            }
            else
            {
                var cts = Instance.CreateLinkedCTS(cancellationToken);
                return AwaitAndDispose(UniTask.WaitUntilCanceled(cts.Token, timing, completeImmediately), cts);
            }
        }

        /// <summary>
        /// 等待任务完成后释放 CTS 资源
        /// </summary>
        private static async UniTask AwaitAndDispose(UniTask task, CancellationTokenSource cts)
        {
            try
            {
                await task;
            }
            finally
            {
                cts.Dispose();
            }
        }

        #endregion
    }
}