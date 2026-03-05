using System.Collections.Generic;

namespace JulyCore.Provider.Fsm
{
    /// <summary>
    /// 状态机提供者接口
    /// 提供通用状态机管理能力，支持任意状态类型
    /// </summary>
    public interface IFsmProvider : Core.IProvider
    {
        /// <summary>
        /// 创建状态机
        /// </summary>
        /// <param name="owner">状态机拥有者</param>
        /// <param name="states">状态列表</param>
        /// <param name="defaultState">默认状态</param>
        /// <returns>状态机实例</returns>
        IFsm CreateFsm(
            object owner,
            Dictionary<int, IFsmState> states,
            int defaultState);

        /// <summary>
        /// 销毁状态机
        /// </summary>
        /// <param name="fsm">状态机实例</param>
        void DestroyFsm(IFsm fsm);

        /// <summary>
        /// 销毁所有状态机
        /// </summary>
        void DestroyAllFsms();
    }

    /// <summary>
    /// 状态机接口
    /// </summary>
    public interface IFsm
    {
        /// <summary>
        /// 状态机拥有者
        /// </summary>
        object Owner { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        int CurrentState { get; }

        /// <summary>
        /// 上一个状态
        /// </summary>
        int PreviousState { get; }

        /// <summary>
        /// 状态历史记录（支持回退）
        /// </summary>
        IReadOnlyList<int> StateHistory { get; }

        /// <summary>
        /// 切换状态
        /// </summary>
        /// <param name="newState">新状态</param>
        /// <returns>是否切换成功</returns>
        bool ChangeState(int newState);

        /// <summary>
        /// 更新状态机
        /// </summary>
        /// <param name="elapseSeconds">游戏时间流逝（秒）</param>
        /// <param name="realElapseSeconds">真实时间流逝（秒）</param>
        void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 检查是否可以切换到指定状态
        /// </summary>
        /// <param name="state">目标状态</param>
        /// <returns>是否可以切换</returns>
        bool CanChangeTo(int state);
    }

    /// <summary>
    /// 状态接口
    /// </summary>
    public interface IFsmState
    {
        /// <summary>
        /// 状态机引用（由框架自动注入）
        /// </summary>
        IFsm Fsm { get; }

        /// <summary>
        /// 初始化状态（由框架调用，注入 FSM 引用）
        /// </summary>
        /// <param name="fsm">状态机实例</param>
        void OnInit(IFsm fsm);

        /// <summary>
        /// 进入状态时调用
        /// </summary>
        void OnEnter();

        /// <summary>
        /// 状态更新时调用
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// 离开状态时调用
        /// </summary>
        void OnExit();

        /// <summary>
        /// 检查是否可以切换到指定状态
        /// </summary>
        /// <param name="targetState">目标状态</param>
        /// <returns>是否可以切换</returns>
        bool CanChangeTo(int targetState);
    }

    /// <summary>
    /// 状态基类（推荐继承此类而非直接实现 IFsmState）
    /// 提供 FSM 引用和便捷的状态切换方法
    /// </summary>
    public abstract class FsmStateBase : IFsmState
    {
        /// <summary>
        /// 状态机引用
        /// </summary>
        public IFsm Fsm { get; private set; }

        /// <summary>
        /// 状态机拥有者
        /// </summary>
        protected object Owner => Fsm?.Owner;

        /// <summary>
        /// 初始化状态（由框架调用）
        /// </summary>
        public void OnInit(IFsm fsm)
        {
            Fsm = fsm;
            OnInitialize();
        }

        /// <summary>
        /// 状态初始化（子类可重写）
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// 进入状态
        /// </summary>
        public abstract void OnEnter();

        /// <summary>
        /// 状态更新
        /// </summary>
        public virtual void OnUpdate()
        {
        }

        /// <summary>
        /// 离开状态
        /// </summary>
        public virtual void OnExit()
        {
        }

        /// <summary>
        /// 检查是否可以切换到目标状态
        /// </summary>
        public virtual bool CanChangeTo(int targetState)
        {
            return true;
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        /// <param name="newState">新状态</param>
        /// <returns>是否切换成功</returns>
        protected bool ChangeState(int newState)
        {
            return Fsm?.ChangeState(newState) ?? false;
        }
    }
}

