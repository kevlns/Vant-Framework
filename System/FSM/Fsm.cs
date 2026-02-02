using System;
using System.Collections.Generic;

namespace Vant.System.FSM
{
    /// <summary>
    /// FSM 接口
    /// </summary>
    public interface IFsm<T> where T : class
    {
        T Owner { get; }
        FsmState<T> CurrentState { get; }
        float CurrentStateTime { get; }
        void Start<TState>() where TState : FsmState<T>;
        void ChangeState<TState>() where TState : FsmState<T>;
        void Update(float elapseSeconds, float realElapseSeconds);
        void Shutdown();
    }

    /// <summary>
    /// FSM 状态机实现
    /// </summary>
    public class Fsm<T> : IFsm<T> where T : class
    {
        private readonly T _owner;
        private readonly Dictionary<Type, FsmState<T>> _states;
        private FsmState<T> _currentState;
        private float _currentStateTime;
        private bool _isDestroyed;

        public T Owner => _owner;
        public FsmState<T> CurrentState => _currentState;
        public float CurrentStateTime => _currentStateTime;

        public Fsm(T owner, params FsmState<T>[] states)
        {
            _owner = owner;
            _states = new Dictionary<Type, FsmState<T>>();
            foreach (var state in states)
            {
                if (state == null) continue;
                Type type = state.GetType();
                if (_states.ContainsKey(type))
                {
                    throw new Exception($"FSM state {type.FullName} is already exist.");
                }
                _states.Add(type, state);
                state.Fsm = this;
            }
        }

        public void Start<TState>() where TState : FsmState<T>
        {
            if (_isDestroyed) return;
            Type type = typeof(TState);
            if (!_states.TryGetValue(type, out var state))
            {
                throw new Exception($"FSM state {type.FullName} is not exist.");
            }

            _currentState = state;
            _currentStateTime = 0f;
            _currentState.OnEnter();
        }

        public void ChangeState<TState>() where TState : FsmState<T>
        {
            if (_isDestroyed) return;
            if (_currentState == null)
            {
                Start<TState>();
                return;
            }

            Type type = typeof(TState);
            if (!_states.TryGetValue(type, out var state))
            {
                throw new Exception($"FSM state {type.FullName} is not exist.");
            }

            _currentState.OnExit(false);
            _currentState = state;
            _currentStateTime = 0f;
            _currentState.OnEnter();
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (_currentState == null || _isDestroyed) return;
            _currentStateTime += elapseSeconds;
            _currentState.OnUpdate(elapseSeconds, realElapseSeconds);
        }

        public void Shutdown()
        {
            if (_isDestroyed) return;
            if (_currentState != null)
            {
                _currentState.OnExit(true);
                _currentState = null;
            }

            foreach (var state in _states.Values)
            {
                state.OnDestroy();
            }
            _states.Clear();
            _isDestroyed = true;
        }
    }
}
