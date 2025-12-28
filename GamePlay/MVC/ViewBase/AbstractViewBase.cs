using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Vant.MVC
{
    /// <summary>
    /// MVC View 基类
    /// 定义视图的基本生命周期：初始化 -> 展示前 -> 刷新 -> 展示后 -> 隐藏/销毁
    /// </summary>
    public abstract class AbstractViewBase : MonoBehaviour
    {
        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible => gameObject.activeSelf;

        /// <summary>
        /// 视图参数上下文
        /// </summary>
        public object Context { get; private set; }

        #region Public Lifecycle Methods

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="args">初始化参数</param>
        public void Init(object args = null)
        {
            OnInit(args);
        }

        /// <summary>
        /// 展示视图
        /// </summary>
        /// <param name="args">展示参数</param>
        public async UniTask Show(object args = null)
        {
            Context = args;

            // 1. 展示前处理 (如重置状态、加载资源)
            await OnBeforeShow(args);

            // 2. 激活物体
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // 3. 刷新视图表现
            OnRefresh();

            // 4. 展示后处理 (如播放入场动画、引导逻辑)
            await OnAfterShow();
        }

        /// <summary>
        /// 刷新视图
        /// </summary>
        public void Refresh()
        {
            if (IsVisible)
            {
                OnRefresh();
            }
        }

        /// <summary>
        /// 隐藏视图
        /// </summary>
        public async UniTask Hide()
        {
            // 1. 隐藏前处理 (如播放出场动画)
            await OnBeforeHide();

            // 2. 隐藏物体
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            // 3. 隐藏后处理 (如清理临时状态)
            await OnAfterHide();
        }

        /// <summary>
        /// 销毁视图
        /// </summary>
        public void Dispose()
        {
            OnDispose();
            Destroy(gameObject);
        }

        #endregion

        #region Protected Virtual Methods (Subclass Override)

        /// <summary>
        /// 初始化时调用
        /// </summary>
        protected virtual void OnInit(object args) { }

        /// <summary>
        /// 展示前调用 (支持异步)
        /// </summary>
        protected virtual UniTask OnBeforeShow(object args) => UniTask.CompletedTask;

        /// <summary>
        /// 刷新时调用 (核心表现逻辑)
        /// </summary>
        protected virtual void OnRefresh() { }

        /// <summary>
        /// 展示后调用 (支持异步)
        /// </summary>
        protected virtual UniTask OnAfterShow() => UniTask.CompletedTask;

        /// <summary>
        /// 隐藏前调用 (支持异步)
        /// </summary>
        protected virtual UniTask OnBeforeHide() => UniTask.CompletedTask;

        /// <summary>
        /// 隐藏后调用 (支持异步)
        /// </summary>
        protected virtual UniTask OnAfterHide() => UniTask.CompletedTask;

        /// <summary>
        /// 销毁时调用
        /// </summary>
        protected virtual void OnDispose() { }

        #endregion
    }
}
