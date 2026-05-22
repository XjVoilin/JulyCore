using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Fsm;

namespace JulyCore.Module.Fsm
{
    internal class FsmModule : ModuleBase
    {
        private IFsmProvider _provider;

        protected override LogChannel LogChannel => LogChannel.Fsm;
        public override int Priority => Frameworkconst.PriorityFsmModule;

        protected override UniTask OnInitAsync()
        {
            _provider = GetProvider<IFsmProvider>();
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            _provider?.DestroyAllFsms();
        }

        internal IFsm CreateFsm(object owner, Dictionary<int, IFsmState> states, int defaultState)
        {
            return _provider.CreateFsm(owner, states, defaultState);
        }

        internal void DestroyFsm(IFsm fsm)
        {
            _provider?.DestroyFsm(fsm);
        }

        internal void DestroyAllFsms()
        {
            _provider?.DestroyAllFsms();
        }
    }
}
