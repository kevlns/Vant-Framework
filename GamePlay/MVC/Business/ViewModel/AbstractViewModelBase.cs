using Vant.System;

namespace Vant.MVC
{
    /// <summary>
    /// Base class for ViewModels.
    /// Only provides interfaces for Binding and Disposing.
    /// </summary>
    public abstract class AbstractViewModelBase : IViewModel
    {
        /// <summary>
        /// 实体级 Notifier
        /// </summary>
        protected Notifier Notifier { get; private set; }

        /// <summary>
        /// 绑定实体级 Notifier
        /// </summary>
        public virtual void BindNotifier(Notifier notifier)
        {
            Notifier = notifier;
        }

        /// <summary>
        /// Called when the ViewModel is bound to the View/Controller.
        /// Use this to initialize BindableProperties with Model data.
        /// </summary>
        public virtual void BindProperties() { }

        /// <summary>
        /// Called when the ViewModel is no longer needed.
        /// Use this to clear bindings or references.
        /// </summary>
        public virtual void Destroy() { }
    }
}
