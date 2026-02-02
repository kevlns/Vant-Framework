using System;
using Vant.System.FSM;
using Vant.Core;

namespace Vant.GamePlay.Procedure
{
    /// <summary>
    /// 流程管理器
    /// 负责管理游戏的整体流程 (初始化 -> 登录 -> 大厅 -> 战斗 等)
    /// </summary>
    public class ProcedureManager
    {
        private IFsm<ProcedureManager> _fsm;

        /// <summary>
        /// 获取当前流程
        /// </summary>
        public ProcedureBase CurrentProcedure => _fsm?.CurrentState as ProcedureBase;

        /// <summary>
        /// 获取 AppCore 引用 (供流程状态使用)
        /// </summary>
        private AppCore _appCore;

        public ProcedureManager(AppCore appCore)
        {
            _appCore = appCore;
            _appCore.GameLifeCycle.OnUpdateEvent += OnUpdate;
        }

        /// <summary>
        /// 初始化流程管理器
        /// </summary>
        /// <param name="procedures">所有可用的流程状态</param>
        public void Init(params ProcedureBase[] procedures)
        {
            _fsm = new Fsm<ProcedureManager>(this, procedures);
        }

        /// <summary>
        /// 启动流程
        /// </summary>
        public void StartProcedure<T>() where T : ProcedureBase
        {
            _fsm.Start<T>();
        }

        /// <summary>
        /// 是否是当前流程
        /// </summary>
        public bool IsCurrentProcedure<T>() where T : ProcedureBase
        {
            if (_fsm == null)
            {
                return false;
            }

            return _fsm.CurrentState is T;
        }

        /// <summary>
        /// 轮询流程
        /// </summary>
        private void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            _fsm?.Update(elapseSeconds, realElapseSeconds);
        }
    }
}
