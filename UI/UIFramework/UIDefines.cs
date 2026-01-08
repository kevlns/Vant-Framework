using System;
using Cysharp.Threading.Tasks;
using Vant.MVC;

namespace Vant.UI.UIFramework
{
    /// <summary>
    /// UI 层级定义
    /// </summary>
    public enum UILayer
    {
        Bottom = 0,       // 底层 (如主界面背景)
        Video = 500,      // 视频层 (通常在背景之上，UI 之下)
        Timeline = 600,   // Timeline 层
        Normal = 1000,    // 普通层 (大多数窗口)
        Popup = 2000,     // 弹窗层 (确认框等)
        Guide = 2500,     // 新手引导层 (通常在弹窗之上)
        Loading = 3000,   // Loading 层 (阻挡操作)
        System = 4000     // 系统层 (断线重连、错误提示等)
    }

    /// <summary>
    /// UI 叠加模式
    /// </summary>
    public enum UIMode
    {
        /// <summary>
        /// 叠加模式：不影响下面的 UI
        /// </summary>
        Overlay,

        /// <summary>
        /// 独占模式：隐藏下面的 UI (性能优化)
        /// </summary>
        HideOther,

        /// <summary>
        /// 单例栈模式：打开时，关闭栈中所有其他 UI (如回到主界面)
        /// </summary>
        Single
    }

    /// <summary>
    /// UI 事件定义
    /// </summary>
    public enum UICommonEvent
    {
        OPEN_UI,
        CLOSE_UI,
        UI_OPENED,
        UI_CLOSED
    }

    /// <summary>
    /// UI 配置数据
    /// </summary>
    public class UIConfig
    {
        /// <summary>
        /// UI 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// UI C# 类型
        /// </summary>
        public Type UIClass { get; set; }

        /// <summary>
        /// 资源路径 (Addressables Key)
        /// </summary>
        public string AssetPath { get; set; }

        /// <summary>
        /// UI 层级
        /// </summary>
        public UILayer Layer { get; set; } = UILayer.Normal;

        /// <summary>
        /// 叠加模式
        /// </summary>
        public UIMode Mode { get; set; } = UIMode.Overlay;

        /// <summary>
        /// 是否需要背景遮罩 (模态窗口)
        /// 如果为 true，UIManager 会在该 UI 下方生成一个遮罩，屏蔽更低层级的操作
        /// </summary>
        public bool NeedMask { get; set; } = false;

        /// <summary>
        /// 是否参与缓存 (LRU)
        /// </summary>
        public bool IsCacheable { get; set; } = true;

        /// <summary>
        /// 是否允许多个实例
        /// </summary>
        public bool AllowMultiInstance { get; set; } = false;

        /// <summary>
        /// 入场动画代理
        /// </summary>
        public Func<AbstractUIBase, UniTask> EnterAnimation { get; set; }

        /// <summary>
        /// 出场动画代理
        /// </summary>
        public Func<AbstractUIBase, UniTask> ExitAnimation { get; set; }
    }
}
