using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Events;
using JulyCore.Data.Network;
using JulyCore.Module.Base;
using JulyCore.Provider.Network;

namespace JulyCore.Module.Network
{
    /// <summary>
    /// 协议处理器委托
    /// </summary>
    /// <param name="message">收到的消息</param>
    public delegate void ProtocolHandler(NetworkMessage message);

    /// <summary>
    /// 消息序列化器接口
    /// </summary>
    public interface IMessageSerializer
    {
        /// <summary>
        /// 序列化消息
        /// </summary>
        byte[] Serialize<T>(int protocolId, T data);

        /// <summary>
        /// 反序列化消息
        /// </summary>
        (int protocolId, T data) Deserialize<T>(byte[] data);

        /// <summary>
        /// 从原始数据解析协议号
        /// </summary>
        int ParseProtocolId(byte[] data);
    }

    /// <summary>
    /// 网络模块
    /// 
    /// 【职责】
    /// - 业务逻辑层：协议分发、请求-响应管理、心跳管理、消息队列
    /// - 状态变化通知：通过 EventBus 发布网络状态事件
    /// 
    /// 【通信模式】
    /// - 调用 Provider：执行技术操作（连接、发送、断开等）
    /// - 发布 Event：通知外部网络状态变化（供其他模块或业务层订阅）
    /// - Provider 回调 → Module 事件：将底层事件转换为业务事件
    /// </summary>
    public class NetworkModule : ModuleBase
    {
        private INetworkProvider _networkProvider;
        private NetworkConfig _config;
        private IMessageSerializer _serializer;

        protected override LogChannel LogChannel => LogChannel.Network;

        // 协议处理器
        private readonly Dictionary<int, List<ProtocolHandler>> _protocolHandlers = new Dictionary<int, List<ProtocolHandler>>();
        private readonly object _handlerLock = new object();

        // 请求-响应管理
        private readonly Dictionary<string, PendingRequest> _pendingRequests = new Dictionary<string, PendingRequest>();
        private readonly object _requestLock = new object();

        // 心跳管理
        private readonly Dictionary<string, CancellationTokenSource> _heartbeatCts = new Dictionary<string, CancellationTokenSource>();

        // 消息日志
        private readonly Queue<NetworkMessage> _messageLog = new Queue<NetworkMessage>();
        private readonly object _logLock = new object();

        /// <summary>
        /// 模块执行优先级
        /// </summary>
        public override int Priority => Frameworkconst.PriorityNetworkModule;

        /// <summary>
        /// 默认连接状态
        /// </summary>
        public NetworkConnectionState ConnectionState => _networkProvider?.ConnectionState ?? NetworkConnectionState.Disconnected;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _networkProvider?.IsConnected ?? false;

        #region 生命周期

        protected override UniTask OnInitAsync()
        {
            try
            {
                _networkProvider = GetProvider<INetworkProvider>();

                if (_networkProvider == null)
                {
                    LogError($"[{Name}] 获取网络提供者失败");
                    throw new InvalidOperationException("网络提供者未注册");
                }

                // 订阅Provider事件
                _networkProvider.OnOpen += OnWebSocketOpen;
                _networkProvider.OnClose += OnWebSocketClose;
                _networkProvider.OnTextMessage += OnWebSocketTextMessage;
                _networkProvider.OnBinaryMessage += OnWebSocketBinaryMessage;
                _networkProvider.OnError += OnWebSocketError;

                _config = new NetworkConfig();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 初始化失败: {ex.Message}");
                throw;
            }
            return UniTask.CompletedTask;
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            // 检查超时请求
            CheckTimeoutRequests();
        }

        protected override async UniTask OnShutdownAsync()
        {
            // 停止所有心跳
            StopAllHeartbeats();

            // 取消订阅事件
            if (_networkProvider != null)
            {
                _networkProvider.OnOpen -= OnWebSocketOpen;
                _networkProvider.OnClose -= OnWebSocketClose;
                _networkProvider.OnTextMessage -= OnWebSocketTextMessage;
                _networkProvider.OnBinaryMessage -= OnWebSocketBinaryMessage;
                _networkProvider.OnError -= OnWebSocketError;

                await _networkProvider.DisconnectAllAsync();
            }

            // 清理
            lock (_handlerLock)
            {
                _protocolHandlers.Clear();
            }

            lock (_requestLock)
            {
                _pendingRequests.Clear();
            }

            lock (_logLock)
            {
                _messageLog.Clear();
            }

            _networkProvider = null;
        }

        #endregion

        #region 配置

        /// <summary>
        /// 配置网络模块
        /// </summary>
        public void Configure(NetworkConfig config)
        {
            _config = config ?? new NetworkConfig();
            _networkProvider?.ConfigureHttp(_config.Http);
        }

        /// <summary>
        /// 设置消息序列化器
        /// </summary>
        public void SetSerializer(IMessageSerializer serializer)
        {
            _serializer = serializer;
        }

        #endregion

        #region WebSocket 连接管理

        /// <summary>
        /// 连接WebSocket服务器
        /// </summary>
        public async UniTask<bool> ConnectAsync(string url, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null)
            {
                LogError($"[{Name}] 网络提供者未初始化");
                return false;
            }

            return await _networkProvider.ConnectAsync(url, cancellationToken);
        }

        /// <summary>
        /// 使用配置连接WebSocket服务器
        /// </summary>
        public async UniTask<bool> ConnectAsync(WebSocketConfig config, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null)
            {
                LogError($"[{Name}] 网络提供者未初始化");
                return false;
            }

            var result = await _networkProvider.ConnectAsync(config, cancellationToken);

            if (result && config.EnableHeartbeat)
            {
                StartHeartbeat(config.Name ?? "default", config);
            }

            return result;
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async UniTask DisconnectAsync(string connectionName = null, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null) return;

            StopHeartbeat(connectionName ?? "default");

            if (string.IsNullOrEmpty(connectionName))
            {
                await _networkProvider.DisconnectAsync(cancellationToken);
            }
            else
            {
                await _networkProvider.DisconnectAsync(connectionName, cancellationToken);
            }
        }

        /// <summary>
        /// 断开所有连接
        /// </summary>
        public async UniTask DisconnectAllAsync(CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null) return;

            StopAllHeartbeats();
            await _networkProvider.DisconnectAllAsync(cancellationToken);
        }

        /// <summary>
        /// 获取连接信息
        /// </summary>
        public NetworkConnectionInfo GetConnectionInfo(string connectionName = null)
        {
            return _networkProvider?.GetConnectionInfo(connectionName);
        }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public NetworkConnectionState GetConnectionState(string connectionName = null)
        {
            return _networkProvider?.GetConnectionState(connectionName) ?? NetworkConnectionState.Disconnected;
        }

        #endregion

        #region 消息发送

        /// <summary>
        /// 发送文本消息
        /// </summary>
        public bool SendText(string text, string connectionName = null)
        {
            if (_networkProvider == null) return false;

            if (string.IsNullOrEmpty(connectionName))
            {
                return _networkProvider.SendText(text);
            }
            return _networkProvider.SendText(connectionName, text);
        }

        /// <summary>
        /// 发送二进制消息
        /// </summary>
        public bool SendBinary(byte[] data, string connectionName = null)
        {
            if (_networkProvider == null) return false;

            if (string.IsNullOrEmpty(connectionName))
            {
                return _networkProvider.SendBinary(data);
            }
            return _networkProvider.SendBinary(connectionName, data);
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        public bool Send(NetworkMessage message, string connectionName = null)
        {
            if (_networkProvider == null) return false;

            bool result;
            if (string.IsNullOrEmpty(connectionName))
            {
                result = _networkProvider.Send(message);
            }
            else
            {
                result = _networkProvider.Send(connectionName, message);
            }

            // 通知外部：消息已发送
            EventBus?.Publish(new WebSocketMessageSentEvent
            {
                ConnectionName = connectionName ?? "default",
                Message = message,
                Success = result
            });

            // 记录消息日志
            if (_config.EnableMessageLog)
            {
                LogMessage(message);
            }

            return result;
        }

        /// <summary>
        /// 发送协议消息（使用序列化器）
        /// </summary>
        public bool SendProtocol<T>(int protocolId, T data, string connectionName = null)
        {
            if (_serializer == null)
            {
                LogError($"[{Name}] 未设置消息序列化器");
                return false;
            }

            var binaryData = _serializer.Serialize(protocolId, data);
            var message = NetworkMessage.CreateBinary(binaryData, protocolId);
            return Send(message, connectionName);
        }

        /// <summary>
        /// 发送请求并等待响应
        /// </summary>
        public async UniTask<NetworkMessage> RequestAsync(NetworkMessage request, float timeoutSeconds = 0, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null)
            {
                throw new InvalidOperationException("网络提供者未初始化");
            }

            var timeout = timeoutSeconds > 0 ? timeoutSeconds : _config.RequestTimeoutSeconds;

            var tcs = new UniTaskCompletionSource<NetworkMessage>();

            var pendingRequest = new PendingRequest
            {
                MessageId = request.MessageId,
                ProtocolId = request.ProtocolId,
                SendTime = DateTime.UtcNow,
                TimeoutSeconds = timeout,
                Callback = response => tcs.TrySetResult(response),
                TimeoutCallback = () =>
                {
                    tcs.TrySetException(new TimeoutException($"请求超时: {request.MessageId}"));
                    EventBus?.Publish(new NetworkRequestTimeoutEvent
                    {
                        MessageId = request.MessageId,
                        ProtocolId = request.ProtocolId,
                        TimeoutSeconds = timeout
                    });
                }
            };

            lock (_requestLock)
            {
                _pendingRequests[request.MessageId] = pendingRequest;
            }

            try
            {
                if (!Send(request))
                {
                    throw new Exception("消息发送失败");
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeout));

                return await tcs.Task.AttachExternalCancellation(cts.Token);
            }
            finally
            {
                lock (_requestLock)
                {
                    _pendingRequests.Remove(request.MessageId);
                }
            }
        }

        /// <summary>
        /// 发送协议请求并等待响应
        /// </summary>
        public async UniTask<TResponse> RequestAsync<TRequest, TResponse>(int protocolId, TRequest data, float timeoutSeconds = 0, CancellationToken cancellationToken = default)
        {
            if (_serializer == null)
            {
                throw new InvalidOperationException("未设置消息序列化器");
            }

            var binaryData = _serializer.Serialize(protocolId, data);
            var request = NetworkMessage.CreateBinary(binaryData, protocolId);

            var response = await RequestAsync(request, timeoutSeconds, cancellationToken);

            var (_, responseData) = _serializer.Deserialize<TResponse>(response.BinaryData);
            return responseData;
        }

        #endregion

        #region 协议处理

        /// <summary>
        /// 注册协议处理器
        /// </summary>
        public void RegisterHandler(int protocolId, ProtocolHandler handler)
        {
            if (handler == null) return;

            lock (_handlerLock)
            {
                if (!_protocolHandlers.TryGetValue(protocolId, out var handlers))
                {
                    handlers = new List<ProtocolHandler>();
                    _protocolHandlers[protocolId] = handlers;
                }

                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                }
            }
        }

        /// <summary>
        /// 注销协议处理器
        /// </summary>
        public void UnregisterHandler(int protocolId, ProtocolHandler handler)
        {
            if (handler == null) return;

            lock (_handlerLock)
            {
                if (_protocolHandlers.TryGetValue(protocolId, out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        /// <summary>
        /// 注销指定协议的所有处理器
        /// </summary>
        public void UnregisterAllHandlers(int protocolId)
        {
            lock (_handlerLock)
            {
                _protocolHandlers.Remove(protocolId);
            }
        }

        /// <summary>
        /// 分发消息到处理器
        /// </summary>
        private void DispatchMessage(NetworkMessage message)
        {
            // 检查是否是请求的响应
            lock (_requestLock)
            {
                if (_pendingRequests.TryGetValue(message.MessageId, out var pending))
                {
                    _pendingRequests.Remove(message.MessageId);
                    pending.Callback?.Invoke(message);
                    return;
                }
            }

            // 分发到协议处理器
            List<ProtocolHandler> handlers = null;
            lock (_handlerLock)
            {
                if (_protocolHandlers.TryGetValue(message.ProtocolId, out var list))
                {
                    handlers = new List<ProtocolHandler>(list);
                }
            }

            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler.Invoke(message);
                    }
                    catch (Exception ex)
                    {
                        LogError($"[{Name}] 协议处理器异常: {ex.Message}");
                    }
                }
            }
        }

        private void CheckTimeoutRequests()
        {
            List<PendingRequest> timeoutRequests = null;

            lock (_requestLock)
            {
                foreach (var kvp in _pendingRequests)
                {
                    if (kvp.Value.IsTimeout)
                    {
                        timeoutRequests ??= new List<PendingRequest>();
                        timeoutRequests.Add(kvp.Value);
                    }
                }

                if (timeoutRequests != null)
                {
                    foreach (var request in timeoutRequests)
                    {
                        _pendingRequests.Remove(request.MessageId);
                    }
                }
            }

            // 执行超时回调
            if (timeoutRequests != null)
            {
                foreach (var request in timeoutRequests)
                {
                    request.TimeoutCallback?.Invoke();
                }
            }
        }

        #endregion

        #region 心跳管理

        private void StartHeartbeat(string connectionName, WebSocketConfig config)
        {
            StopHeartbeat(connectionName);

            var cts = new CancellationTokenSource();
            _heartbeatCts[connectionName] = cts;

            HeartbeatLoopAsync(connectionName, config.HeartbeatIntervalSeconds, cts.Token).Forget();
        }

        private void StopHeartbeat(string connectionName)
        {
            if (_heartbeatCts.TryGetValue(connectionName, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _heartbeatCts.Remove(connectionName);
            }
        }

        private void StopAllHeartbeats()
        {
            foreach (var cts in _heartbeatCts.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _heartbeatCts.Clear();
        }

        private async UniTaskVoid HeartbeatLoopAsync(string connectionName, float intervalSeconds, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested) break;

                if (_networkProvider?.IsConnectionConnected(connectionName) == true)
                {
                    // 发送Ping消息
                    var pingTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var pingMessage = new NetworkMessage
                    {
                        MessageId = Guid.NewGuid().ToString("N"),
                        Type = NetworkMessageType.Ping,
                        Timestamp = pingTime,
                        TextData = pingTime.ToString()
                    };

                    Send(pingMessage, connectionName);
                }
            }
        }

        #endregion

        #region HTTP请求

        /// <summary>
        /// 发送HTTP GET请求
        /// </summary>
        public async UniTask<HttpResponse> GetAsync(string url, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null)
            {
                throw new InvalidOperationException("网络提供者未初始化");
            }

            var response = await _networkProvider.GetAsync(url, headers, cancellationToken);
            PublishHttpEvent("GET", url, response);
            return response;
        }

        /// <summary>
        /// 发送HTTP POST请求
        /// </summary>
        public async UniTask<HttpResponse> PostAsync(string url, byte[] data, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null)
            {
                throw new InvalidOperationException("网络提供者未初始化");
            }

            var response = await _networkProvider.PostAsync(url, data, headers, cancellationToken);
            PublishHttpEvent("POST", url, response);
            return response;
        }

        /// <summary>
        /// 发送HTTP POST请求（JSON格式）
        /// </summary>
        public async UniTask<HttpResponse> PostJsonAsync(string url, string jsonData, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null)
            {
                throw new InvalidOperationException("网络提供者未初始化");
            }

            var response = await _networkProvider.PostJsonAsync(url, jsonData, headers, cancellationToken);
            PublishHttpEvent("POST", url, response);
            return response;
        }

        /// <summary>
        /// 发送HTTP PUT请求
        /// </summary>
        public async UniTask<HttpResponse> PutAsync(string url, byte[] data, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null)
            {
                throw new InvalidOperationException("网络提供者未初始化");
            }

            var response = await _networkProvider.PutAsync(url, data, headers, cancellationToken);
            PublishHttpEvent("PUT", url, response);
            return response;
        }

        /// <summary>
        /// 发送HTTP DELETE请求
        /// </summary>
        public async UniTask<HttpResponse> DeleteAsync(string url, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        {
            if (_networkProvider == null)
            {
                throw new InvalidOperationException("网络提供者未初始化");
            }

            var response = await _networkProvider.DeleteAsync(url, headers, cancellationToken);
            PublishHttpEvent("DELETE", url, response);
            return response;
        }

        private void PublishHttpEvent(string method, string url, HttpResponse response)
        {
            EventBus?.Publish(new HttpRequestCompletedEvent
            {
                Method = method,
                Url = url,
                Response = response,
                IsSuccess = response.IsSuccess
            });
        }

        #endregion

        #region 消息日志

        private void LogMessage(NetworkMessage message)
        {
            lock (_logLock)
            {
                _messageLog.Enqueue(message);
                while (_messageLog.Count > _config.MessageLogCapacity)
                {
                    _messageLog.Dequeue();
                }
            }
        }

        /// <summary>
        /// 获取消息日志
        /// </summary>
        public List<NetworkMessage> GetMessageLog()
        {
            lock (_logLock)
            {
                return new List<NetworkMessage>(_messageLog);
            }
        }

        /// <summary>
        /// 清空消息日志
        /// </summary>
        public void ClearMessageLog()
        {
            lock (_logLock)
            {
                _messageLog.Clear();
            }
        }

        #endregion

        #region 统计

        /// <summary>
        /// 获取网络统计信息
        /// </summary>
        public NetworkStatistics GetStatistics()
        {
            return _networkProvider?.GetStatistics();
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStatistics()
        {
            _networkProvider?.ResetStatistics();
        }

        #endregion

        #region Provider事件处理 → 业务事件转换

        /// <summary>
        /// 将 Provider 的底层连接事件转换为业务层事件
        /// </summary>
        private void OnWebSocketOpen(string connectionName)
        {
            var info = _networkProvider?.GetConnectionInfo(connectionName);
            EventBus?.Publish(new WebSocketConnectedEvent
            {
                ConnectionName = connectionName,
                ServerUrl = info?.ServerUrl,
                IsReconnect = info?.ReconnectCount > 0,
                ReconnectCount = info?.ReconnectCount ?? 0
            });

            EventBus?.Publish(new WebSocketStateChangedEvent
            {
                ConnectionName = connectionName,
                PreviousState = NetworkConnectionState.Connecting,
                CurrentState = NetworkConnectionState.Connected
            });
        }

        private void OnWebSocketClose(string connectionName, int code, string reason)
        {
            var disconnectReason = code == 1000 ? DisconnectReason.Normal :
                                   code == 1001 ? DisconnectReason.ServerClosed :
                                   DisconnectReason.Unknown;

            EventBus?.Publish(new WebSocketDisconnectedEvent
            {
                ConnectionName = connectionName,
                Reason = disconnectReason,
                CloseCode = code,
                CloseReason = reason
            });

            EventBus?.Publish(new WebSocketStateChangedEvent
            {
                ConnectionName = connectionName,
                PreviousState = NetworkConnectionState.Connected,
                CurrentState = NetworkConnectionState.Disconnected,
                DisconnectReason = disconnectReason
            });
        }

        private void OnWebSocketTextMessage(string connectionName, string text)
        {
            var message = NetworkMessage.CreateText(text);
            ProcessReceivedMessage(connectionName, message);
        }

        private void OnWebSocketBinaryMessage(string connectionName, byte[] data)
        {
            var message = NetworkMessage.CreateBinary(data);

            // 如果有序列化器，解析协议号
            if (_serializer != null)
            {
                message.ProtocolId = _serializer.ParseProtocolId(data);
            }

            ProcessReceivedMessage(connectionName, message);
        }

        private void ProcessReceivedMessage(string connectionName, NetworkMessage message)
        {
            // 发布消息接收事件
            EventBus?.Publish(new WebSocketMessageReceivedEvent
            {
                ConnectionName = connectionName,
                Message = message
            });

            // 记录消息日志
            if (_config.EnableMessageLog)
            {
                LogMessage(message);
            }

            // 分发消息
            DispatchMessage(message);
        }

        private void OnWebSocketError(string connectionName, string error)
        {
            LogError($"[{Name}] WebSocket错误: {connectionName} - {error}");

            EventBus?.Publish(new WebSocketErrorEvent
            {
                ConnectionName = connectionName,
                ErrorMessage = error
            });
        }

        #endregion
    }
}
