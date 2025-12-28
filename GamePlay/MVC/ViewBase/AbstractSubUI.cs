using System.Collections.Generic;
using Vant.Core;
using UnityEngine;

namespace Vant.MVC
{
    /// <summary>
    /// 附加 UI 基类 (SubUI)
    /// 纯 C# 类，非 MonoBehaviour
    /// 用于管理 UI 内部的嵌套界面逻辑 (如 Tab 页、Item、Widget)
    /// </summary>
    public abstract class AbstractSubUI
    {
        /// <summary>
        /// 所属的主 UI 界面 (Root)
        /// </summary>
        public AbstractUIBase OwnerUI { get; private set; }

        /// <summary>
        /// 管理的 GameObject
        /// </summary>
        public GameObject gameObject { get; private set; }

        /// <summary>
        /// 管理的 Transform
        /// </summary>
        public Transform transform { get; private set; }

        /// <summary>
        /// AppCore 引用
        /// </summary>
        protected AppCore AppCore { get; private set; }

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible => gameObject != null && gameObject.activeSelf;

        // 子 SubUI 列表 (递归管理)
        private readonly List<AbstractSubUI> _subUIs = new List<AbstractSubUI>();

        /// <summary>
        /// 初始化 SubUI
        /// </summary>
        public void Init(AbstractUIBase ownerUI, GameObject go, AppCore appCore, object args = null)
        {
            OwnerUI = ownerUI;
            gameObject = go;
            transform = go.transform;
            AppCore = appCore;

            OnInit(args);
        }

        /// <summary>
        /// 刷新视图
        /// </summary>
        public void Refresh()
        {
            if (IsVisible)
            {
                OnRefresh();
                RefreshSubUIs();
            }
        }

        /// <summary>
        /// 销毁视图
        /// </summary>
        public void Dispose()
        {
            // 销毁所有子 SubUI
            for (int i = _subUIs.Count - 1; i >= 0; i--)
            {
                var sub = _subUIs[i];
                if (sub != null)
                {
                    sub.Dispose();
                }
            }
            _subUIs.Clear();

            OnDispose();

            OwnerUI = null;
            gameObject = null;
            transform = null;
            AppCore = null;
        }

        #region SubUI Management (Recursive)

        /// <summary>
        /// 注册子 SubUI
        /// </summary>
        /// <typeparam name="T">SubUI 类型</typeparam>
        /// <param name="go">管理的 GameObject</param>
        /// <param name="args">初始化参数</param>
        protected T RegisterSubUI<T>(GameObject go, object args = null) where T : AbstractSubUI, new()
        {
            if (go == null) return null;

            T subUI = new T();
            subUI.Init(this.OwnerUI, go, this.AppCore, args);
            _subUIs.Add(subUI);
            return subUI;
        }

        /// <summary>
        /// 刷新所有子 SubUI
        /// </summary>
        protected void RefreshSubUIs()
        {
            foreach (var ui in _subUIs)
            {
                if (ui != null && ui.IsVisible)
                {
                    ui.Refresh();
                }
            }
        }

        #endregion

        #region Virtual Methods

        /// <summary>
        /// 初始化时调用
        /// </summary>
        protected virtual void OnInit(object args) { }

        /// <summary>
        /// 刷新时调用
        /// </summary>
        protected virtual void OnRefresh() { }

        /// <summary>
        /// 销毁时调用
        /// </summary>
        protected virtual void OnDispose() { }

        #endregion
    }
}
