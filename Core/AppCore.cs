using Vant.System;
using Vant.System.GM;
using Vant.System.Guide;
using Vant.LubanConfig;
using Vant.MVC;
using Vant.UI.UIFramework;
using Vant.Resources;
using Vant.GamePlay.Procedure;
using Vant.Net;
using System;

namespace Vant.Core
{
    public interface IGameLifeCycle
    {
        event Action<float, float> OnUpdate;
        event Action<float, float> OnLateUpdate;
        event Action<float> OnFixedUpdate;
        event Action<bool> OnApplicationFocusChanged;
        event Action<bool> OnApplicationPauseChanged;
        event Action OnApplicationQuitRequest;
        event Action OnDestroyed;
    }

    /// <summary>
    /// MVC 核心类
    /// 管理 ModelManager, ViewManager, ControllerManager 以及 Notifier
    /// </summary>
    public class AppCore
    {

        public enum ECodeMode
        {
            Debug,
            Release
        }

        public static class GlobalSettings
        {
            public static ECodeMode CODE_MODE = ECodeMode.Debug;
            public static bool LUBAN_HOTFIX = false;
            public static string LUBAN_CONFIG_PATH_NON_HF = "";  // 非热更模式下，相对于 Assets/Resources/ 的路径
            public static string LUBAN_CONFIG_PATH_HF = "";  // 热更模式下，AB包的路径
            public static uint UI_LRU_MAX_SIZE = 5;
        }

        /// <summary>
        /// 全局单例引用 (方便调试工具访问)
        /// </summary>
        public static AppCore Instance { get; private set; }

        /// <summary>
        /// 核心事件通知器
        /// </summary>
        public Notifier Notifier { get; private set; }

        /// <summary>
        /// 配置管理器
        /// </summary>
        public ConfigManager ConfigManager { get; private set; }

        /// <summary>
        /// 数据模型管理器
        /// </summary>
        public ModelManager ModelManager { get; private set; }

        /// <summary>
        /// UI 管理器
        /// </summary>
        public UIManager UIManager { get; private set; }

        /// <summary>
        /// 流程管理器
        /// </summary>
        public ProcedureManager ProcedureManager { get; private set; }

        /// <summary>
        /// 网络管理器
        /// </summary>
        public NetManager NetManager { get; private set; }

        /// <summary>
        /// GM 管理器
        /// </summary>
        public GMManager GMManager { get; private set; }

        /// <summary>
        /// 引导管理器
        /// </summary>
        public GuideManager GuideManager { get; private set; }

        /// <summary>
        /// Addressables 资源管理器
        /// </summary>
        public IAssetManager MainAssetManager { get; private set; }

        /// <summary>
        /// Resources 资源管理器
        /// </summary>
        public IAssetManager FallbackAssetManager { get; private set; }

        private IGameLifeCycle _gameLifeCycle;

        public AppCore(IGameLifeCycle gameLifeCycle)
        {
            Instance = this;
            _gameLifeCycle = gameLifeCycle;

            // 1. 初始化基础服务
            Notifier = new Notifier();
            GMManager = new GMManager();
            GuideManager = GuideManager.Instance;
            GuideManager.Initialize();

            // 2. 初始化资源管理器 (Addressables 为主，Resources 为辅)
            MainAssetManager = new AddressablesManager();
            FallbackAssetManager = new ResourcesAssetManager();

            // 3. 初始化业务模块并注入依赖
            ConfigManager = new ConfigManager();
            ModelManager = new ModelManager(this);

            // 4. 初始化 UI 管理器，注入依赖
            UIManager = new UIManager(this, MainAssetManager, FallbackAssetManager);

            // 5. 初始化流程管理器
            ProcedureManager = new ProcedureManager(this);

            // 6. 初始化网络管理器
            NetManager = new NetManager(this);

            GameLifeCycleSubscribe();
        }

        private void GameLifeCycleSubscribe()
        {
            if (_gameLifeCycle != null)
            {
                _gameLifeCycle.OnUpdate += OnUpdate;
                _gameLifeCycle.OnLateUpdate += OnLateUpdate;
                _gameLifeCycle.OnFixedUpdate += OnFixedUpdate;
                _gameLifeCycle.OnApplicationFocusChanged += OnApplicationFocusChanged;
                _gameLifeCycle.OnApplicationPauseChanged += OnApplicationPauseChanged;
                _gameLifeCycle.OnApplicationQuitRequest += OnApplicationQuitRequest;
                _gameLifeCycle.OnDestroyed += OnDestroyed;
            }
        }

        private void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            ProcedureManager.OnUpdate(deltaTime, unscaledDeltaTime);
            NetManager.OnUpdate(deltaTime, unscaledDeltaTime);
            GuideManager.Update(deltaTime);
        }

        private void OnLateUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        private void OnFixedUpdate(float fixedDeltaTime)
        {
        }

        private void OnApplicationFocusChanged(bool hasFocus)
        {
        }

        private void OnApplicationPauseChanged(bool pauseStatus)
        {
        }

        private void OnApplicationQuitRequest()
        {
            NetManager.OnApplicationQuit();
        }

        private void OnDestroyed()
        {
            NetManager.OnDestroy();
        }
    }
}
