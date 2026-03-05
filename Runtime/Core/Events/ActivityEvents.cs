using JulyCore.Data.Activity;

namespace JulyCore.Core.Events
{
    /// <summary>
    /// 活动开启事件
    /// 当活动首次进入"进行中"状态时触发
    /// </summary>
    public class ActivityOpenedEvent : IEvent
    {
        /// <summary>
        /// 活动 ID
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// 活动定义
        /// </summary>
        public ActivityDefinition Definition { get; set; }
    }

    /// <summary>
    /// 活动关闭事件
    /// 当活动进入"已结束"状态时触发
    /// </summary>
    public class ActivityClosedEvent : IEvent
    {
        /// <summary>
        /// 活动 ID
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// 活动定义
        /// </summary>
        public ActivityDefinition Definition { get; set; }
    }

    /// <summary>
    /// 活动进度变更事件
    /// 当活动运行时记录更新时触发
    /// </summary>
    public class ActivityProgressChangedEvent : IEvent
    {
        /// <summary>
        /// 活动 ID
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// 活动运行时记录
        /// </summary>
        public ActivityRecord Record { get; set; }
    }

    /// <summary>
    /// 活动模块就绪事件
    /// 当活动模块初始化完成后触发
    /// </summary>
    public class ActivityModuleReadyEvent : IEvent
    {
        /// <summary>
        /// 当前进行中的活动数量
        /// </summary>
        public int ActiveCount { get; set; }

        /// <summary>
        /// 新开启的活动数量
        /// </summary>
        public int NewlyOpenedCount { get; set; }
    }

    /// <summary>
    /// 活动注册完成事件
    /// 当业务层完成活动注册后触发
    /// </summary>
    public class ActivityRegisteredEvent : IEvent
    {
        /// <summary>
        /// 注册的活动数量
        /// </summary>
        public int RegisteredCount { get; set; }
    }
}
