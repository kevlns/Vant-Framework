using System;
using Vant.Core;

namespace Vant.MVC
{
    /// <summary>
    /// MVC Model 基类
    /// 负责数据的存储、逻辑处理以及事件的监听与分发
    /// </summary>
    public abstract class AbstractModelBase : IDisposable
    {
        private ModelManager _host;
        protected AppCore appCore;

        /// <summary>
        /// ModelManager 内部调用
        /// </summary>
        public void PreInit(AppCore appCore, object host)
        {
            if (_host != null) throw new Exception("[AbstractModelBase] Model 已经初始化！");

            this.appCore = appCore;
            _host = host as ModelManager;

            RegisterEvents();
        }

        /// <summary>
        /// 注册事件
        /// </summary>
        protected virtual void RegisterEvents() { }

        /// <summary>
        /// 解绑事件
        /// </summary>
        protected virtual void UnregisterEvents() { }

        /// <summary>
        /// 子类销毁时调用，须重写
        /// </summary>
        protected virtual void OnDispose() { }

        /// <summary>
        /// 标准的 Dispose 模式入口。不要重写此方法。
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            _host = null;
            UnregisterEvents();
            OnDispose();
        }
    }
}
