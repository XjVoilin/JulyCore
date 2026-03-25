using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using UnityEngine;

namespace JulyCore.Provider.Time
{
    /// <summary>
    /// Unity时间提供者实现
    /// 纯技术执行层：负责底层时间获取、定时器底层执行、NTP时间获取
    /// 不包含任何业务语义，不维护业务状态
    /// </summary>
    internal class UnityTimeProvider : ProviderBase, ITimeProvider
    {
        public override int Priority => Frameworkconst.PriorityTimeProvider;
        protected override LogChannel LogChannel => LogChannel.Time;

        private readonly Dictionary<int, TimerInfo> _timers = new();
        private readonly List<TimerInfo> _snapshot = new(16);
        private readonly List<int> _timersToRemove = new(8);
        private readonly object _timerLock = new();

        private bool _isServerTimeSynced;
        private double _serverTimeOffset;

        #region ITimeProvider Properties

        public float GameTime => UnityEngine.Time.time;
        public float RealTime => UnityEngine.Time.realtimeSinceStartup;
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;
        public int FrameCount => UnityEngine.Time.frameCount;

        public float TimeScale
        {
            get => UnityEngine.Time.timeScale;
            set => UnityEngine.Time.timeScale = Mathf.Clamp(value, 0f, 100f);
        }

        public DateTime ServerTimeUtc { get; }
        public DateTime ServerTimeLocal { get; }
        public bool IsServerTimeSynced { get; }

        #endregion

        #region Server Time (纯技术操作)

        public void SetServerTimeOffset(double offsetSeconds)
        {
            _serverTimeOffset = offsetSeconds;
        }

        public double GetServerTimeOffset()
        {
            return _serverTimeOffset;
        }

        public void SetServerTimeSynced(bool synced)
        {
            _isServerTimeSynced = synced;
        }

        public async UniTask<DateTime?> GetNtpTimeAsync(string ntpServer, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(ntpServer))
            {
                return null;
            }

            try
            {
                // NTP请求包
                var ntpData = new byte[48];
                ntpData[0] = 0x1B; // LI = 0, VN = 3, Mode = 3 (Client)

                var addresses = await Dns.GetHostAddressesAsync(ntpServer);
                if (addresses.Length == 0)
                {
                    return null;
                }

                var ipEndPoint = new IPEndPoint(addresses[0], 123);

                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.ReceiveTimeout = 3000;
                    socket.SendTimeout = 3000;

                    await socket.ConnectAsync(ipEndPoint);
                    await socket.SendAsync(new ArraySegment<byte>(ntpData), SocketFlags.None);

                    var buffer = new byte[48];
                    await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                    // 解析NTP响应
                    // 从第40字节开始是传输时间戳
                    ulong intPart = (ulong)buffer[40] << 24 | (ulong)buffer[41] << 16 |
                                    (ulong)buffer[42] << 8 | buffer[43];
                    ulong fractPart = (ulong)buffer[44] << 24 | (ulong)buffer[45] << 16 |
                                      (ulong)buffer[46] << 8 | buffer[47];

                    var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                    var ntpTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddMilliseconds((long)milliseconds);

                    return ntpTime;
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[{Name}] NTP请求失败 ({ntpServer}): {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Timer System (纯技术操作)

        public void RegisterTimer(int timerId, float delay, Action callback, bool useRealTime = false)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (delay < 0) delay = 0;

            lock (_timerLock)
            {
                if (_timers.TryGetValue(timerId, out var old))
                    TimerInfo.Return(old);

                var timer = TimerInfo.Rent();
                timer.Id = timerId;
                timer.Interval = delay;
                timer.RemainingTime = delay;
                timer.Callback = callback;
                timer.UseRealTime = useRealTime;
                timer.IsRepeat = false;
                timer.RemainingRepeatCount = 1;
                _timers[timerId] = timer;
            }
        }

        public void RegisterRepeatTimer(int timerId, float interval, Action callback, bool useRealTime = false, int repeatCount = -1)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (interval <= 0) interval = 0.001f;

            lock (_timerLock)
            {
                if (_timers.TryGetValue(timerId, out var old))
                    TimerInfo.Return(old);

                var timer = TimerInfo.Rent();
                timer.Id = timerId;
                timer.Interval = interval;
                timer.RemainingTime = interval;
                timer.Callback = callback;
                timer.UseRealTime = useRealTime;
                timer.IsRepeat = true;
                timer.RemainingRepeatCount = repeatCount;
                _timers[timerId] = timer;
            }
        }

        public bool CancelTimer(int timerId)
        {
            lock (_timerLock)
            {
                if (_timers.TryGetValue(timerId, out var timer))
                {
                    timer.IsCancelled = true;
                    return true;
                }
                return false;
            }
        }

        public bool PauseTimer(int timerId)
        {
            lock (_timerLock)
            {
                if (_timers.TryGetValue(timerId, out var timer) && !timer.IsCancelled)
                {
                    timer.IsPaused = true;
                    return true;
                }
                return false;
            }
        }

        public bool ResumeTimer(int timerId)
        {
            lock (_timerLock)
            {
                if (_timers.TryGetValue(timerId, out var timer) && !timer.IsCancelled)
                {
                    timer.IsPaused = false;
                    return true;
                }
                return false;
            }
        }

        public void UpdateTimers(float deltaTime, float unscaledDeltaTime)
        {
            _snapshot.Clear();
            _timersToRemove.Clear();

            lock (_timerLock)
            {
                foreach (var kvp in _timers)
                    _snapshot.Add(kvp.Value);
            }

            for (int i = 0, count = _snapshot.Count; i < count; i++)
            {
                var timer = _snapshot[i];

                if (timer.IsCancelled)
                {
                    _timersToRemove.Add(timer.Id);
                    continue;
                }

                if (timer.IsPaused)
                    continue;

                var dt = timer.UseRealTime ? unscaledDeltaTime : deltaTime;
                timer.RemainingTime -= dt;

                if (timer.RemainingTime > 0)
                    continue;

                try
                {
                    timer.Callback?.Invoke();
                }
                catch (Exception ex)
                {
                    GF.LogException(ex);
                }

                if (timer.IsRepeat)
                {
                    if (timer.RemainingRepeatCount > 0)
                        timer.RemainingRepeatCount--;

                    if (timer.RemainingRepeatCount == 0)
                        _timersToRemove.Add(timer.Id);
                    else
                        timer.RemainingTime += timer.Interval;
                }
                else
                {
                    _timersToRemove.Add(timer.Id);
                }
            }

            if (_timersToRemove.Count > 0)
            {
                lock (_timerLock)
                {
                    for (int i = 0, count = _timersToRemove.Count; i < count; i++)
                    {
                        var id = _timersToRemove[i];
                        if (_timers.Remove(id, out var removed))
                        {
                            TimerInfo.Return(removed);
                        }
                    }
                }
            }
        }

        #endregion

        #region Lifecycle

        protected override void OnShutdown()
        {
            lock (_timerLock)
            {
                foreach (var kvp in _timers)
                    TimerInfo.Return(kvp.Value);
                _timers.Clear();
            }
            _snapshot.Clear();
            _timersToRemove.Clear();
            _isServerTimeSynced = false;
            _serverTimeOffset = 0;
        }

        #endregion
    }
}

