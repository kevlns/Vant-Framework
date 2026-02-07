using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vant.System;

namespace Vant.MVC
{
    /// <summary>
    /// MVC View 基类
    /// 定义视图的基本生命周期：初始化 -> 展示前 -> 刷新 -> 展示后 -> 隐藏/销毁
    /// </summary>
    public abstract class AbstractGeneralViewBase : IView
    {
        /// <summary>
        /// 绑定的视图 GameObject（可选）
        /// </summary>
        public GameObject gameObject { get; private set; }

        /// <summary>
        /// 实体级 Notifier（可选）
        /// </summary>
        public Notifier Notifier { get; private set; }

        /// <summary>
        /// 绑定视图 GameObject（可选）
        /// </summary>
        public virtual void BindViewObject(GameObject viewObject)
        {
            gameObject = viewObject;
        }

        /// <summary>
        /// 绑定实体级 Notifier（可选）
        /// </summary>
        public virtual void BindNotifier(Notifier notifier)
        {
            Notifier = notifier;
        }

        /// <summary>
        /// 视图注册时的配置
        /// 子类通常返回静态配置
        /// </summary>
        public virtual GeneralViewConfig RegisterConfig => null;

        #region IView 接口实现

        public void InitView(object args = null) => OnCreate();

        public void ShowView(object args = null)
        {
            ShowViewAsync(args).Forget();
        }

        private async UniTask ShowViewAsync(object args)
        {
            try
            {
                await OnBeforeShow(args);
                OnRefreshOnceOnOpen();
                await OnAfterShow();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                throw;
            }
        }

        public void HideView() => OnHide();

        public void DestroyView()
        {
            OnDestroy();
            gameObject = null;
            Notifier = null;
        }

        #endregion


        #region 生命周期方法

        /// <summary>
        /// 创建时调用 (只调用一次)
        /// </summary>
        protected virtual void OnCreate() { }

        /// <summary>
        /// 打开前调用
        /// </summary>
        protected virtual async UniTask OnBeforeShow(object args) { await UniTask.CompletedTask; }

        /// <summary>
        /// 打开UI时立即刷新一次
        /// </summary>
        protected virtual void OnRefreshOnceOnOpen() { }

        /// <summary>
        /// 打开后调用 (动画播放完毕后)
        /// </summary>
        protected virtual async UniTask OnAfterShow() { await UniTask.CompletedTask; }

        /// <summary>
        /// 关闭时调用
        /// </summary>
        protected virtual void OnHide() { }

        /// <summary>
        /// 销毁时调用
        /// </summary>
        protected virtual void OnDestroy() { }

        #endregion
    }
}
