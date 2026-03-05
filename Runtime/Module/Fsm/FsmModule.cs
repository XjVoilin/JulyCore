using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Fsm;

namespace JulyCore.Module.Fsm
{
    /// <summary>
    /// 状态机模块
    /// 纯技术层代理：Fsm本身是纯技术实现（状态机创建、状态切换）
    /// 如果未来需要业务规则（如状态机生命周期管理、状态切换规则），可在此层添加
    /// </summary>
    internal class FsmModule : ModuleBase
    {
        private IFsmProvider _fsmProvider;

        protected override LogChannel LogChannel => LogChannel.Fsm;

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PriorityFsmModule;

        /// <summary>
        /// 初始化Module
        /// </summary>
        protected override UniTask OnInitAsync()
        {
            try
            {
                // 获取状态机提供者
                _fsmProvider = GetProvider<IFsmProvider>();
                if (_fsmProvider == null)
                {
                    throw new JulyException($"[{Name}] 未找到IFsmProvider，请先注册FsmProvider");
                }

                JLogger.Log($"[{Name}] 状态机模块初始化完成");
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                JLogger.LogError($"[{Name}] 状态机模块初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建状态机
        /// </summary>
        /// <param name="owner">状态机拥有者</param>
        /// <param name="states">状态列表</param>
        /// <param name="defaultState">默认状态</param>
        /// <returns>状态机实例</returns>
        internal IFsm CreateFsm(
            object owner,
            Dictionary<int, IFsmState> states,
            int defaultState)
        {
            return _fsmProvider.CreateFsm(owner, states, defaultState);
        }

        /// <summary>
        /// 销毁状态机
        /// </summary>
        /// <param name="fsm">状态机实例</param>
        internal void DestroyFsm(IFsm fsm)
        {
            _fsmProvider.DestroyFsm(fsm);
        }

        /// <summary>
        /// 销毁所有状态机
        /// </summary>
        internal void DestroyAllFsms()
        {
            _fsmProvider.DestroyAllFsms();
        }

        /// <summary>
        /// 关闭Module
        /// </summary>
        protected override UniTask OnShutdownAsync()
        {
            // 销毁所有状态机
            if (_fsmProvider != null)
            {
                try
                {
                    _fsmProvider.DestroyAllFsms();
                }
                catch (Exception ex)
                {
                    JLogger.LogError($"[{Name}] 销毁状态机时异常: {ex.Message}");
                }
            }

            _fsmProvider = null;
            JLogger.Log($"[{Name}] 状态机模块已关闭");
            return base.OnShutdownAsync();
        }
    }
}

