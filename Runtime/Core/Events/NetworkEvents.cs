using JulyCore.Data.Network;

namespace JulyCore.Core.Events
{
    /// <summary>
    /// WebSocket连接状态变更事件
    /// </summary>
    public class WebSocketStateChangedEvent : IEvent
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// 之前的状态
        /// </summary>
        public NetworkConnectionState PreviousState { get; set; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public NetworkConnectionState CurrentState { get; set; }

        /// <summary>
        /// 断开原因（当状态为Disconnected时）
        /// </summary>
        public DisconnectReason? DisconnectReason { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// WebSocket连接成功事件
    /// </summary>
    public class WebSocketConnectedEvent : IEvent
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// 服务器地址
        /// </summary>
        public string ServerUrl { get; set; }

        /// <summary>
        /// 是否为重连
        /// </summary>
        public bool IsReconnect { get; set; }

        /// <summary>
        /// 重连次数（如果是重连）
        /// </summary>
        public int ReconnectCount { get; set; }
    }

    /// <summary>
    /// WebSocket断开连接事件
    /// </summary>
    public class WebSocketDisconnectedEvent : IEvent
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// 断开原因
        /// </summary>
        public DisconnectReason Reason { get; set; }

        /// <summary>
        /// 关闭代码（WebSocket关闭代码）
        /// </summary>
        public int CloseCode { get; set; }

        /// <summary>
        /// 关闭原因描述
        /// </summary>
        public string CloseReason { get; set; }

        /// <summary>
        /// 是否会尝试重连
        /// </summary>
        public bool WillReconnect { get; set; }
    }

    /// <summary>
    /// WebSocket接收消息事件
    /// </summary>
    public class WebSocketMessageReceivedEvent : IEvent
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// 消息数据
        /// </summary>
        public NetworkMessage Message { get; set; }
    }

    /// <summary>
    /// WebSocket发送消息事件
    /// </summary>
    public class WebSocketMessageSentEvent : IEvent
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// 消息数据
        /// </summary>
        public NetworkMessage Message { get; set; }

        /// <summary>
        /// 是否发送成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息（发送失败时）
        /// </summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// WebSocket重连事件
    /// </summary>
    public class WebSocketReconnectingEvent : IEvent
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// 当前重连次数
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// 最大重连次数
        /// </summary>
        public int MaxAttempts { get; set; }

        /// <summary>
        /// 下次重连间隔（秒）
        /// </summary>
        public float NextRetryIntervalSeconds { get; set; }
    }

    /// <summary>
    /// WebSocket错误事件
    /// </summary>
    public class WebSocketErrorEvent : IEvent
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 异常对象
        /// </summary>
        public System.Exception Exception { get; set; }
    }

    /// <summary>
    /// 网络延迟更新事件
    /// </summary>
    public class NetworkLatencyUpdatedEvent : IEvent
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// 当前延迟（毫秒）
        /// </summary>
        public long LatencyMs { get; set; }

        /// <summary>
        /// 平均延迟（毫秒）
        /// </summary>
        public double AverageLatencyMs { get; set; }
    }

    /// <summary>
    /// HTTP请求完成事件
    /// </summary>
    public class HttpRequestCompletedEvent : IEvent
    {
        /// <summary>
        /// 请求URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 请求方法（GET/POST等）
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 响应数据
        /// </summary>
        public HttpResponse Response { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; set; }
    }

    /// <summary>
    /// 请求超时事件
    /// </summary>
    public class NetworkRequestTimeoutEvent : IEvent
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// 协议号
        /// </summary>
        public int ProtocolId { get; set; }

        /// <summary>
        /// 超时时间（秒）
        /// </summary>
        public float TimeoutSeconds { get; set; }
    }

    /// <summary>
    /// 网络可达性变化事件
    /// </summary>
    public class NetworkReachabilityChangedEvent : IEvent
    {
        /// <summary>
        /// 是否可达
        /// </summary>
        public bool IsReachable { get; set; }

        /// <summary>
        /// 网络类型（WiFi/移动数据等）
        /// </summary>
        public string NetworkType { get; set; }
    }
}

