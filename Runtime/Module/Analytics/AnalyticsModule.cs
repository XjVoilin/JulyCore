using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Analytics;

namespace JulyCore.Module.Analytics
{
    /// <summary>
    /// 数据统计模块
    /// 业务语义与流程调度层：决定上报策略、事件过滤规则、业务规则
    /// 管理数据统计状态和业务逻辑
    /// 不直接操作网络请求，不负责数据序列化
    /// </summary>
    internal class AnalyticsModule : ModuleBase 
    {
        private IAnalyticsProvider _analyticsProvider;

        protected override LogChannel LogChannel => LogChannel.Analytics;

        /// <summary>
        /// 是否启用数据统计
        /// </summary>
        private bool _isEnabled = true;

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PriorityAnalyticsModule;

        protected override UniTask OnInitAsync()
        {
            try
            {
                _analyticsProvider = GetProvider<IAnalyticsProvider>();
                if (_analyticsProvider == null)
                {
                    throw new JulyException($"[{Name}] 未找到IAnalyticsProvider，请先注册AnalyticsProvider");
                }

                Log($"[{Name}] 数据统计模块初始化完成");
                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 数据统计模块初始化失败: {ex.Message}");
                throw;
            }
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            // 数数SDK通常是实时上报，不需要在Update中处理
            // 如果需要定时上报或其他逻辑，可以在这里添加
        }

        /// <summary>
        /// 上报事件（业务层：事件过滤、业务规则）
        /// </summary>
        /// <param name="evt">事件对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上报成功</returns>
        internal async UniTask<bool> TrackEventAsync(AnalyticsEvent evt, CancellationToken cancellationToken = default)
        {
            if (!_isEnabled)
            {
                return false;
            }

            if (evt == null)
            {
                LogWarning($"[{Name}] 事件对象为空，跳过上报");
                return false;
            }

            if (string.IsNullOrEmpty(evt.EventName))
            {
                LogWarning($"[{Name}] 事件名称为空，跳过上报");
                return false;
            }

            EnsureProvider();
            return await _analyticsProvider.TrackEventAsync(evt, cancellationToken);
        }

        /// <summary>
        /// 批量上报事件（业务层：事件过滤、业务规则）
        /// </summary>
        /// <param name="events">事件列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上报成功</returns>
        internal async UniTask<bool> TrackEventsAsync(List<AnalyticsEvent> events, CancellationToken cancellationToken = default)
        {
            if (!_isEnabled)
            {
                return false;
            }

            if (events == null || events.Count == 0)
            {
                return true;
            }

            EnsureProvider();
            return await _analyticsProvider.TrackEventsAsync(events, cancellationToken);
        }

        /// <summary>
        /// 设置用户ID（业务层：用户管理）
        /// </summary>
        /// <param name="userId">用户ID</param>
        internal void SetUserId(string userId)
        {
            EnsureProvider();
            _analyticsProvider.SetUserId(userId);
        }

        /// <summary>
        /// 设置用户属性（业务层：用户管理）
        /// </summary>
        /// <param name="properties">用户属性字典</param>
        internal void SetUserProperties(Dictionary<string, object> properties)
        {
            EnsureProvider();
            _analyticsProvider.SetUserProperties(properties);
        }

        /// <summary>
        /// 刷新上报（立即上报缓存的事件，业务层：强制上报策略）
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上报成功</returns>
        internal async UniTask<bool> FlushAsync(CancellationToken cancellationToken = default)
        {
            if (!_isEnabled)
            {
                return false;
            }

            EnsureProvider();
            return await _analyticsProvider.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// 获取待上报事件数量（业务层：状态查询）
        /// </summary>
        /// <returns>待上报事件数量</returns>
        internal int GetPendingEventCount()
        {
            EnsureProvider();
            return _analyticsProvider.GetPendingEventCount();
        }

        /// <summary>
        /// 启用/禁用数据统计（业务层：功能开关）
        /// </summary>
        /// <param name="enabled">是否启用</param>
        internal void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            Log($"[{Name}] 数据统计功能已{(enabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 是否启用数据统计
        /// </summary>
        internal bool IsAnalyticsEnabled => _isEnabled;

        private void EnsureProvider()
        {
            if (_analyticsProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] AnalyticsProvider未初始化");
            }
        }

        protected override async UniTask OnShutdownAsync()
        {
            // 关闭时上报剩余事件
            if (_isEnabled && _analyticsProvider != null)
            {
                await FlushAsync();
            }

            await base.OnShutdownAsync();
        }
    }
}

