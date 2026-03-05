using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Analytics;
using JulyCore.Provider.Analytics;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary> 
        /// 数据统计相关操作
        /// </summary>
        public static class Analytics
        {
            private static AnalyticsModule _module;
            private static AnalyticsModule Module
            {
                get
                {
                    _module ??= GetModule<AnalyticsModule>();
                    return _module;
                }
            }
            
            /// <summary>
            /// 上报事件
            /// </summary>
            /// <param name="evt">事件对象</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否上报成功</returns>
            public static async UniTask<bool> TrackEventAsync(AnalyticsEvent evt,
                CancellationToken cancellationToken = default)
            {
                return await Module.TrackEventAsync(evt, cancellationToken);
            }

            /// <summary>
            /// 上报事件（便捷方法，内部创建AnalyticsEvent对象）
            /// </summary>
            /// <param name="eventName">事件名称</param>
            /// <param name="parameters">事件参数</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否上报成功</returns>
            public static async UniTask<bool> TrackEventAsync(string eventName,
                Dictionary<string, object> parameters = null, CancellationToken cancellationToken = default)
            {
                var evt = new AnalyticsEvent
                {
                    EventName = eventName,
                    Parameters = parameters ?? new Dictionary<string, object>()
                };

                return await TrackEventAsync(evt, cancellationToken);
            }

            /// <summary>
            /// 批量上报事件
            /// </summary>
            /// <param name="events">事件列表</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否上报成功</returns>
            public static async UniTask<bool> TrackEventsAsync(List<AnalyticsEvent> events,
                CancellationToken cancellationToken = default)
            {
                return await Module.TrackEventsAsync(events, cancellationToken);
            }

            /// <summary>
            /// 设置用户ID
            /// </summary>
            /// <param name="userId">用户ID</param>
            public static void SetUserId(string userId)
            {
                Module.SetUserId(userId);
            }

            /// <summary>
            /// 设置用户属性
            /// </summary>
            /// <param name="properties">用户属性字典</param>
            public static void SetUserProperties(Dictionary<string, object> properties)
            {
                Module.SetUserProperties(properties);
            }

            /// <summary>
            /// 刷新上报（立即上报缓存的事件）
            /// </summary>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否上报成功</returns>
            public static async UniTask<bool> FlushAsync(CancellationToken cancellationToken = default)
            {
                return await Module.FlushAsync(cancellationToken);
            }

            /// <summary>
            /// 获取待上报事件数量
            /// </summary>
            /// <returns>待上报事件数量</returns>
            public static int GetPendingEventCount()
            {
                return Module.GetPendingEventCount();
            }

            /// <summary>
            /// 启用/禁用数据统计
            /// </summary>
            /// <param name="enabled">是否启用</param>
            public static void SetEnabled(bool enabled)
            {
                Module.SetEnabled(enabled);
            }

            /// <summary>
            /// 是否启用数据统计
            /// </summary>
            public static bool IsEnabled
            {
                get
                {
                    var analyticsModule = GetModule<AnalyticsModule>();

                    return analyticsModule.IsAnalyticsEnabled;
                }
            }
        }
    }
}