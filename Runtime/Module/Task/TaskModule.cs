using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.Task;
using JulyCore.Module.Base;
using JulyCore.Provider.Task;

namespace JulyCore.Module.Task
{
    /// <summary>
    /// 奖励发放处理器委托
    /// </summary>
    public delegate bool RewardHandler(List<TaskReward> rewards);

    /// <summary>
    /// 解锁条件检查处理器委托
    /// </summary>
    public delegate bool UnlockCheckHandler(TaskData taskData);

    /// <summary>
    /// 任务模块
    /// 业务逻辑层：负责任务状态流转、解锁判断、进度更新、奖励发放
    /// 调用 Provider 进行数据存储
    /// </summary>
    /// <summary>
    /// 任务框架配置（仅包含框架级参数，业务级重置策略由 ITaskResetScheduler 实现）
    /// </summary>
    [Serializable]
    public class TaskResetConfig
    {
        /// <summary>
        /// 过期检测间隔（秒，默认60秒）
        /// </summary>
        public float expireCheckIntervalSeconds = 60f;
    }

    internal class TaskModule : ModuleBase, ITaskHandlerContext
    {
        private ITaskProvider _provider;
        private ITimeCapability _timeCapability;

        protected override LogChannel LogChannel => LogChannel.Task;
        private RewardHandler _rewardHandler;
        private UnlockCheckHandler _unlockCheckHandler;
        private TaskResetConfig _resetConfig;

        /// <summary>
        /// 任务类型Handler字典：TaskType -> Handler
        /// Handler是共享的，按任务类型管理
        /// </summary>
        private readonly Dictionary<TaskType, ITaskTypeHandler> _typeHandlers = new();

        /// <summary>
        /// 可插拔的重置调度器，由业务层注入
        /// </summary>
        private ITaskResetScheduler _resetScheduler;

        private int _expireCheckTimerId;

        public override int Priority => Frameworkconst.PriorityTaskModule;

        protected override async UniTask OnInitAsync()
        {
            _provider = GetProvider<ITaskProvider>();
            _timeCapability = GetCapability<ITimeCapability>();

            _resetConfig = FrameworkConfig.TaskResetConfig;

            _resetScheduler?.RegisterScheduledResets(_timeCapability, type => ResetTasksByType(type));
            RegisterExpireCheck();

            await base.OnInitAsync();
        }

        /// <summary>
        /// 设置重置调度器（在 InitAsync 之前调用）
        /// </summary>
        internal void SetResetScheduler(ITaskResetScheduler scheduler) => _resetScheduler = scheduler;

        #region 配置

        internal void SetRewardHandler(RewardHandler handler) => _rewardHandler = handler;
        internal void SetUnlockCheckHandler(UnlockCheckHandler handler) => _unlockCheckHandler = handler;

        /// <summary>
        /// 注册任务类型Handler（可选）
        /// 只有特殊任务类型才需要注册Handler
        /// 【重要】Handler是按TaskType共享的，每个TaskType只有一个Handler实例
        /// </summary>
        internal void RegisterTaskTypeHandler(ITaskTypeHandler handler)
        {
            if (handler == null)
            {
                LogWarning($"[{Name}] 注册Handler失败：handler为空");
                return;
            }

            var taskType = handler.TaskType;
            if (_typeHandlers.TryGetValue(taskType, out var oldHandler))
            {
                LogWarning($"[{Name}] Handler已存在，将被覆盖: {taskType}");
                try
                {
                    oldHandler.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] 旧Handler释放异常: {ex.Message}");
                }
            }

            // 注入上下文（在OnRegister之前）
            try
            {
                handler.SetContext(this);
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] Handler.SetContext异常: {ex.Message}");
                return;
            }

            _typeHandlers[taskType] = handler;

            // 调用OnRegister，开始监听触发条件
            try
            {
                handler.OnRegister();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] Handler.OnRegister异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 注销任务类型Handler
        /// </summary>
        internal void UnregisterTaskTypeHandler(TaskType taskType)
        {
            if (_typeHandlers.TryGetValue(taskType, out var handler))
            {
                try
                {
                    handler.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] Handler释放异常: {ex.Message}");
                }
                _typeHandlers.Remove(taskType);
            }
        }

        #endregion

        #region 任务注册

        internal void RegisterTask(TaskData taskData)
        {
            _provider.Store(taskData);
        }

        internal void RegisterTasks(IEnumerable<TaskData> tasks)
        {
            _provider.StoreBatch(tasks);
        }

        internal void UnregisterTask(string taskId)
        {
            _provider.Remove(taskId);
        }

        internal void ClearAllTasks()
        {
            _provider.Clear();
        }

        #endregion

        #region 任务查询

        internal TaskData GetTask(string taskId) => _provider.Get(taskId);
        internal List<TaskData> GetAllTasks() => _provider.GetAll();
        internal List<TaskData> GetTasksByType(TaskType type) => _provider.QueryByType(type);
        internal List<TaskData> GetTasksByState(TaskState state) => _provider.QueryByState(state);
        internal List<TaskData> GetTasksByGroup(string group) => _provider.QueryByGroup(group);
        internal List<TaskData> GetTasks(Func<TaskData, bool> predicate) => _provider.Query(predicate);
        internal int GetCompletedTaskCount() => _provider.QueryByState(TaskState.Completed).Count;
        internal int GetInProgressTaskCount() => _provider.QueryByState(TaskState.InProgress).Count;

        /// <summary>
        /// 获取任务列表（支持排序）
        /// </summary>
        /// <param name="type">任务类型（可选）</param>
        /// <param name="state">任务状态（可选）</param>
        /// <param name="sortBy">排序方式</param>
        /// <param name="ascending">是否升序</param>
        /// <returns>排序后的任务列表</returns>
        internal List<TaskData> GetTasksSorted(TaskType? type = null, TaskState? state = null, TaskSortBy sortBy = TaskSortBy.Priority, bool ascending = true)
        {
            List<TaskData> tasks;
            
            // 先按条件筛选
            if (type.HasValue && state.HasValue)
            {
                tasks = _provider.Query(t => t.Type == type.Value && t.State == state.Value);
            }
            else if (type.HasValue)
            {
                tasks = _provider.QueryByType(type.Value);
            }
            else if (state.HasValue)
            {
                tasks = _provider.QueryByState(state.Value);
            }
            else
            {
                tasks = _provider.GetAll();
            }

            // 排序
            IOrderedEnumerable<TaskData> orderedTasks;
            switch (sortBy)
            {
                case TaskSortBy.Priority:
                    orderedTasks = ascending 
                        ? tasks.OrderBy(t => t.Priority)
                        : tasks.OrderByDescending(t => t.Priority);
                    break;
                case TaskSortBy.Type:
                    orderedTasks = ascending 
                        ? tasks.OrderBy(t => t.Type)
                        : tasks.OrderByDescending(t => t.Type);
                    break;
                case TaskSortBy.State:
                    orderedTasks = ascending 
                        ? tasks.OrderBy(t => t.State)
                        : tasks.OrderByDescending(t => t.State);
                    break;
                default:
                    orderedTasks = ascending 
                        ? tasks.OrderBy(t => t.Priority)
                        : tasks.OrderByDescending(t => t.Priority);
                    break;
            }

            // 如果按优先级排序，相同优先级时按类型和状态排序
            if (sortBy == TaskSortBy.Priority)
            {
                orderedTasks = ascending
                    ? orderedTasks.ThenBy(t => t.Type).ThenBy(t => t.State)
                    : orderedTasks.ThenByDescending(t => t.Type).ThenByDescending(t => t.State);
            }

            return orderedTasks.ToList();
        }

        #endregion

        #region 业务逻辑 - 解锁判断

        /// <summary>
        /// 检查任务是否可解锁（业务逻辑）
        /// </summary>
        internal bool CanUnlockTask(string taskId)
        {
            var task = _provider.Get(taskId);
            if (task == null) return false;
            if (task.State != TaskState.Locked) return false;
            if (IsTaskExpired(task)) return false;
            if (!ArePrerequisiteTasksCompleted(task)) return false;
            if (_unlockCheckHandler != null && !_unlockCheckHandler(task)) return false;
            return true;
        }

        /// <summary>
        /// 检查前置任务是否都已完成（业务逻辑）
        /// </summary>
        private bool ArePrerequisiteTasksCompleted(TaskData task)
        {
            if (task.PrerequisiteTaskIds == null || task.PrerequisiteTaskIds.Count == 0)
                return true;

            foreach (var prereqId in task.PrerequisiteTaskIds)
            {
                var prereqTask = _provider.Get(prereqId);
                if (prereqTask == null || prereqTask.State < TaskState.Completed)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 检查任务是否过期（业务逻辑）
        /// </summary>
        private bool IsTaskExpired(TaskData task)
        {
            return task.ExpireTime.HasValue && DateTime.UtcNow > task.ExpireTime.Value;
        }

        #endregion

        #region 业务逻辑 - 状态流转

        /// <summary>
        /// 解锁任务
        /// </summary>
        internal bool UnlockTask(string taskId)
        {
            if (!CanUnlockTask(taskId))
            {
                LogWarning($"[{Name}] 解锁任务失败：任务 {taskId} 不满足解锁条件");
                return false;
            }

            var task = _provider.Get(taskId);
            var oldState = task.State;
            task.State = TaskState.InProgress;
            _provider.Update(task);

            // 激活Handler（如果存在）
            ActivateHandler(taskId, task);

            PublishEvent(new TaskUnlockedEvent { TaskId = taskId, TaskData = task });
            PublishEvent(new TaskStateChangedEvent
            {
                TaskId = taskId,
                OldState = oldState,
                NewState = TaskState.InProgress,
                TaskData = task
            });

            return true;
        }

        /// <summary>
        /// 尝试解锁所有可解锁的任务
        /// </summary>
        internal int TryUnlockAllTasks()
        {
            int count = 0;
            var lockedTasks = _provider.QueryByState(TaskState.Locked);

            foreach (var task in lockedTasks)
            {
                if (CanUnlockTask(task.TaskId) && UnlockTask(task.TaskId))
                {
                    count++;
                }
            }
            return count;
        }

        #endregion

        #region 业务逻辑 - 进度更新

        /// <summary>
        /// 更新任务进度（通用接口，增量更新）
        /// 适用于Accumulate类型的条件
        /// </summary>
        internal void UpdateProgress(TaskConditionType conditionType, string param, int delta = 1)
        {
            var matches = _provider.QueryByCondition(conditionType, param);

            foreach (var (taskId, conditionId) in matches)
            {
                var task = _provider.Get(taskId);
                if (task == null || task.State != TaskState.InProgress)
                    continue;

                UpdateTaskCondition(task, conditionId, delta, false);
            }
        }

        /// <summary>
        /// 更新达到数值类型的任务进度（绝对值更新）
        /// 适用于Reach类型的条件
        /// </summary>
        internal void UpdateReachProgress(string param, int value)
        {
            var matches = _provider.QueryByCondition(TaskConditionType.Reach, param);

            foreach (var (taskId, conditionId) in matches)
            {
                var task = _provider.Get(taskId);
                if (task == null || task.State != TaskState.InProgress)
                    continue;

                var condition = task.Conditions?.Find(c => c.ConditionId == conditionId);
                if (condition == null) continue;

                // 使用绝对值更新，取当前值和目标值的较大者（但不能超过目标值）
                // 这样可以支持"达到数值"的语义：如果当前值已经达到目标，就保持目标值
                var newValue = Math.Min(Math.Max(condition.CurrentValue, value), condition.TargetValue);
                UpdateTaskCondition(task, conditionId, newValue, true);
            }
        }

        /// <summary>
        /// 更新指定任务的进度
        /// </summary>
        internal void UpdateTaskProgress(string taskId, string conditionId, int value)
        {
            var task = _provider.Get(taskId);
            if (task == null) return;

            UpdateTaskCondition(task, conditionId, value, true);
        }

        private void UpdateTaskCondition(TaskData task, string conditionId, int valueOrDelta, bool isAbsolute)
        {
            var condition = task.Conditions?.Find(c => c.ConditionId == conditionId);
            if (condition == null) return;

            var oldValue = condition.CurrentValue;
            var wasCompleted = condition.IsCompleted;
            var wasTaskCompleted = task.AreAllConditionsCompleted();

            // 更新值
            if (isAbsolute)
            {
                // 绝对值更新（Reach类型）：取当前值和目标值的较大者，但不能超过目标值
                // 这样可以支持"达到数值"的语义：如果当前值已经达到目标，就保持目标值
                condition.CurrentValue = Math.Min(Math.Max(condition.CurrentValue, valueOrDelta), condition.TargetValue);
            }
            else
            {
                // 增量更新（Accumulate类型）：累加数值，但不能超过目标值
                condition.CurrentValue = Math.Min(condition.CurrentValue + valueOrDelta, condition.TargetValue);
            }

            _provider.Update(task);

            var justCompleted = !wasCompleted && condition.IsCompleted;
            var taskJustCompleted = !wasTaskCompleted && task.AreAllConditionsCompleted();

            // 发布进度事件
            PublishEvent(new TaskProgressUpdatedEvent
            {
                TaskId = task.TaskId,
                ConditionId = conditionId,
                OldValue = oldValue,
                NewValue = condition.CurrentValue,
                TargetValue = condition.TargetValue,
                ConditionJustCompleted = justCompleted,
                TaskJustCompleted = taskJustCompleted
            });

            // 任务完成
            if (taskJustCompleted && task.State == TaskState.InProgress)
            {
                var oldState = task.State;
                task.State = TaskState.Completed;
                _provider.Update(task);

                PublishEvent(new TaskStateChangedEvent
                {
                    TaskId = task.TaskId,
                    OldState = oldState,
                    NewState = TaskState.Completed,
                    TaskData = task
                });

                PublishEvent(new TaskCompletedEvent { TaskId = task.TaskId, TaskData = task });

                // 任务完成后自动检查并解锁后续任务
                OnTaskCompleted(task.TaskId);
            }
        }

        #endregion

        #region 业务逻辑 - 奖励领取

        /// <summary>
        /// 领取任务奖励
        /// </summary>
        internal bool ClaimReward(string taskId)
        {
            var task = _provider.Get(taskId);
            if (task == null)
            {
                LogWarning($"[{Name}] 领取奖励失败：任务 {taskId} 不存在");
                return false;
            }

            if (task.State != TaskState.Completed)
            {
                LogWarning($"[{Name}] 领取奖励失败：任务 {taskId} 状态不是Completed");
                return false;
            }

            // 发放奖励
            if (task.Rewards != null && task.Rewards.Count > 0)
            {
                if (_rewardHandler == null)
                {
                    // 【重要】有奖励但未设置Handler，记录警告
                    LogWarning($"[{Name}] 任务 {taskId} 有 {task.Rewards.Count} 个奖励待发放，但未设置 RewardHandler！" +
                        "请调用 GF.Task.SetRewardHandler() 设置奖励处理器。奖励将不会被发放！");
                }
                else
                {
                    if (!_rewardHandler(task.Rewards))
                    {
                        LogWarning($"[{Name}] 奖励发放失败：任务 {taskId}");
                        return false;
                    }
                }
            }

            var oldState = task.State;
            task.State = TaskState.Rewarded;
            _provider.Update(task);

            PublishEvent(new TaskStateChangedEvent
            {
                TaskId = taskId,
                OldState = oldState,
                NewState = TaskState.Rewarded,
                TaskData = task
            });

            PublishEvent(new TaskRewardClaimedEvent
            {
                TaskId = taskId,
                Rewards = task.Rewards,
                TaskData = task
            });

            TryUnlockAllTasks();

            return true;
        }

        /// <summary>
        /// 一键领取所有可领取的奖励
        /// </summary>
        internal List<string> ClaimAllRewards()
        {
            var claimed = new List<string>();
            var completedTasks = _provider.QueryByState(TaskState.Completed);

            foreach (var task in completedTasks)
            {
                if (ClaimReward(task.TaskId))
                    claimed.Add(task.TaskId);
            }
            return claimed;
        }

        #endregion

        #region 任务重置

        internal bool ResetTask(string taskId)
        {
            var task = _provider.Get(taskId);
            if (task == null) return false;

            // 重置条件进度
            if (task.Conditions != null)
            {
                foreach (var condition in task.Conditions)
                    condition.CurrentValue = 0;
            }

            task.State = TaskState.InProgress;
            _provider.Update(task);

            return true;
        }

        internal void ResetTasksByType(TaskType type)
        {
            var tasks = _provider.QueryByType(type);
            foreach (var task in tasks)
            {
                if (task.State == TaskState.Rewarded || task.State == TaskState.InProgress)
                    ResetTask(task.TaskId);
            }
        }

        #endregion

        #region 数据持久化

        internal Dictionary<string, TaskSaveData> ExportProgress() => _provider.Export();
        internal void ImportProgress(Dictionary<string, TaskSaveData> data) => _provider.Import(data);

        #endregion

        #region 配置加载

        internal void LoadFromConfig(TaskConfig config, DateTime? baseTime = null)
        {
            if (config == null) return;
            var taskData = config.ToTaskData(baseTime);
            _provider.Store(taskData);
        }

        internal void LoadFromConfigTable(TaskConfigTable configTable, DateTime? baseTime = null)
        {
            if (configTable?.Tasks == null) return;
            var taskDataList = configTable.ToTaskDataList(baseTime);
            _provider.StoreBatch(taskDataList);
        }

        #endregion

        #region Handler管理

        /// <summary>
        /// 激活Handler（任务解锁时调用）
        /// Handler是共享的，按任务类型管理，不需要按任务ID存储
        /// </summary>
        private void ActivateHandler(string taskId, TaskData taskData)
        {
            // 获取该任务类型的Handler
            if (!_typeHandlers.TryGetValue(taskData.Type, out var handler))
            {
                return; // 没有注册Handler，使用默认行为
            }

            // 调用Handler的OnTaskUnlocked方法
            // Handler是共享的，不需要存储到_activeHandlers
            try
            {
                handler.OnTaskUnlocked(taskData);
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] Handler.OnTaskUnlocked异常: {ex.Message}");
            }
        }

        #endregion

        #region 任务链支持

        /// <summary>
        /// 任务完成后自动检查并解锁后续任务
        /// </summary>
        private void OnTaskCompleted(string taskId)
        {
            // 1. 查找所有以该任务为前置任务的任务
            var nextTasks = _provider.Query(t =>
                t.PrerequisiteTaskIds != null &&
                t.PrerequisiteTaskIds.Contains(taskId) &&
                t.State == TaskState.Locked
            );

            // 2. 尝试解锁这些任务
            // CanUnlockTask已经检查了前置任务，直接调用即可
            foreach (var nextTask in nextTasks)
            {
                if (CanUnlockTask(nextTask.TaskId))
                {
                    UnlockTask(nextTask.TaskId);
                }
            }

            // 3. 关键时机：尝试解锁所有可解锁的任务
            TryUnlockAllTasks();
        }

        #endregion

        #region 定时重置和过期检测

        private void RegisterExpireCheck()
        {
            // 通过 ITimeCapability 注册重复定时器（自动分配 ID）
            _expireCheckTimerId = _timeCapability.ScheduleRepeat(_resetConfig.expireCheckIntervalSeconds, CheckExpiredTasks, useRealTime: true);
        }

        /// <summary>
        /// 检测并标记过期任务
        /// </summary>
        private void CheckExpiredTasks()
        {
            var now = DateTime.UtcNow;
            var inProgressTasks = _provider.QueryByState(TaskState.InProgress);

            foreach (var task in inProgressTasks)
            {
                if (task.ExpireTime.HasValue && now > task.ExpireTime.Value)
                {
                    var oldState = task.State;
                    task.State = TaskState.Expired;
                    _provider.Update(task);

                    PublishEvent(new TaskStateChangedEvent
                    {
                        TaskId = task.TaskId,
                        OldState = oldState,
                        NewState = TaskState.Expired,
                        TaskData = task
                    });
                }
            }
        }

        #endregion

        private void PublishEvent<T>(T evt) where T : class, IEvent
        {
            try
            {
                EventBus.Publish(evt);
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 发布事件失败: {ex.Message}");
            }
        }

        #region ITaskHandlerContext 实现

        /// <summary>
        /// 事件总线（供Handler使用）
        /// </summary>
        IEventBus ITaskHandlerContext.EventBus => EventBus;

        /// <summary>
        /// 更新任务进度（供Handler使用）
        /// </summary>
        void ITaskHandlerContext.UpdateProgress(TaskConditionType conditionType, string param, int delta)
        {
            UpdateProgress(conditionType, param, delta);
        }

        /// <summary>
        /// 更新指定任务的进度（供Handler使用）
        /// </summary>
        void ITaskHandlerContext.UpdateTaskProgress(string taskId, string conditionId, int value)
        {
            UpdateTaskProgress(taskId, conditionId, value);
        }

        /// <summary>
        /// 获取任务数据（供Handler使用）
        /// </summary>
        TaskData ITaskHandlerContext.GetTask(string taskId)
        {
            return GetTask(taskId);
        }

        #endregion

        protected override UniTask OnShutdownAsync()
        {
            _resetScheduler?.UnregisterScheduledResets(_timeCapability);

            if (_timeCapability != null && _expireCheckTimerId != 0)
            {
                _timeCapability.CancelTimer(_expireCheckTimerId);
            }

            foreach (var handler in _typeHandlers.Values)
            {
                try
                {
                    handler.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"[{Name}] Handler释放异常: {ex.Message}");
                }
            }
            _typeHandlers.Clear();

            _provider = null;
            _timeCapability = null;
            _rewardHandler = null;
            _unlockCheckHandler = null;
            _resetScheduler = null;
            return base.OnShutdownAsync();
        }
    }
}
