using System.Collections.Generic;
using JulyCore.Core;

namespace JulyCore.Provider.Fsm
{
    public interface IFsm
    {
        object Owner { get; }
        int CurrentState { get; }
        int PreviousState { get; }
        IReadOnlyList<int> StateHistory { get; }

        bool ChangeState(int newState);
        void Update(float elapseSeconds, float realElapseSeconds);
        bool CanChangeTo(int state);
    }

    public interface IFsmState
    {
        IFsm Fsm { get; }

        void OnInit(IFsm fsm);
        void OnEnter();
        void OnUpdate();
        void OnExit();
        bool CanChangeTo(int targetState);
    }

    public abstract class FsmStateBase : IFsmState
    {
        public IFsm Fsm { get; private set; }
        protected object Owner => Fsm?.Owner;

        public void OnInit(IFsm fsm)
        {
            Fsm = fsm;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }
        public abstract void OnEnter();
        public virtual void OnUpdate() { }
        public virtual void OnExit() { }
        public virtual bool CanChangeTo(int targetState) => true;

        protected bool ChangeState(int newState) => Fsm?.ChangeState(newState) ?? false;
    }

    public interface IFsmProvider : IProvider
    {
        IFsm CreateFsm(object owner, Dictionary<int, IFsmState> states, int defaultState);
        void DestroyFsm(IFsm fsm);
        void DestroyAllFsms();
    }
}
