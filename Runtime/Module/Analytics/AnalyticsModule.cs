using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Analytics;

namespace JulyCore.Module.Analytics
{
    /// <summary>
    /// 数据统计模块
    /// 业务语义层：功能开关、参数校验、生命周期管理
    /// </summary>
    internal class AnalyticsModule : ModuleBase
    {
        private IAnalyticsProvider _analyticsProvider;

        protected override LogChannel LogChannel => LogChannel.Analytics;

        private bool _isEnabled = true;

        public override int Priority => Frameworkconst.PriorityAnalyticsModule;

        protected override UniTask OnInitAsync()
        {
            _analyticsProvider = GetProvider<IAnalyticsProvider>();
            if (_analyticsProvider == null)
                throw new JulyException($"[{Name}] 未找到IAnalyticsProvider，请先注册AnalyticsProvider");

            Log($"[{Name}] 数据统计模块初始化完成");
            return base.OnInitAsync();
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds) { }

        internal void Track(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!_isEnabled) return;

            if (string.IsNullOrEmpty(eventName))
            {
                LogWarning($"[{Name}] 事件名称为空，跳过上报");
                return;
            }

            EnsureProvider();
            _analyticsProvider.Track(eventName, parameters);
        }

        internal void SetUserId(string userId)
        {
            EnsureProvider();
            _analyticsProvider.SetUserId(userId);
        }

        internal void SetUserProperties(Dictionary<string, object> properties)
        {
            EnsureProvider();
            _analyticsProvider.SetUserProperties(properties);
        }

        internal void Flush()
        {
            if (!_isEnabled) return;
            EnsureProvider();
            _analyticsProvider.Flush();
        }

        internal void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            Log($"[{Name}] 数据统计功能已{(enabled ? "启用" : "禁用")}");
        }

        internal bool IsAnalyticsEnabled => _isEnabled;

        private void EnsureProvider()
        {
            if (_analyticsProvider == null)
                throw new InvalidOperationException($"[{Name}] AnalyticsProvider未初始化");
        }

        protected override void OnShutdown()
        {
            if (_isEnabled && _analyticsProvider != null)
                _analyticsProvider.Flush();
        }
    }
}
