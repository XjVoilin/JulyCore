using System;
using System.Collections.Generic;
using JulyCore.Core.Events;
using JulyCore.Data.ABTest;
using JulyCore.Module.ABTest;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// AB测试相关操作
        /// </summary>
        public static class ABTest
        {
            private static ABTestModule _module;
            private static ABTestModule Module
            {
                get
                {
                    _module ??= GetModule<ABTestModule>();
                    return _module;
                }
            }

            #region 用户设置

            /// <summary>
            /// 设置当前用户ID
            /// </summary>
            public static void SetUserId(string userId)
            {
                Module.SetUserId(userId);
            }

            /// <summary>
            /// 设置当前设备ID
            /// </summary>
            public static void SetDeviceId(string deviceId)
            {
                Module.SetDeviceId(deviceId);
            }

            /// <summary>
            /// 设置用户属性（用于条件检查）
            /// </summary>
            public static void SetUserAttribute(string key, object value)
            {
                Module.SetUserAttribute(key, value);
            }

            /// <summary>
            /// 批量设置用户属性
            /// </summary>
            public static void SetUserAttributes(Dictionary<string, object> attributes)
            {
                Module.SetUserAttributes(attributes);
            }

            /// <summary>
            /// 清空用户属性
            /// </summary>
            public static void ClearUserAttributes()
            {
                Module.ClearUserAttributes();
            }

            #endregion

            #region 实验管理

            /// <summary>
            /// 注册实验
            /// </summary>
            public static void Register(Experiment experiment)
            {
                Module.RegisterExperiment(experiment);
            }

            /// <summary>
            /// 批量注册实验
            /// </summary>
            public static void RegisterBatch(IEnumerable<Experiment> experiments)
            {
                Module.RegisterExperiments(experiments);
            }

            /// <summary>
            /// 注销实验
            /// </summary>
            public static void Unregister(string experimentId)
            {
                Module.UnregisterExperiment(experimentId);
            }

            /// <summary>
            /// 清空所有实验
            /// </summary>
            public static void ClearAll()
            {
                Module.ClearAllExperiments();
            }

            /// <summary>
            /// 获取实验
            /// </summary>
            public static Experiment GetExperiment(string experimentId)
            {
                return Module.GetExperiment(experimentId);
            }

            /// <summary>
            /// 获取所有实验
            /// </summary>
            public static List<Experiment> GetAllExperiments()
            {
                return Module.GetAllExperiments() ?? new List<Experiment>();
            }

            /// <summary>
            /// 获取运行中的实验
            /// </summary>
            public static List<Experiment> GetRunningExperiments()
            {
                return Module.GetRunningExperiments() ?? new List<Experiment>();
            }

            /// <summary>
            /// 设置实验状态
            /// </summary>
            public static void SetStatus(string experimentId, ExperimentStatus status)
            {
                Module.SetExperimentStatus(experimentId, status);
            }

            /// <summary>
            /// 启动实验
            /// </summary>
            public static void Start(string experimentId)
            {
                SetStatus(experimentId, ExperimentStatus.Running);
            }

            /// <summary>
            /// 暂停实验
            /// </summary>
            public static void Pause(string experimentId)
            {
                SetStatus(experimentId, ExperimentStatus.Paused);
            }

            /// <summary>
            /// 结束实验
            /// </summary>
            public static void End(string experimentId)
            {
                SetStatus(experimentId, ExperimentStatus.Ended);
            }

            /// <summary>
            /// 从配置表加载实验
            /// </summary>
            public static void LoadFromConfigTable(ExperimentConfigTable configTable)
            {
                Module.LoadFromConfigTable(configTable);
            }

            #endregion

            #region 核心API - 分组分配

            /// <summary>
            /// 获取用户在实验中的分组
            /// </summary>
            public static ExperimentGroup GetGroup(string experimentId, string userId = null)
            {
                return Module.GetUserGroup(experimentId, userId);
            }

            /// <summary>
            /// 获取用户在实验中的分组ID
            /// </summary>
            public static string GetGroupId(string experimentId, string userId = null)
            {
                return GetGroup(experimentId, userId)?.GroupId;
            }

            /// <summary>
            /// 获取实验参数值
            /// </summary>
            public static T GetParameter<T>(string experimentId, string paramKey, T defaultValue = default)
            {
                return Module.GetParameter(experimentId, paramKey, defaultValue);
            }

            /// <summary>
            /// 检查用户是否在指定分组
            /// </summary>
            public static bool IsInGroup(string experimentId, string groupId, string userId = null)
            {
                return Module.IsInGroup(experimentId, groupId, userId);
            }

            /// <summary>
            /// 检查用户是否在对照组
            /// </summary>
            public static bool IsInControlGroup(string experimentId, string userId = null)
            {
                return Module.IsInControlGroup(experimentId, userId);
            }

            /// <summary>
            /// 检查用户是否在实验组（非对照组）
            /// </summary>
            public static bool IsInTreatmentGroup(string experimentId, string userId = null)
            {
                return Module.IsInTreatmentGroup(experimentId, userId);
            }

            /// <summary>
            /// 检查用户是否参与了实验
            /// </summary>
            public static bool IsInExperiment(string experimentId, string userId = null)
            {
                return GetGroup(experimentId, userId) != null;
            }

            #endregion

            #region 曝光记录

            /// <summary>
            /// 记录实验曝光
            /// </summary>
            public static void RecordExposure(string experimentId, string scene = null,
                Dictionary<string, object> extraData = null, string userId = null)
            {
                Module.RecordExposure(experimentId, scene, extraData, userId);
            }

            #endregion

            #region 条件检查器

            /// <summary>
            /// 注册自定义条件检查器
            /// </summary>
            public static void RegisterConditionChecker(string conditionType, ConditionChecker checker)
            {
                Module.RegisterConditionChecker(conditionType, checker);
            }

            /// <summary>
            /// 设置自定义分配器
            /// </summary>
            public static void SetCustomAllocator(CustomAllocator allocator)
            {
                Module.SetCustomAllocator(allocator);
            }

            #endregion

            #region 数据持久化

            /// <summary>
            /// 导出用户AB测试数据
            /// </summary>
            public static ABTestSaveData ExportData(string userId = null)
            {
                return Module.ExportData(userId) ?? new ABTestSaveData();
            }

            /// <summary>
            /// 导入用户AB测试数据
            /// </summary>
            public static void ImportData(ABTestSaveData saveData)
            {
                Module.ImportData(saveData);
            }

            /// <summary>
            /// 清空用户AB测试数据
            /// </summary>
            public static void ClearUserData(string userId = null)
            {
                Module.ClearUserData(userId);
            }

            #endregion

            #region 事件订阅

            /// <summary>
            /// 订阅用户分配到实验事件
            /// </summary>
            public static void OnAssigned(Action<UserAssignedToExperimentEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅实验曝光事件
            /// </summary>
            public static void OnExposure(Action<ExperimentExposureEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅实验状态变更事件
            /// </summary>
            public static void OnStatusChanged(Action<ExperimentStatusChangedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            #endregion

            #region 便捷方法 - 构建器

            /// <summary>
            /// 创建实验构建器
            /// </summary>
            public static ExperimentBuilder CreateExperiment(string experimentId, string name)
            {
                return new ExperimentBuilder(experimentId, name);
            }

            /// <summary>
            /// 快速创建简单AB实验（50/50分流）
            /// </summary>
            public static Experiment CreateSimpleABExperiment(string experimentId, string name,
                Dictionary<string, object> controlParams = null,
                Dictionary<string, object> treatmentParams = null)
            {
                return new ExperimentBuilder(experimentId, name)
                    .WithStrategy(AllocationStrategy.UserIdHash)
                    .AddControlGroup("control", "对照组", 50, controlParams)
                    .AddTreatmentGroup("treatment", "实验组", 50, treatmentParams)
                    .SetStatus(ExperimentStatus.Running)
                    .Build();
            }

            #endregion
        }
    }
}