using System;
using JulyCore.Core;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 事件相关操作
        /// </summary>
        public static class Event
        {
            /// <summary>
            /// 订阅事件（绑定对象，支持批量移除）
            /// </summary>
            /// <typeparam name="T">事件类型（必须实现IEvent接口）</typeparam>
            /// <param name="handler">事件处理器</param>
            /// <param name="target">绑定对象，用于批量移除该对象的所有监听</param>
            public static void Subscribe<T>(Action<T> handler, object target) where T : IEvent
            {
                _context.EventBus.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅事件（带优先级）
            /// </summary>
            /// <typeparam name="T">事件类型（必须实现IEvent接口）</typeparam>
            /// <param name="handler">事件处理器</param>
            /// <param name="target">绑定对象，用于批量移除该对象的所有监听</param>
            /// <param name="priority">优先级（数值越小优先级越高，默认0）</param>
            public static void Subscribe<T>(Action<T> handler, object target, int priority) where T : IEvent
            {
                _context.EventBus.Subscribe(handler, target, priority);
            }

            /// <summary>
            /// 取消订阅事件
            /// </summary>
            /// <typeparam name="T">事件类型（必须实现IEvent接口）</typeparam>
            /// <param name="handler">事件处理器</param>
            public static void Unsubscribe<T>(Action<T> handler) where T : IEvent
            {
                _context.EventBus.Unsubscribe(handler);
            }

            /// <summary>
            /// 移除指定对象的所有事件监听
            /// </summary>
            /// <param name="target">要移除监听的对象</param>
            public static void UnsubscribeAll(object target)
            {
                _context.EventBus.UnsubscribeAll(target);
            }

            /// <summary>
            /// 发布事件
            /// </summary>
            /// <typeparam name="T">事件类型（必须实现IEvent接口）</typeparam>
            /// <param name="eventData">事件数据</param>
            public static void Publish<T>(T eventData) where T : IEvent
            {
                _context.EventBus.Publish(eventData);
            }
        }
    }
}
