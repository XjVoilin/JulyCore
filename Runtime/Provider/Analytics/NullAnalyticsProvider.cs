using System.Collections.Generic;
using JulyCore.Core;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.Analytics
{
    /// <summary>
    /// 空实现的数据统计 Provider。
    /// 所有操作静默成功，不产生任何副作用。
    /// 项目侧应通过 RegisterBusinessProviders 或热更注册器替换为实际 SDK 实现。
    /// </summary>
    public class NullAnalyticsProvider : ProviderBase, IAnalyticsProvider
    {
        protected override LogChannel LogChannel => LogChannel.Analytics;

        public void Track(string eventName, Dictionary<string, object> parameters = null) { }
        public void SetUserId(string userId) { }
        public void SetUserProperties(Dictionary<string, object> properties) { }
        public void Flush() { }
    }
}
