using System.Collections.Generic;
using JulyCore.Module.Analytics;

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
            /// <param name="eventName">事件名称</param>
            /// <param name="parameters">事件参数</param>
            public static void Track(string eventName, Dictionary<string, object> parameters = null)
            {
                Module.Track(eventName, parameters);
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
            /// 立即上报缓存的事件
            /// </summary>
            public static void Flush()
            {
                Module.Flush();
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
            public static bool IsEnabled => Module.IsAnalyticsEnabled;
        }
    }
}
