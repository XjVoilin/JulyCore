using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Data.Network;

namespace JulyCore.Provider.Network
{
    /// <summary>
    /// 网络提供者接口
    /// 提供WebSocket和HTTP网络通信的技术能力
    /// </summary>
    public interface INetworkProvider : Core.IProvider
    {
        #region WebSocket 连接管理

        /// <summary>
        /// 默认连接的当前状态
        /// </summary>
        NetworkConnectionState ConnectionState { get; }

        /// <summary>
        /// 默认连接是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 获取指定连接的状态
        /// </summary>
        NetworkConnectionState GetConnectionState(string connectionName);

        /// <summary>
        /// 检查指定连接是否已连接
        /// </summary>
        bool IsConnectionConnected(string connectionName);

        /// <summary>
        /// 连接WebSocket服务器（默认连接）
        /// </summary>
        UniTask<bool> ConnectAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// 连接WebSocket服务器（使用配置）
        /// </summary>
        UniTask<bool> ConnectAsync(WebSocketConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// 断开WebSocket连接（默认连接）
        /// </summary>
        UniTask DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 断开指定的WebSocket连接
        /// </summary>
        UniTask DisconnectAsync(string connectionName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 断开所有WebSocket连接
        /// </summary>
        UniTask DisconnectAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取连接信息
        /// </summary>
        NetworkConnectionInfo GetConnectionInfo(string connectionName = null);

        /// <summary>
        /// 获取所有活跃的连接名称
        /// </summary>
        IReadOnlyList<string> GetActiveConnectionNames();

        #endregion

        #region WebSocket 消息发送

        /// <summary>
        /// 发送文本消息（默认连接）
        /// </summary>
        bool SendText(string text);

        /// <summary>
        /// 发送文本消息到指定连接
        /// </summary>
        bool SendText(string connectionName, string text);

        /// <summary>
        /// 发送二进制消息（默认连接）
        /// </summary>
        bool SendBinary(byte[] data);

        /// <summary>
        /// 发送二进制消息到指定连接
        /// </summary>
        bool SendBinary(string connectionName, byte[] data);

        /// <summary>
        /// 发送网络消息（默认连接）
        /// </summary>
        bool Send(NetworkMessage message);

        /// <summary>
        /// 发送网络消息到指定连接
        /// </summary>
        bool Send(string connectionName, NetworkMessage message);

        /// <summary>
        /// 异步发送并等待消息入队
        /// </summary>
        UniTask<bool> SendAsync(NetworkMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步发送到指定连接并等待消息入队
        /// </summary>
        UniTask<bool> SendAsync(string connectionName, NetworkMessage message, CancellationToken cancellationToken = default);

        #endregion

        #region WebSocket 事件回调

        /// <summary>
        /// 连接打开回调
        /// </summary>
        event Action<string> OnOpen;

        /// <summary>
        /// 连接关闭回调
        /// </summary>
        event Action<string, int, string> OnClose;

        /// <summary>
        /// 收到文本消息回调
        /// </summary>
        event Action<string, string> OnTextMessage;

        /// <summary>
        /// 收到二进制消息回调
        /// </summary>
        event Action<string, byte[]> OnBinaryMessage;

        /// <summary>
        /// 连接错误回调
        /// </summary>
        event Action<string, string> OnError;

        #endregion

        #region HTTP 请求

        /// <summary>
        /// 发送HTTP GET请求
        /// </summary>
        UniTask<HttpResponse> GetAsync(string url, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送HTTP POST请求
        /// </summary>
        UniTask<HttpResponse> PostAsync(string url, byte[] data, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送HTTP POST请求（JSON格式）
        /// </summary>
        UniTask<HttpResponse> PostJsonAsync(string url, string jsonData, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送HTTP PUT请求
        /// </summary>
        UniTask<HttpResponse> PutAsync(string url, byte[] data, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送HTTP DELETE请求
        /// </summary>
        UniTask<HttpResponse> DeleteAsync(string url, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default);

        #endregion

        #region 配置与统计

        /// <summary>
        /// 配置HTTP设置
        /// </summary>
        void ConfigureHttp(HttpConfig config);

        /// <summary>
        /// 获取网络统计信息
        /// </summary>
        NetworkStatistics GetStatistics();

        /// <summary>
        /// 重置统计信息
        /// </summary>
        void ResetStatistics();

        #endregion
    }
}
