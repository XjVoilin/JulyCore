using System;
using System.Collections.Generic;

namespace JulyCore.Data.Network
{
    /// <summary>
    /// 网络连接状态
    /// </summary>
    public enum NetworkConnectionState
    {
        /// <summary>
        /// 未连接
        /// </summary>
        Disconnected,

        /// <summary>
        /// 连接中
        /// </summary>
        Connecting,

        /// <summary>
        /// 已连接
        /// </summary>
        Connected,

        /// <summary>
        /// 重连中
        /// </summary>
        Reconnecting,

        /// <summary>
        /// 关闭中
        /// </summary>
        Closing,

        /// <summary>
        /// 已关闭
        /// </summary>
        Closed
    }

    /// <summary>
    /// 网络消息类型
    /// </summary>
    public enum NetworkMessageType
    {
        /// <summary>
        /// 文本消息
        /// </summary>
        Text,

        /// <summary>
        /// 二进制消息
        /// </summary>
        Binary,

        /// <summary>
        /// Ping消息
        /// </summary>
        Ping,

        /// <summary>
        /// Pong消息
        /// </summary>
        Pong
    }

    /// <summary>
    /// 断开连接原因
    /// </summary>
    public enum DisconnectReason
    {
        /// <summary>
        /// 正常关闭
        /// </summary>
        Normal,

        /// <summary>
        /// 服务器关闭
        /// </summary>
        ServerClosed,

        /// <summary>
        /// 网络错误
        /// </summary>
        NetworkError,

        /// <summary>
        /// 超时
        /// </summary>
        Timeout,

        /// <summary>
        /// 手动断开
        /// </summary>
        Manual,

        /// <summary>
        /// 重连失败
        /// </summary>
        ReconnectFailed,

        /// <summary>
        /// 协议错误
        /// </summary>
        ProtocolError,

        /// <summary>
        /// 未知原因
        /// </summary>
        Unknown
    }

    /// <summary>
    /// 网络消息（运行时数据）
    /// </summary>
    [Serializable]
    public class NetworkMessage
    {
        /// <summary>
        /// 消息ID（用于请求-响应匹配）
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// 消息类型
        /// </summary>
        public NetworkMessageType Type { get; set; }

        /// <summary>
        /// 协议号/命令ID（业务层定义）
        /// </summary>
        public int ProtocolId { get; set; }

        /// <summary>
        /// 文本数据（当Type为Text时）
        /// </summary>
        public string TextData { get; set; }

        /// <summary>
        /// 二进制数据（当Type为Binary时）
        /// </summary>
        public byte[] BinaryData { get; set; }

        /// <summary>
        /// 发送/接收时间戳
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 自定义扩展数据
        /// </summary>
        public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 创建文本消息
        /// </summary>
        public static NetworkMessage CreateText(string text, int protocolId = 0)
        {
            return new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Type = NetworkMessageType.Text,
                ProtocolId = protocolId,
                TextData = text,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        /// <summary>
        /// 创建二进制消息
        /// </summary>
        public static NetworkMessage CreateBinary(byte[] data, int protocolId = 0)
        {
            return new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Type = NetworkMessageType.Binary,
                ProtocolId = protocolId,
                BinaryData = data,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }

    /// <summary>
    /// 网络连接信息
    /// </summary>
    [Serializable]
    public class NetworkConnectionInfo
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// 服务器地址
        /// </summary>
        public string ServerUrl { get; set; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public NetworkConnectionState State { get; set; }

        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime? ConnectedTime { get; set; }

        /// <summary>
        /// 断开时间
        /// </summary>
        public DateTime? DisconnectedTime { get; set; }

        /// <summary>
        /// 断开原因
        /// </summary>
        public DisconnectReason? DisconnectReason { get; set; }

        /// <summary>
        /// 重连次数
        /// </summary>
        public int ReconnectCount { get; set; }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// 延迟（毫秒）
        /// </summary>
        public long LatencyMs { get; set; }

        /// <summary>
        /// 发送消息计数
        /// </summary>
        public long SentMessageCount { get; set; }

        /// <summary>
        /// 接收消息计数
        /// </summary>
        public long ReceivedMessageCount { get; set; }

        /// <summary>
        /// 发送字节数
        /// </summary>
        public long SentBytes { get; set; }

        /// <summary>
        /// 接收字节数
        /// </summary>
        public long ReceivedBytes { get; set; }
    }

    /// <summary>
    /// 请求-响应匹配信息
    /// </summary>
    public class PendingRequest
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
        /// 发送时间
        /// </summary>
        public DateTime SendTime { get; set; }

        /// <summary>
        /// 超时时间（秒）
        /// </summary>
        public float TimeoutSeconds { get; set; }

        /// <summary>
        /// 响应回调
        /// </summary>
        public Action<NetworkMessage> Callback { get; set; }

        /// <summary>
        /// 超时回调
        /// </summary>
        public Action TimeoutCallback { get; set; }

        /// <summary>
        /// 是否已超时
        /// </summary>
        public bool IsTimeout => (DateTime.UtcNow - SendTime).TotalSeconds > TimeoutSeconds;
    }

    /// <summary>
    /// 网络统计信息
    /// </summary>
    [Serializable]
    public class NetworkStatistics
    {
        /// <summary>
        /// 总发送消息数
        /// </summary>
        public long TotalSentMessages { get; set; }

        /// <summary>
        /// 总接收消息数
        /// </summary>
        public long TotalReceivedMessages { get; set; }

        /// <summary>
        /// 总发送字节数
        /// </summary>
        public long TotalSentBytes { get; set; }

        /// <summary>
        /// 总接收字节数
        /// </summary>
        public long TotalReceivedBytes { get; set; }

        /// <summary>
        /// 总重连次数
        /// </summary>
        public int TotalReconnectCount { get; set; }

        /// <summary>
        /// 总连接次数
        /// </summary>
        public int TotalConnectCount { get; set; }

        /// <summary>
        /// 平均延迟（毫秒）
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// 最大延迟（毫秒）
        /// </summary>
        public long MaxLatencyMs { get; set; }

        /// <summary>
        /// 最小延迟（毫秒）
        /// </summary>
        public long MinLatencyMs { get; set; } = long.MaxValue;

        /// <summary>
        /// HTTP请求总数
        /// </summary>
        public long TotalHttpRequests { get; set; }

        /// <summary>
        /// HTTP请求成功数
        /// </summary>
        public long HttpSuccessCount { get; set; }

        /// <summary>
        /// HTTP请求失败数
        /// </summary>
        public long HttpFailureCount { get; set; }

        /// <summary>
        /// 重置统计
        /// </summary>
        public void Reset()
        {
            TotalSentMessages = 0;
            TotalReceivedMessages = 0;
            TotalSentBytes = 0;
            TotalReceivedBytes = 0;
            TotalReconnectCount = 0;
            TotalConnectCount = 0;
            AverageLatencyMs = 0;
            MaxLatencyMs = 0;
            MinLatencyMs = long.MaxValue;
            TotalHttpRequests = 0;
            HttpSuccessCount = 0;
            HttpFailureCount = 0;
        }
    }
}

