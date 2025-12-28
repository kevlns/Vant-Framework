namespace Vant.MVC
{
    /// <summary>
    /// Controller Base Class with ViewModel support.
    /// Acts as the Binder between View and ViewModel.
    /// </summary>
    /// <typeparam name="TView">The View type.</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    public abstract class AbstractController<TView, TViewModel>
        where TView : AbstractViewBase
        where TViewModel : ViewModel, new()
    {
        protected TView View { get; private set; }
        protected TViewModel ViewModel { get; private set; }

        /// <summary>
        /// Initializes the Controller with the View.
        /// Creates and binds the ViewModel.
        /// </summary>
        public virtual void Init(TView view)
        {
            View = view;
            ViewModel = new TViewModel();

            // Initialize ViewModel (Bind to Model data)
            ViewModel.OnBind();

            // Bind View to ViewModel
            OnBind();
        }

        /// <summary>
        /// Implement this to bind View events to ViewModel commands, 
        /// and ViewModel properties to View updates.
        /// </summary>
        protected abstract void OnBind();

        public virtual void OnOpen(object args)
        {
            // Optional: Pass args to ViewModel if needed
        }

        public virtual void OnClose() { }

        public virtual void OnDestroy()
        {
            ViewModel?.OnDispose();
            View = null;
            ViewModel = null;
        }
    }
}
