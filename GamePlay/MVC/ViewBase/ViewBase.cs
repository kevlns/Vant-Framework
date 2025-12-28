using Cysharp.Threading.Tasks;

namespace Vant.MVC
{
    /// <summary>
    /// Base class for Views that use a Controller and ViewModel.
    /// </summary>
    /// <typeparam name="TController">The Controller type.</typeparam>
    /// <typeparam name="TView">The View type (usually the class itself).</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    public abstract class ViewBase<TController, TView, TViewModel> : AbstractViewBase
        where TController : AbstractController<TView, TViewModel>, new()
        where TView : AbstractViewBase
        where TViewModel : ViewModel, new()
    {
        /// <summary>
        /// The Controller instance.
        /// </summary>
        protected TController Controller { get; private set; }

        protected override void OnInit(object args)
        {
            base.OnInit(args);
            Controller = new TController();
            // Controller will create and bind the ViewModel internally
            Controller.Init(this as TView);
        }

        protected override async UniTask OnBeforeShow(object args)
        {
            await base.OnBeforeShow(args);
            Controller.OnOpen(args);
        }

        protected override async UniTask OnBeforeHide()
        {
            Controller.OnClose();
            await base.OnBeforeHide();
        }

        protected override void OnDispose()
        {
            Controller.OnDestroy();
            base.OnDispose();
        }
    }
}
