using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore.Provider.Analytics
{
    /// <summary>
    /// 数据统计提供者接口
    /// 负责数据统计事件的收集和上报
    /// </summary>
    public interface IAnalyticsProvider : IProvider
    {
        /// <summary>
        /// 上报单个事件
        /// </summary>
        /// <param name="evt">事件对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上报成功</returns>
        UniTask<bool> TrackEventAsync(AnalyticsEvent evt, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量上报事件
        /// </summary>
        /// <param name="events">事件列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上报成功</returns>
        UniTask<bool> TrackEventsAsync(List<AnalyticsEvent> events, CancellationToken cancellationToken = default);

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
        /// 刷新上报（立即上报缓存的事件）
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上报成功</returns>
        UniTask<bool> FlushAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取待上报事件数量
        /// </summary>
        /// <returns>待上报事件数量</returns>
        int GetPendingEventCount();
    }

    /// <summary>
    /// 数据统计事件
    /// </summary>
    public class AnalyticsEvent
    {
        /// <summary>
        /// 事件名称
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// 事件参数
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// 事件时间戳（Unix时间戳，秒）
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; }

        public AnalyticsEvent()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Parameters = new Dictionary<string, object>();
        }
    }
}

