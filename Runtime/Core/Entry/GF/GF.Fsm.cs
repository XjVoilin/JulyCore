using System.Collections.Generic;
using JulyCore.Module.Fsm;
using JulyCore.Provider.Fsm;

namespace JulyCore
{
    public static partial class GF
    {
        public static class Fsm
        {
            private static FsmModule _module;
            private static FsmModule Module => _module ??= GetModule<FsmModule>();

            public static IFsm CreateFsm(object owner, Dictionary<int, IFsmState> states, int defaultState)
            {
                return Module.CreateFsm(owner, states, defaultState);
            }

            public static void DestroyFsm(IFsm fsm)
            {
                Module.DestroyFsm(fsm);
            }

            public static void DestroyAllFsms()
            {
                Module.DestroyAllFsms();
            }
        }
    }
}
