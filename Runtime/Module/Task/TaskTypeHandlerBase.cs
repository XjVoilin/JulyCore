using System;
using JulyCore.Core;
using JulyCore.Data.Task;

namespace JulyCore.Module.Task
{
    /// <summary>
    /// 任务类型处理器基类
    /// 提供辅助方法，简化Handler实现
    /// 
    /// 【重要】Handler是按TaskType共享的
    /// - 每个TaskType只有一个共享的Handler实例
    /// - OnTaskUnlocked会为每个解锁的任务调用，但Handler本身是共享的
    /// - Handler内部不应维护任务相关的状态，状态应存储在TaskData中
    /// </summary>
    public abstract class TaskTypeHandlerBase : ITaskTypeHandler
    {
        private ITaskHandlerContext _context;
        
        /// <summary>
        /// 任务处理器上下文（由框架注入）
        /// 提供EventBus和Task操作的访问
        /// </summary>
        protected ITaskHandlerContext Context => _context;
        
        /// <summary>
        /// 事件总线（快捷访问）
        /// </summary>
        protected IEventBus EventBus => _context?.EventBus;
        
        public abstract TaskType TaskType { get; }
        
        /// <summary>
        /// 设置上下文（由框架在注册前调用）
        /// </summary>
        public void SetContext(ITaskHandlerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        
        public virtual void OnRegister() { }
        
        public virtual void OnTaskUnlocked(TaskData taskData) { }
        
        public virtual void Dispose()
        {
            // 清理所有事件订阅
            _context?.EventBus?.UnsubscribeAll(this);
            _context = null;
        }
        
        #region 辅助方法
        
        /// <summary>
        /// 订阅事件（会自动在Dispose时清理）
        /// </summary>
        protected void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            EnsureContext();
            _context.EventBus.Subscribe(handler, this);
        }
        
        /// <summary>
        /// 当收到事件时自动更新任务进度
        /// 注意：UpdateProgress内部会过滤状态，只处理InProgress的任务
        /// 所以即使任务完成后事件监听还在，也不会影响已完成的任务
        /// </summary>
        protected void UpdateProgressOnEvent<TEvent>(
            TaskConditionType conditionType,
            Func<TEvent, string> paramSelector,
            Func<TEvent, int> valueSelector = null
        ) where TEvent : IEvent
        {
            EnsureContext();
            _context.EventBus.Subscribe<TEvent>(e =>
            {
                // UpdateProgress内部会过滤状态，只处理InProgress的任务
                // 所以即使任务完成后事件监听还在，也不会影响已完成的任务
                var param = paramSelector(e);
                var value = valueSelector?.Invoke(e) ?? 1;
                _context.UpdateProgress(conditionType, param, value);
            }, this);
        }
        
        /// <summary>
        /// 当收到事件时更新指定任务的进度
        /// 注意：会检查任务状态，只处理InProgress的任务，避免已完成任务继续更新
        /// </summary>
        protected void UpdateTaskProgressOnEvent<TEvent>(
            string taskId,
            string conditionId,
            Func<TEvent, int> valueSelector
        ) where TEvent : IEvent
        {
            EnsureContext();
            _context.EventBus.Subscribe<TEvent>(e =>
            {
                // 检查任务状态，只处理InProgress的任务
                // 避免已完成任务继续更新进度
                var task = _context.GetTask(taskId);
                if (task == null || task.State != TaskState.InProgress)
                    return;
                
                var value = valueSelector(e);
                _context.UpdateTaskProgress(taskId, conditionId, value);
            }, this);
        }
        
        /// <summary>
        /// 更新任务进度（增量更新）
        /// </summary>
        protected void UpdateProgress(TaskConditionType conditionType, string param, int delta = 1)
        {
            EnsureContext();
            _context.UpdateProgress(conditionType, param, delta);
        }
        
        /// <summary>
        /// 更新指定任务的进度（绝对值更新）
        /// </summary>
        protected void UpdateTaskProgress(string taskId, string conditionId, int value)
        {
            EnsureContext();
            _context.UpdateTaskProgress(taskId, conditionId, value);
        }
        
        /// <summary>
        /// 获取任务数据
        /// </summary>
        protected TaskData GetTask(string taskId)
        {
            EnsureContext();
            return _context.GetTask(taskId);
        }
        
        private void EnsureContext()
        {
            if (_context == null)
            {
                throw new InvalidOperationException($"[{GetType().Name}] Context未初始化，请确保Handler已通过TaskModule注册");
            }
        }
        
        #endregion
    }
}
