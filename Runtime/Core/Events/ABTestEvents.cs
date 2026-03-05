using System.Collections.Generic;
using JulyCore.Data.ABTest;

namespace JulyCore.Core.Events
{
    /// <summary>
    /// 用户分配到实验组事件
    /// </summary>
    public class UserAssignedToExperimentEvent : IEvent
    {
        public string UserId { get; set; }
        public string ExperimentId { get; set; }
        public string GroupId { get; set; }
        public Experiment Experiment { get; set; }
        public ExperimentGroup Group { get; set; }
        public bool IsNewAssignment { get; set; }
    }

    /// <summary>
    /// 实验曝光事件
    /// </summary>
    public class ExperimentExposureEvent : IEvent
    {
        public string UserId { get; set; }
        public string ExperimentId { get; set; }
        public string GroupId { get; set; }
        public string Scene { get; set; }
        public bool IsFirstExposure { get; set; }
        public Dictionary<string, object> ExtraData { get; set; }
    }

    /// <summary>
    /// 实验状态变更事件
    /// </summary>
    public class ExperimentStatusChangedEvent : IEvent
    {
        public string ExperimentId { get; set; }
        public ExperimentStatus OldStatus { get; set; }
        public ExperimentStatus NewStatus { get; set; }
        public Experiment Experiment { get; set; }
    }

    /// <summary>
    /// 实验配置更新事件
    /// </summary>
    public class ExperimentConfigUpdatedEvent : IEvent
    {
        public string ExperimentId { get; set; }
        public Experiment OldConfig { get; set; }
        public Experiment NewConfig { get; set; }
    }

    /// <summary>
    /// 用户退出实验事件
    /// </summary>
    public class UserExitedExperimentEvent : IEvent
    {
        public string UserId { get; set; }
        public string ExperimentId { get; set; }
        public string GroupId { get; set; }
        public string Reason { get; set; }
    }
}

