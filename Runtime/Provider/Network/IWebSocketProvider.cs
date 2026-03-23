using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Data.Network;

namespace JulyCore.Provider.Network
{
    /// <summary>
    /// WebSocket 提供者接口
    /// </summary>
    public interface IWebSocketProvider : Core.IProvider
    {
        #region 连接管理

        NetworkConnectionState ConnectionState { get; }
        bool IsConnected { get; }
        NetworkConnectionState GetConnectionState(string connectionName);
        bool IsConnectionConnected(string connectionName);

        UniTask<bool> ConnectAsync(string url,
            CancellationToken cancellationToken = default);
        UniTask<bool> ConnectAsync(WebSocketConfig config,
            CancellationToken cancellationToken = default);

        UniTask DisconnectAsync(CancellationToken cancellationToken = default);
        UniTask DisconnectAsync(string connectionName,
            CancellationToken cancellationToken = default);
        UniTask DisconnectAllAsync(CancellationToken cancellationToken = default);

        NetworkConnectionInfo GetConnectionInfo(string connectionName = null);
        IReadOnlyList<string> GetActiveConnectionNames();

        #endregion

        #region 消息发送

        bool SendText(string text);
        bool SendText(string connectionName, string text);
        bool SendBinary(byte[] data);
        bool SendBinary(string connectionName, byte[] data);
        bool Send(NetworkMessage message);
        bool Send(string connectionName, NetworkMessage message);
        UniTask<bool> SendAsync(NetworkMessage message,
            CancellationToken cancellationToken = default);
        UniTask<bool> SendAsync(string connectionName, NetworkMessage message,
            CancellationToken cancellationToken = default);

        #endregion

        #region 事件回调

        event Action<string> OnOpen;
        event Action<string, int, string> OnClose;
        event Action<string, string> OnTextMessage;
        event Action<string, byte[]> OnBinaryMessage;
        event Action<string, string> OnError;

        #endregion

        #region 统计

        NetworkStatistics GetStatistics();
        void ResetStatistics();

        #endregion
    }
}
