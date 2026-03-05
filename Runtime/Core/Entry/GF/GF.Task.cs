using System;
using System.Collections.Generic;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.Task;
using JulyCore.Module.Task;
// TaskResetConfig 位于 Module.Task 命名空间

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 任务相关操作
        /// </summary>
        public static class Task
        {
            private static TaskModule _module;
            private static TaskModule Module
            {
                get
                {
                    _module ??= GetModule<TaskModule>();
                    return _module;
                }
            }
            
            #region 配置与注册

            public static void SetRewardHandler(RewardHandler handler)
            {
                Module.SetRewardHandler(handler);
            }

            public static void SetUnlockCheckHandler(UnlockCheckHandler handler)
            {
                Module.SetUnlockCheckHandler(handler);
            }

            /// <summary>
            /// 设置任务重置调度器（在框架初始化之前调用）。
            /// 业务层实现 ITaskResetScheduler 来定义哪些任务类型需要定时重置。
            /// </summary>
            public static void SetResetScheduler(ITaskResetScheduler scheduler)
            {
                Module.SetResetScheduler(scheduler);
            }

            /// <summary>
            /// 注册任务类型Handler（可选）
            /// 只有特殊任务类型才需要注册Handler
            /// 
            /// 【重要】Handler是按TaskType共享的：
            /// - 每个TaskType只有一个Handler实例
            /// - OnTaskUnlocked会为每个解锁的任务调用，但Handler本身是共享的
            /// - Handler内部不应维护任务相关的状态，状态应存储在TaskData.ExtraData中
            /// - Handler的Dispose在Module关闭时调用，而非单个任务完成时
            /// </summary>
            public static void RegisterTaskTypeHandler(ITaskTypeHandler handler)
            {
                Module.RegisterTaskTypeHandler(handler);
            }

            /// <summary>
            /// 注销任务类型Handler
            /// 注意：这会清理该Handler的所有事件订阅
            /// </summary>
            public static void UnregisterTaskTypeHandler(TaskType taskType)
            {
                Module.UnregisterTaskTypeHandler(taskType);
            }

            public static void Register(TaskData taskData)
            {
                Module.RegisterTask(taskData);
            }

            public static void RegisterBatch(IEnumerable<TaskData> tasks)
            {
                Module.RegisterTasks(tasks);
            }

            public static void Unregister(string taskId)
            {
                Module.UnregisterTask(taskId);
            }

            public static void ClearAll()
            {
                Module.ClearAllTasks();
            }

            public static void LoadFromConfig(TaskConfig config, DateTime? baseTime = null)
            {
                Module.LoadFromConfig(config, baseTime);
            }

            public static void LoadFromConfigTable(TaskConfigTable configTable, DateTime? baseTime = null)
            {
                Module.LoadFromConfigTable(configTable, baseTime);
            }

            #endregion

            #region 任务查询

            public static TaskData Get(string taskId)
            {
                return Module.GetTask(taskId);
            }

            public static List<TaskData> GetAll()
            {
                return Module.GetAllTasks() ?? new List<TaskData>();
            }

            public static List<TaskData> GetByType(TaskType type)
            {
                return Module.GetTasksByType(type) ?? new List<TaskData>();
            }

            public static List<TaskData> GetByState(TaskState state)
            {
                return Module.GetTasksByState(state) ?? new List<TaskData>();
            }

            public static List<TaskData> GetByGroup(string group)
            {
                return Module.GetTasksByGroup(group) ?? new List<TaskData>();
            }

            public static List<TaskData> GetWhere(Func<TaskData, bool> predicate)
            {
                return Module.GetTasks(predicate) ?? new List<TaskData>();
            }

            /// <summary>
            /// 获取任务列表（支持排序）
            /// </summary>
            /// <param name="type">任务类型（可选）</param>
            /// <param name="state">任务状态（可选）</param>
            /// <param name="sortBy">排序方式</param>
            /// <param name="ascending">是否升序</param>
            /// <returns>排序后的任务列表</returns>
            public static List<TaskData> GetSorted(TaskType? type = null, TaskState? state = null, TaskSortBy sortBy = TaskSortBy.Priority, bool ascending = true)
            {
                return Module.GetTasksSorted(type, state, sortBy, ascending) ?? new List<TaskData>();
            }

            public static int CompletedCount => Module.GetCompletedTaskCount();

            public static int InProgressCount => 
                 Module.GetInProgressTaskCount();

            #endregion

            #region 任务操作

            public static bool Unlock(string taskId)
            {
                return Module.UnlockTask(taskId);
            }

            public static int TryUnlockAll()
            {
                return Module.TryUnlockAllTasks();
            }

            public static void UpdateProgress(TaskConditionType conditionType, string param, int delta = 1)
            {
                Module.UpdateProgress(conditionType, param, delta);
            }

            /// <summary>
            /// 更新指定任务的进度
            /// </summary>
            /// <param name="taskId">任务ID</param>
            /// <param name="conditionId">条件ID</param>
            /// <param name="value">进度值（绝对值）</param>
            public static void UpdateTaskProgress(string taskId, string conditionId, int value)
            {
                Module.UpdateTaskProgress(taskId, conditionId, value);
            }

            /// <summary>
            /// 报告累计进度（增量更新）
            /// 适用于Accumulate类型的条件，每次事件触发时累加数值
            /// </summary>
            /// <param name="param">条件参数（如："kill_enemy_001"、"collect_item_sword"、"complete_stage_001"）</param>
            /// <param name="count">累计增量，默认为1</param>
            /// <example>
            /// // 击杀怪物
            /// GF.Task.ReportAccumulate("kill_enemy_001", 1);
            /// 
            /// // 收集道具
            /// GF.Task.ReportAccumulate("collect_item_sword", 1);
            /// 
            /// // 通关关卡
            /// GF.Task.ReportAccumulate("complete_stage_001", 1);
            /// </example>
            public static void ReportAccumulate(string param, int count = 1)
            {
                UpdateProgress(TaskConditionType.Accumulate, param, count);
            }

            /// <summary>
            /// 报告达到数值（绝对值更新）
            /// 适用于Reach类型的条件，直接检查当前数值是否达到目标
            /// </summary>
            /// <param name="param">条件参数（如："player_level"、"player_power"、"player_gold"）</param>
            /// <param name="value">当前数值（绝对值）</param>
            /// <example>
            /// // 等级达到10级
            /// GF.Task.ReportReach("player_level", currentLevel);
            /// 
            /// // 战力达到10000
            /// GF.Task.ReportReach("player_power", currentPower);
            /// 
            /// // 金币达到1000
            /// GF.Task.ReportReach("player_gold", currentGold);
            /// </example>
            public static void ReportReach(string param, int value)
            {
                Module.UpdateReachProgress(param, value);
            }

            public static bool ClaimReward(string taskId)
            {
                return Module.ClaimReward(taskId);
            }

            public static List<string> ClaimAllRewards()
            {
                return Module.ClaimAllRewards() ?? new List<string>();
            }

            public static bool Reset(string taskId)
            {
                return Module.ResetTask(taskId);
            }

            public static void ResetByType(TaskType type)
            {
                Module.ResetTasksByType(type);
            }

            #endregion

            #region 数据持久化

            public static Dictionary<string, TaskSaveData> ExportProgress()
            {
                return Module.ExportProgress() ?? new Dictionary<string, TaskSaveData>();
            }

            public static void ImportProgress(Dictionary<string, TaskSaveData> progressData)
            {
                Module.ImportProgress(progressData);
            }

            #endregion

            #region 事件订阅

            public static void OnStateChanged(Action<TaskStateChangedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            public static void OnProgressUpdated(Action<TaskProgressUpdatedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            public static void OnCompleted(Action<TaskCompletedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            public static void OnRewardClaimed(Action<TaskRewardClaimedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            public static void OnUnlocked(Action<TaskUnlockedEvent> handler, object target)
            {
                _context.EventBus.Subscribe(handler, target);
            }

            #endregion
        }
    }
}
