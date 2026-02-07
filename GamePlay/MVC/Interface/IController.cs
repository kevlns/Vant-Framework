

namespace Vant.MVC
{
    public interface IController
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public void Init();

        /// <summary>
        /// 每帧更新
        /// </summary>
        public void Update(float deltaTime, float unscaledDeltaTime = 0);
        
        /// <summary>
        /// 销毁
        /// </summary>
        public void Destroy();
    }
}