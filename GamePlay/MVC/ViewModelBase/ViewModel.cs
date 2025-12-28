namespace Vant.MVC
{
    /// <summary>
    /// Base class for ViewModels.
    /// Only provides interfaces for Binding and Disposing.
    /// </summary>
    public abstract class ViewModel
    {
        /// <summary>
        /// Called when the ViewModel is bound to the View/Controller.
        /// Use this to initialize BindableProperties with Model data.
        /// </summary>
        public virtual void OnBind() { }

        /// <summary>
        /// Called when the ViewModel is no longer needed.
        /// Use this to clear bindings or references.
        /// </summary>
        public virtual void OnDispose() { }
    }
}
