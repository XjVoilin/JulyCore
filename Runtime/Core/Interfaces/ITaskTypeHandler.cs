using System;
using JulyCore.Data.Task;

namespace JulyCore.Core
{
    /// <summary>
    /// 任务处理器上下文
    /// 用于向Handler注入框架依赖，避免Handler直接依赖GF门面类
    /// </summary>
    public interface ITaskHandlerContext
    {
        /// <summary>
        /// 事件总线
        /// </summary>
        IEventBus EventBus { get; }
        
        /// <summary>
        /// 更新任务进度（增量更新，适用于Accumulate类型）
        /// </summary>
        void UpdateProgress(TaskConditionType conditionType, string param, int delta = 1);
        
        /// <summary>
        /// 更新指定任务的进度（绝对值更新）
        /// </summary>
        void UpdateTaskProgress(string taskId, string conditionId, int value);
        
        /// <summary>
        /// 获取任务数据
        /// </summary>
        TaskData GetTask(string taskId);
    }

    /// <summary>
    /// 任务类型处理器接口（可选）
    /// 按任务类型处理，负责事件监听和数据处理
    /// 只有特殊任务类型才需要实现此接口
    /// 
    /// 【设计理念】
    /// 1. 性能优化：任务解锁时才注册事件监听，任务完成后清理订阅
    /// 2. 事件驱动：根据不同事件更新任务进度
    /// 3. 职责分离：只负责事件监听和数据处理，UI由UI层处理
    /// 
    /// 【生命周期】
    /// 1. SetContext() - 注册前由框架调用，注入上下文依赖
    /// 2. OnRegister() - 注册时调用，开始监听触发条件（轻量级）
    /// 3. OnTaskUnlocked() - 任务解锁时调用，注册事件监听，处理任务数据
    /// 4. Dispose() - Module关闭时框架自动调用，清理所有订阅
    /// 
    /// 【重要】Handler是按TaskType共享的
    /// - 每个TaskType只有一个共享的Handler实例
    /// - OnTaskUnlocked会为每个解锁的任务调用，但Handler本身是共享的
    /// - Handler内部不应维护任务相关的状态，状态应存储在TaskData中
    /// </summary>
    public interface ITaskTypeHandler : IDisposable
    {
        /// <summary>
        /// 任务类型
        /// </summary>
        TaskType TaskType { get; }
        
        /// <summary>
        /// 设置上下文（由框架在注册前调用）
        /// </summary>
        void SetContext(ITaskHandlerContext context);
        
        /// <summary>
        /// 注册时调用
        /// 在此订阅触发条件事件，决定何时激活该类型的任务
        /// 只有该类型的任务解锁时才会调用OnTaskUnlocked
        /// </summary>
        void OnRegister();
        
        /// <summary>
        /// 任务解锁时调用
        /// 在此：
        /// 1. 注册事件监听，根据不同事件更新任务进度
        /// 2. 处理任务数据（如何解析、如何使用）
        /// </summary>
        /// <param name="taskData">任务数据</param>
        void OnTaskUnlocked(TaskData taskData);
    }
}
