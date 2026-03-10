using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Module.Base;
using JulyCore.Provider.Time;

namespace JulyCore.Module.Time
{
    /// <summary>
    /// 时间管理模块
    /// 业务语义与流程调度层：决定定时器调度策略、时间格式化规则、服务器时间同步策略
    /// 管理时间状态和业务逻辑
    /// 不直接操作底层时间API，不负责NTP协议实现
    /// 实现 ITimeCapability 接口，供其他 Module 通过能力接口访问
    /// </summary>
    internal class TimeModule : ModuleBase, ITimeCapability
    {
        private ITimeProvider _timeProvider;

        /// <summary>
        /// 日志通道
        /// </summary>
        protected override LogChannel LogChannel => LogChannel.Time;

        // 业务状态：定时器ID生成器
        private int _nextTimerId = 1;

        // 业务状态：活跃的定时器ID列表（用于CancelAllTimers）
        private readonly HashSet<int> _activeTimerIds = new HashSet<int>();

        // 业务状态：服务器时间相关
        private bool _isServerTimeSynced = false;
        private double _serverTimeOffset = 0;

        // 默认NTP服务器列表（业务规则）
        private static readonly string[] DefaultNtpServers = new[]
        {
            "time.windows.com",
            "pool.ntp.org",
            "time.google.com",
            "time.apple.com"
        };

        public override int Priority => Frameworkconst.PriorityTimeModule;

        #region Properties

        /// <summary>
        /// 当前游戏时间（受 Time.timeScale 影响）
        /// </summary>
        internal float GameTime => _timeProvider?.GameTime ?? 0f;

        /// <summary>
        /// 当前真实时间（不受 Time.timeScale 影响）
        /// </summary>
        internal float RealTime => _timeProvider?.RealTime ?? 0f;

        /// <summary>
        /// 当前帧的 DeltaTime
        /// </summary>
        internal float DeltaTime => _timeProvider?.DeltaTime ?? 0f;

        /// <summary>
        /// 当前帧的 UnscaledDeltaTime
        /// </summary>
        internal float UnscaledDeltaTime => _timeProvider?.UnscaledDeltaTime ?? 0f;

        /// <summary>
        /// 当前帧数
        /// </summary>
        internal int FrameCount => _timeProvider?.FrameCount ?? 0;

        /// <summary>
        /// 时间缩放因子
        /// </summary>
        internal float TimeScale
        {
            get => _timeProvider?.TimeScale ?? 1f;
            set
            {
                if (_timeProvider != null)
                {
                    _timeProvider.TimeScale = value;
                }
            }
        }

        #endregion

        protected override UniTask OnInitAsync()
        {
            try
            {
                _timeProvider = GetProvider<ITimeProvider>();
                if (_timeProvider == null)
                {
                    throw new JulyException($"[{Name}] 未找到ITimeProvider，请先注册TimeProvider");
                }

                return base.OnInitAsync();
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 时间模块初始化失败: {ex.Message}");
                throw;
            }
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            // 更新定时器系统（技术层）
            _timeProvider?.UpdateTimers(elapseSeconds, realElapseSeconds);
        }

        #region Server Time (业务层)

        /// <summary>
        /// 服务器时间（UTC，业务状态）
        /// </summary>
        public DateTime ServerTimeUtc
        {
            get
            {
                if (_isServerTimeSynced)
                {
                    return DateTime.UtcNow.AddSeconds(_serverTimeOffset);
                }
                return DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 服务器时间戳
        /// </summary>
        public long ServerTimeSeconds {
            get
            {
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
                return (long)(ServerTimeUtc - epoch).TotalSeconds;
            }
        }

        /// <summary>
        /// 服务器时间（本地时区，业务状态）
        /// </summary>
        internal DateTime ServerTimeLocal => ServerTimeUtc.ToLocalTime();

        /// <summary>
        /// 是否已同步服务器时间（业务状态）
        /// </summary>
        internal bool IsServerTimeSynced => _isServerTimeSynced;

        /// <summary>
        /// 服务器时间偏移（业务状态）
        /// </summary>
        internal double ServerTimeOffset => _serverTimeOffset;

        /// <summary>
        /// 同步服务器时间（业务规则：计算偏移并更新状态）
        /// </summary>
        /// <param name="serverTimeUtc">服务器UTC时间</param>
        internal void SyncServerTime(DateTime serverTimeUtc)
        {
            EnsureProvider();

            var localUtcNow = DateTime.UtcNow;
            _serverTimeOffset = (serverTimeUtc - localUtcNow).TotalSeconds;
            _isServerTimeSynced = true;

            // 更新Provider的技术层状态
            _timeProvider.SetServerTimeOffset(_serverTimeOffset);
            _timeProvider.SetServerTimeSynced(true);
        }

        /// <summary>
        /// 从网络同步服务器时间（业务规则：尝试多个NTP服务器）
        /// </summary>
        /// <param name="ntpServer">NTP服务器地址（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否同步成功</returns>
        internal async UniTask<bool> SyncServerTimeFromNetworkAsync(string ntpServer = null, CancellationToken cancellationToken = default)
        {
            EnsureProvider();

            var servers = string.IsNullOrEmpty(ntpServer) ? DefaultNtpServers : new[] { ntpServer };

            foreach (var server in servers)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                try
                {
                    // 通过Provider获取NTP时间（技术层）
                    var ntpTime = await _timeProvider.GetNtpTimeAsync(server, cancellationToken);
                    if (ntpTime.HasValue)
                    {
                        // 业务规则：同步时间
                        SyncServerTime(ntpTime.Value);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[{Name}] 从NTP服务器 {server} 同步时间失败: {ex.Message}");
                }
            }

            LogError($"[{Name}] 所有NTP服务器同步失败");
            return false;
        }

        #endregion

        #region Timer (业务层)

        /// <summary>
        /// 注册一次性定时器（业务规则：分配ID）
        /// </summary>
        /// <param name="delay">延迟时间（秒）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <returns>定时器ID</returns>
        internal int ScheduleOnce(float delay, Action callback, bool useRealTime = false)
        {
            EnsureProvider();

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            // 业务规则：分配定时器ID
            var timerId = _nextTimerId++;

            // 记录活跃的定时器ID
            _activeTimerIds.Add(timerId);

            // 通过Provider注册定时器（技术层）
            _timeProvider.RegisterTimer(timerId, delay, callback, useRealTime);

            return timerId;
        }

        /// <summary>
        /// 注册重复定时器（业务规则：分配ID）
        /// </summary>
        /// <param name="interval">间隔时间（秒）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="useRealTime">是否使用真实时间</param>
        /// <param name="repeatCount">重复次数，-1表示无限</param>
        /// <returns>定时器ID</returns>
        internal int ScheduleRepeat(float interval, Action callback, bool useRealTime = false, int repeatCount = -1)
        {
            EnsureProvider();

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            // 业务规则：分配定时器ID
            var timerId = _nextTimerId++;

            // 记录活跃的定时器ID
            _activeTimerIds.Add(timerId);

            // 通过Provider注册重复定时器（技术层）
            _timeProvider.RegisterRepeatTimer(timerId, interval, callback, useRealTime, repeatCount);

            return timerId;
        }

        /// <summary>
        /// 取消定时器
        /// </summary>
        /// <param name="timerId">定时器ID</param>
        /// <returns>是否取消成功</returns>
        internal bool CancelTimer(int timerId)
        {
            EnsureProvider();

            var result = _timeProvider.CancelTimer(timerId);
            if (result)
            {
                // 从活跃列表中移除
                _activeTimerIds.Remove(timerId);
            }

            return result;
        }

        /// <summary>
        /// 取消所有定时器（业务规则：遍历所有活跃定时器）
        /// </summary>
        internal void CancelAllTimers()
        {
            EnsureProvider();

            // 复制列表以避免在遍历时修改集合
            var timerIds = new List<int>(_activeTimerIds);
            foreach (var timerId in timerIds)
            {
                _timeProvider.CancelTimer(timerId);
            }

            // 清空活跃列表
            _activeTimerIds.Clear();
        }

        /// <summary>
        /// 暂停定时器
        /// </summary>
        /// <param name="timerId">定时器ID</param>
        /// <returns>是否暂停成功</returns>
        internal bool PauseTimer(int timerId)
        {
            EnsureProvider();
            return _timeProvider.PauseTimer(timerId);
        }

        /// <summary>
        /// 恢复定时器
        /// </summary>
        /// <param name="timerId">定时器ID</param>
        /// <returns>是否恢复成功</returns>
        internal bool ResumeTimer(int timerId)
        {
            EnsureProvider();
            return _timeProvider.ResumeTimer(timerId);
        }

        #endregion

        #region Formatting (业务规则)

        /// <summary>
        /// 格式化时间（业务规则：时间格式化策略）
        /// </summary>
        /// <param name="seconds">秒数</param>
        /// <param name="format">格式</param>
        /// <returns>格式化后的字符串</returns>
        internal string FormatTime(float seconds, string format = null)
        {
            if (seconds < 0) seconds = 0;

            var timeSpan = TimeSpan.FromSeconds(seconds);
            return FormatTimeSpan(timeSpan, format);
        }

        /// <summary>
        /// 格式化时间跨度（业务规则：时间格式化策略）
        /// </summary>
        /// <param name="timeSpan">时间跨度</param>
        /// <param name="format">格式</param>
        /// <returns>格式化后的字符串</returns>
        internal string FormatTimeSpan(TimeSpan timeSpan, string format = null)
        {
            if (string.IsNullOrEmpty(format))
            {
                // 业务规则：默认格式化策略
                if (timeSpan.TotalHours >= 1)
                {
                    return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                }

                if (timeSpan.TotalMinutes >= 1)
                {
                    return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                }

                return $"{timeSpan.Seconds:D2}";
            }

            // 业务规则：自定义格式解析
            return format
                .Replace("HH", ((int)timeSpan.TotalHours).ToString("D2"))
                .Replace("H", ((int)timeSpan.TotalHours).ToString())
                .Replace("mm", timeSpan.Minutes.ToString("D2"))
                .Replace("m", timeSpan.Minutes.ToString())
                .Replace("ss", timeSpan.Seconds.ToString("D2"))
                .Replace("s", timeSpan.Seconds.ToString())
                .Replace("fff", timeSpan.Milliseconds.ToString("D3"))
                .Replace("ff", (timeSpan.Milliseconds / 10).ToString("D2"))
                .Replace("f", (timeSpan.Milliseconds / 100).ToString());
        }

        #endregion

        protected override UniTask OnShutdownAsync()
        {
            // 取消所有定时器
            CancelAllTimers();

            // 清理业务状态
            _isServerTimeSynced = false;
            _serverTimeOffset = 0;
            _nextTimerId = 1;
            _activeTimerIds.Clear();

            return base.OnShutdownAsync();
        }

        private void EnsureProvider()
        {
            if (_timeProvider == null)
            {
                throw new InvalidOperationException($"[{Name}] TimeProvider未初始化");
            }
        }

        #region ITimeCapability 显式实现

        int ITimeCapability.ScheduleOnce(float delay, Action callback, bool useRealTime) => ScheduleOnce(delay, callback, useRealTime);
        int ITimeCapability.ScheduleRepeat(float interval, Action callback, bool useRealTime, int repeatCount) => ScheduleRepeat(interval, callback, useRealTime, repeatCount);
        bool ITimeCapability.CancelTimer(int timerId) => CancelTimer(timerId);

        #endregion
    }
}

