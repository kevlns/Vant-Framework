using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vant.Core;

namespace Vant.System.Guide
{
    /// <summary>
    /// 引导步骤基类
    /// </summary>
    public abstract class GuideStepBase
    {
        protected AppCore AppCore => AppCore.Instance;
        public string StepId { get; private set; }
        public object GuideStepData { get; private set; }

        /// <summary>
        /// 步骤类型 (对应 GuideStepType 定义的字符串)
        /// </summary>
        public virtual string StepType => GuideStepType.None;

        #region Virtual Methods

        protected virtual UniTask<bool> OnValidate() { return UniTask.FromResult(true); }
        protected virtual UniTask OnRun() => UniTask.CompletedTask;
        protected virtual void OnUpdate(float deltaTime) { }
        protected virtual void OnDispose() { }
        public virtual async UniTask Preload() { await UniTask.CompletedTask; }

        public async UniTask<string> Play(string stepId, object GuideStepData, CancellationTokenSource cancellationTokenSource = default)
        {
            this.StepId = stepId;
            this.GuideStepData = GuideStepData;
            string stepResult = string.Empty;

            var token = cancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                bool isValid = await OnValidate();
                if (!isValid)
                {
                    stepResult = $"GuideStep [{StepId}] Condition Validate Failed.";
                    return stepResult;
                }

                var runTask = OnRun();
                var cancelTask = UniTask.WaitUntilCanceled(token);
                int index = await UniTask.WhenAny(runTask, cancelTask);

                if (index == 0) await runTask;
                else stepResult = $"GuideStep [{StepId}] Canceled.";
            }
            catch (OperationCanceledException)
            {
                stepResult = $"GuideStep [{StepId}] Global Canceled.";
                throw;
            }
            catch (Exception ex)
            {
                stepResult = $"GuideStep [{StepId}] Exception: {ex}";
            }
            finally
            {
                OnDispose();
            }
            return stepResult;
        }

        #endregion
    }
}
