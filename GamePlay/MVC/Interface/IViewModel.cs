

namespace Vant.MVC
{
    public interface IViewModel
    {
        /// <summary>
        /// 绑定实体级 Notifier
        /// </summary>
        public void BindNotifier(Vant.System.Notifier notifier);

        /// <summary>
        /// 绑定视图的属性和事件
        /// </summary>
        public void BindProperties();

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Destroy();
    }
}