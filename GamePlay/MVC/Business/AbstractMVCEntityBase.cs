using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vant.Core;
using Vant.System;

namespace Vant.MVC
{
    /// <summary>
    /// MVC实例基类，持有自定义的Controller、View和ViewModel实例
    /// </summary>
    public abstract class AbstractMVCEntityBase<TController, TView, TViewModel> : IMVCEntity
        where TController : AbstractControllerBase<TView, TViewModel>
        where TView : AbstractGeneralViewBase
        where TViewModel : class, IViewModel
    {
        public TController Controller { get; private set; }
        public TView View { get; private set; }
        public TViewModel ViewModel { get; private set; }
        public Notifier Notifier { get; private set; }

        private bool _isInitialized = false;
        private bool _isInitializing = false;

        public AbstractMVCEntityBase(TController controller, TView view, TViewModel viewModel, bool showOnInit, CancellationToken token = default)
        {
            if (_isInitialized || _isInitializing) return;
            Initialize(controller, view, viewModel, showOnInit, token).Forget();
        }

        public async UniTask Initialize(TController controller, TView view, TViewModel viewModel, bool showOnInit, CancellationToken token = default)
        {
            if (_isInitialized || _isInitializing) return;
            _isInitializing = true;

            try
            {
                Controller = controller;
                View = view;
                ViewModel = viewModel;

                Notifier ??= new Notifier();

                ViewModel?.BindNotifier(Notifier);
                ViewModel?.BindProperties();
                View?.BindNotifier(Notifier);
                View?.InitView();
                Controller?.InternalInit(View, ViewModel);
                _isInitialized = true;
                SetViewActive(showOnInit, true);
                await UniTask.DelayFrame(1, cancellationToken: token);
            }
            catch (Exception)
            {
                Destroy();
                throw;
            }
            finally
            {
                _isInitializing = false;
                if (!_isInitialized)
                {
                    Controller = null;
                    View = null;
                    ViewModel = null;
                }
            }
        }

        /// <summary>
        /// 重置实体并重新初始化（会释放上一次资源）
        /// </summary>
        public async UniTask Reset(TController controller, TView view, TViewModel viewModel, bool showOnInit, CancellationToken token = default)
        {
            if (_isInitializing) return;

            Cleanup();
            await Initialize(controller, view, viewModel, showOnInit, token);
        }

        /// <summary>
        /// IMVCEntity Reset (non-generic)
        /// </summary>
        public async UniTask Reset(object controller, AbstractGeneralViewBase view, object viewModel, bool showOnInit, CancellationToken token = default)
        {
            if (controller is not TController typedController)
            {
                throw new InvalidCastException($"Controller type mismatch. Expected {typeof(TController).Name}.");
            }

            if (view is not TView typedView)
            {
                throw new InvalidCastException($"View type mismatch. Expected {typeof(TView).Name}.");
            }

            if (viewModel is not TViewModel typedViewModel)
            {
                throw new InvalidCastException($"ViewModel type mismatch. Expected {typeof(TViewModel).Name}.");
            }

            await Reset(typedController, typedView, typedViewModel, showOnInit, token);
        }

        private void Update(float deltaTime, float unscaledDeltaTime)
        {
            Controller?.Update(deltaTime, unscaledDeltaTime);
        }

        public void Destroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            // Always stop update subscription
            AppCore.Instance.GameLifeCycle.OnUpdateEvent -= Update;

            View?.HideView();
            Controller?.Destroy();
            View?.DestroyView();
            ViewModel?.Destroy();
            Notifier?.Clear();

            _isInitialized = false;
            _isInitializing = false;

            Controller = null;
            View = null;
            ViewModel = null;
            Notifier = null;
        }

        private void SetViewActive(bool enable, bool stopUpdate = true)
        {
            if (!_isInitialized) return;

            if (enable)
            {
                View?.ShowView();
                AppCore.Instance.GameLifeCycle.OnUpdateEvent -= Update;
                AppCore.Instance.GameLifeCycle.OnUpdateEvent += Update;
            }
            else
            {
                View?.HideView();
                if (stopUpdate) AppCore.Instance.GameLifeCycle.OnUpdateEvent -= Update;
            }
        }
    }
}
