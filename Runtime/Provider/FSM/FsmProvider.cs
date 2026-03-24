using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.Fsm
{
    /// <summary>
    /// 状态机实现
    /// </summary>
    internal class Fsm : IFsm
    {
        private readonly Dictionary<int, IFsmState> _states;
        private IFsmState _currentInstance;
        private int _current;
        private int _previousState;
        private readonly List<int> _stateHistory;
        private readonly int _maxHistorySize;

        public object Owner { get; }
        public int CurrentState { get; }
        public int PreviousState => _previousState;
        public IReadOnlyList<int> StateHistory => _stateHistory;

        public Fsm(object owner, Dictionary<int, IFsmState> states, int defaultState,
            int maxHistorySize = 10)
        {
            Owner = owner;
            _states = states ?? throw new ArgumentNullException(nameof(states));
            _current = defaultState;
            _previousState = defaultState;
            _stateHistory = new List<int> { defaultState };
            _maxHistorySize = maxHistorySize;

            // 初始化所有状态，注入 FSM 引用
            foreach (var state in _states.Values)
            {
                state.OnInit(this);
            }

            // 调用初始状态的 OnEnter
            if (_states.TryGetValue(defaultState, out var defaultInstance))
            {
                _currentInstance = defaultInstance;
                defaultInstance.OnEnter();
            }
        }

        public bool ChangeState(int newState)
        {
            if (EqualityComparer<int>.Default.Equals(_current, newState))
            {
                return false;
            }

            if (!_states.TryGetValue(newState, out var newStateInstance))
            {
                JLogger.LogChannel(LogChannel.Fsm, GetType().Name, $"[Fsm] 状态 {newState} 不存在");
                return false;
            }

            // 检查是否可以切换
            if (!_currentInstance.CanChangeTo(newState))
            {
                JLogger.LogChannel(LogChannel.Fsm, GetType().Name, $"[Fsm] 无法从状态 {_current} 切换到 {newState}");
                return false;
            }

            _currentInstance.OnExit();

            // 更新状态
            _previousState = _current;
            _current = newState;
            _currentInstance = newStateInstance;

            // 添加到历史记录
            _stateHistory.Add(newState);
            if (_stateHistory.Count > _maxHistorySize)
            {
                _stateHistory.RemoveAt(0);
            }

            // 进入新状态
            newStateInstance.OnEnter();

            return true;
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            _currentInstance?.OnUpdate();
        }

        public bool CanChangeTo(int state)
        {
            if (!_states.ContainsKey(state))
            {
                return false;
            }

            _currentInstance.CanChangeTo(state);

            return true;
        }
    }

    /// <summary>
    /// 状态机提供者实现
    /// </summary>
    internal class FsmProvider : ProviderBase, IFsmProvider
    {
        public override int Priority => Frameworkconst.PriorityPoolProvider; // 使用 Pool 优先级（功能服务层）
        protected override LogChannel LogChannel => LogChannel.Fsm;

        private readonly HashSet<IFsm> _fsms = new();

        protected override UniTask OnInitAsync()
        {
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            DestroyAllFsms();
        }

        public IFsm CreateFsm(
            object owner,
            Dictionary<int, IFsmState> states,
            int defaulint)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (states == null || states.Count == 0)
            {
                throw new ArgumentException("状态列表不能为空", nameof(states));
            }

            var fsm = new Fsm(owner, states, defaulint);
            lock (_fsms)
            {
                _fsms.Add(fsm);
            }

            return fsm;
        }

        public void DestroyFsm(IFsm fsm)
        {
            if (fsm == null)
            {
                return;
            }

            lock (_fsms)
            {
                _fsms.Remove(fsm);
            }
        }

        public void DestroyAllFsms()
        {
            lock (_fsms)
            {
                _fsms.Clear();
            }
        }
    }
}