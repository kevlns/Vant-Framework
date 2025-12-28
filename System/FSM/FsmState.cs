namespace Vant.System.FSM
{
    /// <summary>
    /// FSM 状态基类
    /// </summary>
    /// <typeparam name="T">状态持有者类型</typeparam>
    public abstract class FsmState<T> where T : class
    {
        /// <summary>
        /// 初始化时调用
        /// </summary>
        public virtual void OnInit(IFsm<T> fsm) { }

        /// <summary>
        /// 进入状态时调用
        /// </summary>
        public virtual void OnEnter(IFsm<T> fsm) { }

        /// <summary>
        /// 轮询时调用
        /// </summary>
        public virtual void OnUpdate(IFsm<T> fsm, float elapseSeconds, float realElapseSeconds) { }

        /// <summary>
        /// 离开状态时调用
        /// </summary>
        /// <param name="isShutdown">是否是关闭状态机时触发</param>
        public virtual void OnExit(IFsm<T> fsm, bool isShutdown) { }

        /// <summary>
        /// 销毁时调用
        /// </summary>
        public virtual void OnDestroy(IFsm<T> fsm) { }
        
        /// <summary>
        /// 切换状态的快捷方法
        /// </summary>
        protected void ChangeState<TState>(IFsm<T> fsm) where TState : FsmState<T>
        {
            fsm.ChangeState<TState>();
        }
    }
}
