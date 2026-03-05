using System;

namespace JulyCore.Core
{
    /// <summary>
    /// 事件总线接口
    /// 用于模块间解耦通信
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 订阅事件（绑定对象，支持批量移除）
        /// </summary>
        /// <typeparam name="TEvent">事件类型（必须实现IEvent接口）</typeparam>
        /// <param name="handler">事件处理器</param>
        /// <param name="target">绑定对象，用于批量移除该对象的所有监听</param>
        void Subscribe<TEvent>(Action<TEvent> handler, object target) where TEvent : IEvent;

        /// <summary>
        /// 订阅事件（带优先级）
        /// </summary>
        /// <typeparam name="TEvent">事件类型（必须实现IEvent接口）</typeparam>
        /// <param name="handler">事件处理器</param>
        /// <param name="target">绑定对象，用于批量移除该对象的所有监听</param>
        /// <param name="priority">优先级（数值越小优先级越高，默认0）</param>
        void Subscribe<TEvent>(Action<TEvent> handler, object target, int priority) where TEvent : IEvent;

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型（必须实现IEvent接口）</typeparam>
        /// <param name="handler">事件处理器</param>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型（必须实现IEvent接口）</typeparam>
        /// <param name="eventData">事件数据</param>
        void Publish<TEvent>(TEvent eventData) where TEvent : IEvent;

        /// <summary>
        /// 移除指定对象的所有事件监听
        /// </summary>
        /// <param name="target">要移除监听的对象</param>
        void UnsubscribeAll(object target);

        /// <summary>
        /// 清除所有订阅
        /// </summary>
        void Clear();

        /// <summary>
        /// 处理事件总线延迟动作（帧分片机制）
        /// </summary>
        void ProcessDeferredActions();
    }
}
