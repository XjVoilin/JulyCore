using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core.Events;
using JulyCore.Data.Network;
using JulyCore.Module.Network;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 网络相关操作
        /// </summary>
        public static class Network
        {
            private static NetworkModule _module;
            private static NetworkModule Module
            {
                get
                {
                    _module ??= GetModule<NetworkModule>();
                    return _module;
                }
            }
            
            #region 属性

            /// <summary>
            /// 默认连接状态
            /// </summary>
            public static NetworkConnectionState ConnectionState => Module.ConnectionState;

            /// <summary>
            /// 是否已连接
            /// </summary>
            public static bool IsConnected => Module.IsConnected;

            #endregion

            #region 配置

            /// <summary>
            /// 配置网络模块
            /// </summary>
            public static void Configure(NetworkConfig config)
            {
                Module.Configure(config);
            }

            /// <summary>
            /// 使用构建器配置网络模块
            /// </summary>
            public static void Configure(Action<NetworkConfigBuilder> configAction)
            {
                var builder = new NetworkConfigBuilder();
                configAction?.Invoke(builder);
                Configure(builder.Build());
            }

            /// <summary>
            /// 设置消息序列化器
            /// </summary>
            public static void SetSerializer(IMessageSerializer serializer)
            {
                Module.SetSerializer(serializer);
            }

            #endregion

            #region WebSocket 连接

            /// <summary>
            /// 连接WebSocket服务器
            /// </summary>
            public static UniTask<bool> ConnectAsync(string url, CancellationToken cancellationToken = default)
            {
                return Module.ConnectAsync(url, cancellationToken);
            }

            /// <summary>
            /// 使用配置连接WebSocket服务器
            /// </summary>
            public static UniTask<bool> ConnectAsync(WebSocketConfig config, CancellationToken cancellationToken = default)
            {
                return Module.ConnectAsync(config, cancellationToken);
            }

            /// <summary>
            /// 断开连接
            /// </summary>
            public static UniTask DisconnectAsync(string connectionName = null, CancellationToken cancellationToken = default)
            {
                return Module.DisconnectAsync(connectionName, cancellationToken);
            }

            /// <summary>
            /// 断开所有连接
            /// </summary>
            public static UniTask DisconnectAllAsync(CancellationToken cancellationToken = default)
            {
                return Module.DisconnectAllAsync(cancellationToken);
            }

            /// <summary>
            /// 获取连接信息
            /// </summary>
            public static NetworkConnectionInfo GetConnectionInfo(string connectionName = null)
            {
                return Module.GetConnectionInfo(connectionName);
            }

            /// <summary>
            /// 获取连接状态
            /// </summary>
            public static NetworkConnectionState GetConnectionState(string connectionName = null)
            {
                return Module.GetConnectionState(connectionName);
            }

            #endregion

            #region WebSocket 消息发送

            /// <summary>
            /// 发送文本消息
            /// </summary>
            public static bool SendText(string text, string connectionName = null)
            {
                return Module.SendText(text, connectionName);
            }

            /// <summary>
            /// 发送二进制消息
            /// </summary>
            public static bool SendBinary(byte[] data, string connectionName = null)
            {
                return Module.SendBinary(data, connectionName);
            }

            /// <summary>
            /// 发送网络消息
            /// </summary>
            public static bool Send(NetworkMessage message, string connectionName = null)
            {
                return Module.Send(message, connectionName);
            }

            /// <summary>
            /// 发送协议消息
            /// </summary>
            public static bool SendProtocol<T>(int protocolId, T data, string connectionName = null)
            {
                return Module.SendProtocol(protocolId, data, connectionName);
            }

            /// <summary>
            /// 发送请求并等待响应
            /// </summary>
            public static UniTask<NetworkMessage> RequestAsync(NetworkMessage request, float timeoutSeconds = 0, CancellationToken cancellationToken = default)
            {
                return Module.RequestAsync(request, timeoutSeconds, cancellationToken);
            }

            /// <summary>
            /// 发送协议请求并等待响应
            /// </summary>
            public static UniTask<TResponse> RequestAsync<TRequest, TResponse>(int protocolId, TRequest data, float timeoutSeconds = 0, CancellationToken cancellationToken = default)
            {
                return Module.RequestAsync<TRequest, TResponse>(protocolId, data, timeoutSeconds, cancellationToken);
            }

            #endregion

            #region 协议处理

            /// <summary>
            /// 注册协议处理器
            /// </summary>
            public static void RegisterHandler(int protocolId, ProtocolHandler handler)
            {
                Module.RegisterHandler(protocolId, handler);
            }

            /// <summary>
            /// 注销协议处理器
            /// </summary>
            public static void UnregisterHandler(int protocolId, ProtocolHandler handler)
            {
                Module.UnregisterHandler(protocolId, handler);
            }

            /// <summary>
            /// 注销指定协议的所有处理器
            /// </summary>
            public static void UnregisterAllHandlers(int protocolId)
            {
                Module.UnregisterAllHandlers(protocolId);
            }

            #endregion

            #region 统计与日志

            /// <summary>
            /// 获取网络统计信息
            /// </summary>
            public static NetworkStatistics GetStatistics()
            {
                return Module.GetStatistics();
            }

            /// <summary>
            /// 重置统计信息
            /// </summary>
            public static void ResetStatistics()
            {
                Module.ResetStatistics();
            }

            /// <summary>
            /// 获取消息日志
            /// </summary>
            public static List<NetworkMessage> GetMessageLog()
            {
                return Module.GetMessageLog() ?? new List<NetworkMessage>();
            }

            /// <summary>
            /// 清空消息日志
            /// </summary>
            public static void ClearMessageLog()
            {
                Module.ClearMessageLog();
            }

            #endregion

            #region 事件订阅

            /// <summary>
            /// 订阅WebSocket连接状态变更事件
            /// </summary>
            public static void OnStateChanged(Action<WebSocketStateChangedEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅WebSocket连接成功事件
            /// </summary>
            public static void OnConnected(Action<WebSocketConnectedEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅WebSocket断开连接事件
            /// </summary>
            public static void OnDisconnected(Action<WebSocketDisconnectedEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅WebSocket接收消息事件
            /// </summary>
            public static void OnMessageReceived(Action<WebSocketMessageReceivedEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅WebSocket发送消息事件
            /// </summary>
            public static void OnMessageSent(Action<WebSocketMessageSentEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅WebSocket重连事件
            /// </summary>
            public static void OnReconnecting(Action<WebSocketReconnectingEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅WebSocket错误事件
            /// </summary>
            public static void OnError(Action<WebSocketErrorEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅网络延迟更新事件
            /// </summary>
            public static void OnLatencyUpdated(Action<NetworkLatencyUpdatedEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            /// <summary>
            /// 订阅请求超时事件
            /// </summary>
            public static void OnRequestTimeout(Action<NetworkRequestTimeoutEvent> handler, object target)
            {
                _context.EventBus?.Subscribe(handler, target);
            }

            #endregion
        }
    }
}

