using Cysharp.Threading.Tasks;
using Vant.Core;

namespace Vant.System.Guide
{
    /// <summary>
    /// 引导步骤基类
    /// </summary>
    public abstract class AbstractGuideStep
    {
        protected AppCore AppCore => AppCore.Instance;
        
        /// <summary>
        /// 步骤类型 (对应 GuideStepType 定义的字符串)
        /// </summary>
        public virtual string StepType => GuideStepType.None;

        /// <summary>
        /// 步骤 ID
        /// </summary>
        public int StepId { get; set; }

        /// <summary>
        /// 所属引导 ID
        /// </summary>
        public int GuideId { get; set; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted { get; protected set; }

        /// <summary>
        /// 进入步骤
        /// </summary>
        public async UniTask Enter()
        {
            IsCompleted = false;
            await OnEnter();
        }

        /// <summary>
        /// 离开步骤
        /// </summary>
        public async UniTask Exit()
        {
            await OnExit();
        }

        /// <summary>
        /// 更新步骤 (每帧调用)
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!IsCompleted)
            {
                OnUpdate(deltaTime);
            }
        }

        /// <summary>
        /// 完成步骤
        /// </summary>
        protected void Complete()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            OnComplete();
        }

        #region Virtual Methods

        protected virtual UniTask OnEnter() => UniTask.CompletedTask;
        protected virtual UniTask OnExit() => UniTask.CompletedTask;
        protected virtual void OnUpdate(float deltaTime) { }
        protected virtual void OnComplete() { }

        #endregion
    }
}
