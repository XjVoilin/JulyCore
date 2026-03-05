using System;
using System.Collections.Generic;

namespace JulyCore.Data.Network
{
    /// <summary>
    /// WebSocket连接配置
    /// </summary>
    [Serializable]
    public class WebSocketConfig
    {
        /// <summary>
        /// 连接名称（用于多连接场景的标识）
        /// </summary>
        public string Name { get; set; } = "default";

        /// <summary>
        /// 服务器地址（ws:// 或 wss://）
        /// </summary>
        public string ServerUrl { get; set; }

        /// <summary>
        /// 连接超时时间（秒）
        /// </summary>
        public float ConnectTimeoutSeconds { get; set; } = 10f;

        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// 最大重连次数（0表示无限重连）
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 5;

        /// <summary>
        /// 重连间隔（秒）
        /// </summary>
        public float ReconnectIntervalSeconds { get; set; } = 2f;

        /// <summary>
        /// 重连间隔递增因子（指数退避）
        /// </summary>
        public float ReconnectBackoffMultiplier { get; set; } = 1.5f;

        /// <summary>
        /// 最大重连间隔（秒）
        /// </summary>
        public float MaxReconnectIntervalSeconds { get; set; } = 30f;

        /// <summary>
        /// 是否启用心跳
        /// </summary>
        public bool EnableHeartbeat { get; set; } = true;

        /// <summary>
        /// 心跳间隔（秒）
        /// </summary>
        public float HeartbeatIntervalSeconds { get; set; } = 30f;

        /// <summary>
        /// 心跳超时时间（秒）
        /// </summary>
        public float HeartbeatTimeoutSeconds { get; set; } = 10f;

        /// <summary>
        /// 是否启用消息队列（断线期间缓存消息）
        /// </summary>
        public bool EnableMessageQueue { get; set; } = true;

        /// <summary>
        /// 消息队列最大容量
        /// </summary>
        public int MessageQueueCapacity { get; set; } = 100;

        /// <summary>
        /// 自定义请求头
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 子协议列表
        /// </summary>
        public List<string> SubProtocols { get; set; } = new List<string>();

        /// <summary>
        /// 发送缓冲区大小
        /// </summary>
        public int SendBufferSize { get; set; } = 16384;

        /// <summary>
        /// 接收缓冲区大小
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 16384;
    }

    /// <summary>
    /// HTTP配置
    /// </summary>
    [Serializable]
    public class HttpConfig
    {
        /// <summary>
        /// 默认超时时间（秒）
        /// </summary>
        public float TimeoutSeconds { get; set; } = 30f;

        /// <summary>
        /// 默认请求头
        /// </summary>
        public Dictionary<string, string> DefaultHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 是否启用重试
        /// </summary>
        public bool EnableRetry { get; set; } = true;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 重试间隔（秒）
        /// </summary>
        public float RetryIntervalSeconds { get; set; } = 1f;

        /// <summary>
        /// 需要重试的HTTP状态码
        /// </summary>
        public List<int> RetryStatusCodes { get; set; } = new List<int> { 408, 429, 500, 502, 503, 504 };

        /// <summary>
        /// 基础URL（用于拼接相对路径）
        /// </summary>
        public string BaseUrl { get; set; }
    }

    /// <summary>
    /// 网络总配置
    /// </summary>
    [Serializable]
    public class NetworkConfig
    {
        /// <summary>
        /// 默认WebSocket配置
        /// </summary>
        public WebSocketConfig DefaultWebSocket { get; set; } = new WebSocketConfig();

        /// <summary>
        /// 多连接WebSocket配置
        /// </summary>
        public List<WebSocketConfig> WebSocketConfigs { get; set; } = new List<WebSocketConfig>();

        /// <summary>
        /// HTTP配置
        /// </summary>
        public HttpConfig Http { get; set; } = new HttpConfig();

        /// <summary>
        /// 是否启用网络统计
        /// </summary>
        public bool EnableStatistics { get; set; } = true;

        /// <summary>
        /// 是否启用消息日志
        /// </summary>
        public bool EnableMessageLog { get; set; }

        /// <summary>
        /// 消息日志最大条数
        /// </summary>
        public int MessageLogCapacity { get; set; } = 100;

        /// <summary>
        /// 请求超时时间（秒，用于请求-响应模式）
        /// </summary>
        public float RequestTimeoutSeconds { get; set; } = 15f;

        /// <summary>
        /// 获取指定名称的WebSocket配置
        /// </summary>
        public WebSocketConfig GetWebSocketConfig(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "default")
                return DefaultWebSocket;

            return WebSocketConfigs?.Find(c => c.Name == name) ?? DefaultWebSocket;
        }
    }

    /// <summary>
    /// 网络配置构建器
    /// </summary>
    public class NetworkConfigBuilder
    {
        private readonly NetworkConfig _config = new NetworkConfig();

        /// <summary>
        /// 设置默认WebSocket服务器地址
        /// </summary>
        public NetworkConfigBuilder WithWebSocketServer(string url)
        {
            _config.DefaultWebSocket.ServerUrl = url;
            return this;
        }

        /// <summary>
        /// 设置自动重连
        /// </summary>
        public NetworkConfigBuilder WithAutoReconnect(bool enable, int maxAttempts = 5, float intervalSeconds = 2f)
        {
            _config.DefaultWebSocket.AutoReconnect = enable;
            _config.DefaultWebSocket.MaxReconnectAttempts = maxAttempts;
            _config.DefaultWebSocket.ReconnectIntervalSeconds = intervalSeconds;
            return this;
        }

        /// <summary>
        /// 设置心跳
        /// </summary>
        public NetworkConfigBuilder WithHeartbeat(bool enable, float intervalSeconds = 30f, float timeoutSeconds = 10f)
        {
            _config.DefaultWebSocket.EnableHeartbeat = enable;
            _config.DefaultWebSocket.HeartbeatIntervalSeconds = intervalSeconds;
            _config.DefaultWebSocket.HeartbeatTimeoutSeconds = timeoutSeconds;
            return this;
        }

        /// <summary>
        /// 设置HTTP基础URL
        /// </summary>
        public NetworkConfigBuilder WithHttpBaseUrl(string baseUrl)
        {
            _config.Http.BaseUrl = baseUrl;
            return this;
        }

        /// <summary>
        /// 设置HTTP超时
        /// </summary>
        public NetworkConfigBuilder WithHttpTimeout(float timeoutSeconds)
        {
            _config.Http.TimeoutSeconds = timeoutSeconds;
            return this;
        }

        /// <summary>
        /// 设置HTTP重试
        /// </summary>
        public NetworkConfigBuilder WithHttpRetry(bool enable, int maxRetryCount = 3)
        {
            _config.Http.EnableRetry = enable;
            _config.Http.MaxRetryCount = maxRetryCount;
            return this;
        }

        /// <summary>
        /// 添加默认HTTP请求头
        /// </summary>
        public NetworkConfigBuilder WithHttpHeader(string key, string value)
        {
            _config.Http.DefaultHeaders[key] = value;
            return this;
        }

        /// <summary>
        /// 添加额外的WebSocket连接配置
        /// </summary>
        public NetworkConfigBuilder AddWebSocketConfig(WebSocketConfig config)
        {
            _config.WebSocketConfigs.Add(config);
            return this;
        }

        /// <summary>
        /// 启用统计
        /// </summary>
        public NetworkConfigBuilder WithStatistics(bool enable)
        {
            _config.EnableStatistics = enable;
            return this;
        }

        /// <summary>
        /// 启用消息日志
        /// </summary>
        public NetworkConfigBuilder WithMessageLog(bool enable, int capacity = 100)
        {
            _config.EnableMessageLog = enable;
            _config.MessageLogCapacity = capacity;
            return this;
        }

        /// <summary>
        /// 构建配置
        /// </summary>
        public NetworkConfig Build()
        {
            return _config;
        }
    }
}

