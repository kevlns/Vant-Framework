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
        public TaskChain _defaultChain;

        /// <summary>
        /// 将一个串行异步任务排入默认串行任务链。
        /// </summary>
        public void ScheduleSerial(Func<UniTask> work)
        {
            _defaultChain.EnqueueAsync(work);
        }
    }
}