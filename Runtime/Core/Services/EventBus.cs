using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace JulyCore.Core
{
    
    /// <summary>
    /// 事件总线配置
    /// </summary>
    [Serializable]
    public class EventBusConfig
    {
        /// <summary>
        /// 单个处理器执行超时阈值（毫秒），超过此时间会记录警告
        /// </summary>
        public int handlerTimeoutMs = 16;

        /// <summary>
        /// 单次Publish最大处理器执行数量，超过后延迟到下一帧
        /// </summary>
        public int maxHandlersPerFrame  = 50;

        /// <summary>
        /// 是否启用帧分片（订阅者过多时分帧执行）
        /// </summary>
        public bool enableFrameSlicing  = true;

        /// <summary>
        /// 处理器连续异常阈值，超过后自动禁用
        /// </summary>
        public int handlerExceptionThreshold  = 3;
    }

    
    /// <summary>
    /// 事件总线实现
    /// </summary>
    internal class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
        private readonly Dictionary<object, HashSet<HandlerInfo>> _targetToHandlers = new();
        private readonly Dictionary<Delegate, int> _handlerPriorityCache = new();
        private readonly Dictionary<Delegate, int> _handlerExceptionCounts = new();
        private readonly ConcurrentQueue<Action> _deferredActions = new();
        private readonly object _lockObject = new();
        private EventBusConfig _config;

#if JULYGF_ENABLE_LOG
        private Stopwatch _stopwatch = new Stopwatch();
#endif

        private class HandlerInfo
        {
            public Type EventType;
            public Delegate Handler;
            public int Priority;
        }

        internal EventBus(EventBusConfig config)
        {
            _config = config;
        }

        public void Subscribe<TEvent>(Action<TEvent> handler, object target) where TEvent : IEvent
            => Subscribe(handler, target, 0);

        public void Subscribe<TEvent>(Action<TEvent> handler, object target, int priority) where TEvent : IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (target == null) throw new ArgumentNullException(nameof(target));
            SubscribeInternal(typeof(TEvent), handler, target, priority);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            if (handler != null) UnsubscribeInternal(typeof(TEvent), handler);
        }

        private void SubscribeInternal(Type eventType, Delegate handler, object target, int priority)
        {
            var handlerList = _handlers.GetOrAdd(eventType, _ => new List<Delegate>());
            lock (_lockObject)
            {
                if (handlerList.Contains(handler)) return;

                handlerList.Add(handler);

                if (!_targetToHandlers.TryGetValue(target, out var handlerSet))
                {
                    handlerSet = new HashSet<HandlerInfo>();
                    _targetToHandlers[target] = handlerSet;
                }

                handlerSet.Add(new HandlerInfo
                {
                    EventType = eventType,
                    Handler = handler,
                    Priority = priority
                });

                _handlerPriorityCache[handler] = priority;
                _handlerExceptionCounts[handler] = 0;
            }
        }

        private void UnsubscribeInternal(Type eventType, Delegate handler)
        {
            if (!_handlers.TryGetValue(eventType, out var handlerList)) return;

            lock (_lockObject)
            {
                handlerList.Remove(handler);
                if (handlerList.Count == 0)
                {
                    _handlers.TryRemove(eventType, out _);
                }

                // 从映射中移除
                var targetsToRemove = new List<object>();
                foreach (var kvp in _targetToHandlers)
                {
                    var isRemoved = TryRemoveHandleInfo(kvp.Value, eventType, handler);
                    if (isRemoved && kvp.Value.Count == 0)
                    {
                        targetsToRemove.Add(kvp.Key);
                    }
                }

                foreach (var target in targetsToRemove)
                {
                    _targetToHandlers.Remove(target);
                }

                _handlerPriorityCache.Remove(handler);
                _handlerExceptionCounts.Remove(handler);
            }
        }

        private bool TryRemoveHandleInfo(HashSet<HandlerInfo> handleList, Type eventType, Delegate handler)
        {
            foreach (var handlerInfo in handleList)
            {
                if (handlerInfo.EventType != eventType || handlerInfo.Handler != handler)
                    continue;
                handleList.Remove(handlerInfo);
                return true;
            }

            return false;
        }

        public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (eventData == null) return;

            var eventType = typeof(TEvent);

            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                InvokeHandlers(handlers, eventData, eventType);
            }
        }

        public void ProcessDeferredActions()
        {
            var processedCount = 0;
            while (_deferredActions.TryDequeue(out var action) && processedCount < _config.maxHandlersPerFrame)
            {
                try
                {
                    action();
                    processedCount++;
                }
                catch (Exception ex)
                {
                    JLogger.LogError($"{Frameworkconst.TagEventBus} 延迟执行处理器异常: {ex.Message}");
                }
            }
        }

        public int PendingDeferredActionCount => _deferredActions.Count;

        public void UnsubscribeAll(object target)
        {
            if (target == null) return;

            lock (_lockObject)
            {
                if (!_targetToHandlers.TryGetValue(target, out var handlerSet)) return;

                var handlersToRemove = new List<HandlerInfo>(handlerSet);
                foreach (var handlerInfo in handlersToRemove)
                {
                    if (_handlers.TryGetValue(handlerInfo.EventType, out var handlerList))
                    {
                        handlerList.Remove(handlerInfo.Handler);
                        if (handlerList.Count == 0)
                        {
                            _handlers.TryRemove(handlerInfo.EventType, out _);
                        }
                    }

                    _handlerExceptionCounts.Remove(handlerInfo.Handler);
                    _handlerPriorityCache.Remove(handlerInfo.Handler);
                }

                _targetToHandlers.Remove(target);
            }
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _handlers.Clear();
                _targetToHandlers.Clear();
                _handlerExceptionCounts.Clear();
                _handlerPriorityCache.Clear();
            }

            _deferredActions.Clear();
        }

        #region 私有辅助方法

        private void InvokeHandlers<TEvent>(List<Delegate> handlers, TEvent eventData, Type eventType)
            where TEvent : IEvent
        {
            // 在锁内快照 handler 列表，避免遍历期间 Subscribe/Unsubscribe 修改集合导致崩溃
            Delegate[] snapshot;
            lock (_lockObject)
            {
                if (handlers.Count == 0) return;
                snapshot = handlers.ToArray();
            }

            if (snapshot.Length == 1)
            {
                SafeInvoke(snapshot[0] as Action<TEvent>, eventData, eventType);
                return;
            }

            var prioritizedHandlers = GetSortedHandlersFromSnapshot<Action<TEvent>>(snapshot);

            if (_config.enableFrameSlicing && prioritizedHandlers.Count > _config.maxHandlersPerFrame)
            {
                for (int i = 0; i < prioritizedHandlers.Count; i++)
                {
                    var (handler, _) = prioritizedHandlers[i];
                    if (i < _config.maxHandlersPerFrame)
                    {
                        SafeInvoke(handler, eventData, eventType);
                    }
                    else
                    {
                        var capturedHandler = handler;
                        var capturedEvent = eventData;
                        var capturedType = eventType;
                        _deferredActions.Enqueue(() => SafeInvoke(capturedHandler, capturedEvent, capturedType));
                    }
                }
            }
            else
            {
                foreach (var (handler, _) in prioritizedHandlers)
                {
                    SafeInvoke(handler, eventData, eventType);
                }
            }
        }

        private List<(THandler Handler, int Priority)> GetSortedHandlersFromSnapshot<THandler>(Delegate[] snapshot) where THandler : Delegate
        {
            List<(THandler Handler, int Priority)> result;
            lock (_lockObject)
            {
                result = new List<(THandler, int)>(snapshot.Length);
                foreach (var handler in snapshot)
                {
                    if (handler is THandler typedHandler)
                    {
                        var priority = _handlerPriorityCache.GetValueOrDefault(handler, 0);
                        result.Add((typedHandler, priority));
                    }
                }
            }

            result.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            return result;
        }

        private void SafeInvoke<TEvent>(Action<TEvent> handler, TEvent eventData, Type eventType)
            where TEvent : IEvent
        {
            if (handler == null) return;
            if (!CheckExceptionThreshold(handler)) return;

#if JULYGF_ENABLE_LOG
            _stopwatch.Restart();
#endif
            try
            {
                handler(eventData);
                ResetExceptionCount(handler);
            }
            catch (Exception ex)
            {
                HandleException(handler, eventType, ex);
            }
            finally
            {
#if JULYGF_ENABLE_LOG
                _stopwatch.Stop();
                if (_stopwatch.ElapsedMilliseconds > _config.handlerTimeoutMs)
                {
                    JLogger.LogWarning(
                        $"{Frameworkconst.TagEventBus} 处理器执行超时: 事件={eventType.Name}, 耗时={_stopwatch.ElapsedMilliseconds}ms");
                }
#endif
            }
        }

        private bool CheckExceptionThreshold(Delegate handler)
        {
            lock (_lockObject)
            {
                return !_handlerExceptionCounts.TryGetValue(handler, out var count) ||
                       count < _config.handlerExceptionThreshold;
            }
        }

        private void ResetExceptionCount(Delegate handler)
        {
            lock (_lockObject)
            {
                _handlerExceptionCounts[handler] = 0;
            }
        }

        private void HandleException(Delegate handler, Type eventType, Exception ex)
        {
            int count;
            lock (_lockObject)
            {
                _handlerExceptionCounts.TryAdd(handler, 0);
                count = ++_handlerExceptionCounts[handler];
            }

            JLogger.LogError(
                $"{Frameworkconst.TagEventBus} 处理事件 {eventType.Name} 时异常 (第 {count} 次): {ex.Message}");

            if (count >= _config.handlerExceptionThreshold)
            {
                JLogger.LogError($"{Frameworkconst.TagEventBus} 处理器连续异常 {_config.handlerExceptionThreshold} 次，已自动禁用");
            }
        }

        #endregion
    }
}