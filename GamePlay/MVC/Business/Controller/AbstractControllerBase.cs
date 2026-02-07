

namespace Vant.MVC
{
    /// <summary>
    /// MVC Controller 基类
    /// </summary>
    public abstract class AbstractControllerBase<TView, TViewModel> : IController
        where TView : AbstractGeneralViewBase
        where TViewModel : class, IViewModel
    {
        protected TView View { get; private set; }
        protected TViewModel ViewModel { get; private set; }

        public void InternalInit(TView view, TViewModel viewModel)
        {
            View = view;
            ViewModel = viewModel;
            Init();
        }


        #region IController 接口实现

        public void Init() => OnInit();

        public void Update(float deltaTime, float unscaledDeltaTime = 0) => OnUpdate(deltaTime, unscaledDeltaTime);

        public void Destroy() => OnDestroy();

        #endregion


        #region 生命周期方法

        /// <summary>
        /// 创建时调用 (只调用一次)
        /// </summary>
        protected virtual void OnInit() { }

        /// <summary>
        /// 每帧刷新时调用
        /// </summary>
        protected virtual void OnUpdate(float deltaTime, float unscaledDeltaTime = 0) { }

        /// <summary>
        /// 销毁时调用 (只调用一次)
        /// </summary>
        protected virtual void OnDestroy() { }

        #endregion
    }
}