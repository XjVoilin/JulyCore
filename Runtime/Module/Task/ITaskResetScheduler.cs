using System;
using JulyCore.Core;
using JulyCore.Data.Task;

namespace JulyCore.Module.Task
{
    /// <summary>
    /// 任务重置调度器接口。
    /// 由业务层实现，定义哪些任务类型需要定时重置、何时重置。
    /// 框架层不预设任何业务特定的重置逻辑（如每日/每周）。
    /// 
    /// 使用示例：
    /// <code>
    /// public class MyResetScheduler : ITaskResetScheduler
    /// {
    ///     private int _dailyTimerId;
    ///     
    ///     public void RegisterScheduledResets(ITimeCapability time, Action&lt;TaskType&gt; resetAction)
    ///     {
    ///         var delay = CalcNextDailyDelay(time.ServerTimeUtc);
    ///         _dailyTimerId = time.ScheduleOnce(delay, () =&gt;
    ///         {
    ///             resetAction(MyTaskType.Daily);
    ///             RegisterScheduledResets(time, resetAction); // 递归注册下一次
    ///         }, useRealTime: true);
    ///     }
    ///     
    ///     public void UnregisterScheduledResets(ITimeCapability time)
    ///     {
    ///         time.CancelTimer(_dailyTimerId);
    ///     }
    /// }
    /// </code>
    /// </summary>
    public interface ITaskResetScheduler
    {
        /// <summary>
        /// 注册所有重置调度（在 TaskModule 初始化时调用）。
        /// 实现方应使用 ITimeCapability 注册定时器，并在回调中调用 resetAction 触发重置。
        /// </summary>
        /// <param name="timeCapability">时间能力接口，用于注册定时器</param>
        /// <param name="resetAction">重置回调，传入需要重置的任务类型</param>
        void RegisterScheduledResets(ITimeCapability timeCapability, Action<TaskType> resetAction);

        /// <summary>
        /// 注销所有重置调度（在 TaskModule 关闭时调用）
        /// </summary>
        /// <param name="timeCapability">时间能力接口，用于取消定时器</param>
        void UnregisterScheduledResets(ITimeCapability timeCapability);
    }
}
