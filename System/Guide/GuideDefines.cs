namespace Vant.System.Guide
{
    /// <summary>
    /// 引导事件定义
    /// </summary>
    public enum GuideInternalEvent
    {
        TryStartGuide
    }

    /// <summary>
    /// 引导步骤类型
    /// 使用 partial class + const string 方便扩展，避免修改框架代码
    /// </summary>
    public static partial class GuideStepType
    {
        public const string None = "None";
    }
}
