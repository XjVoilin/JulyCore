using System;
using System.Collections.Generic;
using JulyCore.Module.Fsm;
using JulyCore.Provider.Fsm;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 状态机相关操作
        /// </summary>
        public static class Fsm
        {
            private static FsmModule _module;
            private static FsmModule Module
            {
                get
                {
                    _module ??= GetModule<FsmModule>();
                    return _module;
                }
            }
            
            /// <summary>
            /// 创建状态机
            /// </summary>
            /// <typeparam name="TState">状态类型</typeparam>
            /// <typeparam name="TOwner">状态机拥有者类型</typeparam>
            /// <param name="owner">状态机拥有者</param>
            /// <param name="states">状态列表</param>
            /// <param name="defaultState">默认状态</param>
            /// <returns>状态机实例</returns>
            /// <exception cref="InvalidOperationException">当FsmModule未注册时抛出</exception>
            public static IFsm CreateFsm(
                object owner,
                Dictionary<int, IFsmState> states,
                int defaultState)
            {
                return Module.CreateFsm(owner, states, defaultState);
            }

            /// <summary>
            /// 销毁状态机
            /// </summary>
            /// <param name="fsm">状态机实例</param>
            public static void DestroyFsm(IFsm fsm)
            {
                if (fsm == null)
                {
                    return;
                }
                Module.DestroyFsm(fsm);
            }

            /// <summary>
            /// 销毁所有状态机
            /// </summary>
            public static void DestroyAllFsms()
            {
                Module.DestroyAllFsms();
            }
        }
    }
}