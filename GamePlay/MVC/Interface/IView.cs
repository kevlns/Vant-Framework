using Cysharp.Threading.Tasks;

namespace Vant.MVC
{
    public interface IView
    {
        /// <summary>
        /// 初始化视图
        /// </summary>
        public void InitView(object args = null);

        /// <summary>
        /// 显示视图
        /// </summary>
        public void ShowView(object args = null);

        /// <summary>
        /// 关闭视图
        /// </summary>
        public void HideView();

        /// <summary>
        /// 销毁视图
        /// </summary>
        public void DestroyView();
    }
}