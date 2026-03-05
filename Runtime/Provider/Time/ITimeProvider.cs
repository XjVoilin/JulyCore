using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;

namespace JulyCore.Provider.Time
{
    /// <summary>
    /// 时间提供者接口
    /// 纯技术执行层：负责底层时间获取、定时器底层执行
    /// 不包含任何业务语义，不维护业务状态
    /// 所有业务逻辑（定时器管理、时间格式化、服务器时间同步策略）由Module层处理
    /// </summary>
    public interface ITimeProvider : IProvider
    {
        /// <summary>
        /// 当前游戏时间（受 Time.timeScale 影响）
        /// </summary>
        float GameTime { get; }

        /// <summary>
        /// 当前真实时间（不受 Time.timeScale 影响）
        /// </summary>
        float RealTime { get; }

        /// <summary>
        /// 当前帧的 DeltaTime（受 Time.timeScale 影响）
        /// </summary>
        float DeltaTime { get; }

        /// <summary>
        /// 当前帧的 UnscaledDeltaTime（不受 Time.timeScale 影响）
        /// </summary>
        float UnscaledDeltaTime { get; }

        /// <summary>
        /// 当前帧数
        /// </summary>
        int FrameCount { get; }

        /// <summary>
        /// 时间缩放因子
        /// </summary>
        float TimeScale { get; set; }

        /// <summary>
        /// 服务器时间（UTC）
        /// 如果未同步则返回本地时间
        /// </summary>
        DateTime ServerTimeUtc { get; }

        /// <summary>
        /// 服务器时间（本地时区）
        /// </summary>
        DateTime ServerTimeLocal { get; }

        /// <summary>
        /// 是否已同步服务器时间
        /// </summary>
        bool IsServerTimeSynced { get; }

        /// <summary>
        /// 设置服务器时间偏移（纯技术操作）
        /// </summary>
        /// <param name="offsetSeconds">偏移量（秒）</param>
        void SetServerTimeOffset(double offsetSeconds);

        /// <summary>
        /// 获取服务器时间偏移（纯技术查询）
        /// </summary>
        double GetServerTimeOffset();

        /// <summary>
        /// 设置服务器时间同步状态（纯技术操作）
        /// </summary>
        /// <param name="synced">是否已同步</param>
        void SetServerTimeSynced(bool synced);

        /// <summary>
        /// 从NTP服务器获取时间（纯技术操作）
        /// </summary>
        /// <param name="ntpServer">NTP服务器地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>NTP时间，如果失败则返回null</returns>
        UniTask<DateTime?> GetNtpTimeAsync(string ntpServer, CancellationToken cancellationToken = default);

        /// <summary>
        /// 注册定时器（纯技术操作，由Module层管理定时器ID和业务规则）
        /// </summary>
        /// <param name="timerId">定时器ID（由Module层分配）</param>
        /// <param name="delay">延迟时间（秒）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        void RegisterTimer(int timerId, float delay, Action callback, bool useRealTime = false);

        /// <summary>
        /// 注册重复定时器（纯技术操作）
        /// </summary>
        /// <param name="timerId">定时器ID（由Module层分配）</param>
        /// <param name="interval">间隔时间（秒）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <param name="repeatCount">重复次数，-1表示无限重复</param>
        void RegisterRepeatTimer(int timerId, float interval, Action callback, bool useRealTime = false, int repeatCount = -1);

        /// <summary>
        /// 取消定时器（纯技术操作）
        /// </summary>
        /// <param name="timerId">定时器ID</param>
        /// <returns>是否取消成功</returns>
        bool CancelTimer(int timerId);

        /// <summary>
        /// 暂停定时器（纯技术操作）
        /// </summary>
        /// <param name="timerId">定时器ID</param>
        /// <returns>是否暂停成功</returns>
        bool PauseTimer(int timerId);

        /// <summary>
        /// 恢复定时器（纯技术操作）
        /// </summary>
        /// <param name="timerId">定时器ID</param>
        /// <returns>是否恢复成功</returns>
        bool ResumeTimer(int timerId);

        /// <summary>
        /// 更新定时器（每帧调用，纯技术操作）
        /// </summary>
        /// <param name="deltaTime">游戏时间流逝</param>
        /// <param name="unscaledDeltaTime">真实时间流逝</param>
        void UpdateTimers(float deltaTime, float unscaledDeltaTime);
    }
}

