using System.Collections.Generic;
using JulyCore.Core;

namespace JulyCore.Provider.Analytics
{
    /// <summary>
    /// 数据统计提供者接口。
    /// 全部同步 fire-and-forget，SDK 内部管理时间戳和用户标识。
    /// </summary>
    public interface IAnalyticsProvider : IProvider
    {
        /// <summary>
        /// 延迟初始化 SDK，由外部在首场景渲染后主动调用。
        /// </summary>
        void DeferredInit() { }

        /// <summary>
        /// 上报事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="parameters">事件参数</param>
        void Track(string eventName, Dictionary<string, object> parameters = null);

        /// <summary>
        /// 设置用户ID
        /// </summary>
        /// <param name="userId">用户ID</param>
        void SetUserId(string userId);

        /// <summary>
        /// 设置用户属性
        /// </summary>
        /// <param name="properties">用户属性字典</param>
        void SetUserProperties(Dictionary<string, object> properties);

        /// <summary>
        /// 立即上报缓存的事件
        /// </summary>
        void Flush();
    }
}
