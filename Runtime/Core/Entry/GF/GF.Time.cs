using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Module.Time;

namespace JulyCore
{
    public static partial class GF
    {
        /// <summary>
        /// 时间相关操作
        /// </summary>
        public static class Time
        {
            private static TimeModule _module;
            private static TimeModule Module
            {
                get
                {
                    _module ??= GetModule<TimeModule>();
                    return _module;
                }
            }

            #region Properties

            /// <summary>
            /// 当前游戏时间（受 TimeScale 影响）
            /// </summary>
            public static float GameTime => Module.GameTime;

            /// <summary>
            /// 当前真实时间（不受 TimeScale 影响）
            /// </summary>
            public static float RealTime => Module.RealTime;

            /// <summary>
            /// 当前帧的 DeltaTime
            /// </summary>
            public static float DeltaTime => Module.DeltaTime;

            /// <summary>
            /// 当前帧的 UnscaledDeltaTime
            /// </summary>
            public static float UnscaledDeltaTime => Module.UnscaledDeltaTime;

            /// <summary>
            /// 当前帧数
            /// </summary>
            public static int FrameCount => Module.FrameCount;

            /// <summary>
            /// 时间缩放因子
            /// </summary>
            public static float TimeScale
            {
                get => Module.TimeScale;
                set => Module.TimeScale = value;
            }

            /// <summary>
            /// 服务器时间（UTC）
            /// </summary>
            public static DateTime ServerTimeUtc => Module.ServerTimeUtc;
            
            /// <summary>
            /// 服务器 UTC 时间戳（秒）
            /// </summary>
            public static long ServerTimeUtcTimestamp => new DateTimeOffset(Module.ServerTimeUtc).ToUnixTimeSeconds();

            /// <summary>
            /// 服务器时间（本地时区）
            /// </summary>
            public static DateTime ServerTimeLocal => Module.ServerTimeLocal;

            /// <summary>
            /// 是否已同步服务器时间
            /// </summary>
            public static bool IsServerTimeSynced => Module.IsServerTimeSynced;

            /// <summary>
            /// 服务器时间偏移（秒）
            /// </summary>
            public static double ServerTimeOffset => Module.ServerTimeOffset;

            #endregion

            #region Server Time Sync

            /// <summary>
            /// 同步服务器时间
            /// </summary>
            /// <param name="serverTimeUtc">服务器UTC时间</param>
            public static void SyncServerTime(DateTime serverTimeUtc)
            {
                Module.SyncServerTime(serverTimeUtc);
            }

            /// <summary>
            /// 从网络同步服务器时间（使用NTP）
            /// </summary>
            /// <param name="ntpServer">NTP服务器地址（可选，默认使用内置服务器）</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>是否同步成功</returns>
            public static async UniTask<bool> SyncServerTimeFromNetworkAsync(string ntpServer = null, CancellationToken cancellationToken = default)
            {
                return await Module.SyncServerTimeFromNetworkAsync(ntpServer, cancellationToken);
            }

            #endregion

            #region Timer

            /// <summary>
            /// 注册一次性定时器
            /// </summary>
            /// <param name="delay">延迟时间（秒）</param>
            /// <param name="callback">回调函数</param>
            /// <param name="useRealTime">是否使用真实时间（不受 TimeScale 影响）</param>
            /// <returns>定时器ID，可用于取消</returns>
            public static int ScheduleOnce(float delay, Action callback, bool useRealTime = false)
            {
                return Module.ScheduleOnce(delay, callback, useRealTime);
            }

            /// <summary>
            /// 注册重复定时器
            /// </summary>
            /// <param name="interval">间隔时间（秒）</param>
            /// <param name="callback">回调函数</param>
            /// <param name="useRealTime">是否使用真实时间（不受 TimeScale 影响）</param>
            /// <param name="repeatCount">重复次数，-1表示无限重复</param>
            /// <returns>定时器ID，可用于取消</returns>
            public static int ScheduleRepeat(float interval, Action callback, bool useRealTime = false, int repeatCount = -1)
            {
                return Module.ScheduleRepeat(interval, callback, useRealTime, repeatCount);
            }

            /// <summary>
            /// 取消定时器
            /// </summary>
            /// <param name="timerId">定时器ID</param>
            /// <returns>是否取消成功</returns>
            public static bool CancelTimer(int timerId)
            {
                return Module.CancelTimer(timerId);
            }

            /// <summary>
            /// 取消所有定时器
            /// </summary>
            public static void CancelAllTimers()
            {
                Module.CancelAllTimers();
            }

            /// <summary>
            /// 暂停定时器
            /// </summary>
            /// <param name="timerId">定时器ID</param>
            /// <returns>是否暂停成功</returns>
            public static bool PauseTimer(int timerId)
            {
                return Module.PauseTimer(timerId);
            }

            /// <summary>
            /// 恢复定时器
            /// </summary>
            /// <param name="timerId">定时器ID</param>
            /// <returns>是否恢复成功</returns>
            public static bool ResumeTimer(int timerId)
            {
                return Module.ResumeTimer(timerId);
            }

            #endregion

            #region Formatting

            /// <summary>
            /// 格式化秒数为时间字符串
            /// </summary>
            /// <param name="seconds">秒数</param>
            /// <param name="format">格式（支持 HH, H, mm, m, ss, s, fff, ff, f）</param>
            /// <returns>格式化后的字符串</returns>
            public static string Format(float seconds, string format = null)
            {
                return Module.FormatTime(seconds, format);
            }

            /// <summary>
            /// 格式化时间跨度
            /// </summary>
            /// <param name="timeSpan">时间跨度</param>
            /// <param name="format">格式</param>
            /// <returns>格式化后的字符串</returns>
            public static string Format(TimeSpan timeSpan, string format = null)
            {
                return Module.FormatTimeSpan(timeSpan, format);
            }

            #endregion
        }
    }
}

