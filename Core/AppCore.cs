using Vant.System;
using Vant.System.Scene;
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
        event Action<float, float> OnUpdateEvent;
        event Action<float, float> OnLateUpdateEvent;
        event Action<float> OnFixedUpdateEvent;
        event Action<bool> OnApplicationFocusEvent;
        event Action<bool> OnApplicationPauseEvent;
        event Action OnApplicationQuitEvent;
        event Action OnDestroyEvent;
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
            public static string LUBAN_CONFIG_PATH_NON_HF = "";  // 非热更模式下，相对于 Assets/Resources/ 的路径前缀
            public static string LUBAN_CONFIG_PATH_HF = "";  // 热更模式下，AB包的路径前缀
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
        /// 场景管理器
        /// </summary>
        public SceneManager SceneManager { get; private set; }

        /// <summary>
        /// 主资源管理器，默认为 Addressables 资源管理器
        /// </summary>
        public IAssetManager MainAssetManager { get; private set; }

        /// <summary>
        /// 降级备用资源管理器，默认为 ResourcesManager 资源管理器（用于在主资源管理器未找到资源时，尝试二次搜索）
        /// </summary>
        public IAssetManager FallbackAssetManager { get; private set; }

        /// <summary>
        /// 游戏任务管理器，默认的任务链为非熔断模式
        /// </summary>
        public TaskManager TaskManager { get; private set; }

        /// <summary>
        /// 运行时游戏生命周期，可被订阅
        /// </summary>
        private IGameLifeCycle _gameLifeCycle;
        public IGameLifeCycle GameLifeCycle => _gameLifeCycle;

        public AppCore(IGameLifeCycle gameLifeCycle, IAssetManager mainAssetManager = null, IAssetManager fallbackAssetManager = null)
        {
            Instance = this;
            _gameLifeCycle = gameLifeCycle;

            // 1. 初始化基础服务
            ConfigManager = new ConfigManager();
            Notifier = new Notifier();
            GMManager = new GMManager();

            // 2. 初始化资源管理器 (Addressables 为主，Resources 为辅)
            MainAssetManager = mainAssetManager ?? new AddressablesManager();
            FallbackAssetManager = fallbackAssetManager ?? new ResourcesAssetManager();

            // 3. 初始化业务模块并注入依赖
            ModelManager = new ModelManager(this);

            // 4. 初始化 UI 管理器，注入依赖
            UIManager = new UIManager(this, MainAssetManager, FallbackAssetManager);

            // 5. 初始化流程管理器
            ProcedureManager = new ProcedureManager(this);

            // 6. 初始化网络管理器
            NetManager = new NetManager(this);

            // 7. 初始化任务管理器
            TaskManager = new TaskManager(this);

            // 8. 初始化引导管理器
            GuideManager = new GuideManager(this);

            // 9. 初始化场景管理器
            SceneManager = new SceneManager(this, MainAssetManager);
        }
    }
}
