using System.Collections.Generic;
using JulyCore.Data.Activity;
using JulyCore.Module.Activity;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 活动相关操作
        /// 
        /// 【使用流程】
        /// 1. 框架启动后，业务层从配置获取活动数据
        /// 2. 将配置数据转换为 ActivityDefinition
        /// 3. 调用 RegisterActivities() 注册活动
        /// 4. 调用 CompleteRegistration() 完成注册
        /// 5. 之后可正常使用活动模块的各项功能
        /// 
        /// 【设计说明】
        /// - 框架层不依赖任何具体配置表
        /// - 活动定义由业务层通过注册机制提供
        /// - 框架提供活动的通用业务能力：状态管理、进度存储等
        /// - 具体活动逻辑由业务层实现
        /// </summary>
        public static class Activity
        {
            private static ActivityModule _module;

            private static ActivityModule Module
            {
                get
                {
                    if (_module == null)
                    {
                        _module = GetModule<ActivityModule>();
                    }
                    return _module;
                }
            }

            #region 活动注册

            /// <summary>
            /// 注册单个活动
            /// </summary>
            /// <param name="definition">活动定义</param>
            public static void RegisterActivity(ActivityDefinition definition)
            {
                Module.RegisterActivity(definition);
            }

            /// <summary>
            /// 批量注册活动
            /// </summary>
            /// <param name="definitions">活动定义列表</param>
            public static void RegisterActivities(IEnumerable<ActivityDefinition> definitions)
            {
                Module.RegisterActivities(definitions);
            }

            /// <summary>
            /// 注销活动
            /// </summary>
            /// <param name="activityId">活动 ID</param>
            /// <returns>是否成功</returns>
            public static bool UnregisterActivity(string activityId)
            {
                return Module.UnregisterActivity(activityId);
            }

            /// <summary>
            /// 完成注册
            /// 业务层注册完所有活动后调用，触发状态初始化和事件发布
            /// </summary>
            public static void CompleteRegistration()
            {
                Module.CompleteRegistration();
            }

            #endregion

            #region 活动查询

            /// <summary>
            /// 获取所有活动
            /// </summary>
            /// <returns>活动信息列表（按优先级排序）</returns>
            public static List<ActivityInfo> GetAllActivities()
            {
                return Module.GetAllActivities();
            }

            /// <summary>
            /// 获取指定活动
            /// </summary>
            /// <param name="activityId">活动 ID</param>
            /// <returns>活动信息，不存在则返回 null</returns>
            public static ActivityInfo GetActivity(string activityId)
            {
                return Module.GetActivity(activityId);
            }

            /// <summary>
            /// 按类型获取活动
            /// </summary>
            /// <param name="type">活动类型</param>
            /// <returns>活动信息列表</returns>
            public static List<ActivityInfo> GetActivitiesByType(int type)
            {
                return Module.GetActivitiesByType(type);
            }

            /// <summary>
            /// 按状态获取活动
            /// </summary>
            /// <param name="state">活动状态</param>
            /// <returns>活动信息列表</returns>
            public static List<ActivityInfo> GetActivitiesByState(ActivityState state)
            {
                return Module.GetActivitiesByState(state);
            }

            /// <summary>
            /// 获取活动状态
            /// </summary>
            /// <param name="activityId">活动 ID</param>
            /// <returns>活动状态</returns>
            public static ActivityState GetActivityState(string activityId)
            {
                return Module.GetActivityState(activityId);
            }

            /// <summary>
            /// 检查活动是否存在
            /// </summary>
            /// <param name="activityId">活动 ID</param>
            /// <returns>是否存在</returns>
            public static bool HasActivity(string activityId)
            {
                return Module.HasActivity(activityId);
            }

            /// <summary>
            /// 计算活动状态（纯计算，不依赖已注册活动）
            /// </summary>
            /// <param name="preAnnounceTime">预告开始时间（0 表示无预告期）</param>
            /// <param name="startTime">开始时间</param>
            /// <param name="endTime">结束时间</param>
            /// <returns>活动状态</returns>
            public static ActivityState CalculateState(long preAnnounceTime, long startTime, long endTime)
            {
                return Module.CalculateState(preAnnounceTime, startTime, endTime);
            }

            /// <summary>
            /// 计算活动状态（无预告期）
            /// </summary>
            /// <param name="startTime">开始时间</param>
            /// <param name="endTime">结束时间</param>
            /// <returns>活动状态</returns>
            public static ActivityState CalculateState(long startTime, long endTime)
            {
                return Module.CalculateState(startTime, endTime);
            }

            #endregion

            #region 活动记录

            /// <summary>
            /// 获取活动运行时记录
            /// </summary>
            /// <param name="activityId">活动 ID</param>
            /// <returns>活动记录，不存在则返回 null</returns>
            public static ActivityRecord GetActivityRecord(string activityId)
            {
                return Module.GetActivityRecord(activityId);
            }

            /// <summary>
            /// 保存活动进度数据
            /// </summary>
            /// <param name="activityId">活动 ID</param>
            /// <param name="dataPayload">进度数据载荷（JSON 字符串）</param>
            public static void SaveProgressData(string activityId, string dataPayload)
            {
                Module.SaveProgressData(activityId, dataPayload);
            }

            #endregion

            #region 数据管理

            /// <summary>
            /// 清理活动数据
            /// </summary>
            /// <param name="activityId">活动 ID</param>
            public static void ClearActivityData(string activityId)
            {
                Module.ClearActivityData(activityId);
            }

            #endregion
        }
    }
}
