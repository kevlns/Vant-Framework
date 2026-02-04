using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Vant.UI.UIFramework;
using UnityEngine;
using Vant.Core;

namespace Vant.MVC
{
    /// <summary>
    /// UI 基类
    /// 负责定义 UI 的生命周期和基本属性
    /// </summary>
    public abstract class AbstractUIBase
    {
        public UIConfig Config { get; private set; }

        /// <summary>
        /// Registration-time configuration for this UI type.
        /// Override in subclasses and typically return a static config (e.g. MyPanel.StaticConfig).
        /// </summary>
        public virtual UIConfig RegisterConfig => null;
        public object Context { get; private set; }
        public GameObject gameObject { get; private set; }
        public Transform transform { get; private set; }
        protected AppCore appCore { get; private set; }

        /// <summary>
        /// 唯一 ID (用于区分多实例)
        /// </summary>
        public int InstanceId { get; private set; }

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;

        // 附加 UI 列表
        private readonly List<AbstractSubUI> _subUIs = new List<AbstractSubUI>();

        public bool IsVisible => gameObject != null && gameObject.activeSelf;

        // 由 UIManager 注入，用于实例级关闭
        internal UIManager UIManager { get; set; }

        #region Internal Lifecycle (由 UIManager 调用)

        internal void InternalInit(UIConfig config, int instanceId, GameObject gameObject, AppCore appCore)
        {
            Config = config;
            InstanceId = instanceId;
            this.gameObject = gameObject;
            this.transform = gameObject.transform;
            this.appCore = appCore;

            _canvas = gameObject.GetComponent<Canvas>();
            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            OnCreate();
        }

        internal async UniTask InternalOpen(object args)
        {
            Context = args;

            // 1. 打开前处理
            await OnBeforeOpen(args);

            gameObject.SetActive(true);

            // 2. 刷新数据
            OnRefresh();

            // 3. 播放入场动画 (如果有)
            if (Config.EnterAnimation != null)
            {
                await Config.EnterAnimation.Invoke(this);
            }
            else
            {
                gameObject.transform.localScale = Vector3.one;
            }

            // 4. 打开后处理
            await OnAfterOpen();

            appCore.Notifier.Dispatch(UIInternalEvent.UI_OPENED, Config.Name);
        }

        internal async UniTask InternalClose()
        {
            // 1. 关闭前处理
            await OnBeforeClose();

            // 2. 播放出场动画 (如果有)
            if (Config.ExitAnimation != null)
            {
                await Config.ExitAnimation.Invoke(this);
            }
            else
            {
                gameObject.transform.localScale = Vector3.one;
            }

            gameObject.SetActive(false);
            appCore.Notifier.Dispatch(UIInternalEvent.UI_CLOSED, Config.Name);
        }

        internal void InternalDestroy()
        {
            // 销毁所有附加 UI
            for (int i = _subUIs.Count - 1; i >= 0; i--)
            {
                var sub = _subUIs[i];
                if (sub != null)
                {
                    sub.Dispose();
                }
            }
            _subUIs.Clear();

            OnDestroyUI();
            transform = null;
            appCore = null;
            Config = null;
            Context = null;
            InstanceId = 0;
            // GameObject 的实际销毁由 UIManager/AssetManager 统一处理（ReleaseInstance），
            // 避免出现重复 Destroy 以及无法正确维护资源引用计数的问题。
            gameObject = null;
        }

        #endregion

        #region Virtual Methods (子类重写)

        /// <summary>
        /// 1. 创建时调用 (只调用一次)
        /// 用于初始化组件引用、事件监听等
        /// </summary>
        protected virtual void OnCreate() { }

        /// <summary>
        /// 2. 打开前调用
        /// 用于重置状态、准备数据。支持异步。
        /// </summary>
        protected virtual async UniTask OnBeforeOpen(object args) { await UniTask.CompletedTask; }

        /// <summary>
        /// 3. 刷新时调用
        /// 用于将数据绑定到 UI 元素
        /// </summary>
        protected virtual void OnRefresh() { }

        /// <summary>
        /// 4. 打开后调用 (动画播放完毕后)
        /// 用于开始引导、自动滚动等需要 UI 完全展示后的逻辑
        /// </summary>
        protected virtual async UniTask OnAfterOpen() { await UniTask.CompletedTask; }

        /// <summary>
        /// 5. 关闭前调用
        /// 用于停止计时器、保存临时数据等
        /// </summary>
        protected virtual async UniTask OnBeforeClose() { await UniTask.CompletedTask; }

        /// <summary>
        /// 7. 销毁时调用
        /// 用于释放非托管资源
        /// </summary>
        protected virtual void OnDestroyUI() { }

        #endregion

        #region Public Methods

        public void SetSortingOrder(int order)
        {
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = order;
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = interactable;
                _canvasGroup.blocksRaycasts = interactable;
            }
        }

        public void CloseSelf()
        {
            InternalCloseSelf().Forget();
        }

        internal async UniTask InternalCloseSelf()
        {
            if (UIManager == null)
            {
                Debug.LogWarning($"[UI] InternalCloseSelf called but UIManager is null. UI: {Config?.Name}");
                await UniTask.CompletedTask;
                return;
            }

            if (Config.Layer == UILayer.Normal)
            {
                await appCore.UIManager.Back();
                return;
            }

            await UIManager.CloseInstanceAsync(this);
        }

        #endregion

        #region SubUI Management

        /// <summary>
        /// 注册附加 UI (SubUI)
        /// </summary>
        /// <typeparam name="T">SubUI 类型</typeparam>
        /// <param name="go">管理的 GameObject</param>
        /// <param name="args">初始化参数</param>
        protected T RegisterSubUI<T>(GameObject go, object args = null) where T : AbstractSubUI, new()
        {
            if (go == null) return null;

            T subUI = new T();
            subUI.Init(this, go, this.appCore, args);
            _subUIs.Add(subUI);
            return subUI;
        }

        /// <summary>
        /// 注销附加 UI
        /// </summary>
        protected void UnregisterSubUI(AbstractSubUI subUI)
        {
            if (subUI != null && _subUIs.Contains(subUI))
            {
                _subUIs.Remove(subUI);
            }
        }

        /// <summary>
        /// 刷新所有附加 UI
        /// </summary>
        protected void RefreshSubUIs()
        {
            foreach (var view in _subUIs)
            {
                if (view != null && view.IsVisible)
                {
                    view.Refresh();
                }
            }
        }

        #endregion
    }
}
