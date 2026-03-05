using System.Collections.Generic;
using JulyCore.Data.Task;

namespace JulyCore.Core.Events
{
    /// <summary>
    /// 任务状态变更事件
    /// </summary>
    public class TaskStateChangedEvent : IEvent
    {
        public string TaskId { get; set; }
        public TaskState OldState { get; set; }
        public TaskState NewState { get; set; }
        public TaskData TaskData { get; set; }
    }

    /// <summary>
    /// 任务进度更新事件
    /// </summary>
    public class TaskProgressUpdatedEvent : IEvent
    {
        public string TaskId { get; set; }
        public string ConditionId { get; set; }
        public int OldValue { get; set; }
        public int NewValue { get; set; }
        public int TargetValue { get; set; }
        public bool ConditionJustCompleted { get; set; }
        public bool TaskJustCompleted { get; set; }
    }

    /// <summary>
    /// 任务完成事件
    /// </summary>
    public class TaskCompletedEvent : IEvent
    {
        public string TaskId { get; set; }
        public TaskData TaskData { get; set; }
    }

    /// <summary>
    /// 任务奖励领取事件
    /// </summary>
    public class TaskRewardClaimedEvent : IEvent
    {
        public string TaskId { get; set; }
        public List<TaskReward> Rewards { get; set; }
        public TaskData TaskData { get; set; }
    }

    /// <summary>
    /// 任务解锁事件
    /// </summary>
    public class TaskUnlockedEvent : IEvent
    {
        public string TaskId { get; set; }
        public TaskData TaskData { get; set; }
    }
}

