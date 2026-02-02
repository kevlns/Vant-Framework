namespace Vant.System.FSM
{
    /// <summary>
    /// FSM 状态基类
    /// </summary>
    /// <typeparam name="T">状态持有者类型</typeparam>
    public abstract class FsmState<T> where T : class
    {
        public IFsm<T> Fsm { get; set; }

        /// <summary>
        /// 进入状态时调用
        /// </summary>
        public virtual void OnEnter() { }

        /// <summary>
        /// 轮询时调用
        /// </summary>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds) { }

        /// <summary>
        /// 离开状态时调用
        /// </summary>
        /// <param name="isShutdown">是否是关闭状态机时触发</param>
        public virtual void OnExit(bool isShutdown) { }

        /// <summary>
        /// 销毁时调用
        /// </summary>
        public virtual void OnDestroy() { }

        /// <summary>
        /// 切换状态的快捷方法
        /// </summary>
        protected void ChangeState<TState>() where TState : FsmState<T>
        {
            Fsm.ChangeState<TState>();
        }
    }
}
